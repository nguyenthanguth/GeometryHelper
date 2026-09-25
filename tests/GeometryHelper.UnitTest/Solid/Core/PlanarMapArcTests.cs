using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Carrying geometry that curves between the plane and space. Until now the round trip was half open: a
    /// curved edge of a plate could be brought into the plane to be worked on, but the answer could only be
    /// put back by flattening it and losing the curves. The property that matters is that the trip closes —
    /// lift a shape and lay it out again and it is the shape that went in.
    /// </summary>
    public class PlanarMapArcTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        /// <summary>
        /// A frame standing well away from the origin and tilted about every axis, so nothing passes by
        /// accident of the identity.
        /// </summary>
        private static GeoCoordinateSystem3 Tilted()
        {
            var plane = new GeoPlane3(new GeoPoint3(37, -14, 52), new GeoVector3(2, -3, 6));

            return new GeoCoordinateSystem3(plane);
        }

        private static GeoArc2 Quarter() => GeoArc2.FromBulge(
            new GeoPoint2(10, 20), new GeoPoint2(60, 70), Math.Tan(Math.PI / 8.0));

        /// <summary>
        /// A slot outline: three straight sides and one that bulges.
        /// </summary>
        private static GeoPolylineArc2 Slot() => new GeoPolylineArc2(
            new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) },
            new[] { 0.0, -1.0, 0.0, 0.0 });

        [Fact]
        public void AnArcLiftedAndLaidOutAgainIsTheArcThatWentIn()
        {
            GeoCoordinateSystem3 frame = Tilted();
            GeoArc2 flat = Quarter();

            GeoArc3 lifted = PlanarMap.ToArc3(frame, flat);

            // The shape is carried, not only the ends.
            Assert.Equal(flat.Radius, lifted.Radius, 7);
            Assert.Equal(Math.Abs(flat.SweptAngle), Math.Abs(lifted.SweptAngle), 7);
            Assert.Equal(flat.Length, lifted.Length, 7);

            // It stands in the plane of the frame, and nowhere else.
            Assert.True(lifted.Normal.IsParallelTo(frame.ZAxis, Loose));
            Assert.Equal(0.0, frame.ToLocal(lifted.Center).Z, 7);

            // And it comes back.
            Assert.True(PlanarMap.TryToArc2(frame, lifted, out GeoArc2 back));
            Assert.True(back.IsEqualTo(flat, Loose));
            Assert.True(PlanarMap.ProjectToArc2(frame, lifted).IsEqualTo(flat, Loose));
        }

        [Fact]
        public void AnArcThatDoesNotLieInTheFrameIsRefusedRatherThanFlattened()
        {
            GeoCoordinateSystem3 frame = Tilted();

            // Tilted out of the plane of the frame.
            var leaning = new GeoArc3(new GeoPoint3(37, -14, 52), new GeoVector3(1, 0, 0), 40.0, 0.0, Math.PI / 2.0);

            Assert.False(PlanarMap.TryToArc2(frame, leaning, out _));

            // In the right plane, but standing off it: a circle of the right tilt sitting above the frame.
            GeoArc3 lifted = PlanarMap.ToArc3(frame, Quarter());
            GeoArc3 raised = lifted.TransformBy(GeoTransform3.Translation(frame.ZAxis.Multiply(25.0)));

            Assert.True(raised.Normal.IsParallelTo(frame.ZAxis, Loose));
            Assert.False(PlanarMap.TryToArc2(frame, raised, out _));

            // Projecting says nothing and answers anyway, which is what its name promises.
            Assert.Equal(lifted.Radius, PlanarMap.ProjectToArc2(frame, raised).Radius, 6);
        }

        [Fact]
        public void AnEdgeMakesTheSameTripAndKeepsItsBulge()
        {
            GeoCoordinateSystem3 frame = Tilted();

            foreach (double bulge in new[] { 0.0, 0.3, -0.3, 1.0, -1.0, 2.5 })
            {
                var flat = new GeoEdge2(new GeoPoint2(10, 20), new GeoPoint2(60, 70), bulge);
                GeoEdge3 lifted = PlanarMap.ToEdge3(frame, flat);

                Assert.Equal(flat.IsArc, lifted.IsArc);
                Assert.Equal(flat.Length, lifted.Length, 7);

                Assert.True(PlanarMap.TryToEdge2(frame, lifted, out GeoEdge2 back), "bulge " + bulge);
                Assert.True(back.IsEqualTo(flat, Loose), "bulge " + bulge);
                Assert.Equal(flat.Bulge, back.Bulge, 7);
            }
        }

        [Fact]
        public void AnArcSeenFromBehindChangesTheSignOfItsBulgeComingDown()
        {
            // The same curve read in a frame facing the other way is the same curve swept the other way, so
            // the bulge turns over. Without that the round trip would come back mirrored.
            var frame = new GeoCoordinateSystem3(new GeoPlane3(GeoPoint3.Origin, new GeoVector3(0, 0, 1)));
            var facingBack = new GeoCoordinateSystem3(new GeoPlane3(GeoPoint3.Origin, new GeoVector3(0, 0, -1)));

            var flat = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), 1.0);
            GeoEdge3 lifted = PlanarMap.ToEdge3(frame, flat);

            Assert.True(PlanarMap.TryToEdge2(frame, lifted, out GeoEdge2 sameWay));
            Assert.Equal(1.0, sameWay.Bulge, 9);

            Assert.True(PlanarMap.TryToEdge2(facingBack, lifted, out GeoEdge2 otherWay));
            Assert.Equal(-1.0, otherWay.Bulge, 9);

            // Either way the curve is the same one: the point half along it lands in the same place.
            GeoPoint3 middle = lifted.GetPointAtParameter(0.5);

            Assert.True(PlanarMap.ToEdge3(frame, sameWay).GetPointAtParameter(0.5).IsEqualTo(middle, Loose));
            Assert.True(PlanarMap.ToEdge3(facingBack, otherWay).GetPointAtParameter(0.5).IsEqualTo(middle, Loose));
        }

        [Fact]
        public void AChainLiftedAndLaidOutAgainIsTheChainThatWentIn()
        {
            GeoCoordinateSystem3 frame = Tilted();
            GeoPolylineArc2 flat = Slot();

            GeoPolylineArc3 lifted = PlanarMap.ToPolylineArc3(frame, flat);

            Assert.Equal(flat.EdgeCount, lifted.EdgeCount);
            Assert.Equal(flat.Length, lifted.Length, 6);
            Assert.True(lifted.IsPlanar());
            Assert.True(lifted.TryGetPlane(out GeoPlane3 plane));
            Assert.True(plane.Normal.IsParallelTo(frame.ZAxis, Loose));

            Assert.True(PlanarMap.TryToPolylineArc2(frame, lifted, out GeoPolylineArc2 back));
            Assert.True(back.IsEqualTo(flat, Loose));

            // Walking both lands in the same places, which is the trip closing on the curve and not only on
            // the vertices.
            for (int i = 0; i <= 20; i++)
            {
                Assert.True(
                    lifted.GetPointAtParameter(i / 20.0).IsEqualTo(
                        PlanarMap.ToPoint3(frame, flat.GetPointAtParameter(i / 20.0)), Loose),
                    "at " + i);
            }
        }

        [Fact]
        public void AChainBentAboutTwoAxesHasNoPlaneToBeLaidOutIn()
        {
            var frame = new GeoCoordinateSystem3(new GeoPlane3(GeoPoint3.Origin, new GeoVector3(0, 0, 1)));

            // A bar bent once in the XY plane and once out of it.
            GeoPolylineArc3 bent = new GeoPolyline3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(300, 0, 0),
                new GeoPoint3(300, 300, 0), new GeoPoint3(300, 300, 300)).Fillet(50.0);

            Assert.False(bent.IsPlanar());
            Assert.False(PlanarMap.TryToPolylineArc2(frame, bent, out _));

            // One that stays flat does come down.
            GeoPolylineArc3 flatBar = new GeoPolyline3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(300, 0, 0), new GeoPoint3(300, 300, 0)).Fillet(50.0);

            Assert.True(flatBar.IsPlanar());
            Assert.True(PlanarMap.TryToPolylineArc2(frame, flatBar, out GeoPolylineArc2 laidOut));
            Assert.Equal(flatBar.Length, laidOut.Length, 6);

            // A single arc bulging out of the frame is enough to refuse the whole chain.
            var oneBad = new GeoPolylineArc3(
                new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(200, 0, 0) },
                new[] { 0.0, 0.5, 0.0 },
                new[] { default(GeoVector3), new GeoVector3(0, 1, 0), default(GeoVector3) });

            Assert.False(PlanarMap.TryToPolylineArc2(frame, oneBad, out _));
        }

        [Fact]
        public void TheRoundTripIsWhatMakesAPlateEdgeWorkable()
        {
            // The whole point of the bridge: a curved plate edge in the model, offset in its own plane, and
            // put back with its curves still curves.
            GeoCoordinateSystem3 frame = Tilted();
            GeoPolylineArc3 inModel = PlanarMap.ToPolylineArc3(frame, Slot());

            Assert.True(PlanarMap.TryToPolylineArc2(frame, inModel, out GeoPolylineArc2 laidOut));

            GeoPolylineArc2[] moved = laidOut.Offset(10.0);

            Assert.NotEmpty(moved);

            GeoPolylineArc3 backInModel = PlanarMap.ToPolylineArc3(frame, moved[0]);

            // It still curves, and it still lies in the plane of the plate.
            Assert.True(backInModel.GetEdgeAt(1).IsArc);
            Assert.True(backInModel.IsPlanar());
            Assert.Equal(moved[0].Length, backInModel.Length, 6);

            // Flattening first would have thrown the curve away, which is what the bridge is for.
            Assert.True(inModel.Flatten().Length < inModel.Length);
        }

        [Fact]
        public void NothingIsAskedOfNothing()
        {
            GeoCoordinateSystem3 frame = Tilted();

            Assert.Throws<ArgumentNullException>(() => PlanarMap.ToPolylineArc3(frame, null));
            Assert.Throws<ArgumentNullException>(() => PlanarMap.TryToPolylineArc2(frame, null, out _));
            Assert.Throws<ArgumentNullException>(() => PlanarMap.TryToPolylineArc2(frame, null, out _, Tolerance.Global));
        }
    }
}
