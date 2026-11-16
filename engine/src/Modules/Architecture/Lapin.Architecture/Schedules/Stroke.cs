using Lapin.Geometry;

namespace Lapin.Architecture.Schedules;

/// <summary>A stroke of a 2D symbol: its ends (m), its weight (cut, outline, thin) and whether it is dashed.</summary>
public readonly record struct Stroke(Vec2 A, Vec2 B, string Weight, bool Dashed = false);
