using GeometryHelper.IfcConvert.Core;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    [Collection("IfcEngine")]
    public class OpeningPlacementTests
    {
        // Wall: 1 (X) x 0.5 (Y) x 2 (Z), placed at world (10,0,0).
        // Opening: 0.4 x 1.0 x 1.0 through the wall thickness, placed relative to the wall at (0.4,0,0.5),
        // so it only notches the wall end (x 0.2..0.6 against wall x -0.5..0.5): removed 0.3 x 0.5 x 1 = 0.15.
        // If the opening were positioned in the wrong frame it would cut the middle (x -0.2..0.2) and remove 0.2.
        private static readonly string WallWithRelativeOpening = IfcTestFile.CommonHeader() + @"
#20=IFCRECTANGLEPROFILEDEF(.AREA.,$,#5,1.,0.5);
#21=IFCEXTRUDEDAREASOLID(#20,#8,#9,2.);
#22=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#21));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#22));
#30=IFCCARTESIANPOINT((10.,0.,0.));
#31=IFCAXIS2PLACEMENT3D(#30,$,$);
#24=IFCLOCALPLACEMENT($,#31);
#25=IFCWALL('0000000000000000000002',$,'Wall',$,$,#24,#23,$,$);
#40=IFCRECTANGLEPROFILEDEF(.AREA.,$,#5,0.4,1.);
#41=IFCEXTRUDEDAREASOLID(#40,#8,#9,1.);
#42=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#41));
#43=IFCPRODUCTDEFINITIONSHAPE($,$,(#42));
#44=IFCCARTESIANPOINT((0.4,0.,0.5));
#45=IFCAXIS2PLACEMENT3D(#44,$,$);
#46=IFCLOCALPLACEMENT(#24,#45);
#47=IFCOPENINGELEMENT('0000000000000000000003',$,'Opening',$,$,#46,#43,$,$);
#48=IFCRELVOIDSELEMENT('0000000000000000000004',$,$,$,#25,#47);
";

        // Wall 1 x 0.5 x 2 with a round r = 0.1 opening drilled through its thickness (along Y) at mid height:
        // V = 1 - pi * 0.01 * 0.5.
        private static readonly string WallWithRoundOpening = IfcTestFile.CommonHeader() + @"
#20=IFCRECTANGLEPROFILEDEF(.AREA.,$,#5,1.,0.5);
#21=IFCEXTRUDEDAREASOLID(#20,#8,#9,2.);
#22=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#21));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#22));
#24=IFCLOCALPLACEMENT($,#8);
#25=IFCWALL('0000000000000000000002',$,'Wall',$,$,#24,#23,$,$);
#40=IFCCIRCLEPROFILEDEF(.AREA.,$,#5,0.1);
#41=IFCEXTRUDEDAREASOLID(#40,#8,#9,1.);
#42=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#41));
#43=IFCPRODUCTDEFINITIONSHAPE($,$,(#42));
#44=IFCCARTESIANPOINT((0.,-0.5,1.));
#49=IFCDIRECTION((0.,1.,0.));
#50=IFCDIRECTION((1.,0.,0.));
#45=IFCAXIS2PLACEMENT3D(#44,#49,#50);
#46=IFCLOCALPLACEMENT(#24,#45);
#47=IFCOPENINGELEMENT('0000000000000000000003',$,'Hole',$,$,#46,#43,$,$);
#48=IFCRELVOIDSELEMENT('0000000000000000000004',$,$,$,#25,#47);
";

        [Fact]
        public void ApplyVoids_RoundOpening_IsSubtracted()
        {
            IfcTestFile.Run(WallWithRoundOpening, model =>
            {
                var geom = model.GetGeometry("0000000000000000000002", new IfcConvertOptions { ApplyVoids = true });
                double expected = 1.0 - System.Math.PI * 0.01 * 0.5;
                Assert.True(System.Math.Abs(geom.TotalVolume - expected) / expected < 0.001,
                    $"volume {geom.TotalVolume:F5} vs expected {expected:F5}");
            });
        }

        [Theory]
        [InlineData(CoordinateSpace.Global, 10.0)]
        [InlineData(CoordinateSpace.Local, 0.0)]
        public void ApplyVoids_OpeningIsCutInHostFrame(CoordinateSpace space, double expectedCentreX)
        {
            IfcTestFile.Run(WallWithRelativeOpening, model =>
            {
                var options = new IfcConvertOptions { ApplyVoids = true, CoordinateSpace = space };
                var geom = model.GetGeometry("0000000000000000000002", options);

                Assert.Equal(0.85, geom.TotalVolume, 3);
                Assert.Equal(expectedCentreX, geom.BoundingBox.Center.X, 3);
            });
        }

        [Fact]
        public void TargetUnit_KeepsSkipNames()
        {
            IfcTestFile.Run(WallWithRelativeOpening, model =>
            {
                var options = new IfcConvertOptions { TargetUnit = LengthUnit.Millimeters }.AddSkipNames("Wall");
                Assert.True(model.GetGeometry("0000000000000000000002", options).IsEmpty);
            });
        }

        [Fact]
        public void TargetUnit_KeepsOnlyNames()
        {
            IfcTestFile.Run(WallWithRelativeOpening, model =>
            {
                // Resolving the unit copies the options; the copy must keep the list.
                var others = new IfcConvertOptions { TargetUnit = LengthUnit.Millimeters, OnlyNames = { "Column*" } };
                Assert.True(model.GetGeometry("0000000000000000000002", others).IsEmpty);

                var walls = new IfcConvertOptions { TargetUnit = LengthUnit.Millimeters, OnlyNames = { "Wall" } };
                Assert.Equal(1e9, model.GetGeometry("0000000000000000000002", walls).TotalVolume, 0);
            });
        }

        [Fact]
        public void ApplyVoids_OnlyNamesOfTheHost_StillCutsItsOpening()
        {
            IfcTestFile.Run(WallWithRelativeOpening, model =>
            {
                // The opening is named "Opening"; the list names products to read, not the openings cutting them.
                var options = new IfcConvertOptions { ApplyVoids = true, OnlyNames = { "Wall" } };
                Assert.Equal(0.85, model.GetGeometry("0000000000000000000002", options).TotalVolume, 3);
            });
        }
    }
}
