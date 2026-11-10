using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>DIMJOGGED: a jogged radius, its line drawn from a centre put in place of one off the drawing.</summary>
    /// <remarks>
    /// As AutoCAD 2021 draws it: the line from the centre drawn runs parallel to the radius, jogs across to it at DIMJOGANG
    /// and runs on along it to the arrowhead at the rim. The jog's middle is its point, half way between the radius and its
    /// parallel. A circle of 50 about the origin, its centre drawn at (0, -100), its words put at (30, 0) and its jog at
    /// (15, -50) has its lines from (0, -100) to (65, -100), on to (-35, 0) and on to the arrowhead's back at (47.5, 0).
    /// </remarks>
    internal static partial class CadCommands
    {
        /// <summary>
        /// DIMJOGGED: an arc or a circle, the centre drawn in its place, where the words go (Mtext and Text for them, Angle for
        /// theirs), then where the line jogs. One undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> DimJogged(CadCommandContext c)
        {
            DxfDimStyleTableRecord style = DimStyle(c);

            if (style == null)
            {
                c.Write("The drawing has no dimension style to draw in.");
                yield break;
            }

            GeoPoint2 center;
            double radius;

            while (true)
            {
                yield return CadPrompt.ForEntity("Select arc or circle");

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                DxfEntity picked = c.Document.FindEntity(c.Input.Handles[0]);

                if (picked is DxfCircle circle && circle.Radius > c.Session.Tolerance.EqualPoint)
                {
                    center = new GeoPoint2(circle.Center.X, circle.Center.Y);
                    radius = circle.Radius;
                    break;
                }

                if (picked is DxfArc arc && arc.Radius > c.Session.Tolerance.EqualPoint)
                {
                    center = new GeoPoint2(arc.Center.X, arc.Center.Y);
                    radius = arc.Radius;
                    break;
                }

                c.Write("Object selected is not a circle or arc.");
            }

            yield return CadPrompt.ForPoint("Specify center location override");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 over = c.Input.Point;
            var probe = new DxfRadialDimensionLarge(P3(center), P3(center), P3(over), P3(over), 0.0, string.Empty, null);
            c.Write("Dimension text = " + Words(style, probe, radius, null, string.Empty));
            string text = null;
            double? textAngle = null;

            while (true)
            {
                string words = text;
                double? turned = textAngle;
                yield return CadPrompt.ForPoint("Specify dimension line location", center, new[] { "Mtext", "Text", "Angle" })
                    .Previewing(DimPreview(c, input => JoggedMade(c, style, center, radius, over, input.Point, Middle2(over, input.Point), words, turned, out _)));

                if (c.Input.Is("Mtext") || c.Input.Is("Text"))
                {
                    yield return CadPrompt.ForString("Enter dimension text", "<>");

                    if (c.Input.Kind == CadInputKind.Text)
                    {
                        text = c.Input.Text;
                    }

                    continue;
                }

                if (c.Input.Is("Angle"))
                {
                    yield return CadPrompt.ForAngle("Specify angle of dimension text");

                    if (c.Input.Kind == CadInputKind.Number)
                    {
                        textAngle = TextAngle(c, c.Input.Number);
                    }

                    continue;
                }

                break;
            }

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 location = c.Input.Point;
            string given = text;
            double? angle = textAngle;
            yield return CadPrompt.ForPoint("Specify jog location", location)
                .Previewing(DimPreview(c, input => JoggedMade(c, style, center, radius, over, location, input.Point, given, angle, out _)));

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            List<DxfEntity> picture = JoggedMade(c, style, center, radius, over, location, c.Input.Point, text, textAngle, out DxfDimension made);
            AddedHandle(Place(c, style, TextHeight(c, style), made, picture));
        }

        private static GeoPoint2 Middle2(GeoPoint2 a, GeoPoint2 b) => new GeoPoint2((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);

        /// <summary>
        /// The picture of a jogged radius of a circle, its words put at a point and its jog given at another, and the dimension
        /// it is the picture of, as DIMJOGGED makes them (<see cref="JogPlaces"/>).
        /// </summary>
        private static List<DxfEntity> JoggedMade(CadCommandContext c, DxfDimStyleTableRecord style, GeoPoint2 center, double radius, GeoPoint2 over, GeoPoint2 location, GeoPoint2 jogGiven, string text, double? textAngle, out DxfDimension made)
        {
            double textHeight = TextHeight(c, style);
            var probe = new DxfRadialDimensionLarge(P3(center), P3(center), P3(over), P3(over), 0.0, text ?? string.Empty, null);
            string words = Words(style, probe, radius, text, "R");
            Sizes(style, Dimscale(c), textHeight, out double scale, out double arrow, out double height, out double gap, out _, out _);
            double width = WordsWidth(style, words, height, out _);
            JogPlaces(center, radius, over, location, jogGiven, style.Dimjogang, arrow, gap, width, style.Dimtad == 0, c.Session.Tolerance.EqualPoint, out GeoPoint2 chord, out GeoPoint2 jog, out GeoPoint2 at);

            // AutoCAD 2021 writes nought for the jog's angle and draws it at DIMJOGANG.
            var jogged = new DxfRadialDimensionLarge(P3(center), P3(chord), P3(over), P3(jog), 0.0, text ?? string.Empty, null) { TextPosition = P3(at) };
            made = jogged;
            return WithTextAngle(made, Jogged(style, Dimscale(c), textHeight, center, chord, over, jog, at, words), textAngle);
        }

        /// <summary>
        /// Where DIMJOGGED puts the chord point, the jog and the words, as AutoCAD 2021 puts them. The words stay where they are
        /// put if they fall on the same side of the rim as the foot of the centre drawn on the radius through them (and, where
        /// that centre is on the radius, not past it); else they go three quarters of the way from the centre drawn to the rim
        /// where they were put, and the radius turns through them: words put 80 out at 14.04 degrees about a circle of 50, the
        /// centre drawn 100 below, stand at (36.38, -15.905), the radius at -23.61 degrees. The jog's middle lies half way
        /// between the radius and its parallel through the centre drawn, as far from a point on that middle line as the point
        /// given is: the point where a line from the centre drawn at the jog's angle, leaning away from the rim, meets it. Words
        /// the jog would leave off the line stand past the arrowhead, a gap clear.
        /// </summary>
        internal static void JogPlaces(GeoPoint2 center, double radius, GeoPoint2 over, GeoPoint2 location, GeoPoint2 jogGiven, double jogAngle, double arrow, double gap, double width, bool onLine, double tolerance, out GeoPoint2 chord, out GeoPoint2 jog, out GeoPoint2 words)
        {
            GeoVector2 u = UnitOf(location - center);
            double tOver = Dot(over - center, u);
            double tWords = (location - center).Length;
            bool onRadius = Math.Abs(Dot(over - center, new GeoVector2(-u.Y, u.X))) <= 1E-9;
            words = location;

            if (onRadius && tOver > radius && tWords > tOver)
            {
                // Past the centre drawn on the radius itself: the jog half way between it and the rim, the words an arrowhead,
                // a gap and half their width from the jog toward the rim, as AutoCAD 2021 draws words put 120 out under a
                // centre drawn 100 out.
                chord = center + u * radius;
                double half = (radius + tOver) / 2.0;
                jog = center + u * half;
                words = center + u * (half - (arrow + gap + width / 2.0));
                return;
            }

            if ((tWords - radius) * (tOver - radius) <= 0.0 || Math.Abs(tOver - radius) <= tolerance)
            {
                GeoPoint2 rim = center + u * radius;
                GeoPoint2 instead = over + (rim - over) * 0.75;

                if ((instead - center).Length > 1E-9)
                {
                    words = instead;
                    u = UnitOf(instead - center);
                }
            }

            chord = center + u * radius;
            jog = JogOn(center, radius, u, over, jogGiven, jogAngle);

            if (Math.Abs(Dot(over - center, new GeoVector2(-u.Y, u.X))) <= 1E-9)
            {
                return;
            }

            // The words kept on the line between the rim and the end of the jog.
            JogFrame(center, radius, u, over, jog, jogAngle, out _, out GeoPoint2 back, out _);
            double tBack = Dot(back - center, u);
            double t = Dot(words - center, u);

            if (t < Math.Min(tBack, radius) - 1E-9 || t > Math.Max(tBack, radius) + 1E-9)
            {
                double way = tBack >= radius ? 1.0 : -1.0;
                double past = radius + way * (arrow + (onLine ? 2.0 : 1.0) * gap + width / 2.0);
                double half = (radius + way * arrow + tBack) / 2.0;
                words = center + u * (way > 0.0 ? Math.Min(past, half) : Math.Max(past, half));
            }
        }

        /// <summary>
        /// The jog of a jogged radius whose line runs along <paramref name="u"/>, given at a point: on the middle line between the
        /// radius and its parallel through the centre drawn, as AutoCAD 2021 puts it; on the radius where the centre drawn is on it.
        /// </summary>
        internal static GeoPoint2 JogOn(GeoPoint2 center, double radius, GeoVector2 u, GeoPoint2 over, GeoPoint2 given, double jogAngle)
        {
            var n = new GeoVector2(-u.Y, u.X);
            double h = Dot(over - center, n);
            double tGiven = Dot(given - center, u);

            if (Math.Abs(h) <= 1E-9)
            {
                return center + u * tGiven;
            }

            double tOver = Dot(over - center, u);
            double sign = radius >= tOver ? 1.0 : -1.0;
            double reach = JogReach(h, jogAngle);
            double tFrom = tOver - sign * reach;
            double off = Dot(given - center, n) - h / 2.0;
            double t = tFrom + sign * Math.Sqrt((tGiven - tFrom) * (tGiven - tFrom) + off * off);
            return center + u * t + n * (h / 2.0);
        }

        /// <summary>How far along the radius half the jog runs: half the gap between the radius and its parallel, over the tangent of the jog's angle.</summary>
        private static double JogReach(double h, double jogAngle)
        {
            double tangent = Math.Tan(jogAngle > 1E-9 && jogAngle < Math.PI ? jogAngle : Math.PI / 4.0);
            return Math.Abs(tangent) > 1E6 ? 0.0 : Math.Abs(h) / (2.0 * Math.Abs(tangent));
        }

        /// <summary>
        /// The ends of a jog: <paramref name="front"/> on the parallel through the centre drawn, toward the rim from the jog's
        /// middle, and <paramref name="back"/> on the radius, away from it; the jog's middle itself for both where the centre
        /// drawn is on the radius. <paramref name="side"/> is 1 where the line runs out from the end of the jog to the rim.
        /// </summary>
        private static void JogFrame(GeoPoint2 center, double radius, GeoVector2 u, GeoPoint2 over, GeoPoint2 jog, double jogAngle, out GeoPoint2 front, out GeoPoint2 back, out double side)
        {
            var n = new GeoVector2(-u.Y, u.X);
            double h = Dot(over - center, n);
            double tOver = Dot(over - center, u);
            double sign = radius >= tOver ? 1.0 : -1.0;
            double reach = Math.Abs(h) <= 1E-9 ? 0.0 : JogReach(h, jogAngle);
            front = jog + u * (sign * reach) + n * (h / 2.0);
            back = jog - u * (sign * reach) - n * (h / 2.0);
            double tBack = Dot(back - center, u);
            side = radius >= tBack ? 1.0 : -1.0;
        }

        /// <summary>
        /// The picture of a jogged radius, as AutoCAD 2021 draws it: the line from the centre drawn along the radius's parallel to
        /// the jog, across at DIMJOGANG to the radius and on to the back of the arrowhead at the rim; the words at their point,
        /// on the line for DIMTAD 0 (the line broken a gap clear of them), above it as they read for the others.
        /// </summary>
        internal static List<DxfEntity> Jogged(DxfDimStyleTableRecord style, double dimscale, double textHeight, GeoPoint2 center, GeoPoint2 chord, GeoPoint2 over, GeoPoint2 jog, GeoPoint2 at, string words)
        {
            double radius = (chord - center).Length;

            if (radius <= 1E-12)
            {
                return null;
            }

            Sizes(style, dimscale, textHeight, out double scale, out double arrow, out double height, out double gap, out _, out _);
            var picture = new List<DxfEntity>();
            GeoVector2 u = (chord - center) / radius;
            double angle = Readable(Math.Atan2(u.Y, u.X));
            DxfEntity text = Words(style, default, angle, height, words, scale);
            double width = WordsWidth(style, words, height, out height);
            JogFrame(center, radius, u, over, jog, style.Dimjogang, out GeoPoint2 front, out GeoPoint2 back, out double side);
            string arrowName = ArrowOf(style, DxfDimArrowFlag.FirstArrow);
            GeoPoint2 arrowBack = CadArrowheads.TakesNoRoom(arrowName) ? chord : chord - u * (side * arrow);
            var lines = new List<KeyValuePair<GeoPoint2, GeoPoint2>>();

            if ((front - back).Length > 1E-9)
            {
                if ((front - over).Length > 1E-9)
                {
                    lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(over, front));
                }

                lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(front, back));
            }
            else if ((back - over).Length > 1E-9)
            {
                lines.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(over, back));
            }

            var run = new List<KeyValuePair<GeoPoint2, GeoPoint2>> { new KeyValuePair<GeoPoint2, GeoPoint2>(back, arrowBack) };
            bool onLine = style.Dimtad == 0;
            GeoPoint2 wordsAt = at;

            if (onLine)
            {
                GeoPoint2 middle = center + u * Dot(at - center, u);
                run = Broken(run, middle, u, width / 2.0 + gap);
            }
            else
            {
                var reads = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
                var above = new GeoVector2(-reads.Y, reads.X);
                wordsAt = at + above * ((style.Dimtad == 4 ? -1.0 : 1.0) * (gap + height / 2.0));
            }

            lines.AddRange(run);

            foreach (KeyValuePair<GeoPoint2, GeoPoint2> line in lines)
            {
                if ((line.Value - line.Key).Length > 1E-9)
                {
                    picture.Add(Lined(new DxfLine(P3(line.Key), P3(line.Value)), style.Dimclrd, style.Dimlwd));
                }
            }

            AddArrow(picture, style, DxfDimArrowFlag.FirstArrow, chord, u * side, arrow);
            ((DxfMText)text).Location = P3(wordsAt);
            picture.Add(text);
            AddFrame(picture, style, wordsAt, ((DxfMText)text).Rotation, width, height, gap);
            picture.Add(Definition(chord));
            picture.Add(Definition(over));
            picture.Add(Definition(jog));
            return picture;
        }

        /// <summary>The picture of a jogged radius of the drawing from its points, its words at its text's point.</summary>
        private static List<DxfEntity> JoggedPicture(DxfRadialDimensionLarge dimension, DxfDimStyleTableRecord style, double textHeight)
        {
            GeoPoint2 center = P2(dimension.Center);
            GeoPoint2 chord = P2(dimension.ChordPoint);
            double radius = (chord - center).Length;

            if (radius <= 1E-12)
            {
                return null;
            }

            return Jogged(style, style.Dimscale, textHeight, center, chord, P2(dimension.OverrideCenter), P2(dimension.JogPoint), P2(dimension.TextPosition), Formatted(dimension, radius, "R"));
        }

        /// <summary>
        /// Writes into a jogged radius what dragging one of its grips does: the chord point (0) turns the radius about the centre,
        /// the words with it; the centre drawn (1) moves; the jog (2) goes where it is dragged; the words (3) move and the radius
        /// turns through them. The jog is then put back half way between the radius and its parallel, as DIMJOGGED puts it.
        /// </summary>
        internal static void MoveJogged(DxfRadialDimensionLarge jogged, int gripIndex, GeoPoint2 to)
        {
            GeoPoint2 center = P2(jogged.Center);
            GeoPoint2 chord = P2(jogged.ChordPoint);
            double radius = (chord - center).Length;
            GeoPoint2 over = P2(jogged.OverrideCenter);
            GeoPoint2 jog = P2(jogged.JogPoint);
            GeoPoint2 words = P2(jogged.TextPosition);
            GeoVector2 u = UnitOf(chord - center);

            switch (gripIndex)
            {
                case 0:
                    GeoVector2 turned = UnitOf(to - center);
                    double turn = Math.Atan2(turned.Y, turned.X) - Math.Atan2(u.Y, u.X);
                    GeoVector2 off = words - center;
                    words = center + new GeoVector2(off.X * Math.Cos(turn) - off.Y * Math.Sin(turn), off.X * Math.Sin(turn) + off.Y * Math.Cos(turn));
                    u = turned;
                    break;
                case 1:
                    over = to;
                    break;
                case 2:
                    jog = to;
                    break;
                default:
                    words = to;
                    u = UnitOf(to - center);
                    break;
            }

            jogged.ChordPoint = new GeoPoint3(center.X + u.X * radius, center.Y + u.Y * radius, jogged.ChordPoint.Z);
            jogged.OverrideCenter = new GeoPoint3(over.X, over.Y, jogged.OverrideCenter.Z);
            GeoPoint2 put = JogOn(center, radius, u, over, jog, jogged.GetDimstyleData().Dimjogang);
            jogged.JogPoint = new GeoPoint3(put.X, put.Y, jogged.JogPoint.Z);
            jogged.TextPosition = new GeoPoint3(words.X, words.Y, jogged.TextPosition.Z);
        }
    }
}
