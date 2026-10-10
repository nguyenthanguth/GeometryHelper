using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Solid.Ellipse3Oracle;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Decisions 18 and 19: where the rim of an ellipse in space crosses a triangle, a polygon or a face, and whether it
    /// touches one, as <see cref="GeoCircle3"/> answers (Arc3.Flat.cs). Standing across the ellipse's plane, the shape is
    /// crossed where the rim meets its plane and the shape holds the point, its material only, so a hole lets the rim
    /// through (Arc3.Flat.cs:84-90, 257-261), in order along N × the shape's normal. Lying in the ellipse's plane, it is
    /// crossed where its edges cut the rim, edge by edge, the outline before the holes, a corner counted once
    /// (Arc3.Flat.cs:92-96, 263-272, 34-44); it touches the rim when one crosses or the shape holds the rim whole
    /// (Arc3.Flat.cs:114-123, 198-207, 291-300), so a shape lying inside the disc, short of the rim, touches nothing.
    /// TryIntersectWith is true exactly when there is a crossing. The 2D ellipse laid out in the plane answers every
    /// coplanar case, edge by edge.
    /// </summary>
    public class Ellipse3QueryFlatTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private const double Close = 1E-9;

        /// <summary>The rim of 300 by 100 at x = ±100: y = ±100 √(1 - 1/9) = ±94.2809.</summary>
        private static readonly double AtHundred = 100 * Math.Sqrt(8.0 / 9.0);

        /// <summary>The rim at y = ±50: x = ±300 √(1 - 0.25) = ±259.8076.</summary>
        private static readonly double AtFifty = 300 * Math.Sqrt(0.75);

        /// <summary>
        /// The chord from the rim point at t = 0.3 to the one at t = 2.5, a plane leaning 60 degrees about it, and the
        /// direction W in that plane square to the chord, so that shapes can be stood on the chord's line.
        /// </summary>
        private static void Standing(Specimen e, out GeoPoint3 first, out GeoPoint3 second, out GeoVector3 chord, out GeoVector3 w)
        {
            first = e.Rim(0.3);
            second = e.Rim(2.5);
            chord = first.GetVectorTo(second);
            GeoVector3 normal = LeanAbout(e, chord, 60 * Math.PI / 180);
            w = normal.CrossProduct(chord.Normalize());
        }

        /// <summary>A polygon of points given in the frame of the ellipse, in its plane and a height above it.</summary>
        private static GeoPolygon3 FramePolygon(Specimen e, double h, params double[] uv)
        {
            var points = new GeoPoint3[uv.Length / 2];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = e.At(uv[2 * i], uv[2 * i + 1], h);
            }

            return new GeoPolygon3(points);
        }

        /// <summary>
        /// The crossings the 2D ellipse gives for edges lying in the plane, edge by edge in the order given, each edge's
        /// in its own order, a point already found not found again: Arc3.Flat.cs:34-44 laid out.
        /// </summary>
        private static GeoPoint3[] EdgeByEdge(Specimen e, IEnumerable<GeoLine3> edges)
        {
            GeoEllipse2 flat = e.Flat();
            var found = new List<GeoPoint3>();
            foreach (GeoLine3 edge in edges)
            {
                foreach (GeoPoint2 point in flat.GetIntersections(new GeoLine2(e.Foot(edge.StartPoint), e.Foot(edge.EndPoint)), Tol))
                {
                    GeoPoint3 lifted = e.Lift(point);
                    if (!found.Any(p => p.DistanceTo(lifted) <= Tol.EqualPoint))
                    {
                        found.Add(lifted);
                    }
                }
            }

            return found.ToArray();
        }

        #region Standing across the ellipse's plane

        [Fact]
        public void ATriangleStandingAcrossTheRimIsCrossedAtBothRimPointsItHolds()
        {
            // The triangle stands in the leaning plane on the chord's line: its base 20 below the chord and twice as long,
            // its apex 200 above, so at the chord's line it spans 1.82 chords and holds both ends.
            Specimen e = Tilted();
            Standing(e, out GeoPoint3 first, out GeoPoint3 second, out GeoVector3 chord, out GeoVector3 w);
            GeoPoint3 middle = first.Add(chord.Multiply(0.5));
            var triangle = new GeoTriangle3(
                first.Add(chord.Multiply(-0.5)).Add(w.Multiply(-20)), second.Add(chord.Multiply(0.5)).Add(w.Multiply(-20)), middle.Add(w.Multiply(200)));

            GeoPoint3[] crossings = e.Build().GetIntersections(triangle, Tol);

            SamePointsInOrder(Along(e.N.CrossProduct(triangle.GetPlane().Normal), first, second), crossings, Close);
            Assert.True(e.Build().CollidesWith(triangle, Tol));
            Assert.True(e.Build().TryIntersectWith(triangle, out GeoPoint3[] tried, Tol));
            SamePointsInOrder(crossings, tried, 0.0);
        }

        [Fact]
        public void ATriangleStandingOverOneRimPointOnlyIsCrossedThereOnly()
        {
            // The same triangle moved 0.6 chords along: at the chord's line it now spans -0.31 to 1.51 chords from the
            // old middle, holding the second end at +0.5 and not the first at -0.5.
            Specimen e = Tilted();
            Standing(e, out GeoPoint3 first, out GeoPoint3 second, out GeoVector3 chord, out GeoVector3 w);
            GeoVector3 shift = chord.Multiply(0.6);
            GeoPoint3 middle = first.Add(chord.Multiply(0.5));
            var triangle = new GeoTriangle3(
                first.Add(chord.Multiply(-0.5)).Add(w.Multiply(-20)).Add(shift),
                second.Add(chord.Multiply(0.5)).Add(w.Multiply(-20)).Add(shift),
                middle.Add(w.Multiply(200)).Add(shift));

            GeoPoint3 point = Assert.Single(e.Build().GetIntersections(triangle, Tol));

            Assert.True(point.DistanceTo(second) <= Close, $"{point} is {point.DistanceTo(second):E2} from {second}");
        }

        [Fact]
        public void ATriangleStandingThroughTheDiscShortOfTheRimTouchesNothing()
        {
            // A small triangle about the chord's middle, 0.2 chords wide: it goes through the disc but reaches no rim point.
            Specimen e = Tilted();
            Standing(e, out GeoPoint3 first, out _, out GeoVector3 chord, out GeoVector3 w);
            GeoPoint3 middle = first.Add(chord.Multiply(0.5));
            var triangle = new GeoTriangle3(
                middle.Add(chord.Multiply(-0.1)).Add(w.Multiply(-20)), middle.Add(chord.Multiply(0.1)).Add(w.Multiply(-20)), middle.Add(w.Multiply(30)));

            Assert.Empty(e.Build().GetIntersections(triangle, Tol));
            Assert.False(e.Build().CollidesWith(triangle, Tol));
            Assert.False(e.Build().TryIntersectWith(triangle, out _, Tol));
        }

        [Fact]
        public void APolygonStandingAcrossTheRimIsCrossedAtBothRimPointsItHolds()
        {
            // A quadrilateral 1.6 chords long and 60 high, centred on the chord.
            Specimen e = Tilted();
            Standing(e, out GeoPoint3 first, out GeoPoint3 second, out GeoVector3 chord, out GeoVector3 w);
            var polygon = new GeoPolygon3(
                first.Add(chord.Multiply(-0.3)).Add(w.Multiply(-30)), second.Add(chord.Multiply(0.3)).Add(w.Multiply(-30)),
                second.Add(chord.Multiply(0.3)).Add(w.Multiply(30)), first.Add(chord.Multiply(-0.3)).Add(w.Multiply(30)));

            GeoPoint3[] crossings = e.Build().GetIntersections(polygon, Tol);

            SamePointsInOrder(Along(e.N.CrossProduct(polygon.GetPlane().Normal), first, second), crossings, Close);
            Assert.True(e.Build().CollidesWith(polygon, Tol));
        }

        [Fact]
        public void AFaceStandingAcrossTheRimWithAHoleAboutOneRimPointIsCrossedAtTheOtherOnly()
        {
            // The quadrilateral as a face, with a square hole 20 across about the first rim point: the rim passes through
            // the hole there and meets no material (Arc3.Flat.cs:240-250).
            Specimen e = Tilted();
            Standing(e, out GeoPoint3 first, out GeoPoint3 second, out GeoVector3 chord, out GeoVector3 w);
            GeoVector3 along = chord.Normalize();
            var outline = new GeoPolygon3(
                first.Add(chord.Multiply(-0.3)).Add(w.Multiply(-30)), second.Add(chord.Multiply(0.3)).Add(w.Multiply(-30)),
                second.Add(chord.Multiply(0.3)).Add(w.Multiply(30)), first.Add(chord.Multiply(-0.3)).Add(w.Multiply(30)));
            GeoPolygon3 Square(GeoPoint3 about) => new GeoPolygon3(
                about.Add(along.Multiply(-10)).Add(w.Multiply(-10)), about.Add(along.Multiply(10)).Add(w.Multiply(-10)),
                about.Add(along.Multiply(10)).Add(w.Multiply(10)), about.Add(along.Multiply(-10)).Add(w.Multiply(10)));
            var face = new GeoFace3(outline, new[] { Square(first) });

            GeoPoint3 point = Assert.Single(e.Build().GetIntersections(face, Tol));

            Assert.True(point.DistanceTo(second) <= Close, $"{point} is {point.DistanceTo(second):E2} from {second}");
            Assert.True(e.Build().CollidesWith(face, Tol));

            // With a hole about each rim point, the rim goes through the face without touching it.
            var holed = new GeoFace3(outline, new[] { Square(first), Square(second) });

            Assert.Empty(e.Build().GetIntersections(holed, Tol));
            Assert.False(e.Build().CollidesWith(holed, Tol));
        }

        #endregion

        #region Lying in the ellipse's plane

        [Fact]
        public void ACoplanarTriangleAcrossTheRimIsCrossedWhereItsEdgesCutItEdgeByEdge()
        {
            // (-50, -50), (400, -50), (-50, 150) in the frame: the first edge cuts the rim at (259.8, -50), the last at
            // (-50, 98.6), and the long edge between them cuts it as the 2D ellipse says.
            Specimen e = Tilted();
            GeoPolygon3 corners = FramePolygon(e, 0, -50, -50, 400, -50, -50, 150);
            var triangle = new GeoTriangle3(corners.Vertices[0], corners.Vertices[1], corners.Vertices[2]);

            GeoPoint3[] crossings = e.Build().GetIntersections(triangle, Tol);

            SamePointsInOrder(EdgeByEdge(e, new[] { triangle.GetEdgeAt(0), triangle.GetEdgeAt(1), triangle.GetEdgeAt(2) }), crossings, Close);
            Assert.Contains(crossings, p => p.DistanceTo(e.At(AtFifty, -50, 0)) <= Close);
            Assert.Contains(crossings, p => p.DistanceTo(e.At(-50, 100 * Math.Sqrt(1 - 1.0 / 36), 0)) <= Close);
            Assert.True(e.Build().CollidesWith(triangle, Tol));
            Assert.True(e.Build().TryIntersectWith(triangle, out GeoPoint3[] tried, Tol));
            SamePointsInOrder(crossings, tried, 0.0);
        }

        [Fact]
        public void ACoplanarTriangleInsideTheDiscShortOfTheRimTouchesNothing()
        {
            // The rim is what counts: the triangle (-20, -20), (20, -20), (0, 20) lies on the disc, crosses no rim, and
            // does not hold the rim (Arc3.Flat.cs:114-123).
            Specimen e = Tilted();
            GeoPolygon3 corners = FramePolygon(e, 0, -20, -20, 20, -20, 0, 20);
            var triangle = new GeoTriangle3(corners.Vertices[0], corners.Vertices[1], corners.Vertices[2]);

            Assert.Empty(e.Build().GetIntersections(triangle, Tol));
            Assert.False(e.Build().CollidesWith(triangle, Tol));
        }

        [Fact]
        public void AnEllipseInsideACoplanarTriangleCrossesNothingButTouchesIt()
        {
            Specimen e = Tilted();
            GeoPolygon3 corners = FramePolygon(e, 0, -1000, -1000, 1000, -1000, 0, 1000);
            var triangle = new GeoTriangle3(corners.Vertices[0], corners.Vertices[1], corners.Vertices[2]);

            Assert.Empty(e.Build().GetIntersections(triangle, Tol));
            Assert.True(e.Build().CollidesWith(triangle, Tol));
            Assert.False(e.Build().TryIntersectWith(triangle, out _, Tol));
        }

        [Fact]
        public void ACoplanarRectangleNarrowerThanTheEllipseIsCrossedOnItsTwoLongSidesInEdgeOrder()
        {
            // x = ±100, y = ±200: the long sides cut the rim at (±100, ±94.28), the short ones miss it.
            Specimen e = Tilted();
            GeoPolygon3 polygon = FramePolygon(e, 0, -100, -200, 100, -200, 100, 200, -100, 200);

            GeoPoint3[] crossings = e.Build().GetIntersections(polygon, Tol);

            SamePointsInOrder(EdgeByEdge(e, polygon.GetEdges()), crossings, Close);
            SamePoints(new[] { e.At(100, -AtHundred, 0), e.At(100, AtHundred, 0), e.At(-100, AtHundred, 0), e.At(-100, -AtHundred, 0) }, crossings, Close);
            Assert.True(e.Build().CollidesWith(polygon, Tol));
        }

        [Fact]
        public void ACoplanarFaceHoldingTheEllipseIsCrossedWhereTheRimCutsTheRimOfAHole()
        {
            // The outline (±400, ±200) holds the ellipse; the hole from (200, -50) to (350, 50) is cut by the rim on its
            // two long sides, at (259.8, ±50).
            Specimen e = Tilted();
            var face = new GeoFace3(FramePolygon(e, 0, -400, -200, 400, -200, 400, 200, -400, 200),
                new[] { FramePolygon(e, 0, 200, -50, 350, -50, 350, 50, 200, 50) });

            GeoPoint3[] crossings = e.Build().GetIntersections(face, Tol);

            SamePoints(new[] { e.At(AtFifty, -50, 0), e.At(AtFifty, 50, 0) }, crossings, Close);
            SamePointsInOrder(EdgeByEdge(e, face.Boundary.GetEdges().Concat(face.Holes.SelectMany(h => h.GetEdges()))), crossings, Close);
            Assert.True(e.Build().CollidesWith(face, Tol));
        }

        [Fact]
        public void AnEllipseLyingInTheHoleOfACoplanarFaceTouchesNothing()
        {
            Specimen e = Tilted();
            var face = new GeoFace3(FramePolygon(e, 0, -1000, -1000, 1000, -1000, 1000, 1000, -1000, 1000),
                new[] { FramePolygon(e, 0, -400, -200, 400, -200, 400, 200, -400, 200) });

            Assert.Empty(e.Build().GetIntersections(face, Tol));
            Assert.False(e.Build().CollidesWith(face, Tol));
        }

        [Fact]
        public void AnEllipseOnACoplanarFaceWithASmallHoleInsideTheRimTouchesIt()
        {
            // The rim lies on the material all round; the hole (±20, ±20) is inside the disc and is cut by nothing.
            Specimen e = Tilted();
            var face = new GeoFace3(FramePolygon(e, 0, -400, -200, 400, -200, 400, 200, -400, 200),
                new[] { FramePolygon(e, 0, -20, -20, 20, -20, 20, 20, -20, 20) });

            Assert.Empty(e.Build().GetIntersections(face, Tol));
            Assert.True(e.Build().CollidesWith(face, Tol));
        }

        [Fact]
        public void ATriangleAHairAboveTheEllipsesPlaneIsReadAsLyingInIt()
        {
            // The triangle of the edge-by-edge case lifted 0.0001, a tenth of the planar tolerance: it is cut as if in
            // the plane, every crossing within 0.0002 of the one in the plane.
            Specimen e = Tilted();
            GeoPolygon3 corners = FramePolygon(e, 0.0001, -50, -50, 400, -50, -50, 150);
            var triangle = new GeoTriangle3(corners.Vertices[0], corners.Vertices[1], corners.Vertices[2]);
            GeoPolygon3 flat = FramePolygon(e, 0, -50, -50, 400, -50, -50, 150);
            var inPlane = new GeoTriangle3(flat.Vertices[0], flat.Vertices[1], flat.Vertices[2]);

            SamePointsInOrder(EdgeByEdge(e, new[] { inPlane.GetEdgeAt(0), inPlane.GetEdgeAt(1), inPlane.GetEdgeAt(2) }),
                e.Build().GetIntersections(triangle, Tol), 0.0002);
        }

        #endregion

        [Fact]
        public void ANullPolygonOrFaceIsRefused()
        {
            // As the circle's are (Arc3.Flat.cs:168-171, 252-255).
            GeoEllipse3 ellipse = Tilted().Build();

            Assert.Throws<ArgumentNullException>(() => ellipse.GetIntersections((GeoPolygon3)null, Tol));
            Assert.Throws<ArgumentNullException>(() => ellipse.CollidesWith((GeoPolygon3)null, Tol));
            Assert.Throws<ArgumentNullException>(() => ellipse.TryIntersectWith((GeoPolygon3)null, out _, Tol));
            Assert.Throws<ArgumentNullException>(() => ellipse.GetIntersections((GeoFace3)null, Tol));
            Assert.Throws<ArgumentNullException>(() => ellipse.CollidesWith((GeoFace3)null, Tol));
            Assert.Throws<ArgumentNullException>(() => ellipse.TryIntersectWith((GeoFace3)null, out _, Tol));
        }
    }
}
