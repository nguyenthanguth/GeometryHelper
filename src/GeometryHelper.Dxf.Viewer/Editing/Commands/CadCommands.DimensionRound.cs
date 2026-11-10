using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The pictures of the round dimensions, radius, diameter and angular, laid out from the style as AutoCAD 2021 lays them
    /// out: where the words stand by DIMTAD, DIMTVP, DIMJUST, DIMTIH and DIMTOH, what moves out where they do not fit (DIMATFIT,
    /// DIMTIX, DIMSOXD, DIMTOFL), the centre mark of DIMCEN, and the box of a DIMGAP below nought.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// The picture of a radius or a diameter, as AutoCAD 2021 draws it from where its line was put. Put outside the circle,
        /// the words stand there: the arrowhead outside the rim, pointing in, the line from its back to the words, and inside
        /// the line from the centre (or the far rim, with the far arrowhead outside it) to the rim where DIMTOFL is on, else the
        /// centre mark of DIMCEN. Put inside, DIMATFIT 3 keeps the words there, no nearer the rim than two arrowheads and DIMGAP,
        /// with one arrowhead inside the rim; DIMATFIT 0 to 2 put them in the middle of the radius line (of the diameter, at the
        /// centre between two arrowheads) where they fit, else outside past the rim, two arrowheads clear of it, the arrowheads
        /// outside too unless DIMATFIT 2 keeps them in; DIMTIX puts the words outside.
        /// </summary>
        /// <remarks>
        /// A radius of 20 drawn with ISO-25's sizes and its line put 45 from the centre has its arrowhead from 20 to 22.5 out,
        /// its line from 22.5 on under the words and a line from the centre to the rim; put 8 from the centre, its arrowhead
        /// from 20 in to 17.5 and its line from 17.5 to past the words. The words stand on the line (DIMTAD 0, the line stopped
        /// short of them), above it as they read (1, 2, 3) or below (4); kept level (DIMTOH outside, DIMTIH inside) they stand on
        /// a level landing from the end of the line. <paramref name="placed"/> says the words were put by hand (DIMTEDIT): they
        /// stay where they are put along the line. <paramref name="free"/> says they were put off it under DIMTMOVE 2: they stand
        /// at <paramref name="location"/> itself, turned along the line, and the line is drawn inside as though they were not
        /// there, from the centre (the back of the far arrowhead) to the back of the arrowhead inside the rim, broken where it
        /// runs through them.
        /// </remarks>
        internal static List<DxfEntity> Radial(DxfDimStyleTableRecord style, double dimscale, double textHeight, GeoPoint2 center, double radius, GeoPoint2 chord, GeoPoint2 far, GeoPoint2 location, bool diameter, string words, bool placed = false, bool free = false)
        {
            Sizes(style, dimscale, textHeight, out double scale, out double arrow, out double height, out double gap, out _, out _);
            var picture = new List<DxfEntity>();
            GeoVector2 u = (chord - center) / radius;
            double along = (location - center).X * u.X + (location - center).Y * u.Y;
            double aligned = Readable(Math.Atan2(u.Y, u.X));
            DxfMText text = (DxfMText)Words(style, default, aligned, height, words, scale);
            // The words laid out as high as the font measures them, past the height their text is written with.
            double width = WordsWidth(style, words, height, out height);
            string nearName = ArrowOf(style, DxfDimArrowFlag.FirstArrow);
            string farName = ArrowOf(style, DxfDimArrowFlag.SecondArrow);
            double trim = CadArrowheads.TakesNoRoom(nearName) ? 0.0 : arrow;
            double farTrim = CadArrowheads.TakesNoRoom(farName) ? 0.0 : arrow;

            if (free)
            {
                return FreeRadial(style, picture, text, center, chord, far, location, u, aligned, diameter, width, height, gap, arrow, trim, farTrim);
            }

            var words2 = new RoundWords(style, scale, height, gap, width);
            var lines = new List<KeyValuePair<GeoPoint2, GeoPoint2>>();
            bool arrowOut;
            bool farArrow = false;
            bool farOut = false;
            bool nearArrow = true;
            bool single = false;
            bool textOut;
            double at;

            if (along > radius + 1E-9)
            {
                arrowOut = true;
                textOut = true;
                at = along;
                farArrow = diameter && style.Dimtofl;
                farOut = true;
            }
            else if (!placed && style.Dimtix)
            {
                arrowOut = true;
                textOut = true;
                at = radius + 2.0 * arrow + words2.Beside;
                farArrow = diameter;
                farOut = true;
            }
            else if (placed || style.Dimatfit == 3)
            {
                arrowOut = false;
                textOut = false;
                single = true;
                at = Math.Min(along, radius - 2.0 * arrow - gap - width / 2.0);

                // AutoCAD 2021 draws no arrowhead on a radius whose words stand inside next to the first extension line.
                nearArrow = diameter || style.Dimjust != 1;
            }
            else if (!diameter)
            {
                // The words fit where the radius takes them and a gap each side, the arrowhead not counted, as AutoCAD 2021 fits
                // "R6" 4.03 wide in a radius of 6 and not "R5" in one of 5; DIMATFIT 2 keeps the arrowhead in where it and a gap fit.
                textOut = radius < width + 2.0 * gap;
                arrowOut = textOut && !(style.Dimatfit == 2 && radius >= trim + gap);
                at = !textOut ? (radius - trim) / 2.0 : radius + (arrowOut ? 2.0 * arrow : trim) + words2.Beside;
            }
            else
            {
                // The words take their width and a gap each side; on the line (DIMTAD 0) they take the arrowheads' room as well,
                // as a linear dimension's do: "%%c6" 4.357 wide fits a diameter of 6 under DIMATFIT 1, but with its arrowheads
                // under DIMATFIT 0 not even one of 9, as AutoCAD 2021 draws them.
                double needText = width + 2.0 * gap;
                double needArrows = CadArrowheads.TakesNoRoom(nearName) && CadArrowheads.TakesNoRoom(farName) ? 0.0 : 2.0 * arrow + 2.0 * gap;
                bool both = 2.0 * radius >= (words2.On(false) ? needText + 2.0 * arrow : Math.Max(needText, needArrows));
                textOut = !both && !(style.Dimatfit == 1 && 2.0 * radius >= needText);
                arrowOut = !both && !(style.Dimatfit == 2 && 2.0 * radius >= needArrows);
                at = !textOut ? 0.0 : radius + (arrowOut ? 2.0 * arrow : trim) + words2.Beside;
                farArrow = true;
                farOut = arrowOut;
            }

            bool level = textOut ? style.Dimtoh : style.Dimtih;
            GeoPoint2 knee = center + u * at;
            GeoPoint2 back = arrowOut ? chord + u * trim : chord - u * trim;
            GeoPoint2 wordsAt;
            double angle = aligned;

            if (textOut && !(along > radius + 1E-9))
            {
                // Moved out past the rim: the line from the rim (or the back of an arrowhead outside) to the words.
                double clear = arrowOut ? 2.0 * arrow : trim;
                GeoPoint2 bend = chord + u * clear;
                GeoPoint2 start = arrowOut ? back : chord;
                wordsAt = words2.Along(lines, start, bend, u, level, true, ref angle, clearOfBend: true);
            }
            else if (textOut)
            {
                wordsAt = words2.Along(lines, back, knee, u, level, true, ref angle, clearOfBend: false);
            }
            else if (single)
            {
                // Inside where they were put: the line from the back of the arrowhead in to the words.
                wordsAt = words2.Along(lines, back, knee, -u, level, false, ref angle, clearOfBend: false);
            }
            else if (diameter && !arrowOut)
            {
                // Inside between two arrowheads: the line from the back of one to the back of the other, broken about words on it.
                GeoPoint2 farBack = far + u * farTrim;
                wordsAt = words2.Centred(lines, farBack, back, center, u, level, ref angle);
            }
            else if (diameter)
            {
                // Inside with the arrowheads outside: the words at the centre, DIMTOFL's line through from rim to rim, broken
                // about words on it (DIMTAD 0) as AutoCAD 2021 breaks it, a gap clear of them.
                var through = new List<KeyValuePair<GeoPoint2, GeoPoint2>>();
                wordsAt = words2.Centred(through, far, chord, center, u, level, ref angle);

                if (style.Dimtofl)
                {
                    lines.AddRange(through);
                }
            }
            else
            {
                // In the middle of the radius line, which runs whole from the centre to the arrowhead.
                wordsAt = words2.Centred(lines, center, back, knee, u, level, ref angle);
            }

            // The line the other side of the circle: from the centre, or the far rim, to the rim (DIMTOFL).
            if (style.Dimtofl && textOut)
            {
                GeoPoint2 inner = arrowOut ? chord : chord - u * trim;
                GeoPoint2 from = diameter ? (farOut ? far : far + u * farTrim) : center;
                lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(from, inner));
            }

            if (farArrow && farOut && !CadArrowheads.TakesNoRoom(farName))
            {
                lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(far - u * arrow, far - u * (2.0 * arrow)));
            }

            // The short line the arrowhead outside stands on, where the words stand inside the diameter.
            if (diameter && arrowOut && !textOut && !CadArrowheads.TakesNoRoom(nearName))
            {
                lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(chord + u * arrow, chord + u * (2.0 * arrow)));
            }

            foreach (KeyValuePair<GeoPoint2, GeoPoint2> line in lines)
            {
                picture.Add(Lined(new DxfLine(P3(line.Key), P3(line.Value)), style.Dimclrd, style.Dimlwd));
            }

            if (nearArrow)
            {
                AddArrow(picture, style, DxfDimArrowFlag.FirstArrow, chord, arrowOut ? -u : u, arrow);
            }

            if (farArrow)
            {
                AddArrow(picture, style, DxfDimArrowFlag.SecondArrow, far, farOut ? u : -u, arrow);
            }

            text.Location = P3(wordsAt);
            text.Rotation = angle < 0.0 ? angle + 2.0 * Math.PI : angle;
            picture.Add(text);
            AddFrame(picture, style, wordsAt, angle, width, height, gap);

            // The centre mark where the line stands outside and DIMTOFL draws none through the circle.
            if (textOut && !style.Dimtofl)
            {
                AddCenterMark(picture, style, center, radius, scale);
            }

            picture.Add(Definition(diameter ? far : center));
            picture.Add(Definition(chord));
            return picture;
        }

        /// <summary>
        /// The picture of a radius or a diameter whose words were put off its line under DIMTMOVE 2, as AutoCAD 2021 draws it:
        /// the words at their point, turned along the line; the line inside from the centre, or the back of the far arrowhead,
        /// to the back of the arrowhead inside the rim, taken out for DIMGAP and half their width either side where it runs
        /// through them. A diameter of 40 at 30 degrees with "%%c40" put 8 out along its line has its line from 17.5 back to
        /// 4.229 and from 11.771 on to 17.5.
        /// </summary>
        private static List<DxfEntity> FreeRadial(DxfDimStyleTableRecord style, List<DxfEntity> picture, DxfMText text, GeoPoint2 center, GeoPoint2 chord, GeoPoint2 far, GeoPoint2 location, GeoVector2 u, double angle, bool diameter, double width, double height, double gap, double arrow, double trim, double farTrim)
        {
            GeoPoint2 from = diameter ? far + u * farTrim : center;
            var lines = new List<KeyValuePair<GeoPoint2, GeoPoint2>> { new KeyValuePair<GeoPoint2, GeoPoint2>(from, chord - u * trim) };
            double across = Cross(u, location - center);

            if (Math.Abs(across) < height / 2.0 + gap)
            {
                lines = Broken(lines, location - new GeoVector2(-u.Y, u.X) * across, u, width / 2.0 + gap);
            }

            foreach (KeyValuePair<GeoPoint2, GeoPoint2> line in lines)
            {
                picture.Add(Lined(new DxfLine(P3(line.Key), P3(line.Value)), style.Dimclrd, style.Dimlwd));
            }

            AddArrow(picture, style, DxfDimArrowFlag.FirstArrow, chord, u, arrow);

            if (diameter)
            {
                AddArrow(picture, style, DxfDimArrowFlag.SecondArrow, far, -u, arrow);
            }

            text.Location = P3(location);
            text.Rotation = angle < 0.0 ? angle + 2.0 * Math.PI : angle;
            picture.Add(text);
            AddFrame(picture, style, location, angle, width, height, gap);
            picture.Add(Definition(diameter ? far : center));
            picture.Add(Definition(chord));
            return picture;
        }

        /// <summary>
        /// The centre mark of DIMCEN about a centre, along the axes: a cross DIMCEN long each way, and where DIMCEN is below
        /// nought centre lines from twice its length out to its length past the rim, as AutoCAD 2021 draws them.
        /// </summary>
        private static void AddCenterMark(List<DxfEntity> picture, DxfDimStyleTableRecord style, GeoPoint2 center, double radius, double scale)
        {
            double mark = Math.Abs(style.Dimcen) * scale;

            if (mark <= 0.0)
            {
                return;
            }

            var x = new GeoVector2(1.0, 0.0);
            var y = new GeoVector2(0.0, 1.0);
            picture.Add(Lined(new DxfLine(P3(center - x * mark), P3(center + x * mark)), style.Dimclrd, style.Dimlwd));
            picture.Add(Lined(new DxfLine(P3(center - y * mark), P3(center + y * mark)), style.Dimclrd, style.Dimlwd));

            if (style.Dimcen < 0.0 && radius + mark > 2.0 * mark)
            {
                foreach (GeoVector2 way in new[] { -x, x, -y, y })
                {
                    picture.Add(Lined(new DxfLine(P3(center + way * (2.0 * mark)), P3(center + way * (radius + mark))), style.Dimclrd, style.Dimlwd));
                }
            }
        }

        /// <summary>
        /// The box of a DIMGAP below nought round the words: four lines DIMGAP clear of them, from the lower left as they read,
        /// as AutoCAD 2021 draws it; none where DIMGAP is nought or more.
        /// </summary>
        private static void AddFrame(List<DxfEntity> picture, DxfDimStyleTableRecord style, GeoPoint2 at, double angle, double width, double height, double gap)
        {
            if (!(style.Dimgap < 0.0))
            {
                return;
            }

            foreach (DxfLine side in Frame(at, angle, width, height, gap))
            {
                picture.Add(Lined(side, style.Dimclrd, style.Dimlwd));
            }
        }

        /// <summary>The four sides of the box round words centred on a point, turned to an angle, a gap clear of them.</summary>
        internal static List<DxfLine> Frame(GeoPoint2 at, double angle, double width, double height, double gap)
        {
            var x = new GeoVector2(Math.Cos(angle), Math.Sin(angle)) * (width / 2.0 + gap);
            var y = new GeoVector2(-Math.Sin(angle), Math.Cos(angle)) * (height / 2.0 + gap);
            GeoPoint2[] corners = { at - x - y, at + x - y, at + x + y, at - x + y };
            var sides = new List<DxfLine>();

            for (int i = 0; i < 4; i++)
            {
                sides.Add(new DxfLine(P3(corners[i]), P3(corners[(i + 1) % 4])));
            }

            return sides;
        }

        /// <summary>
        /// Where the words of a round dimension stand about a line, from DIMTAD, DIMTVP and the gap: on it, above it as they read,
        /// or below; and kept level, on a level landing.
        /// </summary>
        private sealed class RoundWords
        {
            private readonly int _tad;
            private readonly double _lift;
            private readonly double _onOffset;
            private readonly double _gap;
            private readonly double _width;
            private readonly double _height;
            private readonly bool _box;
            private readonly double _arrow;

            internal RoundWords(DxfDimStyleTableRecord style, double scale, double height, double gap, double width)
            {
                _arrow = style.Dimasz * scale;
                _tad = style.Dimtad;
                _box = style.Dimgap < 0.0;
                _gap = gap;
                _width = width;
                _height = height;
                _lift = gap + height / 2.0 + (_box ? gap : 0.0);

                // DIMTVP with DIMTAD 0: the words that far above the line, times their height; from 0.7 up they stand off it.
                if (_tad == 0 && Math.Abs(style.Dimtvp) >= 0.7)
                {
                    _tad = 1;
                    _lift = style.Dimtvp * height;
                }
                else if (_tad == 0)
                {
                    _onOffset = style.Dimtvp * height;
                }
            }

            /// <summary>How far past the bend the middle of words beside a line stands: DIMGAP and half their width.</summary>
            internal double Beside => _gap + _width / 2.0;

            /// <summary>Whether the words stand on the line: DIMTAD 0, and for words kept level inside, DIMTAD 1 and 4 as well.</summary>
            internal bool On(bool levelInside) => _tad == 0 || (levelInside && (_tad == 1 || _tad == 4));

            private double Side => _tad == 4 ? -1.0 : 1.0;

            /// <summary>
            /// Words at the end of a line from <paramref name="start"/> that runs <paramref name="way"/> to <paramref name="bend"/>:
            /// along it, on it (the line stopped a gap short of them) or beside it (run on under them); or kept level on a landing.
            /// </summary>
            internal GeoPoint2 Along(List<KeyValuePair<GeoPoint2, GeoPoint2>> lines, GeoPoint2 start, GeoPoint2 bend, GeoVector2 way, bool level, bool outside, ref double angle, bool clearOfBend)
            {
                bool on = On(level && !outside);

                if (level)
                {
                    angle = 0.0;
                    double flat = way.X < -1E-9 ? -1.0 : 1.0;
                    var x = new GeoVector2(flat, 0.0);
                    var up = new GeoVector2(0.0, 1.0);

                    if ((bend - start).Length > 1E-12)
                    {
                        lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(start, bend));
                    }

                    double landing = on ? _arrow : _width + _gap + (_box ? _gap : 0.0);
                    lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(bend, bend + x * landing));
                    return on ? bend + x * (_arrow + _gap + _width / 2.0) + up * _onOffset : bend + x * (_gap + _width / 2.0) + up * (Side * _lift);
                }

                var readable = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
                var above = new GeoVector2(-readable.Y, readable.X);
                GeoPoint2 middle = clearOfBend ? bend + way * (_gap + _width / 2.0) : bend;

                if (on)
                {
                    GeoPoint2 end = middle - way * (_gap + _width / 2.0);

                    if ((end - start).X * way.X + (end - start).Y * way.Y > 1E-12)
                    {
                        lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(start, end));
                    }

                    return middle + above * _onOffset;
                }

                lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(start, middle + way * (_gap + _width / 2.0)));
                return middle + above * (Side * _lift);
            }

            /// <summary>
            /// Words at a point of a line drawn whole from <paramref name="from"/> to <paramref name="to"/>: above it, or on it with
            /// the line broken about them; kept level, on it where they stand on a slanting line (DIMTAD 0, 1, 4), else clear of it.
            /// </summary>
            internal GeoPoint2 Centred(List<KeyValuePair<GeoPoint2, GeoPoint2>> lines, GeoPoint2 from, GeoPoint2 to, GeoPoint2 at, GeoVector2 way, bool level, ref double angle)
            {
                double lineAngle = angle;

                if (level)
                {
                    angle = 0.0;
                }

                var readable = new GeoVector2(Math.Cos(lineAngle), Math.Sin(lineAngle));
                var above = new GeoVector2(-readable.Y, readable.X);
                bool on = On(level);
                double half = level ? Chord(_width, _height, _gap, angle - lineAngle) : _width / 2.0 + _gap;
                var whole = new List<KeyValuePair<GeoPoint2, GeoPoint2>> { new KeyValuePair<GeoPoint2, GeoPoint2>(from, to) };
                lines.AddRange(on ? Broken(whole, at, way, half) : whole);

                if (on)
                {
                    return at + above * _onOffset;
                }

                double off = level ? (_width / 2.0 + _gap) * Math.Abs(Math.Sin(angle - lineAngle)) + (_height / 2.0 + _gap) * Math.Abs(Math.Cos(angle - lineAngle)) : _lift;
                return at + above * (Side * off);
            }
        }

        /// <summary>
        /// The picture of an angular dimension, as AutoCAD 2021 lays it out: the arc about the vertex from the back of one
        /// arrowhead to the back of the other (the arrowheads set along their chords), the extension lines from the objects out
        /// to the arc or in to it, and the words in the middle of the arc, next to the second ray for DIMJUST 1 or the first for
        /// 2: on the arc, which is broken about them (DIMTAD 0), on the outer side as they read (1, 3), the inner (4), or the
        /// side away from the points measured (2); kept level by DIMTIH. Where both do not fit along the chord, DIMTIX keeps
        /// them inside (DIMSOXD then leaves the arrowheads and the arc out), else DIMATFIT moves both out (0), the arrowheads
        /// (1), the words (2), or (3) the words where they fit, else the arrowheads; words outside stand past the first ray (the
        /// second for DIMJUST 1), the arc run on and a line along it to them, kept level by DIMTOH on a level landing.
        /// </summary>
        /// <remarks>
        /// <para>
        /// With ISO-25's sizes, an angle of 60 degrees with its arc 30 from the vertex has its arc from 4.78 to 55.22 degrees and
        /// its words "60" 31.9 out at 30 degrees; an angle of 8 degrees at 20 has its arrowheads outside on arcs two arrowheads
        /// long, its words outside past the first ray. The words fit where the chord takes them and two gaps (four on the arc),
        /// the arrowheads where it takes two of them, both where it takes the words and the arrowheads.
        /// </para>
        /// <para>
        /// <paramref name="placed"/> is where the words were put by hand (DIMTEDIT): for DIMTMOVE 0 a point of the arc, the words
        /// standing on it there as in the middle of the arc (above it as they read for DIMTAD 1, "60%%d" put 50 out at 30
        /// degrees centred 51.9 out); for 2 the words' own point, turned along the arc; for 1 the words' point too, level, with a
        /// leader from the middle of the arc and a landing an arrowhead long.
        /// </para>
        /// <para>
        /// An arc length dimension draws by it too (<see cref="ArcLength"/>): <paramref name="before"/> is the room its arc
        /// symbol takes before the words as they read, which the words need to fit and the arc is broken for, and
        /// <paramref name="turned"/> the angle its words were given (Angle), at which they stand on the arc, broken about them.
        /// </para>
        /// </remarks>
        internal static List<DxfEntity> Angular(DxfDimStyleTableRecord style, double dimscale, double textHeight, GeoPoint2 vertex, double radius, double from, double sweep, double[] near, double[] far, string words, GeoPoint2? placed = null, double before = 0.0, double? turned = null)
        {
            Sizes(style, dimscale, textHeight, out double scale, out double arrow, out double height, out double gap, out double offset, out double beyond);
            var picture = new List<DxfEntity>();
            double to = from + sweep;
            bool[] suppressed = { style.Dimse1, style.Dimse2 };

            // The middle of the points measured, where the extension lines start, from the vertex: words of DIMTAD 2 stand on
            // the side of the arc away from it.
            var measured = new GeoVector2(0.0, 0.0);

            for (int i = 0; i < 2; i++)
            {
                double angle = i == 0 ? from : to;
                var ray = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
                measured = measured + ray * ((radius > far[i] ? far[i] : radius < near[i] ? near[i] : radius) / 2.0);

                if (suppressed[i])
                {
                    continue;
                }

                // From DIMEXO off the object to DIMEXE past the arc, wherever that is any length, as a linear dimension's: an arc
                // 20.3 from the vertex through points 20 out has its extension lines from 20.625 to 21.55, as AutoCAD 2021 draws.
                if (radius >= far[i])
                {
                    if (radius + beyond > far[i] + offset)
                    {
                        picture.Add(Lined(new DxfLine(P3(vertex + ray * (far[i] + offset)), P3(vertex + ray * (radius + beyond))), style.Dimclre, style.Dimlwe));
                    }
                }
                else if (radius < near[i] && radius - beyond < near[i] - offset)
                {
                    picture.Add(Lined(new DxfLine(P3(vertex + ray * (near[i] - offset)), P3(vertex + ray * Math.Max(0.0, radius - beyond))), style.Dimclre, style.Dimlwe));
                }
            }

            string firstName = ArrowOf(style, DxfDimArrowFlag.FirstArrow);
            string secondName = ArrowOf(style, DxfDimArrowFlag.SecondArrow);
            bool noRoom = CadArrowheads.TakesNoRoom(firstName) && CadArrowheads.TakesNoRoom(secondName);
            double trim1 = CadArrowheads.TakesNoRoom(firstName) ? 0.0 : arrow;
            double trim2 = CadArrowheads.TakesNoRoom(secondName) ? 0.0 : arrow;
            double turn = ChordTurn(radius, arrow);
            double chord = sweep < Math.PI ? 2.0 * radius * Math.Sin(sweep / 2.0) : 2.0 * radius;
            DxfMText text = (DxfMText)Words(style, default, 0.0, height, words, scale);
            // The words laid out as high as the font measures them, past the height their text is written with.
            double width = WordsWidth(style, words, height, out height);
            var round = new RoundWords(style, scale, height, gap, width);
            bool levelInside = style.Dimtih;
            bool onInside = round.On(levelInside);
            double needText = width + before + 2.0 * gap + (onInside ? 2.0 * gap : 0.0);
            double needArrows = noRoom ? 0.0 : 2.0 * arrow;
            double needBoth = needText + needArrows;
            bool textInside;
            bool arrowsInside;
            bool arrowsLeftOut = false;

            if (placed.HasValue)
            {
                textInside = true;
                arrowsInside = needArrows <= chord;
            }
            else if (style.Dimtix)
            {
                textInside = true;
                arrowsInside = true;
                arrowsLeftOut = needBoth > chord && style.Dimsoxd;
            }
            else if (needBoth <= chord)
            {
                textInside = true;
                arrowsInside = true;
            }
            else
            {
                switch (style.Dimatfit)
                {
                    case 0:
                        textInside = false;
                        arrowsInside = false;
                        break;
                    case 1:
                        textInside = needText <= chord;
                        arrowsInside = false;
                        break;
                    case 2:
                        textInside = false;
                        arrowsInside = needArrows <= chord;
                        break;
                    default:
                        textInside = needText <= chord;
                        arrowsInside = !textInside && needArrows <= chord;
                        break;
                }
            }

            // Where the words stand, and the arc and the lines that go with them.
            var arcs = new List<KeyValuePair<double, double>>();
            var lines = new List<KeyValuePair<GeoPoint2, GeoPoint2>>();
            GeoPoint2 at;
            double textAngle;
            int tad = style.Dimtad == 0 && Math.Abs(style.Dimtvp) >= 0.7 ? 1 : style.Dimtad;
            double lift = tad != style.Dimtad ? style.Dimtvp * height : gap + height / 2.0 + (style.Dimgap < 0.0 ? gap : 0.0);
            double onOffset = tad == 0 ? style.Dimtvp * height : 0.0;
            GeoVector2 away = Heading(from + sweep / 2.0) * radius - measured;
            double middle = from + sweep / 2.0;
            double breakFrom = 0.0;
            double breakTo = 0.0;
            bool overArc = false;

            if (placed.HasValue && style.Dimtmove == 0)
            {
                // Put by hand with the arc moving with them: the arc runs through their point, and they stand on it there as
                // in the middle of the arc, above it as they read for DIMTAD 1, on it for 0 with the arc broken about them.
                double theta = Math.Atan2(placed.Value.Y - vertex.Y, placed.Value.X - vertex.X);
                double tangent = Readable(theta + Math.PI / 2.0);
                var up = new GeoVector2(-Math.Sin(tangent), Math.Cos(tangent));
                double upSide = tad == 4 ? -1.0 : tad == 2 ? Sign(Dot(up, away)) : 1.0;

                if (levelInside)
                {
                    textAngle = 0.0;
                    double off = (width / 2.0 + gap) * Math.Abs(Math.Sin(tangent)) + (height / 2.0 + gap) * Math.Abs(Math.Cos(tangent));
                    at = onInside ? placed.Value : placed.Value + up * (upSide * off);
                }
                else
                {
                    textAngle = tangent;
                    at = onInside ? placed.Value + up * onOffset : placed.Value + up * (upSide * lift);
                }

                if (onInside && arrowsInside)
                {
                    LevelBreak(vertex, radius, at, textAngle, width, height, gap, theta, out breakFrom, out breakTo);
                }
            }
            else if (placed.HasValue)
            {
                at = placed.Value;
                double toward = Math.Atan2(at.Y - vertex.Y, at.X - vertex.X);
                textAngle = Readable(toward + Math.PI / 2.0);

                if (style.Dimtmove == 1)
                {
                    // A leader from the middle of the arc to a landing an arrowhead long, the words level past it.
                    textAngle = 0.0;
                    GeoPoint2 start = vertex + Heading(middle) * radius;

                    if ((at - start).Length > gap + height / 2.0)
                    {
                        double flat = at.X >= start.X ? 1.0 : -1.0;
                        GeoPoint2 knee = at - new GeoVector2(flat, 0.0) * (width / 2.0 + gap + arrow);
                        lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(start, knee));
                        lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(knee, knee + new GeoVector2(flat, 0.0) * arrow));
                    }
                }
            }
            else if (textInside)
            {
                // In the middle of the arc, or next to a ray for DIMJUST 1 and 2 where the words fit there.
                double along = arrow + gap + width / 2.0;
                double side = 2.0 * along <= chord ? ChordTurn(radius, along) : 0.0;
                double theta = side > 0.0 && style.Dimjust == 1 ? to - side : side > 0.0 && style.Dimjust == 2 ? from + side : middle;
                double tangent = Readable(theta + Math.PI / 2.0);
                GeoPoint2 point = vertex + Heading(theta) * radius;
                var up = new GeoVector2(-Math.Sin(tangent), Math.Cos(tangent));
                double upSide = tad == 4 ? -1.0 : tad == 2 ? Sign(Dot(up, away)) : 1.0;

                if (turned.HasValue)
                {
                    textAngle = turned.Value;
                    at = point;
                }
                else if (levelInside)
                {
                    textAngle = 0.0;
                    double off = (width / 2.0 + gap) * Math.Abs(Math.Sin(tangent)) + (height / 2.0 + gap) * Math.Abs(Math.Cos(tangent));
                    at = onInside ? point : point + up * (upSide * off);
                }
                else
                {
                    textAngle = tangent;
                    at = onInside ? point + up * onOffset : point + up * (upSide * lift);
                }

                // The arc broken about the words on it, and about the symbol before them.
                if ((onInside || turned.HasValue) && arrowsInside)
                {
                    GeoPoint2 middleOfAll = at - Heading(textAngle) * (before / 2.0);
                    LevelBreak(vertex, radius, middleOfAll, textAngle, width + before, height, gap, theta, out breakFrom, out breakTo);
                }
            }
            else if (style.Dimtmove != 0 && !style.Dimtoh)
            {
                // Over the arc where DIMTMOVE is 1 or 2, out along the middle of the sweep and turned along the arc there, as
                // AutoCAD 2021 puts words the fit moved out. Under 1 a leader runs from the arc to two arrowheads past it, or past
                // the farthest point measured, and on by the words' height for words reading toward the vertex not on the line
                // (DIMTAD 0); the words stand a gap and half their height beyond. Under 2 the words' middle stands two arrowheads
                // past the arc, or, where the points lie beyond it, a gap and half their height beyond where their box first
                // fits between the extension lines, no further than the farthest point: "8%%d" over an angle of 8 degrees at 20,
                // its points 40 out, at 32.04.
                GeoVector2 outward = Heading(middle);
                overArc = true;
                textAngle = Readable(middle + Math.PI / 2.0);
                bool towardVertex = Dot(new GeoVector2(-Math.Sin(textAngle), Math.Cos(textAngle)), outward) < 0.0;
                double farthest = 0.0;

                for (int i = 0; i < 2; i++)
                {
                    farthest = Math.Max(farthest, double.IsInfinity(far[i]) ? near[i] : far[i]);
                }

                if (style.Dimtmove == 1)
                {
                    double knee = Math.Max(radius, farthest) + 2.0 * arrow + (towardVertex && tad != 0 ? height : 0.0);
                    lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(vertex + outward * radius, vertex + outward * knee));
                    at = vertex + outward * (knee + gap + height / 2.0);
                }
                else if (farthest > radius)
                {
                    double fits = sweep < Math.PI ? (width / 2.0 + gap) / Math.Tan(sweep / 2.0) : 0.0;
                    at = vertex + outward * (Math.Min(fits, farthest) + gap + height / 2.0);
                }
                else
                {
                    at = vertex + outward * (radius + 2.0 * arrow);
                }
            }
            else
            {
                // Outside past the first ray (the second for DIMJUST 1): the arc run on, then a line along it to the words.
                bool second = style.Dimjust == 1;
                double clear = !arrowsInside ? 2.0 * arrow : noRoom ? 0.0 : arrow;
                double run = clear > 0.0 ? ChordTurn(radius, clear) : 0.0;
                double end = second ? to + run : from - run;

                if (run > 0.0)
                {
                    arcs.Add(second ? new KeyValuePair<double, double>(to, end) : new KeyValuePair<double, double>(end, from));
                }

                GeoPoint2 point = vertex + Heading(end) * radius;
                GeoVector2 way = second ? new GeoVector2(-Math.Sin(end), Math.Cos(end)) : new GeoVector2(Math.Sin(end), -Math.Cos(end));

                // Turned along the arc there, read the way words in the middle of the arc would read.
                double across = middle - Math.PI / 2.0;
                double tangent = end - Math.PI / 2.0 + (Math.Cos(Readable(across) - across) < 0.0 ? Math.PI : 0.0);
                var up = new GeoVector2(-Math.Sin(tangent), Math.Cos(tangent));
                bool on = round.On(false);

                if (style.Dimtoh)
                {
                    // Level words run out on the side the arc faces there.
                    textAngle = 0.0;
                    double flat = Heading(end).X < -1E-9 ? -1.0 : 1.0;
                    var x = new GeoVector2(flat, 0.0);
                    double upSide = tad == 4 ? -1.0 : tad == 2 ? Sign(away.Y) : 1.0;
                    lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(point, point + x * (on ? arrow : arrow + width + 2.0 * gap)));
                    at = point + x * (arrow + gap + width / 2.0) + new GeoVector2(0.0, 1.0) * (on ? onOffset : upSide * lift);
                }
                else
                {
                    textAngle = tangent;
                    double upSide = tad == 4 ? -1.0 : tad == 2 ? Sign(Dot(up, away)) : 1.0;

                    if (!on)
                    {
                        lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(point, point + way * (width + 2.0 * gap)));
                    }

                    at = point + way * (gap + width / 2.0) + up * (on ? onOffset : upSide * lift);
                }
            }

            if (arrowsLeftOut)
            {
                // DIMSOXD leaves out the arrowheads that do not fit, and the arc with them.
            }
            else if (arrowsInside)
            {
                double a = from + (trim1 > 0.0 ? ChordTurn(radius, trim1) : 0.0);
                double b = to - (trim2 > 0.0 ? ChordTurn(radius, trim2) : 0.0);

                if (breakTo > breakFrom && breakFrom > a && breakTo < b)
                {
                    arcs.Add(new KeyValuePair<double, double>(a, breakFrom));
                    arcs.Add(new KeyValuePair<double, double>(breakTo, b));
                }
                else
                {
                    arcs.Add(new KeyValuePair<double, double>(a, b));
                }
            }
            else
            {
                // The arrowheads outside on arcs of their own, back to back with them, and the arc between by DIMTOFL, or always
                // under words over the arc, as AutoCAD 2021 draws it.
                if (!CadArrowheads.TakesNoRoom(firstName))
                {
                    arcs.Add(new KeyValuePair<double, double>(from - 2.0 * turn, from - turn));
                }

                if (!CadArrowheads.TakesNoRoom(secondName))
                {
                    arcs.Add(new KeyValuePair<double, double>(to + turn, to + 2.0 * turn));
                }

                if (style.Dimtofl || overArc)
                {
                    arcs.Add(new KeyValuePair<double, double>(from, to));
                }
            }

            foreach (KeyValuePair<double, double> arc in arcs)
            {
                picture.Add(Lined(new DxfArc(P3(vertex), radius, Turned(arc.Key), Turned(arc.Value)), style.Dimclrd, style.Dimlwd));
            }

            foreach (KeyValuePair<GeoPoint2, GeoPoint2> line in lines)
            {
                picture.Add(Lined(new DxfLine(P3(line.Key), P3(line.Value)), style.Dimclrd, style.Dimlwd));
            }

            GeoPoint2 start2 = vertex + Heading(from) * radius;
            GeoPoint2 end2 = vertex + Heading(to) * radius;

            if (!arrowsLeftOut)
            {
                // Each arrowhead along the chord from its back on the arc to its tip on the ray; one that takes no room, as a tick,
                // along the arc at its tip, as AutoCAD 2021 turns it.
                double sign = arrowsInside ? 1.0 : -1.0;
                GeoVector2 firstWay = CadArrowheads.TakesNoRoom(firstName) ? new GeoVector2(Math.Sin(from), -Math.Cos(from)) * sign : UnitOf(start2 - (vertex + Heading(from + sign * turn) * radius));
                GeoVector2 secondWay = CadArrowheads.TakesNoRoom(secondName) ? new GeoVector2(-Math.Sin(to), Math.Cos(to)) * sign : UnitOf(end2 - (vertex + Heading(to - sign * turn) * radius));
                AddArrow(picture, style, DxfDimArrowFlag.FirstArrow, start2, firstWay, arrow);
                AddArrow(picture, style, DxfDimArrowFlag.SecondArrow, end2, secondWay, arrow);
            }

            text.Location = P3(at);
            text.Rotation = textAngle < 0.0 ? textAngle + 2.0 * Math.PI : textAngle;
            picture.Add(text);
            AddFrame(picture, style, at, textAngle, width, height, gap);
            picture.Add(Definition(vertex));
            picture.Add(Definition(start2));
            picture.Add(Definition(end2));
            return picture;
        }

        /// <summary>The angle about the centre of a chord of a length on a circle of a radius: half a turn for a chord past its width.</summary>
        private static double ChordTurn(double radius, double length) => 2.0 * Math.Asin(Math.Min(1.0, length / (2.0 * radius)));

        private static GeoVector2 Heading(double angle) => new GeoVector2(Math.Cos(angle), Math.Sin(angle));

        private static GeoVector2 UnitOf(GeoVector2 v) => v.Length > 1E-12 ? v / v.Length : new GeoVector2(1.0, 0.0);

        private static double Sign(double value) => value < 0.0 ? -1.0 : 1.0;

        /// <summary>
        /// The angles about the vertex between which an arc through words centred on it is taken out: where the arc leaves the
        /// box of the words grown by the gap, either side of the angle of the words.
        /// </summary>
        private static void LevelBreak(GeoPoint2 vertex, double radius, GeoPoint2 at, double angle, double width, double height, double gap, double theta, out double low, out double high)
        {
            var x = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
            var y = new GeoVector2(-x.Y, x.X);
            double a = width / 2.0 + gap;
            double b = height / 2.0 + gap;
            low = theta;
            high = theta;
            double lowest = 0.0;
            double highest = 0.0;

            // Each side of the box, as a + s t for t from 0 to 1, meets the circle where |a + s t - vertex| = radius.
            GeoPoint2[] corners = { at - x * a - y * b, at + x * a - y * b, at + x * a + y * b, at - x * a + y * b };

            for (int i = 0; i < 4; i++)
            {
                GeoPoint2 p = corners[i];
                GeoVector2 s = corners[(i + 1) % 4] - p;
                GeoVector2 d = p - vertex;
                double qa = Dot(s, s);
                double qb = 2.0 * Dot(d, s);
                double qc = Dot(d, d) - radius * radius;
                double disc = qb * qb - 4.0 * qa * qc;

                if (qa <= 1E-24 || disc < 0.0)
                {
                    continue;
                }

                foreach (double t in new[] { (-qb - Math.Sqrt(disc)) / (2.0 * qa), (-qb + Math.Sqrt(disc)) / (2.0 * qa) })
                {
                    if (t < -1E-12 || t > 1.0 + 1E-12)
                    {
                        continue;
                    }

                    GeoPoint2 q = p + s * t;
                    double off = Math.IEEERemainder(Math.Atan2(q.Y - vertex.Y, q.X - vertex.X) - theta, 2.0 * Math.PI);
                    lowest = Math.Min(lowest, off);
                    highest = Math.Max(highest, off);
                }
            }

            low = theta + lowest;
            high = theta + highest;
        }
    }
}
