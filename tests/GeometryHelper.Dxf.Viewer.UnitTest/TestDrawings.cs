using System.Globalization;
using System.Text;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>Small drawings written as DXF text, so that a test reads what the file says and not how it was built.</summary>
    internal static class TestDrawings
    {
        public static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        public static string Line(
            double x0,
            double y0,
            double x1,
            double y1,
            string layer = "0",
            int color = 256,
            string handle = null
        ) => Entity("LINE", layer, color, handle, "10", x0, "20", y0, "30", 0.0, "11", x1, "21", y1, "31", 0.0);

        public static string Circle(
            double x,
            double y,
            double radius,
            string layer = "0",
            int color = 256,
            string handle = null
        ) => Entity("CIRCLE", layer, color, handle, "10", x, "20", y, "30", 0.0, "40", radius);

        public static string Arc(
            double x,
            double y,
            double radius,
            double startDegrees,
            double endDegrees,
            string layer = "0",
            string handle = null
        ) =>
            Entity(
                "ARC",
                layer,
                256,
                handle,
                "10",
                x,
                "20",
                y,
                "30",
                0.0,
                "40",
                radius,
                "50",
                startDegrees,
                "51",
                endDegrees
            );

        public static string Text(
            double x,
            double y,
            double height,
            string value,
            string layer = "0",
            string handle = null
        ) => Entity("TEXT", layer, 256, handle, "10", x, "20", y, "30", 0.0, "40", height, "1", value);

        public static string Insert(
            string block,
            double x,
            double y,
            double scale = 1.0,
            double rotation = 0.0,
            string layer = "0",
            int color = 256,
            string handle = null
        ) =>
            Entity(
                "INSERT",
                layer,
                color,
                handle,
                "2",
                block,
                "10",
                x,
                "20",
                y,
                "30",
                0.0,
                "41",
                scale,
                "42",
                scale,
                "43",
                scale,
                "50",
                rotation
            );

        /// <summary>A closed light-weight polyline with a bulge on its first edge: a rounded slot end.</summary>
        public static string Polyline(string layer, string handle, bool closed, params double[] xyBulge)
        {
            var builder = new StringBuilder();
            builder
                .Append("0\nLWPOLYLINE\n5\n")
                .Append(handle)
                .Append("\n8\n")
                .Append(layer)
                .Append("\n100\nAcDbEntity\n100\nAcDbPolyline\n90\n")
                .Append(xyBulge.Length / 3)
                .Append("\n70\n")
                .Append(closed ? 1 : 0)
                .Append('\n');

            for (int i = 0; i + 2 < xyBulge.Length; i += 3)
            {
                builder
                    .Append("10\n")
                    .Append(Number(xyBulge[i]))
                    .Append("\n20\n")
                    .Append(Number(xyBulge[i + 1]))
                    .Append('\n');

                if (xyBulge[i + 2] != 0.0)
                {
                    builder.Append("42\n").Append(Number(xyBulge[i + 2])).Append('\n');
                }
            }

            return builder.ToString();
        }

        public static string Block(string name, params string[] entities) =>
            "0\nBLOCK\n8\n0\n2\n"
            + name
            + "\n70\n0\n10\n0\n20\n0\n30\n0\n3\n"
            + name
            + "\n1\n\n"
            + string.Concat(entities)
            + "0\nENDBLK\n8\n0\n";

        public static string Layer(string name, int color, bool off = false, string linetype = "CONTINUOUS") =>
            "0\nLAYER\n2\n" + name + "\n70\n0\n62\n" + (off ? -color : color) + "\n6\n" + linetype + "\n";

        /// <summary>A linetype of dashes, an on length above nought and an off length below it, as the LTYPE table holds one.</summary>
        public static string Linetype(string name, params double[] dashes)
        {
            double total = 0.0;
            var builder = new StringBuilder();

            foreach (double dash in dashes)
            {
                total += System.Math.Abs(dash);
                builder.Append("49\n").Append(Number(dash)).Append("\n74\n0\n");
            }

            return "0\nLTYPE\n2\n"
                + name
                + "\n70\n0\n3\n"
                + name
                + "\n72\n65\n73\n"
                + dashes.Length
                + "\n40\n"
                + Number(total)
                + "\n"
                + builder;
        }

        public static string Solid(
            double x0,
            double y0,
            double x1,
            double y1,
            double x2,
            double y2,
            double x3,
            double y3,
            string layer = "0",
            int color = 256,
            string handle = null
        ) =>
            Entity(
                "SOLID",
                layer,
                color,
                handle,
                "10",
                x0,
                "20",
                y0,
                "30",
                0.0,
                "11",
                x1,
                "21",
                y1,
                "31",
                0.0,
                "12",
                x2,
                "22",
                y2,
                "32",
                0.0,
                "13",
                x3,
                "23",
                y3,
                "33",
                0.0
            );

        /// <summary>A TEXT held by its alignment point: horizontal 0 left, 1 centre, 2 right; vertical 0 baseline, 1 bottom, 2 middle, 3 top.</summary>
        public static string AlignedText(
            double x,
            double y,
            double height,
            string value,
            int horizontal,
            int vertical,
            string layer = "0",
            int color = 256,
            string handle = null
        ) =>
            Entity(
                "TEXT",
                layer,
                color,
                handle,
                "10",
                x,
                "20",
                y,
                "30",
                0.0,
                "40",
                height,
                "1",
                value,
                "72",
                horizontal,
                "11",
                x,
                "21",
                y,
                "31",
                0.0,
                "73",
                vertical
            );

        /// <summary>A whole drawing of the DXF of AutoCAD 2000: its layers, its blocks and its model-space entities.</summary>
        public static DxfDatabase Drawing(string layers, string blocks, params string[] entities) =>
            DrawingWithLinetypes(null, layers, blocks, entities);

        /// <summary>A whole drawing as <see cref="Drawing(string, string, string[])"/> makes one, with linetypes of its own beside CONTINUOUS.</summary>
        public static DxfDatabase DrawingWithLinetypes(
            string linetypes,
            string layers,
            string blocks,
            params string[] entities
        )
        {
            var text = new StringBuilder();
            text.Append("0\nSECTION\n2\nHEADER\n9\n$ACADVER\n1\nAC1015\n0\nENDSEC\n");
            text.Append("0\nSECTION\n2\nTABLES\n");
            text.Append(
                    "0\nTABLE\n2\nLTYPE\n70\n1\n0\nLTYPE\n2\nCONTINUOUS\n70\n0\n3\nSolid line\n72\n65\n73\n0\n40\n0\n"
                )
                .Append(linetypes ?? string.Empty)
                .Append("0\nENDTAB\n");
            text.Append("0\nTABLE\n2\nLAYER\n70\n3\n")
                .Append(Layer("0", 7))
                .Append(layers ?? string.Empty)
                .Append("0\nENDTAB\n0\nENDSEC\n");
            text.Append("0\nSECTION\n2\nBLOCKS\n").Append(blocks ?? string.Empty).Append("0\nENDSEC\n");
            text.Append("0\nSECTION\n2\nENTITIES\n").Append(string.Concat(entities)).Append("0\nENDSEC\n0\nEOF\n");
            return DxfDatabase.Parse(text.ToString());
        }

        public static string Entity(string type, string layer, int color, string handle, params object[] codes)
        {
            var builder = new StringBuilder();
            builder.Append("0\n").Append(type).Append('\n');

            if (handle != null)
            {
                builder.Append("5\n").Append(handle).Append('\n');
            }

            builder.Append("8\n").Append(layer).Append('\n');

            if (color != 256)
            {
                builder.Append("62\n").Append(color).Append('\n');
            }

            for (int i = 0; i + 1 < codes.Length; i += 2)
            {
                object value = codes[i + 1];
                builder
                    .Append(codes[i])
                    .Append('\n')
                    .Append(value is double number ? Number(number) : value)
                    .Append('\n');
            }

            return builder.ToString();
        }
    }
}
