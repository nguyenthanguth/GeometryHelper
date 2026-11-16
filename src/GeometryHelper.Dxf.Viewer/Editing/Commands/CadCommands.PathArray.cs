using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// ARRAYPATH and the PAth type of ARRAY as AutoCAD 2021 asks them on the command line (probed through its console): the
    /// objects, the path curve (a line, arc, circle, ellipse, polyline or spline) and then the options of the array until
    /// eXit or Enter makes it, each item as a separate object, one undo step. Items follow the path from the end nearest the
    /// pick, a step of the Measure distance or an equal share (Divide) from one another, turned by how the path turns unless
    /// Align items is No. A path of 100 measured by 30 from its start gives items at 0, 30, 60 and 90.
    /// </summary>
    internal static partial class CadCommands
    {
        private static readonly string[] PathOptions =
        {
            "ASsociative",
            "Method",
            "Base point",
            "Tangent direction",
            "Items",
            "Rows",
            "Levels",
            "Align items",
            "Z direction",
            "eXit",
        };

        private static IEnumerable<CadPrompt> ArrayPathCommand(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            foreach (CadPrompt prompt in PathArray(c))
            {
                yield return prompt;
            }
        }

        /// <summary>
        /// The path array of the objects selected: the path curve asked, then the options of the array offered with the array as
        /// it stands shown, until eXit or Enter makes it. The first item stays where the objects are when the array begins at
        /// the base point, the start of the path, and moves where another base point or a direction changes it.
        /// </summary>
        private static IEnumerable<CadPrompt> PathArray(CadCommandContext c)
        {
            c.Write("Type = Path  Associative = No");
            DxfCurve curve;
            double total;
            GeoPoint2? pickedAt;

            while (true)
            {
                yield return CadPrompt.ForEntity("Select path curve");

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                string handle = c.Input.Handles[0];

                if (c.Selected.Any(h => string.Equals(h, handle, StringComparison.OrdinalIgnoreCase)))
                {
                    c.Write("Objects selected to be arrayed may not be selected as path curve.");
                    c.Write("1 was filtered out");
                    continue;
                }

                curve = c.Document.FindEntity(handle) as DxfCurve;
                total =
                    curve == null || curve is DxfXline || curve is DxfRay || curve is DxfLeader
                        ? 0.0
                        : CurveLength(curve);

                if (!(total > 0.0))
                {
                    c.Write("1 was filtered out");
                    continue;
                }

                pickedAt = c.Input.IsPickedAt ? c.Input.Point : (GeoPoint2?)null;
                break;
            }

            bool closed = curve.Closed || curve.StartPoint.DistanceTo(curve.EndPoint) <= 1E-10 * Math.Max(1.0, total);
            bool fromEnd =
                !closed
                && pickedAt.HasValue
                && Flat(curve.EndPoint).DistanceTo(pickedAt.Value) < Flat(curve.StartPoint).DistanceTo(pickedAt.Value);
            var array = new CadPathArray(curve, total, closed, fromEnd);
            GeoAabb3 box = Extents(c, c.Selected);
            double wide =
                box.IsEmpty || !(box.Max.X - box.Min.X > c.Session.Tolerance.EqualPoint)
                    ? 1.0
                    : 1.5 * (box.Max.X - box.Min.X);
            double high =
                box.IsEmpty || !(box.Max.Y - box.Min.Y > c.Session.Tolerance.EqualPoint)
                    ? 1.0
                    : 1.5 * (box.Max.Y - box.Min.Y);
            array.Spacing = wide;
            array.Items = array.MaxItems(wide);
            array.RowSpacing = high;
            var associative = new Ref<bool>();
            bool maintainZ = true;
            var count = new Ref<int?>();
            var distance = new Ref<double?>();

            while (true)
            {
                // The array as it stands, as AutoCAD 2021 shows it while its grips and options are offered.
                yield return CadPrompt
                    .ForPoint("Select grip to edit array", null, PathOptions, "eXit")
                    .WithDefaultTight()
                    .Previewing(CadPreview.Moving(c, c.Selected, input => array.Moves().ConvertAll(CadPreview.Flat)));

                if (c.Input.IsPoint)
                {
                    c.Write("Grips of an array are not built: use the options.");
                    continue;
                }

                if (c.Input.Is("ASsociative"))
                {
                    foreach (CadPrompt prompt in Associative(c, associative))
                    {
                        yield return prompt;
                    }

                    continue;
                }

                if (c.Input.Is("Method"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Enter path method",
                        new[] { "Divide", "Measure" },
                        array.Divide ? "Divide" : "Measure"
                    );

                    if (c.Input.Kind == CadInputKind.Keyword)
                    {
                        array.Divide = c.Input.Is("Divide");
                        array.Items = array.MaxItems(array.Spacing);
                    }

                    continue;
                }

                if (c.Input.Is("Base point"))
                {
                    yield return CadPrompt.ForPoint(
                        "Specify base point or",
                        null,
                        new[] { "Key point" },
                        "end of path curve"
                    );

                    if (c.Input.IsPoint)
                    {
                        array.Base = P3(c.Input.Point);
                    }

                    continue;
                }

                if (c.Input.Is("Tangent direction"))
                {
                    yield return CadPrompt.ForPoint(
                        "Specify first point of tangent direction vector or",
                        null,
                        new[] { "Normal" }
                    );

                    if (c.Input.Is("Normal"))
                    {
                        array.Tangent = CadPathTangent.Normal;
                    }
                    else if (c.Input.IsPoint)
                    {
                        GeoPoint2 first = c.Input.Point;
                        yield return CadPrompt.ForPoint("Specify second point of tangent direction vector", first);

                        if (c.Input.IsPoint && c.Input.Point.DistanceTo(first) > c.Session.Tolerance.EqualPoint)
                        {
                            GeoVector2 way = c.Input.Point - first;
                            array.Tangent = CadPathTangent.Vector;
                            array.TangentAngle = Math.Atan2(way.Y, way.X);
                        }
                    }

                    continue;
                }

                if (c.Input.Is("Items"))
                {
                    if (array.Divide)
                    {
                        foreach (CadPrompt prompt in Count(c, "Enter number of items along path", array.Items, count))
                        {
                            yield return prompt;
                        }

                        array.Items = count.Value ?? array.Items;
                        continue;
                    }

                    yield return CadPrompt.ForDistance(
                        "Specify the distance between items along path",
                        null,
                        new[] { "Expression" },
                        Length(array.Spacing)
                    );

                    if (c.Input.Kind == CadInputKind.Number && c.Input.Number > c.Session.Tolerance.EqualPoint)
                    {
                        array.Spacing = c.Input.Number;
                    }

                    int most = array.MaxItems(array.Spacing);
                    c.Write("Maximum items = " + most.ToString(CultureInfo.InvariantCulture));
                    yield return CadPrompt
                        .ForInteger(
                            "Specify number of items",
                            new[] { "Fill entire path", "Expression" },
                            most.ToString(CultureInfo.InvariantCulture)
                        )
                        .Refusing("Requires a positive nonzero integer or option keyword.");
                    array.Items =
                        c.Input.Kind == CadInputKind.Number && c.Input.Number >= 1.0
                            ? (int)Math.Min(c.Input.Number, most)
                            : most;
                    continue;
                }

                if (c.Input.Is("Rows"))
                {
                    foreach (CadPrompt prompt in Count(c, "Enter the number of rows", array.Rows, count))
                    {
                        yield return prompt;
                    }

                    array.Rows = count.Value ?? array.Rows;

                    foreach (
                        CadPrompt prompt in Spacing(
                            c,
                            "Specify the distance between rows",
                            "Enter total distance between start and end rows",
                            array.RowSpacing,
                            array.Rows,
                            distance
                        )
                    )
                    {
                        yield return prompt;
                    }

                    array.RowSpacing = distance.Value ?? array.RowSpacing;
                    yield return CadPrompt.ForDistance(
                        "Specify the incrementing elevation between rows",
                        null,
                        new[] { "Expression" },
                        Length(array.Elevation)
                    );
                    array.Elevation = c.Input.Kind == CadInputKind.Number ? c.Input.Number : array.Elevation;
                    continue;
                }

                if (c.Input.Is("Levels"))
                {
                    foreach (CadPrompt prompt in Count(c, "Enter the number of levels", array.Levels, count))
                    {
                        yield return prompt;
                    }

                    array.Levels = count.Value ?? array.Levels;

                    foreach (
                        CadPrompt prompt in Spacing(
                            c,
                            "Specify the distance between levels",
                            "Enter total distance between start and end levels",
                            array.LevelSpacing,
                            array.Levels,
                            distance
                        )
                    )
                    {
                        yield return prompt;
                    }

                    array.LevelSpacing = distance.Value ?? array.LevelSpacing;
                    continue;
                }

                if (c.Input.Is("Align items"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Align arrayed items to path?",
                        new[] { "Yes", "No" },
                        array.Align ? "Yes" : "No"
                    );
                    array.Align = c.Input.Kind == CadInputKind.Keyword ? c.Input.Is("Yes") : array.Align;
                    continue;
                }

                if (c.Input.Is("Z direction"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Maintain Z direction for all items in the array?",
                        new[] { "Yes", "No" },
                        "Yes"
                    );

                    maintainZ = !c.Input.Is("No");

                    if (c.Input.Is("No"))
                    {
                        c.Write("A path in the plan carries no turn about its own direction: the items keep their Z.");
                    }

                    continue;
                }

                if (c.Input.Is("eXit") || c.Input.IsNone)
                {
                    break;
                }

                yield break;
            }

            if (associative.Value && !CadArrays.CanBePath(curve))
            {
                associative.Value = false;
                c.Write("The path is not a line, arc, circle or polyline: the items are made as separate objects.");
            }

            if (associative.Value && CadCommands.MakeAssociative(c, () => new CadAssocArray
                    {
                        Kind = CadArrayKind.Path,
                        Origin = GeoPoint3.Origin,
                        BasePoint = array.Base ?? curve.GetPointAtDist(fromEnd ? total : 0.0),
                        Path = curve,
                        PathLength = total,
                        PathClosed = closed,
                        FromEnd = array.FromEnd,
                        Divide = array.Divide,
                        Spacing = array.Spacing,
                        Items = array.Items,
                        Tangent = array.Tangent,
                        TangentAngle = array.TangentAngle,
                        Align = array.Align,
                        MaintainZ = maintainZ,
                        Rows = array.Rows,
                        RowSpacing = array.RowSpacing,
                        RowElevation = array.Elevation,
                        Levels = array.Levels,
                        LevelSpacing = array.LevelSpacing,
                    }))
            {
                yield break;
            }

            PathCopies(c, c.Selected, array.Moves());
        }

        /// <summary>
        /// Makes the items of a path array in one edit: a copy of each object for each transformation but the first, and the
        /// objects themselves moved by the first where it is not the identity (a base point away from the path's start).
        /// </summary>
        private static void PathCopies(CadCommandContext c, IReadOnlyList<string> handles, List<GeoTransform3> moves)
        {
            if (moves.Count == 0)
            {
                return;
            }

            bool moveFirst = !moves[0].IsIdentity(c.Session.Tolerance);
            List<CadEditRecord> planned = null;

            if (moveFirst)
            {
                planned = new List<CadEditRecord>();

                foreach (string handle in handles)
                {
                    planned.Add(new CadEditRecord(CadEditRecordKind.Transformed, handle, "Move", null, null));
                }
            }
            else if (moves.Count == 1)
            {
                return;
            }

            c.Apply(planned, s =>
            {
                for (int k = 1; k < moves.Count; k++)
                {
                    foreach (string handle in handles)
                    {
                        AppendCopy(s, handle, moves[k]);
                    }
                }

                if (moveFirst)
                {
                    foreach (string handle in handles)
                    {
                        s.Document.FindEntity(handle).TransformBy(moves[0]);
                    }
                }
            });
        }
    }
}
