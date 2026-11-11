using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public class DimensionFuzzTests
    {
        private readonly ITestOutputHelper _output;

        public DimensionFuzzTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void FiveHundredRandomBoxAssembliesGiveNoErrorTrueValuesAndNoOverlap()
        {
            var failures = new List<string>();
            var watch = Stopwatch.StartNew();
            int dimensions = 0;
            double[] scales = { 1, 5, 10, 20 };

            for (int seed = 0; seed < 500; seed++)
            {
                var random = new Random(seed);
                DrawingObject[] objects = Assembly(random);
                GeoCoordinateSystem3 frame = new[] { Top, Front, Right }[random.Next(3)];
                var settings = new DimensionSettings
                {
                    Scale = scales[random.Next(scales.Length)],
                    Cumulative = random.Next(3) == 0,
                    Ordinate = random.Next(3) == 0,
                    MergeEqual = random.Next(2) == 0,
                };

                try
                {
                    ProjectedView2 view = Project(objects, frame);
                    IReadOnlyList<DrawingDimension> made = Annotator.Dimension(view, objects, frame, settings, Tol);
                    dimensions += made.Count;
                    Check(seed, objects[0], frame, made, failures);

                    foreach (string overlap in Annotator.FindOverlaps(made, Tol))
                    {
                        failures.Add(seed + ": " + overlap);
                    }

                    // The same objects in another order give the same dimensions.
                    DrawingObject[] shuffled = objects.OrderBy(_ => random.Next()).ToArray();
                    string again = string.Join(
                        "\n",
                        Annotator.Dimension(Project(shuffled, frame), shuffled, frame, settings, Tol).Select(Signature)
                    );

                    if (again != string.Join("\n", made.Select(Signature)))
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
                $"500 cases, {dimensions} dimensions, {watch.Elapsed.TotalSeconds:0.0} s, {failures.Count} failures"
            );
            failures.Take(20).ToList().ForEach(_output.WriteLine);
            Assert.Empty(failures);
        }

        /// <summary>
        /// Checks every dimension against the model: its points are where their model points fall in the view, its value is
        /// the distance of its model points along its direction, and the overall dimensions are the main box's sizes.
        /// </summary>
        private static void Check(
            int seed,
            DrawingObject main,
            GeoCoordinateSystem3 frame,
            IReadOnlyList<DrawingDimension> made,
            List<string> failures
        )
        {
            GeoAabb3 box = main.Solid.GetAabb();

            foreach (DrawingDimension d in made)
            {
                bool alongX = d.Direction.X > 0.5;
                GeoVector3 axis = alongX ? frame.XAxis : frame.YAxis;

                foreach (DimensionPoint p in d.Points)
                {
                    GeoPoint3 local = frame.ToLocal(p.ModelPoint);

                    if (
                        Math.Abs(local.X - p.ViewPoint.X) > Tol.EqualPoint
                        || Math.Abs(local.Y - p.ViewPoint.Y) > Tol.EqualPoint
                    )
                    {
                        failures.Add(seed + ": " + d.Id + " point " + p + " is not where its model point falls");
                    }
                }

                double model = Math.Abs(axis.DotProduct(d.Points[0].ModelPoint.GetVectorTo(d.Points[1].ModelPoint)));
                double seen = alongX
                    ? Math.Abs(d.Points[1].ViewPoint.X - d.Points[0].ViewPoint.X)
                    : Math.Abs(d.Points[1].ViewPoint.Y - d.Points[0].ViewPoint.Y);

                if (Math.Abs(model - d.Value) > Tol.EqualPoint || Math.Abs(seen - d.Value) > Tol.EqualPoint)
                {
                    failures.Add(
                        seed
                            + ": "
                            + d.Id
                            + " value "
                            + d.Value
                            + " against "
                            + model
                            + " in the model and "
                            + seen
                            + " in the view"
                    );
                }

                if (d.Kind == DimensionKind.Overall)
                {
                    GeoVector3 size = box.Min.GetVectorTo(box.Max);
                    double expected = Math.Abs(axis.X) * size.X + Math.Abs(axis.Y) * size.Y + Math.Abs(axis.Z) * size.Z;

                    if (Math.Abs(expected - d.Value) > Tol.EqualPoint)
                    {
                        failures.Add(seed + ": overall " + d.Value + " against the main box's " + expected);
                    }
                }
            }
        }

        /// <summary>
        /// One to five boxes square to the axes on whole millimetres, so that they touch and line up often; the first the
        /// main part, some plates with holes through them along Z, and now and then a bolt in a hole.
        /// </summary>
        private static DrawingObject[] Assembly(Random random)
        {
            int count = random.Next(1, 6);
            var objects = new List<DrawingObject>();

            for (int i = 0; i < count; i++)
            {
                int x = random.Next(0, 40) * 10,
                    y = random.Next(0, 40) * 10,
                    z = random.Next(0, 40) * 10;
                int dx = random.Next(2, 60) * 10,
                    dy = random.Next(2, 40) * 10,
                    dz = random.Next(1, 30) * 10;
                string id = "P" + i;

                if (random.Next(3) == 0 && dx >= 80 && dy >= 80)
                {
                    // A plate with a row or two of holes of 22, 25 at least from its sides.
                    int holes = random.Next(1, 5);
                    var centres = new List<GeoPoint2>();
                    double pitch = Math.Max(30, Math.Floor((dx - 50.0) / holes / 10) * 10);

                    foreach (double row in random.Next(2) == 0 ? new[] { dy / 2.0 } : new[] { 30.0, dy - 30.0 })
                    {
                        for (int h = 0; h < holes && 25 + h * pitch <= dx - 25; h++)
                        {
                            centres.Add(new GeoPoint2(25 + h * pitch, row));
                        }
                    }

                    GeoSolid3 plate = GeoSolid3.Extrude(
                        new GeoFace2(Rectangle(0, 0, dx, dy), centres.Select(c => Circle(c.X, c.Y, 11, 24)).ToArray()),
                        new GeoCoordinateSystem3(new GeoPoint3(x, y, z), GeoVector3.XAxis, GeoVector3.YAxis),
                        dz,
                        Tol
                    );
                    objects.Add(
                        Part(
                            id,
                            plate,
                            i == 0,
                            new GeoCoordinateSystem3(new GeoPoint3(x, y, z), GeoVector3.XAxis, GeoVector3.YAxis)
                        )
                    );

                    if (centres.Count > 0 && random.Next(2) == 0)
                    {
                        GeoPoint2 c = centres[0];
                        var start = new GeoPoint3(x + c.X, y + c.Y, z - 15);
                        GeoSolid3 bolt = GeoSolid3.Cylinder(
                            start,
                            new GeoPoint3(x + c.X, y + c.Y, z + dz + 15),
                            10,
                            24,
                            Tol
                        );
                        objects.Add(
                            new DrawingObject(
                                id + "-bolt",
                                DrawingObjectKind.Bolt,
                                bolt,
                                new GeoCoordinateSystem3(start, GeoVector3.ZAxis, GeoVector3.XAxis)
                            )
                        );
                    }
                }
                else
                {
                    objects.Add(
                        Part(
                            id,
                            Box(x, y, z, x + dx, y + dy, z + dz),
                            i == 0,
                            new GeoCoordinateSystem3(new GeoPoint3(x, y, z), GeoVector3.XAxis, GeoVector3.YAxis)
                        )
                    );
                }
            }

            return objects.ToArray();
        }

        /// <summary>A dimension as text, its numbers to the bit, for comparing two runs.</summary>
        private static string Signature(DrawingDimension d)
        {
            string Bits(double v) => BitConverter.DoubleToInt64Bits(v).ToString("X16");
            return d.Id
                + " "
                + d.Kind
                + " "
                + d.Text
                + " "
                + Bits(d.Value)
                + " "
                + d.Tier
                + " "
                + Bits(d.TextPosition.X)
                + " "
                + Bits(d.TextPosition.Y)
                + " "
                + string.Join(
                    ",",
                    d.Points.Select(p =>
                        p.ObjectId
                        + " "
                        + p.Kind
                        + " "
                        + Bits(p.ModelPoint.X)
                        + Bits(p.ModelPoint.Y)
                        + Bits(p.ModelPoint.Z)
                    )
                );
        }
    }
}
