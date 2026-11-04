using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// IMAGEDEFs, IMAGEs and WIPEOUTs made in code, as AutoCAD 2021's API makes them: the definition under the name
    /// <c>SuggestName</c> gives, in the dictionary of images made for it, with the RASTERVARIABLES and the CLASS rows; the image
    /// placed at one unit a pixel, given its IMAGEDEF_REACTOR when appended; the wipeout set from its polygon; erased, renamed,
    /// placed in blocks and paper space, and refused before the versions that hold them.
    /// </summary>
    public class DxfRasterImageMakeTests
    {
        private const string Picture = @"C:\pictures\px43.png";

        private static void Near(GeoPoint3 expected, GeoPoint3 actual) => Assert.True(expected.DistanceTo(actual) <= 1E-9, expected + " was " + actual);

        private static void Near(GeoVector3 expected, GeoVector3 actual) => Assert.True((expected - actual).Length <= 1E-9, expected + " was " + actual);

        private static string Record(string text, string handle) => Records(text).First(r => HandleOf(r) == handle);

        private static string[] Groups(string record)
        {
            string[] lines = record.TrimEnd('\n').Split('\n');
            var groups = new List<string>();

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                groups.Add(lines[i].Trim() + "=" + lines[i + 1].Trim());
            }

            return groups.ToArray();
        }

        private static string After(string record, string from) => string.Join(" ", Groups(record).SkipWhile(g => !g.StartsWith(from, StringComparison.Ordinal)));

        private static string ClassRecord(string text, string name)
            => Records(text).FirstOrDefault(r => r.StartsWith("  0\nCLASS\n", StringComparison.Ordinal) && r.Contains("\n  1\n" + name + "\n"));

        private static DxfRasterImageDef Definition(DxfDatabase db)
        {
            DxfDBDictionary images = DxfRasterImageDef.CreateImageDictionary(db);
            var definition = new DxfRasterImageDef(Picture, new GeoVector2(4, 3));
            images.SetAt(DxfRasterImageDef.SuggestName(images, Picture), definition);
            return definition;
        }

        [Fact]
        public void ADefinitionMadeInCode_IsAddedUnderItsSuggestedName_WithTheDictionaryAndTheVariablesMadeForIt()
        {
            var db = new DxfDatabase("AC1032");
            Assert.Null(DxfRasterImageDef.GetImageDictionary(db));

            DxfDBDictionary images = DxfRasterImageDef.CreateImageDictionary(db);
            Assert.Same(images, DxfRasterImageDef.CreateImageDictionary(db));
            Assert.Same(images, DxfRasterImageDef.GetImageDictionary(db));

            // AutoCAD 2021's SuggestName: the file's name without its extension, then _1 once that is taken.
            Assert.Equal("px43", DxfRasterImageDef.SuggestName(images, Picture));
            Assert.Equal("Photo 1", DxfRasterImageDef.SuggestName(images, @"pictures\Photo 1.JPG"));
            var definition = new DxfRasterImageDef(Picture, new GeoVector2(4, 3))
            {
                ResolutionMMPerPixel = new GeoVector2(0.2646, 0.2646),
                ResolutionUnits = DxfUnit.Centimeter,
            };
            Assert.Equal(new GeoVector2(4, 3), definition.Size);
            images.SetAt("px43", definition);
            Assert.Equal("px43_1", DxfRasterImageDef.SuggestName(images, Picture));
            Assert.Equal("PX43_1", DxfRasterImageDef.SuggestName(images, "PX43.tif"));

            string text = db.ToText();
            Assert.Equal("100=AcDbRasterImageDef 90=0 1=C:\\pictures\\px43.png 10=4 20=3 11=0.2646 21=0.2646 280=1 281=2", After(Record(text, definition.Handle), "100=AcDbRasterImageDef"));
            Assert.Throws<ArgumentOutOfRangeException>(() => definition.ResolutionUnits = DxfUnit.Millimeter);

            // The variables of images, as AutoCAD 2021 gives a drawing it opens with images, and the classes counted.
            var variables = Assert.IsType<DxfRasterVariables>(db.NamedObjectsDictionary.GetAt("ACAD_IMAGE_VARS"));
            Assert.Equal(DxfFrameSetting.ImageFrameAbove, variables.ImageFrame);
            Assert.Equal(DxfImageQuality.High, variables.ImageQuality);
            Assert.Equal(DxfUnit.None, variables.UserScale);
            Assert.Contains("\n 91\n1\n", ClassRecord(text, "IMAGEDEF"));
            Assert.Contains("\n 91\n1\n", ClassRecord(text, "RASTERVARIABLES"));

            DxfDatabase reopened = DxfDatabase.Parse(text);
            var read = (DxfRasterImageDef)DxfRasterImageDef.GetImageDictionary(reopened).GetAt("px43");
            Assert.Equal(Picture, read.SourceFileName);
            Assert.Equal(new GeoVector2(4, 3), read.Size);
            Assert.Equal(DxfUnit.Centimeter, read.ResolutionUnits);
        }

        [Fact]
        public void AnImageMadeInCode_IsPlacedAsAutoCad2021PlacesANewOne_AndGivenItsReactorWhenAppended()
        {
            var db = new DxfDatabase("AC1032");
            DxfRasterImageDef definition = Definition(db);
            var image = new DxfRasterImage(definition);

            // AutoCAD 2021: a new image given its definition is one unit a pixel at the origin, (4, 0, 0) by (0, 3, 0).
            image.GetOrientation(out GeoPoint3 origin, out GeoVector3 u, out GeoVector3 v);
            Near(new GeoPoint3(0, 0, 0), origin);
            Near(new GeoVector3(4, 0, 0), u);
            Near(new GeoVector3(0, 3, 0), v);
            Assert.Same(definition, image.ImageDef);
            Assert.Equal(DxfImageDisplayOptions.Show | DxfImageDisplayOptions.ShowUnaligned | DxfImageDisplayOptions.Clip, image.DisplayOptions);

            image.SetOrientation(new GeoPoint3(10, 20, 0), new GeoVector3(40, 0, 0), new GeoVector3(0, 30, 0));
            db.ModelSpace.AppendEntity(image);
            string text = db.ToText();
            string record = Record(text, image.Handle);
            string reactor = Groups(record).Single(g => g.StartsWith("360=", StringComparison.Ordinal)).Substring(4);

            Assert.Equal("100=AcDbRasterImage 90=0 10=10 20=20 30=0 11=10 21=0 31=0 12=0 22=10 32=0 13=4 23=3 340=" + definition.Handle
                + " 70=7 280=0 281=50 282=50 283=0 290=0 360=" + reactor + " 71=1 91=2 14=-0.5 24=-0.5 14=3.5 24=2.5", After(record, "100=AcDbRasterImage"));

            // The reactor as AutoCAD writes it: owned by the image and naming it; the definition lists it.
            Assert.Equal("0=IMAGEDEF_REACTOR 5=" + reactor + " 330=" + image.Handle + " 100=AcDbRasterImageDefReactor 90=2 330=" + image.Handle, string.Join(" ", Groups(Record(text, reactor))));
            Assert.Contains("330=" + reactor, Groups(Record(text, definition.Handle)));
            Assert.Equal(1, definition.GetEntityCount(out bool locked));
            Assert.False(locked);
            Assert.Contains("\n 91\n1\n", ClassRecord(text, "IMAGE"));
            Assert.Contains("\n 91\n1\n", ClassRecord(text, "IMAGEDEF_REACTOR"));
            Near(new GeoPoint3(50, 50, 0), db.Extmax);

            // AutoCAD 2021's Name set renames the entry of the definition.
            Assert.Equal("px43", image.Name);
            image.Name = "photo";
            Assert.Same(definition, DxfRasterImageDef.GetImageDictionary(db).GetAt("photo"));
            Assert.False(DxfRasterImageDef.GetImageDictionary(db).Contains("px43"));
        }

        [Fact]
        public void ImagesMadeInCode_GoIntoBlocksAndPaperSpace_EachWithItsReactor_AndAreCounted()
        {
            var db = new DxfDatabase("AC1032");
            DxfRasterImageDef definition = Definition(db);
            var tag = new DxfBlockTableRecord { Name = "TAG" };
            db.BlockTable.Add(tag);
            tag.AppendEntity(new DxfRasterImage(definition));
            db.PaperSpace.AppendEntity(new DxfRasterImage(definition));
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(1000, 0, 0), tag));
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(2000, 0, 0), tag));

            Assert.Equal(2, definition.GetEntityCount(out _));
            string text = db.ToText();
            Assert.Equal(2, Records(text).Count(r => r.StartsWith("  0\nIMAGEDEF_REACTOR\n", StringComparison.Ordinal)));
            Assert.Contains("67=1", Groups(Record(text, db.PaperSpace.Cast<DxfEntity>().Single().Handle)));

            var placed = (DxfRasterImage)db.ModelSpace.OfType<DxfBlockReference>().Last().Explode().Single();
            placed.GetOrientation(out GeoPoint3 origin, out _, out _);
            Near(new GeoPoint3(2000, 0, 0), origin);
        }

        [Fact]
        public void AWipeoutMadeInCode_IsSetFromItsPolygon_WithTheVariablesOfWipeouts_AndRefusedWithoutOne()
        {
            var db = new DxfDatabase("AC1032");
            var wipeout = new DxfWipeout();

            // AutoCAD 2021 writes a wipeout never set as one of no boundary, and then crashes opening it.
            Assert.Throws<InvalidOperationException>(() => db.ModelSpace.AppendEntity(wipeout));
            Assert.False(wipeout.IsResident);

            wipeout.SetFrom(new[] { new GeoPoint2(-30, -10), new GeoPoint2(10, -10), new GeoPoint2(10, 20), new GeoPoint2(-30, 20), new GeoPoint2(-30, -10) }, new GeoVector3(0, 0, 1));
            db.ModelSpace.AppendEntity(wipeout);
            string text = db.ToText();
            Assert.Equal("100=AcDbWipeout 90=0 10=-30 20=-10 30=0 11=40 21=0 31=0 12=0 22=40 32=0 13=1 23=1 340=0 70=7 280=1 281=50 282=50 283=0 290=0 360=0 71=2 91=5 "
                + "14=-0.5 24=0.5 14=0.5 24=0.5 14=0.5 24=-0.25 14=-0.5 24=-0.25 14=-0.5 24=0.5", After(Record(text, wipeout.Handle), "100=AcDbWipeout"));

            // The variables of wipeouts as AutoCAD 2021 gives a drawing it opens with one: frames shown.
            DxfDBObject variables = db.NamedObjectsDictionary.GetAt("ACAD_WIPEOUT_VARS");
            Assert.Equal("100=AcDbWipeoutVariables 70=1", After(Record(text, variables.Handle), "100=AcDbWipeoutVariables"));
            Assert.Contains("\n 91\n1\n", ClassRecord(text, "WIPEOUT"));
            Assert.Contains("\n 91\n1\n", ClassRecord(text, "WIPEOUTVARIABLES"));
            Near(new GeoPoint3(-30, -10, 0), db.Extmin);
        }

        [Fact]
        public void ImagesDefinitionsAndWipeouts_AreRefusedBeforeTheVersionsThatHoldThem_AndAcrossDrawings()
        {
            DxfDatabase r12 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            Assert.Throws<InvalidOperationException>(() => DxfRasterImageDef.CreateImageDictionary(r12));

            var wipeout = new DxfWipeout();
            wipeout.SetFrom(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 5), new GeoPoint2(0, 0) }, new GeoVector3(0, 0, 1));
            Assert.Throws<ArgumentException>(() => r12.ModelSpace.AppendEntity(wipeout));

            // An image shows a definition of its own drawing: appended to another, it would point at nothing there.
            var one = new DxfDatabase("AC1032");
            var two = new DxfDatabase("AC1032");
            var image = new DxfRasterImage(Definition(one));
            Assert.Throws<ArgumentException>(() => two.ModelSpace.AppendEntity(image));
            Assert.Throws<ArgumentException>(() => new DxfRasterImage(new DxfRasterImageDef(Picture, new GeoVector2(4, 3))));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DxfRasterImageDef(Picture, new GeoVector2(0, 3)));
            one.ModelSpace.AppendEntity(image);
        }

        [Fact]
        public void ADefinitionErased_TakesTheImagesThatShowItWithIt_AndAnImageErasedItsReactor()
        {
            var db = new DxfDatabase("AC1032");
            DxfRasterImageDef definition = Definition(db);
            var shown = new DxfRasterImage(definition);
            var erased = new DxfRasterImage(definition);
            db.ModelSpace.AppendEntity(shown);
            db.ModelSpace.AppendEntity(erased);
            DxfEntity copy = shown.Clone();
            db.ModelSpace.AppendEntity(copy);
            Assert.Equal(2, definition.GetEntityCount(out _));

            erased.Erase();
            Assert.Equal(1, definition.GetEntityCount(out _));
            Assert.Single(Records(db.ToText()), r => r.StartsWith("  0\nIMAGEDEF_REACTOR\n", StringComparison.Ordinal));

            // As AutoCAD 2021's Erase of a RasterImageDef takes the images it is associated with; the copy of no reactor too.
            definition.Erase();
            Assert.True(definition.IsErased);
            Assert.True(shown.IsErased);
            Assert.True(copy.IsErased);
            Assert.False(DxfRasterImageDef.GetImageDictionary(db).Contains("px43"));
            Assert.DoesNotContain(Records(db.ToText()), r => r.StartsWith("  0\nIMAGE", StringComparison.Ordinal));
        }

        [Fact]
        public void AnImageAssociatedWithAnotherDefinition_KeepsItsSides_AndMovesItsReactor()
        {
            var db = new DxfDatabase("AC1032");
            DxfRasterImageDef first = Definition(db);
            DxfDBDictionary images = DxfRasterImageDef.GetImageDictionary(db);
            var second = new DxfRasterImageDef(@"C:\pictures\big.png", new GeoVector2(8, 6));
            images.SetAt("big", second);
            var image = new DxfRasterImage(first);
            image.SetOrientation(new GeoPoint3(0, 0, 0), new GeoVector3(40, 0, 0), new GeoVector3(0, 30, 0));
            db.ModelSpace.AppendEntity(image);

            image.AssociateRasterDef(second);
            Assert.Same(second, image.ImageDef);
            Assert.Equal(8.0, image.ImageWidth);
            image.GetOrientation(out _, out GeoVector3 u, out GeoVector3 v);
            Near(new GeoVector3(40, 0, 0), u);
            Near(new GeoVector3(0, 30, 0), v);
            Assert.Equal(new[] { new GeoPoint2(-0.5, -0.5), new GeoPoint2(-0.5, 5.5), new GeoPoint2(7.5, 5.5), new GeoPoint2(7.5, -0.5), new GeoPoint2(-0.5, -0.5) }, image.GetClipBoundary());
            Assert.Equal(0, first.GetEntityCount(out _));
            Assert.Equal(1, second.GetEntityCount(out _));
            Assert.Throws<ArgumentException>(() => image.AssociateRasterDef(Definition(new DxfDatabase("AC1032"))));
        }
    }
}
