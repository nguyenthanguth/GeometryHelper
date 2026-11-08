using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The commands the AutoCAD gap list called cheap: XLINE, RAY, POLYGON, DONUT, SELECTALL, the layer commands of the Layers
    /// panel, MEASUREGEOM, UNITS, SCRIPT, and the options LAYOUT Rename and Set, ELLIPSE Arc and AREA Add and Subtract; each
    /// asks AutoCAD's prompts and makes one undo step.
    /// </summary>
    public class EditingCommandGapTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        [Fact]
        public void XlineThroughAPoint_DrawsALineWithoutEndForEachThroughPoint_AsOneUndoStep()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "XL");
            Assert.Equal("Specify a point or [Hor/Ver/Ang/Bisect]:", session.PromptText);
            Run(session, P(0, 0));
            Assert.Equal("Specify through point:", session.PromptText);
            Run(session, P(10, 0), P(0, 10), Enter);

            var lines = All<DxfXline>(document.ModelSpace);
            Assert.Equal(2, lines.Count);
            Near(new GeoPoint3(0, 0, 0), lines[0].BasePoint);
            Near(new GeoPoint3(1, 0, 0), new GeoPoint3(lines[0].UnitDir.X, lines[0].UnitDir.Y, lines[0].UnitDir.Z));
            Near(new GeoPoint3(0, 1, 0), new GeoPoint3(lines[1].UnitDir.X, lines[1].UnitDir.Y, lines[1].UnitDir.Z));
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Empty(All<DxfXline>(document.ModelSpace));
        }

        [Fact]
        public void XlineHorAndBisect_LayTheLineLevelOrHalfwayRoundTheAngle()
        {
            CadDocument document = Blank();

            Start(document, "XLINE", "H", P(5, 7), Enter);
            DxfXline level = Assert.Single(All<DxfXline>(document.ModelSpace));
            Near(new GeoPoint3(5, 7, 0), level.BasePoint);
            Assert.Equal(0.0, level.UnitDir.Y, 12);

            Start(document, "XLINE", "B", P(0, 0), P(10, 0), P(0, 10), Enter);
            DxfXline half = All<DxfXline>(document.ModelSpace).Last();
            Assert.Equal(Math.Sqrt(0.5), half.UnitDir.X, 12);
            Assert.Equal(Math.Sqrt(0.5), half.UnitDir.Y, 12);
        }

        [Fact]
        public void RayFromAStartPoint_DrawsOneRayForEachThroughPoint()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "RAY");
            Assert.Equal("Specify start point:", session.PromptText);
            Run(session, P(1, 1), P(1, 5), P(4, 1), Enter);

            var rays = All<DxfRay>(document.ModelSpace);
            Assert.Equal(2, rays.Count);
            Near(new GeoPoint3(1, 1, 0), rays[1].BasePoint);
            Assert.Equal(1.0, rays[1].UnitDir.X, 12);
            Assert.True(document.Undo());
            Assert.Empty(All<DxfRay>(document.ModelSpace));
        }

        [Fact]
        public void PolygonInscribed_ARadiusTypedLaysTheBottomSideLevel()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "POL");
            Assert.Equal("Enter number of sides <4>:", session.PromptText);
            Run(session, "6");
            Assert.Equal("Specify center of polygon or [Edge]:", session.PromptText);
            Run(session, P(0, 0));
            Assert.Equal("Enter an option [Inscribed in circle/Circumscribed about circle] <I>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Specify radius of circle:", session.PromptText);
            Run(session, "10");

            DxfPolyline hexagon = Assert.Single(All<DxfPolyline>(document.ModelSpace));
            Assert.True(hexagon.Closed);
            Assert.Equal(6, hexagon.NumberOfVertices);
            Assert.Equal(1.5 * Math.Sqrt(3.0) * 100.0, hexagon.Area, 9);
            Near(P(5, -5 * Math.Sqrt(3.0)), hexagon.GetPoint2dAt(0));
            Near(P(-5, -5 * Math.Sqrt(3.0)), hexagon.GetPoint2dAt(5));
            AssertReopens(document);

            // The number of sides is offered again.
            Run(session, "POLYGON");
            Assert.Equal("Enter number of sides <6>:", session.PromptText);
        }

        [Fact]
        public void PolygonCircumscribedAboutAPointPicked_AndOnAnEdge()
        {
            CadDocument document = Blank();

            Start(document, "POLYGON", "4", P(0, 0), "C", P(10, 0));
            DxfPolyline square = Assert.Single(All<DxfPolyline>(document.ModelSpace));
            Assert.Equal(400.0, square.Area, 9);
            Near(P(10, -10), square.GetPoint2dAt(0));

            Start(document, "POLYGON", "3", "E", P(0, 0), P(10, 0));
            DxfPolyline triangle = All<DxfPolyline>(document.ModelSpace).Last();
            Near(P(5, 5 * Math.Sqrt(3.0)), triangle.GetPoint2dAt(2), 1E-9);
        }

        [Fact]
        public void PolygonRefusesTwoSides()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "POLYGON", "2");

            Assert.Equal("Requires an integer between 3 and 1024.", session.History.Last());
            Assert.Equal("Enter number of sides <4>:", session.PromptText);
        }

        [Fact]
        public void DonutIsAClosedPolylineOfTwoHalfCirclesAsWideAsTheRing()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "DO");
            Assert.Equal("Specify inside diameter of donut <0.5000>:", session.PromptText);
            Run(session, "10");
            Assert.Equal("Specify outside diameter of donut <1.0000>:", session.PromptText);
            Run(session, "20");
            Assert.Equal("Specify center of donut or <exit>:", session.PromptText);
            Run(session, P(0, 0), P(100, 0), Enter);

            var donuts = All<DxfPolyline>(document.ModelSpace);
            Assert.Equal(2, donuts.Count);
            DxfPolyline donut = donuts[0];
            Assert.True(donut.Closed);
            Assert.Equal(2, donut.NumberOfVertices);
            Assert.Equal(5.0, donut.ConstantWidth, 12);
            Assert.Equal(1.0, donut.GetBulgeAt(0), 12);
            Assert.Equal(1.0, donut.GetBulgeAt(1), 12);
            Near(P(-7.5, 0), donut.GetPoint2dAt(0));
            Near(P(7.5, 0), donut.GetPoint2dAt(1));
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Empty(All<DxfPolyline>(document.ModelSpace));
        }

        [Fact]
        public void SelectAllSelectsEveryObjectOfTheSpace_ButThoseOnAFrozenLayer_AndCtrlAIsItsKey()
        {
            CadDocument document = Plan();
            document.Database.LayerTable["Steel"].IsFrozen = true;

            CadCommandSession session = Start(document, "SELECTALL");

            Assert.Equal(new[] { "A2", "A3", "A4", "A5", "A6", "A7", "A8" }, document.Selection.Handles.OrderBy(h => h, StringComparer.Ordinal));
            Assert.Equal("7 found", session.History.Last());
            Assert.Equal("SELECTALL", CadShortcuts.Default.Keys["Ctrl+A"]);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void LayoffTurnsOffTheLayerOfEachObjectPicked_UndoTurnsTheLastBackOn_AllOneUndoStep()
        {
            CadDocument document = Plan();

            CadCommandSession session = Start(document, "LAYOFF");
            Assert.Equal("Select an object on the layer to be turned off or [Undo]:", session.PromptText);
            Run(session, (object)new[] { "A1" });
            Assert.Equal("Layer \"Steel\" has been turned off.", session.History.Last());
            Assert.True(document.Database.LayerTable["Steel"].IsOff);

            Run(session, "U");
            Assert.False(document.Database.LayerTable["Steel"].IsOff);
            Run(session, (object)new[] { "A1" });

            // Layer 0 is current: AutoCAD asks first.
            Run(session, (object)new[] { "A2" });
            Assert.Equal("Layer \"0\" is the current layer, do you want to turn it off? [Yes/No] <No>:", session.PromptText);
            Run(session, "Y", Enter);
            Assert.True(document.Database.LayerTable["0"].IsOff);

            Assert.True(document.Undo());
            Assert.False(document.Database.LayerTable["Steel"].IsOff);
            Assert.False(document.Database.LayerTable["0"].IsOff);
            Assert.False(document.CanUndo);

            Start(document, "LAYOFF", new[] { "A1" }, Enter, "LAYON");
            Assert.False(document.Database.LayerTable["Steel"].IsOff);
        }

        [Fact]
        public void LayfrzFreezesTheLayerPicked_RefusesTheCurrent_AndLaythwThawsAll()
        {
            CadDocument document = Plan();

            CadCommandSession session = Start(document, "LAYFRZ", new[] { "A1" }, new[] { "A2" }, Enter);

            Assert.True(document.Database.LayerTable["Steel"].IsFrozen);
            Assert.False(document.Database.LayerTable["0"].IsFrozen);
            Assert.Contains("Layer \"Steel\" has been frozen.", session.History);
            Assert.Contains("Cannot freeze layer \"0\".  It is the CURRENT layer.", session.History);

            Run(session, "LAYTHW");
            Assert.False(document.Database.LayerTable["Steel"].IsFrozen);
            Assert.Equal("All layers have been thawed.", session.History.Last());
        }

        [Fact]
        public void LaylckLocksAndLayulkUnlocksTheLayerOfOneObject()
        {
            CadDocument document = Plan();

            CadCommandSession session = Start(document, "LAYLCK");
            Assert.Equal("Select an object on the layer to be locked:", session.PromptText);
            Run(session, (object)new[] { "A1" });
            Assert.True(document.Database.LayerTable["Steel"].IsLocked);
            Assert.False(session.IsRunning);

            Run(session, "LAYULK", new[] { "A8" });
            Assert.False(document.Database.LayerTable["Locked"].IsLocked);
            Assert.Equal("Layer \"Locked\" has been unlocked.", session.History.Last());
        }

        [Fact]
        public void LayisoTurnsOffEveryOtherLayer_AndLayunisoTurnsThemBackOn()
        {
            CadDocument document = Plan();

            CadCommandSession session = Start(document, "LAYISO", new[] { "A1" }, Enter);

            Assert.Equal("Layer \"Steel\" has been isolated.", session.History.Last());
            Assert.False(document.Database.LayerTable["Steel"].IsOff);
            Assert.True(document.Database.LayerTable["0"].IsOff);
            Assert.True(document.Database.LayerTable["Locked"].IsOff);
            Assert.Equal("Steel", document.Database.Clayer);

            Run(session, "LAYUNISO");
            Assert.False(document.Database.LayerTable["0"].IsOff);
            Assert.False(document.Database.LayerTable["Locked"].IsOff);

            Run(session, "LAYUNISO");
            Assert.Equal("No layers were isolated by LAYISO.", session.History.Last());
        }

        [Fact]
        public void UnitsOnTheCommandLineSetsTheFormatsAndPlaces_AsOneUndoStep()
        {
            CadDocument document = Blank();
            int auprec = document.Database.Auprec;

            CadCommandSession session = Start(document, "-UNITS");
            Assert.Contains("  2. Decimal              15.50", session.History);
            Assert.Equal("Enter choice, 1 to 5 <2>:", session.PromptText);
            Run(session, "6");
            Assert.Equal("Requires an integer between 1 and 5.", session.History.Last());
            Run(session, "5");
            Assert.Equal("(1, 2, 4, 8, 16, 32, 64, 128, or 256) <16>:", session.PromptText);
            Run(session, "8");
            Assert.Equal("Enter choice, 1 to 5 <1>:", session.PromptText);
            Run(session, "4");
            Assert.Equal("Enter number of fractional places for display of angles (0 to 8) <" + auprec + ">:", session.PromptText);
            Run(session, "3", Enter);
            Assert.Equal("Measure angles clockwise? [Yes/No] <No>:", session.PromptText);
            Run(session, "Y");

            Assert.False(session.IsRunning);
            Assert.Equal(5, document.Database.Lunits);
            Assert.Equal(3, document.Database.Luprec);
            Assert.Equal(3, document.Database.Aunits);
            Assert.Equal(3, document.Database.Auprec);
            Assert.True(document.Database.Angdir);

            Assert.True(document.Undo());
            Assert.Equal(2, document.Database.Lunits);
            Assert.Equal(4, document.Database.Luprec);
            Assert.False(document.Database.Angdir);
            Assert.Equal("UNITS", CadCommandRegistry.CreateDefault().Find("UN").Name);
        }

        [Fact]
        public void ScriptFeedsEachLineAsTyped_AndStopsAtTheFirstRefused()
        {
            CadDocument document = Blank();
            string file = Path.Combine(Path.GetTempPath(), "gh-script-" + Guid.NewGuid().ToString("N") + ".scr");
            File.WriteAllText(file, "; a line and a circle\nLINE\n0,0\n100,0\n\nCIRCLE\n50,50\n10\nFOO\nLINE\n0,0\n0,100\n\n");

            try
            {
                CadCommandSession session = Start(document, "SCR");
                Assert.Equal("Enter script file name:", session.PromptText);
                Run(session, file);

                Assert.Single(All<DxfLine>(document.ModelSpace));
                Assert.Single(All<DxfCircle>(document.ModelSpace));
                Assert.Contains("Command: CIRCLE", session.History);
                Assert.Equal("Script stopped at line 9: FOO", session.History.Last());
                Assert.False(session.IsRunning);

                Run(session, "SCRIPT", file + ".none");
                Assert.Equal("Cannot find script file \"" + file + ".none\".", session.History.Last());
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public void LayoutRenameRenamesTheTab_UndoTakesTheNameBack_AndSetShowsALayout()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "LO");
            Assert.Equal("Enter layout option [New/Rename/Set/?] <New>:", session.PromptText);
            Run(session, "R");
            Assert.Equal("Enter layout to rename:", session.PromptText);
            Run(session, "layout1");
            Assert.Equal("Enter new layout name <Layout1>:", session.PromptText);
            Run(session, "Sheet A");

            Assert.Equal("Layout \"Layout1\" renamed to \"Sheet A\".", session.History.Last());
            Assert.Equal(new[] { "Model", "Sheet A" }, document.Spaces.Select(s => s.Name));
            Assert.Equal("Sheet A", document.Spaces[1].Layout.LayoutName);
            AssertReopens(document);

            Run(session, "LAYOUT", "S", "sheet a");
            Assert.Equal("Sheet A", document.CurrentSpace.Name);

            Assert.True(document.Undo());
            Assert.Equal(new[] { "Model", "Layout1" }, document.Spaces.Select(s => s.Name));

            Run(session, "LAYOUT", "S", "Nowhere");
            Assert.Equal("Layout \"Nowhere\" not found.", session.History.Last());
        }

        [Fact]
        public void EllipseArc_ItsAnglesCountedFromTheFirstEndOfTheAxis()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "ELLIPSE", "A");
            Assert.Equal("Specify axis endpoint of elliptical arc or [Center]:", session.PromptText);
            Run(session, P(0, 0), P(100, 0), "25");
            Assert.Equal("Specify start angle:", session.PromptText);
            Run(session, "0");
            Assert.Equal("Specify end angle or [Included angle]:", session.PromptText);
            Run(session, "90");

            DxfEllipse arc = Assert.Single(All<DxfEllipse>(document.ModelSpace));
            Assert.False(arc.Closed);
            Near(new GeoPoint3(0, 0, 0), arc.StartPoint, 1E-9);
            Near(new GeoPoint3(50, -25, 0), arc.EndPoint, 1E-9);
            AssertReopens(document);
            Assert.True(document.Undo());
            Assert.Empty(All<DxfEllipse>(document.ModelSpace));

            // By its centre, the first axis the short one: angles still from that axis's end, an included angle after.
            Start(document, "EL", "A", "C", P(0, 0), P(0, 10), "40", P(-5, 0), "I", "90");
            DxfEllipse other = Assert.Single(All<DxfEllipse>(document.ModelSpace));
            Near(new GeoPoint3(-40, 0, 0), other.StartPoint, 1E-9);
            Near(new GeoPoint3(0, -10, 0), other.EndPoint, 1E-9);
        }

        [Fact]
        public void AreaAddAndSubtract_KeepARunningTotal()
        {
            CadDocument document = Plan();

            CadCommandSession session = Start(document, "AREA");
            Assert.Equal("Specify first corner point or [Object/Add area/Subtract area] <Object>:", session.PromptText);
            Run(session, "A");
            Assert.Equal("Specify first corner point or [Object/Subtract area]:", session.PromptText);
            Run(session, P(0, 0), P(100, 0));
            Assert.Equal("(ADD mode) Specify next point or [Undo]:", session.PromptText);
            Run(session, P(100, 50), P(0, 50), Enter);
            Assert.Equal("Total area = 5000.0000", session.History.Last());

            Run(session, "S", "O");
            Assert.Equal("(SUBTRACT mode) Select objects:", session.PromptText);
            Run(session, (object)new[] { "A3" });
            Assert.Equal("Total area = 4685.8407", session.History.Last());
            Run(session, Enter);
            Assert.Equal("Specify first corner point or [Object/Add area]:", session.PromptText);
            Run(session, Enter);

            Assert.False(session.IsRunning);
            Assert.False(document.IsModified);
        }

        [Fact]
        public void MeasuregeomMeasuresRadiusAndAngle_ThenOffersExit()
        {
            CadDocument document = Plan();

            CadCommandSession session = Start(document, "MEA");
            Assert.Equal("Enter an option [Distance/Radius/Angle/ARea/eXit] <Distance>:", session.PromptText);
            Run(session, "R", new[] { "A3" });
            Assert.Equal("Radius = 10.0000", session.History[session.History.Count - 2]);
            Assert.Equal("Diameter = 20.0000", session.History.Last());
            Assert.Equal("Enter an option [Distance/Radius/Angle/ARea/eXit] <eXit>:", session.PromptText);

            Run(session, "A", new[] { "A4" });
            Assert.Equal("Angle = 90.00°", session.History.Last());

            Run(session, "A", new[] { "A1" }, new[] { "A2" });
            Assert.Equal("Angle = 90.00°", session.History.Last());

            Run(session, "A", Enter, P(0, 0), P(10, 0), P(10, 10));
            Assert.Equal("Angle = 45.00°", session.History.Last());

            Run(session, "D", P(0, 0), P(3, 4));
            Assert.Equal("Delta X = 3.0000,  Delta Y = 4.0000,  Delta Z = 0.0000", session.History.Last());

            Run(session, Enter);
            Assert.False(session.IsRunning);
            Assert.False(document.IsModified);
        }

        [Fact]
        public void TheGapCommandsHaveAutoCadsAliases()
        {
            CadCommandRegistry registry = CadCommandRegistry.CreateDefault();

            Assert.Equal("XLINE", registry.Find("XL").Name);
            Assert.Equal("POLYGON", registry.Find("POL").Name);
            Assert.Equal("DONUT", registry.Find("DO").Name);
            Assert.Equal("MEASUREGEOM", registry.Find("MEA").Name);
            Assert.Equal("-UNITS", registry.Find("-UN").Name);
            Assert.Equal("SCRIPT", registry.Find("SCR").Name);
            Assert.Equal("RAY", registry.Find("RAY").Name);

            foreach (string name in new[] { "SELECTALL", "LAYOFF", "LAYON", "LAYFRZ", "LAYTHW", "LAYLCK", "LAYULK", "LAYISO", "LAYUNISO" })
            {
                Assert.Equal(name, registry.Find(name).Name);
            }
        }
    }
}
