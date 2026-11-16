using Lapin.Model;

namespace Lapin.Engine.Tests;

/// <summary>One free-name convention (audit 15.11.2026, D12): „Name 2”, and „Name 25 (2)” when the name ends in a digit.</summary>
public class FreeNamesTests
{
    static string Free(string name, params string[] taken) => Names.Free(taken, name);

    [Fact]
    public void A_free_name_is_kept() => Assert.Equal("Door", Free("Door", "Window"));

    [Fact]
    public void A_taken_name_takes_the_first_free_number()
    {
        Assert.Equal("Door 2", Free("Door", "Door"));
        Assert.Equal("Door 3", Free("Door", "door", "Door 2"));
    }

    [Fact]
    public void A_name_ending_in_a_digit_takes_its_number_in_brackets()
    {
        Assert.Equal("Brick 25 (2)", Free("Brick 25", "Brick 25"));
        Assert.Equal("C30 (3)", Free("C30", "C30", "C30 (2)"));
        Assert.Equal("IPE 300 (2)", Free("IPE 300", "IPE 300", "IPE"));
    }

    [Fact]
    public void A_copy_of_a_numbered_copy_goes_on_from_its_number()
    {
        Assert.Equal("Brick 25 (3)", Free("Brick 25 (2)", "Brick 25", "Brick 25 (2)"));
        Assert.Equal("Wall 3", Free("Wall (2)", "Wall", "Wall (2)"));
        // Brackets that are part of a name stay its own when there is no such stem.
        Assert.Equal("Room (2) 2", Free("Room (2)", "Room (2)"));
    }

    [Fact]
    public void Numbered_writes_as_free_does()
    {
        Assert.Equal("Sheet 4", Names.Numbered("Sheet", 4));
        Assert.Equal("Sheet A1 (4)", Names.Numbered("Sheet A1", 4));
    }
}
