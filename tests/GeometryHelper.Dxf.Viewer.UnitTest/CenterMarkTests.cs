using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// CENTERMARK as AutoCAD 2021's console ran it (accoreconsole fed through standard input): the picture, the variables that
    /// size it, the associative objects written with it, and how it follows the circle it is drawn from.
    /// </summary>
    public class CenterMarkTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        /// <summary>A drawing of R2013 with a circle of radius 20 about (50, 50), and the circle's handle.</summary>
        private static CadDocument CircleOfTwenty(out string circle)
        {
            CadDocument document = Blank();
            Start(document, "CIRCLE", P(50, 50), "20");
            circle = All<DxfCircle>(document.ModelSpace).Single().Handle;
            return document;
        }

        private static DxfBlockReference TheInsert(CadDocument document) =>
            All<DxfBlockReference>(document.ModelSpace).Single();

        private static List<DxfLine> LinesOf(DxfBlockReference insert) =>
            insert.BlockTableRecord.OfType<DxfLine>().ToList();

        private static void HasLine(IReadOnlyList<DxfLine> lines, double x0, double y0, double x1, double y1)
        {
            Assert.Contains(lines, l =>
                Math.Abs(l.StartPoint.X - x0) < 1E-9
                && Math.Abs(l.StartPoint.Y - y0) < 1E-9
                && Math.Abs(l.EndPoint.X - x1) < 1E-9
                && Math.Abs(l.EndPoint.Y - y1) < 1E-9
            );
        }

        [Fact]
        public void ACenterMarkOnACircle_IsAnInsertOfAnAnonymousBlock_WithACrossAndFourLines()
        {
            CadDocument document = CircleOfTwenty(out _);
            CadCommandSession session = Start(document, "CENTERMARK");

            Assert.Equal("Select circle or arc to add center mark:", session.PromptText);
            Run(session, P(70, 50));
            Assert.Equal("Select circle or arc to add center mark:", session.PromptText);
            Run(session, Enter);
            Assert.False(session.IsRunning);

            DxfBlockReference insert = TheInsert(document);
            Assert.StartsWith("*U", insert.Name, StringComparison.Ordinal);
            Near(new GeoPoint3(50, 50, 0), insert.Position);
            Assert.Equal("CENTER2", insert.Linetype, ignoreCase: true);
            Assert.Equal(1.0, insert.ScaleFactors.X);
            Assert.Equal(0.0, insert.Rotation);

            // AutoCAD 2021: a cross 4 long (0.1x of the 40 across), then lines from the gap 2 past it (0.05x) to 0.12 past the rim.
            List<DxfLine> lines = LinesOf(insert);
            Assert.Equal(6, lines.Count);
            HasLine(lines, -2, 0, 2, 0);
            HasLine(lines, 0, -2, 0, 2);
            HasLine(lines, -20.12, 0, -4, 0);
            HasLine(lines, 4, 0, 20.12, 0);
            HasLine(lines, 0, -20.12, 0, -4);
            HasLine(lines, 0, 4, 0, 20.12);

            foreach (DxfLine line in lines)
            {
                Assert.True(line.Color.IsByBlock);
                Assert.True(line.Transparency.IsByBlock);
                Assert.True(line.Visible);
            }

            Assert.Equal("Continuous", lines[0].Linetype, ignoreCase: true);
            Assert.Equal("Continuous", lines[1].Linetype, ignoreCase: true);
            Assert.All(lines.Skip(2), l => Assert.Equal("CENTER2", l.Linetype, ignoreCase: true));
            Assert.True(document.Database.LinetypeTable.Has("CENTER2"));
            AssertReopens(document);
        }

        [Fact]
        public void TheSizesOfAMark_FollowTheRadius_AsTimesTheDiameterAndTheOvershoot()
        {
            foreach (double radius in new[] { 10.0, 50.0 })
            {
                CadDocument document = Blank();
                Start(
                    document,
                    "CIRCLE",
                    P(0, 0),
                    radius.ToString(CultureInfo.InvariantCulture),
                    "CENTERMARK",
                    P(radius, 0),
                    Enter
                );
                List<DxfLine> lines = LinesOf(TheInsert(document));
                double half = 0.1 * radius;
                HasLine(lines, -half, 0, half, 0);
                HasLine(lines, 2 * half, 0, radius + 0.12, 0);
                HasLine(lines, 0, -radius - 0.12, 0, -2 * half);
            }
        }

        [Fact]
        public void AMarkOnAnArc_IsDrawnAsOnItsCircle()
        {
            CadDocument document = Blank();
            document.Database.ModelSpace.AppendEntity(new DxfArc(new GeoPoint3(300, 50, 0), 30.0, 0.0, Math.PI / 2.0));
            DxfArc arc = All<DxfArc>(document.ModelSpace).Single();
            Start(document, "CENTERMARK", P(330, 50), Enter);

            DxfBlockReference insert = TheInsert(document);
            Near(arc.Center, insert.Position);
            HasLine(LinesOf(insert), 6, 0, 30.12, 0);
            HasLine(LinesOf(insert), -3, 0, 3, 0);
        }

        [Fact]
        public void AnObjectThatIsNoCircleOrArc_IsRefused_AndTheMarkAskedAgain()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "CENTERMARK", P(50, 0));

            Assert.Contains("Object selected is not a circle or arc.", session.History);
            Assert.Equal("Select circle or arc to add center mark:", session.PromptText);
            Assert.DoesNotContain(All<DxfBlockReference>(document.ModelSpace), i =>
                i.Name.StartsWith("*U", StringComparison.Ordinal)
            );
        }

        [Fact]
        public void AMark_IsOneUndoStep_AndTheVariablesSizeIt()
        {
            CadDocument document = CircleOfTwenty(out _);
            document.Database.CenterExe = 1.5;
            document.Database.CenterCrossSize = "6";
            document.Database.CenterCrossGap = "2";
            Start(document, "CENTERMARK", P(70, 50), Enter);

            List<DxfLine> lines = LinesOf(TheInsert(document));
            HasLine(lines, -3, 0, 3, 0);
            HasLine(lines, -21.5, 0, -5, 0);
            HasLine(lines, 5, 0, 21.5, 0);

            Assert.Equal("CENTERMARK", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Empty(All<DxfBlockReference>(document.ModelSpace));
        }

        [Fact]
        public void CentermarkexeNought_HidesTheLinesThatRunOutToTheRim_LikeAutoCad()
        {
            CadDocument document = CircleOfTwenty(out _);
            document.Database.CenterMarkExe = false;
            Start(document, "CENTERMARK", P(70, 50), Enter);

            List<DxfLine> lines = LinesOf(TheInsert(document));
            Assert.Equal(6, lines.Count);
            Assert.All(lines.Take(2), l => Assert.True(l.Visible));
            Assert.All(lines.Skip(2), l => Assert.False(l.Visible));
        }

        [Fact]
        public void TheLinetypeScaleLayerAndLinetypeOfTheVariables_AreTheMarks()
        {
            CadDocument document = CircleOfTwenty(out _);
            document.Database.CenterLtScale = 2.0;
            document.Database.CenterLtype = "Continuous";
            document.Database.CenterLayer = "CL";
            Start(document, "CENTERMARK", P(70, 50), Enter);

            DxfBlockReference insert = TheInsert(document);
            Assert.Equal("CL", insert.Layer);
            Assert.True(document.Database.LayerTable.Has("CL"));
            Assert.Equal("Continuous", insert.Linetype, ignoreCase: true);
            Assert.Equal(2.0, insert.LinetypeScale);
            Assert.All(LinesOf(insert), l => Assert.Equal("CL", l.Layer));
            Assert.All(LinesOf(insert), l => Assert.Equal(2.0, l.LinetypeScale));
            Assert.All(LinesOf(insert), l => Assert.Equal("Continuous", l.Linetype, ignoreCase: true));
        }

        [Fact]
        public void TheMarkTakesTheCurrentLayerColourAndLineweight_OfTheDrawing()
        {
            CadDocument document = CircleOfTwenty(out _);
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "ZZ" });
            document.Database.Clayer = "ZZ";
            document.Database.Cecolor = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1);
            document.Database.Celweight = (DxfLineWeight)50;
            Start(document, "CENTERMARK", P(70, 50), Enter);

            DxfBlockReference insert = TheInsert(document);
            Assert.Equal("ZZ", insert.Layer);
            Assert.Equal(1, insert.Color.ColorIndex);
            Assert.Equal((DxfLineWeight)50, insert.LineWeight);
            Assert.All(LinesOf(insert), l => Assert.Equal("ZZ", l.Layer));
            Assert.All(LinesOf(insert), l => Assert.Equal((DxfLineWeight)50, l.LineWeight));
            Assert.All(LinesOf(insert), l => Assert.True(l.Color.IsByBlock));
        }

        [Fact]
        public void DimcenterDrawsItsLinesInTheCurrentLayerColourLinetypeAndLineweight_NotInDimclrdOrDimltype()
        {
            // AutoCAD 2021, DIMCEN -5, DIMCLRD red, DIMLTYPE DASHED, DIMLWD 0.5, the current layer ZZ, colour 3, Continuous, 0.25.
            CadDocument document = CircleOfTwenty(out _);
            document.Database.Dimcen = -5.0;
            document.Database.Dimclrd = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1);
            document.Database.Dimlwd = (DxfLineWeight)50;
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "ZZ" });
            document.Database.Clayer = "ZZ";
            document.Database.Cecolor = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3);
            document.Database.Celweight = (DxfLineWeight)25;
            Start(document, "DIMCENTER", P(70, 50));

            List<DxfLine> lines = All<DxfLine>(document.ModelSpace);
            Assert.Equal(6, lines.Count);
            Assert.All(lines, l => Assert.Equal("ZZ", l.Layer));
            Assert.All(lines, l => Assert.Equal(3, l.Color.ColorIndex));
            Assert.All(lines, l => Assert.Equal((DxfLineWeight)25, l.LineWeight));
            Assert.All(lines, l => Assert.Equal("ByLayer", l.Linetype, ignoreCase: true));
        }

        [Fact]
        public void AMarkFollowsItsCircleMovedAndScaled_AndKeepsItsOwnSizes()
        {
            CadDocument document = CircleOfTwenty(out string circle);
            Start(document, "CENTERMARK", P(70, 50), Enter);
            DxfBlockReference insert = TheInsert(document);

            document.Selection.Set(new[] { circle });
            Start(document, "MOVE", P(0, 0), P(0, 100));
            Near(new GeoPoint3(50, 150, 0), TheInsert(document).Position);

            // Scaled about its own centre to a radius of 40: the lines and the cross grow with it; the insert stays unscaled.
            document.Selection.Set(new[] { circle });
            Start(document, "SCALE", P(50, 150), "2");
            insert = TheInsert(document);
            Near(new GeoPoint3(50, 150, 0), insert.Position);
            Assert.Equal(1.0, insert.ScaleFactors.X);
            List<DxfLine> lines = LinesOf(insert);
            HasLine(lines, -4, 0, 4, 0);
            HasLine(lines, 8, 0, 40.12, 0);
            Assert.True(document.Undo());
            HasLine(LinesOf(TheInsert(document)), 4, 0, 20.12, 0);
        }

        [Fact]
        public void AMarkMovedOnItsOwn_NoLongerFollowsTheCircle_AndItsActionIsTakenOut()
        {
            CadDocument document = CircleOfTwenty(out string circle);
            Start(document, "CENTERMARK", P(70, 50), Enter);
            string insert = TheInsert(document).Handle;
            Assert.Contains(Objects(document), r => r.Type == "ACDBASSOCACTION");

            document.Selection.Set(new[] { insert });
            Start(document, "MOVE", P(0, 0), P(0, 30));
            Near(new GeoPoint3(50, 80, 0), TheInsert(document).Position);
            Assert.DoesNotContain(Objects(document), r => r.Type == "ACDBASSOCACTION");
            Assert.DoesNotContain(Objects(document), r => r.Type == "ACDBASSOCNETWORK");
            Assert.Empty(document.FindEntity(circle).GetPersistentReactorIds());

            document.Selection.Set(new[] { circle });
            Start(document, "MOVE", P(0, 0), P(30, 0));
            Near(new GeoPoint3(50, 80, 0), TheInsert(document).Position);
            AssertReopens(document);
        }

        [Fact]
        public void AMarkMovedWithItsCircle_StaysTiedToIt()
        {
            CadDocument document = CircleOfTwenty(out string circle);
            Start(document, "CENTERMARK", P(70, 50), Enter);
            string insert = TheInsert(document).Handle;

            document.Selection.Set(new[] { circle, insert });
            Start(document, "MOVE", P(0, 0), P(0, 100));
            Near(new GeoPoint3(50, 150, 0), TheInsert(document).Position);
            Assert.Contains(Objects(document), r => r.Type == "ACDBASSOCACTION");

            document.Selection.Set(new[] { circle });
            Start(document, "MOVE", P(0, 0), P(10, 0));
            Near(new GeoPoint3(60, 150, 0), TheInsert(document).Position);
        }

        [Fact]
        public void ACircleErased_LeavesItsMark_AndAMarkErased_TakesItsActionAndNetworksOut()
        {
            CadDocument document = CircleOfTwenty(out string circle);
            Start(document, "CENTERMARK", P(70, 50), Enter);
            string insert = TheInsert(document).Handle;

            document.Selection.Set(new[] { circle });
            Start(document, "ERASE");
            Assert.Single(All<DxfBlockReference>(document.ModelSpace));
            Assert.Contains(Objects(document), r => r.Type == "ACDBASSOCACTION");

            document.Selection.Set(new[] { insert });
            Start(document, "ERASE");
            Assert.Empty(All<DxfBlockReference>(document.ModelSpace));
            Assert.DoesNotContain(Objects(document), r => r.Type.StartsWith("ACDBASSOC", StringComparison.Ordinal));
            Assert.DoesNotContain(Objects(document), r => r.Type == "AcDbCenterMarkActionBody");
            AssertReopens(document);
        }

        [Fact]
        public void TwoMarksOnOneCircle_BothFollowIt_ChainedAmongItsDependencies()
        {
            CadDocument document = CircleOfTwenty(out string circle);
            Start(document, "CENTERMARK", P(70, 50), Enter);
            Start(document, "CENTERMARK", P(64.142, 64.142), Enter);
            Assert.Equal(2, All<DxfBlockReference>(document.ModelSpace).Count);

            List<Rec> geoms = Objects(document).Where(r => r.Type == "ACDBASSOCGEOMDEPENDENCY").ToList();
            Assert.Equal(2, geoms.Count);
            Assert.Equal(geoms[1].Handle, geoms[0].Groups.Where(g => g.Key == 330).Skip(3).First().Value);

            document.Selection.Set(new[] { circle });
            Start(document, "MOVE", P(0, 0), P(0, 10));
            Assert.All(All<DxfBlockReference>(document.ModelSpace), i => Near(new GeoPoint3(50, 60, 0), i.Position));
        }

        [Fact]
        public void TheAssociativeObjectsOfAMark_AreAutoCad2021sGroupForGroup()
        {
            CadDocument document = CircleOfTwenty(out string circle);
            Start(document, "CENTERMARK", P(70, 50), Enter);

            List<Rec> records = Objects(document);
            Rec action = records.Single(r => r.Type == "ACDBASSOCACTION");
            Rec body = records.Single(r => r.Type == "AcDbCenterMarkActionBody");
            Rec edge = records.Single(r => r.Type == "ACDBASSOCEDGEACTIONPARAM");
            Rec geom = records.Single(r => r.Type == "ACDBASSOCGEOMDEPENDENCY");
            Rec dependency = records.Single(r => r.Type == "ACDBASSOCDEPENDENCY");
            List<Rec> networks = records.Where(r => r.Type == "ACDBASSOCNETWORK").ToList();
            Assert.Equal(2, networks.Count);
            Rec space = networks.Single(n => n.Groups.Any(g => g.Key == 360 && g.Value == action.Handle));
            Rec top = networks.Single(n => n.Handle != space.Handle);

            var names = new Dictionary<string, string>
            {
                [action.Handle] = "ACTION",
                [body.Handle] = "BODY",
                [edge.Handle] = "EDGE",
                [geom.Handle] = "GEOM",
                [dependency.Handle] = "DEP",
                [space.Handle] = "NET",
                [top.Handle] = "TOP",
                [circle] = "CIRCLE",
                [TheInsert(document).Handle] = "INSERT",
                ["0"] = "0",
            };

            Assert.Equal(
                new[]
                {
                    "5=ACTION",
                    "330=NET",
                    "100=AcDbAssocAction",
                    "90=2",
                    "90=0",
                    "330=NET",
                    "360=BODY",
                    "90=1",
                    "90=2",
                    "90=2",
                    "360=GEOM",
                    "360=DEP",
                    "90=0",
                    "90=1",
                    "360=EDGE",
                    "90=0",
                    "90=16",
                }
                    .Concat(Value("HorizontalStartExtension", "40=1E+60"))
                    .Concat(Value("HorizontalEndExtension", "40=1E+60"))
                    .Concat(Value("VerticalStartExtension", "40=1E+60"))
                    .Concat(Value("VerticalEndExtension", "40=1E+60"))
                    .Concat(Value("HorizontalStartOvershoot", "40=0.12"))
                    .Concat(Value("HorizontalEndOvershoot", "40=0.12"))
                    .Concat(Value("VerticalStartOvershoot", "40=0.12"))
                    .Concat(Value("VerticalEndOvershoot", "40=0.12"))
                    .Concat(Value("CrossHairLengthExpression", "1=0.1x"))
                    .Concat(Value("CrossHairGapExpression", "1=0.05x"))
                    .Concat(Value("CenterDepId", "330=DEP"))
                    .Concat(Value("CachedCircleRadius", "40=20"))
                    .Concat(Value("SubentLineType", "1=CENTER2"))
                    .Concat(Value("SubentLineWeight", "40=-1"))
                    .Concat(Value("SubentLinetypeScale", "40=1"))
                    .Concat(Value("CachedAssociateStatus", "40=1"))
                    .ToArray(),
                Shape(action, names)
            );
            Assert.Equal(
                new[]
                {
                    "5=BODY",
                    "330=ACTION",
                    "100=AcDbAssocActionBody",
                    "90=2",
                    "100=AcDbSmartCenterActionBody",
                    "90=0",
                },
                Shape(body, names)
            );
            Assert.Equal(
                new[]
                {
                    "5=EDGE",
                    "330=ACTION",
                    "100=AcDbAssocActionParam",
                    "90=1",
                    "90=0",
                    "1=InputEdge",
                    "100=AcDbAssocSingleDependencyActionParam",
                    "90=0",
                    "330=GEOM",
                    "100=AcDbAssocEdgeActionParam",
                    "90=0",
                    "330=0",
                    "290=1",
                    "90=11",
                    "10=50",
                    "20=50",
                    "30=0",
                    "10=0",
                    "20=0",
                    "30=1",
                    "10=1",
                    "20=0",
                    "30=0",
                    "40=20",
                    "40=0",
                    "40=6.28319",
                    "40=0",
                },
                Shape(edge, names)
            );
            Assert.Equal(
                new[]
                {
                    "5=GEOM",
                    "330=ACTION",
                    "100=AcDbAssocDependency",
                    "90=2",
                    "90=0",
                    "290=1",
                    "290=0",
                    "290=1",
                    "290=1",
                    "90=0",
                    "330=CIRCLE",
                    "290=0",
                    "330=0",
                    "330=0",
                    "360=0",
                    "90=1",
                    "100=AcDbAssocGeomDependency",
                    "90=0",
                    "290=1",
                    "1=AcDbAssocSingleEdgePersSubentId",
                    "290=0",
                },
                Shape(geom, names)
            );
            Assert.Equal(
                new[]
                {
                    "5=DEP",
                    "330=ACTION",
                    "100=AcDbAssocDependency",
                    "90=2",
                    "90=0",
                    "290=1",
                    "290=1",
                    "290=1",
                    "290=1",
                    "90=0",
                    "330=INSERT",
                    "290=0",
                    "330=0",
                    "330=0",
                    "360=0",
                    "90=2",
                },
                Shape(dependency, names)
            );
            Assert.Equal(
                new[]
                {
                    "5=NET",
                    "100=AcDbAssocAction",
                    "90=2",
                    "90=0",
                    "330=TOP",
                    "360=0",
                    "90=1",
                    "90=0",
                    "90=0",
                    "90=0",
                    "90=0",
                    "90=0",
                    "90=0",
                    "100=AcDbAssocNetwork",
                    "90=0",
                    "90=1",
                    "90=1",
                    "360=ACTION",
                    "90=0",
                },
                Shape(space, names, true)
            );
            Assert.Equal(
                new[]
                {
                    "5=TOP",
                    "100=AcDbAssocAction",
                    "90=2",
                    "90=0",
                    "330=0",
                    "360=0",
                    "90=0",
                    "90=0",
                    "90=0",
                    "90=0",
                    "90=0",
                    "90=0",
                    "90=0",
                    "100=AcDbAssocNetwork",
                    "90=0",
                    "90=1",
                    "90=1",
                    "330=NET",
                    "90=0",
                },
                Shape(top, names, true)
            );

            // The circle and the INSERT name the dependencies as their reactors; the space's extension dictionary lists the network.
            Assert.Equal(new[] { geom.Handle }, document.FindEntity(circle).GetPersistentReactorIds());
            Assert.Equal(new[] { dependency.Handle }, TheInsert(document).GetPersistentReactorIds());
            Assert.NotNull(document.ModelSpace.Block.ExtensionDictionary);
            Assert.True(document.Database.NamedObjectsDictionary.Contains("ACAD_ASSOCNETWORK"));
            AssertReopens(document);
        }

        [Fact]
        public void TheCenterVariables_AreSetByTheirNames_AndRefuseWhatAutoCadRefused()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "CENTERLTYPE");
            Assert.Equal("Enter new value for CENTERLTYPE <\"Center2\">:", session.PromptText);
            Run(session, "NOPE");
            Assert.Contains("Cannot set CENTERLTYPE to that value.", session.History);
            Assert.Equal("Center2", document.Database.CenterLtype);

            // DASHED is one of the presenter's own linetypes, loaded when a mark is drawn, as AutoCAD loads it from acad.lin.
            Start(document, "CENTERLTYPE", "DASHED");
            Assert.Equal("DASHED", document.Database.CenterLtype);

            session = Start(document, "CENTERMARKEXE", "2");
            Assert.Contains("Requires 0 or 1 only.", session.History);
            Start(document, "CENTERMARKEXE", "0");
            Assert.False(document.Database.CenterMarkExe);

            Start(document, "CENTEREXE", "0.5");
            Assert.Equal(0.5, document.Database.CenterExe);
            Start(document, "CENTERLTSCALE", "2");
            Assert.Equal(2.0, document.Database.CenterLtScale);
            Start(document, "CENTERLTSCALE", "0");
            Assert.Equal(2.0, document.Database.CenterLtScale);

            session = Start(document, "CENTERCROSSSIZE", "abc");
            Assert.Contains("Cannot set CENTERCROSSSIZE to that value.", session.History);
            Start(document, "CENTERCROSSSIZE", "0.2x");
            Start(document, "CENTERCROSSGAP", "3");
            Start(document, "CENTERLAYER", "CL");
            Assert.Equal("0.2x", document.Database.CenterCrossSize);
            Assert.Equal("3", document.Database.CenterCrossGap);
            Assert.Equal("CL", document.Database.CenterLayer);

            Assert.True(document.Undo());
            Assert.Equal(".", document.Database.CenterLayer);
        }

        [Fact]
        public void AMarkInADrawingOfR12_IsRefused()
        {
            var database = DxfDatabase.Parse(
                "  0\nSECTION\n  2\nHEADER\n  9\n$ACADVER\n  1\nAC1009\n  0\nENDSEC\n  0\nSECTION\n  2\nENTITIES\n  0\nENDSEC\n  0\nEOF\n"
            );
            CadDocument document = CadDocument.From(database, "r12.dxf");
            CadCommandSession session = Start(document, "CENTERMARK");

            Assert.False(session.IsRunning);
            Assert.Contains(session.History, l => l.Contains("R12"));
        }

        /// <summary>The groups of a value of an action, as the DXF writes them after the count: the name and what holds it.</summary>
        internal static string[] Value(string name, string value) =>
            new[] { "90=0", "1=" + name, "90=0", "90=1", value, "330=0", "330=0" };

        /// <summary>The groups of a record as tokens: handles by the names given, numbers to six digits.</summary>
        internal static string[] Shape(Rec record, IDictionary<string, string> names, bool noOwnerGroup = false)
        {
            var tokens = new List<string>();

            foreach (KeyValuePair<int, string> group in record.Groups)
            {
                string value = group.Value.Trim();

                if (group.Key >= 10 && group.Key <= 59)
                {
                    value = double.Parse(value, CultureInfo.InvariantCulture)
                        .ToString("G6", CultureInfo.InvariantCulture);
                    value = value.Replace("E+060", "E+60");
                }
                else if (group.Key == 5 || group.Key == 330 || group.Key == 360 || group.Key == 350)
                {
                    value = names.TryGetValue(value, out string name) ? name : "?" + value;
                }

                tokens.Add(group.Key.ToString(CultureInfo.InvariantCulture) + "=" + value);
            }

            if (noOwnerGroup)
            {
                // A network of a dictionary names it as its reactor and owner: those groups left out.
                tokens.RemoveAll(t => t.StartsWith("102=", StringComparison.Ordinal));
                // The two 330 of the reactors and the owner come before the marker; remove them.
                int marker = tokens.IndexOf("100=AcDbAssocAction");
                tokens.RemoveRange(1, marker - 1);
            }
            else
            {
                tokens.RemoveAll(t => t.StartsWith("102=", StringComparison.Ordinal));
            }

            return tokens.ToArray();
        }

        /// <summary>One record of the OBJECTS section: its type, handle and groups.</summary>
        internal sealed class Rec
        {
            internal string Type;
            internal string Handle;
            internal List<KeyValuePair<int, string>> Groups = new List<KeyValuePair<int, string>>();
        }

        /// <summary>The records of the OBJECTS section of the drawing as it would be saved.</summary>
        internal static List<Rec> Objects(CadDocument document)
        {
            string[] lines = document.Database.ToText().Replace("\r", string.Empty).Split('\n');
            var records = new List<Rec>();
            Rec current = null;
            bool inObjects = false;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);
                string value = lines[i + 1];

                if (code == 0)
                {
                    string type = value.Trim();

                    if (type == "SECTION")
                    {
                        current = null;
                        continue;
                    }

                    if (type == "ENDSEC")
                    {
                        inObjects = false;
                        current = null;
                        continue;
                    }

                    if (inObjects)
                    {
                        current = new Rec { Type = type };
                        records.Add(current);
                    }

                    continue;
                }

                if (code == 2 && value.Trim() == "OBJECTS" && current == null)
                {
                    inObjects = true;
                    continue;
                }

                if (current != null)
                {
                    current.Groups.Add(new KeyValuePair<int, string>(code, value.Trim()));

                    if (code == 5 && current.Handle == null)
                    {
                        current.Handle = value.Trim();
                    }
                }
            }

            return records;
        }
    }
}
