using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Lighting;
using static Lapin.Ifc.Step;

namespace Lapin.Ifc;

public static partial class IfcExport
{
    /// <summary>A light's data in words other programs read (docs/plan-render.md, S2): never with IFC's „Pset_” prefix.</summary>
    public const string LightSetName = "Lapin_Light";

    /// <summary>
    /// Lights in IFC (docs/plan-render.md, S2). What has light data (an element, its type or a material) gets the set
    /// <see cref="LightSetName"/> (Lumens, ColourTemperature, Kind, BeamAngle, FieldAngle, Width, Length, Profile, On), and an
    /// element that lights gets a "Lighting" representation of the kind IFC has: a point an IfcLightSourcePositional, a spot an
    /// IfcLightSourceSpot (its cone), a panel or a profile an IfcLightSourceGoniometric with its colour temperature, its flux and
    /// its distribution as IFC's type C table (IfcLightIntensityDistribution, cd per 1000 lm). Lapin reads the data back from
    /// its intent set; these are for the others.
    /// </summary>
    sealed partial class Writer
    {
        /// <summary>The Lapin_Light set of an entity with light data (merged over its type's for an element), or null.</summary>
        string? LightSet(Entity e, string gid)
        {
            if (e.Attrs[Lights.Key] is not JsonObject own) return null;
            var spec = e.Class == IfcClass.Material || e.Class.EndsWith("Type", StringComparison.Ordinal) ? LightSpec.Read(own) : Lights.Of(m, e) ?? LightSpec.Read(own);
            var props = new List<string>
            {
                Single("Lumens", Typed("IFCLUMINOUSFLUXMEASURE", R(spec.Lumens))),
                Single("ColourTemperature", Typed("IFCTHERMODYNAMICTEMPERATUREMEASURE", R(spec.Kelvin))),
                Single("Kind", Typed("IFCLABEL", S(spec.Kind))),
                Single("On", Typed("IFCBOOLEAN", spec.On ? True : False)),
            };
            if (spec.Kind == "spot")
            {
                props.Add(Single("BeamAngle", Typed("IFCREAL", R(spec.Beam)), "degrees"));
                props.Add(Single("FieldAngle", Typed("IFCREAL", R(spec.Field)), "degrees"));
            }
            if (spec.Width is { } w) props.Add(Single("Width", Typed("IFCPOSITIVELENGTHMEASURE", R(w))));
            if (spec.Length is { } l) props.Add(Single("Length", Typed("IFCPOSITIVELENGTHMEASURE", R(l))));
            if (spec.Profile is { } p) props.Add(Single("Profile", Typed("IFCLABEL", S(Lights.Profile(m, p)?.Name ?? p))));
            return f.Add("IFCPROPERTYSET", gid, Null, S(LightSetName), Null, L(props));
        }

        /// <summary>An element's light as IFC's light source, in the body's frame; null when it gives none.</summary>
        string? LightRep(Entity e, ElementGeometry? geo, Frame fr)
        {
            if (geo == null || Lights.Of(m, e) is not { } spec || Lights.Place(e, spec, geo) is not { } src) return null;
            var (r, g, b) = Kelvin.Rgb(spec.Kelvin);
            var colour = f.Add("IFCCOLOURRGB", Null, R(Math.Round(r, 4)), R(Math.Round(g, 4)), R(Math.Round(b, 4)));
            var name = S(e.Name);
            var at = fr.Local(src.Position);
            var point = f.Add("IFCCARTESIANPOINT", L(new[] { R(at.X), R(at.Y), R(at.Z) }));
            var aim = fr.Turn(src.Aim);
            var intensity = Typed("IFCNORMALISEDRATIOMEASURE", R(1));
            var profile = Lights.Profile(m, spec.Profile) ?? (spec.Kind == "area" ? LightProfiles.Generated(LightProfiles.Hemispherical) : null);
            string item;
            if (profile != null)
            {
                var across = fr.Turn(src.Across);
                var place = f.Add("IFCAXIS2PLACEMENT3D", point, f.Add("IFCDIRECTION", L(new[] { R(aim.X), R(aim.Y), R(aim.Z) })), f.Add("IFCDIRECTION", L(new[] { R(across.X), R(across.Y), R(across.Z) })));
                // cd per 1000 lm, as IFC's distribution measure is: the table's relative values × the profile's scale over an even light's 1000 / 4π.
                var perKlm = 1000.0 / (4 * Math.PI) * profile.Scale;
                var planes = new List<string>();
                for (var h = 0; h < LightProfile.Horizontal; h++)
                {
                    var gammas = Enumerable.Range(0, LightProfile.Vertical).Select(v => R(Math.Round(v * LightProfile.GammaStep * Math.PI / 180, 6))).ToArray();
                    var values = Enumerable.Range(0, LightProfile.Vertical).Select(v => R(Math.Round(profile.Table[v * LightProfile.Horizontal + h] * perKlm, 3))).ToArray();
                    planes.Add(f.Add("IFCLIGHTDISTRIBUTIONDATA", R(Math.Round(h * LightProfile.CStep * Math.PI / 180, 6)), L(gammas), L(values)));
                }
                var data = f.Add("IFCLIGHTINTENSITYDISTRIBUTION", E("TYPE_C"), L(planes));
                item = f.Add("IFCLIGHTSOURCEGONIOMETRIC", name, colour, Null, intensity, place, Null, R(spec.Kelvin), R(spec.Lumens), E("LIGHTEMITTINGDIODE"), data);
            }
            else if (spec.Kind == "spot")
                item = f.Add("IFCLIGHTSOURCESPOT", name, colour, Null, intensity, point, R(10), R(1), R(0), R(1),
                    f.Add("IFCDIRECTION", L(new[] { R(aim.X), R(aim.Y), R(aim.Z) })), Null, R(Math.Round(spec.Field / 2 * Math.PI / 180, 6)), R(Math.Round(spec.Beam / 2 * Math.PI / 180, 6)));
            else item = f.Add("IFCLIGHTSOURCEPOSITIONAL", name, colour, Null, intensity, point, R(10), R(1), R(0), R(1));
            return f.Add("IFCSHAPEREPRESENTATION", body, S("Lighting"), S("LightSource"), L(new[] { item }));
        }
    }
}
