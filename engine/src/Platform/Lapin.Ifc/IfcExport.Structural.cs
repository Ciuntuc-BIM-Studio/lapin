using Lapin.Geometry;
using Lapin.Platform;
using Lapin.Platform.Analysis;
using Lapin.Platform.ProfileLibraries;
using static Lapin.Ifc.Step;

namespace Lapin.Ifc;

public static partial class IfcExport
{
    /// <summary>
    /// IFC's structural analysis view (docs/plan-fem.md §8.7), written when the export asks for it (<see cref="IfcExportOptions.Structural"/>):
    /// the analytical model (<see cref="AnalyticalModel.Build"/>: every load-bearing wall, the feet fixed, as JG's export by default)
    /// as an IfcStructuralAnalysisModel (LOADING_3D) serving the building; its nodes IfcStructuralPointConnections on a vertex,
    /// the supports' IfcBoundaryNodeConditions; its bars IfcStructuralCurveMembers on an edge between their nodes, their section
    /// and material an IfcMaterialProfileSet; its shells IfcStructuralSurfaceMembers (SHELL) on a face bounded by their nodes'
    /// edges; each member connected to its nodes and assigned to its physical element. The load cases, their actions and the
    /// combinations come from the analysis module (<see cref="IStructuralLoads"/>): each case an IfcStructuralLoadCase, its actions
    /// planar, curve and point actions (constant, global, in N, N/m, N/m²) on their own topology, grouped in it and connected to
    /// the member under them when there is one; each combination an IfcStructuralLoadGroup (LOAD_COMBINATION) over its cases by
    /// factor. GlobalIds: a member's from its element's and its piece, a node's from its place, a derived load's from its source.
    /// </summary>
    sealed partial class Writer
    {
        string newton = "";

        /// <summary>The forces' units: N, N/m and N/m² (IFC 4.3's derived unit has a name besides).</summary>
        IEnumerable<string> StructuralUnits()
        {
            newton = f.Add("IFCSIUNIT", Derived, E("FORCEUNIT"), Null, E("NEWTON"));
            string Per(string type, int metres)
            {
                var elements = L(new[] { f.Add("IFCDERIVEDUNITELEMENT", newton, I(1)), f.Add("IFCDERIVEDUNITELEMENT", metre, I(metres)) });
                return o.Ifc43 ? f.Add("IFCDERIVEDUNIT", elements, E(type), Null, Null) : f.Add("IFCDERIVEDUNIT", elements, E(type), Null);
            }
            return [newton, Per("LINEARFORCEUNIT", -1), Per("PLANARFORCEUNIT", -2)];
        }

        string P3(Vec3 p) => f.Add("IFCCARTESIANPOINT", L(new[] { R(p.X), R(p.Y), R(p.Z) }));
        string Dir3(Vec3 d) => f.Add("IFCDIRECTION", L(new[] { R(d.X), R(d.Y), R(d.Z) }));

        void Structural()
        {
            var reader = s.Reader;
            var am = AnalyticalModel.Build(m, reader.Services, new AnalyticalOptions());
            var loads = s.Catalog.Get<IStructuralLoads>()?.For(reader);
            var reference = f.Add("IFCGEOMETRICREPRESENTATIONSUBCONTEXT", S("Reference"), S("Model"), Derived, Derived, Derived, Derived, model, Null, E("GRAPH_VIEW"), Null);
            var shared = Place(null, 0);
            string Shape(string type, string item) => f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null,
                L(new[] { f.Add("IFCTOPOLOGYREPRESENTATION", reference, S("Reference"), S(type), L(new[] { item })) }));
            var items = new List<string>();

