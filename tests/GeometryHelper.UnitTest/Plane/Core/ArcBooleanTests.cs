using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The operations that cannot keep arcs, and say so by what they hand back: the booleans, which go
    /// through Clipper, and the step into space, where there is no arc-carrying shape to become.
    /// </summary>
    public class ArcBooleanTests
    {
        /// <summary>
        /// What the automatic chord tolerance costs: the chords lie inside the arcs, so a flattened slot
        /// encloses about a quarter of a percent less of its round ends, which is under a twentieth of a
        /// percent of the whole.
        /// </summary>
        private const double Flattening = 1E-3;

        /// <summary>
        /// What a chord tolerance of half a thousandth costs, two orders finer.
        /// </summary>
        private const double FineFlattening = 1E-5;
        private static GeoPolygonArc2 Slot()
        {
            return new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 50), new GeoPoint2(0, 50) },
                new[] { 0.0, 1.0, 0.0, 1.0 });
        }

        private static GeoPolygon2 Opening()
        {
            return new GeoPolygon2(
                new GeoPoint2(80, 15), new GeoPoint2(120, 15), new GeoPoint2(120, 35), new GeoPoint2(80, 35));
        }

        [Fact]
        public void ABooleanCutsTheArcsIntoPiecesAndTheTypeSaysSo()
        {
            GeoFace2[] pierced = Slot().Subtract(Opening());

            Assert.Single(pierced);
            Assert.Single(pierced[0].Holes);

            // The result is straight throughout: that is what a GeoFace2 means here.
            double slot = Slot().Area;
            double opening = Opening().Area;

            // Within the flattening, the hole came out of the slot.
            Assert.True(Math.Abs(pierced[0].Area - (slot - opening)) / slot < Flattening);
        }

        [Fact]
        public void EveryBooleanIsThereAndReadsBothWaysRound()
        {
            GeoPolygonArc2 slot = Slot();
            GeoPolygon2 opening = Opening();
            var curvedOpening = new GeoPolygonArc2(opening);

            Assert.Single(slot.Union(curvedOpening));
            Assert.Single(slot.Intersect(curvedOpening));
            Assert.Single(slot.Subtract(curvedOpening));
            Assert.Single(slot.Xor(curvedOpening));

            // The opening is wholly inside, so what they both cover is the opening itself.
            Assert.True(Math.Abs(slot.Intersect(opening)[0].Area - opening.Area) / opening.Area < 1E-9);

            // Union with something inside changes nothing but the flattening.
            Assert.True(Math.Abs(slot.Union(opening)[0].Area - slot.Area) / slot.Area < Flattening);

            // The static form is there as well, either shape straight or curved.
            Assert.Single(Boolean2.Subtract(slot, opening));
            Assert.Single(Boolean2.Subtract(slot.Flatten(), curvedOpening, 0.0, Tolerance.Global));

            Assert.Throws<ArgumentNullException>(() => Boolean2.Union((GeoPolygonArc2)null, curvedOpening));
            Assert.Throws<ArgumentNullException>(() => Boolean2.Union(slot, (GeoPolygonArc2)null));
        }

        [Fact]
        public void AFinerChordToleranceBringsTheAnswerNearer()
        {
            GeoPolygonArc2 slot = Slot();
            GeoPolygon2 opening = Opening();

            double coarse = Boolean2.Subtract(slot, opening, 5.0, Tolerance.Global)[0].Area;
            double fine = Boolean2.Subtract(slot, opening, 0.0005, Tolerance.Global)[0].Area;

            double exact = slot.Area - opening.Area;

            // The chords lie inside the arcs, so both fall short, and the finer one falls less short.
            Assert.True(coarse < exact);
            Assert.True(fine < exact);
            Assert.True(exact - fine < exact - coarse);
            Assert.True((exact - fine) / exact < FineFlattening);
        }

        [Fact]
        public void ACurvedShapeGoesIntoSpaceThroughItsFlattening()
        {
            GeoPolygonArc2 slot = Slot();

            var frame = new GeoCoordinateSystem3(
                new GeoPoint3(1000, 500, 250), GeoVector3.XAxis, GeoVector3.ZAxis);

            GeoPolygon3 placed = slot.ToPolygon3(frame);
            GeoPolygon3 finer = slot.ToPolygon3(frame, 0.0005);

            Assert.True(finer.VertexCount > placed.VertexCount);
            Assert.True(Math.Abs(placed.Area - slot.Area) / slot.Area < Flattening);

            // It lands where the frame puts it.
            Assert.True(placed[0].IsEqualTo(frame.Origin, new Tolerance(1E-9, 1E-9)));

            var chain = new GeoPolylineArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100) },
                new[] { 0.5, 0.0 });

            GeoPolyline3 run = chain.ToPolyline3(frame);

            Assert.True(run.VertexCount > chain.VertexCount);
            Assert.True(Math.Abs(run.Length - chain.Length) / chain.Length < Flattening);
        }
    }
}
