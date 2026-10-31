using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Part = (string Name, string Color, System.Collections.Generic.IReadOnlyList<Lapin.Geometry.Vec3> Vertices, System.Collections.Generic.IReadOnlyList<(int A, int B, int C)> Triangles);

namespace Lapin.Ifc;

public static partial class IfcImport
{
    /// <summary>
    /// A reference's body as the import's rules read it (docs/spec-ifc.md, Regulile importului): its parts (simplified
    /// when asked), what its triangles are wherever they stand (<see cref="Hash"/>: the same body moved has the same
    /// one), its low corner and centre, whether it lies in one plane, and how many triangles it had as read.
    /// </summary>
    sealed record BodyRead(List<Part> Parts, string? Hash, Vec3 Lo, Vec3 Centre, bool Flat, long Before)
    {
        public static readonly BodyRead Empty = new([], null, Vec3.Zero, Vec3.Zero, false, 0);
    }

    sealed partial class Reader
    {
        /// <summary>The ids another program gives what is not a building element: a port, an opening, a virtual element, an annotation written as an object.</summary>
        static readonly string[] PseudoClasses = ["IfcDistributionPort", "IfcPort", "IfcOpeningElement", "IfcVirtualElement", "IfcAnnotation"];

        /// <summary>The properties another program names an object's kind by (Navisworks' Tipo, BIMCamel's IfcClass, Revit's Category).</summary>
        static readonly HashSet<string> KindProperties = new(StringComparer.OrdinalIgnoreCase) { "Tipo", "Type", "IfcClass", "IfcEntity", "Entity", "Category", "Categoría", "Class", "Clase" };

        readonly ConcurrentDictionary<string, Lazy<BodyRead>> sharedReads = [];
        readonly ConcurrentDictionary<string, int> hashUses = [];
        long trianglesRead, trianglesKept;
        Vec3? middle;
        readonly Dictionary<string, int> dropped = [];
        readonly HashSet<string> placed = [];
        readonly Dictionary<string, (string? Id, Vec3 Lo)> contentBodies = [];
        int instancesByContent;

        /// <summary>A body read: its corner, centre and hash (for sharing it and telling a duplicate), whether it is flat, its parts simplified as asked.</summary>
        BodyRead Read(List<Part> raw)
        {
            if (raw.Count == 0) return BodyRead.Empty;
            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
            long before = 0;
            foreach (var p in raw)
            {
                before += p.Triangles.Count;
                foreach (var v in p.Vertices)
                {
                    if (v.X < minX) minX = v.X; if (v.Y < minY) minY = v.Y; if (v.Z < minZ) minZ = v.Z;
                    if (v.X > maxX) maxX = v.X; if (v.Y > maxY) maxY = v.Y; if (v.Z > maxZ) maxZ = v.Z;
                }
            }
            var lo = new Vec3(minX, minY, minZ);
            var centre = new Vec3((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
            var hash = o.Dedupe || o.Duplicates ? Hash(raw, lo) : null;
            var flat = o.Flat && IsFlat(raw);
            var parts = raw;
            if (o.Simplify > 0)
            {
                parts = new List<Part>(raw.Count);
                foreach (var p in raw)
                {
                    if (p.Triangles.Count <= 24) { parts.Add(p); continue; }
                    var (v, t) = Simplifier.Simplify(p.Vertices, p.Triangles, o.Simplify);
                    parts.Add(t.Count > 0 ? (p.Name, p.Color, v, t) : p);
                }
            }
            Interlocked.Add(ref trianglesRead, before);
            Interlocked.Add(ref trianglesKept, parts.Sum(p => (long)p.Triangles.Count));
            return new BodyRead(parts, hash, lo, centre, flat, before);
        }

        /// <summary>A shared body (a map, a shape many products use) read once, on whichever core asks first.</summary>
        BodyRead SharedRead(string key, ISolidBooleans? booleans) =>
            sharedReads.GetOrAdd(key, k => new Lazy<BodyRead>(() =>
            {
                var raw = new List<Part>();
                foreach (var (mesh, colour) in model.SharedBody(k, booleans))
                {
                    var m = mesh.Welded();
                    if (m.T.Count > 0) raw.Add(("Body", colour ?? "#c9ced6", m.V, m.T));
                }
                return Read(raw);
            }, LazyThreadSafetyMode.ExecutionAndPublication)).Value;

        /// <summary>
        /// What a body's triangles are wherever it stands: each part's colour, its vertices from the body's low corner
        /// to a tenth of a millimetre, its triangles; two FNV-1a hashes of 64 bits, so two hundred thousand bodies do not meet by chance.
        /// </summary>
        static string Hash(List<Part> parts, Vec3 lo)
        {
            ulong a = 14695981039346656037UL, b = 1099511628211UL * 31;
            void Mix(long v)
            {
                unchecked
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var by = (byte)(v >> (8 * i));
                        a = (a ^ by) * 1099511628211UL;
                        b = (b ^ by) * 6700417UL + 0x9E3779B97F4A7C15UL;
                    }
                }
            }
            foreach (var p in parts)
            {
                foreach (var ch in p.Color) Mix(ch);
                Mix(p.Vertices.Count); Mix(p.Triangles.Count);
                foreach (var v in p.Vertices) { Mix((long)Math.Round((v.X - lo.X) * 1e4)); Mix((long)Math.Round((v.Y - lo.Y) * 1e4)); Mix((long)Math.Round((v.Z - lo.Z) * 1e4)); }
                foreach (var (x, y, z) in p.Triangles) { Mix(x); Mix(y); Mix(z); }
            }
            return $"{a:x16}{b:x16}";
        }

