using System;
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
    public class DimensionTextLayoutTests
    {
        private readonly ITestOutputHelper _output;

        public DimensionTextLayoutTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void NoTextOfAViewComesNearAnotherOrOverAnArrowOfTC1TC2AndTC5FromOneToTwoToOneToTwenty()
        {
            var failures = new List<string>();
            int count = 0;

            foreach (TestBody body in new[] { TestBodies.TC1(), TestBodies.TC2(), TestBodies.TC5() })
            {
                DrawingObject[] objects = Objects(body);

                foreach ((string viewName, GeoCoordinateSystem3 frame) in MainViews(body.ToSource().Frame))
                {
                    ProjectedView2 view = Project(objects, frame);

                    foreach (double scale in new[] { 2.0, 5.0, 10.0, 20.0 })
                    {
                        IReadOnlyList<DrawingDimension> dimensions = Annotator.Dimension(view, objects, frame, new DimensionSettings { Scale = scale }, Tol);
                        string where = $"{body.Name} {viewName} 1:{scale}";
                        count += dimensions.Count;

                        for (int i = 0; i < dimensions.Count; i++)
                        {
                            for (int j = 0; j < dimensions.Count; j++)
                            {
                                DrawingDimension a = dimensions[i], b = dimensions[j];

                                // Each text keeps a quarter of its height clear round it, as two texts in the middle of
                                // neighbouring lines do: '50' and '50' never stand together as '5050'.
                                if (i < j && Overlap(a.TextBox, b.TextBox, 0.25 * (a.TextHeight + b.TextHeight)))
                                {
                                    failures.Add($"{where}: text '{a.Text}' of {a.Id} against text '{b.Text}' of {b.Id}");
                                }

                                if (i != j && Arrowheads(b).Any(arrow => Overlap(a.TextBox, arrow, 0.0)))
                                {
                                    failures.Add($"{where}: text '{a.Text}' of {a.Id} over an arrowhead of {b.Id}");
                                }
                            }
                        }

                        failures.AddRange(Annotator.FindOverlaps(dimensions, Tol).Select(o => $"{where}: {o}"));
                    }
                }
            }

            _output.WriteLine($"{count} dimensions, {failures.Count} failures");
            failures.Take(20).ToList().ForEach(_output.WriteLine);
            Assert.Empty(failures);
        }

        [Fact]
        public void TheRowOfFiftyThreeHundredFiftyAtOneToTwentyKeepsThreeHundredInItsMiddleAndPutsEachFiftyOutside()
        {
            // TC5's base plate seen from its end at 1:20: the holes 300 apart and 50 from the plate's edges. Each '50', 80
            // wide in the view, is too long for its step; the '300' fits in the middle of its line and stays there, and the
            // two 50s go past the ends of the row, not side by side over the 300 where they read as 5050.
            TestBody body = TestBodies.TC5();
            DrawingObject[] objects = Objects(body);
            (string _, GeoCoordinateSystem3 frame) = MainViews(body.ToSource().Frame).Single(v => v.Name == "right");
            IReadOnlyList<DrawingDimension> dimensions = Annotator.Dimension(Project(objects, frame), objects, frame, new DimensionSettings { Scale = 20 }, Tol);
            DrawingDimension[] rows = dimensions.Where(d => d.Kind == DimensionKind.HoleGroup && d.Text == "300").ToArray();

            Assert.Equal(2, rows.Length);

            foreach (DrawingDimension row in rows)
            {
                bool alongX = row.Direction.X > 0.5;
                (double lo, double hi) = Span(row, alongX);
                double centre = alongX ? row.TextPosition.X : row.TextPosition.Y;
                Assert.True(centre > lo && centre < hi, $"{row.Id} '300' at {centre}, its line from {lo} to {hi}");

                DrawingDimension[] edges = dimensions.Where(d => d.Kind == DimensionKind.HoleEdge && d.Text == "50" && (d.Direction.X > 0.5) == alongX && d.Tier == row.Tier).ToArray();
                Assert.Equal(2, edges.Length);

                foreach (DrawingDimension edge in edges)
                {
                    double low = alongX ? edge.TextBox.Center.X - 0.5 * edge.TextBox.Width : edge.TextBox.Center.Y - 0.5 * edge.TextBox.Height;
                    double high = alongX ? edge.TextBox.Center.X + 0.5 * edge.TextBox.Width : edge.TextBox.Center.Y + 0.5 * edge.TextBox.Height;
                    Assert.True(high <= lo + Tol.EqualPoint || low >= hi - Tol.EqualPoint, $"{edge.Id} '50' from {low} to {high} stands over the 300 from {lo} to {hi}");
                }
            }

            Assert.Empty(Annotator.FindOverlaps(dimensions, Tol));
        }

        private static (double Low, double High) Span(DrawingDimension dimension, bool alongX)
        {
            GeoLine2 line = dimension.DimensionLine.Value;
            double a = alongX ? line.StartPoint.X : line.StartPoint.Y, b = alongX ? line.EndPoint.X : line.EndPoint.Y;
            return (Math.Min(a, b), Math.Max(a, b));
        }

        /// <summary>Whether two boxes overlap by more than the tolerance once the first is grown by a margin all round.</summary>
        private static bool Overlap(GeoRectangle2 a, GeoRectangle2 b, double margin)
        {
            double x = Math.Min(a.Center.X + 0.5 * a.Width, b.Center.X + 0.5 * b.Width) - Math.Max(a.Center.X - 0.5 * a.Width, b.Center.X - 0.5 * b.Width);
            double y = Math.Min(a.Center.Y + 0.5 * a.Height, b.Center.Y + 0.5 * b.Height) - Math.Max(a.Center.Y - 0.5 * a.Height, b.Center.Y - 0.5 * b.Height);
            return x + margin > Tol.EqualPoint && y + margin > Tol.EqualPoint;
        }

        /// <summary>
        /// The boxes round a dimension's arrowheads as the block draws them: as long as the text is high and a third of that
        /// wide, inside a line at least two arrows long, outside a shorter one.
        /// </summary>
        private static IEnumerable<GeoRectangle2> Arrowheads(DrawingDimension dimension)
        {
            if (!dimension.DimensionLine.HasValue)
            {
                yield break;
            }

            GeoLine2 line = dimension.DimensionLine.Value;
            double arrow = dimension.TextHeight, length = line.Length;
            double ux = (line.EndPoint.X - line.StartPoint.X) / length, uy = (line.EndPoint.Y - line.StartPoint.Y) / length;
            double inward = length >= 2.0 * arrow ? 1.0 : -1.0;

            foreach ((GeoPoint2 tip, double sign) in new[] { (line.StartPoint, inward), (line.EndPoint, -inward) })
            {
                double backX = tip.X + sign * ux * arrow, backY = tip.Y + sign * uy * arrow;
                double acrossX = Math.Abs(uy) * arrow / 6.0, acrossY = Math.Abs(ux) * arrow / 6.0;
                double x0 = Math.Min(tip.X, backX - acrossX), x1 = Math.Max(tip.X, backX + acrossX);
                double y0 = Math.Min(tip.Y, backY - acrossY), y1 = Math.Max(tip.Y, backY + acrossY);
                yield return new GeoRectangle2(new GeoPoint2(0.5 * (x0 + x1), 0.5 * (y0 + y1)), x1 - x0, y1 - y0);
            }
        }
    }
}
