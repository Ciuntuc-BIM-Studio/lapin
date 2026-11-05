using System.Text;
using System.Text.Json.Nodes;
using Lapin.Drawing.Documents;
using Lapin.Model;

namespace Lapin.Platform.Documents;

/// <summary>
/// A document filled from the project (docs/plan-documents.md): its blocks walked with every <c>{{field}}</c> read from the modules'
/// <see cref="IDocumentFields"/>, its repeats over their collections, its ifs, the user's inputs, its tables from
/// <see cref="IDocumentTables"/> and its pictures drawn, into the <see cref="FilledDocument"/> the writers write. A field the
/// project does not fill stays as its key (<see cref="DocRun.Missing"/>) and is listed as to complete.
/// <para>
/// The text's markup is the same everywhere: <c>**bold**</c>, <c>*italic*</c>, <c>{{key}}</c>; in an input, a new line is a new
/// paragraph and lines starting with "- " a bulleted list. Inside a repeat, <c>{{&lt;prefix&gt;.&lt;field&gt;}}</c> reads the item
/// (<c>{{room.name}}</c>), and <c>{{&lt;prefix&gt;.index}}</c> is its number from 1.
/// </para>
/// </summary>
public static class DocumentFill
{
    /// <summary>The papers a document may be on (mm, portrait).</summary>
    public static readonly Dictionary<string, (double W, double H)> Papers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A4"] = (210, 297), ["A3"] = (297, 420), ["A5"] = (148, 210), ["Letter"] = (215.9, 279.4), ["Legal"] = (215.9, 355.6),
    };

    /// <summary>The document's format: its language and the project's units.</summary>
    public static DocFormat FormatOf(IModelView m, Entity doc) => new((string?)doc.Str("lang") is { Length: > 0 } l ? l : "ro", DraftingResources.Book(m).Units);

    /// <summary>Every field the product's modules give, by key (the first module to give one wins), with its label.</summary>
    public static List<DocField> AllFields(IQueryContext q, DocFormat f)
    {
        var o = new List<DocField>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in q.Services.All<IDocumentFields>())
            foreach (var field in Safe(() => provider.Fields(q, f).ToList()) ?? [])
                if (seen.Add(field.Key)) o.Add(field);
        return o;
    }

    /// <summary>Every collection a document can repeat over, by key.</summary>
    public static List<DocCollection> AllCollections(IQueryContext q)
    {
        var o = new List<DocCollection>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var provider in q.Services.All<IDocumentFields>())
            foreach (var c in provider.Collections)
                if (seen.Add(c.Key)) o.Add(c);
        return o;
    }

    /// <summary>
    /// A collection's items: those of the modules that declare it, then what the others add to them by <c>id</c> (architecture gives
    /// the platform's storeys their areas); a module that does not declare it adds no items of its own.
    /// </summary>
    public static List<Dictionary<string, string>> Items(IQueryContext q, string collection, DocFormat f)
    {
        var providers = q.Services.All<IDocumentFields>();
        var items = new List<Dictionary<string, string>>();
        foreach (var p in providers.Where(p => p.Collections.Any(c => c.Key == collection)))
            items.AddRange((Safe(() => p.Items(q, collection, f).ToList()) ?? []).Select(i => new Dictionary<string, string>(i, StringComparer.OrdinalIgnoreCase)));
        var byId = items.Where(i => i.ContainsKey("id")).GroupBy(i => i["id"]).ToDictionary(g => g.Key, g => g.First());
        foreach (var p in providers.Where(p => !p.Collections.Any(c => c.Key == collection)))
            foreach (var more in Safe(() => p.Items(q, collection, f).ToList()) ?? [])
                if (more.TryGetValue("id", out var id) && byId.TryGetValue(id, out var item))
                    foreach (var (k, v) in more) item.TryAdd(k, v);
        return items;
    }

    /// <summary>What a module failed to give leaves its fields missing, never the document unfilled.</summary>
    static T? Safe<T>(Func<T> f) where T : class
    {
        try { return f(); }
        catch (Exception e) when (e is ModelException or InvalidOperationException or ArgumentException or KeyNotFoundException or NullReferenceException) { return null; }
    }

    /// <summary>The fields of the document itself: its name, revision and status, its template.</summary>
    public static IEnumerable<DocField> OwnFields(Entity doc) =>
    [
        new("document.name", "Document name", doc.Name), new("document.revision", "Document revision", doc.Str("revision")),
        new("document.status", "Document status", doc.Str("status")), new("document.template", "Template", doc.Str("templateTitle") ?? doc.Str("template")),
    ];

    /// <summary>The document filled, and the fields and inputs left to complete (keys, in the order they first appear).</summary>
    public static (FilledDocument Document, IReadOnlyList<string> Missing) Fill(IQueryContext q, Entity doc)
    {
        var f = FormatOf(q.Model, doc);
        var filler = new Filler(q, doc, f);
        var page = filler.Page(doc.Attrs["page"] as JsonObject);
        filler.TextWidth = page.TextWidth;
        var blocks = filler.Blocks(doc.Attrs["blocks"] as JsonArray ?? [], "");
        var info = ProjectInfo.Of(q.Model);
        var meta = new DocMeta((string?)info["author"] ?? (string?)info["designer"] ?? "", doc.Str("templateTitle") ?? "", doc.Str("revision") ?? "", doc.Str("status") ?? "", f.Lang);
        return (new FilledDocument(doc.Name, page, blocks, meta), filler.Missing);
    }

    /// <summary>
    /// Some blocks filled in a document's context (its language, fields, inputs and page) as if inside the repeats' items
    /// <paramref name="scopes"/> name, outer first (the editor's <c>document.block</c>): the blocks, what they leave to complete,
    /// the items' values and how many items each repeat keeps (-1: a collection no module gives).
    /// </summary>
    public static (List<DocBlock> Blocks, IReadOnlyList<string> Missing, Dictionary<string, string> Scope, List<int> Counts) FillPart(IQueryContext q, Entity doc, JsonArray blocks, IReadOnlyList<DocScope> scopes)
    {
        var filler = new Filler(q, doc, FormatOf(q.Model, doc));
        filler.TextWidth = filler.Page(doc.Attrs["page"] as JsonObject).TextWidth;
        var counts = scopes.Select(s => filler.Enter(s.Collection, s.As, s.Where, s.Index)).ToList();
        var filled = filler.Blocks(blocks, "part");
        return (filled, filler.Missing, filler.ScopeValues(), counts);
    }

    /// <summary>The inputs a document declares, with their values now (its own, else the default).</summary>
    public static List<(DocInput Input, string? Value)> Inputs(Entity doc)
    {
        var values = doc.Attrs["inputs"] as JsonObject;
        var defs = doc.Attrs["inputDefs"] is JsonArray a ? a.Select(DocInput.From).OfType<DocInput>().ToList() : [];
        var o = defs.Select(d => (d, (string?)values?[d.Key] ?? d.Default)).ToList();
        // Values for inputs no longer declared (a template changed) are still shown, under their key.
        foreach (var (k, v) in values ?? [])
            if (defs.All(d => d.Key != k)) o.Add((new DocInput(k, k), (string?)v));
        return o;
    }

    sealed class Filler(IQueryContext q, Entity doc, DocFormat f)
    {
        readonly Dictionary<string, string?> _fields = Load(q, doc, f);
        readonly Dictionary<string, List<Dictionary<string, string>>> _items = [];
        readonly List<(string Prefix, IReadOnlyDictionary<string, string> Item)> _scopes = [];
        readonly List<string> _missing = [];
        readonly HashSet<string> _seen = [];
        readonly IReadOnlyDictionary<string, DocCollection> _collections = AllCollections(q).ToDictionary(c => c.Key);
        readonly IDocumentTables _tables = q.Services.Get<IDocumentTables>() ?? new ScheduleSourceTables();
        public double TextWidth { get; set; } = DocPage.A4.TextWidth;
        public IReadOnlyList<string> Missing => _missing;

        static Dictionary<string, string?> Load(IQueryContext q, Entity doc, DocFormat f)
        {
            var o = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in OwnFields(doc).Concat(AllFields(q, f))) o.TryAdd(field.Key, field.Value);
            return o;
        }

        void Miss(string key)
        {
            if (_seen.Add(key)) _missing.Add(key);
        }

        /// <summary>A field's value: the innermost repeat's item that has its prefix, else the project's; null (missing) when empty.</summary>
        string? Value(string key)
        {
            for (var i = _scopes.Count - 1; i >= 0; i--)
            {
                var (prefix, item) = _scopes[i];
                if (!key.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase)) continue;
                return item.TryGetValue(key[(prefix.Length + 1)..], out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;
            }
            return _fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
        }

        /// <summary>Text with its markup as runs: bold and italic toggled by ** and *, each {{key}} filled (page and pages left to the writer).</summary>
        public List<DocRun> Runs(string? text, bool record = true)
        {
            var o = new List<DocRun>();
            if (string.IsNullOrEmpty(text)) return o;
            var sb = new StringBuilder();
            bool bold = false, italic = false;
            void Flush()
            {
                if (sb.Length > 0) o.Add(new DocRun(sb.ToString(), bold, italic));
                sb.Clear();
            }
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '{' && i + 1 < text.Length && text[i + 1] == '{' && text.IndexOf("}}", i + 2, StringComparison.Ordinal) is var end and > 0)
                {
                    Flush();
                    var key = text[(i + 2)..end].Trim();
                    if (key is "page" or "pages") o.Add(new DocRun("", bold, italic, Field: key));
                    else if (Value(key) is { } v) o.Add(new DocRun(v, bold, italic));
                    else
                    {
                        o.Add(new DocRun(key, bold, italic, Missing: true));
                        if (record) Miss(key);
                    }
                    i = end + 1;
                    continue;
                }
                if (text[i] == '*')
                {
                    Flush();
                    if (i + 1 < text.Length && text[i + 1] == '*') { bold = !bold; i++; }
                    else italic = !italic;
                    continue;
                }
                sb.Append(text[i]);
            }
            Flush();
            return o;
        }

        /// <summary>Text filled as plain text (a where's value, a table's title): the markup dropped, missing fields empty.</summary>
        string Plain(string? text, bool record = false) => string.Concat(Runs(text, record).Where(r => !r.Missing && r.Field == null).Select(r => r.Text));

        public DocPage Page(JsonObject? p)
        {
            var (w, h) = Papers.GetValueOrDefault((string?)p?["size"] ?? "A4", Papers["A4"]);
            if ((string?)p?["orientation"] == "landscape") (w, h) = (h, w);
            var d = DocPage.A4;
            double M(int i, double fallback) => p?["margins"] is JsonArray { Count: 4 } a && Json.Number(a[i]) is { } v && v >= 0 && v < Math.Min(w, h) / 3 ? v : fallback;
            return new DocPage(w, h, M(0, d.Top), M(1, d.Right), M(2, d.Bottom), M(3, d.Left), Runs((string?)p?["header"]), Runs((string?)p?["footer"]), DocumentStyles.Parse(p?["style"]));
        }

        public List<DocBlock> Blocks(JsonArray blocks, string path)
        {
            var o = new List<DocBlock>();
            for (var i = 0; i < blocks.Count; i++)
                if (blocks[i] is JsonObject b) Block(b, path.Length == 0 ? $"{i}" : $"{path}.{i}", o);
            return o;
        }

        void Block(JsonObject b, string path, List<DocBlock> o)
        {
            switch ((string?)b["kind"])
            {
                case "heading":
                    o.Add(new DocHeading(Math.Clamp((int)(Json.Number(b["level"]) ?? 1), 1, 3), Runs((string?)b["text"])));
                    break;
                case "paragraph":
                    o.Add(new DocParagraph(Runs((string?)b["text"]), (string?)b["style"] is "caption" or "note" ? (string)b["style"]! : "normal"));
                    break;
                case "list":
                    var items = (b["items"] as JsonArray ?? []).Select(x => (IReadOnlyList<DocRun>)Runs((string?)x)).Where(r => r.Count > 0).ToList();
                    if (items.Count > 0) o.Add(new DocList(b["ordered"] is JsonValue ov && ov.TryGetValue<bool>(out var ord) && ord, items));
                    break;
                case "fields":
                    Fields(b, o);
                    break;
                case "table":
                    Table(b, o);
                    break;
                case "image":
                    Image(b, path, o);
                    break;
                case "repeat":
                    Repeat(b, path, o);
                    break;
                case "if":
                    var test = Value((string?)b["field"] ?? "");
                    var holds = !DocNumbers.IsEmpty(test);
                    if (b["not"] is JsonValue nv && nv.TryGetValue<bool>(out var not) && not) holds = !holds;
                    if ((holds ? b["blocks"] : b["else"]) is JsonArray branch) o.AddRange(Blocks(branch, path + (holds ? "" : "e")));
                    break;
                case "input":
                    Input(b, o);
                    break;
                case "toc":
                    o.Add(new DocToc((string?)b["title"] is { Length: > 0 } title ? Plain(title) : f.Lang.StartsWith("ro", StringComparison.OrdinalIgnoreCase) ? "Cuprins" : "Contents"));
                    break;
                case "pageBreak":
                    o.Add(new DocPageBreak());
                    break;
            }
        }

        void Fields(JsonObject b, List<DocBlock> o)
        {
            var skipEmpty = b["skipEmpty"] is JsonValue sv && sv.TryGetValue<bool>(out var skip) && skip;
            var rows = new List<IReadOnlyList<IReadOnlyList<DocRun>>>();
            foreach (var row in b["rows"] as JsonArray ?? [])
            {
                if (row is not JsonArray { Count: >= 2 } cells) continue;
                var value = Runs((string?)cells[1], record: !skipEmpty);
                if (skipEmpty && value.All(r => r.Missing || r.Text.Trim().Length == 0)) continue;
                rows.Add([Runs((string?)cells[0]), value]);
            }
            if (rows.Count > 0) o.Add(new DocTable([], rows, new HashSet<int>(), Plain: true, Title: (string?)b["title"] is { Length: > 0 } t ? Plain(t) : null));
        }

        /// <summary>Columns whose numbers are identifiers, never written again in the language's format (a room's number 1.01).</summary>
        static bool IsIdColumn(string header) =>
            header.Trim().ToLowerInvariant() is "number" or "no." or "nr." or "nr" or "mark" or "id" or "code" or "cod" or "număr" or "marca" or "marcă";

        void Table(JsonObject b, List<DocBlock> o)
        {
            var source = (string?)b["source"] ?? "";
            ScheduleGrid? grid;
            try { grid = _tables.Table(q, source); }
            catch (CommandException) { grid = null; }
            if (grid == null)
            {
                Miss("table:" + source);
                o.Add(new DocParagraph([new DocRun("table:" + source, Missing: true)]));
                return;
            }
            var cols = Enumerable.Range(0, grid.Columns.Count).ToList();
            if (b["columns"] is JsonArray wanted && wanted.Count > 0)
            {
                var picked = wanted.Select(x => (string?)x).OfType<string>()
                    .Select(name => grid.Columns.ToList().FindIndex(c => string.Equals(c.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase))).Where(i => i >= 0).Distinct().ToList();
                if (picked.Count > 0) cols = picked;
            }
            var rows = grid.Rows.ToList();
            var hasTotal = rows.Count > 0 && grid.Keys is { } keys && keys.Count == rows.Count && keys[^1] == null;
            var wantTotal = b["total"] is not JsonValue tv || !tv.TryGetValue<bool>(out var t) || t;
            if (hasTotal && !wantTotal) rows.RemoveAt(rows.Count - 1);
            string Cell(IReadOnlyList<string> row, int c) => c < row.Count ? row[c] ?? "" : "";
            var numeric = new HashSet<int>();
            for (var k = 0; k < cols.Count; k++)
            {
                var texts = rows.Select(r => Cell(r, cols[k])).Where(x => x.Trim().Length > 0).ToList();
                if (texts.Count > 0 && texts.All(x => DocNumbers.Parse(x) != null)) numeric.Add(k);
            }
            var cells = rows.Select(r => (IReadOnlyList<IReadOnlyList<DocRun>>)cols.Select((c, k) =>
            {
                var text = Cell(r, c);
                if (numeric.Contains(k) && !IsIdColumn(grid.Columns[c]) && DocNumbers.Parse(text) is { Decimals: > 0 }) text = DocNumbers.Reformat(text, f);
                return (IReadOnlyList<DocRun>)(text.Length > 0 ? [new DocRun(text)] : []);
            }).ToList()).ToList();
            var title = b.ContainsKey("title") ? ((string?)b["title"] is { Length: > 0 } bt ? Plain(bt) : null) : grid.Title;
            o.Add(new DocTable(cols.Select(c => grid.Columns[c]).ToList(), cells, numeric, TotalRow: hasTotal && wantTotal, Title: title));
        }

        void Image(JsonObject b, string path, List<DocBlock> o)
        {
            var viewId = Plain((string?)b["view"]);
            var key = $"img-{path}-{viewId}";
            var caption = (string?)b["caption"] is { Length: > 0 } c ? Plain(c, record: true) : null;
            DocImage? image;
            // Cropped to a named boundary of the view when it says one (decision 236); a boundary that is not the view's is to complete.
            var boundary = (string?)b["boundary"] is { Length: > 0 } bd ? Plain(bd) : null;
            if (boundary != null) key += $"-{boundary}";
            try { image = DocumentImages.Draw(q, viewId, Json.Number(b["scale"]), Json.Number(b["width"]), TextWidth, key, caption, boundary); }
            catch (CommandException) when (boundary != null && Views.Find(q.Model, viewId) != null)
            {
                Miss("boundary:" + boundary);
                o.Add(new DocParagraph([new DocRun("boundary:" + boundary, Missing: true)], "caption"));
                return;
            }
            catch (CommandException) { image = null; }
            if (image == null)
            {
                Miss("view:" + viewId);
                o.Add(new DocParagraph([new DocRun("view:" + viewId, Missing: true)], "caption"));
                return;
            }
            o.Add(image);
        }

        List<Dictionary<string, string>> ItemsOf(string over) => _items.TryGetValue(over, out var l) ? l : _items[over] = Items(q, over, f);

        /// <summary>The items of a collection a repeat's where keeps, its values filled in the repeats around it.</summary>
        List<Dictionary<string, string>> Matching(string over, JsonObject? whereObj)
        {
            var where = whereObj?.Select(kv => (kv.Key, Value: Plain((string?)kv.Value))).ToList() ?? [];
            // A where on "storey" given an id matches the item's "storeyId" too (the plan's own example).
            return ItemsOf(over).Where(item => !where.Any(w => !string.Equals(item.GetValueOrDefault(w.Key) ?? "", w.Value, StringComparison.Ordinal)
                && !string.Equals(item.GetValueOrDefault(w.Key + "Id") ?? "", w.Value, StringComparison.Ordinal))).ToList();
        }

        /// <summary>
        /// Stands inside a repeat's item (the editor's, <see cref="DocumentEditing"/>): the index-th of the items its where keeps
        /// (the last when there are fewer; none when it keeps none, its fields then missing). The number of items it keeps, or -1
        /// for a collection no module gives.
        /// </summary>
        public int Enter(string over, string? alias, JsonObject? where, int index)
        {
            if (!_collections.TryGetValue(over, out var collection)) return -1;
            var items = Matching(over, where);
            var prefix = alias is { Length: > 0 } ? alias : collection.ItemPrefix;
            var at = Math.Clamp(index, 0, Math.Max(items.Count - 1, 0));
            var scoped = items.Count == 0 ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(items[at], StringComparer.OrdinalIgnoreCase) { ["index"] = (at + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) };
            _scopes.Add((prefix, scoped));
            return items.Count;
        }

        /// <summary>The values of the items stood in, by their full key (room.name), the inner ones last.</summary>
        public Dictionary<string, string> ScopeValues()
        {
            var o = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (prefix, item) in _scopes)
                foreach (var (k, v) in item) o[$"{prefix}.{k}"] = v;
            return o;
        }

        void Repeat(JsonObject b, string path, List<DocBlock> o)
        {
            var over = (string?)b["over"] ?? "";
            if (!_collections.TryGetValue(over, out var collection))
            {
                Miss("collection:" + over);
                return;
            }
            var prefix = (string?)b["as"] is { Length: > 0 } alias ? alias : collection.ItemPrefix;
            var inner = b["blocks"] as JsonArray ?? [];
            var n = 0;
            foreach (var item in Matching(over, b["where"] as JsonObject))
            {
                n++;
                var scoped = new Dictionary<string, string>(item, StringComparer.OrdinalIgnoreCase) { ["index"] = n.ToString(System.Globalization.CultureInfo.InvariantCulture) };
                _scopes.Add((prefix, scoped));
                try { o.AddRange(Blocks(inner, $"{path}.{n}")); }
                finally { _scopes.RemoveAt(_scopes.Count - 1); }
            }
            if (n == 0 && b["empty"] is JsonArray none) o.AddRange(Blocks(none, path + "e"));
        }

        void Input(JsonObject b, List<DocBlock> o)
        {
            var key = (string?)b["key"] ?? "";
            var text = doc.Attrs["inputs"] is JsonObject values && values[key] is JsonValue v && v.TryGetValue<string>(out var own) ? own
                : (doc.Attrs["inputDefs"] as JsonArray)?.Select(DocInput.From).FirstOrDefault(d => d?.Key == key)?.Default;
            if (string.IsNullOrWhiteSpace(text))
            {
                Miss("input." + key);
                o.Add(new DocParagraph([new DocRun("input." + key, Missing: true)]));
                return;
            }
            var bullets = new List<IReadOnlyList<DocRun>>();
            void EndList()
            {
                if (bullets.Count > 0) o.Add(new DocList(false, [.. bullets]));
                bullets.Clear();
            }
            foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0) { EndList(); continue; }
                if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("• ", StringComparison.Ordinal)) { bullets.Add(Runs(line[2..].TrimStart())); continue; }
                EndList();
                o.Add(new DocParagraph(Runs(line)));
            }
            EndList();
        }
    }
}

/// <summary>The tables a product without the architecture module offers documents: each module's <see cref="IScheduleSource"/> by its kind.</summary>
public sealed class ScheduleSourceTables : IDocumentTables
{
    public IEnumerable<DocTableChoice> Choices(IQueryContext q) =>
        q.Services.All<IScheduleSource>().Select(s => new DocTableChoice("kind:" + s.Kind, s.Label));

    public ScheduleGrid? Table(IQueryContext q, string source) =>
        source.StartsWith("kind:", StringComparison.Ordinal) && q.Services.All<IScheduleSource>().FirstOrDefault(s => s.Kind == source[5..]) is { } src ? src.Grid(q) : null;
}
