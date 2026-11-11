using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The file EXPORT or -EXPORT asks the window for (<see cref="CadUiRequestEventArgs.Export"/>), or SAVEAS shows its Save
    /// Drawing As dialog for: the title of the file dialog, the types it lists, the type and the file offered. The window
    /// shows a standard Save dialog of those types and sets <see cref="FileName"/> and <see cref="FileType"/> to what was
    /// chosen, or <see cref="FileName"/> to null where it was cancelled.
    /// </summary>
    public sealed class CadExportJob
    {
        /// <summary>The type Bitmap (*.bmp) of <see cref="ExportFileTypes"/>, the one EXPORT writes.</summary>
        public const string Bitmap = "Bitmap (*.bmp)";

        /// <summary>The one type of -EXPORT's file of format Pdf.</summary>
        public const string Pdf = "PDF (*.pdf)";

        /// <summary>The DWG type of Save As, the one the DWG writer writes, listed first as AutoCAD 2021 lists it.</summary>
        public const string Dwg = "AutoCAD 2018 Drawing (*.dwg)";

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
