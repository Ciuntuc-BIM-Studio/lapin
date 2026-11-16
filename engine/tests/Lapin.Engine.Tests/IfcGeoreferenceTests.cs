using System.Text;
using System.Text.Json.Nodes;
using Lapin.Ifc;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A georeferenced IFC (issue 27, forum 85): the export writes IfcMapConversion and IfcProjectedCRS from the survey point
/// or, without one, from the base point's latitude and longitude in the project's system (Stereo 70 against PROJ's
/// numbers), IFC 4.3's two more scales, IfcSite's RefLatitude and RefLongitude; nothing for a project with no location.
/// The import reads IFC 4's map conversion and IFC2X3's ePSet_MapConversion and ePSet_ProjectedCRS back into the
/// project's location, and the site's latitude and longitude.
/// </summary>
public class IfcGeoreferenceTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-geo").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    // PROJ 9 (pyproj), as GeoTests: 26.1025° E, 44.4268° N in EPSG:3844 and EPSG:32635.
    const double Lon = 26.1025, Lat = 44.4268, E70 = 587904.3249, N70 = 325824.3903, EUtm = 428561.4934, NUtm = 4919670.0321;

    (StepFile File, IReadOnlyList<string> Notes) Export(Fixture f, string name, IfcExportOptions? options = null)
    {
        var path = Path.Combine(_dir, name);
        var notes = IfcExport.Write(f.Session, path, options).Notes;
        return (StepFile.Read(path), notes);
    }

    static StepEntity Single(StepFile f, string type) => Assert.Single(f.OfType(type));
    static double N(object? v) => IfcModel.Num(v);

    [Fact]
    public void A_project_in_Stereo_70_with_only_a_latitude_and_longitude_is_placed_on_the_map()
    {
        var f = new Fixture();
        // The base point 10 m east and 5 m north of the plan's origin, turned 30° from true north, ±0.00 at 80 m.
        f.Run("project.location", new JsonObject
        {
            ["crs"] = "EPSG:3844",
            ["base"] = new JsonObject { ["x"] = 10, ["y"] = 5, ["elevation"] = 80, ["northAngle"] = 30, ["latitude"] = Lat, ["longitude"] = Lon },
        });
        Assert.Null(Location.SetSurvey(f.Model));
        var (file, notes) = Export(f, "stereo70.ifc");
        var mc = Single(file, "IFCMAPCONVERSION");
        Assert.Equal(8, mc.Args.Count);
        var (c, s) = (Math.Cos(Math.PI / 6), -Math.Sin(Math.PI / 6));
        // The plan's origin on the map: the base point's eastings and northings less its plan offset turned by true north.
        Assert.Equal(E70 - (c * 10 - s * 5), N(mc[2]), 0.01);
        Assert.Equal(N70 - (s * 10 + c * 5), N(mc[3]), 0.01);
        Assert.Equal(80, N(mc[4]), 9);
        Assert.Equal(c, N(mc[5]), 6);
        Assert.Equal(s, N(mc[6]), 6);
        Assert.Equal(1, N(mc[7]), 9);
        // From the Model context to the projected system, by its EPSG name, its datum and projection, in metres.
        Assert.Equal("Model", IfcModel.Str(file.Get(mc[0])![1]));
        var crs = file.Get(mc[1])!;
        Assert.Equal("IFCPROJECTEDCRS", crs.Type);
        Assert.Equal("EPSG:3844", IfcModel.Str(crs[0]));
        Assert.Equal("Pulkovo 1942(58)", IfcModel.Str(crs[2]));
        Assert.Equal("Oblique Stereographic", IfcModel.Str(crs[4]));
        Assert.Equal("METRE", ((StepEnum)file.Get(crs[6])![3]!).Value);
        // The base point, mapped back through the conversion, lands on its own eastings and northings.
        var (e0, n0) = (N(mc[2]), N(mc[3]));
        Assert.Equal(E70, e0 + c * 10 - s * 5, 0.01);
        Assert.Equal(N70, n0 + s * 10 + c * 5, 0.01);
        // The site's latitude and longitude, as IfcCompoundPlaneAngleMeasure: 44°25'36.48", 26°6'9".
        var site = Single(file, "IFCSITE");
        Assert.Equal(new object?[] { 44L, 25L, 36L, 480000L }, (List<object?>)site[9]!);
        Assert.Equal(new object?[] { 26L, 6L, 9L, 0L }, (List<object?>)site[10]!);
        Assert.Equal(80, N(site[11]), 9);
        Assert.DoesNotContain(notes, n => n.Contains("map conversion") || n.Contains("survey point"));
        // The plans and the other exports read the same tie: the base point's map coordinates.
        var (be, bn) = GeoRef.ToMap(f.Model, new Lapin.Geometry.Vec2(10, 5));
        Assert.Equal(E70, be, 0.01);
        Assert.Equal(N70, bn, 0.01);
    }

    [Fact]
    public void A_survey_point_is_written_as_it_was_and_IFC_4_3_takes_its_two_more_scales()
    {
        var f = new Fixture();
        f.Run("project.location", new JsonObject
        {
            ["crs"] = "3844",
            ["survey"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["easting"] = 587900, ["northing"] = 325820, ["elevation"] = 80 },
            ["base"] = new JsonObject { ["elevation"] = 80 },
        });
        var (file, _) = Export(f, "survey43.ifc", new IfcExportOptions(Schema: "IFC4X3"));
        var mc = Single(file, "IFCMAPCONVERSION");
        Assert.Equal(10, mc.Args.Count);
        Assert.Equal(587900, N(mc[2]), 9);
        Assert.Equal(325820, N(mc[3]), 9);
        Assert.Equal(1, N(mc[7]), 9);
        Assert.Null(mc[8]);
        Assert.Null(mc[9]);
        // The latitude and longitude of the base point worked out from the survey point.
        var place = IfcImport.PlaceOf(file)!.Value;
        var (lon, lat) = GeoRef.ToWgs84(GeoRef.Stereo70, 587900, 325820);
        Assert.Equal(lat, place.Lat, 1e-6);
        Assert.Equal(lon, place.Lon, 1e-6);
    }

    [Fact]
    public void A_latitude_and_longitude_without_a_projected_system_take_the_UTM_zone_of_the_place()
    {
        var f = new Fixture();
        f.Run("project.location", new JsonObject { ["base"] = new JsonObject { ["latitude"] = Lat, ["longitude"] = Lon } });
        var (file, notes) = Export(f, "utm.ifc");
        var mc = Single(file, "IFCMAPCONVERSION");
        Assert.Equal(EUtm, N(mc[2]), 0.001);
        Assert.Equal(NUtm, N(mc[3]), 0.001);
        var crs = file.Get(mc[1])!;
        Assert.Equal("EPSG:32635", IfcModel.Str(crs[0]));
        Assert.Equal("35N", IfcModel.Str(crs[5]));
        Assert.Contains(notes, n => n.Contains("UTM zone (EPSG:32635)"));
    }

    [Fact]
    public void A_project_without_a_location_writes_no_map_conversion_and_no_latitude()
    {
        var f = new Fixture();
        var (file, _) = Export(f, "none.ifc");
        Assert.Empty(file.OfType("IFCMAPCONVERSION"));
        Assert.Empty(file.OfType("IFCPROJECTEDCRS"));
        var site = Single(file, "IFCSITE");
        Assert.Null(site[9]);
        Assert.Null(site[10]);
    }

    [Fact]
    public void A_survey_point_left_at_0_0_in_a_known_system_is_told()
    {
        var f = new Fixture();
        f.Run("project.location", new JsonObject { ["crs"] = "3844", ["survey"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["easting"] = 0, ["northing"] = 0 } });
        var (_, notes) = Export(f, "zero.ifc");
        Assert.Contains(notes, n => n.Contains("eastings and northings are 0"));
    }

    [Fact]
    public void Lapin_reads_its_own_latitude_and_longitude_back()
    {
        var f = new Fixture();
        f.Run("project.location", new JsonObject { ["crs"] = "EPSG:3844", ["base"] = new JsonObject { ["northAngle"] = 12.5, ["latitude"] = Lat, ["longitude"] = Lon } });
        var path = Path.Combine(_dir, "own.ifc");
        IfcExport.Write(f.Session, path);
        var g = new Fixture();
        IfcImport.Open(g.Session, StepFile.Read(path), "own.ifc", null, out _);
        var b = Location.Base(g.Model);
        Assert.Equal(Lat, b.Latitude!.Value, 1e-12);
        Assert.Equal(Lon, b.Longitude!.Value, 1e-12);
        Assert.Equal(12.5, b.NorthAngle!.Value, 9);
        Assert.Equal(GeoRef.Stereo70, Location.Crs(g.Model));
        // As the file Lapin wrote has it, not a survey point made from its map conversion.
        Assert.Null(Location.SetSurvey(g.Model));
    }

    const string Head = "ISO-10303-21;\nHEADER;\nFILE_DESCRIPTION(('ViewDefinition [ReferenceView]'),'2;1');\nFILE_NAME('geo.ifc','2026-11-15T00:00:00',(''),(''),'','','');\nFILE_SCHEMA(('{0}'));\nENDSEC;\nDATA;\n";
    const string Tail = "ENDSEC;\nEND-ISO-10303-21;\n";

    /// <summary>Another program's file: a project in millimetres, its Model context, a site.</summary>
    static string Foreign(string schema, string more) => string.Format(Head, schema) + """
        #1=IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);
        #2=IFCUNITASSIGNMENT((#1));
        #3=IFCCARTESIANPOINT((0.,0.,0.));
        #4=IFCAXIS2PLACEMENT3D(#3,$,$);
        #5=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.E-05,#4,$);
        #6=IFCPROJECT('0YvctVUKr0kugbFTf53O9L',$,'Geo',$,$,$,$,(#5),#2);
        #10=IFCLOCALPLACEMENT($,#4);
        #11=IFCSITE('1YvctVUKr0kugbFTf53O9L',$,'Site',$,$,#10,$,$,.ELEMENT.,$,$,$,$,$);
        #12=IFCRELAGGREGATES('2YvctVUKr0kugbFTf53O9L',$,$,$,#6,(#11));

        """ + more + Tail;

    static void AssertTied(Fixture g, double e, double n, double h, double north)
    {
        var sv = Location.SetSurvey(g.Model)!;
        Assert.Equal(0, sv.At.X, 9);
        Assert.Equal(0, sv.At.Y, 9);
        Assert.Equal(e, sv.Easting!.Value, 1e-6);
        Assert.Equal(n, sv.Northing!.Value, 1e-6);
        Assert.Equal(h, sv.Elevation, 1e-6);
        Assert.Equal(north, Location.Base(g.Model).NorthAngle!.Value, 1e-6);
        Assert.Equal(GeoRef.Stereo70, Location.Crs(g.Model));
    }

    [Fact]
    public void An_IFC_4_map_conversion_comes_into_the_project_s_location()
    {
        // The map unit the metre while the project is in millimetres; the X axis 30° from east, clockwise.
        var text = Foreign("IFC4", """
            #7=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);
            #8=IFCPROJECTEDCRS('EPSG:3844','Stereo 70','Pulkovo 1942(58)',$,'Oblique Stereographic',$,#7);
            #9=IFCMAPCONVERSION(#5,#8,587904.3249,325824.3903,80.,0.866025403784439,-0.5,1.);

            """);
        var g = new Fixture();
        var result = IfcImport.Run(g.Session, Encoding.UTF8.GetBytes(text), "geo4.ifc", null, out _);
        AssertTied(g, E70, N70, 80, 30);
        Assert.Contains(result.Notes, x => x.Contains("tied to the map as the file is"));
        // And out again as it came in.
        var (file, _) = Export(g, "again.ifc");
        var mc = Single(file, "IFCMAPCONVERSION");
        Assert.Equal(E70, N(mc[2]), 1e-4);
        Assert.Equal(N70, N(mc[3]), 1e-4);
        Assert.Equal(0.866025403784439, N(mc[5]), 9);
        Assert.Equal(-0.5, N(mc[6]), 9);
    }

    [Fact]
    public void A_map_conversion_without_a_map_unit_is_in_the_project_s_unit()
    {
        var text = Foreign("IFC4", """
            #8=IFCPROJECTEDCRS('EPSG:3844',$,$,$,$,$,$);
            #9=IFCMAPCONVERSION(#5,#8,587904324.9,325824390.3,80000.,1.,0.,$);

            """);
        var g = new Fixture();
        IfcImport.Run(g.Session, Encoding.UTF8.GetBytes(text), "mm.ifc", null, out _);
        AssertTied(g, E70, N70, 80, 0);
    }

    [Fact]
    public void IFC2X3_s_property_sets_come_into_the_project_s_location()
    {
        var text = Foreign("IFC2X3", """
            #20=IFCPROPERTYSINGLEVALUE('Eastings',$,IFCLENGTHMEASURE(587904.3249),$);
            #21=IFCPROPERTYSINGLEVALUE('Northings',$,IFCLENGTHMEASURE(325824.3903),$);
            #22=IFCPROPERTYSINGLEVALUE('OrthogonalHeight',$,IFCLENGTHMEASURE(80.),$);
            #23=IFCPROPERTYSINGLEVALUE('XAxisAbscissa',$,IFCREAL(0.866025403784439),$);
            #24=IFCPROPERTYSINGLEVALUE('XAxisOrdinate',$,IFCREAL(-0.5),$);
            #25=IFCPROPERTYSINGLEVALUE('Scale',$,IFCREAL(1.),$);
            #26=IFCPROPERTYSET('3YvctVUKr0kugbFTf53O9L',$,'ePSet_MapConversion',$,(#20,#21,#22,#23,#24,#25));
            #27=IFCRELDEFINESBYPROPERTIES('4YvctVUKr0kugbFTf53O9L',$,$,$,(#11),#26);
            #30=IFCPROPERTYSINGLEVALUE('Name',$,IFCLABEL('EPSG:3844'),$);
            #31=IFCPROPERTYSINGLEVALUE('MapUnit',$,IFCLABEL('METRE'),$);
            #32=IFCPROPERTYSET('5YvctVUKr0kugbFTf53O9L',$,'ePSet_ProjectedCRS',$,(#30,#31));
            #33=IFCRELDEFINESBYPROPERTIES('6YvctVUKr0kugbFTf53O9L',$,$,$,(#11),#32);

            """);
        var g = new Fixture();
        IfcImport.Run(g.Session, Encoding.UTF8.GetBytes(text), "geo2x3.ifc", null, out _);
        AssertTied(g, E70, N70, 80, 30);
    }

    [Fact]
    public void A_site_s_latitude_and_longitude_alone_become_the_base_point_s()
    {
        var text = Foreign("IFC4", "").Replace("$,.ELEMENT.,$,$,$,$,$);", "$,.ELEMENT.,(44,25,36,480000),(26,6,9,0),$,$,$);");
        var g = new Fixture();
        IfcImport.Run(g.Session, Encoding.UTF8.GetBytes(text), "place.ifc", null, out _);
        var b = Location.Base(g.Model);
        Assert.Equal(Lat, b.Latitude!.Value, 1e-9);
        Assert.Equal(Lon, b.Longitude!.Value, 1e-9);
        Assert.Null(Location.SetSurvey(g.Model));
    }

    [Fact]
    public void A_project_tied_already_keeps_its_own_tie()
    {
        var text = Foreign("IFC4", """
            #8=IFCPROJECTEDCRS('EPSG:3844',$,$,$,$,$,$);
            #9=IFCMAPCONVERSION(#5,#8,587904324.9,325824390.3,80000.,1.,0.,$);

            """);
        var g = new Fixture();
        g.Run("project.location", new JsonObject { ["crs"] = "3844", ["survey"] = new JsonObject { ["easting"] = 500000, ["northing"] = 400000 } });
        var result = IfcImport.Run(g.Session, Encoding.UTF8.GetBytes(text), "other.ifc", null, out _);
        Assert.Equal(500000, Location.SetSurvey(g.Model)!.Easting!.Value, 9);
        Assert.Contains(result.Notes, x => x.Contains("the project keeps its own"));
    }

    [Fact]
    public void A_latitude_out_of_range_is_refused_and_null_clears_it()
    {
        var f = new Fixture();
        Assert.Throws<CommandException>(() => f.Run("project.location", new JsonObject { ["base"] = new JsonObject { ["latitude"] = 91 } }));
        f.Run("project.location", new JsonObject { ["base"] = new JsonObject { ["latitude"] = Lat, ["longitude"] = Lon } });
        Assert.NotNull(Location.Base(f.Model).Latitude);
        f.Run("project.location", new JsonObject { ["base"] = new JsonObject { ["latitude"] = null } });
        Assert.Null(Location.Base(f.Model).Latitude);
        Assert.Null(Location.Base(f.Model).Longitude);
    }
}
