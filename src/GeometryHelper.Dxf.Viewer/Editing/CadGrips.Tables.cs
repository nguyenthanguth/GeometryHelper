using System;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The grips of a whole table selected, as AutoCAD 2021 gives them.</summary>
    public static partial class CadGrips
    {
        /// <summary>
        /// A table's grips: the move grip at its top left corner, a column grip on its top edge at each line between two
        /// columns, the stretch grips at its top right (width), bottom left (height) and bottom right (both) corners, and the
        /// table breaking grip at the middle of its bottom edge. A table of five columns has nine.
        /// </summary>
        private static void TableGrips(DxfTable table, Action<CadGripKind, GeoPoint2, GeoVector2> add)
        {
            if (table.NumRows < 1 || table.NumColumns < 1)
            {
                return;
            }

            GeoPoint2[] corners = CadTableEditor.Corners(
                table,
                new DxfCellRange(0, 0, table.NumRows - 1, table.NumColumns - 1)
            );
            GeoVector2 across = Unit(corners[1] - corners[0], new GeoVector2(1.0, 0.0));
            GeoVector2 down = Unit(corners[3] - corners[0], new GeoVector2(0.0, -1.0));
            add(CadGripKind.Insertion, corners[0], default);
            double x = 0.0;

            for (int c = 0; c < table.NumColumns - 1; c++)
            {
                x += table.ColumnWidth(c);
                add(CadGripKind.TableColumn, corners[0] + across * x, default);
            }

            add(CadGripKind.TableWidth, corners[1], across);
            add(CadGripKind.TableHeight, corners[3], down);
            add(CadGripKind.TableSize, corners[2], across + down);
            add(
                CadGripKind.TableBreak,
                new GeoPoint2((corners[2].X + corners[3].X) / 2.0, (corners[2].Y + corners[3].Y) / 2.0),
                down
            );
        }

        /// <summary>A vector of length one along a way, or the fallback where the way has none.</summary>
        internal static GeoVector2 Unit(GeoVector2 way, GeoVector2 fallback) =>
            way.Length > 0.0 ? way / way.Length : fallback;
    }
}
