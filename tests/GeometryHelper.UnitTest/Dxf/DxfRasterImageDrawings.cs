using System;
using System.Text;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Drawings of IMAGEs and WIPEOUTs for the tests of <c>DxfRasterImage</c> and <c>DxfWipeout</c>, built in code: the drawings
    /// of ezdxf 1.4.5 of R2018 and R2000 (<see cref="DxfFileEzdxfDrawings"/>), which hold the IMAGEDEF 8D of photo.jpg, with
    /// records written as AutoCAD 2021 writes them appended to model space, on the layer pictures.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>C10, a picture of 4 by 3 pixels placed at (10, 20), a pixel 10 across and 10 up, not clipped: 40 by 30.</item>
    /// <item>C11, the same clipped by the triangle of the pixels (0, 0), (3, 0), (1.5, 2.5).</item>
    /// <item>C12, the same clipped by the rectangle of the pixels (0.5, 0.5) and (2.5, 1.5).</item>
    /// <item>C13, the same standing upright at (0, 0, 50), its pixel up (0, 0, 10), not clipped.</item>
    /// <item>C14, a WIPEOUT of the frame of side 40 at (-30, -10) hiding the rectangle from there to (10, 20).</item>
    /// <item>C15, C11 with its display options of 3, which do not clip it, as ezdxf writes a new image.</item>
    /// </list>
    /// </remarks>
    internal static class DxfRasterImageDrawings
    {
        /// <summary>The drawing of R2018 with the records appended.</summary>
        internal static string R2018 => With(DxfFileEzdxfDrawings.R2018, true);

        /// <summary>The drawing of R2000 with the records appended, without the group 290 R2000 has not.</summary>
        internal static string R2000 => With(DxfFileEzdxfDrawings.R2000, false);

        private static string With(string drawing, bool inverted)
        {
            var b = new StringBuilder();
            Image(b, "C10", "10.0", "20.0", "0.0", "0.0", "10.0", "0.0", "7", "0", "1", inverted, "-0.5", "-0.5", "3.5", "2.5");
            Image(b, "C11", "10.0", "20.0", "0.0", "0.0", "10.0", "0.0", "7", "1", "2", inverted, "0.0", "0.0", "3.0", "0.0", "1.5", "2.5", "0.0", "0.0");
            Image(b, "C12", "10.0", "20.0", "0.0", "0.0", "10.0", "0.0", "7", "1", "1", inverted, "0.5", "0.5", "2.5", "1.5");
            Image(b, "C13", "0.0", "0.0", "50.0", "0.0", "0.0", "10.0", "7", "0", "1", inverted, "-0.5", "-0.5", "3.5", "2.5");
            Wipeout(b, inverted);
            Image(b, "C15", "10.0", "20.0", "0.0", "0.0", "10.0", "0.0", "3", "1", "2", inverted, "0.0", "0.0", "3.0", "0.0", "1.5", "2.5", "0.0", "0.0");

            int entities = drawing.IndexOf("\nENTITIES\n", StringComparison.Ordinal);
            int end = drawing.IndexOf("  0\nENDSEC\n", entities, StringComparison.Ordinal);
            return drawing.Substring(0, end) + b + drawing.Substring(end);
        }

        private static void Head(StringBuilder b, string type, string handle)
        {
            b.Append("  0\n").Append(type).Append("\n  5\n").Append(handle).Append("\n330\n17\n100\nAcDbEntity\n  8\npictures\n");
        }

        private static void Image(StringBuilder b, string handle, string x, string y, string z, string vx, string vy, string vz, string flags, string clipped, string type, bool inverted, params string[] corners)
        {
            Head(b, "IMAGE", handle);
            b.Append("100\nAcDbRasterImage\n 90\n0\n");
            b.Append(" 10\n").Append(x).Append("\n 20\n").Append(y).Append("\n 30\n").Append(z).Append('\n');
            b.Append(" 11\n10.0\n 21\n0.0\n 31\n0.0\n");
            b.Append(" 12\n").Append(vx).Append("\n 22\n").Append(vy).Append("\n 32\n").Append(vz).Append('\n');
            b.Append(" 13\n4.0\n 23\n3.0\n340\n8D\n 70\n").Append(flags).Append("\n280\n").Append(clipped).Append("\n281\n50\n282\n50\n283\n0\n");
            Boundary(b, type, inverted, corners);
        }

        private static void Wipeout(StringBuilder b, bool inverted)
        {
            Head(b, "WIPEOUT", "C14");
            b.Append("100\nAcDbWipeout\n 90\n0\n");
            b.Append(" 10\n-30.0\n 20\n-10.0\n 30\n0.0\n 11\n40.0\n 21\n0.0\n 31\n0.0\n 12\n0.0\n 22\n40.0\n 32\n0.0\n");
            b.Append(" 13\n1.0\n 23\n1.0\n340\n0\n 70\n7\n280\n1\n281\n50\n282\n50\n283\n0\n");
            Boundary(b, "2", inverted, "-0.5", "0.5", "0.5", "0.5", "0.5", "-0.25", "-0.5", "-0.25", "-0.5", "0.5");
        }

        private static void Boundary(StringBuilder b, string type, bool inverted, params string[] corners)
        {
            if (inverted)
            {
                b.Append("290\n0\n");
            }

            b.Append("360\n0\n 71\n").Append(type).Append("\n 91\n").Append(corners.Length / 2).Append('\n');

            for (int i = 0; i + 1 < corners.Length; i += 2)
            {
                b.Append(" 14\n").Append(corners[i]).Append("\n 24\n").Append(corners[i + 1]).Append('\n');
            }
        }
    }
}
