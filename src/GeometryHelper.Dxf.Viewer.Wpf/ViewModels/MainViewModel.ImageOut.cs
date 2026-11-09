using System;
using System.IO;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// PNGOUT, JPGOUT and BMPOUT's file, asked through a function the workspace sets, as AutoCAD's Create Raster File dialog;
    /// with none, the command asks "Enter file name:" on the command line. The canvas draws and writes the image.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>
        /// Gets or sets how the workspace asks where an image is written, as AutoCAD's Create Raster File: given the folder
        /// offered (null for none) and the file's name with its extension, .png, .jpg or .bmp, which sets the type listed, the
        /// path chosen, or null where it was cancelled.
        /// </summary>
        public Func<string, string, string> AskImageFile { get; set; }

        /// <summary>PNGOUT's, JPGOUT's and BMPOUT's request: the file chosen put into the job, null where the dialog was cancelled.</summary>
        private void OnImageFileRequested(CadUiRequestEventArgs e)
        {
            CadImageJob job = e.Image;

            if (job == null || AskImageFile == null)
            {
                return;
            }

            string offered = job.FileName;
            job.FileName = AskImageFile(Path.GetDirectoryName(offered), Path.GetFileName(offered));
            e.Handled = true;
        }
    }
}