        /// <summary>Whether every vertex lies within half a millimetre of the plane of the body's largest triangle (or the body has no area at all).</summary>
        static bool IsFlat(List<Part> parts)
        {
            Vec3 n = Vec3.Zero, at = Vec3.Zero;
            double best = 0;
            foreach (var p in parts)
                foreach (var (x, y, z) in p.Triangles)
                {
                    var c = Vec3.Cross(p.Vertices[y] - p.Vertices[x], p.Vertices[z] - p.Vertices[x]);
                    var l = c.Length;
                    if (l > best) { best = l; n = c * (1 / l); at = p.Vertices[x]; }
                }
            if (best < 1e-12) return true;
            foreach (var p in parts)
                foreach (var v in p.Vertices)
                    if (Math.Abs(Vec3.Dot(n, v - at)) > 0.0005) return false;
            return true;
        }

        /// <summary>Whether the file names this element as a port, an opening, a virtual element or an annotation: by its name, its object type, or the property its program names kinds by.</summary>
        bool IsPseudo(StepEntity e)
        {
            static bool Names(string? s) => s != null && PseudoClasses.Any(c => s.StartsWith(c, StringComparison.OrdinalIgnoreCase)
                && (s.Length == c.Length || s[c.Length] is ':' or ' ' or '.' or '-' or '_'));
            if (Names(Str(e[2])) || Names(Str(e[4]))) return true;
            foreach (var (_, props) in model.Properties(e.Id))
                foreach (var (k, v) in props)
                    if (KindProperties.Contains(k) && Names(v)) return true;
            return false;
        }

        /// <summary>Where a reference stands, for telling one far from the rest: the middle of the references' centres (their median, axis by axis).</summary>
        void FindMiddle()
        {
            if (o.FarAway == null) return;
            var cs = prepared.Values.Where(p => p.Body.Parts.Count > 0).Select(p => p.Centre).ToList();
            if (cs.Count < 8) return;
            double Med(Func<Vec3, double> g) { var l = cs.Select(g).Order().ToList(); return l[l.Count / 2]; }
            middle = new Vec3(Med(v => v.X), Med(v => v.Y), Med(v => v.Z));
        }

