using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;
using static GeometryHelper.Drawing.UnitTest.Annotation.AnnotationKit;
using static GeometryHelper.Drawing.UnitTest.Projection.ProjectionKit;

namespace GeometryHelper.Drawing.UnitTest.Annotation
{
    public class DimensionPlacementTests
    {
        private readonly ITestOutputHelper _output;

        public DimensionPlacementTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TheChainStandsOnTheFirstTierAndTheOverallOutsideIt()
        {
            DrawingObject[] objects = Objects(TestBodies.TC1());
            IReadOnlyList<DrawingDimension> dimensions = Dimension(objects, Top);
            ProjectedView2 view = Project(objects, Top);
            double top = view.Bounds.Center.Y + 0.5 * view.Bounds.Height;

            DrawingDimension[] chain = dimensions
                .Where(d => d.Kind == DimensionKind.Chain && d.Direction.X > 0.5)
                .ToArray();
            DrawingDimension overall = dimensions.Single(d => d.Kind == DimensionKind.Overall && d.Direction.X > 0.5);

            Assert.Equal(new[] { "50", "200", "50" }, chain.Select(d => d.Text));
            Assert.All(chain, d => Assert.Equal(0, d.Tier));
            Assert.Equal(1, overall.Tier);

            // At 1:1 the tiers stand 8 and 16 above the plate, the text a quarter of its height above its line.
            Near(top + 8.0, chain[0].DimensionLine.Value.StartPoint.Y);
            Near(top + 16.0, overall.DimensionLine.Value.StartPoint.Y);
            Near(16.0, overall.Offset);
            Near(top + 16.0 + 0.625 + 1.25, overall.TextPosition.Y);
            Near(150.0, overall.TextPosition.X);
        }

        [Fact]
        public void TheTiersAndTheTextGrowWithTheScale()
        {
            DrawingObject[] objects = Objects(TestBodies.TC1());
            IReadOnlyList<DrawingDimension> dimensions = Dimension(objects, Top, new DimensionSettings { Scale = 10 });
            DrawingDimension overall = dimensions.Single(d => d.Kind == DimensionKind.Overall && d.Direction.X > 0.5);

            Near(160.0, overall.Offset);
            Near(25.0, overall.TextHeight);
            Near(25.0, overall.TextBox.Height);
            Near(3 * 0.8 * 25.0, overall.TextBox.Width);
        }

        [Fact]
        public void ATextTooLongForItsStepGoesPastTheEndOfItsLine()
        {
            // Holes 30 apart at 1:10: the text "30", 40 wide in the view, does not fit between its extension lines.
            DrawingObject[] objects = { Bar(1000, 100, P(400, 50), P(430, 50)) };
            IReadOnlyList<DrawingDimension> dimensions = Dimension(
                objects,
                Top,
                new DimensionSettings { Scale = 10, MergeEqual = false }
            );
            DrawingDimension step = dimensions.Single(d => d.Kind == DimensionKind.Chain && d.Text == "30");

            Assert.True(
                step.TextBox.Center.X - 0.5 * step.TextBox.Width >= 430.0
                    || step.TextBox.Center.X + 0.5 * step.TextBox.Width <= 400.0,
                step.TextBox.ToString()
            );
            Assert.Empty(Annotator.FindOverlaps(dimensions, Tol));
        }

        [Fact]
        public void TheOverlapCountFindsADimensionLineOnAnotherAndATextOverALine()
        {
            DrawingObject[] objects = Objects(TestBodies.TC1());
            IReadOnlyList<DrawingDimension> dimensions = Dimension(objects, Top);
            Assert.Empty(Annotator.FindOverlaps(dimensions, Tol));

            // The same dimensions laid out a second time, as if for another view on the same place, lie on the first.
            IReadOnlyList<string> twice = Annotator.FindOverlaps(dimensions.Concat(Dimension(objects, Top)), Tol);
            Assert.Contains(twice, s => s.Contains("dimension line over dimension line"));

            // At 1:100 the texts of the plate's steps of 50 are far too wide for anything but tiers of their own.
            IReadOnlyList<DrawingDimension> small = Dimension(objects, Top, new DimensionSettings { Scale = 100 });
            Assert.Empty(Annotator.FindOverlaps(small, Tol));
        }

        [Fact]
        public void NoDimensionOverlapsAnotherOnAnyTestBodyInTheThreeMainViews()
        {
            var bodies = new List<(string Name, DrawingObject[] Objects, (string, GeoCoordinateSystem3)[] Views)>();
            (string, GeoCoordinateSystem3)[] kitViews = { ("top", Top), ("front", Front), ("right", Right) };

            foreach (
                TestBody body in new[]
                {
                    TestBodies.TC1(),
                    TestBodies.TC2(),
                    TestBodies.TC3(),
                    TestBodies.TC4(),
                    TestBodies.TC5(),
                    TestBodies.TC6(),
                    TestBodies.TC7(),
                    TestBodies.TC8(),
                    TestBodies.TC9(),
                    TestBodies.TC10(2, 2),
                }
            )
            {
                bodies.Add((body.Name, Objects(body), MainViews(body.ToSource().Frame)));
            }

            // The projection kit's bodies, seen in its own three views.
            bodies.Add(("box", new[] { Part("box", Box(0, 0, 0, 300, 200, 100), true) }, kitViews));
            bodies.Add(
                (
                    "plate",
                    new[] { Part("plate", PlateWithHoles(300, 200, 20, 22, 24, P(60, 100), P(240, 100)), true) },
                    kitViews
                )
            );
            bodies.Add(("angle", new[] { Part("angle", Angle(100, 10, 1000), true) }, kitViews));
            bodies.Add(("tube", new[] { Part("tube", Tube(168.3, 6, 1000, 32), true) }, kitViews));
            bodies.Add(
                ("beam", BeamWithEndPlates().Select(i => Part(i.Id, i.Solid, i.Id == "beam")).ToArray(), kitViews)
            );

            var settingsList = new[]
            {
                ("default 1:1", new DimensionSettings()),
                ("default 1:10", new DimensionSettings { Scale = 10 }),
                ("default 1:20", new DimensionSettings { Scale = 20 }),
                (
                    "all rules 1:5",
                    new DimensionSettings
                    {
                        Scale = 5,
                        Cumulative = true,
                        Ordinate = true,
                        MergeEqual = false,
                    }
                ),
            };

            var failures = new List<string>();
            int count = 0;

            foreach ((string name, DrawingObject[] objects, (string, GeoCoordinateSystem3)[] views) in bodies)
            {
                foreach ((string viewName, GeoCoordinateSystem3 frame) in views)
                {
                    ProjectedView2 view = Project(objects, frame);

                    foreach ((string settingsName, DimensionSettings settings) in settingsList)
                    {
                        IReadOnlyList<DrawingDimension> dimensions = Annotator.Dimension(
                            view,
                            objects,
                            frame,
                            settings,
                            Tol
                        );
                        count += dimensions.Count;
                        IReadOnlyList<string> overlaps = Annotator.FindOverlaps(dimensions, Tol);
                        failures.AddRange(overlaps.Select(o => $"{name} {viewName} {settingsName}: {o}"));
                        _output.WriteLine(
                            $"{name} {viewName} {settingsName}: {dimensions.Count} dimensions, {overlaps.Count} overlaps"
                        );
                    }
                }
            }

            _output.WriteLine($"{count} dimensions, {failures.Count} overlaps");
            failures.Take(20).ToList().ForEach(_output.WriteLine);
            Assert.Empty(failures);
        }
    }
}
