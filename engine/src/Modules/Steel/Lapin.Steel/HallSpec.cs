using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Steel;

/// <summary>
/// What a portal frame hall is (decision 270), kept on its <c>LapinSteelHall</c>: where it stands (<see cref="X"/>,
/// <see cref="Y"/>: axes A and 1 crossing; <see cref="Angle"/>: the span's way in plan, degrees), its <see cref="Span"/>
/// (column axis to column axis, along x), its <see cref="Bays"/> (frame to frame, along y), its <see cref="Eaves"/> height
/// (the rafter's top over the column's outer face, from the storey's level), its <see cref="Pitch"/>, a duo-pitch or a
/// mono-pitch <see cref="Roof"/>; the frames' column and rafter profiles; the haunches at the eaves and at the apex; the
/// gable frames (a regular frame, or gable posts every <see cref="PostSpacing"/> under a simple rafter); the column bases
/// (pinned or fixed) and their <see cref="Anchor"/>s (straight, hooked, with an anchor plate); whether its frames get
/// their connections (<see cref="Nodes"/>, decision 273) and their parameters by kind (<see cref="NodeSet"/>: base, eaves,
/// apex, post; what is not given chosen by the profiles); and the secondary steel to come (stage 4): purlins and side
/// rails at their spacing, the bays braced, an eaves beam — kept, not built yet.
/// </summary>
public sealed record HallSpec(
    double X, double Y, double Angle,
    double Span, IReadOnlyList<double> Bays, double Eaves, double Pitch, string Roof,
    Section Column, Section Rafter, Haunch? EavesHaunch, Haunch? ApexHaunch,
    string Gable, double PostSpacing, Section Post, Section GableRafter,
    string Base, double PurlinSpacing, double RailSpacing, IReadOnlyList<int> Bracing, bool EavesBeam,
    bool Nodes = true, string Anchor = "straight", IReadOnlyDictionary<string, NodeParams>? NodeSet = null)
{
    /// <summary>The kinds of connection a hall sets parameters for.</summary>
    public static readonly string[] NodeKinds = ["base", "eaves", "apex", "post"];

    /// <summary>The parameters the hall gives its connections of a kind (none: all by the profiles).</summary>
    public NodeParams NodeParamsOf(string kind) => NodeSet != null && NodeSet.TryGetValue(kind, out var p) ? p : NodeParams.None;

    public static readonly string[] Roofs = ["duo", "mono"];
    public static readonly string[] Gables = ["frame", "posts"];
    public static readonly string[] Bases = ["pinned", "fixed"];

    /// <summary>The attributes a spec is made of (a style carries them all but where the hall stands).</summary>
    public static readonly string[] Keys =
    [
        "span", "bays", "eaves", "pitch", "roof", "column", "rafter", "eavesHaunch", "apexHaunch",
        "gable", "postSpacing", "post", "gableRafter", "base", "purlinSpacing", "railSpacing", "bracing", "eavesBeam",
        "nodes", "anchor", "nodeParams",
    ];

    public static readonly string[] Placement = ["x", "y", "angle"];

    public bool Mono => Roof == "mono";
    public double Length => Bays.Sum();
    public int Frames => Bays.Count + 1;

    /// <summary>Where each frame stands along the hall, from the first.</summary>
    public IReadOnlyList<double> Stations
    {
        get
        {
            var o = new List<double> { 0 };
            foreach (var b in Bays) o.Add(o[^1] + b);
            return o;
        }
    }

    /// <summary>A typical 20 m hall: six bays of 6 m, 6 m at the eaves, 10°, haunches cut from the rafter, gable posts.</summary>
    public static HallSpec Default => new(0, 0, 0, 20, [6, 6, 6, 6, 6], 6, 10, "duo",
        Section.Catalogue["IPE 400"], Section.Catalogue["IPE 360"], new Haunch(2, 0.36), new Haunch(1, 0.18),
        "posts", 5, Section.Catalogue["IPE 240"], Section.Catalogue["IPE 240"], "pinned", 1.5, 1.5, [1], false);

    static JsonNode SectionJson(Section s) => s.Name != null && Section.Catalogue.TryGetValue(s.Name, out var c) && c == s ? JsonValue.Create(s.Name)! : s.ToJson();

    public JsonObject ToJson()
    {
        var o = new JsonObject
        {
            ["x"] = X, ["y"] = Y, ["angle"] = Angle,
            ["span"] = Span, ["bays"] = new JsonArray([.. Bays.Select(b => (JsonNode)b)]), ["eaves"] = Eaves, ["pitch"] = Pitch, ["roof"] = Roof,
            ["column"] = SectionJson(Column), ["rafter"] = SectionJson(Rafter),
            ["gable"] = Gable, ["postSpacing"] = PostSpacing, ["post"] = SectionJson(Post), ["gableRafter"] = SectionJson(GableRafter),
            ["base"] = Base, ["purlinSpacing"] = PurlinSpacing, ["railSpacing"] = RailSpacing,
            ["bracing"] = new JsonArray([.. Bracing.Select(b => (JsonNode)b)]), ["eavesBeam"] = EavesBeam,
            ["nodes"] = Nodes, ["anchor"] = Anchor,
        };
        var set = new JsonObject();
        foreach (var k in NodeKinds) if (NodeParamsOf(k).ToJson() is { Count: > 0 } pj) set[k] = pj;
        o["nodeParams"] = set;
        o["eavesHaunch"] = EavesHaunch?.ToJson();
        o["apexHaunch"] = ApexHaunch?.ToJson();
        return o;
    }

    /// <summary>A spec from a hall's attributes (or a draft), what is missing taken from <paramref name="fallback"/> (else <see cref="Default"/>); unchecked.</summary>
    public static HallSpec From(JsonObject o, HallSpec? fallback = null)
    {
        var d = fallback ?? Default;
        double N(string k, double v) => Json.Number(o[k]) ?? v;
        string C(string k, string[] allowed, string v) => (string?)o[k] is { } s && allowed.Contains(s) ? s : v;
        Section S(string k, Section v) => o.ContainsKey(k) ? Section.From(o[k]) ?? v : v;
        Haunch? H(string k, Haunch? v) => o.ContainsKey(k) ? Haunch.From(o[k]) : v;
        var bays = o["bays"] is JsonArray ba ? ba.Select(x => Json.Number(x) ?? 0).ToList() : d.Bays.ToList();
        var bracing = o["bracing"] is JsonArray br ? br.Select(x => (int)(Json.Number(x) ?? 0)).Where(x => x > 0).Distinct().Order().ToList() : d.Bracing.ToList();
        var beam = o["eavesBeam"] is JsonValue bv && bv.TryGetValue<bool>(out var b) ? b : d.EavesBeam;
        var nodes = o["nodes"] is JsonValue nv && nv.TryGetValue<bool>(out var nb) ? nb : d.Nodes;
        var set = d.NodeSet;
        if (o["nodeParams"] is JsonObject np)
        {
            var next = new Dictionary<string, NodeParams>();
            foreach (var k in NodeKinds) if (np.ContainsKey(k)) next[k] = NodeParams.From(np[k]); else if (d.NodeSet?.TryGetValue(k, out var was) == true) next[k] = was;
            set = next;
        }
        return new HallSpec(N("x", d.X), N("y", d.Y), N("angle", d.Angle), N("span", d.Span), bays, N("eaves", d.Eaves), N("pitch", d.Pitch), C("roof", Roofs, d.Roof),
            S("column", d.Column), S("rafter", d.Rafter), H("eavesHaunch", d.EavesHaunch), H("apexHaunch", d.ApexHaunch),
            C("gable", Gables, d.Gable), N("postSpacing", d.PostSpacing), S("post", d.Post), S("gableRafter", d.GableRafter),
            C("base", Bases, d.Base), N("purlinSpacing", d.PurlinSpacing), N("railSpacing", d.RailSpacing), bracing, beam,
            nodes, C("anchor", NodeParams.AnchorKinds, d.Anchor), set);
    }

    /// <summary>The spec checked as a command takes it: sizes in range, steel profiles, haunches that fit their rafters.</summary>
    public HallSpec Check()
    {
        if (!(Span >= 3 && Span <= 120)) throw new CommandException("A hall's span is from 3 m to 120 m.");
        if (Bays.Count is < 1 or > 100 || Bays.Any(b => !(b >= 1 && b <= 30))) throw new CommandException("A hall has from 1 to 100 bays, each from 1 m to 30 m.");
        if (!(Eaves >= 1 && Eaves <= 40)) throw new CommandException("A hall's eaves are from 1 m to 40 m high.");
        if (!(Pitch >= (Mono ? 0 : 1) && Pitch <= 45)) throw new CommandException(Mono ? "A mono-pitch roof slopes from 0° to 45°." : "A duo-pitch roof slopes from 1° to 45°.");
        foreach (var (s, what) in new[] { (Column, "column"), (Rafter, "rafter"), (Post, "gable post"), (GableRafter, "gable rafter") })
            if (!s.IsProfile) throw new CommandException($"The hall's {what} needs a steel profile.");
        if (Gable == "posts" && !(PostSpacing >= 1)) throw new CommandException("Gable posts stand at least 1 m apart.");
        if (!(PurlinSpacing > 0 && RailSpacing > 0)) throw new CommandException("Purlins and side rails need a spacing.");
        // A rafter's haunches end short of each other.
        var run = (Mono ? Span : Span / 2) / Math.Cos(Pitch * Math.PI / 180) - Column.Depth / 2;
        var haunches = (EavesHaunch?.Length ?? 0) * (Mono ? 2 : 1) + (Mono ? 0 : ApexHaunch?.Length ?? 0);
        if (haunches >= run - 0.1) throw new CommandException("The haunches are longer than the rafter they are under.");
        foreach (var h in new[] { EavesHaunch, ApexHaunch })
            if (h != null && h.Depth > 3 * Rafter.Depth + 0.5) throw new CommandException("A haunch is deeper than three times its rafter.");
        if (Bracing.Any(b => b > Bays.Count)) throw new CommandException("A braced bay is one of the hall's bays.");
        // Each kind's parameters checked as a connection's would be.
        foreach (var k in NodeKinds) NodeParams.Patch(NodeParams.None, NodeParamsOf(k).ToJson());
        return this;
    }

    // ------------------------------------------------------------ the frame's line

    double Tan => Math.Tan(Pitch * Math.PI / 180);

    /// <summary>
    /// The rafters' top at <paramref name="x"/> across the frame (from axis A), over the storey's level: the eaves height over
    /// the column's outer face, rising at the pitch to the apex at mid-span (duo-pitch) or to the far side (mono-pitch).
    /// </summary>
    public double Top(double x) => Eaves + ((Mono ? x : Math.Min(x, Span - x)) + Column.Depth / 2) * Tan;

    /// <summary>Where the rafters meet: mid-span (duo-pitch), or the high column's axis (mono-pitch).</summary>
    public double ApexX => Mono ? Span : Span / 2;

    /// <summary>The top of the roof's steel at its highest: the apex, or the high column's outer face.</summary>
    public double ApexHeight => Mono ? Top(Span) + Column.Depth / 2 * Tan : Top(Span / 2);

    /// <summary>Where the gable posts stand across the span: as many equal parts as the spacing asks, at least two.</summary>
    public IReadOnlyList<double> PostStations
    {
        get
        {
            var n = Math.Max(2, (int)Math.Ceiling(Span / PostSpacing - 1e-9));
            return Enumerable.Range(1, n - 1).Select(k => Span * k / n).ToList();
        }
    }
}