        /// <summary>Why a reference is left out by the import's rules, or null: flat, far from the rest, the same as one already brought.</summary>
        string? LeftOutBy(Prepared p, bool has2d)
        {
            if (p.Pseudo) return "ports, openings and virtual elements written as objects";
            if (p.Body.Parts.Count == 0) return null;
            if (o.Flat && p.Body.Flat && !has2d) return "flat bodies (texts, lines and hatches made of triangles)";
            if (o.FarAway is { } far && middle is { } mid && (p.Centre - mid).Length > far) return $"further than {far:0.#} m from the rest of the model";
            if (o.Duplicates && p.Body.Hash != null && !placed.Add(p.Body.Hash + "|" + Where(p))) return "the same body in the same place as another";
            return null;
        }

        /// <summary>Where a body stands, to the millimetre: an instance's placement, or an own body's low corner.</summary>
        static string Where(Prepared p)
        {
            static string R(double v) => Math.Round(v, 3).ToString(System.Globalization.CultureInfo.InvariantCulture);
            static string V(Vec3 v) => $"{R(v.X)},{R(v.Y)},{R(v.Z)}";
            return p.Shared is var (_, at) ? $"{V(at.X)};{V(at.Y)};{V(at.Z)};{V(at.Apply(p.Body.Lo))}" : V(p.Body.Lo);
        }

        void Drop(string reason) => dropped[reason] = dropped.GetValueOrDefault(reason) + 1;

        /// <summary>
        /// A body the file repeats, kept once by what it is (<see cref="BodyRead.Hash"/>): made the first time (its id
        /// from the file and the hash, so importing again finds it), in the frame the first one came in (a map's own),
        /// or from its low corner (<paramref name="fromCorner"/>: a body the file gives in the world's coordinates).
        /// Its id, and where its low corner is in the body kept; null when it has no triangles.
        /// </summary>
        (string Id, Vec3 Lo)? ContentBody(IEdit edit, StepEntity first, BodyRead body, bool fromCorner)
        {
            if (body.Hash == null || body.Parts.Count == 0) return null;
            if (contentBodies.TryGetValue(body.Hash, out var known)) return known.Id is { } kid ? (kid, known.Lo) : null;
            var parts = fromCorner ? body.Parts.Select(p => (p.Name, p.Color, (IReadOnlyList<Vec3>)p.Vertices.Select(v => v - body.Lo).ToList(), p.Triangles)).ToList() : body.Parts;
            var lo = fromCorner ? Vec3.Zero : body.Lo;
            var id = Step.Guid(Seed, "shared-body\ncontent\n" + body.Hash);
            Put(edit, id, StoredBodies.SharedClass, $"{Str(first[2]) ?? ClassName(first.Type)[3..]} (shared)", new JsonObject { [StoredBodies.Key] = StoredBodies.Write(parts), ["source"] = "content:" + body.Hash }, null, counts: false);
            contentBodies[body.Hash] = (id, lo);
            return (id, lo);
        }

        static JsonObject Instance(string bodyId, Xf at) => new()
        {
            [StoredBodies.BodyOf] = bodyId,
            [StoredBodies.XfKey] = StoredBodies.WriteXf([at.X.X, at.X.Y, at.X.Z, at.Y.X, at.Y.Y, at.Y.Z, at.Z.X, at.Z.Y, at.Z.Z, at.O.X, at.O.Y, at.O.Z]),
        };

        /// <summary>What the rules did, in the import's notes.</summary>
        void RuleNotes()
        {
            foreach (var (reason, n) in dropped.OrderByDescending(kv => kv.Value))
                notes.Add($"{n} element(s) left out, as chosen: {reason}.");
            if (instancesByContent > 0)
                notes.Add($"{instancesByContent} element(s) share {contentBodies.Count(kv => kv.Value.Id != null)} bod(ies) the file repeats, each kept once.");
            if (o.Simplify > 0 && trianglesRead > 0)
                notes.Add($"Meshes simplified within {o.Simplify * 1000:0.##} mm: {trianglesRead:N0} → {trianglesKept:N0} triangles.");
        }
    }
}