            // The nodes: a vertex each, the supports' conditions.
            var vertexOf = new Dictionary<ANode, string>();
            var connectionOf = new Dictionary<ANode, string>();
            string Bool(bool b) => Typed("IFCBOOLEAN", b ? True : False);
            foreach (var n in am.Nodes)
            {
                var v = f.Add("IFCVERTEXPOINT", P3(n.At));
                vertexOf[n] = v;
                var cond = n.Restraints is { Length: 6 } r
                    ? f.Add("IFCBOUNDARYNODECONDITION", S(n.RestraintKind ?? "support"), Bool(r[0]), Bool(r[1]), Bool(r[2]), Bool(r[3]), Bool(r[4]), Bool(r[5]))
                    : Null;
                var key = FormattableString.Invariant($"structural-node:{Math.Round(n.At.X * 1000)},{Math.Round(n.At.Y * 1000)},{Math.Round(n.At.Z * 1000)}");
                var c = f.Add("IFCSTRUCTURALPOINTCONNECTION", Gid(project.Id, key), Null, S(n.Id), Null, Null, shared, Shape("Vertex", v), cond, Null);
                connectionOf[n] = c;
                items.Add(c);
            }
            // One edge between two nodes, whoever bounds by it.
            var edges = new Dictionary<(ANode, ANode), string>();
            (string Edge, bool Same) EdgeOf(ANode a, ANode b)
            {
                if (edges.TryGetValue((a, b), out var e)) return (e, true);
                if (edges.TryGetValue((b, a), out e)) return (e, false);
                return (edges[(a, b)] = f.Add("IFCEDGE", vertexOf[a], vertexOf[b]), true);
            }
            void Connect(string member, string memberGid, ANode n) =>
                f.Add("IFCRELCONNECTSSTRUCTURALMEMBER", Gid(memberGid, $"connects:{n.Id}"), Null, Null, Null, member, connectionOf[n], Null, Null, Null, Null);
            var membersOf = new Dictionary<string, List<string>>();
            void Own(string sourceId, string member) => (membersOf.TryGetValue(sourceId, out var l) ? l : membersOf[sourceId] = []).Add(member);

            // The bars: local 2 as the member's axis, its section and material as a profile set shared by the bars that have them.
            var frameRef = new Dictionary<AFrame, string>();
            var profileSets = new Dictionary<(string, string?), (string Set, List<string> Members)>();
            foreach (var fr in am.Frames)
            {
                var (pi, pj) = (fr.I.At + fr.OffI, fr.J.At + fr.OffJ);
                var u = (pj - pi).Normalized();
                var v0 = AnalyticalModel.Reference(u);
                var th = fr.RotationDeg * Math.PI / 180;
                var local2 = (v0 * Math.Cos(th) + Vec3.Cross(u, v0) * Math.Sin(th)).Normalized();
                var (edge, _) = EdgeOf(fr.I, fr.J);
                var gid = Gid(fr.Id, "structural-member");
                var member = f.Add("IFCSTRUCTURALCURVEMEMBER", gid, Null, S(fr.Pieces > 1 ? $"{fr.Source.Name} ({fr.Piece}/{fr.Pieces})" : fr.Source.Name), Null, Null, shared,
                    Shape("Edge", edge), E("RIGID_JOINED_MEMBER"), Dir3(local2));
                frameRef[fr] = member;
                items.Add(member);
                Connect(member, gid, fr.I);
                Connect(member, gid, fr.J);
                Own(fr.Source.SourceId, member);
                var key = (fr.Source.Section.Key, fr.Source.MaterialId);
                if (!profileSets.TryGetValue(key, out var ps))
                {
                    var profile = StructuralProfile(fr.Source.Section);
                    var mp = f.Add("IFCMATERIALPROFILE", S(fr.Source.Section.Name), Null, fr.Source.MaterialId is { } mat ? Material(mat) : Null, profile, Null, Null);
                    ps = profileSets[key] = (f.Add("IFCMATERIALPROFILESET", S(fr.Source.Section.Name), Null, L(new[] { mp }), Null), []);
                }
                ps.Members.Add(member);
            }
            foreach (var ((section, mat), (set, members)) in profileSets)
                f.Add("IFCRELASSOCIATESMATERIAL", Gid(project.Id, $"structural-profile:{section}:{mat}"), Null, Null, Null, L(members), set);

