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

        /// <summary>A real to 10 significant digits, however small (a second moment in m⁴), STEP's way: 8.53E-07 has its point as 8.53E-07.</summary>
        static string Sig(double v)
        {
            if (!double.IsFinite(v) || v == 0) return "0.";
            var t = v.ToString("0.#########E+00", System.Globalization.CultureInfo.InvariantCulture);
            return t.Contains('.') ? t : t.Replace("E", ".E");
        }

        string P3(Vec3 p) => f.Add("IFCCARTESIANPOINT", L(new[] { R(p.X), R(p.Y), R(p.Z) }));
        string Dir3(Vec3 d) => f.Add("IFCDIRECTION", L(new[] { R(d.X), R(d.Y), R(d.Z) }));

        void Structural()
        {
            var reader = s.Reader;
            // What the export leaves out (its Exclude, and what its phase has not built yet) stays out of the analysis view too:
            // its members, its supports, its connections and the loads derived from it.
            var left = new HashSet<string>(o.Exclude ?? new HashSet<string>());
            if (o.Phase != null) foreach (var e in m.Entities.Where(e => IsElement(e) && !InPhase(e))) left.Add(e.Id);
            var options = new AnalyticalOptions(Exclude: left.Count > 0 ? left : null);
            var am = AnalyticalModel.Build(m, reader.Services, options);
            var loads = s.Catalog.Get<IStructuralLoads>()?.For(reader, options);
            var axial = FrameReleases.AxialMembers(am, m);
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
            // A bar's end: its releases as the condition it is joined by (true: held), an eccentric end (a joint's arm,
            // AFrame.OffI/OffJ) its offset from the node as well, the member's edge then ending at its own vertex there. Both in the
            // member's local axes, as IFC implies with no coordinate system given: x along the edge, z the member's Axis (Lapin's
            // local 2, the section's depth), y = z × x (minus Lapin's local 3); so Lapin's [ux, u2, u3, rx, r2, r3] are IFC's
            // [x, z, −y]: a beam pinned about its strong axis (r3) is free about IFC's y. The member lies on its
            // analytical line (the section's box centre, where a parametric profile has its origin): an unsymmetric section's
            // centroid off it is the profile's to say, not an eccentricity (IFC puts the profile's origin on the reference curve;
            // writing it twice would move the bar twice for a reader that honours both).
            var releases = FrameReleases.Of(am, m);
            var eccentric = 0;
            void ConnectEnd(string member, string memberGid, AFrame fr, bool atJ, Vec3 local1, Vec3 local2)
            {
                var n = atJ ? fr.J : fr.I;
                var off = atJ ? fr.OffJ : fr.OffI;
                var rel = releases.TryGetValue(fr, out var r) ? (atJ ? r.J : r.I) : null;
                var cond = rel != null && rel.Any(x => x)
                    ? f.Add("IFCBOUNDARYNODECONDITION", S(rel.SequenceEqual(FrameReleases.Pinned) ? "pinned" : rel.SequenceEqual(FrameReleases.PinnedStrong) ? "pinned about the strong axis" : "released"),
                        Bool(!rel[0]), Bool(!rel[2]), Bool(!rel[1]), Bool(!rel[3]), Bool(!rel[5]), Bool(!rel[4]))
                    : Null;
                var gid = Gid(memberGid, $"connects:{n.Id}");
                if (off.Length <= 1e-9)
                {
                    f.Add("IFCRELCONNECTSSTRUCTURALMEMBER", gid, Null, Null, Null, member, connectionOf[n], cond, Null, Null, Null);
                    return;
                }
                var y = Vec3.Cross(local2, local1);
                var ecc = f.Add("IFCCONNECTIONPOINTECCENTRICITY", P3(n.At + off), vertexOf[n], R(Vec3.Dot(off, local1)), R(Vec3.Dot(off, y)), R(Vec3.Dot(off, local2)));
                f.Add("IFCRELCONNECTSWITHECCENTRICITY", gid, Null, Null, S("eccentric end: the offset from the node to the member's end, in the member's local axes"), member, connectionOf[n], cond, Null, Null, Null, ecc);
                eccentric++;
            }
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
                // An eccentric bar on its own line: an edge of its own between its ends, a node's vertex where there is no arm.
                var edge = fr.OffI.Length <= 1e-9 && fr.OffJ.Length <= 1e-9 ? EdgeOf(fr.I, fr.J).Edge
                    : f.Add("IFCEDGE", fr.OffI.Length <= 1e-9 ? vertexOf[fr.I] : f.Add("IFCVERTEXPOINT", P3(pi)), fr.OffJ.Length <= 1e-9 ? vertexOf[fr.J] : f.Add("IFCVERTEXPOINT", P3(pj)));
                var gid = Gid(fr.Id, "structural-member");
                var member = f.Add("IFCSTRUCTURALCURVEMEMBER", gid, Null, S(fr.Pieces > 1 ? $"{fr.Source.Name} ({fr.Piece}/{fr.Pieces})" : fr.Source.Name), Null, Null, shared,
                    Shape("Edge", edge), E(axial.Contains(fr.Source.SourceId) ? "PIN_JOINED_MEMBER" : "RIGID_JOINED_MEMBER"), Dir3(local2));
                frameRef[fr] = member;
                items.Add(member);
                ConnectEnd(member, gid, fr, false, u, local2);
                ConnectEnd(member, gid, fr, true, u, local2);
                Own(fr.Source.SourceId, member);
                var key = (fr.Source.Section.Key, fr.Source.MaterialId);
                if (!profileSets.TryGetValue(key, out var ps))
                {
                    var profile = StructuralProfile(fr.Source.Section);
                    // The section's values as the analysis takes them (SectionProperties, from its outline), so that a reader
                    // need not draw it again (a sloped flange is drawn more than one way): Pset_ProfileMechanical, about the
                    // profile's own axes (IFC's member y is the profile's x: MomentOfInertiaY bends in the depth, Lapin's I33).
                    if (SectionProperties.Of(fr.Source.Section.Shape) is { Area: > 0 } sp && (sp.Geometric ?? sp) is var g)
                        f.Add("IFCPROFILEPROPERTIES", S("Pset_ProfileMechanical"), Null, L(new[]
                        {
                            Single("CrossSectionArea", Typed("IFCAREAMEASURE", Sig(g.Area))),
                            Single("MomentOfInertiaY", Typed("IFCMOMENTOFINERTIAMEASURE", Sig(g.I33))),
                            Single("MomentOfInertiaZ", Typed("IFCMOMENTOFINERTIAMEASURE", Sig(g.I22))),
                            Single("TorsionalConstantX", Typed("IFCMOMENTOFINERTIAMEASURE", Sig(g.J))),
                        }), profile);
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
                            // Snow on a sloping face is per area of its plan (PROJECTED_LENGTH), wind's pressure per area of the face.
                            action = f.Add("IFCSTRUCTURALPLANARACTION", gid, Null, S(a.Name), Null, Null, shared, Shape("Face", ActionFace(a.Points, a.Holes)), load,
                                E("GLOBAL_COORDS"), Null, a.Projected is { } projected ? E(projected ? "PROJECTED_LENGTH" : "TRUE_LENGTH") : Null, E("CONST"));
                            break;
                        }
                        case "curve" when a.Points.Count >= 2:
                        {
                            // On the bars it lies along, as the analysis takes it: one action for each bar's stretch (the stretch
                            // a bar covers, a line over several bars split at their ends), each on that bar; a stretch on no bar
                            // goes on the slab under it. A line on one bar or none stays one action, with its own GlobalId.
                            var stretches = Stretches(am, a);
                            for (var k = 0; k < stretches.Count; k++)
                            {
                                var (p0, p1, bar) = stretches[k];
                                var sgid = stretches.Count == 1 ? gid : Gid(gid, $"stretch:{k}");
                                var load = f.Add("IFCSTRUCTURALLOADLINEARFORCE", Null, R(force.X), R(force.Y), R(force.Z), Null, Null, Null);
                                var edge = f.Add("IFCEDGE", f.Add("IFCVERTEXPOINT", P3(p0)), f.Add("IFCVERTEXPOINT", P3(p1)));
                                var piece = f.Add("IFCSTRUCTURALCURVEACTION", sgid, Null, S(stretches.Count == 1 ? a.Name : $"{a.Name} ({k + 1}/{stretches.Count})"), Null, Null, shared,
                                    Shape("Edge", edge), load, E("GLOBAL_COORDS"), Null, Null, E("CONST"));
                                actions++;
                                (inCase.TryGetValue(a.CaseId, out var cl) ? cl : inCase[a.CaseId] = []).Add(piece);
                                var on = bar != null ? frameRef[bar] : Under(am, a with { Points = [p0, p1] }, frameRef, shellRef);
                                if (on != null) f.Add("IFCRELCONNECTSSTRUCTURALACTIVITY", Gid(sgid, "structural-under"), Null, Null, Null, on, piece);
                            }
                            continue;
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
            if (eccentric > 0) Notes.Add($"Structural analysis view: {eccentric} eccentric bar ends written with their offsets (IfcRelConnectsWithEccentricity), each bar's edge ending at its own end.");
            if (releases.Count > 0) Notes.Add($"Structural analysis view: {releases.Count} bars with released ends (their connections' AppliedCondition).");
            if (am.LeftOut.Count > 0) Notes.Add($"Structural analysis view: {am.LeftOut.Count} elements left out of the analytical model (as the JG Structures export says why).");
            if (axial.Count > 0) Notes.Add($"Structural analysis view: {axial.Count} rods and ties as pin-joined members (PIN_JOINED_MEMBER), pinned at their ends.");
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
            // A planar action on a wall (wind on a façade) bears on no slab.
            var normal = a.Kind == "planar" ? VecMath.NewellNormal(a.Points.ToList()) : default;
            if (a.Kind == "planar" && normal.Length > 1e-12 && Math.Abs(normal.Normalized().Z) < 1e-6) return null;
            var at = a.Kind switch
            {
                // On a sloping face (snow, wind on a roof), its middle on its own plane.
                "planar" => Middle(a.Points.Select(p => p.Xy).ToList()) is var mid && normal.Length > 1e-12
                    ? mid.At(a.Points[0].Z - (normal.X * (mid.X - a.Points[0].X) + normal.Y * (mid.Y - a.Points[0].Y)) / normal.Z) : default,
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

        /// <summary>
        /// A line load's stretches over the bars under it, by the analysis' rule (Lapin.Analysis' LoadMapping): the bars that are
        /// not columns, parallel to it within 10° in plan, their axis within 0.3 m of it, from 1 m under it to 0.3 m over; each
        /// stretch between the bars' ends on the highest of them (then the nearest), null where none is. Neighbouring stretches
        /// on the same bar, or on none, are one.
        /// </summary>
        static List<(Vec3 P0, Vec3 P1, AFrame? Bar)> Stretches(AnalyticalModel am, StructuralActionData act)
        {
            var (a3, b3) = (act.Points[0], act.Points[1]);
            var (a, b) = (a3.Xy, b3.Xy);
            var z = (a3.Z + b3.Z) / 2;
            var len = (b - a).Length;
            if (len < 1e-9) return [(a3, b3, null)];
            var dir = (b - a) * (1 / len);
            var cands = new List<(AFrame Bar, double U0, double U1, double Z, double Off)>();
            foreach (var fr in am.Frames)
            {
                if (fr.Source.Kind == "column") continue;
                var (pi, pj) = (fr.I.At + fr.OffI, fr.J.At + fr.OffJ);
                var fd = pj.Xy - pi.Xy;
                var fl = fd.Length;
                if (fl < 1e-6 || Math.Abs(Vec2.Cross(fd * (1 / fl), dir)) > Math.Sin(10 * Math.PI / 180)) continue;
                var fu = fd * (1 / fl);
                var (s0, s1) = (Vec2.Dot(pi.Xy - a, dir) / len, Vec2.Dot(pj.Xy - a, dir) / len);
                var (u0, u1) = (Math.Max(0, Math.Min(s0, s1)), Math.Min(1, Math.Max(s0, s1)));
                if (u1 - u0 < 1e-9) continue;
                var mid = a + (b - a) * ((u0 + u1) / 2);
                var off = Math.Abs(Vec2.Cross(fu, mid - pi.Xy));
                if (off > 0.3) continue;
                var t = Vec2.Dot(mid - pi.Xy, fu) / fl;
                var fz = pi.Z + (pj.Z - pi.Z) * Math.Clamp(t, 0, 1);
                if (fz > z + 0.3 || fz < z - 1.0) continue;
                cands.Add((fr, u0, u1, fz, off));
            }
            var cuts = cands.SelectMany(c => new[] { c.U0, c.U1 }).Append(0).Append(1).Distinct().Order().ToList();
            var stretches = new List<(double U0, double U1, AFrame? Bar)>();
            for (var i = 0; i + 1 < cuts.Count; i++)
            {
                var (u0, u1) = (cuts[i], cuts[i + 1]);
                if (u1 - u0 < 1e-9) continue;
                var um = (u0 + u1) / 2;
                var best = cands.Where(c => c.U0 <= um && c.U1 >= um).OrderByDescending(c => c.Z).ThenBy(c => c.Off).Select(c => c.Bar).FirstOrDefault();
                if (stretches.Count > 0 && ReferenceEquals(stretches[^1].Bar, best))
                    stretches[^1] = (stretches[^1].U0, u1, best);
                else stretches.Add((u0, u1, best));
            }
            Vec3 At(double u) => a3 + (b3 - a3) * u;
            return stretches.Count == 0 ? [(a3, b3, null)] : stretches.Select(s => (At(s.U0), At(s.U1), s.Bar)).ToList();
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
