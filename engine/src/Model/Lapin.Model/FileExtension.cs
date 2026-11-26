namespace Lapin.Model;

/// <summary>
/// An extension module a project was written with (docs/plan-extension-modules.md §4.4): the module's id and the schema of
/// its part, and the extension it came in (its catalogue id and version). What a package's manifest records and a session
/// reports when the edition opening it does not have that module.
/// </summary>
public sealed record FileExtension(string Module, int SchemaVersion, string Id, string Version);
