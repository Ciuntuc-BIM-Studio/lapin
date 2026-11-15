using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Coordination;

namespace Lapin.Engine.Tests;

/// <summary>
/// Audit 15.11.2026, P4: one MEP element or host the boolean library cannot take (an open mesh, as manifold refuses one)
/// used to throw out of the whole openings check. The pair is reported as not checked now and the check goes on.
/// </summary>
public class OpeningClashOpenMeshTests
{
    /// <summary>The session's reader with the boolean library refusing every solid of one element, as manifold refuses an open mesh.</summary>
    sealed class Refusing(IQueryContext inner, string element) : IQueryContext, IServices, ISolidBooleans
    {
        readonly ConditionalWeakTable<Solid, object> refused = [];
        ISolidBooleans Real => inner.Services.Get<ISolidBooleans>()!;
        public IModelView Model => inner.Model;
        public IServices Services => this;
        public ElementGeometry? Geometry(string entityId)
        {
            var g = inner.Geometry(entityId);
            if (entityId == element && g != null) foreach (var p in g.Parts) refused.AddOrUpdate(p.Solid, this);
            return g;
        }
        void Check(Solid s) { if (refused.TryGetValue(s, out _)) throw new InvalidOperationException("manifold: error 1 (the solid is not closed and orientable)."); }
        public Solid? Difference(Solid a, IReadOnlyList<Solid> cutters, string? tag = null) { Check(a); return Real.Difference(a, cutters, tag); }
        public Solid? Union(IReadOnlyList<Solid> solids, string? tag = null) => Real.Union(solids, tag);
        public Solid? Intersection(Solid a, Solid b, string? tag = null) { Check(a); Check(b); return Real.Intersection(a, b, tag); }
        public T? Get<T>() where T : class => typeof(T) == typeof(ISolidBooleans) ? this as T : inner.Services.Get<T>();
        public IReadOnlyList<T> All<T>() where T : class => typeof(T) == typeof(ISolidBooleans) ? [(this as T)!] : inner.Services.All<T>();
    }

    static string Pipe(MepFixture f, double x)
    {
        var system = f.System(defaults: new JsonObject { ["insulation"] = 0 });
        var run = f.Draw(system, MepFixture.Pts((x, -1), (x, 3)), more: new JsonObject { ["pair"] = false });
        f.Session.Edit("set", e =>
        {
            var p = e.Model.Get(run)!;
            var a = p.CloneAttrs();
            a["size"] = 0.16; a["elevation"] = 1.0;
            e.Replace(p.With(attrs: a));
        });
        return run;
    }

    [Fact]
    public void A_pipe_the_boolean_library_refuses_is_reported_not_checked_and_the_other_pipe_is_still_checked()
    {
        var f = new MepFixture();
        var wall = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = -2, ["y1"] = 1, ["x2"] = 8, ["y2"] = 1, ["join"] = false })!["id"]!;
        var open = Pipe(f, 0);
        var good = Pipe(f, 4);
        var found = OpeningClashes.Run(new Refusing(f.Session.Reader, open), new OpeningClashes.Options());
        var skipped = Assert.Single(found, c => c.MepId == open);
        Assert.Equal(OpeningClashes.Unchecked, skipped.Status);
        Assert.Equal(wall, skipped.HostId);
        Assert.False(skipped.IsClash);
        Assert.Contains("could not be checked", skipped.Message);
        // The other pipe through the wall with no opening: found as before.
        var missing = Assert.Single(found, c => c.MepId == good);
        Assert.Equal(OpeningClashes.Missing, missing.Status);
    }

    [Fact]
    public void The_report_lists_the_pairs_it_could_not_check_none_when_all_were_checked()
    {
        var f = new MepFixture();
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = -2, ["y1"] = 1, ["x2"] = 8, ["y2"] = 1, ["join"] = false });
        Pipe(f, 0);
        var first = (JsonObject)f.Run("coordination.runCheck", new JsonObject())!;
        Assert.Single(first["items"]!.AsArray());
        Assert.Empty(first["unchecked"]!.AsArray());
    }
}
