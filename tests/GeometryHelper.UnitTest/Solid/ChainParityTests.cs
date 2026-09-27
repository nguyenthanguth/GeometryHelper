using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The chains of the plane and of space offer the same members in the same shape of call.
    /// </summary>
    /// <remarks>
    /// A chain in space had its ends and the plane's did not; one cut in two gave two pieces in the plane and a
    /// list in space; a bent bar in space was cut at several distances by a method answering true or false
    /// where every other chain handed back the pieces. Code moving between the two had to be rewritten for
    /// nothing. Each now has both shapes of call, and a test keeps it so.
    /// </remarks>
    public class ChainParityTests
    {
        private static readonly Type[] Chains = { typeof(GeoPolyline2), typeof(GeoPolylineArc2), typeof(GeoPolyline3), typeof(GeoPolylineArc3) };

        private static GeoPolyline2 Bar2() => new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 60));

        private static GeoPolyline3 Bar3() => new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 60, 30));

        [Fact]
        public void EveryChainHasTheSameMembersInTheSameShape()
        {
            foreach (Type chain in Chains)
            {
                Type point = chain.Name.EndsWith("2") ? typeof(GeoPoint2) : typeof(GeoPoint3);

                foreach (string name in new[] { "StartPoint", "EndPoint", "MidPoint" })
                {
                    Assert.True(chain.GetProperty(name)?.PropertyType == point, $"{chain.Name}.{name}");
                }

                Assert.NotNull(chain.GetMethod("TrySplitAtDistance", new[] { typeof(double), chain.MakeByRefType(), chain.MakeByRefType() }));
                Assert.NotNull(chain.GetMethod("TrySplitAtDistance", new[] { typeof(double), chain.MakeArrayType().MakeByRefType() }));
                Assert.Equal(chain.MakeArrayType(), chain.GetMethod("SplitAtDistances", new[] { typeof(IEnumerable<double>) })?.ReturnType);
            }

            foreach (Type edge in new[] { typeof(GeoEdge2), typeof(GeoEdge3) })
            {
                Assert.NotNull(edge.GetProperty("MidPoint"));
            }
        }

        [Fact]
        public void EndsAndMiddlesAreWhereTheyShouldBe()
        {
            GeoPolyline2 straight = Bar2();
            GeoPolylineArc2 bent = new GeoPolylineArc2(straight).Fillet(20);
            GeoPolyline3 inSpace = Bar3();
            GeoPolylineArc3 bentInSpace = inSpace.Fillet(20);

            Assert.Equal(new GeoPoint2(0, 0), straight.StartPoint);
            Assert.Equal(new GeoPoint2(100, 60), straight.EndPoint);
            Assert.Equal(bent.Vertices[0], bent.StartPoint);
            Assert.Equal(bent.Vertices[bent.VertexCount - 1], bent.EndPoint);

            Assert.True(bent.MidPoint.DistanceTo(bent.GetPointAtDistance(bent.Length / 2)) < 1E-12);
            Assert.True(inSpace.MidPoint.DistanceTo(new GeoPoint3(inSpace.Length / 2, 0, 0)) < 1E-9);
            Assert.True(bentInSpace.MidPoint.DistanceTo(bentInSpace.GetPointAtDistance(bentInSpace.Length / 2)) < 1E-12);

            var arc = new GeoArc2(new GeoPoint2(0, 0), 10, 0, Math.PI);
            Assert.True(new GeoEdge2(arc).MidPoint.DistanceTo(new GeoPoint2(0, 10)) < 1E-9);
            Assert.True(new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)).MidPoint.DistanceTo(new GeoPoint3(5, 0, 0)) < 1E-12);
        }

        [Fact]
        public void BothShapesOfSplittingGiveTheSamePieces()
        {
            GeoPolyline2 straight = Bar2();
            GeoPolyline3 inSpace = Bar3();
            GeoPolylineArc3 bentInSpace = inSpace.Fillet(20);

            Assert.True(straight.TrySplitAtDistance(40, out GeoPolyline2 a, out GeoPolyline2 b));
            Assert.True(straight.TrySplitAtDistance(40, out GeoPolyline2[] two));
            Assert.Equal(new[] { a.Length, b.Length }, two.Select(p => p.Length));

            Assert.True(inSpace.TrySplitAtDistance(40, out GeoPolyline3 c, out GeoPolyline3 d));
            Assert.Equal(inSpace.Length, c.Length + d.Length, 9);
            Assert.Equal(40.0, c.Length, 9);

            Assert.True(bentInSpace.TrySplitAtDistance(40, out GeoPolylineArc3 e, out GeoPolylineArc3 f));
            Assert.Equal(bentInSpace.Length, e.Length + f.Length, 9);

            GeoPolylineArc3[] schedule = bentInSpace.SplitAtDistances(new[] { 30.0, 90.0 });
            Assert.True(bentInSpace.SplitAtDistances(new[] { 30.0, 90.0 }, out GeoPolylineArc3[] same));
            Assert.Equal(same.Select(p => p.Length), schedule.Select(p => p.Length));
            Assert.Equal(3, schedule.Length);
        }

        [Fact]
        public void ADistanceOffTheChainCutsNothingInEitherShape()
        {
            GeoPolyline2 straight = Bar2();
            GeoPolyline3 inSpace = Bar3();

            Assert.False(straight.TrySplitAtDistance(-5, out GeoPolyline2 a, out GeoPolyline2 b));
            Assert.Null(a);
            Assert.Null(b);
            Assert.False(straight.TrySplitAtDistance(1000, out GeoPolyline2[] whole));
            Assert.Same(straight, Assert.Single(whole));

            Assert.False(inSpace.TrySplitAtDistance(1000, out GeoPolyline3 c, out GeoPolyline3 d));
            Assert.Null(c);
            Assert.Null(d);

            GeoPolylineArc3 bentInSpace = inSpace.Fillet(20);
            Assert.Single(bentInSpace.SplitAtDistances(new[] { -1.0, 1E9 }));
        }
    }
}
