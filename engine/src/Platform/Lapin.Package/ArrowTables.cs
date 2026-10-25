using System.Text.Json;
using System.Text.Json.Nodes;
using Apache.Arrow;
using Apache.Arrow.Ipc;
using Apache.Arrow.Types;
using Lapin.Model;

namespace Lapin.Package;

/// <summary>
/// Entities and relationships as Arrow IPC tables. A table holds entities of
/// one class; every attribute becomes a typed column (Float64, Utf8, Boolean,
/// a list of Float64), and an attribute of any other shape (a list of
/// layers) a Utf8 column of JSON, marked in its field's metadata. A table is
/// self-describing: reading needs no schema registry, so the parts of a
/// module the reader does not have come back intact.
/// </summary>
public static class ArrowTables
{
    const string AttrPrefix = "attr:";
    const string KindKey = "lapin.kind";

    enum Kind { Number, Text, Bool, Numbers, Json }

    // ------------------------------------------------------------ entities

    public static byte[] WriteEntities(IReadOnlyList<Entity> rows, Func<string, int> orderOf)
    {
        var keys = rows.SelectMany(e => e.Attrs.Select(kv => kv.Key)).Distinct().Order(StringComparer.Ordinal).ToList();
        var fields = new List<Field>
        {
            new("id", StringType.Default, false),
            new("class", StringType.Default, false),
            new("name", StringType.Default, false),
            new("predefinedType", StringType.Default, true),
            new("order", Int32Type.Default, false),
        };
        var arrays = new List<IArrowArray>
        {
            Strings(rows.Select(e => e.Id)),
            Strings(rows.Select(e => e.Class)),
            Strings(rows.Select(e => e.Name)),
            Strings(rows.Select(e => e.PredefinedType)),
            Ints(rows.Select(e => orderOf(e.Id))),
        };
        foreach (var key in keys)
        {
            var values = rows.Select(e => e.Attrs[key]).ToList();
            var kind = KindOf(values);
            fields.Add(new Field(AttrPrefix + key, TypeOf(kind), true, [new(KindKey, kind.ToString())]));
            arrays.Add(Column(kind, values));
        }
        return Write(new Schema(fields, null), arrays, rows.Count);
    }

    public static List<(Entity Entity, int Order)> ReadEntities(byte[] bytes)
    {
        var o = new List<(Entity, int)>();
        foreach (var batch in Read(bytes))
        {
            var id = (StringArray)batch.Column("id");
            var cls = (StringArray)batch.Column("class");
            var name = (StringArray)batch.Column("name");
            var pre = (StringArray)batch.Column("predefinedType");
            var order = (Int32Array)batch.Column("order");
            var attrs = batch.Schema.FieldsList
                .Select((f, i) => (f, i))
                .Where(x => x.f.Name.StartsWith(AttrPrefix, StringComparison.Ordinal))
                .Select(x => (Key: x.f.Name[AttrPrefix.Length..], Kind: Enum.Parse<Kind>(x.f.Metadata?.GetValueOrDefault(KindKey) ?? nameof(Kind.Json)), Array: batch.Column(x.i)))
                .ToList();
            for (var r = 0; r < batch.Length; r++)
            {
                var a = new JsonObject();
                foreach (var (key, kind, array) in attrs)
                    if (Value(kind, array, r) is { } v) a[key] = v;
                o.Add((new Entity(id.GetString(r), cls.GetString(r), name.GetString(r) ?? "", a, pre.IsNull(r) ? null : pre.GetString(r)), order.GetValue(r)!.Value));
            }
        }
        return o;
    }

    // ------------------------------------------------------------ relations

    public static byte[] WriteRelations(IReadOnlyList<Relation> rows, Func<string, int> orderOf)
    {
        var related = new ListArray.Builder(StringType.Default);
        var values = (StringArray.Builder)related.ValueBuilder;
        foreach (var r in rows)
        {
            related.Append();
            foreach (var x in r.Related) values.Append(x);
        }
        var schema = new Schema(
        [
            new Field("id", StringType.Default, false),
            new Field("class", StringType.Default, false),
            new Field("relating", StringType.Default, false),
            new Field("related", new ListType(StringType.Default), false),
            new Field("attrs", StringType.Default, true),
            new Field("order", Int32Type.Default, false),
        ], null);
        return Write(schema,
        [
            Strings(rows.Select(r => r.Id)),
            Strings(rows.Select(r => r.Class)),
            Strings(rows.Select(r => r.Relating)),
            related.Build(),
            Strings(rows.Select(r => r.Attrs?.ToJsonString(Json.Options))),
            Ints(rows.Select(r => orderOf(r.Id))),
        ], rows.Count);
    }

