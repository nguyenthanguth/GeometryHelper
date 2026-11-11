using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;
using static GeometryHelper.Drawing.UnitTest.Projection.ProjectionKit;

namespace GeometryHelper.Drawing.UnitTest.Projection
{
    /// <summary>
    /// Times the views of an assembly of 50 parts and of a frame of about 1 000; the limits are loose until G0 sets them.
    /// </summary>
    public class ProjectionSpeedTests
    {
        private readonly ITestOutputHelper _output;

        public ProjectionSpeedTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TheThreeViewsOfFiveColumnBasesOfFiftyPartsAreMadeInUnderASecond()
        {
            ProjectionInput[] parts = TestBodies
                .TC10(5, 1)
                .Parts.Select(p => new ProjectionInput(p.Object.Id, p.Object.Solid))
                .ToArray();
            Assert.Equal(50, parts.Length);
            ViewProjector.Project(parts, Top, Tol);

            var watch = Stopwatch.StartNew();
            int lines = 0;

            foreach (GeoCoordinateSystem3 frame in new[] { Top, Front, Right })
            {
                ProjectedView2 view = ViewProjector.Project(parts, frame, Tol);
                lines += view.VisibleEdges.Count + view.HiddenEdges.Count;
            }

            watch.Stop();
            _output.WriteLine($"50 parts, three views: {watch.Elapsed.TotalMilliseconds:0} ms, {lines} lines");
            Assert.True(watch.Elapsed.TotalSeconds < 1.0, watch.Elapsed.ToString());
        }

        [Fact]
        public void AFrameOfAThousandMembersIsDrawnFromAboveFromTheFrontAndFromACorner()
        {
            ProjectionInput[] frame = Frame();
            _output.WriteLine($"{frame.Length} members");

            foreach ((string name, GeoCoordinateSystem3 view) in new[] { ("top", Top), ("front", Front), ("iso", Iso) })
            {
                var watch = Stopwatch.StartNew();
                ProjectedView2 projected = ViewProjector.Project(frame, view, Tol);
                watch.Stop();
                _output.WriteLine(
                    $"{name}: {watch.Elapsed.TotalMilliseconds:0} ms, {projected.VisibleEdges.Count} seen, {projected.HiddenEdges.Count} hidden"
                );
                Assert.False(projected.IsEmpty);
                Assert.True(watch.Elapsed.TotalSeconds < 60.0, watch.Elapsed.ToString());
            }
        }

        [Fact]
        public void TheTwoHundredColumnBasesOfTC10AreDrawnFromAboveFromTheFrontAndFromACorner()
        {
            ProjectionInput[] parts = TestBodies
                .TC10()
                .Parts.Select(p => new ProjectionInput(p.Object.Id, p.Object.Solid))
                .ToArray();
            _output.WriteLine($"{parts.Length} objects");

            foreach ((string name, GeoCoordinateSystem3 view) in new[] { ("top", Top), ("front", Front), ("iso", Iso) })
            {
                var watch = Stopwatch.StartNew();
                ProjectedView2 projected = ViewProjector.Project(parts, view, Tol);
                watch.Stop();
                _output.WriteLine(
                    $"TC10 {name}: {watch.Elapsed.TotalMilliseconds:0} ms, {projected.VisibleEdges.Count} seen, {projected.HiddenEdges.Count} hidden, {projected.VisibleEdges.Count(e => e.IsCircle)} circles"
                );
                Assert.True(watch.Elapsed.TotalSeconds < 60.0, watch.Elapsed.ToString());
            }
        }

        /// <summary>
        /// A frame of three storeys on a grid of 10 by 10 bays of 6 000: 300 columns HE 300, 540 beams IPE 400, and 160 floor
        /// plates 20 thick; 1 000 members.
        /// </summary>
        internal static ProjectionInput[] Frame()
        {
            var members = new List<ProjectionInput>();

            for (int level = 0; level < 3; level++)
            {
                double z0 = level * 4000.0;

                for (int i = 0; i < 10; i++)
                {
                    for (int j = 0; j < 10; j++)
                    {
                        double x = i * 6000.0,
                            y = j * 6000.0;
                        var up = new GeoCoordinateSystem3(new GeoPoint3(x, y, z0), GeoVector3.XAxis, GeoVector3.YAxis);
                        members.Add(
                            new ProjectionInput(
                                $"C-{level}-{i:00}-{j:00}",
                                GeoSolid3.Extrude(Section(300, 300, 19, 11), up, 3600, Tol)
                            )
                        );

                        if (i < 9)
                        {
                            members.Add(
                                new ProjectionInput(
                                    $"BX-{level}-{i:00}-{j:00}",
                                    Beam(new GeoPoint3(x + 150, y, z0 + 3800), GeoVector3.XAxis, 5700)
                                )
                            );
                        }

                        if (j < 9)
                        {
                            members.Add(
                                new ProjectionInput(
                                    $"BY-{level}-{i:00}-{j:00}",
                                    Beam(new GeoPoint3(x, y + 150, z0 + 3800), GeoVector3.YAxis, 5700)
                                )
                            );
                        }
                    }
                }
            }

            int plates = 1000 - members.Count;

            for (int k = 0; k < plates; k++)
            {
                int i = k % 9,
                    j = (k / 9) % 9,
                    level = k / 81;
                double x = i * 6000.0 + 300,
                    y = j * 6000.0 + 300,
                    z = (level % 3) * 4000.0 + 4000;
                members.Add(new ProjectionInput($"P-{k:000}", Box(x, y, z, x + 5400, y + 5400, z + 20)));
            }

            return members.ToArray();
        }

        private static GeoSolid3 Beam(GeoPoint3 start, GeoVector3 along, double length)
        {
            GeoVector3 across = GeoVector3.ZAxis.CrossProduct(along);
            var placement = new GeoCoordinateSystem3(start, across, GeoVector3.ZAxis);
            return GeoSolid3.Extrude(Section(400, 180, 13.5, 8.6), placement, length, Tol);
        }

        private static GeoPolygon2 Section(double depth, double width, double flange, double web)
        {
            double w = width / 2,
                d = depth / 2,
                t = web / 2,
                f = d - flange;
            return new GeoPolygon2(
                P(-w, -d),
                P(w, -d),
                P(w, -f),
                P(t, -f),
                P(t, f),
                P(w, f),
                P(w, d),
                P(-w, d),
                P(-w, f),
                P(-t, f),
                P(-t, -f),
                P(-w, -f)
            );
        }
    }
}
