namespace Lapin.Mesh.Fragments;

/// <summary>
/// The Fragments FlatBuffer layout as vtable slot numbers, taken from the
/// generated code inside <c>@thatopen/fragments</c> 3.4.5 (the version the
/// files were checked against); the schema on the repository's main branch
/// (<c>packages/fragments/flatbuffers/index.fbs</c>, MIT) matched it field for
/// field. A table addresses its fields by slot, so the ORDER of the fields is
/// the contract, not their names: a field is only ever added at the end, and
/// this file is derived again after any upgrade of the package:
/// <code>grep -oE "static add\w+\(builder,\s*\w+\)\s*\{\s*builder\.addField\w+\(\d+" node_modules/@thatopen/fragments/dist/index.cjs</code>
/// Structs are inline, little-endian, with FlatBuffers' natural alignment.
/// </summary>
public static class FragSchema
{
    public static class Model
    {
        public const int Metadata = 0, Guids = 1, GuidsItems = 2, MaxLocalId = 3, LocalIds = 4, Categories = 5, Meshes = 6, Attributes = 7,
            Relations = 8, RelationsItems = 9, Guid = 10, SpatialStructure = 11, UniqueAttributes = 12, RelationNames = 13, Indexes = 14, Fields = 15;
    }

    public static class Meshes
    {
        public const int Coordinates = 0, MeshesItems = 1, Samples = 2, Representations = 3, Materials = 4, CircleExtrusions = 5, Shells = 6,
            LocalTransforms = 7, GlobalTransforms = 8, MaterialIds = 9, RepresentationIds = 10, SampleIds = 11, LocalTransformIds = 12, GlobalTransformIds = 13, Fields = 14;
    }

    public static class Shell
    {
        public const int Profiles = 0, Holes = 1, Points = 2, BigProfiles = 3, BigHoles = 4, Type = 5, ProfilesFaceIds = 6, Fields = 7;
    }

    public static class ShellProfile { public const int Indices = 0, Fields = 1; }
    public static class ShellHole { public const int Indices = 0, ProfileId = 1, Fields = 2; }
    public static class Attribute { public const int Data = 0, Fields = 1; }
    public static class Relation { public const int Data = 0, Fields = 1; }
    public static class Spatial { public const int LocalId = 0, Category = 1, Children = 2, Fields = 3; }

    public const sbyte RenderedFacesOne = 0, RenderedFacesTwo = 1, StrokeDefault = 0;
    public const sbyte RepresentationShell = 1;
    public const sbyte ShellTypeNone = 0, ShellTypeBig = 1;

    /// <summary>Struct sizes in bytes, with their alignment padding.</summary>
    public const int SizeFloatVector = 12, SizeTransform = 48, SizeMaterial = 6, SizeRepresentation = 32, SizeSample = 16;

    /// <summary>A shell addresses its points with ushort; past this it takes the BIG form (uint).</summary>
    public const int MaxSmallPoints = 65_535;
}
