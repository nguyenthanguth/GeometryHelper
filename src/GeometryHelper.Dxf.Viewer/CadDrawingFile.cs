using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The drawing files the presenter opens, attaches and inserts: a DWG, read by <see cref="DwgReader"/>, of AutoCAD 2000
    /// to 2018, and a DXF. A file is read as a DWG where its first bytes say so ("AC1032" for AutoCAD 2018), whatever its
    /// name; a drawing saved to a name ending in .dwg is written by <see cref="DwgWriter"/> as a DWG of AutoCAD 2018, any
    /// other name as a DXF.
    /// </summary>
    public static class CadDrawingFile
    {
        /// <summary>
        /// The filter of the file dialogs that pick a drawing, DWG first as in AutoCAD 2021's Select File: Drawing (*.dwg),
        /// DXF (*.dxf), All files (*.*).
        /// </summary>
        public const string OpenFilter = "Drawing (*.dwg)|*.dwg|DXF (*.dxf)|*.dxf|All files (*.*)|*.*";

        /// <summary>Gets whether a file's name ends in .dwg or .dxf, of either case: plan.dwg and PLAN.DXF, not plan.dwt.</summary>
        /// <param name="path">The name or path of the file; null is none.</param>
        /// <returns>Whether it names a drawing.</returns>
        public static bool IsDrawingName(string path) => IsDwgName(path) || HasExtension(path, ".dxf");

        /// <summary>
        /// Gets whether a file is a DWG: its first six bytes AC1 and three more digits, as "AC1015" of AutoCAD 2000 and
        /// "AC1032" of AutoCAD 2018 are. A DXF, of text or binary, never starts so.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <returns>Whether the file is a DWG; false for a file shorter than six bytes.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        public static bool IsDwg(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var start = new byte[6];
            int read = 0;

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                while (read < start.Length)
                {
                    int got = stream.Read(start, read, start.Length - read);

                    if (got == 0)
                    {
                        return false;
                    }

                    read += got;
                }
            }

            return start[0] == 'A'
                && start[1] == 'C'
                && start[2] == '1'
                && char.IsDigit((char)start[3])
                && char.IsDigit((char)start[4])
                && char.IsDigit((char)start[5]);
        }

        /// <summary>
        /// Reads a drawing file as a database: a DWG by <see cref="DwgReader.Open(string)"/>, any other file by
        /// <see cref="DxfDatabase.Open(string)"/>.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <returns>The drawing.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        /// <exception cref="FormatException">Thrown when the file is a DWG that cannot be read.</exception>
        /// <exception cref="NotSupportedException">Thrown when the file is a DWG of a version that is not read, R14 among them; the message names it.</exception>
        public static DxfDatabase Open(string path) => Open(path, null);

        /// <summary>
        /// Reads a drawing file as a database, as <see cref="Open(string)"/> does, telling how far it has got as a share from
        /// 0 to 1: in thousandths, never going back, 0 first and 1 once read. A DWG of 100 000 lines tells it some 1 000 times
        /// as its objects are read and its DXF made; a DXF, read at once from its bytes, tells 0 and 1.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="progress">What is told how far it has got, on the thread that reads; null for nothing.</param>
        /// <returns>The drawing.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        /// <exception cref="FormatException">Thrown when the file is a DWG that cannot be read.</exception>
        /// <exception cref="NotSupportedException">Thrown when the file is a DWG of a version that is not read, R14 among them; the message names it.</exception>
        public static DxfDatabase Open(string path, IProgress<double> progress) =>
            Open(path, progress, CancellationToken.None);

        /// <summary>
        /// Reads a drawing file as <see cref="Open(string, IProgress{double})"/> does, a DXF given up when a token says so:
        /// its walk over the records looks at the token every 4 096 records, so that a stop is answered within a few
        /// milliseconds even for a file of a million entities. A DWG is read as before, the token looked at once before it.
        /// </summary>
        /// <exception cref="OperationCanceledException">Thrown when the token is stopped before the file is read.</exception>
        internal static DxfDatabase Open(string path, IProgress<double> progress, CancellationToken stop)
        {
            if (IsDwg(path))
            {
                stop.ThrowIfCancellationRequested();
                return DwgReader.Open(path, null, progress);
            }

            progress?.Report(0.0);
            DxfDatabase database = DxfDatabase.Open(path, stop);
            progress?.Report(1.0);
            return database;
        }

        /// <summary>Whether a name ends in .dwg, of either case.</summary>
        internal static bool IsDwgName(string path) => HasExtension(path, ".dwg");

        /// <summary>
        /// Saves a database to a path, replacing whatever is there: as a DWG of AutoCAD 2018 where the name ends in .dwg, else
        /// as its DXF. Gives what the DWG left out, as "IMAGE 1, MULTILEADER 2", the types in ordinal order; empty where nothing
        /// was or the file is a DXF. A drawing the DWG writer fails on is refused with a <see cref="NotSupportedException"/>
        /// that says why, as the save commands catch it, and the file is not touched.
        /// </summary>
        internal static string Save(DxfDatabase database, string path) => Save(database, path, null, null, null);

        /// <summary>
        /// Saves a database as <see cref="Save(DxfDatabase, string)"/> does, telling how far it has got as a share from 0 to 1:
        /// in thousandths, never going back, 0 first and 1 at the end. A DWG tells it as its text is taken apart and its objects
        /// written; a DXF, written at once from its bytes, tells 0 and 1. A DXF is written in the version given, as
        /// <see cref="DxfDatabase.Save(string, string)"/> writes it, what could not be written there told in <paramref name="told"/>;
        /// in its own version where none is given. A DWG is written as AutoCAD 2018 only.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown when a version other than AutoCAD 2018 is given for a DWG.</exception>
        internal static string Save(
            DxfDatabase database,
            string path,
            string version,
            IList<string> told,
            IProgress<double> progress
        )
        {
            progress?.Report(0.0);

            if (!IsDwgName(path))
            {
                if (version == null)
                {
                    database.Save(path);
                }
                else
                {
                    foreach (string sentence in database.Save(path, version))
                    {
                        told?.Add(sentence);
                    }
                }

                progress?.Report(1.0);
                return string.Empty;
            }

            if (
                version != null
                && !string.Equals(version, DwgWriter.DefaultVersion, StringComparison.OrdinalIgnoreCase)
            )
            {
                throw new NotSupportedException(
                    "A DWG is written as AutoCAD 2018 (" + DwgWriter.DefaultVersion + ") only, not as " + version + "."
                );
            }
            byte[] bytes;
            IDictionary<string, int> left;

            try
            {
                bytes = DwgWriter.ToDwg(database.ToText(), DwgWriter.DefaultVersion, out left, progress);
            }
            catch (Exception exception)
                when (!(exception is OutOfMemoryException || exception is NotSupportedException))
            {
                GeometryHelperLog.Warn("CadDrawingFile: the drawing could not be written as a DWG.", exception);
                throw new NotSupportedException(
                    "The drawing cannot be written as a DWG: " + exception.Message,
                    exception
                );
            }

            File.WriteAllBytes(path, bytes);
            var said = new StringBuilder();

            foreach (KeyValuePair<string, int> pair in left)
            {
                said.Append(said.Length == 0 ? string.Empty : ", ")
                    .Append(pair.Key)
                    .Append(' ')
                    .Append(pair.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            return said.ToString();
        }

        private static bool HasExtension(string path, string extension) =>
            path != null && path.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
    }
}
