using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// Writes a drawing as a DWG file, the mirror of <see cref="DwgReader"/>: a <see cref="DxfDatabase"/> with every record
    /// it holds, or a <see cref="DxfDocument"/> as the drawing <see cref="DxfWriter"/> makes of its shapes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The drawing is written as its DXF, and each record of that DXF is encoded into the object of the DWG that says the
    /// same, with the handles of the drawing: the header variables, the classes, the nine tables, the blocks, the entities
    /// and the objects. Written are the tables and their records, the blocks, LINE, POINT, CIRCLE, ARC, ELLIPSE, SPLINE,
    /// RAY, XLINE, LWPOLYLINE, the 2D and 3D POLYLINE with their vertices, the polygon and polyface mesh, TEXT, MTEXT,
    /// ATTDEF, INSERT, alone or as an array, with its ATTRIBs of one line, SOLID, TRACE, 3DFACE, DIMENSION, VIEWPORT, HATCH,
    /// LEADER, TOLERANCE and MLINE, and of the objects DICTIONARY, XRECORD, LAYOUT, GROUP, MLINESTYLE, DBCOLOR and the small
    /// ones a new drawing holds. A record of another type, MATERIAL, VISUALSTYLE, MULTILEADER, IMAGE and 3DSOLID among them,
    /// is left out, every handle to it written as 0 and every dictionary entry naming it dropped, and one Warn line of
    /// <see cref="GeometryHelperLog"/> counts them by type.
    /// </para>
    /// <para>
    /// The file is of AC1032 (AutoCAD 2018 and later), the form AutoCAD 2021 saves in. The same bytes come of the same
    /// drawing every time.
    /// </para>
    /// <code>
    /// DxfDatabase database = DxfDatabase.Open("plan.dxf");
    /// DwgWriter.Write(database, "plan.dwg");
    /// </code>
    /// </remarks>
    public static class DwgWriter
    {
        /// <summary>The version written where none is given: AC1032, AutoCAD 2018 and later.</summary>
        public const string DefaultVersion = "AC1032";

        /// <summary>
        /// Writes a drawing database as a DWG file at a path, replacing whatever is there.
        /// </summary>
        /// <param name="database">The drawing.</param>
        /// <param name="path">The path of the file.</param>
        /// <param name="version">The version of the file; AC1032 (AutoCAD 2018) is written.</param>
        /// <exception cref="ArgumentNullException">Thrown when the drawing or the path is null.</exception>
        /// <exception cref="NotSupportedException">Thrown when the version is not written; the message names it.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
        public static void Write(DxfDatabase database, string path, string version = DefaultVersion)
        {
            Need(database, nameof(database));
            Need(path, nameof(path));
            File.WriteAllBytes(path, ToDwg(database.ToText(), version));
        }

        /// <summary>
        /// Writes a drawing database as a DWG file to a stream, where it stands.
        /// </summary>
        /// <param name="database">The drawing.</param>
        /// <param name="stream">The stream; it is written to and left open.</param>
        /// <param name="version">The version of the file; AC1032 (AutoCAD 2018) is written.</param>
        /// <exception cref="ArgumentNullException">Thrown when the drawing or the stream is null.</exception>
        /// <exception cref="NotSupportedException">Thrown when the version is not written; the message names it.</exception>
        public static void Write(DxfDatabase database, Stream stream, string version = DefaultVersion)
        {
            Need(database, nameof(database));
            Need(stream, nameof(stream));
            byte[] bytes = ToDwg(database.ToText(), version);
            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Writes the shapes of a document as a DWG file at a path, replacing whatever is there: the drawing
        /// <see cref="DxfWriter"/> makes of them, each on its layer.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="path">The path of the file.</param>
        /// <param name="version">The version of the file; AC1032 (AutoCAD 2018) is written.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document or the path is null.</exception>
        /// <exception cref="NotSupportedException">Thrown when the version is not written; the message names it.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
        public static void Write(DxfDocument document, string path, string version = DefaultVersion)
        {
            Need(document, nameof(document));
            Need(path, nameof(path));
            File.WriteAllBytes(path, ToDwg(TextOf(document, version), version));
        }

        /// <summary>
        /// Writes the shapes of a document as a DWG file to a stream, where it stands.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="stream">The stream; it is written to and left open.</param>
        /// <param name="version">The version of the file; AC1032 (AutoCAD 2018) is written.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document or the stream is null.</exception>
        /// <exception cref="NotSupportedException">Thrown when the version is not written; the message names it.</exception>
        public static void Write(DxfDocument document, Stream stream, string version = DefaultVersion)
        {
            Need(document, nameof(document));
            Need(stream, nameof(stream));
            byte[] bytes = ToDwg(TextOf(document, version), version);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static string TextOf(DxfDocument document, string version)
        {
            Check(version);
            var writer = new DxfWriter { Version = version.Trim().ToUpperInvariant(), CodePage = "ANSI_1252" };

            foreach (DxfShape shape in document.Shapes)
            {
                writer.Add(shape);
            }

            return writer.ToString();
        }

        private static void Need(object value, string name)
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }
        }

        private static string Check(string version)
        {
            Need(version, nameof(version));
            string named = version.Trim().ToUpperInvariant();

            if (named != "AC1032")
            {
                throw new NotSupportedException(
                    "A DWG file of "
                        + named
                        + " ("
                        + DwgContainer.ReleaseOf(named)
                        + ") is not written; AC1032 (AutoCAD 2018) is."
                );
            }

            return named;
        }

        /// <summary>The bytes of the DWG file of the DXF <paramref name="text"/> in <paramref name="version"/>.</summary>
        internal static byte[] ToDwg(string text, string version) => ToDwg(text, version, out _);

        /// <summary>
        /// The bytes of the DWG file of the DXF <paramref name="text"/> in <paramref name="version"/>, with the types of record
        /// left out, each with how many, in ordinal order of the type.
        /// </summary>
        internal static byte[] ToDwg(string text, string version, out IDictionary<string, int> leftOut)
        {
            string named = Check(version);
            DwgDrawing drawing = DwgDrawing.Parse(text);
            byte[] bytes = DwgFileWriter.Write(drawing, named, out DwgFileWriter writer);
            leftOut = writer.LeftOut;
            int left = 0;

            if (writer.LeftOut.Count > 0)
            {
                var line = new StringBuilder("DwgWriter: types that are not written yet were left out:");

                foreach (KeyValuePair<string, int> pair in writer.LeftOut)
                {
                    line.Append(' ')
                        .Append(pair.Key)
                        .Append(' ')
                        .Append(pair.Value.ToString(CultureInfo.InvariantCulture));
                    left += pair.Value;
                }

                GeometryHelperLog.Warn(line.ToString());
            }

            GeometryHelperLog.Info(
                "DwgWriter: wrote a DWG of "
                    + DwgContainer.ReleaseOf(named)
                    + " with "
                    + writer.Written.ToString(CultureInfo.InvariantCulture)
                    + " objects, "
                    + left.ToString(CultureInfo.InvariantCulture)
                    + " of types not written yet left out, "
                    + bytes.Length.ToString(CultureInfo.InvariantCulture)
                    + " bytes."
            );
            return bytes;
        }
    }
}
