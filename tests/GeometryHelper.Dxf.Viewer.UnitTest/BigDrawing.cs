using System.Globalization;
using System.Text;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The text of a DXF of many entities, for timing: lines, arcs, circles, polylines with a bulge and texts on a grid of
    /// cells 30 apart, each its own handle, and among them INSERTs of a block of 20 entities, turned.
    /// </summary>
    internal static class BigDrawing
    {
        public const double Cell = 30.0;

        public const int Columns = 320;

        /// <summary>The text of the drawing: <paramref name="count"/> entities in model space, one in fifty an INSERT.</summary>
        public static string Text(int count)
        {
            var text = new StringBuilder(count * 160);
            text.Append("0\nSECTION\n2\nHEADER\n9\n$ACADVER\n1\nAC1015\n9\n$HANDSEED\n5\nFFFFFF\n0\nENDSEC\n");
            text.Append("0\nSECTION\n2\nTABLES\n");
            text.Append(
                "0\nTABLE\n2\nLTYPE\n70\n1\n0\nLTYPE\n2\nCONTINUOUS\n70\n0\n3\nSolid line\n72\n65\n73\n0\n40\n0\n0\nENDTAB\n"
            );
            text.Append("0\nTABLE\n2\nLAYER\n70\n3\n");
            text.Append(TestDrawings.Layer("0", 7))
                .Append(TestDrawings.Layer("Steel", 1))
                .Append(TestDrawings.Layer("Notes", 2))
                .Append(TestDrawings.Layer("Bolts", 3));
            text.Append("0\nENDTAB\n0\nENDSEC\n");
            text.Append("0\nSECTION\n2\nBLOCKS\n0\nBLOCK\n8\n0\n2\nB20\n70\n0\n10\n0\n20\n0\n30\n0\n3\nB20\n1\n\n");

            for (int k = 0; k < 20; k++)
            {
                double a = k * 0.5;

                switch (k % 4)
                {
                    case 0:
                        Line(text, null, "0", 0, a, 10, a + 2);
                        break;
                    case 1:
                        Circle(text, null, "0", 5, a, 1.0 + k * 0.1);
                        break;
                    case 2:
                        Arc(text, null, "0", 5, a, 3.0, k * 10.0, k * 10.0 + 120.0);
                        break;
                    default:
                        Line(text, null, "0", 10, 0, 0, a);
                        break;
                }
            }

            text.Append("0\nENDBLK\n8\n0\n0\nENDSEC\n");
            text.Append("0\nSECTION\n2\nENTITIES\n");

            for (int i = 0; i < count; i++)
            {
                string handle = (0x100 + i).ToString("X", CultureInfo.InvariantCulture);
                double x = (i % Columns) * Cell;
                double y = (i / Columns) * Cell;
                string layer =
                    (i % 3) == 0 ? "Steel"
                    : (i % 3) == 1 ? "Notes"
                    : "Bolts";

                if (i % 50 == 25)
                {
                    text.Append("0\nINSERT\n5\n").Append(handle).Append("\n8\n").Append(layer).Append("\n2\nB20\n");
                    Point(text, 10, x + 5, y + 5);
                    text.Append("50\n").Append(Number(i * 7 % 360)).Append('\n');
                    continue;
                }

                switch (i % 5)
                {
                    case 0:
                        Line(text, handle, layer, x, y, x + 20, y + 12);
                        break;
                    case 1:
                        Arc(text, handle, layer, x + 10, y + 10, 8, 30, 250);
                        break;
                    case 2:
                        Circle(text, handle, layer, x + 15, y + 15, 9);
                        break;
                    case 3:
                        text.Append("0\nLWPOLYLINE\n5\n")
                            .Append(handle)
                            .Append("\n8\n")
                            .Append(layer)
                            .Append("\n100\nAcDbEntity\n100\nAcDbPolyline\n90\n4\n70\n1\n");
                        text.Append("10\n").Append(Number(x)).Append("\n20\n").Append(Number(y)).Append('\n');
                        text.Append("10\n")
                            .Append(Number(x + 20))
                            .Append("\n20\n")
                            .Append(Number(y))
                            .Append("\n42\n0.5\n");
                        text.Append("10\n").Append(Number(x + 20)).Append("\n20\n").Append(Number(y + 20)).Append('\n');
                        text.Append("10\n").Append(Number(x)).Append("\n20\n").Append(Number(y + 20)).Append('\n');
                        break;
                    default:
                        text.Append("0\nTEXT\n5\n").Append(handle).Append("\n8\n").Append(layer).Append('\n');
                        Point(text, 10, x + 2, y + 2);
                        text.Append("40\n2.5\n1\nT").Append(handle).Append('\n');
                        break;
                }
            }

            text.Append("0\nENDSEC\n0\nEOF\n");
            return text.ToString();
        }

        private static string Number(double value) => TestDrawings.Number(value);

        private static void Point(StringBuilder text, int code, double x, double y) =>
            text.Append(code)
                .Append('\n')
                .Append(Number(x))
                .Append('\n')
                .Append(code + 10)
                .Append('\n')
                .Append(Number(y))
                .Append('\n')
                .Append(code + 20)
                .Append("\n0\n");

        private static void Head(StringBuilder text, string type, string handle, string layer)
        {
            text.Append("0\n").Append(type).Append('\n');

            if (handle != null)
            {
                text.Append("5\n").Append(handle).Append('\n');
            }

            text.Append("8\n").Append(layer).Append('\n');
        }

        private static void Line(
            StringBuilder text,
            string handle,
            string layer,
            double x0,
            double y0,
            double x1,
            double y1
        )
        {
            Head(text, "LINE", handle, layer);
            Point(text, 10, x0, y0);
            Point(text, 11, x1, y1);
        }

        private static void Circle(StringBuilder text, string handle, string layer, double x, double y, double radius)
        {
            Head(text, "CIRCLE", handle, layer);
            Point(text, 10, x, y);
            text.Append("40\n").Append(Number(radius)).Append('\n');
        }

        private static void Arc(
            StringBuilder text,
            string handle,
            string layer,
            double x,
            double y,
            double radius,
            double start,
            double end
        )
        {
            Head(text, "ARC", handle, layer);
            Point(text, 10, x, y);
            text.Append("40\n")
                .Append(Number(radius))
                .Append("\n50\n")
                .Append(Number(start))
                .Append("\n51\n")
                .Append(Number(end))
                .Append('\n');
        }
    }
}
