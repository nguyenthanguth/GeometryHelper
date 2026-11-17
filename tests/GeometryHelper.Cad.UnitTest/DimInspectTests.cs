using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// DIMINSPECT and -DIMINSPECT: the questions and the data AutoCAD 2021 keeps (the application ACAD_DSTYLE_DIMINSPECT, the
    /// rate typed, the label, the flags 49, 50 and 36 for round, angular and none), and the frame drawn about the words of the
    /// dimension, with its dimension line cut where the frame begins.
    /// </summary>
    public class DimInspectTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static (CadDocument Document, CadCommandSession Session, DxfDimension Dimension) Dimensioned()
        {
            CadDocument document = Blank();

            // The words stand on the dimension line, which is broken at them.
            DxfDimStyleTableRecord style = document.Database.DimStyleTable[document.Database.Dimstyle];
            style.Dimtad = 0;
            document.Database.SetDimstyleData(style);
            CadCommandSession session = Start(document, "DIMLINEAR", "0,0", "100,0", "50,20");
            DxfDimension dimension = document.ModelSpace.Block.OfType<DxfDimension>().Single();
            return (document, session, dimension);
        }

        private static string Data(DxfDimension dimension) =>
            string.Join(
                ";",
                dimension
                    .GetXDataForApplication("ACAD_DSTYLE_DIMINSPECT")
                    .Skip(1)
                    .Select(g => g.TypeCode + "=" + g.Value)
            );

        private static List<T> Picture<T>(DxfDimension dimension)
            where T : DxfEntity => dimension.DimBlock.OfType<T>().ToList();

        [Fact]
        public void TheTypedForm_AsksAsAutoCadAsks_AndKeepsTheDataAsAutoCadKeepsIt()
        {
            var (document, session, dimension) = Dimensioned();
            Run(session, "-DIMINSPECT");
            Assert.Equal("Add inspection data or [Remove] <Add>:", session.PromptText);
            Run(session, "Add");
            Assert.Equal("Select dimensions:", session.PromptText);
            Run(session, new[] { dimension.Handle }, Enter);
            Assert.Equal("Enter shape option [Round/Angular/None] <Round>:", session.PromptText);
            Run(session, "Round");
            Assert.Equal("Enter label data or <None>:", session.PromptText);
            Run(session, "A");
            Assert.Equal("Enter inspection rate <100%>:", session.PromptText);
            Run(session, "50");

            Assert.False(session.IsRunning);
            Assert.Equal("1070=396;1000=50;1070=395;1000=A;1070=394;1070=49;1070=393;1070=1", Data(dimension));
            Assert.True(document.Undo());
            Assert.Null(
                ((DxfDimension)document.FindEntity(dimension.Handle)).GetXDataForApplication("ACAD_DSTYLE_DIMINSPECT")
            );
        }

        [Fact]
        public void AngularWithALabel_IsFlag50_AndNoneWithNoLabelIsFlag36_WithTheDefaultRate100Percent()
        {
            var (document, session, dimension) = Dimensioned();
            Run(session, "-DIMINSPECT", "A", new[] { dimension.Handle }, Enter, "Angular", "LBL", "25");
            Assert.Equal("1070=396;1000=25;1070=395;1000=LBL;1070=394;1070=50;1070=393;1070=1", Data(dimension));

            // Enter at the label keeps the label the dimension had, and shows none.
            Run(session, "-DIMINSPECT", Enter, new[] { dimension.Handle }, Enter, "None", Enter, Enter);
            Assert.Equal("1070=396;1000=100%;1070=395;1000=LBL;1070=394;1070=36;1070=393;1070=1", Data(dimension));
        }

        [Fact]
        public void ARoundInspectionWithALabelAndARate_IsFramedBetweenTwoDividersWithRoundEnds_CentredOnTheWords()
        {
            var (document, session, dimension) = Dimensioned();
            DxfMText wordsBefore = Picture<DxfMText>(dimension).Single();
            double middle = wordsBefore.Location.X;
            Run(session, "-DIMINSPECT", "A", new[] { dimension.Handle }, Enter, "Round", "A", "50");

            List<DxfArc> arcs = Picture<DxfArc>(dimension);
            List<DxfMText> texts = Picture<DxfMText>(dimension);
            Assert.Equal(2, arcs.Count);
            Assert.Equal(3, texts.Count);
            Assert.Contains(texts, t => t.Contents == "A");
            Assert.Contains(texts, t => t.Contents == "50");
            DxfArc left = arcs.OrderBy(a => a.Center.X).First();
            DxfArc right = arcs.OrderBy(a => a.Center.X).Last();
            Assert.Equal(left.Radius, right.Radius, 9);
            Assert.Equal(2.0 * middle, left.Center.X + right.Center.X, 9);
            Assert.Equal(wordsBefore.Location.Y, left.Center.Y, 9);

            // The frame: the two sides along the words, a divider after the label and one before the rate.
            var upright = Picture<DxfLine>(dimension)
                .Where(l => Math.Abs(l.StartPoint.X - l.EndPoint.X) < 1E-9)
                .ToList();
            var level = Picture<DxfLine>(dimension).Where(l => Math.Abs(l.StartPoint.Y - l.EndPoint.Y) < 1E-9).ToList();
            Assert.Equal(2, upright.Count(l => l.StartPoint.X > left.Center.X && l.StartPoint.X < right.Center.X));

            // The words stand between the dividers, a gap clear of each, the gaps equal.
            double[] dividers = upright
                .Where(l => l.StartPoint.X > left.Center.X && l.StartPoint.X < right.Center.X)
                .Select(l => l.StartPoint.X)
                .OrderBy(x => x)
                .ToArray();
            DxfMText words = texts.Single(t => t.Contents != "A" && t.Contents != "50");
            Assert.Equal((dividers[0] + dividers[1]) / 2.0, words.Location.X, 9);
            Assert.NotEqual(middle, words.Location.X);

            // The dimension line stops at the tips of the frame, which stand a radius beyond the centres of the arcs.
            List<DxfLine> onLine = level
                .Where(l =>
                    Math.Abs(l.StartPoint.Y - 20.0) < 1E-9
                    && Math.Min(l.StartPoint.X, l.EndPoint.X) is double lo
                    && (lo < 10.0 || Math.Max(l.StartPoint.X, l.EndPoint.X) > 90.0)
                )
                .ToList();
            Assert.Equal(2, onLine.Count);
            Assert.Equal(left.Center.X - left.Radius, onLine.Min(l => Math.Max(l.StartPoint.X, l.EndPoint.X)), 9);
            Assert.Equal(right.Center.X + right.Radius, onLine.Max(l => Math.Min(l.StartPoint.X, l.EndPoint.X)), 9);
        }

        [Fact]
        public void AnAngularInspection_HasPointedEnds_AndNoneHasNoFrameAtAll()
        {
            var (document, session, dimension) = Dimensioned();
            int linesBefore = Picture<DxfLine>(dimension).Count;
            Run(session, "-DIMINSPECT", "A", new[] { dimension.Handle }, Enter, "Angular", "LBL", "25");

            Assert.Empty(Picture<DxfArc>(dimension));
            Assert.Equal(3, Picture<DxfMText>(dimension).Count);
            int slanted = Picture<DxfLine>(dimension)
                .Count(l =>
                    Math.Abs(l.StartPoint.X - l.EndPoint.X) > 1E-9 && Math.Abs(l.StartPoint.Y - l.EndPoint.Y) > 1E-9
                );
            Assert.Equal(4, slanted);

            Run(session, "-DIMINSPECT", "A", new[] { dimension.Handle }, Enter, "None", Enter, Enter);
            Assert.Empty(Picture<DxfArc>(dimension));
            Assert.Equal(2, Picture<DxfMText>(dimension).Count);
            Assert.Contains(Picture<DxfMText>(dimension), t => t.Contents == "100%");
            Assert.Equal(linesBefore, Picture<DxfLine>(dimension).Count);
        }

        [Fact]
        public void RemoveTakesTheInspectionOut_AndTheDimensionIsDrawnAsItWas()
        {
            var (document, session, dimension) = Dimensioned();
            int partsBefore = dimension.DimBlock.Count();
            double xBefore = Picture<DxfMText>(dimension).Single().Location.X;

            Run(session, "-DIMINSPECT", "A", new[] { dimension.Handle }, Enter, "Round", "A", "50");
            Assert.NotEqual(partsBefore, dimension.DimBlock.Count());

            Run(session, "-DIMINSPECT", "R", new[] { dimension.Handle }, Enter);
            Assert.Equal("1 of inspection removed", session.History.Last());
            Assert.Equal(partsBefore, dimension.DimBlock.Count());
            Assert.Equal(xBefore, Picture<DxfMText>(dimension).Single().Location.X, 9);
            Assert.Null(CadDimInspectionOf(dimension));
        }

        private static object CadDimInspectionOf(DxfDimension dimension) =>
            dimension.GetXDataForApplication("ACAD_DSTYLE_DIMINSPECT") is IReadOnlyList<DxfTypedValue> groups
            && groups.Count > 1
                ? groups
                : null;

        [Fact]
        public void TheDialog_ShowsTheInspectionAndItsChoicesAreWritten_OrRemoved()
        {
            var (document, session, dimension) = Dimensioned();
            CadDimInspectRequest seen = null;
            session.UiRequested += (s, e) =>
            {
                if (e.Name != "DIMINSPECT")
                {
                    return;
                }

                seen = (CadDimInspectRequest)e.Payload;
                seen.Shape = CadInspectionShape.Angular;
                seen.ShowLabel = true;
                seen.Label = "X";
                seen.Rate = "75%";
                seen.Action = CadDialogAction.Ok;
                e.Handled = true;
            };

            Run(session, "DIMINSPECT", new[] { dimension.Handle }, Enter);

            Assert.NotNull(seen);
            Assert.Equal(1, seen.DimensionCount);
            Assert.False(seen.HasInspection);
            Assert.Equal("1070=396;1000=75%;1070=395;1000=X;1070=394;1070=50;1070=393;1070=1", Data(dimension));

            session.UiRequested += (s, e) =>
            {
                if (e.Name == "DIMINSPECT")
                {
                    var request = (CadDimInspectRequest)e.Payload;
                    Assert.True(request.HasInspection);
                    Assert.Equal(CadInspectionShape.Angular, request.Shape);
                    Assert.Equal("X", request.Label);
                    Assert.Equal("75%", request.Rate);
                    request.Remove = true;
                    request.Action = CadDialogAction.Ok;
                }
            };
            Run(session, "DIMINSPECT", new[] { dimension.Handle }, Enter);
            Assert.Equal("1 of inspection removed", session.History.Last());
            Assert.Null(CadDimInspectionOf(dimension));
        }

        [Fact]
        public void WithNoWindowTheDialogCommandPointsToTheTypedOne_AndNonDimensionsAreLeftOut()
        {
            var (document, session, dimension) = Dimensioned();
            Run(session, "DIMINSPECT", new[] { dimension.Handle }, Enter);
            Assert.StartsWith("DIMINSPECT is shown by the window", session.History.Last(), StringComparison.Ordinal);

            Run(session, "-DIMINSPECT", Enter, new string[0], Enter);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void AnInspectionDimensionSurvivesAReadFromItsText_WithItsFrame()
        {
            var (document, session, dimension) = Dimensioned();
            Run(session, "-DIMINSPECT", "A", new[] { dimension.Handle }, Enter, "Round", "A", "50");
            DxfDatabase again = DxfDatabase.Parse(document.Database.ToText());
            DxfDimension read = again.ModelSpace.OfType<DxfDimension>().Single();

            Assert.Equal(Data(dimension), Data(read));
            Assert.Equal(2, read.DimBlock.OfType<DxfArc>().Count());
        }
    }
}
