using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Where an arc in space crosses the surface of a box or a body. The surface is flat faces, so this is the
    /// union of what the arc does against each of them — with the shared edges counted once, and with a body's
    /// openings read as surface too.
    /// </summary>
    public class Arc3BodiesTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        /// <summary>
        /// A hundred cube from the origin.
        /// </summary>
        private static GeoAabb3 Crate() => new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(100, 100, 100));

        /// <summary>
        /// A half turn of radius 100 in the plane y = 50, arching from (-100, 50, 0) over (0, 50, 100) to
        /// (100, 50, 0) — so it goes in through one wall of the cube and out through the other.
        /// </summary>
        private static GeoArc3 Arching() => GeoArc3.FromThreePoints(
            new GeoPoint3(-100, 50, 0), new GeoPoint3(0, 50, 100), new GeoPoint3(100, 50, 0));

        [Fact]
        public void AnArcThroughABoxGoesInOneSideAndOutTheOther()
        {
            GeoAabb3 crate = Crate();
            GeoArc3 arch = Arching();

            GeoPoint3[] crossings = Arc3.GetIntersections(arch, crate);

            Assert.NotEmpty(crossings);
            Assert.True(Arc3.CollidesWith(arch, crate));

            // Every crossing is on the arc and on the surface of the box.
            Assert.All(crossings, point => Assert.True(arch.IsPointOn(point, Loose), $"{point} is not on the arc"));
            Assert.All(crossings, point => Assert.True(crate.Contains(point, Loose), $"{point} is not on the box"));

            // The oriented reading is the same answer, not an approximation of it.
            Assert.Equal(Arc3.GetIntersections(arch, crate.ToObb()).Length, crossings.Length);
            Assert.Equal(Arc3.CollidesWith(arch, crate.ToObb()), Arc3.CollidesWith(arch, crate));

            // Well clear of the box and it crosses nothing.
            GeoArc3 away = arch.Translate(new GeoVector3(0, 500, 0));

            Assert.Empty(Arc3.GetIntersections(away, crate));
            Assert.False(Arc3.CollidesWith(away, crate));
        }

        [Fact]
        public void AnArcWhollyInsideABoxCrossesNothingAndIsInItAllTheSame()
        {
            GeoAabb3 crate = Crate();

            // A small arc about the middle of the cube.
            GeoArc3 inside = GeoArc3.FromThreePoints(
                new GeoPoint3(60, 50, 50), new GeoPoint3(50, 60, 50), new GeoPoint3(40, 50, 50));

            Assert.Empty(Arc3.GetIntersections(inside, crate));
            Assert.True(Arc3.CollidesWith(inside, crate));

            // And the same for a body.
            GeoSolid3 cube = crate.ToObb().ToSolid();

            Assert.Empty(Arc3.GetIntersections(inside, cube));
            Assert.True(Arc3.CollidesWith(inside, cube));
        }

        [Fact]
        public void AnEmptyBoxIsCrossedNowhereAndTouchedByNothing()
        {
            var nothing = default(GeoAabb3);

            Assert.True(nothing.IsEmpty);
            Assert.Empty(Arc3.GetIntersections(Arching(), nothing));
            Assert.False(Arc3.CollidesWith(Arching(), nothing));
        }

        [Fact]
        public void AnArcInAnOpeningOfABodyTouchesNothing()
        {
            GeoSolid3 cube = Crate().ToObb().ToSolid();

            // A duct straight through the middle of the cube, from below to above.
            GeoSolid3 duct = new GeoAabb3(new GeoPoint3(40, 40, -10), new GeoPoint3(60, 60, 110)).ToObb().ToSolid();
            GeoSolid3 pierced = cube.WithOpenings(new[] { duct });

            // A small arc inside the duct: it is in the hole, not in the material.
            GeoArc3 inDuct = GeoArc3.FromThreePoints(
                new GeoPoint3(55, 50, 50), new GeoPoint3(50, 55, 50), new GeoPoint3(45, 50, 50));

            Assert.False(Arc3.CollidesWith(inDuct, pierced));
            Assert.Empty(Arc3.GetIntersections(inDuct, pierced));

            // The same arc in the cube without the duct is inside the material.
            Assert.True(Arc3.CollidesWith(inDuct, cube));

            // And an arc reaching out of the duct into the material does touch it, crossing the duct wall.
            GeoArc3 outOfDuct = GeoArc3.FromThreePoints(
                new GeoPoint3(50, 50, 50), new GeoPoint3(70, 50, 60), new GeoPoint3(90, 50, 50));

            Assert.True(Arc3.CollidesWith(outOfDuct, pierced));
            Assert.NotEmpty(Arc3.GetIntersections(outOfDuct, pierced));

            // Every crossing the body gives is one the body itself calls a boundary point.
            Assert.All(Arc3.GetIntersections(outOfDuct, pierced),
                point => Assert.Equal(0.0, pierced.SignedDistanceTo(point), 3));
        }

        [Fact]
        public void ACircleThroughABoxIsTheWholeTurn()
        {
            GeoAabb3 crate = Crate();

            // An upright circle of radius 100 about the centre of the cube's lower face, in the plane y = 50.
            var ring = new GeoCircle3(new GeoPoint3(50, 50, 0), new GeoVector3(0, 1, 0), 100.0);

            Assert.NotEmpty(Arc3.GetIntersections(ring, crate));
            Assert.True(Arc3.CollidesWith(ring, crate));

            // A circle small enough to sit inside crosses nothing and is in it.
            var within = new GeoCircle3(new GeoPoint3(50, 50, 50), GeoVector3.ZAxis, 10.0);

            Assert.Empty(Arc3.GetIntersections(within, crate));
            Assert.True(Arc3.CollidesWith(within, crate));

            // And one well clear touches nothing.
            var clear = new GeoCircle3(new GeoPoint3(500, 500, 500), GeoVector3.ZAxis, 10.0);

            Assert.Empty(Arc3.GetIntersections(clear, crate));
            Assert.False(Arc3.CollidesWith(clear, crate));
        }

        [Fact]
        public void ACornerIsNamedOnceThoughThreeFacesMeetThere()
        {
            GeoAabb3 crate = Crate();

            // An arc through the corner at (100, 100, 100), where three faces meet: it must be one answer.
            GeoArc3 throughCorner = GeoArc3.FromThreePoints(
                new GeoPoint3(200, 100, 100), new GeoPoint3(100, 100, 100), new GeoPoint3(100, 200, 100));

            GeoPoint3[] crossings = Arc3.GetIntersections(throughCorner, crate);

            Assert.Single(crossings, point => point.IsEqualTo(new GeoPoint3(100, 100, 100), Loose));
        }

        [Fact]
        public void TheTypesAnswerTheSameFromEitherSide()
        {
            GeoAabb3 crate = Crate();
            GeoObb3 box = crate.ToObb();
            GeoSolid3 cube = box.ToSolid();
            GeoArc3 arch = Arching();
            var ring = new GeoCircle3(new GeoPoint3(50, 50, 0), new GeoVector3(0, 1, 0), 100.0);

            Assert.Equal(arch.GetIntersections(crate).Length, crate.GetIntersections(arch).Length);
            Assert.Equal(arch.GetIntersections(box).Length, box.GetIntersections(arch).Length);
            Assert.Equal(arch.GetIntersections(cube).Length, cube.GetIntersections(arch).Length);
            Assert.Equal(ring.GetIntersections(crate).Length, crate.GetIntersections(ring).Length);

            Assert.Equal(arch.CollidesWith(crate), crate.CollidesWith(arch));
            Assert.Equal(arch.CollidesWith(box), box.CollidesWith(arch));
            Assert.Equal(arch.CollidesWith(cube), cube.CollidesWith(arch));
            Assert.Equal(ring.CollidesWith(cube), cube.CollidesWith(ring));

            Assert.True(arch.TryIntersectWith(cube, out GeoPoint3[] a));
            Assert.True(cube.TryIntersectWith(arch, out GeoPoint3[] b));
            Assert.Equal(a.Length, b.Length);
        }

        [Fact]
        public void EveryPairTakesAToleranceAndNothingIsAskedOfNothing()
        {
            Tolerance global = Tolerance.Global;
            GeoAabb3 crate = Crate();
            GeoObb3 box = crate.ToObb();
            GeoSolid3 cube = box.ToSolid();
            GeoArc3 arch = Arching();
            var ring = new GeoCircle3(new GeoPoint3(50, 50, 0), new GeoVector3(0, 1, 0), 100.0);

            Assert.Equal(arch.GetIntersections(crate).Length, arch.GetIntersections(crate, global).Length);
            Assert.Equal(arch.GetIntersections(box).Length, arch.GetIntersections(box, global).Length);
            Assert.Equal(arch.GetIntersections(cube).Length, arch.GetIntersections(cube, global).Length);
            Assert.Equal(ring.GetIntersections(cube).Length, ring.GetIntersections(cube, global).Length);
            Assert.Equal(arch.CollidesWith(cube), arch.CollidesWith(cube, global));
            Assert.Equal(cube.CollidesWith(arch), cube.CollidesWith(arch, global));

            Assert.Throws<ArgumentNullException>(() => Arc3.GetIntersections(arch, (GeoSolid3)null));
            Assert.Throws<ArgumentNullException>(() => Arc3.CollidesWith(arch, (GeoSolid3)null));
            Assert.Throws<ArgumentNullException>(() => arch.GetIntersections((GeoSolid3)null));
        }
    }
}
