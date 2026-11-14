using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>TABLEEXPORT, a table written to a CSV file, as AutoCAD 2021's Export of the table menu runs it.</summary>
    internal static partial class CadCommands
    {
        /// <summary>The type of the file TABLEEXPORT's Export Data dialog lists.</summary>
        private const string CsvFileType = "Comma Delimited (*.csv)";

        internal static void AddTableExportTo(CadCommandRegistry registry) =>
            registry.Add(new CadCommand("TABLEEXPORT", TableExport));

        /// <summary>
        /// TABLEEXPORT: "Select a table:", asked again with "Object selected is not a table." for any other object; then the
        /// file, by the window's Export Data dialog or, with no window or FILEDIA 0, "Enter file name &lt;file&gt;:", and the
        /// table written to it (<see cref="CadTableCsv"/>), ".csv" added to a name with no extension, in the system's code page
        /// as AutoCAD writes it. Nothing is said when it is written and the drawing is not changed, so there is no undo step;
        /// a file that cannot be written gives "Can't  open file.", in AutoCAD's own two spaces.
        /// </summary>
        private static IEnumerable<CadPrompt> TableExport(CadCommandContext c)
        {
            DxfTable table = null;

            while (table == null)
            {
                yield return CadPrompt.ForEntity("Select a table");
                string handle = c.Input.Handles?.FirstOrDefault();

                if (handle == null)
                {
                    yield break;
                }

                table = c.Document.FindEntity(handle) as DxfTable;

                if (table == null)
                {
                    c.Write("Object selected is not a table.");
                }
            }

            string offered = ImageFileName(c.Document, ".csv");
            var job = new CadExportJob("Export Data", new[] { CsvFileType }, CsvFileType, offered);
            string file;

            if (c.Session.FileDialogs && c.Session.Request("TABLEEXPORT", c.Document, job))
            {
                if (job.FileName == null)
                {
                    yield break;
                }

                file = job.FileName;
            }
            else
            {
                while (true)
                {
                    yield return CadPrompt.ForString("Enter file name", offered);
                    file =
                        c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0
                            ? c.Input.Text.Trim().Trim('"')
                            : offered;

                    if (file.IndexOfAny(Path.GetInvalidPathChars()) < 0)
                    {
                        break;
                    }

                    c.Write("Invalid file name.");
                }

                if (!Path.IsPathRooted(file))
                {
                    file = Path.Combine(Path.GetDirectoryName(offered) ?? string.Empty, file);
                }
            }

            if (!Path.HasExtension(file))
            {
                file += ".csv";
            }

            try
            {
                File.WriteAllText(file, CadTableCsv.Of(table), Encoding.Default);
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                    || exception is NotSupportedException
                )
            {
                c.Write("Can't  open file.");
            }
        }
    }
}
