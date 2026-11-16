using Lapin.Planning;

namespace Lapin.Engine.Tests;

/// <summary>
/// MS Project XML's dates read as written (audit 15.11.2026, follow-up), as the IFC import's are: a Z or an offset after the
/// clock time is kept out, so a task starting late in the day does not fall on another day in the machine's time zone.
/// </summary>
public class MsProjectDatesTests
{
    static string File(string start, string taskStart, string constraint, string actual, string status) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <Project xmlns="http://schemas.microsoft.com/project">
          <Name>Dates.xml</Name>
          <StartDate>{start}</StartDate>
          <StatusDate>{status}</StatusDate>
          <MinutesPerDay>480</MinutesPerDay>
          <Tasks>
            <Task>
              <UID>1</UID><ID>1</ID><Name>Walls</Name><OutlineLevel>1</OutlineLevel>
              <Start>{taskStart}</Start><Duration>PT16H0M0S</Duration>
              <ConstraintType>4</ConstraintType><ConstraintDate>{constraint}</ConstraintDate>
              <ActualStart>{actual}</ActualStart>
              <Baseline><Number>0</Number><Start>{taskStart}</Start><Finish>{constraint}</Finish><Duration>PT16H0M0S</Duration></Baseline>
            </Task>
          </Tasks>
        </Project>
        """;

    [Theory]
    // UTC late in the day: east of Greenwich, read as local time, it was the next day.
    [InlineData("2026-03-02T23:30:00Z", "2026-03-04T23:59:00Z", "2026-03-06T23:00:00Z", "2026-03-04T22:00:00Z", "2026-03-09T23:45:00Z")]
    // An offset west of Greenwich late in the day, and east of it early: each the day written.
    [InlineData("2026-03-02T22:00:00-05:00", "2026-03-04T23:00:00-08:00", "2026-03-06T00:30:00+03:00", "2026-03-04T01:00:00+14:00", "2026-03-09T00:15:00+05:30")]
    // No offset (as MS Project writes them): as written too.
    [InlineData("2026-03-02T08:00:00", "2026-03-04T08:00:00", "2026-03-06T17:00:00", "2026-03-04", "2026-03-09T00:00:00")]
    public void Dates_are_read_as_written_whatever_their_offset(string start, string taskStart, string constraint, string actual, string status)
    {
        var s = MsProject.Read(File(start, taskStart, constraint, actual, status));
        Assert.Equal(new DateOnly(2026, 3, 2), s.Start);
        Assert.Equal(new DateOnly(2026, 3, 9), s.DataDate);
        var t = Assert.Single(s.Tasks);
        Assert.Equal(new DateOnly(2026, 3, 4), t.Start);
        Assert.Equal("snet", t.Constraint);
        Assert.Equal(new DateOnly(2026, 3, 6), t.ConstraintDate);
        Assert.Equal(new DateOnly(2026, 3, 4), t.ActualStart);
        Assert.Equal(new DateOnly(2026, 3, 4), t.Baseline!.Start);
        Assert.Equal(new DateOnly(2026, 3, 6), t.Baseline.Finish);
    }

    [Fact]
    public void A_date_that_is_not_one_is_none()
    {
        var s = MsProject.Read(File("2026-03-02", "not a date", "2026-02-30T08:00:00", "", "2026-03-09"));
        var t = Assert.Single(s.Tasks);
        Assert.Null(t.Start);
        Assert.Null(t.ConstraintDate);
        Assert.Null(t.Constraint);
        Assert.Null(t.ActualStart);
        Assert.Null(t.Baseline);
    }
}
