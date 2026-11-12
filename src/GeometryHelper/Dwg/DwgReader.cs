using System;
using System.IO;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// Reads a DWG file as the drawing its DXF would give: the same <see cref="DxfDocument"/> that
    /// <see cref="DxfReader"/> makes and the same <see cref="DxfDatabase"/> that <see cref="DxfDatabase.Open(string)"/>
    /// opens, so everything that works on a DXF works on a DWG.
    /// </summary>
    /// <remarks>
    /// <para>
    /// DWG files of AC1015 (AutoCAD 2000), AC1018 (AutoCAD 2004), AC1021 (AutoCAD 2007), AC1024 (AutoCAD 2010), AC1027
    /// (AutoCAD 2013) and AC1032 (AutoCAD 2018 and later) are read. A file of another version, R14 and older among them,
    /// or one protected by a password, is refused with a <see cref="NotSupportedException"/> that names the version.
    /// </para>
    /// <para>
    /// The file is turned, in memory, into the DXF that says the same, record for record with the handles of the file:
    /// the header variables, the classes, the nine tables, the blocks, the entities and the objects. Read are LINE, POINT,
    /// CIRCLE, ARC, ELLIPSE, SPLINE, RAY, XLINE, LWPOLYLINE, the 2D and 3D POLYLINE with their vertices, the polygon and
    /// polyface mesh, TEXT, MTEXT, ATTDEF, INSERT, alone or as an array, with its ATTRIBs, SOLID, TRACE, 3DFACE and
    /// DIMENSION, and of the objects DICTIONARY, XRECORD, LAYOUT, GROUP, MLINESTYLE, MATERIAL, VISUALSTYLE, the detail
    /// and section view styles and the small ones a new drawing holds (see the guide for the full list); from AutoCAD
    /// 2013 the ACIS data of the solids in the
    /// data store of the file comes as the section ACDSDATA; each with its layer, linetype, colour, lineweight, owner,
    /// reactors, extension dictionary and extended data. The types not read yet, SHAPE and the objects of dynamic blocks
    /// among them, are left out and counted by type in one Debug line of <see cref="GeometryHelperLog"/>.
    /// </para>
    /// <para>
    /// The check sums of the file are checked. A wrong one in the file header, the header variables, the classes or the
    /// object map, or from AutoCAD 2004 in a page of the file (from AutoCAD 2007 to 2009 its CRCs of 64 bits), refuses the file with a <see cref="FormatException"/>, and
    /// so does a file cut short there; one object
    /// with a wrong check sum, or that cannot be decoded, is left out, the others are read, and a Warn line counts them.
    /// </para>
    /// <code>
    /// DxfDocument drawing = DwgReader.Read("plan.dwg", new DxfReadOptions { PlanAs2D = true });
    /// DxfDatabase database = DwgReader.Open("plan.dwg");
    /// </code>
    /// </remarks>
    public static class DwgReader
    {
        /// <summary>
        /// Reads a DWG file from a path into the library's shapes, as <see cref="DxfReader.Read(string, DxfReadOptions)"/>
        /// reads its DXF.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="options">How to read it; null reads it with the defaults of <see cref="DxfReadOptions"/>.</param>
        /// <returns>The entities of the file and what it says of itself.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="FormatException">Thrown when the file is not a DWG, is cut short, or has a wrong check sum where nothing can be read past it.</exception>
        /// <exception cref="NotSupportedException">Thrown when the DWG is of a version that is not read; the message names it.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        public static DxfDocument Read(string path, DxfReadOptions options = null)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            return Read(File.ReadAllBytes(path), options);
        }

        /// <summary>
        /// Reads a DWG file from a stream, from where the stream stands to its end.
        /// </summary>
        /// <param name="stream">The stream; it is read to its end and left open.</param>
        /// <param name="options">How to read it; null reads it with the defaults of <see cref="DxfReadOptions"/>.</param>
        /// <returns>The entities of the file and what it says of itself.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the stream is null.</exception>
        /// <exception cref="FormatException">Thrown when the data is not a DWG, is cut short, or has a wrong check sum where nothing can be read past it.</exception>
        /// <exception cref="NotSupportedException">Thrown when the DWG is of a version that is not read; the message names it.</exception>
        public static DxfDocument Read(Stream stream, DxfReadOptions options = null) => Read(BytesOf(stream), options);

        /// <summary>
        /// Opens a DWG file from a path as a drawing database, as <see cref="DxfDatabase.Open(string)"/> opens its DXF.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <returns>The drawing. It has no file name of its own, and saving it writes a DXF.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="FormatException">Thrown when the file is not a DWG, is cut short, or has a wrong check sum where nothing can be read past it.</exception>
        /// <exception cref="NotSupportedException">Thrown when the DWG is of a version that is not read; the message names it.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        public static DxfDatabase Open(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            return Open(File.ReadAllBytes(path));
        }

        /// <summary>
        /// Opens a DWG file from a stream, from where the stream stands to its end, as a drawing database.
        /// </summary>
        /// <param name="stream">The stream; it is read to its end and left open.</param>
        /// <returns>The drawing. It has no file name of its own, and saving it writes a DXF.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the stream is null.</exception>
        /// <exception cref="FormatException">Thrown when the data is not a DWG, is cut short, or has a wrong check sum where nothing can be read past it.</exception>
        /// <exception cref="NotSupportedException">Thrown when the DWG is of a version that is not read; the message names it.</exception>
        public static DxfDatabase Open(Stream stream) => Open(BytesOf(stream));

        /// <summary>
        /// The DXF that says what the DWG in <paramref name="file"/> says, as the bytes of a text DXF file in the drawing's
        /// code page.
        /// </summary>
        internal static byte[] ToDxf(byte[] file)
        {
            try
            {
                return DwgTranscoder.ToDxf(file);
            }
            catch (Exception fault)
                when (!(fault is FormatException)
                    && !(fault is NotSupportedException)
                    && !(fault is OutOfMemoryException)
                )
            {
                // The decoders check what they read, and this is the net under them: whatever broken data still leads
                // them astray is reported as what it is, a broken file.
                throw new FormatException("The DWG file is broken: " + fault.Message, fault);
            }
        }

        private static DxfDocument Read(byte[] file, DxfReadOptions options)
        {
            using (var dxf = new MemoryStream(ToDxf(file), false))
            {
                return DxfReader.Read(dxf, options);
            }
        }

        private static DxfDatabase Open(byte[] file)
        {
            using (var dxf = new MemoryStream(ToDxf(file), false))
            {
                return DxfDatabase.Open(dxf);
            }
        }

        private static byte[] BytesOf(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using (var copy = new MemoryStream())
            {
                stream.CopyTo(copy);
                return copy.ToArray();
            }
        }
    }
}
