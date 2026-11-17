using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The contextual Image tab: shown while images (and nothing else) are selected, its Adjust, Clipping and Options groups
    /// writing the images selected, each value one undo step named after the command that changes the same thing; the three
    /// levels greyed for a bitonal picture.
    /// </summary>
    public sealed class ImageTabTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "imagetab-" + Guid.NewGuid().ToString("N")
        );

        public ImageTabTests()
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

        /// <summary>A bitmap file of 8 by 8 pixels: 1 bit a pixel (bitonal), or 24 bits.</summary>
        private string Bitmap(string name, bool oneBit)
        {
            var bytes = new List<byte> { (byte)'B', (byte)'M' };
            int bits = oneBit ? 1 : 24;
            int palette = oneBit ? 8 : 0;
            int stride = oneBit ? 4 : 24;
            int offset = 14 + 40 + palette;
            AddLittle(bytes, offset + stride * 8, 4);
            AddLittle(bytes, 0, 4);
            AddLittle(bytes, offset, 4);
            AddLittle(bytes, 40, 4);
            AddLittle(bytes, 8, 4);
            AddLittle(bytes, 8, 4);
            AddLittle(bytes, 1, 2);
            AddLittle(bytes, bits, 2);
            AddLittle(bytes, 0, 4);
            AddLittle(bytes, stride * 8, 4);
            AddLittle(bytes, 3780, 4);
            AddLittle(bytes, 3780, 4);
            AddLittle(bytes, oneBit ? 2 : 0, 4);
            AddLittle(bytes, 0, 4);

            if (oneBit)
            {
                bytes.AddRange(new byte[] { 0, 0, 0, 0, 255, 255, 255, 0 });
            }

            for (int i = 0; i < stride * 8; i++)
            {
                bytes.Add((byte)(i * 37));
            }

            string path = Path.Combine(_folder, name);
            File.WriteAllBytes(path, bytes.ToArray());
            return path;
        }

        private static void AddLittle(List<byte> bytes, int value, int count)
        {
            for (int i = 0; i < count; i++)
            {
                bytes.Add((byte)(value >> (8 * i)));
            }
        }

        private static DxfRasterImage Place(DxfDatabase database, string path, string name, double x)
        {
            DxfDBDictionary images = database.NamedObjectsDictionary.Contains("ACAD_IMAGE_DICT")
                ? DxfRasterImageDef.GetImageDictionary(database)
                : DxfRasterImageDef.CreateImageDictionary(database);
            var definition = new DxfRasterImageDef(path, new GeoVector2(8, 8));
            images.SetAt(name, definition);
            var image = new DxfRasterImage(definition);
            image.SetOrientation(new GeoPoint3(x, 0, 0), new GeoVector3(40, 0, 0), new GeoVector3(0, 40, 0));
            database.ModelSpace.AppendEntity(image);
            return image;
        }

        private MainViewModel WithImages(out string colour, out string scan, out string other)
        {
            var database = new DxfDatabase("AC1027");
            DxfRasterImage a = Place(database, Bitmap("colour.bmp", false), "colour", 0);
            DxfRasterImage b = Place(database, Bitmap("scan.bmp", true), "scan", 100);
            var circle = new DxfCircle(new GeoPoint3(500, 500, 0), new GeoVector3(0, 0, 1), 5);
            database.ModelSpace.AppendEntity(circle);
            colour = a.Handle;
            scan = b.Handle;
            other = circle.Handle;
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, Path.Combine(_folder, "images.dxf")));
            return model;
        }

        [Fact]
        public void TheImageTabIsShownWhileImagesAloneAreSelected_AndHiddenForAnythingElse()
        {
            MainViewModel model = WithImages(out string colour, out string scan, out string other);
            Assert.False(model.ImageTab.IsTabVisible);
            Assert.Null(model.ImageEditor);

            model.Document.Selection.Set(new[] { colour });

            Assert.True(model.ImageTab.IsTabVisible);
            Assert.Equal("Image", model.ImageTab.TabHeader);
            Assert.Same(model.ImageTab, model.SelectedTab);
            Assert.Equal("1 image selected.", model.ImageEditor.Caption);

            model.Document.Selection.Set(new[] { colour, scan });
            Assert.True(model.ImageTab.IsTabVisible);
            Assert.Equal("2 images selected.", model.ImageEditor.Caption);

            model.Document.Selection.Set(new[] { colour, other });
            Assert.False(model.ImageTab.IsTabVisible);
            Assert.Null(model.ImageEditor);

            model.Document.Selection.Set(new[] { other });
            Assert.False(model.ImageTab.IsTabVisible);

            model.Document.Selection.Set(new[] { scan });
            Assert.True(model.ImageTab.IsTabVisible);
            model.Document.Selection.Clear();
            Assert.False(model.ImageTab.IsTabVisible);
        }

        [Fact]
        public void ALevelWrittenInTheTab_ChangesTheImage_OneUndoStepNamedImageAdjust_AndEveryImageSelectedTakesIt()
        {
            var database = new DxfDatabase("AC1027");
            DxfRasterImage a = Place(database, Bitmap("a.bmp", false), "a", 0);
            DxfRasterImage b = Place(database, Bitmap("b.bmp", false), "b", 100);
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, Path.Combine(_folder, "two.dxf")));
            model.Document.Selection.Set(new[] { a.Handle, b.Handle });
            ImageTabViewModel tab = model.ImageEditor;
            Assert.True(tab.CanAdjust);
            Assert.Equal(50, tab.Brightness);
            Assert.Equal(50, tab.Contrast);
            Assert.Equal(0, tab.Fade);

            tab.Brightness = 70;
            tab.Contrast = 120;
            tab.Fade = 25;

            Assert.Equal("IMAGEADJUST", model.Document.NextUndo.Name);
            Assert.Equal(70, tab.Brightness);
            Assert.Equal(100, tab.Contrast);
            Assert.Equal(25, tab.Fade);

            foreach (string handle in new[] { a.Handle, b.Handle })
            {
                var image = (DxfRasterImage)model.Document.FindEntity(handle);
                Assert.Equal(70, image.Brightness);
                Assert.Equal(100, image.Contrast);
                Assert.Equal(25, image.Fade);
            }

            while (model.Document.CanUndo && model.Document.NextUndo.Name == "IMAGEADJUST")
            {
                Assert.True(model.Document.Undo());
            }

            Assert.Equal(50, model.ImageEditor.Brightness);
            Assert.Equal(0, model.ImageEditor.Fade);
        }

        [Fact]
        public void ForABitonalPicture_TheLevelsAreGreyed_AndASetIsIgnored()
        {
            MainViewModel model = WithImages(out string colour, out string scan, out _);
            model.Document.Selection.Set(new[] { scan });
            ImageTabViewModel tab = model.ImageEditor;
            Assert.False(tab.CanAdjust);

            tab.Brightness = 90;

            Assert.Equal(50, ((DxfRasterImage)model.Document.FindEntity(scan)).Brightness);

            // One bitonal among the images greys them all, as the command refuses the bitonal one.
            model.Document.Selection.Set(new[] { colour, scan });
            Assert.False(model.ImageEditor.CanAdjust);
            model.Document.Selection.Set(new[] { colour });
            Assert.True(model.ImageEditor.CanAdjust);
        }

        [Fact]
        public void ShowImageAndTransparency_AreWrittenOneUndoStepEach_AndFollowAnUndo()
        {
            MainViewModel model = WithImages(out _, out string scan, out _);
            model.Document.Selection.Set(new[] { scan });
            ImageTabViewModel tab = model.ImageEditor;
            Assert.True(tab.ShowImage);
            Assert.False(tab.Transparency);

            tab.Transparency = true;
            Assert.Equal("TRANSPARENCY", model.Document.NextUndo.Name);
            Assert.True(((DxfRasterImage)model.Document.FindEntity(scan)).ImageTransparency);

            tab.ShowImage = false;
            Assert.False(((DxfRasterImage)model.Document.FindEntity(scan)).ShowImage);
            Assert.False(model.ImageEditor.ShowImage);

            Assert.True(model.Document.Undo());
            Assert.True(model.ImageEditor.ShowImage);
            Assert.True(model.ImageEditor.Transparency);
            Assert.True(model.Document.Undo());
            Assert.False(model.ImageEditor.Transparency);
        }

        [Fact]
        public void TheClippingGroup_RemovesTheBoundary_AndCreatesOneByIMAGECLIPOnTheImage()
        {
            MainViewModel model = WithImages(out string colour, out string scan, out _);
            var image = (DxfRasterImage)model.Document.FindEntity(colour);
            image.SetClipBoundary(
                DxfClipBoundaryType.Rectangle,
                new List<GeoPoint2>
                {
                    new GeoPoint2(1, 1),
                    new GeoPoint2(1, 5),
                    new GeoPoint2(5, 5),
                    new GeoPoint2(5, 1),
                    new GeoPoint2(1, 1),
                }
            );
            image.IsClipped = true;
            model.Document.Selection.Set(new[] { scan });
            model.Document.Selection.Set(new[] { colour });
            ImageTabViewModel tab = model.ImageEditor;
            Assert.True(tab.IsClipped);
            Assert.True(tab.RemoveClipCommand.CanExecute(null));

            tab.RemoveClipCommand.Execute(null);

            Assert.Equal("IMAGECLIP", model.Document.NextUndo.Name);
            Assert.False(model.ImageEditor.IsClipped);
            Assert.False(model.ImageEditor.RemoveClipCommand.CanExecute(null));

            // Create Clipping Boundary starts IMAGECLIP, New boundary, on the image: the boundary is asked on the canvas.
            Assert.True(model.ImageEditor.CreateClipCommand.CanExecute(null));
            model.ImageEditor.CreateClipCommand.Execute(null);
            Assert.True(model.Session.IsRunning);
            Assert.Contains("Specify clipping boundary", model.Session.PromptText);

            // Two images selected: one boundary is made for one image, so the button is greyed.
            model.Session.Escape();
            model.Document.Selection.Set(new[] { colour, scan });
            Assert.False(model.ImageEditor.CreateClipCommand.CanExecute(null));
        }

        [Fact]
        public void TheFrameBoxOfTheTab_SetsTheImageFrameOfTheDrawing_AsTheInsertTabsBoxDoes()
        {
            MainViewModel model = WithImages(out string colour, out _, out _);
            model.Document.Selection.Set(new[] { colour });
            ImageTabViewModel tab = model.ImageEditor;
            Assert.Equal(
                new[] { "Hide Frames", "Display and Plot Frames", "Display but Not Plot Frames" },
                tab.FrameNames
            );
            Assert.Equal("Display and Plot Frames", tab.Frame);

            tab.Frame = "Display but Not Plot Frames";

            Assert.Equal("Display but Not Plot Frames", model.CurrentImageFrame);
            Assert.Equal("Display but Not Plot Frames", model.ImageEditor?.Frame ?? tab.Frame);
        }

        [Fact]
        public void CloseImage_LetsGoOfTheImages_WhichHidesTheTab()
        {
            MainViewModel model = WithImages(out string colour, out _, out _);
            model.Document.Selection.Set(new[] { colour });

            model.ImageEditor.CloseCommand.Execute(null);

            Assert.False(model.ImageTab.IsTabVisible);
            Assert.Empty(model.Document.Selection.Handles);
        }

        [Fact]
        public void AWipeoutIsNoImageToTheTab()
        {
            var database = new DxfDatabase("AC1027");
            var mask = new DxfWipeout();
            mask.SetFrom(
                new[]
                {
                    new GeoPoint2(0, 0),
                    new GeoPoint2(10, 0),
                    new GeoPoint2(10, 10),
                    new GeoPoint2(0, 10),
                    new GeoPoint2(0, 0),
                },
                new GeoVector3(0, 0, 1)
            );
            database.ModelSpace.AppendEntity(mask);
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, Path.Combine(_folder, "mask.dxf")));

            model.Document.Selection.Set(new[] { mask.Handle });

            Assert.False(model.ImageTab.IsTabVisible);
        }
    }
}
