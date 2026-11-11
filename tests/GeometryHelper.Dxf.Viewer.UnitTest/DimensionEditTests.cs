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

        private static List<DxfLine> PictureLines(DxfDimension dimension) =>
            dimension.DimBlock.OfType<DxfLine>().Where(l => !l.IsErased && l.Layer != "Defpoints").ToList();

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
            // On the same arc, from the back of one arrowhead to the back of the other, broken about the words.
            double radius = Math.Sqrt(40 * 40 + 15 * 15);
            double back = 2 * Math.Asin(dimensions[1].GetDimstyleData().Dimasz / (2 * radius));
            List<DxfArc> arcs = dimensions[1].DimBlock.OfType<DxfArc>().OrderBy(a => a.StartAngle).ToList();
            Assert.All(arcs, a => Assert.Equal(radius, a.Radius, 9));
            Assert.Equal(Math.PI / 4.0 + back, arcs[0].StartAngle, 9);
            Assert.Equal(Math.PI / 2.0 - back, arcs.Last().EndAngle, 9);
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
            Assert.Equal(
                "Specify dimension line position, or [Continuous/Staggered/Baseline/Ordinate/Radius/Diameter/datumPoint/Edit/seTtings] <Continuous>:",
                session.PromptText
            );
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
            Assert.Equal(
                new[] { -40.0, -40.0 - spacing, -40.0 - 2 * spacing },
                baseline.Select(d => Math.Round(d.DimLinePoint.Y, 9))
            );

            Run(session, "QDIM", lines, Enter, "S", P(100, -40));
            List<DxfRotatedDimension> staggered = All<DxfRotatedDimension>(document.ModelSpace).Skip(6).ToList();
            Assert.Equal(new[] { 150.0, 400.0 }, staggered.Select(d => Math.Round(d.Measurement, 9)));
            Assert.Equal(new[] { -40.0, -40.0 - spacing }, staggered.Select(d => Math.Round(d.DimLinePoint.Y, 9)));

            // Upright where the line is put to the right of the points.
            Run(session, "QDIM", lines, Enter, P(500, 25));
            Assert.All(All<DxfRotatedDimension>(document.ModelSpace).Skip(8), d =>
                Assert.Equal(Math.PI / 2.0, d.Rotation, 12)
            );
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
            Assert.Equal(
                new[] { 20.0, 30.0 },
                All<DxfRadialDimension>(document.ModelSpace).Select(d => Math.Round(d.Measurement, 9))
            );

            Run(session, "QDIM", circles, Enter, "E", P(100, 0), "X", "C", P(50, -50));
            Assert.Contains("One dimension point removed.", session.History);
            Assert.Empty(All<DxfRotatedDimension>(document.ModelSpace));
            Assert.Contains(
                "No dimensions made: the geometry selected has nothing to dimension that way.",
                session.History
            );
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
            Assert.Contains(PictureLines(dimension), l =>
                Math.Abs(
                    Math.Abs((l.EndPoint.X - l.StartPoint.X) * slant.Y - (l.EndPoint.Y - l.StartPoint.Y) * slant.X)
                ) < 1E-9
                && Math.Abs(l.StartPoint.X * slant.Y - l.StartPoint.Y * slant.X) < 1E-9
            );
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
        public void DimEdit_Oblique_KeepsTheAngleFromTheDimensionLine_AsAutoCad()
        {
            // AutoCAD 2021, DIMEDIT Oblique 60 on two dimensions, read back by .NET: the extension lines of each run at 60
            // degrees from the x axis, and Oblique is that angle less the dimension line's: 30 on a DIMLINEAR rotated 30, 33.435
            // on a DIMALIGNED from (0, 100) to (100, 150); -30 on a level one gives 330.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "DIMLINEAR", P(0, -100), P(100, -50), P(50, -20));
            Run(session, "DIMALIGNED", P(0, 100), P(100, 150), P(40, 170));
            Run(session, "DIMLINEAR", P(0, -300), P(100, -300), P(50, -280));
            List<DxfRotatedDimension> linear = All<DxfRotatedDimension>(document.ModelSpace);
            DxfRotatedDimension turned = linear[0];
            DxfAlignedDimension aligned = Assert.Single(All<DxfAlignedDimension>(document.ModelSpace));
            DxfRotatedDimension level = linear[1];

            // DIMLINEAR here has no Rotated option: the first is turned to 30 by hand, and DIMEDIT draws it anew.
            turned.Rotation = Math.PI / 6.0;
            turned.DimLinePoint = new GeoPoint3(74.509619, -5.849365, 0);

            Run(session, "DIMEDIT", "O", new[] { turned.Handle, aligned.Handle }, Enter, "60");
            Run(session, "DIMEDIT", "O", new[] { level.Handle }, Enter, "-30");
            turned = Entity<DxfRotatedDimension>(document, turned.Handle);
            aligned = Entity<DxfAlignedDimension>(document, aligned.Handle);
            level = Entity<DxfRotatedDimension>(document, level.Handle);

            Assert.Equal(Math.PI / 6.0, turned.Oblique, 9);
            Assert.Equal(Math.PI / 3.0 - Math.Atan2(50, 100), aligned.Oblique, 9);
            Assert.Equal(11.0 * Math.PI / 6.0, level.Oblique, 9);

            foreach (DxfDimension dimension in new DxfDimension[] { turned, aligned })
            {
                // Both extension lines run at 60 degrees from the x axis, as AutoCAD draws them.
                Assert.Equal(2, PictureLines(dimension).Count(l => Along(l, Math.PI / 3.0)));
            }

            Assert.Equal(2, PictureLines(level).Count(l => Along(l, -Math.PI / 6.0)));
        }

        private static bool Along(DxfLine line, double angle) =>
            Math.Abs(
                Math.Sin(Math.Atan2(line.EndPoint.Y - line.StartPoint.Y, line.EndPoint.X - line.StartPoint.X) - angle)
            ) < 1E-9;

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
            Assert.Equal(
                "Specify new location for dimension text or [Left/Right/Center/Home/Angle]:",
                session.PromptText
            );
            Run(session, P(60, -50));

            dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.False(dimension.UsingDefaultTextPosition);
            // AutoCAD 2021: the line through the point given, the words above it there by the gap and half their height.
            Near(new GeoPoint3(60, -50, 0), dimension.TextPosition);
            Near(new GeoPoint3(100, -50, 0), dimension.DimLinePoint);
            Near(new GeoPoint3(60, -50 + lift, 0), dimension.DimBlock.OfType<DxfMText>().Single().Location, 0.05);
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
            Assert.Contains(
                "1 dimension is not parallel to the base dimension and stays where it is.",
                session.History
            );

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
            Assert.All(All<DxfRotatedDimension>(document.ModelSpace).Take(3), d =>
                Assert.Equal(-30, d.DimLinePoint.Y, 9)
            );
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

            List<DxfLine> onLine = PictureLines(Entity<DxfRotatedDimension>(document, handle))
                .Where(l => Math.Abs(l.StartPoint.Y + 30) < 1E-9 && Math.Abs(l.EndPoint.Y + 30) < 1E-9)
                .OrderBy(l => Math.Min(l.StartPoint.X, l.EndPoint.X))
                .ToList();
            Assert.Equal(2, onLine.Count);
            Assert.Equal(50 - size / 2, Math.Max(onLine[0].StartPoint.X, onLine[0].EndPoint.X), 9);
            Assert.Equal(50 + size / 2, Math.Min(onLine[1].StartPoint.X, onLine[1].EndPoint.X), 9);
            Assert.Equal("DIMBREAK", document.NextUndo.Name);

            Run(session, "DIMBREAK", new[] { handle }, "R");
            Assert.Single(PictureLines(Entity<DxfRotatedDimension>(document, handle)), l =>
                Math.Abs(l.StartPoint.Y + 30) < 1E-9 && Math.Abs(l.EndPoint.Y + 30) < 1E-9
            );

            Run(session, "DIMBREAK", new[] { handle }, "M", P(20, -30), P(30, -31));
            onLine = PictureLines(Entity<DxfRotatedDimension>(document, handle))
                .Where(l => Math.Abs(l.StartPoint.Y + 30) < 1E-9)
                .OrderBy(l => Math.Min(l.StartPoint.X, l.EndPoint.X))
                .ToList();
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
        public void DimBreak_KeepsItsBreaksAsAutoCadDoes_AndBreaksTheDimensionAgainWhenItIsDrawnAnew()
        {
            // AutoCAD 2021 keeps DIMBREAK's breaks in a BREAKDATA under ACAD_BREAKDATA in the dimension's extension dictionary:
            // Auto one AcDbBreakPointRef of 71 0, 91 -1 and the object in 331 for each object crossing it, the object naming the
            // BREAKDATA among its reactors; Manual one of 71 2, the points as picked and the line in 91 (0 the first extension
            // line, 3 the dimension line); Remove takes it out, the extension dictionary and the reactors with it.
            CadDocument document = WithDimension(out CadCommandSession session);
            Run(session, "L", P(-10, -15), P(110, -15), Enter);
            Run(session, "L", P(50, -50), P(50, -10), Enter);
            Run(session, "L", P(300, 0), P(300, 10), Enter);
            List<DxfLine> lines = All<DxfLine>(document.ModelSpace);
            DxfRotatedDimension dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            string handle = dimension.Handle;

            Run(session, "DIMBREAK", new[] { handle }, Enter);
            List<KeyValuePair<int, string>> data = BreakData(document);
            string breakData = Value(data, 5);
            Assert.Equal(
                new[] { "70 0", "330 " + handle, "90 2" }
                    .Concat(Reference("0", "-1", "0", "0", "0", "0"))
                    .Concat(new[] { "331 " + lines[0].Handle, "73 2", "92 0" })
                    .Concat(Reference("0", "-1", "0", "0", "0", "0"))
                    .Concat(new[] { "331 " + lines[1].Handle, "73 2", "92 0" }),
                AfterMarker(data, "AcDbBreakData")
            );
            Assert.Contains(breakData, lines[0].GetPersistentReactorIds());
            Assert.Contains(breakData, lines[1].GetPersistentReactorIds());
            Assert.Empty(lines[2].GetPersistentReactorIds());

            // DIMEDIT draws the picture anew: the breaks come back, the dimension line in two about x 50 and each extension
            // line in two about y -15.
            Run(session, "DIMEDIT", "N", "<> X", new[] { handle }, Enter);
            dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.Equal(
                2,
                PictureLines(dimension)
                    .Count(l => Math.Abs(l.StartPoint.Y + 30) < 1E-9 && Math.Abs(l.EndPoint.Y + 30) < 1E-9)
            );
            Assert.Equal(
                2,
                PictureLines(dimension).Count(l => Math.Abs(l.StartPoint.X) < 1E-9 && Math.Abs(l.EndPoint.X) < 1E-9)
            );

            Run(session, "DIMBREAK", new[] { handle }, "R");
            Assert.Empty(BreakData(document));
            Assert.Null(Entity<DxfRotatedDimension>(document, handle).ExtensionDictionary);
            Assert.Empty(lines[0].GetPersistentReactorIds());
            Assert.Single(PictureLines(Entity<DxfRotatedDimension>(document, handle)), l =>
                Math.Abs(l.StartPoint.Y + 30) < 1E-9 && Math.Abs(l.EndPoint.Y + 30) < 1E-9
            );

            Run(session, "DIMBREAK", new[] { handle }, "M", P(20, -30), P(30, -31));
            Run(session, "DIMBREAK", new[] { handle }, "M", P(0, -5), P(0, -8));
            Assert.Equal(
                new[] { "70 0", "330 " + handle, "90 2" }
                    .Concat(Reference("2", "3", "20", "-30", "30", "-31"))
                    .Concat(Reference("2", "0", "0", "-5", "0", "-8")),
                AfterMarker(BreakData(document), "AcDbBreakData")
            );

            // Read back from the file, the breaks kept break the picture drawn anew again.
            CadDocument again = CadDocument.From(DxfDatabase.Parse(document.Database.ToText()), "again.dxf");
            CadCommandSession other = Session(again);
            Run(other, "DIMEDIT", "N", "<> Y", new[] { handle }, Enter);
            List<DxfLine> onLine = PictureLines(Entity<DxfRotatedDimension>(again, handle))
                .Where(l => Math.Abs(l.StartPoint.Y + 30) < 1E-9)
                .OrderBy(l => Math.Min(l.StartPoint.X, l.EndPoint.X))
                .ToList();
            Assert.Equal(2, onLine.Count);
            Assert.Equal(20, Math.Max(onLine[0].StartPoint.X, onLine[0].EndPoint.X), 9);
            Assert.Equal(30, Math.Min(onLine[1].StartPoint.X, onLine[1].EndPoint.X), 9);
            Assert.Equal(
                2,
                PictureLines(Entity<DxfRotatedDimension>(again, handle))
                    .Count(l => Math.Abs(l.StartPoint.X) < 1E-9 && Math.Abs(l.EndPoint.X) < 1E-9)
            );
            AssertReopens(again);
        }

        /// <summary>The groups of one AcDbBreakPointRef as AutoCAD 2021 writes them, from its marker to its second point (its reals written as the library writes them, 0 for 0.0).</summary>
        private static IEnumerable<string> Reference(
            string kind,
            string line,
            string x1,
            string y1,
            string x2,
            string y2
        ) =>
            new[]
            {
                "100 AcDbBreakPointRef",
                "71 " + kind,
                "72 0",
                "91 " + line,
                "10 " + x1,
                "20 " + y1,
                "30 0",
                "11 " + x2,
                "21 " + y2,
                "31 0",
            };

        /// <summary>The groups of the drawing's first BREAKDATA, as its text has them; none where it has none.</summary>
        private static List<KeyValuePair<int, string>> BreakData(CadDocument document)
        {
            string[] text = document.Database.ToText().Replace("\r\n", "\n").Split('\n');
            var groups = new List<KeyValuePair<int, string>>();

            for (int i = 0; i + 1 < text.Length; i += 2)
            {
                int code = int.Parse(text[i].Trim(), System.Globalization.CultureInfo.InvariantCulture);

                if (code == 0 && groups.Count > 0)
                {
                    break;
                }

                if (code == 0 && text[i + 1].Trim() == "BREAKDATA")
                {
                    groups.Add(new KeyValuePair<int, string>(code, "BREAKDATA"));
                }
                else if (groups.Count > 0)
                {
                    groups.Add(new KeyValuePair<int, string>(code, text[i + 1].Trim()));
                }
            }

            return groups;
        }

        private static string Value(List<KeyValuePair<int, string>> groups, int code) =>
            groups.First(g => g.Key == code).Value;

        private static List<string> AfterMarker(List<KeyValuePair<int, string>> groups, string marker) =>
            groups
                .SkipWhile(g => !(g.Key == 100 && g.Value == marker))
                .Skip(1)
                .Select(g => g.Key.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + g.Value)
                .ToList();

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
