using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// What AutoCAD 2021 keeps in the extension dictionary of an entity (<c>ACAD_XREC_ROUNDTRIP</c>, an XRECORD) when it saves a
    /// drawing with dynamic blocks as R2007: the identifiers of the vertices of a LWPOLYLINE (<c>VERTEXIDENTIFIER</c>), which R2007
    /// has no place for, and a mark <c>ISDYNAMIC</c> on each dimension of a constraint, which it makes invisible and takes the text
    /// of. The constraint groups of the drawing need both: AUDIT of 2021 found 34 errors in the 5 groups of a drawing written to
    /// R2007 with the groups and without the records, so the groups were left out, and with them the network, the dependencies and
    /// the evaluation graph of the block. The drawings here are built in code from the layouts of AutoCAD's own R2007 files
    /// (26 of the 167 drawings of its samples and tests: 8 polylines and 25 dimensions in Architectural - Metric, each as the
    /// records here).
    /// </summary>
    public class DwgDynamicRoundTripOf2007Tests
    {
        private const string Constraints = "*ADSK_CONSTRAINTS";

        private static readonly string[] Classes =
        {
            "ACDBASSOCNETWORK|AcDbAssocNetwork",
            "ACDBASSOCVARIABLE|AcDbAssocVariable",
            "ACDBASSOCDEPENDENCY|AcDbAssocDependency",
            "ACDBASSOC2DCONSTRAINTGROUP|AcDbAssoc2dConstraintGroup",
        };

        private static string Handle(DwgAnswerKey.Record record) => record.Key.Substring(record.Key.IndexOf(':') + 1);

        private static List<DwgAnswerKey.Record> Records(string text) =>
            DwgAnswerKey.Parse(Encoding.UTF8.GetBytes(text));

        private static string Object(string type, string handle, string owner, string groups)
        {
            var text = new StringBuilder("  0\n" + type + "\n  5\n" + handle + "\n330\n" + owner + "\n");

            foreach (string group in groups.Split('|'))
            {
                int equals = group.IndexOf('=');
                text.Append(group.Substring(0, equals)).Append('\n').Append(group.Substring(equals + 1)).Append('\n');
            }

            return text.ToString();
        }

        /// <summary>A network owned by the dictionary with one action, a constraint group that owns a dependency and a variable.</summary>
        private static string Group(string dictionary) =>
            Object(
                "ACDBASSOCNETWORK",
                "F300",
                dictionary,
                "100=AcDbAssocAction|90=1|90=0|330=0|360=0|90=1|90=0|90=0"
                    + "|100=AcDbAssocNetwork|90=0|90=12|90=2|360=F301|360=F302|90=0"
            )
            + Object(
                "ACDBASSOC2DCONSTRAINTGROUP",
                "F301",
                "F300",
                "100=AcDbAssocAction|90=1|90=0|330=F300|360=0|90=3|90=7|90=0"
                    + "|90=2|70=0|10=0.0|20=0.0|30=0.0|10=1.0|20=0.0|30=0.0|10=0.0|20=1.0|30=0.0"
                    + "|360=0|90=1|360=F303|90=8|90=1"
                    + "|1=AcConstrainedBoundedLine|330=F301|90=1|70=0|90=1|90=3|330=F303|90=0"
                    + "|10=0.0|20=4.0|30=0.0|10=0.0|20=-1.0|30=0.0|290=0|10=0.0|20=4.0|30=0.0|11=36.0|21=4.0|31=0.0"
            )
            + Object(
                "ACDBASSOCVARIABLE",
                "F302",
                "F300",
                "100=AcDbAssocAction|90=1|90=0|330=F300|360=0|90=9|90=0|90=0"
                    + "|100=AcDbAssocVariable|90=2|1=Size|1=|1=AcDbCalc:1.0|1=|40=0.0625|290=0|290=0|90=0"
            )
            + Object(
                "ACDBASSOCDEPENDENCY",
                "F303",
                "F301",
                "100=AcDbAssocDependency|90=2|90=0|290=1|290=0|290=1|290=1|90=0|330=0|290=0|330=0|330=0|360=0|90=15"
            );

        /// <summary>
        /// The R2018 text of a drawing with the records <paramref name="entities"/> added to its model space (given the handle of
        /// the model space's block record) and <paramref name="objects"/> to its OBJECTS (given the handle of its first
        /// dictionary), the classes of the new types in its CLASSES and a layer of the constraints in its table.
        /// </summary>
        private static string Drawing(Func<string, string> entities, Func<string, string> objects)
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            string text = db.ToText().Replace("\r\n", "\n");

            // The layer is written as AutoCAD names it, with the star the database refuses in a name.
            int table = text.IndexOf("\n  2\nLAYER\n  5\n", StringComparison.Ordinal);
            int from = table + 15;
            string tableHandle = text.Substring(from, text.IndexOf('\n', from) - from);
            text = text.Insert(
                text.IndexOf("  0\nENDTAB", table, StringComparison.Ordinal),
                "  0\nLAYER\n  5\nF0A0\n330\n"
                    + tableHandle
                    + "\n100\nAcDbSymbolTableRecord\n100\nAcDbLayerTableRecord\n  2\n"
                    + Constraints
                    + "\n 70\n     0\n 62\n     7\n  6\nContinuous\n"
            );
            List<DwgAnswerKey.Record> all = Records(text);
            string dictionary = Handle(all.First(r => r.Type == "DICTIONARY"));
            string model = Handle(
                all.First(r => r.Type == "BLOCK_RECORD" && r.Groups.Any(g => g.Key == 2 && g.Value == "*Model_Space"))
            );
            var classes = new StringBuilder();

            foreach (string one in Classes.Where(c => !text.Contains("\n" + c.Split('|')[0] + "\n")))
            {
                string[] names = one.Split('|');
                classes
                    .Append("  0\nCLASS\n  1\n")
                    .Append(names[0])
                    .Append("\n  2\n")
                    .Append(names[1])
                    .Append("\n  3\nObjectDBX Classes\n 90\n1025\n280\n0\n281\n0\n");
            }

            text = Insert(text, "CLASSES", classes.ToString());
            text = Insert(text, "ENTITIES", entities(model));
            return Insert(text, "OBJECTS", objects(dictionary));
        }

        /// <summary>The records <paramref name="records"/> put ahead of the end of the section <paramref name="section"/>.</summary>
        private static string Insert(string text, string section, string records)
        {
            int at = text.IndexOf(
                "  0\nENDSEC",
                text.IndexOf("  2\n" + section + "\n", StringComparison.Ordinal),
                StringComparison.Ordinal
            );
            return text.Insert(at, records);
        }

        /// <summary>A LWPOLYLINE of four vertices at the corners of a square 10 on a side, the vertices given the identifiers.</summary>
        private static string Polyline(string handle, string owner, params int[] ids)
        {
            var text = new StringBuilder(
                "  0\nLWPOLYLINE\n  5\n" + handle + "\n330\n" + owner + "\n100\nAcDbEntity\n  8\n0\n100\nAcDbPolyline\n"
            );
            text.Append(" 90\n" + ids.Length.ToString(CultureInfo.InvariantCulture) + "\n 70\n     1\n 43\n0.0\n");

            for (int i = 0; i < ids.Length; i++)
            {
                text.Append(" 10\n" + (i % 2 == 0 ? "0.0" : "10.0") + "\n 20\n" + (i < 2 ? "0.0" : "10.0") + "\n");
                text.Append(" 91\n" + ids[i].ToString(CultureInfo.InvariantCulture) + "\n");
            }

            return text.ToString();
        }

        /// <summary>
        /// An aligned dimension of 76.2 on <paramref name="layer"/>, with the text 76.2 typed and the reactors given; as
        /// AutoCAD keeps one of the constraints of a dynamic block.
        /// </summary>
        private static string Dimension(string handle, string owner, string layer, params string[] reactors) =>
            "  0\nDIMENSION\n  5\n"
            + handle
            + "\n"
            + (
                reactors.Length == 0
                    ? string.Empty
                    : "102\n{ACAD_REACTORS\n" + string.Concat(reactors.Select(r => "330\n" + r + "\n")) + "102\n}\n"
            )
            + "330\n"
            + owner
            + "\n100\nAcDbEntity\n  8\n"
            + layer
            + "\n100\nAcDbDimension\n 10\n0.0\n 20\n5.0\n 30\n0.0\n 11\n0.0\n 21\n0.0\n 31\n0.0\n 70\n     1\n  1\n76.2\n"
            + " 71\n     5\n 42\n76.2\n  3\nStandard\n100\nAcDbAlignedDimension\n 13\n0.0\n 23\n0.0\n 33\n0.0\n"
            + " 14\n76.2\n 24\n0.0\n 34\n0.0\n";

        /// <summary>The records of a drawing written as R2007 and read back.</summary>
        private static List<DwgAnswerKey.Record> Back(string text, out IDictionary<string, int> leftOut) =>
            DwgAnswerKey.Parse(DwgReader.ToDxf(DwgWriter.ToDwg(text, "AC1021", out leftOut)));

        /// <summary>The XRECORD an entity keeps under <c>ACAD_XREC_ROUNDTRIP</c> of its extension dictionary; null where it has none.</summary>
        private static DwgAnswerKey.Record RoundTripOf(List<DwgAnswerKey.Record> records, DwgAnswerKey.Record entity)
        {
            string dictionary = entity
                .Groups.SkipWhile(g => g.Value != "{ACAD_XDICTIONARY")
                .Skip(1)
                .FirstOrDefault()
                .Value;

            if (dictionary == null)
            {
                return null;
            }

            DwgAnswerKey.Record dictionaryRecord = records.Single(r => Handle(r) == dictionary);
            string entry = dictionaryRecord
                .Groups.SkipWhile(g => g.Key != 3 || g.Value != "ACAD_XREC_ROUNDTRIP")
                .Skip(1)
                .First()
                .Value;
            return records.Single(r => Handle(r) == entry);
        }

        private static string Hex(params int[] numbers) =>
            string.Concat(
                numbers.SelectMany(BitConverter.GetBytes).Select(b => b.ToString("X2", CultureInfo.InvariantCulture))
            );

        [Fact]
        public void APolylineWithVertexIds_WrittenAsR2007_KeepsThemInTheRoundTripOfItsExtensionDictionary()
        {
            // AutoCAD's own: the ids 27, 28, 31, 29 of a rectangle as the bytes 05000000 1B000000 1C000000 1F000000 1D000000, the
            // count of whole numbers (the ids and itself) first.
            string text = Drawing(model => Polyline("F100", model, 27, 28, 31, 29), dictionary => string.Empty);

            List<DwgAnswerKey.Record> back = Back(text, out _);
            DwgAnswerKey.Record polyline = Assert.Single(back, r => r.Type == "LWPOLYLINE" && Handle(r) == "F100");
            DwgAnswerKey.Record record = RoundTripOf(back, polyline);

            Assert.NotNull(record);
            Assert.Equal("XRECORD", record.Type);
            Assert.Contains(record.Groups, g => g.Key == 102 && g.Value == "VERTEXIDENTIFIER");
            Assert.Equal(Hex(5, 27, 28, 31, 29), record.Groups.Single(g => g.Key == 310).Value.Trim());
            Assert.DoesNotContain(polyline.Groups, g => g.Key == 91);
        }

        [Fact]
        public void APolylineOfMoreThan30Vertices_KeepsItsIdsInGroupsOf127Bytes()
        {
            int[] ids = Enumerable.Range(1, 200).ToArray();
            string drawing = Drawing(model => Polyline("F100", model, ids), dictionary => string.Empty);

            List<DwgAnswerKey.Record> back = Back(drawing, out _);
            DwgAnswerKey.Record polyline = Assert.Single(back, r => r.Type == "LWPOLYLINE" && Handle(r) == "F100");
            DwgAnswerKey.Record record = RoundTripOf(back, polyline);

            Assert.Equal(
                Hex(new[] { 201 }.Concat(ids).ToArray()),
                string.Concat(record.Groups.Where(g => g.Key == 310).Select(g => g.Value.Trim()))
            );
            Assert.Equal(7, record.Groups.Count(g => g.Key == 310));
        }

        [Fact]
        public void APolylineWhoseIdsAreAllZero_GetsNoRecord()
        {
            string text = Drawing(model => Polyline("F100", model, 0, 0, 0, 0), dictionary => string.Empty);

            List<DwgAnswerKey.Record> back = Back(text, out _);

            Assert.Null(RoundTripOf(back, back.Single(r => r.Type == "LWPOLYLINE" && Handle(r) == "F100")));
        }

        [Fact]
        public void ADimensionOfTheConstraints_WrittenAsR2007_IsInvisibleWithoutItsTextAndMarkedDynamic()
        {
            string text = Drawing(
                model => Dimension("F200", model, Constraints, "F303"),
                dictionary => Group(dictionary)
            );

            List<DwgAnswerKey.Record> back = Back(text, out _);
            DwgAnswerKey.Record dimension = Assert.Single(back, r => r.Type == "DIMENSION" && Handle(r) == "F200");
            DwgAnswerKey.Record record = RoundTripOf(back, dimension);

            Assert.Contains(dimension.Groups, g => g.Key == 60 && g.Value.Trim() == "1");
            Assert.DoesNotContain(dimension.Groups, g => g.Key == 1);
            Assert.NotNull(record);
            Assert.Contains(record.Groups, g => g.Key == 102 && g.Value == "ISDYNAMIC");
            Assert.Contains(record.Groups, g => g.Key == 70 && g.Value.Trim() == "1");
        }

        [Fact]
        public void ADimensionThatIsNotOfTheConstraints_IsWrittenAsItIs()
        {
            string text = Drawing(
                model => Dimension("F200", model, "0", "F303") + Dimension("F201", model, Constraints),
                dictionary => Group(dictionary)
            );

            List<DwgAnswerKey.Record> back = Back(text, out _);

            foreach (string handle in new[] { "F200", "F201" })
            {
                DwgAnswerKey.Record dimension = back.Single(r => r.Type == "DIMENSION" && Handle(r) == handle);

                Assert.DoesNotContain(dimension.Groups, g => g.Key == 60);
                Assert.Contains(dimension.Groups, g => g.Key == 1 && g.Value == "76.2");
                Assert.Null(RoundTripOf(back, dimension));
            }
        }

        [Fact]
        public void TheConstraintGroupsOfADrawingMadeIntoR2007_AreKept_AsAutoCadKeepsThem()
        {
            // Without the records the group, its network, its variable and its dependency were left out of the file.
            string text = Drawing(
                model => Polyline("F100", model, 27, 28, 31, 29) + Dimension("F200", model, Constraints, "F303"),
                dictionary => Group(dictionary)
            );

            List<DwgAnswerKey.Record> back = Back(text, out IDictionary<string, int> leftOut);

            Assert.DoesNotContain("ACDBASSOC2DCONSTRAINTGROUP", leftOut.Keys);
            Assert.DoesNotContain("ACDBASSOCNETWORK", leftOut.Keys);
            Assert.Single(back, r => r.Type == "ACDBASSOC2DCONSTRAINTGROUP");
            Assert.Single(back, r => r.Type == "ACDBASSOCDEPENDENCY");
        }

        [Fact]
        public void TheConstraintGroupsOfADrawingWithNoPolylineIds_AreKeptToo_WhereADimensionIsDynamic()
        {
            // The Mechanical samples have no vertex ids, only dimensions of constraints: their dynamic block (60 dependencies, 56
            // dimensions in the Imperial one) was left out whole, and AUDIT finds in it what it finds in the original.
            string text = Drawing(
                model => Dimension("F200", model, Constraints, "F303"),
                dictionary => Group(dictionary)
            );

            Back(text, out IDictionary<string, int> leftOut);

            Assert.DoesNotContain("ACDBASSOC2DCONSTRAINTGROUP", leftOut.Keys);
            Assert.DoesNotContain("ACDBASSOCNETWORK", leftOut.Keys);
        }

        [Fact]
        public void TheConstraintGroupsOfADrawingWithNoRoundTripRecord_AreStillLeftOutOfR2007()
        {
            // A group in a drawing with neither a polyline with ids nor a dynamic dimension has nothing that AUDIT could find wrong
            // with it, and nothing to say that the drawing was made for R2007: it is left as it was, left out.
            string text = Drawing(model => string.Empty, dictionary => Group(dictionary));

            Back(text, out IDictionary<string, int> leftOut);

            Assert.Contains("ACDBASSOC2DCONSTRAINTGROUP", leftOut.Keys);
        }

        [Theory]
        [InlineData(1015, false)]
        [InlineData(1018, false)]
        [InlineData(1021, true)]
        [InlineData(1024, false)]
        [InlineData(1027, false)]
        [InlineData(1032, false)]
        public void OnlyTheDxfOfR2007_GetsTheRoundTripRecords(int target, bool records)
        {
            string text = Drawing(
                model => Polyline("F100", model, 27, 28, 31, 29) + Dimension("F200", model, Constraints, "F303"),
                dictionary => Group(dictionary)
            );

            string converted = DxfVersionConverter.Convert(text, target, new List<string>());

            Assert.Equal(records, converted.Contains("VERTEXIDENTIFIER"));
            Assert.Equal(records, converted.Contains("ISDYNAMIC"));
        }

        [Fact]
        public void ADxfSavedAsR2007_KeepsTheRoundTripRecordsToo()
        {
            string text = Drawing(
                model => Polyline("F100", model, 27, 28, 31, 29) + Dimension("F200", model, Constraints, "F303"),
                dictionary => Group(dictionary)
            );

            string saved = DxfVersionConverter.Convert(text, 1021, new List<string>());

            Assert.Equal(1, Count(saved, "VERTEXIDENTIFIER"));
            Assert.Equal(1, Count(saved, "ISDYNAMIC"));
            Assert.Equal(2, Count(saved, "{ACAD_XDICTIONARY") - Count(text, "{ACAD_XDICTIONARY"));
        }

        private static int Count(string text, string what)
        {
            int count = 0;

            for (
                int at = text.IndexOf(what, StringComparison.Ordinal);
                at >= 0;
                at = text.IndexOf(what, at + 1, StringComparison.Ordinal)
            )
            {
                count++;
            }

            return count;
        }
    }
}
