using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using SkiaSharp;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// INSERTOBJ in the window: the Insert Object dialog is the standard Open dialog the window shows for a file of a bitmap or
    /// a PNG, here a function the test sets in its place, so that no real dialog opens; the picture chosen is a frame in the
    /// drawing, and the dialog's cancelling leaves it as it was.
    /// </summary>
    public class InsertObjectWindowTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "insertobj-window-" + Guid.NewGuid().ToString("N")
        );

        public InsertObjectWindowTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException)
            {
                // A file still held is left for the system to clear.
            }
        }

        private string Picture()
        {
            string path = Path.Combine(_folder, "logo.png");

            using (var bitmap = new SKBitmap(new SKImageInfo(40, 30, SKColorType.Rgba8888, SKAlphaType.Unpremul)))
            {
                bitmap.Erase(SKColors.Red);

                using (SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100))
                {
                    File.WriteAllBytes(path, data.ToArray());
                }
            }

            return path;
        }

        private MainViewModel Window()
        {
            string source = Path.Combine(_folder, "plan.dxf");
            PlotPdfTests.Plan().Database.Save(source);
            var model = new MainViewModel();
            Assert.True(model.Open(source));
            return model;
        }

        [Fact]
        public void TheDialogOfInsertobj_ListsPicturesAndOpensAFile_AndTheFileChosenBecomesAFrame()
        {
            MainViewModel model = Window();
            var asked = new List<CadExportJob>();
            string logo = Picture();
            model.AskExportFile = job =>
            {
                asked.Add(job);
                job.FileName = logo;
            };

            model.StartCommand("INSERTOBJ");

            CadExportJob dialog = Assert.Single(asked);
            Assert.Equal("Insert Object", dialog.Title);
            Assert.True(dialog.Opens);
            Assert.Single(model.Document.Database.ModelSpace.OfType<DxfOle2Frame>());
        }

        [Fact]
        public void ACancelledDialog_LeavesTheDrawingAsItWas()
        {
            MainViewModel model = Window();
            model.AskExportFile = job => job.FileName = null;

            model.StartCommand("INSERTOBJ");

            Assert.Empty(model.Document.Database.ModelSpace.OfType<DxfOle2Frame>());
            Assert.False(model.Document.IsModified);
        }

        [Fact]
        public void PasteSpecial_OffersThePictureOnTheClipboard_AsImagePng_AndAnswersPicture()
        {
            var dialog = new PasteSpecialViewModel(new CadClipboardData(null, null, picture: new byte[] { 1 }));

            PasteFormatViewModel row = dialog.Formats.Single(f => f.Name == "Image (PNG)");
            Assert.True(row.IsEnabled);
            Assert.Equal("Picture", dialog.Source);
            Assert.Equal("PICTURE", dialog.Answer);
            Assert.False(new PasteSpecialViewModel(null).Formats.Single(f => f.Name == "Image (PNG)").IsEnabled);
        }

        [Fact]
        public void WithNoDialogToShow_TheFileIsAskedOnTheCommandLine()
        {
            MainViewModel model = Window();

            model.StartCommand("INSERTOBJ");

            Assert.Equal("Enter name of picture file to insert:", model.Session.PromptText);
            model.Session.Escape();
        }
    }
}
