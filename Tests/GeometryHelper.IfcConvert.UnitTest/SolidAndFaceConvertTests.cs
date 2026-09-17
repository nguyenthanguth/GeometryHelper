using System;
using System.IO;
using System.Linq;
using GeometryHelper.CommonGeometry;
using GeometryHelper.IfcConvert.Converters;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Models;
using GeometryHelper.SolidGeometry.Geometry;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    [Collection("IfcEngine")]
    public class SolidAndFaceConvertTests
    {
        private const string MinimalIfcBoxStep = @"ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('ViewDefinition [CoordinationView]'),'2;1');
FILE_NAME('test_box.ifc','2026-09-17T23:00:00',('Tester'),('TestOrg'),'xBIM','xBIM','');
FILE_SCHEMA(('IFC4'));
ENDSEC;
DATA;
#1=IFCPROJECT('0000000000000000000001',$,'TestProject',$,$,$,$,$,#2);
#2=IFCUNITASSIGNMENT((#3));
#3=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);
#4=IFCCARTESIANPOINT((0.,0.));
#5=IFCAXIS2PLACEMENT2D(#4,$);
#6=IFCRECTANGLEPROFILEDEF(.AREA.,$,#5,1.,0.5);
#7=IFCCARTESIANPOINT((0.,0.,0.));
#8=IFCAXIS2PLACEMENT3D(#7,$,$);
#9=IFCDIRECTION((0.,0.,1.));
#10=IFCEXTRUDEDAREASOLID(#6,#8,#9,2.);
#11=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,0.0001,#8,$);
#12=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#10));
#13=IFCPRODUCTDEFINITIONSHAPE($,$,(#12));
#14=IFCLOCALPLACEMENT($,#8);
#15=IFCWALL('0000000000000000000002',$,'TestWall',$,$,#14,#13,$,$);
ENDSEC;
END-ISO-10303-21;
";

        [Fact]
        public void NullProduct_ReturnsEmptyProductGeometry()
        {
            IIfcProduct product = null;
            IfcProductGeometry geom = product.ToProductGeometry();

            Assert.NotNull(geom);
            Assert.Null(geom.Product);
            Assert.Empty(geom.Solids);
            Assert.Empty(geom.OpenSurfaces);
        }

        [Fact]
        public void IfcConvertOptions_HasExpectedDefaults()
        {
            var options = new IfcConvertOptions();

            Assert.Equal(Tolerance.Global, options.Tolerance);
            Assert.Equal(1.0, options.ScaleFactor);
            Assert.False(options.ApplyVoids);
            Assert.True(options.TessellateNonPlanarFaces);
            Assert.Equal(0.5, options.DeflectionTolerance);
            Assert.NotNull(options.SkipNames);
            Assert.Empty(options.SkipNames);
        }

        [Fact]
        public void SyntheticIfcWall_ConvertsToValidGeoSolid3()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"test_box_{Guid.NewGuid():N}.ifc");
            File.WriteAllText(tempFile, MinimalIfcBoxStep);

            try
            {
                using (var store = IfcStore.Open(tempFile))
                using (var cache = new IfcStoreCache(store))
                {
                    var wall = cache.GetProducts<IIfcWall>().FirstOrDefault();
                    Assert.NotNull(wall);
                    Assert.Equal("TestWall", wall.Name?.ToString());

                    var geom = wall.ToProductGeometry();
                    Assert.NotNull(geom);
                    Assert.NotEmpty(geom.Solids);

                    GeoSolid3 solid = geom.Solids[0];
                    Assert.NotNull(solid);

                    // Box dimensions: 1.0 (X) x 0.5 (Y) x 2.0 (Z extrusion) = 1.0 m3
                    Assert.True(solid.Volume > 0, "Solid volume must be strictly positive");
                    Assert.Equal(1.0, solid.Volume, 2);

                    // A standard rectangular box has 6 planar boundary faces
                    Assert.Equal(6, solid.Faces.Count);

                    // Verify centroid is near (0, 0, 1.0) because profile is centered at (0,0) and extruded by 2.0 along +Z
                    Assert.Equal(0.0, solid.Centroid.X, 1);
                    Assert.Equal(0.0, solid.Centroid.Y, 1);
                    Assert.Equal(1.0, solid.Centroid.Z, 1);
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }
    }
}
