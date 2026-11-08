using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Drawing.UnitTest.Builder.BuilderKit;

namespace GeometryHelper.Drawing.UnitTest.Builder
{
    public class ViewBuilderTests
    {
        /// <summary>A projector that counts its calls and keeps the objects of each, and projects as the drawing does.</summary>
        private sealed class CountingProjector
        {
            public List<string[]> Calls { get; } = new List<string[]>();

            public ProjectedView2 Project(IEnumerable<ProjectionInput> inputs, GeoCoordinateSystem3 frame, ProjectionOptions options, Tolerance tolerance)
            {
                ProjectionInput[] list = inputs.ToArray();
                Calls.Add(list.Select(i => i.Id).ToArray());
                return ViewProjector.Project(list, frame, options, tolerance);
            }
        }

        [Fact]
        public void TheThreeViewsAreThreeProjectionsAndANewCropOfOneMakesThatViewAloneAgain()
        {
            var projector = new CountingProjector();
            var builder = new ViewBuilder(TC2(extra: true), DrawingSettings.Assembly, Tol, projector.Project);

            IReadOnlyList<DrawingView> views = builder.Build();

            Assert.Equal(new[] { "V-FRONT", "V-TOP", "V-RIGHT" }, views.Select(v => v.Id));
            Assert.Equal(3, projector.Calls.Count);
            Assert.All(projector.Calls, c => Assert.Equal(new[] { "P-EXTRA", "TC2-beam", "TC2-endplate" }, c));

            DrawingView front = views[0];
            DrawingView end = builder.WithCrop(front, new GeoRectangle2(2800, -200, 300, 400));

            Assert.Equal(4, projector.Calls.Count);
            Assert.Equal(new[] { "TC2-beam", "TC2-endplate" }, projector.Calls[3]);
            Assert.Equal(new[] { "TC2-beam", "TC2-endplate" }, end.ObjectIds);
            Assert.Equal("V-FRONT", end.Id);
            Assert.Equal(ViewKind.Front, end.Kind);
            Assert.NotSame(front.Lines, end.Lines);
            Assert.DoesNotContain(end.Lines.VisibleEdges, e => e.ObjectId == "P-EXTRA");
            Assert.Contains(front.Lines.VisibleEdges, e => e.ObjectId == "P-EXTRA");
        }

        [Fact]
        public void ANewScaleMakesNoLinesAgainAndANewDirectionMakesThatViewAgainAsAFreeView()
        {
            var projector = new CountingProjector();
            var builder = new ViewBuilder(TC2(), DrawingSettings.Assembly, Tol, projector.Project);
            IReadOnlyList<DrawingView> views = builder.Build();

            DrawingView scaled = builder.WithScale(views[1], 20);

            Assert.Equal(3, projector.Calls.Count);
            Assert.Same(views[1].Lines, scaled.Lines);
            Assert.Equal(20.0, scaled.Scale);

            var frame = new ViewFrame3(GeoPoint3.Origin, new GeoVector3(1, 1, -1), GeoVector3.ZAxis, double.PositiveInfinity, double.PositiveInfinity, Tol);
            DrawingView turned = builder.WithDirection(views[2], frame);

            Assert.Equal(4, projector.Calls.Count);
            Assert.Equal("V-RIGHT", turned.Id);
            Assert.Equal(ViewKind.Free, turned.Kind);
            Assert.Same(frame, turned.Frame);
        }

        [Fact]
        public void ANewDepthMakesThatViewAgainAndSeesOnlyWhatLiesWithinIt()
        {
            var projector = new CountingProjector();
            var builder = new ViewBuilder(TC2(), DrawingSettings.Assembly, Tol, projector.Project);
            DrawingView right = builder.Build()[2];

            // The right end view looks from +X: in front of the frame's origin, at the beam's start, is all of the beam;
            // seeing 100 in front of it and nothing behind leaves the end plate, at 3 000, out.
            DrawingView shallow = builder.WithDepth(right, 100, 0);

            Assert.Equal(4, projector.Calls.Count);
            Assert.Equal(100.0, shallow.Frame.DepthInFront);
            Assert.DoesNotContain(shallow.Lines.VisibleEdges.Concat(shallow.Lines.HiddenEdges), e => e.ObjectId == "TC2-endplate");
            Assert.Contains(right.Lines.VisibleEdges, e => e.ObjectId == "TC2-endplate");
        }

        [Fact]
        public void ASectionAcrossTheBeamAtItsMiddleIsItsCrossSectionHatchedWithNothingBeyondItsDepth()
        {
            DrawingSettings settings = DrawingSettings.Assembly;
            settings.Views.Sections.Add(new SectionViewRequest("A", ViewKind.Right, 0.5));

            DrawingView section = new ViewBuilder(TC2(), settings, Tol).Build().Single(v => v.Kind == ViewKind.Section);

            Assert.Equal("V-SECTION-A", section.Id);
            Assert.Equal("SECTION A-A", section.Name);
            Assert.True(section.SectionPlane.HasValue);
            ProjectedFace2 cut = Assert.Single(section.Lines.SectionFaces);
            Assert.Equal("TC2-beam", cut.ObjectId);
            Assert.Equal(2 * 150 * 12 + 276 * 8, cut.Face.Area, 6);
            Assert.DoesNotContain(section.Lines.VisibleEdges.Concat(section.Lines.HiddenEdges), e => e.ObjectId == "TC2-endplate");
        }

        [Fact]
        public void ADetailOfTheEndOfTheBeamIsTakenFromTheFrontViewAndSeesOnlyWhatItsRectangleMeets()
        {
            DrawingSettings settings = DrawingSettings.Assembly;
            settings.Views.Details.Add(new DetailViewRequest("B", ViewKind.Front, new GeoRectangle2(2800, -200, 300, 400), 4));

            DrawingView detail = new ViewBuilder(TC2(extra: true), settings, Tol).Build().Single(v => v.Kind == ViewKind.Detail);

            Assert.Equal("V-DETAIL-B", detail.Id);
            Assert.Equal("V-FRONT", detail.ParentViewId);
            Assert.Equal(new[] { "TC2-beam", "TC2-endplate" }, detail.ObjectIds);
            Assert.Equal(2950.0, detail.Crop.Center.X, 9);
            Assert.Equal(300.0, detail.Crop.Width, 9);
        }

        [Fact]
        public void TheScaleChosenIsTheLargestAllowedAtWhichTheBoxFitsTheSpace()
        {
            var builder = new ViewBuilder(TC2(), DrawingSettings.Assembly, Tol);

            Assert.Equal(10.0, builder.ChooseScale(new GeoRectangle2(0, 0, 3200, 400), 380, 250));
            Assert.Equal(1.0, builder.ChooseScale(new GeoRectangle2(0, 0, 300, 200), 380, 250));
            Assert.Equal(500.0, builder.ChooseScale(new GeoRectangle2(0, 0, 1E7, 400), 380, 250));
        }

        [Fact]
        public void AViewWithNoLinesIsNotMade()
        {
            DrawingSettings settings = DrawingSettings.Assembly;
            settings.Views.Details.Add(new DetailViewRequest("C", ViewKind.Front, new GeoRectangle2(-5000, 5000, 100, 100), 2));

            IReadOnlyList<DrawingView> views = new ViewBuilder(TC2(), settings, Tol).Build();

            Assert.DoesNotContain(views, v => v.Kind == ViewKind.Detail);
        }
    }
}
