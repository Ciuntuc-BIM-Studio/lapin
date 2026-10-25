using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Lapin.Package;
using Lapin.Protocol;

namespace Lapin.Browser;

/// <summary>
/// The recovery journals in the browser's own storage (the origin private file
/// system), through the worker's "lapin-journal" module: it keeps every
/// journal in memory, read at start, and writes each line through to storage
/// as it comes. A journal another tab is writing (it holds its lock) is in use.
/// There are no project files to start from: a journal always carries its
/// starting model.
/// </summary>
[SupportedOSPlatform("browser")]
sealed partial class BrowserJournalStore : IJournalStore
{
    [JSImport("create", "lapin-journal")] private static partial void JsCreate(string name);
    [JSImport("open", "lapin-journal")] private static partial void JsOpen(string name);
    [JSImport("close", "lapin-journal")] private static partial void JsClose(string name);
    [JSImport("write", "lapin-journal")] private static partial void JsWrite(string name, string line);
    [JSImport("names", "lapin-journal")] private static partial string[] JsNames();
    [JSImport("inUse", "lapin-journal")] private static partial bool JsInUse(string name);
    [JSImport("lines", "lapin-journal")] private static partial string[] JsLines(string name);
    [JSImport("lastWrite", "lapin-journal")] private static partial double JsLastWrite(string name);
    [JSImport("remove", "lapin-journal")] private static partial void JsRemove(string name);

    sealed class Writer(string name) : IJournalWriter
    {
        public void WriteLine(string line) => JsWrite(name, line);
        public void Dispose() => JsClose(name);
    }

    public (string Name, IJournalWriter Writer) Create(string fileName)
    {
        JsCreate(fileName);
        return (fileName, new Writer(fileName));
    }

    public IJournalWriter Append(string name)
    {
        JsOpen(name);
        return new Writer(name);
    }

    public IEnumerable<string> Journals() => JsNames();
    public bool InUse(string name) => JsInUse(name);
    public bool Holds(string name) => JsNames().Contains(name);
    public IReadOnlyList<string> ReadLines(string name) => JsLines(name);
    public DateTime LastWrite(string name) => DateTime.UnixEpoch.AddMilliseconds(JsLastWrite(name));
    public void Delete(string name) => JsRemove(name);
    public string? ProjectHash(string path) => null;
    public PackageContent ReadProject(string path) => throw new InvalidOperationException("The browser has no project files to read.");
}
