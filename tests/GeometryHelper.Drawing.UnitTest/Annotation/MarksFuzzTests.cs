using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;
using static GeometryHelper.Drawing.UnitTest.Annotation.AnnotationKit;
using static GeometryHelper.Drawing.UnitTest.Annotation.MarksKit;
using static GeometryHelper.Drawing.UnitTest.Projection.ProjectionKit;

namespace GeometryHelper.Drawing.UnitTest.Annotation
{
    public class MarksFuzzTests
    {
        private readonly ITestOutputHelper _output;

        public MarksFuzzTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void FiveHundredRandomBoxAssembliesGiveMarksOnTheirObjectsClearOfEachOtherAndOfTheDimensions()
        {
            var failures = new List<string>();
            var leftOut = new List<string>();
            var watch = Stopwatch.StartNew();
            int marks = 0, partMarks = 0, wanted = 0;
            double[] scales = { 1, 5, 10, 20 };

            for (int seed = 0; seed < 500; seed++)
            {
                var random = new Random(seed);
                DrawingObject[] objects = Assembly(random);
                GeoCoordinateSystem3 frame = new[] { Top, Front, Right }[random.Next(3)];
                double scale = scales[random.Next(scales.Length)];

                try
                {
                    Marked marked = Mark(objects, frame, scale);
                    marks += marked.Marks.Count;
                    failures.AddRange(Marker.FindOverlaps(marked.Marks, marked.Dimensions, Tol).Select(o => seed + ": " + o));
                    failures.AddRange(LeadersOffTheirObjects(marked).Select(o => seed + ": " + o));

                    // One mark at most for each object, and only for objects with a visible line.
                    var seen = new HashSet<string>(marked.View.VisibleEdges.SelectMany(e => e.ObjectIds));
                    wanted += objects.Count(o => o.Kind == DrawingObjectKind.Part && seen.Contains(o.Id));
                    int partsMarked = marked.Marks.Count(m => m.Kind != MarkKind.Bolt);
                    partMarks += partsMarked;

                    if (partsMarked < objects.Count(o => o.Kind == DrawingObjectKind.Part && seen.Contains(o.Id)))
                    {
                        leftOut.Add(seed + " at 1:" + scale);
                    }

                    foreach (DrawingMark mark in marked.Marks)
                    {
                        if (!seen.Contains(mark.ObjectId))
                        {
                            failures.Add(seed + ": " + mark.Id + " marks " + mark.ObjectId + ", which has no visible line");
                        }
                    }

                    if (marked.Marks.SelectMany(m => m.ObjectIds).Distinct().Count() != marked.Marks.Sum(m => m.ObjectIds.Count))
                    {
                        failures.Add(seed + ": an object is marked twice");
                    }

                    // The same objects in another order give the same marks to the bit.
                    DrawingObject[] shuffled = objects.OrderBy(_ => random.Next()).ToArray();
                    string again = string.Join("\n", Mark(shuffled, frame, scale).Marks.Select(Signature));

                    if (again != string.Join("\n", marked.Marks.Select(Signature)))
                    {
                        failures.Add(seed + ": order");
                    }
                }
                catch (Exception exception)
                {
                    failures.Add(seed + ": " + exception.GetType().Name + " " + exception.Message);
                }
            }

            _output.WriteLine($"500 cases, {marks} marks, {partMarks} of them of the {wanted} parts seen, {watch.Elapsed.TotalSeconds:0.0} s, {failures.Count} failures");
            _output.WriteLine("Marks left out, no place found: " + string.Join(", ", leftOut));
            failures.Take(20).ToList().ForEach(_output.WriteLine);
            Assert.Empty(failures);
        }

        /// <summary>
        /// One to five boxes square to the axes on whole millimetres, as the dimension fuzz makes them, each with a position
        /// mark; some plates with holes, and now and then a bolt in a hole.
        /// </summary>
        internal static DrawingObject[] Assembly(Random random)
        {
            int count = random.Next(1, 6);
            var objects = new List<DrawingObject>();

            for (int i = 0; i < count; i++)
            {
                int x = random.Next(0, 40) * 10, y = random.Next(0, 40) * 10, z = random.Next(0, 40) * 10;
                int dx = random.Next(2, 60) * 10, dy = random.Next(2, 40) * 10, dz = random.Next(1, 30) * 10;
                string id = "P" + i;
                var frame = new GeoCoordinateSystem3(new GeoPoint3(x, y, z), GeoVector3.XAxis, GeoVector3.YAxis);
                GeoSolid3 body;
                GeoPoint2 firstHole = default;
                bool holes = random.Next(3) == 0 && dx >= 80 && dy >= 80;

                if (holes)
                {
                    int across = random.Next(1, 5);
                    var centres = new List<GeoPoint2>();
                    double pitch = Math.Max(30, Math.Floor((dx - 50.0) / across / 10) * 10);

                    foreach (double row in random.Next(2) == 0 ? new[] { dy / 2.0 } : new[] { 30.0, dy - 30.0 })
                    {
                        for (int h = 0; h < across && 25 + h * pitch <= dx - 25; h++)
                        {
                            centres.Add(new GeoPoint2(25 + h * pitch, row));
                        }
                    }

                    firstHole = centres[0];
                    body = GeoSolid3.Extrude(new GeoFace2(Rectangle(0, 0, dx, dy), centres.Select(c => Circle(c.X, c.Y, 11, 24)).ToArray()), frame, dz, Tol);
                }
                else
                {
                    body = Box(x, y, z, x + dx, y + dy, z + dz);
                }

                objects.Add(new DrawingObject(id, DrawingObjectKind.Part, body, frame, mark: "M" + (i % 3), parentId: "A", isMain: i == 0));

                if (holes && random.Next(2) == 0)
                {
                    var start = new GeoPoint3(x + firstHole.X, y + firstHole.Y, z - 15);
                    GeoSolid3 bolt = GeoSolid3.Cylinder(start, new GeoPoint3(x + firstHole.X, y + firstHole.Y, z + dz + 15), 10, 24, Tol);
                    objects.Add(new DrawingObject(id + "-bolt", DrawingObjectKind.Bolt, bolt, new GeoCoordinateSystem3(start, GeoVector3.ZAxis, GeoVector3.XAxis), parentId: "A"));
                }
            }

            return objects.ToArray();
        }
    }
}
