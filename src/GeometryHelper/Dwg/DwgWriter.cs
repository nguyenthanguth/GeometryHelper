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
    /// ATTDEF, INSERT, alone or as an array, with its ATTRIBs, of one line or of several, SOLID, TRACE, 3DFACE, DIMENSION,
    /// VIEWPORT, HATCH, LEADER, MULTILEADER, TOLERANCE, MLINE, IMAGE, WIPEOUT, ACAD_TABLE, and REGION, 3DSOLID and BODY
    /// with their ACIS data in the data store of the file, and of the objects DICTIONARY, XRECORD, LAYOUT, GROUP,
    /// MLINESTYLE, MLEADERSTYLE, TABLESTYLE, DBCOLOR, IMAGEDEF, RASTERVARIABLES, WIPEOUTVARIABLES, MATERIAL, VISUALSTYLE
    /// (of the form of R2013 and later, its 58 properties), the detail and section view styles, the manager of the
    /// persistent names of subentities beside solids, the TABLECONTENT and TABLEGEOMETRY a table keeps for a round trip, the
    /// data of a dynamic block of a linear parameter and a stretch action (from AutoCAD 2007; a block whose graph holds
    /// another expression is written without it),
    /// and the small ones a new drawing holds, so that a new drawing is written whole. A table goes in the form of
    /// R2010: its cells, their overrides and borders, merged ranges, sizes and
    /// style from the groups DXF keeps of R2007, the cell styles of its rows, columns and whole from the TABLECONTENT it
    /// keeps for a round trip or else from its own overrides (93 to 96), and its table style's cell styles from the
    /// style's rows, then those of its own (101 on) from the CELLSTYLEMAP AutoCAD writes in DXF alone; that map, and the
    /// extension dictionary of the table style that holds it, are not written, as AutoCAD writes no such object in a DWG.
    /// A REGION, 3DSOLID or BODY is written with its ACIS data in the data store of the file: where the section ACDSDATA
    /// holds its SAB, as a DXF of R2013 and later gives it, and where it holds SAT text, as a DXF of R2004 to R2010 gives
    /// it, turned into the SAB AutoCAD 2021 writes for R2018 and named by a GUID, the one its extended data kept (else one
    /// made of its handle, so that the same drawing gives the same bytes); the 11 solids and 3 regions of a drawing of
    /// AutoCAD 2004, 2007 and 2010 written so open in AutoCAD 2021 with <c>AUDIT</c> finding 0 errors and the volumes and
    /// areas of the DXF. The ACIS 4.00 text of R2000 and a SAT with a record the grammars do not place are left out. The
    /// section <c>THUMBNAILIMAGE</c> of the DXF (a bitmap of Windows without its file header, or a PNG file) is written as
    /// the preview of the DWG, the picture of the file in Explorer and in the Open dialog. A SHAPE is written where its DXF
    /// gives its number (group 70, or a number as its name), of the first text style of a file of shapes; one given by
    /// its name alone is left out. The FIELD and FIELDLIST of fields, the SORTENTSTABLE of the draw order of a block and
    /// the DATALINK of a table linked to data are written as AutoCAD writes them, with their class listed where the DXF
    /// lists none, as a drawing made in code does. A record of another type, and a record that has no handle, which a DWG cannot name, is
    /// left out, every handle to it written as 0 and every dictionary entry naming it dropped, and one Warn line of
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
        internal static byte[] ToDwg(string text, string version, out IDictionary<string, int> leftOut) =>
            ToDwg(text, version, out leftOut, null);

        /// <summary>
        /// The bytes of the DWG file of the DXF <paramref name="text"/>, as <see cref="ToDwg(string, string, out IDictionary{string, int})"/>,
        /// how far it has got told to <paramref name="progress"/> as a share from 0 to 1, in thousandths, never going back:
        /// the text taken apart to 0.55, the objects written to 0.95, the file put together to 1.
        /// </summary>
        internal static byte[] ToDwg(
            string text,
            string version,
            out IDictionary<string, int> leftOut,
            IProgress<double> progress
        ) => ToDwg(text, version, out leftOut, progress, null);

        /// <summary>
        /// The bytes of the DWG file of the DXF <paramref name="text"/>, as
        /// <see cref="ToDwg(string, string, out IDictionary{string, int}, IProgress{double})"/>, written as a drawing template
        /// whose description is <paramref name="templateDescription"/> (null or empty for none), as AutoCAD's Template Options
        /// writes one into the file's AcDb:Template section.
        /// </summary>
        internal static byte[] ToDwg(
            string text,
            string version,
            out IDictionary<string, int> leftOut,
            IProgress<double> progress,
            string templateDescription
        )
        {
            string named = Check(version);
            ProgressSteps steps = ProgressSteps.Of(progress);
            DwgDrawing drawing = DwgDrawing.Parse(text, steps);
            drawing.TemplateDescription = templateDescription;
            drawing.SatBodiesToSab(AcisFormat.For(named));
            byte[] bytes = DwgFileWriter.Write(drawing, named, out DwgFileWriter writer, steps);
            steps?.Done();
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
