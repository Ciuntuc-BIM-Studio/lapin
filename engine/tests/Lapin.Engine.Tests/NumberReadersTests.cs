using Lapin.Architecture;
using Lapin.Architecture.Schedules;
using Lapin.Drawing.Export;
using Lapin.Graphs;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Each place that reads a number from text, on the inputs it reads today (audit 15.11.2026, D2): they all go through
/// <see cref="Lapin.Model.Numbers"/> and keep their meaning; the cases marked fixed read differently than before, as a bug.
/// </summary>
public class NumberReadersTests
{
    [Theory]
    [InlineData("12", 12)]
    [InlineData("+3.20", 3.2)]
    [InlineData("1:100", 1)]
    [InlineData("12 m", 12)]
    [InlineData("12,5", 12.5)]
    [InlineData("−3", -3)]
    [InlineData("1e3", 1000)]
    [InlineData("Level 2", 2)]
    [InlineData("1 250", 1250)] // fixed: was 1
    [InlineData("1.234,5", 1234.5)] // fixed: was 1.234
    public void A_live_texts_value_reads_its_first_number(string text, double value) => Assert.Equal(value, LiveText.Number(text)!.Value, 9);

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    public void A_live_texts_value_without_a_number_is_none(string text) => Assert.Null(LiveText.Number(text));

    [Theory]
    [InlineData("1,5", 1.5)]
    [InlineData("1,200", 1.2)]
    [InlineData("1.234,5", 1234.5)]
    [InlineData("1,234.5", 1234.5)]
    [InlineData("1 250", 1250)]
    [InlineData("1 250,5", 1250.5)]
    [InlineData("-2.5", -2.5)]
    [InlineData("1e3", 1000)]
    [InlineData("1'250", 1250)] // fixed: was none
    [InlineData("−3", -3)] // fixed: was none
    [InlineData("1.234.567", 1234567)] // fixed: was none
    public void A_sheets_cell_reads_as_a_sheet_writes_it(string text, double value) => Assert.Equal(value, XlsxReader.Number(text)!.Value, 9);

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("12 m")]
    [InlineData("NaN")]
    public void A_sheets_cell_that_is_no_number_is_none(string text) => Assert.Null(XlsxReader.Number(text));

    [Theory]
    [InlineData("12,5 m²", 12.5)]
    [InlineData("40 %", 40)]
    [InlineData("-3.25", -3.25)]
    [InlineData("1 250", 1250)]
    [InlineData("12.", 12)]
    [InlineData("1.234,5 m", 1234.5)] // fixed: was none
    public void A_charts_cell_reads_its_leading_number(string text, double value) => Assert.Equal(value, ScheduleCharts.Number(text)!.Value, 9);

    [Theory]
    [InlineData("12", 12)]
    [InlineData(" 12,5", 12.5)]
    [InlineData("1e3", 1000)]
    [InlineData("−3", -3)] // fixed: was none
    public void A_field_value_is_a_number_only_when_it_is_one(string text, double value) => Assert.Equal(value, FieldValue.StrictNumber(text)!.Value, 9);

    [Theory]
    [InlineData("12 m")]
    [InlineData("1 250")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public void A_field_value_with_more_than_a_number_is_a_text(string text) => Assert.Null(FieldValue.StrictNumber(text));

    [Theory]
    [InlineData("3", 3)]
    [InlineData("2,5", 2.5)]
    [InlineData("-1e-3", -0.001)]
    public void A_graphs_typed_item_reads_as_a_number(string text, double value) => Assert.Equal(value, Lists.Number(text)!.Value, 9);

    [Theory]
    [InlineData("a")]
    [InlineData("NaN")]
    [InlineData("1 2")]
    public void A_graphs_typed_item_that_is_no_number_is_none(string text) => Assert.Null(Lists.Number(text));

    [Fact]
    public void A_beam_and_block_catalogue_reads_its_millimetres_with_a_point_or_a_comma()
    {
        var specs = BeamBlock.ParseCsv("Name;Joist width (mm);Joist height (mm)\nA;120,5;175\nB;130.5;180\n");
        Assert.Equal([0.1205, 0.1305], specs.Select(s => Math.Round(s.JoistWidth, 6)));
        Assert.Throws<CommandException>(() => BeamBlock.ParseCsv("Name;Joist width (mm)\nA;NaN\n"));
    }

    [Fact]
    public void A_type_lists_thicknesses_read_with_a_point_or_a_comma_whatever_the_separator()
    {
        Assert.Equal(0.0125, TypeExchange.Read("Type;Material;Thickness (mm)\nW;Brick;12,5\n").Single().Layers.Single().Thickness, 9);
        Assert.Equal(0.0125, TypeExchange.Read("Type,Material,Thickness (mm)\nW,Brick,12.5\n").Single().Layers.Single().Thickness, 9);
        // fixed: a quoted decimal comma in a comma-separated file was refused
        Assert.Equal(0.0125, TypeExchange.Read("Type,Material,Thickness (mm)\nW,Brick,\"12,5\"\n").Single().Layers.Single().Thickness, 9);
        Assert.Throws<CommandException>(() => TypeExchange.Read("Type;Material;Thickness (mm)\nW;Brick;thick\n"));
        Assert.Throws<CommandException>(() => TypeExchange.Read("Type;Material;Thickness (mm)\nW;Brick;NaN\n"));
    }

    [Fact]
    public void A_structure_type_tables_numbers_carry_their_units()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        List<TypeTables.Row> Rows(string text)
        {
            var g = TypeTables.Guess(Tables.Csv(text));
            return TypeTables.Read(f.Model, Tables.Csv(text), g.Header, g.Map, g.UnitColumns, "beam", false, null);
        }
        var rows = Rows("Name;Kind;Profile;h (mm);b (mm);Material;Mark;Bearing (mm)\n" +
            "A;beam;;400;250;;;125\nB;beam;;0,4 m;25 cm;;;\nC;beam;;71mm;1 250;;;\n");
        Assert.Equal(0.4, rows[0].Section!.Depth, 9);
        Assert.Equal(0.25, rows[0].Section!.Width, 9);
        Assert.Equal(0.125, rows[0].Bearing!.Value, 9);
        Assert.Equal(0.4, rows[1].Section!.Depth, 9);
        Assert.Equal(0.25, rows[1].Section!.Width, 9);
        Assert.Equal(0.071, rows[2].Section!.Depth, 9);
        Assert.Equal(1.25, rows[2].Section!.Width, 9); // fixed: „1 250” was 1 mm
    }
}
