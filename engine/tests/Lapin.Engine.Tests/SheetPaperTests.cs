using System.Text.Json.Nodes;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Sheets of any paper (a tester's wish, #89): the A sizes, ISO's long ones, the North American ones, and one of the user's own size.</summary>
public class SheetPaperTests
{
    [Fact]
    public void A_sheet_takes_a_long_or_a_custom_paper()
    {
        var f = new Fixture();
        string Sheet(JsonObject a) => (string)f.Run("sheet.create", a)!["id"]!;
        var custom = Sheet(new JsonObject { ["paper"] = "custom", ["paperWidth"] = 1500, ["paperHeight"] = 650, ["orientation"] = "portrait" });
        Assert.Equal((1500d, 650d), Sheets.PaperOf(f.Model.Get(custom)!));
        var page = Sheets.Compose(f.Session.Reader, f.Model.Get(custom)!);
        Assert.Equal(1500, page.W, 3);
        Assert.Equal(650, page.H, 3);
        var longSheet = Sheet(new JsonObject { ["paper"] = "A3x3" });
        Assert.Equal((891d, 420d), Sheets.PaperOf(f.Model.Get(longSheet)!));
        // Changed in its properties later: the size follows.
        f.Run("element.update", new JsonObject { ["id"] = longSheet, ["attrs"] = new JsonObject { ["paper"] = "custom", ["paperWidth"] = 2000, ["paperHeight"] = 900 } });
        Assert.Equal((2000d, 900d), Sheets.PaperOf(f.Model.Get(longSheet)!));
        Assert.ThrowsAny<Exception>(() => Sheet(new JsonObject { ["paper"] = "B7" }));
    }
}
