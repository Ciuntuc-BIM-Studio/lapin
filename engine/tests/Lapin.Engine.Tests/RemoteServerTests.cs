using System.Text.Json.Nodes;
using Lapin.Host;
using Lapin.Package;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// The client against a running Atrium Server (docs/remote.md, "Against Atrium Server on 127.0.0.1"): run only when
/// <c>LAPIN_REMOTE_SERVER</c> names it, with <c>LAPIN_REMOTE_PROJECT</c> (a project with no commit yet, Mihai its owner, Ana an
/// editor), <c>LAPIN_REMOTE_MIHAI</c>, <c>LAPIN_REMOTE_ANA</c> (their e-mails) and <c>LAPIN_REMOTE_PASSWORD</c>; passes at once without them.
/// </summary>
public sealed class RemoteServerTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-server-").FullName;
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    sealed class Client(Fixture f)
    {
        public Fixture F { get; } = f;
        public EngineDispatcher D { get; } = new(f.Session, PackageFiles.Create());
        public JsonObject Auth { get; set; } = [];
        public JsonNode? Call(string method, JsonObject? p = null)
        {
            p ??= [];
            foreach (var (k, v) in Auth) p[k] = v?.DeepClone();
            var (reply, _) = D.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString());
            var o = (JsonObject)JsonNode.Parse(BinaryFrame.HeaderText(reply))!;
            if (o["error"] is { } err) throw new InvalidOperationException((string)err!);
            if (o["result"]?["tokens"] is JsonObject t) { Auth["token"] = t["accessToken"]?.DeepClone(); Auth["refreshToken"] = t["refreshToken"]?.DeepClone(); }
            return o["result"];
        }

        public void SignIn(string server, string user, string password, string name)
        {
            var t = Call("remote.signIn", new JsonObject { ["url"] = server, ["username"] = user, ["password"] = password })!;
            Auth = new JsonObject
            {
                ["token"] = t["accessToken"]?.DeepClone(), ["refreshToken"] = t["refreshToken"]?.DeepClone(),
                ["author"] = t["user"]?["id"] is { } id ? new JsonObject { ["id"] = id.DeepClone(), ["name"] = name } : new JsonObject { ["id"] = "local:" + Lapin.Model.Hashes.Sha256Hex(user)[..16], ["name"] = name },
            };
        }
    }

    [Fact]
    public void Push_clone_conflict_and_pull_against_a_running_server()
    {
        var server = Environment.GetEnvironmentVariable("LAPIN_REMOTE_SERVER");
        if (string.IsNullOrEmpty(server)) return;
        var project = Environment.GetEnvironmentVariable("LAPIN_REMOTE_PROJECT")!;
        var password = Environment.GetEnvironmentVariable("LAPIN_REMOTE_PASSWORD")!;

        var f = new Fixture();
        var type = f.Type("Brick 37", ("Plaster", 0.02, false), ("Brick", 0.25, true));
        f.Wall(0, 0, 6, 0, type);
        f.Wall(0, 3, 6, 3, type);
        var mihai = new Client(f);
        mihai.SignIn(server, Environment.GetEnvironmentVariable("LAPIN_REMOTE_MIHAI")!, password, "Mihai Ionescu");
        mihai.Call("project.save", new JsonObject { ["path"] = Path.Combine(_dir, "mihai.lapin") });
        mihai.Call("history.commit", new JsonObject { ["message"] = "Initial model" });
        mihai.Call("remote.add", new JsonObject { ["kind"] = "server", ["url"] = server, ["project"] = project });
        var first = mihai.Call("remote.push")!;
        Assert.True((bool)first["pushed"]!, first.ToJsonString());

        var ana = new Client(new Fixture());
        ana.SignIn(server, Environment.GetEnvironmentVariable("LAPIN_REMOTE_ANA")!, password, "Ana Popescu");
        ana.Call("remote.clone", new JsonObject { ["kind"] = "server", ["url"] = server, ["project"] = project, ["path"] = Path.Combine(_dir, "ana.lapin") });
        Assert.Equal(LapinPackage.Read(Path.Combine(_dir, "mihai.lapin")).Manifest.Parts.Select(p => p.Sha256), LapinPackage.Read(Path.Combine(_dir, "ana.lapin")).Manifest.Parts.Select(p => p.Sha256));

        ana.F.Wall(10, 0, 10, 6);
        ana.Call("history.commit", new JsonObject { ["message"] = "Ana's wall" });
        var second = ana.Call("remote.push")!;
        Assert.True((bool)second["pushed"]!, second.ToJsonString());

        mihai.F.Wall(-4, 0, -4, 6);
        mihai.Call("history.commit", new JsonObject { ["message"] = "Mihai's wall" });
        var refused = mihai.Call("remote.push")!;
        Assert.False((bool)refused["pushed"]!);
        var kept = mihai.Call("remote.pull", new JsonObject { ["resolve"] = "branch" })!;
        Assert.True((bool)kept["pulled"]!, kept.ToJsonString());
        Assert.False(ModelCompare.Compare(mihai.F.Session.Model.Entities.ToList(), mihai.F.Session.Model.Relations.ToList(), ana.F.Session.Model.Entities.ToList(), ana.F.Session.Model.Relations.ToList()).Any);
    }

    /// <summary>
    /// The account's password changed through the engine and changed back, against a running Atrium Server: only with
    /// <c>LAPIN_REMOTE_PASSWORD_ACCOUNT</c> (a throwaway account of a test server, never a real one) and <c>LAPIN_REMOTE_PASSWORD</c>.
    /// </summary>
    [Fact]
    public void Password_changed_and_back_against_a_running_server()
    {
        var server = Environment.GetEnvironmentVariable("LAPIN_REMOTE_SERVER");
        var account = Environment.GetEnvironmentVariable("LAPIN_REMOTE_PASSWORD_ACCOUNT");
        if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(account)) return;
        var password = Environment.GetEnvironmentVariable("LAPIN_REMOTE_PASSWORD")!;
        const string Other = "changed-by-the-engine-tests";

        var me = new Client(new Fixture());
        me.SignIn(server, account, password, "Test");
        var wrong = me.Call("remote.changePassword", new JsonObject { ["url"] = server, ["currentPassword"] = "not-the-password", ["newPassword"] = Other })!;
        Assert.False((bool)wrong["changed"]!);
        Assert.Equal("wrong_password", (string?)wrong["code"]);

        var old = me.Auth["token"]!.GetValue<string>();
        var changed = me.Call("remote.changePassword", new JsonObject { ["url"] = server, ["currentPassword"] = password, ["newPassword"] = Other })!;
        Assert.True((bool)changed["changed"]!, changed.ToJsonString());
        Assert.NotEqual(old, me.Auth["token"]!.GetValue<string>());
        Assert.Throws<InvalidOperationException>(() => new Client(new Fixture()).SignIn(server, account, password, "Test"));

        var back = me.Call("remote.changePassword", new JsonObject { ["url"] = server, ["currentPassword"] = Other, ["newPassword"] = password })!;
        Assert.True((bool)back["changed"]!, back.ToJsonString());
        new Client(new Fixture()).SignIn(server, account, password, "Test");
    }
}
