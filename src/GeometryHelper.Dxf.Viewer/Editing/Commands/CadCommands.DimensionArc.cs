using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// DIMARC, the arc length dimension, and its picture, as AutoCAD 2021 makes and draws them: the length of an arc, or of a
    /// part of it, along an arc of its own with the arc symbol of DIMARCSYM, stored as the library's
    /// <see cref="DxfArcDimension"/>.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>The angle of sweep from which the extension lines of an arc length dimension run from the centre, not along the middle of the arc.</summary>
        private const double RadialFrom = Math.PI / 2.0 - 1E-9;

        /// <summary>
        /// DIMARC: an arc, then where the dimension goes, with Mtext and Text for its words, Angle for theirs, Partial for the
        /// length between two points of the arc, and Leader (No leader) for a leader from the words to the arc, offered where the
        /// arc sweeps more than 90 degrees. A point in the sector the arc does not sweep dimensions the rest of its circle.
        /// </summary>
        private static IEnumerable<CadPrompt> DimArc(CadCommandContext c)
        {
            DxfDimStyleTableRecord style = DimStyle(c);

            if (style == null)
            {
                c.Write("The drawing has no dimension style to draw in.");
                yield break;
            }

            yield return CadPrompt.ForEntity("Select arc or polyline arc segment");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            if (
                !(c.Document.FindEntity(c.Input.Handles[0]) is DxfArc arc)
                || !(arc.Radius > c.Session.Tolerance.EqualPoint)
            )
            {
                c.Write("Object selected is not an arc or polyline arc segment.");
                yield break;
            }

            var center = new GeoPoint2(arc.Center.X, arc.Center.Y);
            double radius = arc.Radius;
            double start = arc.StartAngle;
            double sweep = Turned(arc.EndAngle - arc.StartAngle);
            sweep = sweep <= 1E-12 ? 2.0 * Math.PI : sweep;
            string text = null;
            double? textAngle = null;
            bool leader = false;
            bool partial = false;

            while (true)
            {
                var keywords = new List<string> { "Mtext", "Text", "Angle", "Partial" };

                if (sweep > Math.PI / 2.0 + 1E-9)
                {
                    keywords.Add(leader ? "No leader" : "Leader");
                }

                yield return CadPrompt.ForPoint("Specify arc length dimension location, or", center, keywords);

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

                if (c.Input.Is("Leader") || c.Input.Is("No leader"))
                {
                    leader = c.Input.Is("Leader");
                    continue;
                }

                if (c.Input.Is("Partial"))
                {
                    yield return CadPrompt.ForPoint("Specify first point for arc length dimension");

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    GeoPoint2 first = c.Input.Point;
                    yield return CadPrompt.ForPoint("Specify second point for arc length dimension", first);

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    // The part between the two points, each taken to the arc square to it, the way the arc runs.
                    double a = Turned(Math.Atan2(first.Y - center.Y, first.X - center.X) - start);
                    double b = Turned(Math.Atan2(c.Input.Point.Y - center.Y, c.Input.Point.X - center.X) - start);

                    if (Math.Abs(a - b) <= 1E-12)
                    {
                        c.Write("*Invalid* The two points are one.");
                        yield break;
                    }

                    start += Math.Min(a, b);
                    sweep = Math.Abs(a - b);
                    partial = true;
                    continue;
                }

                break;
            }

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            MakeArcLength(c, style, center, radius, start, sweep, c.Input.Point, text, textAngle, partial, leader);
        }

        /// <summary>
        /// Makes an arc length dimension of an arc about a centre from an angle through a sweep, its own arc through a point; its
        /// words given or the measurement, turned where <paramref name="textAngle"/> says. A point outside the sector of the
        /// sweep dimensions the rest of the circle, as AutoCAD 2021 does.
        /// </summary>
        private static void MakeArcLength(
            CadCommandContext c,
            DxfDimStyleTableRecord style,
            GeoPoint2 center,
            double radius,
            double start,
            double sweep,
            GeoPoint2 location,
            string text,
            double? textAngle,
            bool partial,
            bool leader
        )
        {
            double toward = Turned(Math.Atan2(location.Y - center.Y, location.X - center.X) - start);

            if (toward > sweep && sweep < 2.0 * Math.PI)
            {
                start += sweep;
                sweep = 2.0 * Math.PI - sweep;
            }

            double distance = Math.Max((location - center).Length, 1E-9);
            ArcLengthPlace(center, radius, start, sweep, distance, out GeoPoint2 arcPoint);
            GeoPoint2 first = center + Heading(start) * radius;
            GeoPoint2 second = center + Heading(start + sweep) * radius;

            // As AutoCAD 2021 writes one: the angles of the ends as its parameters (40, 41) and its ends as the leader's points
            // (16, 17), whether partial or with a leader or not.
            var made = new DxfArcDimension(P3(center), P3(first), P3(second), P3(arcPoint), text ?? string.Empty, null)
            {
                IsPartial = partial,
                ArcStartParam = Turned(start),
                ArcEndParam = Turned(start + sweep),
                Leader1Point = P3(first),
                Leader2Point = P3(second),
            };

            string words = Words(style, made, radius * sweep, text, string.Empty);
            c.Write("Dimension text = " + words);
            double textHeight = TextHeight(c, style);
            List<DxfEntity> picture = ArcLength(
                style,
                Dimscale(c),
                textHeight,
                center,
                radius,
                start,
                sweep,
                distance,
                words,
                style.Dimarcsym,
                textAngle,
                leader,
                out bool led
            );

            if (picture == null)
            {
                c.Write("*Invalid* The arc has no length there.");
                return;
            }

            made.HasLeader = led;

            // The text's point where the words stand, as AutoCAD writes it, the words not taken as moved by hand.
            DxfMText placedWords = picture.OfType<DxfMText>().FirstOrDefault();

            if (placedWords != null)
            {
                made.TextPosition = placedWords.Location;
                made.UsingDefaultTextPosition = true;
            }

            if (textAngle.HasValue)
            {
                made.TextRotation = textAngle.Value;
            }

            AddedHandle(Place(c, style, textHeight, made, picture));
        }

        /// <summary>
        /// Where the arc of an arc length dimension runs, its centre and radius, for a dimension whose point is a distance from
        /// the centre, and its arc point, as AutoCAD 2021 stores it: the point of the dimension's arc the third of the way along
        /// the arc measured comes to along its extension line. Below 90 degrees of sweep, the extension lines run along the middle
        /// of the arc (the bisector), and a dimension nearer the centre than the arc has an arc as big as the arc measured, moved
        /// in along the bisector; from 90 degrees up they run from the centre, the dimension's arc about the same centre.
        /// </summary>
        private static bool ArcLengthPlace(
            GeoPoint2 center,
            double radius,
            double start,
            double sweep,
            double distance,
            out GeoPoint2 arcPoint
        )
        {
            bool radial = sweep >= RadialFrom;
            GeoVector2 bisector = Heading(start + sweep / 2.0);
            GeoPoint2 middle = radial || distance >= radius ? center : center + bisector * (distance - radius);
            double size = radial || distance >= radius ? distance : radius;
            GeoPoint2 third = center + Heading(start + sweep / 3.0) * radius;
            arcPoint = radial ? center + Heading(start + sweep / 3.0) * size : AlongTo(third, bisector, middle, size);
            return radial;
        }

        /// <summary>Where a line from a point along a way meets a circle, the nearer of the two; the point itself where it misses.</summary>
        private static GeoPoint2 AlongTo(GeoPoint2 point, GeoVector2 way, GeoPoint2 center, double radius)
        {
            GeoVector2 d = point - center;
            double b = Dot(d, way);
            double disc = b * b - (Dot(d, d) - radius * radius);

            if (disc < 0.0)
            {
                return point;
            }

            double root = Math.Sqrt(disc);
            double t1 = -b + root;
            double t2 = -b - root;
            return point + way * (Math.Abs(t1) <= Math.Abs(t2) ? t1 : t2);
        }

        /// <summary>
        /// The picture of an arc length dimension, as AutoCAD 2021 draws it: the extension lines from the ends of the length,
        /// along the middle of the arc below 90 degrees of sweep and from the centre from 90 up; its own arc, arrowheads and words
        /// laid out as an angle's (<see cref="Angular"/>), between where the extension lines meet it; and the arc symbol of
        /// DIMARCSYM, 0 before the words as they read, 1 over them, 2 none. A leader, where asked for and the dimension's arc is
        /// outside the arc measured, runs from that arc at the words down to the arc measured, an arrowhead at its end.
        /// </summary>
        /// <remarks>
        /// The arc symbol scales with the words' height <c>t</c> as the font measures it: before them, half a circle of 0.8 t
        /// about a point t before their start and 0.4 t below their middle; over them, an arc through 0.75 t either side of
        /// their middle at 0.8 t up and 1.1 t up over it. An arc of 30 from 20 to 60 degrees dimensioned 40 out has its words
        /// "21" 41.88 out at 40 degrees, the symbol before them, its extension lines from 0.625 off the arc along 40 degrees.
        /// </remarks>
        internal static List<DxfEntity> ArcLength(
            DxfDimStyleTableRecord style,
            double dimscale,
            double textHeight,
            GeoPoint2 center,
            double radius,
            double start,
            double sweep,
            double distance,
            string words,
            int symbol,
            double? textAngle,
            bool leader,
            out bool led
        )
        {
            led = false;
            Sizes(
                style,
                dimscale,
                textHeight,
                out double scale,
                out _,
                out double height,
                out _,
                out double offset,
                out double beyond
            );
            bool radial = ArcLengthPlace(center, radius, start, sweep, distance, out _);
            GeoVector2 bisector = Heading(start + sweep / 2.0);
            GeoPoint2 middle = radial || distance >= radius ? center : center + bisector * (distance - radius);
            double size = radial || distance >= radius ? distance : radius;
            var ends = new[] { center + Heading(start) * radius, center + Heading(start + sweep) * radius };
            var tips = new GeoPoint2[2];

            for (int i = 0; i < 2; i++)
            {
                tips[i] = radial
                    ? center + Heading(i == 0 ? start : start + sweep) * size
                    : AlongTo(ends[i], bisector, middle, size);
            }

            double from = Math.Atan2(tips[0].Y - middle.Y, tips[0].X - middle.X);
            double turn = Turned(Math.Atan2(tips[1].Y - middle.Y, tips[1].X - middle.X) - from);
            turn = turn <= 1E-12 ? 2.0 * Math.PI : turn;
            WordsWidth(style, words, height, out double tall);
            double before = symbol == 0 ? 1.8 * tall : 0.0;
            double[] near = { radial ? radius : size, radial ? radius : size };
            // Below 90 degrees the extension lines run along the bisector, drawn below; the angle's own are left out.
            double[] far = radial ? (double[])near.Clone() : new[] { double.PositiveInfinity, double.PositiveInfinity };
            List<DxfEntity> picture = Angular(
                style,
                dimscale,
                textHeight,
                middle,
                size,
                from,
                turn,
                near,
                far,
                words,
                null,
                before,
                textAngle
            );

            if (picture == null)
            {
                return null;
            }

            picture.RemoveAll(e => e is DxfDBPoint);
            bool[] suppressed = { style.Dimse1, style.Dimse2 };

            for (int i = 0; i < 2 && !radial; i++)
            {
                // Along the bisector, from DIMEXO off the arc measured to DIMEXE past the dimension's arc.
                GeoVector2 run = tips[i] - ends[i];
                double length = run.Length;
                GeoVector2 way = length > 1E-12 ? run / length : bisector;

                if (!suppressed[i] && length + beyond > offset)
                {
                    picture.Add(
                        Lined(
                            new DxfLine(P3(ends[i] + way * offset), P3(tips[i] + way * beyond)),
                            style.Dimclre,
                            style.Dimlwe
                        )
                    );
                }
            }

            DxfMText text = picture.OfType<DxfMText>().FirstOrDefault();

            if (text != null && symbol != 2)
            {
                var x = new GeoVector2(Math.Cos(text.Rotation), Math.Sin(text.Rotation));
                var y = new GeoVector2(-x.Y, x.X);
                double width = WordsWidth(style, words, height, out _);
                GeoPoint2 at = P2(text.Location);
                DxfArc mark =
                    symbol == 0
                        ? new DxfArc(
                            P3(at - x * (width / 2.0 + tall) - y * (0.4 * tall)),
                            0.8 * tall,
                            Turned(text.Rotation),
                            Turned(text.Rotation + Math.PI)
                        )
                        : new DxfArc(
                            P3(at + y * (0.0125 * tall)),
                            1.0875 * tall,
                            Turned(text.Rotation + Math.Atan2(0.7875, 0.75)),
                            Turned(text.Rotation + Math.PI - Math.Atan2(0.7875, 0.75))
                        );
                Picture(mark);

                if (style.Dimclrt != null && !style.Dimclrt.IsByBlock)
                {
                    mark.Color = style.Dimclrt;
                }

                picture.Insert(picture.IndexOf(text) + 1, mark);
            }

            // The leader from the dimension's arc at the middle of its sweep to the arc measured, where it is outside that arc.
            if (leader && sweep > Math.PI / 2.0 + 1E-9 && size > radius)
            {
                GeoVector2 at = Heading(from + turn / 2.0);
                GeoPoint2 leaderFrom = middle + at * size;
                GeoPoint2 leaderTo = center + at * radius;
                led = true;
                picture.Add(Lined(new DxfLine(P3(leaderFrom), P3(leaderTo)), style.Dimclrd, style.Dimlwd));
                AddArrow(picture, style, DxfDimArrowFlag.FirstArrow, leaderTo, -at, style.Dimasz * scale);
            }

            picture.Add(Definition(center));
            picture.Add(Definition(ends[0]));
            picture.Add(Definition(ends[1]));
            return picture;
        }

        /// <summary>The picture of an arc length dimension of the drawing from its points; null where they give no arc.</summary>
        private static List<DxfEntity> ArcLengthPicture(
            DxfArcDimension dimension,
            DxfDimStyleTableRecord style,
            double textHeight
        )
        {
            GeoPoint2 center = P2(dimension.CenterPoint);
            GeoPoint2 first = P2(dimension.XLine1Point);
            GeoPoint2 second = P2(dimension.XLine2Point);
            GeoPoint2 arcPoint = P2(dimension.ArcPoint);
            double radius = (first - center).Length;

            if (radius <= 1E-12 || (arcPoint - center).Length <= 1E-12)
            {
                return null;
            }

            // The sweep from the first point to the second on the side of the arc point, else from the second to the first.
            double start = Math.Atan2(first.Y - center.Y, first.X - center.X);
            double sweep = Turned(Math.Atan2(second.Y - center.Y, second.X - center.X) - start);
            sweep = sweep <= 1E-12 ? 2.0 * Math.PI : sweep;

            if (Turned(Math.Atan2(arcPoint.Y - center.Y, arcPoint.X - center.X) - start) > sweep)
            {
                start += sweep;
                sweep = 2.0 * Math.PI - sweep;
            }

            // The distance the dimension was put at: its arc's radius, or, for one moved in along the bisector, the arc's radius
            // and how far it was moved in.
            double distance = (arcPoint - center).Length;

            if (sweep < RadialFrom && distance < radius)
            {
                GeoVector2 bisector = Heading(start + sweep / 2.0);
                GeoVector2 d = arcPoint - center;
                double along = Dot(d, bisector);
                double disc = along * along - Dot(d, d) + radius * radius;
                distance = radius + along - Math.Sqrt(Math.Max(0.0, disc));
            }

            int symbol;

            try
            {
                symbol = dimension.Dimarcsym;
            }
            catch (InvalidOperationException)
            {
                symbol = style.Dimarcsym;
            }

            double? turned = dimension.TextRotation != 0.0 ? dimension.TextRotation : (double?)null;
            return ArcLength(
                style,
                style.Dimscale,
                textHeight,
                center,
                radius,
                start,
                sweep,
                distance,
                Formatted(dimension, radius * sweep, string.Empty),
                symbol,
                turned,
                dimension.HasLeader,
                out _
            );
        }
    }
}
