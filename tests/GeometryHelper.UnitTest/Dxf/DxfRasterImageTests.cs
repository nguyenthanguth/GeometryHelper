using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The IMAGE as a <see cref="DxfRasterImage"/>, the WIPEOUT as a <see cref="DxfWipeout"/>, the IMAGEDEF as a
    /// <see cref="DxfRasterImageDef"/> and the RASTERVARIABLES as a <see cref="DxfRasterVariables"/>: read as AutoCAD 2021 reads
    /// them, set and written as it writes them, a setter given what it has leaving the file byte for byte. The numbers are those
    /// AutoCAD 2021 gave for the same records.
    /// </summary>
    public class DxfRasterImageTests
    {
        private static readonly double Close = 1E-9;

        private static void Near(double expected, double actual) =>
            Assert.InRange(actual, expected - Close, expected + Close);

        private static void Near(GeoPoint3 expected, GeoPoint3 actual)
        {
            Near(expected.X, actual.X);
            Near(expected.Y, actual.Y);
            Near(expected.Z, actual.Z);
        }

        private static void Near(GeoVector3 expected, GeoVector3 actual) =>
            Near(new GeoPoint3(expected.X, expected.Y, expected.Z), new GeoPoint3(actual.X, actual.Y, actual.Z));

        private static void Near(IReadOnlyList<GeoPoint3> expected, IReadOnlyList<GeoPoint3> actual)
        {
            Assert.Equal(expected.Count, actual.Count);

            for (int i = 0; i < expected.Count; i++)
            {
                Near(expected[i], actual[i]);
            }
        }

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoPoint2 Q(double x, double y) => new GeoPoint2(x, y);

        private static string[] Groups(string text, string handle)
        {
            string record = Records(text).First(r => HandleOf(r) == handle);
            string[] lines = record.TrimEnd('\n').Split('\n');
            var groups = new List<string>();

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                groups.Add(lines[i].Trim() + "=" + lines[i + 1].Trim());
            }

            return groups.ToArray();
        }

        private static string After(string[] groups, string code) =>
            string.Join(" ", groups.SkipWhile(g => !g.StartsWith(code + "=", StringComparison.Ordinal)));

        [Fact]
        public void AnImageAndAWipeoutReadFromAFile_AreOfTheirOwnClasses_AsAutoCadNamesThem()
        {
            foreach (string text in new[] { DxfRasterImageDrawings.R2000, DxfRasterImageDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);

                Assert.IsType<DxfRasterImage>(ByHandle<DxfEntity>(db, "8E"));
                Assert.IsType<DxfRasterImage>(ByHandle<DxfEntity>(db, "C10"));
                Assert.IsType<DxfWipeout>(ByHandle<DxfEntity>(db, "C14"));
                Assert.IsType<DxfRasterImageDef>(db.GetObject("8D"));
                Assert.IsType<DxfRasterVariables>(db.NamedObjectsDictionary.GetAt("ACAD_IMAGE_VARS"));
                Assert.DoesNotContain(db.ModelSpace, e =>
                    e is DxfUnknownEntity unknown && (unknown.DxfName == "IMAGE" || unknown.DxfName == "WIPEOUT")
                );
            }
        }

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            foreach (
                string text in new[]
                {
                    DxfRasterImageDrawings.R2000,
                    DxfRasterImageDrawings.R2018,
                    DxfFileEzdxfDrawings.R2018,
                }
            )
            {
                SettingWhatItHasChangesNothing<DxfRasterImage>(text, image =>
                {
                    image.GetOrientation(out GeoPoint3 origin, out GeoVector3 u, out GeoVector3 v);
                    image.SetOrientation(origin, u, v);
                    image.DisplayOptions = image.DisplayOptions;
                    image.ShowImage = image.ShowImage;
                    image.ImageTransparency = image.ImageTransparency;
                    image.IsClipped = image.IsClipped;
                    image.IsClipInverted = image.IsClipInverted;
                    image.Brightness = image.Brightness;
                    image.Contrast = image.Contrast;
                    image.Fade = image.Fade;
                    image.Layer = image.Layer;
                    image.TransformBy(GeoTransform3.Identity);

                    if (image.Handle != "C13")
                    {
                        image.Rotation = image.Rotation;
                    }
                });
            }
        }

        [Fact]
        public void ThePictureOf4By3PixelsPlacedAt10And20_StandsWhereAutoCad2021PutsIt()
        {
            DxfRasterImage image = ByHandle<DxfRasterImage>(DxfDatabase.Parse(DxfRasterImageDrawings.R2018), "C10");

            image.GetOrientation(out GeoPoint3 origin, out GeoVector3 u, out GeoVector3 v);
            Near(P(10, 20, 0), origin);
            Near(new GeoVector3(40, 0, 0), u);
            Near(new GeoVector3(0, 30, 0), v);
            Near(P(50, 50, 0), image.Position);
            Near(40, image.Width);
            Near(30, image.Height);
            Assert.Equal(4.0, image.ImageWidth);
            Assert.Equal(3.0, image.ImageHeight);
            Assert.Equal(0.0, image.Rotation);
            Assert.Equal(
                DxfImageDisplayOptions.Show | DxfImageDisplayOptions.ShowUnaligned | DxfImageDisplayOptions.Clip,
                image.DisplayOptions
            );
            Assert.True(image.IsClipped);
            Assert.False(image.IsClipInverted);
            Assert.Equal(50, image.Brightness);
            Assert.Equal(DxfClipBoundaryType.Rectangle, image.ClipBoundaryType);

            // AutoCAD 2021: the pixel (0, 0) at (15, 45, 0), the top left corner (-0.5, -0.5) at (10, 50, 0).
            Near(P(15, 45, 0), image.PixelToModelTransform.Transform(P(0, 0, 0)));
            Near(P(10, 50, 0), image.PixelToModelTransform.Transform(P(-0.5, -0.5, 0)));
            Near(new[] { P(10, 20, 0), P(10, 50, 0), P(50, 50, 0), P(50, 20, 0), P(10, 20, 0) }, image.GetVertices());
            Assert.Equal(
                new[] { Q(-0.5, -0.5), Q(-0.5, 2.5), Q(3.5, 2.5), Q(3.5, -0.5), Q(-0.5, -0.5) },
                image.GetClipBoundary()
            );
        }

        [Fact]
        public void AnImageClipped_ShowsItsBoundary_AsAutoCad2021ReadsItBack()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfRasterImageDrawings.R2018);

            // The triangle in its order; the rectangle from its least corner up its x, as AutoCAD 2021 reads them back.
            Near(
                new[] { P(15, 45, 0), P(45, 45, 0), P(30, 20, 0), P(15, 45, 0) },
                ByHandle<DxfRasterImage>(db, "C11").GetVertices()
            );
            Assert.Equal(DxfClipBoundaryType.Poly, ByHandle<DxfRasterImage>(db, "C11").ClipBoundaryType);
            Near(
                new[] { P(20, 40, 0), P(20, 30, 0), P(40, 30, 0), P(40, 40, 0), P(20, 40, 0) },
                ByHandle<DxfRasterImage>(db, "C12").GetVertices()
            );
            Assert.Equal(
                new[] { Q(0.5, 0.5), Q(0.5, 1.5), Q(2.5, 1.5), Q(2.5, 0.5), Q(0.5, 0.5) },
                ByHandle<DxfRasterImage>(db, "C12").GetClipBoundary()
            );

            // Display options of 3 do not clip, whatever the record holds, as AutoCAD 2021 shows its frame.
            Assert.False(ByHandle<DxfRasterImage>(db, "C15").IsClipped);
            Near(
                new[] { P(10, 20, 0), P(10, 50, 0), P(50, 50, 0), P(50, 20, 0), P(10, 20, 0) },
                ByHandle<DxfRasterImage>(db, "C15").GetVertices()
            );
        }

        [Fact]
        public void ARectangleOfFiveCornersSet_IsWrittenAsItsFirstAndThird_AndClipsTheImage()
        {
            string text = DxfRasterImageDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfRasterImage image = ByHandle<DxfRasterImage>(db, "C10");

            image.SetClipBoundary(
                DxfClipBoundaryType.Rectangle,
                new[] { Q(0.5, 0.5), Q(2.5, 0.5), Q(2.5, 1.5), Q(0.5, 1.5), Q(0.5, 0.5) }
            );
            string after = db.ToText();

            // AutoCAD 2021 wrote this rectangle given either way round as 71 of 1 and its corners (0.5, 0.5) and (2.5, 1.5), 280 of 1.
            Assert.Equal("71=1 91=2 14=0.5 24=0.5 14=2.5 24=1.5", After(Groups(after, "C10"), "71"));
            Assert.Contains("280=1", Groups(after, "C10"));
            Near(new[] { P(20, 40, 0), P(20, 30, 0), P(40, 30, 0), P(40, 40, 0), P(20, 40, 0) }, image.GetVertices());
            OnlyTheseChanged(text, after, "C10");

            // Held the other way round, (2.5, 1.5) first, it reads back from its least corner, as AutoCAD 2021 reads it.
            image.SetClipBoundary(
                DxfClipBoundaryType.Rectangle,
                new[] { Q(2.5, 1.5), Q(2.5, 0.5), Q(0.5, 0.5), Q(0.5, 1.5), Q(2.5, 1.5) }
            );
            Assert.Equal("71=1 91=2 14=2.5 24=1.5 14=0.5 24=0.5", After(Groups(db.ToText(), "C10"), "71"));
            Assert.Equal(
                new[] { Q(0.5, 0.5), Q(0.5, 1.5), Q(2.5, 1.5), Q(2.5, 0.5), Q(0.5, 0.5) },
                image.GetClipBoundary()
            );
            Near(new[] { P(20, 40, 0), P(20, 30, 0), P(40, 30, 0), P(40, 40, 0), P(20, 40, 0) }, image.GetVertices());
            after = db.ToText();

            // Refused as AutoCAD refuses them: a rectangle of two corners, a polygon not closed, a kind of none.
            Assert.Throws<ArgumentException>(() =>
                image.SetClipBoundary(DxfClipBoundaryType.Rectangle, new[] { Q(0.5, 0.5), Q(2.5, 1.5) })
            );
            Assert.Throws<ArgumentException>(() =>
                image.SetClipBoundary(DxfClipBoundaryType.Poly, new[] { Q(0, 0), Q(3, 0), Q(1.5, 2.5) })
            );
            Assert.Throws<ArgumentException>(() =>
                image.SetClipBoundary(DxfClipBoundaryType.Invalid, new[] { Q(0, 0), Q(3, 0), Q(1.5, 2.5), Q(0, 0) })
            );
            Assert.Equal(after, db.ToText());
        }

        [Fact]
        public void APolygonSet_ClipsTheImage_AndTheWholeImageSetClipsItByNone()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfRasterImageDrawings.R2018);
            DxfRasterImage image = ByHandle<DxfRasterImage>(db, "C10");

            image.SetClipBoundary(DxfClipBoundaryType.Poly, new[] { Q(0, 0), Q(3, 0), Q(1.5, 2.5), Q(0, 0) });
            Assert.Equal(
                "71=2 91=4 14=0 24=0 14=3 24=0 14=1.5 24=2.5 14=0 24=0",
                After(Groups(db.ToText(), "C10"), "71")
            );
            Near(new[] { P(15, 45, 0), P(45, 45, 0), P(30, 20, 0), P(15, 45, 0) }, image.GetVertices());

            // As AutoCAD 2021: IsClipped false takes flag 4 off and keeps the boundary, 280 at 1; the frame is shown.
            image.IsClipped = false;
            Assert.Contains("70=3", Groups(db.ToText(), "C10"));
            Assert.Contains("280=1", Groups(db.ToText(), "C10"));
            Assert.Equal(5, image.GetVertices().Count);

            image.SetClipBoundaryToWholeImage();
            Assert.Contains("280=0", Groups(db.ToText(), "C10"));
            Assert.Equal("71=1 91=2 14=-0.5 24=-0.5 14=3.5 24=2.5", After(Groups(db.ToText(), "C10"), "71"));
        }

        [Fact]
        public void AnInvertedClip_IsReadAndSetFromR2010_AndRefusedBefore()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfRasterImageDrawings.R2018);
            DxfRasterImage image = ByHandle<DxfRasterImage>(db, "C11");

            image.IsClipInverted = true;
            Assert.True(ByHandle<DxfRasterImage>(DxfDatabase.Parse(db.ToText()), "C11").IsClipInverted);
            Assert.Contains("290=1", Groups(db.ToText(), "C11"));

            DxfRasterImage old = ByHandle<DxfRasterImage>(DxfDatabase.Parse(DxfRasterImageDrawings.R2000), "C11");
            Assert.False(old.IsClipInverted);
            Assert.Throws<InvalidOperationException>(() => old.IsClipInverted = true);
        }

        [Fact]
        public void TheLookOfAnImage_IsSetFrom0To100_AndAWipeoutRefusesIt_AsAutoCad2021Does()
        {
            string text = DxfRasterImageDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfRasterImage image = ByHandle<DxfRasterImage>(db, "C10");
            DxfWipeout wipeout = ByHandle<DxfWipeout>(db, "C14");

            image.Brightness = 0;
            image.Contrast = 100;
            image.Fade = 100;
            image.ImageTransparency = true;
            image.ShowImage = false;
            string[] groups = Groups(db.ToText(), "C10");
            Assert.Contains("281=0", groups);
            Assert.Contains("282=100", groups);
            Assert.Contains("283=100", groups);
            Assert.Contains("70=14", groups);
            Assert.Throws<ArgumentOutOfRangeException>(() => image.Brightness = 101);

            Assert.Throws<NotSupportedException>(() => wipeout.Fade = 10);
            Assert.Throws<NotSupportedException>(() => wipeout.Brightness = 60);
            Assert.Throws<NotSupportedException>(() => wipeout.DisplayOptions = DxfImageDisplayOptions.Show);
            Assert.Null(wipeout.ImageDef);
            Assert.Null(wipeout.Name);
            Assert.Null(wipeout.Path);
            OnlyTheseChanged(text, db.ToText(), "C10");
        }

        [Fact]
        public void TheRotation_IsTheAngleOfTheBottomInXy_AndSetLaysTheImageInXy_AsAutoCad2021Does()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfRasterImageDrawings.R2018);
            DxfRasterImage upright = ByHandle<DxfRasterImage>(db, "C13");
            DxfRasterImage flat = ByHandle<DxfRasterImage>(db, "C10");

            // AutoCAD 2021: the image turned to its bottom along -y reads 3 pi / 2; set to 1, the upright one lies in xy at z 50.
            flat.SetOrientation(P(10, 20, 0), new GeoVector3(0, -40, 0), new GeoVector3(30, 0, 0));
            Near(3 * Math.PI / 2, flat.Rotation);

            upright.Rotation = 1.0;
            upright.GetOrientation(out GeoPoint3 origin, out GeoVector3 u, out GeoVector3 v);
            Near(P(0, 0, 50), origin);
            Near(new GeoVector3(40 * Math.Cos(1.0), 40 * Math.Sin(1.0), 0), u);
            Near(new GeoVector3(-30 * Math.Sin(1.0), 30 * Math.Cos(1.0), 0), v);
        }

        [Fact]
        public void AWipeoutSetFrom_IsFramedAsAutoCad2021FramesIt()
        {
            string text = DxfRasterImageDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfWipeout wipeout = ByHandle<DxfWipeout>(db, "C14");

            // AutoCAD 2021: the rectangle (200, 100) to (230, 110) in the square of side 230 at the origin.
            wipeout.SetFrom(
                new[] { Q(200, 100), Q(230, 100), Q(230, 110), Q(200, 110), Q(200, 100) },
                new GeoVector3(0, 0, 1)
            );
            wipeout.GetOrientation(out GeoPoint3 origin, out GeoVector3 u, out GeoVector3 v);
            Near(P(0, 0, 0), origin);
            Near(new GeoVector3(230, 0, 0), u);
            Near(new GeoVector3(0, 230, 0), v);
            IReadOnlyList<GeoPoint2> boundary = wipeout.GetClipBoundary();
            Assert.Equal(5, boundary.Count);
            Near(0.3695652173913043, boundary[0].X);
            Near(0.0652173913043479, boundary[0].Y);
            Near(
                new[] { P(200, 100, 0), P(230, 100, 0), P(230, 110, 0), P(200, 110, 0), P(200, 100, 0) },
                wipeout.GetVertices()
            );

            // The rectangle (-30, -10) to (10, 20): the square of side 40 at (-30, -10), as the file has it, to the byte.
            DxfDatabase again = DxfDatabase.Parse(text);
            ByHandle<DxfWipeout>(again, "C14")
                .SetFrom(
                    new[] { Q(-30, -10), Q(10, -10), Q(10, 20), Q(-30, 20), Q(-30, -10) },
                    new GeoVector3(0, 0, 2)
                );
            Assert.Equal(text, again.ToText());

            // Upright, its normal (0, -1, 0): its frame along x and z, as AutoCAD 2021 laid it.
            wipeout.SetFrom(new[] { Q(0, 0), Q(20, 0), Q(20, 10), Q(0, 10), Q(0, 0) }, new GeoVector3(0, -1, 0));
            wipeout.GetOrientation(out origin, out u, out v);
            Near(new GeoVector3(20, 0, 0), u);
            Near(new GeoVector3(0, 0, 20), v);

            Assert.Throws<ArgumentException>(() =>
                wipeout.SetFrom(new[] { Q(0, 0), Q(30, 0), Q(15, 20) }, new GeoVector3(0, 0, 1))
            );
            Assert.Throws<ArgumentException>(() =>
                wipeout.SetFrom(new[] { Q(0, 0), Q(30, 0), Q(60, 0), Q(0, 0) }, new GeoVector3(0, 0, 1))
            );
            Assert.Throws<ArgumentException>(() =>
                wipeout.SetFrom(new[] { Q(0, 0), Q(30, 0), Q(15, 20), Q(0, 0) }, new GeoVector3(0, 0, 0))
            );
        }

        [Fact]
        public void TheImageDefAndTheRasterVariables_AreReadAsTheFileHasThem_AndTheVariablesSet()
        {
            string text = DxfRasterImageDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfRasterImage image = ByHandle<DxfRasterImage>(db, "8E");
            DxfRasterImageDef definition = image.ImageDef;

            Assert.Equal("8D", definition.Handle);
            Assert.Equal("photo.jpg", definition.SourceFileName);
            Assert.Equal(new GeoVector2(640, 480), definition.Size);
            Assert.Equal(new GeoVector2(0.01, 0.01), definition.ResolutionMMPerPixel);
            Assert.Equal(DxfUnit.None, definition.ResolutionUnits);
            Assert.True(definition.IsLoaded);
            Assert.Equal("photo.jpg", image.Name);
            Assert.Equal("photo.jpg", image.Path);
            Assert.Equal(new GeoVector2(640, 480), image.ImageSize(false));
            Assert.Same(definition, DxfRasterImageDef.GetImageDictionary(db).GetAt("photo.jpg"));
            Assert.Null(DxfRasterImageDef.GetImageDictionary(new DxfDatabase("AC1032")));

            // ezdxf's RASTERVARIABLES: 70 of 0, 71 of 1, 72 of 3.
            var variables = (DxfRasterVariables)db.NamedObjectsDictionary.GetAt("ACAD_IMAGE_VARS");
            Assert.Equal(DxfFrameSetting.ImageFrameOff, variables.ImageFrame);
            Assert.Equal(DxfImageQuality.High, variables.ImageQuality);
            Assert.Equal(DxfUnit.Meter, variables.UserScale);

            // Set, each written as its number, as AutoCAD 2021 writes 3 for ImageFrameOnNoPlot.
            variables.ImageFrame = DxfFrameSetting.ImageFrameOnNoPlot;
            variables.ImageQuality = DxfImageQuality.Draft;
            variables.UserScale = DxfUnit.Millimeter;
            Assert.Equal("90=0 70=3 71=0 72=1", After(Groups(db.ToText(), variables.Handle), "90"));
            Assert.Throws<ArgumentOutOfRangeException>(() => variables.ImageFrame = DxfFrameSetting.ImageFrameInvalid);

            definition.SourceFileName = @"C:\pictures\photo.jpg";
            Assert.Equal(
                @"C:\pictures\photo.jpg",
                ((DxfRasterImageDef)DxfDatabase.Parse(db.ToText()).GetObject("8D")).SourceFileName
            );
            OnlyTheseChanged(text, db.ToText(), variables.Handle, "8D");
        }

        [Fact]
        public void AnImageErased_TakesItsReactorWithIt_AndLeavesItsImageDef()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);

            ByHandle<DxfRasterImage>(db, "8E").Erase();
            string text = db.ToText();

            Assert.DoesNotContain(Records(text), r => HandleOf(r) == "8F");
            Assert.Contains(Records(text), r => HandleOf(r) == "8D");
            Assert.IsType<DxfRasterImageDef>(DxfDatabase.Parse(text).GetObject("8D"));
        }
    }
}
