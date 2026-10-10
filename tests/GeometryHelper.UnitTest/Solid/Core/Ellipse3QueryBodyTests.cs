using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Solid.Ellipse3Oracle;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Decisions 18 and 19: where the rim of an ellipse in space crosses the surface of a box or a body, and whether it
    /// touches one, as <see cref="GeoCircle3"/> answers (Arc3.Bodies.cs). The surface is flat faces, so the crossings are
    /// those of every face, a shared edge or corner counted once (Arc3.Bodies.cs:150-163); a body's openings are surface
    /// too, and a point is kept only where the body itself says it is on its surface, so a point where an opening has taken
    /// the material away is dropped (Arc3.Bodies.cs:36-53). The ellipse touches a box or a body when it crosses it or its rim
    /// is in the material (Arc3.Bodies.cs:103-112, 180-184), so a rim inside an opening touches nothing, and neither does a
    /// box the disc passes round without the rim reaching it. An empty box is crossed nowhere and touched by nothing
    /// (Arc3.Bodies.cs:221-229, 242-250); an axis-aligned box answers as the same oriented box. The bodies are the cube from
    /// the origin to (100, 100, 100) and, for openings, the same cube with a duct (40, 40, -10) to (60, 60, 110) through it,
    /// as in Arc3BodiesTests.
    /// </summary>
    public class Ellipse3QueryBodyTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private const double Close = 1E-9;

        /// <summary>The rim of 80 by 30 at 50 along its major axis from the centre: 30 √(1 - (50/80)²) = 23.4187.</summary>
        private static readonly double AtWall = 30 * Math.Sqrt(1 - 50.0 * 50.0 / (80.0 * 80.0));

        private static GeoAabb3 Crate() => new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(100, 100, 100));

        private static GeoSolid3 Pierced() => Crate().ToObb().ToSolid().WithOpenings(new[]
        {
            new GeoAabb3(new GeoPoint3(40, 40, -10), new GeoPoint3(60, 60, 110)).ToObb().ToSolid(),
        });

        /// <summary>A level ellipse about (50, 50, z), its major axis along X.</summary>
        private static Specimen Level(double z, double a, double b) =>
            new Specimen(new GeoPoint3(50, 50, z), GeoVector3.XAxis, GeoVector3.YAxis, a, b);

        /// <summary>
        /// 80 by 30 level at mid-height: it reaches from x = -30 to 130, out through the walls x = 0 and x = 100 at
        /// y = 50 ± 23.42, and from y = 20 to 80, inside the other two.
        /// </summary>
        private static GeoPoint3[] ThroughTheWalls(double z) => new[]
        {
            new GeoPoint3(0, 50 - AtWall, z), new GeoPoint3(0, 50 + AtWall, z), new GeoPoint3(100, 50 - AtWall, z), new GeoPoint3(100, 50 + AtWall, z),
        };

        [Fact]
        public void AnEllipseThroughABoxCrossesTwoOfItsWallsTwiceEach()
        {
            GeoEllipse3 ellipse = Level(50, 80, 30).Build();

            GeoPoint3[] crossings = ellipse.GetIntersections(Crate(), Tol);

            SamePoints(ThroughTheWalls(50), crossings, Close);
            Assert.True(ellipse.CollidesWith(Crate(), Tol));
            Assert.True(ellipse.TryIntersectWith(Crate(), out GeoPoint3[] tried, Tol));
            SamePointsInOrder(crossings, tried, 0.0);

            // The axis-aligned box is answered as the oriented box it is (Arc3.Bodies.cs:228).
            SamePointsInOrder(ellipse.GetIntersections(Crate().ToObb(), Tol), crossings, 0.0);
            Assert.True(ellipse.CollidesWith(Crate().ToObb(), Tol));
        }

        [Fact]
        public void AnEllipseThroughATurnedBoxCrossesItWhereTheTurnTakesTheCrossings()
        {
            // The cube and the ellipse both turned 30 degrees about the vertical through (50, 50): the four crossings
            // turn with them.
            double c = Math.Cos(Math.PI / 6), s = Math.Sin(Math.PI / 6);
            var axisX = new GeoVector3(c, s, 0);
            var axisY = new GeoVector3(-s, c, 0);
            var centre = new GeoPoint3(50, 50, 50);
            var box = new GeoObb3(centre, 100, 100, 100, axisX, axisY);
            var e = new Specimen(centre, axisX, axisY, 80, 30);
            GeoPoint3 Turn(GeoPoint3 p) => centre.Add(axisX.Multiply(p.X - 50)).Add(axisY.Multiply(p.Y - 50));

            SamePoints(Array.ConvertAll(ThroughTheWalls(50), Turn), e.Build().GetIntersections(box, Tol), Close);
            Assert.True(e.Build().CollidesWith(box, Tol));
        }

        [Fact]
        public void AnEllipseWhollyInsideABoxCrossesNothingAndIsInItAllTheSame()
        {
            GeoEllipse3 ellipse = Level(50, 40, 20).Build();

            Assert.Empty(ellipse.GetIntersections(Crate(), Tol));
            Assert.True(ellipse.CollidesWith(Crate(), Tol));
            Assert.False(ellipse.TryIntersectWith(Crate(), out _, Tol));
            Assert.True(ellipse.CollidesWith(Crate().ToObb(), Tol));
            Assert.True(ellipse.CollidesWith(Crate().ToObb().ToSolid(), Tol));
        }

        [Fact]
        public void AnEllipseWellClearOfABoxTouchesNothing()
        {
            GeoEllipse3 ellipse = new Specimen(new GeoPoint3(550, 50, 50), GeoVector3.XAxis, GeoVector3.YAxis, 80, 30).Build();

            Assert.Empty(ellipse.GetIntersections(Crate(), Tol));
            Assert.False(ellipse.CollidesWith(Crate(), Tol));
            Assert.False(ellipse.CollidesWith(Crate().ToObb().ToSolid(), Tol));
        }

        [Fact]
        public void ABoxTheDiscPassesRoundWithoutTheRimReachingItTouchesNothing()
        {
            // 300 by 200 level through the middle of the cube: the disc cuts the cube in two, but its rim runs at least
            // 150 outside it, and the rim is what is asked about.
            GeoEllipse3 ellipse = Level(50, 300, 200).Build();

            Assert.Empty(ellipse.GetIntersections(Crate(), Tol));
            Assert.False(ellipse.CollidesWith(Crate(), Tol));
            Assert.False(ellipse.CollidesWith(Crate().ToObb().ToSolid(), Tol));
        }

        [Fact]
        public void AnEmptyBoxIsCrossedNowhereAndTouchedByNothing()
        {
            GeoAabb3 nothing = default;

            Assert.True(nothing.IsEmpty);
            Assert.Empty(Level(50, 80, 30).Build().GetIntersections(nothing, Tol));
            Assert.False(Level(50, 80, 30).Build().CollidesWith(nothing, Tol));
            Assert.False(Level(50, 80, 30).Build().TryIntersectWith(nothing, out _, Tol));
        }

        [Fact]
        public void AnEllipseLyingOnTheTopOfABoxInsideItCrossesNothingButTouchesIt()
        {
            // 40 by 20 in the plane z = 100, inside the top face.
            GeoEllipse3 ellipse = Level(100, 40, 20).Build();

            Assert.Empty(ellipse.GetIntersections(Crate(), Tol));
            Assert.True(ellipse.CollidesWith(Crate(), Tol));
        }

        [Fact]
        public void AnEllipseLyingOnTheTopOfABoxAndOverItsEdgesCrossesEachEdgeOnce()
        {
            // 80 by 30 in the plane z = 100 runs over the top edges x = 0 and x = 100: each crossing is found by the top
            // face and by the wall below it, and named once.
            GeoEllipse3 ellipse = Level(100, 80, 30).Build();

            SamePoints(ThroughTheWalls(100), ellipse.GetIntersections(Crate(), Tol), Close);
            SamePoints(ThroughTheWalls(100), ellipse.GetIntersections(Crate().ToObb().ToSolid(), Tol), Close);
        }

        [Fact]
        public void AnEllipseThroughABodyCrossesItAsThroughTheBoxItIs()
        {
            GeoEllipse3 ellipse = Level(50, 80, 30).Build();
            GeoSolid3 cube = Crate().ToObb().ToSolid();

            SamePoints(ThroughTheWalls(50), ellipse.GetIntersections(cube, Tol), Close);
            Assert.True(ellipse.CollidesWith(cube, Tol));
            Assert.True(ellipse.TryIntersectWith(cube, out GeoPoint3[] tried, Tol));
            SamePoints(ThroughTheWalls(50), tried, Close);
        }

        [Fact]
        public void AnEllipseAboutTheDuctCrossesTheWallsOfTheOpening()
        {
            // 15 by 5 about the centre runs from x = 35 to 65, so it crosses the duct's walls x = 40 and x = 60 at
            // y = 50 ± 5 √(1 - (10/15)²) = 50 ± 3.7268, inside the duct's width; in the plain cube it is all in the material.
            double v = 5 * Math.Sqrt(1 - 100.0 / 225.0);
            GeoEllipse3 ellipse = Level(50, 15, 5).Build();
            GeoPoint3[] expected =
            {
                new GeoPoint3(40, 50 - v, 50), new GeoPoint3(40, 50 + v, 50), new GeoPoint3(60, 50 - v, 50), new GeoPoint3(60, 50 + v, 50),
            };

            SamePoints(expected, ellipse.GetIntersections(Pierced(), Tol), Close);
            Assert.True(ellipse.CollidesWith(Pierced(), Tol));
            Assert.Empty(ellipse.GetIntersections(Crate().ToObb().ToSolid(), Tol));
            Assert.True(ellipse.CollidesWith(Crate().ToObb().ToSolid(), Tol));
        }

        [Fact]
        public void AnEllipseInsideTheDuctTouchesNothing()
        {
            // 8 by 4 about the centre is wholly in the opening, which is not material.
            GeoEllipse3 ellipse = Level(50, 8, 4).Build();

            Assert.Empty(ellipse.GetIntersections(Pierced(), Tol));
            Assert.False(ellipse.CollidesWith(Pierced(), Tol));
            Assert.True(ellipse.CollidesWith(Crate().ToObb().ToSolid(), Tol));
        }

        [Fact]
        public void AnEllipseThroughTheMouthOfTheDuctMeetsTheTopOnlyWhereThereIsNoMaterial()
        {
            // Upright in the plane y = 50 about (50, 50, 100), 30 along Z by 8 along X: the rim (50 + 8 sin t, 50,
            // 100 + 30 cos t) crosses the top z = 100 at x = 42 and 58. In the plain cube those are on its top; in the
            // pierced one they are inside the duct, where the top has been taken away, and the rim crosses nothing else
            // of the body (the duct's own top at z = 110 is outside it).
            var e = new Specimen(new GeoPoint3(50, 50, 100), GeoVector3.ZAxis, GeoVector3.XAxis, 30, 8);
            GeoSolid3 cube = Crate().ToObb().ToSolid();

            SamePoints(new[] { new GeoPoint3(42, 50, 100), new GeoPoint3(58, 50, 100) }, e.Build().GetIntersections(cube, Tol), Close);
            Assert.True(e.Build().CollidesWith(cube, Tol));
            Assert.Empty(e.Build().GetIntersections(Pierced(), Tol));
            Assert.False(e.Build().CollidesWith(Pierced(), Tol));
        }

        [Fact]
        public void ANullSolidIsRefused()
        {
            // As the circle's is (Arc3.Bodies.cs:74-77, 105-108).
            GeoEllipse3 ellipse = Level(50, 80, 30).Build();

            Assert.Throws<ArgumentNullException>(() => ellipse.GetIntersections((GeoSolid3)null, Tol));
            Assert.Throws<ArgumentNullException>(() => ellipse.CollidesWith((GeoSolid3)null, Tol));
            Assert.Throws<ArgumentNullException>(() => ellipse.TryIntersectWith((GeoSolid3)null, out _, Tol));
        }
    }
}
