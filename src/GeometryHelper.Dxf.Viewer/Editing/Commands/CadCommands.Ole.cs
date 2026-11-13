using System;
using System.Collections.Generic;
using System.IO;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// INSERTOBJ and the variables of OLE objects: OLEFRAME (their frames shown and plotted), OLEHIDE (the spaces whose objects
    /// are shown and plotted) and OLEQUALITY (the plot quality a new object takes). INSERTOBJ is AutoCAD's Insert Object
    /// dialog reduced to its Create from File: a bitmap or a PNG is made a static picture, an OLE object that needs no other
    /// application to be drawn, at the upper left of the view; the dialog's Create New, Link and Display as Icon, and the
    /// documents of other applications, which need the application's own server, are not built.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>The one type of the file dialog INSERTOBJ shows.</summary>
        private const string PictureType = "Picture (*.bmp;*.png)";

        internal static void AddOleTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("INSERTOBJ", InsertObject));

            // Whether the frames of OLE objects are drawn and plotted: none, shown and plotted, shown and left off a plot.
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "OLEFRAME",
                    false,
                    0,
                    2,
                    s => s.Document.OleFrame,
                    (c, v) =>
                    {
                        c.Document.OleFrame = v;
                        c.Document.Regen();
                    }
                )
            );

            // Which OLE objects are shown and plotted: those of both spaces, of paper space only, of model space only, none.
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "OLEHIDE",
                    false,
                    0,
                    3,
                    s => s.Document.OleHide,
                    (c, v) =>
                    {
                        c.Document.OleHide = v;
                        c.Document.Regen();
                    }
                )
            );

            // The plot quality a new OLE object takes: monochrome, low graphics, high graphics, or by the kind of object.
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "OLEQUALITY",
                    false,
                    0,
                    3,
                    s => s.Document.OleQuality,
                    (c, v) => c.Document.OleQuality = v
                )
            );
        }

        /// <summary>
        /// INSERTOBJ (IO): the window's Open dialog for a bitmap or a PNG, titled Insert Object; with FILEDIA 0, or with no window
        /// to show it, "Enter name of picture file to insert:". The file becomes a static OLE picture at its natural size at
        /// the upper left of the view, one undo step. A file not there is answered "Can't find file."; one that is no .bmp or
        /// .png, or no picture, is refused with the reason, nothing inserted.
        /// </summary>
        private static IEnumerable<CadPrompt> InsertObject(CadCommandContext c)
        {
            var job = new CadExportJob("Insert Object", new[] { PictureType }, PictureType, null) { Opens = true };
            string file;

            if (c.Session.FileDialogs && c.Session.Request("INSERTOBJ", c.Document, job))
            {
                if (job.FileName == null)
                {
                    yield break;
                }

                file = job.FileName;
            }
            else
            {
                yield return CadPrompt.ForString("Enter name of picture file to insert");

                if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Trim().Length == 0)
                {
                    yield break;
                }

                file = c.Input.Text.Trim().Trim('"');
            }

            if (!File.Exists(file))
            {
                c.Write("Can't find file.");
                yield break;
            }

            string extension = Path.GetExtension(file);

            if (
                !string.Equals(extension, ".bmp", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
            )
            {
                c.Write(
                    (extension.Length == 0 ? "A file of no type" : extension.ToUpperInvariant() + " files")
                        + " cannot be inserted by the presenter, which takes a bitmap or a PNG picture: nothing was inserted."
                );
                yield break;
            }

            byte[] bytes;

            try
            {
                bytes = File.ReadAllBytes(file);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                c.Write("The file cannot be read: " + exception.Message);
                yield break;
            }

            CadOleFrames.TryInsert(c, bytes);
        }
    }
}