            // The shells: a face on its plane bounded by its nodes' edges, its core's thickness and material.
            var shellRef = new Dictionary<AShell, string>();
            var shellMaterials = new Dictionary<string, List<string>>();
            foreach (var sh in am.Shells)
            {
                var pts = sh.Nodes.Select(n => n.At).ToList();
                var normal = VecMath.NewellNormal(pts).Normalized();
                var refDir = (pts[1] - pts[0]).Normalized();
                var plane = f.Add("IFCPLANE", f.Add("IFCAXIS2PLACEMENT3D", P3(pts[0]), Dir3(normal), Dir3(refDir)));
                var loop = new List<string>();
                for (var i = 0; i < sh.Nodes.Count; i++)
                {
                    var (edge, same) = EdgeOf(sh.Nodes[i], sh.Nodes[(i + 1) % sh.Nodes.Count]);
                    loop.Add(f.Add("IFCORIENTEDEDGE", Derived, Derived, edge, same ? True : False));
                }
                var face = f.Add("IFCFACESURFACE", L(new[] { f.Add("IFCFACEOUTERBOUND", f.Add("IFCEDGELOOP", L(loop)), True) }), plane, True);
                var gid = Gid(sh.Id, "structural-member");
                var member = f.Add("IFCSTRUCTURALSURFACEMEMBER", gid, Null, S(sh.Pieces > 1 ? $"{sh.Source.Name} ({sh.Piece}/{sh.Pieces})" : sh.Source.Name), Null, Null, shared,
                    Shape("Face", face), E("SHELL"), R(sh.Source.Thickness));
                shellRef[sh] = member;
                items.Add(member);
                foreach (var n in sh.Nodes) Connect(member, gid, n);
                Own(sh.Source.SourceId, member);
                if (sh.Source.MaterialId is { } mat) (shellMaterials.TryGetValue(mat, out var l) ? l : shellMaterials[mat] = []).Add(member);
            }
            foreach (var (mat, members) in shellMaterials)
                f.Add("IFCRELASSOCIATESMATERIAL", Gid(project.Id, $"structural-shells:{mat}"), Null, Null, Null, L(members), Material(mat));
            // Each member assigned to its physical element (when the export wrote it).
            foreach (var (sourceId, members) in membersOf)
                if (written.TryGetValue(sourceId, out var product))
                    f.Add("IFCRELASSIGNSTOPRODUCT", Gid(sourceId, "structural-assigns"), Null, Null, Null, L(members), Null, product);

            // The cases, their actions, the combinations.
            var groups = new List<string>();
            var caseRef = new Dictionary<string, string>();
            var actions = 0;
            if (loads != null)
            {
                foreach (var c in loads.Cases)
                {
                    var r = f.Add("IFCSTRUCTURALLOADCASE", Gid(c.Id), Null, S(c.Code), S(c.Name), Null, E("LOAD_CASE"), E(c.ActionType), E(c.ActionSource), Null, Null,
                        c.SelfWeight ? L(new[] { R(0), R(0), R(-1) }) : Null);
                    caseRef[c.Id] = r;
                    groups.Add(r);
                }
                var inCase = new Dictionary<string, List<string>>();
                foreach (var a in loads.Actions.Where(a => caseRef.ContainsKey(a.CaseId) && a.Points.Count > 0))
                {
                    var gid = a.Derived ? Gid(a.SourceId ?? a.Id, $"structural-load:{a.Key}") : Gid(a.Id);
                    var force = a.Direction * (a.Value * 1000);
                    string action;
                    switch (a.Kind)
                    {
                        case "planar" when a.Points.Count >= 3:
                        {
                            var load = f.Add("IFCSTRUCTURALLOADPLANARFORCE", Null, R(force.X), R(force.Y), R(force.Z));
                            action = f.Add("IFCSTRUCTURALPLANARACTION", gid, Null, S(a.Name), Null, Null, shared, Shape("Face", ActionFace(a.Points, a.Holes)), load,
                                E("GLOBAL_COORDS"), Null, Null, E("CONST"));
                            break;
                        }
                        case "curve" when a.Points.Count >= 2:
                        {
                            var load = f.Add("IFCSTRUCTURALLOADLINEARFORCE", Null, R(force.X), R(force.Y), R(force.Z), Null, Null, Null);
                            var edge = f.Add("IFCEDGE", f.Add("IFCVERTEXPOINT", P3(a.Points[0])), f.Add("IFCVERTEXPOINT", P3(a.Points[1])));
                            action = f.Add("IFCSTRUCTURALCURVEACTION", gid, Null, S(a.Name), Null, Null, shared, Shape("Edge", edge), load,
                                E("GLOBAL_COORDS"), Null, Null, E("CONST"));
                            break;
                        }
                        case "point":
                        {
                            var load = f.Add("IFCSTRUCTURALLOADSINGLEFORCE", Null, R(force.X), R(force.Y), R(force.Z), Null, Null, Null);
                            action = f.Add("IFCSTRUCTURALPOINTACTION", gid, Null, S(a.Name), Null, Null, shared, Shape("Vertex", f.Add("IFCVERTEXPOINT", P3(a.Points[0]))), load,
                                E("GLOBAL_COORDS"), Null);
                            break;
                        }
                        default: continue;
                    }
                    actions++;
                    (inCase.TryGetValue(a.CaseId, out var l) ? l : inCase[a.CaseId] = []).Add(action);
                    if (Under(am, a, frameRef, shellRef) is { } under)
                        f.Add("IFCRELCONNECTSSTRUCTURALACTIVITY", Gid(gid, "structural-under"), Null, Null, Null, under, action);
                }
                foreach (var (caseId, list) in inCase)
                    f.Add("IFCRELASSIGNSTOGROUP", Gid(caseId, "structural-actions"), Null, Null, Null, L(list), Null, caseRef[caseId]);
                foreach (var cmb in loads.Combinations)
                {
                    var cgid = Gid(project.Id, $"structural-combination:{cmb.Key}");
                    var g = f.Add("IFCSTRUCTURALLOADGROUP", cgid, Null, S(cmb.Name), S(cmb.Key), Null, E("LOAD_COMBINATION"), E("NOTDEFINED"), E("NOTDEFINED"), Null, S(cmb.Kind));
                    groups.Add(g);
                    foreach (var (caseId, factor) in cmb.Factors)
                        if (caseRef.TryGetValue(caseId, out var cr))
                            f.Add("IFCRELASSIGNSTOGROUPBYFACTOR", Gid(cgid, $"factor:{caseId}"), Null, Null, Null, L(new[] { cr }), Null, g, R(factor));
                }
            }

