using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using SkiaSharp;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;
using static GeometryHelper.Dxf.Viewer.UnitTest.OleFrameDisplayTests;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// INSERTOBJ's Create from File for a bitmap or a PNG, the frame it makes at the upper left of the view as one undo step,
    /// PASTECLIP of a picture on the Clipboard as the same kind of frame, and the plot quality OLEQUALITY gives new frames.
    /// </summary>
    public class InsertObjectCommandTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "insertobj-" + Guid.NewGuid().ToString("N")
        );

        public InsertObjectCommandTests()
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

        /// <summary>A PNG of 40 by 30 pixels with no resolution, which a frame sizes at 96 dots an inch: 10.5820 by 7.9365.</summary>
        private static byte[] Png()
        {
            using (var bitmap = new SKBitmap(new SKImageInfo(40, 30, SKColorType.Rgba8888, SKAlphaType.Unpremul)))
            {
                bitmap.Erase(SKColors.Red);

                using (SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100))
                {
                    return data.ToArray();
                }
            }
        }

        private string Saved(string name, byte[] bytes)
        {
            string path = Path.Combine(_folder, name);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        /// <summary>An empty drawing seen through a view of 200 by 100 pixels showing (100, 200) to (300, 300).</summary>
        private static CadCommandSession Seeing(out CadDocument document, bool withLayout = false)
        {
            DxfDatabase database = CadTemplate.CreateMetric();

            if (withLayout)
            {
                var layout = new DxfLayout { LayoutName = "Plan" };
                layout.AddToLayoutDictionary(database, null);
                layout.Initialize();
            }

            document = CadDocument.From(database, "empty.dxf");
            CadCommandSession session = Session(document);
            var view = new CadView(200, 100);
            view.ZoomTo(new DisplayBox(100, 200, 300, 300), 0.0);
            session.View = view;
            return session;
        }

        /// <summary>The window's file dialog, answering with a file and keeping what it was asked.</summary>
        private static void Choosing(CadCommandSession session, string file, List<CadExportJob> asked)
        {
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "INSERTOBJ")
                {
                    asked.Add(e.Export);
                    e.Export.FileName = file;
                    e.Handled = true;
                }
            };
        }

        [Fact]
        public void AFileChosenInTheDialog_IsInsertedAsAStaticPicture_AtTheUpperLeftOfTheView()
        {
            CadCommandSession session = Seeing(out CadDocument document);
            var asked = new List<CadExportJob>();
            Choosing(session, Saved("logo.png", Png()), asked);

            Run(session, "INSERTOBJ");

            Assert.False(session.IsRunning);
            CadExportJob job = Assert.Single(asked);
            Assert.Equal("Insert Object", job.Title);
            Assert.True(job.Opens);
            Assert.Contains("(*.bmp", job.FileType);
            Assert.Contains("*.png)", job.FileType);
            DxfOle2Frame frame = Assert.Single(All<DxfOle2Frame>(document.ModelSpace));
            Assert.Equal(DxfOle2FrameItemType.Static, frame.Type);
            GeoAabb3 box = frame.GeometricExtents;
            Assert.Equal(100.0, box.Min.X, 6);
            Assert.Equal(300.0, box.Max.Y, 6);
            Assert.Equal(40 * 1000.0 / 3780.0, box.Max.X - box.Min.X, 6);
            Assert.Equal(30 * 1000.0 / 3780.0, box.Max.Y - box.Min.Y, 6);
        }

        [Fact]
        public void TheInsertedFrame_IsOneUndoStep_AndTheDrawingIsChanged()
        {
            CadCommandSession session = Seeing(out CadDocument document);
            Choosing(session, Saved("logo.png", Png()), new List<CadExportJob>());

            Run(session, "INSERTOBJ");
            Assert.True(document.IsModified);
            Assert.True(document.Undo());

            Assert.Empty(All<DxfOle2Frame>(document.ModelSpace));
            Assert.True(document.Redo());
            Assert.Single(All<DxfOle2Frame>(document.ModelSpace));
        }

        [Fact]
        public void AFrameInserted_IsDrawnAsItsPicture()
        {
            CadCommandSession session = Seeing(out CadDocument document);
            Choosing(session, Saved("logo.png", Png()), new List<CadExportJob>());

            Run(session, "INSERTOBJ");

            Assert.Single(document.GetDisplay(document.ModelSpace).Items.OfType<DisplayImage2>());
        }

        [Fact]
        public void ACancelledDialog_InsertsNothing()
        {
            CadCommandSession session = Seeing(out CadDocument document);
            Choosing(session, null, new List<CadExportJob>());

            Run(session, "INSERTOBJ");

            Assert.False(session.IsRunning);
            Assert.Empty(All<DxfOle2Frame>(document.ModelSpace));
            Assert.False(document.IsModified);
        }

        [Fact]
        public void WithFiledia0_TheFileIsAskedOnTheCommandLine_AndTheDialogIsNotShown()
        {
            CadCommandSession session = Seeing(out CadDocument document);
            var asked = new List<CadExportJob>();
            Choosing(session, null, asked);
            Run(session, "FILEDIA", "0");

            Run(session, "INSERTOBJ");
            Assert.Equal("Enter name of picture file to insert:", session.PromptText);
            Run(session, Saved("logo.png", Png()));

            Assert.Empty(asked);
            Assert.False(session.IsRunning);
            Assert.Single(All<DxfOle2Frame>(document.ModelSpace));
        }

        [Fact]
        public void ABitmapIsInsertedToo_AndEnterAtTheNameLeaves()
        {
            CadCommandSession session = Seeing(out CadDocument document);
            Run(session, "FILEDIA", "0");

            Run(session, "INSERTOBJ", Saved("two.bmp", RedAndBlueBitmap()));
            Assert.Single(All<DxfOle2Frame>(document.ModelSpace));

            Run(session, "INSERTOBJ", Enter);
            Assert.False(session.IsRunning);
            Assert.Single(All<DxfOle2Frame>(document.ModelSpace));
        }

        [Fact]
        public void AFileThatIsNotThere_IsCantFindFile_AndOneThatIsNoPictureOfTheKindsTaken_IsRefusedWithWhy()
        {
            CadCommandSession session = Seeing(out CadDocument document);
            Run(session, "FILEDIA", "0");

            Run(session, "INSERTOBJ", Path.Combine(_folder, "missing.png"));
            Assert.Equal("Can't find file.", session.History.Last());

            Run(session, "INSERTOBJ", Saved("photo.jpg", new byte[] { 1, 2, 3 }));
            Assert.Equal(
                ".JPG files cannot be inserted by the presenter, which takes a bitmap or a PNG picture: nothing was inserted.",
                session.History.Last()
            );

            Run(session, "INSERTOBJ", Saved("broken.png", Png().Take(20).ToArray()));
            Assert.StartsWith("The file is no picture an OLE object can be made of:", session.History.Last());
            Assert.Empty(All<DxfOle2Frame>(document.ModelSpace));
        }

        [Fact]
        public void TheAliasIO_RunsInsertobj()
        {
            Assert.Equal("INSERTOBJ", CadShortcuts.Default.Aliases["IO"]);
            Assert.NotNull(CadCommandRegistry.CreateDefault().Find("INSERTOBJ"));
        }

        [Fact]
        public void AFrameInserted_TakesThePlotQualityOleQualityGives_HighGraphicsForAPictureWhereItIs3()
        {
            CadCommandSession session = Seeing(out CadDocument document);
            Run(session, "FILEDIA", "0");

            Run(session, "INSERTOBJ", Saved("a.png", Png()));
            Run(session, "OLEQUALITY", "0");
            Run(session, "INSERTOBJ", Saved("b.png", Png()));
            Run(session, "OLEQUALITY", "1");
            Run(session, "INSERTOBJ", Saved("c.png", Png()));

            DxfOle2Frame[] frames = All<DxfOle2Frame>(document.ModelSpace).ToArray();
            Assert.Equal(new byte[] { 2, 0, 1 }, frames.Select(f => f.OutputQuality).ToArray());
        }

        [Fact]
        public void InALayout_TheFrameIsInsertedInTheLayout_AtTheUpperLeftOfItsView()
        {
            CadCommandSession session = Seeing(out CadDocument document, true);
            CadSpace plan = document.Spaces.Single(s => s.Name == "Plan");
            document.CurrentSpace = plan;
            Run(session, "FILEDIA", "0");

            Run(session, "INSERTOBJ", Saved("a.png", Png()));

            Assert.Empty(All<DxfOle2Frame>(document.ModelSpace));
            DxfOle2Frame frame = Assert.Single(All<DxfOle2Frame>(plan));
            Assert.Equal(300.0, frame.GeometricExtents.Max.Y, 6);
        }

        [Fact]
        public void APictureOnTheClipboard_IsPastedByPasteclipAsTheSameFrame_AtTheUpperLeftOfTheView()
        {
            CadCommandSession session = Seeing(out CadDocument document);
            session.Clipboard.SetData(new CadClipboardData(null, null, picture: Png()));

            Run(session, "PASTECLIP");

            Assert.False(session.IsRunning);
            DxfOle2Frame frame = Assert.Single(All<DxfOle2Frame>(document.ModelSpace));
            Assert.Equal(DxfOle2FrameItemType.Static, frame.Type);
            Assert.Equal(100.0, frame.GeometricExtents.Min.X, 6);
            Assert.Equal(300.0, frame.GeometricExtents.Max.Y, 6);
            Assert.True(document.Undo());
            Assert.Empty(All<DxfOle2Frame>(document.ModelSpace));
        }

        [Fact]
        public void APictureOnTheClipboard_IsNotPastedAsABlockOrToTheOriginalPlace_AsItIsNoDrawingObject()
        {
            CadCommandSession session = Seeing(out CadDocument document);
            session.Clipboard.SetData(new CadClipboardData(null, null, picture: Png()));

            Run(session, "PASTEBLOCK");

            Assert.StartsWith("The Clipboard holds a picture, not drawing objects", session.History.Last());
            Assert.Empty(All<DxfOle2Frame>(document.ModelSpace));
        }

        [Fact]
        public void APictureAndItsWordsTogether_PasteAsTheWords_AsAutoCadPastesTextBeforeAPicture()
        {
            CadCommandSession session = Seeing(out CadDocument document);
            session.Clipboard.SetData(new CadClipboardData(null, "words", picture: Png()));

            Run(session, "PASTECLIP", P(150, 250));

            Assert.Empty(All<DxfOle2Frame>(document.ModelSpace));
            Assert.Single(All<DxfMText>(document.ModelSpace));
        }

        [Fact]
        public void PasteSpecialsPictureAnswer_PastesThePicture_EvenWhereWordsCameWithIt()
        {
            CadCommandSession session = Seeing(out CadDocument document);
            session.Clipboard.SetData(new CadClipboardData(null, "words", picture: Png()));
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "PASTESPEC")
                {
                    e.Answer = "PICTURE";
                    e.Handled = true;
                }
            };

            Run(session, "PASTESPEC");

            Assert.Single(All<DxfOle2Frame>(document.ModelSpace));
            Assert.Empty(All<DxfMText>(document.ModelSpace));
        }
    }
}
