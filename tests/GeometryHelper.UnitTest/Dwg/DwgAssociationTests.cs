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

        /// <summary>The groups <c>code=value</c> between bars as the lines of a DXF file.</summary>
        private static string Lines(string groups) =>
            string.Concat(
                groups
                    .Split('|')
                    .Select(g => g.Substring(0, g.IndexOf('=')) + "\n" + g.Substring(g.IndexOf('=') + 1) + "\n")
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
            NetworkOf("1", handle, dictionary, actions);

        /// <summary>A network whose action is of <paramref name="version"/> (the version 2 holds four numbers more).</summary>
        private static string NetworkOf(string version, string handle, string dictionary, params string[] actions) =>
            Object(
                "ACDBASSOCNETWORK",
                handle,
                dictionary,
                "100=AcDbAssocAction|90="
                    + version
                    + "|90=0|330=0|360=0|90=1|90=0|90=0"
                    + (version == "2" ? "|90=0|90=0|90=0|90=0" : string.Empty)
                    + "|100=AcDbAssocNetwork|90=0|90=12|90="
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
        private static int Kept(string text, byte[] back, params string[] handles) => Kept(1032, text, back, handles);

        /// <summary>
        /// <see cref="Kept(string, byte[], string[])"/> for a DWG of <paramref name="target"/> (1024 for R2010, 1027 for R2013,
        /// 1032 for R2018): AutoCAD holds a network and a variable at the version 2 of an action, with four numbers more, from
        /// R2013, and at the version 1 before, whichever version the text gives.
        /// </summary>
        private static int Kept(int target, string text, byte[] back, params string[] handles)
        {
            List<DwgAnswerKey.Record> before = Records(text);
            List<DwgAnswerKey.Record> after = DwgAnswerKey.Parse(back);

            foreach (string handle in handles)
            {
                DwgAnswerKey.Record sent = before.Single(r => Handle(r) == handle);
                DwgAnswerKey.Record got = after.SingleOrDefault(r => Handle(r) == handle);
                Assert.True(got != null, sent.Type + " " + handle + " did not come back.");
                List<KeyValuePair<int, string>> a = sent.Groups.Where(g => g.Key != 102 && g.Key != 330).ToList();

                if (sent.Type == "ACDBASSOCNETWORK" || sent.Type == "ACDBASSOCVARIABLE")
                {
                    // After the 100 come the version, the status, the body, then three numbers; the version 2 has four more.
                    string version = target >= 1027 ? "2" : "1";
                    int start = a.FindIndex(g => g.Key == 100) + 1;

                    if (a[start].Value.Trim() != version)
                    {
                        a[start] = new KeyValuePair<int, string>(90, version);

                        if (version == "2")
                        {
                            a.InsertRange(start + 6, Enumerable.Repeat(new KeyValuePair<int, string>(90, "0"), 4));
                        }
                        else
                        {
                            a.RemoveRange(start + 6, 4);
                        }
                    }
                }

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

        /// <summary>A variable that two dependencies on an edge lean on, the second naming it by three numbers, and the network.</summary>
        private static string Edges(string dictionary) =>
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
            + Network("F200", dictionary, "F203");

        /// <summary>The number that opens the action of the object of <paramref name="handle"/> in the DXF a DWG reads back as.</summary>
        private static string ActionVersion(byte[] back, string handle) =>
            DwgAnswerKey.Parse(back).Single(r => Handle(r) == handle).Groups.First(g => g.Key == 90).Value.Trim();

        [Fact]
        public void AVariableAndANetworkOfR2010_AreWrittenToR2018AsTheSecondVersion_AsAutoCADBringsThemUp()
        {
            // The network and the variable of a block saved by AutoCAD 2010 hold the version 1 of an action; AutoCAD 2021 saving
            // the drawing as R2013 or R2018 writes the version 2 (four numbers more), as it does a constraint group.
            string text = Drawing(Edges, out _);
            byte[] back = DwgReader.ToDxf(DwgWriter.ToDwg(text, "AC1032"));

            Assert.Equal(
                "1",
                Records(text).Single(r => Handle(r) == "F203").Groups.First(g => g.Key == 90).Value.Trim()
            );
            Assert.Equal("2", ActionVersion(back, "F200"));
            Assert.Equal("2", ActionVersion(back, "F203"));
            Assert.Equal(5, Kept(text, back, "F200", "F201", "F202", "F203", "F204"));
        }

        [Fact]
        public void AVariableAndANetworkOfR2018_AreWrittenToR2010AsTheFirstVersion_AsAutoCADTakesThemDown()
        {
            // AutoCAD 2021 saving a drawing of dynamic blocks as R2010 or R2007 writes the version 1 of both, without the four
            // numbers, and as R2013 the version 2.
            string text = Drawing(
                dictionary =>
                    Variable("F104", "F103", "2", "90=4", "90=0") + NetworkOf("2", "F103", dictionary, "F104"),
                out _
            );
            DwgDrawing drawing = DwgDrawing.Parse(text, null);
            byte[] to2010 = DwgReader.ToDxf(DwgFileWriter.Write(drawing, "AC1024", out _));
            byte[] to2013 = DwgReader.ToDxf(DwgFileWriter.Write(drawing, "AC1027", out _));

            Assert.Equal("1", ActionVersion(to2010, "F103"));
            Assert.Equal("1", ActionVersion(to2010, "F104"));
            Assert.Equal("2", ActionVersion(to2013, "F103"));
            Assert.Equal("2", ActionVersion(to2013, "F104"));
            Assert.Equal(2, Kept(1024, text, to2010, "F103", "F104"));
            Assert.Equal(2, Kept(1027, text, to2013, "F103", "F104"));
        }

        [Fact]
        public void ADependencyOnAnEdge_IsKeptWhicheverWayItNamesTheEdge()
        {
            string text = Drawing(Edges, out _);
            byte[] back = DwgReader.ToDxf(DwgWriter.ToDwg(text, "AC1032"));

            Assert.Equal(5, Kept(text, back, "F200", "F201", "F202", "F203", "F204"));
        }

        [Fact]
        public void ADependencyOnAnEdgeAndAGroupOfTheSecondVersion_AreKeptInADwgOfR2013_AsAutoCADHoldsThemThere()
        {
            // AutoCAD 2013 names an edge by a flag and a number of a short, and holds a group as AutoCAD 2018 does (its own DWG of
            // R2013 read back to every object of its DXF); the writer of R2013, which AutoCAD does not open for other reasons,
            // writes them so.
            string text = Drawing(dictionary => Group2(dictionary) + Edges(dictionary), out _);
            byte[] dwg = DwgFileWriter.Write(DwgDrawing.Parse(text, null), "AC1027", out DwgFileWriter writer);

            Assert.DoesNotContain(writer.LeftOut.Keys, Dynamic);
            Assert.Equal(
                9,
                Kept(text, DwgReader.ToDxf(dwg), "F200", "F201", "F202", "F203", "F204", "F300", "F301", "F302", "F303")
            );
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

        /// <summary>
        /// The group of <see cref="Group"/> in the layout of AutoCAD 2018's DXF: the action of the version 2 (four numbers more),
        /// two numbers and a flag past the next identifier, a directory of the nodes (class and identifier), and each node with
        /// its identifier and identifiers first and its byte after them.
        /// </summary>
        private static string Group2(string dictionary) =>
            Network("F300", dictionary, "F301", "F302")
            + Object(
                "ACDBASSOC2DCONSTRAINTGROUP",
                "F301",
                "F300",
                "100=AcDbAssocAction|90=2|90=0|330=F300|360=0|90=3|90=7|90=0|90=0|90=0|90=0|90=0"
                    + "|90=2|70=0|10=0.0|20=0.0|30=0.0|10=1.0|20=0.0|30=0.0|10=0.0|20=1.0|30=0.0"
                    + "|360=0|90=1|360=F303|90=8|90=0|90=0|290=0|90=5"
                    + "|1=AcConstrainedBoundedLine|90=1|1=AcConstrainedImplicitPoint|90=2|1=AcPointCurveConstraint|90=3"
                    + "|1=AcDistanceConstraint|90=4|1=AcFixedConstraint|90=5"
                    + "|90=1|90=1|90=3|70=0|330=F303|90=0"
                    + "|10=0.0|20=4.0|30=0.0|10=0.0|20=-1.0|30=0.0|290=0|10=0.0|20=4.0|30=0.0|11=36.0|21=4.0|31=0.0"
                    + "|90=2|90=1|90=3|70=0|330=0|90=0|280=1|90=-1|90=1"
                    + "|90=3|90=2|90=2|90=1|70=0|90=0|290=1|290=1"
                    + "|90=4|90=2|90=2|90=2|70=4|90=0|290=0|290=1|340=F303|340=F303|280=1|10=1.0|20=0.0|30=0.0"
                    + "|90=5|90=1|90=1|70=0|90=0|290=0|290=1"
            )
            + Variable("F302", "F300", "1", "40=0.0625", "90=0")
            + Dependency("F303", "F301", "0", string.Empty);

        [Fact]
        public void AConstraintGroupOfTheFirstVersion_IsWrittenToR2018AsTheSecond_WithTheNetworkAndWhatItOwns()
        {
            // AutoCAD erased a group written to R2018 in the layout of R2010 and then refused the file for the network that listed
            // it. The group is written as AutoCAD 2018 writes it, brought up to the version 2: its DXF, read back, is that of the
            // version 2 with every number of the first version in its place.
            string text = Drawing(Group, out _);
            byte[] dwg = DwgWriter.ToDwg(text, "AC1032", out IDictionary<string, int> leftOut);
            byte[] back = DwgReader.ToDxf(dwg);

            Assert.DoesNotContain(leftOut.Keys, Dynamic);
            Assert.Equal(1, Kept(Drawing(Group2, out _), back, "F301"));
            Assert.Equal(3, Kept(text, back, "F300", "F302", "F303"));
        }

        [Fact]
        public void AConstraintGroupOfTheSecondVersion_IsWrittenToR2018AndReadBackEqual()
        {
            string text = Drawing(Group2, out _);
            byte[] dwg = DwgWriter.ToDwg(text, "AC1032", out IDictionary<string, int> leftOut);

            Assert.DoesNotContain(leftOut.Keys, Dynamic);
            Assert.Equal(4, Kept(text, DwgReader.ToDxf(dwg), "F300", "F301", "F302", "F303"));
        }

        [Fact]
        public void TheNodesOfAGroupOfTheFirstVersion_AreWrittenByIdentifierWithTheClassesInTheOrderTheyAreFirstNamed()
        {
            // Two nodes given the wrong way round are written by identifier, and the table of classes holds each class once, in
            // the order of the first identifier that names it.
            string boundedLine = Lines(
                "1=AcConstrainedBoundedLine|330=F301|90=1|70=0|90=1|90=3|330=F303|90=0"
                    + "|10=0.0|20=4.0|30=0.0|10=0.0|20=-1.0|30=0.0|290=0|10=0.0|20=4.0|30=0.0|11=36.0|21=4.0|31=0.0"
            );
            string implicitPoint = Lines(
                "1=AcConstrainedImplicitPoint|330=F301|90=2|70=0|90=1|90=3|330=0|90=0|280=1|90=-1|90=1"
            );
            string text = Drawing(Group, out _).Replace(boundedLine + implicitPoint, implicitPoint + boundedLine);
            List<DwgAnswerKey.Record> after = DwgAnswerKey.Parse(
                DwgReader.ToDxf(DwgWriter.ToDwg(text, "AC1032", out IDictionary<string, int> leftOut))
            );

            Assert.NotEqual(Drawing(Group, out _), text);
            Assert.DoesNotContain(leftOut.Keys, Dynamic);
            Assert.Equal(1, Kept(Drawing(Group2, out _), DwgReader.ToDxf(DwgWriter.ToDwg(text, "AC1032")), "F301"));
            Assert.Equal(
                new[]
                {
                    "AcConstrainedBoundedLine",
                    "AcConstrainedImplicitPoint",
                    "AcPointCurveConstraint",
                    "AcDistanceConstraint",
                    "AcFixedConstraint",
                },
                after.Single(r => Handle(r) == "F301").Groups.Where(g => g.Key == 1).Select(g => g.Value.Trim())
            );
        }

        [Fact]
        public void ACircleAndAnArcOfTheFirstVersion_GetTheFourthRealOfTheSecond()
        {
            // The circles and arcs of the version 2 hold a fourth real after their three, which is 0 in every one of AutoCAD's
            // samples; the version 1 has no such real, and the node is written with a 0.
            string text = Drawing(Group, out _)
                .Replace(
                    Lines(
                        "1=AcConstrainedBoundedLine|330=F301|90=1|70=0|90=1|90=3|330=F303|90=0"
                            + "|10=0.0|20=4.0|30=0.0|10=0.0|20=-1.0|30=0.0|290=0|10=0.0|20=4.0|30=0.0|11=36.0|21=4.0|31=0.0"
                    ),
                    Lines(
                        "1=AcConstrainedCircle|330=F301|90=1|70=0|90=1|90=3|330=F303|90=0"
                            + "|10=1.0|20=2.0|30=0.0|10=0.0|20=0.0|30=1.0|10=1.0|20=0.0|30=0.0|40=25.4|40=0.0|40=6.28"
                    )
                );
            DwgAnswerKey.Record group = DwgAnswerKey
                .Parse(DwgReader.ToDxf(DwgWriter.ToDwg(text, "AC1032")))
                .Single(r => Handle(r) == "F301");
            List<string> reals = group.Groups.Where(g => g.Key == 40).Select(g => g.Value.Trim()).ToList();

            Assert.Contains(group.Groups, g => g.Key == 1 && g.Value.Trim() == "AcConstrainedCircle");
            Assert.Equal(new[] { "25.4", "0.0", "6.28", "0.0" }, reals);
        }

        [Fact]
        public void AConstraintGroupOfTheSecondVersion_IsWrittenToADwgOfR2010AsTheFirst_WithTheNetworkThatListsIt()
        {
            // The layouts of R2010 and R2018 differ. AutoCAD 2021 saves a drawing of R2018 as R2010 with the group in the first
            // layout, its action of version 1 and its nodes by identifier with their class as a text (its DXF of the two, the
            // second of 7 fewer groups on the smallest group of Architectural - Metric, gives the first): the group goes into R2010
            // as that, with its network, variable and dependency, and reads back as the group of the first version.
            string text = Drawing(Group2, out _);
            DwgDrawing drawing = DwgDrawing.Parse(text, null);
            byte[] dwg = DwgFileWriter.Write(drawing, "AC1024", out DwgFileWriter writer);
            byte[] back = DwgReader.ToDxf(dwg);

            Assert.DoesNotContain(writer.LeftOut.Keys, Dynamic);
            Assert.Equal(1, Kept(1024, Drawing(Group, out _), back, "F301"));
            Assert.Equal(3, Kept(1024, text, back, "F300", "F302", "F303"));
        }

        [Fact]
        public void TheNodesOfAConstraintGroupOfTheSecondVersion_AreWrittenToR2010ByIdentifier()
        {
            // Two nodes in the directory the wrong way round, with their bodies, come out by identifier.
            string boundedLine = Lines("1=AcConstrainedBoundedLine|90=1");
            string implicitPoint = Lines("1=AcConstrainedImplicitPoint|90=2");
            string boundedLineBody = Lines(
                "90=1|90=1|90=3|70=0|330=F303|90=0"
                    + "|10=0.0|20=4.0|30=0.0|10=0.0|20=-1.0|30=0.0|290=0|10=0.0|20=4.0|30=0.0|11=36.0|21=4.0|31=0.0"
            );
            string implicitPointBody = Lines("90=2|90=1|90=3|70=0|330=0|90=0|280=1|90=-1|90=1");
            string text = Drawing(Group2, out _)
                .Replace(boundedLine + implicitPoint, implicitPoint + boundedLine)
                .Replace(boundedLineBody + implicitPointBody, implicitPointBody + boundedLineBody);
            DwgDrawing drawing = DwgDrawing.Parse(text, null);
            byte[] back = DwgReader.ToDxf(DwgFileWriter.Write(drawing, "AC1024", out DwgFileWriter writer));

            Assert.NotEqual(Drawing(Group2, out _), text);
            Assert.DoesNotContain(writer.LeftOut.Keys, Dynamic);
            Assert.Equal(1, Kept(1024, Drawing(Group, out _), back, "F301"));
        }

        [Fact]
        public void ACircleOfAConstraintGroupOfTheSecondVersion_LosesItsFourthRealInADwgOfR2010()
        {
            // The first layout holds three reals after the points of a circle and an arc where the second holds four (the fourth
            // 0 in every sample of AutoCAD's).
            string text = Drawing(Group2, out _)
                .Replace(Lines("1=AcConstrainedBoundedLine|90=1"), Lines("1=AcConstrainedCircle|90=1"))
                .Replace(
                    Lines(
                        "90=1|90=1|90=3|70=0|330=F303|90=0"
                            + "|10=0.0|20=4.0|30=0.0|10=0.0|20=-1.0|30=0.0|290=0|10=0.0|20=4.0|30=0.0|11=36.0|21=4.0|31=0.0"
                    ),
                    Lines(
                        "90=1|90=1|90=3|70=0|330=F303|90=0"
                            + "|10=1.0|20=2.0|30=0.0|10=0.0|20=0.0|30=1.0|10=1.0|20=0.0|30=0.0|40=25.4|40=0.0|40=6.28|40=0.0"
                    )
                );
            DwgDrawing drawing = DwgDrawing.Parse(text, null);
            byte[] dwg = DwgFileWriter.Write(drawing, "AC1024", out DwgFileWriter writer);
            DwgAnswerKey.Record group = DwgAnswerKey.Parse(DwgReader.ToDxf(dwg)).Single(r => Handle(r) == "F301");

            Assert.DoesNotContain(writer.LeftOut.Keys, Dynamic);
            Assert.Contains(group.Groups, g => g.Key == 1 && g.Value.Trim() == "AcConstrainedCircle");
            Assert.Equal(
                new[] { "25.4", "0.0", "6.28" },
                group.Groups.Where(g => g.Key == 40).Select(g => g.Value.Trim())
            );
        }

        [Fact]
        public void AConstraintGroupOfTheSecondVersion_IsKeptInADwgOfR2007_WhereAnXRecordHoldsTheVertexIdentifiers()
        {
            string text = Drawing(
                dictionary =>
                    Group2(dictionary)
                    + Object(
                        "XRECORD",
                        "F3F0",
                        "F300",
                        "100=AcDbXrecord|280=1|102=VERTEXIDENTIFIER|310=0100000015000000"
                    ),
                out _
            );
            DwgDrawing drawing = DwgDrawing.Parse(text, null);
            byte[] back = DwgReader.ToDxf(DwgFileWriter.Write(drawing, "AC1021", out DwgFileWriter writer));

            Assert.DoesNotContain("ACDBASSOC2DCONSTRAINTGROUP", writer.LeftOut.Keys);
            Assert.Equal(1, Kept(1021, Drawing(Group, out _), back, "F301"));
        }

        [Fact]
        public void AConstraintGroup_IsLeftOutOfADwgOfR2007_WhereNoXRecordHoldsTheVertexIdentifiers()
        {
            // A file of R2007 has no identifier of a vertex in a LWPOLYLINE: AutoCAD 2007 keeps them in an extended record, and
            // AUDIT of 2021 finds an error in each group whose polylines have none (34 in the 5 groups of a sample drawing
            // written from R2018, 0 with the groups left out).
            DwgDrawing drawing = DwgDrawing.Parse(Drawing(Group, out _), null);
            DwgFileWriter.Write(drawing, "AC1021", out DwgFileWriter writer);

            Assert.Contains("ACDBASSOC2DCONSTRAINTGROUP", writer.LeftOut.Keys);
        }

        [Fact]
        public void AConstraintGroup_IsKeptInADwgOfR2007_WhereAnXRecordHoldsTheVertexIdentifiers()
        {
            // The record AutoCAD 2007 writes with the identifiers of a polyline: VERTEXIDENTIFIER and the count and the
            // identifiers as bytes.
            string text = Drawing(
                dictionary =>
                    Group(dictionary)
                    + Object(
                        "XRECORD",
                        "F3F0",
                        "F300",
                        "100=AcDbXrecord|280=1|102=VERTEXIDENTIFIER|310=0100000015000000"
                    ),
                out _
            );
            DwgDrawing drawing = DwgDrawing.Parse(text, null);
            DwgFileWriter.Write(drawing, "AC1021", out DwgFileWriter writer);

            Assert.DoesNotContain("ACDBASSOC2DCONSTRAINTGROUP", writer.LeftOut.Keys);
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
            Assert.Equal(4, Kept(1024, text, back, "F300", "F301", "F302", "F303"));
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

        #region An ellipse, a bounded ellipse and a concentric constraint

        /// <summary>A node holding the same numbers whichever layout, and the same groups past its byte.</summary>
        private const string EllipseFields = "330=F303|90=0|10=700.0|20=0.0|30=0.0|11=20.0|21=0.0|31=0.0|40=0.5";

        private const string BoundedEllipseFields =
            "330=0|90=0|10=800.0|20=0.0|30=0.0|11=20.0|21=5.0|31=0.0|40=0.4"
            + "|10=814.5|20=8.8|30=0.0|11=795.3|21=-9.2|31=0.0";

        private const string ConcentricFields = "90=0|290=0|290=1";

        /// <summary>
        /// The group of <see cref="Group2"/> with an ellipse (6), a bounded ellipse (7) and a concentric constraint of the two
        /// (8) after its five nodes: the layouts of AutoCAD 2021's own DXF of a group holding them, with numbers made up here.
        /// </summary>
        private static string GroupOfEllipses2(string dictionary) =>
            Group2(dictionary)
                .Replace(Lines("90=8|90=0|90=0|290=0|90=5"), Lines("90=12|90=0|90=0|290=0|90=8"))
                .Replace(
                    Lines("1=AcDistanceConstraint|90=4|1=AcFixedConstraint|90=5"),
                    Lines(
                        "1=AcDistanceConstraint|90=4|1=AcFixedConstraint|90=5|1=AcConstrainedEllipse|90=6"
                            + "|1=AcConstrainedBoundedEllipse|90=7|1=AcConcentricConstraint|90=8"
                    )
                )
                .Replace(
                    Lines("90=5|90=1|90=1|70=0|90=0|290=0|290=1"),
                    Lines(
                        "90=5|90=1|90=1|70=0|90=0|290=0|290=1"
                            + "|90=6|90=1|90=8|70=0|"
                            + EllipseFields
                            + "|90=7|90=1|90=8|70=0|"
                            + BoundedEllipseFields
                            + "|90=8|90=2|90=6|90=7|70=4|"
                            + ConcentricFields
                    )
                );

        /// <summary>The same group in the first layout, by identifier with the class of each node and its group.</summary>
        private static string GroupOfEllipses1(string dictionary) =>
            Group(dictionary)
                .Replace(Lines("90=8|90=5"), Lines("90=12|90=8"))
                .Replace(
                    Lines("1=AcFixedConstraint|330=F301|90=5|70=0|90=1|90=1|90=0|290=0|290=1"),
                    Lines(
                        "1=AcFixedConstraint|330=F301|90=5|70=0|90=1|90=1|90=0|290=0|290=1"
                            + "|1=AcConstrainedEllipse|330=F301|90=6|70=0|90=1|90=8|"
                            + EllipseFields
                            + "|1=AcConstrainedBoundedEllipse|330=F301|90=7|70=0|90=1|90=8|"
                            + BoundedEllipseFields
                            + "|1=AcConcentricConstraint|330=F301|90=8|70=4|90=2|90=6|90=7|"
                            + ConcentricFields
                    )
                );

        [Fact]
        public void ADwgOfR2010_IsReadAsTheDxfOfR2010_AndSavedAsR2018InTheSecondLayout()
        {
            // The text a DWG of R2010 reads as names R2010 (AC1024) and holds the first layout, so that it matches its header; saved
            // as R2018 it holds the second under the header of R2018, and saved as R2010 again it stays as it is.
            string text = Drawing(Group, out _);
            byte[] dwg = DwgFileWriter.Write(DwgDrawing.Parse(text, null), "AC1024", out _);
            byte[] dxf = DwgReader.ToDxf(dwg);

            Assert.Equal("AC1024", VersionNamed(dxf));
            Assert.Equal(4, Kept(1024, text, dxf, "F300", "F301", "F302", "F303"));

            using (var stream = new System.IO.MemoryStream(dwg, false))
            using (var up = new System.IO.MemoryStream())
            using (var same = new System.IO.MemoryStream())
            {
                DxfDatabase db = DwgReader.Open(stream);
                db.Save(up, "AC1032");
                db.Save(same, "AC1024");

                Assert.Equal("AC1032", VersionNamed(up.ToArray()));
                Assert.Equal(1, Kept(Drawing(Group2, out _), up.ToArray(), "F301"));
                Assert.Equal("AC1024", VersionNamed(same.ToArray()));
                Assert.Equal(4, Kept(1024, text, same.ToArray(), "F300", "F301", "F302", "F303"));
            }
        }

        /// <summary>The version a DXF names in its header (the value after <c>$ACADVER</c>).</summary>
        private static string VersionNamed(byte[] dxf)
        {
            string[] lines = Encoding.UTF8.GetString(dxf).Replace("\r\n", "\n").Split('\n');
            return lines[Array.IndexOf(lines, "$ACADVER") + 2].Trim();
        }

        [Fact]
        public void AnEllipseABoundedEllipseAndAConcentricConstraint_AreWrittenToR2018AndReadBackEqual()
        {
            string text = Drawing(GroupOfEllipses2, out _);
            byte[] dwg = DwgWriter.ToDwg(text, "AC1032", out IDictionary<string, int> leftOut);

            Assert.DoesNotContain(leftOut.Keys, Dynamic);
            Assert.Equal(4, Kept(text, DwgReader.ToDxf(dwg), "F300", "F301", "F302", "F303"));
        }

        [Fact]
        public void AnEllipseABoundedEllipseAndAConcentricConstraint_AreWrittenToR2013AndReadBackEqual()
        {
            string text = Drawing(GroupOfEllipses2, out _);
            byte[] dwg = DwgFileWriter.Write(DwgDrawing.Parse(text, null), "AC1027", out DwgFileWriter writer);

            Assert.DoesNotContain(writer.LeftOut.Keys, Dynamic);
            Assert.Equal(4, Kept(1027, text, DwgReader.ToDxf(dwg), "F300", "F301", "F302", "F303"));
        }

        [Fact]
        public void AnEllipseABoundedEllipseAndAConcentricConstraint_AreWrittenToR2010AndReadBackEqual()
        {
            // The layout of AutoCAD 2010 holds them as the second does: the same groups past the byte of each node.
            string text = Drawing(GroupOfEllipses1, out _);
            byte[] dwg = DwgFileWriter.Write(DwgDrawing.Parse(text, null), "AC1024", out DwgFileWriter writer);

            Assert.DoesNotContain(writer.LeftOut.Keys, Dynamic);
            Assert.Equal(4, Kept(1024, text, DwgReader.ToDxf(dwg), "F300", "F301", "F302", "F303"));
        }

        [Fact]
        public void AGroupOfTheFirstVersionHoldingAnEllipse_IsWrittenToR2018AsTheSecond()
        {
            string text = Drawing(GroupOfEllipses1, out _);
            byte[] back = DwgReader.ToDxf(DwgWriter.ToDwg(text, "AC1032"));

            Assert.Equal(1, Kept(Drawing(GroupOfEllipses2, out _), back, "F301"));
        }

        #endregion
    }
}
