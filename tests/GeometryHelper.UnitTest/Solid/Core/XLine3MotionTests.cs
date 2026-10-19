using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Solid.XLine3Probe;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A rigid motion of a line and a shape together, a turn about an axis and a move, leaves what the line is asked of the
    /// shape as it was: the same distance, the same collision, the same number of crossings, and the crossings carried by the motion.
    /// </summary>
    /// <remarks>
    /// The kinds are those of the fuzz but for the axis-aligned box, which a turn does not keep. 400 pairs of each kind, a
    /// fifth through the shape.
    /// </remarks>
    public class XLine3MotionTests
    {
        public static TheoryData<string> KindRows()
        {
            var data = new TheoryData<string>();

            foreach (string kind in XLine3Gen.Kinds.Where(k => k != "Aabb3"))
            {
                data.Add(kind);
            }

            return data;
        }

        private static object Moved(object shape, GeoTransform3 motion)
        {
            MethodInfo method = shape.GetType().GetMethod("TransformBy", new[] { typeof(GeoTransform3) });

            Assert.True(method != null, shape.GetType().Name + " cannot be moved");
            return method.Invoke(shape, new object[] { motion });
        }

        [Theory]
        [MemberData(nameof(KindRows))]
        public void MovingTheLineAndTheShapeTogether_ChangesNothingTheyAnswer(string kind)
        {
            var random = new Random(404 + Array.IndexOf(XLine3Gen.Kinds, kind));
            var failures = new List<string>();

            for (int i = 0; i < 400; i++)
            {
                XLineSpecimen specimen = XLine3Gen.Make(kind, random);
                GeoXLine3 line = XLine3Gen.MakeLine(random, specimen, i % 5 == 0 ? XLine3Gen.Layout.Random : XLine3Gen.Layout.Through);
                Vec axis = XLine3Gen.RandomUnit(random);
                GeoTransform3 motion = GeoTransform3.Translation(new GeoVector3(random.NextDouble() * 400 - 200, random.NextDouble() * 400 - 200, random.NextDouble() * 400 - 200))
                    .Multiply(GeoTransform3.RotationAxis(new GeoVector3(axis.X, axis.Y, axis.Z), random.NextDouble() * 2 * Math.PI));

                try
                {
                    object shape = specimen.Shape;
                    object movedShape = Moved(shape, motion);
                    GeoXLine3 movedLine = line.TransformBy(motion);
                    bool isPoint = shape is GeoPoint3;

                    double distance = Answers(line, "DistanceTo", shape) ? Distance(line, shape, Strict) : double.NaN;

                    // Away from the point tolerance, so that no answer hangs on the rounding of the motion.
                    double gap = specimen.Distance(new Ln(line));

                    if (Math.Abs(gap - Strict.EqualPoint) < 1E-4)
                    {
                        continue;
                    }

                    if (!isPoint)
                    {
                        Assert.Equal(Collides(line, shape, Strict), Collides(movedLine, movedShape, Strict));
                    }

                    if (!double.IsNaN(distance))
                    {
                        double again = Distance(movedLine, movedShape, Strict);

                        Assert.True(Math.Abs(distance - again) <= 1E-6 * Math.Max(1.0, distance), "distance " + distance + " moved to " + again);
                    }

                    if (Answers(line, "GetIntersections", shape) && !specimen.Delicate(new Ln(line)))
                    {
                        GeoPoint3[] before = Crossings(line, shape, Strict);
                        GeoPoint3[] after = Crossings(movedLine, movedShape, Strict);

                        Assert.True(before.Length == after.Length, before.Length + " crossings moved to " + after.Length);

                        for (int k = 0; k < before.Length; k++)
                        {
                            Assert.True(motion.Transform(before[k]).DistanceTo(after[k]) <= 1E-6, "crossing " + k + " not carried");
                        }
                    }
                }
                catch (Exception e)
                {
                    failures.Add(kind + " #" + i + ": " + e.Message + "\n   shape " + specimen.Shape + "\n   line  " + line);
                }
            }

            Assert.True(failures.Count == 0, failures.Count + " of 400 pairs of " + kind + " changed:\n" + string.Join("\n", failures.Take(8)));
        }
    }
}
