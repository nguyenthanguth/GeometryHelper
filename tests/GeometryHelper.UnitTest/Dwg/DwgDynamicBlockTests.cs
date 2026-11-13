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
    /// The data of a dynamic block was left out of a DWG: its evaluation graph, its parameter, action and grips, and the purge
    /// preventer. AutoCAD takes the anonymous block of a dynamic block that no INSERT places (<c>*U6</c> of the sheets
    /// <c>A-01</c> to <c>S-03</c> in its sample drawings, true name "Drawing Title") for an unused one without them and drops it
    /// from the drawing written again, with the 6 fields and the draw order of its three attribute definitions. A block of
    /// a linear parameter and a stretch action, made in code, is written to a DWG of R2018 and read back.
    /// </summary>
    public class DwgDynamicBlockTests
    {
        private static readonly string[] Types =
        {
            "ACAD_EVALUATION_GRAPH",
            "ACDB_DYNAMICBLOCKPURGEPREVENTER_VERSION",
            "BLOCKGRIPLOCATIONCOMPONENT",
            "BLOCKLINEARGRIP",
            "BLOCKLINEARPARAMETER",
            "BLOCKSTRETCHACTION",
        };

        /// <summary>A panel of four lines with a tag, its width a linear parameter whose stretch takes the right edge and the tag.</summary>
        private static DxfDatabase Stretched()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var block = new DxfBlockTableRecord { Name = "GH_PANEL", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            var lines = new[]
            {
                new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0)),
            };

            foreach (DxfLine line in lines)
            {
                block.AppendEntity(line);
            }

            var tag = new DxfAttributeDefinition(new GeoPoint3(95, 20, 0), "T1", "TAG", "Tag", null);
            block.AppendEntity(tag);
            tag.LockPositionInBlock = true;
            DxfBlockLinearParameter width = block.AddLinearParameter(
                "Width",
                "Overall width",
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                DxfBlockParameterValueSet.Increments(50, 300, 10)
            );
            block.AddStretchAction(
                width,
                new GeoPoint2(90, -10),
                new GeoPoint2(110, 60),
                lines.Take(3).Cast<DxfEntity>().Concat(new DxfEntity[] { tag })
            );
            return db;
        }

        private static List<DwgAnswerKey.Record> Records(string text) =>
            DwgAnswerKey.Parse(Encoding.UTF8.GetBytes(text));

        private static byte[] ThroughDwg(string text) => DwgReader.ToDxf(DwgWriter.ToDwg(text, "AC1032"));

        private static bool Same(string a, string b) =>
            a.Trim() == b.Trim()
            || (
                double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out double x)
                && double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out double y)
                && Math.Abs(x - y) < 1E-9
            );

        [Fact]
        public void ABlockOfALinearParameterAndAStretch_KeepsItsGraphAndEveryObjectItOwns()
        {
            string text = Stretched().ToText();
            List<DwgAnswerKey.Record> before = Records(text);
            List<DwgAnswerKey.Record> after = DwgAnswerKey.Parse(ThroughDwg(text));
            int compared = 0;

            foreach (string type in Types)
            {
                List<DwgAnswerKey.Record> sent = before.Where(r => r.Type == type).ToList();
                List<DwgAnswerKey.Record> got = after.Where(r => r.Type == type).ToList();

                Assert.NotEmpty(sent);
                Assert.Equal(sent.Count, got.Count);

                foreach (DwgAnswerKey.Record one in sent)
                {
                    DwgAnswerKey.Record other = got.Single(r => r.Key == one.Key);
                    List<KeyValuePair<int, string>> a = one.Groups.Where(g => g.Key != 102 && g.Key != 330).ToList();
                    List<KeyValuePair<int, string>> b = other.Groups.Where(g => g.Key != 102 && g.Key != 330).ToList();

                    Assert.Equal(a.Select(g => g.Key), b.Select(g => g.Key));
                    Assert.All(a.Zip(b, (x, y) => new[] { x.Value, y.Value }), pair =>
                        Assert.True(
                            Same(pair[0], pair[1]),
                            one.Type + " " + one.Key + ": " + pair[0] + " is not " + pair[1]
                        )
                    );
                    Assert.Equal(one.Owner, other.Owner);
                    compared++;
                }
            }

            // Seven graph nodes: the parameter, its two grips with two location components each, the action.
            Assert.Equal(1 + 1 + 4 + 2 + 1 + 1, compared);
        }

        [Fact]
        public void ABlockWhoseGraphNamesAnExpressionTheWriterDoesNotKnow_IsWrittenWithoutTheGraph()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var block = new DxfBlockTableRecord { Name = "GH_DOOR", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0)));
            block.AddFlipParameter("Hand", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0));
            string text = db.ToText();

            Assert.Contains(Records(text), r => r.Type == "ACAD_EVALUATION_GRAPH");
            Assert.Contains(Records(text), r => r.Type == "BLOCKFLIPPARAMETER");

            byte[] dwg = DwgWriter.ToDwg(text, "AC1032", out IDictionary<string, int> leftOut);
            List<DwgAnswerKey.Record> after = DwgAnswerKey.Parse(DwgReader.ToDxf(dwg));

            Assert.Contains("ACAD_EVALUATION_GRAPH", leftOut.Keys);
            Assert.Contains("BLOCKFLIPPARAMETER", leftOut.Keys);
            Assert.DoesNotContain(after, r => r.Type == "ACAD_EVALUATION_GRAPH");
            Assert.DoesNotContain(after, r =>
                r.Type.StartsWith("BLOCK", StringComparison.Ordinal) && r.Type != "BLOCK" && r.Type != "BLOCK_RECORD"
            );
        }

        [Fact]
        public void ALocationComponentThatHoldsAShort_KeepsItsKindAndItsValue()
        {
            // AutoCAD keeps a real (kind 40, DXF 140) in the components of a grip's location, and in a flip's a short (kind 70).
            List<string> lines = Stretched().ToText().Replace("\r\n", "\n").Split('\n').ToList();
            int at = lines.FindIndex(l => l.Trim() == "140");

            while (!(lines[at - 2].Trim() == "70" && lines[at - 1].Trim() == "40"))
            {
                at = lines.FindIndex(at + 1, l => l.Trim() == "140");
            }

            lines[at - 1] = "70";
            lines[at] = "70";
            lines[at + 1] = "3";
            string text = string.Join("\n", lines);
            DwgAnswerKey.Record sent = Records(text)
                .First(r => r.Type == "BLOCKGRIPLOCATIONCOMPONENT" && r.Groups.Any(g => g.Key == 1));
            DwgAnswerKey.Record got = DwgAnswerKey.Parse(ThroughDwg(text)).Single(r => r.Key == sent.Key);
            string[] value = got
                .Groups.SkipWhile(g => g.Key != 1)
                .Take(3)
                .Select(g => g.Key + "=" + g.Value.Trim())
                .ToArray();

            Assert.Equal(new[] { "1=", "70=70", "70=3" }, value);
        }

        [Fact]
        public void TheLargestReal_IsGivenAsAutoCADsDxfGivesIt()
        {
            // AutoCAD keeps double.MaxValue for a value not set yet; its DXF holds 1.797693134862314E+99. The digits that bring
            // the number back rounded past the largest one and could not be read, which stopped the whole DWG from being read.
            Assert.Equal("1.797693134862314E+99", DwgDxfWriter.Number(double.MaxValue));
            Assert.Equal("-1.797693134862314E+99", DwgDxfWriter.Number(double.MinValue));
        }
    }
}
