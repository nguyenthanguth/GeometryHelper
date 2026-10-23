using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Opens the files the library writes as a <see cref="Scene"/>: Wavefront OBJ (<c>ObjWriter</c>), DXF (<c>DxfWriter</c>), well-known text
    /// (<c>Wkt</c>) and SVG (<c>SvgWriter</c>). Each reader takes what its writer writes, and what other programs write of the same kinds; a
    /// part of a file it does not take is left out and said in <see cref="Scene.LoadWarnings"/>, not dropped quietly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The numbers are read as the file prints them, to the bit where the format keeps a double whole (OBJ, DXF and WKT as the library writes
    /// them; SVG prints six places and comes back to within five ten-millionths). A shape of an OBJ, a WKT or an SVG is kept as the numbers that were
    /// read, with no library constructor between, so a polygon of two points or a face a hair off flat is drawn as the file has it. A DXF is read by
    /// the library's <c>DxfReader</c>, so its shapes are those the library reads: a shape lying on the floor as the shape of the plane it is, each
    /// INSERT as the entities of its block placed where it puts them, a TEXT or an MTEXT as a label, and an entity with no shape to it left out and
    /// counted. A DXF file is handed to <c>DxfReader.Read</c> as its bytes, which decodes them as the library does: UTF-8 from R2007, and before that
    /// the code page the header names, so the layer St€l of a file of R2000 in Windows-1252 is St€l; the viewer gives the runtime the code pages of
    /// Windows, so one in Windows-1250 or any other is read in it too.
    /// </para>
    /// <para>
    /// What a file cannot say, it does not: an OBJ has no polygon and no solid, only faces and lines, so a body comes back as a mesh
    /// (<c>GeoMesh3</c>); a DXF has no polygon with holes, so a face comes back as the loops of it; an SVG has no point, so a dot is a small circle
    /// without an outline. A DXF's 3DFACEs share a corner where it is the same to the bit; an OBJ's triangles are written with vertices of their own. What makes
    /// faces one body is neither: it is the edges they use, matched within the tolerance of the scene (a body, a body with a void inside it, a body that lies against another), so
    /// each body of a layer or of an object is an item, however many corners it shares with its neighbours.
    /// </para>
    /// </remarks>
    public static class SceneImport
    {
        private static readonly string[] _extensions = { ".obj", ".dxf", ".wkt", ".svg" };

        /// <summary>
        /// Gives the runtime the code pages of Windows, once, before any file is read: .NET 8 has Unicode, ASCII and Latin-1
        /// alone, and a DXF before R2007 is written in the code page its header names, Windows-1250 to 1258 and the Asian ones.
        /// </summary>
        static SceneImport()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// Gets the extensions of the files that can be opened, with their dots, in lower case.
        /// </summary>
        public static IReadOnlyList<string> Extensions => _extensions;

        /// <summary>
        /// Gets whether a path is a file this reader opens, by its extension.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>true when it ends in .obj, .dxf, .wkt or .svg, in either case; otherwise, false.</returns>
        public static bool IsImportable(string path)
        {
            string extension = path == null ? string.Empty : Path.GetExtension(path);
            return Array.Exists(_extensions, e => string.Equals(e, extension, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Reads a file into a scene titled with the name of the file.
        /// </summary>
        /// <param name="path">The path of an OBJ, DXF, WKT or SVG file.</param>
        /// <param name="cancel">Stops the reading, by throwing <see cref="OperationCanceledException"/>.</param>
        /// <returns>The scene, with what was left out in <see cref="Scene.LoadWarnings"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="FormatException">Thrown when the extension is not one of <see cref="Extensions"/>, or the file is not of the format it says.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        public static Scene ReadFile(string path, CancellationToken cancel = default(CancellationToken))
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            string format = Path.GetExtension(path).ToLowerInvariant();

            if (!IsImportable(path))
            {
                throw new FormatException("The viewer opens " + string.Join(", ", _extensions) + " files; '" + Path.GetFileName(path) + "' is not one.");
            }

            if (format == ".dxf")
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    return DxfReader.Read(stream, Path.GetFileName(path), cancel);
                }
            }

            byte[] bytes = File.ReadAllBytes(path);
            return ReadText(Decode(bytes), format, Path.GetFileName(path), cancel);
        }

        /// <summary>
        /// Reads the text of a file of one of the formats into a scene.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="format">The format: obj, dxf, wkt or svg, with or without a dot.</param>
        /// <param name="title">The title of the scene; none when null.</param>
        /// <param name="cancel">Stops the reading, by throwing <see cref="OperationCanceledException"/>.</param>
        /// <returns>The scene.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the text or the format is null.</exception>
        /// <exception cref="FormatException">Thrown when the format is not one of the four, or the text is not of the format.</exception>
        public static Scene ReadText(string text, string format, string title = null, CancellationToken cancel = default(CancellationToken))
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (format == null)
            {
                throw new ArgumentNullException(nameof(format));
            }

            switch (format.TrimStart('.').ToLowerInvariant())
            {
                case "obj": return ObjReader.Read(text, title, cancel);
                case "dxf": return DxfReader.Read(text, title, cancel);
                case "wkt": return WktReader.Read(text, title, cancel);
                case "svg": return SvgReader.Read(text, title, cancel);
                default: throw new FormatException("The viewer reads obj, dxf, wkt and svg; '" + format + "' is not one.");
            }
        }

        /// <summary>
        /// Decodes the bytes of a file: UTF-8, with or without the mark.
        /// </summary>
        private static string Decode(byte[] bytes)
        {
            try
            {
                return new UTF8Encoding(false, true).GetString(bytes).TrimStart('﻿');
            }
            catch (DecoderFallbackException)
            {
                return Encoding.UTF8.GetString(bytes).TrimStart('﻿');
            }
        }
    }
}
