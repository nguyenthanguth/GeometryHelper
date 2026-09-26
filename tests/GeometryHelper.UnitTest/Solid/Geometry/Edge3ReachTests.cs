using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A <see cref="GeoEdge3"/> is one leg of a bar: a straight run or a bend, and nothing else. So every
    /// question it answers about another shape is that reading handed on — and the test that matters is that
    /// it answers exactly what the shape it stands for answers, since an edge disagreeing with its own arc
    /// would be a second, quieter opinion.
    /// </summary>
    public class Edge3ReachTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        /// <summary>
        /// A hundred cube from the origin.
        /// </summary>
        private static GeoAabb3 Crate() => new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(100, 100, 100));

        /// <summary>
        /// A straight leg running through the cube along y = z = 50.
        /// </summary>
        private static GeoEdge3 Straight() =>
            new GeoEdge3(new GeoPoint3(-50, 50, 50), new GeoPoint3(150, 50, 50));

        /// <summary>
        /// The same chord bowed into a half turn in the plane z = 50, so it leaves the chord entirely.
        /// </summary>
        private static GeoEdge3 Bent() =>
            new GeoEdge3(new GeoPoint3(-50, 50, 50), new GeoPoint3(150, 50, 50), 1.0, GeoVector3.ZAxis);

        [Fact]
        public void AnEdgeAnswersExactlyWhatTheShapeItStandsForAnswers()
        {
            GeoEdge3 straight = Straight();
            GeoEdge3 bent = Bent();

            GeoAabb3 crate = Crate();
            GeoObb3 box = crate.ToObb();
            GeoSolid3 cube = box.ToSolid();
            var plane = new GeoPlane3(new GeoPoint3(50, 0, 0), new GeoVector3(1, 0, 0));
            var flat = new GeoTriangle3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(0, 100, 0));

            Assert.False(straight.IsArc);
            Assert.True(bent.IsArc);

            Assert.Equal(straight.ToLine().GetIntersections(crate).Length, straight.GetIntersections(crate).Length);
            Assert.Equal(straight.ToLine().GetIntersections(cube).Length, straight.GetIntersections(cube).Length);
            Assert.Equal(straight.ToLine().GetIntersections(plane).Length, straight.GetIntersections(plane).Length);
            Assert.Equal(straight.ToLine().CollidesWith(cube), straight.CollidesWith(cube));
            Assert.Equal(straight.ToLine().CollidesWith(flat), straight.CollidesWith(flat));

            Assert.Equal(bent.ToArc().GetIntersections(crate).Length, bent.GetIntersections(crate).Length);
            Assert.Equal(bent.ToArc().GetIntersections(cube).Length, bent.GetIntersections(cube).Length);
            Assert.Equal(bent.ToArc().GetIntersections(plane).Length, bent.GetIntersections(plane).Length);
            Assert.Equal(bent.ToArc().CollidesWith(cube), bent.CollidesWith(cube));
            Assert.Equal(bent.ToArc().CollidesWith(box), bent.CollidesWith(box));
        }

        [Fact]
        public void AStraightLegThroughABoxGoesInOneSideAndOutTheOther()
        {
            GeoEdge3 straight = Straight();
            GeoAabb3 crate = Crate();

            GeoPoint3[] crossings = straight.GetIntersections(crate);

            Assert.Equal(2, crossings.Length);
            Assert.True(straight.CollidesWith(crate));

            // It enters at x = 0 and leaves at x = 100.
            foreach (GeoPoint3 point in crossings)
            {
                Assert.True(Math.Abs(point.X) < 1E-6 || Math.Abs(point.X - 100.0) < 1E-6, point.ToString());
            }

            // A leg clear of the box crosses nothing.
            GeoEdge3 away = straight.Translate(new GeoVector3(0, 500, 0));

            Assert.Empty(away.GetIntersections(crate));
            Assert.False(away.CollidesWith(crate));
        }

        [Fact]
        public void ABendIsMeasuredAsTheArcItIsAndNotAsItsChord()
        {
            GeoEdge3 straight = Straight();
            GeoEdge3 bent = Bent();
            GeoAabb3 crate = Crate();

            // The chord of the bend is the straight leg, so if the bend were read as its chord the two would
            // give the same answer. They do not: the bend arches out to y = 150, clear of the cube for most
            // of its length, and meets it somewhere else entirely.
            Assert.Equal(straight.GetChord().StartPoint, bent.GetChord().StartPoint);
            Assert.Equal(straight.GetChord().EndPoint, bent.GetChord().EndPoint);

            GeoPoint3[] alongChord = straight.GetIntersections(crate);
            GeoPoint3[] alongArc = bent.GetIntersections(crate);

            Assert.NotEqual(
                string.Join(";", Array.ConvertAll(alongChord, point => point.ToString())),
                string.Join(";", Array.ConvertAll(alongArc, point => point.ToString())));

            // And every crossing the bend reports really is on the bend.
            Assert.All(alongArc, point => Assert.True(bent.ToArc().IsPointOn(point, Loose), point.ToString()));
        }

        [Fact]
        public void EveryShapeCanBeAskedAboutAnEdgeAndAgreesWithIt()
        {
            GeoEdge3 straight = Straight();
            GeoEdge3 bent = Bent();

            GeoAabb3 crate = Crate();
            GeoObb3 box = crate.ToObb();
            GeoSolid3 cube = box.ToSolid();
            var plane = new GeoPlane3(new GeoPoint3(50, 0, 0), new GeoVector3(1, 0, 0));
            var flat = new GeoTriangle3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(0, 100, 0));
            var polygon = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0));
            var face = new GeoFace3(polygon);

            foreach (GeoEdge3 edge in new[] { straight, bent })
            {
                Assert.Equal(edge.GetIntersections(crate).Length, crate.GetIntersections(edge).Length);
                Assert.Equal(edge.GetIntersections(box).Length, box.GetIntersections(edge).Length);
                Assert.Equal(edge.GetIntersections(cube).Length, cube.GetIntersections(edge).Length);
                Assert.Equal(edge.GetIntersections(plane).Length, plane.GetIntersections(edge).Length);
                Assert.Equal(edge.GetIntersections(flat).Length, flat.GetIntersections(edge).Length);
                Assert.Equal(edge.GetIntersections(polygon).Length, polygon.GetIntersections(edge).Length);
                Assert.Equal(edge.GetIntersections(face).Length, face.GetIntersections(edge).Length);

                Assert.Equal(edge.CollidesWith(crate), crate.CollidesWith(edge));
                Assert.Equal(edge.CollidesWith(box), box.CollidesWith(edge));
                Assert.Equal(edge.CollidesWith(cube), cube.CollidesWith(edge));
                Assert.Equal(edge.CollidesWith(flat), flat.CollidesWith(edge));
                Assert.Equal(edge.CollidesWith(polygon), polygon.CollidesWith(edge));
                Assert.Equal(edge.CollidesWith(face), face.CollidesWith(edge));
            }
        }

        [Fact]
        public void AnEdgeAndACurveCanBeAskedAboutEachOther()
        {
            GeoEdge3 bent = Bent();

            // A circle in the plane of the bend, crossing it.
            var ring = new GeoCircle3(new GeoPoint3(50, 50, 50), GeoVector3.ZAxis, 100.0);
            // An arc in a plane square to the bend's, through the point the bend arches to.
            GeoPoint3 apex = bent.ToArc().MidPoint;
            var arc = GeoArc3.FromThreePoints(
                apex.Add(new GeoVector3(0, -100, 0)), apex, apex.Add(new GeoVector3(0, 0, 100)));

            Assert.Equal(bent.GetIntersections(ring).Length, ring.GetIntersections(bent).Length);
            Assert.Equal(bent.CollidesWith(ring), ring.CollidesWith(bent));
            Assert.Equal(bent.GetIntersections(arc).Length, arc.GetIntersections(bent).Length);
            Assert.Equal(bent.CollidesWith(arc), arc.CollidesWith(bent));

            // They do meet, at the point the bend arches to.
            Assert.True(bent.CollidesWith(arc));

            Assert.Equal(
                bent.TryIntersectWith(ring, out GeoPoint3[] a),
                ring.TryIntersectWith(bent, out GeoPoint3[] b));
            Assert.Equal(a.Length, b.Length);
        }

        [Fact]
        public void EveryNewDirectionTakesAToleranceAndNothingIsAskedOfNothing()
        {
            Tolerance global = Tolerance.Global;
            GeoEdge3 bent = Bent();
            GeoAabb3 crate = Crate();
            GeoSolid3 cube = crate.ToObb().ToSolid();
            var plane = new GeoPlane3(new GeoPoint3(50, 0, 0), new GeoVector3(1, 0, 0));

            Assert.Equal(bent.GetIntersections(crate).Length, bent.GetIntersections(crate, global).Length);
            Assert.Equal(bent.GetIntersections(cube).Length, bent.GetIntersections(cube, global).Length);
            Assert.Equal(bent.GetIntersections(plane).Length, bent.GetIntersections(plane, global).Length);
            Assert.Equal(bent.CollidesWith(cube), bent.CollidesWith(cube, global));
            Assert.Equal(crate.CollidesWith(bent), crate.CollidesWith(bent, global));
            Assert.Equal(cube.GetIntersections(bent).Length, cube.GetIntersections(bent, global).Length);

            Assert.Throws<ArgumentNullException>(() => bent.GetIntersections((GeoSolid3)null));
            Assert.Throws<ArgumentNullException>(() => bent.CollidesWith((GeoSolid3)null));
            Assert.Throws<ArgumentNullException>(() => bent.GetIntersections((GeoFace3)null));
        }
    }
}
