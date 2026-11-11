using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The draw commands: LINE, PLINE, CIRCLE, ARC, RECTANG, ELLIPSE, POINT, TEXT, MTEXT, MLEADER, MVIEW.</summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> Line(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Specify first point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            var points = new List<GeoPoint2> { c.Input.Point };
            var made = new List<string>();

            while (true)
            {
                GeoPoint2 last = points[points.Count - 1];
                string[] keywords = made.Count >= 2 ? new[] { "Close", "Undo" } : new[] { "Undo" };
                yield return CadPrompt.ForPoint("Specify next point", last, keywords);

                if (c.Input.IsPoint)
                {
                    GeoPoint2 next = c.Input.Point;
                    string handle = AddedHandle(
                        c.Apply(null, s => s.Append(c.Space, Current(c, new DxfLine(P3(last), P3(next)))))
                    );

                    if (handle != null)
                    {
                        made.Add(handle);
                        points.Add(next);
                        RememberLast(c, next, next - last);
                    }
                }
                else if (c.Input.Is("Undo"))
                {
                    if (made.Count == 0)
                    {
                        c.Write("All segments already undone.");
                        continue;
                    }

                    EraseAll(c, new[] { made[made.Count - 1] });
                    made.RemoveAt(made.Count - 1);
                    points.RemoveAt(points.Count - 1);
                }
                else if (c.Input.Is("Close"))
                {
                    GeoPoint2 first = points[0];

                    if (
                        AddedHandle(c.Apply(null, s => s.Append(c.Space, Current(c, new DxfLine(P3(last), P3(first))))))
                        != null
                    )
                    {
                        RememberLast(c, first, first - last);
                    }

                    yield break;
                }
                else
                {
                    yield break;
                }
            }
        }

        private static IEnumerable<CadPrompt> Circle(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint(
                "Specify center point for circle",
                null,
                new[] { "3P", "2P", "Ttr (tan tan radius)" }
            );
            GeoPoint2 center;
            double radius;

            if (c.Input.Is("Ttr (tan tan radius)"))
            {
                var touches = new List<Touch>();

                foreach (
                    string message in new[]
                    {
                        "Specify point on object for first tangent of circle",
                        "Specify point on object for second tangent of circle",
                    }
                )
                {
                    foreach (CadPrompt prompt in AskTouch(c, message, null, touches, true))
                    {
                        yield return prompt;
                    }

                    if (touches.Count == 0 || touches[touches.Count - 1].Asked != message)
                    {
                        yield break;
                    }
                }

                double last = c.Remembered("circle", 0.0);

                // The circle of the radius touching both, of the centres the shapes moved by it either way cross, the one whose
                // points of touching lie nearest the picks, as FILLET finds its arc.
                bool Touching(double r, out GeoPoint2 at) =>
                    Tangent(
                        touches[0].Shape,
                        touches[0].Pick,
                        touches[1].Shape,
                        touches[1].Pick,
                        r,
                        c.Session.Tolerance,
                        out at,
                        out _,
                        out _
                    );

                yield return CadPrompt
                    .ForDistance("Specify radius of circle", touches[1].Pick, null, last > 0.0 ? Length(last) : null)
                    .Previewing(
                        CadPreview.MakingOne(
                            c,
                            input =>
                                input.Number > 0.0 && Touching(input.Number, out GeoPoint2 at)
                                    ? MakeCircle(c, at, input.Number)
                                    : null,
                            true
                        )
                    );
                radius = c.Input.Kind == CadInputKind.Number ? c.Input.Number : last;

                if (radius <= 0.0)
                {
                    c.Write("Value must be positive and nonzero.");
                    yield break;
                }

                if (!Touching(radius, out center))
                {
                    c.Write("Circle does not exist.");
                    yield break;
                }
            }
            else if (c.Input.Is("2P"))
            {
                yield return CadPrompt.ForPoint("Specify first end point of circle's diameter");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 a = c.Input.Point;
                yield return CadPrompt
                    .ForPoint("Specify second end point of circle's diameter", a)
                    .Previewing(
                        CadPreview.MakingOne(c, input =>
                            (input.Point - a).Length > 0.0
                                ? MakeCircle(
                                    c,
                                    new GeoPoint2((a.X + input.Point.X) / 2.0, (a.Y + input.Point.Y) / 2.0),
                                    (input.Point - a).Length / 2.0
                                )
                                : null
                        )
                    );

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 b = c.Input.Point;
                center = new GeoPoint2((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
                radius = (b - a).Length / 2.0;
            }
            else if (c.Input.Is("3P"))
            {
                // Each point a point on the circle, or, typed _tan before it is picked, a line, an arc or a circle it touches, as
                // the Draw ribbon's Tan, Tan, Tan runs CIRCLE 3P with a tangent snap at each.
                var touches = new List<Touch>();
                string[] messages =
                {
                    "Specify first point on circle",
                    "Specify second point on circle",
                    "Specify third point on circle",
                };

                // The circle of the two given and the third at the cursor, as it is made once the third is given.
                DxfEntity Third(Touch third)
                {
                    var three = new List<Touch>(touches) { third };
                    return CircleOf(three, c.Session.Tolerance, out GeoPoint2 at, out double r) && r > 0.0
                        ? MakeCircle(c, at, r)
                        : null;
                }

                for (int i = 0; i < 3; i++)
                {
                    foreach (
                        CadPrompt prompt in AskTouch(
                            c,
                            messages[i],
                            i == 0 ? (GeoPoint2?)null : touches[i - 1].Pick,
                            touches,
                            false,
                            i == 2 ? Third : (Func<Touch, DxfEntity>)null
                        )
                    )
                    {
                        yield return prompt;
                    }

                    if (touches.Count == i)
                    {
                        if (i == 2)
                        {
                            c.Write("Circle does not exist.");
                        }

                        yield break;
                    }
                }

                if (!CircleOf(touches, c.Session.Tolerance, out center, out radius))
                {
                    c.Write("Circle does not exist.");
                    yield break;
                }
            }
            else if (c.Input.IsPoint)
            {
                center = c.Input.Point;
                GeoPoint2 at = center;
                double last = c.Remembered("circle", 0.0);
                string offered = last > 0.0 ? Length(last) : null;
                yield return CadPrompt
                    .ForDistance("Specify radius of circle", center, new[] { "Diameter" }, offered)
                    .Previewing(
                        CadPreview.MakingOne(
                            c,
                            input => input.Number > 0.0 ? MakeCircle(c, at, input.Number) : null,
                            true
                        )
                    );

                if (c.Input.Is("Diameter"))
                {
                    yield return CadPrompt
                        .ForDistance("Specify diameter of circle", center, null, last > 0.0 ? Length(2.0 * last) : null)
                        .Previewing(
                            CadPreview.MakingOne(
                                c,
                                input => input.Number > 0.0 ? MakeCircle(c, at, input.Number / 2.0) : null,
                                true
                            )
                        );
                    radius = c.Input.Kind == CadInputKind.Number ? c.Input.Number / 2.0 : last;
                }
                else
                {
                    radius = c.Input.Kind == CadInputKind.Number ? c.Input.Number : last;
                }
            }
            else
            {
                yield break;
            }

            if (radius <= 0.0)
            {
                c.Write("Value must be positive and nonzero.");
                yield break;
            }

            c.Remember("circle", radius);
            GeoPoint2 middle = center;
            double r = radius;
            c.Apply(null, s => s.Append(c.Space, MakeCircle(c, middle, r)));
        }

        /// <summary>The circle CIRCLE makes, and previews, of a centre and a radius, in the current layer and colour.</summary>
        private static DxfCircle MakeCircle(CadCommandContext c, GeoPoint2 center, double radius) =>
            Current(c, new DxfCircle(P3(center), Up, radius));

        /// <summary>The circle through three points, or through some and touching the objects of the others.</summary>
        private static bool CircleOf(
            List<Touch> touches,
            Tolerance tolerance,
            out GeoPoint2 center,
            out double radius
        ) =>
            touches.TrueForAll(t => t.Shape == null)
                ? CadGrips.CircleThrough(touches[0].Pick, touches[1].Pick, touches[2].Pick, out center, out radius)
                : CircleTouching(touches, tolerance, out center, out radius);

        private static IEnumerable<CadPrompt> Rectangle(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Specify first corner point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            // The sides run along the axes of the UCS, as AutoCAD draws them.
            CadUcs ucs = c.Session.Ucs;
            GeoPoint2 first = c.Input.Point;
            GeoPoint2 a = ucs.ToUcs(first);

            // The rectangle of the other corner, as it is previewed and made; none where the corners lie in a line.
            DxfPolyline Make(GeoPoint2 other)
            {
                GeoPoint2 b = ucs.ToUcs(other);

                if (
                    !(
                        Math.Abs(b.X - a.X) > c.Session.Tolerance.EqualPoint
                        && Math.Abs(b.Y - a.Y) > c.Session.Tolerance.EqualPoint
                    )
                )
                {
                    return null;
                }

                var rectangle = new DxfPolyline();
                rectangle.AddVertexAt(0, ucs.ToWorld(a), 0.0, 0.0, 0.0);
                rectangle.AddVertexAt(1, ucs.ToWorld(new GeoPoint2(b.X, a.Y)), 0.0, 0.0, 0.0);
                rectangle.AddVertexAt(2, ucs.ToWorld(b), 0.0, 0.0, 0.0);
                rectangle.AddVertexAt(3, ucs.ToWorld(new GeoPoint2(a.X, b.Y)), 0.0, 0.0, 0.0);
                rectangle.Closed = true;
                return Current(c, rectangle);
            }

            GeoPoint2 corner;

            while (true)
            {
                yield return CadPrompt
                    .ForPoint("Specify other corner point", first)
                    .Previewing(CadPreview.MakingOne(c, input => Make(input.Point)));

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                corner = c.Input.Point;

                if (Make(corner) != null)
                {
                    break;
                }

                c.Write("*Invalid* The corners lie in a line.");
            }

            c.Apply(null, s => s.Append(c.Space, Make(corner)));
        }

        /// <summary>
        /// ELLIPSE, whole or an Arc of it, by the ends of one axis or by its centre and one end, then the other axis's half or a
        /// rotation; an arc's start and end angles are counter-clockwise from the first end of an axis given, as AutoCAD's.
        /// </summary>
        private static IEnumerable<CadPrompt> Ellipse(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Specify axis endpoint of ellipse", null, new[] { "Arc", "Center" });
            bool arc = c.Input.Is("Arc");
            GeoPoint2 center;
            GeoVector2 half;
            GeoVector2 axis;

            if (arc)
            {
                yield return CadPrompt.ForPoint("Specify axis endpoint of elliptical arc", null, new[] { "Center" });
            }

            if (c.Input.Is("Center"))
            {
                yield return CadPrompt.ForPoint(arc ? "Specify center of elliptical arc" : "Specify center of ellipse");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                center = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify endpoint of axis", center);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                half = c.Input.Point - center;
                axis = half;
            }
            else if (c.Input.IsPoint)
            {
                GeoPoint2 a = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify other endpoint of axis", a);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                center = new GeoPoint2((a.X + c.Input.Point.X) / 2.0, (a.Y + c.Input.Point.Y) / 2.0);
                half = c.Input.Point - center;
                axis = a - center;
            }
            else
            {
                yield break;
            }

            if (half.Length <= c.Session.Tolerance.EqualPoint)
            {
                c.Write("*Invalid* The ends of the axis are one point.");
                yield break;
            }

            // The ellipse of the other axis's half, from the start angle to the end angle about the first axis given, as it is
            // previewed and made; none where the other axis has no length.
            GeoPoint2 middle = center;
            GeoVector2 first = half;
            GeoVector2 given = axis;

            DxfEllipse Make(double length, double from, double to, bool part)
            {
                if (!(length > c.Session.Tolerance.EqualPoint))
                {
                    return null;
                }

                // The longer half is the major axis; the shorter lies square to it.
                GeoVector2 major =
                    length > first.Length ? new GeoVector2(-first.Y, first.X) / first.Length * length : first;
                double ratio = Math.Min(length, first.Length) / Math.Max(length, first.Length);

                // The ellipse measures its angles from its major axis.
                if (part)
                {
                    from += Math.Atan2(given.Y, given.X) - Math.Atan2(major.Y, major.X);
                    to += Math.Atan2(given.Y, given.X) - Math.Atan2(major.Y, major.X);
                }

                return Current(
                    c,
                    new DxfEllipse(
                        P3(middle),
                        new GeoVector3(0.0, 0.0, 1.0),
                        new GeoVector3(major.X, major.Y, 0.0),
                        ratio,
                        from,
                        to
                    )
                );
            }

            yield return CadPrompt
                .ForDistance("Specify distance to other axis", center, new[] { "Rotation" })
                .Previewing(
                    CadPreview.MakingOne(c, input => Make(Math.Abs(input.Number), 0.0, 2.0 * Math.PI, false), true)
                );
            double other;

            if (c.Input.Is("Rotation"))
            {
                yield return CadPrompt
                    .ForAngle("Specify rotation around major axis", center)
                    .Turning()
                    .Previewing(
                        CadPreview.MakingOne(
                            c,
                            input =>
                                Math.Abs(Math.Cos(input.Number)) < Math.Cos(89.4 * Math.PI / 180.0)
                                    ? null
                                    : Make(first.Length * Math.Abs(Math.Cos(input.Number)), 0.0, 2.0 * Math.PI, false),
                            true
                        )
                    );

                // AutoCAD takes a rotation below 89.4 degrees, the other half the first's times its cosine.
                if (
                    c.Input.Kind != CadInputKind.Number
                    || Math.Abs(Math.Cos(c.Input.Number)) < Math.Cos(89.4 * Math.PI / 180.0)
                )
                {
                    c.Write("*Invalid* Requires an angle from 0 to 89.4 degrees.");
                    yield break;
                }

                other = half.Length * Math.Abs(Math.Cos(c.Input.Number));
            }
            else if (c.Input.Kind == CadInputKind.Number)
            {
                other = Math.Abs(c.Input.Number);
            }
            else
            {
                yield break;
            }

            if (other <= c.Session.Tolerance.EqualPoint)
            {
                c.Write("*Invalid* The other axis has no length.");
                yield break;
            }

            double start = 0.0;
            double end = 2.0 * Math.PI;

            if (arc)
            {
                // An angle typed is from the first axis end, clockwise with ANGDIR 1; one picked is the way to the point, made from
                // that end too (the session gives a pick at these prompts less ANGBASE, which is put back here).
                double fromUcs = c.Session.Ucs.AngleToUcs(Math.Atan2(axis.Y, axis.X)) - c.Database.Angbase;
                yield return CadPrompt
                    .ForAngle("Specify start angle", center)
                    .Turning()
                    .Previewing(CadPreview.MakingOne(c, input => Make(other, 0.0, 2.0 * Math.PI, false), true));

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                start = Typed(c.Input) ? c.Input.Number : c.Input.Number - fromUcs;
                double begin = start;
                yield return CadPrompt
                    .ForAngle("Specify end angle", center, new[] { "Included angle" })
                    .Turning()
                    .Previewing(
                        CadPreview.MakingOne(c, input => Make(other, begin, input.Number - fromUcs, true), true)
                    );

                if (c.Input.Is("Included angle"))
                {
                    yield return CadPrompt
                        .ForAngle("Specify included angle for arc", center, null, "180")
                        .Turning()
                        .Previewing(
                            CadPreview.MakingOne(c, input => Make(other, begin, input.Number - fromUcs, true), true)
                        );
                    end =
                        c.Input.IsNone ? start + Math.PI
                        : c.Input.Kind != CadInputKind.Number ? double.NaN
                        : Typed(c.Input) ? start + c.Input.Number
                        : c.Input.Number - fromUcs;
                }
                else
                {
                    end =
                        c.Input.Kind == CadInputKind.Number
                            ? (Typed(c.Input) ? c.Input.Number : c.Input.Number - fromUcs)
                            : double.NaN;
                }

                if (double.IsNaN(end))
                {
                    yield break;
                }
            }

            c.Apply(null, s => s.Append(c.Space, Make(other, start, end, arc)));
        }

        /// <summary>POINT: one point object where the point is given, drawn as the drawing's PDMODE says.</summary>
        private static IEnumerable<CadPrompt> Point(CadCommandContext c)
        {
            c.Write(
                "Current point modes:  PDMODE="
                    + c.Database.Pdmode.ToString(CultureInfo.InvariantCulture)
                    + "  PDSIZE="
                    + Length(c.Database.Pdsize)
            );
            yield return CadPrompt.ForPoint("Specify a point");

            if (c.Input.IsPoint)
            {
                GeoPoint2 at = c.Input.Point;
                c.Apply(null, s => s.Append(c.Space, Current(c, new DxfDBPoint(P3(at)))));
            }
        }

        private static IEnumerable<CadPrompt> MLeader(CadCommandContext c)
        {
            if (!HasMLeaderStyle(c.Database))
            {
                // A drawing that can hold multileaders is given Standard, as AutoCAD 2021's always has it.
                if (string.CompareOrdinal(c.Database.Version ?? string.Empty, "AC1021") < 0)
                {
                    c.Write(
                        "The drawing has no multileader style to draw in: one made by AutoCAD's MLEADERSTYLE is needed."
                    );
                    yield break;
                }

                c.Document.EnsureNamedObjectsDictionary();
                c.Apply(
                    new[]
                    {
                        new CadEditRecord(
                            CadEditRecordKind.TableChanged,
                            null,
                            "New multileader style",
                            null,
                            "Standard"
                        ),
                    },
                    s => CadMLeaderStyleEditor.MakeStandard(s.Database)
                );
            }

            yield return CadPrompt.ForPoint("Specify leader arrowhead location");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 arrow = c.Input.Point;
            string style = CadMLeaderStyleEditor.Current(c.Database);

            // The scale MLEADERSCALE gives, the style's own until the variable is set, as AutoCAD 2021 draws in it.
            double scale = CadMLeaderStyleEditor.NewScale(
                c.Database,
                CadMLeaderStyleEditor.Find(c.Database, style),
                c.Document.ActiveViewport
            );

            // The leader and its landing to the cursor, made as the multileader is but of no words yet, as AutoCAD drags them.
            yield return CadPrompt
                .ForPoint("Specify leader landing location", arrow)
                .Previewing(
                    CadPreview.MakingOne(c, input =>
                        (input.Point - arrow).Length > c.Session.Tolerance.EqualPoint
                            ? Current(
                                c,
                                CadMLeaderStyleEditor.Make(
                                    CadMLeaderStyleEditor.Find(c.Database, style),
                                    scale,
                                    P3(arrow),
                                    P3(input.Point),
                                    CadMLeaderStyleEditor.Find(c.Database, style).ContentType
                                    == DxfContentType.NoneContent
                                        ? null
                                        : " "
                                )
                            )
                            : null
                    )
                );

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 landing = c.Input.Point;
            string name = CadMLeaderStyleEditor.Current(c.Database);

            // A style of no content asks for nothing more, as AutoCAD 2021's MLEADER ends at the landing; one of a block asks
            // the values of its attributes, "Enter attribute values" then "Enter tag number <TAGNUMBER>:", as it asks them.
            DxfMLeaderStyle current = CadMLeaderStyleEditor.Find(c.Database, name);
            bool none = current.ContentType == DxfContentType.NoneContent;
            string block =
                current.ContentType == DxfContentType.BlockContent && current.Block != null ? current.Block.Name : null;
            string words = null;
            var values = new List<string>();

            if (block != null)
            {
                foreach (CadPrompt prompt in AttributeValues(c, c.Database.BlockTable[block], values))
                {
                    yield return prompt;
                }
            }
            else if (!none)
            {
                yield return CadPrompt.ForString("Enter text");

                if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Length == 0)
                {
                    yield break;
                }

                words = c.Input.Text;
            }

            // Drawn in the current multileader style (CMLEADERSTYLE, else Standard): its text height, landing, arrows and the
            // rest of its values, as AutoCAD's MLEADER draws one. A scale other than the style's is the multileader's own (its
            // bit 24), as AutoCAD 2021 writes MLEADERSCALE 2 in a style of scale 5.
            c.Apply(null, s =>
            {
                DxfMLeaderStyle found = CadMLeaderStyleEditor.Find(s.Database, name);
                DxfMLeader mleader = CadMLeaderStyleEditor.Make(found, scale, P3(arrow), P3(landing), words);
                s.Append(c.Space, Current(c, mleader));
                mleader.MLeaderStyle = name;

                // The block taken first, so that the style gives it its scale, colour and connection, then placed past the
                // landing and given its leader and the values of its attributes.
                DxfBlockTableRecord record = block == null ? null : s.Database.BlockTable[block];

                if (record != null)
                {
                    mleader.BlockContent = record;
                }

                mleader.SetFromStyle();

                if (record != null)
                {
                    CadMLeaderStyleEditor.PlaceBlock(mleader, record, P3(arrow), P3(landing));
                    List<DxfAttributeDefinition> definitions = Definitions(record);

                    for (int i = 0; i < definitions.Count; i++)
                    {
                        mleader.SetBlockAttribute(
                            definitions[i],
                            new DxfAttributeReference { Tag = definitions[i].Tag, TextString = values[i] }
                        );
                    }
                }

                if (none)
                {
                    mleader.ContentType = DxfContentType.NoneContent;
                }

                if (!scale.Equals(found.Scale))
                {
                    mleader.Scale = scale;
                }
            });
        }

        /// <summary>MLEADERSTYLE (MLS): the window's Multileader Style Manager; with no window, the current style said.</summary>
        private static IEnumerable<CadPrompt> MLeaderStyleDialog(CadCommandContext c)
        {
            if (!c.Session.Request("MLEADERSTYLE", c.Document))
            {
                c.Write(
                    "Current multileader style: \""
                        + CadMLeaderStyleEditor.Current(c.Database)
                        + "\". The Multileader Style Manager is shown by the window."
                );
            }

            yield break;
        }

        private static IEnumerable<CadPrompt> MView(CadCommandContext c)
        {
            if (c.Space.IsModel)
            {
                c.Write("** Command not allowed in Model Tab **");
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify corner of viewport", null, new[] { "Fit" }, "Fit");
            DisplayBox frame;

            if (c.Input.IsPoint)
            {
                GeoPoint2 a = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify opposite corner", a);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 b = c.Input.Point;
                frame = new DisplayBox(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
            }
            else if (c.Input.Is("Fit"))
            {
                frame = c.Space.PaperBounds;
            }
            else
            {
                yield break;
            }

            if (frame.Width <= 0.0 || frame.Height <= 0.0)
            {
                c.Write("*Invalid* The corners lie in a line.");
                yield break;
            }

            // The viewport shows all of model space, as AutoCAD's MVIEW zooms a new viewport to the extents.
            DisplayBox model = c.Document.GetDisplay(c.Document.ModelSpace).Bounds;
            double viewHeight = 1.0;
            var viewCenter = new GeoPoint2(0.0, 0.0);

            if (!model.IsEmpty)
            {
                viewCenter = new GeoPoint2((model.MinX + model.MaxX) / 2.0, (model.MinY + model.MaxY) / 2.0);
                viewHeight = Math.Max(model.Height, model.Width * frame.Height / frame.Width);

                if (viewHeight <= 0.0)
                {
                    viewHeight = 1.0;
                }
            }

            CadSpace space = c.Space;
            c.Apply(null, s =>
            {
                // A layout never shown has no viewport 1 for its sheet yet; AutoCAD gives it one before the first of its own.
                int before = space.Layout.GetViewports().Count;
                DxfViewport sheet = space.Layout.Initialize();

                if (space.Layout.GetViewports().Count > before)
                {
                    s.Added(space, sheet);
                }

                var viewport = new DxfViewport();
                s.Append(space, Current(c, viewport));
                viewport.CenterPoint = new GeoPoint3(
                    (frame.MinX + frame.MaxX) / 2.0,
                    (frame.MinY + frame.MaxY) / 2.0,
                    0.0
                );
                viewport.Width = frame.Width;
                viewport.Height = frame.Height;
                viewport.ViewCenter = viewCenter;
                viewport.ViewHeight = viewHeight;
                viewport.On = true;

                // The layers frozen in new viewports (New VP Freeze, VPLAYER Vpvisdflt) were frozen in this one as it was
                // appended, as AutoCAD freezes them.
            });
        }

        /// <summary>Whether the drawing has the multileader style a new MULTILEADER takes, which the library reads but does not write.</summary>
        private static bool HasMLeaderStyle(DxfDatabase database)
        {
            try
            {
                DxfDBDictionary styles = database.MLeaderStyleDictionary;
                return styles != null
                    && CadMLeaderStyleEditor.Find(database, CadMLeaderStyleEditor.Current(database)) != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string TextStyle(CadCommandContext c)
        {
            string name;

            try
            {
                name = c.Database.Textstyle;
            }
            catch (Exception)
            {
                return null;
            }

            return !string.IsNullOrEmpty(name) && c.Database.TextStyleTable.Has(name) ? name : null;
        }

        /// <summary>The record of the style a new text is written in: the current one, else Standard; null where the drawing has neither.</summary>
        private static DxfTextStyleTableRecord StyleRecord(CadCommandContext c, string style)
        {
            string name = style ?? "Standard";
            return c.Database.TextStyleTable.Has(name) ? c.Database.TextStyleTable[name] : null;
        }

        private static double TextSize(CadCommandContext c)
        {
            try
            {
                double size = c.Database.Textsize;
                return size > 0.0 ? size : 2.5;
            }
            catch (Exception)
            {
                return 2.5;
            }
        }
    }
}
