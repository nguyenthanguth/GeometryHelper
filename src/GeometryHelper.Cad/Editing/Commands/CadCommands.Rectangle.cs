using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// RECTANG with its options, as AutoCAD 2021 asks them: Chamfer, Elevation, Fillet, Thickness and Width at the first
    /// prompt, Area, Dimensions and Rotation at the second.
    /// </summary>
    /// <remarks>
    /// The modes stay for the next RECTANG, as AutoCAD keeps them for the session, and are listed at the start of the command
    /// ("Current rectangle modes:  Chamfer=1.0000 x 2.0000  Width=0.5000") where any is not nought. A chamfer and a fillet
    /// shut each other out: one set clears the other, and the distance each offers is that of the other where it is the one
    /// in force. The rotation stays until set to nought.
    /// </remarks>
    internal static partial class CadCommands
    {
        private const string RectangleFirstChamferKey = "rectang.chamfer1";
        private const string RectangleSecondChamferKey = "rectang.chamfer2";
        private const string RectangleFilletKey = "rectang.fillet";
        private const string RectangleElevationKey = "rectang.elevation";
        private const string RectangleThicknessKey = "rectang.thickness";
        private const string RectangleWidthKey = "rectang.width";
        private const string RectangleRotationKey = "rectang.rotation";
        private const string RectangleLengthKey = "rectang.length";
        private const string RectangleSideKey = "rectang.side";
        private const string RectangleAreaKey = "rectang.area";

        /// <summary>
        /// The words of the modes in force for "Current rectangle modes:", in AutoCAD's order (Chamfer, Elevation, Fillet,
        /// Rotation, Thickness, Width), each only where it is not nought; null where none is.
        /// </summary>
        private static string RectangleModesText(CadCommandContext c)
        {
            var parts = new List<string>();
            double first = c.Remembered(RectangleFirstChamferKey, 0.0);
            double second = c.Remembered(RectangleSecondChamferKey, 0.0);
            double fillet = c.Remembered(RectangleFilletKey, 0.0);
            double rotation = c.Remembered(RectangleRotationKey, 0.0);

            if (first != 0.0 || second != 0.0)
            {
                parts.Add("Chamfer=" + Length(first) + " x " + Length(second));
            }

            if (c.Remembered(RectangleElevationKey, 0.0) != 0.0)
            {
                parts.Add("Elevation=" + Length(c.Remembered(RectangleElevationKey, 0.0)));
            }

            if (fillet != 0.0)
            {
                parts.Add("Fillet=" + Length(fillet));
            }

            if (rotation != 0.0)
            {
                parts.Add("Rotation=" + Counted(c, rotation));
            }

            if (c.Remembered(RectangleThicknessKey, 0.0) != 0.0)
            {
                parts.Add("Thickness=" + Length(c.Remembered(RectangleThicknessKey, 0.0)));
            }

            if (c.Remembered(RectangleWidthKey, 0.0) != 0.0)
            {
                parts.Add("Width=" + Length(c.Remembered(RectangleWidthKey, 0.0)));
            }

            return parts.Count == 0 ? null : string.Join("  ", parts);
        }

        /// <summary>Asks one distance of a mode: typed or two points, Enter taking the offered one; the value is left null where the answer is none.</summary>
        private static IEnumerable<CadPrompt> RectangleValue(
            CadCommandContext c,
            string message,
            double offered,
            Ref<double?> result
        )
        {
            yield return CadPrompt.ForDistance(message, null, null, Length(offered));

            if (c.Input.IsNone)
            {
                result.Value = offered;
            }
            else if (c.Input.Kind == CadInputKind.Number)
            {
                result.Value = c.Input.Number;
            }
        }

        /// <summary>
        /// The four corners of a rectangle of sides <paramref name="lx"/> along the rotation and <paramref name="ly"/> along
        /// the turn of it a quarter, from its first corner: in the order RECTANG draws them, the first side along the first axis.
        /// </summary>
        private static GeoPoint2[] RectangleCorners(GeoPoint2 first, double rotation, double lx, double ly)
        {
            double cos = Math.Cos(rotation);
            double sin = Math.Sin(rotation);
            var p1 = new GeoPoint2(first.X + cos * lx, first.Y + sin * lx);
            var p3 = new GeoPoint2(first.X - sin * ly, first.Y + cos * ly);
            return new[] { first, p1, new GeoPoint2(p1.X - sin * ly, p1.Y + cos * ly), p3 };
        }

        /// <summary>
        /// The rectangle of four corners, in the modes in force: the corners cut by a chamfer or rounded by a fillet where
        /// the sides are long enough (one side at least the two distances, or twice the radius), which AutoCAD otherwise draws
        /// as the plain rectangle of eight vertices, two at each corner; a width, an elevation and a thickness. Each corner
        /// takes its chamfer back along the side that comes to it by the first distance and forward along the side that leaves
        /// it by the second, a negative distance cutting outwards, and a fillet is the arc of a quarter between the same points.
        /// </summary>
        private static DxfPolyline BuildRectangle(CadCommandContext c, GeoPoint2[] corners)
        {
            CadUcs ucs = c.Session.Ucs;
            double first = c.Remembered(RectangleFirstChamferKey, 0.0);
            double second = c.Remembered(RectangleSecondChamferKey, 0.0);
            double fillet = c.Remembered(RectangleFilletKey, 0.0);
            double width = Math.Max(0.0, c.Remembered(RectangleWidthKey, 0.0));
            var rectangle = new DxfPolyline();

            if (first == 0.0 && second == 0.0 && fillet == 0.0)
            {
                for (int i = 0; i < 4; i++)
                {
                    rectangle.AddVertexAt(i, ucs.ToWorld(corners[i]), 0.0, width, width);
                }
            }
            else
            {
                double back = fillet != 0.0 ? fillet : first;
                double forward = fillet != 0.0 ? fillet : second;
                double shortest = Math.Min((corners[1] - corners[0]).Length, (corners[3] - corners[0]).Length);
                bool fits = back + forward <= shortest + c.Session.Tolerance.EqualPoint;
                double turn = (corners[1] - corners[0]).CrossProduct(corners[3] - corners[0]);
                double bulge = fits && fillet != 0.0 ? (turn > 0.0 ? 1.0 : -1.0) * Math.Tan(Math.PI / 8.0) : 0.0;
                int at = 0;

                for (int i = 0; i < 4; i++)
                {
                    GeoPoint2 corner = corners[i];
                    GeoVector2 arriving = (corner - corners[(i + 3) % 4]).Normalize();
                    GeoVector2 leaving = (corners[(i + 1) % 4] - corner).Normalize();
                    GeoPoint2 start = fits ? corner - arriving * back : corner;
                    GeoPoint2 end = fits ? corner + leaving * forward : corner;
                    rectangle.AddVertexAt(at++, ucs.ToWorld(start), bulge, width, width);
                    rectangle.AddVertexAt(at++, ucs.ToWorld(end), 0.0, width, width);
                }
            }

            rectangle.Closed = true;

            if (c.Database.Plinegen)
            {
                rectangle.Plinegen = true;
            }

            double elevation = c.Remembered(RectangleElevationKey, 0.0);
            double thickness = c.Remembered(RectangleThicknessKey, 0.0);

            if (elevation != 0.0)
            {
                rectangle.Elevation = elevation;
            }

            if (thickness != 0.0)
            {
                rectangle.Thickness = thickness;
            }

            return Current(c, rectangle);
        }

        private static IEnumerable<CadPrompt> Rectangle(CadCommandContext c)
        {
            string modes = RectangleModesText(c);

            if (modes != null)
            {
                c.Write("Current rectangle modes:  " + modes);
            }

            while (true)
            {
                yield return CadPrompt.ForPoint(
                    "Specify first corner point",
                    null,
                    new[] { "Chamfer", "Elevation", "Fillet", "Thickness", "Width" }
                );

                if (c.Input.Kind != CadInputKind.Keyword)
                {
                    break;
                }

                var value = new Ref<double?>();

                if (c.Input.Is("Chamfer"))
                {
                    double first = c.Remembered(RectangleFirstChamferKey, 0.0);
                    double second = c.Remembered(RectangleSecondChamferKey, 0.0);
                    double fillet = c.Remembered(RectangleFilletKey, 0.0);
                    bool chamfered = first != 0.0 || second != 0.0;
                    double offered = chamfered ? first : fillet;

                    foreach (
                        CadPrompt prompt in RectangleValue(
                            c,
                            "Specify first chamfer distance for rectangles",
                            offered,
                            value
                        )
                    )
                    {
                        yield return prompt;
                    }

                    if (!value.Value.HasValue)
                    {
                        yield break;
                    }

                    double typed = value.Value.Value;
                    var other = new Ref<double?>();

                    foreach (
                        CadPrompt prompt in RectangleValue(
                            c,
                            "Specify second chamfer distance for rectangles",
                            chamfered ? second
                                : fillet != 0.0 ? fillet
                                : typed,
                            other
                        )
                    )
                    {
                        yield return prompt;
                    }

                    if (!other.Value.HasValue)
                    {
                        yield break;
                    }

                    c.Remember(RectangleFirstChamferKey, typed);
                    c.Remember(RectangleSecondChamferKey, other.Value.Value);
                    c.Remember(RectangleFilletKey, 0.0);
                }
                else if (c.Input.Is("Fillet"))
                {
                    bool chamfered =
                        c.Remembered(RectangleFirstChamferKey, 0.0) != 0.0
                        || c.Remembered(RectangleSecondChamferKey, 0.0) != 0.0;

                    foreach (
                        CadPrompt prompt in RectangleValue(
                            c,
                            "Specify fillet radius for rectangles",
                            chamfered
                                ? c.Remembered(RectangleFirstChamferKey, 0.0)
                                : c.Remembered(RectangleFilletKey, 0.0),
                            value
                        )
                    )
                    {
                        yield return prompt;
                    }

                    if (!value.Value.HasValue)
                    {
                        yield break;
                    }

                    c.Remember(RectangleFilletKey, value.Value.Value);
                    c.Remember(RectangleFirstChamferKey, 0.0);
                    c.Remember(RectangleSecondChamferKey, 0.0);
                }
                else
                {
                    bool elevation = c.Input.Is("Elevation");
                    bool thickness = c.Input.Is("Thickness");
                    string key =
                        elevation ? RectangleElevationKey
                        : thickness ? RectangleThicknessKey
                        : RectangleWidthKey;

                    foreach (
                        CadPrompt prompt in RectangleValue(
                            c,
                            elevation ? "Specify the elevation for rectangles"
                                : thickness ? "Specify thickness for rectangles"
                                : "Specify line width for rectangles",
                            c.Remembered(key, 0.0),
                            value
                        )
                    )
                    {
                        yield return prompt;
                    }

                    if (!value.Value.HasValue)
                    {
                        yield break;
                    }

                    c.Remember(key, value.Value.Value);
                }
            }

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            // The sides run along the axes of the UCS, turned by the rotation, as AutoCAD draws them.
            CadUcs ucs = c.Session.Ucs;
            GeoPoint2 pick = c.Input.Point;
            GeoPoint2 a = ucs.ToUcs(pick);
            double tolerance = c.Session.Tolerance.EqualPoint;

            // Length and width of Dimensions, or none where the other corner gives the sides.
            double? length = null;
            double? side = null;

            // The corners of the other corner, as it is previewed and made; none where they lie in a line.
            GeoPoint2[] Corners(GeoPoint2 other)
            {
                GeoPoint2 b = ucs.ToUcs(other);
                double rotation = c.Remembered(RectangleRotationKey, 0.0);

                if (rotation == 0.0 && !length.HasValue)
                {
                    return Math.Abs(b.X - a.X) > tolerance && Math.Abs(b.Y - a.Y) > tolerance
                        ? new[] { a, new GeoPoint2(b.X, a.Y), b, new GeoPoint2(a.X, b.Y) }
                        : null;
                }

                double lx = (b.X - a.X) * Math.Cos(rotation) + (b.Y - a.Y) * Math.Sin(rotation);
                double ly = (b.Y - a.Y) * Math.Cos(rotation) - (b.X - a.X) * Math.Sin(rotation);

                if (length.HasValue)
                {
                    lx = (lx < 0.0 ? -1.0 : 1.0) * length.Value;
                    ly = (ly < 0.0 ? -1.0 : 1.0) * side.Value;
                }

                return Math.Abs(lx) > tolerance && Math.Abs(ly) > tolerance
                    ? RectangleCorners(a, rotation, lx, ly)
                    : null;
            }

            DxfPolyline Make(GeoPoint2 other)
            {
                GeoPoint2[] corners = Corners(other);
                return corners == null ? null : BuildRectangle(c, corners);
            }

            GeoPoint2 corner;

            while (true)
            {
                yield return CadPrompt
                    .ForPoint("Specify other corner point", pick, new[] { "Area", "Dimensions", "Rotation" })
                    .Previewing(CadPreview.MakingOne(c, input => Make(input.Point)));

                if (c.Input.Is("Area"))
                {
                    foreach (CadPrompt prompt in RectangleByArea(c, a))
                    {
                        yield return prompt;
                    }

                    yield break;
                }

                if (c.Input.Is("Dimensions"))
                {
                    var sizes = new Ref<double?>();

                    foreach (
                        CadPrompt prompt in RectangleValue(
                            c,
                            "Specify length for rectangles",
                            c.Remembered(RectangleLengthKey, 10.0),
                            sizes
                        )
                    )
                    {
                        yield return prompt;
                    }

                    if (!sizes.Value.HasValue)
                    {
                        yield break;
                    }

                    var across = new Ref<double?>();

                    foreach (
                        CadPrompt prompt in RectangleValue(
                            c,
                            "Specify width for rectangles",
                            c.Remembered(RectangleSideKey, 10.0),
                            across
                        )
                    )
                    {
                        yield return prompt;
                    }

                    if (!across.Value.HasValue)
                    {
                        yield break;
                    }

                    length = Math.Abs(sizes.Value.Value);
                    side = Math.Abs(across.Value.Value);
                    c.Remember(RectangleLengthKey, length.Value);
                    c.Remember(RectangleSideKey, side.Value);
                    continue;
                }

                if (c.Input.Is("Rotation"))
                {
                    double rotation = c.Remembered(RectangleRotationKey, 0.0);

                    yield return CadPrompt.ForAngle(
                        "Specify rotation angle",
                        null,
                        new[] { "Pick points" },
                        Counted(c, rotation)
                    );

                    if (c.Input.Is("Pick points"))
                    {
                        var picked = new Ref<double?>();

                        foreach (CadPrompt prompt in TwoPoints(c, true, picked))
                        {
                            yield return prompt;
                        }

                        if (!picked.Value.HasValue)
                        {
                            yield break;
                        }

                        rotation = picked.Value.Value;
                    }
                    else if (c.Input.Kind == CadInputKind.Number)
                    {
                        rotation = c.Input.Number;
                    }
                    else if (!c.Input.IsNone)
                    {
                        yield break;
                    }

                    c.Remember(RectangleRotationKey, rotation);

                    // The rotation asked again starts the rectangle over: the sides of Dimensions are given up, as AutoCAD does.
                    length = null;
                    side = null;
                    continue;
                }

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                corner = c.Input.Point;

                if (Corners(corner) != null)
                {
                    break;
                }

                c.Write("*Invalid* The corners lie in a line.");
            }

            DxfPolyline made = Make(corner);
            c.Apply(null, s => s.Append(c.Space, made));
        }

        /// <summary>
        /// Area: the area of the rectangle, then the side it is worked from, Length or Width, and its value; the other side
        /// follows from the area, the corners cut by a chamfer or rounded by a fillet taken off it. The rectangle is drawn at
        /// once, to the side of the first corner the cursor is on (up and to the right where there is no cursor). The
        /// length and width become those Dimensions offers next, the area that Area offers.
        /// </summary>
        private static IEnumerable<CadPrompt> RectangleByArea(CadCommandContext c, GeoPoint2 a)
        {
            double area;

            while (true)
            {
                var asked = new Ref<double?>();

                foreach (
                    CadPrompt prompt in RectangleValue(
                        c,
                        "Enter area of rectangle in current units",
                        c.Remembered(RectangleAreaKey, 100.0),
                        asked
                    )
                )
                {
                    yield return prompt;
                }

                if (!asked.Value.HasValue)
                {
                    yield break;
                }

                if (asked.Value.Value > 0.0)
                {
                    area = asked.Value.Value;
                    break;
                }

                c.Write("Value must be positive and nonzero.");
            }

            c.Remember(RectangleAreaKey, area);
            yield return CadPrompt.ForKeyword(
                "Calculate rectangle dimensions based on",
                new[] { "Length", "Width" },
                "Length"
            );
            bool byLength = c.Input.IsNone || c.Input.Is("Length");

            if (!byLength && !c.Input.Is("Width"))
            {
                yield break;
            }

            double given;

            while (true)
            {
                var asked = new Ref<double?>();

                foreach (
                    CadPrompt prompt in RectangleValue(
                        c,
                        byLength ? "Enter rectangle length" : "Enter rectangle width",
                        c.Remembered(byLength ? RectangleLengthKey : RectangleSideKey, 10.0),
                        asked
                    )
                )
                {
                    yield return prompt;
                }

                if (!asked.Value.HasValue)
                {
                    yield break;
                }

                if (asked.Value.Value > 0.0)
                {
                    given = asked.Value.Value;
                    break;
                }

                c.Write("Value must be positive and nonzero.");
            }

            // The corners take their share off the area: a chamfer a by b two triangles of half a times b, a fillet of
            // radius r four quarters of the square on r less the circle's quarter.
            double first = c.Remembered(RectangleFirstChamferKey, 0.0);
            double second = c.Remembered(RectangleSecondChamferKey, 0.0);
            double fillet = c.Remembered(RectangleFilletKey, 0.0);
            double cut = fillet != 0.0 ? (4.0 - Math.PI) * fillet * fillet : 2.0 * first * second;
            double other = (area + cut) / given;

            if (!(other > 0.0) || double.IsInfinity(other))
            {
                c.Write("Value must be positive and nonzero.");
                yield break;
            }

            double lx = byLength ? given : other;
            double ly = byLength ? other : given;
            c.Remember(RectangleLengthKey, lx);
            c.Remember(RectangleSideKey, ly);

            // The side of the first corner the rectangle grows to: that of the cursor, in the rectangle's own axes.
            double rotation = c.Remembered(RectangleRotationKey, 0.0);

            if (c.Session.Cursor.HasValue)
            {
                GeoPoint2 at = c.Session.Ucs.ToUcs(c.Session.Cursor.Value);
                double dx = (at.X - a.X) * Math.Cos(rotation) + (at.Y - a.Y) * Math.Sin(rotation);
                double dy = (at.Y - a.Y) * Math.Cos(rotation) - (at.X - a.X) * Math.Sin(rotation);
                lx = dx < 0.0 ? -lx : lx;
                ly = dy < 0.0 ? -ly : ly;
            }

            DxfPolyline made = BuildRectangle(c, RectangleCorners(a, rotation, lx, ly));
            c.Apply(null, s => s.Append(c.Space, made));
        }
    }
}
