using System.Text;
using System.Text.RegularExpressions;

namespace Lapin.Ids;

/// <summary>
/// An XML Schema regular expression (XML Schema Part 2, Appendix F, which IDS's xs:pattern uses) as a .NET one. What differs:
/// an XSD pattern matches the whole value (no anchors: ^ and $ are plain characters); its . is any character but a line
/// end; \w is every character but punctuation, separators and others (so not the underscore), \s only space, tab and line
/// ends, \i and \c XML's name characters. Groups, classes (with subtraction, [a-z-[aeiou]]), quantifiers, \p{…} and blocks
/// (\p{IsBasicLatin}) read the same in both. .NET's own constructs that XSD has not ((?…), back references, \b, lazy
/// quantifiers) are refused, so a pattern means here what it means for every other checker.
/// </summary>
public static class XsdPattern
{
    // \i: the first character of an XML name; \c: any character of one (XML 1.0 NameStartChar / NameChar, by category).
    const string NameStart = @"\p{L}\p{Nl}_:";
    const string NameChar = @"\p{L}\p{Nl}\p{Mn}\p{Mc}\p{Nd}\p{Pc}·_:.\-";
    const string Word = @"\p{L}\p{M}\p{N}\p{S}";
    const string NotWord = @"\p{P}\p{Z}\p{C}";

    /// <summary>The .NET pattern, anchored at both ends; a FormatException saying what is wrong for a pattern XSD does not allow.</summary>
    public static string Translate(string xsd)
    {
        var o = new StringBuilder(@"\A(?:");
        var depth = 0; // inside a character class (nested for subtraction)
        for (var i = 0; i < xsd.Length; i++)
        {
            var c = xsd[i];
            if (c == '\\')
            {
                if (i + 1 >= xsd.Length) throw new FormatException("The pattern ends with a lone backslash.");
                var e = xsd[++i];
                switch (e)
                {
                    case 'n': case 'r': case 't': case '\\': case '|': case '.': case '-': case '^': case '?': case '*': case '+':
                    case '{': case '}': case '(': case ')': case '[': case ']':
                        o.Append('\\').Append(e); break;
                    case 'd': o.Append(@"\p{Nd}"); break;
                    case 'D': o.Append(depth > 0 ? throw new FormatException(@"\D inside a character class is not supported.") : @"\P{Nd}"); break;
                    case 's': o.Append(depth > 0 ? @" \t\n\r" : @"[ \t\n\r]"); break;
                    case 'S': o.Append(depth > 0 ? throw new FormatException(@"\S inside a character class is not supported.") : @"[^ \t\n\r]"); break;
                    case 'w': o.Append(depth > 0 ? Word : $"[{Word}]"); break;
                    case 'W': o.Append(depth > 0 ? NotWord : $"[{NotWord}]"); break;
                    case 'i': o.Append(depth > 0 ? NameStart : $"[{NameStart}]"); break;
                    case 'I': o.Append(depth > 0 ? throw new FormatException(@"\I inside a character class is not supported.") : $"[^{NameStart}]"); break;
                    case 'c': o.Append(depth > 0 ? NameChar : $"[{NameChar}]"); break;
                    case 'C': o.Append(depth > 0 ? throw new FormatException(@"\C inside a character class is not supported.") : $"[^{NameChar}]"); break;
                    case 'p': case 'P':
                    {
                        var close = xsd.IndexOf('}', i);
                        if (i + 1 >= xsd.Length || xsd[i + 1] != '{' || close < 0) throw new FormatException($@"\{e} needs a category in braces, as \{e}{{Lu}}.");
                        o.Append('\\').Append(e).Append(xsd, i + 1, close - i);
                        i = close;
                        break;
                    }
                    default: throw new FormatException($@"\{e} is not an escape of XML Schema patterns.");
                }
                continue;
            }
            if (depth > 0)
            {
                if (c == '[') { depth++; o.Append(c); continue; }
                if (c == ']') { depth--; o.Append(c); continue; }
                // A character of a class: as it is (.NET reads the class's ^ and - the same way), quoting only what .NET would read otherwise.
                o.Append(c);
                continue;
            }
            switch (c)
            {
                case '[': depth++; o.Append(c); break;
                case ']': throw new FormatException("A ] without its [.");
                case '^': o.Append(@"\^"); break;
                case '$': o.Append(@"\$"); break;
                case '.': o.Append(@"[^\n\r]"); break;
                case '(':
                    if (i + 1 < xsd.Length && xsd[i + 1] == '?') throw new FormatException("(? constructs are not part of XML Schema patterns.");
                    o.Append(c); break;
                case '*': case '+': case '?': case '}':
                    o.Append(c);
                    // A lazy or possessive quantifier (*?, +?, ??, {n}?) is .NET's, not XSD's.
                    if (i + 1 < xsd.Length && xsd[i + 1] is '?' or '+') throw new FormatException("Lazy quantifiers are not part of XML Schema patterns.");
                    break;
                default: o.Append(c); break;
            }
        }
        if (depth != 0) throw new FormatException("A [ without its ].");
        return o.Append(@")\z").ToString();
    }

    /// <summary>The pattern compiled (culture-invariant, a time limit so no pattern hangs the engine); a FormatException when it does not read.</summary>
    public static Regex Compile(string xsd)
    {
        var net = Translate(xsd);
        try { return new Regex(net, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2)); }
        catch (ArgumentException ex) { throw new FormatException($"The pattern „{xsd}” does not read: {ex.Message}"); }
    }

    /// <summary>Whether the value matches; a pattern too slow for its value (catastrophic backtracking) does not.</summary>
    public static bool Matches(Regex r, string value)
    {
        try { return r.IsMatch(value); }
        catch (RegexMatchTimeoutException) { return false; }
    }
}
