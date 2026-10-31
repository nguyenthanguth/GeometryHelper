using System;
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
    /// What followed from AutoCAD 2021's answers to plugin run v14 of 31 October 2026: a spline of the flag 2048 whose seam
    /// unclamps off by millionths is not periodic (17t); a closed spline whose seam turns by a hundredth of a degree is given
    /// no flag 2 (17u); and periodic fit data of any degree fit AutoCAD's periodic cubic (17v). Each drawing is built in the
    /// form AutoCAD wrote, read as a file is.
    /// </summary>
    public class DxfAutoCadChecksD22Tests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9, 1E-9);

        private static GeoPoint3 P3(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static string G(int code, string value) => code.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "\n" + value + "\n";

        private static string G(int code, double value) => G(code, value.ToString("R", CultureInfo.InvariantCulture));

        private static string Flags(DxfSpline spline) => spline.Item.Groups.First(g => g.TypeCode == 70).Value.Trim();

        /// <summary>The drawing of the R2018 file of ezdxf holding a cubic F00 as AutoCAD 2021 saved one: its knots and control points, no fit points.</summary>
        private static DxfDatabase DrawingOf(int flags, double[] knots, GeoPoint3[] points)
        {
            var record = new StringBuilder();
            record.Append(G(0, "SPLINE")).Append(G(5, "F00")).Append(G(330, "17")).Append(G(100, "AcDbEntity")).Append(G(8, "0")).Append(G(100, "AcDbSpline"))
                .Append(G(210, "0.0")).Append(G(220, "0.0")).Append(G(230, "1.0")).Append(G(70, flags.ToString(CultureInfo.InvariantCulture))).Append(G(71, "3"))
                .Append(G(72, knots.Length.ToString(CultureInfo.InvariantCulture))).Append(G(73, points.Length.ToString(CultureInfo.InvariantCulture)))
                .Append(G(74, "0")).Append(G(42, "0.000000001")).Append(G(43, "0.0000000001"));

            foreach (double knot in knots)
            {
                record.Append(G(40, knot));
            }

            foreach (GeoPoint3 point in points)
            {
                record.Append(G(10, point.X)).Append(G(20, point.Y)).Append(G(30, point.Z));
            }

            string text = DxfFileEzdxfDrawings.R2018.Replace("\r\n", "\n").Replace("$HANDSEED\n  5\nA1\n", "$HANDSEED\n  5\nF01\n")
                .Replace("  0\nENDSEC\n  0\nSECTION\n  2\nOBJECTS\n", record + "  0\nENDSEC\n  0\nSECTION\n  2\nOBJECTS\n");
            return DxfDatabase.Parse(text);
        }

        // AutoCAD's closed cubic through (0, 0), (40, 0), (50, 30) and (10, 25), its knots the chords, as its v14-17v.dxf holds it.
        private static readonly GeoPoint3[] ClampedPoints =
        {
            P3(1.6E-15, 0, 0), P3(4.7622651293465061, -9.8751694805472585, 0), P3(44.579653578015218, -10.52612047361797, 0),
            P3(60.524970764674258, 41.580682103876377, 0), P3(8.5624146039839744, 34.009365277659363, 0), P3(-3.205697822100078, 6.6474268938914758, 0),
            P3(1.6E-15, 0, 0),
        };

        private static readonly double[] ClampedKnots = { 0.0, 0, 0, 0, 40, 71.6227766016838, 111.9340653431765, 138.859889378849, 138.859889378849, 138.859889378849, 138.859889378849 };

        /// <summary>
        /// The cubic of <see cref="ClampedPoints"/> of a group 70, a control point moved up as given, then the whole scaled about
        /// the origin and moved along x.
        /// </summary>
        private static DxfDatabase ClampedDrawing(int flags, int moved = -1, double by = 0.0, double scale = 1.0, double shift = 0.0)
        {
            GeoPoint3[] points = ClampedPoints.ToArray();

            if (moved >= 0)
            {
                points[moved] = P3(points[moved].X, points[moved].Y + by, 0);
            }

            return DrawingOf(flags, ClampedKnots.Select(k => k * scale).ToArray(), points.Select(p => P3(p.X * scale + shift, p.Y * scale, 0)).ToArray());
        }

        private static DxfSpline Clamped(int flags, int moved = -1, double by = 0.0, double scale = 1.0, double shift = 0.0)
            => ByHandle<DxfSpline>(ClampedDrawing(flags, moved, by, scale, shift), "F00");

        #region Point 17t: the seam of a spline of the flag 2048 off by millionths

        [Fact]
        public void ASplineOfTheFlag2048WhoseSeamIsOffByMillionths_IsNotPeriodic_AsAutoCadReadIt()
        {
            // Plugin v14, 17t: AutoCAD 2021's 2059, its control point 1 moved up by 1E-6, 1E-4, 1E-3 and 1E-2, its seam unclamped
            // off by 4.1E-6 to 4.1E-2: AutoCAD read none of the four as periodic, each closed, of 7 control points.
            foreach (double by in new[] { 1E-6, 1E-4, 1E-3, 1E-2 })
            {
                foreach (DxfDatabase db in new[] { ClampedDrawing(2059, 1, by), DxfDatabase.Parse(ClampedDrawing(2059, 1, by).ToText()) })
                {
                    DxfSpline spline = ByHandle<DxfSpline>(db, "F00");
                    Assert.False(spline.IsPeriodic, "moved by " + by.ToString("R", CultureInfo.InvariantCulture));
                    Assert.True(spline.Closed);
                    Assert.Equal(7, spline.NumControlPoints);
                }
            }
        }

        [Fact]
        public void ASplineOfTheFlag2048WhoseSeamIsOffByRounding_IsPeriodic_AtAnySizeAndAnyPlace()
        {
            // AutoCAD's own periodic splines unclamp within 1E-15 of their size: the 2059 is periodic as it is, with its control
            // point 1 moved by 1E-12, and scaled by 100 000 to six million or moved a million along x, where rounding alone
            // leaves its seam off by more than a ten-billionth, the moved point then 1E-7 off; scaled so, a move of a millionth
            // (0.1 at its size) is not, nor moved a thousand along x.
            Assert.True(Clamped(2059).IsPeriodic);
            Assert.True(Clamped(2059, 1, 1E-12).IsPeriodic);
            Assert.True(Clamped(2059, scale: 1E5).IsPeriodic);
            Assert.True(Clamped(2059, 1, 1E-12, 1E5).IsPeriodic);
            Assert.True(Clamped(2059, shift: 1E6).IsPeriodic);
            Assert.False(Clamped(2059, 1, 1E-6, 1E5).IsPeriodic);
            Assert.False(Clamped(2059, 1, 1E-6, shift: 1E3).IsPeriodic);
        }

        #endregion

        #region Point 17u: the flag 2 of a seam turned by a hundredth of a degree

        /// <summary>The cubic of 70 11 of AutoCAD, its control point 1 turned about the point 0 by an angle, scaled as given.</summary>
        private static (DxfSpline Spline, GeoPoint3[] Points) Turned(double degrees, double scale = 1.0)
        {
            DxfSpline spline = Clamped(11, scale: scale);
            GeoPoint3[] points = spline.NurbsData.GetControlPoints().ToArray();
            double a = degrees * Math.PI / 180.0;
            GeoVector3 arm = points[0].GetVectorTo(points[1]);
            points[1] = P3(points[0].X + arm.X * Math.Cos(a) - arm.Y * Math.Sin(a), points[0].Y + arm.X * Math.Sin(a) + arm.Y * Math.Cos(a), points[0].Z);
            return (spline, points);
        }

        [Fact]
        public void AClosedCubicWhoseSeamTurnsAHundredthOfADegree_GivenNurbsData_IsWritten9WithoutTheFlag2_AsAutoCadSavedIt()
        {
            // Plugin v14, 17u: AutoCAD 2021's closed cubic of 70 11, its control point 1 turned about the point 0 by 0.01, 0.5 and
            // 2 degrees, given its NURBS data (closed, not periodic), saved 70 9 each and read closed.
            foreach (double degrees in new[] { 0.01, 0.5, 2.0 })
            {
                foreach (double scale in new[] { 1.0, 1E5 })
                {
                    (DxfSpline spline, GeoPoint3[] points) = Turned(degrees, scale);
                    spline.SetNurbsData(3, false, true, false, points, spline.Knots, null);

                    Assert.Equal("9", Flags(spline));
                    Assert.True(spline.Closed);
                }
            }
        }

        [Fact]
        public void AClosedCubicWhoseSeamRunsOnInOneDirectionToRounding_GivenNurbsData_KeepsTheFlag2_AtAnySize()
        {
            // AutoCAD's 11 and 1067 leave their seam and come back to it in one direction to 4E-16 of a radian, and keep the flag
            // 2: so does the 11 given its data back not periodic, scaled by 100 000 or not.
            foreach (double scale in new[] { 1.0, 1E5 })
            {
                (DxfSpline spline, GeoPoint3[] points) = Turned(0.0, scale);
                spline.SetNurbsData(3, false, false, false, points, spline.Knots, null);

                Assert.Equal("11", Flags(spline));
            }
        }

        #endregion

        #region Point 17v: periodic fit data of a degree other than 3

        // The fit points the plugin gave AutoCAD's 1035 in 17v.
        private static readonly GeoPoint3[] Ring = { P3(0, 0, 0), P3(40, 0, 0), P3(50, 30, 0), P3(10, 25, 0) };

        [Fact]
        public void PeriodicFitDataOfDegree2ThroughFourPoints_GiveAutoCadsPeriodicCubic_70Of3115()
        {
            // Plugin v14, 17v: AutoCAD 2021 set its 1035 fit data through the four points, degree 2, periodic, no tangents (an even
            // degree through an even count of points), and wrote its periodic cubic: 70 3115, degree 3, 7 control points and 11
            // knots, those of its closed cubic through the same points to 1E-14, read again periodic, 5 points at its knots.
            DxfDatabase db = ClampedDrawing(1035);
            ByHandle<DxfSpline>(db, "F00").FitData = new DxfFitData(Ring, 2, 1E-10) { KnotParam = DxfKnotParameterizationEnum.Chord, Periodic = true };

            foreach (DxfSpline read in new[] { ByHandle<DxfSpline>(db, "F00"), ByHandle<DxfSpline>(DxfDatabase.Parse(db.ToText()), "F00") })
            {
                Assert.Equal("3115", Flags(read));
                Assert.Equal(3, read.Degree);
                Assert.True(read.IsPeriodic);
                Assert.True(read.Closed);
                Assert.Equal(7, read.NumControlPoints);

                for (int i = 0; i < 7; i++)
                {
                    Assert.True(ClampedPoints[i].IsEqualTo(read.GetControlPointAt(i), Tight), "control point " + i + ": " + read.GetControlPointAt(i));
                }

                Assert.Equal(11, read.Knots.Count);

                for (int i = 0; i < 11; i++)
                {
                    Assert.True(Math.Abs(ClampedKnots[i] - read.Knots[i]) <= 1E-9, "knot " + i + ": " + read.Knots[i]);
                }

                Assert.Equal(5, read.NumFitPoints);
            }
        }

        [Fact]
        public void PeriodicFitDataOfDegree1Or4Or5ThroughSixPoints_GiveThePeriodicCubic_AsDegree3Does()
        {
            // AutoCAD fitted its periodic cubic where it was asked a degree 2 (17v); asked any degree, the library fits the cubic,
            // as degree 3 fits it through the same six points.
            GeoPoint3[] ring = { P3(0, 0, 0), P3(40, 0, 0), P3(55, 12, 0), P3(50, 30, 0), P3(10, 25, 0), P3(-5, 10, 0) };
            DxfSpline cubic = Clamped(1035);
            cubic.FitData = new DxfFitData(ring, 3, 1E-10) { KnotParam = DxfKnotParameterizationEnum.Chord, Periodic = true };

            Assert.Equal("3115", Flags(cubic));
            Assert.Equal(3, cubic.Degree);

            foreach (int degree in new[] { 1, 2, 4, 5 })
            {
                DxfSpline spline = Clamped(1035);
                spline.FitData = new DxfFitData(ring, degree, 1E-10) { KnotParam = DxfKnotParameterizationEnum.Chord, Periodic = true };

                Assert.Equal("3115", Flags(spline));
                Assert.Equal(3, spline.Degree);
                Assert.True(spline.IsPeriodic);
                Assert.Equal(cubic.NumControlPoints, spline.NumControlPoints);

                for (int i = 0; i < cubic.NumControlPoints; i++)
                {
                    Assert.True(cubic.GetControlPointAt(i).IsEqualTo(spline.GetControlPointAt(i), Tight), "degree " + degree + ", control point " + i);
                }

                Assert.Equal(cubic.Knots, spline.Knots);
            }
        }

        #endregion
    }
}
