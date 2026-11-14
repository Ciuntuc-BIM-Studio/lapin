namespace Lapin.Platform;

/// <summary>
/// The names a door's or window's parts carry as IFC material constituents (report #76): what the export names each part of the
/// joinery by in its IfcMaterialConstituentSet, and what the import maps a file's constituents back to. Lapin writes Frame, Sash,
/// Glazing, Panel, Board and Hardware; it reads those and IFC4's own names for a door's or window's constituents (Lining for
/// the frame, Framing for a window's sash or a door's leaf), whatever their case.
/// </summary>
public static class JoineryConstituents
{
    /// <summary>The constituent a part of the joinery's body is (its role: frame, sash, muntin, glass, leaf, board, handle, hardware); null for any other part.</summary>
    public static string? Name(string role) => role switch
    {
        "frame" => "Frame", "sash" or "muntin" => "Sash", "glass" => "Glazing", "leaf" => "Panel", "board" => "Board",
        "handle" or "hardware" => "Hardware", _ => null,
    };

    /// <summary>The part of a door's (<paramref name="door"/>) or window's materials a constituent's name sets (frame, sash, glass, leaf, board, hardware), or null.</summary>
    public static string? Part(string? name, bool door) => (name ?? "").Trim().ToLowerInvariant() switch
    {
        "frame" or "lining" or "casing" => "frame",
        "sash" => "sash",
        "framing" => door ? "leaf" : "sash",
        "glazing" or "glass" or "pane" => "glass",
        "panel" or "leaf" or "door leaf" => "leaf",
        "board" or "window board" or "sill" => "board",
        "hardware" or "handle" or "fittings" => "hardware",
        _ => null,
    };
}
