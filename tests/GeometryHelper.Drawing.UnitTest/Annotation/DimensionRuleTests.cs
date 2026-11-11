using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Drawing.UnitTest.Annotation.AnnotationKit;
using static GeometryHelper.Drawing.UnitTest.Projection.ProjectionKit;

namespace GeometryHelper.Drawing.UnitTest.Annotation
{
    public class DimensionRuleTests
    {
        [Fact]
        public void APlate300By200SeenFromAboveGetsTheOverall300And200AndNoChainTwiceOverThem()
        {
            DrawingObject[] objects = { Part("plate", Box(0, 0, 0, 300, 200, 20), true) };
            IReadOnlyList<DrawingDimension> dimensions = Dimension(objects, Top);

            Assert.Equal(2, dimensions.Count);
            Assert.Equal(new[] { "300" }, Texts(dimensions, DimensionKind.Overall, true));
            Assert.Equal(new[] { "200" }, Texts(dimensions, DimensionKind.Overall, false));
            Near(300.0, dimensions.Single(d => d.Direction.X > 0.5).Value);
            Near(200.0, dimensions.Single(d => d.Direction.Y > 0.5).Value);
            Assert.Equal(new[] { "D-1", "D-2" }, dimensions.Select(d => d.Id));
        }

        [Fact]
        public void ABeam3000WithHolesAt100And2900GetsTheChain100_2800_100AndTheOverall3000()
        {
            DrawingObject[] objects = { Bar(3000, 200, P(100, 100), P(2900, 100)) };
            IReadOnlyList<DrawingDimension> dimensions = Dimension(objects, Top);

            Assert.Equal(new[] { "100", "2800", "100" }, Texts(dimensions, DimensionKind.Chain, true));
            Assert.Equal(new[] { "3000" }, Texts(dimensions, DimensionKind.Overall, true));
            Near(2800.0, dimensions.Single(d => d.Kind == DimensionKind.Chain && d.Text == "2800").Value);
            Near(3000.0, dimensions.Single(d => d.Kind == DimensionKind.Overall && d.Direction.X > 0.5).Value);

            // The row of holes gives the same three along X, so it gives none of its own; across, each hole is 100 from
            // either edge, which the overall 200 does not say.
            Assert.DoesNotContain(dimensions, d =>
                d.Direction.X > 0.5 && (d.Kind == DimensionKind.HoleEdge || d.Kind == DimensionKind.HoleGroup)
            );
            Assert.Equal(new[] { "200" }, Texts(dimensions, DimensionKind.Overall, false));
            Assert.Equal(new[] { "100", "100" }, Texts(dimensions, DimensionKind.HoleEdge, false));

            // Each dimension is tied to its object and to its two model points.
            DrawingDimension first = dimensions.First(d => d.Kind == DimensionKind.Chain);
            Assert.Equal(new[] { "bar" }, first.Links.ObjectIds);
            Assert.Equal(2, first.Links.Points.Count);
            Near(0.0, first.Links.Points[0].Point.X);
            Near(100.0, first.Links.Points[1].Point.X);
        }

        [Fact]
        public void EqualNeighboursOfAChainAreMergedIntoNTimesPWhereTheSettingsSaySo()
        {
            DrawingObject[] objects = { Bar(1000, 100, P(100, 50), P(200, 50), P(300, 50), P(400, 50)) };

            IReadOnlyList<DrawingDimension> merged = Dimension(objects, Top);
            Assert.Equal(new[] { "4 x 100 = 400", "600" }, Texts(merged, DimensionKind.Chain, true));
            DrawingDimension four = merged.Single(d => d.Text == "4 x 100 = 400");
            Assert.Equal(4, four.Count);
            Near(400.0, four.Value);

            // The row of holes gives its pitches as one, and its distance to the near end; the far one is the chain's 600.
            Assert.Equal(new[] { "3 x 100 = 300" }, Texts(merged, DimensionKind.HoleGroup, true));

            IReadOnlyList<DrawingDimension> apart = Dimension(
                objects,
                Top,
                new DimensionSettings { MergeEqual = false }
            );
            Assert.Equal(new[] { "100", "100", "100", "100", "600" }, Texts(apart, DimensionKind.Chain, true));
        }

        [Fact]
        public void ACumulativeLineMeasuresEveryPointFromTheFirstEnd()
        {
            DrawingObject[] objects = { Bar(3000, 200, P(100, 100), P(2900, 100)) };
            var settings = new DimensionSettings
            {
                Chain = false,
                HolesByGroup = false,
                Cumulative = true,
            };
            IReadOnlyList<DrawingDimension> dimensions = Dimension(objects, Top, settings);

            DrawingDimension[] along = dimensions
                .Where(d => d.Kind == DimensionKind.Cumulative && d.Direction.X > 0.5)
                .OrderBy(d => d.Value)
                .ToArray();
            Assert.Equal(new[] { "0", "100", "2900", "3000" }, along.Select(d => d.Text));
            Assert.All(along, d => Near(0.0, d.Points[0].ViewPoint.X));
            Assert.All(along, d => Assert.Null(d.DimensionLine));
            Assert.All(along, d => Assert.Single(d.ExtensionLines));
            Assert.Single(along.Select(d => d.Tier).Distinct());
        }

