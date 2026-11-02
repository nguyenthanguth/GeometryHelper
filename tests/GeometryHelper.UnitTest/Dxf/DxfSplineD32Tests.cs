using System;
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
    /// What AutoCAD 2021 answered in the D32 runs of 2 November 2026 (side databases in accoreconsole): it fits a spline that is
    /// not periodic through its fit points as one cubic whatever degree it is asked, its knots the lengths of the chords, its
    /// derivative at an end the tangent given there, of the length given, and where none is given its curve bent no more at
    /// that end; it reads a SPLINE of fit points by fitting it so anew; it carries a tangent through a move, turn or scale at
    /// its own length; and a control point moved or the fit data updated takes the fit data out. Each drawing is built in the
    /// form AutoCAD wrote, read as a file is.
    /// </summary>
    public class DxfSplineD32Tests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9, 1E-9);

        private static GeoPoint3 P3(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static string G(int code, string value) => code.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "\n" + value + "\n";

        private static string G(int code, double value) => G(code, value.ToString("R", CultureInfo.InvariantCulture));

        private static string Flags(DxfSpline spline) => spline.Item.Groups.First(g => g.TypeCode == 70).Value.Trim();

        private static bool Has(DxfSpline spline, int code) => spline.Item.Groups.Any(g => g.TypeCode == code);

        // The five points AutoCAD was given in D32 (F5): from (0, 0) by (40, 1), (50, 30) and (10, 25) to (-20, 40).
        private static readonly GeoPoint3[] Five = { P3(0, 0, 0), P3(40, 1, 0), P3(50, 30, 0), P3(10, 25, 0), P3(-20, 40, 0) };

        // Its chords from the first point: 40.01, 70.69, 111.00 and 144.54.
        private static readonly double[] FiveKnots =
        {
            0, 0, 0, 0, 40.01249804748511, 70.68822134784105, 110.9995100893338, 144.54052975183063, 144.54052975183063, 144.54052975183063, 144.54052975183063,
        };

        // AutoCAD's cubic through the five, no tangents (its o4-F5, and p-d1 to p-d5 asked degrees 1 to 5).
        private static readonly GeoPoint3[] FivePoints =
        {
            P3(0, 0, 0), P3(14.759780882511993, -4.468243234709654, 0), P3(40.83520003855784, -12.362080966018246, 0), P3(60.80794184444102, 45.83876486566954, 0),
            P3(8.063112077169013, 15.59079385166805, 0), P3(-11.235345706127593, 32.37653140928565, 0), P3(-20, 40, 0),
        };

        // AutoCAD's cubic through the five, leaving along (1, 0, 0) and arriving along (0, 1, 0) (its t-o4-F5-unit).
        private static readonly GeoPoint3[] FiveTangentPoints =
        {
            P3(0, 0, 0), P3(13.337499349161703, 0, 0), P3(41.68418177259183, -13.773072673476884, 0), P3(59.5923740391797, 45.91675094506153, 0),
            P3(13.075363474744153, 17.475214900144326, 0), P3(-20, 28.819660112501055, 0), P3(-20, 40, 0),
        };

        /// <summary>
        /// The drawing of the R2018 file of ezdxf holding a spline F00 as AutoCAD 2021 saved one: its group 70, knots and control
        /// points, fit points, tangents where given, and the fit tolerance 1E-10 where there are fit points.
        /// </summary>
        private static DxfDatabase DrawingOf(int flags, double[] knots, GeoPoint3[] points, GeoPoint3[] fit, GeoVector3? start = null, GeoVector3? end = null, int degree = 3)
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

            if (start.HasValue)
            {
                record.Append(G(12, start.Value.X)).Append(G(22, start.Value.Y)).Append(G(32, start.Value.Z));
            }

            if (end.HasValue)
            {
                record.Append(G(13, end.Value.X)).Append(G(23, end.Value.Y)).Append(G(33, end.Value.Z));
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

        /// <summary>AutoCAD's cubic through the five points, no tangents, as it saved it (70 1064), in a drawing.</summary>
        private static DxfDatabase FiveDrawing() => DrawingOf(1064, FiveKnots, FivePoints, Five);

        /// <summary>AutoCAD's cubic through the five points, leaving along x and arriving along y, as it saved it, in a drawing.</summary>
        private static DxfDatabase FiveTangentDrawing() => DrawingOf(1064, FiveKnots, FiveTangentPoints, Five, new GeoVector3(1, 0, 0), new GeoVector3(0, 1, 0));

        /// <summary>The spline F00 of a drawing written and read again.</summary>
        private static DxfSpline Again(DxfDatabase db) => ByHandle<DxfSpline>(DxfDatabase.Parse(db.ToText()), "F00");

        private static void SameCurve(IReadOnlyList<GeoPoint3> points, IReadOnlyList<double> knots, DxfSpline spline, double within = 1E-9)
        {
            DxfNurbsData data = spline.NurbsData;
            IReadOnlyList<GeoPoint3> got = data.GetControlPoints();
            Assert.Equal(3, data.Degree);
            Assert.Equal(points.Count, got.Count);
            var close = new Tolerance(within, within, within);

            for (int i = 0; i < points.Count; i++)
            {
                Assert.True(points[i].IsEqualTo(got[i], close), "control point " + i + ": " + got[i] + ", AutoCAD's " + points[i]);
            }

            Assert.Equal(knots.Count, data.GetKnots().Count);

            for (int i = 0; i < knots.Count; i++)
            {
                Assert.True(Math.Abs(knots[i] - data.GetKnots()[i]) <= within, "knot " + i + ": " + data.GetKnots()[i] + ", AutoCAD's " + knots[i]);
            }
        }

        private static DxfFitData Fit(GeoPoint3[] points, int degree, GeoVector3 start, GeoVector3 end) => new DxfFitData(points, degree, 1E-10)
        {
            StartTangent = start,
            EndTangent = end,
            TangentsExist = true,
            KnotParam = DxfKnotParameterizationEnum.Chord,
        };

        private static readonly GeoVector3 None = new GeoVector3(0, 0, 0);

        #region Fitting: AutoCAD's cubic

        [Fact]
        public void FitDataThroughFivePoints_IsAutoCadsCubic_ItsKnotsTheChords_ItsEndsBentNoMore()
        {
            // D32 o4-F5: through the five, no tangents, AutoCAD 2021 fitted a cubic of 7 control points, its knots the chords,
            // its curve bent no more at either end (no second derivative there).
            DxfDatabase db = FiveTangentDrawing();
            ByHandle<DxfSpline>(db, "F00").FitData = new DxfFitData(Five, 3, 1E-10) { KnotParam = DxfKnotParameterizationEnum.Chord };

            foreach (DxfSpline spline in new[] { ByHandle<DxfSpline>(db, "F00"), Again(db) })
            {
                SameCurve(FivePoints, FiveKnots, spline);
                Assert.Equal(3, spline.Degree);
                Assert.Equal(5, spline.NumFitPoints);
                Assert.False(Has(spline, 12));
                Assert.False(Has(spline, 13));
                Assert.Equal(0.0, spline.StartParam);
                Assert.Equal(144.54052975183063, spline.EndParam, 9);
            }
        }

        [Fact]
        public void FitDataOfAnyDegree_IsTheCubic_AsAutoCadFittedDegrees1To5()
        {
            // D32 p-d1 to p-d5 and o2-F5 to o6-F5: whatever degree AutoCAD 2021 was asked, it fitted the cubic, Degree 3.
            foreach (int degree in new[] { 1, 2, 4, 5 })
            {
                DxfDatabase db = FiveDrawing();
                ByHandle<DxfSpline>(db, "F00").FitData = new DxfFitData(Five, degree, 1E-10) { KnotParam = DxfKnotParameterizationEnum.Chord };
                DxfSpline spline = Again(db);

                SameCurve(FivePoints, FiveKnots, spline);
                Assert.Equal(3, spline.Degree);
                Assert.Equal(3, spline.FitData.Degree);
            }
        }

        [Fact]
        public void FitDataThroughThreeOrTwoPoints_IsACubicToo()
        {
            // D32 o4-F3: through (0, 0), (30, 40) and (60, 0) AutoCAD fitted the cubic of 5 control points (0, 0), (10, 20),
            // (30, 60), (50, 20) and (60, 0) over the knots 0 to 100; o4-F2: through (0, 0) and (100, 0) the cubic straight
            // line of 4 points a third apart, not a line of degree 1.
            DxfDatabase three = FiveDrawing();
            ByHandle<DxfSpline>(three, "F00").FitData = new DxfFitData(new[] { P3(0, 0, 0), P3(30, 40, 0), P3(60, 0, 0) }, 3, 1E-10);
            SameCurve(new[] { P3(0, 0, 0), P3(10, 20, 0), P3(30, 60, 0), P3(50, 20, 0), P3(60, 0, 0) }, new[] { 0.0, 0, 0, 0, 50, 100, 100, 100, 100 }, Again(three));

            DxfDatabase two = FiveDrawing();
            ByHandle<DxfSpline>(two, "F00").FitData = new DxfFitData(new[] { P3(0, 0, 0), P3(100, 0, 0) }, 3, 1E-10);
            SameCurve(new[] { P3(0, 0, 0), P3(100.0 / 3, 0, 0), P3(200.0 / 3, 0, 0), P3(100, 0, 0) }, new[] { 0.0, 0, 0, 0, 100, 100, 100, 100 }, Again(two));
        }

        [Fact]
        public void FitDataOfTangents_LeaveAndArriveAtTheirOwnLength()
        {
            // D32 t-o4-F5-unit: leaving along (1, 0, 0) and arriving along (0, 1, 0), AutoCAD's second control point is the first
            // moved by the first chord over 3; c5-tan5: AutoCAD read the tangents (5, 0, 0) and (0, 5, 0) as they were, five
            // times as fast, and kept them so.
            DxfDatabase unit = FiveDrawing();
            ByHandle<DxfSpline>(unit, "F00").FitData = Fit(Five, 3, new GeoVector3(1, 0, 0), new GeoVector3(0, 1, 0));
            SameCurve(FiveTangentPoints, FiveKnots, Again(unit));

            DxfDatabase fast = FiveDrawing();
            ByHandle<DxfSpline>(fast, "F00").FitData = Fit(Five, 3, new GeoVector3(5, 0, 0), new GeoVector3(0, 5, 0));
            DxfSpline spline = Again(fast);
            SameCurve(
                new[]
                {
                    P3(0, 0, 0), P3(66.68749674580852, 0, 0), P3(23.121400697891087, -11.966247160717582, 0), P3(65.63312321553855, 40.53607312227036, 0),
                    P3(11.492972380506574, 42.83481138054137, 0), P3(-20, -15.901699437494727, 0), P3(-20, 40, 0),
                },
                FiveKnots,
                spline);
            Assert.Equal(Bits(new GeoVector3(5, 0, 0)), Bits(spline.StartFitTangent));
            Assert.Equal(Bits(new GeoVector3(0, 5, 0)), Bits(spline.EndFitTangent));
        }

        [Fact]
        public void FitDataOfOneTangent_IsBentNoMoreAtTheOtherEnd()
        {
            // D32 c-mixed-start: leaving along (1, 0, 0), the end tangent nought, AutoCAD bent the curve no more at its end.
            DxfDatabase db = FiveDrawing();
            ByHandle<DxfSpline>(db, "F00").FitData = Fit(Five, 3, new GeoVector3(1, 0, 0), None);
            DxfSpline spline = Again(db);

            SameCurve(
                new[]
                {
                    P3(0, 0, 0), P3(13.337499349161703, 0, 0), P3(41.32962133250944, -13.915356143958348, 0), P3(60.64824550176321, 46.34046729662013, 0),
                    P3(8.098950630833277, 15.478203361531463, 0), P3(-11.224152630591426, 32.34136721508968, 0), P3(-20, 40, 0),
                },
                FiveKnots,
                spline);
            Assert.True(Has(spline, 12));
            Assert.False(Has(spline, 13));
        }

        [Fact]
        public void FitDataOfAPointTwice_HoldItOnce_AndOfPointsAMillionthApart_HoldBoth()
        {
            // D32 c-dup and c-near-dup: AutoCAD took (40, 1) given twice, or a trillionth apart, as one point, 4 fit points; a
            // millionth apart it kept both, 5.
            GeoPoint3[] twice = { P3(0, 0, 0), P3(40, 1, 0), P3(40, 1, 0), P3(50, 30, 0), P3(10, 25, 0) };
            GeoPoint3[] trillionth = { P3(0, 0, 0), P3(40, 1, 0), P3(40, 1 + 1E-12, 0), P3(50, 30, 0), P3(10, 25, 0) };
            GeoPoint3[] once =
            {
                P3(0, 0, 0), P3(14.87341691967347, -4.226917609997772, 0), P3(41.14959158312176, -11.694416571947478, 0), P3(59.76681331149228, 43.627749769620465, 0),
                P3(28.025112396359265, 31.74681099605644, 0), P3(10, 25, 0),
            };
            double[] onceKnots = { 0, 0, 0, 0, 40.01249804748511, 70.68822134784105, 110.9995100893338, 110.9995100893338, 110.9995100893338, 110.9995100893338 };

            foreach (GeoPoint3[] given in new[] { twice, trillionth })
            {
                DxfDatabase db = FiveDrawing();
                ByHandle<DxfSpline>(db, "F00").FitData = new DxfFitData(given, 3, 1E-10);
                DxfSpline spline = Again(db);

                SameCurve(once, onceKnots, spline);
                Assert.Equal(4, spline.NumFitPoints);
            }

            DxfDatabase apart = FiveDrawing();
            ByHandle<DxfSpline>(apart, "F00").FitData = new DxfFitData(new[] { P3(0, 0, 0), P3(40, 1, 0), P3(40, 1 + 1E-6, 0), P3(50, 30, 0), P3(10, 25, 0) }, 3, 1E-10);
            DxfSpline both = Again(apart);
            Assert.Equal(5, both.NumFitPoints);
            SameCurve(
                new[]
                {
                    P3(0, 0, 0), P3(19.999999887782767, -6.168749512892931, 0), P3(40.000000275409356, -12.337499179956428, 0), P3(39.99999978885646, 11.225241577490324, 0),
                    P3(64.30612279792956, 41.908363211287465, 0), P3(29.669211476952775, 31.124064002312117, 0), P3(10, 25, 0),
                },
                new[] { 0, 0, 0, 0, 40.01249804748511, 40.01249904748511, 70.68822140246806, 110.9995101439608, 110.9995101439608, 110.9995101439608, 110.9995101439608 },
                both,
                1E-6);
        }

        [Fact]
        public void ClosedFitDataOfNoTangents_IsBentNoMoreAtItsSeam_AsAutoCadFittedIt()
        {
            // Plugin v13, 17l (autocad-out/v13-17l-v8-17f-autocad-knot-halfway.dxf): AutoCAD 2021 set its closed spline the fit
            // points (0, 0), (30, 1), (30, 15), (20, 15) and (0, 0) again, no tangents, and saved 70 1065, its cubic bent no
            // more at the seam, a corner there; D32 o4-F5c the same through the five closed.
            DxfDatabase db = FiveDrawing();
            ByHandle<DxfSpline>(db, "F00").FitData = new DxfFitData(new[] { P3(0, 0, 0), P3(30, 1, 0), P3(30, 15, 0), P3(20, 15, 0), P3(0, 0, 0) }, 3, 1E-10);
            DxfSpline spline = Again(db);

            SameCurve(
                new[]
                {
                    P3(0, 0, 0), P3(12.47305066234907, -4.114916721363762, 0), P3(30.76362723888775, -10.14906197063903, 0), P3(33.63130891905931, 17.66114330539279, 0),
                    P3(13.17067628983927, 14.5654070421119, 0), P3(5.487781787433027, 6.068919600879954, 0), P3(0, 0, 0),
                },
                new[] { 0, 0, 0, 0, 30.01666203960727, 44.01666203960727, 54.01666203960727, 79.01666203960727, 79.01666203960727, 79.01666203960727, 79.01666203960727 },
                spline);
            Assert.True(spline.Closed);
            Assert.Equal(1 | 8 | 32 | 1024, int.Parse(Flags(spline), CultureInfo.InvariantCulture) & (1 | 2 | 8 | 32 | 1024));
        }

        [Fact]
        public void FitPointsPutInTakenOutAndMoved_FitTheCubicAnew_AsAutoCadDid()
        {
            // D32 BT-insfit, BT-remfit, BT-setfit and B0-setfit, on AutoCAD's own cubic through the five.
            DxfDatabase inserted = FiveTangentDrawing();
            ByHandle<DxfSpline>(inserted, "F00").InsertFitPointAt(2, P3(45, 15, 0));
            SameCurve(
                new[]
                {
                    P3(0, 0, 0), P3(13.337499349161703, 0, 0), P3(40.637414108423314, -10.242258628620252, 0), P3(43.94709835993091, 15.177545309439147, 0),
                    P3(58.08229906629643, 42.30497473687888, 0), P3(11.445680500823661, 17.337068347797853, 0), P3(-20, 28.819660112501047, 0), P3(-20, 40, 0),
                },
                new[] { 0, 0, 0, 0, 40.01249804748511, 54.87856679480362, 70.68995509564552, 111.00124383713826, 144.5422634996351, 144.5422634996351, 144.5422634996351, 144.5422634996351 },
                Again(inserted));

            DxfDatabase removed = FiveTangentDrawing();
            ByHandle<DxfSpline>(removed, "F00").RemoveFitPointAt(1);
            SameCurve(
                new[] { P3(0, 0, 0), P3(19.436506316151, 0, 0), P3(76.14485298038873, 43.5912510143222, 0), P3(12.239605097313948, 19.40889864562869, 0), P3(-20, 28.819660112501047, 0), P3(-20, 40, 0) },
                new[] { 0, 0, 0, 0, 58.309518948453004, 98.62080768994575, 132.1618273524426, 132.1618273524426, 132.1618273524426, 132.1618273524426 },
                Again(removed));

            double[] movedKnots = { 0, 0, 0, 0, 40.311288741492746, 67.23711277716527, 107.54840151865801, 141.08942118115485, 141.08942118115485, 141.08942118115485, 141.08942118115485 };
            DxfDatabase moved = FiveTangentDrawing();
            ByHandle<DxfSpline>(moved, "F00").SetFitPointAt(1, P3(40, 5, 0));
            SameCurve(
                new[]
                {
                    P3(0, 0, 0), P3(13.437096247164249, 0, 0), P3(39.8406150599534, -8.267207455135544, 0), P3(59.392500258588655, 44.87200192447677, 0),
                    P3(12.67233389151434, 17.48191535367948, 0), P3(-20, 28.819660112501055, 0), P3(-20, 40, 0),
                },
                movedKnots,
                Again(moved));

            DxfDatabase movedNone = FiveDrawing();
            ByHandle<DxfSpline>(movedNone, "F00").SetFitPointAt(1, P3(40, 5, 0));
            SameCurve(
                new[]
                {
                    P3(0, 0, 0), P3(14.671411687233048, -2.8315347616776334, 0), P3(39.142555950088976, -7.554386053390834, 0), P3(60.42178069267993, 44.942167151276955, 0),
                    P3(7.709603852740289, 15.546539396851468, 0), P3(-11.345753182270593, 32.36270988456079, 0), P3(-20, 40, 0),
                },
                movedKnots,
                Again(movedNone));
        }

        #endregion

        #region Reading: a SPLINE of fit points alone

        [Fact]
        public void ASplineOfFitPointsAlone_IsReadAsAutoCadFitsIt_OfAnyDegreeAndFlags()
        {
            // D32 c1-fitonly and c3-deg2: a SPLINE of the five fit points and the tangents along x and y, no control points, of
            // the flags 1064 or 8 and the degree 3 or 2, AutoCAD 2021 read as its cubic through them, over 0 to 144.54.
            foreach (int flags in new[] { 1064, 8 })
            {
                foreach (int degree in new[] { 3, 2 })
                {
                    DxfSpline spline = ByHandle<DxfSpline>(DrawingOf(flags, new double[0], new GeoPoint3[0], Five, new GeoVector3(1, 0, 0), new GeoVector3(0, 1, 0), degree), "F00");

                    SameCurve(FiveTangentPoints, FiveKnots, spline);
                    Assert.Equal(0.0, spline.StartParam);
                    Assert.Equal(144.54052975183063, spline.EndParam, 9);
                    Assert.True(spline.GetPointAtParameter(40.01249804748511).IsEqualTo(Five[1], Tight));
                }
            }

            // The reader passes the same cubic through them.
            DxfDatabase db = DrawingOf(1064, new double[0], new GeoPoint3[0], Five, new GeoVector3(1, 0, 0), new GeoVector3(0, 1, 0));
            var shape = Assert.IsAssignableFrom<GeoSpline3>(Assert.Single(ByHandle<DxfSpline>(db, "F00").GetGeometry()));
            Assert.Equal(3, shape.Degree);
            Assert.Equal(7, shape.ControlPoints.Count);
            Assert.True(shape.ControlPoints[1].IsEqualTo(FiveTangentPoints[1], Tight));
        }

        [Fact]
        public void TheReader_PassesAutoCadsCubicThroughFitPointsAlone_TwoOfThemAStraightCubic()
        {
            // As AutoCAD reads such a SPLINE: through (0, 0) and (100, 0), the cubic straight line, of degree 3 and 4 points.
            string text = "  0\nSECTION\n  2\nENTITIES\n" + G(0, "SPLINE") + G(8, "0") + G(70, "8") + G(71, "3") + G(72, "0") + G(73, "0") + G(74, "2")
                + G(11, "0") + G(21, "0") + G(31, "0") + G(11, "100") + G(21, "0") + G(31, "0") + "  0\nENDSEC\n  0\nEOF\n";
            DxfDocument read = DxfReader.Parse(text, new DxfReadOptions { PlanAs2D = true, Tolerance = Tolerance.Default });

            var spline = Assert.IsType<GeoSpline2>(Assert.Single(read.Shapes).Shape);
            Assert.Equal(3, spline.Degree);
            Assert.Equal(4, spline.ControlPoints.Count);
            Assert.Equal(100.0 / 3, spline.ControlPoints[1].X, 12);
            Assert.Equal(100.0, spline.Knots[spline.Knots.Count - 1]);
        }

        [Fact]
        public void ASplineOfFitPointsWhoseControlPointsAreAnotherCurve_IsReadAsTheFit_AsAutoCadReadThe17G()
        {
            // Plugin v9, 17g: the library's 17-g, closed cubics of 70 11 with control points of AutoCAD's periodic cubics and
            // their 5 and 6 fit points, AutoCAD read as the cubics it fits through the fit points: HALFWAY's control point 1
            // (12.40, -4.51), 44 vertices at 10 (vertex 1 (6.17, -2.19)); NEAR (0, 0), (40.5, 0.5), (49.32, 14.74), (50, 30) and
            // (10.71, 25.63) at 0, 42 vertices at 10.
            DxfSpline halfway = ByHandle<DxfSpline>(
                DrawingOf(
                    11,
                    new[] { 0.0, 0, 0, 0, 30, 45, 55, 80, 80, 80, 80 },
                    new[] { P3(0.0000000000000007, -0.0000000000000004, 0), P3(1.9696772796188, -6.041152263374485, 0), P3(33.61425167857916, -10.88065843621399, 0), P3(33.34423507327991, 17.56241426611796, 0), P3(14.65605371453325, 14.81893004115226, 0), P3(-1.641397733015665, 5.03429355281207, 0), P3(0.0000000000000007, -0.0000000000000004, 0) },
                    new[] { P3(0, 0, 0), P3(30, 0, 0), P3(30, 15, 0), P3(20, 15, 0), P3(0, 0, 0) }),
                "F00");

            SameCurve(
                new[]
                {
                    P3(0, 0, 0), P3(12.39793281653747, -4.5064599483204111, 0), P3(30.99483204134367, -11.266149870801028, 0), P3(33.941429801894913, 17.650301464254948, 0),
                    P3(13.235142118863047, 14.609819121447028, 0), P3(5.5146425495262683, 6.0874246339362612, 0), P3(0, 0, 0),
                },
                new[] { 0.0, 0, 0, 0, 30, 45, 55, 80, 80, 80, 80 },
                halfway);
            Assert.Equal(7, halfway.NumControlPoints);
            var ten = Assert.IsType<DxfPolyline>(halfway.ToPolylineWithPrecision(10, false, true));
            Assert.Equal(44, ten.NumberOfVertices);
            Assert.True(new GeoPoint2(6.1656617858168268, -2.1906402526557542).IsEqualTo(ten.GetPoint2dAt(1), Tight));
            Assert.True(new GeoPoint2(30, 0).IsEqualTo(ten.GetPoint2dAt(16), Tight));

            DxfSpline near = ByHandle<DxfSpline>(
                DrawingOf(
                    11,
                    new[] { 0.0, 0, 0, 0, 40, 40.70710678118654, 71.69904121581001, 112.0103299573027, 138.9361539929752, 138.9361539929752, 138.9361539929752, 138.9361539929752 },
                    new[] { P3(0.0000000000000004, 0.0000000000000022, 0), P3(5.581908864552045, -9.637933498405571, 0), P3(30.82057656148167, -9.244936922688699, 0), P3(47.57923142560183, 7.619549683548036, 0), P3(61.79166624560641, 41.95179922457442, 0), P3(8.605513742194279, 34.03076806443996, 0), P3(-3.757437396752223, 6.487732536139555, 0), P3(0.0000000000000004, 0.0000000000000022, 0) },
                    new[] { P3(0, 0, 0), P3(40, 0, 0), P3(40.5, 0.5, 0), P3(50, 30, 0), P3(10, 25, 0), P3(0, 0, 0) }),
                "F00");

            // NEAR at 10: AutoCAD's 42 vertices, each at its parameter. (At 0 AutoCAD gave 0, 40.71, 55.57, 71.70 and 111.15, of
            // no closing vertex, where the library gives 0, 40.71, 71.70, 112.01 and the start again: parked.)
            double[] parameters =
            {
                0, 8.6835096245609549, 13.025264436841434, 17.36701924912191, 21.708774061402387, 26.050528873682865, 30.392283685963339, 32.563161092103584,
                34.73403849824382, 36.904915904384062, 40, 40.707106781186539, 43.417548122804774, 47.759302935085245, 52.101057747365729, 56.442812559646207,
                60.784567371926684, 62.955444778066919, 65.126322184207169, 67.297199590347418, 69.468076996487639, 71.699041215810013, 73.809831808768109,
                75.980709214908359, 78.1515866210486, 80.322464027188829, 82.493341433329078, 84.6642188394693, 86.835096245609549, 91.176851057890019,
                95.518605870170489, 99.860360682450988, 104.20211549473147, 108.54387030701193, 110.71474771315218, 112.01032995730276, 115.05650252543265,
                117.2273799315729, 121.56913474385337, 125.91088955613384, 130.25264436841434, 138.93615399297528,
            };
            var near10 = Assert.IsType<DxfPolyline>(near.ToPolylineWithPrecision(10, false, true));
            Assert.Equal(parameters.Length, near10.NumberOfVertices);

            for (int i = 0; i < parameters.Length; i++)
            {
                GeoPoint3 at = near.GetPointAtParameter(parameters[i]);
                Assert.True(new GeoPoint2(at.X, at.Y).IsEqualTo(near10.GetPoint2dAt(i), Tight), "vertex " + i + ": " + near10.GetPoint2dAt(i));
            }

            Assert.True(new GeoPoint2(9.871870180065244, -2.8521746616571768).IsEqualTo(near10.GetPoint2dAt(1), Tight));
        }

        [Fact]
        public void AClosedSplineOfFitPointsNotPeriodic_HasItsKnotsInPlace_AsAutoCadMadeItsPolylines()
        {
            // D32 round D, ring4-closed and ring3-closed: AutoCAD's closed cubics not periodic (70 1065, a corner at the seam)
            // through (0, 0), (30, 0), (30, 15), (20, 15) and through (0, 0), (30, 0), (20, 15), each knot in the place of the
            // vertex of the halving nearest it, as on its periodic cubics: at 0 the knots 45 and 55 for the quarters 40 and 60.
            DxfDatabase four = FiveDrawing();
            DxfSpline ring = ByHandle<DxfSpline>(four, "F00");
            ring.FitData = new DxfFitData(new[] { P3(0, 0, 0), P3(30, 0, 0), P3(30, 15, 0), P3(20, 15, 0), P3(0, 0, 0) }, 3, 1E-10);
            Assert.Equal(1 | 8 | 32 | 1024, int.Parse(Flags(ring), CultureInfo.InvariantCulture));

            var nought = Assert.IsType<DxfPolyline>(ring.ToPolylineWithPrecision(0, false, true));
            GeoPoint2[] wanted = { new GeoPoint2(0, 0), new GeoPoint2(22.664369796152744, -5.0071777203560117), new GeoPoint2(30, 15), new GeoPoint2(20, 15), new GeoPoint2(0, 0) };
            Assert.Equal(wanted.Length, nought.NumberOfVertices);

            for (int i = 0; i < wanted.Length; i++)
            {
                Assert.True(wanted[i].IsEqualTo(nought.GetPoint2dAt(i), Tight), "vertex " + i + ": " + nought.GetPoint2dAt(i));
            }

            DxfDatabase three = FiveDrawing();
            DxfSpline triangle = ByHandle<DxfSpline>(three, "F00");
            triangle.FitData = new DxfFitData(new[] { P3(0, 0, 0), P3(30, 0, 0), P3(20, 15, 0), P3(0, 0, 0) }, 3, 1E-10);
            var one = Assert.IsType<DxfPolyline>(triangle.ToPolylineWithPrecision(1, false, true));
            GeoPoint2[] at1 =
            {
                new GeoPoint2(0, 0), new GeoPoint2(23.875106961491714, -4.2306899640060927), new GeoPoint2(30, 0), new GeoPoint2(28.331509384931575, 5.873592885118355),
                new GeoPoint2(20, 15), new GeoPoint2(14.485773542642477, 15.046299180086059), new GeoPoint2(0, 0),
            };
            Assert.Equal(at1.Length, one.NumberOfVertices);

            for (int i = 0; i < at1.Length; i++)
            {
                Assert.True(at1[i].IsEqualTo(one.GetPoint2dAt(i), Tight), "vertex " + i + ": " + one.GetPoint2dAt(i));
            }
        }

        #endregion

        #region Polylines: the precision

        private static DxfSpline Nurbs(int degree, double[] xy, double[] knots, double[] weights = null)
            => new DxfSpline(degree, weights != null, false, false, Enumerable.Range(0, xy.Length / 2).Select(i => P3(xy[2 * i], xy[2 * i + 1], 0)), knots, weights, 0, 0);

        private static void Counts(DxfSpline spline, int[] precisions, int[] counts)
        {
            for (int i = 0; i < precisions.Length; i++)
            {
                Assert.True(counts[i] == ((DxfPolyline)spline.ToPolylineWithPrecision(precisions[i], false, true)).NumberOfVertices, "at " + precisions[i] + ": " + ((DxfPolyline)spline.ToPolylineWithPrecision(precisions[i], false, true)).NumberOfVertices + ", AutoCAD's " + counts[i]);
            }
        }

        [Fact]
        public void APolylineOfAPrecision_IsWithinTheLargerOfTheBendAndATenThousandthOfTheLength_AsAutoCadCounted()
        {
            // D32 round E and plugin v6/v7 (17n): AutoCAD's counts of vertices made lines.
            int[] nine = { 0, 1, 2, 3, 5, 10, 20, 50, 99 };
            int[] quintic = { 86, 169, 240, 335, 478, 877, 1684, 4028, 7929 };
            double[] bezier5 = { 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1 };
            double[] bezier3 = { 0, 0, 0, 0, 1, 1, 1, 1 };

            // The quintic Bezier, and the same ten times its size: the same counts.
            Counts(Nurbs(5, new double[] { 0, 0, 10, 10, 20, 10, 30, 0, 40, -10, 50, 0 }, bezier5), nine, quintic);
            Counts(Nurbs(5, new double[] { 0, 0, 100, 100, 200, 100, 300, 0, 400, -100, 500, 0 }, bezier5), nine, quintic);

            // A hairpin 100 by 1: over the knots 0 to 1 its length decides, over 0 to 100 its bend.
            Counts(Nurbs(3, new double[] { 0, 0, 100, 0, 100, 1, 0, 1 }, bezier3), nine, new[] { 129, 257, 257, 513, 513, 1025, 2049, 4097, 8193 });
            Counts(Nurbs(3, new double[] { 0, 0, 100, 0, 100, 1, 0, 1 }, new double[] { 0, 0, 0, 0, 100, 100, 100, 100 }), nine, new[] { 5, 9, 9, 17, 17, 33, 65, 129, 257 });

            // An S of three spans whose middle lies on its chord: not one segment (and 3 719 at 20 where AutoCAD gives 3 717).
            Counts(
                Nurbs(3, new double[] { 0, 0, 10, 50, 20, -50, 30, 50, 40, -50, 50, 0 }, new double[] { 0, 0, 0, 0, 1, 2, 3, 3, 3, 3 }),
                new[] { 0, 1, 2, 3, 5, 10, 50, 99 },
                new[] { 173, 341, 535, 685, 1067, 1963, 8935, 17609 });

            // The rational cubic of the weights 1, 2, 2, 1 (v7 17n d), and the quintic of two spans (v6).
            Counts(Nurbs(3, new double[] { 0, 0, 10, 10, 20, 10, 30, 0 }, bezier3, new double[] { 1, 2, 2, 1 }), nine, new[] { 97, 191, 279, 381, 557, 1023, 1959, 4727, 9257 });
            Counts(Nurbs(5, new double[] { 0, -160, 10, -150, 20, -150, 30, -160, 40, -170, 50, -160, 60, -150 }, new double[] { 0, 0, 0, 0, 0, 0, 1, 2, 2, 2, 2, 2, 2 }), new[] { 0, 10, 99 }, new[] { 96, 1050, 9806 });
        }

        #endregion

        #region Edits AutoCAD writes otherwise

        [Fact]
        public void AFitSplineScaledOrTurned_KeepsTheLengthOfItsTangents_AsAutoCadCarriedThem()
        {
            // D32 c5-tan5: AutoCAD's cubic of the tangents (5, 0, 0) and (0, 5, 0), scaled by 2 about the origin, kept them
            // (5, 0, 0) and (0, 5, 0) and its knots, its control points twice as far; turned by 30 degrees, (4.33, 2.5, 0) and
            // (-2.5, 4.33, 0).
            GeoPoint3[] points =
            {
                P3(0, 0, 0), P3(66.68749674580852, 0, 0), P3(23.121400697891087, -11.966247160717582, 0), P3(65.63312321553855, 40.53607312227036, 0),
                P3(11.492972380506574, 42.83481138054137, 0), P3(-20, -15.901699437494727, 0), P3(-20, 40, 0),
            };

            DxfDatabase scaled = DrawingOf(1064, FiveKnots, points, Five, new GeoVector3(5, 0, 0), new GeoVector3(0, 5, 0));
            ByHandle<DxfSpline>(scaled, "F00").TransformBy(GeoTransform3.Scaling(2));
            DxfSpline twice = Again(scaled);
            Assert.True(twice.StartFitTangent.IsEqualTo(new GeoVector3(5, 0, 0), Tight));
            Assert.True(twice.EndFitTangent.IsEqualTo(new GeoVector3(0, 5, 0), Tight));
            Assert.True(twice.GetControlPointAt(1).IsEqualTo(P3(133.37499349161703, 0, 0), Tight));
            Assert.Equal(FiveKnots, twice.Knots.ToArray());

            DxfDatabase turned = DrawingOf(1064, FiveKnots, points, Five, new GeoVector3(5, 0, 0), new GeoVector3(0, 5, 0));
            ByHandle<DxfSpline>(turned, "F00").TransformBy(GeoTransform3.RotationAxis(P3(5, 5, 0), new GeoVector3(0, 0, 1), Math.PI / 6));
            DxfSpline round = Again(turned);
            Assert.True(round.StartFitTangent.IsEqualTo(new GeoVector3(4.3301270189221945, 2.4999999999999996, 0), Tight));
            Assert.True(round.EndFitTangent.IsEqualTo(new GeoVector3(-2.4999999999999996, 4.3301270189221945, 0), Tight));
        }

        [Fact]
        public void AControlPointOfAFitSplineMoved_TakesTheFitDataOut_AsAutoCadDid()
        {
            // D32 B0-setctl: AutoCAD moved the control point 2 of its cubic through the five and saved it 70 8, no fit points,
            // so that the point moved stays where it was put when the file is read again.
            DxfDatabase db = FiveDrawing();
            ByHandle<DxfSpline>(db, "F00").SetControlPointAt(2, P3(45, -5, 0));
            DxfSpline spline = Again(db);

            Assert.Equal("8", Flags(spline));
            Assert.Equal(0, spline.NumFitPoints);
            Assert.Equal(DxfSplineType.ControlPoints, spline.Type);
            Assert.Equal(Bits(P3(45, -5, 0)), Bits(spline.GetControlPointAt(2)));
        }

        [Fact]
        public void TheFitDataUpdated_IsTakenOut_AsAutoCad2021Did_OfAFitSplineAndOfOneOfControlPoints()
        {
            // D32 B0-updatefit and BC-updatefit: after UpdateFitData AutoCAD 2021 held no fit data on either, saved 70 8, the
            // control points kept.
            DxfDatabase db = FiveDrawing();
            ByHandle<DxfSpline>(db, "F00").UpdateFitData();
            DxfSpline spline = Again(db);

            Assert.Equal("8", Flags(spline));
            Assert.Equal(0, spline.NumFitPoints);
            SameCurve(FivePoints, FiveKnots, spline, 0.0);

            DxfDatabase control = DrawingOf(8, FiveKnots, FivePoints, new GeoPoint3[0]);
            ByHandle<DxfSpline>(control, "F00").UpdateFitData();
            Assert.Equal(0, Again(control).NumFitPoints);
        }

        [Fact]
        public void AKnotPutIntoAFitSpline_KeepsItsEightControlPoints_ItsFitPointsAtItsKnots_AsAutoCadSavedIt()
        {
            // D32 B0-insknot: AutoCAD put the knot 72.27 into its cubic through the five and saved 70 1288 (1024 and 256, the 32
            // taken off), 8 control points and no fit points, which it reads as the 6 points at its knots: the control points
            // stay 8 when the file is read again, where 5 fit points left in would fit 7 anew.
            DxfDatabase db = FiveDrawing();
            ByHandle<DxfSpline>(db, "F00").InsertKnot(72.27026487591532);
            DxfSpline spline = Again(db);

            Assert.Equal("1288", Flags(spline));
            Assert.Equal(8, spline.NumControlPoints);
            Assert.False(Has(spline, 11));
            Assert.Equal(6, spline.NumFitPoints);
            Assert.True(spline.GetControlPointAt(3).IsEqualTo(P3(53.83917942051862, 25.531694789146737, 0), Tight));
        }

        [Fact]
        public void AutoCadsPeriodicCubic_PurgedOrAPointMoved_Is2059_AndGivenAKnot_3339_AsAutoCadSavedIt()
        {
            // D32 round D, on AutoCAD's own 3115 (v8-17f-autocad-knot-halfway.dxf): PurgeFitData, UpdateFitData and a control
            // point moved saved 70 2059, the flags 1024 and 32 out; a knot put in 3339, the 32 out and 256 in.
            Func<DxfDatabase> drawing = () => DrawingOf(
                3115,
                new[] { 0.0, 0, 0, 0, 30, 45, 55, 80, 80, 80, 80 },
                new[] { P3(0.0000000000000007, -0.0000000000000004, 0), P3(1.9696772796188, -6.041152263374485, 0), P3(33.61425167857916, -10.88065843621399, 0), P3(33.34423507327991, 17.56241426611796, 0), P3(14.65605371453325, 14.81893004115226, 0), P3(-1.641397733015665, 5.03429355281207, 0), P3(0.0000000000000007, -0.0000000000000004, 0) },
                new GeoPoint3[0]);
            var edits = new Dictionary<string, Action<DxfSpline>>
            {
                ["2059 purge"] = s => s.PurgeFitData(),
                ["2059 update"] = s => s.UpdateFitData(),
                ["2059 move"] = s => s.SetControlPointAt(2, P3(33.61425167857916, -9.88065843621399, 0)),
                ["3339 knot"] = s => s.InsertKnot(40),
            };

            foreach (KeyValuePair<string, Action<DxfSpline>> edit in edits)
            {
                DxfDatabase db = drawing();
                edit.Value(ByHandle<DxfSpline>(db, "F00"));
                Assert.Equal(edit.Key.Substring(0, 4), Flags(Again(db)));
            }
        }

        [Fact]
        public void TheFitDataPurged_TakesTheFlag32OutToo_AsAutoCadSavedIt()
        {
            // D32 B0-purge: AutoCAD saved its cubic through the five 70 8 after PurgeFitData, the flags 32 and 1024 both out.
            DxfDatabase db = FiveDrawing();
            ByHandle<DxfSpline>(db, "F00").PurgeFitData();

            Assert.Equal("8", Flags(Again(db)));
        }

        #endregion
    }
}
