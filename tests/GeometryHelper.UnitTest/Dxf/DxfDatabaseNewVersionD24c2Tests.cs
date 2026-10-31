using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The new drawings of R2004 and later (D24c2): the groups each version adds to the records of the writer's drawing of R2000,
    /// as AutoCAD 2021 writes them in its own saves of R2000, R2004, R2007 and R2010. AutoCAD refused the five new drawings of
    /// R2004 on (plugin v19, "Group 91 (db instance count) expected") and opened the one of R2000.
    /// </summary>
    public class DxfDatabaseNewVersionD24c2Tests
    {
        /// <summary>The records of a DXF text, each its type and its groups after the type, in the file's order.</summary>
        private static List<KeyValuePair<string, List<KeyValuePair<int, string>>>> Records(string text)
        {
            string[] lines = text.Replace("\r", string.Empty).Split('\n');
            var records = new List<KeyValuePair<string, List<KeyValuePair<int, string>>>>();

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);
                string value = lines[i + 1].Trim();

                if (code == 0)
                {
                    records.Add(new KeyValuePair<string, List<KeyValuePair<int, string>>>(value, new List<KeyValuePair<int, string>>()));
                }
                else if (records.Count > 0)
                {
                    records[records.Count - 1].Value.Add(new KeyValuePair<int, string>(code, value));
                }
            }

            return records;
        }

        private static List<List<KeyValuePair<int, string>>> OfType(string text, string type)
            => Records(text).Where(r => r.Key == type).Select(r => r.Value).ToList();

        private static string Codes(IEnumerable<KeyValuePair<int, string>> groups) => string.Join(" ", groups.Select(g => g.Key.ToString(CultureInfo.InvariantCulture)));

        private static string Value(List<KeyValuePair<int, string>> groups, int code) => groups.First(g => g.Key == code).Value;

        /// <summary>The groups of a record from the first of a code on, up to (not with) the next of another code given.</summary>
        private static List<KeyValuePair<int, string>> From(List<KeyValuePair<int, string>> groups, int first, int count)
            => groups.Skip(groups.FindIndex(g => g.Key == first)).Take(count).ToList();

        [Theory]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        [InlineData("AC1024")]
        [InlineData("AC1027")]
        [InlineData("AC1032")]
        public void EveryClassOfANewDrawingOfR2004On_CountsItsObjects_AsAutoCADWantsOfThatVersion(string version)
        {
            string text = new DxfDatabase(version).ToText();
            var records = Records(text);
            var classes = OfType(text, "CLASS");

            Assert.Equal(3, classes.Count);

            foreach (List<KeyValuePair<int, string>> c in classes)
            {
                Assert.Equal("1 2 3 90 91 280 281", Codes(c));
                string name = Value(c, 1);
                Assert.Equal(records.Count(r => r.Key == name).ToString(CultureInfo.InvariantCulture), Value(c, 91));
            }

            Assert.Equal(new[] { "1", "1", "2" }, classes.Select(c => Value(c, 91)));
        }

        [Theory]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        [InlineData("AC1024")]
        [InlineData("AC1027")]
        [InlineData("AC1032")]
        public void TheLayoutsOfANewDrawingOfR2004On_GiveTheShadePlotAfterTheScale_AsAutoCADWritesThem(string version)
        {
            var layouts = OfType(new DxfDatabase(version).ToText(), "LAYOUT");

            Assert.Equal(2, layouts.Count);

            foreach (List<KeyValuePair<int, string>> layout in layouts)
            {
                Assert.Equal("147 1 76 0 77 2 78 300 148 0", string.Join(" ", From(layout, 147, 5).Select(g => g.Key.ToString(CultureInfo.InvariantCulture) + " " + g.Value)));
            }
        }

        [Theory]
        [InlineData("AC1021")]
        [InlineData("AC1024")]
        [InlineData("AC1027")]
        [InlineData("AC1032")]
        public void ModelAndPaperSpaceOfANewDrawingOfR2007On_GiveTheirUnitsExplodabilityAndScaling_AndReadAsBefore(string version)
        {
            var db = new DxfDatabase(version);
            var records = OfType(db.ToText(), "BLOCK_RECORD");

            Assert.Equal(2, records.Count);

            foreach (List<KeyValuePair<int, string>> record in records)
            {
                Assert.Equal("340 70 280 281", Codes(From(record, 340, 4)));
                Assert.Equal(new[] { "0", "1", "0" }, From(record, 70, 3).Select(g => g.Value));
            }

            Assert.Equal(DxfUnits.Undefined, db.ModelSpace.Units);
            Assert.True(db.ModelSpace.Explodable);
            Assert.Equal(DxfBlockScaling.Any, db.PaperSpace.BlockScaling);
        }

        [Fact]
        public void ANewDrawingOfR2004_GivesNoGroupR2007BroughtToModelAndPaperSpace()
        {
            var records = OfType(new DxfDatabase("AC1018").ToText(), "BLOCK_RECORD");

            Assert.Equal(2, records.Count);
            Assert.All(records, r => Assert.Equal("5 330 100 100 2 340", Codes(r)));
        }

        [Fact]
        public void EveryClassOfADynamicBlockMadeInANewDrawingOfR2010_GivesItsCount()
        {
            var db = new DxfDatabase("AC1024");
            var block = new DxfBlockTableRecord { Name = "GH_PANEL", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            var right = new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0));
            block.AppendEntity(right);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            DxfBlockLinearParameter width = block.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, DxfBlockParameterValueSet.Increments(50, 300, 10));
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), new[] { right });
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), block));

            var classes = OfType(DxfDatabase.Parse(db.ToText()).ToText(), "CLASS");

            Assert.True(classes.Count > 3);
            Assert.All(classes, c => Assert.Equal("1 2 3 90 91 280 281", Codes(c)));
        }
    }
}
