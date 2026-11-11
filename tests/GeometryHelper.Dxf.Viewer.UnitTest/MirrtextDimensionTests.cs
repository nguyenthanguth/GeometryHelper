using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// MIRROR of dimensions, as AutoCAD 2021 runs it under MIRRTEXT 0 and 1 alike (probed in its console): the points
    /// mirrored, the plane kept the drawing's, the words reading as before.
    /// </summary>
    public class MirrtextDimensionTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ALinearDimensionWithItsTextOverridden_MirroredAcrossAnUpright_KeepsTheDrawingsPlane(bool mirrtext)
        {
            CadDocument document = Blank();
            document.Database.Mirrtext = mirrtext;
            CadCommandSession session = Session(document);
            Run(session, "DIMLINEAR", P(0, -20), P(30, -20), "T", "ABC", P(15, -30));
            string source = All<DxfRotatedDimension>(document.ModelSpace).Single().Handle;

            Run(session, "MIRROR", new[] { source }, Enter, P(100, -100), P(100, 100), Enter);

            // AutoCAD 2021: origins (200, -20) and (170, -20), the line through (170, -30), at 180 degrees, normal (0, 0, 1).
            DxfRotatedDimension copy = All<DxfRotatedDimension>(document.ModelSpace).Single(d => d.Handle != source);
            Near(new GeoPoint3(200, -20, 0), copy.XLine1Point, 1E-9);
            Near(new GeoPoint3(170, -20, 0), copy.XLine2Point, 1E-9);
            Near(new GeoPoint3(170, -30, 0), copy.DimLinePoint, 1E-9);
            GeoVector3 normal = copy.Normal;
            Near(new GeoPoint3(0, 0, 1), new GeoPoint3(normal.X, normal.Y, normal.Z), 1E-12);
            Assert.Equal(Math.PI, copy.Rotation, 9);
            Assert.Equal("ABC", copy.DimensionText);
        }
    }
}
