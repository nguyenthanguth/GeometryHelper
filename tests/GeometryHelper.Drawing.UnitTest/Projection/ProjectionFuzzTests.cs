using System;
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
    public class ProjectionFuzzTests
    {
        private readonly ITestOutputHelper _output;

        public ProjectionFuzzTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TwoThousandRandomBoxesAndCutsGiveNoErrorNoLineTwiceAndTheSameViewInAnyOrder()
        {
            var failures = new List<string>();
            var watch = Stopwatch.StartNew();
            int edges = 0;

            for (int seed = 0; seed < 2000; seed++)
            {
                var random = new Random(seed);
                ProjectionInput[] inputs = Bodies(random);
                GeoCoordinateSystem3 frame = Frame(random);
                ProjectionOptions options =
                    random.Next(5) == 0
                        ? new ProjectionOptions(
                            sectionPlane: new GeoPlane3(new GeoPoint3(random.Next(0, 200), 0, 0), frame.ZAxis)
                        )
                        : ProjectionOptions.Default;

                try
                {
                    ProjectedView2 view = ViewProjector.Project(inputs, frame, options, Tol);
                    edges += view.VisibleEdges.Count + view.HiddenEdges.Count;
                    List<string> both = Overlaps(view.VisibleEdges, view.HiddenEdges, false);
                    List<string> twice = Overlaps(view.VisibleEdges, view.VisibleEdges, true)
                        .Concat(Overlaps(view.HiddenEdges, view.HiddenEdges, true))
                        .ToList();

                    if (both.Count > 0)
                    {
                        failures.Add(seed + ": seen and hidden " + both[0]);
                    }

                    if (twice.Count > 0)
                    {
                        failures.Add(seed + ": twice " + twice[0]);
                    }

                    ProjectionInput[] shuffled = inputs.OrderBy(_ => random.Next()).ToArray();

                    if (Signature(ViewProjector.Project(shuffled, frame, options, Tol)) != Signature(view))
                    {
                        failures.Add(seed + ": order");
                    }
                }
                catch (Exception exception)
                {
                    failures.Add(seed + ": " + exception.GetType().Name + " " + exception.Message);
                }
            }

            _output.WriteLine(
                $"2000 cases, {edges} lines, {watch.Elapsed.TotalSeconds:0.0} s, {failures.Count} failures"
            );
            failures.Take(20).ToList().ForEach(_output.WriteLine);
            Assert.Empty(failures);
        }

        /// <summary>
        /// One to four boxes: square to the axes on whole millimetres, so that they often touch face to face or edge to edge,
        /// or turned at random; some cut by a plane or by another box.
        /// </summary>
        internal static ProjectionInput[] Bodies(Random random)
        {
            int count = random.Next(1, 5);
            var inputs = new ProjectionInput[count];

            for (int i = 0; i < count; i++)
            {
                GeoSolid3 body;

                if (random.Next(2) == 0)
                {
                    int x = random.Next(0, 150),
                        y = random.Next(0, 150),
                        z = random.Next(0, 150);
                    body = Box(
                        x,
                        y,
                        z,
                        x + random.Next(1, 8) * 10,
                        y + random.Next(1, 8) * 10,
                        z + random.Next(1, 8) * 10
                    );
                }
                else
                {
                    var axis = new GeoVector3(
                        random.NextDouble() - 0.5,
                        random.NextDouble() - 0.5,
                        random.NextDouble() - 0.5
                    );
                    var across = new GeoVector3(
                        random.NextDouble() - 0.5,
                        random.NextDouble() - 0.5,
                        random.NextDouble() - 0.5
                    );
                    var placement = new GeoCoordinateSystem3(
                        new GeoPoint3(random.Next(0, 150), random.Next(0, 150), random.Next(0, 150)),
                        axis,
                        across
                    );
                    body = GeoSolid3.Extrude(
                        Rectangle(0, 0, random.Next(5, 80), random.Next(5, 80)),
                        placement,
                        random.Next(5, 120),
                        Tol
                    );
                }

                int cut = random.Next(4);

                if (cut == 1)
                {
                    GeoAabb3 box = body.GetAabb();
                    GeoPoint3 at = new GeoPoint3(
                        box.Min.X + random.NextDouble() * (box.Max.X - box.Min.X),
                        box.Min.Y + random.NextDouble() * (box.Max.Y - box.Min.Y),
                        box.Min.Z + random.NextDouble() * (box.Max.Z - box.Min.Z)
                    );
                    var normal = new GeoVector3(
                        random.NextDouble() - 0.5,
                        random.NextDouble() - 0.5,
                        random.NextDouble() - 0.5
                    );

                    if (body.TrySplitBy(new GeoPlane3(at, normal), out GeoSolid3 above, out GeoSolid3 _, Tol))
                    {
                        body = above;
                    }
                }
                else if (cut == 2)
                {
                    GeoAabb3 box = body.GetAabb();
                    double x = box.Min.X + random.NextDouble() * (box.Max.X - box.Min.X);
                    double y = box.Min.Y + random.NextDouble() * (box.Max.Y - box.Min.Y);
                    GeoSolid3 tool = Box(
                        x,
                        y,
                        box.Min.Z - 5,
                        x + random.Next(5, 40),
                        y + random.Next(5, 40),
                        box.Max.Z + 5
                    );

                    if (body.TrySubtract(tool, out GeoSolid3 result, Tol) && result != null)
                    {
                        body = result;
                    }
                }

                inputs[i] = new ProjectionInput("B" + i, body);
            }

            return inputs;
        }

        internal static GeoCoordinateSystem3 Frame(Random random)
        {
            switch (random.Next(4))
            {
                case 0:
                    return Top;
                case 1:
                    return Front;
                case 2:
                    return Right;
                default:
                    var x = new GeoVector3(
                        random.NextDouble() - 0.5,
                        random.NextDouble() - 0.5,
                        random.NextDouble() - 0.5
                    );
                    var y = new GeoVector3(
                        random.NextDouble() - 0.5,
                        random.NextDouble() - 0.5,
                        random.NextDouble() - 0.5
                    );
                    return new GeoCoordinateSystem3(GeoPoint3.Origin, x, y);
            }
        }
    }
}
