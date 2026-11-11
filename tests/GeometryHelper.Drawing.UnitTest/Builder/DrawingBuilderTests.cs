using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using GeometryHelper.Packing;
using Xunit;
using Xunit.Abstractions;
using static GeometryHelper.Drawing.UnitTest.Builder.BuilderKit;

namespace GeometryHelper.Drawing.UnitTest.Builder
{
    public class DrawingBuilderTests
    {
        private readonly ITestOutputHelper _output;

        public DrawingBuilderTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static double Left(GeoRectangle2 box) => box.Center.X - box.Width / 2;

        private static double Right(GeoRectangle2 box) => box.Center.X + box.Width / 2;

        private static double Bottom(GeoRectangle2 box) => box.Center.Y - box.Height / 2;

        private static double Top(GeoRectangle2 box) => box.Center.Y + box.Height / 2;

        /// <summary>Asserts that no two boxes on the sheet overlap and each stands in the frame, clear of the title block.</summary>
        private static void AssertLaidOut(Drawing drawing)
        {
            var boxes = drawing.Views.Select(v => (v.Id, Box: v.SheetBox)).ToList();

            if (drawing.Sheet.PartListBox.HasValue)
            {
                boxes.Add(("part list", drawing.Sheet.PartListBox.Value));
            }

            double tol = Tol.EqualPoint;
            GeoRectangle2 frame = drawing.Sheet.Frame;

            foreach ((string id, GeoRectangle2 box) in boxes)
            {
                Assert.True(
                    Left(box) >= Left(frame) - tol && Right(box) <= Right(frame) + tol && Top(box) <= Top(frame) + tol,
                    id + " runs off the frame."
                );
                Assert.True(
                    Bottom(box) >= Top(drawing.Sheet.TitleBlockBox) - tol,
                    id + " runs into the strip of the title block."
                );
            }

            for (int i = 0; i < boxes.Count; i++)
            {
                for (int j = i + 1; j < boxes.Count; j++)
                {
                    GeoRectangle2 a = boxes[i].Box,
                        b = boxes[j].Box;
                    bool apart =
                        Right(a) <= Left(b) + tol
                        || Right(b) <= Left(a) + tol
                        || Top(a) <= Bottom(b) + tol
                        || Top(b) <= Bottom(a) + tol;
                    Assert.True(apart, boxes[i].Id + " overlaps " + boxes[j].Id + ".");
                }
            }
        }

        [Fact]
        public void TC2WithTheAssemblyPresetHasItsThreeViewsAtOneScaleAndItsOverallDimensionOf3000()
        {
            Drawing drawing = DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol);

            foreach (DrawingView view in drawing.Views)
            {
                _output.WriteLine(
                    $"{view.Id} {view.Title} box {view.SheetBox.Center.X:0.#},{view.SheetBox.Center.Y:0.#} {view.SheetBox.Width:0.#}x{view.SheetBox.Height:0.#} dims {view.Dimensions.Count} marks {view.Marks.Count}"
                );
            }

            Assert.Equal(new[] { "V-FRONT", "V-TOP", "V-RIGHT" }, drawing.Views.Select(v => v.Id));
            Assert.Equal("TC2-beam", drawing.Id);
            Assert.Equal(10.0, drawing.Scale);
            Assert.All(drawing.Views, v => Assert.Equal(10.0, v.Scale));
            Assert.Equal("A3", drawing.Sheet.SizeName);
            Assert.Empty(drawing.Warnings);

            DrawingView front = drawing.Views[0];
            int overall = front.DimensionIds.ToList().IndexOf("V-FRONT/D-Overall-X-1");
            Assert.True(overall >= 0, string.Join(", ", front.DimensionIds));
            Assert.Equal("3000", front.Dimensions[overall].Text);
            Assert.Equal(3000.0, front.Dimensions[overall].Value, 6);
            Assert.NotEmpty(drawing.Views[1].Dimensions);
            Assert.Empty(drawing.Views[2].Dimensions);
            AssertLaidOut(drawing);
        }

        [Fact]
        public void InThirdAngleTheTopViewStandsAboveTheFrontAndTheRightOnItsRightAndInFirstAngleTheOtherWay()
        {
            Drawing third = DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol);
            DrawingSettings settings = AssemblyOnA3();
            settings.ProjectionMethod = ProjectionMethod.FirstAngle;
            Drawing first = DrawingBuilder.Build(TC2(), settings, Tol);

            GeoRectangle2 front = third.Views[0].SheetBox,
                top = third.Views[1].SheetBox,
                right = third.Views[2].SheetBox;
            Assert.True(Bottom(top) >= Top(front));
            Assert.True(Left(right) >= Right(front));

