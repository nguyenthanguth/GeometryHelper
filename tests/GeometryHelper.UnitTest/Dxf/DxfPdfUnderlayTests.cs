using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// PDFDEFINITIONs and PDFUNDERLAYs made in code as AutoCAD 2021's API makes them, and read back from DXF and from DWG: the
    /// dictionary ACAD_PDFUNDERLAYS, the groups of the record in AutoCAD's order and their defaults, the clip boundary kept as
    /// AutoCAD keeps it, the classes listed, and the definitions erased with the underlays that show them.
    /// </summary>
    public class DxfPdfUnderlayTests
    {
        private const string Pdf = @"D:\plans\sample.pdf";

        private static DxfPdfDefinition Definition(DxfDatabase db, string page = "1")
        {
            DxfDBDictionary underlays = DxfPdfDefinition.CreatePdfDictionary(db);
            var definition = new DxfPdfDefinition(Pdf, page);
            underlays.SetAt(DxfPdfDefinition.SuggestName(underlays, Pdf, page), definition);
            return definition;
        }

        private static byte[] Written(DxfDatabase database)
        {
            using (var stream = new MemoryStream())
            {
                DwgWriter.Write(database, stream);
                return stream.ToArray();
            }
        }

        private static void Near(GeoPoint3 expected, GeoPoint3 actual) =>
            Assert.True(expected.DistanceTo(actual) <= 1E-9, expected + " was " + actual);

        [Fact]
        public void ADefinitionMadeInCode_IsAddedUnderTheNameOfItsFileAndPage_InTheDictionaryMadeForIt()
        {
            var db = new DxfDatabase("AC1032");
            Assert.Null(DxfPdfDefinition.GetPdfDictionary(db));

            DxfDBDictionary underlays = DxfPdfDefinition.CreatePdfDictionary(db);
            Assert.Same(underlays, DxfPdfDefinition.CreatePdfDictionary(db));
            Assert.Equal("sample - Page_1", DxfPdfDefinition.SuggestName(underlays, Pdf, "1"));
            Assert.Equal("sheets - Page_12", DxfPdfDefinition.SuggestName(underlays, @"plans/sheets.PDF", "12"));

            DxfPdfDefinition definition = Definition(db);
            Assert.Equal("sample - Page_1_1", DxfPdfDefinition.SuggestName(underlays, Pdf, "1"));
            Assert.Equal("SAMPLE - Page_1_1", DxfPdfDefinition.SuggestName(underlays, "SAMPLE.pdf", "1"));
            Assert.Equal(Pdf, definition.SourceFileName);
            Assert.Equal("1", definition.ItemName);

            string text = db.ToText();
            Assert.Contains("ACAD_PDFUNDERLAYS", text);
            Assert.Contains("AcDbUnderlayDefinition", text);
            Assert.Contains("\n  1\nPDFDEFINITION\n", text);
            Assert.Contains("\n  2\nAcDbPdfDefinition\n", text);

            DxfDatabase reopened = DxfDatabase.Parse(text);
            var read = (DxfPdfDefinition)DxfPdfDefinition.GetPdfDictionary(reopened).GetAt("sample - Page_1");
            Assert.Equal(Pdf, read.SourceFileName);
            Assert.Equal("1", read.ItemName);
        }

        [Fact]
        public void ADefinitionIsRefused_ByADrawingOlderThanR2010_AndAnEntryOfTheSameName()
        {
            var old = new DxfDatabase("AC1018");
            Assert.Throws<InvalidOperationException>(() => DxfPdfDefinition.CreatePdfDictionary(old));

            var db = new DxfDatabase("AC1032");
            DxfDBDictionary underlays = DxfPdfDefinition.CreatePdfDictionary(db);
            underlays.SetAt("sample - Page_1", new DxfPdfDefinition(Pdf, "1"));
            Assert.Throws<ArgumentException>(() => underlays.SetAt("sample - Page_1", new DxfPdfDefinition(Pdf, "1")));
        }

        [Fact]
        public void AnUnderlayMadeInCode_IsAsAutoCad2021MakesANewOne_AndListsItsClassesWhenAppended()
        {
            var db = new DxfDatabase("AC1032");
            DxfPdfDefinition definition = Definition(db);
            var underlay = new DxfPdfUnderlay(definition);

            Near(new GeoPoint3(0, 0, 0), underlay.Position);
            Assert.Equal(new GeoVector3(1, 1, 1), underlay.ScaleFactors);
            Assert.Equal(0.0, underlay.Rotation);
            Assert.Equal(new GeoVector3(0, 0, 1), underlay.Normal);
            Assert.True(underlay.IsOn);
            Assert.True(underlay.IsClipped);
            Assert.False(underlay.HasClipBoundary);
            Assert.False(underlay.IsClipInverted);
            Assert.False(underlay.Monochrome);
            Assert.True(underlay.AdjustColorForBackground);
            Assert.Equal(100, underlay.Contrast);
            Assert.Equal(0, underlay.Fade);
            Assert.Same(definition, underlay.Definition);

            db.ModelSpace.AppendEntity(underlay);
            Assert.Same(definition, underlay.Definition);
            Assert.Equal("sample - Page_1", underlay.Name);
            Assert.Equal(Pdf, underlay.Path);
            Assert.Equal("1", underlay.NameOfSheet);
            Assert.Equal(1, definition.GetEntityCount());

            string text = db.ToText();
            Assert.Contains("\n  1\nPDFUNDERLAY\n  2\nAcDbPdfReference\n", text.Replace("\r", string.Empty));
            Assert.Contains("\n  1\nPDFDEFINITION\n", text);
            Assert.Contains("AcDbUnderlayReference", text);
        }

        [Fact]
        public void AnUnderlayIsPlacedScaledTurnedAndFaded_AsItsRecordSaysAndTheFileReadsItBack()
        {
            var db = new DxfDatabase("AC1032");
            DxfPdfDefinition definition = Definition(db);
            var underlay = new DxfPdfUnderlay(definition);
            db.ModelSpace.AppendEntity(underlay);
            underlay.Position = new GeoPoint3(10, 20, 0);
            underlay.ScaleFactors = new GeoVector3(2, 2, 2);
            underlay.Rotation = 0.5;
            underlay.Fade = 40;
            underlay.Contrast = 70;
            underlay.Monochrome = true;
            underlay.IsOn = false;

            // AutoCAD 2021's record of the same underlay: the turn in degrees, the flags 1 (clip) + 4 (mono) + 8 (adjust) = 13.
            string[] lines = db.ToText().Split('\n').Select(l => l.Trim()).ToArray();
            int at = Array.IndexOf(lines, "AcDbUnderlayReference");
            Assert.True(at > 0);
            string own = string.Join(" ", lines.Skip(at).Take(30));
            Assert.Contains("10 10 20 20 30 0 41 2 42 2 43 2 50 28.64788975654", own);
            Assert.Contains("280 13 281 70 282 40", own);

            DxfDatabase reopened = DxfDatabase.Parse(db.ToText());
            var read = reopened.ModelSpace.OfType<DxfPdfUnderlay>().Single();
            Near(new GeoPoint3(10, 20, 0), read.Position);
            Assert.Equal(new GeoVector3(2, 2, 2), read.ScaleFactors);
            Assert.Equal(0.5, read.Rotation, 12);
            Assert.False(read.IsOn);
            Assert.True(read.Monochrome);
            Assert.Equal(40, read.Fade);
            Assert.Equal(70, read.Contrast);
            Assert.Equal("sample - Page_1", read.Name);

            Assert.Throws<ArgumentOutOfRangeException>(() => underlay.Fade = 101);
            Assert.Throws<ArgumentOutOfRangeException>(() => underlay.Contrast = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => underlay.ScaleFactors = new GeoVector3(0, 1, 1));
            Assert.Throws<ArgumentException>(() => underlay.Normal = new GeoVector3(0, 0, 0));
        }

        [Fact]
        public void TheTransformOfAnUnderlay_TakesThePagesInchesToTheWorld()
        {
            var db = new DxfDatabase("AC1032");
            var underlay = new DxfPdfUnderlay(Definition(db));
            db.ModelSpace.AppendEntity(underlay);
            underlay.Position = new GeoPoint3(10, 20, 0);
            underlay.ScaleFactors = new GeoVector3(2, 2, 2);
            Near(new GeoPoint3(12, 20, 0), underlay.Transform.Transform(new GeoPoint3(1, 0, 0)));
            Near(new GeoPoint3(10, 24, 0), underlay.Transform.Transform(new GeoPoint3(0, 2, 0)));

            underlay.Rotation = Math.PI / 2;
            Near(new GeoPoint3(10, 22, 0), underlay.Transform.Transform(new GeoPoint3(1, 0, 0)));
            Near(new GeoPoint3(6, 20, 0), underlay.Transform.Transform(new GeoPoint3(0, 2, 0)));

            // Laid in the plane xz, normal -y: the page's x runs along world x and its y up world z.
            underlay.Rotation = 0;
            underlay.Normal = new GeoVector3(0, -1, 0);
            Near(new GeoPoint3(12, 20, 0), underlay.Transform.Transform(new GeoPoint3(1, 0, 0)));
            Near(new GeoPoint3(10, 20, 4), underlay.Transform.Transform(new GeoPoint3(0, 2, 0)));
        }

        [Fact]
        public void TheClipBoundary_IsKeptAsAutoCadKeepsIt_ARectangleByItsUpperLeftAndLowerRight()
        {
            var db = new DxfDatabase("AC1032");
            var underlay = new DxfPdfUnderlay(Definition(db));
            db.ModelSpace.AppendEntity(underlay);
            underlay.Position = new GeoPoint3(10, 20, 0);
            underlay.ScaleFactors = new GeoVector3(2, 2, 2);

            underlay.SetClipBoundary(new[] { new GeoPoint2(1, 1), new GeoPoint2(2, 2) });
            Assert.True(underlay.HasClipBoundary);
            Assert.Equal(new[] { new GeoPoint2(1, 2), new GeoPoint2(2, 1) }, underlay.GetClipBoundary());
            Assert.Equal(4, underlay.GetClipFrame().Count);
            Near(new GeoPoint3(12, 22, 0), underlay.GetClipFrame()[0]);
            Near(new GeoPoint3(14, 24, 0), underlay.GetClipFrame()[2]);
            GeoAabb3 box = underlay.GeometricExtents;
            Near(new GeoPoint3(12, 22, 0), box.Min);
            Near(new GeoPoint3(14, 24, 0), box.Max);

            underlay.SetClipBoundary(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(30, 0), new GeoPoint2(30, 20), new GeoPoint2(10, 25) }
            );
            underlay.IsClipInverted = true;
            Assert.Equal(4, underlay.GetClipBoundary().Count);
            Assert.True(underlay.IsClipInverted);

            DxfDatabase reopened = DxfDatabase.Parse(db.ToText());
            var read = reopened.ModelSpace.OfType<DxfPdfUnderlay>().Single();
            Assert.Equal(4, read.GetClipBoundary().Count);
            Assert.Equal(new GeoPoint2(10, 25), read.GetClipBoundary()[3]);
            Assert.True(read.IsClipInverted);

            underlay.ClearClipBoundary();
            Assert.False(underlay.HasClipBoundary);
            Assert.False(underlay.IsClipInverted);
            Assert.Throws<ArgumentException>(() => underlay.SetClipBoundary(new[] { new GeoPoint2(1, 1) }));
        }

        [Fact]
        public void MovingAnUnderlay_MovesItsCorner_AndAScaleAndTurnAreTakenAsAutoCadTakesThem()
        {
            var db = new DxfDatabase("AC1032");
            var underlay = new DxfPdfUnderlay(Definition(db));
            db.ModelSpace.AppendEntity(underlay);
            underlay.Position = new GeoPoint3(10, 20, 0);

            underlay.TransformBy(GeoTransform3.Translation(new GeoVector3(5, 5, 0)));
            Near(new GeoPoint3(15, 25, 0), underlay.Position);

            underlay.TransformBy(GeoTransform3.Scaling(2.0));
            Near(new GeoPoint3(30, 50, 0), underlay.Position);
            Assert.Equal(2.0, underlay.ScaleFactors.X, 12);

            underlay.TransformBy(GeoTransform3.RotationZ(Math.PI / 2));
            Assert.Equal(Math.PI / 2, underlay.Rotation, 9);
            Assert.Equal(2.0, underlay.ScaleFactors.Y, 12);

            Assert.Throws<ArgumentException>(() => underlay.TransformBy(GeoTransform3.Scaling(-1.0, 1.0, 1.0)));
        }

        [Fact]
        public void ErasingADefinition_ErasesItsUnderlaysAndItsEntry()
        {
            var db = new DxfDatabase("AC1032");
            DxfPdfDefinition definition = Definition(db);
            var first = new DxfPdfUnderlay(definition);
            var second = new DxfPdfUnderlay(definition);
            db.ModelSpace.AppendEntity(first);
            db.ModelSpace.AppendEntity(second);
            Assert.Equal(2, definition.GetEntityCount());

            definition.Erase();
            Assert.True(first.IsErased);
            Assert.True(second.IsErased);
            Assert.False(DxfPdfDefinition.GetPdfDictionary(db).Contains("sample - Page_1"));
            Assert.DoesNotContain(db.ModelSpace.OfType<DxfPdfUnderlay>(), u => !u.IsErased);
        }

        [Fact]
        public void AnUnderlay_IsWrittenAsDwgAndReadBack_WithItsDefinitionAndItsClipBoundary()
        {
            var db = new DxfDatabase("AC1032");
            DxfPdfDefinition definition = Definition(db);
            var one = new DxfPdfUnderlay(definition);
            db.ModelSpace.AppendEntity(one);
            one.Position = new GeoPoint3(10, 20, 0);
            one.ScaleFactors = new GeoVector3(2, 2, 2);
            one.Rotation = 0.5;
            one.SetClipBoundary(new[] { new GeoPoint2(0, 0), new GeoPoint2(30, 20) });
            var two = new DxfPdfUnderlay(definition);
            db.ModelSpace.AppendEntity(two);
            two.Position = new GeoPoint3(100, 0, 0);
            two.Fade = 40;
            two.Contrast = 70;
            two.Monochrome = true;
            two.IsOn = false;
            two.SetClipBoundary(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(30, 0), new GeoPoint2(30, 20), new GeoPoint2(10, 25) }
            );
            two.IsClipInverted = true;

            DxfDatabase back = DwgReader.Open(new MemoryStream(Written(db), false));
            DxfPdfUnderlay[] read = back.ModelSpace.OfType<DxfPdfUnderlay>().OrderBy(u => u.Position.X).ToArray();
            Assert.Equal(2, read.Length);
            Near(new GeoPoint3(10, 20, 0), read[0].Position);
            Assert.Equal(new GeoVector3(2, 2, 2), read[0].ScaleFactors);
            Assert.Equal(0.5, read[0].Rotation, 12);
            Assert.Equal(new GeoPoint2(0, 20), read[0].GetClipBoundary()[0]);
            Assert.Equal(new GeoPoint2(30, 0), read[0].GetClipBoundary()[1]);
            Assert.True(read[0].IsOn);
            Assert.True(read[0].IsClipped);

            Assert.False(read[1].IsOn);
            Assert.True(read[1].Monochrome);
            Assert.True(read[1].IsClipInverted);
            Assert.Equal(40, read[1].Fade);
            Assert.Equal(70, read[1].Contrast);
            Assert.Equal(4, read[1].GetClipBoundary().Count);
            Assert.Equal("sample - Page_1", read[1].Name);
            Assert.Equal(Pdf, read[1].Path);
            Assert.Equal("1", read[1].NameOfSheet);
            Assert.Equal(2, read[0].Definition.GetEntityCount());
        }

        [Fact]
        public void AnUnderlayCannotBeAppended_ToADrawingOlderThanR2010_OrOfAnotherDefinitionsDrawing()
        {
            var db = new DxfDatabase("AC1032");
            DxfPdfDefinition definition = Definition(db);
            var other = new DxfDatabase("AC1032");
            Assert.Throws<ArgumentException>(() => other.ModelSpace.AppendEntity(new DxfPdfUnderlay(definition)));
            Assert.Throws<ArgumentException>(() => new DxfPdfUnderlay(new DxfPdfDefinition(Pdf, "1")));
        }
    }
}
