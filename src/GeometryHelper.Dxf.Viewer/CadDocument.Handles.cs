using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The handles AutoCAD gives the entities of a drawing that writes none, on opening it.</summary>
    public sealed partial class CadDocument
    {
        // The records of the ENTITIES and BLOCKS sections that are not entities, given no handle here.
        private static readonly HashSet<string> NotEntities = new HashSet<string>(StringComparer.Ordinal)
        {
            "SECTION",
            "ENDSEC",
            "BLOCK",
            "ENDBLK",
            "EOF",
        };

        /// <summary>
        /// Gives each entity of a drawing that writes no handles, as one of R12 written by hand with neither header nor tables,
        /// a handle of its own, as AutoCAD gives one to each on opening such a file: the next above every handle of the file,
        /// in the order of the file, with <c>$HANDSEED</c> above them and, in a drawing of R12, <c>$HANDLING</c> 1, so that an
        /// entity added later is given one too. The document picks, snaps and edits by handle, and an undo reads the drawing
        /// back from its saved bytes, so the handles must be written to hold across it; the drawing is read anew with them,
        /// and saved with them, as AutoCAD saves it. A drawing that writes handles is given back as it is.
        /// </summary>
        private static DxfDatabase WithHandles(DxfDatabase database)
        {
            if (database.File.IsDetached || database.File.WritesHandles)
            {
                return database;
            }

            var raw = new MemoryStream();
            database.Save(raw);
            Encoding latin = Encoding.GetEncoding(28591);
            string text = latin.GetString(raw.GetBuffer(), 0, (int)raw.Length);
            string newline = text.Contains("\r\n") ? "\r\n" : "\n";
            string[] lines = text.Split('\n');
            var wanting = new List<int>();
            ulong top = 0;
            string section = null;
            string version = null;
            int header = -1;
            int handling = -1;
            int seed = -1;
            int record = -1;
            bool owned = false;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                string code = lines[i].Trim();
                string value = lines[i + 1].Trim();

                if (code == "0")
                {
                    if (record >= 0 && !owned)
                    {
                        wanting.Add(record);
                    }

                    record =
                        section != null
                        && (section == "ENTITIES" || section == "BLOCKS")
                        && !NotEntities.Contains(value)
                            ? i
                            : -1;
                    owned = false;

                    if (value == "ENDSEC")
                    {
                        section = null;
                    }
                }
                else if (code == "2" && section == null && i >= 2 && lines[i - 1].Trim() == "SECTION")
                {
                    section = value;
                    header = value == "HEADER" ? i : header;
                }
                else if (code == "9" && section == "HEADER")
                {
                    handling = value == "$HANDLING" ? i : handling;
                    seed = value == "$HANDSEED" ? i : seed;
                    version = value == "$ACADVER" && i + 3 < lines.Length ? lines[i + 3].Trim() : version;
                }
                else if (code == "5" || code == "105")
                {
                    owned = true;

                    if (
                        ulong.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong handle)
                        && handle > top
                    )
                    {
                        top = handle;
                    }
                }
            }

            if (wanting.Count == 0)
            {
                return database;
            }

            // Each line, a handle put after the type of each entity that has none; the header's handles before the first line.
            bool r12 = version == null || string.CompareOrdinal(version, "AC1012") < 0;
            string next = (top + (ulong)wanting.Count + 1).ToString("X", CultureInfo.InvariantCulture);
            var made = new StringBuilder(text.Length + wanting.Count * 16 + 128);
            int at = 0;

            if (header < 0)
            {
                made.Append(
                        string.Join(
                            newline,
                            "  0",
                            "SECTION",
                            "  2",
                            "HEADER",
                            "  9",
                            "$HANDLING",
                            " 70",
                            "     1",
                            "  9",
                            "$HANDSEED",
                            "  5",
                            next,
                            "  0",
                            "ENDSEC"
                        )
                    )
                    .Append(newline);
            }

            for (int i = 0; i < lines.Length; i++)
            {
                made.Append(lines[i]);

                if (i < lines.Length - 1)
                {
                    made.Append('\n');
                }

                if (at < wanting.Count && wanting[at] + 1 == i)
                {
                    made.Append("  5")
                        .Append(newline)
                        .Append((top + (ulong)at + 1).ToString("X", CultureInfo.InvariantCulture))
                        .Append(newline);
                    at++;
                }
                else if (header >= 0 && i == header + 1 && seed < 0)
                {
                    made.Append("  9")
                        .Append(newline)
                        .Append("$HANDSEED")
                        .Append(newline)
                        .Append("  5")
                        .Append(newline)
                        .Append(next)
                        .Append(newline);

                    if (r12 && handling < 0)
                    {
                        made.Append("  9")
                            .Append(newline)
                            .Append("$HANDLING")
                            .Append(newline)
                            .Append(" 70")
                            .Append(newline)
                            .Append("     1")
                            .Append(newline);
                    }
                }
                else if (i == seed + 3 && seed >= 0)
                {
                    // The value of $HANDSEED is the line after its group code: written again as the next handle.
                    made.Length -= lines[i].Length + 1;
                    made.Append(next).Append(lines[i].EndsWith("\r", StringComparison.Ordinal) ? "\r\n" : "\n");
                }
                else if (r12 && i == handling + 3 && handling >= 0)
                {
                    made.Length -= lines[i].Length + 1;
                    made.Append("     1").Append(lines[i].EndsWith("\r", StringComparison.Ordinal) ? "\r\n" : "\n");
                }
            }

            return DxfDatabase.Open(new MemoryStream(latin.GetBytes(made.ToString())));
        }
    }
}
