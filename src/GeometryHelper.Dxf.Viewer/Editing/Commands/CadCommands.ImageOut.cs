using System;
using System.Collections.Generic;
using System.IO;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// PNGOUT, JPGOUT and BMPOUT, as AutoCAD 2021's: the file asked for, by the window's Create Raster File dialog or on the
    /// command line, then "Select objects or &lt;all objects and viewports&gt;:", and the drawing area written as it is shown,
    /// or with only the objects selected. The window draws and writes the image (<see cref="CadImageJob"/>); nothing is
    /// printed when it is written, and the drawing is not changed, so there is no undo step.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>The width of the image where there is no drawing area: 1 920 pixels.</summary>
        internal const int DefaultImageWidth = 1920;

        /// <summary>The height of the image where there is no drawing area: 1 080 pixels.</summary>
        internal const int DefaultImageHeight = 1080;

        internal static void AddImageOutTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("PNGOUT", c => ImageOut(c, "PNGOUT", CadImageFormat.Png)));
            registry.Add(new CadCommand("JPGOUT", c => ImageOut(c, "JPGOUT", CadImageFormat.Jpeg)));
            registry.Add(new CadCommand("BMPOUT", c => ImageOut(c, "BMPOUT", CadImageFormat.Bmp)));
        }

        /// <summary>
        /// The file, by the window's dialog or, with no window or FILEDIA 0, "Enter file name &lt;file&gt;:", the objects, then the image handed to the window
        /// to write. A name typed without the extension, or with another, has it added, as AutoCAD writes plan.jpeg as
        /// plan.jpeg.jpg; a name typed with no folder goes into the folder offered.
        /// </summary>
        private static IEnumerable<CadPrompt> ImageOut(CadCommandContext c, string name, CadImageFormat format)
        {
            CadSpace space = c.Space;
            CadView view = c.Session.View;

            if (view == null)
            {
                view = new CadView(DefaultImageWidth, DefaultImageHeight);
                view.ZoomTo(c.Document.GetDisplay(space).Bounds.Union(space.PaperBounds));
            }

            var job = new CadImageJob(format, space, view);
            string offered = ImageFileName(c.Document, job.Extension);
            job.FileName = offered;
            string file;

            if (c.Session.FileDialogs && c.Session.Request(name, c.Document, job))
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
                    file = c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0 ? c.Input.Text.Trim().Trim('"') : offered;

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

            if (!file.EndsWith(job.Extension, StringComparison.OrdinalIgnoreCase))
            {
                file += job.Extension;
            }

            job.FileName = file;
            yield return CadPrompt.ForSelection("Select objects or <all objects and viewports>");
            job.Handles = c.Input.Handles == null || c.Input.Handles.Count == 0 ? null : c.Input.Handles;

            if (!c.Session.Request("IMAGEOUT", c.Document, job))
            {
                c.Write(name + " needs the window to draw the image: nothing was written.");
            }
        }

        /// <summary>The file an image is offered as: the drawing's name beside the drawing, in Documents for a drawing of no file.</summary>
        private static string ImageFileName(CadDocument document, string extension)
        {
            string folder = document.HasFile ? Path.GetDirectoryName(document.FileName) : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string name = Path.GetFileNameWithoutExtension(document.Title);

            foreach (char bad in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(bad, '_');
            }

            return Path.Combine(folder ?? string.Empty, name + extension);
        }
    }
}
