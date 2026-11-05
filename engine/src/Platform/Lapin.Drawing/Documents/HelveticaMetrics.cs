using Lapin.Drawing.Export;

namespace Lapin.Drawing.Documents;

/// <summary>
/// Helvetica's and Helvetica-Bold's advance widths (1/1000 em), as Adobe's public AFM files of the PDF standard 14
/// fonts give them, for the letters the PDF writer can encode: ASCII, Latin-1, WinAnsi's typographic signs, and the
/// Romanian letters it adds (ă as a, ș as s, ț as t; their capitals as A, S, T). Oblique has the upright's widths.
/// A letter the writer cannot encode is printed as '?', and measured as one.
/// </summary>
public static class HelveticaMetrics
{
    // 32 … 126.
    static readonly short[] RegularAscii =
    [
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556,
        1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556,
        333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556,
        556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584,
    ];
    static readonly short[] BoldAscii =
    [
        278, 333, 474, 556, 556, 889, 722, 238, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 333, 333, 584, 584, 584, 611,
        975, 722, 722, 722, 722, 667, 611, 778, 722, 278, 556, 722, 611, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 333, 278, 333, 584, 556,
        333, 556, 611, 556, 611, 556, 333, 611, 611, 278, 278, 556, 278, 889, 611, 611,
        611, 611, 389, 556, 333, 611, 556, 778, 556, 556, 500, 389, 280, 389, 584,
    ];
    // 160 … 255.
    static readonly short[] RegularLatin1 =
    [
        278, 333, 556, 556, 556, 556, 260, 556, 333, 737, 370, 556, 584, 333, 737, 333,
        400, 584, 333, 333, 333, 556, 537, 278, 333, 333, 365, 556, 834, 834, 834, 611,
        667, 667, 667, 667, 667, 667, 1000, 722, 667, 667, 667, 667, 278, 278, 278, 278,
        722, 722, 778, 778, 778, 778, 778, 584, 778, 722, 722, 722, 722, 667, 667, 611,
        556, 556, 556, 556, 556, 556, 889, 500, 556, 556, 556, 556, 278, 278, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 584, 611, 556, 556, 556, 556, 500, 556, 500,
    ];
    static readonly short[] BoldLatin1 =
    [
        278, 333, 556, 556, 556, 556, 280, 556, 333, 737, 370, 556, 584, 333, 737, 333,
        400, 584, 333, 333, 333, 611, 556, 278, 333, 333, 365, 556, 834, 834, 834, 611,
        722, 722, 722, 722, 722, 722, 1000, 722, 667, 667, 667, 667, 278, 278, 278, 278,
        722, 722, 778, 778, 778, 778, 778, 584, 778, 722, 722, 722, 722, 667, 667, 611,
        556, 556, 556, 556, 556, 556, 889, 556, 556, 556, 556, 556, 278, 278, 278, 278,
        611, 611, 611, 611, 611, 611, 611, 584, 611, 611, 611, 611, 611, 556, 611, 556,
    ];

    /// <summary>A letter's advance in 1/1000 em.</summary>
    public static int Width(char ch, bool bold)
    {
        ch = Base(ch);
        switch (ch)
        {
            case '„' or '”' or '“': return bold ? 500 : 333;
            case '’' or '‘': return bold ? 278 : 222;
            case '–' or '€': return 556;
            case '—' or '…': return 1000;
            case '•': return 350;
        }
        if (!PdfWriter.CanEncode(ch)) ch = '?';
        if (ch >= 32 && ch <= 126) return (bold ? BoldAscii : RegularAscii)[ch - 32];
        if (ch >= 160 && ch <= 255) return (bold ? BoldLatin1 : RegularLatin1)[ch - 160];
        return 0;   // a control character prints nothing
    }

    /// <summary>The letter a Romanian letter (and the minus, a tab) is measured as: its base letter.</summary>
    internal static char Base(char ch) => ch switch
    {
        'ă' => 'a', 'Ă' => 'A', 'ș' or 'ş' => 's', 'Ș' or 'Ş' => 'S', 'ț' or 'ţ' => 't', 'Ț' or 'Ţ' => 'T', '−' => '-', '\t' => ' ',
        _ => ch,
    };

    /// <summary>A text's advance in em.</summary>
    public static double Advance(string s, bool bold)
    {
        var w = 0;
        foreach (var ch in s) w += Width(ch, bold);
        return w / 1000.0;
    }

    /// <summary>A text's width in mm at a size in points.</summary>
    public static double Mm(string s, bool bold, double sizePt) => Advance(s, bold) * sizePt * 25.4 / 72;
}
