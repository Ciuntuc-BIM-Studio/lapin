namespace Lapin.Geometry;

/// <summary>
/// One planar face. <see cref="Outer"/> is CCW seen from outside (around
/// <see cref="Normal"/>); holes are wound the other way. Loops index the
/// solid's vertex pool; the closing edge is implicit.
/// </summary>
public sealed record Face(int[] Outer, int[][] Holes, Vec3 Normal, string? Tag = null);

/// <summary>
/// A closed, orientable polyhedron with planar faces. Curved elements arrive
/// already tessellated. Build one with <see cref="SolidBuilder.Make"/> — it
/// welds, cleans and derives normals, which everything downstream assumes.
/// Port of BubbleBIM's src/lib/brep (millimetres there, metres here).
/// </summary>
public sealed record Solid(Vec3[] Vertices, Face[] Faces, string? Tag = null);

/// <summary>A face as given to <see cref="SolidBuilder.Make"/>; the normal is optional and only its sign is used.</summary>
public sealed record FaceInput(int[] Outer, int[][]? Holes = null, Vec3? Normal = null, string? Tag = null);

public enum DiagnosticSeverity { Error, Warning }

public sealed record BrepDiagnostic(string Code, DiagnosticSeverity Severity, string Message, int? Face = null);
