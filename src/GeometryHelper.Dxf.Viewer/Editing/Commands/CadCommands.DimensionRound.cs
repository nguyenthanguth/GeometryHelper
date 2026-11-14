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
        /// outside too unless DIMATFIT 2 keeps them in. DIMTIX under DIMATFIT 3 puts the words outside next to the rim wherever the
        /// point was, as the fit does (a diameter's far arrowhead outside with them, on its line, where DIMTOFL is on, else the
        /// centre marked); under DIMATFIT 0 to 2 it keeps the words inside however tight the radius, the arrowhead going out
        /// with its short line where it does not fit with them.
        /// </summary>
        /// <remarks>
        /// A radius of 20 drawn with ISO-25's sizes and its line put 45 from the centre has its arrowhead from 20 to 22.5 out,
        /// its line from 22.5 on under the words and a line from the centre to the rim; put 8 from the centre, its arrowhead
        /// from 20 in to 17.5 and its line from 17.5 to past the words. The words stand on the line (DIMTAD 0, the line stopped
        /// short of them), above it as they read (1, 2, 3) or below (4); kept level (DIMTOH outside, DIMTIH inside) they stand on
        /// a level landing from the end of the line. <paramref name="placed"/> says the words were put by hand (DIMTEDIT): they
        /// stay where they are put along the line (under DIMATFIT 0 to 2 and DIMTMOVE 0 at the point itself inside the circle, the
        /// line whole to the arrowhead, and outside where the fit puts them past the rim, the arrowhead outside but for DIMATFIT 2:
        /// "R20" of a circle of 20 put 25 out stands 28.6 out; under DIMATFIT 3 no nearer the rim than the fit puts them). <paramref name="free"/> says they were put off it under DIMTMOVE 2: they stand
        /// at <paramref name="location"/> itself, turned along the line, and the line is drawn inside as though they were not
        /// there, from the centre (the back of the far arrowhead) to the back of the arrowhead inside the rim, broken where it
        /// runs through them. Words put past the rim stand no nearer it than two arrowheads and the gap and half their width (two
        /// arrowheads where they are level), so acad.dwt's "R20.0000" put 20.5 out stands 20.979 out; on a line exactly level the level words
        /// stand along it with no landing, and on an exactly upright one the landing runs left where the line runs down.
        /// Level words (DIMTAD 1 and 4) off a line that runs down keep the height of the words more room past two arrowheads, outside
        /// the rim and in; level words on a line within 15 degrees of level stand past the point with no landing, inside as outside.
        /// Whether the words fit a radius is the room their box takes along its line, grown by the gap (its width along a level
        /// line, the length the box reaches along a slanting one that DIMTIH keeps level): words on the line need the chord of
        /// the line through the box and an arrowhead and two gaps to stay in with the arrowhead out, and three arrowheads past the
        /// chord to keep the arrowhead in as well; "R15" of Arial 2.5 high keeps its words in from a radius of 10.5 and its
        /// arrowhead in from 14.74. A diameter's words on its line need the chord and two arrowheads and two gaps along it.
        /// </remarks>
        internal static List<DxfEntity> Radial(
            DxfDimStyleTableRecord style,
            double dimscale,
            double textHeight,
            GeoPoint2 center,
            double radius,
            GeoPoint2 chord,
            GeoPoint2 far,
            GeoPoint2 location,
            bool diameter,
            string words,
            bool placed = false,
            bool free = false
        ) =>
            RadialLaidOut(
                style,
                dimscale,
                textHeight,
                center,
                radius,
                chord,
                far,
                location,
                diameter,
                words,
                placed,
                free,
                out _
            );

        /// <summary>
        /// <see cref="Radial"/>, and whether its words stand where the line was put, which AutoCAD 2021 records by flag 128 of
        /// group 70 (164 and 163 against 36 and 35). They do not where the fit (DIMATFIT 0 to 2 under DIMTMOVE 0) or DIMTIX put
        /// them, nor where the room next to the rim pushed them out: "R20" put 20.3 out of a circle of 20 stands 20.979 out, and
        /// is a 36. Words put inside are recorded as put even where they are held back from the rim.
        /// </summary>
        internal static List<DxfEntity> RadialLaidOut(
            DxfDimStyleTableRecord style,
            double dimscale,
            double textHeight,
            GeoPoint2 center,
            double radius,
            GeoPoint2 chord,
            GeoPoint2 far,
            GeoPoint2 location,
            bool diameter,
            string words,
            bool placed,
            bool free,
            out bool atPoint
        )
        {
            atPoint = false;
            Sizes(
                style,
                dimscale,
                textHeight,
                out double scale,
                out double arrow,
                out double height,
                out double gap,
                out _,
                out _
            );
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
                atPoint = !style.Dimtix;
                return FreeRadial(
                    style,
                    picture,
                    text,
                    center,
                    chord,
                    far,
                    location,
                    u,
                    aligned,
                    diameter,
                    width,
                    height,
                    gap,
                    arrow,
                    trim,
                    farTrim,
                    scale
                );
            }

            var words2 = new RoundWords(style, scale, height, gap, width);
            var lines = new List<KeyValuePair<GeoPoint2, GeoPoint2>>();
            bool arrowOut;
            bool farArrow = false;
            bool farOut = false;
            bool nearArrow = true;
            bool single = false;
            bool tixOut = false;
            bool pushedOut = false;
            bool textOut;
            double at;

            // Under DIMATFIT 0 to 2 a radius's words go where the fit puts them however far out the line was put, as AutoCAD 2021
            // draws them: "R20" put 35 out stands in the middle of the radius.
            bool fitted =
                !placed && style.Dimatfit != 3 && (style.Dimtmove == 0 || (style.Dimtix && style.Dimtmove == 2));

            bool pastRim = along >= radius - 1E-10;
            bool flatLine = Math.Abs(u.Y) <= 1E-9;

            // Level words off a line that runs down, DIMTAD 1 or 4: AutoCAD 2021 keeps room for one more line of words, their height,
            // past two arrowheads outside the rim and inside it, where DIMTAD 0 and a line running up keep the two: "R20" of Arial
            // 0.18 high put 20.3 out straight down, level, DIMTAD 1, has its landing 20.5438 out.
            bool lower = (style.Dimtad == 1 || style.Dimtad == 4) && u.Y < -1E-9;
            double beyond = style.Dimtoh && !flatLine ? 0.0 : words2.Beside;

            // The room the words take along the line: their box grown by the gap, as long as the box reaches along the line where
            // they are kept level across it (DIMTIH), and as the line runs through the box's middle.
            bool across = style.Dimtih && !flatLine;
            double boxWidth = width + 2.0 * gap;
            double reachAlong = across ? boxWidth * Math.Abs(u.X) + (height + 2.0 * gap) * Math.Abs(u.Y) : boxWidth;
            double chordAlong = across ? 2.0 * Chord(width, height, gap, Math.Atan2(u.Y, u.X)) : boxWidth;

            // Words put by hand under DIMATFIT 0 to 2 (DIMTEDIT) stand at the point put inside the circle, the line whole from the
            // centre to the arrowhead, and past the rim where the fit puts them, the arrowhead outside but for DIMATFIT 2 keeping
            // it in: "R20" of a circle of 20 put 15 out stands 15 out, put 25 out 28.75 out.
            bool fitPlaced = placed && style.Dimatfit != 3 && style.Dimtmove == 0;

            if (fitPlaced)
            {
                single = false;
                textOut = pastRim;
                double needArrows =
                    CadArrowheads.TakesNoRoom(nearName) && CadArrowheads.TakesNoRoom(farName)
                        ? 0.0
                        : 2.0 * arrow + 2.0 * gap;
                arrowOut =
                    pastRim && !(style.Dimatfit == 2 && (diameter ? 2.0 * radius >= needArrows : radius >= trim + gap));
                at = !pastRim
                    ? along
                    : Math.Max(
                        along,
                        radius
                            + (arrowOut ? 2.0 * arrow : trim)
                            + (style.Dimtoh && !flatLine ? (lower ? height : 0.0) : words2.Beside)
                    );
                farArrow = diameter;
                farOut = arrowOut;
                atPoint = true;
            }
            else if (pastRim && !fitted && !(style.Dimtix && !placed))
            {
                arrowOut = true;
                textOut = true;
                // Words put within the arrowhead's room of the rim stand where the fit would put them past it: AutoCAD 2021
                // keeps "R25" put 25.25 out where it stands put 33.9 out, its arrowhead and two gaps clear of the rim.
                at =
                    placed && style.Dimtmove != 0
                        ? along
                        : Math.Max(
                            along,
                            radius
                                + (
                                    style.Dimtoh && !flatLine
                                        ? 2.0 * arrow + (lower ? height : 0.0)
                                        : 2.0 * arrow + words2.Beside
                                )
                        );
                farArrow = diameter && style.Dimtofl;
                farOut = true;
                atPoint = (placed || !style.Dimtix) && at <= along + 1E-9;
                pushedOut =
                    !placed
                    && !atPoint
                    && WordsStayWithTheLine(
                        diameter ? 2.0 * radius : radius,
                        reachAlong,
                        chordAlong,
                        diameter
                            ? (
                                CadArrowheads.TakesNoRoom(nearName) && CadArrowheads.TakesNoRoom(farName)
                                    ? 0.0
                                    : 2.0 * arrow + 2.0 * gap
                            )
                            : trim + 2.0 * gap
                    );
            }
            else if (!placed && style.Dimtix && !fitted)
            {
                arrowOut = true;
                textOut = true;
                tixOut = true;
                at =
                    radius
                    + (style.Dimtih && !flatLine ? 2.0 * arrow + (lower ? height : 0.0) : 2.0 * arrow + words2.Beside);
                farArrow = diameter && style.Dimtofl;
                farOut = true;
            }
            else if (placed || style.Dimatfit == 3 || style.Dimtmove == 1)
            {
                arrowOut = false;
                textOut = false;
                single = true;
                at = Math.Min(
                    along,
                    radius
                        - (
                            style.Dimtih && !flatLine
                                ? 2.0 * arrow + (lower ? height : 0.0)
                                : 2.0 * arrow + gap + width / 2.0
                        )
                );

                // AutoCAD 2021 draws no arrowhead on a radius whose words stand inside next to the first extension line.
                nearArrow = diameter || style.Dimjust != 1;
                atPoint = !style.Dimtix || style.Dimtmove == 0;
            }
            else if (!diameter)
            {
                // The words fit where the radius takes them and a gap each side along the line, the arrowhead not counted, as
                // AutoCAD 2021 fits "R6" 4.03 wide in a radius of 6 and not "R5" in one of 5 (words kept level across a slanting line
                // take the box's reach along it); on the line (DIMTAD 0) they need the arrowhead and two gaps more to stay in
                // with the arrowhead going out, and three arrowheads past their chord to keep it in.
                bool on = words2.On(style.Dimtih && !flatLine);
                double within = on ? chordAlong + trim + 2.0 * gap : reachAlong;
                double inside = on ? Math.Max(within, chordAlong + 3.0 * trim) : reachAlong;
                double fits = style.Dimatfit == 1 ? reachAlong : within;
                // DIMTIX keeps the words inside however tight the radius, the arrowhead going out where they leave it no room.
                textOut = !(radius > fits + 1E-9) && !style.Dimtix;
                arrowOut = textOut
                    ? !(style.Dimatfit == 2 && radius > trim + 2.0 * gap + 1E-9)
                    : !(radius > inside + 1E-9);
                at = !textOut
                    ? (radius - (arrowOut && !(radius > within + 1E-9) ? 0.0 : trim)) / 2.0
                    : radius + (arrowOut ? 2.0 * arrow : trim) + beyond;
            }
            else
            {
                // The words take their width and a gap each side along the line; on it (DIMTAD 0) they take the arrowheads' room
                // and two gaps more as well, through their middle, as AutoCAD 2021 draws them: "%%c6" 4.357 wide fits a diameter of 6
                // under DIMATFIT 1, but with its arrowheads under DIMATFIT 0 not even one of 9.
                double needArrows =
                    CadArrowheads.TakesNoRoom(nearName) && CadArrowheads.TakesNoRoom(farName)
                        ? 0.0
                        : 2.0 * arrow + 2.0 * gap;
                bool both =
                    2.0 * radius
                    > (
                        words2.On(style.Dimtih && !flatLine)
                            ? chordAlong + 2.0 * arrow + (needArrows > 0.0 ? 2.0 * gap : 0.0)
                            : Math.Max(reachAlong, needArrows)
                    ) + 1E-9;
                textOut = !both && !(style.Dimatfit == 1 && 2.0 * radius > reachAlong + 1E-9) && !style.Dimtix;
                arrowOut = !both && !(style.Dimatfit == 2 && 2.0 * radius >= needArrows && !style.Dimtix);
                at = !textOut ? 0.0 : radius + (arrowOut ? 2.0 * arrow : trim) + beyond;
                farArrow = true;
                farOut = arrowOut;
            }

            // Words kept level on a line exactly level are the words that stand along it: no landing, as AutoCAD 2021 draws them.
            bool level = (textOut && !tixOut ? style.Dimtoh : style.Dimtih) && !flatLine;
            GeoPoint2 knee = center + u * at;
            GeoPoint2 back = arrowOut ? (textOut || diameter ? chord + u * trim : chord) : chord - u * trim;
            GeoPoint2 wordsAt;
            double angle = aligned;

            if (textOut && !pastRim && !tixOut)
            {
                // Moved out past the rim: the line from the rim (or the back of an arrowhead outside) to the words.
                double clear = arrowOut ? 2.0 * arrow : trim;
                GeoPoint2 bend = chord + u * clear;
                GeoPoint2 start = arrowOut ? back : chord;
                wordsAt = words2.Along(lines, start, bend, u, level, true, ref angle, clearOfBend: true);
            }
            else if (textOut)
            {
                // The line of words outside with the arrowhead in starts at the rim, past the arrowhead.
                wordsAt = words2.Along(
                    lines,
                    arrowOut ? back : chord,
                    knee,
                    u,
                    level,
                    !tixOut,
                    ref angle,
                    clearOfBend: false,
                    pushed: pushedOut
                );
            }
            else if (single)
            {
                // Inside where they were put: the line from the back of the arrowhead in to the words.
                wordsAt = words2.Along(
                    lines,
                    back,
                    knee,
                    -u,
                    level,
                    false,
                    ref angle,
                    clearOfBend: false,
                    lineLength: style.Dimtmove == 1 ? double.PositiveInfinity : (diameter ? 2.0 * radius : radius),
                    leftTheLine: style.Dimtmove == 1
                        && along < -1E-9
                        && (!diameter || (location - center).Length > radius + 1E-9)
                );
            }
            else if (diameter && !arrowOut)
            {
                // Inside between two arrowheads: the line from the back of one to the back of the other, broken about words on it.
                GeoPoint2 farBack = far + u * farTrim;
                wordsAt = words2.Centred(lines, farBack, back, knee, u, level, ref angle);
            }
            else if (diameter)
            {
                // Inside with the arrowheads outside: the words at the centre, DIMTOFL's line through from rim to rim, broken
                // about words on it (DIMTAD 0) as AutoCAD 2021 breaks it, a gap clear of them.
                var through = new List<KeyValuePair<GeoPoint2, GeoPoint2>>();
                wordsAt = words2.Centred(through, far, chord, center, u, level, ref angle);

                // DIMTIX words that take all of the line leave it whole, as they do a radius's.
                if (style.Dimtix && through.Count == 0)
                {
                    through.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(far, chord));
                }

                if (style.Dimtofl)
                {
                    lines.AddRange(through);
                }
            }
            else
            {
                // In the middle of the radius line, which runs whole from the centre to the arrowhead.
                int before = lines.Count;
                wordsAt = words2.Centred(lines, center, back, knee, u, level, ref angle);

                // DIMTIX words on the line that take all of it leave it whole; the arrowhead outside, DIMTOFL off, no line at all.
                if (style.Dimtix && lines.Count == before)
                {
                    lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(center, back));
                }

                if (arrowOut && !style.Dimtofl)
                {
                    lines.RemoveRange(before, lines.Count - before);
                }
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
            if (arrowOut && !textOut && !CadArrowheads.TakesNoRoom(nearName))
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

            // AutoCAD 2021 records words standing off their line by the point of the line they stand by: "R20" above a
            // radius's line put 40 out at 30 degrees under DIMTAD 1 writes (34.641, 20) as group 11, its middle 0.18 above.
            if ((words2.Standing - wordsAt).Length > 1E-12)
            {
                RecordWordsBy(text, words2.Standing);
            }

            // The centre mark where the line stands outside and DIMTOFL draws none through the circle.
            if ((textOut || (arrowOut && !diameter)) && !style.Dimtofl)
            {
                AddCenterMark(picture, style, center, radius, scale);
            }

            picture.Add(Definition(diameter ? far : center));
            picture.Add(Definition(chord));
            return picture;
        }

        /// <summary>
        /// Whether the words of a radius or a diameter put out past the rim stand with the line, on its landing (they fit the
        /// line and the arrowheads do not take their place), rather than above a landing of their own. AutoCAD 2021 fits the
        /// words and the arrowheads to the line (<paramref name="line"/>, the radius or the diameter) the way it does for
        /// DIMATFIT 3: the words fit where the line is longer than the room they reach along it
        /// (<paramref name="reach"/>), the arrowheads where it is longer than <paramref name="arrows"/>, both where it is
        /// longer than the arrowheads and the chord of the line through the words' box (<paramref name="chord"/>). Where both
        /// fit, or the arrowheads fit and are the larger of the two (a tie goes to them), the words stand above their own
        /// landing: with Arial 2.5 high and DIMASZ 2.5, "R8" at 60 degrees stands on the landing and "R10" at 45 above one,
        /// "%%c6" straight up on it and "%%c7" above one.
        /// </summary>
        private static bool WordsStayWithTheLine(double line, double reach, double chord, double arrows)
        {
            if (!(line > reach + 1E-9) || line > chord + arrows + 1E-9)
            {
                return false;
            }

            return !(line > arrows + 1E-9 && arrows >= reach - 1E-9);
        }

        /// <summary>
        /// The picture of a radius or a diameter whose words were put off its line under DIMTMOVE 2, as AutoCAD 2021 draws it:
        /// the words at their point, turned along the line, or level under DIMTIH; the line inside from the centre, or the back
        /// of the far arrowhead, to the back of the arrowhead inside the rim, taken out where it runs through them for DIMGAP and
        /// half their width either side, or as much of it as runs through their box grown by DIMGAP where they are level. A
        /// diameter of 40 at 30 degrees with "%%c40" put 8 out along its line has its line from 17.5 back to 4.229 and from
        /// 11.771 on to 17.5.
        /// </summary>
        private static List<DxfEntity> FreeRadial(
            DxfDimStyleTableRecord style,
            List<DxfEntity> picture,
            DxfMText text,
            GeoPoint2 center,
            GeoPoint2 chord,
            GeoPoint2 far,
            GeoPoint2 location,
            GeoVector2 u,
            double angle,
            bool diameter,
            double width,
            double height,
            double gap,
            double arrow,
            double trim,
            double farTrim,
            double scale
        )
        {
            GeoPoint2 from = diameter ? far + u * farTrim : center;
            var lines = new List<KeyValuePair<GeoPoint2, GeoPoint2>>
            {
                new KeyValuePair<GeoPoint2, GeoPoint2>(from, chord - u * trim),
            };
            double across = Cross(u, location - center);
            bool onLine = Math.Abs(across) < height / 2.0 + gap;
            double along = (location - center).X * u.X + (location - center).Y * u.Y;
            double reach = (chord - center).Length;
            // The stretch of the line their box grown by the gap takes: words turned along it take their width and a gap either
            // side of their middle.
            double enters = along - width / 2.0 - gap;
            double leaves = along + width / 2.0 + gap;

            // DIMTIH keeps free words level, inside the circle or out, as AutoCAD 2021 draws them whatever DIMTOH says.
            if (style.Dimtih)
            {
                angle = 0.0;

                // Level words stand on a slanting line wherever it runs through their box grown by the gap, which is further off
                // than the box's half height: a diameter of 40 at 30 degrees with "%%c40" put 6 out at 240 degrees, 3 off the line,
                // has its line broken about them (the box reaches 3.5 across it), and AutoCAD 2021 breaks it by the length the words
                // take (below) about the middle of what the line takes of their box, not about the foot of their middle.
                if (LineThroughBox(u, location - center, width / 2.0 + gap, height / 2.0 + gap, out enters, out leaves))
                {
                    onLine = true;
                    along = (enters + leaves) / 2.0;
                }
            }

            double lineAngle = Math.Atan2(u.Y, u.X);
            double half = style.Dimtih ? Chord(width, height, gap, angle - lineAngle) : width / 2.0 + gap;
            // Over a rim from outside, or inside reaching the rim (a diameter's the back of its arrowhead); over a radius's
            // centre by half their width, the gap and an arrowhead, level or not (AutoCAD 2021 pushes "R25" put 6 out under
            // DIMASZ 2.5, not 7), but not once they stand behind it: where their box takes the line from a quarter of a unit
            // behind the centre to an arrowhead and a half past it ("R8" and "R20" put every 0.5 along and across the line,
            // level and turned, 6 differing of 340; before, words 6 behind the centre pushed the arrowhead out too). Behind the
            // centre means behind it by any length: a radius of 20 with "R20" level put 4 behind it and on its line, whose box
            // takes the line to 0.25 short of the centre, keeps the arrowhead in, and one whose box reaches 0.011 past it pushes
            // it out (0.066 short keeps it; td3: 6 differing of 760 down to 0). Words whose point is within two arrowheads of the
            // centre push it out as well, where the box takes the line further out than an arrowhead and a half: "R20" put 4
            // along and 3 across (5.0 from the centre) pushes the arrowhead out, put 5 along (5.83) or 2 across and 5 along (5.39)
            // does not.
            bool overRim = along - half <= reach && along + half >= (diameter ? reach - trim : reach);
            bool overFarRim = diameter && along + half >= -reach && along - half <= -(reach - farTrim);
            bool pushed =
                onLine
                && (
                    overRim
                    || overFarRim
                    || (
                        !diameter
                        && leaves >= -1E-9
                        && (enters <= 1.5 * trim || (location - center).Length <= 2.0 * trim + 1E-9)
                    )
                );

            if (pushed)
            {
                // Words on the line over a rim, or over a radius's centre, put the arrowheads outside, pointing in, each on a
                // line an arrowhead long; DIMTOFL runs the line whole from the centre, or the far rim, to the rim, else the
                // centre is marked. AutoCAD 2021 draws "R20" so put 1 out under ISO-25 and 19.4 to 20.6 out under acad.dwt's
                // Standard; put 8 out the arrowhead stays inside and the line is broken about the words.
                lines = new List<KeyValuePair<GeoPoint2, GeoPoint2>>();

                if (style.Dimtofl)
                {
                    lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(diameter ? far : center, chord));
                }

                if (trim > 0.0)
                {
                    lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(chord + u * trim, chord + u * (2.0 * trim)));
                }

                if (diameter && farTrim > 0.0)
                {
                    lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(far - u * farTrim, far - u * (2.0 * farTrim)));
                }
            }
            else if (onLine && (diameter || leaves > 1E-9))
            {
                // A radius's line starts at the centre: words whose box takes the line only behind it leave it whole.
                GeoPoint2 foot = center + u * along;
                lines = Broken(lines, foot, u, half);

                // Words reaching past the back of a radius's arrowhead short of the rim: AutoCAD 2021 still draws the line
                // from the back of the arrowhead out to their end, over the arrowhead.
                if (along + half > reach - trim && along - half < reach - trim)
                {
                    lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(chord - u * trim, foot + u * half));
                }
            }

            foreach (KeyValuePair<GeoPoint2, GeoPoint2> line in lines)
            {
                picture.Add(Lined(new DxfLine(P3(line.Key), P3(line.Value)), style.Dimclrd, style.Dimlwd));
            }

            AddArrow(picture, style, DxfDimArrowFlag.FirstArrow, chord, pushed ? -u : u, arrow);

            if (diameter)
            {
                AddArrow(picture, style, DxfDimArrowFlag.SecondArrow, far, pushed ? u : -u, arrow);
            }

            text.Location = P3(location);
            text.Rotation = angle < 0.0 ? angle + 2.0 * Math.PI : angle;
            picture.Add(text);
            AddFrame(picture, style, location, angle, width, height, gap);

            if (pushed && !style.Dimtofl)
            {
                AddCenterMark(picture, style, center, reach, scale);
            }

            picture.Add(Definition(diameter ? far : center));
            picture.Add(Definition(chord));
            return picture;
        }

        /// <summary>
        /// The centre mark of DIMCEN about a centre, along the axes: a cross DIMCEN long each way, and where DIMCEN is below
        /// nought centre lines from twice its length out to its length past the rim, where the radius is over twice it, as AutoCAD
        /// 2021 draws them.
        /// </summary>
        private static void AddCenterMark(
            List<DxfEntity> picture,
            DxfDimStyleTableRecord style,
            GeoPoint2 center,
            double radius,
            double scale
        )
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

            if (style.Dimcen < 0.0 && CentreLinesFit(radius, mark))
            {
                foreach (GeoVector2 way in new[] { -x, x, -y, y })
                {
                    picture.Add(
                        Lined(
                            new DxfLine(P3(center + way * (2.0 * mark)), P3(center + way * (radius + mark))),
                            style.Dimclrd,
                            style.Dimlwd
                        )
                    );
                }
            }
        }

        /// <summary>
        /// The box of a DIMGAP below nought round the words: four lines DIMGAP clear of them, from the lower left as they read,
        /// as AutoCAD 2021 draws it; none where DIMGAP is nought or more.
        /// </summary>
        private static void AddFrame(
            List<DxfEntity> picture,
            DxfDimStyleTableRecord style,
            GeoPoint2 at,
            double angle,
            double width,
            double height,
            double gap
        )
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
            private readonly bool _bendRecorded;

            internal RoundWords(DxfDimStyleTableRecord style, double scale, double height, double gap, double width)
            {
                _arrow = style.Dimasz * scale;
                _bendRecorded = style.Dimtmove == 1;
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

            /// <summary>The point of their line or landing the words last laid out stand by: their middle without their lift off it.</summary>
            internal GeoPoint2 Standing { get; private set; }

            /// <summary>Whether the words stand on the line: DIMTAD 0, and for words kept level inside, DIMTAD 1 and 4 as well.</summary>
            internal bool On(bool levelInside) => _tad == 0 || (levelInside && (_tad == 1 || _tad == 4));

            private double Side => _tad == 4 ? -1.0 : 1.0;

            /// <summary>
            /// Whether level words inside a radius or a diameter (DIMTAD 1 and 4) keep to its line, standing level with a landing an
            /// arrowhead long, or leave it for a wider landing, above or below. AutoCAD 2021 keeps them where the line, whole, has
            /// two arrowheads of room (and the words' height more on a line that runs down) and, on a slanting line, the width of
            /// the words and a gap as well, as far as it leans toward level: a radius of 8 with "R8" at 30 degrees does not keep
            /// them (it would need 5 + 4.66 across), at 60 degrees it does (5 + 2.3). On a line within 15 degrees of level only the
            /// arrowheads and the height count. Fitted over 17 000 probes (98 of 100 right; wide words on steep lines are the
            /// ones it misses). A line within 15 degrees of level of exactly that length does not keep them: a radius of 5 under
            /// DIMASZ 2.5 or of 8 under DIMASZ 4, put at half the radius 10, 170, 190 and 350 degrees, with words 1.8, 2.5 and 3.5
            /// high, left them off the line 14 times of 16 (the two that kept them are float noise in AutoCAD); steeper, at 90
            /// degrees, all 4 kept them.
            /// </summary>
            private bool KeepsToLine(double lineLength, double across, bool runsDown, bool nearlyLevel)
            {
                double need =
                    2.0 * _arrow + (runsDown ? _height : 0.0) + (nearlyLevel ? 0.0 : (_width + _gap) * across);
                return nearlyLevel ? lineLength > need + 1E-9 : !(lineLength < need - 1E-9);
            }

            /// <summary>
            /// Words at the end of a line from <paramref name="start"/> that runs <paramref name="way"/> to <paramref name="bend"/>:
            /// along it, on it (the line stopped a gap short of them) or beside it (run on under them); or kept level on a landing.
            /// <paramref name="lineLength"/> is the length of the whole line the words are inside, the radius or the diameter, for
            /// level words under DIMTAD 1 and 4 inside it: they keep to the line only where it has the room (see
            /// <see cref="KeepsToLine"/>), else they stand above a landing.
            /// </summary>
            internal GeoPoint2 Along(
                List<KeyValuePair<GeoPoint2, GeoPoint2>> lines,
                GeoPoint2 start,
                GeoPoint2 bend,
                GeoVector2 way,
                bool level,
                bool outside,
                ref double angle,
                bool clearOfBend,
                bool pushed = false,
                double lineLength = double.PositiveInfinity,
                bool leftTheLine = false
            )
            {
                // Level words pushed out past the rim by the room next to it stand on their landing under DIMTAD 1 and 4 as under 0.
                bool on = On(level && !outside) || (pushed && level && (_tad == 1 || _tad == 4));
                bool nearly = Math.Abs(way.Y) < Math.Sin(Math.PI / 12.0) - 1E-9;
                bool inside =
                    level
                    && !outside
                    && (_tad == 1 || _tad == 4)
                    && (leftTheLine || !double.IsPositiveInfinity(lineLength));

                if (inside)
                {
                    // way runs from the words toward the centre, so a line that runs down has way.Y above nought.
                    on = !leftTheLine && KeepsToLine(lineLength, Math.Abs(way.X), way.Y > 1E-9, nearly);
                }

                bool wide = inside && !on && !nearly && !leftTheLine;

                if (level)
                {
                    angle = 0.0;
                    // On an upright line the landing runs left where the line runs down, right where it runs up, as AutoCAD 2021
                    // lands it.
                    double flat =
                        way.X < -1E-9 ? -1.0
                        : way.X > 1E-9 ? 1.0
                        : way.Y < 0.0 ? -1.0
                        : 1.0;
                    var x = new GeoVector2(flat, 0.0);
                    var up = new GeoVector2(0.0, 1.0);

                    if ((bend - start).Length > 1E-12)
                    {
                        lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(start, bend));
                    }

                    // On a line within 15 degrees of level (not exactly 15), outside or inside for words on the line, as AutoCAD 2021
                    // draws them: words on it have no landing, standing a gap past the bend, and words off it the landing under them;
                    // both are recorded by the bend.
                    bool nearlyLevel = (outside || on || inside) && nearly;

                    if (nearlyLevel && on)
                    {
                        Standing = bend;
                        return bend + x * (_gap + _width / 2.0) + up * _onOffset;
                    }

                    // Words inside that left the line stand where they stood on it, an arrowhead and a gap past its end, above a
                    // landing run on under them.
                    double landing =
                        on ? _arrow
                        : wide ? _arrow + _width + _gap + (_box ? _gap : 0.0)
                        : _width + _gap + (_box ? _gap : 0.0);
                    lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(bend, bend + x * landing));
                    GeoPoint2 wordsMiddle =
                        on || wide ? bend + x * (_arrow + _gap + _width / 2.0) : bend + x * (_gap + _width / 2.0);
                    Standing = nearlyLevel || _bendRecorded ? bend : wordsMiddle;
                    GeoPoint2 at = wordsMiddle + (on ? up * _onOffset : up * (Side * _lift));

                    return at;
                }

                var readable = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
                var above = new GeoVector2(-readable.Y, readable.X);
                GeoPoint2 middle = clearOfBend ? bend + way * (_gap + _width / 2.0) : bend;
                Standing = middle;

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
            internal GeoPoint2 Centred(
                List<KeyValuePair<GeoPoint2, GeoPoint2>> lines,
                GeoPoint2 from,
                GeoPoint2 to,
                GeoPoint2 at,
                GeoVector2 way,
                bool level,
                ref double angle
            )
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
                var whole = new List<KeyValuePair<GeoPoint2, GeoPoint2>>
                {
                    new KeyValuePair<GeoPoint2, GeoPoint2>(from, to),
                };
                lines.AddRange(on ? Broken(whole, at, way, half) : whole);
                Standing = at;

                if (on)
                {
                    return at + above * _onOffset;
                }

                double off = level
                    ? (_width / 2.0 + _gap) * Math.Abs(Math.Sin(angle - lineAngle))
                        + (_height / 2.0 + _gap) * Math.Abs(Math.Cos(angle - lineAngle))
                    : _lift;
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
        internal static List<DxfEntity> Angular(
            DxfDimStyleTableRecord style,
            double dimscale,
            double textHeight,
            GeoPoint2 vertex,
            double radius,
            double from,
            double sweep,
            double[] near,
            double[] far,
            string words,
            GeoPoint2? placed = null,
            double before = 0.0,
            double? turned = null
        ) =>
            AngularLaidOut(
                style,
                dimscale,
                textHeight,
                vertex,
                radius,
                from,
                sweep,
                near,
                far,
                words,
                placed,
                before,
                turned,
                out _,
                out _
            );

        // The outer edge of the band round an angle's arc in which words put under DIMTMOVE 1 get no leader is a length plus a
        // multiple of the landing, both read from these tables where the point lies in a direction whose cosine from the vertex's
        // level is 0, 0.35, 0.7, 0.87, 0.95 and 1 (fitted to AutoCAD 2021's pictures; see AngularLaidOut).
        private static readonly double[] BandCosines = { 0.0, 0.35, 0.7, 0.87, 0.95, 1.0 };
        private static readonly double[] BandOuterLengths = { 1.219, 1.19, 1.0, 0.979, 0.959, 1.271 };
        private static readonly double[] BandOuterLandings = { 0.527, 0.601, 0.868, 0.993, 1.072, 0.978 };

        /// <summary>
        /// The outer edge of the leader band of an angle's words, off the arc, for a point in the direction whose cosine from the
        /// vertex's level is <paramref name="leans"/> and a landing <paramref name="landing"/> long, both tables read between
        /// their knots.
        /// </summary>
        private static double BandOuterEdge(double leans, double landing)
        {
            int i = 1;

            while (i < BandCosines.Length - 1 && leans > BandCosines[i])
            {
                i++;
            }

            double t = Math.Min(
                Math.Max((leans - BandCosines[i - 1]) / (BandCosines[i] - BandCosines[i - 1]), 0.0),
                1.0
            );
            double length = BandOuterLengths[i - 1] + (BandOuterLengths[i] - BandOuterLengths[i - 1]) * t;
            double landings = BandOuterLandings[i - 1] + (BandOuterLandings[i] - BandOuterLandings[i - 1]) * t;
            return length + landing * landings;
        }

        /// <summary>
        /// What level words of an angle or an arc length dimension, standing on its arc, take of the arc, as AutoCAD 2021 counts it: their
        /// width (the symbol before them counted) times the sine of the direction <paramref name="toward"/> of the arc's middle,
        /// plus their height times its cosine, each grown by a gap either side, plus a gap at either end of the arc.
        /// </summary>
        /// <remarks>
        /// Words 0.75 wide and 0.18 high, the symbol before them 0.33, DIMGAP 0.09, at 30 degrees: (0.75 + 0.33 + 0.18) times 0.5
        /// plus (0.18 + 0.18) times 0.87 plus 0.18, 1.12.
        /// </remarks>
        private static double LevelRoom(double width, double height, double gap, double toward) =>
            (width + 2.0 * gap) * Math.Abs(Math.Sin(toward))
            + (height + 2.0 * gap) * Math.Abs(Math.Cos(toward))
            + 2.0 * gap;

        /// <summary>
        /// <see cref="Angular"/>, and whether its words stand outside the arc, where they did not fit between the extension lines
        /// (and were not put by hand): AutoCAD 2021 writes the arc point of such an angle two thirds of the way round, not a third.
        /// The words kept level by DIMTIH fit by what they take of the chord across the turn of the arc.
        /// For an arc length dimension (<paramref name="arcLength"/>) words outside stand level past the first end (the second
        /// for DIMJUST 1), the room of the arc symbol (<paramref name="before"/>) between the landing and them; words inside stand
        /// beside the ray DIMJUST names by what they take of the chord, and <paramref name="wordsFirst"/> says whether they stand
        /// nearer the first end than the middle of the arc, where AutoCAD 2021 writes its arc point two thirds of the way round, not a
        /// third. <paramref name="over"/> is the room the arc symbol takes above the words (DIMARCSYM 1), added to their height.
        /// </summary>
        internal static List<DxfEntity> AngularLaidOut(
            DxfDimStyleTableRecord style,
            double dimscale,
            double textHeight,
            GeoPoint2 vertex,
            double radius,
            double from,
            double sweep,
            double[] near,
            double[] far,
            string words,
            GeoPoint2? placed,
            double before,
            double? turned,
            out bool wordsOutside,
            out bool wordsFirst,
            bool arcLength = false,
            double over = 0.0
        )
        {
            wordsFirst = false;
            Sizes(
                style,
                dimscale,
                textHeight,
                out double scale,
                out double arrow,
                out double height,
                out double gap,
                out double offset,
                out double beyond
            );
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
                measured =
                    measured
                    + ray
                        * (
                            (
                                radius > far[i] ? far[i]
                                : radius < near[i] ? near[i]
                                : radius
                            ) / 2.0
                        );

                if (suppressed[i])
                {
                    continue;
                }

                // From DIMEXO off the object to DIMEXE past the arc, wherever that is any length, as a linear dimension's: an arc
                // 20.3 from the vertex through points 20 out has its extension lines from 20.625 to 21.55, as AutoCAD 2021 draws.
                // Under DIMFXLON they start DIMFXL short of the arc, as a linear dimension's do.
                if (radius >= far[i])
                {
                    double start = far[i] + ExtensionStart(style, scale, offset, radius - far[i]);

                    if (radius + beyond > start)
                    {
                        picture.Add(
                            Lined(
                                new DxfLine(P3(vertex + ray * start), P3(vertex + ray * (radius + beyond))),
                                style.Dimclre,
                                style.Dimlwe
                            )
                        );
                    }
                }
                else if (
                    radius < near[i]
                    && radius - beyond < near[i] - ExtensionStart(style, scale, offset, near[i] - radius)
                )
                {
                    picture.Add(
                        Lined(
                            new DxfLine(
                                P3(vertex + ray * (near[i] - ExtensionStart(style, scale, offset, near[i] - radius))),
                                P3(vertex + ray * Math.Max(0.0, radius - beyond))
                            ),
                            style.Dimclre,
                            style.Dimlwe
                        )
                    );
                }
            }

            string firstName = ArrowOf(style, DxfDimArrowFlag.FirstArrow);
            string secondName = ArrowOf(style, DxfDimArrowFlag.SecondArrow);
            bool noRoom = CadArrowheads.TakesNoRoom(firstName) && CadArrowheads.TakesNoRoom(secondName);
            double trim1 = CadArrowheads.TakesNoRoom(firstName) ? 0.0 : arrow;
            double trim2 = CadArrowheads.TakesNoRoom(secondName) ? 0.0 : arrow;
            double turn = ChordTurn(radius, arrow);
            double chord = sweep < Math.PI ? 2.0 * radius * Math.Sin(sweep / 2.0) : 2.0 * radius;
            // What the words and arrowheads are fitted against is the chord and a third of what the arc has beyond it, which
            // matters from wide sweeps on: probed with arcs of 1 from 90 to 165 degrees, the chord that first took the words
            // (DIMARCSYM 2) ran from 1.09 to 0.99, within 0.01 of this; by the chord alone it came 0.04 to 0.14 too high.
            double room = chord + (radius * sweep - chord) / 3.0;
            // The words between the arrowheads have what is left of the sweep once each arrowhead has taken its turn of the arc,
            // fitted the same way: probed over arcs of 1 from 90 to 165 degrees (tar24, DIMARCSYM 2), the distance that first
            // took the words and both arrowheads ran 0.125 to 0.08 nearer the vertex than the words and the arrowheads laid end
            // to end.
            double between =
                sweep - (trim1 > 0.0 ? ChordTurn(radius, trim1) : 0.0) - (trim2 > 0.0 ? ChordTurn(radius, trim2) : 0.0);
            double roomBetween =
                between <= 0.0 ? 0.0
                : between < Math.PI
                    ? 2.0 * radius * Math.Sin(between / 2.0)
                        + (radius * between - 2.0 * radius * Math.Sin(between / 2.0)) / 3.0
                : 2.0 * radius + (radius * between - 2.0 * radius) / 3.0;
            DxfMText text = (DxfMText)Words(style, default, 0.0, height, words, scale);
            // The words laid out as high as the font measures them, past the height their text is written with.
            double width = WordsWidth(style, words, height, out height);
            var round = new RoundWords(style, scale, height, gap, width);
            bool levelInside = style.Dimtih;
            bool onInside = round.On(levelInside);
            double needText = width + before + 2.0 * gap + (onInside ? 2.0 * gap : 0.0);
            double needArrows = noRoom ? 0.0 : 2.0 * arrow;
            double needBoth = needText + needArrows;

            if (levelInside && !turned.HasValue)
            {
                // The words of an angle or an arc length dimension, with the symbol, stand level where DIMTIH keeps them so: what
                // they take of the arc is their width times the sine of the direction of the arc's middle and their height
                // times its cosine, each grown by a gap either side, and a gap at either end of the arc more (LevelRoom), as
                // measured by where AutoCAD 2021 stands them beside a ray. Probed with arcs of 10 put 12 out, the chord that
                // first took the words ran from 0.57 at 1.6 degrees to 1.43 at 92, within 0.01 of this (before, joined at 45
                // degrees, 0.04), and from 0.375 to 2.35 as DIMGAP went from 0.045 to 0.4. The symbol over the words
                // (DIMARCSYM 1) adds 0.3 of their height (over). An angle of three points was probed alike (rays 10 long, the
                // arc 12 out, chords of 0.2 to 1.6: 4 of 432 differed, now none). Words not on the arc (DIMTAD 2 and 3) keep
                // the older fit, joined at 45 degrees, nothing having been probed there.
                double toward = from + sweep / 2.0;

                if (onInside)
                {
                    needText = LevelRoom(width + before, height + over, gap, toward);
                }
                else
                {
                    double along = needText * Math.Abs(Math.Sin(toward));
                    double across = (height + over + 2.0 * gap) * Math.Abs(Math.Cos(toward));
                    needText = Math.Sqrt(along * along + across * across + Math.Sqrt(2.0) * along * across);
                }

                needBoth = needText + needArrows;
            }

            // Words of an arc length dimension with the arc symbol before them need more than the room left between the
            // arrowheads to stand there with them: probed with arcs of 10 put 12 out (DIMASZ 0.09 to 0.5, chords of 0.1 to
            // 2.5), 0.8 of their height more where they run along the chord (aligned, or level where the arc runs level, at 90
            // degrees), none where they run across it (level, at 0 to 45), and none on wide sweeps (arcs of 1 from 90 degrees
            // on); 0.147 for text 0.18 high. Without the symbol they need none.
            double bothExtra = 0.0;

            if (arcLength && !turned.HasValue && before > 0.0)
            {
                double alongChord = levelInside ? Math.Pow(Math.Abs(Math.Sin(from + sweep / 2.0)), 8.0) : 1.0;
                bothExtra = 0.8 * height * alongChord * Math.Max(0.0, 1.0 - sweep / (Math.PI / 2.0));
            }

            bool textInside;
            bool arrowsInside;
            bool arrowsLeftOut = false;

            if (placed.HasValue)
            {
                textInside = true;
                arrowsInside = needArrows <= room;
            }
            else if (style.Dimtix)
            {
                textInside = true;
                arrowsInside = true;
                arrowsLeftOut = needBoth > room && style.Dimsoxd;
            }
            else if (needText + bothExtra <= roomBetween)
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
                        textInside = needText <= room;
                        arrowsInside = false;
                        break;
                    case 2:
                        textInside = false;
                        arrowsInside = needArrows <= room;
                        break;
                    default:
                        // Where each fits but not both, AutoCAD 2021 keeps the one that takes more room between the rays:
                        // arcs of 10 put 12 out, DIMASZ 0.5 (1 for two arrowheads) and words 0.79 with their gaps, the arrowheads
                        // from a chord of 1.0 to 1.8 with the words outside, those under DIMASZ 0.36 (0.72) leaving the words in.
                        bool arrowsFirst = needArrows <= room && needArrows > needText && needText > roomBetween;
                        textInside = needText <= room && !arrowsFirst;
                        arrowsInside = !textInside && needArrows <= room;
                        break;
                }
            }

            wordsOutside = !placed.HasValue && !textInside;

            // Where the words stand, and the arc and the lines that go with them.
            var arcs = new List<KeyValuePair<double, double>>();
            var lines = new List<KeyValuePair<GeoPoint2, GeoPoint2>>();
            GeoPoint2 at;
            double textAngle;
            int tad = style.Dimtad == 0 && Math.Abs(style.Dimtvp) >= 0.7 ? 1 : style.Dimtad;
            double lift =
                tad != style.Dimtad ? style.Dimtvp * height : gap + height / 2.0 + (style.Dimgap < 0.0 ? gap : 0.0);
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
                double upSide =
                    tad == 4 ? -1.0
                    : tad == 2 ? Sign(Dot(up, away))
                    : 1.0;

                if (levelInside)
                {
                    textAngle = 0.0;
                    double off =
                        (width / 2.0 + gap) * Math.Abs(Math.Sin(tangent))
                        + (height / 2.0 + gap) * Math.Abs(Math.Cos(tangent));
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

                    // Words on the arc that cut into an arrowhead's room send the arrowheads outside, the arc drawn whole: "60%%d"
                    // put 6 degrees from the first ray of an angle of 60 at 35 stands there, as AutoCAD 2021 draws it.
                    if (
                        CutsArrows(
                            breakFrom,
                            breakTo,
                            AlongSweep(theta, from),
                            from,
                            to,
                            from + (trim1 > 0.0 ? ChordTurn(radius, trim1) : 0.0),
                            to - (trim2 > 0.0 ? ChordTurn(radius, trim2) : 0.0)
                        )
                    )
                    {
                        arrowsInside = false;
                        breakFrom = 0.0;
                        breakTo = 0.0;
                    }
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

                    // Words put near the arc have no leader, as AutoCAD 2021 draws them: probed with words every 0.05 off an arc of 30
                    // in 26 styles (DIMASZ 1.5, 2.5 and 4, DIMTXT 2.5 and 4, DIMGAP 0.3, 0.625 and 1.5, "60%%d" with 0 to 6 decimals),
                    // the leader appears once the point is far enough off the arc, as far as the landing (half the words, the gap
                    // and an arrowhead) leans: it counts in full for words level with the vertex and by half for words above or
                    // below it.
                    // The band follows the direction the point lies in from the vertex, and is not the same either side of the arc:
                    // probed with "60%%d" put every 0.25 to 0.5 round an angle of 60 degrees at 20 directions or 72 (SHX and Arial,
                    // DIMASZ 1.5 to 4, DIMTXT 1.8 to 4, DIMGAP 0.3 to 1.5, DIMADEC 0 and 4), the outside edge is a length (1.0 to
                    // 1.3) plus 0.53 to 1.07 landings by the cosine of the direction (tables), the inside edge 0.94 plus up to 2.0
                    // more as the cosine falls, plus the landing times the cosine, and the inside edge above or below the vertex
                    // is 2.9 whatever the words. Arial words 2.5 high with a landing of 5.58 have their edges 6.7 out and 6.5 in
                    // beside the vertex, 4.2 out and 2.9 in above it, 6.5 out at 30 degrees (the leader appears from 6.6 there); a
                    // longer landing moves the outside edge a unit for a unit beside the vertex and half a unit above it, and taller
                    // words narrow the inside by 0.2 a unit of height. The band jumps where the point crosses the height of the
                    // arc's middle: probed every 0.1 across it at five distances either side of the arc of 30 (an angle of 60
                    // degrees) the leader came and went at exactly that height, and between the height of the vertex and it the
                    // band narrows as the point rises: outside by 0.18 landings, inside on the left of the vertex by 0.23 and on
                    // the right wider by 0.18. Refitted over 30 000 probes; it is a fit, not a rule (no box of the words against
                    // the arc, rays or arrowheads followed both edges): a5 49 differing of 2 938 to 33, a7 101 of 5 832 to 71, and
                    // the styles above by a quarter.
                    double distance = (at - vertex).Length;
                    double offArc = distance - radius;
                    double leans = distance > 1E-12 ? Math.Abs(at.X - vertex.X) / distance : 1.0;
                    double basicOff = 1.405 + 0.137 * height;
                    double landingLen = width / 2.0 + gap + arrow;
                    double middleRise = start.Y - vertex.Y;
                    double rise = at.Y - vertex.Y;
                    bool below = middleRise > 1E-9 && rise > 0.0 && rise < middleRise - 1E-9;
                    double lower = below ? rise / middleRise : 0.0;
                    double innerShift =
                        !below ? 0.0
                        : at.X < vertex.X ? -0.233 * lower
                        : 0.18;
                    double outerEdge =
                        BandOuterEdge(leans, landingLen) - 0.182 * landingLen * lower + 0.024 * (height - 2.5);
                    double innerEdge =
                        0.94
                        - 0.207 * (height - 2.5)
                        + 1.997 * Math.Pow(Math.Max(0.0, 1.0 - leans), 2.145)
                        + landingLen * (leans + innerShift);

                    // Words exactly above or below the vertex, under DIMTAD 1 and 4, are the one direction AutoCAD 2021 draws another
                    // leader for: its landing runs under (over) the words, from a gap past them on the side the leader comes from to
                    // their far end, and the band is as wide as it is a little off level (0.79 landings out, 0.7 in).
                    bool straight = Math.Abs(at.X - vertex.X) < 1E-9 && (style.Dimtad == 1 || style.Dimtad == 4);

                    if (
                        (at - start).Length > gap + height / 2.0
                        && (
                            straight
                                ? offArc > basicOff + landingLen * 0.79 || offArc < -(basicOff + landingLen * 0.7)
                                : offArc > outerEdge || offArc < -innerEdge
                        )
                    )
                    {
                        double flat = at.X >= start.X ? 1.0 : -1.0;

                        if (straight)
                        {
                            double under = at.Y + (style.Dimtad == 4 ? lift : -lift);
                            var knee = new GeoPoint2(at.X - flat * (width / 2.0 + gap), under);
                            lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(start, knee));
                            lines.Add(
                                new KeyValuePair<GeoPoint2, GeoPoint2>(
                                    knee,
                                    new GeoPoint2(at.X + flat * width / 2.0, under)
                                )
                            );
                        }
                        else
                        {
                            GeoPoint2 knee = at - new GeoVector2(flat, 0.0) * (width / 2.0 + gap + arrow);
                            lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(start, knee));
                            lines.Add(
                                new KeyValuePair<GeoPoint2, GeoPoint2>(knee, knee + new GeoVector2(flat, 0.0) * arrow)
                            );
                        }
                    }
                    else if (straight)
                    {
                        // Without a leader such words stand a gap and half their height above (below) the point, as above their landing.
                        at += new GeoVector2(0.0, style.Dimtad == 4 ? -lift : lift);
                    }
                }

                // Words put on the arc, free or level, break it about them, as words in the middle of it do, and send the arrowheads
                // outside where they cut into their room.
                if (arrowsInside)
                {
                    double thetaAt = Math.Atan2(at.Y - vertex.Y, at.X - vertex.X);
                    LevelBreak(vertex, radius, at, textAngle, width, height, gap, thetaAt, out breakFrom, out breakTo);

                    if (
                        CutsArrows(
                            breakFrom,
                            breakTo,
                            AlongSweep(thetaAt, from),
                            from,
                            to,
                            from + (trim1 > 0.0 ? ChordTurn(radius, trim1) : 0.0),
                            to - (trim2 > 0.0 ? ChordTurn(radius, trim2) : 0.0)
                        )
                    )
                    {
                        arrowsInside = false;
                        breakFrom = 0.0;
                        breakTo = 0.0;
                    }
                }
            }
            else if (textInside)
            {
                // In the middle of the arc, or next to a ray for DIMJUST 1 and 2 where the words fit there.
                // An arc length dimension's words stand an arrowhead and a gap from the ray, then half of what the fit asked for
                // (the gaps round them, the symbol before them and the turn of the arc counted), measured along the arc, even
                // where that carries them past its middle: probed with the words turned (DIMASZ 0.09 to 0.36, DIMGAP 0.045 to 0.2,
                // text 0.18 and 0.36 high, DIMDEC 0 to 6) they stood within 0.001 of it.
                // Words kept level stand by what their box takes along the arc where it runs through its middle, not what the fit
                // asked for: probed with arcs of 3 put 1.2 out (sweeps 18 to 70 from 10, 100 and 200 degrees, with and without the
                // symbol) and arcs of 10 put 1.2 out (DIMASZ 0.09 to 0.36, DIMGAP 0.045 to 0.2, text 0.18 and 0.36), the room
                // was linear in the width the symbol and the words take, as the sine of the direction of the arc's middle, plus the
                // height as its cosine (each grown by a gap either side), plus a gap at either end of the arc, within 0.002.
                // Aligned words above or below the arc (DIMTAD 1 and the like) leave no gap round them at all.
                double placeNeed = needText;

                if (arcLength && !turned.HasValue && !levelInside && !onInside)
                {
                    placeNeed = needText - 2.0 * gap;
                }

                double along = arcLength ? arrow + gap + placeNeed / 2.0 : arrow + gap + width / 2.0;
                double side =
                    arcLength ? Math.Min(along / radius, Math.PI)
                    : 2.0 * along <= chord ? ChordTurn(radius, along)
                    : 0.0;
                double theta =
                    side > 0.0 && style.Dimjust == 1 ? to - side
                    : side > 0.0 && style.Dimjust == 2 ? from + side
                    : middle;
                wordsFirst = theta < middle - 1E-9;
                double tangent = Readable(theta + Math.PI / 2.0);
                GeoPoint2 point = vertex + Heading(theta) * radius;
                var up = new GeoVector2(-Math.Sin(tangent), Math.Cos(tangent));
                double upSide =
                    tad == 4 ? -1.0
                    : tad == 2 ? Sign(Dot(up, away))
                    : 1.0;

                if (turned.HasValue)
                {
                    textAngle = turned.Value;
                    at = point;
                }
                else if (levelInside)
                {
                    textAngle = 0.0;
                    double off =
                        (width / 2.0 + gap) * Math.Abs(Math.Sin(tangent))
                        + (height / 2.0 + gap) * Math.Abs(Math.Cos(tangent));
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
                    // The symbol over level words (DIMARCSYM 1) takes the arc out above them as far as it stands.
                    double symbolUp = levelInside && !turned.HasValue ? over : 0.0;
                    GeoPoint2 middleOfAll =
                        at
                        - Heading(textAngle) * (before / 2.0)
                        + new GeoVector2(-Math.Sin(textAngle), Math.Cos(textAngle)) * (symbolUp / 2.0);
                    LevelBreak(
                        vertex,
                        radius,
                        middleOfAll,
                        textAngle,
                        width + before,
                        height + symbolUp,
                        gap,
                        theta,
                        out breakFrom,
                        out breakTo
                    );
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
                wordsFirst = true;
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
                    lines.Add(
                        new KeyValuePair<GeoPoint2, GeoPoint2>(vertex + outward * radius, vertex + outward * knee)
                    );
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
                wordsFirst = !second;
                double clear =
                    !arrowsInside ? 2.0 * arrow
                    : noRoom ? 0.0
                    : arrow;
                double run = clear > 0.0 ? ChordTurn(radius, clear) : 0.0;
                double end = second ? to + run : from - run;

                if (run > 0.0)
                {
                    arcs.Add(
                        second ? new KeyValuePair<double, double>(to, end) : new KeyValuePair<double, double>(end, from)
                    );
                }

                GeoPoint2 point = vertex + Heading(end) * radius;
                GeoVector2 way = second
                    ? new GeoVector2(-Math.Sin(end), Math.Cos(end))
                    : new GeoVector2(Math.Sin(end), -Math.Cos(end));

                // Turned along the arc there, read the way words in the middle of the arc would read.
                double across = middle - Math.PI / 2.0;
                double tangent = end - Math.PI / 2.0 + (Math.Cos(Readable(across) - across) < 0.0 ? Math.PI : 0.0);
                var up = new GeoVector2(-Math.Sin(tangent), Math.Cos(tangent));
                bool on = round.On(false);

                if (style.Dimtoh)
                {
                    // Level words run out the way the arc runs on there, left or right, as AutoCAD 2021 puts those of an angle
                    // whose first ray points at -5 degrees to the left, and at 100 degrees to the right.
                    textAngle = 0.0;
                    double flat = way.X < -1E-9 ? -1.0 : 1.0;

                    if (!arcLength && !second)
                    {
                        // The words of an angle, though, go by where its arc point stands, two thirds round: to the right where that is
                        // more than 0.05 above the vertex, else to the left. Probed with the first ray from -3 to 5 degrees and from
                        // 180 on, the arc 12 out (the change came at the arc point's 0.24 degrees, 0.05 up) and again with it 24 and 120
                        // out (0.05 up at 0.12 and 0.024 degrees).
                        flat = (Heading(from + sweep * 2.0 / 3.0) * radius).Y > 0.05 ? 1.0 : -1.0;
                    }

                    if (arcLength)
                    {
                        // An arc length dimension's go by the direction of its first end (the arc's start): to the right where it
                        // points up from the horizontal, to the left where it points down, the other way round past the second
                        // ray (DIMJUST 1); and the room of the arc symbol stands before the words on either side of the landing.
                        double s = Math.Sin(from);
                        flat = s > 1E-9 || (s >= -1E-9 && Math.Cos(from) > 0.0) ? 1.0 : -1.0;
                        flat = second ? -flat : flat;
                    }

                    var x = new GeoVector2(flat, 0.0);
                    double upSide =
                        tad == 4 ? -1.0
                        : tad == 2 ? Sign(away.Y)
                        : 1.0;
                    lines.Add(
                        new KeyValuePair<GeoPoint2, GeoPoint2>(
                            point,
                            point + x * (on ? arrow : arrow + width + 2.0 * gap)
                        )
                    );
                    at =
                        point
                        + x * (arrow + gap + (arcLength ? before : 0.0) + width / 2.0)
                        + new GeoVector2(0.0, 1.0) * (on ? onOffset : upSide * lift);
                }
                else
                {
                    textAngle = tangent;
                    double upSide =
                        tad == 4 ? -1.0
                        : tad == 2 ? Sign(Dot(up, away))
                        : 1.0;

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
                picture.Add(
                    Lined(
                        new DxfArc(P3(vertex), radius, Turned(arc.Key), Turned(arc.Value)),
                        style.Dimclrd,
                        style.Dimlwd
                    )
                );
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
                GeoVector2 firstWay = CadArrowheads.TakesNoRoom(firstName)
                    ? new GeoVector2(Math.Sin(from), -Math.Cos(from)) * sign
                    : UnitOf(start2 - (vertex + Heading(from + sign * turn) * radius));
                GeoVector2 secondWay = CadArrowheads.TakesNoRoom(secondName)
                    ? new GeoVector2(-Math.Sin(to), Math.Cos(to)) * sign
                    : UnitOf(end2 - (vertex + Heading(to - sign * turn) * radius));
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
        private static double ChordTurn(double radius, double length) =>
            2.0 * Math.Asin(Math.Min(1.0, length / (2.0 * radius)));

        private static GeoVector2 Heading(double angle) => new GeoVector2(Math.Cos(angle), Math.Sin(angle));

        private static GeoVector2 UnitOf(GeoVector2 v) => v.Length > 1E-12 ? v / v.Length : new GeoVector2(1.0, 0.0);

        private static double Sign(double value) => value < 0.0 ? -1.0 : 1.0;

        /// <summary>
        /// Whether words standing on the arc, broken between two angles, cut into the room of an arrowhead at either end: the words'
        /// middle (<paramref name="theta"/>) between the rays, their break reaching back past where an arrowhead's back is.
        /// </summary>
        private static bool CutsArrows(
            double breakFrom,
            double breakTo,
            double theta,
            double from,
            double to,
            double firstBack,
            double secondBack
        ) =>
            breakTo > breakFrom
            && theta > from + 1E-9
            && theta < to - 1E-9
            && ((breakFrom < firstBack && breakTo > from) || (breakTo > secondBack && breakFrom < to));

        /// <summary>An angle brought within a turn above <paramref name="from"/>, so that it can be compared with the sweep that starts there.</summary>
        private static double AlongSweep(double angle, double from)
        {
            double turn = 2.0 * Math.PI;
            double a = angle - from;
            a -= Math.Floor(a / turn) * turn;
            return from + a;
        }

        /// <summary>
        /// The angles about the vertex between which an arc through words centred on it is taken out: where the arc leaves the
        /// box of the words grown by the gap, either side of the angle of the words.
        /// </summary>
        private static void LevelBreak(
            GeoPoint2 vertex,
            double radius,
            GeoPoint2 at,
            double angle,
            double width,
            double height,
            double gap,
            double theta,
            out double low,
            out double high
        )
        {
            var x = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
            var y = new GeoVector2(-x.Y, x.X);
            double a = width / 2.0 + gap;
            double b = height / 2.0 + gap;
            low = theta;
            high = theta;
            double lowest = double.MaxValue;
            double highest = double.MinValue;

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

                foreach (
                    double t in new[] { (-qb - Math.Sqrt(disc)) / (2.0 * qa), (-qb + Math.Sqrt(disc)) / (2.0 * qa) }
                )
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

            // An arc that leaves the point at the words' angle outside their box (its corner clipped by the circle) is broken only
            // where the circle meets the box: "60%%d" of 3 wide, a gap of 1.5, put 5 off an arc of 30 at 30 degrees, from 29.45 to 29.53.
            if (lowest > highest)
            {
                return;
            }

            low = theta + lowest;
            high = theta + highest;
        }
    }
}
