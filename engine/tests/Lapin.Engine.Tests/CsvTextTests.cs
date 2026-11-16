using Lapin.Platform;
using Lapin.Platform.ProfileLibraries;

namespace Lapin.Engine.Tests;

/// <summary>The one CSV reader and writer (audit 15.11.2026, D7): what spreadsheets write reads back cell for cell.</summary>
public class CsvTextTests
{
    [Fact]
    public void The_separator_is_the_one_the_first_line_uses_most()
    {
        Assert.Equal(';', CsvText.SeparatorOf("a;b;c\n1,5;2;3"));
        Assert.Equal(',', CsvText.SeparatorOf("a,b,c\n1;2;3"));
        Assert.Equal('\t', CsvText.SeparatorOf("a\tb\n"));
        Assert.Equal(';', CsvText.SeparatorOf("single"));
        Assert.Equal(',', CsvText.SeparatorOf("\n\na,b\n"));
    }

    [Fact]
    public void Quotes_hold_separators_quotes_and_line_breaks()
    {
        var rows = CsvText.Read("Name;Note\n\"A; B\";\"two\nlines\"\r\n\"say \"\"hi\"\"\";x\rlast;1");
        Assert.Equal(4, rows.Count);
        Assert.Equal(["Name", "Note"], rows[0]);
        Assert.Equal(["A; B", "two\nlines"], rows[1]);
        Assert.Equal(["say \"hi\"", "x"], rows[2]);
        Assert.Equal(["last", "1"], rows[3]);
    }

    [Fact]
    public void A_quote_inside_a_cell_is_kept_as_it_is()
    {
        var rows = CsvText.Read("a;b\n5\" pipe;2\n");
        Assert.Equal(["5\" pipe", "2"], rows[1]);
    }

    [Fact]
    public void Empty_cells_and_blank_lines_are_kept()
    {
        var rows = CsvText.Read("a;;c\n\n;\n\"\"");
        Assert.Equal(["a", "", "c"], rows[0]);
        Assert.Equal([""], rows[1]);
        Assert.Equal(["", ""], rows[2]);
        Assert.Equal([""], rows[3]);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("a;b", "\"a;b\"")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("a\tb", "\"a\tb\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("two\nlines", "\"two\nlines\"")]
    [InlineData("cr\rhere", "\"cr\rhere\"")]
    public void A_field_is_quoted_when_it_must_be(string? cell, string written) => Assert.Equal(written, CsvText.Field(cell));

    [Theory]
    [InlineData(';')]
    [InlineData(',')]
    [InlineData('\t')]
    public void What_is_written_reads_back(char sep)
    {
        string[][] table = [["Name", "Note", "Size"], ["A; B, C", "two\r\nlines", "\"5\""], ["", "x", "1,5"]];
        var text = string.Concat(table.Select(r => CsvText.Line(r, sep) + "\n"));
        var back = CsvText.Read(text);
        Assert.Equal(table.Length, back.Count);
        for (var i = 0; i < table.Length; i++) Assert.Equal(table[i], back[i]);
    }

    [Fact]
    public void Tables_read_csv_by_the_one_reader() =>
        Assert.Equal([["a", "b"], ["1", "x;y"]], Tables.Csv("a;b\n1;\"x;y\"\n"));

    [Fact]
    public void A_profile_named_on_two_lines_with_a_semicolon_stays_one_cell()
    {
        var p = BuiltInProfiles.Find("IPE 300")! with { Name = "IPE 300;\nspecial", Family = "say \"IPE\"" };
        var rows = Tables.Csv(ProfileTables.Csv([p]));
        Assert.Equal(2, rows.Count(r => r.Any(c => c.Length > 0)));
        Assert.Equal("IPE 300;\nspecial", rows[1][0]);
        Assert.Equal("say \"IPE\"", rows[1][1]);
        Assert.Equal(rows[0].Count, rows[1].Count);
    }

    [Fact]
    public void A_lintel_catalogues_quoted_cells_read_and_its_lines_keep_their_own_separator()
    {
        var (products, skipped) = Lapin.Architecture.LintelProducts.Parse("code;width;height;lengths\n\"L;1\";\"115\";71;1000|1250\nL2,175,71,1000\n");
        Assert.Equal(["L;1", "L2"], products.Select(p => p.Code));
        Assert.Equal(0.115, products[0].Width, 9);
        Assert.Empty(skipped);
    }
}
