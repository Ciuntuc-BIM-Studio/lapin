namespace Lapin.Platform;

/// <summary>
/// The few words the engine writes on a drawing by itself (a legend's heading, line roles and types), in the languages of
/// the interface (en, ro, es, de, fr, da, it, pl, pt, lo, zh). A drawing is printed, so its language is stored with what
/// draws it (a legend's <c>lang</c>, set from the interface's language when it is placed), never taken from whoever looks at
/// it; a word or a language not here stays English.
/// </summary>
public static class DrawingWords
{
    public static readonly string[] Languages = ["en", "ro", "es", "de", "fr", "da", "it", "pl", "pt", "lo", "zh"];

    static readonly Dictionary<string, Dictionary<string, string>> Words = new()
    {
        ["LEGEND"] = new() { ["ro"] = "LEGENDĂ", ["es"] = "LEYENDA", ["de"] = "LEGENDE", ["fr"] = "LÉGENDE", ["da"] = "SIGNATURFORKLARING", ["it"] = "LEGENDA", ["pl"] = "LEGENDA", ["pt"] = "LEGENDA", ["lo"] = "ຄຳອະທິບາຍສັນຍາລັກ", ["zh"] = "图例" },
        ["LINES"] = new() { ["ro"] = "LINII", ["es"] = "LÍNEAS", ["de"] = "LINIEN", ["fr"] = "TRAITS", ["da"] = "LINJER", ["it"] = "LINEE", ["pl"] = "LINIE", ["pt"] = "LINHAS", ["lo"] = "ເສັ້ນ", ["zh"] = "线型" },
        ["Cut"] = new() { ["ro"] = "Secționat", ["es"] = "Cortado", ["de"] = "Geschnitten", ["fr"] = "Coupé", ["da"] = "Snit", ["it"] = "Sezionato", ["pl"] = "Przekrój", ["pt"] = "Cortado", ["lo"] = "ຕັດ", ["zh"] = "剖切" },
        ["Seen"] = new() { ["ro"] = "Văzut", ["es"] = "Visto", ["de"] = "Ansicht", ["fr"] = "Vu", ["da"] = "Set", ["it"] = "Visto", ["pl"] = "Widok", ["pt"] = "Visto", ["lo"] = "ເຫັນ", ["zh"] = "可见" },
        ["Hidden"] = new() { ["ro"] = "Ascuns", ["es"] = "Oculto", ["de"] = "Verdeckt", ["fr"] = "Caché", ["da"] = "Skjult", ["it"] = "Nascosto", ["pl"] = "Niewidoczne", ["pt"] = "Oculto", ["lo"] = "ເຊື່ອງ", ["zh"] = "隐藏" },
        ["Overhead"] = new() { ["ro"] = "Deasupra", ["es"] = "Superior", ["de"] = "Darüber", ["fr"] = "Au-dessus", ["da"] = "Over", ["it"] = "Sovrastante", ["pl"] = "Nad", ["pt"] = "Acima", ["lo"] = "ເທິງຫົວ", ["zh"] = "上方" },
        ["Beyond"] = new() { ["ro"] = "Dincolo", ["es"] = "Más allá", ["de"] = "Dahinter", ["fr"] = "Au-delà", ["da"] = "Bagved", ["it"] = "Oltre", ["pl"] = "Dalej", ["pt"] = "Além", ["lo"] = "ໄກອອກໄປ", ["zh"] = "远处" },
        ["Membrane"] = new() { ["ro"] = "Membrană", ["es"] = "Membrana", ["de"] = "Membran", ["fr"] = "Membrane", ["da"] = "Membran", ["it"] = "Membrana", ["pl"] = "Membrana", ["pt"] = "Membrana", ["lo"] = "ເຍື່ອ", ["zh"] = "防水膜" },
        ["continuous"] = new() { ["ro"] = "continuă", ["es"] = "continua", ["de"] = "durchgehend", ["fr"] = "continu", ["da"] = "fuldt optrukket", ["it"] = "continua", ["pl"] = "ciągła", ["pt"] = "contínua", ["lo"] = "ຕໍ່ເນື່ອງ", ["zh"] = "实线" },
        ["dashed"] = new() { ["ro"] = "întreruptă", ["es"] = "discontinua", ["de"] = "gestrichelt", ["fr"] = "tirets", ["da"] = "stiplet", ["it"] = "tratteggiata", ["pl"] = "kreskowa", ["pt"] = "tracejada", ["lo"] = "ເສັ້ນປະ", ["zh"] = "虚线" },
        ["dotted"] = new() { ["ro"] = "punctată", ["es"] = "de puntos", ["de"] = "gepunktet", ["fr"] = "pointillé", ["da"] = "prikket", ["it"] = "punteggiata", ["pl"] = "kropkowa", ["pt"] = "pontilhada", ["lo"] = "ເສັ້ນຈຸດ", ["zh"] = "点线" },
        ["dashdot"] = new() { ["ro"] = "linie-punct", ["es"] = "trazo y punto", ["de"] = "Strichpunkt", ["fr"] = "trait mixte", ["da"] = "streg-prik", ["it"] = "tratto e punto", ["pl"] = "kreska-kropka", ["pt"] = "traço e ponto", ["lo"] = "ຂີດ-ຈຸດ", ["zh"] = "点划线" },
        ["dashes"] = new() { ["ro"] = "liniuțe", ["es"] = "trazos", ["de"] = "Striche", ["fr"] = "tirets", ["da"] = "streger", ["it"] = "tratti", ["pl"] = "kreski", ["pt"] = "traços", ["lo"] = "ຂີດ", ["zh"] = "短划" },
        ["(none on this sheet's views)"] = new() { ["ro"] = "(niciunul în vederile acestei planșe)", ["es"] = "(ninguno en las vistas de esta lámina)", ["de"] = "(keine in den Ansichten dieses Plans)", ["fr"] = "(aucun dans les vues de cette feuille)", ["da"] = "(ingen i dette arks visninger)", ["it"] = "(nessuno nelle viste di questa tavola)", ["pl"] = "(brak w widokach tego arkusza)", ["pt"] = "(nenhum nas vistas desta prancha)", ["lo"] = "(ບໍ່ມີໃນມຸມມອງຂອງແຜ່ນນີ້)", ["zh"] = "（本图纸的视图中没有）" },
    };

    /// <summary>A word in a language (an unknown language, or a word without it: the English).</summary>
    public static string Of(string word, string? lang) =>
        lang != null && lang != "en" && Words.TryGetValue(word, out var by) && by.TryGetValue(lang, out var w) ? w : word;

    /// <summary>A language code the drawing words know, or null.</summary>
    public static string? Known(string? lang) => lang != null && Languages.Contains(lang) ? lang : null;
}
