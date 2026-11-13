using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The plot stamp parameter file, AutoCAD's .pss, which the Plot Stamp dialog's Load and Save As read and write: a PIA
    /// file, its head "PIAFILEVERSION_2.0,plotstampcompress" and "pmzlibcodec", then the Adler-32 of a zlib stream, the
    /// length of the text and of the stream, and the stream of a text of nested blocks, as AutoCAD 2021's mm.pss holds:
    /// <c>pstamp{ fields{ drawing_name=1 … } ud_fields{ … } preview{ … } text_prop{ … } pstamp_units{ … } logfile{ … } }</c>.
    /// </summary>
    public sealed partial class CadPlotStamp
    {
        private const string PiaHead = "PIAFILEVERSION_2.0,plotstampcompress\r\npmzlibcodec";

        private IReadOnlyList<string> _userFields = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the list of the user's own fields the Field 1 and Field 2 lists of Plot Stamp pick from, as its Add /
        /// Edit keeps them; empty to start. The list is copied, so that a stamp and its <see cref="Clone"/> keep their own.
        /// </summary>
        public IReadOnlyList<string> UserFields
        {
            get => _userFields;
            set => _userFields = (value ?? Array.Empty<string>()).Where(f => !string.IsNullOrEmpty(f)).ToArray();
        }

        /// <summary>
        /// Writes the stamp to a plot stamp parameter file, as Save As of the Plot Stamp dialog: the fields, the user's fields
        /// with the two picked, the corner, the orientation, the offset, the text height, the units and the log file. The
        /// corner and orientation codes are those of AutoCAD's mm.pss for its defaults (location 0 lower left, orientation 1
        /// horizontal), the others in the order of the Location list.
        /// </summary>
        /// <param name="path">The file, as D:\Standards\site.pss.</param>
        /// <exception cref="ArgumentException">Thrown when the path is empty.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
        public void SaveParameterFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A plot stamp parameter file needs a path.", nameof(path));
            }

            byte[] text = Encoding.ASCII.GetBytes(ParameterText());
            byte[] stream = Zlib(text);

            using (var file = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                byte[] head = Encoding.ASCII.GetBytes(PiaHead);
                file.Write(head, 0, head.Length);
                file.Write(BitConverter.GetBytes(Adler32(stream, 0, stream.Length)), 0, 4);
                file.Write(BitConverter.GetBytes(text.Length), 0, 4);
                file.Write(BitConverter.GetBytes(stream.Length), 0, 4);
                file.Write(stream, 0, stream.Length);
            }
        }

        /// <summary>
        /// Reads a plot stamp parameter file into the stamp, as Load of the Plot Stamp dialog: every setting the file holds
        /// replaces the stamp's; one it does not hold is kept.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <exception cref="ArgumentException">Thrown when the path is empty.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        /// <exception cref="InvalidDataException">Thrown when the file is no plot stamp parameter file.</exception>
        public void LoadParameterFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A plot stamp parameter file needs a path.", nameof(path));
            }

            byte[] bytes = File.ReadAllBytes(path);
            byte[] head = Encoding.ASCII.GetBytes(PiaHead);

            if (
                bytes.Length < head.Length + 12 + 6
                || !head.SequenceEqual(bytes.Take(head.Length))
                || bytes[head.Length + 12] != 0x78
            )
            {
                throw new InvalidDataException(
                    "\"" + Path.GetFileName(path) + "\" is not a plot stamp parameter file (.pss)."
                );
            }

            string text;

            try
            {
                int start = head.Length + 12 + 2;

                using (var input = new MemoryStream(bytes, start, bytes.Length - start))
                using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
                using (var reader = new StreamReader(deflate, Encoding.ASCII))
                {
                    text = reader.ReadToEnd();
                }
            }
            catch (InvalidDataException)
            {
                throw new InvalidDataException(
                    "\"" + Path.GetFileName(path) + "\" is not a plot stamp parameter file (.pss): its data is damaged."
                );
            }

            Take(Values(text));
        }

        /// <summary>The text of the file, its blocks indented by one space a level and its lines ended by a line feed, as AutoCAD's.</summary>
        private string ParameterText()
        {
            var w = new StringBuilder();
            bool pixels = Units == CadPlotStampUnits.Pixels;
            w.Append("pstamp{\n fields{\n");
            Line(w, 2, "drawing_name", DrawingName);
            Line(w, 2, "layout_name", LayoutName);
            Line(w, 2, "date_time", DateAndTime);
            Line(w, 2, "login_name", LoginName);
            Line(w, 2, "device_name", DeviceName);
            Line(w, 2, "papersize", PaperSize);
            Line(w, 2, "plotscale", PlotScale);
            w.Append(" }\n ud_fields{\n  comment{\n");

            if (UserFields.Count == 0)
            {
                w.Append("   str=\"\n");
            }

            foreach (string field in UserFields)
            {
                w.Append("   str=\"").Append(field).Append('\n');
            }

            w.Append("   combo1{\n    selection=\"").Append(UserField1).Append("\n   }\n");
            w.Append("   combo2{\n    selection=\"").Append(UserField2).Append("\n   }\n  }\n }\n preview{\n");
            Line(w, 2, "location", ((int)Location).ToString(CultureInfo.InvariantCulture));
            Line(w, 2, "orientation", Vertical ? "0" : "1");
            Line(w, 2, "upsidedown", UpsideDown);
            Line(w, 2, "xoffset", Number(OffsetX));
            Line(w, 2, "yoffset", Number(OffsetY));
            Line(w, 2, "dimensional_xoffset", Number(pixels ? 2.54 : OffsetX));
            Line(w, 2, "dimensional_yoffset", Number(pixels ? 2.54 : OffsetY));
            Line(w, 2, "non_dimensional_xoffset", Number(pixels ? OffsetX : 10.0));
            Line(w, 2, "non_dimensional_yoffset", Number(pixels ? OffsetY : 10.0));
            Line(w, 2, "relative_offset", OffsetFromPaperBorder);
            w.Append(" }\n text_prop{\n");
            Line(w, 2, "font_height", Number(TextHeight));
            Line(w, 2, "dimensional_font_height", Number(pixels ? 5.08 : TextHeight));
            Line(w, 2, "non_dimensional_font_height", Number(pixels ? TextHeight : 10.0));
            Line(w, 2, "single_line", SingleLine);
            w.Append(" }\n pstamp_units{\n");
            Line(w, 2, "units", ((int)Units).ToString(CultureInfo.InvariantCulture));
            w.Append(" }\n logfile{\n");
            Line(w, 2, "create_logfile", WriteLog);
            w.Append("  logfile_name=\"").Append(LogFile).Append("\n }\n}\n");
            return w.ToString();
        }

        private static void Line(StringBuilder w, int depth, string key, bool value) =>
            Line(w, depth, key, value ? "1" : "0");

        private static void Line(StringBuilder w, int depth, string key, string value) =>
            w.Append(' ', depth).Append(key).Append('=').Append(value).Append('\n');

        private static string Number(double value) => value.ToString("0.0###########", CultureInfo.InvariantCulture);

        /// <summary>The values of the text by their path, as fields/drawing_name; a key met again, as str, keeps each value.</summary>
        private static Dictionary<string, List<string>> Values(string text)
        {
            var values = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var path = new List<string>();

            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim().TrimEnd('\r');

                if (line.EndsWith("{", StringComparison.Ordinal))
                {
                    path.Add(line.Substring(0, line.Length - 1).Trim());
                    continue;
                }

                if (line == "}")
                {
                    if (path.Count > 0)
                    {
                        path.RemoveAt(path.Count - 1);
                    }

                    continue;
                }

                int equals = line.IndexOf('=');

                if (equals <= 0)
                {
                    continue;
                }

                // The outer block, pstamp, is left out of the path.
                string key = string.Join("/", path.Skip(1).Concat(new[] { line.Substring(0, equals).Trim() }));
                string value = line.Substring(equals + 1);

                if (value.StartsWith("\"", StringComparison.Ordinal))
                {
                    value = value.Substring(1).TrimEnd('"');
                }

                if (!values.TryGetValue(key, out List<string> list))
                {
                    values[key] = list = new List<string>();
                }

                list.Add(value);
            }

            return values;
        }

        private void Take(Dictionary<string, List<string>> v)
        {
            Flag(v, "fields/drawing_name", x => DrawingName = x);
            Flag(v, "fields/layout_name", x => LayoutName = x);
            Flag(v, "fields/date_time", x => DateAndTime = x);
            Flag(v, "fields/login_name", x => LoginName = x);
            Flag(v, "fields/device_name", x => DeviceName = x);
            Flag(v, "fields/papersize", x => PaperSize = x);
            Flag(v, "fields/plotscale", x => PlotScale = x);

            if (v.TryGetValue("ud_fields/comment/str", out List<string> fields))
            {
                UserFields = fields;
            }

            if (v.TryGetValue("ud_fields/comment/combo1/selection", out List<string> first))
            {
                UserField1 = first[0];
            }

            if (v.TryGetValue("ud_fields/comment/combo2/selection", out List<string> second))
            {
                UserField2 = second[0];
            }

            Whole(v, "preview/location", x => Location = (CadPlotStampCorner)Math.Max(0, Math.Min(3, x)));
            Whole(v, "preview/orientation", x => Vertical = x == 0);
            Flag(v, "preview/upsidedown", x => UpsideDown = x);
            Real(v, "preview/xoffset", x => OffsetX = x);
            Real(v, "preview/yoffset", x => OffsetY = x);
            Flag(v, "preview/relative_offset", x => OffsetFromPaperBorder = x);
            Real(v, "text_prop/font_height", x => TextHeight = x);
            Flag(v, "text_prop/single_line", x => SingleLine = x);
            Whole(v, "pstamp_units/units", x => Units = (CadPlotStampUnits)Math.Max(0, Math.Min(2, x)));
            Flag(v, "logfile/create_logfile", x => WriteLog = x);

            if (v.TryGetValue("logfile/logfile_name", out List<string> log))
            {
                LogFile = log[0];
            }
        }

        private static void Flag(Dictionary<string, List<string>> v, string key, Action<bool> set) =>
            Whole(v, key, x => set(x != 0));

        private static void Whole(Dictionary<string, List<string>> v, string key, Action<int> set)
        {
            if (
                v.TryGetValue(key, out List<string> list)
                && int.TryParse(list[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            )
            {
                set(value);
            }
        }

        private static void Real(Dictionary<string, List<string>> v, string key, Action<double> set)
        {
            if (
                v.TryGetValue(key, out List<string> list)
                && double.TryParse(list[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                && !double.IsInfinity(value)
            )
            {
                set(value);
            }
        }

        /// <summary>The text packed as a zlib stream: its head, the deflated text and the Adler-32 of the text, high byte first.</summary>
        private static byte[] Zlib(byte[] text)
        {
            using (var packed = new MemoryStream())
            {
                packed.WriteByte(0x78);
                packed.WriteByte(0x9C);

                using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, true))
                {
                    deflate.Write(text, 0, text.Length);
                }

                uint sum = Adler32(text, 0, text.Length);
                packed.WriteByte((byte)(sum >> 24));
                packed.WriteByte((byte)(sum >> 16));
                packed.WriteByte((byte)(sum >> 8));
                packed.WriteByte((byte)sum);
                return packed.ToArray();
            }
        }

        private static uint Adler32(byte[] data, int start, int count)
        {
            uint a = 1;
            uint b = 0;

            for (int i = start; i < start + count; i++)
            {
                a = (a + data[i]) % 65521;
                b = (b + a) % 65521;
            }

            return (b << 16) | a;
        }
    }
}
