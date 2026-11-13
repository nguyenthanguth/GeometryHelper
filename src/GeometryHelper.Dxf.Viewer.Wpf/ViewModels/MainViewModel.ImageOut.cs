using System;
using System.IO;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// PNGOUT, JPGOUT and BMPOUT's file, asked through a function the workspace sets, as AutoCAD's Create Raster File dialog;
    /// with none, the command asks "Enter file name:" on the command line. The canvas draws and writes the image. EXPORT's
    /// and -EXPORT's file are asked the same way, through the standard Save dialog of the types they list.
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

        /// <summary>
        /// Gets or sets how the workspace asks for the file of EXPORT (Export Data) and -EXPORT (Save As PDF): given the job,
        /// whose title, types, type and file are those offered, it sets the file and the type chosen, or the file to null where
        /// the dialog was cancelled. With none, the commands ask "Enter file name:" on the command line.
        /// </summary>
        public Action<CadExportJob> AskExportFile { get; set; }

        /// <summary>EXPORT's and -EXPORT's request: the file and type chosen put into the job, the file null where the dialog was cancelled.</summary>
        private void OnExportFileRequested(CadUiRequestEventArgs e)
        {
            if (e.Export == null || AskExportFile == null)
            {
                return;
            }

            AskExportFile(e.Export);
            e.Handled = true;
        }

        /// <summary>
        /// Gets or sets how the workspace shows AutoCAD's Insert Object dialog, which INSERTOBJ shows: true when OK was pressed.
        /// With none, INSERTOBJ shows the Open dialog for the file alone.
        /// </summary>
        public Func<InsertObjectViewModel, bool> ShowInsertObject { get; set; }

        /// <summary>INSERTOBJ's file: the Insert Object dialog, its Browse... the Open dialog; else the Open dialog alone.</summary>
        private void OnInsertObjectRequested(CadUiRequestEventArgs e)
        {
            if (e.Export == null || ShowInsertObject == null)
            {
                OnExportFileRequested(e);
                return;
            }

            var dialog = new InsertObjectViewModel(e.Export) { BrowseFile = AskExportFile };

            if (!ShowInsertObject(dialog) || !dialog.Accept())
            {
                dialog.Cancel();
            }

            e.Handled = true;
        }
    }
}
