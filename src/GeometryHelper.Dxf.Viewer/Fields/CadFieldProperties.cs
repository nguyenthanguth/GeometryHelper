using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What a property of an object is, which says how its field formats it.</summary>
    internal enum CadFieldKind
    {
        /// <summary>Words.</summary>
        Text,

        /// <summary>A whole number.</summary>
        Whole,

        /// <summary>A length or other real.</summary>
        Real,

        /// <summary>An angle in radians.</summary>
        Angle,

        /// <summary>A point, as its x, y and z.</summary>
        Point,
    }

    /// <summary>A property of an object a field can read, by the name AutoCAD's ActiveX gives it.</summary>
    internal sealed class CadFieldProperty
    {
        internal CadFieldProperty(string name, CadFieldKind kind, Func<DxfEntity, object> read)
        {
            Name = name;
            Kind = kind;
            Read = read;
        }

        /// <summary>Gets the name: Area, Layer, Radius.</summary>
        internal string Name { get; }

        /// <summary>Gets what the property is.</summary>
        internal CadFieldKind Kind { get; }

        /// <summary>Reads the property of an object: a string, an int, a double, or a <see cref="GeoPoint3"/>.</summary>
        internal Func<DxfEntity, object> Read { get; }
    }

    /// <summary>
    /// The properties of an object a field of the kind Object can show, by their names in AutoCAD's ActiveX: those of every
    /// object (Color, Handle, Layer, Linetype, LinetypeScale, Lineweight, ObjectName) and those of a line (Angle, Delta,
    /// EndPoint, Length, StartPoint), a circle (Area, Center, Circumference, Diameter, Radius), an arc, an ellipse, a polyline
    /// (Area, Closed, ConstantWidth, Elevation, Length), a text, an MTEXT, a block reference, a hatch and a region.
    /// </summary>
    internal static class CadFieldProperties
    {
        /// <summary>Gives the properties of an object, by name.</summary>
        /// <param name="entity">The object.</param>
        /// <returns>The properties, those of every object after the object's own, each set in the order of its names.</returns>
        internal static IReadOnlyList<CadFieldProperty> Of(DxfEntity entity)
        {
            var own = new List<CadFieldProperty>();

            switch (entity)
            {
                case DxfLine _:
                    own.Add(
                        P("Angle", CadFieldKind.Angle, e =>
                            Math.Atan2(
                                ((DxfLine)e).EndPoint.Y - ((DxfLine)e).StartPoint.Y,
                                ((DxfLine)e).EndPoint.X - ((DxfLine)e).StartPoint.X
                            )
                        )
                    );
                    own.Add(
                        P("Delta", CadFieldKind.Point, e => new GeoPoint3(
                            ((DxfLine)e).Delta.X,
                            ((DxfLine)e).Delta.Y,
                            ((DxfLine)e).Delta.Z
                        ))
                    );
                    own.Add(P("EndPoint", CadFieldKind.Point, e => ((DxfLine)e).EndPoint));
                    own.Add(P("Length", CadFieldKind.Real, e => ((DxfLine)e).Length));
                    own.Add(P("StartPoint", CadFieldKind.Point, e => ((DxfLine)e).StartPoint));
                    break;
                case DxfCircle _:
                    own.Add(P("Area", CadFieldKind.Real, e => ((DxfCircle)e).Area));
                    own.Add(P("Center", CadFieldKind.Point, e => ((DxfCircle)e).Center));
                    own.Add(P("Circumference", CadFieldKind.Real, e => ((DxfCircle)e).Circumference));
                    own.Add(P("Diameter", CadFieldKind.Real, e => ((DxfCircle)e).Diameter));
                    own.Add(P("Radius", CadFieldKind.Real, e => ((DxfCircle)e).Radius));
                    break;
                case DxfArc _:
                    own.Add(P("ArcLength", CadFieldKind.Real, e => ((DxfArc)e).Length));
                    own.Add(P("Area", CadFieldKind.Real, e => ((DxfArc)e).Area));
                    own.Add(P("Center", CadFieldKind.Point, e => ((DxfArc)e).Center));
                    own.Add(P("EndAngle", CadFieldKind.Angle, e => ((DxfArc)e).EndAngle));
                    own.Add(P("Radius", CadFieldKind.Real, e => ((DxfArc)e).Radius));
                    own.Add(P("StartAngle", CadFieldKind.Angle, e => ((DxfArc)e).StartAngle));
                    own.Add(P("TotalAngle", CadFieldKind.Angle, e => ((DxfArc)e).TotalAngle));
                    break;
                case DxfEllipse _:
                    own.Add(P("Area", CadFieldKind.Real, e => ((DxfEllipse)e).Area));
                    own.Add(P("Center", CadFieldKind.Point, e => ((DxfEllipse)e).Center));
                    own.Add(P("MajorRadius", CadFieldKind.Real, e => ((DxfEllipse)e).MajorRadius));
                    own.Add(P("MinorRadius", CadFieldKind.Real, e => ((DxfEllipse)e).MinorRadius));
                    own.Add(P("RadiusRatio", CadFieldKind.Real, e => ((DxfEllipse)e).RadiusRatio));
                    break;
                case DxfPolyline _:
                    own.Add(P("Area", CadFieldKind.Real, e => ((DxfPolyline)e).Area));
                    own.Add(P("Closed", CadFieldKind.Whole, e => ((DxfPolyline)e).Closed ? 1 : 0));
                    own.Add(P("ConstantWidth", CadFieldKind.Real, e => ((DxfPolyline)e).ConstantWidth));
                    own.Add(P("Elevation", CadFieldKind.Real, e => ((DxfPolyline)e).Elevation));
                    own.Add(P("Length", CadFieldKind.Real, e => ((DxfPolyline)e).Length));
                    break;
                case DxfDBText _:
                    own.Add(P("Height", CadFieldKind.Real, e => ((DxfDBText)e).Height));
                    own.Add(P("InsertionPoint", CadFieldKind.Point, e => ((DxfDBText)e).Position));
                    own.Add(P("Rotation", CadFieldKind.Angle, e => ((DxfDBText)e).Rotation));
                    own.Add(P("StyleName", CadFieldKind.Text, e => ((DxfDBText)e).TextStyleName ?? string.Empty));
                    own.Add(P("TextString", CadFieldKind.Text, e => ((DxfDBText)e).TextString ?? string.Empty));
                    own.Add(P("ScaleFactor", CadFieldKind.Real, e => ((DxfDBText)e).WidthFactor));
                    break;
                case DxfMText _:
                    own.Add(P("Height", CadFieldKind.Real, e => ((DxfMText)e).TextHeight));
                    own.Add(P("InsertionPoint", CadFieldKind.Point, e => ((DxfMText)e).Location));
                    own.Add(P("Rotation", CadFieldKind.Angle, e => ((DxfMText)e).Rotation));
                    own.Add(P("StyleName", CadFieldKind.Text, e => ((DxfMText)e).TextStyleName ?? string.Empty));
                    own.Add(P("TextString", CadFieldKind.Text, e => ((DxfMText)e).Text ?? string.Empty));
                    own.Add(P("Width", CadFieldKind.Real, e => ((DxfMText)e).Width));
                    break;
                case DxfBlockReference _:
                    own.Add(P("InsertionPoint", CadFieldKind.Point, e => ((DxfBlockReference)e).Position));
                    own.Add(P("Name", CadFieldKind.Text, e => ((DxfBlockReference)e).Name ?? string.Empty));
                    own.Add(P("Rotation", CadFieldKind.Angle, e => ((DxfBlockReference)e).Rotation));
                    own.Add(P("XScaleFactor", CadFieldKind.Real, e => ((DxfBlockReference)e).ScaleFactors.X));
                    own.Add(P("YScaleFactor", CadFieldKind.Real, e => ((DxfBlockReference)e).ScaleFactors.Y));
                    own.Add(P("ZScaleFactor", CadFieldKind.Real, e => ((DxfBlockReference)e).ScaleFactors.Z));
                    break;
                case DxfHatch _:
                    own.Add(P("Area", CadFieldKind.Real, e => ((DxfHatch)e).Area));
                    own.Add(P("PatternAngle", CadFieldKind.Angle, e => ((DxfHatch)e).PatternAngle));
                    own.Add(P("PatternName", CadFieldKind.Text, e => ((DxfHatch)e).PatternName ?? string.Empty));
                    own.Add(P("PatternScale", CadFieldKind.Real, e => ((DxfHatch)e).PatternScale));
                    break;
                case DxfUnknownEntity region when region.DxfName == "REGION":
                    own.Add(P("Area", CadFieldKind.Real, e => ((DxfUnknownEntity)e).GetRegionShape().Area));
                    own.Add(P("Perimeter", CadFieldKind.Real, e => ((DxfUnknownEntity)e).GetRegionShape().Perimeter));
                    break;
            }

            own.Add(P("Color", CadFieldKind.Text, e => ColorName(e.Color)));
            own.Add(P("Handle", CadFieldKind.Text, e => e.Handle ?? string.Empty));
            own.Add(P("Layer", CadFieldKind.Text, e => e.Layer ?? "0"));
            own.Add(P("Linetype", CadFieldKind.Text, e => string.IsNullOrEmpty(e.Linetype) ? "ByLayer" : e.Linetype));
            own.Add(P("LinetypeScale", CadFieldKind.Real, e => e.LinetypeScale));
            own.Add(P("Lineweight", CadFieldKind.Whole, e => (int)e.LineWeight));
            own.Add(P("ObjectName", CadFieldKind.Text, e => ObjectName(e)));
            own.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return own;
        }

        /// <summary>Finds a property of an object by its name, letters of either case the same; null where it has none.</summary>
        internal static CadFieldProperty Find(DxfEntity entity, string name)
        {
            foreach (CadFieldProperty property in Of(entity))
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return property;
                }
            }

            return null;
        }

        private static CadFieldProperty P(string name, CadFieldKind kind, Func<DxfEntity, object> read) =>
            new CadFieldProperty(name, kind, read);

        /// <summary>The colour as AutoCAD's field shows it: BYLAYER, BYBLOCK, the index, or the red, green and blue of a true colour.</summary>
        private static string ColorName(DxfColor color)
        {
            if (color == null || color.IsByLayer)
            {
                return "BYLAYER";
            }

            if (color.IsByBlock)
            {
                return "BYBLOCK";
            }

            return color.IsByColor
                ? color.Red.ToString(CultureInfo.InvariantCulture)
                    + ","
                    + color.Green.ToString(CultureInfo.InvariantCulture)
                    + ","
                    + color.Blue.ToString(CultureInfo.InvariantCulture)
                : color.ColorIndex.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The name of the object's class as AutoCAD gives it: AcDbLine, AcDbPolyline, AcDbText.</summary>
        internal static string ObjectName(DxfEntity entity)
        {
            switch (entity)
            {
                case DxfLine _:
                    return "AcDbLine";
                case DxfCircle _:
                    return "AcDbCircle";
                case DxfArc _:
                    return "AcDbArc";
                case DxfEllipse _:
                    return "AcDbEllipse";
                case DxfPolyline _:
                    return "AcDbPolyline";
                case DxfMText _:
                    return "AcDbMText";
                case DxfAttributeReference _:
                    return "AcDbAttribute";
                case DxfAttributeDefinition _:
                    return "AcDbAttributeDefinition";
                case DxfDBText _:
                    return "AcDbText";
                case DxfBlockReference _:
                    return "AcDbBlockReference";
                case DxfHatch _:
                    return "AcDbHatch";
                default:
                    string name = entity is DxfUnknownEntity unknown
                        ? unknown.DxfName
                        : entity.GetType().Name.Replace("Dxf", string.Empty);
                    return "AcDb"
                        + (
                            name.Length == 0
                                ? name
                                : char.ToUpperInvariant(name[0]) + name.Substring(1).ToLowerInvariant()
                        );
            }
        }
    }
}
