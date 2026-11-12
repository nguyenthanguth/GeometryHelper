using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The ordinate dimensions: DIMORDINATE, and the picture of an ordinate dimension.</summary>
    /// <remarks>
    /// An ordinate dimension measures how far a point stands from the origin of the UCS along its X axis (an X datum) or its Y
    /// axis (a Y datum), as AutoCAD's: the point (300, 40) in the world measures 300 along X, its leader running from the
    /// point square to the axis measured, jogged where it ends to one side, and the words past its end.
    /// </remarks>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> DimOrdinate(CadCommandContext c) => DimOrdinateFrom(c, null);

        /// <summary>
        /// DIMORDINATE: the feature location (or the one given), then the leader endpoint, an X datum where the leader runs more
        /// along Y than along X, else a Y datum, as AutoCAD decides; Xdatum and Ydatum say which, Mtext and Text give the words
        /// (&lt;&gt; for the measurement), Angle turns them.
        /// </summary>
        private static IEnumerable<CadPrompt> DimOrdinateFrom(CadCommandContext c, GeoPoint2? given)
        {
            DxfDimStyleTableRecord style = DimStyle(c);

            if (style == null)
            {
                c.Write("The drawing has no dimension style to draw in.");
                yield break;
            }

            GeoPoint2 feature;
            GeoPoint2? last = null;

            if (given.HasValue)
            {
                feature = given.Value;
            }
            else
            {
                last = c.Session.LastPoint;
                yield return CadPrompt.ForPoint("Specify feature location").SnappingFrom(last.Value);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                feature = c.Input.Point;
            }

            // The feature is the ordinate's point 1, tied as AutoCAD 2021 ties it (group 90 of its DIMASSOC 2): a perpendicular or a
            // tangent drawn from the last point, which is not tied with it.
            DxfOsnapPointRef tied = CadDimAssociation.WithoutLastPoint(
                CadDimAssociation.FromSnap(c.Document, 1, c.Input.Snap, last)
            );

            bool? xDatum = null;
            string text = null;
            double? turn = null;

            while (true)
            {
                // The dimension the leader's end at the cursor would make, along the axis the leader's way chooses unless one was.
                string words = text;
                bool? chosen = xDatum;
                double? turned = turn;
                yield return CadPrompt
                    .ForPoint(
                        "Specify leader endpoint",
                        feature,
                        new[] { "Xdatum", "Ydatum", "Mtext", "Text", "Angle" }
                    )
                    .Previewing(DimPreview(c, input =>
                        {
                            CadUcs system = c.Session.Ucs;
                            GeoVector2 way = system.ToUcs(input.Point - feature);

                            return (input.Point - feature).Length <= c.Session.Tolerance.EqualPoint
                                ? null
                                : OrdinateMade(
                                    c,
                                    style,
                                    chosen ?? Math.Abs(way.Y) > Math.Abs(way.X),
                                    new GeoPoint2(system.Origin.X, system.Origin.Y),
                                    system.Angle,
                                    feature,
                                    input.Point,
                                    words,
                                    turned,
                                    out _,
                                    out _
                                );
                        }));

                if (c.Input.Is("Xdatum") || c.Input.Is("Ydatum"))
                {
                    xDatum = c.Input.Is("Xdatum");
                    continue;
                }

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
                        turn = TextAngle(c, c.Input.Number);
                    }

                    continue;
                }

                break;
            }

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            CadUcs ucs = c.Session.Ucs;
            GeoPoint2 end = c.Input.Point;
            GeoVector2 leader = ucs.ToUcs(end - feature);
            bool alongX = xDatum ?? Math.Abs(leader.Y) > Math.Abs(leader.X);
            var origin = new GeoPoint2(ucs.Origin.X, ucs.Origin.Y);
            CadDimAssociation.Tie(
                c,
                OrdinateDimension(c, style, alongX, origin, ucs.Angle, feature, end, text, turn, true),
                new[] { tied }
            );
        }

        /// <summary>
        /// Makes an ordinate dimension of a point from an origin, along the X (or the Y) axis of a system turned by
        /// <paramref name="turn"/>, its leader ending at a point; its words given or the measurement, turned where
        /// <paramref name="textAngle"/> says; on <paramref name="layer"/> where one is given, as a row goes on its base's. The
        /// handle of the dimension, or null where it was not made.
        /// </summary>
        private static string OrdinateDimension(
            CadCommandContext c,
            DxfDimStyleTableRecord style,
            bool xDatum,
            GeoPoint2 origin,
            double turn,
            GeoPoint2 feature,
            GeoPoint2 end,
            string text,
            double? textAngle,
            bool echo,
            string layer = null
        )
        {
            if ((end - feature).Length <= c.Session.Tolerance.EqualPoint)
            {
                if (echo)
                {
                    c.Write("*Invalid* The leader has no length.");
                }

                return null;
            }

            List<DxfEntity> picture = OrdinateMade(
                c,
                style,
                xDatum,
                origin,
                turn,
                feature,
                end,
                text,
                textAngle,
                out DxfOrdinateDimension made,
                out string words
            );

            if (echo)
            {
                c.Write("Dimension text = " + words);
            }

            return AddedHandle(Place(c, style, TextHeight(c, style), made, picture, onLayer: layer));
        }

        /// <summary>
        /// The picture of an ordinate dimension and the dimension, as <see cref="OrdinateDimension"/> adds them and the prompt
        /// of the leader's end previews them.
        /// </summary>
        private static List<DxfEntity> OrdinateMade(
            CadCommandContext c,
            DxfDimStyleTableRecord style,
            bool xDatum,
            GeoPoint2 origin,
            double turn,
            GeoPoint2 feature,
            GeoPoint2 end,
            string text,
            double? textAngle,
            out DxfOrdinateDimension made,
            out string words
        )
        {
            made = new DxfOrdinateDimension(xDatum, P3(feature), P3(end), text ?? string.Empty, null)
            {
                Origin = P3(origin),
            };

            if (turn != 0.0)
            {
                made.HorizontalRotation = -turn;
            }

            var axis = xDatum
                ? new GeoVector2(Math.Cos(turn), Math.Sin(turn))
                : new GeoVector2(-Math.Sin(turn), Math.Cos(turn));
            double measured = Math.Abs((feature - origin).X * axis.X + (feature - origin).Y * axis.Y);
            words = Words(style, made, measured, text, string.Empty);
            double textHeight = TextHeight(c, style);
            return WithTextAngle(
                made,
                Ordinate(style, DrawnScale(c, style), textHeight, origin, feature, end, xDatum, turn, words),
                textAngle
            );
        }

        /// <summary>
        /// The picture of an ordinate dimension, as AutoCAD 2021 draws it: the leader from DIMEXO off the point square to the axis
        /// measured, a jog to the side of its end two arrowheads before it and a last leg of two arrowheads to its end (one
        /// straight line where its end is in line with the point; under DIMFXLON the first leg, or the one line, DIMFXL long up to
        /// the jog or the end), and the words past the end along the leader, DIMGAP off it,
        /// turned along it, an X datum's a quarter turn more; where DIMTAD is not 0 the leader runs on under them and they stand
        /// beside it by DIMGAP and half their height (below it for 4); words moved free under DIMTMOVE 2 (<paramref name="free"/>)
        /// leave it ending at its end.
        /// </summary>
        /// <remarks>
        /// An X datum of the point (300, 40) with its leader ending at (310, 80), arrowheads of 2.5: the leader runs up from
        /// (300, 40.625) to (300, 70), across to (310, 75) and up to (310, 80), as AutoCAD 2021 draws it.
        /// </remarks>
        internal static List<DxfEntity> Ordinate(
            DxfDimStyleTableRecord style,
            double dimscale,
            double textHeight,
            GeoPoint2 origin,
            GeoPoint2 feature,
            GeoPoint2 end,
            bool xDatum,
            double turn,
            string words,
            bool free = false
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
                out _
            );
            var picture = new List<DxfEntity>();
            var x = new GeoVector2(Math.Cos(turn), Math.Sin(turn));
            var y = new GeoVector2(-Math.Sin(turn), Math.Cos(turn));
            GeoVector2 square = xDatum ? y : x;
            GeoVector2 measured = xDatum ? x : y;
            double reach = (end - feature).X * square.X + (end - feature).Y * square.Y;
            double aside = (end - feature).X * measured.X + (end - feature).Y * measured.Y;
            GeoVector2 way = reach < 0.0 ? -square : square;
            double leg = 2.0 * arrow;
            double first = Math.Max(leg, Math.Abs(reach) - 2.0 * leg);

            // The words read along the leader as AutoCAD turns them, not turned to read from below: 120 degrees in a UCS turned 30.
            double angle = turn + (xDatum ? Math.PI / 2.0 : 0.0);
            angle -= Math.Floor(angle / (2.0 * Math.PI)) * 2.0 * Math.PI;
            DxfEntity text = Words(style, default, angle, height, words, scale);
            // The words laid out as high as the font measures them, past the height their text is written with.
            double width = WordsWidth(style, words, height, out height);
            int tad = style.Dimtad;
            GeoPoint2 jogged = end - way * leg;
            GeoPoint2 last = tad != 0 && !free ? end + way * (gap + width) : end;

            if (Math.Abs(aside) <= 1E-9)
            {
                // Under DIMFXLON the line runs DIMFXL back from its end at the words, as AutoCAD 2021 draws it.
                double start = ExtensionStart(style, scale, offset, (last - feature).Length);
                picture.Add(Lined(new DxfLine(P3(feature + way * start), P3(last)), style.Dimclre, style.Dimlwe));
            }
            else
            {
                // Under DIMFXLON the first leg runs DIMFXL back from the jog, no nearer the point than DIMEXO, as AutoCAD 2021
                // draws it: the jog at 939.28 and DIMFXL 10 start it at 929.28, DIMFXL 50 at DIMEXO off the point.
                double from = ExtensionStart(style, scale, offset, first);
                picture.Add(
                    Lined(new DxfLine(P3(feature + way * from), P3(feature + way * first)), style.Dimclre, style.Dimlwe)
                );
                picture.Add(Lined(new DxfLine(P3(feature + way * first), P3(jogged)), style.Dimclre, style.Dimlwe));
                picture.Add(Lined(new DxfLine(P3(jogged), P3(last)), style.Dimclre, style.Dimlwe));
            }

            var above = new GeoVector2(-Math.Sin(angle), Math.Cos(angle));
            double lift =
                tad == 0 ? 0.0
                : tad == 4 ? -(gap + height / 2.0)
                : gap + height / 2.0;
            ((DxfMText)text).Location = P3(end + way * (gap + width / 2.0) + above * lift);
            picture.Add(text);
            picture.Add(Definition(feature));
            picture.Add(Definition(end));
            picture.Add(Definition(origin));
            return picture;
        }

        /// <summary>The picture of an ordinate dimension of the drawing from its points.</summary>
        private static List<DxfEntity> OrdinatePicture(
            DxfDimension dimension,
            DxfDimStyleTableRecord style,
            double textHeight,
            DxfOrdinateDimension ordinate
        )
        {
            GeoPoint2 feature = P2(ordinate.DefiningPoint);
            GeoPoint2 end = P2(ordinate.LeaderEndPoint);

            if ((end - feature).Length <= 1E-12)
            {
                return null;
            }

            return Ordinate(
                style,
                style.Dimscale,
                textHeight,
                P2(ordinate.Origin),
                feature,
                end,
                ordinate.UsingXAxis,
                -ordinate.HorizontalRotation,
                Formatted(dimension, ordinate.Measurement, string.Empty),
                !dimension.UsingDefaultTextPosition && style.Dimtmove == 2
            );
        }
    }
}
