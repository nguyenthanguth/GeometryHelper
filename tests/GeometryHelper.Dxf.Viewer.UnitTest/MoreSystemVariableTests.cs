using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The system variables an AutoCAD user types most, with the range, the refusal and the start of AutoCAD 2021's console:
    /// the units of the drawing, the polyline and hatch settings, what the commands start with, and those kept with no behaviour.
    /// </summary>
    public class MoreSystemVariableTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static string Header(CadDocument document, string name) =>
            string.Join(";", document.Database.GetHeader(name).Select(g => g.TypeCode + "=" + g.Value.Trim()));

        [Fact]
        public void LunitsLuprecAunitsAuprec_TakeAutoCadsRanges_AndWriteTheHeader_EachOneUndoStep()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "LUNITS");
            Assert.Equal("Enter new value for LUNITS <2>:", session.PromptText);
            Run(session, "6");
            Assert.Equal("Requires an integer between 1 and 5.", session.History.Last());
            Assert.Equal("Enter new value for LUNITS <2>:", session.PromptText);
            Run(session, "4", "LUPREC", "9");
            Assert.Equal("Requires an integer between 0 and 8.", session.History.Last());
            Run(session, "2", "AUNITS", "5");
            Assert.Equal("Requires an integer between 0 and 4.", session.History.Last());
            Run(session, "1", "AUPREC", "3");

            Assert.Equal(4, document.Database.Lunits);
            Assert.Equal(2, document.Database.Luprec);
            Assert.Equal(1, document.Database.Aunits);
            Assert.Equal(3, document.Database.Auprec);
            Assert.Equal("70=4", Header(document, "$LUNITS"));
            Assert.Equal("70=3", Header(document, "$AUPREC"));
            AssertReopens(document);

            for (int i = 0; i < 4; i++)
            {
                Assert.True(document.Undo());
            }

            Assert.False(document.CanUndo);
            Assert.Equal(2, document.Database.Lunits);
            Assert.Equal(4, document.Database.Luprec);
            Assert.Equal(0, document.Database.Aunits);
            Assert.Equal(0, document.Database.Auprec);
        }

        [Fact]
        public void LuprecSet_IsThePlacesTheRecentInputOfPointsIsWrittenTo()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "LUPREC", "2", "LINE", "0,300", "100.456,300", "@0,50");

            Assert.Equal(new[] { "@0.00,50.00", "100.46,300.00", "0.00,300.00" }, session.RecentInput);
        }

        [Fact]
        public void AngbaseIsKeptFromNoughtToATurn_TakenAsTyped_AndAngdirIsASwitch()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "ANGBASE");
            Assert.Equal("Enter new value for ANGBASE <0>:", session.PromptText);
            Run(session, "-5");

            Assert.Equal(355.0 * Math.PI / 180.0, document.Database.Angbase, 9);
            Assert.Equal("50=355", Header(document, "$ANGBASE"));

            // A second 90 is not counted from the base the first one gave.
            Run(session, "ANGBASE", "90");
            Assert.Equal(Math.PI / 2.0, document.Database.Angbase, 9);
            Assert.Equal("50=90", Header(document, "$ANGBASE"));

            Run(session, "ANGDIR", "2");
            Assert.Equal("Requires 0 or 1 only.", session.History.Last());
            Run(session, "1");
            Assert.True(document.Database.Angdir);
            Assert.Equal("70=1", Header(document, "$ANGDIR"));
            Assert.True(document.Undo());
            Assert.False(document.Database.Angdir);
            Assert.True(document.Undo());
            Assert.Equal(355.0 * Math.PI / 180.0, document.Database.Angbase, 9);
        }

        [Fact]
        public void InsunitsTakesZeroTo24_ADrawingOfR12CannotKeepIt_AndMeasurementIsOneOrNought()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "INSUNITS");
            Assert.Equal("Enter new value for INSUNITS <4>:", session.PromptText);
            Run(session, "25");
            Assert.Equal("Requires an integer between 0 and 24.", session.History.Last());
            Run(session, "6");
            Assert.Equal(DxfUnits.Meters, document.Database.Insunits);
            Assert.Equal("70=6", Header(document, "$INSUNITS"));

            Run(session, "MEASUREMENT", "0");
            Assert.Equal(DxfMeasurementValue.English, document.Database.Measurement);
            Run(session, "MEASUREMENT", "2");
            Assert.Equal("Requires 0 or 1 only.", session.History.Last());
            Run(session, Esc);

            var r12 = DxfDatabase.Parse(
                "  0\nSECTION\n  2\nHEADER\n  9\n$ACADVER\n  1\nAC1009\n  0\nENDSEC\n  0\nSECTION\n  2\nENTITIES\n  0\nENDSEC\n  0\nEOF\n"
            );
            CadDocument old = CadDocument.From(r12, "old.dxf");
            CadCommandSession oldSession = Start(old, "INSUNITS", "4");
            Assert.StartsWith(
                "INSUNITS is kept in a drawing of R2000",
                oldSession.History.Last(),
                StringComparison.Ordinal
            );
            Assert.False(old.CanUndo);
        }

        [Fact]
        public void PlinewidIsTheWidthPlineStartsAt_AndANegativeWidthIsRefusedAndNotAskedAgain()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "PLINEWID");
            Assert.Equal("Enter new value for PLINEWID <0.0000>:", session.PromptText);
            Run(session, "-1");
            Assert.Equal("Cannot set PLINEWID to that value.", session.History.Last());
            Assert.False(session.IsRunning);

            Run(session, "PLINEWID", "5");
            Assert.Equal("40=5", Header(document, "$PLINEWID"));

            Run(session, "PLINE", P(0, 0));
            Assert.Equal("Current line-width is 5.0000", session.History.Last());
            Run(session, P(100, 0), Enter);

            DxfPolyline made = Assert.Single(All<DxfPolyline>(document.ModelSpace));
            Assert.Equal(5.0, made.GetStartWidthAt(0), 9);
            Assert.Equal(5.0, made.GetEndWidthAt(0), 9);
        }

        [Fact]
        public void PlinegenOne_GivesTheLinetypeGenerationToThePolylinesPlineRectangAndPolygonMake()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "PLINEGEN", "1");
            Assert.Equal("70=1", Header(document, "$PLINEGEN"));

            Run(session, "PLINE", P(0, 0), P(100, 0), Enter);
            Run(session, "RECTANG", P(0, 50), P(40, 90));
            Run(session, "POLYGON", "5", P(200, 0), "I", P(220, 0));

            Assert.Equal(3, All<DxfPolyline>(document.ModelSpace).Count);
            Assert.All(All<DxfPolyline>(document.ModelSpace), p => Assert.True(p.Plinegen));

            Run(session, "PLINEGEN", "0", "PLINE", P(0, 300), P(100, 300), Enter);
            Assert.False(All<DxfPolyline>(document.ModelSpace).Last().Plinegen);
        }

        [Fact]
        public void FillmodeZero_DrawsSolidsWidePolylinesAndSolidHatchesByTheirOutlines_AndOneBringsTheFillsBack()
        {
            var database = new DxfDatabase("AC1027");
            database.ModelSpace.AppendEntity(
                new DxfSolid(
                    new GeoPoint3(0, 0, 0),
                    new GeoPoint3(10, 0, 0),
                    new GeoPoint3(0, 10, 0),
                    new GeoPoint3(10, 10, 0)
                )
            );
            var wide = new DxfPolyline();
            wide.AddVertexAt(0, new GeoPoint2(20, 0), 0.0, 2.0, 2.0);
            wide.AddVertexAt(1, new GeoPoint2(40, 0), 0.0, 2.0, 2.0);
            database.ModelSpace.AppendEntity(wide);
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "SOLID");
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[] { new GeoPoint2(50, 0), new GeoPoint2(60, 0), new GeoPoint2(60, 10), new GeoPoint2(50, 10) },
                null
            );
            database.ModelSpace.AppendEntity(hatch);
            CadDocument document = CadDocument.From(database, "fill.dxf");

            Assert.Equal(3, document.GetDisplay(document.ModelSpace).Items.OfType<DisplayFill2>().Count());

            CadCommandSession session = Start(document, "FILLMODE");
            Assert.Equal("Enter new value for FILLMODE <1>:", session.PromptText);
            Run(session, "0");

            Assert.False(document.Database.Fillmode);
            Assert.Equal("70=0", Header(document, "$FILLMODE"));
            Assert.Empty(document.GetDisplay(document.ModelSpace).Items.OfType<DisplayFill2>());
            Assert.Equal(
                3,
                document.GetDisplay(document.ModelSpace).Items.OfType<DisplayPolyline2>().Count(p => p.Closed)
            );

            Assert.True(document.Undo());
            Assert.Equal(3, document.GetDisplay(document.ModelSpace).Items.OfType<DisplayFill2>().Count());
        }

        [Fact]
        public void ElevationThicknessAndTheUserVariables_AreKeptInTheHeader_WithAutoCadsRanges()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "ELEVATION", "-5", "THICKNESS", "12.5", "USERI3", "40000");
            Assert.Equal("Requires an integer between -32768 and 32767.", session.History.Last());
            Run(session, "-7", "USERR2", "2.5", "SPLINESEGS", "0");

            Assert.Equal(-5.0, document.Database.Elevation);
            Assert.Equal(12.5, document.Database.Thickness);
            Assert.Equal(-7, document.Database.Useri3);
            Assert.Equal(2.5, document.Database.Userr2);
            Assert.Equal("70=-7", Header(document, "$USERI3"));
            Assert.Equal("Cannot set SPLINESEGS to that value.", session.History.Last());
            Assert.Equal(8, document.Database.Splinesegs);

            Run(session, "SPLINETYPE", "5", "SPLINETYPE", "7");
            Assert.Equal(5, document.Database.Splinetype);
            Assert.Equal("Cannot set SPLINETYPE to that value.", session.History.Last());
        }

        [Fact]
        public void TheSwitchesOfTheDrawing_Limcheck_Splframe_Qtextmode_Unitmode_Attmode_AreKept()
        {
            CadDocument document = Blank();

            Start(document, "LIMCHECK", "1", "SPLFRAME", "1", "QTEXTMODE", "1", "UNITMODE", "1", "ATTMODE", "2");

            Assert.True(document.Database.Limcheck);
            Assert.True(document.Database.Splframe);
            Assert.True(document.Database.Qtextmode);
            Assert.Equal(1, document.Database.Unitmode);
            Assert.Equal(2, document.Database.Attmode);

            for (int i = 0; i < 5; i++)
            {
                Assert.True(document.Undo());
            }

            Assert.False(document.CanUndo);
            Assert.False(document.Database.Limcheck);
            Assert.Equal(1, document.Database.Attmode);
        }

        [Fact]
        public void CircleradPolysidesAndTrimextendmode_AreWhatCirclePolygonTrimAndExtendStartWith()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "CIRCLERAD", "-1");
            Assert.Equal("Cannot set CIRCLERAD to that value.", session.History.Last());
            Run(session, "CIRCLERAD", "12.5", "CIRCLE", P(0, 0));
            Assert.Equal("Specify radius of circle or [Diameter] <12.5000>:", session.PromptText);
            Run(session, Esc);

            Run(session, "POLYSIDES", "2");
            Assert.Equal("Requires an integer between 3 and 1024.", session.History.Last());
            Run(session, "7", "POLYGON");
            Assert.Equal("Enter number of sides <7>:", session.PromptText);
            Run(session, Esc);

            Run(session, "TRIMEXTENDMODE");
            Assert.Equal("Enter new value for TRIMEXTENDMODE <1>:", session.PromptText);
            Run(session, "0", "TRIM");
            Assert.Contains(
                "Mode=Standard",
                session.History.Last(l => l.StartsWith("Current settings", StringComparison.Ordinal))
            );
            Assert.StartsWith("Select objects", session.PromptText, StringComparison.Ordinal);
            Run(session, Esc);

            Run(session, "TRIMEXTENDMODE", "1", "EXTEND");
            Assert.Contains(
                "Mode=Quick",
                session.History.Last(l => l.StartsWith("Current settings", StringComparison.Ordinal))
            );
        }

        [Fact]
        public void TheVariablesOfTheSession_StartAsAutoCadStartsThem_AndTakeItsRanges()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);

            string[] starts =
            {
                "CMDECHO 1",
                "EXPERT 0",
                "HIGHLIGHT 1",
                "PICKAUTO 5",
                "PICKDRAG 2",
                "OSNAPZ 0",
                "ARRAYASSOCIATIVITY 1",
            };

            foreach (string start in starts)
            {
                string[] parts = start.Split(' ');
                Run(session, parts[0]);
                Assert.Equal("Enter new value for " + parts[0] + " <" + parts[1] + ">:", session.PromptText);
                Run(session, Esc);
            }

            Run(session, "EXPERT", "6");
            Assert.Equal("Requires an integer between 0 and 5.", session.History.Last());
            Run(session, "3", "PICKAUTO", "8");
            Assert.Equal("Requires an integer between -7 and 7.", session.History.Last());
            Run(session, "-7", "PICKDRAG", "3");
            Assert.Equal("Requires an integer between 0 and 2.", session.History.Last());
            Run(session, "1", "CMDECHO", "0");

            Assert.Equal(3.0, session.Remembered["expert"]);
            Assert.Equal(-7.0, session.Remembered["pickauto"]);
            Assert.Equal(1.0, session.Remembered["pickdrag"]);
            Assert.Equal(0.0, session.Remembered["cmdecho"]);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void HatchVariables_AreTheSettingsOfTheNextHatch_AndAHatchIsMadeWithThem()
        {
            var database = new DxfDatabase("AC1027");
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "Steel" });
            GeoPoint3[] corners =
            {
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                new GeoPoint3(100, 60, 0),
                new GeoPoint3(0, 60, 0),
            };

            for (int i = 0; i < 4; i++)
            {
                database.ModelSpace.AppendEntity(new DxfLine(corners[i], corners[(i + 1) % 4]));
            }

            CadDocument document = CadDocument.From(database, "hatch.dxf");
            CadCommandSession session = Start(document, "HPNAME");
            Assert.Equal("Enter new value for HPNAME <\"ANSI31\">:", session.PromptText);
            Run(session, "xyz");
            Assert.Equal("Cannot set HPNAME to that value.", session.History.Last());
            Assert.Equal("ANSI31", document.HatchSettings.PatternName);

            Run(session, "HPNAME", "ansi32", "HPSCALE", "0");
            Assert.Equal("Cannot set HPSCALE to that value.", session.History.Last());
            Run(session, "HPSCALE", "2.5", "HPANG", "45", "HPLAYER", "Steel");

            Assert.Equal("ANSI32", document.HatchSettings.PatternName);
            Assert.Equal(2.5, document.HatchSettings.Scale);
            Assert.Equal(45.0, document.HatchSettings.Angle, 9);
            Assert.Equal("Steel", document.HatchSettings.Layer);

            Run(session, "HATCH", P(50, 30), Enter);

            DxfHatch hatch = Assert.Single(All<DxfHatch>(document.ModelSpace));
            Assert.Equal("ANSI32", hatch.PatternName);
            Assert.Equal(2.5, hatch.PatternScale, 9);
            Assert.Equal(Math.PI / 4.0, hatch.PatternAngle, 9);
            Assert.Equal("Steel", hatch.Layer);

            Run(session, "HPNAME", "SOLID");
            Assert.Equal(CadHatchType.Solid, document.HatchSettings.Type);
            Run(session, "HPNAME");
            Assert.Equal("Enter new value for HPNAME <\"SOLID\">:", session.PromptText);
            Run(session, Esc, "HPLAYER");
            Assert.Equal("Enter new value for HPLAYER, or . for use current <\"Steel\">:", session.PromptText);
            Run(session, ".");
            Assert.Null(document.HatchSettings.Layer);
            Run(session, "HPLAYER");
            Assert.Equal("Enter new value for HPLAYER, or . for use current <\"use current\">:", session.PromptText);
        }

        [Fact]
        public void HpassocHpdoubleHpgaptolHporiginmode_AreTheOtherSettingsOfTheNextHatch()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "HPGAPTOL", "5001");
            Assert.Equal("Cannot set HPGAPTOL to that value.", session.History.Last());
            Run(session, "HPGAPTOL", "2.5", "HPASSOC", "0", "HPDOUBLE", "1", "HPORIGINMODE", "6");
            Assert.Equal("Requires an integer between 0 and 5.", session.History.Last());
            Run(session, "5");

            Assert.Equal(2.5, document.HatchSettings.GapTolerance);
            Assert.False(document.HatchSettings.Associative);
            Assert.True(document.HatchSettings.Double);
            Assert.Equal(CadHatchOriginMode.Center, document.HatchSettings.OriginMode);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void DimlayerIsTheLayerNewDimensionsGoOn_AnyNameTaken_AndAPeriodGivesTheCurrentLayerBack()
        {
            var database = new DxfDatabase("AC1027");
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "Steel" });
            CadDocument document = CadDocument.From(database, "dim.dxf");
            CadCommandSession session = Start(document, "DIMLAYER");
            Assert.Equal("Enter new value for DIMLAYER, or . for use current <\"use current\">:", session.PromptText);
            Run(session, "Steel", "DIMLINEAR", P(0, 400), P(100, 400), P(50, 430));

            DxfDimension dimension = All<DxfDimension>(document.ModelSpace).Last();
            Assert.Equal("Steel", dimension.Layer);

            Run(session, "DIMLAYER", ".", "DIMLINEAR", P(0, 500), P(100, 500), P(50, 530));
            Assert.Equal("0", All<DxfDimension>(document.ModelSpace).Last().Layer);

            // A name the drawing has no layer of is kept, and the dimension goes on the current layer.
            Run(session, "DIMLAYER", "NOSUCH");
            Assert.Equal(0, All<DxfDimension>(document.ModelSpace).Count(d => d.Layer == "NOSUCH"));
            Run(session, "DIMLAYER");
            Assert.Equal("Enter new value for DIMLAYER, or . for use current <\"NOSUCH\">:", session.PromptText);
        }

        [Fact]
        public void DistanceAreaAndPerimeter_GiveWhatDistAndAreaMeasuredLast_AndCannotBeSet()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "DIST", P(0, 0), P(3, 4), "DISTANCE");
            Assert.Equal("DISTANCE = 5.0000 (read only)", session.History.Last());

            Run(session, "AREA", P(0, 0), P(10, 0), P(10, 5), Enter);

            // The names of the commands AREA and DIST are theirs: the variable is read through SETVAR.
            Run(session, "SETVAR", "AREA");
            Assert.Equal("AREA = 25.0000 (read only)", session.History.Last());
            Run(session, "PERIMETER");
            Assert.Equal("PERIMETER = 26.1803 (read only)", session.History.Last());
        }

        [Fact]
        public void LastpointIsInTheUserCoordinateSystem_AndSetByTyping_TheNextRelativePointStartsFromIt()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "LASTPOINT");
            Assert.Equal("Enter new value for LASTPOINT <0.0000,0.0000,0.0000>:", session.PromptText);
            Run(session, "30,40");

            Near(P(30, 40), session.LastPoint);
            Run(session, "LINE", P(0, 0), "@10,0", Enter);
            Assert.Single(All<DxfLine>(document.ModelSpace));
            Run(session, "LASTPOINT");
            Assert.Equal("Enter new value for LASTPOINT <10.0000,0.0000,0.0000>:", session.PromptText);
        }

        [Fact]
        public void SetvarListsTheNewVariables_InOrderOfTheirNames()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "SETVAR", "?", "HP*");
            string[] names = session
                .History.Where(l => l.StartsWith("HP", StringComparison.Ordinal))
                .Select(l => l.Substring(0, l.IndexOf(' ')))
                .ToArray();

            Assert.Equal(
                new[]
                {
                    "HPANG",
                    "HPASSOC",
                    "HPBOUNDRETAIN",
                    "HPDLGMODE",
                    "HPDOUBLE",
                    "HPGAPTOL",
                    "HPLAYER",
                    "HPNAME",
                    "HPORIGINMODE",
                    "HPSCALE",
                },
                names
            );
        }
    }
}
