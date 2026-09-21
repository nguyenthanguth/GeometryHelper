using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.IfcConvert.Converters.Internal;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    [Collection("IfcEngine")]
    public class FaceOrientationTests
    {
        private static GeoFace3 Quad(params (double X, double Y, double Z)[] p) =>
            new GeoFace3(new GeoPolygon3(p.Select(v => new GeoPoint3(v.X, v.Y, v.Z)).ToList(), Tolerance.Global));

        // Unit cube, every face wound counter-clockwise seen from outside.
        private static List<GeoFace3> OutwardCube() => new List<GeoFace3>
        {
            Quad((0, 0, 0), (0, 1, 0), (1, 1, 0), (1, 0, 0)), // bottom, -Z
            Quad((0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)), // top, +Z
            Quad((0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)), // front, -Y
            Quad((0, 1, 0), (0, 1, 1), (1, 1, 1), (1, 1, 0)), // back, +Y
            Quad((0, 0, 0), (0, 0, 1), (0, 1, 1), (0, 1, 0)), // left, -X
            Quad((1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)), // right, +X
        };

        [Fact]
        public void MixedOrientations_AreMadeConsistentAndOutward()
        {
            List<GeoFace3> faces = OutwardCube();
            faces[1] = faces[1].Flip();
            faces[4] = faces[4].Flip();

            var solid = new GeoSolid3(FaceOrientation.Orient(faces, Tolerance.Global));

            Assert.Equal(1.0, solid.GetSignedVolume(), 9);
            Assert.All(solid.Faces, f =>
            {
                GeoPoint3 centre = new GeoPoint3(f.Boundary.Vertices.Average(v => v.X), f.Boundary.Vertices.Average(v => v.Y), f.Boundary.Vertices.Average(v => v.Z));
                GeoVector3 outward = new GeoVector3(centre.X - 0.5, centre.Y - 0.5, centre.Z - 0.5);
                Assert.True(f.Normal.DotProduct(outward) > 0.0, $"face at {centre} points inwards");
            });
        }

        [Fact]
        public void FullyInvertedBody_IsTurnedOutward()
        {
            var faces = OutwardCube().Select(f => f.Flip()).ToList();
            var solid = new GeoSolid3(FaceOrientation.Orient(faces, Tolerance.Global));
            Assert.Equal(1.0, solid.GetSignedVolume(), 9);
        }

        // 1 x 1 x 0.1 plate with a 0.4 x 0.4 square hole: purely planar, read face by face.
        private static readonly string PlateWithSquareHole = IfcTestFile.CommonHeader() + @"
#30=IFCCARTESIANPOINT((-0.5,-0.5));
#31=IFCCARTESIANPOINT((0.5,-0.5));
#32=IFCCARTESIANPOINT((0.5,0.5));
#33=IFCCARTESIANPOINT((-0.5,0.5));
#34=IFCPOLYLINE((#30,#31,#32,#33,#30));
#35=IFCCARTESIANPOINT((-0.2,-0.2));
#36=IFCCARTESIANPOINT((0.2,-0.2));
#37=IFCCARTESIANPOINT((0.2,0.2));
#38=IFCCARTESIANPOINT((-0.2,0.2));
#39=IFCPOLYLINE((#35,#36,#37,#38,#35));
#20=IFCARBITRARYPROFILEDEFWITHVOIDS(.AREA.,$,#34,(#39));
#21=IFCEXTRUDEDAREASOLID(#20,#8,#9,0.1);
#22=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#21));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#22));
#24=IFCLOCALPLACEMENT($,#8);
#25=IFCPLATE('0000000000000000000002',$,'Plate',$,$,#24,#23,$,$);
";

        [Fact]
        public void PlanarFaceWithHole_VolumeExcludesHole()
        {
            IfcTestFile.Run(PlateWithSquareHole, model =>
            {
                var solid = model.GetSolid("0000000000000000000002");
                Assert.NotNull(solid);
                Assert.True(solid.IsClosed());
                Assert.Equal((1.0 - 0.16) * 0.1, solid.Volume, 9);
            });
        }
    }
}
