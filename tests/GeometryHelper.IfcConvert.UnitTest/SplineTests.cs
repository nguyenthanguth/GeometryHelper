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
    /// The B-spline curves of an IFC model, read by <see cref="IfcStoreCache.GetSplines"/> as <see cref="GeoSpline3"/> with the degree,
    /// control points, knots (each repeated as often as the file says) and weights of the <c>IfcBSplineCurveWithKnots</c>.
    /// </summary>
    [Collection("IfcEngine")]
    public class SplineTests
    {
        private const string Guid = "0000000000000000000002";

        private static GeometryHelper.Tolerance InMetres => new GeometryHelper.Tolerance(
            GeometryHelper.Tolerance.DefaultEqualPoint * 1E-3, GeometryHelper.Tolerance.DefaultEqualVector * 1E-3,
            GeometryHelper.Tolerance.DefaultEqualAngleRad, GeometryHelper.Tolerance.DefaultEqualPlanar * 1E-3);

        // The cubic of five control points with an inner knot at 0.5, its knots given as (0, 0.5, 1) repeated (4, 1, 4).
        private const string Cubic = @"
#30=IFCCARTESIANPOINT((0.,0.,0.));
#31=IFCCARTESIANPOINT((1.,2.,0.));
#32=IFCCARTESIANPOINT((2.,2.,1.));
#33=IFCCARTESIANPOINT((3.,0.,1.));
#34=IFCCARTESIANPOINT((4.,1.,0.5));
#35=IFCBSPLINECURVEWITHKNOTS(3,(#30,#31,#32,#33,#34),.UNSPECIFIED.,.F.,.F.,(4,1,4),(0.,0.5,1.),.UNSPECIFIED.);
";

        private static readonly GeoPoint3[] CubicPoints =
        {
            new GeoPoint3(0, 0, 0), new GeoPoint3(1, 2, 0), new GeoPoint3(2, 2, 1), new GeoPoint3(3, 0, 1), new GeoPoint3(4, 1, 0.5),
        };

        // A product with the given representations, its placement the given one.
        private static string Product(string items, string representations, string placement = "#24=IFCLOCALPLACEMENT($,#8);", string unit = "$,.METRE.")
            => IfcTestFile.CommonHeader(unit) + items + @"
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(" + representations + @"));
" + placement + @"
#25=IFCBUILDINGELEMENTPROXY('" + Guid + @"',$,'Curve',$,$,#24,#23,$,$);
";

        private static string AxisOf(string curve) => Product(curve + @"
#26=IFCSHAPEREPRESENTATION(#11,'Axis','Curve3D',(#35));
", "#26");

        private static void SamePoints(IReadOnlyList<GeoPoint3> expected, IReadOnlyList<GeoPoint3> actual, double within = 1E-12)
        {
            Assert.Equal(expected.Count, actual.Count);

            for (int i = 0; i < expected.Count; i++)
            {
                Assert.True(expected[i].DistanceTo(actual[i]) <= within, i + ": " + actual[i] + " where " + expected[i] + " was given");
            }
        }

        [Fact]
        public void ACurveOfTheAxis_IsASplineWithItsKnotsRepeatedAsTheFileSays()
        {
            IfcTestFile.Run(AxisOf(Cubic), model =>
            {
                GeoSpline3 spline = Assert.Single(model.GetSplines(Guid, new IfcConvertOptions { Tolerance = InMetres }));

                Assert.Equal(3, spline.Degree);
                Assert.Equal(new[] { 0, 0, 0, 0, 0.5, 1, 1, 1, 1 }, spline.Knots);
                Assert.False(spline.IsRational);
                SamePoints(CubicPoints, spline.ControlPoints, 0.0);
            });
        }

        [Fact]
        public void ARationalCurve_KeepsItsWeights_AQuarterCircleOfThemOnItsCircle()
        {
            // The quarter of the circle of radius 2 about the origin in XY, one rational quadratic piece.
            string quarter = AxisOf(@"
#30=IFCCARTESIANPOINT((2.,0.,0.));
#31=IFCCARTESIANPOINT((2.,2.,0.));
#32=IFCCARTESIANPOINT((0.,2.,0.));
#35=IFCRATIONALBSPLINECURVEWITHKNOTS(2,(#30,#31,#32),.CIRCULAR_ARC.,.F.,.F.,(3,3),(0.,1.),.PIECEWISE_BEZIER_KNOTS.,(1.,0.7071067811865476,1.));
");

            IfcTestFile.Run(quarter, model =>
            {
                GeoSpline3 spline = Assert.Single(model.GetSplines(Guid, new IfcConvertOptions { Tolerance = InMetres }));

                Assert.True(spline.IsRational);
                Assert.Equal(new[] { 1.0, 0.7071067811865476, 1.0 }, spline.Weights);

                for (int i = 0; i <= 8; i++)
                {
                    Assert.Equal(2.0, spline.GetPointAtParameter(i / 8.0).DistanceTo(GeoPoint3.Origin), 12);
                }
            });
        }

        [Fact]
        public void AProductPlacedByATurnAndAShift_CarriesTheSplineWithIt_AndLocalLeavesItInItsFrame()
        {
            // The product's frame: origin (10, 0, 0), Z up, its X axis along global Y: (x, y, z) is at (10 - y, x, z).
            string turned = AxisOf(Cubic).Replace("#24=IFCLOCALPLACEMENT($,#8);", @"
#40=IFCCARTESIANPOINT((10.,0.,0.));
#41=IFCDIRECTION((0.,1.,0.));
#42=IFCAXIS2PLACEMENT3D(#40,#9,#41);
#24=IFCLOCALPLACEMENT($,#42);");

            IfcTestFile.Run(turned, model =>
            {
                var options = new IfcConvertOptions { Tolerance = InMetres };
                GeoSpline3 placed = Assert.Single(model.GetSplines(Guid, options));

                SamePoints(CubicPoints.Select(p => new GeoPoint3(10.0 - p.Y, p.X, p.Z)).ToList(), placed.ControlPoints);
                Assert.Equal(new[] { 0, 0, 0, 0, 0.5, 1, 1, 1, 1 }, placed.Knots);

                options.CoordinateSpace = CoordinateSpace.Local;
                SamePoints(CubicPoints, Assert.Single(model.GetSplines(Guid, options)).ControlPoints, 0.0);
            });
        }

        [Fact]
        public void AModelInMillimetres_GivesTheControlPointsInMetresWhenAskedFor_TheKnotsAsTheyAre()
        {
            IfcTestFile.Run(Product(Cubic + @"
#26=IFCSHAPEREPRESENTATION(#11,'Axis','Curve3D',(#35));
", "#26", unit: "..MILLI.,.METRE."), model =>
            {
                GeoSpline3 asIs = Assert.Single(model.GetSplines(Guid));
                SamePoints(CubicPoints, asIs.ControlPoints, 0.0);

                GeoSpline3 metres = Assert.Single(model.GetSplines(Guid, new IfcConvertOptions { TargetUnit = LengthUnit.Meters, Tolerance = InMetres }));
                SamePoints(CubicPoints.Select(p => new GeoPoint3(p.X * 1E-3, p.Y * 1E-3, p.Z * 1E-3)).ToList(), metres.ControlPoints, 1E-15);
                Assert.Equal(new[] { 0, 0, 0, 0, 0.5, 1, 1, 1, 1 }, metres.Knots);
            });
        }

        [Fact]
        public void ACurveOfTwoDimensions_IsAtHeightNought()
        {
            string flat = AxisOf(@"
#30=IFCCARTESIANPOINT((0.,0.));
#31=IFCCARTESIANPOINT((1.,2.));
#32=IFCCARTESIANPOINT((3.,0.));
#35=IFCBSPLINECURVEWITHKNOTS(2,(#30,#31,#32),.UNSPECIFIED.,.F.,.F.,(3,3),(0.,1.),.UNSPECIFIED.);
");

            IfcTestFile.Run(flat, model =>
            {
                GeoSpline3 spline = Assert.Single(model.GetSplines(Guid, new IfcConvertOptions { Tolerance = InMetres }));
                SamePoints(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(1, 2, 0), new GeoPoint3(3, 0, 0) }, spline.ControlPoints, 0.0);
            });
        }

        [Fact]
        public void ASplineInACompositeCurve_AndAsTheDirectrixOfASweptDisk_IsRead_AndOnceWhereItIsBoth()
        {
            // The same cubic is the one segment of the axis and the path of the bar of the body.
            string bar = Product(Cubic + @"
#36=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.T.,#35);
#37=IFCCOMPOSITECURVE((#36),.F.);
#26=IFCSHAPEREPRESENTATION(#11,'Axis','Curve3D',(#37));
#38=IFCSWEPTDISKSOLID(#35,0.1,$,$,$);
#27=IFCSHAPEREPRESENTATION(#11,'Body','AdvancedSweptSolid',(#38));
", "#26,#27");

            IfcTestFile.Run(bar, model =>
            {
                GeoSpline3 spline = Assert.Single(model.GetSplines(Guid, new IfcConvertOptions { Tolerance = InMetres }));
                SamePoints(CubicPoints, spline.ControlPoints, 0.0);
            });

            string diskOnly = Product(Cubic + @"
#38=IFCSWEPTDISKSOLID(#35,0.1,$,$,$);
#27=IFCSHAPEREPRESENTATION(#11,'Body','AdvancedSweptSolid',(#38));
", "#27");

            IfcTestFile.Run(diskOnly, model => Assert.Single(model.GetSplines(Guid, new IfcConvertOptions { Tolerance = InMetres })));
        }

        private static string Disk(string start, string end, bool withAxis) => Product(Cubic + @"
#38=IFCSWEPTDISKSOLID(#35,0.1,$," + start + "," + end + @");
#27=IFCSHAPEREPRESENTATION(#11,'Body','AdvancedSweptSolid',(#38));
" + (withAxis ? @"#26=IFCSHAPEREPRESENTATION(#11,'Axis','Curve3D',(#35));
" : string.Empty), withAxis ? "#26,#27" : "#27");

        private static GeoSpline3 WholeCubic() => new GeoSpline3(3, CubicPoints, new[] { 0, 0, 0, 0, 0.5, 1, 1, 1, 1 });

        [Fact]
        public void ASweptDiskOverAPartOfItsDirectrix_GivesThatPart_CutAtTheKnotValuesItRunsBetween()
        {
            // The parameter of a B-spline curve is the value of its knots: the disk from 0.25 to 0.75 runs along the middle of the cubic.
            IfcTestFile.Run(Disk("0.25", "0.75", false), model =>
            {
                GeoSpline3 part = Assert.Single(model.GetSplines(Guid, new IfcConvertOptions { Tolerance = InMetres }));
                GeoSpline3 expected = WholeCubic().SplitAtKnot(0.25)[1].SplitAtKnot(0.75)[0];

                Assert.Equal(expected, part);
                Assert.Equal(0.25, part.DomainStart);
                Assert.Equal(0.75, part.DomainEnd);
            });

            // From a start alone it runs to the end of the curve.
            IfcTestFile.Run(Disk("0.25", "$", false), model =>
                Assert.Equal(WholeCubic().SplitAtKnot(0.25)[1], Assert.Single(model.GetSplines(Guid, new IfcConvertOptions { Tolerance = InMetres }))));

            // The whole domain is the whole curve.
            IfcTestFile.Run(Disk("0.", "1.", false), model =>
                Assert.Equal(WholeCubic(), Assert.Single(model.GetSplines(Guid, new IfcConvertOptions { Tolerance = InMetres }))));
        }

        [Fact]
        public void ACurveThatIsTheAxisAndTheDirectrixOfADiskOverAPartOfIt_IsReadAsBoth()
        {
            IfcTestFile.Run(Disk("0.25", "0.75", true), model =>
            {
                IReadOnlyList<GeoSpline3> splines = model.GetSplines(Guid, new IfcConvertOptions { Tolerance = InMetres });

                Assert.Equal(2, splines.Count);
                Assert.Equal(WholeCubic(), splines[0]);
                Assert.Equal(WholeCubic().SplitAtKnot(0.25)[1].SplitAtKnot(0.75)[0], splines[1]);
            });
        }

        [Fact]
        public void ACurveThatIsTheAxisAndTheDirectrixOfADiskOverItsWholeDomainWrittenOut_IsReadOnce()
        {
            IfcTestFile.Run(Disk("0.", "1.", true), model =>
                Assert.Equal(WholeCubic(), Assert.Single(model.GetSplines(Guid, new IfcConvertOptions { Tolerance = InMetres }))));

            IfcTestFile.Run(Disk("0.", "$", true), model =>
                Assert.Equal(WholeCubic(), Assert.Single(model.GetSplines(Guid, new IfcConvertOptions { Tolerance = InMetres }))));
        }

        [Fact]
        public void ASweptDiskOverParametersOutsideTheDomain_GivesTheWholeCurveWithAWarning()
        {
            IfcTestFile.Run(Disk("-1.", "2.", false), model =>
            {
                List<(GeometryHelperLogLevel Level, string Text)> lines = Capture(() =>
                    Assert.Equal(WholeCubic(), Assert.Single(model.GetSplines(Guid, new IfcConvertOptions { Tolerance = InMetres }))));

                Assert.Single(lines, l => l.Level == GeometryHelperLogLevel.Warn && l.Text.Contains("not inside the curve's domain"));
            });
        }

        [Fact]
        public void ASplineInAMappedItem_IsPlacedByTheMapping_AnUnevenScaleCarriedExactly()
        {
            // Mapped from a map whose origin is at (5, 0, 0), by an operator at (0, 10, 0) that scales X by 2, Y by 3 and Z by 1.
            string mapped = Product(Cubic + @"
#26=IFCSHAPEREPRESENTATION(#11,'Axis','Curve3D',(#35));
#40=IFCCARTESIANPOINT((5.,0.,0.));
#41=IFCAXIS2PLACEMENT3D(#40,$,$);
#42=IFCREPRESENTATIONMAP(#41,#26);
#47=IFCCARTESIANPOINT((0.,10.,0.));
#43=IFCCARTESIANTRANSFORMATIONOPERATOR3DNONUNIFORM($,$,#47,2.,$,3.,1.);
#44=IFCMAPPEDITEM(#42,#43);
#45=IFCSHAPEREPRESENTATION(#11,'Axis','MappedRepresentation',(#44));
", "#45");

            IfcTestFile.Run(mapped, model =>
            {
                GeoSpline3 spline = Assert.Single(model.GetSplines(Guid, new IfcConvertOptions { Tolerance = InMetres }));
                SamePoints(CubicPoints.Select(p => new GeoPoint3((p.X + 5.0) * 2.0, p.Y * 3.0 + 10.0, p.Z)).ToList(), spline.ControlPoints);
                Assert.Equal(new[] { 0, 0, 0, 0, 0.5, 1, 1, 1, 1 }, spline.Knots);
            });
        }

        [Fact]
        public void APeriodicCurve_IsLeftOutWithAWarning()
        {
            // Uniform knots that run on past both ends: the curve does not start at its first control point.
            string periodic = AxisOf(@"
#30=IFCCARTESIANPOINT((0.,0.,0.));
#31=IFCCARTESIANPOINT((1.,2.,0.));
#32=IFCCARTESIANPOINT((2.,2.,1.));
#33=IFCCARTESIANPOINT((3.,0.,1.));
#35=IFCBSPLINECURVEWITHKNOTS(2,(#30,#31,#32,#33),.UNSPECIFIED.,.F.,.F.,(1,1,1,1,1,1,1),(0.,1.,2.,3.,4.,5.,6.),.UNIFORM_KNOTS.);
");

            IfcTestFile.Run(periodic, model =>
            {
                List<(GeometryHelperLogLevel Level, string Text)> lines = Capture(() => Assert.Empty(model.GetSplines(Guid, new IfcConvertOptions { Tolerance = InMetres })));

                Assert.Single(lines, l => l.Level == GeometryHelperLogLevel.Warn && l.Text.Contains("#35") && l.Text.Contains("not one GeometryHelper takes"));
                Assert.Single(lines, l => l.Level == GeometryHelperLogLevel.Info && l.Text.Contains("0 spline(s)"));
            });
        }

        [Fact]
        public void KnotsAndMultiplicitiesThatDoNotPair_AreLeftOutWithAWarning()
        {
            string unpaired = AxisOf(Cubic.Replace("(4,1,4),(0.,0.5,1.)", "(4,4),(0.,0.5,1.)"));

            IfcTestFile.Run(unpaired, model =>
            {
                List<(GeometryHelperLogLevel Level, string Text)> lines = Capture(() => Assert.Empty(model.GetSplines(Guid, new IfcConvertOptions { Tolerance = InMetres })));

                Assert.Single(lines, l => l.Level == GeometryHelperLogLevel.Warn && l.Text.Contains("3 knots and 2 multiplicities"));
            });
        }

        [Fact]
        public void AProductWithNoSpline_GivesNone_AndCountsItsItemsInOneDebugLine()
        {
            string ball = Product(@"
#20=IFCCARTESIANPOINT((1.,2.,3.));
#21=IFCAXIS2PLACEMENT3D(#20,$,$);
#22=IFCSPHERE(#21,0.5);
#26=IFCSHAPEREPRESENTATION(#11,'Body','CSG',(#22));
", "#26");

            IfcTestFile.Run(ball, model =>
            {
                List<(GeometryHelperLogLevel Level, string Text)> lines = Capture(() => Assert.Empty(model.GetSplines(Guid, new IfcConvertOptions { Tolerance = InMetres })));

                Assert.Single(lines, l => l.Level == GeometryHelperLogLevel.Info && l.Text.Contains("0 spline(s)"));
                Assert.Single(lines, l => l.Level == GeometryHelperLogLevel.Debug && l.Text.Contains("1 representation item(s) hold no B-spline curve"));
            });
        }

        [Fact]
        public void AGlobalIdTheFileDoesNotHold_GivesNone_AndSaysSo()
        {
            IfcTestFile.Run(AxisOf(Cubic), model =>
            {
                Assert.Empty(model.GetSplines(null));
                List<(GeometryHelperLogLevel Level, string Text)> lines = Capture(() => Assert.Empty(model.GetSplines("0000000000000000000009")));

                Assert.Contains("is not a product of the file", Assert.Single(lines).Text);
            });
        }

        [Fact]
        public void AProductTheNameFiltersLeaveOut_GivesNoSpline()
        {
            IfcTestFile.Run(AxisOf(Cubic), model =>
            {
                Assert.Empty(model.GetSplines(Guid, new IfcConvertOptions { Tolerance = InMetres }.AddSkipNames("Curve")));
                Assert.Single(model.GetSplines(Guid, new IfcConvertOptions { Tolerance = InMetres }.AddOnlyNames("Curve")));
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
