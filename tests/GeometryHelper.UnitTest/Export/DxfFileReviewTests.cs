using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfLines;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// The faults the review of the editing engine found, each held to its fix: a subclass marker that comes late, a record
    /// of R13 with none, a text's justification, the cost of reading after erasing and of adding layers, handles pointed at,
    /// letters that read back as UTF-8, a file that names no version, and the refusals of null.
    /// </summary>
    public class DxfFileReviewTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static List<string> Records(string text) => DxfModel.Records(text);

        private static string Record(DxfFile file, string handle) => Records(file.ToText()).Single(r => DxfModel.HandleOf(r) == handle);

        [Fact]
        public void ACircleWhoseMarkerComesLate_IsGivenTheNewCircle_TheOldGroupsGone()
        {
            DxfFile file = DxfFile.Parse(R2000(G(0, "CIRCLE", 5, "2A", 100, "AcDbEntity", 8, "0", 10, "1", 20, "2", 30, "0", 40, "5", 100, "AcDbCircle")));
            DxfItem circle = file.ModelSpace[0];

            circle.Shape = new GeoCircle3(new GeoPoint3(50, 60, 0), new GeoVector3(0, 0, 1), 7);

            var again = (GeoCircle3)DxfFile.Parse(file.ToText()).ModelSpace[0].Shape;
            Assert.Equal(new GeoPoint3(50, 60, 0), again.Center);
            Assert.Equal(7.0, again.Radius);
            Assert.DoesNotContain(" 40\n5\n", Record(file, "2A"), StringComparison.Ordinal);
        }

        [Fact]
        public void ARecordOfR2000WithNoMarkers_GivenANewShape_GetsTheMarkerAcDbEntity()
        {
            string text = R2000(G(0, "LWPOLYLINE", 5, "2A", 8, "0", 90, "2", 70, "0", 10, "0", 20, "0", 10, "5", 20, "0") + G(0, "LINE", 5, "2B", 8, "0", 10, "0", 20, "0", 11, "1", 21, "0"));
            DxfFile file = DxfFile.Parse(text);

            file.ModelSpace[0].Shape = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10) }, new[] { 0.5, 0.0 });
            file.ModelSpace[1].Shape = new DxfText(new GeoPoint3(1, 2, 0), new GeoVector3(0, 0, 1), 2.5, 0, "note\nmore", true);

            Assert.StartsWith("  0\nLWPOLYLINE\n  5\n2A\n100\nAcDbEntity\n  8\n0\n100\nAcDbPolyline\n", Record(file, "2A"), StringComparison.Ordinal);
            Assert.StartsWith("  0\nMTEXT\n  5\n2B\n100\nAcDbEntity\n  8\n0\n100\nAcDbMText\n", Record(file, "2B"), StringComparison.Ordinal);
        }

        [Fact]
        public void ATextJustified_GivenANewText_KeepsItsJustification_AtTheNewPlace()
        {
            string text = R2000(G(0, "TEXT", 5, "2A", 100, "AcDbEntity", 8, "0", 100, "AcDbText", 10, "100.0", 20, "50.0", 30, "0.0", 40, "5", 1, "Centred", 72, "1", 11, "200.0", 21, "50.0", 31, "0.0", 100, "AcDbText", 73, "2")
                + G(0, "MTEXT", 5, "2B", 100, "AcDbEntity", 8, "0", 100, "AcDbMText", 10, "0", 20, "0", 30, "0", 40, "2.5", 71, "5", 72, "1", 1, "Middle"));
            DxfFile file = DxfFile.Parse(text);

            file.ModelSpace[0].Shape = new DxfText(new GeoPoint3(300, 60, 0), new GeoVector3(0, 0, 1), 5, 0, "Centred and more");
            file.ModelSpace[1].Shape = new DxfText(new GeoPoint3(1, 2, 0), new GeoVector3(0, 0, 1), 2.5, 0, "Middle\nmore", true);

            string centred = Record(file, "2A");
            Assert.Contains(" 72\n1\n", centred, StringComparison.Ordinal);
            Assert.EndsWith("100\nAcDbText\n 73\n2\n", centred, StringComparison.Ordinal);
            Assert.Contains(" 11\n300\n 21\n60\n", centred, StringComparison.Ordinal);
            var read = (DxfText)DxfReader.Parse(file.ToText(), new DxfReadOptions { Tolerance = Tol }).Shapes[0].Shape;
            Assert.Equal(new GeoPoint3(300, 60, 0), read.Position);
            Assert.Equal("Centred and more", read.Value);

            string middle = Record(file, "2B");
            Assert.Contains(" 71\n5\n", middle, StringComparison.Ordinal);
            Assert.Equal(1, Records(file.ToText()).Single(r => DxfModel.HandleOf(r) == "2B").Split('\n').Count(l => l == " 71"));
        }

        [Fact]
        public void ReadingTheHeaderAfterEachErasure_DoesNotSeeToThePointersEachTime()
        {
            DxfFile file = DxfFile.Parse(DxfFileEzdxfDrawings.R2000);
            int before = file.Cleanings;

            foreach (DxfItem item in file.ModelSpace.Where(i => i.Type == "LINE" || i.Type == "CIRCLE").ToList())
            {
                file.ModelSpace.Remove(item);
                Assert.NotNull(file.HeaderOf("$HANDSEED"));
            }

            Assert.Equal(before, file.Cleanings);
            file.ToText();
            Assert.Equal(before + 1, file.Cleanings);
        }

        [Fact]
        public void LayersAddedOneAfterAnother_AreLookedUpWithoutReadingTheTableAgain()
        {
            DxfFile file = DxfFile.Parse(DxfFileEzdxfDrawings.R2000);
            DxfItem line = file.ModelSpace.First(i => i.Type == "LINE");
            int before = file.LayerTableReadings;

            for (int i = 0; i < 50; i++)
            {
                line.Layer = "L" + i;
            }

            Assert.True(file.LayerTableReadings - before <= 1, (file.LayerTableReadings - before) + " readings");
            Assert.Equal(50, DxfFile.Parse(file.ToText()).LayerTable.Count(l => l.Name.StartsWith("L", StringComparison.Ordinal) && l.Name.Length <= 3));
        }

        [Fact]
        public void AHandleSomethingPointsAt_IsNotGivenToANewEntity()
        {
            // The GROUP 20 lists the handle 3F, which no record has: the new entity is not given it.
            string text = G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, "AC1015", 9, "$HANDSEED", 5, "21", 0, "ENDSEC")
                + G(0, "SECTION", 2, "ENTITIES", 0, "LINE", 5, "1F", 100, "AcDbEntity", 8, "0", 100, "AcDbLine", 10, "0", 20, "0", 30, "0", 11, "1", 21, "0", 31, "0", 0, "ENDSEC")
                + G(0, "SECTION", 2, "OBJECTS", 0, "GROUP", 5, "20", 100, "AcDbGroup", 300, "", 70, "1", 71, "1", 340, "3F", 0, "ENDSEC", 0, "EOF");
            DxfFile file = DxfFile.Parse(text);

            DxfItem added = file.ModelSpace.Add(new GeoPoint3(1, 2, 3));

            Assert.Equal("40", added.Handle);
        }

        [Fact]
        public void LettersOfACodePageThatReadAsUtf8_AreWrittenAsTheirEscapes_AndReadBackAsThemselves()
        {
            // "AÃ©" in Windows-1252 is the bytes 41 C3 A9, which read as UTF-8 are "Aé".
            DxfFile file = DxfFile.Parse(R2000(G(0, "LINE", 5, "2A", 8, "0", 10, "0", 20, "0", 11, "1", 21, "0")));
            file.ModelSpace[0].Layer = "AÃ©";

            byte[] saved;

            using (var stream = new MemoryStream())
            {
                file.Save(stream);
                saved = stream.ToArray();
            }

            DxfFile again;

            using (var stream = new MemoryStream(saved))
            {
                again = DxfFile.Open(stream);
            }

            Assert.Equal("AÃ©", DxfReader.NameOf(again.ModelSpace[0].Layer));
            Assert.Equal("AÃ©", again.LayerTable.Count == 0 ? DxfReader.NameOf(again.ModelSpace[0].Layer) : again.LayerTable.Last().Name);
        }

        [Fact]
        public void AFileThatNamesNoVersion_ButWritesSubclassMarkers_IsWrittenInTheFormsOfR2000()
        {
            string text = G(0, "SECTION", 2, "ENTITIES", 0, "LINE", 100, "AcDbEntity", 8, "0", 100, "AcDbLine", 10, "0", 20, "0", 30, "0", 11, "1", 21, "0", 31, "0", 0, "ENDSEC", 0, "EOF");
            DxfFile file = DxfFile.Parse(text);

            DxfItem added = file.ModelSpace.Add(new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0) }, new[] { 0.5 }));

            Assert.Equal("LWPOLYLINE", added.Type);
            Assert.Contains(added.Groups, g => g.TypeCode == 100 && g.Value == "AcDbEntity");
        }

        [Fact]
        public void NoLayer_IsRefused_AndNoShapeWhereThereIsNone_ChangesNothing()
        {
            DxfFile file = DxfFile.Parse(DxfFileEzdxfDrawings.R2000);
            DxfItem line = file.ModelSpace.First(i => i.Type == "LINE");
            DxfItem hatch = file.ModelSpace.First(i => i.Type == "HATCH");
            string before = file.ToText();

            Assert.Throws<ArgumentNullException>(() => line.Layer = null);
            hatch.Shape = hatch.Shape;
            Assert.Throws<ArgumentNullException>(() => line.Shape = null);
            Assert.Equal(before, file.ToText());
        }
    }
}
