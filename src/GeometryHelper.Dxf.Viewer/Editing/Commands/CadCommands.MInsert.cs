using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// MINSERT: a block placed as an array of rows and columns, one entity (a MINSERT) that draws, picks and plots as the block
    /// at each cell. It asks as AutoCAD 2021 does: "Enter block name or [?]:", the insertion point, the X and Y scale factors
    /// and the rotation as INSERT asks them, then "Enter number of rows (---) &lt;1&gt;:", "Enter number of columns (|||) &lt;1&gt;:",
    /// "Enter distance between rows or specify unit cell (---):" and "Specify distance between columns (|||):" (a unit cell
    /// given by two corners gives both distances at once; with one row, or one column, its distance is not asked). The
    /// distances are not scaled by the block's scale, and a negative one lays the array the other way. A block that has
    /// attributes is not placed: AutoCAD's MINSERT does not take one either. EXPLODE refuses a MINSERT with "1 was minserted.",
    /// as AutoCAD's does.
    /// </summary>
    internal static partial class CadCommands
    {
        private const string MInsertCounts = "Requires an integer between 1 and 32767.";

        private static IEnumerable<CadPrompt> MInsert(CadCommandContext c)
        {
            string last = c.Session.LastBlock;
            string name;

            while (true)
            {
                yield return CadPrompt.ForKeywordOrText("Enter block name or", new[] { "?" }, last);

                if (c.Input.Is("?"))
                {
                    WriteBlockList(c);
                    continue;
                }

                name = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : last;

                if (string.IsNullOrEmpty(name))
                {
                    yield break;
                }

                break;
            }

            if (!c.Database.BlockTable.Has(name))
            {
                c.Write("Block \"" + name + "\" not found.");
                yield break;
            }

            name = c.Database.BlockTable[name].Name;

            if (Definitions(c.Database.BlockTable[name]).Count > 0)
            {
                c.Write("Block \"" + name + "\" has attributes: MINSERT does not place it.");
                yield break;
            }

            var placing = new Placing { Scale = new GeoVector3(1.0, 1.0, 1.0), Array = true };

            foreach (CadPrompt prompt in Place(c, name, placing, true, true, true, false))
            {
                yield return prompt;
            }
        }

        /// <summary>
        /// The rows, columns and distances of a MINSERT, asked as AutoCAD asks them; the rows left 0 where an answer was not
        /// understood, which ends the command.
        /// </summary>
        private static IEnumerable<CadPrompt> ArrayCells(CadCommandContext c, Placing placing)
        {
            placing.Rows = 0;
            int rows;
            int columns;

            while (true)
            {
                yield return CadPrompt.ForInteger("Enter number of rows (---)", null, "1").Refusing(MInsertCounts);

                if (c.Input.IsNone)
                {
                    rows = 1;
                    break;
                }

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                if (c.Input.Number >= 1 && c.Input.Number <= short.MaxValue)
                {
                    rows = (int)c.Input.Number;
                    break;
                }

                c.Write(MInsertCounts);
            }

            while (true)
            {
                yield return CadPrompt.ForInteger("Enter number of columns (|||)", null, "1").Refusing(MInsertCounts);

                if (c.Input.IsNone)
                {
                    columns = 1;
                    break;
                }

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                if (c.Input.Number >= 1 && c.Input.Number <= short.MaxValue)
                {
                    columns = (int)c.Input.Number;
                    break;
                }

                c.Write(MInsertCounts);
            }

            double rowSpacing = 0.0;
            double columnSpacing = 0.0;
            bool unitCell = false;

            if (rows > 1)
            {
                yield return CadPrompt.ForPoint(
                    "Enter distance between rows or specify unit cell (---)",
                    null,
                    null,
                    null,
                    true
                );

                if (c.Input.IsPoint)
                {
                    GeoPoint2 corner = c.Input.Point;
                    yield return CadPrompt.ForPoint("Specify other corner", corner);

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    rowSpacing = c.Input.Point.Y - corner.Y;
                    columnSpacing = c.Input.Point.X - corner.X;
                    unitCell = true;
                }
                else if (
                    c.Input.Kind == CadInputKind.Text
                    && double.TryParse(c.Input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double typed)
                    && !double.IsInfinity(typed)
                    && !double.IsNaN(typed)
                )
                {
                    rowSpacing = typed;
                }
                else
                {
                    if (c.Input.Kind == CadInputKind.Text)
                    {
                        c.Write("Requires numeric distance or two points.");
                    }

                    yield break;
                }
            }

            if (columns > 1 && !unitCell)
            {
                yield return CadPrompt.ForDistance("Specify distance between columns (|||)");

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                columnSpacing = c.Input.Number;
            }

            placing.Rows = (short)rows;
            placing.Columns = (short)columns;
            placing.RowSpacing = rows > 1 ? rowSpacing : 0.0;
            placing.ColumnSpacing = columns > 1 ? columnSpacing : 0.0;
        }
    }
}
