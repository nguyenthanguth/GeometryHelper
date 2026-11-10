using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// ARRAY, ARRAYRECT, ARRAYPOLAR and -ARRAY as AutoCAD 2021 asks them on the command line, each making its copies as
    /// separate objects, as AutoCAD does with ARRAYASSOCIATIVITY 0: associative arrays and path arrays are not built. The array
    /// is made when the options end (eXit or Enter), one undo step; Esc makes none.
    /// </summary>
    internal static partial class CadCommands
    {
        private static readonly string[] RectangularOptions = { "ASsociative", "Base point", "COUnt", "Spacing", "COLumns", "Rows", "Levels", "eXit" };

        private static readonly string[] PolarOptions = { "ASsociative", "Base point", "Items", "Angle between", "Fill angle", "ROWs", "Levels", "ROTate items", "eXit" };

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

            yield return CadPrompt.ForKeyword("Enter array type", new[] { "Rectangular", "PAth", "POlar" }, "Rectangular");

            if (c.Input.Is("PAth"))
            {
                c.Write("Path arrays are not built yet: use Rectangular or POlar.");
                yield break;
            }

            IEnumerable<CadPrompt> rest = c.Input.Is("POlar") ? PolarArray(c) : c.Input.Kind == CadInputKind.Keyword ? RectangularArray(c) : null;

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
            double across = box.IsEmpty || !(box.Max.X - box.Min.X > c.Session.Tolerance.EqualPoint) ? 1.0 : 1.5 * (box.Max.X - box.Min.X);
            double up = box.IsEmpty || !(box.Max.Y - box.Min.Y > c.Session.Tolerance.EqualPoint) ? 1.0 : 1.5 * (box.Max.Y - box.Min.Y);
            var count = new Ref<int?>();
            var distance = new Ref<double?>();

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
                yield return CadPrompt.ForPoint("Select grip to edit array", null, RectangularOptions, "eXit")
                    .Previewing(CadPreview.Moving(c, c.Selected, input => Moves().ConvertAll(CadPreview.Flat)));

                if (c.Input.IsPoint)
                {
                    c.Write("Grips of an array are not built: use the options.");
                    continue;
                }

                if (c.Input.Is("ASsociative"))
                {
                    foreach (CadPrompt prompt in Associative(c))
                    {
                        yield return prompt;
                    }

                    continue;
                }

                if (c.Input.Is("Base point"))
                {
                    yield return CadPrompt.ForPoint("Specify base point or", null, new[] { "Key point" }, "centroid");
                    continue;
                }

                if (c.Input.Is("COUnt") || c.Input.Is("COLumns"))
                {
                    bool both = c.Input.Is("COUnt");

                    foreach (CadPrompt prompt in Count(c, "Enter number of columns", columns, count))
                    {
                        yield return prompt;
                    }

                    columns = count.Value ?? columns;

                    if (both)
                    {
                        foreach (CadPrompt prompt in Count(c, "Enter number of rows", rows, count))
                        {
                            yield return prompt;
                        }

                        rows = count.Value ?? rows;
                    }
                    else
                    {
                        foreach (CadPrompt prompt in Spacing(c, "Specify the distance between columns", "Specify the distance between first and last column", across, columns, distance))
                        {
                            yield return prompt;
                        }

                        across = distance.Value ?? across;
                    }

                    continue;
                }

                if (c.Input.Is("Rows"))
                {
                    foreach (CadPrompt prompt in Count(c, "Enter number of rows", rows, count))
                    {
                        yield return prompt;
                    }

                    rows = count.Value ?? rows;

                    foreach (CadPrompt prompt in Spacing(c, "Specify the distance between rows", "Specify the distance between first and last row", up, rows, distance))
                    {
                        yield return prompt;
                    }

                    up = distance.Value ?? up;
                    yield return CadPrompt.ForDistance("Specify the incrementing elevation between rows", null, new[] { "Expression" }, Length(0.0));
                    Planar(c, 0.0);
                    continue;
                }

                if (c.Input.Is("Spacing"))
                {
                    yield return CadPrompt.ForDistance("Specify the distance between columns", null, new[] { "Unit cell" }, Length(across));

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
                    yield return CadPrompt.ForInteger("Enter the number of levels", new[] { "Expression" }, "1");
                    Planar(c, 1.0);
                    continue;
                }

                if (c.Input.Is("eXit") || c.Input.IsNone)
                {
                    break;
                }

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
            var basePoint = box.IsEmpty ? GeoPoint2.Origin : new GeoPoint2((box.Min.X + box.Max.X) / 2.0, (box.Min.Y + box.Max.Y) / 2.0);
            GeoPoint2 centre;

            while (true)
            {
                yield return CadPrompt.ForPoint("Specify center point of array", null, new[] { "Base point", "Axis of rotation" });

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
            bool turn = true;
            var count = new Ref<int?>();
            var distance = new Ref<double?>();

            while (true)
            {
                // The array as it stands, as AutoCAD 2021 shows it while its grips and options are offered.
                yield return CadPrompt.ForPoint("Select grip to edit array", null, PolarOptions, "eXit")
                    .Previewing(CadPreview.Moving(c, c.Selected, input => PolarMoves(centre, basePoint, items, step, rows, apart, turn).ConvertAll(CadPreview.Flat)));

                if (c.Input.IsPoint)
                {
                    c.Write("Grips of an array are not built: use the options.");
                    continue;
                }

                if (c.Input.Is("ASsociative"))
                {
                    foreach (CadPrompt prompt in Associative(c))
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
                    yield return CadPrompt.ForAngle("Specify angle between items", null, new[] { "Expression" }, Number(step * 180.0 / Math.PI));
                    step = c.Input.Kind == CadInputKind.Number ? c.Input.Number : step;
                    continue;
                }

                if (c.Input.Is("Fill angle"))
                {
                    yield return CadPrompt.ForAngle("Specify the angle to fill (+=ccw, -=cw)", null, new[] { "EXpression" }, Number(Fill(items, step) * 180.0 / Math.PI));

                    if (c.Input.Kind == CadInputKind.Number)
                    {
                        step = StepOf(items, c.Input.Number);
                    }

                    continue;
                }

                if (c.Input.Is("ROWs"))
                {
                    foreach (CadPrompt prompt in Count(c, "Enter number of rows", rows, count))
                    {
                        yield return prompt;
                    }

                    rows = count.Value ?? rows;

                    foreach (CadPrompt prompt in Spacing(c, "Specify the distance between rows", "Specify the distance between first and last row", apart, rows, distance))
                    {
                        yield return prompt;
                    }

                    apart = distance.Value ?? apart;
                    yield return CadPrompt.ForDistance("Specify the incrementing elevation between rows", null, new[] { "Expression" }, Length(0.0));
                    Planar(c, 0.0);
                    continue;
                }

                if (c.Input.Is("Levels"))
                {
                    yield return CadPrompt.ForInteger("Enter the number of levels", new[] { "Expression" }, "1");
                    Planar(c, 1.0);
                    continue;
                }

                if (c.Input.Is("ROTate items"))
                {
                    yield return CadPrompt.ForKeyword("Rotate arrayed items?", new[] { "Yes", "No" }, turn ? "Yes" : "No");
                    turn = c.Input.Kind == CadInputKind.Keyword ? c.Input.Is("Yes") : turn;
                    continue;
                }

                if (c.Input.Is("eXit") || c.Input.IsNone)
                {
                    break;
                }

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
                var basePoint = box.IsEmpty ? GeoPoint2.Origin : new GeoPoint2((box.Min.X + box.Max.X) / 2.0, (box.Min.Y + box.Max.Y) / 2.0);

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
                yield return CadPrompt.ForInteger("Enter the number of items in the array");
                int items = c.Input.Kind == CadInputKind.Number ? (int)c.Input.Number : 0;
                yield return CadPrompt.ForAngle("Specify the angle to fill (+=ccw, -=cw)", null, null, "360");
                double fill = c.Input.Kind == CadInputKind.Number ? c.Input.Number : 2.0 * Math.PI;
                double step;

                if (items <= 0)
                {
                    yield return CadPrompt.ForAngle("Angle between items");

                    if (c.Input.Kind != CadInputKind.Number || Math.Abs(c.Input.Number) <= Tolerance.Default.EqualAngleRad)
                    {
                        c.Write("Requires an angle between items other than nought.");
                        yield break;
                    }

                    step = c.Input.Number * Math.Sign(fill == 0.0 ? 1.0 : fill);
                    bool whole = Math.Abs(Math.Abs(fill) - 2.0 * Math.PI) <= Tolerance.Default.EqualAngleRad;
                    items = (int)Math.Floor(Math.Abs(fill / step) + 1E-9) + (whole ? 0 : 1);
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

                yield return CadPrompt.ForKeyword("Rotate arrayed objects?", new[] { "Yes", "No" }, "Y");
                Copies(c, c.Selected, PolarMoves(centre, basePoint, items, step, 1, 0.0, !c.Input.Is("No")));
                yield break;
            }

            if (!c.Input.Is("Rectangular"))
            {
                yield break;
            }

            yield return CadPrompt.ForInteger("Enter the number of rows (---)", null, "1");
            int rows = c.Input.Kind == CadInputKind.Number ? (int)c.Input.Number : 1;
            yield return CadPrompt.ForInteger("Enter the number of columns (|||)", null, "1");
            int columns = c.Input.Kind == CadInputKind.Number ? (int)c.Input.Number : 1;

            if (rows < 1 || columns < 1)
            {
                c.Write("Value must be positive and nonzero.");
                yield break;
            }

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
                yield return CadPrompt.ForPoint("Enter the distance between rows or specify unit cell (---)", null, null, null, true);

                if (c.Input.IsPoint)
                {
                    GeoPoint2 corner = c.Input.Point;

                    // The copies the unit cell to the cursor would make, as the array is made of it.
                    yield return CadPrompt.ForPoint("Specify opposite corner", corner).Previewing(CadPreview.Moving(c, c.Selected, input =>
                    {
                        GeoVector2 drawn = c.Session.Ucs.ToUcs(input.Point - corner);
                        return RectangularMoves(c, rows, columns, drawn.X, drawn.Y).ConvertAll(CadPreview.Flat);
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
                else if (c.Input.Kind != CadInputKind.Text || !double.TryParse(c.Input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out up))
                {
                    c.Write("Requires a distance or two points.");
                    yield break;
                }
            }

            if (columns > 1 && !cell)
            {
                yield return CadPrompt.ForDistance("Specify the distance between columns (|||)");

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                across = c.Input.Number;
            }

            Copies(c, c.Selected, RectangularMoves(c, rows, columns, across, up));
        }

        /// <summary>
        /// The moves of the copies of a rectangular array of -ARRAY, the columns and rows apart by distances along the UCS; the
        /// item in place not moved. Three rows of two columns, 50 across and 30 up, give five copies.
        /// </summary>
        private static List<GeoTransform3> RectangularMoves(CadCommandContext c, int rows, int columns, double across, double up)
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
        private static List<GeoTransform3> PolarMoves(GeoPoint2 centre, GeoPoint2 basePoint, int items, double step, int rows, double apart, bool turn)
        {
            GeoVector2 outward = basePoint - centre;
            outward = outward.Length > Tolerance.Default.EqualPoint ? outward / outward.Length : new GeoVector2(1.0, 0.0);
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
                        var to = new GeoPoint2(centre.X + radius.X * Math.Cos(angle) - radius.Y * Math.Sin(angle), centre.Y + radius.X * Math.Sin(angle) + radius.Y * Math.Cos(angle));
                        moves.Add(GeoTransform3.Translation(new GeoVector3(to.X - basePoint.X, to.Y - basePoint.Y, 0.0)));
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

        private static IEnumerable<CadPrompt> Associative(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword("Create associative array", new[] { "Yes", "No" }, "No");

            if (c.Input.Is("Yes"))
            {
                c.Write("Associative arrays are not built: the items are made as separate objects (ARRAYASSOCIATIVITY 0).");
            }
        }

        /// <summary>Says that an elevation or a number of levels asked is of 3D, where a value other than the plain one (nought, or one level) was given.</summary>
        private static void Planar(CadCommandContext c, double plain)
        {
            if (c.Input.Kind == CadInputKind.Number && Math.Abs(c.Input.Number - plain) > c.Session.Tolerance.EqualPoint)
            {
                c.Write("Levels and elevations are of 3D: the array is made in the plan.");
            }
        }

        /// <summary>Asks a number of columns, rows or items, one or more.</summary>
        private static IEnumerable<CadPrompt> Count(CadCommandContext c, string message, int now, Ref<int?> result)
        {
            result.Value = null;
            yield return CadPrompt.ForInteger(message, new[] { "Expression" }, now.ToString(CultureInfo.InvariantCulture));

            if (c.Input.Kind != CadInputKind.Number)
            {
                yield break;
            }

            if (c.Input.Number < 1.0 || c.Input.Number > 100000.0)
            {
                c.Write("Value must be between 1 and 100000.");
                yield break;
            }

            result.Value = (int)c.Input.Number;
        }

        /// <summary>Asks the distance between columns or rows, or with Total the distance from the first to the last.</summary>
        private static IEnumerable<CadPrompt> Spacing(CadCommandContext c, string message, string total, double now, int count, Ref<double?> result)
        {
            result.Value = null;
            yield return CadPrompt.ForDistance(message, null, new[] { "Total", "Expression" }, Length(now));

            if (c.Input.Is("Total"))
            {
                yield return CadPrompt.ForDistance(total, null, new[] { "Expression" }, Length(now * Math.Max(1, count - 1)));

                if (c.Input.Kind == CadInputKind.Number)
                {
                    result.Value = count > 1 ? c.Input.Number / (count - 1) : now;
                }

                yield break;
            }

            if (c.Input.Kind == CadInputKind.Number)
            {
                result.Value = c.Input.Number;
            }
        }

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
                catch (Exception exception) when (exception is InvalidOperationException || exception is NotSupportedException)
                {
                    GeometryHelperLog.Debug("ARRAY: the box of " + handle + " is left out: " + exception.Message);
                }
            }

            return box;
        }
    }
}
