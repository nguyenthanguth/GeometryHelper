using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The file EXPORT, -EXPORT, EXPORTPDF or IMPORT asks the window for (<see cref="CadUiRequestEventArgs.Export"/>), or
    /// SAVEAS shows its Save Drawing As dialog for: the title of the file dialog, the types it lists, the type and the file
    /// offered. The window shows a standard Save dialog of those types (an Open dialog where <see cref="Opens"/>) and sets
    /// <see cref="FileName"/> and <see cref="FileType"/> to what was chosen, or <see cref="FileName"/> to null where it was
    /// cancelled.
    /// </summary>
    public sealed class CadExportJob
    {
        /// <summary>The type Bitmap (*.bmp) of <see cref="ExportFileTypes"/>, the one EXPORT writes.</summary>
        public const string Bitmap = "Bitmap (*.bmp)";

        /// <summary>The one type of -EXPORT's file of format Pdf.</summary>
        public const string Pdf = "PDF (*.pdf)";

        /// <summary>The one type of IMPORT's file: a DWG or a DXF drawing, brought in as a block.</summary>
        public const string Drawing = "Drawing (*.dwg;*.dxf)";

        /// <summary>The DWG type of Save As, the one the DWG writer writes, listed first as AutoCAD 2021 lists it.</summary>
        public const string Dwg = "AutoCAD 2018 Drawing (*.dwg)";

        // The DXF types of Save As, as AutoCAD 2021 lists them, the newest first, each with the version it writes.
        private static readonly KeyValuePair<string, string>[] DxfTypes =
        {
            new KeyValuePair<string, string>("AutoCAD 2018 DXF (*.dxf)", "AC1032"),
            new KeyValuePair<string, string>("AutoCAD 2013/LT2013 DXF (*.dxf)", "AC1027"),
            new KeyValuePair<string, string>("AutoCAD 2010/LT2010 DXF (*.dxf)", "AC1024"),
            new KeyValuePair<string, string>("AutoCAD 2007/LT2007 DXF (*.dxf)", "AC1021"),
            new KeyValuePair<string, string>("AutoCAD 2004/LT2004 DXF (*.dxf)", "AC1018"),
            new KeyValuePair<string, string>("AutoCAD 2000/LT2000 DXF (*.dxf)", "AC1015"),
            new KeyValuePair<string, string>("AutoCAD R12/LT2 DXF (*.dxf)", "AC1009"),
        };

        /// <summary>
        /// Gets the DXF types of AutoCAD 2021's Save Drawing As and of DXFOUT, the newest first, each a version
        /// <see cref="DxfDatabase.Save(string, string)"/> writes: "AutoCAD 2018 DXF (*.dxf)" down to "AutoCAD R12/LT2 DXF (*.dxf)".
        /// </summary>
        public static IReadOnlyList<string> DxfSaveTypes { get; } =
            Array.AsReadOnly(DxfTypes.Select(t => t.Key).ToArray());

        /// <summary>Gets the DXF type of Save As that writes a version, as AutoCAD 2021 lists it: "AutoCAD 2018 DXF (*.dxf)" for AC1032.</summary>
        /// <param name="version">The version as <c>$ACADVER</c> names it, letters of either case.</param>
        /// <returns>The type; "DXF (*.dxf)" for a version none of them writes, R14 among them.</returns>
        public static string DxfTypeOfVersion(string version)
        {
            string named = (version ?? string.Empty).Trim();

            foreach (KeyValuePair<string, string> type in DxfTypes)
            {
                if (string.Equals(type.Value, named, StringComparison.OrdinalIgnoreCase))
                {
                    return type.Key;
                }
            }

            return "DXF (*.dxf)";
        }

        /// <summary>Gets the version a DXF type of Save As writes: AC1024 for "AutoCAD 2010/LT2010 DXF (*.dxf)".</summary>
        /// <param name="fileType">The type.</param>
        /// <returns>The version; null for the DWG type and for "DXF (*.dxf)", which are written as they are.</returns>
        public static string DxfVersionOfType(string fileType)
        {
            foreach (KeyValuePair<string, string> type in DxfTypes)
            {
                if (string.Equals(type.Key, fileType, StringComparison.Ordinal))
                {
                    return type.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the types of AutoCAD 2021's Export Data dialog, as its help lists them for EXPORT: 3D DWF, 3D DWFx, ACIS,
        /// Bitmap, Block, DXX Extract, Encapsulated PS, IGES, Lithography, Metafile, V7 DGN and V8 DGN. Of these the presenter
        /// writes Bitmap alone; EXPORT refuses the others.
        /// </summary>
        public static IReadOnlyList<string> ExportFileTypes { get; } =
            Array.AsReadOnly(
                new[]
                {
                    "3D DWF (*.dwf)",
                    "3D DWFx (*.dwfx)",
                    "ACIS (*.sat)",
                    Bitmap,
                    "Block (*.dwg)",
                    "DXX Extract (*.dxx)",
                    "Encapsulated PS (*.eps)",
                    "IGES (*.iges;*.igs)",
                    "Lithography (*.stl)",
                    "Metafile (*.wmf)",
                    "V7 DGN (*.dgn)",
                    "V8 DGN (*.dgn)",
                }
            );

        /// <summary>Makes the file a command asks the window for.</summary>
        /// <param name="title">The title of the dialog: "Export Data" or "Save As PDF".</param>
        /// <param name="fileTypes">The types the dialog lists, each as "Bitmap (*.bmp)".</param>
        /// <param name="fileType">The type chosen first, one of <paramref name="fileTypes"/>.</param>
        /// <param name="fileName">The file offered, with its folder.</param>
        /// <exception cref="ArgumentNullException">Thrown when the title or the types are null.</exception>
        /// <exception cref="ArgumentException">Thrown when the type is not one of the types.</exception>
        public CadExportJob(string title, IReadOnlyList<string> fileTypes, string fileType, string fileName)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            FileTypes = fileTypes ?? throw new ArgumentNullException(nameof(fileTypes));

            if (!fileTypes.Contains(fileType))
            {
                throw new ArgumentException("The type is not one of the types listed.", nameof(fileType));
            }

            FileType = fileType;
            FileName = fileName;
        }

        /// <summary>
        /// Gets or sets whether the dialog asks for a file to read, as IMPORT's Import File does, the window showing an Open
        /// dialog that takes only a file that exists; false, as it starts, it is a Save dialog.
        /// </summary>
        public bool Opens { get; set; }

        /// <summary>
        /// Gets what EXPORTPDF's Save As PDF dialog sets beside the file (Output Controls, Export, Page Setup, Options); null
        /// for any other file. A window that shows only a file dialog leaves it as made.
        /// </summary>
        public CadExportPdfOptions PdfOptions { get; internal set; }

        /// <summary>Gets the title of the dialog.</summary>
        public string Title { get; }

        /// <summary>Gets the types the dialog lists, each as "Bitmap (*.bmp)".</summary>
        public IReadOnlyList<string> FileTypes { get; }

        /// <summary>Gets or sets the type chosen: the one offered until the window sets the one its dialog chose.</summary>
        public string FileType { get; set; }

        /// <summary>
        /// Gets or sets the file: the one offered until the window sets the one its dialog chose, or null where it was
        /// cancelled.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>The extensions of a type, with their dots, as listed in its brackets: .iges and .igs of "IGES (*.iges;*.igs)".</summary>
        /// <param name="fileType">The type.</param>
        /// <returns>The extensions; empty for a type of no brackets.</returns>
        public static IReadOnlyList<string> ExtensionsOf(string fileType)
        {
            int open = (fileType ?? string.Empty).LastIndexOf('(');
            int close = (fileType ?? string.Empty).LastIndexOf(')');

            if (open < 0 || close < open)
            {
                return Array.Empty<string>();
            }

            return fileType
                .Substring(open + 1, close - open - 1)
                .Split(';')
                .Select(p => p.Trim().TrimStart('*'))
                .Where(p => p.StartsWith(".", StringComparison.Ordinal))
                .ToList();
        }
    }
}
