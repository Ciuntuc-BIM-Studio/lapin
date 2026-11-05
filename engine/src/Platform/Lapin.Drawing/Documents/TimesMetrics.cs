using Lapin.Drawing.Export;

namespace Lapin.Drawing.Documents;

/// <summary>
/// Times-Roman's and Times-Bold's advance widths (1/1000 em), as Adobe's public AFM files of the PDF standard 14 fonts give
/// them, for the same letters as <see cref="HelveticaMetrics"/> (WinAnsi, and the Romanian letters measured as their base
/// letters). Italic is the upright slanted, so it has the upright's widths.
/// </summary>
public static class TimesMetrics
{
    // 32 … 126.
    static readonly short[] RegularAscii =
    [
        250, 333, 408, 500, 500, 833, 778, 180, 333, 333, 500, 564, 250, 333, 250, 278,
        500, 500, 500, 500, 500, 500, 500, 500, 500, 500, 278, 278, 564, 564, 564, 444,
        921, 722, 667, 667, 722, 611, 556, 722, 722, 333, 389, 722, 611, 889, 722, 722,
        556, 722, 667, 556, 611, 722, 722, 944, 722, 722, 611, 333, 278, 333, 469, 500,
        333, 444, 500, 444, 500, 444, 333, 500, 500, 278, 278, 500, 278, 778, 500, 500,
        500, 500, 333, 389, 278, 500, 500, 722, 500, 500, 444, 480, 200, 480, 541,
    ];
    static readonly short[] BoldAscii =
    [
        250, 333, 555, 500, 500, 1000, 833, 278, 333, 333, 500, 570, 250, 333, 250, 278,
        500, 500, 500, 500, 500, 500, 500, 500, 500, 500, 333, 333, 570, 570, 570, 500,
        930, 722, 667, 722, 722, 667, 611, 778, 778, 389, 500, 778, 667, 944, 722, 778,
        611, 778, 722, 556, 667, 722, 722, 1000, 722, 722, 667, 333, 278, 333, 581, 500,
        333, 500, 556, 444, 556, 444, 333, 500, 556, 278, 333, 556, 278, 833, 556, 500,
        556, 556, 444, 389, 333, 556, 500, 722, 500, 500, 444, 394, 220, 394, 520,
    ];
    // 160 … 255.
    static readonly short[] RegularLatin1 =
    [
        250, 333, 500, 500, 500, 500, 200, 500, 333, 760, 276, 500, 564, 333, 760, 333,
        400, 564, 300, 300, 333, 500, 453, 250, 333, 300, 310, 500, 750, 750, 750, 444,
        722, 722, 722, 722, 722, 722, 889, 667, 611, 611, 611, 611, 333, 333, 333, 333,
        722, 722, 722, 722, 722, 722, 722, 564, 722, 722, 722, 722, 722, 722, 556, 500,
        444, 444, 444, 444, 444, 444, 667, 444, 444, 444, 444, 444, 278, 278, 278, 278,
        500, 500, 500, 500, 500, 500, 500, 564, 500, 500, 500, 500, 500, 500, 500, 500,
    ];
    static readonly short[] BoldLatin1 =
    [
        250, 333, 500, 500, 500, 500, 220, 500, 333, 747, 300, 500, 570, 333, 747, 333,
        400, 570, 300, 300, 333, 556, 540, 250, 333, 300, 330, 500, 750, 750, 750, 500,
        722, 722, 722, 722, 722, 722, 1000, 722, 667, 667, 667, 667, 389, 389, 389, 389,
        722, 722, 778, 778, 778, 778, 778, 570, 778, 722, 722, 722, 722, 722, 611, 556,
        500, 500, 500, 500, 500, 500, 722, 444, 444, 444, 444, 444, 278, 278, 278, 278,
        500, 556, 500, 500, 500, 500, 500, 570, 500, 556, 556, 556, 556, 500, 556, 500,
    ];

    /// <summary>A letter's advance in 1/1000 em.</summary>
    public static int Width(char ch, bool bold)
    {
        ch = HelveticaMetrics.Base(ch);
        switch (ch)
        {
            case '„' or '”' or '“': return bold ? 500 : 444;
            case '’' or '‘': return 333;
            case '–' or '€': return 500;
            case '—' or '…': return 1000;
            case '•': return 350;
        }
        if (!PdfWriter.CanEncode(ch)) ch = '?';
        if (ch >= 32 && ch <= 126) return (bold ? BoldAscii : RegularAscii)[ch - 32];
        if (ch >= 160 && ch <= 255) return (bold ? BoldLatin1 : RegularLatin1)[ch - 160];
        return 0;
    }

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
