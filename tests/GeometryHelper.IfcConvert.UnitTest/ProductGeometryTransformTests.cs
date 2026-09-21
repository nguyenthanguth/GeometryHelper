using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.IfcConvert.Models;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    /// <summary>
    /// <see cref="IfcProductGeometry.TransformBy"/>: moving converted geometry without losing the thin faces the
    /// conversion keeps.
    /// </summary>
    public class ProductGeometryTransformTests
    {
        private const string Guid = "2O2Fr$t4X7Zf8NOew3FNr2";

        // Turned 30 degrees about Z and moved, like a Tekla reference model inserted away from the origin.
        private static readonly GeoTransform3 Move =
            GeoTransform3.Translation(new GeoVector3(12000, 34000, 5000)) * GeoTransform3.RotationZ(Math.PI / 6.0);

        [Fact]
        public void TransformBy_ABodyWithASliverFace_KeepsTheWholeBody()
        {
            GeoSolid3 box = SliverBox();
            IfcProductGeometry product = new IfcProductGeometry(Guid, "GIRDER", "IfcBeam", new[] { box });

            // GeoSolid3.TransformBy checks every face against Tolerance.Global again, and the sliver fails it: the
            // whole body is lost. 3 of 500 beams of a real Tekla model with openings cut carried such a face.
            Assert.Throws<ArgumentException>(() => box.TransformBy(Move));

            IfcProductGeometry moved = product.TransformBy(Move);

            GeoSolid3 body = Assert.Single(moved.Solids);
            Assert.Equal(box.Faces.Count, body.Faces.Count);
            Assert.Equal(1e6, body.GetSignedVolume(), 3);
            Assert.Empty(moved.Warnings);
        }

        [Fact]
        public void TransformBy_CarriesBodiesSurfacesAndPlacement_AndLeavesTheOriginalAlone()
        {
            GeoSolid3 tetrahedron = Tetrahedron(100.0);
            GeoFace3 surface = new GeoFace3(new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(0, 10, 0)));
            GeoTransform3 placement = GeoTransform3.Translation(new GeoVector3(5, 6, 7));
            IfcProductGeometry original = new IfcProductGeometry(
                Guid, "Wall", "IfcWall", new[] { tetrahedron }, new[] { surface }, placement, "T1", new[] { "#12 IfcExtrudedAreaSolid: note" });

            // Turned 90 degrees about Z, then moved.
            GeoTransform3 move = GeoTransform3.Translation(new GeoVector3(1000, -2000, 300)) * GeoTransform3.RotationZ(Math.PI / 2.0);

            IfcProductGeometry moved = original.TransformBy(move);

            Assert.Equal(original.GlobalId, moved.GlobalId);
            Assert.Equal(original.Name, moved.Name);
            Assert.Equal(original.IfcType, moved.IfcType);
            Assert.Equal(original.Tag, moved.Tag);
            Assert.Equal(original.Warnings, moved.Warnings);
            Assert.Single(moved.Solids);
            Assert.Single(moved.OpenSurfaces);

            // (100, 0, 0) turns to (0, 100, 0) and moves to (1000, -1900, 300); so does the surface's (10, 0, 0).
            Assert.Contains(Vertices(moved.Solids[0]), v => v.IsEqualTo(new GeoPoint3(1000, -1900, 300)));
            Assert.Contains(moved.OpenSurfaces[0].Boundary.Vertices, v => v.IsEqualTo(new GeoPoint3(1000, -1990, 300)));
            Assert.Equal(original.Solids[0].GetSignedVolume(), moved.Solids[0].GetSignedVolume(), 6);
            Assert.True(moved.Placement.IsEqualTo(move * placement));

            // The geometry it came from, which may sit in a cache, is untouched.
            Assert.Same(tetrahedron, original.Solids[0]);
            Assert.Contains(Vertices(original.Solids[0]), v => v.IsEqualTo(new GeoPoint3(100, 0, 0)));
            Assert.True(original.Placement.IsEqualTo(placement));
        }

        [Fact]
        public void TransformBy_AMirror_KeepsTheFacesPointingOut()
        {
            IfcProductGeometry original = new IfcProductGeometry(Guid, "Plate", "IfcPlate", new[] { Tetrahedron(100.0) });

            IfcProductGeometry mirrored = original.TransformBy(GeoTransform3.Mirror(GeoPlane3.YZ));

            GeoSolid3 body = Assert.Single(mirrored.Solids);
            Assert.Contains(Vertices(body), v => v.IsEqualTo(new GeoPoint3(-100, 0, 0)));
            Assert.Equal(original.Solids[0].GetSignedVolume(), body.GetSignedVolume(), 6);
        }

        [Fact]
        public void TransformBy_WhatTheTransformationCollapses_IsLeftOutWithAWarning()
        {
            GeoFace3 surface = new GeoFace3(new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(0, 10, 0)));
            IfcProductGeometry original = new IfcProductGeometry(
                Guid, "Plate", "IfcPlate", new[] { Tetrahedron(100.0) }, new[] { surface }, null, null, new[] { "#12 IfcExtrudedAreaSolid: note" });

            // Shrunk a million times, every vertex falls within the point tolerance of the others.
            IfcProductGeometry moved = original.TransformBy(GeoTransform3.Scaling(1e-6));

            Assert.Empty(moved.Solids);
            Assert.Empty(moved.OpenSurfaces);
            Assert.Equal(3, moved.Warnings.Count);
            Assert.Equal(original.Warnings[0], moved.Warnings[0]);
            Assert.Contains("Solid 1 of 1 collapsed", moved.Warnings[1]);
            Assert.Contains("1 open surface(s)", moved.Warnings[2]);
        }

        [Fact]
        public void TransformBy_UsesTheToleranceTheGeometryWasConvertedWith()
        {
            IfcProductGeometry product = new IfcProductGeometry(Guid, "GIRDER", "IfcBeam", new[] { SliverBox() });

            // Converted with a point tolerance of 0.01 mm, the smallest face area is 1e-4 mm2, which the sliver is not.
            IfcProductGeometry moved = product.TransformBy(Move, new Tolerance(1e-2, 1e-2));

            GeoSolid3 body = Assert.Single(moved.Solids);
            Assert.Equal(6, body.Faces.Count);
            Assert.Equal("1 face(s) degenerated under the transformation and were left out.", Assert.Single(moved.Warnings));
        }

        [Fact]
        public void TransformBy_WithoutATransformation_Throws()
        {
            IfcProductGeometry product = new IfcProductGeometry(Guid, "Plate", "IfcPlate", new[] { Tetrahedron(100.0) });

            Assert.Throws<ArgumentNullException>(() => product.TransformBy(null));
        }

        /// <summary>
        /// A 100 mm cube whose top is split in two: a sliver triangle 100 mm long and 1e-6 mm wide (5e-5 mm2), and
        /// the rest. Meshes read from IFC carry faces like it, and the conversion keeps them: it builds polygons with
        /// an area threshold of EqualPoint squared (1e-8 mm2) rather than EqualVector (1e-4).
        /// </summary>
        internal static GeoSolid3 SliverBox()
        {
            const double width = 1e-6;
            Tolerance construction = new Tolerance(Tolerance.DefaultEqualPoint, Tolerance.DefaultEqualPoint * Tolerance.DefaultEqualPoint);

            GeoPoint3 b0 = new GeoPoint3(0, 0, 0), b1 = new GeoPoint3(100, 0, 0), b2 = new GeoPoint3(100, 100, 0), b3 = new GeoPoint3(0, 100, 0);
            GeoPoint3 t0 = new GeoPoint3(0, 0, 100), t1 = new GeoPoint3(100, 0, 100), t2 = new GeoPoint3(100, 100, 100), t3 = new GeoPoint3(0, 100, 100);
            GeoPoint3 m = new GeoPoint3(50, width, 100);

            // Wound so that every normal points out of the cube.
            return new GeoSolid3(new[]
            {
                new GeoFace3(new GeoPolygon3(b0, b3, b2, b1)),
                new GeoFace3(new GeoPolygon3(b0, b1, t1, t0)),
                new GeoFace3(new GeoPolygon3(b1, b2, t2, t1)),
                new GeoFace3(new GeoPolygon3(b2, b3, t3, t2)),
                new GeoFace3(new GeoPolygon3(b3, b0, t0, t3)),
                new GeoFace3(new GeoPolygon3(new[] { t0, t1, m }, construction)),
                new GeoFace3(new GeoPolygon3(t0, m, t1, t2, t3)),
            });
        }

        private static GeoSolid3 Tetrahedron(double size)
        {
            GeoPoint3 a = new GeoPoint3(0, 0, 0);
            GeoPoint3 b = new GeoPoint3(size, 0, 0);
            GeoPoint3 c = new GeoPoint3(0, size, 0);
            GeoPoint3 d = new GeoPoint3(0, 0, size);

            return new GeoSolid3(new[]
            {
                new GeoFace3(new GeoPolygon3(a, c, b)),
                new GeoFace3(new GeoPolygon3(a, b, d)),
                new GeoFace3(new GeoPolygon3(a, d, c)),
                new GeoFace3(new GeoPolygon3(b, c, d)),
            });
        }

        private static GeoPoint3[] Vertices(GeoSolid3 solid)
        {
            return solid.Faces.SelectMany(f => f.Boundary.Vertices).ToArray();
        }
    }
}
