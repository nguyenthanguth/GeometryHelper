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

        /// <summary>
        /// Each record of the <paramref name="types"/> in the DXF <paramref name="text"/>, found again after it has been
        /// written as a DWG and read back, group for group (the owners and the reactors apart), by its handle; how many.
        /// </summary>
        private static int KeptThroughDwg(string text, IEnumerable<string> types)
        {
            List<DwgAnswerKey.Record> before = Records(text);
            List<DwgAnswerKey.Record> after = DwgAnswerKey.Parse(ThroughDwg(text));
            int compared = 0;

            foreach (string type in types)
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

            return compared;
        }

        [Fact]
        public void ABlockOfALinearParameterAndAStretch_KeepsItsGraphAndEveryObjectItOwns()
        {
            // Seven graph nodes: the parameter, its two grips with two location components each, the action.
            Assert.Equal(1 + 1 + 4 + 2 + 1 + 1, KeptThroughDwg(Stretched().ToText(), Types));
        }

        /// <summary>A block of four lines, 100 by 50, named for what it is given.</summary>
        private static DxfBlockTableRecord Panel(DxfDatabase db, string name, out DxfLine[] lines)
        {
            var block = new DxfBlockTableRecord { Name = "GH_" + name, Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            lines = new[]
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

            return block;
        }

        private static DxfBlockLinearParameter Width(DxfBlockTableRecord block) =>
            block.AddLinearParameter(
                "Width",
                "Overall width",
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                DxfBlockParameterValueSet.Increments(50, 300, 10)
            );

        /// <summary>One kind of parameter and action on a panel: what it is called here, and what the DXF holds of it.</summary>
        public static IEnumerable<object[]> Kinds()
        {
            yield return new object[] { "Visibility", new[] { "BLOCKVISIBILITYPARAMETER", "BLOCKVISIBILITYGRIP" } };
            yield return new object[] { "Flip", new[] { "BLOCKFLIPPARAMETER", "BLOCKFLIPGRIP", "BLOCKFLIPACTION" } };
            yield return new object[]
            {
                "Rotation",
                new[] { "BLOCKROTATIONPARAMETER", "BLOCKROTATIONGRIP", "BLOCKROTATEACTION" },
            };
            yield return new object[] { "XY", new[] { "BLOCKXYPARAMETER", "BLOCKXYGRIP", "BLOCKMOVEACTION" } };
            yield return new object[]
            {
                "Polar",
                new[] { "BLOCKPOLARPARAMETER", "BLOCKPOLARGRIP", "BLOCKPOLARSTRETCHACTION" },
            };
            yield return new object[] { "Move", new[] { "BLOCKLINEARPARAMETER", "BLOCKMOVEACTION" } };
            yield return new object[] { "Scale", new[] { "BLOCKLINEARPARAMETER", "BLOCKSCALEACTION" } };
            yield return new object[] { "Array", new[] { "BLOCKLINEARPARAMETER", "BLOCKARRAYACTION" } };
            yield return new object[]
            {
                "Lookup",
                new[] { "BLOCKLOOKUPPARAMETER", "BLOCKLOOKUPGRIP", "BLOCKLOOKUPACTION", "BLOCKSTRETCHACTION" },
            };
            yield return new object[] { "Alignment", new[] { "BLOCKALIGNMENTPARAMETER", "BLOCKALIGNMENTGRIP" } };
            yield return new object[]
            {
                "BasePoint",
                new[] { "BLOCKBASEPOINTPARAMETER", "BLOCKPOINTPARAMETER", "BLOCKMOVEACTION" },
            };
        }

        /// <summary>A panel with one kind of parameter and action, as AutoCAD's Block Editor makes it.</summary>
        private static DxfDatabase Of(string kind)
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfBlockTableRecord block = Panel(db, kind.ToUpperInvariant(), out DxfLine[] lines);
            var acted = lines.Cast<DxfEntity>().ToList();

            switch (kind)
            {
                case "Visibility":
                    block.AddVisibilityParameter(
                        "Mark",
                        "What the panel shows",
                        new GeoPoint3(50, -10, 0),
                        new[]
                        {
                            new DxfBlockVisibilityState("Open", new[] { acted[1] }),
                            new DxfBlockVisibilityState("Shut", new[] { acted[2], acted[3] }),
                            new DxfBlockVisibilityState("Blank", new DxfEntity[0]),
                        }
                    );
                    break;
                case "Flip":
                    block.AddFlipAction(
                        block.AddFlipParameter("Hand", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0)),
                        acted.Take(2)
                    );
                    break;
                case "Rotation":
                    block.AddRotateAction(
                        block.AddRotationParameter("Angle", "Turns the panel", new GeoPoint3(0, 0, 0), 40, 0.5),
                        acted
                    );
                    break;
                case "XY":
                    block.AddMoveAction(
                        block.AddXYParameter(
                            "Across",
                            "Across the panel",
                            "Up",
                            "Up the panel",
                            new GeoPoint3(0, 0, 0),
                            new GeoPoint3(100, 50, 0)
                        ),
                        acted
                    );
                    break;
                case "Polar":
                    block.AddPolarStretchAction(
                        block.AddPolarParameter(
                            "Length",
                            "Sets the length",
                            "Angle",
                            "Sets the angle",
                            new GeoPoint3(0, 0, 0),
                            new GeoPoint3(100, 0, 0),
                            10,
                            null,
                            null
                        ),
                        new GeoPoint2(90, -10),
                        new GeoPoint2(110, 60),
                        acted.Take(3),
                        new[] { acted[3] }
                    );
                    break;
                case "Move":
                    block.AddMoveAction(Width(block), acted.Take(2));
                    break;
                case "Scale":
                    block.AddScaleAction(Width(block), acted);
                    break;
                case "Array":
                    block.AddArrayAction(Width(block), acted.Take(2), 30);
                    break;
                case "Lookup":
                    DxfBlockLinearParameter width = Width(block);
                    block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
                    block.AddLookupParameter(
                        "Size",
                        "Panel size",
                        new GeoPoint3(50, -20, 0),
                        new[] { DxfBlockLookupInput.Distance(width) },
                        new[] { new DxfBlockLookupRow("Small", 100), new DxfBlockLookupRow("Large", 200) }
                    );
                    break;
                case "Alignment":
                    block.AddAlignmentParameter(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
                    break;
                case "BasePoint":
                    block.AddBasePointParameter(new GeoPoint3(0, 0, 0));
                    block.AddMoveAction(block.AddPointParameter("Hook", null, new GeoPoint3(50, 20, 0)), acted.Take(1));
                    break;
                default:
                    throw new ArgumentException(kind);
            }

            return db;
        }

        [Theory]
        [MemberData(nameof(Kinds))]
        public void ABlockOfOneKindOfParameterAndAction_KeepsItsGraphAndEveryObjectItOwns(string kind, string[] types)
        {
            string text = Of(kind).ToText();
            List<DwgAnswerKey.Record> before = Records(text);

            // Every object of the graph, whatever its kind, comes back: the graph, its expressions and the data it keeps.
            string[] owned = before
                .Where(r => r.Type == "ACAD_EVALUATION_GRAPH" || r.Type.StartsWith("BLOCK", StringComparison.Ordinal))
                .Where(r => r.Type != "BLOCK" && r.Type != "BLOCK_RECORD" && r.Type != "BLOCKS")
                .Select(r => r.Type)
                .Distinct()
                .ToArray();

            Assert.All(types, type => Assert.Contains(type, owned));
            Assert.True(KeptThroughDwg(text, owned) >= owned.Length);

            byte[] dwg = DwgWriter.ToDwg(text, "AC1032", out IDictionary<string, int> leftOut);
            Assert.DoesNotContain(leftOut.Keys, type => owned.Contains(type));
            Assert.NotEmpty(dwg);
        }

        [Fact]
        public void ABlockWhoseGraphNamesAnExpressionTheWriterDoesNotKnow_IsWrittenWithoutTheGraph()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var block = new DxfBlockTableRecord { Name = "GH_DOOR", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0)));
            block.AddFlipParameter("Hand", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0));

            // The flip parameter is one of the kinds written; a parameter of a name no writer knows stands for one that is not.
            string text = db.ToText().Replace("BLOCKFLIPPARAMETER", "BLOCKGHOSTPARAMETER");

            Assert.Contains(Records(text), r => r.Type == "ACAD_EVALUATION_GRAPH");
            Assert.Contains(Records(text), r => r.Type == "BLOCKGHOSTPARAMETER");

            byte[] dwg = DwgWriter.ToDwg(text, "AC1032", out IDictionary<string, int> leftOut);
            List<DwgAnswerKey.Record> after = DwgAnswerKey.Parse(DwgReader.ToDxf(dwg));

            Assert.Contains("ACAD_EVALUATION_GRAPH", leftOut.Keys);
            Assert.Contains("BLOCKGHOSTPARAMETER", leftOut.Keys);
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
        public void TheUnsetValueOfAGripLocation_IsStoredAsTheLargestReal()
        {
            // AutoCAD's DXF holds the largest real (the value of a location not set yet) as 1.797693134862314E+99. The DWG
            // keeps double.MaxValue, which AutoCAD reads as unset: the number the text names is not that one.
            byte[] dwg = DwgWriter.ToDwg(Stretched().ToText(), "AC1032");
            DwgContainer container = DwgContainer.Read(dwg);
            var reader = new DwgObjectReader(
                container.Objects,
                DwgClass.ReadAll(container.Classes, container.Version),
                container.Version
            );
            var values = new List<double>();

            foreach (KeyValuePair<ulong, long> entry in container.ObjectMap)
            {
                DwgObject one = reader.Read(entry.Key, entry.Value);

                if (one.Name == "BLOCKGRIPLOCATIONCOMPONENT")
                {
                    values.AddRange(one.Body.Where(g => g.Code == 140).Select(g => (double)g.Value));
                }
            }

            Assert.Contains(double.MaxValue, values);
            Assert.DoesNotContain(values, value => value > 1E50 && value < 1E200);
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
