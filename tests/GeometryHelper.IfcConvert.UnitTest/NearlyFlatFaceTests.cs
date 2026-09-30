using System.Linq;
using GeometryHelper.Enums;
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

        // A plate 200 x 200 x 20 with a 40 x 40 hole through it, one corner of the hole's top rim written 0.3 mm out each
        // way: the hole of the top face stands off the face's plane, and the two walls of the hole meeting there are out
        // of flat, six times what the default tolerance lets a face stray.
        private static readonly string PlateWithAHoleCornerOut = IfcTestFile.CommonHeader(".MILLI.,.METRE.") + @"
#20=IFCCARTESIANPOINT((0.,0.,0.));
#21=IFCCARTESIANPOINT((200.,0.,0.));
#22=IFCCARTESIANPOINT((200.,200.,0.));
#23=IFCCARTESIANPOINT((0.,200.,0.));
#24=IFCCARTESIANPOINT((0.,0.,20.));
#25=IFCCARTESIANPOINT((200.,0.,20.));
#26=IFCCARTESIANPOINT((200.,200.,20.));
#27=IFCCARTESIANPOINT((0.,200.,20.));
#28=IFCCARTESIANPOINT((80.,80.,0.));
#29=IFCCARTESIANPOINT((120.,80.,0.));
#30=IFCCARTESIANPOINT((120.,120.,0.));
#31=IFCCARTESIANPOINT((80.,120.,0.));
#32=IFCCARTESIANPOINT((80.,80.,20.));
#33=IFCCARTESIANPOINT((120.,80.,20.));
#34=IFCCARTESIANPOINT((120.3,120.3,20.3));
#35=IFCCARTESIANPOINT((80.,120.,20.));
#40=IFCPOLYLOOP((#20,#23,#22,#21));
#41=IFCPOLYLOOP((#28,#29,#30,#31));
#42=IFCPOLYLOOP((#24,#25,#26,#27));
#43=IFCPOLYLOOP((#32,#35,#34,#33));
#44=IFCPOLYLOOP((#20,#21,#25,#24));
#45=IFCPOLYLOOP((#21,#22,#26,#25));
#46=IFCPOLYLOOP((#22,#23,#27,#26));
#47=IFCPOLYLOOP((#23,#20,#24,#27));
#48=IFCPOLYLOOP((#28,#32,#33,#29));
#49=IFCPOLYLOOP((#29,#33,#34,#30));
#50=IFCPOLYLOOP((#30,#34,#35,#31));
#51=IFCPOLYLOOP((#31,#35,#32,#28));
#60=IFCFACEOUTERBOUND(#40,.T.);
#61=IFCFACEBOUND(#41,.T.);
#62=IFCFACEOUTERBOUND(#42,.T.);
#63=IFCFACEBOUND(#43,.T.);
#64=IFCFACEOUTERBOUND(#44,.T.);
#65=IFCFACEOUTERBOUND(#45,.T.);
#66=IFCFACEOUTERBOUND(#46,.T.);
#67=IFCFACEOUTERBOUND(#47,.T.);
#68=IFCFACEOUTERBOUND(#48,.T.);
#69=IFCFACEOUTERBOUND(#49,.T.);
#70=IFCFACEOUTERBOUND(#50,.T.);
#71=IFCFACEOUTERBOUND(#51,.T.);
#80=IFCFACE((#60,#61));
#81=IFCFACE((#62,#63));
#82=IFCFACE((#64));
#83=IFCFACE((#65));
#84=IFCFACE((#66));
#85=IFCFACE((#67));
#86=IFCFACE((#68));
#87=IFCFACE((#69));
#88=IFCFACE((#70));
#89=IFCFACE((#71));
#90=IFCCLOSEDSHELL((#80,#81,#82,#83,#84,#85,#86,#87,#88,#89));
#91=IFCFACETEDBREP(#90);
#92=IFCSHAPEREPRESENTATION(#11,'Body','Brep',(#91));
#93=IFCPRODUCTDEFINITIONSHAPE($,$,(#92));
#94=IFCLOCALPLACEMENT($,#8);
#95=IFCPLATE('0000000000000000000P01',$,'PLATE',$,$,#94,#93,$,$);
";

        [Fact]
        public void APlateWithACornerOfItsHoleOutOfFlat_ComesOutClosedWithItsHole()
        {
            IfcTestFile.Run(PlateWithAHoleCornerOut, model =>
            {
                IfcProductGeometry plate = model.GetGeometry("0000000000000000000P01", new IfcConvertOptions { TargetUnit = LengthUnit.Millimeters });

                Assert.DoesNotContain(plate.Warnings, warning => warning.Contains("could not be read") || warning.Contains("dropped"));
                GeoSolid3 body = Assert.Single(plate.Solids);
                Assert.True(body.IsClosed());

                double full = (200.0 * 200.0 - 40.0 * 40.0) * 20.0;
                Assert.InRange(body.Volume, full * 0.99, full * 1.01);
                Assert.Equal(PointLocation.OutSide, body.Locate(new GeoPoint3(100, 100, 10)));
                Assert.Equal(PointLocation.Inside, body.Locate(new GeoPoint3(40, 40, 10)));
            });
        }

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
