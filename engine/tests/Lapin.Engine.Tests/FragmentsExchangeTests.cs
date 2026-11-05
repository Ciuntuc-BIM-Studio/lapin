using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Mesh;
using Lapin.Mesh.Fragments;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Decision 323, the cross-check with That Open's viewer (docs/fragments.md): Lapin's file carries what That
/// Open's IfcImporter writes from the IFC of the same project (attributes, the type, property and quantity
/// sets, materials, the GlobalIds of the spatial structure, the map conversion), its faces as polygons with
/// holes; and a file the official importer made comes in where the IFC had it, its holes open, its glass see-through,
/// its sets under the elements' properties.
/// </summary>
public class FragmentsExchangeTests
{
    /// <summary>A layered wall with a window, a column of a type, a user's parameter in a named set.</summary>
    static (Fixture F, string Wall, string Window, string Column) House()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        var wall = f.Wall(0, 0, 6, 0);
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 3 })!["id"]!;
        var type = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C30", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 } })!["id"]!;
        var column = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = 8, ["y"] = 2 })!["id"]!;
        f.Run("element.setParam", new JsonObject { ["id"] = wall, ["pset"] = "Pset_WallCommon", ["name"] = "FireRating", ["value"] = "REI120" });
        return (f, wall, window, column);
    }

    static FragModel Export(Fixture f, string origin = "internal") => FragReader.Read(MeshExport.Write(f.Session, new MeshExportOptions("frag", Origin: origin)).Bytes);

    static int IndexOf(FragModel m, uint local) => Array.IndexOf(m.LocalIds, local);
    static uint LocalOf(FragModel m, string guid) => m.GuidsItems[Array.IndexOf(m.Guids, guid)];

    [Fact]
    public void An_element_carries_its_type_sets_and_materials_as_the_official_importer_relates_them()
    {
        var (f, wall, _, column) = House();
        var m = Export(f);
        var w = LocalOf(m, wall);
        var attrs = m.AttributesOf(IndexOf(m, w));
        Assert.Equal(wall, (string?)attrs["Tag"]);
        Assert.Equal(f.Model.Get(wall)!.PredefinedType, (string?)attrs["PredefinedType"]);
        var rels = m.RelationsOf(w);
        // The type by IsDefinedBy, an IFC…TYPE item listing its occurrences by ObjectTypeOf.
        var defined = rels["IsDefinedBy"].Select(id => (Id: id, Category: m.Categories[IndexOf(m, id)])).ToList();
        var type = defined.Single(d => d.Category == "IFCWALLTYPE").Id;
        Assert.Equal(f.Model.RelatingOf(wall, IfcClass.RelDefinesByType)!.Name, (string?)m.AttributesOf(IndexOf(m, type))["Name"]);
        Assert.Contains(w, m.RelationsOf(type)["ObjectTypeOf"]);
        // The quantities the module measures, each a quantity item with its value.
        var qto = defined.Where(d => d.Category == "IFCELEMENTQUANTITY").Select(d => d.Id).Single(id => (string?)m.AttributesOf(IndexOf(m, id))["Name"] == "Qto_WallBaseQuantities");
        var quantities = m.RelationsOf(qto)["Quantities"].Select(id => m.AttributesOf(IndexOf(m, id))).ToList();
        var length = quantities.Single(q => (string?)q["Name"] == "Length");
        Assert.Equal(6, (double)length["LengthValue"]!, 3);
        Assert.Equal([w], m.RelationsOf(qto)["DefinesOccurrence"]);
        // The user's parameter in its set, a single value.
        var pset = defined.Where(d => d.Category == "IFCPROPERTYSET").Select(d => d.Id).Single(id => (string?)m.AttributesOf(IndexOf(m, id))["Name"] == "Pset_WallCommon");
        var fire = m.RelationsOf(pset)["HasProperties"].Select(id => m.AttributesOf(IndexOf(m, id))).Single();
        Assert.Equal("FireRating", (string?)fire["Name"]);
        Assert.Equal("REI120", (string?)fire["NominalValue"]);
        // The materials of its layers, each one item however many elements use it.
        var materials = rels["HasAssociations"].Select(id => (string)m.AttributesOf(IndexOf(m, id))["Name"]!).ToList();
        var expected = f.Session.Geometry(wall)!.Parts.Select(p => f.Model.Get(p.Material!)!.Name).Distinct().ToList();
        Assert.Equal(expected, materials);
        var names = m.LocalIds.Where((_, i) => m.Categories[i] == "IFCMATERIAL").Select(id => (string?)m.AttributesOf(IndexOf(m, id))["Name"]).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
        // The column's type, one for its class.
        Assert.Contains(m.RelationsOf(LocalOf(m, column))["IsDefinedBy"], id => m.Categories[IndexOf(m, id)] == "IFCCOLUMNTYPE");
    }

    /// <summary>
    /// Two walls of one type and one length: their quantities are the same values, written once; each keeps sets of its own,
    /// since a set shared by many would lead the library's getItemsData (IsDefinedBy with relations, as BubbleBIM Standalone's
    /// panel asks) from element to element through DefinesOccurrence until its stack overflows (measured on a model of 31,866).
    /// </summary>
    [Fact]
    public void Equal_values_are_written_once_and_every_set_defines_one_element()
    {
        var f = new Fixture(new ArchitectureModule());
        var a = f.Wall(0, 0, 6, 0);
        var b = f.Wall(0, 5, 6, 5);
        var m = Export(f);
        foreach (var (category, i) in m.Categories.Select((c, i) => (c, i)).Where(x => x.c is "IFCPROPERTYSET" or "IFCELEMENTQUANTITY"))
            Assert.Single(m.RelationsOf(m.LocalIds[i])["DefinesOccurrence"]);
        uint Length(string wall) => m.RelationsOf(LocalOf(m, wall))["IsDefinedBy"]
            .Where(id => (string?)m.AttributesOf(IndexOf(m, id))["Name"] == "Qto_WallBaseQuantities")
            .SelectMany(id => m.RelationsOf(id)["Quantities"]).Single(q => (string?)m.AttributesOf(IndexOf(m, q))["Name"] == "Length");
        Assert.Equal(Length(a), Length(b));
    }

    [Fact]
    public void The_spatial_structure_keeps_its_GlobalIds_and_the_file_lists_its_attributes_and_relations()
    {
        var (f, wall, _, _) = House();
        var m = Export(f);
        foreach (var e in new[] { Spatial.Project(f.Model)!, Spatial.Building(f.Model)!, f.Model.OfClass(IfcClass.Site).Single() })
            Assert.Contains(e.Id, m.Guids);
        // What the library's getAttributeNames and getRelationNames read.
        var root = FlatTable.Root(FragReader.Decompress(MeshExport.Write(f.Session, new MeshExportOptions("frag", Origin: "internal")).Bytes));
        var unique = root.Strings(FragSchema.Model.UniqueAttributes)!;
        Assert.Contains($"[\"Tag\",\"{wall}\",\"IFCIDENTIFIER\"]", unique);
        Assert.Equal(unique.Length, unique.Distinct().Count());
        Assert.Equal(m.Attributes!.SelectMany(a => a).Distinct().Count(), unique.Length);
        Assert.Contains("IsDefinedBy", root.Strings(FragSchema.Model.RelationNames)!);
        Assert.Contains("ContainedInStructure", root.Strings(FragSchema.Model.RelationNames)!);
    }

    [Fact]
    public void A_wall_with_a_window_is_written_as_faces_with_holes_and_comes_back_whole()
    {
        var (f, wall, _, _) = House();
        var m = Export(f);
        var mesh = Array.IndexOf(m.Meshes.MeshesItems, (uint)IndexOf(m, LocalOf(m, wall)));
        var shells = m.Meshes.Samples.Where(s => s.Item == mesh).Select(s => m.Meshes.Shells[(int)m.Meshes.Representations[(int)s.Representation].Id]).ToList();
        // Each layer's two sides are one polygon each with the window cut as a hole (the lintel over it has none).
        var layers = f.Session.Geometry(wall)!.Parts.Count(p => p.Key?.StartsWith("layer", StringComparison.Ordinal) == true);
        Assert.True(layers >= 3);
        Assert.Equal(layers, shells.Count(s => s.Holes.Length == 2));
        Assert.All(shells, s => Assert.All(s.Holes, h => Assert.InRange(h.Profile, 0, s.Profiles.Length - 1)));
        var triangles = shells.Sum(s => s.Profiles.Length);
        var original = MeshScene.Build(f.Session, new MeshExportOptions("frag", Origin: "internal")).Groups.SelectMany(g => g.Elements).Single(e => e.GlobalId == wall).Parts.Sum(p => p.Indices.Length / 3);
        Assert.True(triangles * 2 < original, $"{triangles} profiles for {original} triangles");
        // Imported elsewhere: the wall's area is the area it had.
        var g = new Fixture(new ArchitectureModule(), new StructureModule());
        FragImport.Run(g.Session, MeshExport.Write(f.Session, new MeshExportOptions("frag", Origin: "internal")).Bytes, "house.frag", out _);
        Assert.Equal(Area(f, wall), Area(g, wall), 4);
    }

    static double Area(Fixture f, string id)
    {
        double area = 0;
        foreach (var part in f.Session.Geometry(id)!.Parts)
        {
            var mesh = Tessellator.Tessellate(part.Solid);
            for (var t = 0; t < mesh.Indices.Length; t += 3)
            {
                Vec3 P(int i) => new(mesh.Positions[i * 3], mesh.Positions[i * 3 + 1], mesh.Positions[i * 3 + 2]);
                area += Vec3.Cross(P(mesh.Indices[t + 1]) - P(mesh.Indices[t]), P(mesh.Indices[t + 2]) - P(mesh.Indices[t])).Length / 2;
            }
        }
        return area;
    }

    [Fact]
    public void Coplanar_triangles_become_one_polygon_and_an_unclean_region_keeps_its_triangles()
    {
        // A square with a square hole, eight triangles on one face; an L of four triangles on another.
        float[] pts = [0, 0, 0, 3, 0, 0, 3, 3, 0, 0, 3, 0, 1, 1, 0, 2, 1, 0, 2, 2, 0, 1, 2, 0];
        int[] frame = [0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7];
        var p = FragFaces.Of(pts, frame, new ushort[8]);
        Assert.Single(p.Profiles);
        Assert.Equal(4, p.Profiles[0].Length);
        Assert.Single(p.Holes);
        Assert.Equal((4, 0), (p.Holes[0].Indices.Length, p.Holes[0].Profile));
        // Two triangles meeting at a corner only (a bow tie): not one region, left as they are.
        float[] bow = [0, 0, 0, 1, 0, 0, 0, 1, 0, -1, 0, 0, 0, -1, 0];
        var b = FragFaces.Of(bow, [0, 1, 2, 0, 3, 4], [5, 5]);
        Assert.Equal(2, b.Profiles.Count);
        Assert.All(b.Profiles, x => Assert.Equal(3, x.Length));
        Assert.Empty(b.Holes);
    }

    [Fact]
    public void A_file_placed_by_coordinates_comes_in_where_the_ifc_had_it()
    {
        var (f, wall, _, _) = House();
        var m = Export(f);
        // As the official importer stores a model: moved near the origin, the move kept as `coordinates` (stored = coordinates · project).
        var (dx, dy, dz) = (-12.5, 1.0, 30.25);
        var moved = m with
        {
            Meshes = m.Meshes with
            {
                Coordinates = new FragTransform(dx, dy, dz, 1, 0, 0, 0, 1, 0),
                GlobalTransforms = m.Meshes.GlobalTransforms.Select(t => t with { X = t.X + dx, Y = t.Y + dy, Z = t.Z + dz }).ToArray(),
            },
        };
        var g = new Fixture(new ArchitectureModule(), new StructureModule());
        FragImport.Run(g.Session, moved, "moved.frag", out _);
        var (lo, hi) = f.Bounds(wall);
        var (lo2, hi2) = g.Bounds(wall);
        Assert.Equal(lo.X, lo2.X, 3); Assert.Equal(lo.Y, lo2.Y, 3); Assert.Equal(lo.Z, lo2.Z, 3);
        Assert.Equal(hi.X, hi2.X, 3); Assert.Equal(hi.Y, hi2.Y, 3); Assert.Equal(hi.Z, hi2.Z, 3);
        // The columns share a body: its instances are placed through the coordinates too.
        var columnId = f.Model.OfClass(IfcClass.Column).Single().Id;
        Assert.Equal(f.Bounds(columnId).Min.X, g.Bounds(columnId).Min.X, 3);
    }

    [Fact]
    public void Glass_comes_in_see_through_and_the_sets_under_the_elements_properties()
    {
        var (f, wall, window, _) = House();
        var bytes = MeshExport.Write(f.Session, new MeshExportOptions("frag", Origin: "internal")).Bytes;
        var g = new Fixture(new ArchitectureModule(), new StructureModule());
        FragImport.Run(g.Session, bytes, "house.frag", out _);
        // The window's glass: a material of its colour that lets 60 % of the light through, as Lapin's glass.
        var glass = g.Session.Geometry(window)!.Parts.Select(p => p.Material).OfType<string>().Select(id => g.Model.Get(id)!).Single();
        Assert.Equal(MaterialLook.Of(f.Model.OfClass(IfcClass.Material).Single(x => MaterialLook.Of(x).Transparency > 0.1)).Transparency, MaterialLook.Of(glass).Transparency, 2);
        // The wall's sets, its type and materials, read back as an IFC reference's properties.
        var props = g.Model.Get(wall)!.Attrs["properties"]!.AsObject();
        Assert.Equal(6, (double)props["Qto_WallBaseQuantities"]!["Length"]!, 3);
        Assert.Equal("REI120", (string?)props["Pset_WallCommon"]!["FireRating"]);
        Assert.Equal(f.Model.RelatingOf(wall, IfcClass.RelDefinesByType)!.Name, (string?)props["Fragments"]!["Type"]);
        Assert.Contains(f.Model.Get(f.Session.Geometry(wall)!.Parts[0].Material!)!.Name, (string?)props["Fragments"]!["Material"]);
    }

    [Fact]
    public void The_map_conversion_is_the_ifc_exports_moved_to_the_files_origin()
    {
        var (f, _, _, _) = House();
        Assert.Null(Export(f).MetadataJson!["crs"]);
        f.Run("project.location", new JsonObject
        {
            ["crs"] = "EPSG:3844",
            ["base"] = new JsonObject { ["x"] = 2, ["y"] = 1, ["elevation"] = 0, ["northAngle"] = 30 },
            ["survey"] = new JsonObject { ["x"] = 10, ["y"] = 5, ["easting"] = 587900, ["northing"] = 325820, ["elevation"] = 80 },
        });
        // map(p) = (E0, N0) + (c·x − s·y, s·x + c·y) with c = cos 30°, s = −sin 30°: the survey point lands on its coordinates.
        var (c, s) = (Math.Cos(Math.PI / 6), -Math.Sin(Math.PI / 6));
        var (e0, n0) = (587900 - (c * 10 - s * 5), 325820 - (s * 10 + c * 5));
        var crs = Export(f).MetadataJson!["crs"]!;
        Assert.Equal("EPSG:3844", (string?)crs["name"]);
        Assert.Equal("METRE", (string?)crs["mapUnit"]);
        Assert.False(string.IsNullOrEmpty((string?)crs["geodeticDatum"]));
        Assert.Equal(e0, (double)crs["mapConversion"]!["eastings"]!, 4);
        Assert.Equal(n0, (double)crs["mapConversion"]!["northings"]!, 4);
        Assert.Equal(c, (double)crs["mapConversion"]!["xAxisAbscissa"]!, 6);
        Assert.Equal(s, (double)crs["mapConversion"]!["xAxisOrdinate"]!, 6);
        // From the base point (2, 1): the origin of the file is there, so its map coordinates are the base point's.
        var based = Export(f, "base").MetadataJson!["crs"]!["mapConversion"]!;
        Assert.Equal(e0 + c * 2 - s * 1, (double)based["eastings"]!, 4);
        Assert.Equal(n0 + s * 2 + c * 1, (double)based["northings"]!, 4);
    }
}
