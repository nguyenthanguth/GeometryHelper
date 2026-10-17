using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// An arc lies in a plane when no point of it stands further off the plane than the planar tolerance. Its normal
    /// was compared with the plane's by the angle tolerance instead, a whole degree, which let a half circle 1 000
    /// across turned 0.99 degrees out stand 8.64 off a chain called flat, and refused a bend bowing a ten-millionth of
    /// its chord turned 2 degrees, which stands 0.00000017 off.
    /// </summary>
    public class TiltedArc3Tests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static readonly GeoCoordinateSystem3 Plan = new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis);

        /// <summary>The normal +Z turned by an angle about the X axis, the chord's own direction.</summary>
        private static GeoVector3 Turned(double degrees)
        {
            double angle = degrees * Math.PI / 180;
            return new GeoVector3(0, -Math.Sin(angle), Math.Cos(angle));
        }

        /// <summary>The edge from the origin along +X, bowing about a normal turned out of the plan.</summary>
        private static GeoEdge3 Edge(double chord, double bulge, double degrees)
            => new GeoEdge3(GeoPoint3.Origin, new GeoPoint3(chord, 0, 0), bulge, Turned(degrees), Tol);

        /// <summary>The chain (0, 0, 0), (chord, 0, 0), (chord, chord, 0), its first edge that one.</summary>
        private static GeoPolylineArc3 Chain(double chord, double bulge, double degrees)
            => new GeoPolylineArc3(
                new[] { GeoPoint3.Origin, new GeoPoint3(chord, 0, 0), new GeoPoint3(chord, chord, 0) },
                new[] { bulge, 0 },
                new[] { Turned(degrees), GeoVector3.ZAxis });

        /// <summary>The square of side chord in the plan, its first side that edge.</summary>
        private static GeoPolygonArc3 Loop(double chord, double bulge, double degrees)
            => new GeoPolygonArc3(
                new[] { GeoPoint3.Origin, new GeoPoint3(chord, 0, 0), new GeoPoint3(chord, chord, 0), new GeoPoint3(0, chord, 0) },
                new[] { -bulge, 0, 0, 0 },
                new[] { Turned(degrees), GeoVector3.ZAxis, GeoVector3.ZAxis, GeoVector3.ZAxis },
                Tol);

        [Fact]
        public void AHalfCircleTurnedJustUnderADegreeOutOfItsChain_StandsOffIt()
        {
            // 500 times the sine of 0.99 degrees: 8.64.
            Assert.False(Chain(1000, 1, 0.99).IsPlanar(Tol));
            Assert.Equal("normals", Assert.Throws<ArgumentException>(() => Loop(1000, 1, 0.99)).ParamName);
            Assert.False(PlanarMap.TryToEdge2(Plan, Edge(1000, 1, 0.99), out _, Tol));
            Assert.False(PlanarMap.TryToArc2(Plan, Edge(1000, 1, 0.99).ToArc(), out _, Tol));
        }

        [Fact]
        public void ABendBowingATenMillionthTurnedThirtyDegrees_LiesInThePlane()
        {
            // Half of 100 times a ten-millionth, times the sine of 30 degrees: 0.0000025.
            Assert.True(Chain(100, 1E-7, 30).IsPlanar(Tol));
            Loop(100, 1E-7, 30);
            Assert.True(PlanarMap.TryToEdge2(Plan, Edge(100, 1E-7, 30), out _, Tol));
            Assert.True(PlanarMap.TryToArc2(Plan, Edge(100, 1E-7, 30).ToArc(), out _, Tol));
        }

        [Fact]
        public void AnArcWithItsCentreFarOffTheFrameTurnedATenthOfADegree_IsLaidOut()
        {
            // An edge 100 long bowing a ten-thousandth: radius 250 000, its centre 436 off the plan, the arc 0.0000087.
            Assert.True(PlanarMap.TryToArc2(Plan, Edge(100, 1E-4, 0.1).ToArc(), out GeoArc2 flat, Tol));
            Assert.True(flat.StartPoint.DistanceTo(new GeoPoint2(0, 0)) < 1E-9);
            Assert.True(flat.EndPoint.DistanceTo(new GeoPoint2(100, 0)) < 1E-9);
        }

        [Fact]
        public void AnEdgeLaidOut_ComesDownWithinItsStandOffOfItsShadow()
        {
            // Turned 60 degrees and bowing 0.00001 off a chord of 100, so 0.0000087 off the plan; its middle's shadow
            // is half as far from the chord as the middle laid out bowing as it did.
            GeoEdge3 edge = Edge(100, 2E-7, 60);
            Assert.True(PlanarMap.TryToEdge2(Plan, edge, out GeoEdge2 flat, Tol));

            GeoPoint2 shadow = PlanarMap.ProjectToPoint2(Plan, edge.MidPoint);
            Assert.True(flat.MidPoint.DistanceTo(shadow) <= Tol.EqualPlanar);
        }

        [Fact]
        public void AFlatChainWithABendBowingATrillionth_IsFlat()
        {
            // Read from the chord, not from a centre 2.5E13 away that is known only to 0.005.
            Assert.True(Chain(100, 1E-12, 0).IsPlanar(Tol));
            Loop(100, 1E-12, 0);
            Assert.True(PlanarMap.TryToEdge2(Plan, Edge(100, 1E-12, 0), out _, Tol));
        }

        [Theory]
        [InlineData(0.001, true)]
        [InlineData(0.01, false)]
        public void AnArcPastAHalfTurn_IsMeasuredOutPastTheEndsOfItsChord(double degrees, bool lies)
        {
            // A bulge of 2 on a chord of 10: radius 6.25, bowing 10 off the chord. Turned 0.001 degrees it stands
            // 0.00017 off, turned 0.01 degrees 0.0017.
            Assert.Equal(lies, Chain(10, 2, degrees).IsPlanar(Tol));
            Assert.Equal(lies, PlanarMap.TryToEdge2(Plan, Edge(10, 2, degrees), out _, Tol));
            Assert.Equal(lies, PlanarMap.TryToArc2(Plan, Edge(10, 2, degrees).ToArc(), out _, Tol));
        }

        [Fact]
        public void AnArcNearlySquareToTheFrameThatBarelyBows_IsLaidOutBetweenItsEnds()
        {
            // Bowing 0.00000005 off a chord of 100 and turned 89 degrees out, it stands that far off the plan; its
            // shadow bows less than a billionth, three points on a line to any point tolerance.
            GeoArc3 arc = Edge(100, 1E-9, 89).ToArc();

            Assert.True(PlanarMap.TryToArc2(Plan, arc, out GeoArc2 flat, Tol));
            Assert.True(flat.StartPoint.DistanceTo(new GeoPoint2(0, 0)) < 1E-4);
            Assert.True(flat.EndPoint.DistanceTo(new GeoPoint2(100, 0)) < 1E-4);
        }

        [Theory]
        [InlineData(0.5, true)]
        [InlineData(3.0, false)]
        public void AnArcOnARisingChord_IsMeasuredWhereItRunsPastItsEnds(double bulge, bool lies)
        {
            // The chord rises 0.0009 over 10, and the arc bows in the plane through it square to the plan's Y. Up to a
            // half turn it stays between its ends, at most 0.0009 up; a bulge of 3 has radius 8.33 and runs on to 13.33
            // along, 0.0012 up.
            var end = new GeoPoint3(10, 0, 0.0009);
            GeoVector3 normal = GeoPoint3.Origin.GetVectorTo(end).CrossProduct(GeoVector3.YAxis).Multiply(-1).Normalize();
            var edge = new GeoEdge3(GeoPoint3.Origin, end, bulge, normal, Tol);

            Assert.Equal(lies, PlanarMap.TryToEdge2(Plan, edge, out _, Tol));
            Assert.Equal(lies, PlanarMap.TryToArc2(Plan, edge.ToArc(), out _, Tol));
        }

        [Fact]
        public void ALoopSaddledWithinTheTolerance_LiesInItsPlane()
        {
            // Two corners stand 0.0009 above the plan and two on it; the half circle from the second corner to the third
            // bows out along +X and dips back across the plan as it goes, never more than 0.0009 off it.
            var corners = new[] { GeoPoint3.Origin, new GeoPoint3(100, 0, 0.0009), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0.0009) };
            GeoVector3 along = corners[1].GetVectorTo(corners[2]).Normalize();
            GeoVector3 bow = new GeoVector3(1, 0, -1.8E-5).Normalize();
            GeoVector3 normal = bow.CrossProduct(along).Normalize();

            var loop = new GeoPolygonArc3(corners, new[] { 0, 1, 0, 0.0 }, new[] { normal, normal, normal, normal }, Tol);
            var chain = new GeoPolylineArc3(corners, new[] { 0, 1, 0.0 }, new[] { normal, normal, normal });

            Assert.True(loop.Length > 400);
            Assert.True(chain.IsPlanar(Tol));
        }

        [Fact]
        public void AnArcTurnedJustOverADegreeLaidOutInAFrameFacingAway_BowsTowardsItsShadow()
        {
            // Bowing 0.025 off a chord of 100 and turned 1.2 degrees out, it stands 0.0005 off the plan and lies in it.
            // Laid out bowing the wrong way it would land 0.05 from its shadow.
            var away = new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, new GeoVector3(0, -1, 0));
            GeoEdge3 edge = Edge(100, 0.0005, 1.2);
            GeoPoint2 shadow = PlanarMap.ProjectToPoint2(away, edge.MidPoint);

            Assert.True(PlanarMap.TryToEdge2(away, edge, out GeoEdge2 flatEdge, Tol));
            Assert.True(flatEdge.MidPoint.DistanceTo(shadow) <= Tol.EqualPlanar);

            Assert.True(PlanarMap.TryToArc2(away, edge.ToArc(), out GeoArc2 flatArc, Tol));
            Assert.True(flatArc.MidPoint.DistanceTo(shadow) <= Tol.EqualPlanar);
        }

        [Fact]
        public void ASmallWholeCircleTurnedTwoDegrees_IsLaidOutAsAWholeCircle()
        {
            // Radius 0.02 turned 2 degrees: its rim stands 0.0007 off the plan, so it lies in it.
            var circle = new GeoArc3(new GeoPoint3(5, 6, 0), Turned(2), 0.02, 0, 0);

            Assert.True(PlanarMap.TryToArc2(Plan, circle, out GeoArc2 flat, Tol));
            Assert.Equal(2 * Math.PI, Math.Abs(flat.SweptAngle), 12);
            Assert.Equal(0.02, flat.Radius, 12);
            Assert.True(flat.Center.DistanceTo(new GeoPoint2(5, 6)) < 1E-12);
        }

        [Fact]
        public void AnArcAHairShortOfAWholeTurn_IsMeasuredRoundItsCircle()
        {
            // Its start and end are one point to the last digit, so its chord says nothing; turned 45 degrees, its rim
            // stands 350 off the plan, and lying in a tilted plane far from the origin it lies in that plane.
            var tilted = new GeoArc3(new GeoPoint3(1E5, 2E4, 3E4), Turned(45), 500, 1.0, 1.0 - 1E-15);
            var plan = new GeoCoordinateSystem3(new GeoPoint3(1E5, 2E4, 3E4), GeoVector3.XAxis, GeoVector3.YAxis);
            Assert.False(PlanarMap.TryToArc2(plan, tilted, out _, Tol));

            var normal = new GeoVector3(1, 2, 3).Normalize();
            var lying = new GeoArc3(new GeoPoint3(52000, 31000, 7000), normal, 500, 0.5, 0.5 - 1E-12);
            var own = new GeoCoordinateSystem3(new GeoPlane3(new GeoPoint3(52000, 31000, 7000), normal));
            Assert.True(PlanarMap.TryToArc2(own, lying, out GeoArc2 flat, Tol));
            Assert.Equal(500, flat.Radius, 6);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.9)]
        [InlineData(1.1)]
        public void AnArcHalfADegreeShortOfAWholeTurn_IsLaidOutAsItIsHoweverItIsTurned(double degrees)
        {
            // Radius 0.03, so turned up to 1.1 degrees it stays within the tolerance of the plan; it comes down 359.5
            // degrees round, starting where it starts, not as a whole circle.
            double sweep = 359.5 * Math.PI / 180;
            var arc = new GeoArc3(new GeoPoint3(5, 6, 0), Turned(degrees), 0.03, 0.7, 0.7 + sweep);

            Assert.True(PlanarMap.TryToArc2(Plan, arc, out GeoArc2 flat, Tol));
            Assert.Equal(sweep, Math.Abs(flat.SweptAngle), 9);
            Assert.True(flat.StartPoint.DistanceTo(PlanarMap.ProjectToPoint2(Plan, arc.StartPoint)) <= Tol.EqualPlanar);
        }

        [Fact]
        public void AnArcAHairShortOfAWholeTurnInThePlan_ComesDownOnItsShadow()
        {
            var arc = new GeoArc3(new GeoPoint3(5, 6, 0), Turned(0.0005), 9.985, 0.4, 0.4 - 2E-10);

            Assert.True(PlanarMap.TryToArc2(Plan, arc, out GeoArc2 flat, Tol));
            Assert.True(flat.GetPointAtParameter(0.5).DistanceTo(PlanarMap.ProjectToPoint2(Plan, arc.GetPointAtParameter(0.5))) <= Tol.EqualPlanar);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(2.0)]
        public void AnArcTooShortToHaveAChord_IsNotLaidOut(double degrees)
        {
            // A sweep of 1E-15 at radius 0.02: its two ends are one point, and no arc of the plane runs between them.
            var sliver = new GeoArc3(new GeoPoint3(5, 6, 0), Turned(degrees), 0.02, 1.0, 1.0 + 1E-15);

            Assert.False(PlanarMap.TryToArc2(Plan, sliver, out _, Tol));
        }

        [Theory]
        [InlineData(0.0001, true)]
        [InlineData(0.001, false)]
        public void AWholeCircle_LiesInThePlaneWhenItsRimDoes(double degrees, bool lies)
        {
            // Radius 100: turned 0.0001 degrees its rim stands 0.00017 off, turned 0.001 degrees 0.0017.
            var circle = new GeoArc3(new GeoPoint3(50, 50, 0), Turned(degrees), 100, 0, 0);

            Assert.Equal(lies, PlanarMap.TryToArc2(Plan, circle, out _, Tol));
        }
    }
}
