using Lapin.Drawing.Views;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The class sets modules add to as they register are the process's, and engines are made at once (test classes run in
/// parallel): adds and reads from many threads leave them whole.
/// </summary>
public class ClassSetTests
{
    [Fact]
    public void Classes_symbolic_in_plan_are_added_and_read_at_once() =>
        AddAndReadAtOnce(c => Generator.SymbolicInPlan.Add(c), Generator.SymbolicInPlan.Contains);

    [Fact]
    public void Site_classes_are_added_and_read_at_once() =>
        AddAndReadAtOnce(c => SitePlans.SiteClasses.Add(c), SitePlans.SiteClasses.Contains);

    static void AddAndReadAtOnce(Action<string> add, Func<string, bool> contains)
    {
        // Names no element has, so the classes the other tests draw are as they were. Every thread adds all of them, as
        // every engine's modules register the same classes, and there are enough for the set to grow many times over.
        var classes = Enumerable.Range(0, 20_000).Select(i => $"LapinTestClass{i}").ToArray();
        const int count = 8;
        using var start = new Barrier(count);
        var failures = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        var threads = Enumerable.Range(0, count).Select(_ => new Thread(() =>
        {
            start.SignalAndWait();
            try
            {
                foreach (var c in classes)
                {
                    add(c);
                    Assert.True(contains(c), $"{c} is missing right after it was added.");
                    Assert.True(contains("IfcPavement"), "A class there from the start is missing.");
                }
            }
            catch (Exception e) { failures.Enqueue(e); }
        })).ToList();
        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        Assert.Empty(failures);
        Assert.All(classes, c => Assert.True(contains(c), $"{c} is missing."));
    }
}
