using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The DIMENSIONs that show dimensional constraints: made from the points a constraint measures, their words the name of
    /// the parameter and its value (<c>d1=10.0000</c>, <c>rad1=4.0000</c>, <c>ang1=42</c>), drawn again when the constraint's
    /// numbers change.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// The words of a dimensional constraint in the format its name format asks: its parameter's name (0), the value in the
        /// precision the dimension reads (1), or the name, an equals sign and the value (2).
        /// </summary>
        internal static string ConstraintWords(
            DxfDimension dimension,
            CadDimKind kind,
            string name,
            double value,
            int format
        )
        {
            if (format == 0)
            {
                return name;
            }

            bool angle = kind == CadDimKind.Angular;
            int places = angle ? Math.Max(0, dimension.Dimadec) : Math.Max(0, dimension.Dimdec);
            string number = value.ToString(
                "F" + places.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture
            );
            return format == 1 ? number : name + "=" + number;
        }

        /// <summary>
        /// Makes the hidden layer AutoCAD 2021 draws dynamic dimensional constraints on (<c>*ADSK_CONSTRAINTS</c>) where the
        /// drawing has none. The layer table refuses the star of the name in a record made through it, so the record is
        /// written as AutoCAD writes it: colour 7, Continuous, lineweight -3.
        /// </summary>
        internal static void EnsureConstraintLayer(DxfDatabase database)
        {
            if (database.LayerTable.Has(CadConstraintStore.DynamicLayer))
            {
                return;
            }

            // AutoCAD reads no layer of AutoCAD 2000 and later without its plot style (390): the one layer 0 has.
            string plotStyle = null;

            foreach (DxfTypedValue group in database.LayerTable["0"].Item.Groups)
            {
                if (group.TypeCode == 390)
                {
                    plotStyle = group.Value;
                }
            }

            database.File.AddTableRecord("LAYER", "AcDbLayerTableRecord", b =>
            {
                b.Group(2, CadConstraintStore.DynamicLayer);
                b.Group(70, "0");
                b.Group(62, "7");
                b.Group(6, "Continuous");
                b.Group(370, "-3");

                if (plotStyle != null)
                {
                    b.Group(390, plotStyle);
                }
            });
        }

        /// <summary>
        /// Puts a dimension on the hidden layer of dynamic constraints by writing its layer group (8) as it stands in the file:
        /// the star of the name is one the layer property refuses.
        /// </summary>
        internal static void PutOnConstraintLayer(DxfEntity dimension)
        {
            IReadOnlyList<DxfTypedValue> groups = dimension.Item.Groups;

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].TypeCode == 8)
                {
                    dimension.Item.File.SetValueAt(dimension.Item, i, CadConstraintStore.DynamicLayer);
                    return;
                }
            }
        }

        private static GeoPoint2 P2Of(CadConstraints.Work work, CadRef r)
        {
            GeoPoint2? p = CadConstraintShapes.PointOf(work.Shapes[r.Handle], r);
            return p ?? default;
        }

        /// <summary>
        /// Makes the DIMENSION of a dimensional constraint from the numbers the work holds, its line through
        /// <paramref name="location"/>, and adds it to the space; its handle, or null where the dimension measures nothing.
        /// </summary>
        internal static string MakeConstraintDimension(
            CadCommandContext c,
            CadConstraints.Work work,
            CadDimConstraint constraint,
            double value,
            GeoPoint2 location
        )
        {
            DxfDimStyleTableRecord style = DimStyle(c);

            if (style == null)
            {
                return null;
            }

            DxfDimension made;

            switch (constraint.Kind)
            {
                case CadDimKind.Horizontal:
                case CadDimKind.Vertical:
                case CadDimKind.Aligned:
                {
                    GeoPoint2 p1 = P2Of(work, constraint.Refs[0]);
                    GeoPoint2 p2 = P2Of(work, constraint.Refs[1]);
                    double angle =
                        constraint.Kind == CadDimKind.Aligned
                            ? Math.Atan2(p2.Y - p1.Y, p2.X - p1.X)
                            : (constraint.Kind == CadDimKind.Horizontal ? 0.0 : Math.PI / 2.0);
                    var along = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
                    GeoPoint2 q2 = location + along * ((p2 - location).X * along.X + (p2 - location).Y * along.Y);
                    made =
                        constraint.Kind == CadDimKind.Aligned
                            ? (DxfDimension)new DxfAlignedDimension(P3(p1), P3(p2), P3(q2), string.Empty, null)
                            : new DxfRotatedDimension(angle, P3(p1), P3(p2), P3(q2), string.Empty, null);
                    break;
                }

                case CadDimKind.Radius:
                case CadDimKind.Diameter:
                {
                    double[] v = work.Shapes[constraint.Refs[0].Handle].Values;
                    var center = new GeoPoint2(v[0], v[1]);
                    GeoVector2 toward = location - center;

                    if (toward.Length <= 1E-9)
                    {
                        toward = new GeoVector2(1.0, 0.0);
                    }

                    toward = toward / toward.Length;
                    GeoPoint2 chord = center + toward * v[2];
                    made =
                        constraint.Kind == CadDimKind.Diameter
                            ? (DxfDimension)
                                new DxfDiametricDimension(
                                    P3(chord),
                                    P3(center - toward * v[2]),
                                    0.0,
                                    string.Empty,
                                    null
                                )
                            : new DxfRadialDimension(P3(center), P3(chord), 0.0, string.Empty, null);
                    break;
                }

                default:
                {
                    // AutoCAD 2021 draws the angle from the second line of the constraint to its first: its dimension lists them so.
                    CadShape s1 = work.Shapes[constraint.Refs[0].Handle];
                    CadShape s2 = work.Shapes[constraint.Refs[1].Handle];
                    GeoPoint2 a0 = P2Of(work, constraint.Refs[1].Of(CadPart.Start));
                    GeoPoint2 a1 = P2Of(work, constraint.Refs[1].Of(CadPart.End));
                    GeoPoint2 b0 = P2Of(work, constraint.Refs[0].Of(CadPart.Start));
                    GeoPoint2 b1 = P2Of(work, constraint.Refs[0].Of(CadPart.End));
                    made = new DxfLineAngularDimension2(
                        P3(a0),
                        P3(a1),
                        P3(b0),
                        P3(b1),
                        P3(location),
                        string.Empty,
                        null
                    );
                    break;
                }
            }

            made.DimensionText = ConstraintWords(made, constraint.Kind, constraint.Name, value, constraint.NameFormat);
            string layer = constraint.Dynamic ? CadConstraintStore.DynamicLayer : null;
            CadSpace space = c.Space;
            CadEditResult result = c.Apply(null, s =>
            {
                if (layer != null)
                {
                    EnsureConstraintLayer(s.Database);
                }

                var block = new DxfBlockTableRecord { Name = "*D" };
                s.Database.BlockTable.Add(block);
                DxfDimStyleTableRecord named = s.Database.DimStyleTable[style.Name];
                made.DimensionStyle = named;
                made.DimBlock = block;
                made.Layer = s.Database.Clayer;
                space.Block.AppendEntity(made);
                made.SetDatabaseDefaults(s.Database);

                if (layer != null)
                {
                    PutOnConstraintLayer(made);
                }

                Redraw(made);
                s.Added(space, made);
            });

            return result.Succeeded ? AddedHandle(result) : null;
        }

        /// <summary>
        /// Draws the dimension of a dimensional constraint again from the numbers the work holds: its points moved to where the
        /// objects stand, its line kept the way it lay from them, its words the parameter's name and value.
        /// </summary>
        internal static void RefreshConstraintDimension(
            CadConstraints.Work work,
            CadDimConstraint constraint,
            DxfDimension dimension
        )
        {
            CadParameter parameter = work.Set.Parameter(constraint.Name);
            double value = parameter?.Value ?? 0.0;

            switch (dimension)
            {
                case DxfRotatedDimension rotated:
                {
                    GeoPoint3 before = rotated.XLine1Point;
                    GeoPoint2 p1 = P2Of(work, constraint.Refs[0]);
                    GeoPoint2 p2 = P2Of(work, constraint.Refs[1]);
                    GeoVector2 move = p1 - new GeoPoint2(before.X, before.Y);
                    rotated.XLine1Point = P3(p1);
                    rotated.XLine2Point = P3(p2);
                    rotated.DimLinePoint = new GeoPoint3(
                        rotated.DimLinePoint.X + move.X,
                        rotated.DimLinePoint.Y + move.Y,
                        0.0
                    );
                    break;
                }

                case DxfAlignedDimension aligned:
                {
                    GeoPoint3 before = aligned.XLine1Point;
                    GeoPoint2 p1 = P2Of(work, constraint.Refs[0]);
                    GeoPoint2 p2 = P2Of(work, constraint.Refs[1]);
                    GeoVector2 move = p1 - new GeoPoint2(before.X, before.Y);
                    aligned.XLine1Point = P3(p1);
                    aligned.XLine2Point = P3(p2);
                    aligned.DimLinePoint = new GeoPoint3(
                        aligned.DimLinePoint.X + move.X,
                        aligned.DimLinePoint.Y + move.Y,
                        0.0
                    );
                    break;
                }

                case DxfRadialDimension radial:
                {
                    double[] v = work.Shapes[constraint.Refs[0].Handle].Values;
                    var center = new GeoPoint2(v[0], v[1]);
                    GeoVector2 toward =
                        new GeoPoint2(radial.ChordPoint.X, radial.ChordPoint.Y)
                        - new GeoPoint2(radial.Center.X, radial.Center.Y);
                    toward = toward.Length <= 1E-12 ? new GeoVector2(1.0, 0.0) : toward / toward.Length;
                    radial.Center = P3(center);
                    radial.ChordPoint = P3(center + toward * v[2]);
                    break;
                }

                case DxfDiametricDimension diametric:
                {
                    double[] v = work.Shapes[constraint.Refs[0].Handle].Values;
                    var center = new GeoPoint2(v[0], v[1]);
                    GeoVector2 toward =
                        new GeoPoint2(diametric.FarChordPoint.X, diametric.FarChordPoint.Y)
                        - new GeoPoint2(diametric.ChordPoint.X, diametric.ChordPoint.Y);
                    toward = toward.Length <= 1E-12 ? new GeoVector2(1.0, 0.0) : toward / toward.Length;
                    diametric.ChordPoint = P3(center - toward * v[2]);
                    diametric.FarChordPoint = P3(center + toward * v[2]);
                    break;
                }

                case DxfLineAngularDimension2 angular:
                    angular.XLine1Start = P3(P2Of(work, constraint.Refs[1].Of(CadPart.Start)));
                    angular.XLine1End = P3(P2Of(work, constraint.Refs[1].Of(CadPart.End)));
                    angular.XLine2Start = P3(P2Of(work, constraint.Refs[0].Of(CadPart.Start)));
                    angular.XLine2End = P3(P2Of(work, constraint.Refs[0].Of(CadPart.End)));
                    break;
            }

            dimension.DimensionText = ConstraintWords(
                dimension,
                constraint.Kind,
                constraint.Name,
                value,
                constraint.NameFormat
            );
            Redraw(dimension);
        }
    }
}
