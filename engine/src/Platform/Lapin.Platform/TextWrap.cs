namespace Lapin.Platform;

/// <summary>Text broken into lines: on its newlines, and each paragraph word-wrapped to a width (a text note's box, a drafting table's cell).</summary>
public static class TextWrap
{
    /// <summary>Split text on newlines and optionally word-wrap each paragraph to <paramref name="maxW"/> (in the units of <paramref name="charW"/>).</summary>
    public static List<string> Lines(string body, double charW, double? maxW)
    {
        var paras = body.Replace("\r\n", "\n").Split('\n');
        if (maxW is not > 0) return [.. paras];
        var maxChars = Math.Max(1, (int)Math.Floor(maxW.Value / charW));
        var result = new List<string>();
        foreach (var para in paras)
        {
            if (para.Length <= maxChars) { result.Add(para); continue; }
            var cur = "";
            foreach (var word in para.Split(' '))
            {
                if (cur.Length == 0) cur = word;
                else if (cur.Length + 1 + word.Length <= maxChars) cur += " " + word;
                else { result.Add(cur); cur = word; }
            }
            result.Add(cur);
        }
        return result;
    }
}
