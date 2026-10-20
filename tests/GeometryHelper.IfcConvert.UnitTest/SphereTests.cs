using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.IfcConvert.Core;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    /// <summary>
    /// The balls of an IFC model, read by <see cref="IfcStoreCache.GetSpheres"/> as <see cref="GeoSphere3"/> with the exact centre
    /// and radius of the <c>IfcSphere</c>, where <c>GetSolids</c> gives the same ball as a mesh.
    /// </summary>
    [Collection("IfcEngine")]
    public class SphereTests
    {
        private const string Guid = "0000000000000000000002";

        // A ball of radius 0.5 centred on (1, 2, 3), a product with the identity as its placement.
        private static string Ball(string items, string placement = "#24=IFCLOCALPLACEMENT($,#8);", string unit = "$,.METRE.") => IfcTestFile.CommonHeader(unit) + @"
#20=IFCCARTESIANPOINT((1.,2.,3.));
#21=IFCAXIS2PLACEMENT3D(#20,$,$);
" + items + @"
" + placement + @"
#25=IFCBUILDINGELEMENTPROXY('" + Guid + @"',$,'Ball',$,$,#24,#23,$,$);
";

        private static readonly string SphereItem = Ball(@"
#22=IFCSPHERE(#21,0.5);
#26=IFCSHAPEREPRESENTATION(#11,'Body','CSG',(#22));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#26));
");

        private static readonly string CsgSolidItem = Ball(@"
#22=IFCSPHERE(#21,0.5);
#27=IFCCSGSOLID(#22);
#26=IFCSHAPEREPRESENTATION(#11,'Body','CSG',(#27));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#26));
");

        private static GeometryHelper.Tolerance InMetres => new GeometryHelper.Tolerance(
            GeometryHelper.Tolerance.DefaultEqualPoint * 1E-3, GeometryHelper.Tolerance.DefaultEqualVector * 1E-3,
            GeometryHelper.Tolerance.DefaultEqualAngleRad, GeometryHelper.Tolerance.DefaultEqualPlanar * 1E-3);

        private static void AssertBall(GeoSphere3 ball, double x, double y, double z, double radius)
        {
            Assert.Equal(x, ball.Center.X, 12);
            Assert.Equal(y, ball.Center.Y, 12);
            Assert.Equal(z, ball.Center.Z, 12);
            Assert.Equal(radius, ball.Radius, 12);
        }

        [Fact]
        public void ASphereItem_IsABallWithItsCentreAndRadius()
        {
            IfcTestFile.Run(SphereItem, model =>
            {
                IReadOnlyList<GeoSphere3> balls = model.GetSpheres(Guid, new IfcConvertOptions { Tolerance = InMetres });

                GeoSphere3 ball = Assert.Single(balls);
                AssertBall(ball, 1.0, 2.0, 3.0, 0.5);
            });
        }

        [Fact]
        public void ASphereAtTheRootOfACsgSolid_IsABallToo()
        {
            IfcTestFile.Run(CsgSolidItem, model =>
            {
                GeoSphere3 ball = Assert.Single(model.GetSpheres(Guid, new IfcConvertOptions { Tolerance = InMetres }));
                AssertBall(ball, 1.0, 2.0, 3.0, 0.5);
            });
        }

        [Fact]
        public void TheBallInIfc2x3_IsReadAsInIfc4()
        {
            IfcTestFile.Run(SphereItem, model =>
            {
                GeoSphere3 ball = Assert.Single(model.GetSpheres(Guid, new IfcConvertOptions { Tolerance = InMetres }));
                AssertBall(ball, 1.0, 2.0, 3.0, 0.5);
            }, "IFC2X3");
        }

        [Fact]
        public void AModelInMillimetres_GivesTheBallInMetresWhenAskedFor()
        {
            string millimetres = Ball(@"
#22=IFCSPHERE(#21,250.);
#26=IFCSHAPEREPRESENTATION(#11,'Body','CSG',(#22));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#26));
", unit: "..MILLI.,.METRE.");

            IfcTestFile.Run(millimetres, model =>
            {
                GeoSphere3 asIs = Assert.Single(model.GetSpheres(Guid));
                AssertBall(asIs, 1.0, 2.0, 3.0, 250.0);

                var metres = new IfcConvertOptions { TargetUnit = LengthUnit.Meters, Tolerance = InMetres };
                AssertBall(Assert.Single(model.GetSpheres(Guid, metres)), 0.001, 0.002, 0.003, 0.25);
            });
        }

        [Fact]
        public void AProductPlacedByATurnAndAShift_CarriesTheBallWithIt()
        {
            // The product's own frame: origin (10, 0, 0), Z up, its X axis along global Y, so its Y axis is along global -X. The
            // ball at (1, 2, 3) in it is at (10, 0, 0) + 1 * (0, 1, 0) + 2 * (-1, 0, 0) + 3 * (0, 0, 1) = (8, 1, 3).
            string turned = Ball(@"
#22=IFCSPHERE(#21,0.5);
#26=IFCSHAPEREPRESENTATION(#11,'Body','CSG',(#22));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#26));
", @"
#40=IFCCARTESIANPOINT((10.,0.,0.));
#41=IFCDIRECTION((0.,1.,0.));
#42=IFCAXIS2PLACEMENT3D(#40,#9,#41);
#24=IFCLOCALPLACEMENT($,#42);
");

            IfcTestFile.Run(turned, model =>
            {
                var options = new IfcConvertOptions { Tolerance = InMetres };
                AssertBall(Assert.Single(model.GetSpheres(Guid, options)), 8.0, 1.0, 3.0, 0.5);

                options.CoordinateSpace = CoordinateSpace.Local;
                AssertBall(Assert.Single(model.GetSpheres(Guid, options)), 1.0, 2.0, 3.0, 0.5);
            });
        }

        [Fact]
        public void TheMeshOfTheSameBall_IsStillWhatGetSolidsGives()
        {
            IfcTestFile.Run(SphereItem, model =>
            {
                GeoSolid3 mesh = model.GetSolid(Guid, new IfcConvertOptions { Tolerance = InMetres });
                double ball = new GeoSphere3(new GeoPoint3(1.0, 2.0, 3.0), 0.5).Volume;

                Assert.NotNull(mesh);
                Assert.True(mesh.IsClosed(InMetres));

                // The engine's mesh lies inside the sphere and is not the ball: the exact one is what GetSpheres reads.
                Assert.InRange(mesh.GetVolume(InMetres), ball * 0.95, ball * 1.0);
            });
        }

        [Fact]
        public void AProductWithNoBall_GivesNone()
        {
            string plate = IfcTestFile.CommonHeader() + @"
#20=IFCRECTANGLEPROFILEDEF(.AREA.,$,#5,1.,0.5);
#21=IFCEXTRUDEDAREASOLID(#20,#8,#9,2.);
#26=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#21));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#26));
#24=IFCLOCALPLACEMENT($,#8);
#25=IFCPLATE('" + Guid + @"',$,'Plate',$,$,#24,#23,$,$);
";

            IfcTestFile.Run(plate, model =>
            {
                Assert.Empty(model.GetSpheres(Guid));
                Assert.NotNull(model.GetSolid(Guid));
            });
        }

        [Fact]
        public void ABallWithAFlatCutOffIt_IsNoLongerABallAndIsNotRead()
        {
            string cut = Ball(@"
#22=IFCSPHERE(#21,0.5);
#30=IFCBLOCK(#21,2.,2.,0.5);
#27=IFCBOOLEANRESULT(.DIFFERENCE.,#22,#30);
#26=IFCSHAPEREPRESENTATION(#11,'Body','CSG',(#27));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#26));
");

            IfcTestFile.Run(cut, model => Assert.Empty(model.GetSpheres(Guid)));
        }

        // The ball at (1, 2, 3) of radius 0.5 in a representation map whose origin is at (5, 0, 0), mapped by an operator that puts the
        // origin of the map at (0, 10, 0) and scales by what is given: world = Target * Origin * local.
        private static string Mapped(string operatorEntity) => Ball(@"
#22=IFCSPHERE(#21,0.5);
#26=IFCSHAPEREPRESENTATION(#11,'Body','CSG',(#22));
#40=IFCCARTESIANPOINT((5.,0.,0.));
#41=IFCAXIS2PLACEMENT3D(#40,$,$);
#42=IFCREPRESENTATIONMAP(#41,#26);
#47=IFCCARTESIANPOINT((0.,10.,0.));
" + operatorEntity + @"
#44=IFCMAPPEDITEM(#42,#43);
#45=IFCSHAPEREPRESENTATION(#11,'Body','MappedRepresentation',(#44));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#45));
");

        [Fact]
        public void ABallInAMappedItem_IsPlacedByTheMapping_AsASolidIs()
        {
            // (1, 2, 3) -> by the origin of the map (6, 2, 3) -> by the target (6, 12, 3).
            IfcTestFile.Run(Mapped("#43=IFCCARTESIANTRANSFORMATIONOPERATOR3D($,$,#47,$,$);"), model =>
            {
                var options = new IfcConvertOptions { Tolerance = InMetres };
                AssertBall(Assert.Single(model.GetSpheres(Guid, options)), 6.0, 12.0, 3.0, 0.5);

                // The mesh of the same product is where the ball is.
                GeoAabb3 box = model.GetSolid(Guid, options).GetAabb();
                Assert.Equal(6.0, (box.Min.X + box.Max.X) / 2.0, 1);
                Assert.Equal(12.0, (box.Min.Y + box.Max.Y) / 2.0, 1);
            });
        }

        [Fact]
        public void ABallInAMappedItemScaledEvenly_IsAScaledBall()
        {
            // Scaled by 2 about the origin of the operator: (6, 2, 3) -> (12, 4, 6), then to the target (12, 14, 6); the radius is 1.
            IfcTestFile.Run(Mapped("#43=IFCCARTESIANTRANSFORMATIONOPERATOR3D($,$,#47,2.,$);"), model =>
            {
                var options = new IfcConvertOptions { Tolerance = InMetres };
                AssertBall(Assert.Single(model.GetSpheres(Guid, options)), 12.0, 14.0, 6.0, 1.0);

                GeoSolid3 mesh = model.GetSolid(Guid, options);
                Assert.InRange(mesh.GetVolume(InMetres), new GeoSphere3(GeoPoint3.Origin, 1.0).Volume * 0.95, new GeoSphere3(GeoPoint3.Origin, 1.0).Volume);
            });
        }

        [Fact]
        public void ABallInAMappedItemScaledUnevenly_IsAnEllipsoidAndIsLeftOutWithAWarning()
        {
            IfcTestFile.Run(Mapped("#43=IFCCARTESIANTRANSFORMATIONOPERATOR3DNONUNIFORM($,$,#47,2.,$,3.,1.);"), model =>
            {
                List<(GeometryHelperLogLevel Level, string Text)> lines = Capture(() => Assert.Empty(model.GetSpheres(Guid, new IfcConvertOptions { Tolerance = InMetres })));

                Assert.Single(lines, l => l.Level == GeometryHelperLogLevel.Warn && l.Text.Contains("unevenly"));
                Assert.Single(lines, l => l.Level == GeometryHelperLogLevel.Info);
            });
        }

        [Fact]
        public void ABallInAMappedItemOfAProductPlaced_IsPlacedByTheMappingThenTheProduct()
        {
            // As the plain mapping, (6, 12, 3), then the product's frame: origin (10, 0, 0), its X axis along global Y, so (10 - 12, 6, 3).
            string placed = Mapped("#43=IFCCARTESIANTRANSFORMATIONOPERATOR3D($,$,#47,$,$);").Replace("#24=IFCLOCALPLACEMENT($,#8);", @"
#80=IFCCARTESIANPOINT((10.,0.,0.));
#81=IFCDIRECTION((0.,1.,0.));
#82=IFCAXIS2PLACEMENT3D(#80,#9,#81);
#24=IFCLOCALPLACEMENT($,#82);");

            IfcTestFile.Run(placed, model =>
            {
                var options = new IfcConvertOptions { Tolerance = InMetres };
                AssertBall(Assert.Single(model.GetSpheres(Guid, options)), -2.0, 6.0, 3.0, 0.5);

                options.CoordinateSpace = CoordinateSpace.Local;
                AssertBall(Assert.Single(model.GetSpheres(Guid, options)), 6.0, 12.0, 3.0, 0.5);
            });
        }

        // Openings cut a ball: the element's body is the ball with a square hole 2 by 2 through it.
        private static readonly string VoidedBall = IfcTestFile.CommonHeader() + @"
#20=IFCCARTESIANPOINT((0.,0.,0.));
#21=IFCAXIS2PLACEMENT3D(#20,$,$);
#22=IFCSPHERE(#21,0.5);
#26=IFCSHAPEREPRESENTATION(#11,'Body','CSG',(#22));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#26));
#24=IFCLOCALPLACEMENT($,#8);
#25=IFCBUILDINGELEMENTPROXY('" + Guid + @"',$,'Ball',$,$,#24,#23,$,$);
#70=IFCRECTANGLEPROFILEDEF(.AREA.,$,#5,0.2,0.2);
#71=IFCEXTRUDEDAREASOLID(#70,#8,#9,4.);
#72=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#71));
#73=IFCPRODUCTDEFINITIONSHAPE($,$,(#72));
#74=IFCLOCALPLACEMENT(#24,#8);
#75=IFCOPENINGELEMENT('0000000000000000000005',$,'Hole',$,$,#74,#73,$,$);
#76=IFCRELVOIDSELEMENT('0000000000000000000006',$,$,$,#25,#75);
";

        [Fact]
        public void ABallThatAnOpeningCuts_IsNotABallWhenOpeningsAreApplied_AndTheWarningNamesIt()
        {
            IfcTestFile.Run(VoidedBall, model =>
            {
                // The element with its opening cut is less than the whole ball, which is what GetSpheres would otherwise have given.
                var applied = new IfcConvertOptions { ApplyVoids = true, Tolerance = InMetres };
                GeoSolid3 cut = model.GetSolid(Guid, applied);
                Assert.True(cut.GetVolume(InMetres) < new GeoSphere3(GeoPoint3.Origin, 0.5).Volume * 0.99);

                List<(GeometryHelperLogLevel Level, string Text)> lines = Capture(() => Assert.Empty(model.GetSpheres(Guid, applied)));

                Assert.Single(lines, l => l.Level == GeometryHelperLogLevel.Warn && l.Text.Contains(Guid) && l.Text.Contains("ApplyVoids") && l.Text.Contains("not balls"));
                Assert.Single(lines, l => l.Level == GeometryHelperLogLevel.Info);
            });
        }

        [Fact]
        public void ABallThatAnOpeningCuts_IsTheWholeBallWhenOpeningsAreNotApplied()
        {
            IfcTestFile.Run(VoidedBall, model =>
            {
                GeoSphere3 ball = Assert.Single(model.GetSpheres(Guid, new IfcConvertOptions { Tolerance = InMetres }));
                AssertBall(ball, 0.0, 0.0, 0.0, 0.5);
            });
        }

        // An assembly with no body of its own and two parts, each a ball at (1, 2, 3) of radius 0.5 in its own frame: the assembly is at
        // (100, 0, 0), the second part is 5 above it.
        private static readonly string Assembly = IfcTestFile.CommonHeader() + @"
#20=IFCCARTESIANPOINT((1.,2.,3.));
#21=IFCAXIS2PLACEMENT3D(#20,$,$);
#22=IFCSPHERE(#21,0.5);
#26=IFCSHAPEREPRESENTATION(#11,'Body','CSG',(#22));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#26));
#30=IFCCARTESIANPOINT((100.,0.,0.));
#31=IFCAXIS2PLACEMENT3D(#30,$,$);
#32=IFCLOCALPLACEMENT($,#31);
#33=IFCELEMENTASSEMBLY('" + Guid + @"',$,'Assembly',$,$,#32,$,$,$,$);
#34=IFCLOCALPLACEMENT(#32,#8);
#35=IFCBUILDINGELEMENTPROXY('0000000000000000000031',$,'Part1',$,$,#34,#23,$,$);
#36=IFCCARTESIANPOINT((0.,0.,5.));
#37=IFCAXIS2PLACEMENT3D(#36,$,$);
#38=IFCLOCALPLACEMENT(#32,#37);
#39=IFCBUILDINGELEMENTPROXY('0000000000000000000032',$,'Part2',$,$,#38,#23,$,$);
#40=IFCRELAGGREGATES('0000000000000000000033',$,$,$,#33,(#35,#39));
";

        [Fact]
        public void AnAssemblyWhosePartsAreBalls_GivesThemWithIncludeAggregatedParts_AndNoneWithout()
        {
            IfcTestFile.Run(Assembly, model =>
            {
                var without = new IfcConvertOptions { Tolerance = InMetres };
                Assert.Empty(model.GetSpheres(Guid, without));

                var with = new IfcConvertOptions { Tolerance = InMetres, IncludeAggregatedParts = true };
                IReadOnlyList<GeoSphere3> balls = model.GetSpheres(Guid, with);
                Assert.Equal(2, balls.Count);
                AssertBall(balls[0], 101.0, 2.0, 3.0, 0.5);
                AssertBall(balls[1], 101.0, 2.0, 8.0, 0.5);

                // In the frame of the assembly, which is what GetSolids brings its parts into.
                with.CoordinateSpace = CoordinateSpace.Local;
                balls = model.GetSpheres(Guid, with);
                AssertBall(balls[0], 1.0, 2.0, 3.0, 0.5);
                AssertBall(balls[1], 1.0, 2.0, 8.0, 0.5);

                // The part asked for alone is its own ball, placed in the world, with or without the option, and in its own frame in the local space.
                with.CoordinateSpace = CoordinateSpace.Global;
                AssertBall(Assert.Single(model.GetSpheres("0000000000000000000032", with)), 101.0, 2.0, 8.0, 0.5);
                AssertBall(Assert.Single(model.GetSpheres("0000000000000000000032", without)), 101.0, 2.0, 8.0, 0.5);
                with.CoordinateSpace = CoordinateSpace.Local;
                AssertBall(Assert.Single(model.GetSpheres("0000000000000000000032", with)), 1.0, 2.0, 3.0, 0.5);
            });
        }

        // The ball placed and scaled, in a model in millimetres, as a reviewer's case: its position (100, 200, 300) in a product placed by
        // (1000, 0, 0) turned so that its X axis runs along Y, in a parent placed by (5000, 0, 0).
        private static readonly string PlacedInMillimetres = IfcTestFile.CommonHeader("..MILLI.,.METRE.") + @"
#20=IFCCARTESIANPOINT((100.,200.,300.));
#30=IFCDIRECTION((1.,0.,0.));
#31=IFCDIRECTION((0.,1.,0.));
#21=IFCAXIS2PLACEMENT3D(#20,#30,#31);
#22=IFCSPHERE(#21,250.);
#26=IFCSHAPEREPRESENTATION(#11,'Body','CSG',(#22));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#26));
#50=IFCCARTESIANPOINT((5000.,0.,0.));
#51=IFCAXIS2PLACEMENT3D(#50,$,$);
#52=IFCLOCALPLACEMENT($,#51);
#40=IFCCARTESIANPOINT((1000.,0.,0.));
#41=IFCDIRECTION((0.,1.,0.));
#42=IFCAXIS2PLACEMENT3D(#40,#9,#41);
#24=IFCLOCALPLACEMENT(#52,#42);
#25=IFCBUILDINGELEMENTPROXY('" + Guid + @"',$,'Ball',$,$,#24,#23,$,$);
";

        [Fact]
        public void ABallPlacedInAModelInMillimetres_IsWhereTheMeshIs_AsIsInMetresScaledAndLocal()
        {
            IfcTestFile.Run(PlacedInMillimetres, model =>
            {
                // (100, 200, 300) -> (1000, 0, 0) + 100 * (0, 1, 0) + 200 * (-1, 0, 0) + 300 * (0, 0, 1) = (800, 100, 300), then 5000 along X.
                AssertBall(Assert.Single(model.GetSpheres(Guid)), 5800.0, 100.0, 300.0, 250.0);

                var metres = new IfcConvertOptions { TargetUnit = LengthUnit.Meters, Tolerance = InMetres };
                AssertBall(Assert.Single(model.GetSpheres(Guid, metres)), 5.8, 0.1, 0.3, 0.25);

                // A scale factor of the options scales the centre and the radius as it scales every coordinate of a solid.
                AssertBall(Assert.Single(model.GetSpheres(Guid, new IfcConvertOptions { ScaleFactor = 2.0 })), 11600.0, 200.0, 600.0, 500.0);

                var local = new IfcConvertOptions { CoordinateSpace = CoordinateSpace.Local, TargetUnit = LengthUnit.Meters, Tolerance = InMetres };
                AssertBall(Assert.Single(model.GetSpheres(Guid, local)), 0.1, 0.2, 0.3, 0.25);

                // The mesh of the product is round the same centre.
                GeoAabb3 box = model.GetSolid(Guid, metres).GetAabb();
                Assert.Equal(5.8, (box.Min.X + box.Max.X) / 2.0, 2);
                Assert.Equal(0.1, (box.Min.Y + box.Max.Y) / 2.0, 2);
                Assert.Equal(0.3, (box.Min.Z + box.Max.Z) / 2.0, 2);
            });
        }

        [Fact]
        public void TwoBallsOfAProduct_ComeInTheOrderOfItsItems_EveryTime()
        {
            string two = Ball(@"
#22=IFCSPHERE(#21,0.5);
#60=IFCCARTESIANPOINT((-4.,0.,0.));
#61=IFCAXIS2PLACEMENT3D(#60,$,$);
#62=IFCSPHERE(#61,0.25);
#27=IFCCSGSOLID(#62);
#26=IFCSHAPEREPRESENTATION(#11,'Body','CSG',(#27,#22));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#26));
");

            IfcTestFile.Run(two, model =>
            {
                for (int i = 0; i < 3; i++)
                {
                    IReadOnlyList<GeoSphere3> balls = model.GetSpheres(Guid, new IfcConvertOptions { Tolerance = InMetres });
                    Assert.Equal(2, balls.Count);
                    AssertBall(balls[0], -4.0, 0.0, 0.0, 0.25);
                    AssertBall(balls[1], 1.0, 2.0, 3.0, 0.5);
                }
            });
        }

        [Fact]
        public void AnUnknownGuid_GivesNoBall()
        {
            IfcTestFile.Run(SphereItem, model =>
            {
                Assert.Empty(model.GetSpheres("0000000000000000000009"));
                Assert.Empty(model.GetSpheres(null));
                Assert.Empty(model.GetSpheres(string.Empty));
            });
        }

        [Theory]
        [InlineData("0000000000000000000009", "is not a product of the file")]
        [InlineData(null, "no GlobalId was given")]
        [InlineData("", "no GlobalId was given")]
        public void AnUnknownOrMissingGuid_StillWritesTheOneInfoLineOfTheCall(string guid, string said)
        {
            IfcTestFile.Run(SphereItem, model =>
            {
                List<(GeometryHelperLogLevel Level, string Text)> lines = Capture(() => Assert.Empty(model.GetSpheres(guid)));

                (GeometryHelperLogLevel Level, string Text) line = Assert.Single(lines);
                Assert.Equal(GeometryHelperLogLevel.Info, line.Level);
                Assert.Contains(said, line.Text);
            });
        }

        [Fact]
        public void AProductTheNameFiltersLeaveOut_GivesNoBall()
        {
            IfcTestFile.Run(SphereItem, model =>
            {
                var skipped = new IfcConvertOptions { Tolerance = InMetres }.AddSkipNames("Ball");
                Assert.Empty(model.GetSpheres(Guid, skipped));

                var only = new IfcConvertOptions { Tolerance = InMetres }.AddOnlyNames("Other");
                Assert.Empty(model.GetSpheres(Guid, only));

                var kept = new IfcConvertOptions { Tolerance = InMetres }.AddOnlyNames("ba*");
                Assert.Single(model.GetSpheres(Guid, kept));
            });
        }

        [Fact]
        public void ASphereOfNoRadius_IsLeftOutAndSaidSo()
        {
            string flat = Ball(@"
#22=IFCSPHERE(#21,0.);
#26=IFCSHAPEREPRESENTATION(#11,'Body','CSG',(#22));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#26));
");

            IfcTestFile.Run(flat, model =>
            {
                List<(GeometryHelperLogLevel Level, string Text)> lines = Capture(() => Assert.Empty(model.GetSpheres(Guid)));

                // A sphere refused is a Warn and nothing else: it is not counted among the items that are not spheres.
                Assert.Single(lines, l => l.Level == GeometryHelperLogLevel.Warn && l.Text.Contains("no radius above nought"));
                Assert.Single(lines, l => l.Level == GeometryHelperLogLevel.Info);
                Assert.DoesNotContain(lines, l => l.Level == GeometryHelperLogLevel.Debug);
                Assert.Equal(2, lines.Count);
            });
        }

        [Fact]
        public void EachCallWritesOneInfoSummary_AndAnItemThatIsNotASphereOnlyADebugLine()
        {
            string both = Ball(@"
#22=IFCSPHERE(#21,0.5);
#19=IFCRECTANGLEPROFILEDEF(.AREA.,$,#5,1.,0.5);
#18=IFCEXTRUDEDAREASOLID(#19,#8,#9,2.);
#26=IFCSHAPEREPRESENTATION(#11,'Body','CSG',(#22,#18));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#26));
");

            IfcTestFile.Run(both, model =>
            {
                List<(GeometryHelperLogLevel Level, string Text)> lines = Capture(() => Assert.Single(model.GetSpheres(Guid)));

                Assert.Single(lines, l => l.Level == GeometryHelperLogLevel.Info && l.Text.Contains("1 ball(s)"));
                Assert.Single(lines, l => l.Level == GeometryHelperLogLevel.Debug && l.Text.Contains("1 body item(s) are not a sphere"));
                Assert.DoesNotContain(lines, l => l.Level >= GeometryHelperLogLevel.Warn);
            });
        }

        // What the library logs while the work runs, with the writer put back after.
        private static List<(GeometryHelperLogLevel Level, string Text)> Capture(Action work)
        {
            var lines = new List<(GeometryHelperLogLevel, string)>();
            Action<GeometryHelperLogLevel, string, Exception> before = GeometryHelperLog.Writer;
            GeometryHelperLog.Writer = (level, text, exception) => lines.Add((level, text));

            try
            {
                work();
            }
            finally
            {
                GeometryHelperLog.Writer = before;
            }

            return lines;
        }
    }
}
