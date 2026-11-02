using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What AutoCAD 2021 answered in the D32b runs of 2 November 2026 (its own plugin in accoreconsole, side databases): it cuts
    /// a spline that is not periodic evenly where that gives more pieces than the halving, fits a periodic cubic through three
    /// points, fits by the square roots of the chords and one step each and writes those with no fit points, keeps the points
    /// at its knots as fit data through <c>ModifyPositionAndTangent</c> and <c>GetSplitCurves</c>, and gives the curve it reads
    /// a spline of fit points as in its control points, knots and degree. Each drawing is built in the form AutoCAD wrote, read
    /// as a file is.
    /// </summary>
    public class DxfSplineD32bTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9, 1E-9);

        private static GeoPoint3 P3(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoPoint2 P2(double x, double y) => new GeoPoint2(x, y);

        private static string G(int code, string value) => code.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "\n" + value + "\n";

        private static string G(int code, double value) => G(code, value.ToString("R", CultureInfo.InvariantCulture));

        private static int Flags(DxfSpline spline) => int.Parse(spline.Item.Groups.First(g => g.TypeCode == 70).Value.Trim(), CultureInfo.InvariantCulture);

        // The five points of D32 (F5), AutoCAD's cubic through them (its o4-F5) and its knots, the chords.
        private static readonly GeoPoint3[] Five = { P3(0, 0, 0), P3(40, 1, 0), P3(50, 30, 0), P3(10, 25, 0), P3(-20, 40, 0) };

        private static readonly double[] FiveKnots =
        {
            0, 0, 0, 0, 40.01249804748511, 70.68822134784105, 110.9995100893338, 144.54052975183063, 144.54052975183063, 144.54052975183063, 144.54052975183063,
        };

        private static readonly GeoPoint3[] FivePoints =
        {
            P3(0, 0, 0), P3(14.759780882511993, -4.468243234709654, 0), P3(40.83520003855784, -12.362080966018246, 0), P3(60.80794184444102, 45.83876486566954, 0),
            P3(8.063112077169013, 15.59079385166805, 0), P3(-11.235345706127593, 32.37653140928565, 0), P3(-20, 40, 0),
        };

        /// <summary>The drawing of the R2018 file of ezdxf holding a spline F00 of a group 70 and degree, knots, control points and fit points.</summary>
        private static DxfDatabase DrawingOf(int flags, double[] knots, GeoPoint3[] points, GeoPoint3[] fit, int degree = 3)
        {
            var record = new StringBuilder();
            record.Append(G(0, "SPLINE")).Append(G(5, "F00")).Append(G(330, "17")).Append(G(100, "AcDbEntity")).Append(G(8, "0")).Append(G(100, "AcDbSpline"))
                .Append(G(210, "0.0")).Append(G(220, "0.0")).Append(G(230, "1.0")).Append(G(70, flags.ToString(CultureInfo.InvariantCulture)))
                .Append(G(71, degree.ToString(CultureInfo.InvariantCulture))).Append(G(72, knots.Length.ToString(CultureInfo.InvariantCulture)))
                .Append(G(73, points.Length.ToString(CultureInfo.InvariantCulture))).Append(G(74, fit.Length.ToString(CultureInfo.InvariantCulture)))
                .Append(G(42, "0.0000000001")).Append(G(43, "0.0000000001"));

            if (fit.Length > 0)
            {
                record.Append(G(44, "0.0000000001"));
            }

            foreach (double knot in knots)
            {
                record.Append(G(40, knot));
            }

            foreach (GeoPoint3 point in points)
            {
                record.Append(G(10, point.X)).Append(G(20, point.Y)).Append(G(30, point.Z));
            }

            foreach (GeoPoint3 point in fit)
            {
                record.Append(G(11, point.X)).Append(G(21, point.Y)).Append(G(31, point.Z));
            }

            string text = DxfFileEzdxfDrawings.R2018.Replace("\r\n", "\n").Replace("$HANDSEED\n  5\nA1\n", "$HANDSEED\n  5\nF01\n")
                .Replace("  0\nENDSEC\n  0\nSECTION\n  2\nOBJECTS\n", record + "  0\nENDSEC\n  0\nSECTION\n  2\nOBJECTS\n");
            return DxfDatabase.Parse(text);
        }

        /// <summary>AutoCAD's cubic through the five points, as it saved it (70 1064).</summary>
        private static DxfSpline FiveSpline() => ByHandle<DxfSpline>(DrawingOf(1064, FiveKnots, FivePoints, Five), "F00");

        /// <summary>A spline given fit data through points, closed through the first again where asked, or periodic.</summary>
        private static DxfSpline Fitted(GeoPoint3[] points, bool closed, bool periodic = false, DxfKnotParameterizationEnum parameters = DxfKnotParameterizationEnum.Chord)
        {
            DxfSpline spline = FiveSpline();
            List<GeoPoint3> through = points.ToList();

            if (closed)
            {
                through.Add(points[0]);
            }

            spline.FitData = new DxfFitData(through, 3, 1E-10) { Periodic = periodic, KnotParam = parameters };
            return spline;
        }

        private static readonly GeoPoint3[] Ring3 = { P3(0, 0, 0), P3(30, 0, 0), P3(20, 15, 0) };

        private static readonly GeoPoint3[] Ring5 = { P3(0, 0, 0), P3(40, 0, 0), P3(40.5, 0.5, 0), P3(50, 30, 0), P3(10, 25, 0) };

        private static readonly GeoPoint3[] Ring7 = { P3(0, 0, 0), P3(20, -3, 0), P3(40, 0, 0), P3(55, 12, 0), P3(50, 30, 0), P3(10, 25, 0), P3(-5, 10, 0) };

        private static readonly GeoPoint3[] Ring8 = { P3(0, 0, 0), P3(20, -3, 0), P3(40, 0, 0), P3(55, 12, 0), P3(50, 30, 0), P3(30, 33, 0), P3(10, 25, 0), P3(-5, 10, 0) };

        private static DxfPolyline Lines(DxfSpline spline, int precision) => Assert.IsType<DxfPolyline>(spline.ToPolylineWithPrecision(precision, false, true));

        private static void Vertices(DxfPolyline polyline, params GeoPoint2[] wanted)
        {
            Assert.Equal(wanted.Length, polyline.NumberOfVertices);

            for (int i = 0; i < wanted.Length; i++)
            {
                Assert.True(wanted[i].IsEqualTo(polyline.GetPoint2dAt(i), Tight), "vertex " + i + ": " + polyline.GetPoint2dAt(i) + ", AutoCAD's " + wanted[i]);
            }
        }

        #region Polylines: cut evenly

        [Fact]
        public void AClosedCubicOfFitPoints_IsCutEvenly_WhereThatGivesMorePieces_AsAutoCadCutItsRings()
        {
            // D32 round D and D32b round A: AutoCAD's closed cubic through (0, 0), (40, 0), (40.5, 0.5), (50, 30) and (10, 25) at
            // 0 cut into fifths, the knots 40.71 and 71.70 in place and the last vertex not on the first.
            DxfPolyline ring5 = Lines(Fitted(Ring5, true), 0);
            Vertices(ring5, P2(0, 0), P2(40.5, 0.5), P2(49.32030104593162, 14.735734434580177), P2(50, 30), P2(10.707063152367983, 25.629081353684775));
            Assert.True(ring5.Closed);

            // Seven and eight points: 15 and 17 vertices at 2, 53 at 10.
            Assert.Equal(15, Lines(Fitted(Ring7, true), 2).NumberOfVertices);
            Assert.Equal(17, Lines(Fitted(Ring8, true), 2).NumberOfVertices);
            Assert.Equal(53, Lines(Fitted(Ring8, true), 10).NumberOfVertices);
        }

        [Fact]
        public void AClosedCubicOfControlPoints_IsCutEvenly_ItsKnotsNotPutInPlace_AsAutoCadCutIt()
        {
            // D32b round A, ring8-closed of its control points: 8 vertices at eighths of the range at 0.
            DxfNurbsData data = Fitted(Ring8, true).NurbsData;
            var spline = new DxfSpline(3, false, true, false, data.GetControlPoints(), data.GetKnots(), null, 0, 0);
            DxfPolyline eighths = Lines(spline, 0);
            Vertices(
                eighths,
                P2(0, 0), P2(18.854844590551497, -2.9444122517915967), P2(37.67934386318003, -0.7695639386861948), P2(53.99511982229721, 9.79250395863532),
                P2(51.498481352077015, 28.4810610687325), P2(33.35104158088938, 33.522399821119684), P2(15.404305931668059, 27.877024200918928), P2(-2.2225118719994463, 16.30813117823589));
            Assert.True(eighths.Closed);
        }

        [Fact]
        public void AnOpenQuinticOfOneSpan_IsCutInThirds_AtPrecisionNought_AsAutoCadCutIt()
        {
            // D32 round E and D32b round A, quintic-k50: 4 vertices at 0, 16.67, 33.33 and 50, where the halving gives 3.
            var quintic = new DxfSpline(5, false, false, false, new[] { P3(0, 0, 0), P3(10, 10, 0), P3(20, 10, 0), P3(30, 0, 0), P3(40, -10, 0), P3(50, 0, 0) }, new double[] { 0, 0, 0, 0, 0, 0, 50, 50, 50, 50, 50, 50 }, null, 0, 0);
            Vertices(Lines(quintic, 0), P2(0, 0), P2(16.666666666666668, 6.172839506172839), P2(33.333333333333336, -1.234567901234566), P2(50, 0));
            Assert.Equal(6, Lines(quintic, 1).NumberOfVertices);
            Assert.Equal(27, Lines(quintic, 10).NumberOfVertices);
        }

        #endregion

        #region Fitting: three points periodic, and other parameters

        [Fact]
        public void APeriodicFitThroughThreePoints_IsACubic_AsAutoCadFittedIt()
        {
            // D32 round D, ring3-periodic: a cubic of 3 control points to a period, read periodic, its polylines of 5, 9, 15, 17,
            // 31 and 54 vertices at 0, 1, 2, 3, 5 and 10.
            DxfSpline ring = Fitted(Ring3, false, periodic: true);
            Assert.Equal(3, ring.Degree);
            Assert.True(ring.IsPeriodic);
            Assert.Equal(1 | 2 | 8 | 32 | 1024 | 2048, Flags(ring));
            int[] precisions = { 0, 1, 2, 3, 5, 10 };
            int[] counts = { 5, 9, 15, 17, 31, 54 };

            for (int i = 0; i < precisions.Length; i++)
            {
                Assert.Equal(counts[i], Lines(ring, precisions[i]).NumberOfVertices);
            }

            Vertices(Lines(ring, 0), P2(0, 0), P2(19.004173172184, -5.755872137457971), P2(30, 0), P2(20, 15), P2(0, 0));
        }

        [Fact]
        public void FitDataOfUniformParameters_IsAutoCadsCubic_WrittenWithNoFitPoints()
        {
            // D32 p-uniform-F5 and D32b kp-uni-F5: the knots 0 to 4, the control points AutoCAD's, saved 70 1160 with 74 0; read
            // back, the points at its knots its fit points, Uniform, its derivatives at its ends its tangents.
            DxfSpline spline = Fitted(Five, false, parameters: DxfKnotParameterizationEnum.Uniform);
            GeoPoint3[] points =
            {
                P3(0, 0, 0), P3(14.761904761904761, -3.095238095238094, 0), P3(44.285714285714285, -9.285714285714283, 0), P3(62.85714285714284, 43.14285714285714, 0),
                P3(4.2857142857142865, 16.714285714285715, 0), P3(-11.904761904761903, 32.238095238095234, 0), P3(-20, 40, 0),
            };
            DxfNurbsData data = spline.NurbsData;
            Assert.Equal(new double[] { 0, 0, 0, 0, 1, 2, 3, 4, 4, 4, 4 }, data.GetKnots());

            for (int i = 0; i < points.Length; i++)
            {
                Assert.True(points[i].IsEqualTo(data.GetControlPoints()[i], Tight), "control point " + i + ": " + data.GetControlPoints()[i]);
            }

            Assert.Equal(1024 | 128 | 8, Flags(spline));
            Assert.DoesNotContain(spline.Item.Groups, g => g.TypeCode == 11 || g.TypeCode == 12 || g.TypeCode == 44);
            DxfFitData fit = spline.FitData;
            Assert.Equal(DxfKnotParameterizationEnum.Uniform, fit.KnotParam);
            Assert.Equal(5, fit.GetFitPoints().Count);
            Assert.True(P3(50, 30, 0).IsEqualTo(fit.GetFitPoints()[2], Tight));
            Assert.True(new GeoVector3(44.285714285714278, -9.2857142857142811, 0).IsEqualTo(spline.StartFitTangent, Tight));
            Assert.True(new GeoVector3(-24.2857142857143, 23.2857142857143, 0).IsEqualTo(spline.EndFitTangent, Tight));
        }

        [Fact]
        public void AutoCadsSplineOfSquareRootParameters_IsReadSo_ItsTangentsItsDerivatives()
        {
            // D32 p-sqrt-F5 as AutoCAD saved it (70 1096, no fit points), read back by AutoCAD in D32b: SqrtChord, 5 fit points,
            // StartFitTangent (7.007, -1.769).
            double[] knots = { 0, 0, 0, 0, 6.325543300577833, 11.864110200088566, 18.213227362457893, 24.00468828889924, 24.00468828889924, 24.00468828889924, 24.00468828889924 };
            GeoPoint3[] points =
            {
                P3(0, 0, 0), P3(14.774900946494867, -3.730784237945172, 0), P3(42.4865210706659, -10.728196670121807, 0), P3(61.672953459742324, 44.3733582334606, 0),
                P3(6.173503027138644, 16.185475748866704, 0), P3(-11.54681629005828, 32.308689125373846, 0), P3(-20, 40, 0),
            };
            DxfSpline read = ByHandle<DxfSpline>(DrawingOf(1096, knots, points, new GeoPoint3[0]), "F00");
            DxfFitData fit = read.FitData;
            Assert.Equal(DxfKnotParameterizationEnum.SqrtChord, fit.KnotParam);
            Assert.Equal(5, read.NumFitPoints);
            Assert.True(new GeoVector3(7.007256251875722, -1.7693899451787964, 0).IsEqualTo(read.StartFitTangent, Tight));

            // The library's own fit by the same parameters is AutoCAD's.
            DxfSpline made = Fitted(Five, false, parameters: DxfKnotParameterizationEnum.SqrtChord);
            Assert.Equal(1024 | 64 | 8, Flags(made));

            for (int i = 0; i < knots.Length; i++)
            {
                Assert.Equal(knots[i], made.Knots[i], 12);
            }

            for (int i = 0; i < points.Length; i++)
            {
                Assert.True(points[i].IsEqualTo(made.ControlPoints[i], Tight), "control point " + i + ": " + made.ControlPoints[i]);
            }
        }

        [Fact]
        public void AKnotPutIntoAUniformFit_IsSaved1288_AsAutoCadSavedIt()
        {
            // D32b insknot-uniform: 70 1288, the flag 128 of one step each taken off.
            DxfSpline spline = Fitted(Five, false, parameters: DxfKnotParameterizationEnum.Uniform);
            spline.InsertKnot(1.2);
            Assert.Equal(1024 | 256 | 8, Flags(spline));
            Assert.Equal(DxfKnotParameterizationEnum.CustomParameterization, spline.FitData.KnotParam);
        }

        #endregion

        #region Edits AutoCAD keeps fit data through

        [Fact]
        public void AFitSplineMovedByModifyPositionAndTangent_KeepsItsPointsAtItsKnots_AsAutoCadSavedIt1288()
        {
            // D32 B0-modpos and D32b modpos-open: 70 1288, 5 fit points, its own parameters, leaving along (1.11, -0.34).
            DxfSpline spline = FiveSpline();
            double middle = 0.5 * (spline.StartParam + spline.EndParam);
            spline.ModifyPositionAndTangent(middle, spline.GetPointAtParameter(middle) + new GeoVector3(1, 2, 0), spline.GetFirstDerivative(middle));
            Assert.Equal(1024 | 256 | 8, Flags(spline));
            Assert.Equal(5, spline.NumFitPoints);
            Assert.Equal(DxfKnotParameterizationEnum.CustomParameterization, spline.FitData.KnotParam);
            Assert.True(new GeoVector3(1.1066377958953515, -0.33501356721644332, 0).IsEqualTo(spline.StartFitTangent, Tight));
        }

        [Fact]
        public void TheSplitPiecesOfAFitSpline_HaveThePointsAtTheirKnotsAsFitPoints_AsAutoCadGaveThem()
        {
            // D32b split-open, cut at 43.36: pieces of 3 and 4 fit points, the second leaving along (0.724, 0.903).
            DxfSpline spline = FiveSpline();
            IReadOnlyList<DxfCurve> pieces = spline.GetSplitCurves(new[] { 43.362158925549188 });
            Assert.Equal(2, pieces.Count);
            var first = Assert.IsType<DxfSpline>(pieces[0]);
            var second = Assert.IsType<DxfSpline>(pieces[1]);
            Assert.Equal(3, first.NumFitPoints);
            Assert.Equal(4, second.NumFitPoints);
            Assert.Equal(DxfKnotParameterizationEnum.CustomParameterization, second.FitData.KnotParam);
            Assert.True(new GeoVector3(0.72441868794080821, 0.90338072791661372, 0).IsEqualTo(second.StartFitTangent, Tight));
            Assert.True(new GeoVector3(1.1066377958953515, -0.33501356721644332, 0).IsEqualTo(first.StartFitTangent, Tight));
        }

        #endregion

        #region Getters of a record read as its fit

        [Fact]
        public void ARecordWhoseControlPointIsMoved_GivesTheFitsControlPointsKnotsAndDegree_AsAutoCadRead()
        {
            // D32 c2: AutoCAD's cubic through five points with its control point 1 moved by 1 read as the cubic: its 7 points.
            GeoPoint3[] moved = FivePoints.ToArray();
            moved[1] = P3(moved[1].X, moved[1].Y + 1, 0);
            DxfSpline spline = ByHandle<DxfSpline>(DrawingOf(1064, FiveKnots, moved, Five), "F00");
            Assert.True(FivePoints[1].IsEqualTo(spline.ControlPoints[1], Tight));
            Assert.True(FivePoints[1].IsEqualTo(spline.GetControlPointAt(1), Tight));
            Assert.Equal(FiveKnots.Length, spline.Knots.Count);

            // Moved again, the point moves on the cubic AutoCAD holds, the others its.
            spline.SetControlPointAt(2, P3(45, -5, 0));
            Assert.True(FivePoints[1].IsEqualTo(spline.GetControlPointAt(1), Tight));
            Assert.True(P3(45, -5, 0).IsEqualTo(spline.GetControlPointAt(2), Tight));
        }

        [Fact]
        public void ARecordOfFitPointsAloneOfDegree2_IsOfDegree3And7ControlPoints_AsAutoCadRead()
        {
            // D32 c3-deg2: fit points alone, 71 2: AutoCAD's Degree 3, its NumControlPoints 7, FitData.Degree 3.
            DxfSpline spline = ByHandle<DxfSpline>(DrawingOf(1064, new double[0], new GeoPoint3[0], Five, 2), "F00");
            Assert.Equal(3, spline.Degree);
            Assert.Equal(7, spline.NumControlPoints);
            Assert.Equal(3, spline.FitData.Degree);
            Assert.True(FivePoints[3].IsEqualTo(spline.GetControlPointAt(3), Tight));
        }

        #endregion
    }
}
