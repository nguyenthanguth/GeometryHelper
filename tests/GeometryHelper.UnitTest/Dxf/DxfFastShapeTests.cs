using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;
using static GeometryHelper.UnitTest.Dxf.DxfLines;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The fast read of a LINE, ARC, CIRCLE or LWPOLYLINE of a <see cref="DxfFile"/>, straight from the bytes of its record,
    /// against the reader's own way of reading it from the groups made strings: the same shapes to the bit, whatever order
    /// the groups come in, whichever way the numbers are written, and where it cannot follow the reader it leaves the record
    /// to it.
    /// </summary>
    /// <remarks>
    /// The generated records run to <c>GH_FUZZ_COUNT</c> a type (1 500 where it is not set; the check before a release is
    /// 200 000, with <c>GH_FUZZ_SEED</c> moving the seed), and <c>GH_DXF_CORPUS</c> names a folder of DXF files whose every
    /// LINE, ARC, CIRCLE and LWPOLYLINE, in the drawing and in the blocks, is read both ways.
    /// </remarks>
    public class DxfFastShapeTests
    {
        private readonly ITestOutputHelper _output;

        public DxfFastShapeTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static readonly Tolerance Tol = Tolerance.Default;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        #region Helpers

        /// <summary>A shape as the numbers it holds, each double by its bits, so that two equal strings are two equal shapes.</summary>
        private static string Bits(object shape)
        {
            var text = new StringBuilder(shape?.GetType().Name).Append('|');
            Bits(text, shape, 0);
            return text.ToString();
        }

        private static void Bits(StringBuilder text, object value, int depth)
        {
            if (value == null || depth > 8)
            {
                text.Append("null;");
                return;
            }

            Type type = value.GetType();

            if (value is double number)
            {
                text.Append(BitConverter.DoubleToInt64Bits(number).ToString("x", Inv)).Append(';');
                return;
            }

            if (type.IsPrimitive || value is string || type.IsEnum)
            {
                text.Append(value).Append(';');
                return;
            }

            if (value is IEnumerable items)
            {
                text.Append('[');

                foreach (object item in items)
                {
                    Bits(text, item, depth + 1);
                }

                text.Append(']');
                return;
            }

            foreach (
                FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .OrderBy(f => f.Name, StringComparer.Ordinal)
            )
            {
                if (
                    field.FieldType == typeof(IntPtr)
                    || field.Name.IndexOf("cache", StringComparison.OrdinalIgnoreCase) >= 0
                    || field.Name.IndexOf("lazy", StringComparison.OrdinalIgnoreCase) >= 0
                )
                {
                    continue;
                }

                text.Append(field.Name).Append('=');
                Bits(text, field.GetValue(value), depth + 1);
            }
        }

        private static string Bits(IEnumerable<object> shapes) => string.Join("#", shapes.Select(Bits));

        private static int Count(string variable, int fallback) =>
            int.TryParse(Environment.GetEnvironmentVariable(variable), NumberStyles.Integer, Inv, out int n) && n > 0
                ? n
                : fallback;

        /// <summary>A file of R2000 of these entities, as <see cref="DxfLines.R2000"/> writes one.</summary>
        private static DxfFile FileOf(string entities) => DxfFile.Parse(R2000(entities));

        /// <summary>
        /// Reads every record of a file both ways and says where they differ: the shapes the file gives (the fast read where
        /// it can follow the reader) and those the reader makes of the groups. Returns how many records the fast read took.
        /// </summary>
        private static int Compare(
            DxfFile file,
            IEnumerable<DxfItem> items,
            List<string> wrong,
            Func<DxfItem, string> said
        )
        {
            int taken = 0;

            foreach (DxfItem item in items)
            {
                if (item.Type != "LINE" && item.Type != "ARC" && item.Type != "CIRCLE" && item.Type != "LWPOLYLINE")
                {
                    continue;
                }

                string fast = Bits(file.ShapesOf(item));
                string slow = Bits(file.ShapesReadLong(item));
                DxfFile.Rec own = file.RecordAt(item, 0);

                if (DxfReader.TryFastShape(own.Bytes, own.Start, own.End, own.Type, Tol, out object made))
                {
                    taken++;

                    if (Bits(made) != slow)
                    {
                        wrong.Add("fast read differs: " + said(item) + "\nfast " + Bits(made) + "\nslow " + slow);
                    }
                }

                if (fast != slow)
                {
                    wrong.Add("shapes differ: " + said(item) + "\nfile " + fast + "\nslow " + slow);
                }
            }

            return taken;
        }

        #endregion

        #region Generated records

        private static string Pad(Random rnd, int code)
        {
            string text = code.ToString(Inv);
            int how = rnd.Next(100);
            return how < 85 ? text.PadLeft(3)
                : how < 93 ? text
                : how < 97 ? " " + text + " "
                : "\t" + text;
        }

        /// <summary>A number written one of many ways: shortest, fixed, exponent, a plus, leading or trailing noughts, padded.</summary>
        private static string Num(Random rnd, double v)
        {
            if (rnd.Next(300) == 0)
            {
                string[] bad =
                {
                    "abc",
                    "1e999",
                    "NaN",
                    "",
                    " ",
                    "1.2.3",
                    "0x10",
                    "1,5",
                    "1e",
                    "--1",
                    "Infinity",
                    "1e+",
                    ".",
                    "-",
                    "1 2",
                };
                return bad[rnd.Next(bad.Length)];
            }

            string s;

            switch (rnd.Next(18))
            {
                case 0:
                case 1:
                case 2:
                case 3:
                case 4:
                    s = v.ToString("R", Inv);
                    break;
                case 5:
                    s = v.ToString("F2", Inv);
                    break;
                case 6:
                    s = v.ToString("F6", Inv);
                    break;
                case 7:
                    s = v.ToString("F10", Inv);
                    break;
                case 8:
                    s = v.ToString("E5", Inv);
                    break;
                case 9:
                    s = v.ToString("G17", Inv);
                    break;
                case 10:
                    s = v.ToString("0.###############", Inv);
                    break;
                case 11:
                    s = v.ToString("R", Inv).Replace("E", "e");
                    break;
                case 12:
                    s = (v >= 0.0 ? "+" : string.Empty) + v.ToString("R", Inv);
                    break;
                case 13:
                    s = (v >= 0.0 ? "00" : "-00") + Math.Abs(v).ToString("F3", Inv);
                    break;
                case 14:
                    s = v.ToString("F12", Inv);
                    break;
                case 15:
                    s = v.ToString("R", Inv).Replace("E-", "E-0").Replace("E+", "E+0");
                    break;
                case 16:
                    s = v == Math.Floor(v) && Math.Abs(v) < 1e9 ? v.ToString("F0", Inv) + "." : v.ToString("R", Inv);
                    break;
                default:
                    s = v.ToString("R", Inv);
                    break;
            }

            if (s.StartsWith("0.", StringComparison.Ordinal) && rnd.Next(6) == 0)
            {
                s = s.Substring(1);
            }

            switch (rnd.Next(14))
            {
                case 0:
                    return " " + s;
                case 1:
                    return s + " ";
                case 2:
                    return "\t" + s + " ";
                case 3:
                    return "  " + s + "  ";
                default:
                    return s;
            }
        }

        private static double Coordinate(Random rnd)
        {
            switch (rnd.Next(12))
            {
                case 0:
                    return 0.0;
                case 1:
                    return Math.Round(rnd.NextDouble() * 200.0 - 100.0, 3);
                case 2:
                    return Math.Round(rnd.NextDouble() * 2e6 - 1e6, 6);
                case 3:
                    return rnd.NextDouble() * 1e4 - 5e3;
                case 4:
                    return Math.Round(rnd.NextDouble() * 100.0, 1);
                case 5:
                    return rnd.Next(-500, 500);
                case 6:
                    return (rnd.NextDouble() - 0.5) * Math.Pow(10, rnd.Next(-8, 12));
                case 7:
                    return Math.Round(rnd.NextDouble(), 12);
                case 8:
                    return -0.0;
                case 9:
                    return rnd.NextDouble() * 100.0;
                case 10:
                    return Math.Round(rnd.NextDouble() * 5000.0, 4);
                default:
                    return rnd.Next(1, 20) * 0.1;
            }
        }

        private static double Size(Random rnd)
        {
            switch (rnd.Next(10))
            {
                case 0:
                    return 0.0;
                case 1:
                    return rnd.NextDouble() * 1e-3;
                case 2:
                    return -rnd.NextDouble() * 10.0;
                case 3:
                    return 1e-3;
                case 4:
                    return rnd.NextDouble() * 1e6;
                default:
                    return Math.Round(rnd.NextDouble() * 50.0 + 0.001, rnd.Next(0, 6));
            }
        }

        private static double Angle(Random rnd)
        {
            switch (rnd.Next(8))
            {
                case 0:
                    return 0.0;
                case 1:
                    return 360.0;
                case 2:
                    return -rnd.NextDouble() * 400.0;
                case 3:
                    return rnd.NextDouble() * 720.0;
                case 4:
                    return rnd.Next(0, 8) * 45.0;
                default:
                    return Math.Round(rnd.NextDouble() * 360.0, rnd.Next(0, 8));
            }
        }

        /// <summary>The groups of an extrusion: none, up, down, a turned one, one not unit, one of no length, parts of one.</summary>
        private static void Extrusion(Random rnd, List<KeyValuePair<int, string>> groups)
        {
            double x,
                y,
                z;

            switch (rnd.Next(12))
            {
                case 0:
                case 1:
                case 2:
                case 3:
                    return;
                case 4:
                    x = 0.0;
                    y = 0.0;
                    z = 1.0;
                    break;
                case 5:
                    x = 0.0;
                    y = 0.0;
                    z = -1.0;
                    break;
                case 6:
                    x = rnd.NextDouble() - 0.5;
                    y = rnd.NextDouble() - 0.5;
                    z = rnd.NextDouble() - 0.5;
                    double length = Math.Sqrt(x * x + y * y + z * z);
                    x /= length;
                    y /= length;
                    z /= length;
                    break;
                case 7:
                    x = rnd.NextDouble() * 3.0 - 1.5;
                    y = rnd.NextDouble() * 3.0 - 1.5;
                    z = rnd.NextDouble() * 3.0 - 1.5;
                    break;
                case 8:
                    x = 0.0;
                    y = 0.0;
                    z = 0.0;
                    break;
                case 9:
                    x = 1.0;
                    y = 0.0;
                    z = 0.0;
                    break;
                case 10:
                    x = 1E-13;
                    y = -1E-13;
                    z = 1.0 + 1E-13;
                    break;
                default:
                    x = 0.0;
                    y = 0.0;
                    z = 1.0;
                    break;
            }

            // One or two of the three left out now and then, which the reader reads as noughts.
            bool part = rnd.Next(6) == 0;

            if (!part || rnd.Next(2) == 0)
            {
                groups.Add(new KeyValuePair<int, string>(210, Num(rnd, x)));
            }

            if (!part || rnd.Next(2) == 0)
            {
                groups.Add(new KeyValuePair<int, string>(220, Num(rnd, y)));
            }

            if (!part || rnd.Next(2) == 0)
            {
                groups.Add(new KeyValuePair<int, string>(230, Num(rnd, z)));
            }
        }

        /// <summary>Groups the entity does not read, some of them the reader checks for a number: comments, owners, looks, widths.</summary>
        private static void Noise(Random rnd, List<KeyValuePair<int, string>> groups)
        {
            void Add(int code, string value) =>
                groups.Insert(rnd.Next(groups.Count + 1), new KeyValuePair<int, string>(code, value));

            if (rnd.Next(3) == 0)
            {
                Add(100, "AcDbEntity");
            }

            if (rnd.Next(3) == 0)
            {
                Add(8, "Layer" + rnd.Next(3));
            }

            if (rnd.Next(8) == 0)
            {
                Add(6, "DASHED");
            }

            if (rnd.Next(8) == 0)
            {
                Add(62, rnd.Next(30) == 0 ? "red" : rnd.Next(-2, 260).ToString(Inv));
            }

            if (rnd.Next(10) == 0)
            {
                Add(370, rnd.Next(30) == 0 ? "thick" : new[] { "-3", "-2", "-1", "25", "40", "999" }[rnd.Next(6)]);
            }

            if (rnd.Next(8) == 0)
            {
                Add(39, Num(rnd, Size(rnd)));
            }

            if (rnd.Next(12) == 0)
            {
                Add(48, Num(rnd, rnd.NextDouble()));
            }

            if (rnd.Next(15) == 0)
            {
                Add(60, rnd.Next(80) == 0 ? "x" : rnd.Next(2).ToString(Inv));
            }

            if (rnd.Next(15) == 0)
            {
                Add(330, "1F");
            }

            if (rnd.Next(30) == 0)
            {
                Add(420, rnd.Next(30) == 0 ? "blue" : rnd.Next(0, 0xFFFFFF).ToString(Inv));
            }

            if (rnd.Next(40) == 0)
            {
                Add(999, "a comment");
            }

            if (rnd.Next(60) == 0)
            {
                Add(71, "1.5");
            }

            if (rnd.Next(80) == 0)
            {
                Add(41, "abc");
            }

            if (rnd.Next(40) == 0)
            {
                Add(1000, "extended");
            }

            // A group of a code the entity reads, a second time with another value: the first is the one read.
            if (groups.Count > 0 && rnd.Next(20) == 0)
            {
                KeyValuePair<int, string> again = groups[rnd.Next(groups.Count)];

                if (again.Key < 300 && again.Key != 5 && again.Key != 999)
                {
                    groups.Insert(
                        rnd.Next(groups.Count + 1),
                        new KeyValuePair<int, string>(again.Key, Num(rnd, Coordinate(rnd)))
                    );
                }
            }
        }

        private static void Shuffle(Random rnd, List<KeyValuePair<int, string>> groups)
        {
            for (int i = groups.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                KeyValuePair<int, string> swap = groups[i];
                groups[i] = groups[j];
                groups[j] = swap;
            }
        }

        private static void Maybe(Random rnd, List<KeyValuePair<int, string>> groups, int code, double value, int oneIn)
        {
            if (rnd.Next(oneIn) != 0)
            {
                groups.Add(new KeyValuePair<int, string>(code, Num(rnd, value)));
            }
        }

        private static string Text(Random rnd, string type, int handle, List<KeyValuePair<int, string>> groups)
        {
            var text = new StringBuilder();
            text.Append(G(0, type, 5, handle.ToString("X", Inv)));

            foreach (KeyValuePair<int, string> group in groups)
            {
                text.Append(Pad(rnd, group.Key)).Append('\n').Append(group.Value).Append('\n');
            }

            return rnd.Next(4) == 0 ? text.ToString().Replace("\n", "\r\n") : text.ToString();
        }

        private static string Line(Random rnd, int handle)
        {
            var groups = new List<KeyValuePair<int, string>>();
            double x0 = Coordinate(rnd),
                y0 = Coordinate(rnd),
                z0 = rnd.Next(3) == 0 ? Coordinate(rnd) : 0.0;
            double x1 = Coordinate(rnd),
                y1 = Coordinate(rnd),
                z1 = rnd.Next(3) == 0 ? Coordinate(rnd) : 0.0;

            switch (rnd.Next(10))
            {
                case 0:
                    x1 = x0;
                    y1 = y0;
                    z1 = z0;
                    break;
                case 1:
                    x1 = x0 + 4E-4;
                    y1 = y0 - 3E-4;
                    z1 = z0;
                    break;
                case 2:
                    x1 = x0 + 1.1E-3;
                    y1 = y0;
                    z1 = z0;
                    break;
            }

            Maybe(rnd, groups, 10, x0, 40);
            Maybe(rnd, groups, 20, y0, 40);
            Maybe(rnd, groups, 30, z0, 5);
            Maybe(rnd, groups, 11, x1, 40);
            Maybe(rnd, groups, 21, y1, 40);
            Maybe(rnd, groups, 31, z1, 5);

            if (rnd.Next(6) == 0)
            {
                Extrusion(rnd, groups);
            }

            Noise(rnd, groups);

            if (rnd.Next(5) == 0)
            {
                Shuffle(rnd, groups);
            }

            return Text(rnd, "LINE", handle, groups);
        }

        private static string Round(Random rnd, int handle, bool arc)
        {
            var groups = new List<KeyValuePair<int, string>>();
            Maybe(rnd, groups, 10, Coordinate(rnd), 40);
            Maybe(rnd, groups, 20, Coordinate(rnd), 40);
            Maybe(rnd, groups, 30, rnd.Next(3) == 0 ? Coordinate(rnd) : 0.0, 4);
            Maybe(rnd, groups, 40, Size(rnd), 40);

            if (arc)
            {
                double start = Angle(rnd);
                Maybe(rnd, groups, 50, start, 40);
                Maybe(rnd, groups, 51, rnd.Next(12) == 0 ? start : Angle(rnd), 40);
            }

            Extrusion(rnd, groups);
            Noise(rnd, groups);

            if (rnd.Next(5) == 0)
            {
                Shuffle(rnd, groups);
            }

            return Text(rnd, arc ? "ARC" : "CIRCLE", handle, groups);
        }

        private static string Lightweight(Random rnd, int handle)
        {
            var head = new List<KeyValuePair<int, string>>();
            var vertices = new List<KeyValuePair<int, string>>();
            int count =
                rnd.Next(30) == 0 ? 0
                : rnd.Next(12) == 0 ? rnd.Next(7, 40)
                : rnd.Next(1, 7);
            bool closed = rnd.Next(2) == 0;
            double x = Coordinate(rnd),
                y = Coordinate(rnd);
            double firstX = x,
                firstY = y;
            bool bulging = rnd.Next(2) == 0;

            for (int i = 0; i < count; i++)
            {
                switch (rnd.Next(10))
                {
                    case 0:
                        break;
                    case 1:
                        x += 2E-4;
                        y -= 4E-4;
                        break;
                    case 2:
                        x = firstX;
                        y = firstY;
                        break;
                    default:
                        x = rnd.Next(4) == 0 ? Coordinate(rnd) : x + rnd.NextDouble() * 20.0 - 5.0;
                        y = rnd.Next(4) == 0 ? Coordinate(rnd) : y + rnd.NextDouble() * 20.0 - 5.0;
                        break;
                }

                var vertex = new List<KeyValuePair<int, string>>();

                if (rnd.Next(60) != 0)
                {
                    vertex.Add(new KeyValuePair<int, string>(10, Num(rnd, x)));
                }

                if (rnd.Next(40) != 0)
                {
                    vertex.Add(new KeyValuePair<int, string>(20, Num(rnd, y)));
                }

                if (bulging && rnd.Next(3) == 0)
                {
                    double bulge =
                        rnd.Next(6) == 0 ? 1.0
                        : rnd.Next(8) == 0 ? -1.0
                        : rnd.Next(10) == 0 ? 1E-12
                        : Math.Round((rnd.NextDouble() - 0.5) * 8.0, rnd.Next(1, 8));
                    vertex.Add(new KeyValuePair<int, string>(42, Num(rnd, bulge)));
                }

                if (rnd.Next(10) == 0)
                {
                    vertex.Add(new KeyValuePair<int, string>(40, Num(rnd, rnd.NextDouble())));
                    vertex.Add(new KeyValuePair<int, string>(41, Num(rnd, rnd.NextDouble())));
                }

                // The order within a vertex is the file's now and then, and a group of it stands twice.
                if (rnd.Next(12) == 0)
                {
                    Shuffle(rnd, vertex);
                }

                if (vertex.Count > 0 && rnd.Next(25) == 0)
                {
                    vertex.Add(vertex[rnd.Next(vertex.Count)]);
                }

                vertices.AddRange(vertex);
            }

            head.Add(new KeyValuePair<int, string>(100, "AcDbEntity"));
            head.Add(new KeyValuePair<int, string>(100, "AcDbPolyline"));

            if (rnd.Next(20) != 0)
            {
                head.Add(
                    new KeyValuePair<int, string>(90, (rnd.Next(15) == 0 ? rnd.Next(0, 99999) : count).ToString(Inv))
                );
            }

            if (rnd.Next(10) != 0)
            {
                int flag =
                    rnd.Next(25) == 0 ? 128
                    : rnd.Next(30) == 0 ? 3
                    : closed ? 1
                    : 0;
                head.Add(new KeyValuePair<int, string>(70, rnd.Next(30) == 0 ? " " + flag + " " : flag.ToString(Inv)));
            }

            if (rnd.Next(6) == 0)
            {
                head.Add(new KeyValuePair<int, string>(38, Num(rnd, rnd.Next(2) == 0 ? 0.0 : Coordinate(rnd))));
            }

            if (rnd.Next(10) == 0)
            {
                head.Add(new KeyValuePair<int, string>(43, Num(rnd, rnd.Next(3) == 0 ? 0.0 : Size(rnd))));
            }

            if (rnd.Next(4) == 0)
            {
                Extrusion(rnd, head);
            }

            Noise(rnd, head);

            if (rnd.Next(5) == 0)
            {
                Shuffle(rnd, head);
            }

            // The head runs before the vertices, or is cut in two round them, or comes after, as a writer of its own might.
            var groups = new List<KeyValuePair<int, string>>();
            int where = rnd.Next(12);

            if (where == 0)
            {
                groups.AddRange(vertices);
                groups.AddRange(head);
            }
            else if (where == 1)
            {
                int cut = rnd.Next(head.Count + 1);
                groups.AddRange(head.Take(cut));
                groups.AddRange(vertices);
                groups.AddRange(head.Skip(cut));
            }
            else
            {
                groups.AddRange(head);
                groups.AddRange(vertices);
            }

            return Text(rnd, "LWPOLYLINE", handle, groups);
        }

        private static string Record(Random rnd, string type, int handle)
        {
            switch (type)
            {
                case "LINE":
                    return Line(rnd, handle);
                case "CIRCLE":
                    return Round(rnd, handle, false);
                case "ARC":
                    return Round(rnd, handle, true);
                default:
                    return Lightweight(rnd, handle);
            }
        }

        /// <summary>Generated records of a type read both ways; how many the fast read took, and the records it and the reader differ on.</summary>
        private static int Fuzz(string type, int total, int seed, List<string> wrong)
        {
            var rnd = new Random(seed);
            int taken = 0;
            int made = 0;

            while (made < total)
            {
                int batch = Math.Min(400, total - made);
                var texts = new List<string>(batch);
                var body = new StringBuilder();

                for (int i = 0; i < batch; i++)
                {
                    string record = Record(rnd, type, 0x100 + made + i);
                    texts.Add(record);
                    body.Append(record);
                }

                DxfFile file = FileOf(body.ToString());
                var items = file.ModelSpace.ToList();
                Assert.Equal(batch, items.Count);
                taken += Compare(file, items, wrong, item => texts[items.IndexOf(item)].Replace("\n", "|"));
                made += batch;

                if (wrong.Count > 5)
                {
                    break;
                }
            }

            return taken;
        }

        #endregion

        [Theory]
        [InlineData("LINE", 11)]
        [InlineData("CIRCLE", 12)]
        [InlineData("ARC", 13)]
        [InlineData("LWPOLYLINE", 14)]
        public void RecordsOfEveryOrderAndNumberFormat_ReadFastAndLong_GiveTheSameShapesToTheBit(string type, int seed)
        {
            var wrong = new List<string>();
            int total = Count("GH_FUZZ_COUNT", 1500);
            int taken = Fuzz(type, total, seed + Count("GH_FUZZ_SEED", 0), wrong);

            Assert.Empty(wrong.Take(3));

            _output.WriteLine(type + ": " + taken + " of " + total + " records read fast");

            // About half to four fifths of what is generated is read fast: the rest has no length, no radius or a group missing, or an odd number.
            Assert.True(taken > total / 3, taken + " of " + total + " read fast");
        }

        [Fact]
        public void AFileOfRecordsOfEveryKind_OnWholeFiles_GivesTheSameShapesBothWays()
        {
            string folder = Environment.GetEnvironmentVariable("GH_DXF_CORPUS");

            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return;
            }

            var wrong = new List<string>();
            int taken = 0;
            int records = 0;

            foreach (string path in Directory.GetFiles(folder, "*.dxf").OrderBy(p => p, StringComparer.Ordinal))
            {
                DxfFile file;

                try
                {
                    file = DxfFile.Open(path);
                }
                catch (FormatException)
                {
                    continue;
                }

                var items = new List<DxfItem>(file.ModelSpace);
                items.AddRange(file.PaperSpace);

                foreach (DxfFileBlock block in file.Blocks.Values)
                {
                    items.AddRange(block.Items);
                }

                records += items.Count(i =>
                    i.Type == "LINE" || i.Type == "ARC" || i.Type == "CIRCLE" || i.Type == "LWPOLYLINE"
                );
                taken += Compare(file, items, wrong, item =>
                    Path.GetFileName(path) + " " + item.Type + " " + item.Handle
                );
            }

            Assert.Empty(wrong.Take(3));
            _output.WriteLine(records + " LINE, ARC, CIRCLE and LWPOLYLINE records, " + taken + " read fast");
            Assert.True(taken > 0 || records == 0);
        }

        [Theory]
        [InlineData("100")]
        [InlineData("-0")]
        [InlineData("+.5")]
        [InlineData("5.")]
        [InlineData("1.50")]
        [InlineData("0.1")]
        [InlineData("007.5")]
        [InlineData("1E2")]
        [InlineData("1e-3")]
        [InlineData("-2.5E+01")]
        [InlineData("  12.25\t")]
        [InlineData("1E22")]
        [InlineData("9007199254740992")]
        [InlineData("1E23")]
        [InlineData("9007199254740993")]
        [InlineData("123456789.12345678")]
        [InlineData("0.30000000000000004")]
        [InlineData("1234567890123456789012")]
        [InlineData("1e-30")]
        [InlineData("4.9E-324")]
        [InlineData("1.7976931348623157E308")]
        public void ANumberWrittenOneWay_IsReadTheSameByTheFastReadAndTheLongRead(string written)
        {
            string line = G(0, "LINE", 5, "A1", 10, "0", 20, "0", 30, "0") + "11\n" + written + "\n 21\n3\n 31\n0\n";
            DxfFile file = FileOf(line);
            DxfItem item = file.ModelSpace.Single();
            DxfFile.Rec own = file.RecordAt(item, 0);
            bool fast = DxfReader.TryFastShape(own.Bytes, own.Start, own.End, own.Type, Tol, out object made);

            Assert.True(fast);
            Assert.Equal(Bits(file.ShapesReadLong(item)), Bits(file.ShapesOf(item)));

            if (fast)
            {
                Assert.Equal(Bits(file.ShapesReadLong(item)), Bits(made));
            }
        }

        [Fact]
        public void ANegativeZero_KeepsItsSignInTheShapeBothWays()
        {
            string circle = G(0, "CIRCLE", 5, "A2") + "10\n-0.0\n20\n-0\n30\n-0.0\n40\n5\n";
            DxfFile file = FileOf(circle);
            DxfItem item = file.ModelSpace.Single();
            string fast = Bits(file.ShapesOf(item));

            Assert.Equal(Bits(file.ShapesReadLong(item)), fast);
            Assert.StartsWith("GeoCircle3|", fast, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("LINE")]
        [InlineData("CIRCLE")]
        [InlineData("ARC")]
        [InlineData("LWPOLYLINE")]
        public void ARecordReadFast_IsReadTheSameOnceItsGroupsAreWrittenBack(string type)
        {
            var wrong = new List<string>();
            var rnd = new Random(77);
            DxfFile file = FileOf(string.Concat(Enumerable.Range(0, 200).Select(i => Record(rnd, type, 0x200 + i))));
            file.ModelSpace[0].Layer = "Edited";
            Compare(file, file.ModelSpace, wrong, item => item.Type + " " + item.Handle);

            Assert.Empty(wrong.Take(3));
        }

        [Fact]
        public void AValueWithALetterBeyondAscii_IsLeftToTheLongRead()
        {
            string line = G(0, "LINE", 5, "A3", 10, "0", 20, "0", 30, "0") + "11\n 5\n 21\n3\n 31\n0\n";
            DxfFile file = DxfFile.Parse(
                G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, "AC1021", 0, "ENDSEC", 0, "SECTION", 2, "ENTITIES")
                    + line
                    + G(0, "ENDSEC", 0, "EOF")
            );
            DxfItem item = file.ModelSpace.Single();
            DxfFile.Rec own = file.RecordAt(item, 0);

            Assert.False(DxfReader.TryFastShape(own.Bytes, own.Start, own.End, own.Type, Tol, out _));
            Assert.Equal(Bits(file.ShapesReadLong(item)), Bits(file.ShapesOf(item)));
            Assert.Single(file.ShapesOf(item));
        }

        [Fact]
        public void ARecordOfAnotherType_IsNotReadFast()
        {
            DxfFile file = FileOf(G(0, "POINT", 5, "A4", 10, "1", 20, "2", 30, "0"));
            DxfItem item = file.ModelSpace.Single();
            DxfFile.Rec own = file.RecordAt(item, 0);

            Assert.False(DxfReader.TryFastShape(own.Bytes, own.Start, own.End, own.Type, Tol, out _));
        }

        [Fact]
        public void AnArcOfARadiusNoLongerThanTheTolerance_IsLeftToTheLongRead()
        {
            DxfFile file = FileOf(G(0, "ARC", 5, "A5", 10, "0", 20, "0", 40, "0.001", 50, "0", 51, "90"));
            DxfItem item = file.ModelSpace.Single();
            DxfFile.Rec own = file.RecordAt(item, 0);

            Assert.False(DxfReader.TryFastShape(own.Bytes, own.Start, own.End, own.Type, Tol, out _));
            Assert.Empty(file.ShapesOf(item));
        }

        [Fact]
        public void ALightweightPolylineOfAThousandVertices_IsReadFastAsTheLongReadReadsIt()
        {
            var text = new StringBuilder(G(0, "LWPOLYLINE", 5, "A6", 90, 1000, 70, 0));

            for (int i = 0; i < 1000; i++)
            {
                text.Append(G(10, i * 1.5, 20, (i % 7) * 2.25 + i * 0.001, 42, i % 5 == 0 ? 0.25 : 0.0));
            }

            DxfFile file = FileOf(text.ToString());
            DxfItem item = file.ModelSpace.Single();
            DxfFile.Rec own = file.RecordAt(item, 0);

            Assert.True(DxfReader.TryFastShape(own.Bytes, own.Start, own.End, own.Type, Tol, out object made));
            Assert.Equal(Bits(file.ShapesReadLong(item)), Bits(made));
        }
    }
}
