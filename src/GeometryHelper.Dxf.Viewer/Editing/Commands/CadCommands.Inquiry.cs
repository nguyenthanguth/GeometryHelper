using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The inquiry commands: DIST, AREA (with its Add area and Subtract area modes), ID and LIST, which measure and change
    /// nothing, writing what they find on the command line in AutoCAD's words: DIST from (0, 0) to (100, 100) writes "Distance
    /// = 141.4214,  Angle in XY Plane = 45,  Angle from XY Plane = 0" in a drawing whose angles are shown to whole degrees
    /// ($AUPREC 0), its angles to that precision.
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> Dist(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Specify first point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 a = c.Input.Point;
            yield return CadPrompt.ForPoint("Specify second point", a);

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            // The angle and the deltas are along the axes of the UCS, as AutoCAD reports them.
            GeoVector2 d = c.Session.Ucs.ToUcs(c.Input.Point - a);
            c.Write(
                "Distance = "
                    + Length(d.Length)
                    + ",  Angle in XY Plane = "
                    + Degrees(c, c.Session.CountAngle(Math.Atan2(d.Y, d.X)))
                    + ",  Angle from XY Plane = "
                    + Degrees(c, c.Session.CountAngle(0.0))
            );
            c.Write("Delta X = " + Length(d.X) + ",  Delta Y = " + Length(d.Y) + ",  Delta Z = " + Length(0.0));
        }

        private static IEnumerable<CadPrompt> Id(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Specify point");

            if (c.Input.IsPoint)
            {
                // In the coordinates of the UCS, as AutoCAD's ID.
                GeoPoint2 at = c.Session.Ucs.ToUcs(c.Input.Point);
                c.Write("X = " + Length(at.X) + "     Y = " + Length(at.Y) + "     Z = " + Length(0.0));
            }
        }

        /// <summary>
        /// AREA of points picked, closed back to the first, or of a closed object: a circle, a closed polyline, an ellipse; or,
        /// after Add area or Subtract area, a total each area measured is added to or taken from until Enter: a rectangle 100 by
        /// 50 added and a circle of radius 10 taken out leave "Total area = 4685.8407".
        /// </summary>
        private static IEnumerable<CadPrompt> Area(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint(
                "Specify first corner point",
                null,
                new[] { "Object", "Add area", "Subtract area" },
                "Object"
            );

            if (!c.Input.Is("Add area") && !c.Input.Is("Subtract area"))
            {
                foreach (CadPrompt prompt in AreaOf(c, null, null))
                {
                    yield return prompt;
                }

                yield break;
            }

            double sum = 0.0;
            bool adding = c.Input.Is("Add area");

            while (true)
            {
                yield return CadPrompt.ForPoint(
                    "Specify first corner point",
                    null,
                    new[] { "Object", adding ? "Subtract area" : "Add area" }
                );

                if (c.Input.Is("Add area") || c.Input.Is("Subtract area"))
                {
                    adding = c.Input.Is("Add area");
                    continue;
                }

                if (!c.Input.IsPoint && !c.Input.Is("Object"))
                {
                    yield break;
                }

                foreach (CadPrompt prompt in AreaOf(c, adding ? "ADD" : "SUBTRACT", area =>
                    {
                        sum += adding ? area : -area;
                        c.Write("Total area = " + Length(sum));
                    }))
                {
                    yield return prompt;
                }
            }
        }

        /// <summary>
        /// Measures from the answer given to "Specify first corner point": the points that follow it, or with Object the object
        /// picked; in a mode of AREA (ADD, SUBTRACT) its prompts say so, objects are picked until Enter, and each area is handed on.
        /// </summary>
        private static IEnumerable<CadPrompt> AreaOf(CadCommandContext c, string mode, Action<double> measured)
        {
            string before = mode == null ? string.Empty : "(" + mode + " mode) ";

            while (c.Input.Is("Object") || (mode != null && c.Input.Kind == CadInputKind.Selection))
            {
                yield return CadPrompt.ForEntity(before + "Select objects");

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                if (
                    !(c.Document.FindEntity(c.Input.Handles[0]) is DxfCurve curve)
                    || curve is DxfLine
                    || curve is DxfXline
                    || curve is DxfRay
                )
                {
                    c.Write("Selected object does not have an area.");

                    if (mode == null)
                    {
                        yield break;
                    }

                    continue;
                }

                double length = curve.GetDistanceAtParameter(curve.EndParam);
                string around =
                    curve is DxfCircle ? "Circumference"
                    : curve.Closed ? "Perimeter"
                    : "Length";
                c.Write("Area = " + Length(curve.Closed ? curve.Area : 0.0) + ", " + around + " = " + Length(length));

                if (mode == null)
                {
                    yield break;
                }

                measured(curve.Closed ? curve.Area : 0.0);
            }

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            var points = new List<GeoPoint2> { c.Input.Point };

            while (true)
            {
                bool total = points.Count >= 3;
                yield return CadPrompt.ForPoint(
                    before + "Specify next point",
                    points[points.Count - 1],
                    total ? new[] { "Undo", "Total" } : new[] { "Undo" },
                    total ? "Total" : null
                );

                if (c.Input.IsPoint)
                {
                    points.Add(c.Input.Point);
                }
                else if (c.Input.Is("Undo"))
                {
                    if (points.Count > 1)
                    {
                        points.RemoveAt(points.Count - 1);
                    }
                }
                else if (c.Input.Is("Total") || points.Count >= 2)
                {
                    break;
                }
                else
                {
                    yield break;
                }
            }

            // The shoelace sum of the polygon closed back to its first point, and the length round it.
            double twice = 0.0;
            double perimeter = 0.0;

            for (int i = 0; i < points.Count; i++)
            {
                GeoPoint2 p = points[i];
                GeoPoint2 q = points[(i + 1) % points.Count];
                twice += p.X * q.Y - q.X * p.Y;
                perimeter += (q - p).Length;
            }

            c.Write("Area = " + Length(Math.Abs(twice) / 2.0) + ", Perimeter = " + Length(perimeter));
            measured?.Invoke(Math.Abs(twice) / 2.0);
        }

        /// <summary>LIST: each object selected as AutoCAD lists it, its kind, layer, space and handle, then its shape.</summary>
        private static IEnumerable<CadPrompt> ListObjects(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            foreach (string handle in c.Selected)
            {
                DxfEntity entity = c.Document.FindEntity(handle);
                CadSpace space = c.Document.FindSpace(handle);
                // A dimension of any type is a DIMENSION, two spaces before its layer, as AutoCAD 2021 writes it.
                c.Write(
                    new string(' ', 18)
                        + (
                            entity is DxfDimension
                                ? "DIMENSION  "
                                : CadProperties.KindOf(entity).ToUpperInvariant().PadRight(10)
                        )
                        + "Layer: \""
                        + entity.Layer
                        + "\""
                );
                c.Write(
                    new string(' ', 28) + "Space: " + (space == null || space.IsModel ? "Model space" : "Paper space")
                );
                c.Write(new string(' ', 19) + "Handle = " + handle.ToLowerInvariant());

                foreach (string line in Listed(c, entity, handle, space))
                {
                    c.Write(line);
                }
            }
        }

        /// <summary>The lines LIST writes of an object's shape: AutoCAD's for a line, a circle, an arc, a polyline, a point, an ellipse and a dimension (a jogged radius aside), the rows Properties shows for the others.</summary>
        private static IEnumerable<string> Listed(CadCommandContext c, DxfEntity entity, string handle, CadSpace space)
        {
            switch (entity)
            {
                case DxfLine line:
                    GeoVector3 delta = line.Delta;
                    GeoVector2 flat = c.Session.Ucs.ToUcs(new GeoVector2(delta.X, delta.Y));
                    var d = new GeoVector3(flat.X, flat.Y, delta.Z);
                    yield return "              from point, " + At(c, line.StartPoint);
                    yield return "                to point, " + At(c, line.EndPoint);
                    yield return "          Length = "
                        + Length(line.Length)
                        + ",  Angle in XY Plane = "
                        + Degrees(c, c.Session.CountAngle(Math.Atan2(d.Y, d.X))).PadLeft(6);
                    yield return "                  Delta X = "
                        + Length(d.X)
                        + ", Delta Y = "
                        + Length(d.Y)
                        + ", Delta Z = "
                        + Length(d.Z);
                    yield break;
                case DxfCircle circle:
                    yield return "            center point, " + At(c, circle.Center);
                    yield return "            radius " + Length(circle.Radius).PadLeft(9);
                    yield return "     circumference " + Length(circle.Circumference).PadLeft(9);
                    yield return "              area " + Length(circle.Area).PadLeft(9);
                    yield break;
                case DxfArc arc:
                    yield return "            center point, " + At(c, arc.Center);
                    yield return "            radius " + Length(arc.Radius).PadLeft(9);
                    yield return "             start angle "
                        + Degrees(c, c.Session.CountAngle(c.Session.Ucs.AngleToUcs(arc.StartAngle))).PadLeft(6);
                    yield return "               end angle "
                        + Degrees(c, c.Session.CountAngle(c.Session.Ucs.AngleToUcs(arc.EndAngle))).PadLeft(6);
                    yield return "                  length " + Length(arc.Length).PadLeft(9);
                    yield break;
                case DxfPolyline polyline:
                    yield return "                            " + (polyline.Closed ? "Closed" : "Open");
                    yield return "                    area   " + Length(polyline.Closed ? polyline.Area : 0.0);
                    yield return "                "
                        + (polyline.Closed ? "perimeter " : "   length ")
                        + Length(polyline.Length);

                    for (int i = 0; i < polyline.NumberOfVertices; i++)
                    {
                        yield return "                at point  " + At(c, polyline.GetPoint3dAt(i));
                    }

                    yield break;
                case DxfDBPoint point:
                    yield return "                at point, " + At(c, point.Position);
                    yield break;
                case DxfEllipse ellipse:
                    foreach (string line in ListedEllipse(c, ellipse))
                    {
                        yield return line;
                    }

                    yield break;
                case DxfDimension dimension when !(dimension is DxfRadialDimensionLarge):
                    foreach (string line in ListedDimension(c, dimension))
                    {
                        yield return line;
                    }

                    yield break;
            }

            foreach (CadPropertyGroup group in CadProperties.Describe(c.Document, space, new[] { handle }).Groups)
            {
                if (group.Title == "General" || group.Title == "Plot style" || group.Title == "View")
                {
                    continue;
                }

                foreach (CadPropertyRow row in group.Rows)
                {
                    yield return "                  " + row.Label + " = " + row.Value;
                }
            }
        }

        /// <summary>
        /// The lines LIST writes of an ellipse, as AutoCAD 2021 writes them: its area and circumference whole, else its length,
        /// its ends and its start and end angles, these counted as the drawing counts angles (an arc from 30 to 120 degrees reads
        /// 60 and 330 with ANGBASE 90 and ANGDIR 1); its centre, axes and radius ratio either way.
        /// </summary>
        private static IEnumerable<string> ListedEllipse(CadCommandContext c, DxfEllipse ellipse)
        {
            bool whole = ellipse.Closed;

            if (whole)
            {
                yield return Labelled("Area", Length(ellipse.Area));
                yield return Labelled("Circumference", Length(ellipse.GetDistanceAtParameter(ellipse.EndParam)));
            }
            else
            {
                yield return Labelled("Length", Length(ellipse.GetDistanceAtParameter(ellipse.EndParam)));
            }

            yield return Labelled("Center", Spaced(c, ellipse.Center, false));
            yield return Labelled("Major Axis", Spaced(c, ellipse.Center + ellipse.MajorAxis, true, ellipse.Center));
            yield return Labelled("Minor Axis", Spaced(c, ellipse.Center + ellipse.MinorAxis, true, ellipse.Center));

            if (!whole)
            {
                yield return Labelled("Start Point", Spaced(c, ellipse.StartPoint, false));
                yield return Labelled("End Point", Spaced(c, ellipse.EndPoint, false));
                yield return Labelled("Start Angle", Degrees(c, c.Session.CountAngle(ellipse.StartAngle)));
                yield return Labelled("End Angle", Degrees(c, c.Session.CountAngle(ellipse.EndAngle)));
            }

            yield return Labelled("Radius Ratio", Length(ellipse.RadiusRatio));
        }

        /// <summary>A line of an ellipse's list, its label set right to end at the 34th place: "             Center: X = ...".</summary>
        private static string Labelled(string label, string value) => label.PadLeft(33) + ": " + value;

        /// <summary>
        /// A point, or a vector from another point, as LIST writes those of an ellipse: "X = 600.0000 , Y = -300.0000, Z = 0.0000",
        /// in the UCS.
        /// </summary>
        private static string Spaced(CadCommandContext c, GeoPoint3 world, bool vector, GeoPoint3 from = default)
        {
            CadUcs ucs = c.Session.Ucs;
            GeoPoint3 p = ucs.IsWorld ? world : ucs.ToUcs(world);

            if (vector)
            {
                GeoPoint3 o = ucs.IsWorld ? from : ucs.ToUcs(from);
                p = new GeoPoint3(p.X - o.X, p.Y - o.Y, p.Z - o.Z);
            }

            return "X = " + Length(p.X).PadRight(9) + ", Y = " + Length(p.Y).PadRight(9) + ", Z = " + Length(p.Z);
        }

        /// <summary>
        /// The lines LIST writes of a dimension, as AutoCAD 2021 writes them: whether it is associative, its type, its defining
        /// points, its obliquing and text angles where it has them, its text position and text, its style and whether it is
        /// annotative. The rotation of a rotated dimension is an amount turned, counted the way ANGDIR counts from the X axis (30
        /// reads 330 with ANGDIR 1, whatever ANGBASE is); the obliquing and text angles are directions, counted from ANGBASE (30
        /// reads 60 with ANGBASE 90 and ANGDIR 1), as AutoCAD writes them.
        /// </summary>
        private static IEnumerable<string> ListedDimension(CadCommandContext c, DxfDimension dimension)
        {
            double tolerance = c.Session.Tolerance.EqualAngleRad;
            yield return "associative: " + (dimension.DimAssoc != null ? "yes" : "no");
            double oblique = 0.0;

            switch (dimension)
            {
                case DxfRotatedDimension rotated:
                    double turn = rotated.Rotation;
                    bool horizontal = Math.Abs(Math.IEEERemainder(turn, 2.0 * Math.PI)) <= tolerance;
                    bool vertical = Math.Abs(Math.IEEERemainder(turn - Math.PI / 2.0, 2.0 * Math.PI)) <= tolerance;
                    yield return "type: "
                        + (
                            horizontal ? "horizontal"
                            : vertical ? "vertical"
                            : "rotated"
                        );

                    if (!horizontal && !vertical)
                    {
                        yield return " rotation:" + Degrees(c, c.Database.Angdir ? -turn : turn).PadLeft(7);
                    }

                    yield return "1st extension  defining point: " + At(c, rotated.XLine1Point);
                    yield return "2nd extension  defining point: " + At(c, rotated.XLine2Point);
                    yield return "dimension line defining point: " + At(c, rotated.DimLinePoint);
                    oblique = rotated.Oblique;
                    break;
                case DxfAlignedDimension aligned:
                    yield return "type: aligned";
                    yield return "1st extension  defining point: " + At(c, aligned.XLine1Point);
                    yield return "2nd extension  defining point: " + At(c, aligned.XLine2Point);
                    yield return "dimension line defining point: " + At(c, aligned.DimLinePoint);
                    oblique = aligned.Oblique;
                    break;
                case DxfLineAngularDimension2 lines:
                    yield return "type: angular";
                    yield return "first line     defining point: " + At(c, lines.XLine1Start);
                    yield return "first line     defining point: " + At(c, lines.XLine1End);
                    yield return "second line    defining point: " + At(c, lines.XLine2Start);
                    yield return "second line    defining point: " + At(c, lines.XLine2End);
                    yield return "dimension arc defining point: " + At(c, lines.ArcPoint);
                    break;
                case DxfPoint3AngularDimension three:
                    yield return "type: arc";
                    yield return "1st extension defining point: " + At(c, three.XLine1Point);
                    yield return "2nd extension defining point: " + At(c, three.XLine2Point);
                    yield return "arc center    defining point: " + At(c, three.CenterPoint);
                    yield return "dimension arc defining point: " + At(c, three.ArcPoint);
                    break;
                case DxfArcDimension arc:
                    yield return "type: arc";
                    yield return "1st extension defining point: " + At(c, arc.XLine1Point);
                    yield return "2nd extension defining point: " + At(c, arc.XLine2Point);
                    yield return "arc center    defining point: " + At(c, arc.CenterPoint);
                    yield return "dimension arc defining point: " + At(c, arc.ArcPoint);
                    break;
                case DxfRadialDimension radial:
                    yield return "type: radial";
                    yield return "center defining point: " + At(c, radial.Center);
                    yield return "defining point:        " + At(c, radial.ChordPoint);
                    break;
                case DxfDiametricDimension diametric:
                    yield return "type: diameter";
                    yield return "       defining point: " + At(c, diametric.FarChordPoint);
                    yield return "defining point:        " + At(c, diametric.ChordPoint);
                    break;
                case DxfOrdinateDimension ordinate:
                    yield return "type: " + (ordinate.UsingXAxis ? "X" : "Y") + " ordinate";
                    yield return "feature      defining point: " + At(c, ordinate.DefiningPoint);
                    yield return "leader       defining point: " + At(c, ordinate.LeaderEndPoint);
                    yield return "feature UCS  defining point: " + At(c, ordinate.Origin);
                    break;
            }

            if (Math.Abs(Math.IEEERemainder(oblique, 2.0 * Math.PI)) > tolerance)
            {
                yield return "obliquing angle:" + Degrees(c, c.Session.CountAngle(oblique)).PadLeft(7);
            }

            if (Math.Abs(Math.IEEERemainder(dimension.TextRotation, 2.0 * Math.PI)) > tolerance)
            {
                yield return "text angle:" + Degrees(c, c.Session.CountAngle(dimension.TextRotation)).PadLeft(7);
            }

            yield return (dimension.UsingDefaultTextPosition ? "default" : "user specified")
                + " text position: "
                + At(c, dimension.TextPosition);
            yield return string.IsNullOrEmpty(dimension.DimensionText)
                ? "default text"
                : "dimension text modifier: " + dimension.DimensionText;
            yield return "dimension style: \"" + dimension.DimensionStyleName + "\"";
            yield return "Annotative: " + (dimension.Annotative == DxfAnnotativeStates.True ? "Yes" : "No");
        }

        /// <summary>A point as LIST writes one: "X= 100.0000  Y=   0.0000  Z=   0.0000".</summary>
        private static string At(CadCommandContext c, GeoPoint3 world)
        {
            GeoPoint3 p = c.Session.Ucs.IsWorld ? world : c.Session.Ucs.ToUcs(world);
            return "X=" + Length(p.X).PadLeft(9) + "  Y=" + Length(p.Y).PadLeft(9) + "  Z=" + Length(p.Z).PadLeft(9);
        }

        /// <summary>An angle in degrees from 0 to 360, to the places of the drawing's $AUPREC: 45 at 0, as AutoCAD's template has it, 45.00 at 2.</summary>
        private static string Degrees(CadCommandContext c, double radians)
        {
            string format = "F" + Math.Max(0, Math.Min(8, c.Database.Auprec)).ToString(CultureInfo.InvariantCulture);
            double degrees = radians * 180.0 / Math.PI % 360.0;

            // Nought added makes -0 nought; a turn that rounds to 360 is written as nought, as AutoCAD writes it.
            degrees = (degrees < 0.0 ? degrees + 360.0 : degrees) + 0.0;
            string text = degrees.ToString(format, CultureInfo.InvariantCulture);
            return text == 360.0.ToString(format, CultureInfo.InvariantCulture)
                ? 0.0.ToString(format, CultureInfo.InvariantCulture)
                : text;
        }
    }
}
