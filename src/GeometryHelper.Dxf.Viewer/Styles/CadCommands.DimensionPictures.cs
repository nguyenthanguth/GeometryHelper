using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The pictures of dimensions drawn again: a dimension's own block (<c>*D7</c>) made anew from its points and the
    /// variables it reads, as the DIM commands make it, after its style changed; and the sample dimensions of the Dimension
    /// Style Manager's preview, made by the same builders, so that the preview and the dimensions agree.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// Draws a dimension's block anew from its points and the variables it reads now (its overrides, else its style's): a
        /// linear, aligned, radius, diameter or angular dimension whose block is its own anonymous one. The entities of the block
        /// are erased and the new picture appended. False, and nothing changed, for another kind or a block not its own.
        /// </summary>
        internal static bool Redraw(DxfDimension dimension)
        {
            DxfBlockTableRecord block = dimension.DimBlock;

            if (block == null || !block.IsAnonymous)
            {
                return false;
            }

            DxfDimStyleTableRecord data = dimension.GetDimstyleData();
            List<DxfEntity> picture = Picture(dimension, data, data.Dimtxt);

            if (picture == null)
            {
                return false;
            }

            foreach (DxfEntity old in block.ToList())
            {
                old.Erase();
            }

            foreach (DxfEntity entity in picture)
            {
                if (entity.Layer == null || entity.Layer.Length == 0)
                {
                    entity.Layer = "0";
                }

                block.AppendEntity(entity);
            }

            return true;
        }

        /// <summary>
        /// The picture of a dimension of the drawing from its points, as its command made it; null for a kind the commands do
        /// not make. The words are the measurement as the dimension writes it, put in place of the &lt;&gt; of its own text.
        /// </summary>
        private static List<DxfEntity> Picture(DxfDimension dimension, DxfDimStyleTableRecord style, double textHeight)
        {
            switch (dimension)
            {
                case DxfRotatedDimension rotated:
                    return LinearPicture(dimension, style, textHeight, P2(rotated.XLine1Point), P2(rotated.XLine2Point), P2(rotated.DimLinePoint), rotated.Rotation);
                case DxfAlignedDimension aligned:
                    GeoPoint2 a = P2(aligned.XLine1Point);
                    GeoPoint2 b = P2(aligned.XLine2Point);
                    return LinearPicture(dimension, style, textHeight, a, b, P2(aligned.DimLinePoint), Math.Atan2(b.Y - a.Y, b.X - a.X));
                case DxfRadialDimension radial:
                    return RadialPicture(dimension, style, textHeight, P2(radial.Center), P2(radial.ChordPoint), false);
                case DxfDiametricDimension diametric:
                    GeoPoint2 chord = P2(diametric.ChordPoint);
                    GeoPoint2 far = P2(diametric.FarChordPoint);
                    return RadialPicture(dimension, style, textHeight, new GeoPoint2((chord.X + far.X) / 2.0, (chord.Y + far.Y) / 2.0), chord, true);
                case DxfPoint3AngularDimension three:
                    return AngularPicture(dimension, style, textHeight, null, null, P2(three.CenterPoint), new[] { P2(three.XLine1Point), P2(three.XLine2Point) }, P2(three.ArcPoint));
                case DxfLineAngularDimension2 two:
                    var first = new DxfLine(two.XLine1Start, two.XLine1End);
                    var second = new DxfLine(two.XLine2Start, two.XLine2End);
                    return Meet(first, second, new Tolerance(1E-9, 1E-9), out GeoPoint2 vertex)
                        ? AngularPicture(dimension, style, textHeight, first, second, vertex, new GeoPoint2[2], P2(two.ArcPoint))
                        : null;
                default:
                    return null;
            }
        }

        private static List<DxfEntity> LinearPicture(DxfDimension dimension, DxfDimStyleTableRecord style, double textHeight, GeoPoint2 p1, GeoPoint2 p2, GeoPoint2 onLine, double angle)
        {
            var along = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
            double measured = Math.Abs((p2 - p1).X * along.X + (p2 - p1).Y * along.Y);

            if (measured <= 1E-12)
            {
                return null;
            }

            GeoPoint2 q1 = onLine + along * ((p1 - onLine).X * along.X + (p1 - onLine).Y * along.Y);
            GeoPoint2 q2 = onLine + along * ((p2 - onLine).X * along.X + (p2 - onLine).Y * along.Y);
            return Linear(style, textHeight, p1, p2, q1, q2, Formatted(dimension, measured, string.Empty));
        }

        /// <summary>
        /// The picture of a radius or a diameter. Where the words stand along the line is read from the block as it was (its
        /// MTEXT's place along the chord), as the record keeps no point of them; on the rim where the block has none.
        /// </summary>
        private static List<DxfEntity> RadialPicture(DxfDimension dimension, DxfDimStyleTableRecord style, double textHeight, GeoPoint2 center, GeoPoint2 chord, bool diameter)
        {
            double radius = (chord - center).Length;

            if (radius <= 1E-12)
            {
                return null;
            }

            GeoVector2 outward = (chord - center) / radius;
            DxfMText words = dimension.DimBlock.OfType<DxfMText>().FirstOrDefault();
            double reach = words == null ? radius : (P2(words.Location) - center).X * outward.X + (P2(words.Location) - center).Y * outward.Y;
            GeoPoint2 location = center + outward * reach;
            GeoPoint2 far = center - outward * radius;
            return Radial(style, textHeight, center, radius, chord, far, location, diameter, Formatted(dimension, diameter ? 2.0 * radius : radius, diameter ? "%%c" : "R"));
        }

        private static List<DxfEntity> AngularPicture(DxfDimension dimension, DxfDimStyleTableRecord style, double textHeight, DxfLine first, DxfLine second, GeoPoint2 vertex, GeoPoint2[] points, GeoPoint2 location)
        {
            var rays = new GeoVector2[2];
            var near = new double[2];
            var far = new double[2];
            double radius = (location - vertex).Length;
            AngularSector(first, second, vertex, points, location, rays, near, far, out double from, out double sweep);

            if (sweep <= 1E-12 || radius <= 1E-12)
            {
                return null;
            }

            return Angular(style, textHeight, vertex, radius, from, sweep, near, far, Formatted(dimension, sweep, string.Empty));
        }

        /// <summary>The words of a dimension of a drawing: the measurement as it writes it, in place of the &lt;&gt; of its own text.</summary>
        private static string Formatted(DxfDimension dimension, double measured, string prefix)
        {
            string number;

            try
            {
                number = prefix + dimension.FormatMeasurement(measured, string.Empty);
            }
            catch (Exception)
            {
                number = prefix + measured.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            }

            string typed = dimension.DimensionText;
            return string.IsNullOrEmpty(typed) ? number : typed.Replace("<>", number);
        }

        /// <summary>
        /// Draws the sample dimensions of the Dimension Style Manager's preview into model space of a drawing of its own, in a
        /// style of that drawing: a linear one of 32 across a part's top, a radius of 12 and an angle of 60 degrees, each a
        /// dimension with its block made as the DIM commands make it.
        /// </summary>
        internal static void DrawSamples(DxfDatabase database, DxfDimStyleTableRecord style)
        {
            var part = new[] { new GeoPoint2(0, 0), new GeoPoint2(32, 0), new GeoPoint2(32, 20), new GeoPoint2(0, 20) };

            for (int i = 0; i < part.Length; i++)
            {
                database.ModelSpace.AppendEntity(new DxfLine(P3(part[i]), P3(part[(i + 1) % part.Length])));
            }

            database.ModelSpace.AppendEntity(new DxfCircle(P3(new GeoPoint2(56, 10)), new GeoVector3(0, 0, 1), 12.0));
            database.ModelSpace.AppendEntity(new DxfLine(P3(new GeoPoint2(0, -40)), P3(new GeoPoint2(36, -40))));
            database.ModelSpace.AppendEntity(new DxfLine(P3(new GeoPoint2(0, -40)), P3(new GeoPoint2(18, -40 + 31.176914536239792))));

            Sample(database, style, new DxfRotatedDimension(0.0, P3(part[3]), P3(part[2]), P3(new GeoPoint2(16, 32)), string.Empty, null));
            GeoVector2 toward = new GeoVector2(1, 1) / Math.Sqrt(2.0);
            GeoPoint2 chord = new GeoPoint2(56, 10) + toward * 12.0;
            Sample(database, style, new DxfRadialDimension(P3(new GeoPoint2(56, 10)), P3(chord), 8.0, string.Empty, null));
            Sample(database, style, new DxfPoint3AngularDimension(P3(new GeoPoint2(0, -40)), P3(new GeoPoint2(36, -40)), P3(new GeoPoint2(18, -40 + 31.176914536239792)), P3(new GeoPoint2(24, -26)), string.Empty, null));
        }

        /// <summary>Adds one sample dimension with an empty block of its own, then draws its picture into the block.</summary>
        private static void Sample(DxfDatabase database, DxfDimStyleTableRecord style, DxfDimension dimension)
        {
            var block = new DxfBlockTableRecord { Name = "*D" };
            database.BlockTable.Add(block);
            dimension.DimensionStyle = style;
            dimension.DimBlock = block;

            // The radius's words stand out past the rim by its leader, where the sample's MTEXT would have put them.
            if (dimension is DxfRadialDimension radial)
            {
                GeoPoint2 center = P2(radial.Center);
                GeoPoint2 rim = P2(radial.ChordPoint);
                GeoVector2 outward = (rim - center) / (rim - center).Length;
                block.AppendEntity(new DxfMText { Location = P3(rim + outward * radial.LeaderLength), TextHeight = 1.0, Contents = string.Empty });
            }

            database.ModelSpace.AppendEntity(dimension);
            Redraw(dimension);
        }

        private static GeoPoint2 P2(GeoPoint3 point) => new GeoPoint2(point.X, point.Y);
    }
}