    public static List<(Relation Relation, int Order)> ReadRelations(byte[] bytes)
    {
        var o = new List<(Relation, int)>();
        foreach (var batch in Read(bytes))
        {
            var id = (StringArray)batch.Column("id");
            var cls = (StringArray)batch.Column("class");
            var relating = (StringArray)batch.Column("relating");
            var related = (ListArray)batch.Column("related");
            var attrs = (StringArray)batch.Column("attrs");
            var order = (Int32Array)batch.Column("order");
            for (var r = 0; r < batch.Length; r++)
            {
                var list = (StringArray)related.GetSlicedValues(r);
                var ids = Enumerable.Range(0, list.Length).Select(i => list.GetString(i)).ToList();
                var a = attrs.IsNull(r) ? null : JsonNode.Parse(attrs.GetString(r)) as JsonObject;
                o.Add((new Relation(id.GetString(r), cls.GetString(r), relating.GetString(r), ids, a), order.GetValue(r)!.Value));
            }
        }
        return o;
    }

    // ------------------------------------------------------------ columns

    static Kind KindOf(IReadOnlyList<JsonNode?> values)
    {
        var present = values.Where(v => v != null).ToList();
        if (present.Count == 0) return Kind.Json;
        if (present.All(v => v is JsonValue x && x.GetValueKind() == JsonValueKind.Number)) return Kind.Number;
        if (present.All(v => v is JsonValue x && x.GetValueKind() == JsonValueKind.String)) return Kind.Text;
        if (present.All(v => v is JsonValue x && x.GetValueKind() is JsonValueKind.True or JsonValueKind.False)) return Kind.Bool;
        if (present.All(v => v is JsonArray a && a.All(n => n is JsonValue x && x.GetValueKind() == JsonValueKind.Number))) return Kind.Numbers;
        return Kind.Json;
    }

    static IArrowType TypeOf(Kind k) => k switch
    {
        Kind.Number => DoubleType.Default,
        Kind.Bool => BooleanType.Default,
        Kind.Numbers => new ListType(DoubleType.Default),
        _ => StringType.Default,
    };

    static IArrowArray Column(Kind kind, IReadOnlyList<JsonNode?> values)
    {
        switch (kind)
        {
            case Kind.Number:
            {
                var b = new DoubleArray.Builder();
                foreach (var v in values) if (v == null) b.AppendNull(); else b.Append(Json.Number(v)!.Value);
                return b.Build();
            }
            case Kind.Bool:
            {
                var b = new BooleanArray.Builder();
                foreach (var v in values) if (v == null) b.AppendNull(); else b.Append(v.GetValue<bool>());
                return b.Build();
            }
            case Kind.Numbers:
            {
                var b = new ListArray.Builder(DoubleType.Default);
                var items = (DoubleArray.Builder)b.ValueBuilder;
                foreach (var v in values)
                {
                    if (v is not JsonArray a) { b.AppendNull(); continue; }
                    b.Append();
                    foreach (var n in a) items.Append(Json.Number(n)!.Value);
                }
                return b.Build();
            }
            case Kind.Text:
                return Strings(values.Select(v => v?.GetValue<string>()));
            default:
                return Strings(values.Select(v => v?.ToJsonString(Json.Options)));
        }
    }

    static JsonNode? Value(Kind kind, IArrowArray array, int row)
    {
        if (array.IsNull(row)) return null;
        switch (kind)
        {
            case Kind.Number: return JsonValue.Create(((DoubleArray)array).GetValue(row)!.Value);
            case Kind.Bool: return JsonValue.Create(((BooleanArray)array).GetValue(row)!.Value);
            case Kind.Numbers:
            {
                var items = (DoubleArray)((ListArray)array).GetSlicedValues(row);
                var o = new JsonArray();
                for (var i = 0; i < items.Length; i++) o.Add((JsonNode)items.GetValue(i)!.Value);
                return o;
            }
            case Kind.Text: return JsonValue.Create(((StringArray)array).GetString(row));
            default: return JsonNode.Parse(((StringArray)array).GetString(row));
        }
    }

    static Int32Array Ints(IEnumerable<int> values)
    {
        var b = new Int32Array.Builder();
        foreach (var v in values) b.Append(v);
        return b.Build();
    }

    static StringArray Strings(IEnumerable<string?> values)
    {
        var b = new StringArray.Builder();
        foreach (var v in values) if (v == null) b.AppendNull(); else b.Append(v);
        return b.Build();
    }

    // ------------------------------------------------------------ IPC

    static byte[] Write(Schema schema, IReadOnlyList<IArrowArray> arrays, int length)
    {
        using var ms = new MemoryStream();
        using (var w = new ArrowFileWriter(ms, schema, leaveOpen: true))
        {
            w.WriteRecordBatch(new RecordBatch(schema, arrays, length));
            w.WriteEnd();
        }
        return ms.ToArray();
    }

    static IEnumerable<RecordBatch> Read(byte[] bytes)
    {
        using var r = new ArrowFileReader(new MemoryStream(bytes));
        while (r.ReadNextRecordBatch() is { } batch) yield return batch;
    }
}
