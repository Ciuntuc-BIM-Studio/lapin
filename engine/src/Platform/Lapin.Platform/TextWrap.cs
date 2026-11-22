namespace Lapin.Platform;

/// <summary>Text broken into lines: on its newlines, and each paragraph word-wrapped to a width (a text note's box, a drafting table's cell).</summary>
public static class TextWrap
{
    /// <summary>Split text on newlines and optionally word-wrap each paragraph to <paramref name="maxW"/> (in the units of <paramref name="charW"/>), every character as wide.</summary>
    public static List<string> Lines(string body, double charW, double? maxW) => Lines(body, (Func<string, double>)(s => s.Length * charW), maxW);

    /// <summary>
    /// Split text on newlines and optionally word-wrap each paragraph to <paramref name="maxW"/>, each line as wide as
    /// <paramref name="width"/> measures it (a drawing's: <see cref="Lapin.Drawing.TextMeasure"/>). A word wider than the
    /// width stands alone on its line.
    /// </summary>
    public static List<string> Lines(string body, Func<string, double> width, double? maxW)
    {
        var paras = body.Replace("\r\n", "\n").Split('\n');
        if (maxW is not > 0) return [.. paras];
        var max = maxW.Value + 1e-9;
        var result = new List<string>();
        foreach (var para in paras)
        {
            if (width(para) <= max) { result.Add(para); continue; }
            var cur = "";
            foreach (var word in para.Split(' '))
            {
                if (cur.Length == 0) cur = word;
                else if (width(cur + " " + word) <= max) cur += " " + word;
                else { result.Add(cur); cur = word; }
            }
            result.Add(cur);
        }
        return result;
    }
}
