using System.Text.Json.Nodes;

namespace Lapin.Platform.Cde.References;

/// <summary>
/// The built-in defaults of the reference documents (docs/plan-cde-references.md §3.2–3.3, decision 878): the folder tree in
/// Romanian and English, the document types with their default folders and extra fields, and the urbanism certificate's
/// checklist of permits. All of them are indicative: the user edits them per project (<c>references.setFolders</c>,
/// <c>references.setTypes</c>) and a company may bring its own. A project keeps none of them until it changes them: they are
/// what a project without its own copy reads. Folder ids stay when a folder is renamed; documents point at the id.
/// </summary>
public static class ReferenceDefaults
{
    static JsonObject Name(string ro, string en) => new() { ["ro"] = ro, ["en"] = en };

    static JsonObject Folder(string id, string ro, string en, string? derived = null, params JsonObject[] children)
    {
        var o = new JsonObject { ["id"] = id, ["name"] = Name(ro, en), ["children"] = new JsonArray([.. children]) };
        if (derived != null) o["derived"] = derived;
        return o;
    }

    /// <summary>
    /// The default tree (§3.2). Folders 05–06 hold no reference documents: they are the derived view of the issues (<c>derived</c>:
    /// the written documents, the sheets).
    /// </summary>
    public static JsonArray Folders() => new(
        Folder("admin", "00 Administrativ", "00 Administrative"),
        Folder("input", "01 Date de intrare", "01 Input data", null,
            Folder("input-cu", "Certificat de urbanism", "Urbanism certificate"),
            Folder("input-cf", "Extras CF și cadastru", "Land-book extract and cadastre"),
            Folder("input-topo", "Ridicare topografică", "Topographic survey"),
            Folder("input-geo", "Studiu geotehnic", "Geotechnical study"),
            Folder("input-audits", "Expertize și audituri", "Expert reports and audits")),
        Folder("permits", "02 Avize și acorduri", "02 Permits and agreements", null,
            Folder("permits-utilities", "Utilități", "Utilities"),
            Folder("permits-fire", "Securitate la incendiu", "Fire safety"),
            Folder("permits-health", "Sănătate publică", "Public health"),
            Folder("permits-environment", "Mediu", "Environment"),
            Folder("permits-other", "Altele", "Others")),
        Folder("authorisations", "03 Autorizații", "03 Authorisations"),
        Folder("models", "04 Modele de referință", "04 Reference models"),
        Folder("issued-documents", "05 Piese scrise emise", "05 Issued written documents", "documents"),
        Folder("issued-sheets", "06 Piese desenate emise", "06 Issued drawings", "sheets")
    );

    static JsonObject Field(string key, string ro, string en, string kind = "text") => new() { ["key"] = key, ["label"] = Name(ro, en), ["kind"] = kind };

    static JsonObject Type(string id, string ro, string en, string folder, int? validityMonths = null, bool checklist = false, params JsonObject[] fields)
    {
        var o = new JsonObject { ["id"] = id, ["name"] = Name(ro, en), ["folder"] = folder, ["fields"] = new JsonArray([.. fields]) };
        if (validityMonths is { } v) o["validityMonths"] = v;
        if (checklist) o["checklist"] = true;
        return o;
    }

    /// <summary>
    /// The default types (§3.1, §3.3) with their default folders, extra fields and default validity in months. The validity is
    /// indicative, used only to suggest a "valid until" the document itself does not write; what the document says counts.
    /// </summary>
    public static JsonArray Types() => new(
        Type("cu", "Certificat de urbanism", "Urbanism certificate", "input-cu", 12, checklist: true,
            fields: [Field("purpose", "Scopul emiterii", "Purpose"), Field("parcel", "Număr cadastral", "Cadastral number")]),
        Type("aviz", "Aviz", "Permit (aviz)", "permits-other", 12),
        Type("acord", "Acord", "Agreement", "permits-other", 12),
        Type("autorizatie", "Autorizație", "Authorisation", "authorisations", 12,
            fields: [Field("works", "Lucrări autorizate", "Works authorised")]),
        Type("cf", "Extras de carte funciară", "Land-book extract", "input-cf", null,
            fields: [Field("landBook", "Număr carte funciară", "Land-book number"), Field("parcel", "Număr cadastral", "Cadastral number")]),
        Type("cadastru", "Documentație cadastrală", "Cadastral documentation", "input-cf"),
        Type("studiu-geo", "Studiu geotehnic", "Geotechnical study", "input-geo"),
        Type("topo", "Ridicare topografică", "Topographic survey", "input-topo"),
        Type("expertiza", "Expertiză / audit", "Expert report / audit", "input-audits"),
        Type("tema", "Temă de proiectare", "Design brief", "admin"),
        Type("contract", "Contract", "Contract", "admin"),
        Type("corespondenta", "Corespondență", "Correspondence", "admin"),
        Type("model", "Model de referință", "Reference model", "models"),
        Type("altul", "Altul", "Other", "admin")
    );

    /// <summary>Each type's default folder, as <c>typeFolders</c> keeps it.</summary>
    public static JsonObject TypeFolders(JsonArray types)
    {
        var o = new JsonObject();
        foreach (var t in types.OfType<JsonObject>())
            if ((string?)t["id"] is { } id && (string?)t["folder"] is { } folder) o[id] = folder;
        return o;
    }

    static JsonObject Item(string key, string group, string ro, string en, string folder) =>
        new() { ["key"] = key, ["group"] = group, ["label"] = Name(ro, en), ["folder"] = folder };

    /// <summary>
    /// The permits an urbanism certificate commonly asks for (§3.3), as items to tick: indicative, never a list of what the law
    /// requires. Each names the folder its document goes into by default.
    /// </summary>
    public static JsonArray Checklist() => new(
        Item("water", "utilities", "Alimentare cu apă", "Water supply", "permits-utilities"),
        Item("sewerage", "utilities", "Canalizare", "Sewerage", "permits-utilities"),
        Item("electricity", "utilities", "Alimentare cu energie electrică", "Electricity", "permits-utilities"),
        Item("gas", "utilities", "Alimentare cu gaze naturale", "Natural gas", "permits-utilities"),
        Item("telecom", "utilities", "Telefonizare", "Telecommunications", "permits-utilities"),
        Item("waste", "utilities", "Salubritate", "Waste collection", "permits-utilities"),
        Item("transport", "utilities", "Transport urban", "Urban transport", "permits-utilities"),
        Item("fire", "safety", "Securitate la incendiu", "Fire safety", "permits-fire"),
        Item("civil-protection", "safety", "Protecție civilă", "Civil protection", "permits-fire"),
        Item("health", "health", "Sănătatea populației", "Public health", "permits-health"),
        Item("environment", "environment", "Actul administrativ al autorității pentru protecția mediului", "The environmental agency's decision", "permits-environment"),
        Item("study-geo", "studies", "Studiu geotehnic", "Geotechnical study", "input-geo"),
        Item("study-topo", "studies", "Ridicare topografică", "Topographic survey", "input-topo")
    );

    /// <summary>The issuers proposed before a project has its own (it remembers those its documents name).</summary>
    public static readonly string[] Issuers = ["Primăria", "Consiliul Județean", "ISU", "DSP", "APM", "OCPI", "Apă-Canal", "Distribuție Energie Electrică", "Distribuție Gaze Naturale", "Telekom", "Salubritate"];
}
