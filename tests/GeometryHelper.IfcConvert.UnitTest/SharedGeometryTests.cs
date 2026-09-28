using System.Linq;
using System.Threading.Tasks;
using GeometryHelper.Geometry;
using GeometryHelper.IfcConvert.Converters.Internal;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Core.Internal;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    /// <summary>
    /// Geometry that one item describes and many products place — a brep that two beams both point at, a
    /// representation map that two bolts both map — is built once and placed for each, which is how Tekla writes
    /// its models: 21,757 products over 2,541 breps in one of them.
    /// </summary>
    [Collection("IfcEngine")]
    public class SharedGeometryTests
    {
        // A unit cube, one faceted brep (#61), used four times: by A, placed at the origin, and B, placed 10 along X,
        // each through a representation of its own; and by C and D through one representation map, mapped 20 and 30
        // along Y.
        private static readonly string SharedBrep = IfcTestFile.CommonHeader() + @"
#20=IFCCARTESIANPOINT((0.,0.,0.));
#21=IFCCARTESIANPOINT((1.,0.,0.));
#22=IFCCARTESIANPOINT((1.,1.,0.));
#23=IFCCARTESIANPOINT((0.,1.,0.));
#24=IFCCARTESIANPOINT((0.,0.,1.));
#25=IFCCARTESIANPOINT((1.,0.,1.));
#26=IFCCARTESIANPOINT((1.,1.,1.));
#27=IFCCARTESIANPOINT((0.,1.,1.));
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
#73=IFCBUILDINGELEMENTPROXY('0000000000000000000A01',$,'A',$,$,#72,#71,$,$);
#74=IFCSHAPEREPRESENTATION(#11,'Body','Brep',(#61));
#75=IFCPRODUCTDEFINITIONSHAPE($,$,(#74));
#76=IFCCARTESIANPOINT((10.,0.,0.));
#77=IFCAXIS2PLACEMENT3D(#76,$,$);
#78=IFCLOCALPLACEMENT($,#77);
#79=IFCBUILDINGELEMENTPROXY('0000000000000000000B01',$,'B',$,$,#78,#75,$,$);
#80=IFCSHAPEREPRESENTATION(#11,'Body','Brep',(#61));
#81=IFCREPRESENTATIONMAP(#8,#80);
#82=IFCCARTESIANPOINT((0.,20.,0.));
#83=IFCCARTESIANTRANSFORMATIONOPERATOR3D($,$,#82,$,$);
#84=IFCMAPPEDITEM(#81,#83);
#85=IFCSHAPEREPRESENTATION(#11,'Body','MappedRepresentation',(#84));
#86=IFCPRODUCTDEFINITIONSHAPE($,$,(#85));
#87=IFCBUILDINGELEMENTPROXY('0000000000000000000C01',$,'C',$,$,#72,#86,$,$);
#88=IFCCARTESIANPOINT((0.,30.,0.));
#89=IFCCARTESIANTRANSFORMATIONOPERATOR3D($,$,#88,$,$);
#90=IFCMAPPEDITEM(#81,#89);
#91=IFCSHAPEREPRESENTATION(#11,'Body','MappedRepresentation',(#90));
#92=IFCPRODUCTDEFINITIONSHAPE($,$,(#91));
#93=IFCBUILDINGELEMENTPROXY('0000000000000000000D01',$,'D',$,$,#72,#92,$,$);
";

        private static readonly (string Guid, GeoPoint3 Middle)[] Placed =
        {
            ("0000000000000000000A01", new GeoPoint3(0.5, 0.5, 0.5)),
            ("0000000000000000000B01", new GeoPoint3(10.5, 0.5, 0.5)),
            ("0000000000000000000C01", new GeoPoint3(0.5, 20.5, 0.5)),
            ("0000000000000000000D01", new GeoPoint3(0.5, 30.5, 0.5)),
        };

        [Fact]
        public void AnItemSharedByProductsAndMapsIsBuiltOnceAndPlacedForEach()
        {
            IfcTestFile.Run(SharedBrep, model =>
            {
                foreach ((string guid, GeoPoint3 middle) in Placed)
                {
                    GeoSolid3 cube = Assert.Single(model.GetSolids(guid));
                    Assert.Equal(1.0, cube.Volume, 9);
                    Assert.True(cube.Centroid.DistanceTo(middle) < 1E-9, $"{guid} is at {cube.Centroid}, not {middle}");
                }

                Assert.Equal(1, SolidConvert.BuiltItemCount(model.GetProduct("0000000000000000000A01").Model));
            });
        }

        [Fact]
        public void OtherSettingsBuildTheItemAgain()
        {
            IfcTestFile.Run(SharedBrep, model =>
            {
                GeoSolid3 metres = Assert.Single(model.GetSolids("0000000000000000000B01"));
                GeoSolid3 millimetres = Assert.Single(model.GetSolids("0000000000000000000B01", new IfcConvertOptions { TargetUnit = LengthUnit.Millimeters }));

                Assert.Equal(1.0, metres.Volume, 9);
                Assert.Equal(1E9, millimetres.Volume, 3);
                Assert.True(millimetres.Centroid.DistanceTo(new GeoPoint3(10500, 500, 500)) < 1E-6, $"{millimetres.Centroid}");
                Assert.Equal(2, SolidConvert.BuiltItemCount(model.GetProduct("0000000000000000000B01").Model));
            });
        }

        [Fact]
        public void ProductsConvertedTogetherFromManyThreadsShareOneBuild()
        {
            IfcTestFile.Run(SharedBrep, model =>
            {
                string[] guids = Enumerable.Range(0, 64).Select(k => Placed[k % Placed.Length].Guid).ToArray();
                var middles = new GeoPoint3[guids.Length];

                Parallel.For(0, guids.Length, k => middles[k] = model.GetSolids(guids[k]).Single().Centroid);

                for (int k = 0; k < guids.Length; k++)
                {
                    Assert.True(middles[k].DistanceTo(Placed[k % Placed.Length].Middle) < 1E-9, $"{guids[k]} is at {middles[k]}");
                }

                Assert.Equal(1, SolidConvert.BuiltItemCount(model.GetProduct("0000000000000000000A01").Model));
            });
        }
    }
}
