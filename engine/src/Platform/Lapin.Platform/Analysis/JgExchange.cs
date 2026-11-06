using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform.Analysis;

/// <summary>
/// The JG Structures exchange file (docs/jg-structures.md): the analytical model (<see cref="AnalyticalModel"/>) written to
/// the contract "JG_Structures_v2_exchange" 1.0.0 — stories, grids, materials, sections, nodes, frames, shells and the
/// documentation — in the contract's units (cm, kgf) or SI (m, kN) declared once; null where Lapin does not know a value,
/// never 0; each item's own data under a "lapin" object. Checked by <see cref="JgValidator"/> before it is written.
/// </summary>
public static class JgExchange
{
    public const string ContractName = "JG_Structures_v2_exchange";
    public const string SchemaVersion = "1.0.0";
    public const string UnknownMaterial = "lapin-unknown-material";

    public sealed record Options(Units Units, AnalyticalOptions Analytical);

    /// <summary>The options a query sends: units ("contract" or "si"), storeys and kinds chosen (absent: all), allWalls.</summary>
    public static Options OptionsFrom(JsonObject a)
    {
        static IReadOnlySet<string>? Set(JsonNode? n) => n is JsonArray arr ? arr.Select(x => (string?)x).OfType<string>().ToHashSet() : null;
        var all = a["allWalls"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
        return new(Units.Of((string?)a["units"]), new AnalyticalOptions(Set(a["storeys"]), Set(a["kinds"]), all));
    }

    public static void Register(IModuleRegistry r)
    {
        // What the dialog chooses from: the storeys, and how many members of each kind the project has.
        r.Query("jg.survey", (q, _) =>
        {
            var model = AnalyticalModel.Build(q.Model, q.Services, new AnalyticalOptions(AllWalls: true));
            var kinds = new JsonObject
            {
                [AnalyticalKinds.Columns] = model.Frames.Where(f => f.Source.Category == AnalyticalKinds.Columns).Select(f => f.Source.SourceId).Distinct().Count(),
                [AnalyticalKinds.Beams] = model.Frames.Where(f => f.Source.Category == AnalyticalKinds.Beams).Select(f => f.Source.SourceId).Distinct().Count(),
                [AnalyticalKinds.Members] = model.Frames.Where(f => f.Source.Category == AnalyticalKinds.Members).Select(f => f.Source.SourceId).Distinct().Count(),
                [AnalyticalKinds.Walls] = model.Shells.Where(s => s.Source.Kind == "wall").Select(s => s.Source.SourceId).Distinct().Count(),
                [AnalyticalKinds.Slabs] = model.Shells.Where(s => s.Source.Kind == "slab").Select(s => s.Source.SourceId).Distinct().Count(),
                [AnalyticalKinds.Supports] = model.Nodes.Count(n => n.Restraints != null),
            };
            return new JsonObject
            {
                ["storeys"] = new JsonArray(model.Storeys.Select(s => (JsonNode)new JsonObject { ["id"] = s.Id, ["name"] = s.Name, ["elevation"] = s.Num("elevation") }).ToArray()),
                ["kinds"] = kinds,
            };
        });
        r.Query("jg.preview", (q, a) => Export(q, OptionsFrom(a)).Report);
        r.Query("jg.export", (q, a) =>
        {
            var (doc, report) = Export(q, OptionsFrom(a));
            if (report["errors"] is JsonArray { Count: > 0 } errors) throw new CommandException($"The exchange file does not pass its checks: {(string?)errors[0]}");
            var bytes = Encoding.UTF8.GetBytes(doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            return new JsonObject { ["name"] = $"{q.Title ?? "project"}.jg.json", ["data"] = Convert.ToBase64String(bytes), ["report"] = report };
        });
    }

    /// <summary>A number as written: nine significant digits, so a unit's conversion does not print noise.</summary>
    static double R(double x) => x == 0 ? 0 : double.Parse(x.ToString("G9", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    static JsonNode? N(double? x) => x is { } v && double.IsFinite(v) ? JsonValue.Create(R(v)) : null;

    public static (JsonObject Doc, JsonObject Report) Export(IQueryContext q, Options o)
    {
        var m = q.Model;
        var u = o.Units;
        var model = AnalyticalModel.Build(m, q.Services, o.Analytical);
        var file = q.Title is { } t ? $"{t}.lapin" : null;
        var warnings = new List<string>(model.Notes);
        var project = m.OfClass(IfcClass.Project).FirstOrDefault();

        // ---------------------------------------------------------------- stories, grids
        var stories = new JsonArray();
        foreach (var s in model.Storeys)
            stories.Add((JsonNode)new JsonObject
            {
                ["id"] = s.Id, ["name"] = s.Name, ["elevation"] = N(u.L(s.Num("elevation"))),
                ["height"] = s.NumOrNull("height") is { } h && h > 0 ? N(u.L(h)) : null,
                ["lapin"] = new JsonObject { ["globalId"] = s.Id },
            });
        var grids = new JsonArray();
        foreach (var g in m.OfClass(IfcClass.Grid))
        {
            var xs = new JsonArray();
            var ys = new JsonArray();
            foreach (var ax in (g.Attrs["axes"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                if (Args.TryPoint(ax["a"]) is not { } a || Args.TryPoint(ax["b"]) is not { } b) continue;
                var tag = (string?)ax["tag"] ?? "";
                var d = b - a;
                if (d.Length < 1e-9) continue;
                if (Math.Abs(d.X) <= 1e-6 * d.Length) xs.Add((JsonNode)new JsonObject { ["id"] = tag, ["coordinate"] = N(u.L(a.X)) });
                else if (Math.Abs(d.Y) <= 1e-6 * d.Length) ys.Add((JsonNode)new JsonObject { ["id"] = tag, ["coordinate"] = N(u.L(a.Y)) });
                else warnings.Add($"Grid „{g.Name}”, axis {tag}: oblique, a cartesian grid has no place for it (left out).");
            }
            if (xs.Count + ys.Count > 0) grids.Add((JsonNode)new JsonObject { ["id"] = g.Id, ["type"] = "cartesian", ["x"] = xs, ["y"] = ys, ["lapin"] = new JsonObject { ["globalId"] = g.Id, ["name"] = g.Name } });
        }

        // ---------------------------------------------------------------- materials
        var materials = new JsonArray();
        var missing = new JsonArray();
        var materialIds = model.Frames.Select(f => f.Source.MaterialId).Concat(model.Shells.Select(s => s.Source.MaterialId)).Distinct().ToList();
        foreach (var id in materialIds)
        {
            var e = id == null ? null : m.Get(id) ?? Materials.Find(m, id);
            if (e == null)
            {
                materials.Add((JsonNode)new JsonObject
                {
                    ["id"] = UnknownMaterial, ["name"] = "Unknown material", ["kind"] = "other",
                    ["elastic_modulus"] = null, ["poisson_ratio"] = null, ["yield_strength"] = null, ["compressive_strength"] = null, ["weight_density"] = null,
                    ["source"] = new JsonObject { ["kind"] = "absent", ["note"] = "The element's type names no material." },
                });
                missing.Add((JsonNode)Missing("materials", UnknownMaterial, "Unknown material", ["elastic_modulus", "poisson_ratio", "weight_density"]));
                continue;
            }
            var sm = StructuralMaterials.Of(m, e);
            var values = new JsonObject();
            void Val(string key, MaterialValue? v) { if (v != null) values[key] = v.Source; }
            Val("elastic_modulus", sm.E); Val("poisson_ratio", sm.Poisson); Val("yield_strength", sm.Fy); Val("compressive_strength", sm.Fc); Val("weight_density", sm.Density);
            var src = new JsonObject
            {
                ["kind"] = sm.Grade != null ? "standard" : values.Count > 0 ? "project_data" : "absent",
                ["standard"] = sm.Standard, ["grade"] = sm.Grade, ["file"] = file, ["reference"] = $"Material „{e.Name}”", ["values"] = values,
            };
            if (sm.Sources.Count > 0) src["notes"] = new JsonArray(sm.Sources.Select(x => (JsonNode)x).ToArray());
            materials.Add((JsonNode)new JsonObject
            {
                ["id"] = e.Id, ["name"] = e.Name, ["kind"] = sm.Kind,
                ["elastic_modulus"] = N(sm.E is { } ev ? u.Stress(ev.Value) : null),
                ["poisson_ratio"] = N(sm.Poisson?.Value),
                ["yield_strength"] = N(sm.Fy is { } fy ? u.Stress(fy.Value) : null),
                ["compressive_strength"] = N(sm.Fc is { } fc ? u.Stress(fc.Value) : null),
                ["weight_density"] = N(sm.Density is { } dv ? u.WeightDensity(dv.Value) : null),
                ["source"] = src,
                ["lapin"] = new JsonObject { ["materialId"] = e.Id },
            });
            var gaps = new List<string>();
            if (sm.E == null) gaps.Add("elastic_modulus");
            if (sm.Poisson == null) gaps.Add("poisson_ratio");
            if (sm.Kind == "steel" && sm.Fy == null) gaps.Add("yield_strength");
            if (sm.Kind == "concrete" && sm.Fc == null) gaps.Add("compressive_strength");
            if (sm.Density == null) gaps.Add("weight_density");
            if (gaps.Count > 0) missing.Add((JsonNode)Missing("materials", e.Id, e.Name, gaps));
        }
        string MaterialOf(string? id) => id == null || (m.Get(id) ?? Materials.Find(m, id)) is not { } e ? UnknownMaterial : e.Id;

        // ---------------------------------------------------------------- sections
        var sections = new JsonArray();
        var sectionIds = new Dictionary<string, string>();
        foreach (var f in model.Frames.Select(f => f.Source).DistinctBy(f => f.Section.Key))
        {
            var (json, gaps) = FrameSectionJson(f.Section, u, file);
            sections.Add((JsonNode)json);
            sectionIds[f.Section.Key] = f.Section.Key;
            if (gaps.Count > 0) missing.Add((JsonNode)Missing("sections", f.Section.Key, f.Section.Name, gaps));
        }
        var shellThickness = new Dictionary<string, double>();
        string ShellSection(AnalyticalShell s)
        {
            // One section per type's core; a wall of its own thickness, or one whose type's core differs, gets its own.
            var id = shellThickness.TryGetValue(s.SectionKey, out var t0) && Math.Abs(t0 - s.Thickness) > 1e-6 ? $"{s.SectionKey}-{Math.Round(s.Thickness * 1000, 1).ToString(CultureInfo.InvariantCulture)}" : s.SectionKey;
            if (shellThickness.ContainsKey(id)) return id;
            shellThickness[id] = s.Thickness;
            sections.Add((JsonNode)new JsonObject
            {
                ["id"] = id, ["name"] = s.SectionName, ["category"] = s.Kind, ["kind"] = "user_defined", ["catalog"] = null,
                ["geometry"] = new JsonObject
                {
                    ["shape"] = "shell_thin", ["dimensions"] = new JsonObject(),
                    ["source"] = new JsonObject { ["kind"] = "project_data", ["file"] = file, ["reference"] = $"Core layer of „{s.SectionName}”" },
                },
                ["properties"] = new JsonObject(), ["thickness"] = N(u.L(s.Thickness)),
                ["property_status"] = "derived_user_geometry",
                ["source"] = new JsonObject { ["kind"] = "project_data", ["file"] = file, ["reference"] = $"Core layer of „{s.SectionName}”", ["note"] = "The thickness is the core layer's; finishes are left out of the analytical section." },
            });
            return id;
        }

        // ---------------------------------------------------------------- nodes, frames, shells
        var nodes = new JsonArray();
        foreach (var n in model.Nodes)
        {
            var o2 = new JsonObject
            {
                ["id"] = n.Id, ["coordinates"] = new JsonArray(N(u.L(n.At.X)), N(u.L(n.At.Y)), N(u.L(n.At.Z))),
                ["story_id"] = n.StoreyId, ["restraints"] = n.Restraints is { } rs ? new JsonArray(rs.Select(x => (JsonNode)x).ToArray()) : null,
            };
            if (n.Restraints != null) o2["lapin"] = new JsonObject { ["support"] = n.RestraintKind, ["source"] = n.RestraintSource };
            nodes.Add((JsonNode)o2);
        }
        var frames = new JsonArray();
        foreach (var f in model.Frames)
        {
            var l = new JsonObject { ["globalId"] = f.Source.SourceId, ["piece"] = f.Piece, ["pieces"] = f.Pieces, ["name"] = f.Source.Name, ["ifcClass"] = f.Source.IfcClass };
            if (f.Source.Role != null) l["role"] = f.Source.Role;
            if (Math.Abs(f.OffsetZ) > 1e-9) l["offset_z"] = N(u.L(f.OffsetZ));
            if (f.Source.Mirrored) l["mirrored"] = true;
            if (f.Source.Note != null) l["note"] = f.Source.Note;
            frames.Add((JsonNode)new JsonObject
            {
                ["id"] = f.Id, ["kind"] = f.Source.Kind, ["node_i"] = f.I.Id, ["node_j"] = f.J.Id, ["section_id"] = f.Source.Section.Key,
                ["material_id"] = MaterialOf(f.Source.MaterialId), ["local_axis_rotation_deg"] = N(f.RotationDeg), ["end_offsets"] = null, ["lapin"] = l,
            });
        }
        var shells = new JsonArray();
        foreach (var s in model.Shells)
        {
            var l = new JsonObject { ["globalId"] = s.Source.SourceId, ["piece"] = s.Piece, ["pieces"] = s.Pieces, ["name"] = s.Source.Name, ["ifcClass"] = s.Source.IfcClass };
            if (s.Source.Note != null) l["note"] = s.Source.Note;
            shells.Add((JsonNode)new JsonObject
            {
                ["id"] = s.Id, ["kind"] = s.Source.Kind, ["node_ids"] = new JsonArray(s.Nodes.Select(n => (JsonNode)n.Id).ToArray()),
                ["section_id"] = ShellSection(s.Source), ["material_id"] = MaterialOf(s.Source.MaterialId), ["pier_label"] = null, ["spandrel_label"] = null, ["lapin"] = l,
            });
        }

        foreach (var n in model.FreeBases)
            warnings.Add($"Node {n.Id} at the foot of {string.Join(", ", model.Frames.Where(f => f.I == n || f.J == n).Select(f => $"„{f.Source.Name}”").Concat(model.Shells.Where(s => s.Nodes.Contains(n)).Select(s => $"„{s.Source.Name}”")).Distinct())} stands on nothing and has no support: its restraints are null.");
        // Bar ends that meet nothing (a cantilever, or a member Lapin could not connect: secondary steel on top of a rafter).
        var uses = new Dictionary<ANode, int>();
        foreach (var f in model.Frames) { uses[f.I] = uses.GetValueOrDefault(f.I) + 1; uses[f.J] = uses.GetValueOrDefault(f.J) + 1; }
        foreach (var s in model.Shells) foreach (var n in s.Nodes) uses[n] = uses.GetValueOrDefault(n) + 1;
        var loose = model.Frames.Where(f => (uses[f.I] == 1 && f.I.Restraints == null && !model.FreeBases.Contains(f.I)) || (uses[f.J] == 1 && f.J.Restraints == null && !model.FreeBases.Contains(f.J)))
            .Select(f => f.Source.Name).ToList();
        if (loose.Count > 0)
            warnings.Add($"{loose.Count} frame(s) have an end that meets no other member (a cantilever, or an eccentric connection Lapin does not link): {string.Join(", ", loose.Distinct().Take(8).Select(x => $"„{x}”"))}{(loose.Distinct().Count() > 8 ? "…" : "")}.");
        if (model.Nodes.Count > 0 && !model.Nodes.Any(n => n.Restraints != null)) warnings.Add("No node is restrained: add footings under the columns and walls, or steel column bases.");

        var leftOut = new JsonArray(model.LeftOut.Select(x => (JsonNode)new JsonObject { ["id"] = x.SourceId, ["name"] = x.Name, ["ifcClass"] = x.IfcClass, ["reason"] = x.Reason }).ToArray());
        var axes = AxesText;
        var doc = new JsonObject
        {
            ["contract"] = ContractName,
            ["schema_version"] = SchemaVersion,
            ["project"] = new JsonObject
            {
                ["id"] = project?.Id ?? "lapin-project", ["name"] = project?.Name is { Length: > 0 } pn ? pn : q.Title ?? "Project",
                ["source_application"] = "Lapin", ["source_file"] = file,
            },
            ["units"] = u.ToJson(),
            ["coordinate_system"] = new JsonObject { ["handedness"] = "right", ["x_axis"] = "east", ["y_axis"] = "north", ["z_axis"] = "up", ["frame_local_axes"] = axes },
            ["stories"] = stories, ["grids"] = grids, ["materials"] = materials, ["sections"] = sections,
            ["nodes"] = nodes, ["frames"] = frames, ["shells"] = shells,
        };
        doc["documentation"] = new JsonObject
        {
            ["purpose"] = "The analytical model of a Lapin project for JG Structures: derived from the physical model, not drawn by an engineer; check it before analysis.",
            ["json_comments_allowed"] = false,
            ["coordinate_order"] = "[X, Y, Z]",
            ["node_restraint_order"] = "[UX, UY, UZ, RX, RY, RZ]",
            ["null_policy"] = "null: Lapin does not know the value. It is never 0 and never estimated.",
            ["units_note"] = u.Name == "si"
                ? "SI: lengths in m, forces in kN, stresses in kN/m2 (kPa), weight densities in kN/m3 (ρ·g, g = 9.80665 m/s2). SI is Lapin's proposal for the contract's 1.1."
                : "Lapin works in SI (m, N, Pa, kg/m3). Converted with 1 kgf = 9.80665 N exactly: lengths ×100 (m → cm), stresses Pa / 98066.5 (kgf/cm2), weight densities kg/m3 × 1e-6 (kgf/cm3, a kilogram weighing one kilogram-force).",
            ["analytical_model"] = "Columns, beams and steel members on their axes (the section's box centre); load-bearing walls on their core's centre plane, slabs on their core's mid-plane, finishes left out. A beam carrying a slab lies in the slab's mid-plane (its eccentricity in lapin.offset_z); column and wall ends meet the beam or slab at their foot and head; beam ends lie on column axes; slab corners on their supports' nodes (within 0.5 m); points within 1 cm are one node; convex slabs are split along the beams and walls crossing them, walls where walls and columns meet them; every node on a member's edge or span is one of its nodes. Curved members are chords (at most 1 mm off the arc). Supports: the lowest column and wall feet on a footing are fixed; a steel column base is pinned or fixed as its connection says.",
            ["frame_local_axes"] = axes,
            ["section_axes"] = "A section's depth h lies along local 2 and its width b along local 3: I33 = ∫ y2² dA (bending about local 3, a beam's strong axis), I22 = ∫ y3² dA, about the centroid; S = I / farthest fibre, Z = plastic modulus about the equal-area axis; J the torsion constant (its method in the section's source). Properties of L and other unsymmetric sections are about the geometric axes, not the principal ones.",
            ["catalog_policy"] = "Lapin holds no tabulated profile data: a catalogue profile carries its exact designation and product standard, its properties derived from the nominal dimensions (property_status derived_user_geometry), never verified_tabulated.",
            ["lapin_namespace"] = "Items may carry a \"lapin\" object (the element's GlobalId, its piece of it, its name and IFC class, a support's source): Lapin's own, for a results file to find its elements again; an importer may ignore it.",
            ["analysis_notice"] = missing.Count > 0 ? "Some values needed for analysis are missing (documentation.missing): analyses that need them must stay disabled until they are given." : "Every material and section has the values a linear analysis needs.",
            ["missing"] = missing.DeepClone(),
            ["left_out"] = leftOut.DeepClone(),
            ["warnings"] = new JsonArray(warnings.Select(x => (JsonNode)x).ToArray()),
        };

        var errors = JgValidator.Validate(doc);
        var report = new JsonObject
        {
            ["units"] = u.Name,
            ["counts"] = new JsonObject
            {
                ["stories"] = stories.Count, ["grids"] = grids.Count, ["materials"] = materials.Count, ["sections"] = sections.Count,
                ["nodes"] = nodes.Count, ["frames"] = frames.Count, ["shells"] = shells.Count, ["supports"] = model.Nodes.Count(n => n.Restraints != null),
            },
            ["errors"] = new JsonArray(errors.Select(x => (JsonNode)x).ToArray()),
            ["warnings"] = new JsonArray(warnings.Select(x => (JsonNode)x).ToArray()),
            ["missing"] = missing,
            ["leftOut"] = leftOut,
            ["ready"] = errors.Count == 0 && missing.Count == 0 && model.FreeBases.Count == 0 && model.Nodes.Any(n => n.Restraints != null),
        };
        return (doc, report);
    }

    /// <summary>The frames' local axes, as the file states them.</summary>
    public const string AxesText =
        "Local 1 runs from node_i to node_j (a column from its foot up). Before rotation, local 2 is global +X for a vertical frame (local 1 within 0.001 rad of Z), " +
        "else the upward unit vector perpendicular to local 1 in the vertical plane containing it; local 3 = local 1 × local 2 (right-handed). " +
        "local_axis_rotation_deg turns local 2 and 3 about local 1 by the right-hand rule. The section's depth h lies along local 2, its width b along local 3: " +
        "a beam at 0° has its depth vertical (I33 its strong axis); a column at 0° has its depth along X.";

    static JsonObject Missing(string entity, string id, string name, IEnumerable<string> fields) =>
        new() { ["entity"] = entity, ["id"] = id, ["name"] = name, ["fields"] = new JsonArray(fields.Select(x => (JsonNode)x).ToArray()) };

    static readonly Dictionary<string, string> Shapes = new()
    {
        ["rectangle"] = "rectangle", ["circle"] = "circle", ["I"] = "i_wide_flange", ["U"] = "channel", ["T"] = "tee", ["L"] = "angle",
        ["RHS"] = "box", ["CHS"] = "pipe", ["C"] = "cold_formed_c", ["Z"] = "cold_formed_z", ["SIGMA"] = "cold_formed_sigma",
    };

    /// <summary>A frame section as the contract has it, and the analysis fields it leaves null.</summary>
    static (JsonObject Json, List<string> Missing) FrameSectionJson(FrameSection fs, Units u, string? file)
    {
        var s = fs.Shape;
        var props = SectionProperties.Of(s);
        var cat = SectionProperties.CatalogueOf(s.Designation);
        var dims = new JsonObject();
        void D(string k, double v) => dims[k] = N(u.L(v));
        switch (s.Kind)
        {
            case "rectangle": D("b", s.Width); D("h", s.Depth); break;
            case "circle": D("d", s.Width); break;
            case "CHS": D("d", s.Width); D("t", s.Web); break;
            case "RHS": D("h", s.Depth); D("b", s.Width); D("t", s.Web); if (cat is { Radius: > 0 } rc) D("r_outer", rc.Radius); break;
            case "C" or "Z" or "SIGMA": D("h", s.Depth); D("b", s.Width); D("t", s.Web); if (s.Lip > 0) D("lip", s.Lip); break;
            case "L": D("h", s.Depth); D("b", s.Width); D("t", s.Flange); if (cat is { Radius: > 0 } rl) D("r", rl.Radius); break;
            default: D("h", s.Depth); D("b", s.Width); D("tw", s.Web); D("tf", s.Flange); if (cat is { Radius: > 0 } r) D("r", r.Radius); break;
        }
        var properties = new JsonObject
        {
            ["area"] = N(props is { } p0 ? u.L2(p0.Area) : null),
            ["I22"] = N(props is { } p1 ? u.L4(p1.I22) : null), ["I33"] = N(props is { } p2 ? u.L4(p2.I33) : null),
            ["S22"] = N(props is { } p3 ? u.L3(p3.S22) : null), ["S33"] = N(props is { } p4 ? u.L3(p4.S33) : null),
            ["Z22"] = N(props is { } p5 ? u.L3(p5.Z22) : null), ["Z33"] = N(props is { } p6 ? u.L3(p6.Z33) : null),
            ["J"] = N(props is { } p7 ? u.L4(p7.J) : null),
        };
        var geometrySource = new JsonObject { ["kind"] = "project_data", ["file"] = file, ["reference"] = fs.Source };
        JsonObject? catalog = null;
        if (cat is { } c)
        {
            catalog = new JsonObject
            {
                ["standard"] = c.Standard, ["family"] = c.Family, ["designation"] = s.Designation,
                ["source"] = new JsonObject { ["kind"] = "lapin_catalogue", ["reference"] = $"Lapin's steel profile catalogue: nominal dimensions of {s.Designation}; root radius after {c.Standard}" },
            };
        }
        var source = new JsonObject
        {
            ["kind"] = props != null ? "computed" : "absent",
            ["method"] = props?.Method,
            ["note"] = cat != null ? "Derived from the nominal dimensions, not taken from a table: Lapin holds no tabulated profile data, so the section is not verified_tabulated." : null,
        };
        var json = new JsonObject
        {
            ["id"] = fs.Key, ["name"] = fs.Name, ["category"] = "frame", ["kind"] = cat != null ? "catalog" : "user_defined", ["catalog"] = catalog,
            ["geometry"] = new JsonObject { ["shape"] = Shapes.GetValueOrDefault(s.Kind, s.Kind.ToLowerInvariant()), ["dimensions"] = dims, ["source"] = geometrySource },
            ["properties"] = properties, ["thickness"] = null,
            ["property_status"] = props != null ? "derived_user_geometry" : "pending",
            ["source"] = source,
        };
        if (props is { } pc && pc.Centroid.Length > 1e-9) json["lapin"] = new JsonObject { ["centroid_offset"] = new JsonArray(N(u.L(pc.Centroid.X)), N(u.L(pc.Centroid.Y))) };
        var gaps = properties.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList();
        return (json, gaps);
    }
}
