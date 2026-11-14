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
    /// The objects a dynamic block keeps besides the parameters and actions of its evaluation graph: a block properties table
    /// with its grip, a user parameter, the proxy node of the graph, and the associative network of variables, dependencies and
    /// constraint groups that constraint parameters and user parameters lean on. Their layouts were read from AutoCAD 2021's
    /// own DWG bytes and checked group for group against its DXF of the same drawings (1 789 objects of its Dynamic Blocks
    /// samples); these tests keep what can be kept in code: each object written to a DWG and read back.
    /// </summary>
    public class DwgAssociationTests
    {
        private static readonly string[] Classes =
        {
            "ACDB_DYNAMICBLOCKPROXYNODE|AcDbEvalExpr",
            "BLOCKUSERPARAMETER|AcDbBlockUserParameter",
            "ACDBASSOCNETWORK|AcDbAssocNetwork",
            "ACDBASSOCVARIABLE|AcDbAssocVariable",
            "ACDBASSOCDEPENDENCY|AcDbAssocDependency",
            "ACDBASSOCGEOMDEPENDENCY|AcDbAssocGeomDependency",
            "ACDBASSOCVALUEDEPENDENCY|AcDbAssocValueDependency",
            "ASSOCDIMDEPENDENCYBODY|AcDbAssocDimDependencyBody",
            "ACDBASSOC2DCONSTRAINTGROUP|AcDbAssoc2dConstraintGroup",
        };

        /// <summary>Whether a type of record is one of a dynamic block's graph or network.</summary>
        private static bool Dynamic(string type) =>
            type == "ACAD_EVALUATION_GRAPH"
            || type.StartsWith("BLOCK", StringComparison.Ordinal)
            || type.StartsWith("ACDBASSOC", StringComparison.Ordinal)
            || type.StartsWith("ACDB_DYNAMIC", StringComparison.Ordinal)
            || type.StartsWith("ASSOC", StringComparison.Ordinal);

        /// <summary>The handle of a record, which its key names after the section.</summary>
        private static string Handle(DwgAnswerKey.Record record) => record.Key.Substring(record.Key.IndexOf(':') + 1);

        private static List<DwgAnswerKey.Record> Records(string text) =>
            DwgAnswerKey.Parse(Encoding.UTF8.GetBytes(text));

        private static bool Same(string a, string b) =>
            a.Trim() == b.Trim()
            || (
                double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out double x)
                && double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out double y)
                && Math.Abs(x - y) < 1E-9
            );

        /// <summary>One record of the OBJECTS section from its type, handle and owner and its groups, each <c>code=value</c> between bars.</summary>
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

        /// <summary>
        /// A drawing with a panel and a stretch of it (so a graph, whose handle comes out), the classes of the new types and the
        /// records <paramref name="records"/> gives from the handle of a dictionary added to its OBJECTS; "GRAPH" in them stands
        /// for the handle of the graph.
        /// </summary>
        private static string Drawing(Func<string, string> records, out string graph)
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var block = new DxfBlockTableRecord { Name = "GH_PANEL", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            var edge = new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0));
            block.AppendEntity(edge);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            DxfBlockLinearParameter width = block.AddLinearParameter(
                "Width",
                string.Empty,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                DxfBlockParameterValueSet.Increments(50, 300, 10)
            );
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), new DxfEntity[] { edge });
            string text = db.ToText().Replace("\r\n", "\n");
            List<DwgAnswerKey.Record> all = Records(text);
            string dictionary = Handle(all.First(r => r.Type == "DICTIONARY"));
            graph = Handle(all.First(r => r.Type == "ACAD_EVALUATION_GRAPH"));

            // The classes of the new types go ahead of the end of the CLASSES section, the records ahead of that of OBJECTS.
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

            int at = text.IndexOf(
                "  0\nENDSEC",
                text.IndexOf("CLASSES", StringComparison.Ordinal),
                StringComparison.Ordinal
            );
            text = text.Insert(at, classes.ToString());
            at = text.IndexOf(
                "  0\nENDSEC",
                text.IndexOf("  2\nOBJECTS", StringComparison.Ordinal),
                StringComparison.Ordinal
            );
            return text.Insert(at, records(dictionary).Replace("GRAPH", graph));
        }

        /// <summary>A network owned by the dictionary, whose actions are the handles given as hard owned pointers.</summary>
        private static string Network(string handle, string dictionary, params string[] actions) =>
            Object(
                "ACDBASSOCNETWORK",
                handle,
                dictionary,
                "100=AcDbAssocAction|90=1|90=0|330=0|360=0|90=1|90=0|90=0|100=AcDbAssocNetwork|90=0|90=12|90="
                    + actions.Length.ToString(CultureInfo.InvariantCulture)
                    + string.Concat(actions.Select(a => "|360=" + a))
                    + "|90=0"
            );

        private static string Variable(
            string handle,
            string network,
            string version,
            string value,
            string dependencies
        ) =>
            Object(
                "ACDBASSOCVARIABLE",
                handle,
                network,
                "100=AcDbAssocAction|90="
                    + version
                    + "|90=0|330="
                    + network
                    + "|360=0|90=9|90=0|90=0"
                    + (version == "2" ? "|90=0|90=0|90=0|90=0" : string.Empty)
                    + "|100=AcDbAssocVariable|90=2|1=Size|1=|1=AcDbCalc:1.0|1=|"
                    + value
                    + "|290=0|290=0|"
                    + dependencies
            );

        private static string Dependency(string handle, string owner, string body, string tail) =>
            Object(
                "ACDBASSOC"
                    + (
                        tail.Contains("AcDbAssocGeomDependency") ? "GEOM"
                        : tail.Contains("Value") ? "VALUE"
                        : string.Empty
                    )
                    + "DEPENDENCY",
                handle,
                owner,
                "100=AcDbAssocDependency|90=2|90=0|290=1|290=0|290=1|290=1|90=0|330=0|290=0|330=0|330=0|360="
                    + body
                    + "|90=15"
                    + (tail.Length == 0 ? string.Empty : "|" + tail)
            );

        /// <summary>The records of the handles in the text, each found again in the DXF the DWG reads back as, group for group (the owners apart); how many.</summary>
        private static int Kept(string text, byte[] back, params string[] handles)
        {
            List<DwgAnswerKey.Record> before = Records(text);
            List<DwgAnswerKey.Record> after = DwgAnswerKey.Parse(back);

            foreach (string handle in handles)
            {
                DwgAnswerKey.Record sent = before.Single(r => Handle(r) == handle);
                DwgAnswerKey.Record got = after.SingleOrDefault(r => Handle(r) == handle);
                Assert.True(got != null, sent.Type + " " + handle + " did not come back.");
                List<KeyValuePair<int, string>> a = sent.Groups.Where(g => g.Key != 102 && g.Key != 330).ToList();
                List<KeyValuePair<int, string>> b = got.Groups.Where(g => g.Key != 102 && g.Key != 330).ToList();

                Assert.Equal(sent.Type, got.Type);
                Assert.Equal(a.Select(g => g.Key), b.Select(g => g.Key));
                Assert.All(a.Zip(b, (x, y) => new[] { x.Value, y.Value }), pair =>
                    Assert.True(
                        Same(pair[0], pair[1]),
                        sent.Type + " " + handle + ": " + pair[0] + " is not " + pair[1]
                    )
                );
            }

            return handles.Length;
        }

        [Fact]
        public void ABlockOfTwoLinearParametersAndAPropertiesTable_KeepsItsGraphTheTableAndTheGrip()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var block = new DxfBlockTableRecord { Name = "GH_PLATE", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            var right = new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0));
            var top = new DxfLine(new GeoPoint3(100, 50, 0), new GeoPoint3(0, 50, 0));
            block.AppendEntity(right);
            block.AppendEntity(top);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            DxfBlockLinearParameter width = block.AddLinearParameter(
                "Width",
                string.Empty,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -10,
                null
            );
            block.AddStretchAction(
                width,
                new GeoPoint2(90, -5),
                new GeoPoint2(110, 55),
                new DxfEntity[] { right, top }
            );
            DxfBlockLinearParameter height = block.AddLinearParameter(
                "Height",
                string.Empty,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(0, 50, 0),
                10,
                null
            );
            block.AddStretchAction(height, new GeoPoint2(-5, 40), new GeoPoint2(105, 55), new DxfEntity[] { top });
            block.AddBlockPropertiesTable(
                new GeoPoint3(0, -20, 0),
                new[] { DxfBlockPropertiesTableColumn.Distance(width), DxfBlockPropertiesTableColumn.Distance(height) },
                new[] { new DxfBlockPropertiesTableRow(100.0, 50.0), new DxfBlockPropertiesTableRow(150.0, 75.0) }
            );
            string text = db.ToText().Replace("\r\n", "\n");
            string[] tables = Records(text)
                .Where(r => r.Type == "BLOCKPROPERTIESTABLE" || r.Type == "BLOCKPROPERTIESTABLEGRIP")
                .Select(Handle)
                .ToArray();

            byte[] dwg = DwgWriter.ToDwg(text, "AC1032", out IDictionary<string, int> leftOut);
            byte[] back = DwgReader.ToDxf(dwg);

            Assert.Equal(2, tables.Length);
            Assert.DoesNotContain(leftOut.Keys, Dynamic);
            Assert.Equal(2, Kept(text, back, tables));
            Assert.Contains(DwgAnswerKey.Parse(back), r => r.Type == "ACAD_EVALUATION_GRAPH");
        }

        [Fact]
        public void AUserParameterOfAVariableAndTheNetworkHoldingIt_AreKeptWithTheProxyNode()
        {
            string text = Drawing(
                dictionary =>
                    Object("ACDB_DYNAMICBLOCKPROXYNODE", "F101", "GRAPH", "100=AcDbEvalExpr|90=90|98=33|99=174")
                    + Object(
                        "BLOCKUSERPARAMETER",
                        "F102",
                        "GRAPH",
                        "100=AcDbEvalExpr|90=120|98=33|99=174|100=AcDbBlockElement|300=|98=33|99=174|1071=1"
                            + "|100=AcDbBlockParameter|280=1|281=0"
                            + "|100=AcDbBlock1PtParameter|1010=0.0|1020=0.0|1030=0.0|93=0|170=0|171=0"
                            + "|100=AcDbBlockUserParameter|90=0|330=F104|301=|309=AcDbEvalVariant NULL = -9999|170=5"
                    )
                    + Variable("F104", "F103", "2", "90=4", "90=1|360=F105|90=0")
                    + Dependency("F105", "F104", "0", "100=AcDbAssocValueDependency|90=0|1=|40=19.6")
                    + Network("F103", dictionary, "F104"),
                out string graph
            );
            byte[] dwg = DwgWriter.ToDwg(text, "AC1032", out IDictionary<string, int> leftOut);
            byte[] back = DwgReader.ToDxf(dwg);

            Assert.DoesNotContain(leftOut.Keys, Dynamic);
            Assert.Equal(5, Kept(text, back, "F101", "F102", "F103", "F104", "F105"));
            Assert.Equal(graph, DwgAnswerKey.Parse(back).Single(r => Handle(r) == "F102").Owner);
        }

        [Fact]
        public void ADependencyOnAnEdge_IsKeptWhicheverWayItNamesTheEdge()
        {
            string text = Drawing(
                dictionary =>
                    Dependency(
                        "F201",
                        "F203",
                        "0",
                        "100=AcDbAssocGeomDependency|90=0|290=1|1=AcDbAssocSingleEdgePersSubentId|290=0"
                    )
                    + Dependency(
                        "F202",
                        "F203",
                        "F204",
                        "100=AcDbAssocGeomDependency|90=0|290=1|1=AcDbAssocEdgePersSubentId|90=0|90=18|90=19|290=0"
                    )
                    + Object(
                        "ASSOCDIMDEPENDENCYBODY",
                        "F204",
                        "F202",
                        "100=AcDbAssocDependencyBody|90=1|100=AcDbImpAssocDimDependencyBodyBase|90=1|1=Head=7.18"
                            + "|100=AcDbAssocDimDependencyBody|90=1"
                    )
                    + Variable("F203", "F200", "1", "40=2.5", "90=2|360=F201|90=0|360=F202|90=1")
                    + Network("F200", dictionary, "F203"),
                out _
            );
            byte[] back = DwgReader.ToDxf(DwgWriter.ToDwg(text, "AC1032"));

            Assert.Equal(5, Kept(text, back, "F200", "F201", "F202", "F203", "F204"));
        }

        /// <summary>
        /// A network of a group of five nodes of five classes, a variable, and the dependency the group owns and its first node
        /// leans on.
        /// </summary>
        private static string Group(string dictionary) =>
            Network("F300", dictionary, "F301", "F302")
            + Object(
                "ACDBASSOC2DCONSTRAINTGROUP",
                "F301",
                "F300",
                "100=AcDbAssocAction|90=1|90=0|330=F300|360=0|90=3|90=7|90=0"
                    + "|90=2|70=0|10=0.0|20=0.0|30=0.0|10=1.0|20=0.0|30=0.0|10=0.0|20=1.0|30=0.0"
                    + "|360=0|90=1|360=F303|90=8|90=5"
                    + "|1=AcConstrainedBoundedLine|330=F301|90=1|70=0|90=1|90=3|330=F303|90=0"
                    + "|10=0.0|20=4.0|30=0.0|10=0.0|20=-1.0|30=0.0|290=0|10=0.0|20=4.0|30=0.0|11=36.0|21=4.0|31=0.0"
                    + "|1=AcConstrainedImplicitPoint|330=F301|90=2|70=0|90=1|90=3|330=0|90=0|280=1|90=-1|90=1"
                    + "|1=AcPointCurveConstraint|330=F301|90=3|70=0|90=2|90=2|90=1|90=0|290=1|290=1"
                    + "|1=AcDistanceConstraint|330=F301|90=4|70=4|90=2|90=2|90=2|90=0|290=0|290=1"
                    + "|340=F303|340=F303|280=1|10=1.0|20=0.0|30=0.0"
                    + "|1=AcFixedConstraint|330=F301|90=5|70=0|90=1|90=1|90=0|290=0|290=1"
            )
            + Variable("F302", "F300", "1", "40=0.0625", "90=0")
            + Dependency("F303", "F301", "0", string.Empty);

        [Fact]
        public void AConstraintGroupOfR2018_IsLeftOutWithTheNetworkAndWhatItOwns_NotWrittenAsAFileAutoCADRefuses()
        {
            // AutoCAD erases a constraint group of the version the DWG of R2010 holds when it reads R2018 (its R2018 layout is
            // another one), and then refuses the file for the network that lists it and the dependencies it owned. A group is
            // not written to R2018, so its network and what the network and the group own go with it.
            string text = Drawing(Group, out _);
            byte[] dwg = DwgWriter.ToDwg(text, "AC1032", out IDictionary<string, int> leftOut);
            List<DwgAnswerKey.Record> after = DwgAnswerKey.Parse(DwgReader.ToDxf(dwg));

            Assert.Contains("ACDBASSOC2DCONSTRAINTGROUP", leftOut.Keys);
            Assert.Contains("ACDBASSOCNETWORK", leftOut.Keys);
            Assert.Contains("ACDBASSOCVARIABLE", leftOut.Keys);
            Assert.Contains("ACDBASSOCDEPENDENCY", leftOut.Keys);
            Assert.DoesNotContain(after, r => r.Type.StartsWith("ACDBASSOC", StringComparison.Ordinal));
            Assert.Contains(after, r => r.Type == "ACAD_EVALUATION_GRAPH");
        }

        [Fact]
        public void AConstraintGroupOfR2010_IsReadBackWithEveryNode()
        {
            // The group and its nodes in the layout of AutoCAD 2010's DWG, written to a DWG of R2010 and read back.
            string text = Drawing(Group, out _);
            DwgDrawing drawing = DwgDrawing.Parse(text, null);
            byte[] dwg = DwgFileWriter.Write(drawing, "AC1024", out DwgFileWriter writer);
            byte[] back = DwgReader.ToDxf(dwg);

            Assert.DoesNotContain("ACDBASSOC2DCONSTRAINTGROUP", writer.LeftOut.Keys);
            Assert.Equal(4, Kept(text, back, "F300", "F301", "F302", "F303"));
            Assert.Equal(
                new[]
                {
                    "AcConstrainedBoundedLine",
                    "AcConstrainedImplicitPoint",
                    "AcPointCurveConstraint",
                    "AcDistanceConstraint",
                    "AcFixedConstraint",
                },
                DwgAnswerKey
                    .Parse(back)
                    .Single(r => Handle(r) == "F301")
                    .Groups.Where(g => g.Key == 1)
                    .Select(g => g.Value.Trim())
            );
        }
    }
}
