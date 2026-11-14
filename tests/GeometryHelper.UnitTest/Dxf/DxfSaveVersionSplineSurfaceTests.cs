using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.UnitTest.Dwg;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The surfaces AutoCAD 2021 makes of a spline: an extrusion along a slanted direction (<c>sum_spl_sur</c>), a spline turned about
    /// an axis (<c>rot_spl_sur</c>), a profile swept along a spline (<c>sweep_sur</c>, with the law of the sweep or with none and a
    /// scale along the path), a spline extruded straight with its exact surface kept (<c>cyl_spl_sur</c> of <c>full</c>), and a
    /// surface of intersection of two surfaces holding a flag more in R2013 and later (<c>par_int_cur</c>). 65 bodies of 24 kinds
    /// (boxes, cylinders, tori, extrusions, revolves, sweeps, lofts, fillets, shells, surfaces) saved as R2018 and read by AutoCAD
    /// 2021 turned into every version equal its own text of R2010 line for line, and into R2000 to R2007 gave its volume or area to
    /// 1E-4. Here each record, in the shape AutoCAD writes it but with a degree of 1 and two knots, is put in the place of the first
    /// face's plane of a box and turned from the SAT of R2010 into the SAB of R2013 and back.
    /// </summary>
    public class DxfSaveVersionSplineSurfaceTests
    {
        #region Helpers

        private static List<string> Box()
        {
            DwgAnswerKey.Record entity = DwgAnswerKey
                .Parse(DwgAnswerKey.Key("acis_AC1024"))
                .First(r => r.Section == "ENTITIES" && r.Type == "3DSOLID");
            var lines = new List<string>();
            var line = new StringBuilder();
            bool inside = false;

            foreach (KeyValuePair<int, string> group in entity.Groups)
            {
                if (group.Key == 100)
                {
                    inside = group.Value == "AcDbModelerGeometry";
                }
                else if (inside && (group.Key == 3 || group.Key == 1))
                {
                    line.Append(group.Value);

                    if (group.Key == 1)
                    {
                        lines.Add(DxfUnknownEntity.Plain(line.ToString()));
                        line.Clear();
                    }
                }
            }

            return lines;
        }

        /// <summary>The first record of a plane of the box's SAT given the record, which is the plane's face's surface.</summary>
        private static List<string> BoxWith(string record)
        {
            List<string> lines = Box();
            int at = lines.FindIndex(l => l.StartsWith("plane-surface", StringComparison.Ordinal));
            lines[at] = record;
            return lines;
        }

        /// <summary>The words of some SAT, its line ends and spaces alike.</summary>
        private static string Words(IEnumerable<string> lines) =>
            string.Join(
                " ",
                lines.SelectMany(l => l.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            );

        private static string Words(string text) => Words(new[] { text });

        private static readonly AcisFormat R2004 = AcisFormat.For("AC1018");
        private static readonly AcisFormat R2007 = AcisFormat.For("AC1021");
        private static readonly AcisFormat R2010 = AcisFormat.For("AC1024");
        private static readonly AcisFormat R2013 = AcisFormat.For("AC1027");

        // A curve of intersection of the exact kind: a spline of degree 1 of two knots and two points, no surface, no parametric curve.
        private const string Curve =
            "intcurve forward { exact_int_cur 22600 full nubs 1 open 2 0 1 1 1 0 0 0 1 0 0 0 null_surface null_surface nullbs nullbs "
            + "I I 0 0 0 0 I I UNEXTENDED UNEXTENDED } I I";

        // The spline surface that closes the surfaces made of a curve: degree 1 both ways, 2 knots each, 4 points, and 6 empty lists.
        private const string ExactSurface =
            "full nubs 1 1 open open none none 2 2 0 1 1 1 0 1 1 1 0 0 0 1 0 0 0 1 0 1 1 0 0 0 0 0 0 0 0 F";

        /// <summary>Turns SAT into SAB and back: the same words where the SAT is of R2010 as AutoCAD writes it.</summary>
        private static string RoundTrip(List<string> lines)
        {
            byte[] sab = DwgSat.ToSab(lines, R2013, string.Empty);

            Assert.NotNull(sab);
            string back = DwgSat.ToSat(sab, R2010);
            Assert.NotNull(back);
            return back;
        }

        #endregion

        #region An oblique extrusion and a spline turned

        private const string Sum =
            "spline-surface $-1 -1 $-1 forward { sum_spl_sur 22600 "
            + Curve
            + " straight 0 0 0 0 0 1 I I 0 0 0 "
            + ExactSurface
            + " } I I I I #";

        [Fact]
        public void TheSurfaceOfAnObliqueExtrusion_GoesFromSatToSabAndBack_AsTheSameWords()
        {
            List<string> lines = BoxWith(Sum);
            string back = RoundTrip(lines);

            Assert.Equal(Words(lines), Words(back));
        }

        [Fact]
        public void TheSurfaceOfAnObliqueExtrusion_GoingFromR2010ToR2007_LosesItsFlagAndTheNumbersOfItsCurve_AsAutoCadWritesIt()
        {
            string r2007 = DwgSat.ToSat(BoxWith(Sum), R2007);

            Assert.NotNull(r2007);
            Assert.Contains("{ sum_spl_sur 22600 intcurve forward { exact_int_cur full nubs", Words(r2007));
            Assert.DoesNotContain("UNEXTENDED", r2007, StringComparison.Ordinal);
            Assert.Contains("0 0 0 0 0 0 0 } I I I I #", Words(r2007));
        }

        [Fact]
        public void TheSurfaceOfAnObliqueExtrusion_GoingToR2004_IsTheSumsurAutoCadWritesThere()
        {
            string r2004 = DwgSat.ToSat(BoxWith(Sum), R2004);

            Assert.NotNull(r2004);
            Assert.Contains("{ sumsur intcurve forward { exactcur full nubs", Words(r2004));
            Assert.DoesNotContain("22600", r2004, StringComparison.Ordinal);

            // And back up: the SAT of R2004 turned to the SAB gives the R2010 words again.
            Assert.Equal(Words(BoxWith(Sum)), Words(RoundTrip(r2004.Split('\n').Where(l => l.Length > 0).ToList())));
        }

        [Fact]
        public void ASplineTurnedAboutAnAxis_GoesFromSatToSabAndBack_WithItsRationalSurfaceNamingTheDirectionOfItsWeights()
        {
            const string nurbs =
                "full nurbs 1 1 v open open none none 2 2 0 1 1 1 0 1 1 1 0 0 0 1 1 0 0 1 0 1 0 1 1 1 0 1 0 0 0 0 0 0 0 F";
            string rot =
                "spline-surface $-1 -1 $-1 forward { rot_spl_sur 22600 "
                + Curve.Replace("intcurve forward", "intcurve reversed")
                + " 0 7 0 0 1 0 "
                + nurbs
                + " } I I I I #";
            List<string> lines = BoxWith(rot);

            Assert.Equal(Words(lines), Words(RoundTrip(lines)));

            string r2004 = DwgSat.ToSat(lines, R2004);

            Assert.NotNull(r2004);
            Assert.Contains("{ rotsur intcurve reversed { exactcur full nubs", Words(r2004));
        }

        #endregion

        #region A sweep along a spline

        private const string Rail =
            "spline-surface $-1 -1 $-1 forward { sweep_sur 22600 not_on 1 straight 3 2 0 -1 0 0 F 0 F 6 F 0 F 6 is_planar 3 2 0 0 1 0 0 0 0 "
            + "0 1 0 1 0 0 0 0 1 1 has_z intcurve forward { ref 1 } I I F 0 F 6 0 no_scale @13 MTRAIL(EDGE1) 1 @4 EDGE "
            + "intcurve forward { ref 2 } I I 0 6 full nubs 1 1 open open none none 2 2 0 1 1 1 0 1 1 1 0 0 0 1 0 0 0 1 0 1 1 0 "
            + "0 0 0 0 0 0 0 F } I I I I #";

        private const string NoRail =
            "spline-surface $-1 -1 $-1 forward { sweep_sur 22600 not_on 1 ellipse 0 0 0 0 0 1 3 0 0 1 I I F -1 F 1 is_planar 3 0 0 0 0 1 "
            + "0 0 0 0 1 0 1 0 0 0 0 1 2 has_z intcurve forward { ref 1 } I I F 0 F 6 0 no_scale no_rail "
            + "intcurve forward { ref 2 } I I F 0 F 6 2 4 0 0 6 6 0 6 not_periodic yes_x_scaling yes_y_scaling "
            + "full nubs 1 1 open open none none 2 2 0 1 1 1 0 1 1 1 0 0 0 1 0 0 0 1 0 1 1 0 0 0 0 0 0 0 0 F } I I I I #";

        [Fact]
        public void ASweepAlongASplineWithItsRail_GoesFromSatToSabAndBack_AsTheSameWords()
        {
            List<string> lines = BoxWith(Rail);

            Assert.Equal(Words(lines), Words(RoundTrip(lines)));
        }

        [Fact]
        public void ASweepAlongASplineWithNoRail_GoesFromSatToSabAndBack_AsTheSameWords()
        {
            List<string> lines = BoxWith(NoRail);

            Assert.Equal(Words(lines), Words(RoundTrip(lines)));
        }

        [Fact]
        public void TheFirstWholeNumberOfASweptProfile_IsOneInTheSatOfR2010_WhereTheSabHoldsMinusOne()
        {
            byte[] sab = DwgSat.ToSab(BoxWith(Rail), R2013, string.Empty);
            string sat = DwgSat.ToSat(sab, R2010);

            Assert.Contains("not_on 1 straight", Words(sat));
            Assert.DoesNotContain("not_on -1", Words(sat), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        public void ASweepAlongASpline_IsRefusedAsTheSatOfR2004AndR2007_WhichReadItAsASolidOfNoVolume(string version)
        {
            Assert.Null(DwgSat.ToSat(BoxWith(Rail), AcisFormat.For(version)));
            Assert.NotNull(DwgSat.ToSat(BoxWith(Rail), R2010));
        }

        #endregion

        #region A spline extruded, an intersection and an edge

        [Fact]
        public void ASplineExtrudedWithItsExactSurface_GoesFromSatToSabAndBack_AndToR2004AsACylsur()
        {
            string cyl =
                "spline-surface $-1 -1 $-1 forward { cyl_spl_sur 22600 "
                + Curve.Replace("} I I", "} F 0 F 5")
                + " 0 0 1 3 2 0 "
                + ExactSurface
                + " } I I I I #";
            List<string> lines = BoxWith(cyl);

            Assert.Equal(Words(lines), Words(RoundTrip(lines)));

            string r2004 = DwgSat.ToSat(lines, R2004);

            Assert.NotNull(r2004);
            Assert.Contains("{ cylsur intcurve forward { exactcur full nubs", Words(r2004));
        }

        [Fact]
        public void ACurveOfTwoSurfacesOfR2010_HoldsAFlagMoreInTheSabOfR2013_ThatTheTextLeavesOut()
        {
            string par =
                "intcurve-curve $-1 -1 $-1 forward { par_int_cur 22600 full nubs 1 open 2 0 1 1 1 0 0 0 1 0 0 0 "
                + "spline forward { ref 0 } I I I I null_surface nubs 1 open 2 0 1 1 1 0 0 1 0 nullbs I I 0 0 0 0 surf1 } I I #";
            List<string> lines = Box();
            lines.Insert(lines.FindIndex(l => l.StartsWith("plane-surface", StringComparison.Ordinal)), par);

            // The SAT has no such flag: the writer of the SAB gives it a nought (the tag 0x0B between the flag that names the surface, 0x0A,
            // and the closing brace, 0x10), the writer of the SAT leaves it out.
            byte[] sab = DwgSat.ToSab(lines, R2013, string.Empty);

            Assert.NotNull(sab);

            bool held = false;

            for (int i = 0; i + 2 < sab.Length; i++)
            {
                held |= sab[i] == 0x0A && sab[i + 1] == 0x0B && sab[i + 2] == 0x10;
            }

            Assert.True(held);
            Assert.Contains("0 surf1 } I I #", Words(DwgSat.ToSat(sab, R2010)));
        }

        [Fact]
        public void AnEdgeThatIsReversed_GoesFromSatToSabAndBack_AsAnEdgeThatIsForward()
        {
            List<string> lines = Box();
            int at = lines.FindIndex(l => l.StartsWith("edge ", StringComparison.Ordinal) && l.Contains(" forward"));

            Assert.True(at >= 0);
            lines[at] = lines[at].Replace(" forward", " reversed");

            // A filleted box has edges AutoCAD writes as reversed; the grammar knew only forward ones.
            Assert.Equal(Words(lines), Words(RoundTrip(lines)));
        }

        #endregion
    }
}