            var analysis = f.Add("IFCSTRUCTURALANALYSISMODEL", Gid(project.Id, "structural-analysis-model"), Null, S("Structural analysis model"), Null, Null,
                E("LOADING_3D"), Null, groups.Count > 0 ? L(groups) : Null, Null, shared);
            if (items.Count > 0) f.Add("IFCRELASSIGNSTOGROUP", Gid(project.Id, "structural-items"), Null, Null, Null, L(items), Null, analysis);
            f.Add("IFCRELSERVICESBUILDINGS", Gid(project.Id, "structural-services"), Null, Null, Null, analysis, L(new[] { buildingRef }));

            Notes.Add($"Structural analysis view: {am.Nodes.Count} nodes, {am.Frames.Count} bars, {am.Shells.Count} shells" +
                (loads != null ? $", {loads.Cases.Count} load cases, {actions} actions, {loads.Combinations.Count} combinations." : "; without the structural analysis module, no loads."));
            if (am.Frames.Any(x => x.Eccentric)) Notes.Add("Structural analysis view: eccentric bar ends are written on their nodes, their offsets left out.");
            if (am.LeftOut.Count > 0) Notes.Add($"Structural analysis view: {am.LeftOut.Count} elements left out of the analytical model (as the JG Structures export says why).");
        }

        /// <summary>An action's face: its own vertices and edges, on its plane, its holes as inner bounds.</summary>
        string ActionFace(IReadOnlyList<Vec3> outline, IReadOnlyList<IReadOnlyList<Vec3>> holes)
        {
            string Loop(IReadOnlyList<Vec3> ring)
            {
                var vs = ring.Select(p => f.Add("IFCVERTEXPOINT", P3(p))).ToList();
                return f.Add("IFCEDGELOOP", L(vs.Select((v, i) => f.Add("IFCORIENTEDEDGE", Derived, Derived, f.Add("IFCEDGE", v, vs[(i + 1) % vs.Count]), True))));
            }
            var normal = VecMath.NewellNormal(outline.ToList()).Normalized();
            var plane = f.Add("IFCPLANE", f.Add("IFCAXIS2PLACEMENT3D", P3(outline[0]), Dir3(normal), Dir3((outline[1] - outline[0]).Normalized())));
            var bounds = new List<string> { f.Add("IFCFACEOUTERBOUND", Loop(outline), True) };
            bounds.AddRange(holes.Where(h => h.Count >= 3).Select(h => f.Add("IFCFACEBOUND", Loop(h), True)));
            return f.Add("IFCFACESURFACE", L(bounds), plane, True);
        }

