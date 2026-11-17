using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// ARRAY, ARRAYRECT, ARRAYPOLAR and -ARRAY as AutoCAD 2021 asks them on the command line, each making its copies as
    /// separate objects, or, where ASsociative is answered Yes, one associative array (<see cref="CadArrays"/>; the path type,
    /// ARRAYPATH, is in <c>CadCommands.PathArray.cs</c> and ARRAYEDIT in <c>CadCommands.ArrayEdit.cs</c>). -ARRAY makes separate
    /// objects only. The array is made when the options end (eXit or Enter), one undo step; Esc makes none.
    /// </summary>
    internal static partial class CadCommands
    {
        // The most rows, columns, items or levels an array takes, as AutoCAD 2021 refuses more.
        private const double MostItems = 32767.0;

        private static readonly string[] RectangularOptions =
        {
            "ASsociative",
            "Base point",
            "COUnt",
            "Spacing",
            "COLumns",
            "Rows",
            "Levels",
            "eXit",
        };

        private static readonly string[] PolarOptions =
        {
            "ASsociative",
            "Base point",
            "Items",
            "Angle between",
            "Fill angle",
            "ROWs",
            "Levels",
            "ROTate items",
            "eXit",
        };

        private static IEnumerable<CadPrompt> ArrayAny(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForKeyword(
                "Enter array type",
                new[] { "Rectangular", "PAth", "POlar" },
                "Rectangular"
            );

            IEnumerable<CadPrompt> rest =
                c.Input.Is("PAth") ? PathArray(c)
                : c.Input.Is("POlar") ? PolarArray(c)
                : c.Input.Kind == CadInputKind.Keyword ? RectangularArray(c)
                : null;

            if (rest == null)
            {
                yield break;
            }

            foreach (CadPrompt prompt in rest)
            {
                yield return prompt;
            }
        }

        private static IEnumerable<CadPrompt> ArrayRectangular(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            foreach (CadPrompt prompt in RectangularArray(c))
            {
                yield return prompt;
            }
        }

        private static IEnumerable<CadPrompt> ArrayPolar(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            foreach (CadPrompt prompt in PolarArray(c))
            {
                yield return prompt;
            }
        }

        /// <summary>
        /// The rectangular array of the objects selected: 4 columns and 3 rows until changed, a column and a row apart by half
        /// as much again as the objects' box is wide and high, along the axes of the UCS.
        /// </summary>
        private static IEnumerable<CadPrompt> RectangularArray(CadCommandContext c)
        {
            c.Write("Type = Rectangular  Associative = No");
            GeoAabb3 box = Extents(c, c.Selected);
            int columns = 4;
            int rows = 3;
            double across =
                box.IsEmpty || !(box.Max.X - box.Min.X > c.Session.Tolerance.EqualPoint)
                    ? 1.0
                    : 1.5 * (box.Max.X - box.Min.X);
            double up =
                box.IsEmpty || !(box.Max.Y - box.Min.Y > c.Session.Tolerance.EqualPoint)
                    ? 1.0
                    : 1.5 * (box.Max.Y - box.Min.Y);
            var associative = new Ref<bool>();
            GeoPoint2? baseAt = null;
            double elevation = 0.0;
            var count = new Ref<int?>();
            var distance = new Ref<double?>();

            // The levels asked and how far apart: the array is made in the plan (Planar says so), so only the prompts follow AutoCAD.
            int levels = 1;
            double deep = 1.0;

            // The moves of the copies, each column and row but the first, along the axes of the UCS.
            List<GeoTransform3> Moves()
            {
                var moves = new List<GeoTransform3>();

                for (int row = 0; row < rows; row++)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        if (row == 0 && column == 0)
                        {
                            continue;
                        }

                        GeoVector2 step = c.Session.Ucs.ToWorld(new GeoVector2(column * across, row * up));
                        moves.Add(GeoTransform3.Translation(new GeoVector3(step.X, step.Y, 0.0)));
                    }
                }

                return moves;
            }

            while (true)
            {
                // The array as it stands, as AutoCAD 2021 shows it while its grips and options are offered.
                yield return CadPrompt
                    .ForPoint("Select grip to edit array", null, RectangularOptions, "eXit")
                    .WithDefaultTight()
                    .Previewing(CadPreview.Moving(c, c.Selected, input => Moves().ConvertAll(CadPreview.Flat)));

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

                if (c.Input.Is("Base point"))
                {
                    yield return CadPrompt.ForPoint("Specify base point or", null, new[] { "Key point" }, "centroid");

                    if (c.Input.IsPoint)
                    {
                        baseAt = c.Input.Point;
                    }

                    continue;
                }

                if (c.Input.Is("COUnt") || c.Input.Is("COLumns"))
                {
                    bool both = c.Input.Is("COUnt");

                    foreach (CadPrompt prompt in Count(c, "Enter the number of columns", columns, count))
                    {
                        yield return prompt;
                    }

                    columns = count.Value ?? columns;

                    if (both)
                    {
                        foreach (CadPrompt prompt in Count(c, "Enter the number of rows", rows, count))
                        {
                            yield return prompt;
                        }

                        rows = count.Value ?? rows;
                    }
                    else
                    {
                        foreach (
                            CadPrompt prompt in Spacing(
                                c,
                                "Specify the distance between columns",
                                "Enter total distance between start and end columns",
                                across,
                                columns,
                                distance
                            )
                        )
                        {
                            yield return prompt;
                        }

                        across = distance.Value ?? across;
                    }

                    continue;
                }

                if (c.Input.Is("Rows"))
                {
                    foreach (CadPrompt prompt in Count(c, "Enter the number of rows", rows, count))
                    {
                        yield return prompt;
                    }

                    rows = count.Value ?? rows;

                    foreach (
                        CadPrompt prompt in Spacing(
                            c,
                            "Specify the distance between rows",
                            "Enter total distance between start and end rows",
                            up,
                            rows,
                            distance
                        )
                    )
                    {
                        yield return prompt;
                    }

                    up = distance.Value ?? up;
                    yield return CadPrompt.ForDistance(
                        "Specify the incrementing elevation between rows",
                        null,
                        new[] { "Expression" },
                        Length(0.0)
                    );
                    elevation = c.Input.Kind == CadInputKind.Number ? c.Input.Number : elevation;
                    Planar(c, 0.0);
                    continue;
                }

                if (c.Input.Is("Spacing"))
                {
                    yield return CadPrompt.ForDistance(
                        "Specify the distance between columns",
                        null,
                        new[] { "Unit cell" },
                        Length(across)
                    );

                    if (c.Input.Is("Unit cell"))
                    {
                        yield return CadPrompt.ForPoint("Specify first corner");

                        if (!c.Input.IsPoint)
                        {
                            continue;
                        }

                        GeoPoint2 corner = c.Input.Point;
                        yield return CadPrompt.ForPoint("Specify other corner", corner);

                        if (c.Input.IsPoint)
                        {
                            GeoVector2 cell = c.Session.Ucs.ToUcs(c.Input.Point - corner);
                            across = cell.X;
                            up = cell.Y;
                        }

                        continue;
                    }

                    across = c.Input.Kind == CadInputKind.Number ? c.Input.Number : across;
                    yield return CadPrompt.ForDistance("Specify the distance between rows", null, null, Length(up));
                    up = c.Input.Kind == CadInputKind.Number ? c.Input.Number : up;
                    continue;
                }

                if (c.Input.Is("Levels"))
                {
                    foreach (CadPrompt prompt in Count(c, "Enter the number of levels", levels, count))
                    {
                        yield return prompt;
                    }

                    levels = count.Value ?? levels;
                    Planar(c, 1.0);

                    foreach (
                        CadPrompt prompt in Spacing(
                            c,
                            "Specify the distance between levels",
                            "Enter total distance between start and end levels",
                            deep,
                            levels,
                            distance
                        )
                    )
                    {
                        yield return prompt;
                    }

                    deep = distance.Value ?? deep;
                    continue;
                }

                if (c.Input.Is("eXit") || c.Input.IsNone)
                {
                    break;
                }

                yield break;
            }

            if (associative.Value && CadCommands.MakeAssociative(c, () =>
                {
                    GeoPoint3 at = baseAt.HasValue ? P3(baseAt.Value) : CentreOf(c);
                    GeoVector2 along = c.Session.Ucs.ToWorld(new GeoVector2(1.0, 0.0));
                    return new CadAssocArray
                    {
                        Kind = CadArrayKind.Rectangular,
                        Origin = at,
                        BasePoint = at,
                        Columns = columns,
                        ColumnSpacing = across,
                        Rows = rows,
                        RowSpacing = up,
                        RowElevation = elevation,
                        Levels = levels,
                        LevelSpacing = deep,
                        RowAngle = Math.Atan2(along.Y, along.X),
                    };
                }))
            {
                yield break;
            }

            Copies(c, c.Selected, Moves());
        }

        /// <summary>
        /// The polar array of the objects selected about a centre: 6 items filling a whole turn until changed, each turned with
        /// the array, the base point the middle of the objects' box.
        /// </summary>
        private static IEnumerable<CadPrompt> PolarArray(CadCommandContext c)
        {
            c.Write("Type = Polar  Associative = No");
            GeoAabb3 box = Extents(c, c.Selected);
            var basePoint = box.IsEmpty
                ? GeoPoint2.Origin
                : new GeoPoint2((box.Min.X + box.Max.X) / 2.0, (box.Min.Y + box.Max.Y) / 2.0);
            GeoPoint2 centre;

            while (true)
            {
                yield return CadPrompt.ForPoint(
                    "Specify center point of array",
                    null,
                    new[] { "Base point", "Axis of rotation" }
                );

                if (c.Input.Is("Base point"))
                {
                    yield return CadPrompt.ForPoint("Specify base point or", null, new[] { "Key point" }, "centroid");

                    if (c.Input.IsPoint)
                    {
                        basePoint = c.Input.Point;
                    }

                    continue;
                }

                if (c.Input.Is("Axis of rotation"))
                {
                    c.Write("An axis of rotation is of 3D: the array turns about the plan's Z axis.");
                    continue;
                }

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                centre = c.Input.Point;
                break;
            }

            int items = 6;
            double step = Math.PI / 3.0;
            int rows = 1;
            double apart = 1.0;
            int levels = 1;
            double deep = 1.0;
            bool turn = true;
            var associative = new Ref<bool>();
            double elevation = 0.0;
            var count = new Ref<int?>();
            var distance = new Ref<double?>();

            while (true)
            {
                // The array as it stands, as AutoCAD 2021 shows it while its grips and options are offered.
                yield return CadPrompt
                    .ForPoint("Select grip to edit array", null, PolarOptions, "eXit")
                    .WithDefaultTight()
                    .Previewing(
                        CadPreview.Moving(c, c.Selected, input =>
                            PolarMoves(centre, basePoint, items, step, rows, apart, turn).ConvertAll(CadPreview.Flat)
                        )
                    );

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

                if (c.Input.Is("Base point"))
                {
                    yield return CadPrompt.ForPoint("Specify base point or", null, new[] { "Key point" }, "centroid");

                    if (c.Input.IsPoint)
                    {
                        basePoint = c.Input.Point;
                    }

                    continue;
                }

                if (c.Input.Is("Items"))
                {
                    double fill = Fill(items, step);

                    foreach (CadPrompt prompt in Count(c, "Enter number of items in array", items, count))
                    {
                        yield return prompt;
                    }

                    items = count.Value ?? items;
                    step = StepOf(items, fill);
                    continue;
                }

                if (c.Input.Is("Angle between"))
                {
                    yield return CadPrompt
                        .ForAngle(
                            "Specify angle between items",
                            null,
                            new[] { "Expression" },
                            Number(step * 180.0 / Math.PI)
                        )
                        .Turning();
                    step = c.Input.Kind == CadInputKind.Number ? c.Input.Number : step;
                    continue;
                }

                if (c.Input.Is("Fill angle"))
                {
                    yield return CadPrompt
                        .ForAngle(
                            "Specify the angle to fill " + FillWays(c),
                            null,
                            new[] { "EXpression" },
                            Number(Fill(items, step) * 180.0 / Math.PI)
                        )
                        .Turning();

                    if (c.Input.Kind == CadInputKind.Number)
                    {
                        step = StepOf(items, c.Input.Number);
                    }

                    continue;
                }

                if (c.Input.Is("ROWs"))
                {
                    foreach (CadPrompt prompt in Count(c, "Enter the number of rows", rows, count))
                    {
                        yield return prompt;
                    }

                    rows = count.Value ?? rows;

                    foreach (
                        CadPrompt prompt in Spacing(
                            c,
                            "Specify the distance between rows",
                            "Enter total distance between start and end rows",
                            apart,
                            rows,
                            distance
                        )
                    )
                    {
                        yield return prompt;
                    }

                    apart = distance.Value ?? apart;
                    yield return CadPrompt.ForDistance(
                        "Specify the incrementing elevation between rows",
                        null,
                        new[] { "Expression" },
                        Length(0.0)
                    );
                    elevation = c.Input.Kind == CadInputKind.Number ? c.Input.Number : elevation;
                    Planar(c, 0.0);
                    continue;
                }

                if (c.Input.Is("Levels"))
                {
                    foreach (CadPrompt prompt in Count(c, "Enter the number of levels", levels, count))
                    {
                        yield return prompt;
                    }

                    levels = count.Value ?? levels;
                    Planar(c, 1.0);

                    foreach (
                        CadPrompt prompt in Spacing(
                            c,
                            "Specify the distance between levels",
                            "Enter total distance between start and end levels",
                            deep,
                            levels,
                            distance
                        )
                    )
                    {
                        yield return prompt;
                    }

                    deep = distance.Value ?? deep;
                    continue;
                }

                if (c.Input.Is("ROTate items"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Rotate arrayed items?",
                        new[] { "Yes", "No" },
                        turn ? "Yes" : "No"
                    );
                    turn = c.Input.Kind == CadInputKind.Keyword ? c.Input.Is("Yes") : turn;
                    continue;
                }

                if (c.Input.Is("eXit") || c.Input.IsNone)
                {
                    break;
                }

                yield break;
            }

            if (associative.Value && CadCommands.MakeAssociative(c, () => new CadAssocArray
                    {
                        Kind = CadArrayKind.Polar,
                        Origin = P3(centre),
                        BasePoint = P3(basePoint),
                        Items = items,
                        Between = Math.Abs(step),
                        Clockwise = step < 0.0,
                        Rotate = turn,
                        Rows = rows,
                        RowSpacing = apart,
                        RowElevation = elevation,
                        Levels = levels,
                        LevelSpacing = deep,
                    }))
            {
                yield break;
            }

            Copies(c, c.Selected, PolarMoves(centre, basePoint, items, step, rows, apart, turn));
        }

        /// <summary>-ARRAY: AutoCAD's older array on the command line, Rectangular by rows and columns or Polar by items and the angle to fill.</summary>
        private static IEnumerable<CadPrompt> ArrayTyped(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForKeyword("Enter the type of array", new[] { "Rectangular", "Polar" }, "R");

            if (c.Input.Is("Polar"))
            {
                GeoAabb3 box = Extents(c, c.Selected);
                var basePoint = box.IsEmpty
                    ? GeoPoint2.Origin
                    : new GeoPoint2((box.Min.X + box.Max.X) / 2.0, (box.Min.Y + box.Max.Y) / 2.0);

                while (true)
                {
                    yield return CadPrompt.ForPoint("Specify center point of array", null, new[] { "Base" });

                    if (!c.Input.Is("Base"))
                    {
                        break;
                    }

                    yield return CadPrompt.ForPoint("Specify base point of objects");

                    if (c.Input.IsPoint)
                    {
                        basePoint = c.Input.Point;
                    }
                }

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 centre = c.Input.Point;
                var counted = new Ref<int?>();

                foreach (CadPrompt prompt in WholeCount(c, "Enter the number of items in the array", null, counted))
                {
                    yield return prompt;
                }

                int items = counted.Value ?? 0;

                // The copies the angle to the cursor from the centre would make, turned as they are unless No is answered after.
                CadPrompt filling = CadPrompt
                    .ForAngle("Specify the angle to fill " + FillWays(c), centre, null, "360")
                    .Turning();
                yield return items < 2
                    ? filling
                    : filling.Previewing(
                        CadPreview.Moving(
                            c,
                            c.Selected,
                            input =>
                                PolarMoves(centre, basePoint, items, StepOf(items, input.Number), 1, 0.0, true)
                                    .ConvertAll(CadPreview.Flat),
                            true
                        )
                    );
                double fill = c.Input.Kind == CadInputKind.Number ? c.Input.Number : 2.0 * Math.PI;
                double step;

                if (items <= 0)
                {
                    yield return CadPrompt
                        .ForAngle("Angle between items", centre)
                        .Turning()
                        .Previewing(
                            CadPreview.Moving(
                                c,
                                c.Selected,
                                input =>
                                {
                                    if (Math.Abs(input.Number) <= Tolerance.Default.EqualAngleRad)
                                    {
                                        return null;
                                    }

                                    double between = input.Number * Math.Sign(fill == 0.0 ? 1.0 : fill);
                                    return PolarMoves(centre, basePoint, Between(fill, between), between, 1, 0.0, true)
                                        .ConvertAll(CadPreview.Flat);
                                },
                                true
                            )
                        );

                    if (
                        c.Input.Kind != CadInputKind.Number
                        || Math.Abs(c.Input.Number) <= Tolerance.Default.EqualAngleRad
                    )
                    {
                        c.Write("Requires an angle between items other than nought.");
                        yield break;
                    }

                    step = c.Input.Number * Math.Sign(fill == 0.0 ? 1.0 : fill);
                    items = Between(fill, step);
                }
                else
                {
                    step = StepOf(items, fill);
                }

                if (items < 2)
                {
                    c.Write("One-element array, nothing to do.");
                    yield break;
                }

                List<Affine2> turned = PolarMoves(centre, basePoint, items, step, 1, 0.0, true)
                    .ConvertAll(CadPreview.Flat);
                yield return CadPrompt
                    .ForKeyword("Rotate arrayed objects?", new[] { "Yes", "No" }, "Y")
                    .Previewing(CadPreview.Moving(c, c.Selected, input => turned));
                Copies(c, c.Selected, PolarMoves(centre, basePoint, items, step, 1, 0.0, !c.Input.Is("No")));
                yield break;
            }

            if (!c.Input.Is("Rectangular"))
            {
                yield break;
            }

            var asked = new Ref<int?>();

            foreach (CadPrompt prompt in WholeCount(c, "Enter the number of rows (---)", "1", asked))
            {
                yield return prompt;
            }

            int rows = asked.Value ?? 1;

            foreach (CadPrompt prompt in WholeCount(c, "Enter the number of columns (|||)", "1", asked))
            {
                yield return prompt;
            }

            int columns = asked.Value ?? 1;

            if (rows == 1 && columns == 1)
            {
                c.Write("One-element array, nothing to do.");
                yield break;
            }

            double up = 0.0;
            double across = 0.0;
            bool cell = false;

            if (rows > 1)
            {
                yield return CadPrompt.ForPoint(
                    "Enter the distance between rows or specify unit cell (---)",
                    null,
                    null,
                    null,
                    true
                );

                if (c.Input.IsPoint)
                {
                    GeoPoint2 corner = c.Input.Point;

                    // The unit cell alone, a rectangle square to the UCS from the first corner to the cursor, as AutoCAD's
                    // corner prompt draws it: no copies.
                    CadUcs ucs = c.Session.Ucs;
                    yield return CadPrompt
                        .ForPoint("Specify opposite corner", corner)
                        .Previewing(CadPreview.MakingOne(c, input =>
                            {
                                GeoPoint2 from = ucs.ToUcs(corner);
                                GeoPoint2 to = ucs.ToUcs(input.Point);
                                var cell = new DxfPolyline();
                                cell.AddVertexAt(0, ucs.ToWorld(from), 0.0, 0.0, 0.0);
                                cell.AddVertexAt(1, ucs.ToWorld(new GeoPoint2(to.X, from.Y)), 0.0, 0.0, 0.0);
                                cell.AddVertexAt(2, ucs.ToWorld(to), 0.0, 0.0, 0.0);
                                cell.AddVertexAt(3, ucs.ToWorld(new GeoPoint2(from.X, to.Y)), 0.0, 0.0, 0.0);
                                cell.Closed = true;
                                return Current(c, cell);
                            }));

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    GeoVector2 size = c.Session.Ucs.ToUcs(c.Input.Point - corner);
                    across = size.X;
                    up = size.Y;
                    cell = true;
                }
                else if (
                    c.Input.Kind != CadInputKind.Text
                    || !CadUnits.TryLength(c.Input.Text, c.Session.Document.Database.Lunits, out up)
                )
                {
                    c.Write("Requires a distance or two points.");
                    yield break;
                }
            }

            if (columns > 1 && !cell)
            {
                yield return CadPrompt.ForPoint("Specify the distance between columns (|||)", null, null, null, true);

                if (c.Input.IsPoint)
                {
                    // The distance shown by two points: the copies it would make to the cursor, the rows as far apart as typed.
                    double typed = up;
                    yield return CadPrompt
                        .ForDistance("Specify second point", c.Input.Point)
                        .Previewing(
                            CadPreview.Moving(
                                c,
                                c.Selected,
                                input =>
                                    RectangularMoves(c, rows, columns, input.Number, typed).ConvertAll(CadPreview.Flat),
                                true
                            )
                        );

                    if (c.Input.Kind != CadInputKind.Number)
                    {
                        yield break;
                    }

                    across = c.Input.Number;
                }
                else if (
                    c.Input.Kind != CadInputKind.Text
                    || !CadUnits.TryLength(c.Input.Text, c.Session.Document.Database.Lunits, out across)
                )
                {
                    yield break;
                }
            }

            Copies(c, c.Selected, RectangularMoves(c, rows, columns, across, up));
        }

        /// <summary>
        /// The moves of the copies of a rectangular array of -ARRAY, the columns and rows apart by distances along the UCS; the
        /// item in place not moved. Three rows of two columns, 50 across and 30 up, give five copies.
        /// </summary>
        private static List<GeoTransform3> RectangularMoves(
            CadCommandContext c,
            int rows,
            int columns,
            double across,
            double up
        )
        {
            var moves = new List<GeoTransform3>();

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    if (row == 0 && column == 0)
                    {
                        continue;
                    }

                    GeoVector2 step = c.Session.Ucs.ToWorld(new GeoVector2(column * across, row * up));
                    moves.Add(GeoTransform3.Translation(new GeoVector3(step.X, step.Y, 0.0)));
                }
            }

            return moves;
        }

        /// <summary>The ways a fill angle turns, as AutoCAD says them: a number above nought is clockwise with ANGDIR 1.</summary>
        private static string FillWays(CadCommandContext c) => c.Database.Angdir ? "(+=cw, -=ccw)" : "(+=ccw, -=cw)";

        /// <summary>The items of a polar array filling an angle a given angle apart: a whole turn by 60 degrees has six, a half turn seven.</summary>
        private static int Between(double fill, double step)
        {
            bool whole = Math.Abs(Math.Abs(fill) - 2.0 * Math.PI) <= Tolerance.Default.EqualAngleRad;
            return (int)Math.Floor(Math.Abs(fill / step) + 1E-9) + (whole ? 0 : 1);
        }

        /// <summary>The angle a polar array fills: a whole turn its items share evenly, else as far as its last item.</summary>
        private static double Fill(int items, double step)
        {
            double whole = step * items;
            return Math.Abs(Math.Abs(whole) - 2.0 * Math.PI) <= 1E-9 ? whole : step * (items - 1);
        }

        /// <summary>The angle between the items of a polar array filling an angle: a whole turn shared by them all, else from the first to the last.</summary>
        private static double StepOf(int items, double fill)
        {
            bool whole = Math.Abs(Math.Abs(fill) - 2.0 * Math.PI) <= Tolerance.Default.EqualAngleRad;
            return whole || items < 2 ? fill / items : fill / (items - 1);
        }

        /// <summary>
        /// The moves of the copies of a polar array: item by item round the centre, row by row out from it along the way from the
        /// centre to the base point; turned with the array, or moved round with the base point upright.
        /// </summary>
        private static List<GeoTransform3> PolarMoves(
            GeoPoint2 centre,
            GeoPoint2 basePoint,
            int items,
            double step,
            int rows,
            double apart,
            bool turn
        )
        {
            GeoVector2 outward = basePoint - centre;
            outward =
                outward.Length > Tolerance.Default.EqualPoint ? outward / outward.Length : new GeoVector2(1.0, 0.0);
            var moves = new List<GeoTransform3>();

            for (int row = 0; row < rows; row++)
            {
                GeoVector2 off = outward * (row * apart);
                GeoTransform3 outwards = GeoTransform3.Translation(new GeoVector3(off.X, off.Y, 0.0));

                for (int item = 0; item < items; item++)
                {
                    if (row == 0 && item == 0)
                    {
                        continue;
                    }

                    double angle = item * step;

                    if (turn)
                    {
                        moves.Add(GeoTransform3.RotationAxis(P3(centre), Up, angle) * outwards);
                    }
                    else
                    {
                        GeoPoint2 from = basePoint + off;
                        GeoVector2 radius = from - centre;
                        var to = new GeoPoint2(
                            centre.X + radius.X * Math.Cos(angle) - radius.Y * Math.Sin(angle),
                            centre.Y + radius.X * Math.Sin(angle) + radius.Y * Math.Cos(angle)
                        );
                        moves.Add(
                            GeoTransform3.Translation(new GeoVector3(to.X - basePoint.X, to.Y - basePoint.Y, 0.0))
                        );
                    }
                }
            }

            return moves;
        }

        /// <summary>Makes a copy of each object for each move, as separate objects, one edit; the copies of an owner's entity are the presenter's own.</summary>
        private static void Copies(CadCommandContext c, IReadOnlyList<string> handles, List<GeoTransform3> moves)
        {
            if (moves.Count == 0)
            {
                return;
            }

            c.Apply(null, s =>
            {
                foreach (GeoTransform3 move in moves)
                {
                    foreach (string handle in handles)
                    {
                        AppendCopy(s, handle, move);
                    }
                }
            });
        }

        /// <summary>
        /// The ASsociative option: whether the array is made as one associative array (Yes), its objects kept in an anonymous
        /// block with the parameters in the drawing's network, or as separate objects (No, the first answer). A drawing older
        /// than R2010 holds no associations: Yes is answered by saying so, and the items are made as separate objects.
        /// </summary>
        private static IEnumerable<CadPrompt> Associative(CadCommandContext c, Ref<bool> associative)
        {
            yield return CadPrompt.ForKeyword(
                "Create associative array",
                new[] { "Yes", "No" },
                associative.Value ? "Yes" : "No"
            );

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            associative.Value = c.Input.Is("Yes");

            if (associative.Value && !CadArrays.CanTie(c.Database))
            {
                associative.Value = false;
                c.Write("Associative arrays need a drawing of R2010 or later: the items are made as separate objects.");
            }
        }

        /// <summary>Says that an elevation or a number of levels asked is of 3D, where a value other than the plain one (nought, or one level) was given.</summary>
        private static void Planar(CadCommandContext c, double plain)
        {
            if (
                c.Input.Kind == CadInputKind.Number
                && Math.Abs(c.Input.Number - plain) > c.Session.Tolerance.EqualPoint
            )
            {
                c.Write("Levels and elevations are of 3D: the array is made in the plan.");
            }
        }

        /// <summary>
        /// Asks a number of columns, rows, items or levels, one or more up to 32767, as ARRAYRECT, ARRAYPOLAR and ARRAYPATH ask
        /// it: text that is no whole number, or 2.5, is "Requires a positive nonzero integer or option keyword.", nought or
        /// less "Value must be positive and nonzero.", more than 32767 "Requires an integer between 1 and 32767, or option
        /// keyword.", and it is asked again each time. The result is null where Enter or a keyword answered.
        /// </summary>
        private static IEnumerable<CadPrompt> Count(CadCommandContext c, string message, int now, Ref<int?> result)
        {
            result.Value = null;

            while (true)
            {
                yield return CadPrompt
                    .ForInteger(message, new[] { "Expression" }, now.ToString(CultureInfo.InvariantCulture))
                    .Refusing("Requires a positive nonzero integer or option keyword.");

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                if (c.Input.Number < 1.0)
                {
                    c.Write("Value must be positive and nonzero.");
                }
                else if (c.Input.Number > MostItems)
                {
                    c.Write("Requires an integer between 1 and 32767, or option keyword.");
                }
                else
                {
                    result.Value = (int)c.Input.Number;
                    yield break;
                }
            }
        }

        /// <summary>
        /// Asks a number of rows, columns or items of -ARRAY, one or more up to 32767: anything else is "Requires an integer
        /// between 1 and 32767." and is asked again. The result is null where Enter answered.
        /// </summary>
        private static IEnumerable<CadPrompt> WholeCount(
            CadCommandContext c,
            string message,
            string offered,
            Ref<int?> result
        )
        {
            const string refused = "Requires an integer between 1 and 32767.";
            result.Value = null;

            while (true)
            {
                yield return CadPrompt.ForInteger(message, null, offered).Refusing(refused);

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                if (c.Input.Number >= 1.0 && c.Input.Number <= MostItems)
                {
                    result.Value = (int)c.Input.Number;
                    yield break;
                }

                c.Write(refused);
            }
        }

        /// <summary>
        /// Asks the distance between columns, rows or levels, or with Total the distance from the first to the last, "Enter total
        /// distance between start and end rows": nought at either is "Value must be nonzero." and asked again, as AutoCAD 2021
        /// asks it.
        /// </summary>
        private static IEnumerable<CadPrompt> Spacing(
            CadCommandContext c,
            string message,
            string total,
            double now,
            int count,
            Ref<double?> result
        )
        {
            result.Value = null;

            while (true)
            {
                yield return CadPrompt.ForDistance(message, null, new[] { "Total", "Expression" }, Length(now));

                if (c.Input.Is("Total"))
                {
                    // Nought is refused and the total asked again, not the distance between.
                    while (true)
                    {
                        yield return CadPrompt.ForDistance(total, null, null, Length(now * Math.Max(1, count - 1)));

                        if (c.Input.Kind != CadInputKind.Number || !IsNought(c, c.Input.Number))
                        {
                            break;
                        }

                        c.Write("Value must be nonzero.");
                    }

                    if (c.Input.Kind == CadInputKind.Number)
                    {
                        result.Value = count > 1 ? c.Input.Number / (count - 1) : now;
                    }

                    yield break;
                }

                if (c.Input.Kind == CadInputKind.Number && IsNought(c, c.Input.Number))
                {
                    c.Write("Value must be nonzero.");
                    continue;
                }

                if (c.Input.Kind == CadInputKind.Number)
                {
                    result.Value = c.Input.Number;
                }

                yield break;
            }
        }

        private static bool IsNought(CadCommandContext c, double value) =>
            Math.Abs(value) <= c.Session.Tolerance.EqualPoint;

        /// <summary>The box of the objects, those with none left out.</summary>
        private static GeoAabb3 Extents(CadCommandContext c, IReadOnlyList<string> handles)
        {
            GeoAabb3 box = GeoAabb3.Empty;

            foreach (string handle in handles)
            {
                try
                {
                    GeoAabb3 one = c.Document.FindEntity(handle).GeometricExtents;

                    if (!one.IsEmpty)
                    {
                        box = box.IsEmpty ? one : box.Union(one);
                    }
                }
                catch (Exception exception)
                    when (exception is InvalidOperationException || exception is NotSupportedException)
                {
                    GeometryHelperLog.Debug("ARRAY: the box of " + handle + " is left out: " + exception.Message);
                }
            }

            return box;
        }
    }
}
