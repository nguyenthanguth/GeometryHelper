using System;
using GeometryHelper.IfcConvert.Core;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    [Collection("IfcEngine")]
    public class CurvedGeometryTests
    {
        private const string Guid = "0000000000000000000002";

        private static string Member(string profileAndSolid) => IfcTestFile.CommonHeader() + profileAndSolid + @"
#22=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#21));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#22));
#24=IFCLOCALPLACEMENT($,#8);
#25=IFCCOLUMN('" + Guid + @"',$,'Member',$,$,#24,#23,$,$);
";

        // Round column: r = 0.5, h = 2 -> V = pi * 0.25 * 2
        private static readonly string RoundColumn = Member(@"
#20=IFCCIRCLEPROFILEDEF(.AREA.,$,#5,0.5);
#21=IFCEXTRUDEDAREASOLID(#20,#8,#9,2.);
");

        // Tube: r = 0.5, wall 0.1, h = 2 -> V = pi * (0.25 - 0.16) * 2
        private static readonly string Tube = Member(@"
#20=IFCCIRCLEHOLLOWPROFILEDEF(.AREA.,$,#5,0.5,0.1);
#21=IFCEXTRUDEDAREASOLID(#20,#8,#9,2.);
");

        // Steel plate 1 x 1 x 0.1 with one r = 0.2 bolt hole -> V = (1 - pi * 0.04) * 0.1
        private static readonly string PlateWithBoltHole = Member(@"
#30=IFCCARTESIANPOINT((-0.5,-0.5));
#31=IFCCARTESIANPOINT((0.5,-0.5));
#32=IFCCARTESIANPOINT((0.5,0.5));
#33=IFCCARTESIANPOINT((-0.5,0.5));
#34=IFCPOLYLINE((#30,#31,#32,#33,#30));
#35=IFCCIRCLE(#5,0.2);
#20=IFCARBITRARYPROFILEDEFWITHVOIDS(.AREA.,$,#34,(#35));
#21=IFCEXTRUDEDAREASOLID(#20,#8,#9,0.1);
");

        public static TheoryData<string, string, double> Cases => new TheoryData<string, string, double>
        {
            { "round column", RoundColumn, Math.PI * 0.25 * 2.0 },
            { "tube", Tube, Math.PI * (0.25 - 0.16) * 2.0 },
            { "plate with bolt hole", PlateWithBoltHole, (1.0 - Math.PI * 0.04) * 0.1 },
        };

        // Tekla-style millimetre model: 200 x 200 x 20 plate with an M20 bolt hole (r = 11).
        private static readonly string MillimetrePlateWithBoltHole = IfcTestFile.CommonHeader("..MILLI.,.METRE.") + @"
#30=IFCCARTESIANPOINT((-100.,-100.));
#31=IFCCARTESIANPOINT((100.,-100.));
#32=IFCCARTESIANPOINT((100.,100.));
#33=IFCCARTESIANPOINT((-100.,100.));
#34=IFCPOLYLINE((#30,#31,#32,#33,#30));
#35=IFCCIRCLE(#5,11.);
#20=IFCARBITRARYPROFILEDEFWITHVOIDS(.AREA.,$,#34,(#35));
#21=IFCEXTRUDEDAREASOLID(#20,#8,#9,20.);
#22=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#21));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#22));
#24=IFCLOCALPLACEMENT($,#8);
#25=IFCPLATE('" + Guid + @"',$,'Plate',$,$,#24,#23,$,$);
";

        [Theory]
        [InlineData(LengthUnit.Original, 1.0)]
        [InlineData(LengthUnit.Meters, 1e-9)]
        public void MillimetreModel_BoltHoleIsCut(LengthUnit unit, double volumeFactor)
        {
            double expected = (200.0 * 200.0 - Math.PI * 11.0 * 11.0) * 20.0 * volumeFactor;

            IfcTestFile.Run(MillimetrePlateWithBoltHole, model =>
            {
                Assert.Equal("MILLIMETRE", model.OriginalLengthUnit);

                var solid = model.GetSolid(Guid, new IfcConvertOptions { TargetUnit = unit });
                Assert.NotNull(solid);
                Assert.True(solid.IsClosed(), "solid is not closed");

                double error = Math.Abs(solid.Volume - expected) / expected;
                Assert.True(error < 0.001, $"volume {solid.Volume} vs expected {expected} ({error:P3} off)");
            });
        }

        // Millimetre model: 10 x 10 x 2000 flat bar. Its 10 x 10 mm end faces are 1e-4 m2 when output in metres.
        private static readonly string MillimetreFlatBar = IfcTestFile.CommonHeader("..MILLI.,.METRE.") + @"
#20=IFCRECTANGLEPROFILEDEF(.AREA.,$,#5,10.,10.);
#21=IFCEXTRUDEDAREASOLID(#20,#8,#9,2000.);
#22=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#21));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#22));
#24=IFCLOCALPLACEMENT($,#8);
#25=IFCMEMBER('" + Guid + @"',$,'Bar',$,$,#24,#23,$,$);
";

        [Fact]
        public void MillimetreModel_SmallFacesSurviveConversionToMetres()
        {
            IfcTestFile.Run(MillimetreFlatBar, model =>
            {
                var solid = model.GetSolid(Guid, new IfcConvertOptions { TargetUnit = LengthUnit.Meters });
                Assert.NotNull(solid);
                Assert.Equal(6, solid.Faces.Count);
                Assert.True(solid.IsClosed(), "solid is not closed");
                Assert.Equal(10.0 * 10.0 * 2000.0 * 1e-9, solid.Volume, 12);
            });
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void CurvedSolid_IsClosedAndVolumeWithinOnePercent(string name, string data, double expectedVolume)
        {
            IfcTestFile.Run(data, model =>
            {
                var solid = model.GetSolid(Guid, new IfcConvertOptions());
                Assert.True(solid != null, name + ": no solid");
                Assert.True(solid.IsClosed(), name + ": solid is not closed");

                double error = Math.Abs(solid.Volume - expectedVolume) / expectedVolume;
                Assert.True(error < 0.01, $"{name}: volume {solid.Volume:F5} vs expected {expectedVolume:F5} ({error:P2} off)");
            });
        }
    }
}
