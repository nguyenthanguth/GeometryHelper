using GeometryHelper.Geometry;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Models;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    /// <summary>
    /// A closed shell written with a flat gap in it.
    /// </summary>
    /// <remarks>
    /// Tekla writes the nut of a bolt with one face running straight across a corner the two faces beside it keep: the
    /// face skips a vertex its neighbours have, and a sliver of a triangle, three and a half square millimetres, is left
    /// open in its plane. 1,320 bodies of one steel model came out open that way, twelve bodies to a bolt.
    /// </remarks>
    [Collection("IfcEngine")]
    public class FlatGapTests
    {
        // A prism ten deep with a house-shaped end: A (0, 0), B (10, 0), C (10, 10), the ridge D (5, 12) and E (0, 10),
        // in X and Z. The back end has all five corners; the front end runs from C straight to E and skips the ridge,
        // which the two roof faces keep, so the triangle C D E of the front end is open.
        private static readonly string House = IfcTestFile.CommonHeader(".MILLI.,.METRE.") + @"
#20=IFCCARTESIANPOINT((0.,0.,0.));
#21=IFCCARTESIANPOINT((10.,0.,0.));
#22=IFCCARTESIANPOINT((10.,0.,10.));
#23=IFCCARTESIANPOINT((5.,0.,12.));
#24=IFCCARTESIANPOINT((0.,0.,10.));
#25=IFCCARTESIANPOINT((0.,10.,0.));
#26=IFCCARTESIANPOINT((10.,10.,0.));
#27=IFCCARTESIANPOINT((10.,10.,10.));
#28=IFCCARTESIANPOINT((5.,10.,12.));
#29=IFCCARTESIANPOINT((0.,10.,10.));
#30=IFCPOLYLOOP((#20,#21,#22,#24));
#31=IFCPOLYLOOP((#25,#29,#28,#27,#26));
#32=IFCPOLYLOOP((#20,#25,#26,#21));
#33=IFCPOLYLOOP((#21,#26,#27,#22));
#34=IFCPOLYLOOP((#22,#27,#28,#23));
#35=IFCPOLYLOOP((#23,#28,#29,#24));
#36=IFCPOLYLOOP((#24,#29,#25,#20));
#40=IFCFACEOUTERBOUND(#30,.T.);
#41=IFCFACEOUTERBOUND(#31,.T.);
#42=IFCFACEOUTERBOUND(#32,.T.);
#43=IFCFACEOUTERBOUND(#33,.T.);
#44=IFCFACEOUTERBOUND(#34,.T.);
#45=IFCFACEOUTERBOUND(#35,.T.);
#46=IFCFACEOUTERBOUND(#36,.T.);
#50=IFCFACE((#40));
#51=IFCFACE((#41));
#52=IFCFACE((#42));
#53=IFCFACE((#43));
#54=IFCFACE((#44));
#55=IFCFACE((#45));
#56=IFCFACE((#46));
#60=IFCCLOSEDSHELL((#50,#51,#52,#53,#54,#55,#56));
#61=IFCFACETEDBREP(#60);
#70=IFCSHAPEREPRESENTATION(#11,'Body','Brep',(#61));
#71=IFCPRODUCTDEFINITIONSHAPE($,$,(#70));
#72=IFCLOCALPLACEMENT($,#8);
#73=IFCMECHANICALFASTENER('0000000000000000000N01',$,'Bolt assembly',$,$,#72,#71,$,$,$,$);
";

        [Fact]
        public void AFaceSkippingACornerItsNeighboursKeep_IsClosedInItsPlane()
        {
            IfcTestFile.Run(House, model =>
            {
                IfcProductGeometry nut = model.GetGeometry("0000000000000000000N01", new IfcConvertOptions { TargetUnit = LengthUnit.Millimeters });

                GeoSolid3 body = Assert.Single(nut.Solids);
                Assert.DoesNotContain(nut.Warnings, warning => warning.Contains("not closed"));
                Assert.True(body.IsClosed());

                // The end is a ten-square with a roof two high on it: 110, ten deep.
                Assert.Equal(1100.0, body.Volume, 6);
            });
        }
    }
}
