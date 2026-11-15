using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The work of speed15 in the core: a closed loop of arcs in space (<see cref="GeoPolygonArc3"/>, which a closed polyline with
    /// a bulge is read as) walks the vertices it is given by place and measures its area in arrays of the thread's own, and a
    /// plane tests the points of an array or a list by place. The answers are held to the bit against the loop laid out as it was,
    /// and the sizes by the bytes allocated, which do not vary with the machine as a time does.
    /// </summary>
    public class DxfSpeed20Tests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static GeoPoint3[] Rectangle(double width, double height) =>
            new[]
            {
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(width, 0.0, 0.0),
                new GeoPoint3(width, height, 0.0),
                new GeoPoint3(0.0, height, 0.0),
            };

        /// <summary>The area of the loop as it was measured: laid out in its frame, whole, as a polygon, edge by edge.</summary>
        private static double AreaLaidOut(GeoPolygonArc3 loop)
        {
            GeoCoordinateSystem3 frame = loop.GetFrame();
            var starts = new GeoPoint2[loop.EdgeCount];
            var bulges = new double[loop.EdgeCount];

            for (int i = 0; i < loop.EdgeCount; i++)
            {
                GeoEdge3 edge = loop.GetEdgeAt(i);
                starts[i] = PlanarMap.ProjectToPoint2(frame, edge.StartPoint);
                bulges[i] = edge.IsArc && edge.Normal.DotProduct(frame.ZAxis) < 0.0 ? -edge.Bulge : edge.Bulge;
            }

            return GeoPolygonArc2.FromEdgesOf(starts, bulges, default(Tolerance)).Area;
        }

        private static long Bits(double d) => BitConverter.DoubleToInt64Bits(d);

        #region The area

        [Fact]
        public void TheAreaOfARectangleWithOneArc_IsTheLoopLaidOutAndMeasured_ToTheBit()
        {
            // 20 by 20 with the bottom edge bulged 0.5 outwards: 400 and the cap of a half circle of radius 10 that the bulge 1
            // gives, here the cap of the bulge 0.5.
            var loop = new GeoPolygonArc3(
                Rectangle(20.0, 20.0),
                new[] { 0.5, 0.0, 0.0, 0.0 },
                new[] { new GeoVector3(0.0, 0.0, 1.0), default, default, default },
                Tol
            );

            Assert.True(loop.Area > 400.0);
            Assert.Equal(Bits(AreaLaidOut(loop)), Bits(loop.Area));
        }

        [Fact]
        public void TheAreaOfLoopsInAnyPlaneAndAnyTurn_IsTheLoopLaidOutAndMeasured_ToTheBit()
        {
            // 400 loops of three to nine vertices, each with arcs on some edges, stood in a plane of its own, turned either way
            // round, so that some arcs face away from the frame the loop is laid out in.
            var random = new Random(20);
            int checkedLoops = 0;

            for (int n = 0; n < 400; n++)
            {
                int count = 3 + random.Next(7);
                double radius = 5.0 + random.NextDouble() * 50.0;
                var flat = new GeoPoint3[count];
                var bulges = new double[count];
                var flatNormals = new GeoVector3[count];
                bool clockwise = random.Next(2) == 0;

                for (int i = 0; i < count; i++)
                {
                    double angle = (clockwise ? -1.0 : 1.0) * (i + 0.3 * random.NextDouble()) * 2.0 * Math.PI / count;
                    flat[i] = new GeoPoint3(radius * Math.Cos(angle), radius * Math.Sin(angle), 0.0);
                    bulges[i] = random.Next(3) == 0 ? (random.NextDouble() - 0.5) * 1.2 : 0.0;
                    flatNormals[i] = bulges[i] != 0.0 ? new GeoVector3(0.0, 0.0, 1.0) : default;
                }

                // The same loop stood in a plane through a point, turned about two axes.
                double a = random.NextDouble() * 6.28;
                double b = random.NextDouble() * 6.28;
                GeoPoint3[] stood = flat.Select(p => Stand(p, a, b)).ToArray();
                GeoVector3[] normals = flatNormals.Select(v => v.Length == 0.0 ? v : Stand(v, a, b)).ToArray();
                GeoPolygonArc3 loop;

                try
                {
                    loop = new GeoPolygonArc3(stood, bulges, normals, Tol);
                }
                catch (ArgumentException)
                {
                    // Corners too close, or an arc out of the plane by the planar tolerance: the loop is refused as before.
                    continue;
                }

                Assert.Equal(Bits(AreaLaidOut(loop)), Bits(loop.Area));
                checkedLoops++;
            }

            Assert.True(checkedLoops > 300, "only " + checkedLoops + " loops were made");
        }

        private static GeoPoint3 Stand(GeoPoint3 p, double a, double b)
        {
            GeoVector3 v = Stand(new GeoVector3(p.X, p.Y, p.Z), a, b);
            return new GeoPoint3(v.X + 120.0, v.Y - 45.0, v.Z + 300.0);
        }

        private static GeoVector3 Stand(GeoVector3 v, double a, double b)
        {
            double x1 = v.X;
            double y1 = v.Y * Math.Cos(a) - v.Z * Math.Sin(a);
            double z1 = v.Y * Math.Sin(a) + v.Z * Math.Cos(a);
            return new GeoVector3(x1 * Math.Cos(b) + z1 * Math.Sin(b), y1, -x1 * Math.Sin(b) + z1 * Math.Cos(b));
        }

        [Fact]
        public void TheAreaOfALoopOfMoreEdgesThanTheArraysOfTheThreadHold_IsTheLoopLaidOutAndMeasured_ToTheBit()
        {
            // A circle of 150 corners: more than the 64 the thread's own arrays take, laid out in arrays made for the call.
            const int count = 150;
            var corners = new GeoPoint3[count];
            var bulges = new double[count];
            var normals = new GeoVector3[count];

            for (int i = 0; i < count; i++)
            {
                double angle = i * 2.0 * Math.PI / count;
                corners[i] = new GeoPoint3(40.0 * Math.Cos(angle), 40.0 * Math.Sin(angle), 7.0);
                bulges[i] = i % 10 == 0 ? 0.05 : 0.0;
                normals[i] = i % 10 == 0 ? new GeoVector3(0.0, 0.0, 1.0) : default;
            }

            var loop = new GeoPolygonArc3(corners, bulges, normals, Tol);

            Assert.Equal(Bits(AreaLaidOut(loop)), Bits(loop.Area));
        }

        [Fact]
        public void TheAreaOfALoopWithTwoCornersOnTheSamePointWhenLaidOut_IsThatOfTheLoopLaidOutWhole()
        {
            // Two corners 0.005 apart along the normal, which a point tolerance of a millionth keeps and a planar one of a
            // hundredth lets stand in one plane, lie on one point of the plane: laid out, the edge between them has no length and
            // the loop laid out drops it, so that its area is not measured from the arrays of the thread.
            var fine = new Tolerance(1E-6, 1E-5, Tolerance.DefaultEqualAngleRad, 1E-2);
            var corners = new[]
            {
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(10.0, 0.0, 0.0),
                new GeoPoint3(10.0, 10.0, 0.0),
                new GeoPoint3(10.0, 10.0, 0.005),
                new GeoPoint3(0.0, 10.0, 0.0),
            };
            var up = new GeoVector3(0.0, 0.0, 1.0);
            var loop = new GeoPolygonArc3(
                corners,
                new[] { 0.0, 0.3, 0.0, 0.0, 0.0 },
                new[] { default, up, default, default, default },
                fine
            );

            Assert.Equal(5, loop.EdgeCount);
            Assert.Equal(Bits(AreaLaidOut(loop)), Bits(loop.Area));
        }

        [Fact]
        public async System.Threading.Tasks.Task TheAreaOfALoopAskedOnTwoThreadsAtOnce_IsTheSameOnBoth()
        {
            // The arrays are each thread's own: two threads measuring loops of different sizes never share a place in them.
            GeoPolygonArc3 small = new GeoPolygonArc3(
                Rectangle(20.0, 20.0),
                new[] { 0.5, 0.0, 0.0, 0.0 },
                new[] { new GeoVector3(0.0, 0.0, 1.0), default, default, default },
                Tol
            );
            GeoPolygonArc3 big = new GeoPolygonArc3(
                Enumerable
                    .Range(0, 40)
                    .Select(i => new GeoPoint3(
                        30.0 * Math.Cos(i * Math.PI / 20.0),
                        30.0 * Math.Sin(i * Math.PI / 20.0),
                        0.0
                    )),
                null,
                null,
                Tol
            );
            double wantedSmall = AreaLaidOut(small);
            double wantedBig = AreaLaidOut(big);
            var failures = new List<string>();

            void Work(GeoPolygonArc3 loop, double wanted, string name)
            {
                for (int i = 0; i < 20_000; i++)
                {
                    if (Bits(loop.Area) != Bits(wanted))
                    {
                        lock (failures)
                        {
                            failures.Add(name + " at " + i);
                        }

                        return;
                    }
                }
            }

            System.Threading.Tasks.Task a = System.Threading.Tasks.Task.Run(() => Work(small, wantedSmall, "small"));
            System.Threading.Tasks.Task b = System.Threading.Tasks.Task.Run(() => Work(big, wantedBig, "big"));
            await System.Threading.Tasks.Task.WhenAll(a, b);

            Assert.Empty(failures);
        }

        [Fact]
        public void TheAreaOfALoop_MakesNoGarbage()
        {
            // The loop laid out as a polygon of its own and two arrays of it, 176 bytes of each call, before.
            var loop = new GeoPolygonArc3(
                Rectangle(20.0, 20.0),
                new[] { 0.5, 0.0, 0.0, 0.0 },
                new[] { new GeoVector3(0.0, 0.0, 1.0), default, default, default },
                Tol
            );
            double sum = loop.Area;

            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1_000; i++)
            {
                sum += loop.Area;
            }

            long used = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(sum > 0.0);
            Assert.True(used / 1_000 <= 1, "allocated " + used / 1_000 + " bytes a call");
        }

        #endregion

        #region The construction

        [Fact]
        public void ALoopMadeOfAListOfVertices_AllocatesOnlyWhatItHolds()
        {
            // The object (72 bytes) and the three arrays of four vertices, bulges and normals (112, 48 and 112): 344 bytes, 360 as
            // measured; the enumerator of the walk of the list (40 bytes) is not made, and the 448 before are 360.
            var corners = new List<GeoPoint3>(Rectangle(20.0, 20.0));
            var bulges = new List<double> { 0.0, 0.5, 0.0, 0.0 };
            var normals = new List<GeoVector3> { default, new GeoVector3(0.0, 0.0, 1.0), default, default };
            _ = new GeoPolygonArc3(corners, bulges, normals, Tol);

            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1_000; i++)
            {
                _ = new GeoPolygonArc3(corners, bulges, normals, Tol);
            }

            long used = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(used / 1_000 <= 400, "allocated " + used / 1_000 + " bytes a loop");
        }

        [Fact]
        public void ALoopMadeOfASequenceOfVertices_IsTheLoopMadeOfTheirList()
        {
            // A sequence (a query, not a list) is copied once: the loop is the one the list gives, with the vertex that repeats
            // its neighbour and the one that closes on the first dropped.
            GeoPoint3[] corners =
            {
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(10.0, 0.0, 0.0),
                new GeoPoint3(10.0, 0.0005, 0.0),
                new GeoPoint3(10.0, 10.0, 0.0),
                new GeoPoint3(0.0, 10.0, 0.0),
                new GeoPoint3(0.0, 0.0004, 0.0),
            };
            double[] bulges = { 0.0, 0.2, 0.3, 0.0, 0.1, 0.4 };
            GeoVector3[] normals = bulges.Select(b => b != 0.0 ? new GeoVector3(0.0, 0.0, 1.0) : default).ToArray();

            var fromArrays = new GeoPolygonArc3(corners, bulges, normals, Tol);
            var fromSequences = new GeoPolygonArc3(
                corners.Select(p => p),
                bulges.Select(b => b),
                normals.Select(v => v),
                Tol
            );

            Assert.Equal(fromArrays.VertexCount, fromSequences.VertexCount);

            for (int i = 0; i < fromArrays.VertexCount; i++)
            {
                Assert.Equal(fromArrays[i], fromSequences[i]);
                Assert.Equal(Bits(fromArrays.GetBulgeAt(i)), Bits(fromSequences.GetBulgeAt(i)));
                Assert.Equal(fromArrays.GetNormalAt(i), fromSequences.GetNormalAt(i));
            }

            Assert.Equal(Bits(fromArrays.Area), Bits(fromSequences.Area));
        }

        [Fact]
        public void ALoopOfFewerThanThreeDistinctVertices_IsRefusedAsBefore_OfAListAnArrayAndASequence()
        {
            GeoPoint3[] twoAndARepeat =
            {
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(10.0, 0.0, 0.0),
                new GeoPoint3(10.0, 0.0002, 0.0),
                new GeoPoint3(0.0, 0.0001, 0.0),
            };

            Assert.Throws<ArgumentException>(() => new GeoPolygonArc3(twoAndARepeat, null, null, Tol));
            Assert.Throws<ArgumentException>(() =>
                new GeoPolygonArc3(new List<GeoPoint3>(twoAndARepeat), null, null, Tol)
            );
            Assert.Throws<ArgumentException>(() => new GeoPolygonArc3(twoAndARepeat.Select(p => p), null, null, Tol));
            Assert.Throws<ArgumentException>(() => new GeoPolygonArc3(new GeoPoint3[0], null, null, Tol));
        }

        #endregion

        #region The plane

        [Fact]
        public void APlaneTestsTheVerticesOfAnArrayAListAndASequence_AlikeAndStopsAtTheFirstOffIt()
        {
            var plane = new GeoPlane3(new GeoPoint3(0.0, 0.0, 0.0), new GeoVector3(0.0, 0.0, 1.0));
            GeoPoint3[] on =
            {
                new GeoPoint3(1.0, 2.0, 0.0005),
                new GeoPoint3(5.0, 1.0, -0.0004),
                new GeoPoint3(9.0, 9.0, 0.0),
            };
            GeoPoint3[] off =
            {
                new GeoPoint3(1.0, 2.0, 0.0),
                new GeoPoint3(5.0, 1.0, 0.5),
                new GeoPoint3(9.0, 9.0, 0.0),
            };

            Assert.True(plane.ContainsAll(on, Tol));
            Assert.True(plane.ContainsAll(new List<GeoPoint3>(on), Tol));
            Assert.True(plane.ContainsAll(on.Select(p => p), Tol));
            Assert.False(plane.ContainsAll(off, Tol));
            Assert.False(plane.ContainsAll(new List<GeoPoint3>(off), Tol));
            Assert.False(plane.ContainsAll(off.Select(p => p), Tol));
            Assert.True(plane.ContainsAll(new GeoPoint3[0], Tol));
            Assert.Throws<ArgumentNullException>(() => plane.ContainsAll(null, Tol));
        }

        [Fact]
        public void APlaneTestingTheVerticesOfAnArrayOrAList_MakesNoEnumerator()
        {
            var plane = new GeoPlane3(new GeoPoint3(0.0, 0.0, 0.0), new GeoVector3(0.0, 0.0, 1.0));
            GeoPoint3[] array = Rectangle(20.0, 20.0);
            var list = new List<GeoPoint3>(array);
            bool all = plane.ContainsAll(array, Tol) && plane.ContainsAll(list, Tol);

            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1_000; i++)
            {
                all &= plane.ContainsAll(array, Tol) && plane.ContainsAll(list, Tol);
            }

            long used = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(all);
            Assert.True(used / 1_000 <= 1, "allocated " + used / 1_000 + " bytes a call");
        }

        #endregion
    }
}
