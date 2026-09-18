using System.Linq;
using GeometryHelper.IfcConvert.Core;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    [Collection("IfcEngine")]
    public class ConversionWarningTests
    {
        private const string Guid = "0000000000000000000002";

        private static string Wall(string body) => IfcTestFile.CommonHeader() + @"
#20=IFCRECTANGLEPROFILEDEF(.AREA.,$,#5,1.,0.5);
" + body + @"
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#22));
#24=IFCLOCALPLACEMENT($,#8);
#25=IFCWALL('" + Guid + @"',$,'Wall',$,$,#24,#23,$,$);
";

        private static readonly string HealthyWall = Wall(@"
#21=IFCEXTRUDEDAREASOLID(#20,#8,#9,2.);
#22=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#21));
");

        // Zero extrusion depth: the item cannot produce a solid.
        private static readonly string DegenerateWall = Wall(@"
#21=IFCEXTRUDEDAREASOLID(#20,#8,#9,0.);
#22=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#21));
");

        // Unit cube as a faceted B-rep with its top face missing. The engine marks the shape invalid, and reading
        // an invalid shape's faces faults inside the native engine, so it must be skipped and reported.
        private static readonly string OpenBrepWall = Wall(@"
#30=IFCCARTESIANPOINT((0.,0.,0.));
#31=IFCCARTESIANPOINT((1.,0.,0.));
#32=IFCCARTESIANPOINT((1.,1.,0.));
#33=IFCCARTESIANPOINT((0.,1.,0.));
#34=IFCCARTESIANPOINT((0.,0.,1.));
#35=IFCCARTESIANPOINT((1.,0.,1.));
#36=IFCCARTESIANPOINT((1.,1.,1.));
#37=IFCCARTESIANPOINT((0.,1.,1.));
#40=IFCFACE((IFCFACEOUTERBOUND(IFCPOLYLOOP((#30,#33,#32,#31)),.T.)));
#41=IFCFACE((IFCFACEOUTERBOUND(IFCPOLYLOOP((#30,#31,#35,#34)),.T.)));
#42=IFCFACE((IFCFACEOUTERBOUND(IFCPOLYLOOP((#31,#32,#36,#35)),.T.)));
#43=IFCFACE((IFCFACEOUTERBOUND(IFCPOLYLOOP((#32,#33,#37,#36)),.T.)));
#44=IFCFACE((IFCFACEOUTERBOUND(IFCPOLYLOOP((#33,#30,#34,#37)),.T.)));
#45=IFCCLOSEDSHELL((#40,#41,#42,#43,#44));
#21=IFCFACETEDBREP(#45);
#22=IFCSHAPEREPRESENTATION(#11,'Body','Brep',(#21));
");

        // The opening body is a mapped representation, which the engine cutter does not take:
        // conversion must fall back to the GeoSolid3 boolean and say so.
        private static readonly string WallWithMappedOpening = Wall(@"
#21=IFCEXTRUDEDAREASOLID(#20,#8,#9,2.);
#22=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#21));
#50=IFCRECTANGLEPROFILEDEF(.AREA.,$,#5,0.4,1.);
#51=IFCEXTRUDEDAREASOLID(#50,#8,#9,1.);
#52=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#51));
#53=IFCREPRESENTATIONMAP(#8,#52);
#54=IFCCARTESIANTRANSFORMATIONOPERATOR3D($,$,#7,$,$);
#55=IFCMAPPEDITEM(#53,#54);
#56=IFCSHAPEREPRESENTATION(#11,'Body','MappedRepresentation',(#55));
#57=IFCPRODUCTDEFINITIONSHAPE($,$,(#56));
#58=IFCOPENINGELEMENT('0000000000000000000003',$,'Opening',$,$,#24,#57,$,$);
#59=IFCRELVOIDSELEMENT('0000000000000000000004',$,$,$,#25,#58);
");

        [Fact]
        public void HealthyProduct_HasNoWarnings()
        {
            IfcTestFile.Run(HealthyWall, model =>
            {
                var geom = model.GetGeometry(Guid);
                Assert.True(geom.HasSolids);
                Assert.Empty(geom.Warnings);
            });
        }

        [Fact]
        public void ItemWithoutSolid_IsReportedWithEntityLabel()
        {
            IfcTestFile.Run(DegenerateWall, model =>
            {
                var geom = model.GetGeometry(Guid);
                Assert.False(geom.HasSolids);
                Assert.Contains(geom.Warnings, w => w.Contains("#21"));
            });
        }

        [Fact]
        public void InvalidShape_IsSkippedAndReported()
        {
            IfcTestFile.Run(OpenBrepWall, model =>
            {
                var geom = model.GetGeometry(Guid);
                Assert.False(geom.HasSolids);
                Assert.Contains(geom.Warnings, w => w.StartsWith("#21 IfcFacetedBrep:") && w.Contains("invalid"));
                Assert.DoesNotContain(geom.Warnings, w => w.Contains("AccessViolation"));
            });
        }

        [Fact]
        public void OpeningFallback_IsReported_AndCutStillApplied()
        {
            IfcTestFile.Run(WallWithMappedOpening, model =>
            {
                var geom = model.GetGeometry(Guid, new IfcConvertOptions { ApplyVoids = true });
                Assert.Contains(geom.Warnings, w => w.Contains("fallback"));
                Assert.Equal(1.0 - 0.4 * 0.5 * 1.0, geom.TotalVolume, 3);
            });
        }
    }
}