            // The views of a column share the front view's X, those of a row its Y: the beam's start is at one X in front and top.
            Assert.Equal(third.Views[0].SheetOrigin.X, third.Views[1].SheetOrigin.X, 9);
            Assert.Equal(third.Views[0].SheetOrigin.Y, third.Views[2].SheetOrigin.Y, 9);

            front = first.Views[0].SheetBox;
            top = first.Views[1].SheetBox;
            right = first.Views[2].SheetBox;
            Assert.True(Top(top) <= Bottom(front));
            Assert.True(Right(right) <= Left(front));
            AssertLaidOut(first);
        }

        [Fact]
        public void TheViewsAlongTheAxesStayRoundTheFrontViewWhenTheirBlockIsAsWideAsTheSheet()
        {
            // Five column bases 6 000 apart: at 1:50 the front view and the end view on its right are a hair too wide for an
            // A2, and the packer would split them; the drawing takes the next scale instead.
            Drawing drawing = DrawingBuilder.Build(TestBodies.TC10(5, 1).ToSource(), DrawingSettings.Assembly, Tol);
            DrawingView front = drawing.Views.Single(v => v.Kind == ViewKind.Front);
            DrawingView right = drawing.Views.Single(v => v.Kind == ViewKind.Right);
            DrawingView top = drawing.Views.Single(v => v.Kind == ViewKind.Top);

            Assert.Equal(front.SheetOrigin.Y, right.SheetOrigin.Y, 9);
            Assert.True(Left(right.SheetBox) >= Right(front.SheetBox));
            Assert.Equal(front.SheetOrigin.X, top.SheetOrigin.X, 9);
            Assert.True(Bottom(top.SheetBox) >= Top(front.SheetBox));
            AssertLaidOut(drawing);
        }

        [Fact]
        public void NoViewOverlapsAnotherOrRunsOffTheSheetForEveryPresetAndTestBody()
        {
            DrawingSettings withSections = DrawingSettings.Part;
            withSections.Views.Details.Add(
                new DetailViewRequest("B", ViewKind.Front, new GeoRectangle2(2800, -200, 300, 400), 4)
            );
            var sources = new[]
            {
                ("TC2", TC2(extra: true)),
                ("TC1", TestBodies.TC1().ToSource()),
                ("TC5", TestBodies.TC5().ToSource()),
                ("TC6", TestBodies.TC6().ToSource()),
            };

            foreach ((string name, DrawingSource source) in sources)
            {
                foreach (
                    (string preset, DrawingSettings settings) in new[]
                    {
                        ("Part", withSections),
                        ("Assembly", DrawingSettings.Assembly),
                        ("General", DrawingSettings.General),
                    }
                )
                {
                    Drawing drawing = DrawingBuilder.Build(source, settings, Tol);
                    _output.WriteLine(
                        $"{name} {preset}: {drawing.Views.Count} views at 1:{drawing.Scale} on {drawing.Sheet.SizeName}, {drawing.Warnings.Count} warnings"
                    );
                    Assert.Empty(drawing.Warnings);
                    AssertLaidOut(drawing);
                }
            }
        }

        [Fact]
        public void TheIdsAreTheSameEveryTimeAndAreMadeFromTheSourceAndTheKindsOfView()
        {
            DrawingSettings settings = AssemblyOnA3();
            settings.Views.Sections.Add(new SectionViewRequest("A", ViewKind.Right, 0.5));

            Drawing one = DrawingBuilder.Build(TC2(), settings, Tol);
            Drawing two = DrawingBuilder.Build(TC2(), settings, Tol);

            Assert.Equal(one.Elements.Select(e => e.Id), two.Elements.Select(e => e.Id));
            Assert.Equal(one.Elements, two.Elements);
            Assert.Equal(
                new[] { "V-FRONT", "V-TOP", "V-RIGHT", "V-SECTION-A" },
                one.Elements.Where(e => e.Kind == "View").Select(e => e.Id)
            );
            Assert.Contains(one.Elements, e =>
                e.Id == "V-FRONT/M-TC2-endplate" && e.Kind == "Mark" && e.ViewId == "V-FRONT"
            );
            Assert.Contains(one.Elements, e => e.Id == "V-FRONT/D-Overall-X-1" && e.Kind == "Dimension");
            Assert.Equal(
                new[] { "GH-SETTINGS", "GH-SHEET", "GH-TITLE", "GH-PARTLIST" },
                one.Elements.Where(e => e.ViewId == null && e.Kind != "View").Select(e => e.Id)
            );
            Assert.Equal(one.Elements.Count, one.Elements.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void ThePresetsDifferAsTheSettingsGuideSays()
        {
            Drawing part = DrawingBuilder.Build(TC2(), DrawingSettings.Part, Tol);
            Drawing assembly = DrawingBuilder.Build(TC2(), DrawingSettings.Assembly, Tol);
            Drawing general = DrawingBuilder.Build(TC2(), DrawingSettings.General, Tol);

            Assert.Equal(new[] { "V-FRONT", "V-TOP", "V-RIGHT", "V-SECTION-A" }, part.Views.Select(v => v.Id));
            Assert.NotEmpty(part.Views.Single(v => v.Id == "V-RIGHT").Dimensions);
            Assert.Equal(new[] { "TC2-beam" }, part.Views.SelectMany(v => v.Marks).Select(m => m.ObjectId));
            Assert.Equal("A3", part.Sheet.SizeName);

            Assert.Equal(new[] { "TC2-endplate" }, assembly.Views.SelectMany(v => v.Marks).Select(m => m.ObjectId));
            Assert.Equal(2, assembly.Sheet.PartList.Count);
            Assert.Equal("A2", assembly.Sheet.SizeName);

            Assert.Equal(new[] { "V-FRONT", "V-TOP" }, general.Views.Select(v => v.Id));
            Assert.All(general.Views, v => Assert.Empty(v.Dimensions));
            Assert.All(general.Views, v => Assert.False(v.HiddenLines));
            Assert.Empty(general.Sheet.PartList);
            Assert.Null(general.Sheet.PartListBox);
            Assert.Equal("A1", general.Sheet.SizeName);
        }

        [Fact]
        public void TheSettingsAreCopiedIntoTheDrawingSoChangingThemAfterwardsChangesNothing()
        {
            DrawingSettings settings = AssemblyOnA3();
            Drawing drawing = DrawingBuilder.Build(TC2(), settings, Tol);

            settings.Scales.Clear();
            settings.Views.Kinds.Clear();
            settings.Sheet.TitleBlock.Project = "CHANGED";
            DrawingSettings copy = drawing.Settings;
            copy.Marks.TextHeight = 9;

            Assert.Equal(11, drawing.Settings.Scales.Count);
            Assert.Equal(3, drawing.Settings.Views.Kinds.Count);
            Assert.Equal(string.Empty, drawing.Settings.Sheet.TitleBlock.Project);
            Assert.Equal(3.5, drawing.Settings.Marks.TextHeight);
            Assert.Equal(PaperSize.A3, drawing.Settings.Sheet.Size);
        }

        [Fact]
        public void ViewsThatFitNoScaleOfTheSheetTakeALargerSheetWhereAllowedAndAreReportedWhereNot()
        {
            DrawingSettings settings = DrawingSettings.Assembly;
            settings.Sheet.Size = PaperSize.A4;
            settings.Scales.Clear();
            settings.Scales.Add(5);

            Drawing reported = DrawingBuilder.Build(TC2(), settings, Tol);
            settings.Sheet.AllowLargerSheet = true;
            Drawing larger = DrawingBuilder.Build(TC2(), settings, Tol);

            Assert.Equal("A4", reported.Sheet.SizeName);
            string warning = Assert.Single(reported.Warnings);
            Assert.Contains("1:5", warning);
            Assert.Empty(larger.Warnings);
            Assert.Equal("A1", larger.Sheet.SizeName);
            AssertLaidOut(larger);
        }

        [Fact]
        public void TheTitleBlockIsFilledFromTheSourceWhereTheSettingsLeaveItEmpty()
        {
            DrawingSettings settings = AssemblyOnA3();
            settings.Sheet.TitleBlock.Project = "HALL 7";
            Drawing drawing = DrawingBuilder.Build(TC2(), settings, Tol);

            Assert.Equal("HALL 7", drawing.Sheet.TitleBlock.Project);
            Assert.Equal("BEAM B1", drawing.Sheet.TitleBlock.Title);
            Assert.Equal("TC2-beam", drawing.Sheet.TitleBlock.DrawingNumber);
            Assert.Equal(new GeoRectangle2(230, 10, 180, 40), drawing.Sheet.TitleBlockBox);
        }

        [Fact]
        public void ThePartListHasARowForEachMarkProfileMaterialAndLengthWithItsQuantity()
        {
            DrawingSource source = TestBodies.TC5().ToSource();
            Drawing drawing = DrawingBuilder.Build(source, DrawingSettings.Assembly, Tol);

            foreach (PartListRow row in drawing.Sheet.PartList)
            {
                _output.WriteLine(row.ToString());
            }

            Assert.Equal(
                source.Objects.Count(o => o.Kind == DrawingObjectKind.Part),
                drawing.Sheet.PartList.Sum(r => r.Quantity)
            );
            Assert.Equal(new[] { "C1", "P2", "S1", "S1" }, drawing.Sheet.PartList.Select(r => r.Mark));
            Assert.Equal(new[] { 1, 1, 2, 2 }, drawing.Sheet.PartList.Select(r => r.Quantity));
            Assert.Equal(1000.0, drawing.Sheet.PartList[0].Length);
        }
    }
}
