using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>C14: DIMCONTINUE, DIMBASELINE, QDIM, DIMEDIT, DIMTEDIT, DIMSPACE and DIMBREAK, run in a session with no window.</summary>
    public class DimensionEditTests
    {
        private sealed class MoveOnlyOwner : ICadEntityOwner
        {
            public string Name => "Test.Model";

            public CadEditRule GetEditRule(DxfEntity entity) => CadEditRule.MoveOnly;

            public bool TryApply(CadDocument document, CadEdit edit) => false;
        }

        /// <summary>A blank drawing with a level dimension of 100 from (0, 0) to (100, 0), its line 30 below.</summary>
        private static CadDocument WithDimension(out CadCommandSession session)
        {
            CadDocument document = Blank();
            document.Database.DimStyleTable["Standard"].Dimtad = 1;
            session = Session(document);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -30));
            return document;
        }

        private static DxfDimStyleTableRecord Style(DxfDimension dimension) => dimension.GetDimstyleData();

        private static double Scale(DxfDimStyleTableRecord style) => style.Dimscale > 0.0 ? style.Dimscale : 1.0;

        private static List<DxfLine> PictureLines(DxfDimension dimension)
            => dimension.DimBlock.OfType<DxfLine>().Where(l => !l.IsErased && l.Layer != "Defpoints").ToList();

        [Fact]
        public void DimContinue_GoesOnFromTheLastDimensionsSecondExtensionLine_OnTheSameLine()
        {
            CadDocument document = WithDimension(out CadCommandSession session);
            string before = document.Database.ToText();

            Run(session, "DCO");
            Assert.Equal("Specify a second extension line origin or [Select/Undo] <Select>:", session.PromptText);
            Run(session, P(250, 0));
            Assert.StartsWith("Dimension text = 150", session.History.Last(), StringComparison.Ordinal);
            Run(session, P(300, 10), P(400, 0), "U");
            Run(session, Enter);
            Assert.Equal("Select continued dimension:", session.PromptText);
            Run(session, Enter);
            Assert.False(session.IsRunning);

            List<DxfRotatedDimension> dimensions = All<DxfRotatedDimension>(document.ModelSpace);
            Assert.Equal(3, dimensions.Count);
            Assert.Equal(150, dimensions[1].Measurement, 9);
            Assert.Equal(50, dimensions[2].Measurement, 9);
            Near(new GeoPoint3(100, 0, 0), dimensions[1].XLine1Point);
            Near(new GeoPoint3(250, 0, 0), dimensions[2].XLine1Point);
            Near(new GeoPoint3(250, -30, 0), dimensions[1].DimLinePoint);
            Near(new GeoPoint3(300, -30, 0), dimensions[2].DimLinePoint);
            Assert.NotEqual(dimensions[1].DimBlock.Name, dimensions[2].DimBlock.Name);
            Assert.Equal("DIMCONTINUE", document.NextUndo.Name);
            AssertReopens(document);

            // One undo takes the whole run back, to the byte.
            Assert.True(document.Undo());
            Assert.Equal(before, document.Database.ToText());
        }

        [Fact]
        public void DimBaseline_MeasuresFromTheBaseExtensionLine_EachLineDimdliFurtherOut()
        {
            CadDocument document = WithDimension(out CadCommandSession session);

            Run(session, "DBA", P(200, 0), P(300, 20), Enter, Enter);

            List<DxfRotatedDimension> dimensions = All<DxfRotatedDimension>(document.ModelSpace);
            Assert.Equal(3, dimensions.Count);
            double spacing = Style(dimensions[0]).Dimdli * Scale(Style(dimensions[0]));
            Assert.True(spacing > 0.0);
            Assert.Equal(200, dimensions[1].Measurement, 9);
            Assert.Equal(300, dimensions[2].Measurement, 9);
            Near(new GeoPoint3(0, 0, 0), dimensions[1].XLine1Point);
            Near(new GeoPoint3(0, 0, 0), dimensions[2].XLine1Point);
            Assert.Equal(-30 - spacing, dimensions[1].DimLinePoint.Y, 9);
            Assert.Equal(-30 - 2 * spacing, dimensions[2].DimLinePoint.Y, 9);
            Assert.Equal("DIMBASELINE", document.NextUndo.Name);
        }

        [Fact]
        public void DimContinue_SelectTakesTheExtensionLineNearerThePick_AndRefusesAnotherKind()
        {
            CadDocument document = WithDimension(out CadCommandSession session);
            Run(session, "DLI", P(0, 200), P(50, 200), P(25, 230));
            Run(session, "C", P(500, 0), "20");

            Run(session, "DCO", "S");
            Assert.Equal("Select continued dimension:", session.PromptText);
            Run(session, P(520, 0));
            Assert.Equal("Dimension must be linear, ordinate, or angular.", session.History.Last());

            // The first extension line of the first dimension, picked at (0, -15): the row goes on from (0, 0) to the left.
            Run(session, P(0, -15), P(-80, 0), Enter, Enter);

            DxfRotatedDimension made = All<DxfRotatedDimension>(document.ModelSpace).Last();
            Assert.Equal(80, made.Measurement, 9);
            Near(new GeoPoint3(0, 0, 0), made.XLine1Point);
            Assert.Equal(-30, made.DimLinePoint.Y, 9);
        }

        [Fact]
        public void DimContinue_OfAnAngularDimension_MeasuresTheNextAngleOnTheSameArc()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "DAN", Enter, P(0, 0), P(100, 0), P(100, 100), P(40, 15));

            Run(session, "DCO", P(0, 100), Enter, Enter);

            List<DxfPoint3AngularDimension> dimensions = All<DxfPoint3AngularDimension>(document.ModelSpace);
            Assert.Equal(2, dimensions.Count);
            Assert.Equal(Math.PI / 4.0, dimensions[1].Measurement, 9);
            Near(new GeoPoint3(0, 0, 0), dimensions[1].CenterPoint);
            DxfArc arc = dimensions[1].DimBlock.OfType<DxfArc>().Single();
            Assert.Equal(Math.Sqrt(40 * 40 + 15 * 15), arc.Radius, 9);
            Assert.Equal(Math.PI / 4.0, arc.StartAngle, 9);
            Assert.Equal(Math.PI / 2.0, arc.EndAngle, 9);
            AssertReopens(document);
        }

        [Fact]
        public void QDim_Continuous_DimensionsTheEndsInARow_AndBaselineAndStaggeredNestThem()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "L", P(0, 0), P(100, 0), P(100, 50), Enter, "L", P(250, 0), P(400, 50), Enter);
            string[] lines = All<DxfLine>(document.ModelSpace).Select(l => l.Handle).ToArray();

            Run(session, "QDIM", lines, Enter);
            Assert.Equal("Specify dimension line position, or [Continuous/Staggered/Baseline/Ordinate/Radius/Diameter/datumPoint/Edit/seTtings] <Continuous>:", session.PromptText);
            Run(session, P(100, -40));

            List<DxfRotatedDimension> row = All<DxfRotatedDimension>(document.ModelSpace);
            Assert.Equal(new[] { 100.0, 150.0, 150.0 }, row.Select(d => Math.Round(d.Measurement, 9)));
            Assert.All(row, d => Assert.Equal(-40, d.DimLinePoint.Y, 9));
            Assert.All(row, d => Assert.Equal(0.0, d.Rotation, 12));
            Assert.Equal("QDIM", document.NextUndo.Name);
            double spacing = Style(row[0]).Dimdli * Scale(Style(row[0]));

            Run(session, "QDIM", lines, Enter, "B", P(100, -40));
            List<DxfRotatedDimension> baseline = All<DxfRotatedDimension>(document.ModelSpace).Skip(3).ToList();
            Assert.Equal(new[] { 100.0, 250.0, 400.0 }, baseline.Select(d => Math.Round(d.Measurement, 9)));
            Assert.Equal(new[] { -40.0, -40.0 - spacing, -40.0 - 2 * spacing }, baseline.Select(d => Math.Round(d.DimLinePoint.Y, 9)));

            Run(session, "QDIM", lines, Enter, "S", P(100, -40));
            List<DxfRotatedDimension> staggered = All<DxfRotatedDimension>(document.ModelSpace).Skip(6).ToList();
            Assert.Equal(new[] { 150.0, 400.0 }, staggered.Select(d => Math.Round(d.Measurement, 9)));
            Assert.Equal(new[] { -40.0, -40.0 - spacing }, staggered.Select(d => Math.Round(d.DimLinePoint.Y, 9)));

            // Upright where the line is put to the right of the points.
            Run(session, "QDIM", lines, Enter, P(500, 25));
            Assert.All(All<DxfRotatedDimension>(document.ModelSpace).Skip(8), d => Assert.Equal(Math.PI / 2.0, d.Rotation, 12));
            Assert.Single(All<DxfRotatedDimension>(document.ModelSpace).Skip(8));
        }

        [Fact]
        public void QDim_RadiusDimensionsEveryCircle_AndEditTakesAPointOut()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "C", P(0, 0), "20", "C", P(100, 0), "30");
            string[] circles = All<DxfCircle>(document.ModelSpace).Select(l => l.Handle).ToArray();

            Run(session, "QDIM", circles, Enter, "R", P(50, 100));
            Assert.Equal(new[] { 20.0, 30.0 }, All<DxfRadialDimension>(document.ModelSpace).Select(d => Math.Round(d.Measurement, 9)));

            Run(session, "QDIM", circles, Enter, "E", P(100, 0), "X", "C", P(50, -50));
            Assert.Contains("One dimension point removed.", session.History);
            Assert.Empty(All<DxfRotatedDimension>(document.ModelSpace));
            Assert.Contains("No dimensions made: the geometry selected has nothing to dimension that way.", session.History);
        }

        [Fact]
        public void DimEdit_NewRotateObliqueAndHome_EachOneUndoStep()
        {
            CadDocument document = WithDimension(out CadCommandSession session);
            DxfRotatedDimension dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            string handle = dimension.Handle;
            string words = session.History.Last().Substring("Dimension text = ".Length);
            string start = document.Database.ToText();

            Run(session, "DED");
            Assert.Equal("Enter type of dimension editing [Home/New/Rotate/Oblique] <Home>:", session.PromptText);
            Run(session, "N", "<> TYP", new[] { handle }, Enter);
            dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.Equal("<> TYP", dimension.DimensionText);
            Assert.Equal(words + " TYP", dimension.DimBlock.OfType<DxfMText>().Single().Contents);
            Assert.Equal("DIMEDIT", document.NextUndo.Name);

            Run(session, "DIMEDIT", "R", "30", new[] { handle }, Enter);
            dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.Equal(Math.PI / 6.0, dimension.TextRotation, 9);
            Assert.Equal(Math.PI / 6.0, dimension.DimBlock.OfType<DxfMText>().Single().Rotation, 9);

            Run(session, "DIMEDIT", "O", new[] { handle }, Enter, "60");
            dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.Equal(Math.PI / 3.0, dimension.Oblique, 9);

            // The first extension line now runs from (0, 0) at 60 degrees to the line 30 below: down and to the left.
            var slant = new GeoVector2(Math.Cos(Math.PI / 3.0), Math.Sin(Math.PI / 3.0));
            Assert.Contains(PictureLines(dimension), l => Math.Abs(Math.Abs((l.EndPoint.X - l.StartPoint.X) * slant.Y - (l.EndPoint.Y - l.StartPoint.Y) * slant.X)) < 1E-9
                && Math.Abs(l.StartPoint.X * slant.Y - l.StartPoint.Y * slant.X) < 1E-9);
            Assert.Equal(100, dimension.Measurement, 9);

            Run(session, "DIMEDIT", Enter, new[] { handle }, Enter);
            dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.Equal(0.0, dimension.TextRotation, 12);
            Assert.True(dimension.UsingDefaultTextPosition);

            // Four runs, four steps: undoing them gives the drawing back to the byte.
            for (int i = 0; i < 4; i++)
            {
                Assert.True(document.Undo());
            }

            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void DimTedit_MovesTheWordsAndTheLineWithThem_LeftAndAngleAndHome()
        {
            CadDocument document = WithDimension(out CadCommandSession session);
            DxfRotatedDimension dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            string handle = dimension.Handle;
            DxfDimStyleTableRecord style = Style(dimension);
            Assert.Equal(0, dimension.Dimtmove);
            double lift = Math.Abs(style.Dimgap) * Scale(style) + dimension.Dimtxt * Scale(style) / 2.0;
            string start = document.Database.ToText();

            Run(session, "DIMTEDIT");
            Assert.Equal("Select dimension:", session.PromptText);
            Run(session, (object)new[] { handle });
            Assert.Equal("Specify new location for dimension text or [Left/Right/Center/Home/Angle]:", session.PromptText);
            Run(session, P(60, -50));

            dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.False(dimension.UsingDefaultTextPosition);
            Near(new GeoPoint3(60, -50, 0), dimension.TextPosition);
            Near(new GeoPoint3(100, -50 - lift, 0), dimension.DimLinePoint);
            Near(new GeoPoint3(60, -50, 0), dimension.DimBlock.OfType<DxfMText>().Single().Location);
            Assert.Equal("DIMTEDIT", document.NextUndo.Name);

            Run(session, "DIMTEDIT", new[] { handle }, "L");
            dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.True(dimension.TextPosition.X < 50);
            Assert.True(dimension.TextPosition.X > 0);

            Run(session, "DIMTEDIT", new[] { handle }, "A", "90");
            Assert.Equal(Math.PI / 2.0, Entity<DxfRotatedDimension>(document, handle).TextRotation, 9);

            Run(session, "DIMTEDIT", new[] { handle }, "H");
            Assert.True(Entity<DxfRotatedDimension>(document, handle).UsingDefaultTextPosition);

            for (int i = 0; i < 4; i++)
            {
                Assert.True(document.Undo());
            }

            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void DimSpace_PutsParallelDimensionsAValueApart_AndAutoTwiceTheTextHeight()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -30));
            Run(session, "DLI", P(0, 0), P(200, 0), P(50, -33));
            Run(session, "DLI", P(0, 0), P(300, 0), P(50, -70));
            Run(session, "DLI", P(0, 0), P(0, 50), P(-30, 25));
            List<DxfRotatedDimension> dimensions = All<DxfRotatedDimension>(document.ModelSpace);
            string[] others = dimensions.Skip(1).Select(d => d.Handle).ToArray();

            Run(session, "DIMSPACE", new[] { dimensions[0].Handle }, others, Enter);
            Assert.Equal("Enter value or [Auto] <Auto>:", session.PromptText);
            Run(session, "10");
            Assert.Contains("1 dimension is not parallel to the base dimension and stays where it is.", session.History);

            dimensions = All<DxfRotatedDimension>(document.ModelSpace);
            Assert.Equal(-30, dimensions[0].DimLinePoint.Y, 9);
            Assert.Equal(-40, dimensions[1].DimLinePoint.Y, 9);
            Assert.Equal(-50, dimensions[2].DimLinePoint.Y, 9);
            Assert.Equal(-30, dimensions[3].DimLinePoint.X, 9);

            Run(session, "DIMSPACE", new[] { dimensions[0].Handle }, others, Enter, Enter);
            dimensions = All<DxfRotatedDimension>(document.ModelSpace);
            double auto = 2.0 * dimensions[0].Dimtxt * Scale(Style(dimensions[0]));
            Assert.Equal(-30 - auto, dimensions[1].DimLinePoint.Y, 9);
            Assert.Equal(-30 - 2 * auto, dimensions[2].DimLinePoint.Y, 9);

            Run(session, "DIMSPACE", new[] { dimensions[0].Handle }, others, Enter, "0");
            Assert.All(All<DxfRotatedDimension>(document.ModelSpace).Take(3), d => Assert.Equal(-30, d.DimLinePoint.Y, 9));
        }

        [Fact]
        public void DimBreak_AutoGapsTheLinesWhereAnObjectCrosses_ManualBetweenTwoPoints_RemoveMakesThemWhole()
        {
            CadDocument document = WithDimension(out CadCommandSession session);
            Run(session, "L", P(50, -50), P(50, -10), Enter);
            DxfRotatedDimension dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            string handle = dimension.Handle;
            double size = 1.5 * Style(dimension).Dimasz * Scale(Style(dimension));
            string start = document.Database.ToText();

            Run(session, "DIMBREAK");
            Assert.Equal("Select dimension to add/remove break or [Multiple]:", session.PromptText);
            Run(session, (object)new[] { handle });
            Assert.Equal("Select object to break dimension or [Auto/Manual/Remove] <Auto>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("1 object modified", session.History.Last());

            List<DxfLine> onLine = PictureLines(Entity<DxfRotatedDimension>(document, handle)).Where(l => Math.Abs(l.StartPoint.Y + 30) < 1E-9 && Math.Abs(l.EndPoint.Y + 30) < 1E-9).OrderBy(l => Math.Min(l.StartPoint.X, l.EndPoint.X)).ToList();
            Assert.Equal(2, onLine.Count);
            Assert.Equal(50 - size / 2, Math.Max(onLine[0].StartPoint.X, onLine[0].EndPoint.X), 9);
            Assert.Equal(50 + size / 2, Math.Min(onLine[1].StartPoint.X, onLine[1].EndPoint.X), 9);
            Assert.Equal("DIMBREAK", document.NextUndo.Name);

            Run(session, "DIMBREAK", new[] { handle }, "R");
            Assert.Single(PictureLines(Entity<DxfRotatedDimension>(document, handle)), l => Math.Abs(l.StartPoint.Y + 30) < 1E-9 && Math.Abs(l.EndPoint.Y + 30) < 1E-9);

            Run(session, "DIMBREAK", new[] { handle }, "M", P(20, -30), P(30, -31));
            onLine = PictureLines(Entity<DxfRotatedDimension>(document, handle)).Where(l => Math.Abs(l.StartPoint.Y + 30) < 1E-9).OrderBy(l => Math.Min(l.StartPoint.X, l.EndPoint.X)).ToList();
            Assert.Equal(2, onLine.Count);
            Assert.Equal(20, Math.Max(onLine[0].StartPoint.X, onLine[0].EndPoint.X), 9);
            Assert.Equal(30, Math.Min(onLine[1].StartPoint.X, onLine[1].EndPoint.X), 9);

            Run(session, "DIMBREAK", "M", new[] { handle }, Enter, Enter);
            Assert.Equal("1 object modified", session.History.Last());
            AssertReopens(document);

            for (int i = 0; i < 4; i++)
            {
                Assert.True(document.Undo());
            }

            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void ADimensionOfTheModel_IsRefusedInWords_AndLeftAsItIs()
        {
            CadDocument document = WithDimension(out CadCommandSession session);
            DxfRotatedDimension dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            new CadOwnerTag("Test.Model", "VIEW-1").WriteTo(dimension);
            document.Owners.Register(new MoveOnlyOwner());
            string start = document.Database.ToText();

            Run(session, "DIMEDIT", "N", "X", new[] { dimension.Handle }, Enter);
            Assert.Contains("This dimension belongs to the model and can only be moved.", session.History);

            Run(session, "DIMTEDIT", new[] { dimension.Handle });
            Assert.False(session.IsRunning);
            Assert.Equal("This dimension belongs to the model and can only be moved.", session.History.Last());

            Run(session, "DIMBREAK", new[] { dimension.Handle });
            Assert.False(session.IsRunning);
            Run(session, "DIMSPACE", new[] { dimension.Handle }, new[] { dimension.Handle }, Enter, "5");
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void TheAliasesAreAutoCads()
        {
            CadCommandRegistry registry = Session(Blank()).Registry;

            Assert.Equal("DIMCONTINUE", registry.Find("DCO").Name);
            Assert.Equal("DIMCONTINUE", registry.Find("DIMCONT").Name);
            Assert.Equal("DIMBASELINE", registry.Find("DBA").Name);
            Assert.Equal("DIMBASELINE", registry.Find("DIMBASE").Name);
            Assert.Equal("DIMEDIT", registry.Find("DED").Name);
            Assert.Equal("DIMEDIT", registry.Find("DIMED").Name);
            Assert.Equal("QDIM", registry.Find("QDIM").Name);
            Assert.Equal("DIMTEDIT", registry.Find("DIMTEDIT").Name);
            Assert.Equal("DIMSPACE", registry.Find("DIMSPACE").Name);
            Assert.Equal("DIMBREAK", registry.Find("DIMBREAK").Name);
        }
    }
}
