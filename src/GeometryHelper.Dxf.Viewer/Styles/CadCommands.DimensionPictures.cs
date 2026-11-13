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
        /// linear, aligned, ordinate, radius, jogged radius, diameter, angular or arc length dimension whose block is its own anonymous one, a linear one's
        /// jog of DIMJOGLINE with it. The entities of the block
        /// are erased and the new picture appended, broken again where the dimension keeps DIMBREAK's breaks (BREAKDATA), and the
        /// middle of the words at their default place and the measurement written into the dimension (groups 11 and 42), as
        /// AutoCAD writes them with the block it draws. False, and nothing changed, for another kind or a block not its own.
        /// </summary>
        internal static bool Redraw(DxfDimension dimension)
        {
            DxfBlockTableRecord block = dimension.DimBlock;

            if (block == null || !block.IsAnonymous)
            {
                return false;
            }

            if (dimension.Annotative == DxfAnnotativeStates.True && dimension.DimensionPictures().Count > 0)
            {
                return RedrawAnnotative(dimension);
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

            BreakAgain(dimension);
            Recorded(dimension, picture);
            return true;
        }

        /// <summary>
        /// The picture of a dimension of the drawing from its points, as its command made it; null for a kind the commands do
        /// not make. The words are the measurement as the dimension writes it, put in place of the &lt;&gt; of its own text,
        /// dressed as its variables say (DIMTFILL's mask, DIMTXTDIRECTION; see <see cref="Dressed"/>).
        /// </summary>
        private static List<DxfEntity> Picture(DxfDimension dimension, DxfDimStyleTableRecord style, double textHeight)
        {
            List<DxfEntity> picture = KindPicture(dimension, style, textHeight);

            if (picture == null)
            {
                return null;
            }

            // Words moved by DIMTEDIT stand where they were put, and words turned by DIMEDIT or DIMTEDIT read at their angle. Those
            // of a linear dimension, a radius, a diameter or an angle are put by their picture about the point, which may be one of
            // its line or arc.
            DxfMText words = picture.OfType<DxfMText>().FirstOrDefault();
            bool laidOut = !(dimension is DxfOrdinateDimension);

            if (words != null && !dimension.UsingDefaultTextPosition && !laidOut)
            {
                words.Location = dimension.TextPosition;
            }

            if (words != null && dimension.TextRotation != 0.0)
            {
                words.Rotation = Turned(dimension.TextRotation);
            }

            // The box of a DIMGAP below nought goes with the words: the four sides after them drawn again about them.
            int index = words == null ? -1 : picture.IndexOf(words);

            if (
                style.Dimgap < 0.0
                && index >= 0
                && index + 4 < picture.Count
                && picture.Skip(index + 1).Take(4).All(e => e is DxfLine)
            )
            {
                double scale = style.Dimscale > 0.0 ? style.Dimscale : 1.0;
                double width = WordsWidth(style, words.Contents, words.TextHeight, out double tall);
                List<DxfLine> sides = Frame(
                    P2(words.Location),
                    words.Rotation,
                    width,
                    tall,
                    Math.Abs(style.Dimgap) * scale
                );

                for (int i = 0; i < 4; i++)
                {
                    picture[index + 1 + i] = Lined(sides[i], style.Dimclrd, style.Dimlwd);
                }
            }

            Dressed(style, picture);
            return picture;
        }

        private static List<DxfEntity> KindPicture(
            DxfDimension dimension,
            DxfDimStyleTableRecord style,
            double textHeight
        )
        {
            switch (dimension)
            {
                case DxfRotatedDimension rotated:
                    return LinearPicture(
                        dimension,
                        style,
                        textHeight,
                        P2(rotated.XLine1Point),
                        P2(rotated.XLine2Point),
                        P2(rotated.DimLinePoint),
                        rotated.Rotation
                    );
                case DxfAlignedDimension aligned:
                    GeoPoint2 a = P2(aligned.XLine1Point);
                    GeoPoint2 b = P2(aligned.XLine2Point);
                    return LinearPicture(
                        dimension,
                        style,
                        textHeight,
                        a,
                        b,
                        P2(aligned.DimLinePoint),
                        Math.Atan2(b.Y - a.Y, b.X - a.X)
                    );
                case DxfOrdinateDimension ordinate:
                    return OrdinatePicture(dimension, style, textHeight, ordinate);
                case DxfRadialDimension radial:
                    return RadialPicture(dimension, style, textHeight, P2(radial.Center), P2(radial.ChordPoint), false);
                case DxfDiametricDimension diametric:
                    GeoPoint2 chord = P2(diametric.ChordPoint);
                    GeoPoint2 far = P2(diametric.FarChordPoint);
                    return RadialPicture(
                        dimension,
                        style,
                        textHeight,
                        new GeoPoint2((chord.X + far.X) / 2.0, (chord.Y + far.Y) / 2.0),
                        chord,
                        true
                    );
                case DxfPoint3AngularDimension three:
                    return AngularPicture(
                        dimension,
                        style,
                        textHeight,
                        null,
                        null,
                        P2(three.CenterPoint),
                        new[] { P2(three.XLine1Point), P2(three.XLine2Point) },
                        P2(three.ArcPoint)
                    );
                case DxfArcDimension arcLength:
                    return ArcLengthPicture(arcLength, style, textHeight, out _);
                case DxfRadialDimensionLarge jogged:
                    return JoggedPicture(jogged, style, textHeight);
                case DxfLineAngularDimension2 two:
                    var first = new DxfLine(two.XLine1Start, two.XLine1End);
                    var second = new DxfLine(two.XLine2Start, two.XLine2End);
                    return Meet(first, second, new Tolerance(1E-9, 1E-9), out GeoPoint2 vertex)
                        ? AngularPicture(
                            dimension,
                            style,
                            textHeight,
                            first,
                            second,
                            vertex,
                            new GeoPoint2[2],
                            P2(two.ArcPoint)
                        )
                        : null;
                default:
                    return null;
            }
        }

        private static List<DxfEntity> LinearPicture(
            DxfDimension dimension,
            DxfDimStyleTableRecord style,
            double textHeight,
            GeoPoint2 p1,
            GeoPoint2 p2,
            GeoPoint2 onLine,
            double angle
        )
        {
            var along = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
            double measured = Math.Abs((p2 - p1).X * along.X + (p2 - p1).Y * along.Y);

            if (measured <= 1E-12)
            {
                return null;
            }

            double oblique = ObliqueOf(dimension);
            GeoPoint2 q1 = Foot(p1, onLine, along, oblique);
            GeoPoint2 q2 = Foot(p2, onLine, along, oblique);
            string words = Formatted(dimension, measured, string.Empty);
            GeoPoint2? jog = JogOf(dimension);
            GeoPoint2? placed = dimension.UsingDefaultTextPosition ? (GeoPoint2?)null : P2(dimension.TextPosition);
            return Linear(
                style,
                style.Dimscale,
                textHeight,
                p1,
                p2,
                q1,
                q2,
                words,
                placed,
                jog,
                jog.HasValue ? JogHeight(dimension, style, textHeight) : 0.0,
                dimension.TextRotation != 0.0 ? Turned(dimension.TextRotation) : (double?)null
            );
        }

        /// <summary>
        /// The direction a linear dimension's extension lines run in, in radians from the x axis, as DIMEDIT Oblique slants them;
        /// nought for none, where they stand square to the dimension line. The dimension keeps the angle from its line (group
        /// 52, AutoCAD's Oblique), so this is the line's angle and that one together: AutoCAD 2021 gives an Oblique of 30 to a
        /// DIMLINEAR rotated 30 that DIMEDIT slants to 60, and draws its extension lines at 60.
        /// </summary>
        internal static double ObliqueOf(DxfDimension dimension)
        {
            double oblique =
                dimension is DxfRotatedDimension rotated ? rotated.Oblique
                : dimension is DxfAlignedDimension aligned ? aligned.Oblique
                : 0.0;
            return oblique == 0.0 ? 0.0 : LineAngleOf(dimension) + oblique;
        }

        /// <summary>
        /// The angle of a linear dimension's line in radians from the x axis, which its oblique angle is measured from: a
        /// rotated one's rotation, an aligned one's direction from its first extension line's origin to its second's.
        /// </summary>
        internal static double LineAngleOf(DxfDimension dimension)
        {
            switch (dimension)
            {
                case DxfRotatedDimension rotated:
                    return rotated.Rotation;
                case DxfAlignedDimension aligned:
                    return Math.Atan2(
                        aligned.XLine2Point.Y - aligned.XLine1Point.Y,
                        aligned.XLine2Point.X - aligned.XLine1Point.X
                    );
                default:
                    return 0.0;
            }
        }

        /// <summary>
        /// Where the extension line from a point meets the dimension line through <paramref name="onLine"/> along
        /// <paramref name="along"/>: square to it, or along the oblique angle where one is set that is not along the line itself.
        /// </summary>
        internal static GeoPoint2 Foot(GeoPoint2 point, GeoPoint2 onLine, GeoVector2 along, double oblique)
        {
            if (oblique != 0.0)
            {
                var slant = new GeoVector2(Math.Cos(oblique), Math.Sin(oblique));
                double across = Cross(slant, along);

                if (Math.Abs(across) > 1E-9)
                {
                    return point + slant * (Cross(onLine - point, along) / across);
                }
            }

            return onLine + along * ((point - onLine).X * along.X + (point - onLine).Y * along.Y);
        }

        /// <summary>
        /// How wide and how high the words of a picture are, as AutoCAD 2021 measures them to lay the picture out: in the font of
        /// the style's text style (DIMTXSTY), as <see cref="CadDimensionWords"/> measures them; "R20" of arial.ttf 2.5 high is
        /// 5.965 wide and 2.553 high. Where the font is not measured, <paramref name="tall"/> is the height and the width 0.7
        /// of the height a letter, the codes %%c, %%d and %%p a letter each, as the diameter sign, the degree sign and
        /// plus-minus they stand for.
        /// </summary>
        internal static double WordsWidth(DxfDimStyleTableRecord style, string words, double height, out double tall)
        {
            DxfTextStyleTableRecord textStyle;

            try
            {
                textStyle = style?.Dimtxsty;
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException)
            {
                textStyle = null;
            }

            if (CadDimensionWords.TryMeasure(textStyle, words, height, out double width, out tall))
            {
                return width;
            }

            tall = height;
            string letters = words ?? string.Empty;

            foreach (string code in new[] { "%%c", "%%d", "%%p" })
            {
                letters = letters.Replace(code, "#").Replace(code.ToUpperInvariant(), "#");
            }

            return 0.7 * height * letters.Length;
        }

        /// <summary>
        /// The picture of a radius or a diameter. Where the words stand along the line is read from the block as it was (its
        /// MTEXT's place along the chord), as the record keeps no point of them, or from the text's place where it was moved
        /// (flag 128); on the rim where the block has none. Words moved under DIMTMOVE 2 stand at the text's place itself.
        /// </summary>
        private static List<DxfEntity> RadialPicture(
            DxfDimension dimension,
            DxfDimStyleTableRecord style,
            double textHeight,
            GeoPoint2 center,
            GeoPoint2 chord,
            bool diameter
        )
        {
            double radius = (chord - center).Length;

            if (radius <= 1E-12)
            {
                return null;
            }

            GeoVector2 outward = (chord - center) / radius;
            DxfMText words = dimension.DimBlock.OfType<DxfMText>().FirstOrDefault();
            // Words at their default place with a leader of nought, as AutoCAD 2021 writes a radius or a diameter the fit or the
            // room next to the rim put them in (36 and 35), are laid out afresh as for a point on the rim.
            double leader =
                dimension is DxfRadialDimension radial ? radial.LeaderLength
                : dimension is DxfDiametricDimension diametric ? diametric.LeaderLength
                : 0.0;
            GeoPoint2? placed =
                !dimension.UsingDefaultTextPosition ? P2(dimension.TextPosition)
                : words == null || leader <= 1E-9 ? (GeoPoint2?)null
                : P2(words.Location);
            double reach =
                placed == null ? radius : (placed.Value - center).X * outward.X + (placed.Value - center).Y * outward.Y;
            // Words put off the line under DIMTMOVE 2 stand where they were put.
            bool free = !dimension.UsingDefaultTextPosition && dimension.Dimtmove == 2;
            GeoPoint2 location = free ? placed.Value : center + outward * reach;
            GeoPoint2 far = center - outward * radius;
            return Radial(
                style,
                style.Dimscale,
                textHeight,
                center,
                radius,
                chord,
                far,
                location,
                diameter,
                Formatted(dimension, diameter ? 2.0 * radius : radius, diameter ? "%%c" : "R"),
                !dimension.UsingDefaultTextPosition,
                free
            );
        }

        private static List<DxfEntity> AngularPicture(
            DxfDimension dimension,
            DxfDimStyleTableRecord style,
            double textHeight,
            DxfLine first,
            DxfLine second,
            GeoPoint2 vertex,
            GeoPoint2[] points,
            GeoPoint2 location
        )
        {
            var rays = new GeoVector2[2];
            var near = new double[2];
            var far = new double[2];
            double radius = (location - vertex).Length;
            AngularSector(first, second, vertex, points, location, rays, near, far, out double from, out double sweep);

            // Three points along one ray, the arc on it, measure nothing and are drawn so (AngularSector).
            if ((sweep <= 1E-12 && first != null) || radius <= 1E-12)
            {
                return null;
            }

            GeoPoint2? placed = dimension.UsingDefaultTextPosition ? (GeoPoint2?)null : P2(dimension.TextPosition);
            List<DxfEntity> picture = AngularLaidOut(
                style,
                style.Dimscale,
                textHeight,
                vertex,
                radius,
                from,
                sweep,
                near,
                far,
                Formatted(dimension, sweep, string.Empty),
                placed,
                0.0,
                null,
                out bool outside,
                out _
            );

            // Kept for the context of the scale this picture is drawn at, whose arc point depends on whether the words fit.
            DxfMText words = picture?.OfType<DxfMText>().FirstOrDefault();

            if (words != null)
            {
                ArcFits.Remove(words);
                ArcFits.Add(words, new ArcFit(vertex, from, sweep, outside));
            }

            return picture;
        }

        /// <summary>
        /// How wide the words of a radius or a diameter of the drawing are for a measurement (the radius, or the diameter), as its
        /// picture measures them: "R13.5000" of arial.ttf 0.18 high 0.14 wider than "R9.0000".
        /// </summary>
        internal static double RoundWordsWidth(DxfDimension dimension, double measured)
        {
            DxfDimStyleTableRecord data = dimension.GetDimstyleData();
            double scale = data.Dimscale > 0.0 ? data.Dimscale : 1.0;
            string prefix = dimension is DxfDiametricDimension ? "%%c" : "R";
            return WordsWidth(data, Formatted(dimension, measured, prefix), data.Dimtxt * scale, out _);
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
            database.ModelSpace.AppendEntity(
                new DxfLine(P3(new GeoPoint2(0, -40)), P3(new GeoPoint2(18, -40 + 31.176914536239792)))
            );

            Sample(
                database,
                style,
                new DxfRotatedDimension(0.0, P3(part[3]), P3(part[2]), P3(new GeoPoint2(16, 32)), string.Empty, null)
            );
            GeoVector2 toward = new GeoVector2(1, 1) / Math.Sqrt(2.0);
            GeoPoint2 chord = new GeoPoint2(56, 10) + toward * 12.0;
            Sample(
                database,
                style,
                new DxfRadialDimension(P3(new GeoPoint2(56, 10)), P3(chord), 8.0, string.Empty, null)
            );
            Sample(
                database,
                style,
                new DxfPoint3AngularDimension(
                    P3(new GeoPoint2(0, -40)),
                    P3(new GeoPoint2(36, -40)),
                    P3(new GeoPoint2(18, -40 + 31.176914536239792)),
                    P3(new GeoPoint2(24, -26)),
                    string.Empty,
                    null
                )
            );
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
                block.AppendEntity(
                    new DxfMText
                    {
                        Location = P3(rim + outward * radial.LeaderLength),
                        TextHeight = 1.0,
                        Contents = string.Empty,
                    }
                );
            }

            database.ModelSpace.AppendEntity(dimension);
            Redraw(dimension);
        }

        private static GeoPoint2 P2(GeoPoint3 point) => new GeoPoint2(point.X, point.Y);
    }
}