        [Fact]
        public void AnOrdinateLineMeasuresFromTheWorkPointOfTheMainPartWhereverItLies()
        {
            // The bar's frame starts half way along it, so its work point is at 1 500.
            GeoSolid3 body = PlateWithHoles(3000, 200, 20, 22, 24, P(100, 100), P(2900, 100));
            var frame = new GeoCoordinateSystem3(new GeoPoint3(1500, 0, 0), GeoVector3.XAxis, GeoVector3.YAxis);
            DrawingObject[] objects = { Part("bar", body, true, frame) };
            var settings = new DimensionSettings
            {
                Chain = false,
                HolesByGroup = false,
                Overall = false,
                Ordinate = true,
            };
            IReadOnlyList<DrawingDimension> dimensions = Dimension(objects, Top, settings);

            DrawingDimension[] along = dimensions
                .Where(d => d.Kind == DimensionKind.Ordinate && d.Direction.X > 0.5)
                .OrderBy(d => d.Points[1].ViewPoint.X)
                .ToArray();
            Assert.Equal(new[] { "1500", "1400", "0", "1400", "1500" }, along.Select(d => d.Text));
            Assert.All(along, d => Near(1500.0, d.Points[0].ViewPoint.X));
            Assert.All(along, d => Assert.Equal(DimensionPointKind.WorkPoint, d.Points[0].Kind));
        }

        [Fact]
        public void TheEndPlateOfTC2SeenEndOnGetsItsRowsOfHolesByGroup()
        {
            TestBody body = TestBodies.TC2();
            DrawingObject[] objects = Objects(body);
            GeoCoordinateSystem3 frame = StandardViews.GetFrame(body.ToSource().Frame, ViewKind.Right).CoordinateSystem;
            IReadOnlyList<DrawingDimension> dimensions = Dimension(objects, frame);

            // Across the view, two columns 100 apart, 50 from the plate's sides; the beam's flange is 150 wide.
            Assert.Equal(new[] { "25", "100", "25" }, Texts(dimensions, DimensionKind.Chain, true));
            Assert.Equal(new[] { "150" }, Texts(dimensions, DimensionKind.Overall, true));
            Assert.Equal(new[] { "50", "50" }, Texts(dimensions, DimensionKind.HoleEdge, true));

            // Up it, three rows 100 apart, 70 from the plate's ends; the beam is 300 deep. The pitches are the chain's, so
            // the rows give only their distances to the plate's ends.
            Assert.Equal(new[] { "50", "2 x 100 = 200", "50" }, Texts(dimensions, DimensionKind.Chain, false));
            Assert.Equal(new[] { "300" }, Texts(dimensions, DimensionKind.Overall, false));
            Assert.Equal(new[] { "70", "70" }, Texts(dimensions, DimensionKind.HoleEdge, false));
            Assert.DoesNotContain(dimensions, d => d.Kind == DimensionKind.HoleGroup);

            // With no chain, the rows give their pitches too.
            IReadOnlyList<DrawingDimension> holes = Dimension(objects, frame, new DimensionSettings { Chain = false });
            Assert.Equal(new[] { "100" }, Texts(holes, DimensionKind.HoleGroup, true));
            DrawingDimension rows = holes.Single(d => d.Kind == DimensionKind.HoleGroup && d.Direction.Y > 0.5);
            Assert.Equal("2 x 100 = 200", rows.Text);
            Assert.Equal(2, rows.Count);
            Near(200.0, rows.Value);
            Assert.All(rows.Points, p => Assert.Equal("TC2-endplate", p.ObjectId));
            Assert.Equal(new[] { "70", "70" }, Texts(holes, DimensionKind.HoleEdge, false));
        }

        [Fact]
        public void TheTextIsRoundedAsTheSettingsSayAndTheValueKeepsEveryDigit()
        {
            DrawingObject[] objects = { Part("plate", Box(0, 0, 0, 300.4, 200.26, 20), true) };

            IReadOnlyList<DrawingDimension> whole = Dimension(objects, Top);
            Assert.Equal(new[] { "300" }, Texts(whole, DimensionKind.Overall, true));
            Near(300.4, whole.Single(d => d.Direction.X > 0.5).Value);

            IReadOnlyList<DrawingDimension> halves = Dimension(objects, Top, new DimensionSettings { Rounding = 0.5 });
            Assert.Equal(new[] { "300.5" }, Texts(halves, DimensionKind.Overall, true));
            Assert.Equal(new[] { "200.5" }, Texts(halves, DimensionKind.Overall, false));
        }
    }
}
