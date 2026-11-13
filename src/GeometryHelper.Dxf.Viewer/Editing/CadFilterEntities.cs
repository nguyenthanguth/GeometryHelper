using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What the lines of an object selection filter read of an entity: its kind, its names and its numbers.</summary>
    internal static class CadFilterEntities
    {
        // The DXF names of the objects the presenter holds as unknown entities, by the name AutoCAD's filter list gives them.
        private static readonly Dictionary<string, string[]> Unknown = new Dictionary<string, string[]>(
            StringComparer.OrdinalIgnoreCase
        )
        {
            { "Body", new[] { "BODY" } },
            { "Solid Body", new[] { "3DSOLID" } },
            { "Camera", new[] { "CAMERA" } },
            { "DGN Underlay", new[] { "DGNUNDERLAY" } },
            { "DWF Underlay", new[] { "DWFUNDERLAY" } },
            { "Helix", new[] { "HELIX" } },
            { "Light", new[] { "LIGHT" } },
            { "Mesh", new[] { "MESH" } },
            { "MultiLine", new[] { "MLINE" } },
            { "Section Plane", new[] { "SECTION" } },
            { "Shape", new[] { "SHAPE" } },
            { "Tolerance", new[] { "TOLERANCE" } },
            { "Trace", new[] { "TRACE" } },
            { "Surface(Planar)", new[] { "PLANESURFACE" } },
            { "Surface(Sweep)", new[] { "SWEPTSURFACE" } },
            { "Surface(Loft)", new[] { "LOFTEDSURFACE" } },
            { "Surface(Revolve)", new[] { "REVOLVEDSURFACE" } },
            { "Surface(Extrusion)", new[] { "EXTRUDEDSURFACE" } },
            { "Surface(Nurbs)", new[] { "NURBSURFACE" } },
        };

        /// <summary>Whether an entity is of a kind: its name in the Properties panel (Line, MText, Block Reference) or in AutoCAD's filter list (Arc, Block, Polyline, Attribute).</summary>
        internal static bool IsKind(DxfEntity entity, string value)
        {
            if (string.Equals(CadProperties.KindOf(entity), value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            switch (value.ToUpperInvariant())
            {
                case "ATTRIBUTE":
                    return entity is DxfAttributeDefinition;
                case "BLOCK":
                    return entity is DxfBlockReference;
                case "DIMENSION":
                    return entity is DxfDimension;
                case "POLYLINE":
                    return entity is DxfPolyline || entity is DxfPolyline2d || entity is DxfPolyline3d;
                case "IMAGE":
                    return entity is DxfRasterImage && !(entity is DxfWipeout);
                case "3DFACE":
                    return entity is DxfFace;
                default:
                    return entity is DxfUnknownEntity unknown
                        && Unknown.TryGetValue(value, out string[] names)
                        && Array.IndexOf(names, unknown.DxfName) >= 0;
            }
        }

        /// <summary>The DXF name written for an Object line in the file of named lists: ARC, INSERT, LWPOLYLINE.</summary>
        internal static string DxfNameOf(string value)
        {
            switch (value.ToUpperInvariant())
            {
                case "ATTRIBUTE":
                    return "ATTDEF";
                case "BLOCK":
                case "BLOCK REFERENCE":
                    return "INSERT";
                case "POLYLINE":
                    return "LWPOLYLINE";
                case "TEXT":
                    return "TEXT";
                case "MTEXT":
                    return "MTEXT";
                default:
                    return Unknown.TryGetValue(value, out string[] names) ? names[0] : value.ToUpperInvariant();
            }
        }

        /// <summary>Whether an entity has extended data of an application, a name or a wild-card pattern.</summary>
        internal static bool HasApplication(DxfEntity entity, string pattern)
        {
            foreach (DxfTypedValue value in entity.XData)
            {
                if (value.TypeCode == 1001 && CadWildcard.IsMatch(value.Value, pattern, true))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Reads a property of an entity: its name where it is one, its numbers (one for a length or an angle in radians,
        /// three for a point or a direction) where it is one; false where the entity has no such property.
        /// </summary>
        internal static bool TryRead(
            DxfEntity entity,
            CadFilterProperty property,
            out string text,
            out double[] numbers
        )
        {
            text = null;
            numbers = null;

            switch (property)
            {
                case CadFilterProperty.ArcCenter:
                    return entity is DxfArc arc && Set(arc.Center, out numbers);
                case CadFilterProperty.ArcRadius:
                    return entity is DxfArc ar && Set(ar.Radius, out numbers);
                case CadFilterProperty.CircleCenter:
                    return entity is DxfCircle circle && Set(circle.Center, out numbers);
                case CadFilterProperty.CircleRadius:
                    return entity is DxfCircle c && Set(c.Radius, out numbers);
                case CadFilterProperty.EllipseCenter:
                    return entity is DxfEllipse ellipse && Set(ellipse.Center, out numbers);
                case CadFilterProperty.AttributePosition:
                    return entity is DxfAttributeDefinition attribute && Set(attribute.Position, out numbers);
                case CadFilterProperty.AttributeTag:
                    text = (entity as DxfAttributeDefinition)?.Tag;
                    return text != null;
                case CadFilterProperty.BlockName:
                    text = (entity as DxfBlockReference)?.Name;
                    return text != null;
                case CadFilterProperty.BlockPosition:
                    return entity is DxfBlockReference block && Set(block.Position, out numbers);
                case CadFilterProperty.BlockRotation:
                    return entity is DxfBlockReference turned && Set(turned.Rotation, out numbers);
                case CadFilterProperty.DimensionStyle:
                    text = (entity as DxfDimension)?.DimensionStyleName;
                    return text != null;
                case CadFilterProperty.Elevation:
                    return entity is DxfPolyline lw ? Set(lw.Elevation, out numbers)
                        : entity is DxfPolyline2d old ? Set(old.Elevation, out numbers)
                        : entity is DxfHatch hatch && Set(hatch.Elevation, out numbers);
                case CadFilterProperty.HatchPatternName:
                    text = (entity as DxfHatch)?.PatternName;
                    return text != null;
                case CadFilterProperty.ImagePosition:
                    return entity is DxfRasterImage image && Set(image.Position, out numbers);
                case CadFilterProperty.LinetypeScale:
                    return Set(entity.LinetypeScale, out numbers);
                case CadFilterProperty.LineStart:
                    return entity is DxfLine start && Set(start.StartPoint, out numbers);
                case CadFilterProperty.LineEnd:
                    return entity is DxfLine end && Set(end.EndPoint, out numbers);
                case CadFilterProperty.NormalVector:
                    return Normal(entity, out numbers);
                case CadFilterProperty.PointPosition:
                    return entity is DxfDBPoint point && Set(point.Position, out numbers);
                case CadFilterProperty.TextPosition:
                    return entity is DxfDBText placed
                        ? Set(placed.Position, out numbers)
                        : entity is DxfMText mplaced && Set(mplaced.Location, out numbers);
                case CadFilterProperty.TextValue:
                    text = entity is DxfDBText said ? said.TextString : (entity as DxfMText)?.Text;
                    return text != null;
                case CadFilterProperty.TextStyleName:
                    text = entity is DxfDBText styled ? styled.TextStyleName : (entity as DxfMText)?.TextStyleName;
                    return text != null;
                case CadFilterProperty.TextHeight:
                    return entity is DxfDBText tall
                        ? Set(tall.Height, out numbers)
                        : entity is DxfMText mtall && Set(mtall.Height, out numbers);
                case CadFilterProperty.TextRotation:
                    return entity is DxfDBText rotated
                        ? Set(rotated.Rotation, out numbers)
                        : entity is DxfMText mrotated && Set(mrotated.Rotation, out numbers);
                case CadFilterProperty.Thickness:
                    return entity is DxfLine line ? Set(line.Thickness, out numbers)
                        : entity is DxfCircle disc ? Set(disc.Thickness, out numbers)
                        : entity is DxfArc curve ? Set(curve.Thickness, out numbers)
                        : entity is DxfDBText word ? Set(word.Thickness, out numbers)
                        : entity is DxfDBPoint dot ? Set(dot.Thickness, out numbers)
                        : entity is DxfPolyline poly && Set(poly.Thickness, out numbers);
                case CadFilterProperty.ViewportCenter:
                    return entity is DxfViewport viewport && Set(viewport.CenterPoint, out numbers);
                case CadFilterProperty.XdataId:
                    text = string.Empty;
                    return true;
                default:
                    return false;
            }
        }

        private static bool Normal(DxfEntity entity, out double[] numbers)
        {
            GeoVector3? normal =
                entity is DxfLine line ? line.Normal
                : entity is DxfCircle circle ? circle.Normal
                : entity is DxfArc arc ? arc.Normal
                : entity is DxfDBText text ? text.Normal
                : entity is DxfMText mtext ? mtext.Normal
                : entity is DxfPolyline poly ? poly.Normal
                : entity is DxfDBPoint point ? point.Normal
                : entity is DxfBlockReference block ? block.Normal
                : entity is DxfHatch hatch ? hatch.Normal
                : (GeoVector3?)null;

            if (normal == null)
            {
                numbers = null;
                return false;
            }

            numbers = new[] { normal.Value.X, normal.Value.Y, normal.Value.Z };
            return true;
        }

        private static bool Set(GeoPoint3 point, out double[] numbers)
        {
            numbers = new[] { point.X, point.Y, point.Z };
            return true;
        }

        private static bool Set(double value, out double[] numbers)
        {
            numbers = new[] { value };
            return true;
        }
    }
}
