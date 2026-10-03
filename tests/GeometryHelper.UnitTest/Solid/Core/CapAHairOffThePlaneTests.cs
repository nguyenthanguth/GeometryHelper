using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// A plane cutting a body through a corner of it and within the planar tolerance of the next: the cap closing each half
    /// lies in the plane, a corner of it a hair off, and was refused, measured about the normal of its own corners.
    /// </summary>
    public class CapAHairOffThePlaneTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        /// <summary>
        /// A piece of a column lying on its side with a ledge 0.109 thick standing up from it, turned and moved: a bar of its
        /// cells before it was cut across.
        /// </summary>
        internal static GeoSolid3 Piece()
        {
            GeoPoint3[][] faces =
            {
                new[] { P(-2001.8935579515767, 2903.0219151461943, 694.06183617539659), P(-2001.8935579515767, 2903.0219151461943, 895.02170449356629), P(-2254.0526187559462, 2601.2569237827174, 895.02170449356629), P(-2254.0526187559462, 2601.2569237827174, 501.77076430608088), P(-2015.6009576692002, 2886.6179305175656, 501.77076430608088) },
                new[] { P(-2015.6859547423273, 2886.6857215961304, 501.77076430608088), P(-2254.1360247221492, 2601.3266189783335, 501.77076430608088), P(-2254.1360247221492, 2601.3266189783335, 895.02170449356629), P(-2001.9769639177794, 2903.0916103418103, 895.02170449356629), P(-2001.9769639177794, 2903.0916103418103, 694.08415664917493) },
                new[] { P(-2015.6009576692002, 2886.6179305175656, 501.77076430608088), P(-2254.0526187559462, 2601.2569237827174, 501.77076430608088), P(-2254.1360247221492, 2601.3266189783335, 501.77076430608088), P(-2015.6859547423271, 2886.6857215961304, 501.77076430608088) },
                new[] { P(-2348.1019716018313, 3207.7161763828135, 895.02170449356629), P(-2355.6782936521927, 3198.6494042002032, 895.02170449356629), P(-2001.9769639177794, 2903.0916103418103, 895.02170449356629), P(-2254.1360247221492, 2601.3266189783335, 895.02170449356629), P(-2254.0526187559462, 2601.2569237827174, 895.02170449356629), P(-2001.8935579515767, 2903.0219151461943, 895.02170449356629), P(-1305.794540100117, 2321.3518089646445, 895.02170449356618), P(-1279.2361262877032, 2355.2213100420172, 895.02170449356618) },
                new[] { P(-2254.0526187559462, 2601.2569237827174, 501.77076430608088), P(-2254.0526187559462, 2601.2569237827174, 895.02170449356629), P(-2254.1360247221492, 2601.3266189783335, 895.02170449356629), P(-2254.1360247221492, 2601.3266189783335, 501.77076430608088) },
                new[] { P(-2355.6782936521927, 3198.6494042002032, 788.73903105419322), P(-2355.6782936521927, 3198.6494042002032, 895.02170449356629), P(-2348.1019716018313, 3207.7161763828135, 895.02170449356629) },
                new[] { P(-1283.3504481143782, 2302.5972107915454, 501.77076430608082), P(-1305.794540100117, 2321.3518089646445, 895.02170449356618), P(-2001.8935579515767, 2903.0219151461943, 895.02170449356629), P(-2001.8935579515767, 2903.0219151461943, 694.06183617539659) },
                new[] { P(-2001.9769639177794, 2903.0916103418103, 694.08415664917493), P(-2001.9769639177794, 2903.0916103418103, 895.02170449356629), P(-2355.6782936521927, 3198.6494042002032, 895.02170449356629), P(-2355.6782936521927, 3198.6494042002032, 788.73903105419322) },
                new[] { P(-1279.2361262877032, 2355.2213100420172, 895.02170449356618), P(-1305.794540100117, 2321.3518089646445, 895.02170449356618), P(-1283.3504481143784, 2302.5972107915454, 501.77076430608082) },
                new[] { P(-2001.8935579515767, 2903.0219151461943, 694.06183617539659), P(-2015.6009576692002, 2886.6179305175656, 501.77076430608088), P(-2015.6859547423273, 2886.6857215961304, 501.77076430608088), P(-2001.9769639177794, 2903.0916103418103, 694.08415664917493), P(-2355.6782936521927, 3198.6494042002032, 788.73903105419322), P(-2348.1019716018313, 3207.7161763828135, 895.02170449356629), P(-1279.2361262877032, 2355.2213100420172, 895.02170449356618), P(-1283.3504481143782, 2302.5972107915454, 501.77076430608082) },
            };

            return new GeoSolid3(faces.Select(f => new GeoFace3(new GeoPolygon3(f, Tolerance))));
        }

        /// <summary>The plane the piece is cut by.</summary>
        internal static GeoPlane3 Plane() => new GeoPlane3(P(-606.50538686328423, 973.7326319746096, 1095.5852041721971), new GeoVector3(-0.010369337942062018, -0.13262867904180142, -0.99111155291736464));

        [Fact]
        public void ACapWithACornerAHairOffThePlaneClosesEachHalf()
        {
            // The plane passes through a corner of the ledge's tip and 0.0084 from the corner beside it, 0.109 away. The
            // section has a finger as wide as the ledge, and that corner at its tip turned the normal of the cap's corners
            // 8E-5 from the plane's, which over the cap's length put its far corners 0.058 off it.
            GeoSolid3 piece = Piece();
            GeoPlane3 plane = Plane();

            Assert.True(piece.IsClosed(Tolerance));
            Assert.True(piece.TrySplitBy(plane, out GeoSolid3 above, out GeoSolid3 below, Tolerance));
            Assert.True(above.IsClosed(Tolerance));
            Assert.True(below.IsClosed(Tolerance));

            // The halves hold the piece's material, measured by triangles lying in their faces. Measured as Volume measures
            // it, by the fan of each face's corners, each face is read flat through its first corner, and the large faces
            // the cut leaves, a corner of each a hair off their middle planes, hold 560 mm3 more between them, a
            // ten-thousandth.
            Assert.InRange((Material(above) + Material(below)) / Material(piece), 1 - 1E-7, 1 + 1E-7);
            Assert.InRange((above.GetVolume() + below.GetVolume()) / piece.GetVolume(), 1 - 2E-4, 1 + 2E-4);

            // Each cap lies in the plane, facing out of its half along the plane's normal.
            GeoFace3 belowCap = Assert.Single(below.Faces, f => f.Boundary.Vertices.All(v => Math.Abs(plane.SignedDistanceTo(v)) <= Tolerance.EqualPlanar));
            GeoFace3 aboveCap = Assert.Single(above.Faces, f => f.Boundary.Vertices.All(v => Math.Abs(plane.SignedDistanceTo(v)) <= Tolerance.EqualPlanar));
            Assert.True(belowCap.Normal.DotProduct(plane.Normal) > 1 - 1E-9);
            Assert.True(aboveCap.Normal.DotProduct(plane.Normal) < -1 + 1E-9);
            Assert.Equal(belowCap.Area, aboveCap.Area, 6);

            // And the section is that cap, where it was nothing.
            Assert.Equal(belowCap.Area, Assert.Single(piece.Section(plane, Tolerance)).Area, 6);
        }

        /// <summary>The volume a body's faces enclose, measured by the triangles lying in them.</summary>
        private static double Material(GeoSolid3 body)
        {
            GeoPoint3 apex = body.Faces[0].Boundary[0];

            return body.Faces
                .SelectMany(f => f.TriangulateSurface(Tolerance))
                .Sum(t => apex.GetVectorTo(t.A).DotProduct(apex.GetVectorTo(t.B).CrossProduct(apex.GetVectorTo(t.C))) / 6.0);
        }
    }
}