        /// <summary>
        /// The member an action bears on: an area's or a line's, the slab under its middle (its own slab first: a floor's finishes),
        /// at most 1 m below it; a point's, the bar within 30 cm of it, else the slab under it. Null when there is none.
        /// </summary>
        static string? Under(AnalyticalModel am, StructuralActionData a, Dictionary<AFrame, string> frames, Dictionary<AShell, string> shells)
        {
            var at = a.Kind switch
            {
                "planar" => Middle(a.Points.Select(p => p.Xy).ToList()).At(a.Points[0].Z),
                "curve" => (a.Points[0] + a.Points[1]) * 0.5,
                _ => a.Points[0],
            };
            if (a.Kind == "point")
            {
                var near = frames.Keys.Select(fr => (Fr: fr, D: Distance(at, fr.I.At + fr.OffI, fr.J.At + fr.OffJ))).Where(x => x.D <= 0.3).OrderBy(x => x.D).FirstOrDefault();
                if (near.Fr != null) return frames[near.Fr];
            }
            var under = shells.Keys
                .Where(sh => sh.Source.Kind == "slab" && Polygon2.Contains(sh.Nodes.Select(n => n.At.Xy).ToList(), at.Xy))
                .Select(sh => (Sh: sh, Z: sh.Source.Z + Vec2.Dot(sh.Source.Gradient, at.Xy)))
                .Where(x => x.Z <= at.Z + 0.05 && at.Z - x.Z <= 1.0)
                .OrderByDescending(x => x.Sh.Source.SourceId == a.SourceId).ThenByDescending(x => x.Z).FirstOrDefault();
            if (under.Sh != null) return shells[under.Sh];
            // A floor's own load whose middle is off its pieces (its outline past its supports): the piece nearest to it.
            var own = shells.Keys.Where(sh => sh.Source.SourceId == a.SourceId)
                .OrderBy(sh => (Middle(sh.Nodes.Select(n => n.At.Xy).ToList()) - at.Xy).Length).FirstOrDefault();
            return own != null ? shells[own] : null;
        }

        /// <summary>An outline's centroid when it lies inside it, else a point just inside it.</summary>
        static Vec2 Middle(IReadOnlyList<Vec2> ring)
        {
            double a = 0, cx = 0, cy = 0;
            for (var i = 0; i < ring.Count; i++)
            {
                var (p, q) = (ring[i], ring[(i + 1) % ring.Count]);
                var c = Vec2.Cross(p, q);
                a += c; cx += (p.X + q.X) * c; cy += (p.Y + q.Y) * c;
            }
            if (Math.Abs(a) > 1e-12 && new Vec2(cx / (3 * a), cy / (3 * a)) is var g && Polygon2.Contains(ring, g)) return g;
            return Polygon2.InteriorPoint(ring);
        }

        static double Distance(Vec3 p, Vec3 a, Vec3 b)
        {
            var ab = b - a;
            var l2 = Vec3.Dot(ab, ab);
            var t = l2 > 0 ? Math.Clamp(Vec3.Dot(p - a, ab) / l2, 0, 1) : 0;
            return (a + ab * t - p).Length;
        }

        /// <summary>A bar's section as IFC writes a profile, metres: its parametric form when IFC has one, else its outline's rings.</summary>
        string StructuralProfile(FrameSection section)
        {
            var sh = section.Shape;
            var p = new SectionProfile
            {
                Name = section.Name, Kind = sh.Kind, H = sh.Kind == "circle" ? sh.Width : sh.Depth, B = sh.Width, Tw = sh.Web, Tf = sh.Flange, Lip = sh.Lip,
                R1 = sh.Root ?? 0, R2 = sh.Toe ?? 0, Slope = sh.Slope, B2 = sh.Width2, Tf2 = sh.Flange2, Rings = sh.Rings,
            };
            if (ProfileIfc.Parametric(f, p, 1) is { } parametric) return parametric;
            var rings = SectionProperties.RingsOf(sh) ?? [];
            var outsides = rings.Where(r => r.Count >= 3 && Polygon2.SignedArea(r) > 0).ToList();
            if (outsides.Count == 0)
                return f.Add("IFCRECTANGLEPROFILEDEF", E("AREA"), S(section.Name), Null, R(Math.Max(sh.Width, 1e-3)), R(Math.Max(sh.Depth, 1e-3)));
            var holes = rings.Where(r => r.Count >= 3 && Polygon2.SignedArea(r) < 0).ToList();
            string Line(IReadOnlyList<Vec2> r) => f.Add("IFCPOLYLINE", L(r.Concat(r.Take(1)).Select(P2)));
            string One(IReadOnlyList<Vec2> outside, string? name)
            {
                var inside = holes.Where(h => Polygon2.Contains(outside, h[0])).ToList();
                return inside.Count == 0
                    ? f.Add("IFCARBITRARYCLOSEDPROFILEDEF", E("AREA"), S(name), Line(outside))
                    : f.Add("IFCARBITRARYPROFILEDEFWITHVOIDS", E("AREA"), S(name), Line(outside), L(inside.Select(Line)));
            }
            return outsides.Count == 1 ? One(outsides[0], section.Name) : f.Add("IFCCOMPOSITEPROFILEDEF", E("AREA"), S(section.Name), L(outsides.Select(x => One(x, null))), Null);
        }
    }
}
