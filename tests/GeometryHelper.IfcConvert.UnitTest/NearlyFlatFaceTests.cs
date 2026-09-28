using System.Linq;
using GeometryHelper.Geometry;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Models;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    /// <summary>
    /// A face the file calls flat that strays from one plane by more than a polygon allows.
    /// </summary>
    /// <remarks>
    /// Tekla writes coordinates to a tenth of a micron, so a corner rounded the other way leaves a face off its plane
    /// by a few ten-thousandths of a millimetre: more than the ten-thousandth a polygon allows. Such a face was left out,
    /// and its body came out open. 178 bodies of one steel model lost a face that way, and two plates lost all but
    /// three and came out with no body at all.
    /// </remarks>
    [Collection("IfcEngine")]
    public class NearlyFlatFaceTests
    {
        // A strip 290 x 1.9942 x 78 in millimetres, as face 7 of brep #38621 of that model has it: the top corner at
        // (290, 22.0058) written 77.9997 where the other three are 78.
        private static readonly string Strip = IfcTestFile.CommonHeader(".MILLI.,.METRE.") + @"
#20=IFCCARTESIANPOINT((0.,22.0058,0.));
#21=IFCCARTESIANPOINT((290.,22.0058,0.));
#22=IFCCARTESIANPOINT((290.,24.,0.));
#23=IFCCARTESIANPOINT((0.,24.,0.));
#24=IFCCARTESIANPOINT((0.,22.0058,78.));
#25=IFCCARTESIANPOINT((290.,22.0058,77.9997));
#26=IFCCARTESIANPOINT((290.,24.,78.));
#27=IFCCARTESIANPOINT((0.,24.,78.));
#30=IFCPOLYLOOP((#20,#23,#22,#21));
#31=IFCPOLYLOOP((#24,#25,#26,#27));
#32=IFCPOLYLOOP((#20,#21,#25,#24));
#33=IFCPOLYLOOP((#21,#22,#26,#25));
#34=IFCPOLYLOOP((#22,#23,#27,#26));
#35=IFCPOLYLOOP((#23,#20,#24,#27));
#40=IFCFACEOUTERBOUND(#30,.T.);
#41=IFCFACEOUTERBOUND(#31,.T.);
#42=IFCFACEOUTERBOUND(#32,.T.);
#43=IFCFACEOUTERBOUND(#33,.T.);
#44=IFCFACEOUTERBOUND(#34,.T.);
#45=IFCFACEOUTERBOUND(#35,.T.);
#50=IFCFACE((#40));
#51=IFCFACE((#41));
#52=IFCFACE((#42));
#53=IFCFACE((#43));
#54=IFCFACE((#44));
#55=IFCFACE((#45));
#60=IFCCLOSEDSHELL((#50,#51,#52,#53,#54,#55));
#61=IFCFACETEDBREP(#60);
#70=IFCSHAPEREPRESENTATION(#11,'Body','Brep',(#61));
#71=IFCPRODUCTDEFINITIONSHAPE($,$,(#70));
#72=IFCLOCALPLACEMENT($,#8);
#73=IFCPLATE('0000000000000000000S01',$,'GUSSET',$,$,#72,#71,$,$);
";

        [Fact]
        public void AStripWithOneCornerRoundedTheOtherWay_ComesOutClosed()
        {
            IfcTestFile.Run(Strip, model =>
            {
                IfcProductGeometry strip = model.GetGeometry("0000000000000000000S01", new IfcConvertOptions { TargetUnit = LengthUnit.Millimeters });

                Assert.DoesNotContain(strip.Warnings, warning => warning.Contains("could not be read"));
                GeoSolid3 body = Assert.Single(strip.Solids);
                Assert.True(body.IsClosed());

                // Less what the corner written low takes off the top, about the top's area times a third of the dip.
                double full = 290 * 1.9942 * 78;
                Assert.InRange(body.Volume, full - 0.1, full);
            });
        }
    }
}
