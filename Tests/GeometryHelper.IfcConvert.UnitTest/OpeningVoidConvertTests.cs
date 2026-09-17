using System;
using System.IO;
using System.Linq;
using GeometryHelper.IfcConvert.Converters;
using GeometryHelper.IfcConvert.Core;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    [Collection("IfcEngine")]
    public class OpeningVoidConvertTests
    {
        private const string IfcWallWithOpeningStep = @"ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('ViewDefinition [CoordinationView]'),'2;1');
FILE_NAME('test_wall_opening.ifc','2026-09-17T23:00:00',('Tester'),('TestOrg'),'xBIM','xBIM','');
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
#15=IFCWALL('0000000000000000000002',$,'WallWithOpening',$,$,#14,#13,$,$);
#16=IFCRECTANGLEPROFILEDEF(.AREA.,$,#5,0.4,0.4);
#17=IFCEXTRUDEDAREASOLID(#16,#8,#9,1.);
#18=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#17));
#19=IFCPRODUCTDEFINITIONSHAPE($,$,(#18));
#20=IFCOPENINGELEMENT('0000000000000000000003',$,'OpeningHole',$,$,#14,#19,$,$);
#21=IFCRELVOIDSELEMENT('0000000000000000000004',$,$,$,#15,#20);
ENDSEC;
END-ISO-10303-21;
";

        [Fact]
        public void ApplyVoids_False_RetainsOriginalSolidVolume()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"test_void_false_{Guid.NewGuid():N}.ifc");
            File.WriteAllText(tempFile, IfcWallWithOpeningStep);

            try
            {
                using (var store = IfcStore.Open(tempFile))
                using (var cache = new IfcStoreCache(store))
                {
                    var wall = cache.GetProducts<IIfcWall>().FirstOrDefault();
                    Assert.NotNull(wall);

                    var options = new IfcConvertOptions { ApplyVoids = false };
                    var geom = wall.ToProductGeometry(options);

                    Assert.NotNull(geom);
                    Assert.NotEmpty(geom.Solids);

                    // Uncut wall volume: 1.0 x 0.5 x 2.0 = 1.0 m3
                    Assert.Equal(1.0, geom.Solids[0].Volume, 2);
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

        [Fact]
        public void ApplyVoids_True_SubtractsOpeningFromWallSolid()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"test_void_true_{Guid.NewGuid():N}.ifc");
            File.WriteAllText(tempFile, IfcWallWithOpeningStep);

            try
            {
                using (var store = IfcStore.Open(tempFile))
                using (var cache = new IfcStoreCache(store))
                {
                    var wall = cache.GetProducts<IIfcWall>().FirstOrDefault();
                    Assert.NotNull(wall);

                    var options = new IfcConvertOptions { ApplyVoids = true };
                    var geom = wall.ToProductGeometry(options);

                    Assert.NotNull(geom);
                    Assert.NotEmpty(geom.Solids);

                    // Cut wall volume: 1.0 m3 - (0.4 x 0.4 x 1.0 m3) = 0.84 m3
                    double netVolume = geom.Solids.Sum(s => s.Volume);
                    Assert.True(netVolume < 1.0, "Volume must be smaller after opening is subtracted");
                    Assert.Equal(0.84, netVolume, 1);
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
