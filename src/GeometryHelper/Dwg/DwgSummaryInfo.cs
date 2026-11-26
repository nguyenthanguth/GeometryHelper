using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GeometryHelper.Export;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// The section <c>AcDb:SummaryInfo</c> of a DWG of R2004 or later, where AutoCAD keeps the properties of the drawing that
    /// DWGPROPS shows, as AutoCAD 2021 writes it (read from the files it saved of R2004, R2007, R2010, R2013 and R2018, the same
    /// in all but the letters). Eight texts, each a count of characters with the end mark, then the characters and the end
    /// mark: UTF-16 in R2007 and later, the bytes of the drawing's code page in R2004. They are the title, subject, author,
    /// keywords, comments, who saved it last, the revision and the base of the hyperlinks. Then the time spent editing as two
    /// whole numbers (days, milliseconds), the date it was made and the date it was saved, each a Julian day number and the
    /// milliseconds of that day in universal time, a count of custom properties (16 bits), each a name and a value as texts,
    /// and two whole numbers of nought. A drawing of R2000 has no such section: its properties are the DWGPROPS record among
    /// its objects.
    /// </summary>
    internal static class DwgSummaryInfo
    {
        private const double MillisecondsPerDay = 86400000.0;

        /// <summary>The names of the header variables the eight texts are of, in the order of the section.</summary>
        private static readonly string[] TextVariables =
        {
            "TITLE",
            "SUBJECT",
            "AUTHOR",
            "KEYWORDS",
            "COMMENTS",
            "LASTSAVEDBY",
            "REVISIONNUMBER",
            "HYPERLINKBASE",
        };

        /// <summary>What a section holds, its texts as the bytes the DXF of the drawing writes them in (UTF-8 from R2007, the code page's before).</summary>
        internal sealed class Fields
        {
            /// <summary>The eight texts: title, subject, author, keywords, comments, last saved by, revision, base of the hyperlinks.</summary>
            public readonly List<byte[]> Texts = new List<byte[]>();

            /// <summary>The custom properties, each its name and its value.</summary>
            public readonly List<KeyValuePair<byte[], byte[]>> Custom = new List<KeyValuePair<byte[], byte[]>>();
        }

        /// <summary>
        /// Reads a section; null where it is cut short, as a file with a broken section is still a drawing.
        /// </summary>
        /// <param name="data">The data of the section.</param>
        /// <param name="unicode">Whether the strings are UTF-16 (R2007 and later) rather than the bytes of the code page.</param>
        public static Fields Read(byte[] data, bool unicode)
        {
            if (data == null)
            {
                return null;
            }

            try
            {
                var fields = new Fields();
                int at = 0;

                for (int i = 0; i < TextVariables.Length; i++)
                {
                    fields.Texts.Add(Text(data, ref at, unicode));
                }

                // The time spent, the dates it was made and saved: two whole numbers each.
                at += 6 * 4;
                int count = Whole16(data, ref at);

                for (int i = 0; i < count; i++)
                {
                    byte[] name = Text(data, ref at, unicode);
                    byte[] value = Text(data, ref at, unicode);
                    fields.Custom.Add(new KeyValuePair<byte[], byte[]>(name, value));
                }

                return fields;
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// Writes the section of a drawing from its header: the texts of <c>$TITLE</c>, <c>$SUBJECT</c>, <c>$AUTHOR</c>,
        /// <c>$KEYWORDS</c>, <c>$COMMENTS</c>, <c>$LASTSAVEDBY</c>, <c>$REVISIONNUMBER</c> and <c>$HYPERLINKBASE</c>, the time
        /// spent from <c>$TDINDWG</c>, the dates from <c>$TDUCREATE</c> and <c>$TDUUPDATE</c> (the local ones where those are
        /// not in the header), and the custom properties.
        /// </summary>
        /// <param name="drawing">The drawing.</param>
        /// <param name="unicode">Whether the strings are written as UTF-16 (R2007 and later) rather than in the code page.</param>
        public static byte[] Write(DwgDrawing drawing, bool unicode)
        {
            var section = new MemoryStream();

            foreach (string variable in TextVariables)
            {
                Text(section, drawing, drawing.HeaderText(variable, string.Empty).Trim(), unicode);
            }

            double spent = Days(drawing.HeaderText("TDINDWG", "0"));
            int days = (int)Math.Floor(spent);
            Whole32(section, days);
            Whole32(section, (int)Math.Round((spent - days) * MillisecondsPerDay));
            Date(section, drawing.Header("TDUCREATE") != null ? "TDUCREATE" : "TDCREATE", drawing);
            Date(section, drawing.Header("TDUUPDATE") != null ? "TDUUPDATE" : "TDUPDATE", drawing);
            Whole16(section, Math.Min(ushort.MaxValue, drawing.CustomProperties.Count));

            for (int i = 0; i < drawing.CustomProperties.Count && i < ushort.MaxValue; i++)
            {
                Text(section, drawing, drawing.CustomProperties[i].Key, unicode);
                Text(section, drawing, drawing.CustomProperties[i].Value, unicode);
            }

            Whole32(section, 0);
            Whole32(section, 0);
            return section.ToArray();
        }

        /// <summary>The page size AutoCAD gives the section: its length up to the next 128 bytes.</summary>
        public static int PageSize(byte[] section) => Math.Max(0x80, (section.Length + 0x7F) & ~0x7F);

        private static byte[] Text(byte[] data, ref int at, bool unicode)
        {
            int length = Whole16(data, ref at);

            if (length == 0)
            {
                return new byte[0];
            }

            int size = length * (unicode ? 2 : 1);

            if (at + size > data.Length)
            {
                throw new IndexOutOfRangeException();
            }

            // The count includes the end mark, which is not part of the text.
            int body = size - (unicode ? 2 : 1);
            byte[] text = unicode ? Encoding.UTF8.GetBytes(Encoding.Unicode.GetString(data, at, body)) : new byte[body];

            if (!unicode)
            {
                Buffer.BlockCopy(data, at, text, 0, body);
            }

            at += size;
            return text;
        }

        private static void Text(Stream section, DwgDrawing drawing, string text, bool unicode)
        {
            byte[] bytes = unicode ? Encoding.Unicode.GetBytes(text ?? string.Empty) : drawing.PageBytes(text);
            int unit = unicode ? 2 : 1;
            Whole16(section, Math.Min(ushort.MaxValue, bytes.Length / unit + 1));
            section.Write(bytes, 0, bytes.Length);
            section.Write(new byte[unit], 0, unit);
        }

        private static void Date(Stream section, string variable, DwgDrawing drawing)
        {
            double julian = Days(drawing.HeaderText(variable, "0"));
            int day = (int)Math.Floor(julian);
            Whole32(section, day);
            Whole32(section, (int)Math.Round((julian - day) * MillisecondsPerDay));
        }

        private static double Days(string text) =>
            Numbers.TryRead(text, out double value)
            && !double.IsNaN(value)
            && !double.IsInfinity(value)
            && value >= 0.0
            && value < int.MaxValue
                ? value
                : 0.0;

        private static int Whole16(byte[] data, ref int at)
        {
            if (at + 2 > data.Length)
            {
                throw new IndexOutOfRangeException();
            }

            int value = data[at] | (data[at + 1] << 8);
            at += 2;
            return value;
        }

        private static void Whole16(Stream section, int value)
        {
            section.WriteByte((byte)value);
            section.WriteByte((byte)(value >> 8));
        }

        private static void Whole32(Stream section, int value)
        {
            for (int i = 0; i < 4; i++)
            {
                section.WriteByte((byte)(value >> (8 * i)));
            }
        }
    }
}
