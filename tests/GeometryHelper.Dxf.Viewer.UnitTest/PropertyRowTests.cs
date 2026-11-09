using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>The rows of polylines, ellipses, hatches, dimensions, MTEXTs, block references, viewports and multileaders, and MATCHPROP's settings.</summary>
    public class PropertyRowTests
    {
        private static readonly GeoVector3 Up = new GeoVector3(0, 0, 1);

        /// <summary>
        /// A drawing of R2013 with a polyline P1 (0, 0) (30, 0) (30, 20) (0, 20), closed; an ellipse of radii 20 and 10 about
        /// (100, 0); a square 30 by 20 hatched with lines of its own 3 apart at (200, 0); an MTEXT "NOTE" 2.5 high at (0, 100);
        /// the block TAG inserted at (300, 0) with its attribute MARK of B-12; a linear dimension of 300 under (0, -50); and a
        /// multileader B-12 from (0, 200).
        /// </summary>
        private static CadDocument Kinds()
        {
            CadDocument document = BlankWithMLeaderStyle();
            CadCommandSession session = Session(document);
            Run(session, "DLI", P(0, -50), P(300, -50), P(150, -80));
            Run(session, "MLD", P(0, 200), P(30, 220), "B-12");
            DxfDatabase db = document.Database;

            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
            polyline.AddVertexAt(1, new GeoPoint2(30, 0), 0, 0, 0);
            polyline.AddVertexAt(2, new GeoPoint2(30, 20), 0, 0, 0);
            polyline.AddVertexAt(3, new GeoPoint2(0, 20), 0, 0, 0);
            polyline.Closed = true;
            db.ModelSpace.AppendEntity(polyline);

            db.ModelSpace.AppendEntity(new DxfEllipse(new GeoPoint3(100, 0, 0), Up, new GeoVector3(20, 0, 0), 0.5, 0, 2 * Math.PI));

            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            hatch.PatternSpace = 3.0;
            hatch.AppendLoop(DxfHatchLoopTypes.External, new[] { new GeoPoint2(200, 0), new GeoPoint2(230, 0), new GeoPoint2(230, 20), new GeoPoint2(200, 20) }, null);
            db.ModelSpace.AppendEntity(hatch);

            db.ModelSpace.AppendEntity(new DxfMText { Contents = "NOTE", TextHeight = 2.5, Location = new GeoPoint3(0, 100, 0) });
            db.ModelSpace.AppendEntity(new DxfDBText { TextString = "A", Height = 7, Position = new GeoPoint3(0, 400, 0), Oblique = 0.2, WidthFactor = 0.8 });

            var block = new DxfBlockTableRecord { Name = "TAG" };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            var insert = new DxfBlockReference(new GeoPoint3(300, 0, 0), db.BlockTable["TAG"]);
            db.ModelSpace.AppendEntity(insert);
            insert.AttributeCollection.AppendAttribute(new DxfAttributeReference(new GeoPoint3(300, 0, 0), "B-12", "MARK", null));

            return CadDocument.From(DxfDatabase.Parse(db.ToText()), "kinds.dxf");
        }

        private static string Handle<T>(CadDocument document)
            where T : DxfEntity
            => All<T>(document.ModelSpace).First().Handle;

        private static string Shown(CadDocument document, string handle, string label)
            => CadProperties.Describe(document, null, new[] { handle }).Groups.SelectMany(g => g.Rows).Single(r => r.Label == label).Value;

        private static List<string> Rows(CadDocument document, string handle)
            => CadProperties.Describe(document, null, new[] { handle }).Groups.SelectMany(g => g.Rows).Select(r => r.Label + "=" + r.Value).ToList();

        private static void Set(CadDocument document, string handle, string label, string value)
        {
            CadEditResult result = CadPropertyEditor.Set(document, new[] { handle }, label, value);
            Assert.True(result.Succeeded, label + ": " + result.Message);
        }

        public static IEnumerable<object[]> KindNames => new[] { "Polyline", "Ellipse", "Hatch", "MText", "Block Reference", "Rotated Dimension", "Multileader" }.Select(k => new object[] { k });

        [Theory]
        [MemberData(nameof(KindNames))]
        public void EveryRowThatCanBeEdited_TakesBackTheTextItShows_AndShowsTheSame(string kind)
        {
            CadDocument document = Kinds();
            string handle = All<DxfEntity>(document.ModelSpace).First(e => CadProperties.Describe(document, null, new[] { e.Handle }).Label == kind + " (1)").Handle;
            List<string> before = Rows(document, handle);

            foreach (CadPropertyRow row in CadProperties.Describe(document, null, new[] { handle }).Groups.SelectMany(g => g.Rows).Where(r => !r.IsReadOnly))
            {
                Set(document, handle, row.Label, row.Value);
            }

            Assert.Equal(before, Rows(document, handle));
            AssertReopens(document);
        }

        [Fact]
        public void EveryRowOfAViewportThatCanBeEdited_TakesBackTheTextItShows()
        {
            CadDocument document = LayoutTests.TwoViewports();
            CadSpace plan = document.Spaces.Single(s => s.Name == "Plan");
            string handle = document.GetDisplay(plan).Items.OfType<DisplayViewport2>().OrderBy(v => v.CenterX).First().Handle;
            List<string> before = Rows(document, handle);

            foreach (CadPropertyRow row in CadProperties.Describe(document, null, new[] { handle }).Groups.SelectMany(g => g.Rows).Where(r => !r.IsReadOnly))
            {
                Set(document, handle, row.Label, row.Value);
            }

            Assert.Equal(before, Rows(document, handle));
        }

        [Fact]
        public void TheCurrentVertexStepsThroughAPolyline_WithNoStepOfUndo_AndItsRowsSetThatVertex()
        {
            CadDocument document = Kinds();
            string handle = Handle<DxfPolyline>(document);
            document.Selection.Set(new[] { handle });

            Set(document, handle, "Current vertex", "3");

            Assert.Null(document.NextUndo);
            Assert.Equal("3", Shown(document, handle, "Current vertex"));
            Assert.Equal("30.0000", Shown(document, handle, "Vertex X"));
            Assert.Equal("20.0000", Shown(document, handle, "Vertex Y"));

            Set(document, handle, "Vertex Y", "25");
            Set(document, handle, "Start segment width", "2");

            var polyline = Entity<DxfPolyline>(document, handle);
            Near(new GeoPoint2(30, 25), polyline.GetPoint2dAt(2));
            Assert.Equal(2.0, polyline.GetStartWidthAt(2));
            Assert.Equal(0.0, polyline.GetStartWidthAt(1));
            Assert.Equal("Properties", document.NextUndo.Name);

            Set(document, handle, "Current vertex", "9");
            Assert.Equal("4", Shown(document, handle, "Current vertex"));
            document.Selection.Clear();
            Assert.Equal(1, document.Selection.CurrentVertex);
        }

        [Fact]
        public void APolylinesGlobalWidthElevationClosedAndLinetypeGenerationAreSet()
        {
            CadDocument document = Kinds();
            string handle = Handle<DxfPolyline>(document);

            Set(document, handle, "Global width", "1.5");
            Set(document, handle, "Elevation", "100");
            Set(document, handle, "Closed", "No");
            Set(document, handle, "Linetype generation", "Enabled");

            var polyline = Entity<DxfPolyline>(document, handle);
            Assert.Equal(1.5, polyline.ConstantWidth);
            Assert.Equal(100.0, polyline.Elevation);
            Assert.False(polyline.Closed);
            Assert.True(polyline.Plinegen);
            Assert.Equal("Enabled", Shown(document, handle, "Linetype generation"));
            Assert.Equal("80.0000", Shown(document, handle, "Length"));
        }

        [Fact]
        public void AnEllipsesRadiiAreSet_TheMajorScalingItWhole_AndAMinorPastTheMajorIsRefused()
        {
            CadDocument document = Kinds();
            string handle = Handle<DxfEllipse>(document);

            Set(document, handle, "Major radius", "40");
            Assert.Equal("20.0000", Shown(document, handle, "Minor radius"));

            Set(document, handle, "Minor radius", "10");
            Assert.Equal("0.2500", Shown(document, handle, "Radius ratio"));

            Assert.True(CadPropertyEditor.Set(document, new[] { handle }, "Minor radius", "50").Refused);
            Assert.Equal("10.0000", Shown(document, handle, "Minor radius"));

            Set(document, handle, "Center X", "120");
            Near(new GeoPoint3(120, 0, 0), Entity<DxfEllipse>(document, handle).Center);
        }

        [Fact]
        public void AHatchsSpacingTypePatternIslandsBackgroundAndOriginAreSet_AndAScaleOfAPatternOfItsOwnIsRefused()
        {
            CadDocument document = Kinds();
            string handle = Handle<DxfHatch>(document);

            Assert.True(CadProperties.Describe(document, null, new[] { handle }).Groups.SelectMany(g => g.Rows).Single(r => r.Label == "Scale").IsReadOnly);
            Assert.True(CadPropertyEditor.Set(document, new[] { handle }, "Scale", "2").Refused);

            Set(document, handle, "Spacing", "5");
            Set(document, handle, "Double", "Yes");
            Set(document, handle, "Island detection", "Outer");
            Set(document, handle, "Background color", "Red");

            var hatch = Entity<DxfHatch>(document, handle);
            Assert.Equal(5.0, hatch.PatternSpace);
            Assert.True(hatch.PatternDouble);
            Assert.Equal(DxfHatchStyle.Outer, hatch.HatchStyle);
            Assert.Equal("Red", Shown(document, handle, "Background color"));

            Set(document, handle, "Type", "Predefined");
            Set(document, handle, "Pattern name", "ANSI31");
            Set(document, handle, "Scale", "2");

            Assert.Equal("ANSI31", Shown(document, handle, "Pattern name"));
            Assert.Equal("Predefined", Shown(document, handle, "Type"));
            Assert.Equal("2.0000", Shown(document, handle, "Scale"));

            // ANSI31's two lines 3.175 apart, as the drawing has no pattern files to give them: the origin moves both.
            hatch = Entity<DxfHatch>(document, handle);
            hatch.SetPatternDefinitions(new[] { new DxfPatternDefinition(Math.PI / 4, 0, 0, -2.245, 2.245, Array.Empty<double>()), new DxfPatternDefinition(Math.PI / 4, 1, 0, -2.245, 2.245, Array.Empty<double>()) });
            Set(document, handle, "Origin X", "2");

            hatch = Entity<DxfHatch>(document, handle);
            Assert.Equal(2.0, hatch.GetPatternDefinitionAt(0).BaseX, 9);
            Assert.Equal(3.0, hatch.GetPatternDefinitionAt(1).BaseX, 9);
            Assert.Equal("2.0000", Shown(document, handle, "Origin X"));
            AssertReopens(document);
        }

        [Fact]
        public void TheRowsOfADimensionAreWrittenAsOverrides_ItsPictureDrawnAgain_AndItsStyleLeftAsItIs()
        {
            CadDocument document = Kinds();
            string handle = Handle<DxfRotatedDimension>(document);
            var dimension = Entity<DxfRotatedDimension>(document, handle);
            string style = dimension.DimensionStyleName;
            string Words() => Entity<DxfRotatedDimension>(document, handle).DimBlock.OfType<DxfMText>().Single().Contents;
            string before = Words();

            Set(document, handle, "Suppress trailing zeros", "No");
            Set(document, handle, "Precision", "0.000");
            Set(document, handle, "Dim suffix", " TYP");
            Set(document, handle, "Arrow 1", "Architectural tick");
            Set(document, handle, "Text pos vert", "Above");

            dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.Equal("300.000 TYP", Words());
            Assert.Equal(3, dimension.Dimdec);
            Assert.True(dimension.Dimsah);
            Assert.Equal("Architectural tick", CadArrowheads.NameOf(dimension.Dimblk1));
            Assert.Equal("Architectural tick", Shown(document, handle, "Arrow 1"));
            Assert.Equal(CadArrowheads.NameOf(document.Database.DimStyleTable[style].Dimblk), Shown(document, handle, "Arrow 2"));
            Assert.Equal("Above", Shown(document, handle, "Text pos vert"));
            Assert.Equal(style, dimension.DimensionStyleName);
            Assert.NotEqual(3, document.Database.DimStyleTable[style].Dimdec);

            for (int i = 0; i < 5; i++)
            {
                Assert.True(document.Undo());
            }

            Assert.Equal(before, Words());
            AssertReopens(document);
        }

        [Fact]
        public void AnMTextsStyleJustifyLineSpacingWidthAndColumnsAreSet()
        {
            CadDocument document = Kinds();
            string handle = Handle<DxfMText>(document);

            Set(document, handle, "Justify", "Middle center");
            Set(document, handle, "Line space factor", "1.5");
            Set(document, handle, "Line space style", "Exactly");
            Set(document, handle, "Defined width", "60");
            Set(document, handle, "Columns", "Static");
            Set(document, handle, "Style", "Standard");

            var mtext = Entity<DxfMText>(document, handle);
            Assert.Equal(DxfAttachmentPoint.MiddleCenter, mtext.Attachment);
            Assert.Equal(1.5, mtext.LineSpacingFactor, 9);
            Assert.Equal(DxfLineSpacingStyle.Exactly, mtext.LineSpacingStyle);
            Assert.Equal(DxfColumnType.StaticColumns, mtext.ColumnType);
            Assert.Equal(2, mtext.ColumnCount);
            Assert.True(CadPropertyEditor.Set(document, new[] { handle }, "Style", "NoSuchStyle").Refused);
            AssertReopens(document);
        }

        [Fact]
        public void AnAttributeOfABlockReferenceIsARowByItsTag_AndItsNameIsReadOnly()
        {
            CadDocument document = Kinds();
            string handle = Handle<DxfBlockReference>(document);
            CadSelectionInfo info = CadProperties.Describe(document, null, new[] { handle });

            Assert.Equal("B-12", info.Groups.Single(g => g.Title == "Attributes").Rows.Single(r => r.Label == "MARK").Value);
            Assert.True(info.Groups.SelectMany(g => g.Rows).Single(r => r.Label == "Name").IsReadOnly);

            Set(document, handle, "MARK", "B-13");
            Set(document, handle, "Rotation", "90");
            Set(document, handle, "Scale X", "2");

            var insert = Entity<DxfBlockReference>(document, handle);
            Assert.Equal("B-13", insert.AttributeCollection.Single().TextString);
            Assert.Equal(Math.PI / 2, insert.Rotation, 9);
            Assert.Equal(2.0, insert.ScaleFactors.X);
            AssertReopens(document);
        }

        [Fact]
        public void AViewportsStandardScaleLockAndSizeAreSet_TheScaleKeptAsItGrows()
        {
            CadDocument document = LayoutTests.TwoViewports();
            CadSpace plan = document.Spaces.Single(s => s.Name == "Plan");
            string handle = document.GetDisplay(plan).Items.OfType<DisplayViewport2>().OrderBy(v => v.CenterX).First().Handle;

            Set(document, handle, "Standard scale", "1:50");
            Set(document, handle, "Display locked", "Yes");
            Set(document, handle, "Height", "200");

            var viewport = Entity<DxfViewport>(document, handle);
            Assert.Equal(0.02, viewport.CustomScale, 12);
            Assert.True(viewport.Locked);
            Assert.Equal(200.0, viewport.Height);
            Assert.Equal("1:50", Shown(document, handle, "Standard scale"));
            Assert.Equal("No", Shown(document, handle, "Clipped"));
        }

        [Fact]
        public void AMultileadersTextLeaderTypeAndLandingAreSet()
        {
            CadDocument document = Kinds();
            string handle = Handle<DxfMLeader>(document);

            Set(document, handle, "Contents", "B-13");
            Set(document, handle, "Text height", "5");
            Set(document, handle, "Leader type", "Spline");
            Set(document, handle, "Landing distance", "12");

            var mleader = Entity<DxfMLeader>(document, handle);
            Assert.Equal("B-13", mleader.MText.Text);
            Assert.Equal(5.0, mleader.TextHeight);
            Assert.Equal(DxfLeaderType.SplineLeader, mleader.LeaderLineType);
            Assert.Equal(12.0, mleader.DoglegLength);
            Assert.Equal("Standard", Shown(document, handle, "Multileader style"));
            AssertReopens(document);
        }

        [Fact]
        public void TwoPolylinesOfDifferentElevationsShowItVarying()
        {
            CadDocument document = Kinds();
            string first = Handle<DxfPolyline>(document);
            Set(document, first, "Elevation", "5");
            CadCommandSession session = Session(document);
            Run(session, "COPY", new[] { first }, Enter, P(0, 0), P(0, 50), Enter);
            string second = All<DxfPolyline>(document.ModelSpace)[1].Handle;
            Set(document, second, "Elevation", "7");

            CadSelectionInfo info = CadProperties.Describe(document, null, new[] { first, second });

            Assert.Equal(CadPropertyRow.VariesText, info.Groups.SelectMany(g => g.Rows).Single(r => r.Label == "Elevation").Value);
            Assert.Equal("Polyline (2)", info.Label);
        }

        [Fact]
        public void MatchPropAsksForItsSettings_AndCopiesOnlyThoseChecked()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            Entity<DxfLine>(document, "A1").Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3);
            int asked = 0;
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "MATCHPROP")
                {
                    asked++;
                    session.MatchProperties = CadMatchProperties.Layer;
                    e.Handled = true;
                }
            };

            Run(session, "MA", P(50, 0));
            Assert.Equal("Select destination object(s) or [Settings]:", session.PromptText);
            Run(session, "S");
            Assert.Equal(1, asked);
            Assert.Equal("Current active settings: Layer", session.History.Last());
            Run(session, new[] { "A2" }, Enter);

            Assert.Equal("Steel", Entity<DxfLine>(document, "A2").Layer);
            Assert.True(Entity<DxfLine>(document, "A2").Color.IsByLayer);
        }

        [Fact]
        public void MatchPropCopiesTheSpecialPropertiesOfTextHatchPolylineAndDimension()
        {
            CadDocument document = Kinds();
            CadCommandSession session = Session(document);
            DxfDatabase db = document.Database;

            string dimension = Handle<DxfRotatedDimension>(document);
            Set(document, dimension, "Precision", "0.000");
            Set(document, dimension, "Suppress trailing zeros", "No");
            Run(session, "COPY", new[] { dimension }, Enter, P(0, 0), P(0, -100), Enter);
            string copy = All<DxfRotatedDimension>(document.ModelSpace)[1].Handle;
            Set(document, copy, "Precision", "0");

            string polyline = Handle<DxfPolyline>(document);
            Set(document, polyline, "Global width", "2");
            Set(document, polyline, "Linetype generation", "Enabled");
            Run(session, "COPY", new[] { polyline }, Enter, P(0, 0), P(0, 300), Enter);
            string other = All<DxfPolyline>(document.ModelSpace)[1].Handle;
            Set(document, other, "Global width", "0");
            Set(document, other, "Linetype generation", "Disabled");

            string hatch = Handle<DxfHatch>(document);
            Run(session, "COPY", new[] { hatch }, Enter, P(0, 0), P(0, 300), Enter);
            string fill = All<DxfHatch>(document.ModelSpace)[1].Handle;
            Set(document, hatch, "Spacing", "6");
            Set(document, hatch, "Angle", "45");

            DxfDBText text = All<DxfDBText>(document.ModelSpace).Single(t => !(t is DxfAttributeDefinition));
            string mtext = Handle<DxfMText>(document);

            Run(session, "MA", new[] { dimension }, new[] { copy }, Enter);
            Run(session, "MA", new[] { polyline }, new[] { other }, Enter);
            Run(session, "MA", new[] { hatch }, new[] { fill }, Enter);
            Run(session, "MA", new[] { text.Handle }, new[] { mtext }, Enter);

            Assert.Equal(3, Entity<DxfRotatedDimension>(document, copy).Dimdec);
            Assert.Equal("300.000", Entity<DxfRotatedDimension>(document, copy).DimBlock.OfType<DxfMText>().Single().Contents);
            Assert.Equal(2.0, Entity<DxfPolyline>(document, other).ConstantWidth);
            Assert.True(Entity<DxfPolyline>(document, other).Plinegen);
            Assert.Equal(6.0, Entity<DxfHatch>(document, fill).PatternSpace);
            Assert.Equal(Math.PI / 4, Entity<DxfHatch>(document, fill).PatternAngle, 9);
            Assert.Equal(7.0, Entity<DxfMText>(document, mtext).TextHeight);
            Assert.Equal("MATCHPROP", document.NextUndo.Name);
            AssertReopens(document);
        }

        [Fact]
        public void MatchPropCopiesAViewportsScaleLockAndFrozenLayers()
        {
            CadDocument document = LayoutTests.TwoViewports();
            CadSpace plan = document.Spaces.Single(s => s.Name == "Plan");
            List<string> handles = document.GetDisplay(plan).Items.OfType<DisplayViewport2>().OrderBy(v => v.CenterX).Select(v => v.Handle).ToList();
            Set(document, handles[1], "Display locked", "Yes");
            var session = new CadCommandSession(document) { PickAperture = 1.0 };

            Run(session, "MA", new[] { handles[1] }, new[] { handles[0] }, Enter);

            var target = Entity<DxfViewport>(document, handles[0]);
            var source = Entity<DxfViewport>(document, handles[1]);
            Assert.Equal(source.CustomScale, target.CustomScale, 12);
            Assert.True(target.Locked);
            Assert.Equal(new[] { "Notes" }, target.GetFrozenLayers().Select(l => l.Name).ToArray());
        }
    }
}
