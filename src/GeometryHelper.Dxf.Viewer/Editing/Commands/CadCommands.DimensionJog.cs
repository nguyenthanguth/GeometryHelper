using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>DIMJOGLINE: a jog in the line of a linear dimension, as a measurement not drawn to scale is marked.</summary>
    /// <remarks>
    /// AutoCAD keeps the jog in the dimension's extended data, as the AutoCAD .NET Developer's Guide sets it out ("Dimension
    /// joglines"): the application ACAD_DSTYLE_DIMJAG_POSITION, then 1070 387, 1070 3, 1070 389 and the point of the jog as a
    /// 1010. Its height is the style's jog height factor (ACAD_DSTYLE_DIMJAG, 1070 388 then a 1040, 1.5 where none is
    /// given), times the height of the words: 3.75 for words 2.5 high.
    /// </remarks>
    internal static partial class CadCommands
    {
        private const string JogApplication = "ACAD_DSTYLE_DIMJAG_POSITION";

        /// <summary>
        /// DIMJOGLINE: a linear or aligned dimension selected, then the point of the jog on its line, or Enter for halfway
        /// between its first extension line and its words; Remove takes the jog of a dimension out. One undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> DimJogLine(CadCommandContext c)
        {
            yield return CadPrompt.ForEntity("Select dimension to add jog", new[] { "Remove" });
            bool remove = c.Input.Is("Remove");

            if (remove)
            {
                yield return CadPrompt.ForEntity("Select jog to remove");
            }

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            DxfDimension dimension = c.Document.FindEntity(c.Input.Handles[0]) as DxfDimension;

            if (!(dimension is DxfRotatedDimension) && !(dimension is DxfAlignedDimension))
            {
                c.Write("Jogs can be added to linear dimensions only.");
                yield break;
            }

            List<DxfDimension> only = Changeable(c, new[] { dimension.Handle }, "Jog", d => true);

            if (only.Count == 0)
            {
                yield break;
            }

            if (remove)
            {
                if (JogOf(dimension) == null)
                {
                    c.Write("The dimension selected has no jog.");
                    yield break;
                }

                ChangeDimensions(c, only, "Jog", "Remove", d => d.XData = new[] { new DxfTypedValue(1001, JogApplication) });
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify jog location (or press ENTER)");
            GeoPoint2 at;

            if (c.Input.IsPoint)
            {
                at = c.Input.Point;
            }
            else if (c.Input.IsNone)
            {
                at = DefaultJog(dimension);
            }
            else
            {
                yield break;
            }

            LinearPoints(dimension, out GeoPoint2 q1, out GeoPoint2 q2);
            GeoVector2 along = (q2 - q1) / (q2 - q1).Length;
            GeoPoint2 on = Foot(at, q1, along, 0.0);
            ChangeDimensions(c, only, "Jog", Values(on.X, on.Y), d => d.XData = new[]
            {
                new DxfTypedValue(1001, JogApplication),
                new DxfTypedValue(1070, "387"),
                new DxfTypedValue(1070, "3"),
                new DxfTypedValue(1070, "389"),
                new DxfTypedValue(1010, on.X.ToString("R", CultureInfo.InvariantCulture)),
                new DxfTypedValue(1020, on.Y.ToString("R", CultureInfo.InvariantCulture)),
                new DxfTypedValue(1030, d.Elevation.ToString("R", CultureInfo.InvariantCulture)),
            });
        }

        /// <summary>Where a linear dimension's extension lines meet its line: its first, and its second.</summary>
        private static void LinearPoints(DxfDimension dimension, out GeoPoint2 q1, out GeoPoint2 q2)
        {
            GeoPoint2 p1;
            GeoPoint2 p2;
            GeoPoint2 onLine;
            double angle;

            if (dimension is DxfRotatedDimension rotated)
            {
                p1 = P2(rotated.XLine1Point);
                p2 = P2(rotated.XLine2Point);
                onLine = P2(rotated.DimLinePoint);
                angle = rotated.Rotation;
            }
            else
            {
                var aligned = (DxfAlignedDimension)dimension;
                p1 = P2(aligned.XLine1Point);
                p2 = P2(aligned.XLine2Point);
                onLine = P2(aligned.DimLinePoint);
                angle = Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);
            }

            var along = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
            double oblique = ObliqueOf(dimension);
            q1 = Foot(p1, onLine, along, oblique);
            q2 = Foot(p2, onLine, along, oblique);
        }

        /// <summary>
        /// Where AutoCAD puts a jog when Enter is pressed: halfway between the first extension line and the near end of the
        /// words, on the line; halfway along it where the words stand elsewhere.
        /// </summary>
        private static GeoPoint2 DefaultJog(DxfDimension dimension)
        {
            LinearPoints(dimension, out GeoPoint2 q1, out GeoPoint2 q2);
            double length = (q2 - q1).Length;
            GeoVector2 along = (q2 - q1) / length;
            List<DxfEntity> picture = dimension.DimBlock?.ToList() ?? new List<DxfEntity>();
            DxfMText words = picture.OfType<DxfMText>().FirstOrDefault();

            if (words == null)
            {
                return q1 + along * (length / 2.0);
            }

            DxfDimStyleTableRecord data = dimension.GetDimstyleData();
            double height = data.Dimtxt * (data.Dimscale > 0.0 ? data.Dimscale : 1.0);
            GeoVector2 shift = new GeoVector2(dimension.DimBlockPosition.X, dimension.DimBlockPosition.Y);
            GeoPoint2 middle = P2(words.Location) + shift;
            double t = (middle - q1).X * along.X + (middle - q1).Y * along.Y;
            double near = Math.Abs(t) - WordsWidth(picture, words.Contents, height) / 2.0;

            if (t <= 0.0 || t >= length || near <= 0.0)
            {
                return q1 + along * (length / 2.0);
            }

            return q1 + along * (Math.Sign(t) * near / 2.0);
        }

        /// <summary>The point of a dimension's jog, from its extended data (ACAD_DSTYLE_DIMJAG_POSITION); null where it has none.</summary>
        private static GeoPoint2? JogOf(DxfDimension dimension)
        {
            IReadOnlyList<DxfTypedValue> stored;

            try
            {
                stored = dimension.GetXDataForApplication(JogApplication);
            }
            catch (Exception)
            {
                return null;
            }

            bool on = false;

            for (int i = 0; stored != null && i + 1 < stored.Count; i++)
            {
                if (stored[i].TypeCode == 1070 && stored[i].Value.Trim() == "387" && stored[i + 1].TypeCode == 1070)
                {
                    on = stored[i + 1].Value.Trim() != "0";
                }

                if (on && stored[i].TypeCode == 1070 && stored[i].Value.Trim() == "389" && i + 3 < stored.Count && stored[i + 1].TypeCode == 1010 && stored[i + 2].TypeCode == 1020
                    && double.TryParse(stored[i + 1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double x)
                    && double.TryParse(stored[i + 2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
                {
                    return new GeoPoint2(x, y);
                }
            }

            return null;
        }

        /// <summary>
        /// How high a dimension's jog is drawn: the jog height factor (ACAD_DSTYLE_DIMJAG, 1070 388, of the dimension's
        /// overrides, else of its style; 1.5 where neither gives one) times the height of its words.
        /// </summary>
        private static double JogHeight(DxfDimension dimension, DxfDimStyleTableRecord style, double textHeight)
        {
            double factor = 1.5;

            foreach (DxfDBObject holder in new DxfDBObject[] { dimension.DimensionStyle, dimension })
            {
                try
                {
                    IReadOnlyList<DxfTypedValue> stored = holder?.GetXDataForApplication("ACAD_DSTYLE_DIMJAG");

                    for (int i = 0; stored != null && i + 1 < stored.Count; i++)
                    {
                        if (stored[i].TypeCode == 1070 && stored[i].Value.Trim() == "388" && stored[i + 1].TypeCode == 1040
                            && double.TryParse(stored[i + 1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && value > 0.0)
                        {
                            factor = value;
                        }
                    }
                }
                catch (Exception)
                {
                    // Data that cannot be read leaves the factor as it was.
                }
            }

            return factor * textHeight * (style.Dimscale > 0.0 ? style.Dimscale : 1.0);
        }
    }
}
