using System.Linq;
using System.Threading.Tasks;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Models;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    [Collection("IfcEngine")]
    public class AssemblyAndIndexTests
    {
        private const string AssemblyGuid = "0000000000000000000030";

        // Tekla-style assembly at world (100,0,0) without its own body, aggregating two 1 m3 parts
        // placed relative to it at (0,0,0) and (0,0,5).
        private static readonly string Assembly = IfcTestFile.CommonHeader() + @"
#20=IFCRECTANGLEPROFILEDEF(.AREA.,$,#5,1.,0.5);
#21=IFCEXTRUDEDAREASOLID(#20,#8,#9,2.);
#22=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#21));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#22));
#30=IFCCARTESIANPOINT((100.,0.,0.));
#31=IFCAXIS2PLACEMENT3D(#30,$,$);
#32=IFCLOCALPLACEMENT($,#31);
#33=IFCELEMENTASSEMBLY('" + AssemblyGuid + @"',$,'Assembly',$,$,#32,$,$,$,$);
#34=IFCLOCALPLACEMENT(#32,#8);
#35=IFCBEAM('0000000000000000000031',$,'Part1',$,$,#34,#23,$,$);
#36=IFCCARTESIANPOINT((0.,0.,5.));
#37=IFCAXIS2PLACEMENT3D(#36,$,$);
#38=IFCLOCALPLACEMENT(#32,#37);
#39=IFCPLATE('0000000000000000000032',$,'Part2',$,$,#38,#23,$,$);
#40=IFCRELAGGREGATES('0000000000000000000033',$,$,$,#33,(#35,#39));
";

        [Fact]
        public void Assembly_HasNoGeometry_ByDefault()
        {
            IfcTestFile.Run(Assembly, model =>
            {
                Assert.True(model.GetGeometry(AssemblyGuid).IsEmpty);
                Assert.Equal(2, model.GetAllSolids().Count);
            });
        }

        [Theory]
        [InlineData(CoordinateSpace.Global, 100.0, 3.5)]
        [InlineData(CoordinateSpace.Local, 0.0, 3.5)]
        public void Assembly_CollectsAggregatedParts_WhenRequested(CoordinateSpace space, double centreX, double centreZ)
        {
            IfcTestFile.Run(Assembly, model =>
            {
                var options = new IfcConvertOptions { IncludeAggregatedParts = true, CoordinateSpace = space };
                IfcProductGeometry geom = model.GetGeometry(AssemblyGuid, options);

                Assert.Equal(2, geom.Solids.Count);
                Assert.Equal(2.0, geom.TotalVolume, 6);
                Assert.Equal(centreX, geom.BoundingBox.Center.X, 6);
                Assert.Equal(centreZ, geom.BoundingBox.Center.Z, 6);
            });
        }

        [Fact]
        public void DuplicateGlobalIds_AreReported()
        {
            string data = Assembly + "#50=IFCCOLUMN('0000000000000000000031',$,'Clash',$,$,#34,#23,$,$);";
            IfcTestFile.Run(data, model =>
            {
                Assert.Equal(new[] { "0000000000000000000031" }, model.DuplicateGlobalIds.ToArray());
            });
        }

        [Fact]
        public void ConcurrentRequests_ShareOneConversion()
        {
            IfcTestFile.Run(Assembly, model =>
            {
                IfcProductGeometry[] results = new IfcProductGeometry[16];
                Parallel.For(0, results.Length, i => results[i] = model.GetGeometry("0000000000000000000031"));

                Assert.All(results, r => Assert.Same(results[0], r));
                Assert.Equal(1.0, results[0].TotalVolume, 6);
            });
        }
    }
}
