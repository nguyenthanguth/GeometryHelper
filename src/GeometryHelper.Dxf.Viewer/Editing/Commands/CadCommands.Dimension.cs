using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The dimension commands: DIMLINEAR, DIMALIGNED, DIMRADIUS, DIMDIAMETER, DIMANGULAR.</summary>
    /// <remarks>
    /// The library does not draw a dimension: what draws one is its block (<c>*D1</c>), which AutoCAD draws and names itself.
    /// So a dimension is made with its picture (its extension lines, dimension line, arrowheads as SOLIDs, its words as an
    /// MTEXT and its points of definition on Defpoints, from the drawing's current dimension variables) in an anonymous
    /// block added as <c>*D</c>, which the block table names as AutoCAD names its own, the first <c>*D</c> number the drawing
    /// has not. The height of the words is the style's DIMTXT, or the host's default where it sets one ("dim.text.height" of
    /// <see cref="CadCommandSession.Remembered"/>, as the presenter sets Global settings' text height), written then as an
    /// override of the dimension.
    /// </remarks>
    internal static partial class CadCommands
    {
        /// <summary>The start of words a linear picture stands past a leader, kept with their MTEXT for <see cref="Recorded"/>.</summary>
        private static readonly ConditionalWeakTable<DxfMText, StrongBox<GeoPoint3>> WordsStarts =
            new ConditionalWeakTable<DxfMText, StrongBox<GeoPoint3>>();

        /// <summary>Keeps the point a dimension records its words by (group 11), for <see cref="Recorded"/>, where it is not their middle.</summary>
        private static void RecordWordsBy(DxfMText words, GeoPoint2 point)
        {
            WordsStarts.Remove(words);
            WordsStarts.Add(words, new StrongBox<GeoPoint3>(P3(point)));
        }

        private static IEnumerable<CadPrompt> DimLinear(CadCommandContext c) => DimLine(c, false);

        private static IEnumerable<CadPrompt> DimAligned(CadCommandContext c) => DimLine(c, true);

        /// <summary>
        /// DIMLINEAR and DIMALIGNED: two origins (or a line selected), then where the line goes, with AutoCAD 2021's options:
        /// Mtext and Text ask for the words, Angle turns them, and DIMLINEAR's Horizontal, Vertical and Rotated set the angle
        /// of the line, Rotated 30 measuring 0,0 to 100,0 as 86.6025. DIM gives the first origin it
        /// was answered with as <paramref name="first"/>, and the last point before it as <paramref name="lastBefore"/>, and the
        /// command goes on from there. A perpendicular or a tangent snapped at the first origin is drawn from the last point given
        /// before it, as AutoCAD 2021 draws it, and tied with that point as its last point.
        /// </summary>
        private static IEnumerable<CadPrompt> DimLine(
            CadCommandContext c,
            bool aligned,
            GeoPoint2? first = null,
            GeoPoint2? lastBefore = null
        )
        {
            DxfDimStyleTableRecord style = DimStyle(c);

            if (style == null)
            {
                c.Write("The drawing has no dimension style to draw in.");
                yield break;
            }

            GeoPoint2 last = lastBefore ?? c.Session.LastPoint;

            if (!first.HasValue)
            {
                yield return CadPrompt
                    .ForPoint("Specify first extension line origin or", null, null, "select object")
                    .SnappingFrom(last);
            }

            GeoPoint2 p1;
            GeoPoint2 p2;
            var tied = new DxfOsnapPointRef[2];

            if (first.HasValue || c.Input.IsPoint)
            {
                p1 = first ?? c.Input.Point;
                // DIM gives its own answer as the first origin, whose snap is still the input here.
                tied[0] = CadDimAssociation.FromSnap(c.Document, 0, c.Input.Snap, last);
                yield return CadPrompt.ForPoint("Specify second extension line origin", p1);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                p2 = c.Input.Point;
                tied[1] = CadDimAssociation.FromSnap(c.Document, 1, c.Input.Snap, p1, tied[0]);
            }
            else
            {
                yield return CadPrompt.ForEntity("Select object to dimension");

                if (
                    c.Input.Kind != CadInputKind.Selection
                    || !(c.Document.FindEntity(c.Input.Handles[0]) is DxfLine line)
                )
                {
                    c.Write("Object selected is not a line.");
                    yield break;
                }

                p1 = new GeoPoint2(line.StartPoint.X, line.StartPoint.Y);
                p2 = new GeoPoint2(line.EndPoint.X, line.EndPoint.Y);
                tied = CadDimAssociation.LineEnds(line);
            }

            if ((p2 - p1).Length <= c.Session.Tolerance.EqualPoint)
            {
                c.Write("*Invalid* The two origins are one point.");
                yield break;
            }

            string text = null;
            double? fixedAngle = null;
            double? textAngle = null;

            while (true)
            {
                // AutoCAD 2021's options: [Mtext/Text/Angle/Horizontal/Vertical/Rotated], DIMALIGNED's [Mtext/Text/Angle].
                string[] keywords = aligned
                    ? new[] { "Mtext", "Text", "Angle" }
                    : new[] { "Mtext", "Text", "Angle", "Horizontal", "Vertical", "Rotated" };
                string words = text;
                double? fixedTo = fixedAngle;
                double? turned = textAngle;
                yield return CadPrompt
                    .ForPoint("Specify dimension line location", p2, keywords)
                    .Previewing(
                        DimPreview(c, input =>
                            LinearMade(c, style, aligned, p1, p2, input.Point, words, fixedTo, turned, out _, out _)
                        )
                    );

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

                if (c.Input.Is("Horizontal"))
                {
                    fixedAngle = 0.0;
                    continue;
                }

                if (c.Input.Is("Vertical"))
                {
                    fixedAngle = Math.PI / 2.0;
                    continue;
                }

                if (c.Input.Is("Rotated"))
                {
                    // The angle of the line in the UCS, 0 on Enter, as AutoCAD 2021 asks it.
                    yield return CadPrompt.ForAngle("Specify angle of dimension line", null, null, Number(0.0));
                    fixedAngle = c.Input.Kind == CadInputKind.Number ? c.Input.Number : 0.0;
                    continue;
                }

                break;
            }

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            CadDimAssociation.Tie(
                c,
                MakeLinear(c, style, aligned, p1, p2, c.Input.Point, text, fixedAngle, textAngle),
                tied
            );
        }

        /// <summary>
        /// Makes a linear (or an aligned) dimension of two origins, its line through a point, as DIMLINEAR and DIMALIGNED make
        /// it; its words given or the measurement, turned where <paramref name="textAngle"/> says. The handle of the dimension,
        /// or null where it measures nothing.
        /// </summary>
        private static string MakeLinear(
            CadCommandContext c,
            DxfDimStyleTableRecord style,
            bool aligned,
            GeoPoint2 p1,
            GeoPoint2 p2,
            GeoPoint2 location,
            string text,
            double? fixedAngle,
            double? textAngle
        )
        {
            List<DxfEntity> picture = LinearMade(
                c,
                style,
                aligned,
                p1,
                p2,
                location,
                text,
                fixedAngle,
                textAngle,
                out DxfDimension made,
                out string words
            );

            if (picture == null)
            {
                c.Write("*Invalid* The dimension measures nothing.");
                return null;
            }

            c.Write("Dimension text = " + words);
            return AddedHandle(Place(c, style, TextHeight(c, style), made, picture));
        }

        /// <summary>
        /// The picture of a linear (or an aligned) dimension of two origins, its line through a point, and the dimension it is
        /// the picture of, as <see cref="MakeLinear"/> adds them and the prompt of the line's place previews them; null where
        /// it measures nothing.
        /// </summary>
        private static List<DxfEntity> LinearMade(
            CadCommandContext c,
            DxfDimStyleTableRecord style,
            bool aligned,
            GeoPoint2 p1,
            GeoPoint2 p2,
            GeoPoint2 location,
            string text,
            double? fixedAngle,
            double? textAngle,
            out DxfDimension made,
            out string words
        )
        {
            // A linear dimension measures along the X or Y axis of the UCS, as AutoCAD's: in a UCS turned 30 degrees it is a
            // rotated dimension of 30 or 120 degrees.
            CadUcs ucs = c.Session.Ucs;
            double angle = aligned
                ? Math.Atan2(p2.Y - p1.Y, p2.X - p1.X)
                : ucs.AngleToWorld(fixedAngle ?? LinearAngle(ucs.ToUcs(p1), ucs.ToUcs(p2), ucs.ToUcs(location)));
            var along = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
            double measured = Math.Abs((p2 - p1).X * along.X + (p2 - p1).Y * along.Y);
            made = null;
            words = null;

            if (measured <= c.Session.Tolerance.EqualPoint)
            {
                return null;
            }

            GeoPoint2 q1 = location + along * ((p1 - location).X * along.X + (p1 - location).Y * along.Y);
            GeoPoint2 q2 = location + along * ((p2 - location).X * along.X + (p2 - location).Y * along.Y);
            made = aligned
                ? (DxfDimension)new DxfAlignedDimension(P3(p1), P3(p2), P3(q2), text ?? string.Empty, null)
                : new DxfRotatedDimension(angle, P3(p1), P3(p2), P3(q2), text ?? string.Empty, null);
            words = Words(style, made, measured, text, string.Empty);
            double textHeight = TextHeight(c, style);
            return WithTextAngle(
                made,
                Linear(style, Dimscale(c), textHeight, p1, p2, q1, q2, words, turned: textAngle),
                textAngle
            );
        }

        /// <summary>
        /// The preview of the dimension the point at the cursor would make: its picture, each part on the layer and in the
        /// colour the dimension gives it once made, as a picture of layer 0 and colour ByBlock takes the dimension's.
        /// </summary>
        private static CadPreview DimPreview(CadCommandContext c, Func<CadInput, List<DxfEntity>> picture)
        {
            string layer =
                !string.IsNullOrEmpty(c.Session.DimLayer) && c.Database.LayerTable.Has(c.Session.DimLayer)
                    ? c.Session.DimLayer
                    : c.Database.Clayer;
            DxfDimStyleTableRecord style = DimStyle(c);
            return CadPreview.Making(c, input =>
            {
                List<DxfEntity> parts = picture(input);

                if (parts == null)
                {
                    return null;
                }

                if (style != null)
                {
                    Dressed(style, parts);
                }

                foreach (DxfEntity part in parts)
                {
                    if (string.IsNullOrEmpty(part.Layer) || part.Layer == "0")
                    {
                        part.Layer = layer;
                    }

                    if (part.Color.IsByBlock)
                    {
                        part.Color = DxfColor.ByLayer;
                    }
                }

                return parts;
            });
        }

        /// <summary>Gives a dimension to be made the angle its words were given (Text angle), and its picture's words that turn.</summary>
        private static List<DxfEntity> WithTextAngle(DxfDimension made, List<DxfEntity> picture, double? textAngle)
        {
            if (textAngle.HasValue)
            {
                made.TextRotation = textAngle.Value;

                foreach (DxfEntity entity in picture)
                {
                    if (entity is DxfMText words)
                    {
                        words.Rotation = textAngle.Value;
                    }
                }
            }

            return picture;
        }

        private static IEnumerable<CadPrompt> DimRadial(CadCommandContext c, bool diameter)
        {
            DxfDimStyleTableRecord style = DimStyle(c);

            if (style == null)
            {
                c.Write("The drawing has no dimension style to draw in.");
                yield break;
            }

            // AutoCAD 2021 makes each radius and diameter with DIMUPT on, and writes it: 288 1.
            style.Keep(DxfDimVariable.Named("Dimupt"), true);

            yield return CadPrompt.ForEntity("Select arc or circle");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            DxfEntity picked = c.Document.FindEntity(c.Input.Handles[0]);
            GeoPoint2? pickedAt = c.Input.IsPickedAt ? c.Input.Point : (GeoPoint2?)null;
            GeoPoint2 center;
            double radius;

            switch (picked)
            {
                case DxfCircle circle:
                    center = new GeoPoint2(circle.Center.X, circle.Center.Y);
                    radius = circle.Radius;
                    break;
                case DxfArc arc:
                    center = new GeoPoint2(arc.Center.X, arc.Center.Y);
                    radius = arc.Radius;
                    break;
                default:
                    c.Write("Object selected is not a circle or arc.");
                    yield break;
            }

            double measured = diameter ? 2.0 * radius : radius;
            string prefix = diameter ? "%%c" : "R";
            DxfDimension probe = diameter
                ? (DxfDimension)new DxfDiametricDimension(P3(center), P3(center), 0.0, string.Empty, null)
                : new DxfRadialDimension(P3(center), P3(center), 0.0, string.Empty, null);
            string text = null;
            c.Write("Dimension text = " + Words(style, probe, measured, null, prefix));

            while (true)
            {
                string words = text;
                yield return CadPrompt
                    .ForPoint("Specify dimension line location", center, new[] { "Text" })
                    .Previewing(
                        DimPreview(c, input =>
                            RadialMade(c, style, center, radius, diameter, input.Point, words, null, pickedAt, out _)
                        )
                    );

                if (c.Input.Is("Text"))
                {
                    yield return CadPrompt.ForString("Enter dimension text", "<>");

                    if (c.Input.Kind == CadInputKind.Text)
                    {
                        text = c.Input.Text;
                    }

                    continue;
                }

                break;
            }

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            CadDimAssociation.Tie(
                c,
                MakeRadial(c, style, center, radius, diameter, c.Input.Point, text, null, pickedAt),
                CadDimAssociation.CenterOf(picked)
            );
        }

        /// <summary>
        /// Makes a radius (or a diameter) dimension of a circle, its line toward a point, as DIMRADIUS and DIMDIAMETER make it;
        /// its words given or the measurement, turned where <paramref name="textAngle"/> says. The handle of the dimension.
        /// </summary>
        /// <remarks>
        /// As AutoCAD 2021 makes it under the style's DIMTMOVE: 0 turns the line toward the point; 1 and 2 keep it through the
        /// point the circle was picked at (<paramref name="pickedAt"/>, toward the point where it was given by its handle), the
        /// words for 1 slid along the line to where the point falls on it, for 2 standing at the point itself. A circle of 20
        /// picked at 30 degrees, the point 45 out at 70, has its line at 70 degrees for 0; at 30 for 1, the words 34.47 out
        /// along it; at 30 for 2, the words at the point, the line from the centre to the back of the arrowhead inside.
        /// </remarks>
        private static string MakeRadial(
            CadCommandContext c,
            DxfDimStyleTableRecord style,
            GeoPoint2 center,
            double radius,
            bool diameter,
            GeoPoint2 location,
            string text,
            double? textAngle,
            GeoPoint2? pickedAt = null
        )
        {
            List<DxfEntity> picture = RadialMade(
                c,
                style,
                center,
                radius,
                diameter,
                location,
                text,
                textAngle,
                pickedAt,
                out DxfDimension made
            );
            return AddedHandle(Place(c, style, TextHeight(c, style), made, picture));
        }

        /// <summary>
        /// The picture of a radius (or a diameter) dimension of a circle, its line toward a point (or through the point the
        /// circle was picked at, under DIMTMOVE 1 and 2), and the dimension it is the picture of, as <see cref="MakeRadial"/>
        /// adds them and the prompt of the line's place previews them.
        /// </summary>
        private static List<DxfEntity> RadialMade(
            CadCommandContext c,
            DxfDimStyleTableRecord style,
            GeoPoint2 center,
            double radius,
            bool diameter,
            GeoPoint2 location,
            string text,
            double? textAngle,
            GeoPoint2? pickedAt,
            out DxfDimension made
        )
        {
            double measured = diameter ? 2.0 * radius : radius;
            string prefix = diameter ? "%%c" : "R";
            int move = style.Dimtmove;
            GeoVector2 toward =
                move != 0 && pickedAt.HasValue && (pickedAt.Value - center).Length > c.Session.Tolerance.EqualPoint
                    ? pickedAt.Value - center
                    : location - center;

            if (toward.Length <= c.Session.Tolerance.EqualPoint)
            {
                toward = new GeoVector2(1.0, 0.0);
            }

            GeoVector2 unit = toward / toward.Length;
            GeoPoint2 chord = center + unit * radius;
            GeoPoint2 far = center - unit * radius;
            // AutoCAD 2021 writes a leader of nought for every radius and diameter it makes.
            made = diameter
                ? (DxfDimension)new DxfDiametricDimension(P3(chord), P3(far), 0.0, text ?? string.Empty, null)
                : new DxfRadialDimension(P3(center), P3(chord), 0.0, text ?? string.Empty, null);
            string words = Words(style, made, measured, text, prefix);
            double textHeight = TextHeight(c, style);

            // Words of DIMTMOVE 2 stand where put; of 1 where the point falls on a line kept apart from it.
            GeoPoint2 along = center + unit * ((location - center).X * unit.X + (location - center).Y * unit.Y);

            // Level words under DIMTMOVE 1 slide to where the point is level with the line, not to the foot of the point on it:
            // AutoCAD 2021 runs the line of a circle picked at 30 degrees, the point put 40 degrees off it, on to the height of the
            // point, and the words stand level there.
            if (move == 1 && !style.Dimtix && (style.Dimtoh || style.Dimtih) && Math.Abs(unit.Y) > 1E-9)
            {
                double level = (location.Y - center.Y) / unit.Y;

                if (level >= radius - 1E-10 ? style.Dimtoh : style.Dimtih)
                {
                    along = center + unit * level;
                }
            }

            // DIMTIX keeps the words at their default place next to the rim wherever the point was put, under DIMTMOVE 1 and 2 as 0.
            bool free = move == 2 && !style.Dimtix;
            bool slid = move == 1 && !style.Dimtix && (along - location).Length > c.Session.Tolerance.EqualPoint;
            GeoPoint2 wordsAt = slid ? along : location;

            // Words slid onto the line outside the rim still keep the room next to it: AutoCAD 2021 holds "R20" put 20.3 out
            // off the line it was picked on, DIMTMOVE 1, 20.9794 out on it and writes it with the words at their default place.
            List<DxfEntity> picture = RadialLaidOut(
                style,
                Dimscale(c),
                textHeight,
                center,
                radius,
                chord,
                far,
                wordsAt,
                diameter,
                words,
                slid && (wordsAt - center).X * unit.X + (wordsAt - center).Y * unit.Y < radius - 1E-10,
                free,
                out bool atPoint
            );

            // Under DIMTMOVE 1 AutoCAD 2021 records the words by the point they were put at, where the leader ends: "R20" put
            // 40 out at 30 degrees in acad.dwt's Standard writes (34.641, 20), its words level past a landing.
            if (move == 1 && !slid)
            {
                foreach (DxfEntity entity in picture)
                {
                    if (entity is DxfMText mtext)
                    {
                        RecordWordsBy(mtext, wordsAt);
                    }
                }
            }

            // Where the words stand at the point put (not where the fit, DIMTIX or the room next to the rim put them) AutoCAD 2021
            // writes the dimension with flag 128: 164 for "R20" put 35 out at 18.9 degrees under DIMATFIT 3, group 11 the point
            // of the line they stand by; and 36 where the fit put them, group 11 their middle.
            if (atPoint)
            {
                made.TextPosition = free ? P3(wordsAt) : RecordedPoint(picture) ?? P3(wordsAt);
                made.UsingDefaultTextPosition = false;
            }
            else
            {
                // At their default place the words are recorded by their middle.
                foreach (DxfEntity entity in picture)
                {
                    if (entity is DxfMText mtext)
                    {
                        WordsStarts.Remove(mtext);
                    }
                }
            }

            return WithTextAngle(made, picture, textAngle);
        }

        private static IEnumerable<CadPrompt> DimAngular(CadCommandContext c) => DimAngularFrom(c, null, null, null);

        /// <summary>
        /// What DIM's angular dimension of an arc gives back to DIM: the option it was answered with to go back to the radius or
        /// the diameter of the arc (Radius, Diameter), and the words and the angle of the words it was given on the way.
        /// </summary>
        private sealed class DimBack
        {
            internal string Switch;
            internal string Text;
            internal double? TextAngle;
        }

        /// <summary>
        /// DIMANGULAR: an arc, a circle and a second point, two lines or a vertex and two points, then where the arc goes. DIM
        /// gives the object it was answered with as <paramref name="given"/>, and the second line where one was picked as
        /// <paramref name="secondGiven"/>, and the command goes on from there. DIM's angle of an arc gives the <paramref name="back"/>
        /// it keeps its words in: it asks "Specify angular dimension location or [Radius/Diameter/Mtext/Text/text aNgle/Undo]:" and
        /// answers Radius or Diameter by handing control back to DIM's radius or diameter prompt.
        /// </summary>
        private static IEnumerable<CadPrompt> DimAngularFrom(
            CadCommandContext c,
            DxfEntity given,
            DxfLine secondGiven,
            DimBack back
        )
        {
            DxfDimStyleTableRecord style = DimStyle(c);

            if (style == null)
            {
                c.Write("The drawing has no dimension style to draw in.");
                yield break;
            }

            if (given == null)
            {
                yield return CadPrompt.ForEntity("Select arc, circle, line, or", null, "specify vertex");
            }

            GeoPoint2 vertex;
            DxfLine first = null;
            DxfArc tiedArc = null;
            DxfLine second = null;

            // The two rays the angle lies between, and the stretch of each its object covers, as distances from the vertex.
            var rays = new GeoVector2[2];
            var near = new double[2];
            var far = new double[2];
            var points = new GeoPoint2[2];
            var snapped = new DxfOsnapPointRef[3];

            if (given != null || c.Input.Kind == CadInputKind.Selection)
            {
                DxfEntity picked = given ?? c.Document.FindEntity(c.Input.Handles[0]);

                if (picked is DxfArc arc && arc.Normal.Z > 0.0)
                {
                    vertex = new GeoPoint2(arc.Center.X, arc.Center.Y);
                    points[0] = new GeoPoint2(arc.StartPoint.X, arc.StartPoint.Y);
                    points[1] = new GeoPoint2(arc.EndPoint.X, arc.EndPoint.Y);
                    tiedArc = arc;
                }
                else if (picked is DxfCircle circle && circle.Normal.Z > 0.0)
                {
                    // The angle of a circle runs from the point it was picked at, about its center, to a second point, each
                    // brought onto the circle, as AutoCAD 2021 makes it.
                    vertex = new GeoPoint2(circle.Center.X, circle.Center.Y);
                    GeoPoint2 pickedAt = c.Input.IsPickedAt ? c.Input.Point : vertex;
                    points[0] = OnCircle(vertex, circle.Radius, pickedAt);
                    yield return CadPrompt.ForPoint("Specify second angle endpoint", vertex);

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    points[1] = OnCircle(vertex, circle.Radius, c.Input.Point);
                    snapped = CadDimAssociation.CircleAngle(c.Document, circle, points[0], points[1], c.Input.Snap);
                }
                else if (picked is DxfLine line)
                {
                    first = line;
                    DxfLine other = secondGiven;

                    if (other == null)
                    {
                        yield return CadPrompt.ForEntity("Select second line");
                        other =
                            c.Input.Kind == CadInputKind.Selection
                                ? c.Document.FindEntity(c.Input.Handles[0]) as DxfLine
                                : null;
                    }

                    if (other == null || ReferenceEquals(other, line))
                    {
                        c.Write("Object selected is not a second line.");
                        yield break;
                    }

                    // The line selected second is the dimension's first (groups 13 and 14), as AutoCAD 2021 writes it whichever
                    // way the lines run, whichever was drawn first and wherever the arc goes.
                    first = other;
                    second = line;

                    if (!Meet(first, second, c.Session.Tolerance, out vertex))
                    {
                        c.Write("Lines are parallel.");
                        yield break;
                    }
                }
                else
                {
                    c.Write("Object selected is not a line, arc or circle.");
                    yield break;
                }
            }
            else if (c.Input.IsNone)
            {
                // A perpendicular or a tangent at the vertex is drawn from the last point, at the ends from the vertex, each
                // tied with the point it was drawn from, as AutoCAD 2021 ties them.
                GeoPoint2 last = c.Session.LastPoint;
                yield return CadPrompt.ForPoint("Specify angle vertex").SnappingFrom(last);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                vertex = c.Input.Point;
                snapped[2] = CadDimAssociation.FromSnap(c.Document, 2, c.Input.Snap, last);

                for (int i = 0; i < 2; i++)
                {
                    yield return CadPrompt.ForPoint(
                        i == 0 ? "Specify first angle endpoint" : "Specify second angle endpoint",
                        vertex
                    );

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    points[i] = c.Input.Point;
                    snapped[i] = CadDimAssociation.FromSnap(c.Document, i, c.Input.Snap, vertex, snapped[2]);
                }
            }
            else
            {
                yield break;
            }

            string text = back?.Text;
            double? textAngle = back?.TextAngle;

            // The picture of the angular dimension of an arc through a point, and the dimension, as it is previewed and made;
            // null where it measures nothing.
            List<DxfEntity> AngularMade(GeoPoint2 location, string typed, out DxfDimension made, out string words)
            {
                double radius = (location - vertex).Length;
                double a1 = Math.Atan2(points[0].Y - vertex.Y, points[0].X - vertex.X);

                // Two points along one ray measure nothing wherever the arc is put, its point on the ray as far out as the point
                // given, as AutoCAD 2021 makes it: of (100, 0) and (200, 0) about (0, 0) put at (-150, 20), (151.328, 0).
                bool oneRay =
                    first == null
                    && tiedArc == null
                    && OneRay(a1, Math.Atan2(points[1].Y - vertex.Y, points[1].X - vertex.X))
                    && (points[0] - vertex).Length > c.Session.Tolerance.EqualPoint
                    && (points[1] - vertex).Length > c.Session.Tolerance.EqualPoint;

                if (oneRay)
                {
                    location = vertex + new GeoVector2(Math.Cos(a1), Math.Sin(a1)) * radius;
                }

                AngularSector(
                    first,
                    second,
                    vertex,
                    points,
                    location,
                    rays,
                    near,
                    far,
                    out double from,
                    out double sweep
                );
                made = null;
                words = null;

                if (
                    (!oneRay && sweep <= c.Session.Tolerance.EqualAngleRad / 1000.0)
                    || radius <= c.Session.Tolerance.EqualPoint
                )
                {
                    return null;
                }

                // The arc goes through the point a third of the way round from where it starts, as far out as the point given, as
                // AutoCAD 2021 writes it: put at (50, 550) in the right angle at (0, 500), it passes (61.237, 535.355).
                GeoPoint2 arc =
                    vertex + new GeoVector2(Math.Cos(from + sweep / 3.0), Math.Sin(from + sweep / 3.0)) * radius;
                made =
                    first != null
                        ? (DxfDimension)
                            new DxfLineAngularDimension2(
                                first.StartPoint,
                                first.EndPoint,
                                second.StartPoint,
                                second.EndPoint,
                                P3(arc),
                                typed ?? string.Empty,
                                null
                            )
                        : new DxfPoint3AngularDimension(
                            P3(vertex),
                            P3(points[0]),
                            P3(points[1]),
                            P3(arc),
                            typed ?? string.Empty,
                            null
                        );
                words = Words(style, made, sweep, typed, string.Empty);
                return WithTextAngle(
                    made,
                    Angular(style, Dimscale(c), TextHeight(c, style), vertex, radius, from, sweep, near, far, words),
                    textAngle
                );
            }

            while (true)
            {
                string shown = text;
                CadPrompt ask =
                    back == null
                        ? CadPrompt.ForPoint("Specify dimension arc line location", vertex, new[] { "Text" })
                        : CadPrompt.ForPoint(
                            "Specify angular dimension location",
                            vertex,
                            new[] { "Radius", "Diameter", "Mtext", "Text", "text aNgle", "Undo" }
                        );
                yield return ask.Previewing(DimPreview(c, input => AngularMade(input.Point, shown, out _, out _)));

                if (c.Input.Is("Text") || c.Input.Is("Mtext"))
                {
                    yield return CadPrompt.ForString("Enter dimension text", "<>");

                    if (c.Input.Kind == CadInputKind.Text)
                    {
                        text = c.Input.Text;
                    }

                    continue;
                }

                if (back != null && c.Input.Is("text aNgle"))
                {
                    yield return CadPrompt.ForAngle("Specify angle of dimension text");

                    if (c.Input.Kind == CadInputKind.Number)
                    {
                        textAngle = TextAngle(c, c.Input.Number);
                    }

                    continue;
                }

                if (back != null && (c.Input.Is("Radius") || c.Input.Is("Diameter")))
                {
                    back.Switch = c.Input.Is("Diameter") ? "Diameter" : "Radius";
                    back.Text = text;
                    back.TextAngle = textAngle;
                    yield break;
                }

                break;
            }

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            List<DxfEntity> picture = AngularMade(c.Input.Point, text, out DxfDimension made, out string words);

            if (picture == null)
            {
                c.Write("*Invalid* The angle measures nothing.");
                yield break;
            }

            c.Write("Dimension text = " + words);
            string handle = AddedHandle(Place(c, style, TextHeight(c, style), made, picture));

            CadDimAssociation.Tie(
                c,
                handle,
                tiedArc != null ? CadDimAssociation.ArcEnds(tiedArc)
                    : first != null ? CadDimAssociation.TwoLines(first, second)
                    : snapped
            );
        }

        /// <summary>
        /// The angle an angular dimension measures, from where its arc is put: of two lines, the one of their four angles the
        /// point lies in, each ray taking the line that runs along it; of a vertex and two points, the angle between them or the
        /// one outside it. The rays and how far along each its object reaches are written into the arrays given.
        /// </summary>
        private static void AngularSector(
            DxfLine first,
            DxfLine second,
            GeoPoint2 vertex,
            GeoPoint2[] points,
            GeoPoint2 location,
            GeoVector2[] rays,
            double[] near,
            double[] far,
            out double from,
            out double sweep
        )
        {
            double towards = Math.Atan2(location.Y - vertex.Y, location.X - vertex.X);

            if (first != null)
            {
                // Of the four angles two lines make, the one the arc is put in.
                GeoVector2 u1 = Along(first);
                GeoVector2 u2 = Along(second);
                from = 0.0;
                sweep = 0.0;

                foreach (double s1 in new[] { 1.0, -1.0 })
                {
                    foreach (double s2 in new[] { 1.0, -1.0 })
                    {
                        Sector(u1 * s1, u2 * s2, out double a, out double b, out double turn);

                        if (CadGrips.Sweep(a, towards) <= turn)
                        {
                            from = a;
                            sweep = turn;
                            rays[0] = new GeoVector2(Math.Cos(a), Math.Sin(a));
                            rays[1] = new GeoVector2(Math.Cos(b), Math.Sin(b));
                        }
                    }
                }

                // Each ray takes the line that runs along it.
                for (int i = 0; i < 2; i++)
                {
                    DxfLine along =
                        Math.Abs(Cross(rays[i], Along(first))) <= Math.Abs(Cross(rays[i], Along(second)))
                            ? first
                            : second;
                    Reach(along, vertex, rays[i], out near[i], out far[i]);
                }
            }
            else
            {
                double a1 = Math.Atan2(points[0].Y - vertex.Y, points[0].X - vertex.X);
                double a2 = Math.Atan2(points[1].Y - vertex.Y, points[1].X - vertex.X);
                double turn = CadGrips.Sweep(a1, a2);

                // Two points along one ray from the vertex with the arc on it measure nothing, the arc a point of the ray, as
                // AutoCAD 2021 measures them; with the arc off the ray they measure the whole turn.
                if (OneRay(a1, a2) && OneRay(a1, towards))
                {
                    from = a1;
                    sweep = 0.0;
                    rays[0] = rays[1] = new GeoVector2(Math.Cos(a1), Math.Sin(a1));
                    near[0] = far[0] = (points[0] - vertex).Length;
                    near[1] = far[1] = (points[1] - vertex).Length;
                    return;
                }

                bool inside = CadGrips.Sweep(a1, towards) <= turn;
                from = inside ? a1 : a2;
                sweep = inside ? turn : 2.0 * Math.PI - turn;
                rays[0] = new GeoVector2(Math.Cos(from), Math.Sin(from));
                rays[1] = new GeoVector2(Math.Cos(from + sweep), Math.Sin(from + sweep));
                GeoPoint2 p0 = inside ? points[0] : points[1];
                GeoPoint2 p1 = inside ? points[1] : points[0];
                near[0] = far[0] = (p0 - vertex).Length;
                near[1] = far[1] = (p1 - vertex).Length;
            }
        }

        /// <summary>Whether two angles are of one ray, a thousandth of the default angle tolerance apart at most.</summary>
        private static bool OneRay(double a, double b)
        {
            double turn = CadGrips.Sweep(a, b);
            double least = Tolerance.Default.EqualAngleRad / 1000.0;
            return turn <= least || turn >= 2.0 * Math.PI - least;
        }

        private static GeoVector2 Along(DxfLine line)
        {
            var way = new GeoVector2(line.EndPoint.X - line.StartPoint.X, line.EndPoint.Y - line.StartPoint.Y);
            return way / way.Length;
        }

        private static double Cross(GeoVector2 a, GeoVector2 b) => a.X * b.Y - a.Y * b.X;

        /// <summary>
        /// A point brought onto a circle along the line from its center, as AutoCAD 2021 takes the points of the angle of a
        /// circle: (200, 25.2) on the circle of 25 about (200, 0) is (200, 25); the point at angle nought for the center itself.
        /// </summary>
        private static GeoPoint2 OnCircle(GeoPoint2 center, double radius, GeoPoint2 point)
        {
            GeoVector2 way = point - center;
            double far = way.Length;
            return far > 0.0 ? center + way * (radius / far) : center + new GeoVector2(radius, 0.0);
        }

        /// <summary>Where two lines, drawn on as far as needed, meet; false where they are parallel within the tolerance.</summary>
        internal static bool Meet(DxfLine a, DxfLine b, Tolerance tolerance, out GeoPoint2 point)
        {
            point = default;
            var p = new GeoPoint2(a.StartPoint.X, a.StartPoint.Y);
            var q = new GeoPoint2(b.StartPoint.X, b.StartPoint.Y);
            GeoVector2 r = Along(a);
            GeoVector2 s = Along(b);
            double d = Cross(r, s);

            if (Math.Abs(d) <= tolerance.EqualVector)
            {
                return false;
            }

            double t = Cross(q - p, s) / d;
            point = p + r * t;
            return true;
        }

        /// <summary>The smaller angle between two ways, counter-clockwise from <paramref name="from"/> to <paramref name="to"/> angles.</summary>
        private static void Sector(GeoVector2 u, GeoVector2 v, out double from, out double to, out double sweep)
        {
            double a = Math.Atan2(u.Y, u.X);
            double b = Math.Atan2(v.Y, v.X);
            double turn = CadGrips.Sweep(a, b);

            if (turn <= Math.PI)
            {
                from = a;
                to = b;
                sweep = turn;
            }
            else
            {
                from = b;
                to = a;
                sweep = 2.0 * Math.PI - turn;
            }
        }

        /// <summary>How far along a ray from the vertex a line reaches, nearest and farthest, nought where it reaches back past the vertex.</summary>
        private static void Reach(DxfLine line, GeoPoint2 vertex, GeoVector2 ray, out double near, out double far)
        {
            double s = (line.StartPoint.X - vertex.X) * ray.X + (line.StartPoint.Y - vertex.Y) * ray.Y;
            double e = (line.EndPoint.X - vertex.X) * ray.X + (line.EndPoint.Y - vertex.Y) * ray.Y;
            near = Math.Max(0.0, Math.Min(s, e));
            far = Math.Max(0.0, Math.Max(s, e));
        }

        private static double Turned(double angle)
        {
            angle -= Math.Floor(angle / (2.0 * Math.PI)) * 2.0 * Math.PI;
            return angle >= 2.0 * Math.PI ? 0.0 : angle;
        }

        /// <summary>
        /// The variables a dimension to be made is drawn by, as AutoCAD 2021 draws a new one: the drawing's current ones (its
        /// header's), named as the style it is drawn in (see <see cref="NamedDimStyle"/>), DIMSCALE the scale it is drawn by
        /// (see <see cref="Dimscale"/>); null where the drawing has no style. <c>Place</c> writes each other than the
        /// style's as an override: DIMASZ 5 typed over a style of 2.5 draws arrowheads 5 long and writes <c>1070 41, 1040 5</c>.
        /// Where <c>$DIMSTYLE</c> names no style of the drawing, the header is drawn by all the same, in the style fallen back
        /// on, each variable other than that style's written as an override.
        /// </summary>
        private static DxfDimStyleTableRecord DimStyle(CadCommandContext c)
        {
            DxfDimStyleTableRecord style = NamedDimStyle(c);

            if (style == null)
            {
                return null;
            }

            DxfDimStyleTableRecord drawn = c.Database.GetDimstyleData();
            drawn.Name = style.Name;
            drawn.Keep(DxfDimVariable.Named("Dimscale"), Dimscale(c));
            return drawn;
        }

        /// <summary>
        /// The current dimension style of the drawing (the one the header's <c>$DIMSTYLE</c> names), else Standard, else the
        /// first; null where it has none, or in a drawing of R12.
        /// </summary>
        private static DxfDimStyleTableRecord NamedDimStyle(CadCommandContext c)
        {
            if (string.Equals(c.Database.Version, "AC1009", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string name = null;

            try
            {
                name = c.Database.Dimstyle;
            }
            catch (Exception)
            {
                name = null;
            }

            DxfDimStyleTableRecord style =
                string.IsNullOrEmpty(name) || !c.Database.DimStyleTable.Has(name)
                    ? null
                    : c.Database.DimStyleTable[name];

            if (style == null && c.Database.DimStyleTable.Has("Standard"))
            {
                style = c.Database.DimStyleTable["Standard"];
            }

            if (style == null)
            {
                foreach (DxfDimStyleTableRecord any in c.Database.DimStyleTable)
                {
                    return any;
                }
            }

            return style;
        }

        /// <summary>
        /// The angle of a linear dimension AutoCAD takes from where its line is put: level where the point lies above or below
        /// the two origins, upright where it lies to their left or right, else by which way it lies farther out.
        /// </summary>
        private static double LinearAngle(GeoPoint2 p1, GeoPoint2 p2, GeoPoint2 location)
        {
            double outX = Math.Max(Math.Min(p1.X, p2.X) - location.X, location.X - Math.Max(p1.X, p2.X));
            double outY = Math.Max(Math.Min(p1.Y, p2.Y) - location.Y, location.Y - Math.Max(p1.Y, p2.Y));
            return outX > outY ? Math.PI / 2.0 : 0.0;
        }

        /// <summary>The words a dimension shows: the measurement as the style writes it, put in the place of a &lt;&gt; of text typed.</summary>
        private static string Words(
            DxfDimStyleTableRecord style,
            DxfDimension dimension,
            double measured,
            string typed,
            string prefix
        )
        {
            string number;

            try
            {
                dimension.DimensionStyleName = style.Name;

                // A dimension in no drawing yet reads AutoCAD's own variables, not its style's: the measurement is written by a
                // copy given the style's variables as its own, as the dimension reads them once it is in the drawing.
                var probe = (DxfDimension)dimension.Clone();
                probe.SetDimstyleData(style);
                number = probe.FormatMeasurement(measured, string.Empty);
            }
            catch (Exception)
            {
                number = measured.ToString("0.##", CultureInfo.InvariantCulture);
            }

            number = prefix + number;

            if (string.IsNullOrEmpty(typed))
            {
                return number;
            }

            return typed.Replace("<>", number);
        }

        /// <summary>
        /// The picture of a linear dimension: the extension lines (each left out where DIMSE1 or DIMSE2 says), the dimension
        /// line (each half left out where DIMSD1 or DIMSD2 says, run on by DIMDLE past ticks), two arrowheads of the style and
        /// the words, placed as AutoCAD places them from the style's variables (see <see cref="LinearLayout"/>).
        /// </summary>
        /// <remarks>
        /// <paramref name="placed"/> is the text's point of words put somewhere by hand (DIMTEDIT), which the words stand by as
        /// the style's DIMTMOVE says (see <see cref="PlaceMoved"/>); they are not moved outside. <paramref name="jog"/> is the
        /// point of a jog of DIMJOGLINE on the line, drawn <paramref name="jogHeight"/> high. <paramref name="turned"/> is the
        /// angle the words were turned to (DIMLINEAR's Angle, DIMEDIT Rotate), in radians from the x axis; null for none.
        /// </remarks>
        internal static List<DxfEntity> Linear(
            DxfDimStyleTableRecord style,
            double dimscale,
            double textHeight,
            GeoPoint2 p1,
            GeoPoint2 p2,
            GeoPoint2 q1,
            GeoPoint2 q2,
            string words,
            GeoPoint2? placed = null,
            GeoPoint2? jog = null,
            double jogHeight = 0.0,
            double? turned = null
        )
        {
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
            GeoVector2 along = (q2 - q1) / (q2 - q1).Length;
            double angle = Math.Atan2(along.Y, along.X);

            // The words read from the left or from below, as AutoCAD turns them.
            if (angle > Math.PI / 2.0 + 1E-9 || angle <= -Math.PI / 2.0 + 1E-9)
            {
                angle += Math.PI;
            }

            DxfEntity text = Words(style, default, angle, height, words, scale);
            // The words laid out as high as the font measures them, past the height their text is written with.
            double width = WordsWidth(style, words, height, out height);
            LinearLayout layout = LayOut(
                style,
                p1,
                p2,
                q1,
                q2,
                along,
                angle,
                width,
                height,
                gap,
                arrow,
                beyond,
                placed,
                turned
            );
            bool[] suppressed = { style.Dimse1, style.Dimse2 };
            int side = 0;

            // A point on the dimension line has its extension line too, from DIMEXO to DIMEXE past it, the way the other one runs
            // (square to the line where neither runs any way), as AutoCAD 2021 draws it; under DIMFXLON each starts DIMFXL short of
            // the dimension line (see ExtensionStart).
            GeoVector2 normal = new GeoVector2(-along.Y, along.X);
            GeoVector2 other =
                (q1 - p1).Length > 1E-12 ? (q1 - p1) / (q1 - p1).Length
                : (q2 - p2).Length > 1E-12 ? (q2 - p2) / (q2 - p2).Length
                : normal;

            foreach (
                KeyValuePair<GeoPoint2, GeoPoint2> pair in new[]
                {
                    new KeyValuePair<GeoPoint2, GeoPoint2>(p1, q1),
                    new KeyValuePair<GeoPoint2, GeoPoint2>(p2, q2),
                }
            )
            {
                GeoVector2 up = pair.Value - pair.Key;
                double length = up.Length;

                double start = ExtensionStart(style, scale, offset, length);

                if (length + beyond > start && !suppressed[side])
                {
                    GeoVector2 unit = length > 1E-12 ? up / length : other;
                    double longer = layout.OverExtension == side + 1 ? layout.Longer : 0.0;
                    picture.Add(
                        Lined(
                            new DxfLine(P3(pair.Key + unit * start), P3(pair.Value + unit * (beyond + longer))),
                            style.Dimclre,
                            style.Dimlwe
                        )
                    );
                }

                side++;
            }

            // The line between the extension lines, drawn where the arrowheads stand inside or DIMTOFL says, broken about words
            // centred on it and at a jog; and outside, the short lines the arrowheads stand on and the line run on to words there.
            var segments = new List<KeyValuePair<GeoPoint2, GeoPoint2>>();

            if (layout.ArrowsInside || style.Dimtofl)
            {
                DimensionLine(segments, style, q1, q2, along, scale, layout.ArrowsInside ? arrow : 0.0);
            }

            if (layout.Break > 0.0)
            {
                segments = Broken(segments, layout.Middle, along, layout.Break);
            }

            if (jog.HasValue && jogHeight > 0.0)
            {
                GeoPoint2 at = Foot(jog.Value, q1, along, 0.0);
                segments = Broken(segments, at, along, jogHeight / 2.0);
                var square = new GeoVector2(-along.Y, along.X);

                // AutoCAD 2021's jog: a jog 3.75 high about x = 25 runs 23.125, 24.375 up 1.875, 25.625 down 1.875, 26.875.
                GeoPoint2[] zigzag =
                {
                    at - along * (jogHeight / 2.0),
                    at - along * (jogHeight / 6.0) + square * (jogHeight / 2.0),
                    at + along * (jogHeight / 6.0) - square * (jogHeight / 2.0),
                    at + along * (jogHeight / 2.0),
                };

                for (int i = 0; i + 1 < zigzag.Length; i++)
                {
                    segments.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(zigzag[i], zigzag[i + 1]));
                }
            }

            foreach (KeyValuePair<GeoPoint2, GeoPoint2> segment in layout.Outside)
            {
                segments.Add(segment);
            }

            foreach (KeyValuePair<GeoPoint2, GeoPoint2> segment in segments)
            {
                picture.Add(Lined(new DxfLine(P3(segment.Key), P3(segment.Value)), style.Dimclrd, style.Dimlwd));
            }

            if (!layout.ArrowsSuppressed)
            {
                AddArrow(picture, style, DxfDimArrowFlag.FirstArrow, q1, layout.ArrowsInside ? -along : along, arrow);
                AddArrow(picture, style, DxfDimArrowFlag.SecondArrow, q2, layout.ArrowsInside ? along : -along, arrow);
            }

            var mtext = (DxfMText)text;
            mtext.Location = P3(layout.At);

            if (layout.Angle != angle)
            {
                mtext.Rotation = layout.Angle < 0.0 ? layout.Angle + 2.0 * Math.PI : layout.Angle;
            }

            picture.Add(text);

            if (layout.Start.HasValue)
            {
                WordsStarts.Add(mtext, new StrongBox<GeoPoint3>(P3(layout.Start.Value)));
            }

            AddFrame(picture, style, layout.At, mtext.Rotation, width, height, gap);
            picture.Add(Definition(p1));
            picture.Add(Definition(p2));
            picture.Add(Definition(q2));
            return picture;
        }

        /// <summary>Where the parts of a linear dimension stand, as <see cref="LayOut"/> works them out.</summary>
        private sealed class LinearLayout
        {
            /// <summary>The middle of the words.</summary>
            public GeoPoint2 At { get; set; }

            /// <summary>Where words past a leader start, level with their middle, which AutoCAD records as their point; null for others.</summary>
            public GeoPoint2? Start { get; set; }

            /// <summary>The angle the words are turned to, in radians.</summary>
            public double Angle { get; set; }

            public bool ArrowsInside { get; set; }

            /// <summary>Whether the arrowheads are left out, as DIMSOXD leaves them out where they do not fit.</summary>
            public bool ArrowsSuppressed { get; set; }

            /// <summary>The point of the line the words stand centred on, and half the length taken out of the line for them; nought for none.</summary>
            public GeoPoint2 Middle { get; set; }

            public double Break { get; set; }

            /// <summary>The extension line the words stand over (DIMJUST 3 or 4), 1 or 2; nought for none.</summary>
            public int OverExtension { get; set; }

            /// <summary>How much further that extension line runs, on under the words.</summary>
            public double Longer { get; set; }

            /// <summary>The lines outside the extension lines: those the arrowheads stand on, and the line run on to words there.</summary>
            public List<KeyValuePair<GeoPoint2, GeoPoint2>> Outside { get; } =
                new List<KeyValuePair<GeoPoint2, GeoPoint2>>();
        }

        /// <summary>
        /// Works out where the words and the arrowheads of a linear dimension stand, from the style's variables, as AutoCAD 2021
        /// draws them. DIMTAD puts the words on the line, breaking it by DIMGAP about them (0), above it (1, and JIS, 3), on the
        /// side away from the points measured (2) or below it (4). Words kept level by DIMTIH on a line that is not level stand
        /// on it for 0, 1 and 4, and off it, clear of it by DIMGAP, for 2 and 3. DIMJUST puts them in the middle (0), next to
        /// the first or the second extension line (1, 2), or over and along one of them (3, 4). Where the words and the
        /// arrowheads do not both fit between the extension lines, DIMTIX keeps the words inside (DIMSOXD then leaves out
        /// arrowheads that do not fit), else DIMATFIT moves both out (0), the arrowheads first (1), the words first (2), or (3)
        /// the words where the arrowheads fit, else the arrowheads; words outside stand past the second extension line (the
        /// first for DIMJUST 1), the line run on to them, and words kept level by DIMTOH stand on a level landing, or with none
        /// past the end of a line within 15 degrees of level. DIMTVP with
        /// DIMTAD 0 lifts the words by that many times their height, off the line as DIMTAD 1 puts them from 0.7 up; a DIMGAP
        /// below nought, the box round the words, keeps words above the line clear of it by the gap again. Words turned to an
        /// angle of their own (<paramref name="wordsAngle"/>, DIMLINEAR's Angle) stand centred on the line whatever DIMTAD says,
        /// the line broken where their turned box and the gap cross it: "100" 5.25 wide and 2.5 high turned 45 breaks it 2.65
        /// each side of its middle.
        /// </summary>
        /// <remarks>
        /// With arrowheads of 2.5 and DIMGAP 0.625, a dimension of 100 has its words "100", 5.3 wide, in the middle; of 5, its
        /// words "5" inside and its arrowheads outside on lines 2.5 long; of 7, under DIMATFIT 3, its arrowheads inside and its
        /// words 3.9 past the second extension line, as AutoCAD 2021 draws them. Words need their width and DIMGAP each side
        /// between the extension lines, on the line or above it; the arrowheads two lengths and two DIMGAPs, 6.25 (a dimension
        /// of 6.24 has them outside, of 6.26 inside); ticks, and the other arrowheads that take no room
        /// (<see cref="CadArrowheads.TakesNoRoom"/>), never move out.
        /// </remarks>
        private static LinearLayout LayOut(
            DxfDimStyleTableRecord style,
            GeoPoint2 p1,
            GeoPoint2 p2,
            GeoPoint2 q1,
            GeoPoint2 q2,
            GeoVector2 along,
            double angle,
            double width,
            double height,
            double gap,
            double arrow,
            double beyond,
            GeoPoint2? placed,
            double? wordsAngle
        )
        {
            var layout = new LinearLayout { Angle = angle };
            double length = (q2 - q1).Length;
            int tad = style.Dimtad;
            int just = style.Dimjust;
            bool box = style.Dimgap < 0.0;

            // DIMTVP with DIMTAD 0 lifts the words by that many times their height: from 0.7 up they stand off the line as
            // DIMTAD 1 puts them, below that on it, the line broken about them.
            double raised = tad == 0 ? style.Dimtvp * height : 0.0;

            if (tad == 0 && Math.Abs(style.Dimtvp) >= 0.7)
            {
                tad = 1;
                raised = 0.0;
            }

            bool ticks =
                CadArrowheads.TakesNoRoom(ArrowOf(style, DxfDimArrowFlag.FirstArrow))
                && CadArrowheads.TakesNoRoom(ArrowOf(style, DxfDimArrowFlag.SecondArrow));
            double inside = wordsAngle ?? (style.Dimtih ? 0.0 : angle);
            bool level = Parallel(inside, angle);

            // Words turned to an angle of their own stand centred on the line whatever DIMTAD says, the line broken about their
            // turned box, as AutoCAD 2021 draws them: turned 45 or 30, along the line or across it alike.
            bool centred = tad == 0 || wordsAngle.HasValue || (!level && (tad == 1 || tad == 4));
            double half = Chord(width, height, gap, inside - angle);
            double needText = centred ? 2.0 * half : width + 2.0 * gap;
            double needArrows = ticks ? 0.0 : 2.0 * (arrow + gap);
            double needBoth = centred ? needText + (ticks ? 0.0 : 2.0 * (arrow + gap)) : Math.Max(needText, needArrows);
            bool textInside;
            bool arrowsInside;

            if (placed.HasValue || just == 3 || just == 4)
            {
                textInside = true;
                arrowsInside = needArrows <= length;
            }
            else if (style.Dimtix || needBoth <= length)
            {
                textInside = true;
                arrowsInside = needBoth <= length;
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
                        textInside = needText <= length;
                        arrowsInside = false;
                        break;
                    case 2:
                        textInside = false;
                        arrowsInside = needArrows <= length;
                        break;
                    default:
                        arrowsInside = needArrows <= length && !(needText > needArrows && needText <= length);
                        textInside = !arrowsInside && needText <= length;
                        break;
                }
            }

            layout.ArrowsInside = arrowsInside;
            layout.ArrowsSuppressed = !arrowsInside && style.Dimtix && style.Dimsoxd;
            bool arrowsOut = !arrowsInside && !layout.ArrowsSuppressed && !ticks;
            var readable = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
            var above = new GeoVector2(-readable.Y, readable.X);
            var middle = new GeoPoint2((q1.X + q2.X) / 2.0, (q1.Y + q2.Y) / 2.0);

            // Words above the line stand clear of it by the gap, and a box round them (DIMGAP below nought) by the gap again.
            double lift = tad != style.Dimtad ? style.Dimtvp * height : gap + height / 2.0 + (box ? gap : 0.0);
            int outerSide = just == 1 ? 1 : 2;
            double outward2 = style.Dimtoh ? 0.0 : angle;

            // Words moved out by the fit go over the line where DIMTMOVE is 1 or 2, not beside it past an extension line, level
            // where DIMTOH keeps them so.
            bool over = !textInside && !placed.HasValue && style.Dimtmove != 0;

            for (int end = 1; end <= 2 && arrowsOut; end++)
            {
                // The short line an arrowhead outside stands on, an arrowhead long; on the side of words outside the line run on
                // to them is drawn instead.
                if (textInside || over || end != outerSide)
                {
                    GeoPoint2 tip = end == 1 ? q1 : q2;
                    GeoVector2 outward = end == 1 ? -along : along;
                    layout.Outside.Add(
                        new KeyValuePair<GeoPoint2, GeoPoint2>(tip + outward * arrow, tip + outward * (2.0 * arrow))
                    );
                }
            }

            // Off the line words stand above it by the gap and half their height, or as DIMTAD puts them; on it (DIMTAD 0) only by
            // DIMTVP.
            double offLine = tad == 0 ? raised : Side(tad, above, p1, p2, middle) * lift;

            if (placed.HasValue)
            {
                // Words put between the extension lines under DIMTIH stand level on the line, which is broken a gap clear of
                // them, as AutoCAD 2021 draws them after DIMTEDIT Left and Right on a slanted line.
                double put = (placed.Value - q1).X * along.X + (placed.Value - q1).Y * along.Y;

                if (style.Dimtmove == 0 && !level && put >= -1E-9 && put <= length + 1E-9)
                {
                    layout.Angle = inside;
                    layout.At = q1 + along * put;
                    layout.Middle = layout.At;
                    layout.Break = half;
                    return layout;
                }

                PlaceMoved(
                    layout,
                    style.Dimtmove,
                    placed.Value,
                    q1,
                    q2,
                    along,
                    readable,
                    above,
                    middle,
                    width,
                    height,
                    gap,
                    arrow,
                    tad == 0,
                    offLine,
                    style.Dimtxtdirection
                );
                return layout;
            }

            if (over)
            {
                // Over the line, on the side away from the points: DIMEXE, one and a half DIMGAP and half the words' height
                // out, their height more where they stand below it as they read, no nearer than an arrowhead. DIMTMOVE 2 puts the
                // words' middle there; 1 a leader's end, a third as far along again, then a landing on to the words (two gaps
                // long, the words past a gap, on it for DIMTAD 0; under them, as wide as they are and a gap each side). Words
                // DIMTOH keeps level stand DIMEXE, half DIMGAP and their height out, square off the middle of the line, and under
                // 1 the leader runs square to the line and the landing level, on away from the line. AutoCAD 2021 draws the line
                // between the extension lines again for the leader where the arrowheads stand outside.
                var points = new GeoPoint2((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0);
                double side = (middle - points).X * above.X + (middle - points).Y * above.Y < 0.0 ? -1.0 : 1.0;
                bool levelled = style.Dimtoh;
                double clearance = levelled ? beyond + gap / 2.0 + height : beyond + 1.5 * gap + height / 2.0;
                double off = Math.Max(arrow, side > 0.0 ? clearance : clearance + height);
                layout.Angle = outward2;

                if (style.Dimtmove == 2)
                {
                    layout.At = middle + above * (side * off);
                    return layout;
                }

                GeoPoint2 knee = middle + readable * (levelled ? 0.0 : 0.3 * off) + above * (side * off);
                GeoVector2 landing = readable;
                GeoVector2 upward = above;

                if (!Parallel(outward2, angle))
                {
                    landing = new GeoVector2(knee.X < middle.X - 1E-9 ? -1.0 : 1.0, 0.0);
                    upward = new GeoVector2(0.0, 1.0);
                    offLine = tad == 0 ? raised : Side(tad, upward, p1, p2, middle) * lift;
                }

                bool onLanding = tad == 0;
                double start2 = onLanding ? 3.0 * gap : gap;

                // No further out than an arrowhead and a gap, the words stand centred where they would start, with no leader.
                if (off <= arrow + gap)
                {
                    layout.At = knee + landing * start2 + upward * offLine;
                    return layout;
                }

                layout.Outside.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(middle, knee));
                layout.Outside.Add(
                    new KeyValuePair<GeoPoint2, GeoPoint2>(
                        knee,
                        knee + landing * (onLanding ? 2.0 * gap : width + 2.0 * gap)
                    )
                );
                layout.At = knee + landing * (start2 + width / 2.0) + upward * offLine;
                layout.Start = knee + landing * start2 + upward * offLine;

                if (arrowsOut)
                {
                    layout.Outside.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(q1, q2));
                }

                return layout;
            }

            if (just == 3 || just == 4)
            {
                // Over an extension line: along it, past its end by DIMEXE, DIMGAP and half the words; beside it on the side
                // above them, the extension line run on under them, or with DIMTAD 0 on it.
                GeoPoint2 foot = just == 3 ? q1 : q2;
                GeoVector2 up = foot - (just == 3 ? p1 : p2);
                GeoVector2 unit = up.Length > 1E-12 ? up / up.Length : new GeoVector2(-along.Y, along.X);
                double turned = Math.Atan2(unit.Y, unit.X);

                if (turned > Math.PI / 2.0 + 1E-9 || turned <= -Math.PI / 2.0 + 1E-9)
                {
                    turned += Math.PI;
                }

                var beside = new GeoVector2(-Math.Sin(turned), Math.Cos(turned));
                layout.OverExtension = just - 2;
                layout.Longer = tad == 0 ? 0.0 : width + 2.0 * gap;
                layout.Angle = turned;
                layout.At =
                    foot
                    + unit * (beyond + gap + width / 2.0)
                    + beside
                        * (
                            tad == 0 ? 0.0
                            : tad == 4 ? -lift
                            : lift
                        );
                return layout;
            }

            if (textInside)
            {
                // Next to an extension line, an arrowhead and twice DIMGAP in from it, where the words and that much each side
                // fit; else in the middle.
                double from = (centred ? half : width / 2.0 + gap) + gap + (ticks ? 0.0 : arrow);
                GeoPoint2 on =
                    just == 1 && 2.0 * from <= length ? q1 + along * from
                    : just == 2 && 2.0 * from <= length ? q2 - along * from
                    : middle;
                layout.Angle = inside;

                if (centred)
                {
                    layout.At = level ? on + above * raised : on;

                    // Words forced inside that do not fit leave the line whole.
                    if (needText <= length)
                    {
                        layout.Middle = on;
                        layout.Break = half;
                    }
                }
                else
                {
                    // Level words off a slanting line stand clear of it by DIMGAP, their box square to it.
                    double off = level
                        ? lift
                        : (width / 2.0 + gap) * Math.Abs(Math.Sin(inside - angle))
                            + (height / 2.0 + gap) * Math.Abs(Math.Cos(inside - angle));
                    layout.At = on + above * (Side(tad, above, p1, p2, middle) * off);
                }

                return layout;
            }

            // Outside, past the extension line on the words' side: the line runs on under words above it, or up to words on it;
            // words kept level off a slanting line stand on a level landing from the end of the line run on.
            GeoPoint2 start = outerSide == 1 ? q1 : q2;
            GeoVector2 away = outerSide == 1 ? -along : along;
            double clear =
                arrowsOut ? 2.0 * arrow
                : ticks ? 0.0
                : arrow;
            layout.Angle = outward2;

            if (!Parallel(outward2, angle))
            {
                GeoPoint2 bend = start + away * clear;
                // On an upright line the landing runs the way the points run: left where they run down, right where they run up,
                // on either side of it, as AutoCAD 2021 lands it.
                var flat = new GeoVector2(
                    away.X < -1E-9 ? -1.0
                        : away.X > 1E-9 ? 1.0
                        : away.Y < 0.0 ? -1.0
                        : 1.0,
                    0.0
                );
                var up2 = new GeoVector2(0.0, 1.0);

                // A line within 15 degrees of level has no landing: the words stand a gap past its end, level with it, as
                // AutoCAD 2021 draws them at 5, 10 and 15 degrees and from 165 to 195; from 15.01 degrees on they stand past an
                // arrowhead's landing (DIMTAD 0) or over a landing as wide as they are.
                bool landed = Math.Abs(away.Y) > Math.Sin(Math.PI / 12.0) + 1E-12;
                double landing =
                    !landed ? 0.0
                    : tad == 0 ? arrow
                    : width + gap + (box ? gap : 0.0);

                if (clear > (arrowsOut ? arrow : 0.0))
                {
                    layout.Outside.Add(
                        new KeyValuePair<GeoPoint2, GeoPoint2>(start + away * (arrowsOut ? arrow : 0.0), bend)
                    );
                }

                if (landed)
                {
                    layout.Outside.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(bend, bend + flat * landing));
                }

                layout.At =
                    tad == 0
                        ? bend + flat * (landing + gap + width / 2.0)
                        : bend + flat * (gap + width / 2.0) + up2 * (Side(tad, up2, p1, p2, middle) * lift);
                return layout;
            }

            bool onLine = tad == 0;
            double centre = clear + width / 2.0 + gap;
            double reach = onLine ? clear : centre + width / 2.0 + gap;
            layout.At =
                start + away * centre + (onLine ? above * raised : above * (Side(tad, above, p1, p2, middle) * lift));
            layout.Outside.Add(
                new KeyValuePair<GeoPoint2, GeoPoint2>(start + away * (arrowsOut ? arrow : 0.0), start + away * reach)
            );
            return layout;
        }

        /// <summary>
        /// Where the words of a linear dimension moved by DIMTEDIT stand, about their text's point, as AutoCAD 2021 draws them.
        /// Under DIMTMOVE 0 the dimension line runs through the point and the words stand there as in the middle: above the line
        /// by the gap and half their height, or on it for DIMTAD 0, the line broken a gap clear of them; past an end the line
        /// runs on under them to a gap beyond them, or up to a gap short of words on it. Under 2 the words' middle is the point,
        /// the line broken where it runs through them. Under 1 they stand there too (above it, but for DIMTAD 0) until the point
        /// is further from the line than DIMGAP and half the heights of the words and the arrowheads; then a leader runs from
        /// the middle of the line to a gap short of the point, three for DIMTAD 0, and a landing on away from the middle, under
        /// words above it (as wide as they are and a gap each side) or two gaps up to words on it, the words starting at the
        /// point. "100" moved 10 above the line 10 left of its middle has a leader to 0.625 short of the point and a landing
        /// 6.53 long, the words above it. Words read from right to left (<paramref name="backward"/>, DIMTXTDIRECTION) stand on
        /// the other side of the point there, as AutoCAD 2021 puts them.
        /// </summary>
        private static void PlaceMoved(
            LinearLayout layout,
            int move,
            GeoPoint2 at,
            GeoPoint2 q1,
            GeoPoint2 q2,
            GeoVector2 along,
            GeoVector2 readable,
            GeoVector2 above,
            GeoPoint2 middle,
            double width,
            double height,
            double gap,
            double arrow,
            bool onLine,
            double offLine,
            bool backward
        )
        {
            double t = (at - q1).X * along.X + (at - q1).Y * along.Y;
            GeoPoint2 foot = q1 + along * t;
            double across = (at - foot).X * above.X + (at - foot).Y * above.Y;
            double half = width / 2.0 + gap;
            bool crossing = Math.Abs(across) < height / 2.0 + gap;

            if (move == 0)
            {
                layout.At = foot + above * offLine;
                double length = (q2 - q1).Length;
                double reach = onLine ? -half : half;

                if (onLine)
                {
                    layout.Middle = foot;
                    layout.Break = half;
                }

                if (t > length && t + reach > length)
                {
                    layout.Outside.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(q2, q1 + along * (t + reach)));
                }
                else if (t < 0.0 && t - reach < 0.0)
                {
                    layout.Outside.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(q1 + along * (t - reach), q1));
                }

                return;
            }

            if (move == 1 && Math.Abs(across) > gap + (height + arrow) / 2.0)
            {
                GeoVector2 away =
                    (at - middle).X * readable.X + (at - middle).Y * readable.Y >= 0.0 ? readable : -readable;
                GeoPoint2 knee = at - away * (onLine ? 3.0 * gap : gap);
                layout.Outside.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(middle, knee));
                layout.Outside.Add(
                    new KeyValuePair<GeoPoint2, GeoPoint2>(knee, knee + away * (onLine ? 2.0 * gap : width + 2.0 * gap))
                );
                // Words read from right to left (DIMTXTDIRECTION) stand on the other side of the point, as AutoCAD 2021 puts them.
                layout.At = at + (backward ? -away : away) * (width / 2.0) + above * offLine;
                return;
            }

            layout.At = move == 1 ? at + above * offLine : at;

            // Words put past the end of the line, on its way, under DIMTMOVE 1: the line runs on from the end to them, to the near
            // edge of words on it, and under words off it to their far edge, a gap past each, as AutoCAD 2021 draws it: "100" 5
            // wide put 20 past the end of a line 100 long has its line run on from 100 to 116.875 (on it), 123.125 (above it).
            if (move == 1)
            {
                double length = (q2 - q1).Length;
                double reachEnd = onLine ? -half : half;

                if (t > length && t + reachEnd > length)
                {
                    layout.Outside.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(q2, q1 + along * (t + reachEnd)));
                }
                else if (t < 0.0 && t - reachEnd < 0.0)
                {
                    layout.Outside.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(q1 + along * (t - reachEnd), q1));
                }
            }

            if (crossing && (move == 2 || onLine))
            {
                layout.Middle = foot;
                layout.Break = half;
            }
        }

        /// <summary>Whether words turned to one angle run along a line at another, either way.</summary>
        private static bool Parallel(double words, double line) => Math.Abs(Math.Sin(words - line)) <= 1E-9;

        /// <summary>
        /// Half the length of a line through the middle of words, at an angle to them, that lies within their box grown by the
        /// gap: half their width and the gap for a line along them.
        /// </summary>
        private static double Chord(double width, double height, double gap, double turn)
        {
            double along = Math.Abs(Math.Cos(turn));
            double across = Math.Abs(Math.Sin(turn));
            double a = along > 1E-9 ? (width / 2.0 + gap) / along : double.MaxValue;
            double b = across > 1E-9 ? (height / 2.0 + gap) / across : double.MaxValue;
            return Math.Min(a, b);
        }

        /// <summary>
        /// The side of the dimension line words off it stand on, toward <paramref name="above"/> (1) or away from it (-1): above
        /// for DIMTAD 1 and JIS (3), below for 4, and for 2 on the side away from the points measured.
        /// </summary>
        private static double Side(int tad, GeoVector2 above, GeoPoint2 p1, GeoPoint2 p2, GeoPoint2 middle)
        {
            switch (tad)
            {
                case 2:
                    var measured = new GeoPoint2((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0);
                    return (middle - measured).X * above.X + (middle - measured).Y * above.Y < 0.0 ? -1.0 : 1.0;
                case 4:
                    return -1.0;
                default:
                    return 1.0;
            }
        }

        /// <summary>Takes the length of the line within <paramref name="half"/> of a point on it out of segments along it.</summary>
        private static List<KeyValuePair<GeoPoint2, GeoPoint2>> Broken(
            List<KeyValuePair<GeoPoint2, GeoPoint2>> segments,
            GeoPoint2 at,
            GeoVector2 along,
            double half
        )
        {
            var kept = new List<KeyValuePair<GeoPoint2, GeoPoint2>>();

            foreach (KeyValuePair<GeoPoint2, GeoPoint2> segment in segments)
            {
                double s = (segment.Key - at).X * along.X + (segment.Key - at).Y * along.Y;
                double e = (segment.Value - at).X * along.X + (segment.Value - at).Y * along.Y;
                double low = Math.Min(s, e);
                double high = Math.Max(s, e);

                if (high <= -half || low >= half)
                {
                    kept.Add(segment);
                    continue;
                }

                GeoPoint2 lowPoint = s <= e ? segment.Key : segment.Value;
                GeoPoint2 highPoint = s <= e ? segment.Value : segment.Key;

                if (low < -half)
                {
                    kept.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(lowPoint, at - along * half));
                }

                if (high > half)
                {
                    kept.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(at + along * half, highPoint));
                }
            }

            return kept;
        }

        /// <summary>The sizes of a picture: the style's times the scale of <paramref name="dimscale"/>, a DIMSCALE of nought or less drawn as 1.</summary>
        private static void Sizes(
            DxfDimStyleTableRecord style,
            double dimscale,
            double textHeight,
            out double scale,
            out double arrow,
            out double height,
            out double gap,
            out double offset,
            out double beyond
        )
        {
            scale = dimscale > 0.0 ? dimscale : 1.0;
            arrow = style.Dimasz * scale;
            height = textHeight * scale;
            gap = Math.Abs(style.Dimgap) * scale;
            offset = style.Dimexo * scale;
            beyond = style.Dimexe * scale;
        }

        /// <summary>
        /// Where an extension line starts, as a distance along it from the point it is drawn from, the dimension line or arc
        /// <paramref name="length"/> away: DIMEXO off the point; under DIMFXLON, DIMFXL by the scale short of the dimension line,
        /// though no nearer the point than DIMEXO, nor than the dimension line where that is nearer still, as AutoCAD 2021 draws
        /// it. Points 50 below the line, DIMFXL 10 and DIMEXO 0.625 start at 40; DIMFXL 49.5 at 0.625; DIMFXL 49 at 1.
        /// </summary>
        private static double ExtensionStart(
            DxfDimStyleTableRecord style,
            double scale,
            double offset,
            double length
        ) => style.DimfxlenOn ? Math.Max(length - style.Dimfxlen * scale, Math.Min(offset, length)) : offset;

        /// <summary>A closed filled arrowhead with its tip at a point, pointing a way, a third as wide as long, as AutoCAD's.</summary>
        private static DxfEntity Arrowhead(GeoPoint2 tip, GeoVector2 way, double size)
        {
            GeoPoint2 back = tip - way * size;
            var side = new GeoVector2(-way.Y, way.X) * (size / 6.0);
            return Picture(new DxfSolid(P3(tip), P3(back + side), P3(back - side)));
        }

        /// <summary>
        /// The arrowhead of one end as the style draws it (DIMBLK, or DIMBLK1 and DIMBLK2 where DIMSAH parts them): its shape
        /// drawn into the picture (<see cref="CadArrowheads"/>), as AutoCAD's block of it would draw; closed filled where the
        /// style names none, or a block of its own the presenter does not draw.
        /// </summary>
        private static void AddArrow(
            List<DxfEntity> picture,
            DxfDimStyleTableRecord style,
            DxfDimArrowFlag which,
            GeoPoint2 tip,
            GeoVector2 way,
            double size
        )
        {
            string name = ArrowOf(style, which);

            if (name.Length == 0)
            {
                picture.Add(Arrowhead(tip, way, size));
                return;
            }

            foreach (DxfEntity part in CadArrowheads.Shape(name, tip, way, size))
            {
                picture.Add(Lined(part, style.Dimclrd, style.Dimlwd));
            }
        }

        /// <summary>The block name of an arrowhead of a style, upper case: empty for closed filled and for one that cannot be read.</summary>
        private static string ArrowOf(DxfDimStyleTableRecord style, DxfDimArrowFlag which)
        {
            try
            {
                return (style.GetArrow(which)?.Name ?? string.Empty).ToUpperInvariant();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// The dimension line between its two ends: whole, or the half of each end DIMSD1 or DIMSD2 leaves; run on past an end
        /// by DIMDLE where that end is a tick, as AutoCAD runs it. Where the arrowheads stand inside, the line starts at the back
        /// of each, <paramref name="arrow"/> in from its tip, but for one that takes no room (<see cref="CadArrowheads.TakesNoRoom"/>).
        /// </summary>
        private static void DimensionLine(
            List<KeyValuePair<GeoPoint2, GeoPoint2>> segments,
            DxfDimStyleTableRecord style,
            GeoPoint2 q1,
            GeoPoint2 q2,
            GeoVector2 along,
            double scale,
            double arrow
        )
        {
            double dle = Math.Max(0.0, style.Dimdle) * scale;
            string firstName = ArrowOf(style, DxfDimArrowFlag.FirstArrow);
            string secondName = ArrowOf(style, DxfDimArrowFlag.SecondArrow);
            GeoPoint2 a =
                CadArrowheads.IsTick(firstName) && dle > 0.0 ? q1 - along * dle
                : CadArrowheads.TakesNoRoom(firstName) ? q1
                : q1 + along * arrow;
            GeoPoint2 b =
                CadArrowheads.IsTick(secondName) && dle > 0.0 ? q2 + along * dle
                : CadArrowheads.TakesNoRoom(secondName) ? q2
                : q2 - along * arrow;
            bool first = !style.Dimsd1;
            bool second = !style.Dimsd2;
            var middle = new GeoPoint2((q1.X + q2.X) / 2.0, (q1.Y + q2.Y) / 2.0);

            if (first && second)
            {
                segments.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(a, b));
            }
            else if (first)
            {
                segments.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(a, middle));
            }
            else if (second)
            {
                segments.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(middle, b));
            }
        }

        /// <summary>
        /// Gives a line of the picture the colour and the lineweight of the style (DIMCLRD, DIMLWD; DIMCLRE, DIMLWE for the
        /// extension lines) where the style sets one other than ByBlock, the default it is drawn with otherwise.
        /// </summary>
        private static T Lined<T>(T entity, DxfColor color, DxfLineWeight weight)
            where T : DxfEntity
        {
            if (color != null && !color.IsByBlock)
            {
                entity.Color = color;
            }

            if (weight != DxfLineWeight.ByBlock)
            {
                entity.LineWeight = weight;
            }

            return entity;
        }

        /// <summary>The words of the dimension: an MTEXT in the style's text style (DIMTXSTY) and text colour (DIMCLRT), centred on its point.</summary>
        private static DxfEntity Words(
            DxfDimStyleTableRecord style,
            GeoPoint2 at,
            double angle,
            double height,
            string words,
            double scale
        )
        {
            var text = new DxfMText
            {
                Location = P3(at),
                TextHeight = height > 0.0 ? height : 2.5 * scale,
                Attachment = DxfAttachmentPoint.MiddleCenter,
                Contents = words,
            };
            text.Rotation = angle < 0.0 ? angle + 2.0 * Math.PI : angle;
            string textStyle = null;

            try
            {
                textStyle = style.Dimtxsty?.Name;
            }
            catch (Exception)
            {
                textStyle = null;
            }

            if (
                !string.IsNullOrEmpty(textStyle)
                && !string.Equals(textStyle, text.TextStyleName, StringComparison.OrdinalIgnoreCase)
            )
            {
                text.TextStyleName = textStyle;
            }

            Picture(text);
            DxfColor color = style.Dimclrt;

            if (color != null && !color.IsByBlock)
            {
                text.Color = color;
            }

            return text;
        }

        /// <summary>
        /// Dresses the words of a picture as the dimension's variables say, as AutoCAD 2021 writes them into its block: DIMTFILL's
        /// mask behind them, the MTEXT's background 1.25 times their height, of the drawing's background for 1 (written as the
        /// index 9 and 200,200,200, as AutoCAD writes it) and of DIMTFILLCLR for 2 (by layer where it is by layer or by block);
        /// and under DIMTXTDIRECTION the words turned half round, as a dimension reading from right to left has them, a level
        /// one's at 180 degrees and an upright one's at 270, where they stand unchanged.
        /// </summary>
        internal static void Dressed(DxfDimStyleTableRecord style, List<DxfEntity> picture)
        {
            int fill = style.Dimtfill;
            DxfColor colour = fill == 2 ? style.Dimtfillclr : null;
            bool backward = style.Dimtxtdirection;

            foreach (DxfEntity entity in picture)
            {
                if (!(entity is DxfMText words))
                {
                    continue;
                }

                if (backward)
                {
                    words.Rotation = Turned(words.Rotation + Math.PI);
                }

                if (fill != 1 && fill != 2)
                {
                    continue;
                }

                words.BackgroundFill = true;
                words.BackgroundScaleFactor = 1.25;

                if (fill == 1)
                {
                    words.UseBackgroundColor = true;
                    words.BackgroundFillColor = DxfColor.OfRgb(0xC8C8C8, null, 9);
                }
                else if (colour != null && colour.IsByAci)
                {
                    words.BackgroundFillColor = colour;
                }
            }
        }

        /// <summary>
        /// Writes into a dimension the middle of its words (group 11), where they stand at their default place, and its
        /// measurement (42), as AutoCAD 2021 writes them with the block it draws: a DIMLINEAR of 0,0 to 100,0 with its line 20
        /// above and DIMTAD 0 has 50,20 and 100. Words of a linear dimension the fit moves past a leader under DIMTMOVE 1 are
        /// written by their start instead, level with their middle: the "0.5000" of a continued dimension of 0.5 in acad.dwt's
        /// Standard, its middle at (100.8933, 20.4088), writes (100.52, 20.4088). A dimension whose words were put somewhere
        /// keeps their point.
        /// </summary>
        private static void Recorded(DxfDimension dimension, List<DxfEntity> picture)
        {
            if (dimension.UsingDefaultTextPosition)
            {
                GeoPoint3? recorded = RecordedPoint(picture);

                if (recorded.HasValue)
                {
                    dimension.TextPosition = recorded.Value;
                    dimension.UsingDefaultTextPosition = true;
                }
            }

            double measured = dimension.Measurement;

            if (!double.IsNaN(measured) && !double.IsInfinity(measured))
            {
                dimension.WriteMeasurement(measured);
            }
        }

        /// <summary>The point a picture's words are recorded by (group 11): their start where one was kept, else their middle; null with no words.</summary>
        private static GeoPoint3? RecordedPoint(List<DxfEntity> picture)
        {
            foreach (DxfEntity entity in picture)
            {
                if (entity is DxfMText words)
                {
                    return WordsStarts.TryGetValue(words, out StrongBox<GeoPoint3> start)
                        ? start.Value
                        : words.Location;
                }
            }

            return null;
        }

        private static DxfEntity Definition(GeoPoint2 at) => new DxfDBPoint(P3(at)) { Layer = "Defpoints" };

        private static T Picture<T>(T entity)
            where T : DxfEntity
        {
            entity.Color = DxfColor.ByBlock;
            return entity;
        }

        /// <summary>
        /// The height of the text of a dimension to be made, before the style's scale: the host's default where it gives one
        /// ("dim.text.height", as Global settings' text height), else the style's DIMTXT.
        /// </summary>
        private static double TextHeight(CadCommandContext c, DxfDimStyleTableRecord style) =>
            c.Remembered("dim.text.height", style.Dimtxt);

        /// <summary>
        /// The DIMSCALE a dimension to be made is drawn by, as AutoCAD 2021 takes it: the drawing's current one, its header's
        /// <c>$DIMSCALE</c>, and not its style's, where it is above nought; with nought, one over the scale of the viewport
        /// model space is worked through (2 through one of 1:2), else 1, as a leader takes it. <c>Place</c> writes it as an
        /// override where it is not the style's. DIMSCALE 100 typed with the style's 1 draws arrowheads of 2.5 as 250.
        /// </summary>
        private static double Dimscale(CadCommandContext c) => LeaderScale(c);

        /// <summary>
        /// The DIMSCALE a dimension drawn by <paramref name="style"/> is drawn by: the style's, where it is above nought, as
        /// <see cref="DimStyle"/> gives the current one and a row its base's (DIMCONTINUE from a dimension of DIMSCALE 2 draws
        /// at 2 whatever is current); else the current one (<see cref="Dimscale"/>).
        /// </summary>
        private static double DrawnScale(CadCommandContext c, DxfDimStyleTableRecord style) =>
            style.Dimscale > 0.0 ? style.Dimscale : Dimscale(c);

        /// <summary>
        /// Adds a dimension with its picture to the space: the picture appended to an anonymous block *D, which the block
        /// table names as AutoCAD names its own (*D1, *D2), and given to the dimension as its block, and each variable it is
        /// drawn by (<paramref name="style"/>, the text height given) written as an override where it is not its style's;
        /// the variable of the group <paramref name="first"/> written before the rest, and that of <paramref name="always"/>
        /// written whatever the style has, as AutoCAD 2021's DIMCONTINUE writes DIMTMOVE and DIMJOGGED DIMJOGANG. The dimension
        /// goes on the current layer, or DIM's LAyer; on <paramref name="onLayer"/> where one is given, as a row goes on its
        /// base's.
        /// </summary>
        private static CadEditResult Place(
            CadCommandContext c,
            DxfDimStyleTableRecord style,
            double textHeight,
            DxfDimension dimension,
            List<DxfEntity> picture,
            int first = 0,
            int always = 0,
            string onLayer = null
        )
        {
            CadSpace space = c.Space;
            string layer = onLayer ?? c.Session.DimLayer;
            double scale = DrawnScale(c, style);
            Dressed(style, picture);
            return c.Apply(null, s =>
            {
                var block = new DxfBlockTableRecord { Name = "*D" };
                s.Database.BlockTable.Add(block);

                foreach (DxfEntity entity in picture)
                {
                    if (entity.Layer == null || entity.Layer.Length == 0)
                    {
                        entity.Layer = "0";
                    }

                    block.AppendEntity(entity);
                }

                DxfDimStyleTableRecord named = s.Database.DimStyleTable[style.Name];
                dimension.DimensionStyle = named;
                dimension.DimBlock = block;
                dimension.Layer = s.Database.Clayer;
                space.Block.AppendEntity(dimension);
                dimension.SetDatabaseDefaults(s.Database);

                // DIM's LAyer (AutoCAD's DIMLAYER), or a row's base, puts it on a layer of its own where that layer is still in
                // the drawing.
                if (!string.IsNullOrEmpty(layer) && s.Database.LayerTable.Has(layer))
                {
                    dimension.Layer = layer;
                }

                // Each variable the picture is drawn by other than the style's is an override, as AutoCAD 2021 writes them, so
                // that a CAD drawing the dimension again from its variables draws it as its picture has it.
                Override(dimension, named, style, scale, textHeight, first, always);
                Recorded(dimension, picture);
                s.Added(space, dimension);
            });
        }

        /// <summary>
        /// Writes every variable a new dimension is drawn by where it is not its style's, as AutoCAD 2021 writes them, in the
        /// order of their groups as a leader's (see <see cref="VariablesByGroup"/>): DIMSCALE (40) as the scale it is drawn by,
        /// DIMASZ (41), DIMEXO (42), DIMTAD (77), DIMTXT (140) as the text height given, DIMGAP (147), DIMCLRD (176), DIMCLRT
        /// (178), DIMDEC (271), DIMBLK (342, <c>1005</c> and the handle of <c>_Dot</c>), DIMLWD (371) and each other. A variable
        /// the drawing's version cannot hold is left out, as SAVEAS leaves it. The variable of the group <paramref name="first"/>
        /// goes before the rest (DIMCONTINUE's DIMTMOVE: <c>279 41 176</c>), and that of <paramref name="always"/> is written
        /// even where it is the style's (DIMJOGGED's DIMJOGANG, 50).
        /// </summary>
        private static void Override(
            DxfDimension dimension,
            DxfDimStyleTableRecord style,
            DxfDimStyleTableRecord drawn,
            double scale,
            double textHeight,
            int first,
            int always
        )
        {
            var order = new List<DxfDimVariable>(VariablesByGroup);
            int lead = first == 0 ? -1 : order.FindIndex(v => v.Code == first);

            if (lead > 0)
            {
                DxfDimVariable moved = order[lead];
                order.RemoveAt(lead);
                order.Insert(0, moved);
            }

            foreach (DxfDimVariable variable in order)
            {
                object wanted =
                    variable.Code == 40 ? scale
                    : variable.Code == 140 ? textHeight
                    : drawn.Get(variable);

                if (variable.Code == always)
                {
                    try
                    {
                        dimension.WriteOverride(variable, wanted);
                    }
                    catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
                    {
                        GeometryHelperLog.Debug(
                            "DIM: the override of " + variable.Name.ToUpperInvariant() + " is left out: " + e.Message
                        );
                    }

                    continue;
                }

                if (
                    wanted == null && variable.Kind == DxfDimKind.TextStyle
                    || variable.SameValue(wanted, style.Get(variable))
                )
                {
                    continue;
                }

                // One variable at a time, so that each is written last, in the order of the groups.
                DxfDimStyleTableRecord one = dimension.GetDimstyleData();
                one.Keep(variable, wanted);

                try
                {
                    dimension.SetDimstyleData(one);
                }
                catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
                {
                    GeometryHelperLog.Debug(
                        "DIM: the override of " + variable.Name.ToUpperInvariant() + " is left out: " + e.Message
                    );
                }
            }
        }
    }
}
