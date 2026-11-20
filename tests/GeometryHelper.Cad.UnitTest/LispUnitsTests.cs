using System;
using System.Globalization;
using GeometryHelper.Cad.Lisp;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// What AutoLISP's rtos, angtos, distof and angtof gave in AutoCAD 2021 (accoreconsole, en-US, blank drawing), each text
    /// as it came back: the rows named p1, p2 and p4 are the lines of the measurement files of the L1 interpreter, q1 and
    /// q2 are further lines run the same way for the places those files left open (the 16 digit limit, a turn that rounds
    /// up, DIMZIN in angles). The defaults a bare call takes are LUNITS 2, LUPREC 4, AUNITS 0, AUPREC 0 and DIMZIN 0.
    /// </summary>
    public class LispUnitsTests
    {
        [Theory]
        // AutoCAD 2021 (p1): (rtos 1.5) "1.5000"
        [InlineData(1.5, 2, 4, 0, "1.5000")]
        // AutoCAD 2021 (p1): (rtos 1.5 2 3) "1.500"
        [InlineData(1.5, 2, 3, 0, "1.500")]
        // AutoCAD 2021 (p1): (rtos 1234567.891 2 2) "1234567.89"
        [InlineData(1234567.891, 2, 2, 0, "1234567.89")]
        // AutoCAD 2021 (p1): (rtos 1e20 2 2) "1.00E+20"
        [InlineData(1E20, 2, 2, 0, "1.00E+20")]
        // AutoCAD 2021 (p1): (rtos 2.5 2 0) "3"
        [InlineData(2.5, 2, 0, 0, "3")]
        // AutoCAD 2021 (p1): (rtos 3.5 2 0) "4"
        [InlineData(3.5, 2, 0, 0, "4")]
        // AutoCAD 2021 (p1): (rtos -0.0004 2 3) "0.000"
        [InlineData(-0.0004, 2, 3, 0, "0.000")]
        // AutoCAD 2021 (p2): (rtos 1) "1.0000"
        [InlineData(1.0, 2, 4, 0, "1.0000")]
        // AutoCAD 2021 (p2): (rtos 1.23456789 2 8) "1.23456789"
        [InlineData(1.23456789, 2, 8, 0, "1.23456789")]
        // AutoCAD 2021 (p2): (rtos 1.23456789 2 9) "1.234567890"
        [InlineData(1.23456789, 2, 9, 0, "1.234567890")]
        // AutoCAD 2021 (p2): (rtos 0.5 2 0) "1"
        [InlineData(0.5, 2, 0, 0, "1")]
        // AutoCAD 2021 (p2): (rtos 1.5 2 0) "2"
        [InlineData(1.5, 2, 0, 0, "2")]
        // AutoCAD 2021 (p2): (rtos -1.5 2 0) "-2"
        [InlineData(-1.5, 2, 0, 0, "-2")]
        // AutoCAD 2021 (p2): (rtos -2.5 2 0) "-3"
        [InlineData(-2.5, 2, 0, 0, "-3")]
        // AutoCAD 2021 (p2): (rtos 1.005 2 2) "1.01"
        [InlineData(1.005, 2, 2, 0, "1.01")]
        // AutoCAD 2021 (p2): (rtos 1.015 2 2) "1.02"
        [InlineData(1.015, 2, 2, 0, "1.02")]
        // AutoCAD 2021 (p2): (rtos 12.5 2 9) "12.500000000"
        [InlineData(12.5, 2, 9, 0, "12.500000000")]
        // AutoCAD 2021 (p4): (rtos 0.0001 2 2) "0.00"
        [InlineData(0.0001, 2, 2, 0, "0.00")]
        // AutoCAD 2021 (p4): (rtos 123.456 2) "123.4560"
        [InlineData(123.456, 2, 4, 0, "123.4560")]
        public void ALengthInDecimalUnits_IsWrittenAsAutoCadWritesIt(
            double value,
            int mode,
            int precision,
            int dimzin,
            string expected
        )
        {
            Assert.Equal(expected, LispUnits.Rtos(value, mode, precision, dimzin));
        }

        [Theory]
        // AutoCAD 2021 (q1): (rtos 1e10 2 2) "10000000000.00"
        [InlineData(1E10, 2, 2, "10000000000.00")]
        // AutoCAD 2021 (q1): (rtos 1e14 2 2) "100000000000000.0"
        [InlineData(1E14, 2, 2, "100000000000000.0")]
        // AutoCAD 2021 (q1): (rtos 1e15 2 2) "1000000000000000"
        [InlineData(1E15, 2, 2, "1000000000000000")]
        // AutoCAD 2021 (q1): (rtos 1e16 2 2) "1.00E+16"
        [InlineData(1E16, 2, 2, "1.00E+16")]
        // AutoCAD 2021 (q1): (rtos 123456789012345678.0 2 2) "1.23E+17"
        [InlineData(123456789012345678.0, 2, 2, "1.23E+17")]
        // AutoCAD 2021 (q1): (rtos 1e19 2 2) "1.00E+19"
        [InlineData(1E19, 2, 2, "1.00E+19")]
        // AutoCAD 2021 (q1): (rtos 1e30 2 0) "1E+30"
        [InlineData(1E30, 2, 0, "1E+30")]
        // AutoCAD 2021 (q1): (rtos 1e15 2 0) "1000000000000000"
        [InlineData(1E15, 2, 0, "1000000000000000")]
        // AutoCAD 2021 (q1): (rtos 1e16 2 0) "1E+16"
        [InlineData(1E16, 2, 0, "1E+16")]
        // AutoCAD 2021 (q1): (rtos 99999999999999.0 2 2) "99999999999999.00"
        [InlineData(99999999999999.0, 2, 2, "99999999999999.00")]
        // AutoCAD 2021 (q2): (rtos (+ 1e14 0.5) 2 2) "100000000000000.5"
        [InlineData(100000000000000.5, 2, 2, "100000000000000.5")]
        // AutoCAD 2021 (q1): (rtos 1.5e-7 2 2) "0.00"
        [InlineData(1.5E-7, 2, 2, "0.00")]
        // AutoCAD 2021 (q1): (rtos 0.0 2 2) "0.00"
        [InlineData(0.0, 2, 2, "0.00")]
        // AutoCAD 2021 (q1): (rtos 1.5 2 16) "1.500000000000000"
        [InlineData(1.5, 2, 16, "1.500000000000000")]
        // AutoCAD 2021 (q1): (rtos 1.5 2 17) "1.500000000000000"
        [InlineData(1.5, 2, 17, "1.500000000000000")]
        // AutoCAD 2021 (q1): (rtos 1.5 2 20) "1.500000000000000"
        [InlineData(1.5, 2, 20, "1.500000000000000")]
        // AutoCAD 2021 (q1): (rtos 1.5 2 100) "1.500000000000000"
        [InlineData(1.5, 2, 100, "1.500000000000000")]
        // AutoCAD 2021 (q2): (rtos 0.5 2 16) "0.5000000000000000"
        [InlineData(0.5, 2, 16, "0.5000000000000000")]
        // AutoCAD 2021 (q2): (rtos (/ 2.0 3) 2 16) "0.6666666666666666"
        [InlineData(2.0 / 3.0, 2, 16, "0.6666666666666666")]
        // AutoCAD 2021 (q2): (rtos (/ 2.0 3) 2 15) "0.666666666666667"
        [InlineData(2.0 / 3.0, 2, 15, "0.666666666666667")]
        // AutoCAD 2021 (q2): (rtos 1e-5 2 20) "0.00001000000000000000"
        [InlineData(1E-5, 2, 20, "0.00001000000000000000")]
        // AutoCAD 2021 (q1): (rtos nan 2 2) "0.00"
        [InlineData(double.NaN, 2, 2, "0.00")]
        // AutoCAD 2021 (q1): (rtos (* 1e308 10) 2 2) "0.00E+99"
        [InlineData(double.PositiveInfinity, 2, 2, "0.00E+99")]
        // AutoCAD 2021 (q1): (rtos (- (* 1e308 10)) 2 2) "0.00E+99"
        [InlineData(double.NegativeInfinity, 2, 2, "0.00E+99")]
        public void ALengthWithMoreThanSixteenDigits_IsCutAtSixteenAndAHugeOneIsScientific(
            double value,
            int mode,
            int precision,
            string expected
        )
        {
            Assert.Equal(expected, LispUnits.Rtos(value, mode, precision, 0));
        }

        [Theory]
        // AutoCAD 2021 (q8): (rtos inf 3 2) "0.00E+99"
        [InlineData(double.PositiveInfinity, 3, 2, "0.00E+99")]
        // AutoCAD 2021 (q8): (rtos inf 4 2) "0.00E+99"
        [InlineData(double.PositiveInfinity, 4, 2, "0.00E+99")]
        // AutoCAD 2021 (q8): (rtos inf 5 2) "0.00E+99"
        [InlineData(double.PositiveInfinity, 5, 2, "0.00E+99")]
        // AutoCAD 2021 (q8): (rtos (- inf inf) 3 2) "-2147483648'"
        [InlineData(double.NaN, 3, 2, "-2147483648'")]
        // AutoCAD 2021 (q8): (rtos (- inf inf) 5 2) "-2147483648 -1/0"
        [InlineData(double.NaN, 5, 2, "-2147483648 -1/0")]
        // AutoCAD 2021 (q1): (rtos nan 2 2) "0.00", which q8 leaves as it was
        [InlineData(double.NaN, 2, 2, "0.00")]
        public void AnInfinityOrANaNInEngineeringOrFractionalUnits_IsWrittenAsAutoCadWritesIt(
            double value,
            int mode,
            int precision,
            string expected
        )
        {
            Assert.Equal(expected, LispUnits.Rtos(value, mode, precision, 0));
        }

        [Theory]
        // AutoCAD 2021 (p1): (rtos 12.3456 1 3) "1.235E+01"
        [InlineData(12.3456, 3, "1.235E+01")]
        // AutoCAD 2021 (p2): (rtos 0.0 1 3) "0.000E+00"
        [InlineData(0.0, 3, "0.000E+00")]
        // AutoCAD 2021 (p2): (rtos 1234.5 1 2) "1.23E+03"
        [InlineData(1234.5, 2, "1.23E+03")]
        // AutoCAD 2021 (q1): (rtos 1e-10 1 2) "1.00E-10"
        [InlineData(1E-10, 2, "1.00E-10")]
        // AutoCAD 2021 (q1): (rtos 1.5 1 0) "2E+00"
        [InlineData(1.5, 0, "2E+00")]
        // AutoCAD 2021 (q1): (rtos 1.5 1 16) "1.500000000000000E+00"
        [InlineData(1.5, 16, "1.500000000000000E+00")]
        // AutoCAD 2021 (q1): (rtos 1.5 1 20) "1.500000000000000E+00"
        [InlineData(1.5, 20, "1.500000000000000E+00")]
        // AutoCAD 2021 (q2): (rtos 1.5 1 14) "1.50000000000000E+00"
        [InlineData(1.5, 14, "1.50000000000000E+00")]
        // AutoCAD 2021 (q2): (rtos 1e14 1 20) "1.000000000000000E+14"
        [InlineData(1E14, 20, "1.000000000000000E+14")]
        public void ALengthInScientificUnits_IsAMantissaAndAnExponent(double value, int precision, string expected)
        {
            Assert.Equal(expected, LispUnits.Rtos(value, 1, precision, 0));
        }

        [Theory]
        // AutoCAD 2021 (p1): (rtos 12.5 3 2) "1'-0.50\""
        [InlineData(12.5, 2, 0, "1'-0.50\"")]
        // AutoCAD 2021 (p2): (rtos -12.5 3 2) "-1'-0.50\""
        [InlineData(-12.5, 2, 0, "-1'-0.50\"")]
        // AutoCAD 2021 (p2): (rtos 12.5 3 0) "1'-1\""
        [InlineData(12.5, 0, 0, "1'-1\"")]
        // AutoCAD 2021 (p4, DIMZIN 0): (rtos 12.0 3 2) "1'"
        [InlineData(12.0, 2, 0, "1'")]
        // AutoCAD 2021 (p4, DIMZIN 0): (rtos 0.0 3 2) "0.00\""
        [InlineData(0.0, 2, 0, "0.00\"")]
        // AutoCAD 2021 (p4, DIMZIN 1): (rtos 12.0 3 2) "1'-0.00\""
        [InlineData(12.0, 2, 1, "1'-0.00\"")]
        // AutoCAD 2021 (p4, DIMZIN 3): (rtos 12.0 3 2) "1'-0.00\""
        [InlineData(12.0, 2, 3, "1'-0.00\"")]
        // AutoCAD 2021 (q1): (rtos 11.999 3 2) "1'"
        [InlineData(11.999, 2, 0, "1'")]
        // AutoCAD 2021 (q2): (rtos 0.00873 3 2) "0.01\""
        [InlineData(0.00873, 2, 0, "0.01\"")]
        // AutoCAD 2021 (q2): (rtos -0.001 3 2) "0.00\""
        [InlineData(-0.001, 2, 0, "0.00\"")]
        // AutoCAD 2021 (q2): (rtos 123456789.123456 3 4) "10288065'-9.1235\""
        [InlineData(123456789.123456, 4, 0, "10288065'-9.1235\"")]
        // AutoCAD 2021 (q1): (rtos 0.5 3 20) "0.5000000000000000\""
        [InlineData(0.5, 20, 0, "0.5000000000000000\"")]
        // AutoCAD 2021 (q2, DIMZIN 8): (rtos 12.5 3 2) "1'-0.5\""
        [InlineData(12.5, 2, 8, "1'-0.5\"")]
        // AutoCAD 2021 (q1, DIMZIN 8): (rtos 12.5 3 0) "1'-1\""
        [InlineData(12.5, 0, 8, "1'-1\"")]
        // AutoCAD 2021 (q2, DIMZIN 8): (rtos 1.0 3 2) "1\""
        [InlineData(1.0, 2, 8, "1\"")]
        // AutoCAD 2021 (q2, DIMZIN 8): (rtos 0.5 3 2) "0.5\""
        [InlineData(0.5, 2, 8, "0.5\"")]
        // AutoCAD 2021 (q2, DIMZIN 4): (rtos 12.5 3 2) "1'-.50\""
        [InlineData(12.5, 2, 4, "1'-.50\"")]
        // AutoCAD 2021 (q2, DIMZIN 4): (rtos 0.5 3 2) ".50\""
        [InlineData(0.5, 2, 4, ".50\"")]
        // AutoCAD 2021 (q2, DIMZIN 12): (rtos 0.0 3 2) "0\""
        [InlineData(0.0, 2, 12, "0\"")]
        // AutoCAD 2021 (q1, DIMZIN 2): (rtos 12.0 3 2) "1'"
        [InlineData(12.0, 2, 2, "1'")]
        // AutoCAD 2021 (q1, DIMZIN 2): (rtos 0.0 3 2) "0'"
        [InlineData(0.0, 2, 2, "0'")]
        // AutoCAD 2021 (q1, DIMZIN 1): (rtos 0.0 3 2) "0'-0.00\""
        [InlineData(0.0, 2, 1, "0'-0.00\"")]
        public void ALengthInEngineeringUnits_IsFeetAndDecimalInches(
            double value,
            int precision,
            int dimzin,
            string expected
        )
        {
            Assert.Equal(expected, LispUnits.Rtos(value, 3, precision, dimzin));
        }

        [Theory]
        // AutoCAD 2021 (p1): (rtos 12.5 4 2) "1'-0 1/2\""
        [InlineData(12.5, 2, 0, "1'-0 1/2\"")]
        // AutoCAD 2021 (p2): (rtos 0.5 4 2) "1/2\""
        [InlineData(0.5, 2, 0, "1/2\"")]
        // AutoCAD 2021 (p2): (rtos 12.0 4 2) "1'"
        [InlineData(12.0, 2, 0, "1'")]
        // AutoCAD 2021 (p2): (rtos 12.75 4 4) "1'-0 3/4\""
        [InlineData(12.75, 4, 0, "1'-0 3/4\"")]
        // AutoCAD 2021 (p4, DIMZIN 12): (rtos 12.5 4 2) "1'-0 1/2\""
        [InlineData(12.5, 2, 12, "1'-0 1/2\"")]
        // AutoCAD 2021 (p4, DIMZIN 0): (rtos 1.0 4 2) "1\""
        [InlineData(1.0, 2, 0, "1\"")]
        // AutoCAD 2021 (p4, DIMZIN 0): (rtos 0.0 4 2) "0\""
        [InlineData(0.0, 2, 0, "0\"")]
        // AutoCAD 2021 (p4, DIMZIN 1): (rtos 1.0 4 2) "0'-1\""
        [InlineData(1.0, 2, 1, "0'-1\"")]
        // AutoCAD 2021 (p4, DIMZIN 3): (rtos 1.0 4 2) "1\""
        [InlineData(1.0, 2, 3, "1\"")]
        // AutoCAD 2021 (q1): (rtos 12.5 4 0) "1'-1\""
        [InlineData(12.5, 0, 0, "1'-1\"")]
        // AutoCAD 2021 (q1): (rtos 0.001 4 2) "0\""
        [InlineData(0.001, 2, 0, "0\"")]
        // AutoCAD 2021 (q1): (rtos 143.999 4 2) "12'"
        [InlineData(143.999, 2, 0, "12'")]
        // AutoCAD 2021 (q2): (rtos -143.9999 4 2) "-12'"
        [InlineData(-143.9999, 2, 0, "-12'")]
        // AutoCAD 2021 (q2): (rtos -0.001 4 2) "0\""
        [InlineData(-0.001, 2, 0, "0\"")]
        // AutoCAD 2021 (q2): (rtos (/ 1.0 3) 4 9) "171/512\""
        [InlineData(1.0 / 3.0, 9, 0, "171/512\"")]
        // AutoCAD 2021 (q2): (rtos (/ 1.0 3) 4 20) "349525/1048576\""
        [InlineData(1.0 / 3.0, 20, 0, "349525/1048576\"")]
        // AutoCAD 2021 (q2, DIMZIN 8): (rtos 12.5 4 4) "1'-0 1/2\""
        [InlineData(12.5, 4, 8, "1'-0 1/2\"")]
        // AutoCAD 2021 (q1, DIMZIN 2): (rtos 1.0 4 2) "0'-1\""
        [InlineData(1.0, 2, 2, "0'-1\"")]
        // AutoCAD 2021 (q1, DIMZIN 2): (rtos 0.0 4 2) "0'"
        [InlineData(0.0, 2, 2, "0'")]
        // AutoCAD 2021 (q1, DIMZIN 1): (rtos 0.0 4 2) "0'-0\""
        [InlineData(0.0, 2, 1, "0'-0\"")]
        // AutoCAD 2021 (q1, DIMZIN 1): (rtos 0.5 4 2) "0'-0 1/2\""
        [InlineData(0.5, 2, 1, "0'-0 1/2\"")]
        // AutoCAD 2021 (q1, DIMZIN 1): (rtos 12.5 4 2) "1'-0 1/2\""
        [InlineData(12.5, 2, 1, "1'-0 1/2\"")]
        public void ALengthInArchitecturalUnits_IsFeetAndInchesWithAFraction(
            double value,
            int precision,
            int dimzin,
            string expected
        )
        {
            Assert.Equal(expected, LispUnits.Rtos(value, 4, precision, dimzin));
        }

        [Theory]
        // AutoCAD 2021 (p1): (rtos 12.5 5 2) "12 1/2"
        [InlineData(12.5, 2, "12 1/2")]
        // AutoCAD 2021 (p2): (rtos 12.75 5 4) "12 3/4"
        [InlineData(12.75, 4, "12 3/4")]
        // AutoCAD 2021 (p2): (rtos 0.75 5 4) "3/4"
        [InlineData(0.75, 4, "3/4")]
        // AutoCAD 2021 (p2): (rtos -0.75 5 4) "-3/4"
        [InlineData(-0.75, 4, "-3/4")]
        // AutoCAD 2021 (q1): (rtos 0.5 5 0) "1"
        [InlineData(0.5, 0, "1")]
        // AutoCAD 2021 (q1): (rtos 1.5 5 0) "2"
        [InlineData(1.5, 0, "2")]
        // AutoCAD 2021 (q1): (rtos 12.5 5 9) "12 1/2"
        [InlineData(12.5, 9, "12 1/2")]
        // AutoCAD 2021 (q1): (rtos 12.5 5 20) "12 1/2"
        [InlineData(12.5, 20, "12 1/2")]
        // AutoCAD 2021 (q1): (rtos (/ 1.0 3) 5 8) "85/256"
        [InlineData(1.0 / 3.0, 8, "85/256")]
        // AutoCAD 2021 (q2): (rtos (/ 1.0 3) 5 9) "171/512"
        [InlineData(1.0 / 3.0, 9, "171/512")]
        // AutoCAD 2021 (q2): (rtos (/ 1.0 3) 5 10) "341/1024"
        [InlineData(1.0 / 3.0, 10, "341/1024")]
        // AutoCAD 2021 (q2): (rtos (/ 1.0 3) 5 12) "1365/4096"
        [InlineData(1.0 / 3.0, 12, "1365/4096")]
        // AutoCAD 2021 (q2): (rtos (/ 1.0 3) 5 16) "21845/65536"
        [InlineData(1.0 / 3.0, 16, "21845/65536")]
        // AutoCAD 2021 (q2): (rtos (/ 1.0 3) 5 20) "349525/1048576"
        [InlineData(1.0 / 3.0, 20, "349525/1048576")]
        // AutoCAD 2021 (q1): (rtos 0.9999 5 2) "1"
        [InlineData(0.9999, 2, "1")]
        // AutoCAD 2021 (q2): (rtos -0.001 5 2) "0"
        [InlineData(-0.001, 2, "0")]
        // AutoCAD 2021 (q1): (rtos 1e17 5 2) "1.00E+17"
        [InlineData(1E17, 2, "1.00E+17")]
        public void ALengthInFractionalUnits_IsAWholeNumberAndAFraction(double value, int precision, string expected)
        {
            Assert.Equal(expected, LispUnits.Rtos(value, 5, precision, 0));
        }

        [Theory]
        // AutoCAD 2021 (p4, DIMZIN 8): (rtos 1.5) "1.5"
        [InlineData(1.5, 2, 4, 8, "1.5")]
        // AutoCAD 2021 (p4, DIMZIN 8): (rtos 1.0 2 2) "1"
        [InlineData(1.0, 2, 2, 8, "1")]
        // AutoCAD 2021 (p4, DIMZIN 8): (rtos 0.5 2 2) "0.5"
        [InlineData(0.5, 2, 2, 8, "0.5")]
        // AutoCAD 2021 (p4, DIMZIN 4): (rtos 0.5 2 2) ".50"
        [InlineData(0.5, 2, 2, 4, ".50")]
        // AutoCAD 2021 (p4, DIMZIN 4): (rtos 1.5 2 2) "1.50"
        [InlineData(1.5, 2, 2, 4, "1.50")]
        // AutoCAD 2021 (p4, DIMZIN 12): (rtos 0.5 2 2) ".5"
        [InlineData(0.5, 2, 2, 12, ".5")]
        // AutoCAD 2021 (p4, DIMZIN 12): (rtos 0.0 2 2) "0"
        [InlineData(0.0, 2, 2, 12, "0")]
        // AutoCAD 2021 (q2, DIMZIN 8): (rtos 100.0 2 0) "100"
        [InlineData(100.0, 2, 0, 8, "100")]
        // AutoCAD 2021 (q2, DIMZIN 12): (rtos 100.0 2 2) "100"
        [InlineData(100.0, 2, 2, 12, "100")]
        // AutoCAD 2021 (q1, DIMZIN 4): (rtos 0.0 2 2) ".00"
        [InlineData(0.0, 2, 2, 4, ".00")]
        // AutoCAD 2021 (q2, DIMZIN 4): (rtos -0.5 2 2) "-.50"
        [InlineData(-0.5, 2, 2, 4, "-.50")]
        public void ZerosOfADecimalLength_AreDroppedAsDimzinSays(
            double value,
            int mode,
            int precision,
            int dimzin,
            string expected
        )
        {
            Assert.Equal(expected, LispUnits.Rtos(value, mode, precision, dimzin));
        }

        [Theory]
        // AutoCAD 2021 (q1, DIMZIN 8): (rtos 15.0 1 3) "1.500E+01"
        [InlineData(15.0, 1, 3, 8, "1.500E+01")]
        // AutoCAD 2021 (q1, DIMZIN 4): (rtos 0.5 1 2) "5.00E-01"
        [InlineData(0.5, 1, 2, 4, "5.00E-01")]
        // AutoCAD 2021 (q1, DIMZIN 8): (rtos 0.5 5 2) "1/2"
        [InlineData(0.5, 5, 2, 8, "1/2")]
        public void ScientificAndFractionalLengths_KeepTheirZerosUnderDimzin(
            double value,
            int mode,
            int precision,
            int dimzin,
            string expected
        )
        {
            Assert.Equal(expected, LispUnits.Rtos(value, mode, precision, dimzin));
        }

        [Fact]
        public void AModeOutsideOneToFive_IsRefusedWithTheMeasuredWording()
        {
            // AutoCAD 2021 (p2): (rtos 12.5 6 2) ; error: bad argument value: rtos mode: 6
            Assert.Equal(
                "rtos mode: 6",
                Assert.Throws<ArgumentOutOfRangeException>(() => LispUnits.Rtos(12.5, 6, 2, 0)).Message
            );
            // AutoCAD 2021 (q1): (rtos 1.5 0) ; error: bad argument value: rtos mode: 0
            Assert.Equal(
                "rtos mode: 0",
                Assert.Throws<ArgumentOutOfRangeException>(() => LispUnits.Rtos(1.5, 0, 2, 0)).Message
            );
            // AutoCAD 2021 (q1): (rtos 1.5 -1) ; error: bad argument value: rtos mode: -1
            Assert.Equal(
                "rtos mode: -1",
                Assert.Throws<ArgumentOutOfRangeException>(() => LispUnits.Rtos(1.5, -1, 2, 0)).Message
            );
        }

        [Fact]
        public void ANegativePrecision_IsRefusedWithTheMeasuredWording()
        {
            // AutoCAD 2021 (p2): (rtos 12.5 2 -1) ; error: bad argument value: rtos prec: -1
            Assert.Equal(
                "rtos prec: -1",
                Assert.Throws<ArgumentOutOfRangeException>(() => LispUnits.Rtos(12.5, 2, -1, 0)).Message
            );
            // AutoCAD 2021 (q1): (angtos 0.5 1 -1) ; error: bad argument value: angtos prec: -1
            Assert.Equal(
                "angtos prec: -1",
                Assert.Throws<ArgumentOutOfRangeException>(() => LispUnits.Angtos(0.5, 1, -1, 0)).Message
            );
        }

        [Theory]
        // AutoCAD 2021 (p1): (angtos 1.0) "57" (AUNITS 0, AUPREC 0)
        [InlineData(1.0, 0, 0, "57")]
        // AutoCAD 2021 (p1): (angtos pi 0 2) "180.00"
        [InlineData(Math.PI, 0, 2, "180.00")]
        // AutoCAD 2021 (p1): (angtos -1.0 0 2) "302.70"
        [InlineData(-1.0, 0, 2, "302.70")]
        // AutoCAD 2021 (p2): (angtos 0.5 0 4) "28.6479"
        [InlineData(0.5, 0, 4, "28.6479")]
        // AutoCAD 2021 (p2): (angtos 7.0 0 2) "41.07"
        [InlineData(7.0, 0, 2, "41.07")]
        // AutoCAD 2021 (p4): (angtos 1.0 0 4) "57.2958"
        [InlineData(1.0, 0, 4, "57.2958")]
        // AutoCAD 2021 (q1): (angtos 0.5 0 20) "28.64788975654116"
        [InlineData(0.5, 0, 20, "28.64788975654116")]
        // AutoCAD 2021 (q1): (angtos 0.5 0 9) "28.647889757"
        [InlineData(0.5, 0, 9, "28.647889757")]
        // AutoCAD 2021 (q1): (angtos -1e-17 0 2) "0.00"
        [InlineData(-1E-17, 0, 2, "0.00")]
        // AutoCAD 2021 (q1): (angtos (* 2 pi) 0 2) "0.00"
        [InlineData(2.0 * Math.PI, 0, 2, "0.00")]
        // AutoCAD 2021 (q1): (angtos 6.283185 0 2) "0.00"
        [InlineData(6.283185, 0, 2, "0.00")]
        // AutoCAD 2021 (q1): (angtos 6.2831 0 2) "0.00"
        [InlineData(6.2831, 0, 2, "0.00")]
        public void AnAngleInDecimalDegrees_IsWrittenFromNoughtUpToATurn(
            double radians,
            int mode,
            int precision,
            string expected
        )
        {
            Assert.Equal(expected, LispUnits.Angtos(radians, mode, precision, 0));
        }

        [Theory]
        // AutoCAD 2021 (p1): (angtos pi 1 2) "180d0'"
        [InlineData(Math.PI, 2, "180d0'")]
        // AutoCAD 2021 (p2): (angtos 0.5 1 4) "28d38'52\""
        [InlineData(0.5, 4, "28d38'52\"")]
        // AutoCAD 2021 (p2): (angtos 0.5 1 0) "29d"
        [InlineData(0.5, 0, "29d")]
        // AutoCAD 2021 (p2): (angtos 0.5 1 2) "28d39'"
        [InlineData(0.5, 2, "28d39'")]
        // AutoCAD 2021 (p2): (angtos 0.5 1 3) "28d38'52\""
        [InlineData(0.5, 3, "28d38'52\"")]
        // AutoCAD 2021 (p2): (angtos 0.5 1 8) "28d38'52.4031\""
        [InlineData(0.5, 8, "28d38'52.4031\"")]
        // AutoCAD 2021 (q1): (angtos 6.2831 1 2) "0d0'"
        [InlineData(6.2831, 2, "0d0'")]
        // AutoCAD 2021 (q2): (angtos 3.0 1 5) "171d53'14.4\""
        [InlineData(3.0, 5, "171d53'14.4\"")]
        // AutoCAD 2021 (q2): (angtos 3.0 1 6) "171d53'14.42\""
        [InlineData(3.0, 6, "171d53'14.42\"")]
        public void AnAngleInDegreesMinutesAndSeconds_CarriesFromSecondsToDegrees(
            double radians,
            int precision,
            string expected
        )
        {
            Assert.Equal(expected, LispUnits.Angtos(radians, 1, precision, 0));
        }

        [Theory]
        // AutoCAD 2021 (p1): (angtos pi 2 2) "200.00g"
        [InlineData(Math.PI, 2, 2, "200.00g")]
        // AutoCAD 2021 (p2): (angtos 0.5 2 3) "31.831g"
        [InlineData(0.5, 2, 3, "31.831g")]
        // AutoCAD 2021 (q1): (angtos 6.2831 2 2) "399.99g"
        [InlineData(6.2831, 2, 2, "399.99g")]
        // AutoCAD 2021 (q1): (angtos 3.0 2 0) "191g"
        [InlineData(3.0, 2, 0, "191g")]
        // AutoCAD 2021 (q2): (angtos 3.0 2 16) "190.9859317102744g"
        [InlineData(3.0, 2, 16, "190.9859317102744g")]
        public void AnAngleInGrads_IsWrittenWithAG(double radians, int mode, int precision, string expected)
        {
            Assert.Equal(expected, LispUnits.Angtos(radians, mode, precision, 0));
        }

        [Theory]
        // AutoCAD 2021 (p1): (angtos pi 3 2) "3.14r"
        [InlineData(Math.PI, 2, "3.14r")]
        // AutoCAD 2021 (p2): (angtos 0.5 3 3) "0.500r"
        [InlineData(0.5, 3, "0.500r")]
        // AutoCAD 2021 (q1): (angtos 6.2831 3 2) "6.28r"
        [InlineData(6.2831, 2, "6.28r")]
        // AutoCAD 2021 (q1): (angtos 3.0 3 0) "3r"
        [InlineData(3.0, 0, "3r")]
        // AutoCAD 2021 (q2): (angtos 3.0 3 16) "3.000000000000000r"
        [InlineData(3.0, 16, "3.000000000000000r")]
        // AutoCAD 2021 (q2): (angtos 3.0 3 20) "3.000000000000000r"
        [InlineData(3.0, 20, "3.000000000000000r")]
        public void AnAngleInRadians_IsWrittenWithAnR(double radians, int precision, string expected)
        {
            Assert.Equal(expected, LispUnits.Angtos(radians, 3, precision, 0));
        }

        [Theory]
        // AutoCAD 2021 (p1): (angtos pi 4 2) "W"
        [InlineData(Math.PI, 2, "W")]
        // AutoCAD 2021 (p2): (angtos 0.5 4 0) "N 61d E"
        [InlineData(0.5, 0, "N 61d E")]
        // AutoCAD 2021 (p2): (angtos 0.5 4 2) "N 61d21' E"
        [InlineData(0.5, 2, "N 61d21' E")]
        // AutoCAD 2021 (p2): (angtos 0.5 4 4) "N 61d21'8\" E"
        [InlineData(0.5, 4, "N 61d21'8\" E")]
        // AutoCAD 2021 (p2): (angtos 2.0 4 4) "N 24d35'30\" W"
        [InlineData(2.0, 4, "N 24d35'30\" W")]
        // AutoCAD 2021 (p2): (angtos 4.0 4 4) "S 40d49'1\" W"
        [InlineData(4.0, 4, "S 40d49'1\" W")]
        // AutoCAD 2021 (p2): (angtos 5.5 4 4) "S 45d7'36\" E"
        [InlineData(5.5, 4, "S 45d7'36\" E")]
        // AutoCAD 2021 (p2): (angtos (* 0.5 pi) 4 2) "N"
        [InlineData(0.5 * Math.PI, 2, "N")]
        // AutoCAD 2021 (q1): (angtos 0.0 4 2) "E"
        [InlineData(0.0, 2, "E")]
        // AutoCAD 2021 (q1): (angtos 1.5707 4 0) "N 0d E"
        [InlineData(1.5707, 0, "N 0d E")]
        // AutoCAD 2021 (q1): (angtos 0.5 4 8) "N 61d21'7.5969\" E"
        [InlineData(0.5, 8, "N 61d21'7.5969\" E")]
        public void AnAngleInSurveyorsUnits_IsABearingOrACardinalLetter(double radians, int precision, string expected)
        {
            Assert.Equal(expected, LispUnits.Angtos(radians, 4, precision, 0));
        }

        [Theory]
        // AutoCAD 2021 (q1, DIMZIN 8): (angtos 0.5 0 2) "28.65"
        [InlineData(0.5, 0, 2, 8, "28.65")]
        // AutoCAD 2021 (q1, DIMZIN 8): (angtos pi 0 2) "180"
        [InlineData(Math.PI, 0, 2, 8, "180")]
        // AutoCAD 2021 (q1, DIMZIN 8): (angtos pi 1 2) "180d0'"
        [InlineData(Math.PI, 1, 2, 8, "180d0'")]
        // AutoCAD 2021 (q1, DIMZIN 8): (angtos pi 2 2) "200g"
        [InlineData(Math.PI, 2, 2, 8, "200g")]
        // AutoCAD 2021 (q2, DIMZIN 8): (angtos 0.5 1 4) "28d38'52\""
        [InlineData(0.5, 1, 4, 8, "28d38'52\"")]
        // AutoCAD 2021 (q2, DIMZIN 8): (angtos 0.5 1 8) "28d38'52.4031\""
        [InlineData(0.5, 1, 8, 8, "28d38'52.4031\"")]
        // AutoCAD 2021 (q2, DIMZIN 8): (angtos 3.0 3 3) "3r"
        [InlineData(3.0, 3, 3, 8, "3r")]
        // AutoCAD 2021 (q2, DIMZIN 8): (angtos 0.5 4 2) "N 61d21' E"
        [InlineData(0.5, 4, 2, 8, "N 61d21' E")]
        // AutoCAD 2021 (q2, DIMZIN 4): (angtos 0.00873 0 2) ".50"
        [InlineData(0.00873, 0, 2, 4, ".50")]
        // AutoCAD 2021 (q2, DIMZIN 4): (angtos 0.0 0 2) ".00"
        [InlineData(0.0, 0, 2, 4, ".00")]
        // AutoCAD 2021 (q2, DIMZIN 12): (angtos 0.00873 0 2) ".5"
        [InlineData(0.00873, 0, 2, 12, ".5")]
        public void ZerosOfAnAngle_AreDroppedAsDimzinSays(
            double radians,
            int mode,
            int precision,
            int dimzin,
            string expected
        )
        {
            Assert.Equal(expected, LispUnits.Angtos(radians, mode, precision, dimzin));
        }

        [Fact]
        public void AnAngtosModeOutsideZeroToFour_IsRefusedWithTheMeasuredWording()
        {
            // AutoCAD 2021 (p2): (angtos 0.5 5 2) ; error: bad argument value: angtos mode: 5
            Assert.Equal(
                "angtos mode: 5",
                Assert.Throws<ArgumentOutOfRangeException>(() => LispUnits.Angtos(0.5, 5, 2, 0)).Message
            );
            // AutoCAD 2021 (q1): (angtos 0.5 -1 2) ; error: bad argument value: angtos mode: -1
            Assert.Equal(
                "angtos mode: -1",
                Assert.Throws<ArgumentOutOfRangeException>(() => LispUnits.Angtos(0.5, -1, 2, 0)).Message
            );
        }

        [Theory]
        // AutoCAD 2021 (p1): (distof "1.5") 1.5 (LUNITS 2); (p2): (distof "1.5" 2) 1.5
        [InlineData("1.5", 2, "1.5")]
        // AutoCAD 2021 (p1): (distof "1'6\"" 4) 18.0
        [InlineData("1'6\"", 4, "18.0")]
        // AutoCAD 2021 (p1): (distof "abc") nil
        [InlineData("abc", 2, "nil")]
        // AutoCAD 2021 (p2): (distof "1.5" 1) 1.5
        [InlineData("1.5", 1, "1.5")]
        // AutoCAD 2021 (p2): (distof "1.5E+01" 1) 15.0
        [InlineData("1.5E+01", 1, "15.0")]
        // AutoCAD 2021 (p2): (distof "1'6\"" 3) 18.0
        [InlineData("1'6\"", 3, "18.0")]
        // AutoCAD 2021 (p2): (distof "1'-6.5\"" 3) 18.5
        [InlineData("1'-6.5\"", 3, "18.5")]
        // AutoCAD 2021 (p2): (distof "1'-6 1/2\"" 4) 18.5
        [InlineData("1'-6 1/2\"", 4, "18.5")]
        // AutoCAD 2021 (p2): (distof "6 1/2" 5) 6.5
        [InlineData("6 1/2", 5, "6.5")]
        // AutoCAD 2021 (p2): (distof "6-1/2" 5) 6.5
        [InlineData("6-1/2", 5, "6.5")]
        // AutoCAD 2021 (p2): (distof "18\"" 2) nil
        [InlineData("18\"", 2, "nil")]
        // AutoCAD 2021 (p2): (distof "1'" 2) nil
        [InlineData("1'", 2, "nil")]
        // AutoCAD 2021 (p2): (distof " 1.5" 2) 1.5
        [InlineData(" 1.5", 2, "1.5")]
        // AutoCAD 2021 (p2): (distof "1.5 " 2) 1.5
        [InlineData("1.5 ", 2, "1.5")]
        // AutoCAD 2021 (p2): (distof "1/2" 2) 0.5
        [InlineData("1/2", 2, "0.5")]
        // AutoCAD 2021 (p2): (distof "" 2) nil
        [InlineData("", 2, "nil")]
        // AutoCAD 2021 (q1): (distof "1e3" 2) 1000.0
        [InlineData("1e3", 2, "1000.0")]
        // AutoCAD 2021 (q1): (distof "1 1/2" 2) 1.5
        [InlineData("1 1/2", 2, "1.5")]
        // AutoCAD 2021 (q1): (distof "18\"" 3) 18.0
        [InlineData("18\"", 3, "18.0")]
        // AutoCAD 2021 (q1): (distof "-1'6\"" 3) -18.0
        [InlineData("-1'6\"", 3, "-18.0")]
        // AutoCAD 2021 (q1): (distof "1'6\"" 5) nil
        [InlineData("1'6\"", 5, "nil")]
        // AutoCAD 2021 (q1): (distof "1.5.2" 2) nil
        [InlineData("1.5.2", 2, "nil")]
        // AutoCAD 2021 (q1): (distof "1,5" 2) nil
        [InlineData("1,5", 2, "nil")]
        // AutoCAD 2021 (q1): (distof "NaN" 2) nil
        [InlineData("NaN", 2, "nil")]
        // AutoCAD 2021 (q1): (distof "1e400" 2) nil
        [InlineData("1e400", 2, "nil")]
        // AutoCAD 2021 (q1): (distof "6 1/2" 3) 6.5
        [InlineData("6 1/2", 3, "6.5")]
        // AutoCAD 2021 (q1): (distof "1/0" 5) nil
        [InlineData("1/0", 5, "nil")]
        // AutoCAD 2021 (q1): (distof "-1/2" 2) -0.5
        [InlineData("-1/2", 2, "-0.5")]
        // AutoCAD 2021 (q1): (distof "+1.5" 2) 1.5
        [InlineData("+1.5", 2, "1.5")]
        // AutoCAD 2021 (q1): (distof ".5" 2) 0.5
        [InlineData(".5", 2, "0.5")]
        // AutoCAD 2021 (q1): (distof "5." 2) 5.0
        [InlineData("5.", 2, "5.0")]
        public void ALengthTyped_IsReadAsAutoCadReadsIt(string text, int mode, string printed)
        {
            AssertReads(printed, LispUnits.Distof(text, mode));
        }

        [Fact]
        public void ADistofModeOutsideOneToFive_IsRefusedWithTheMeasuredWording()
        {
            // AutoCAD 2021 (p2): (distof "1.5" 6) ; error: bad argument value: distof mode: 6
            Assert.Equal(
                "distof mode: 6",
                Assert.Throws<ArgumentOutOfRangeException>(() => LispUnits.Distof("1.5", 6)).Message
            );
            // AutoCAD 2021 (q1): (distof "1.5" 0) ; error: bad argument value: distof mode: 0
            Assert.Equal(
                "distof mode: 0",
                Assert.Throws<ArgumentOutOfRangeException>(() => LispUnits.Distof("1.5", 0)).Message
            );
        }

        [Theory]
        // AutoCAD 2021 (p1): (angtof "90") 1.5708 (AUNITS 0); (p2): (angtof "90" 0) 1.5708
        [InlineData("90", 0, "1.5708")]
        // AutoCAD 2021 (p1): (angtof "45d30'" 1) 0.794125
        [InlineData("45d30'", 1, "0.794125")]
        // AutoCAD 2021 (p1): (angtof "100" 2) 1.5708
        [InlineData("100", 2, "1.5708")]
        // AutoCAD 2021 (p2): (angtof "45d30'15\"" 1) 0.794198
        [InlineData("45d30'15\"", 1, "0.794198")]
        // AutoCAD 2021 (p2): (angtof "45d" 1) 0.785398
        [InlineData("45d", 1, "0.785398")]
        // AutoCAD 2021 (p2): (angtof "45" 1) 0.785398
        [InlineData("45", 1, "0.785398")]
        // AutoCAD 2021 (p2): (angtof "100g" 2) 1.5708
        [InlineData("100g", 2, "1.5708")]
        // AutoCAD 2021 (p2): (angtof "1.5r" 3) 1.5
        [InlineData("1.5r", 3, "1.5")]
        // AutoCAD 2021 (p2): (angtof "1.5" 3) 1.5
        [InlineData("1.5", 3, "1.5")]
        // AutoCAD 2021 (p2): (angtof "N45d0'E" 4) 0.785398
        [InlineData("N45d0'E", 4, "0.785398")]
        // AutoCAD 2021 (p2): (angtof "N 45d E" 4) 0.785398
        [InlineData("N 45d E", 4, "0.785398")]
        // AutoCAD 2021 (p2): (angtof "E" 4) 0.0
        [InlineData("E", 4, "0.0")]
        // AutoCAD 2021 (p2): (angtof "N" 4) 1.5708
        [InlineData("N", 4, "1.5708")]
        // AutoCAD 2021 (p2): (angtof "S45dW" 4) 3.92699
        [InlineData("S45dW", 4, "3.92699")]
        // AutoCAD 2021 (p2): (angtof "-90" 0) 4.71239
        [InlineData("-90", 0, "4.71239")]
        // AutoCAD 2021 (p2): (angtof "450" 0) 1.5708
        [InlineData("450", 0, "1.5708")]
        // AutoCAD 2021 (p2): (angtof "abc" 0) nil
        [InlineData("abc", 0, "nil")]
        // AutoCAD 2021 (q1): (angtof "1e2" 0) 1.74533
        [InlineData("1e2", 0, "1.74533")]
        // AutoCAD 2021 (q1): (angtof "45d30'15.5\"" 1) 0.7942
        [InlineData("45d30'15.5\"", 1, "0.7942")]
        // AutoCAD 2021 (q1): (angtof "N45E" 4) 0.785398
        [InlineData("N45E", 4, "0.785398")]
        // AutoCAD 2021 (q1): (angtof "n45dw" 4) 2.35619
        [InlineData("n45dw", 4, "2.35619")]
        // AutoCAD 2021 (q1): (angtof "45r" 0) 1.0177
        [InlineData("45r", 0, "1.0177")]
        // AutoCAD 2021 (q1): (angtof "N100dE" 4) nil
        [InlineData("N100dE", 4, "nil")]
        // AutoCAD 2021 (q1): (angtof "" 0) nil
        [InlineData("", 0, "nil")]
        // AutoCAD 2021 (q1): (angtof "45 " 0) 0.785398
        [InlineData("45 ", 0, "0.785398")]
        // AutoCAD 2021 (q1): (angtof "S" 4) 4.71239
        [InlineData("S", 4, "4.71239")]
        // AutoCAD 2021 (q1): (angtof "W" 4) 3.14159
        [InlineData("W", 4, "3.14159")]
        // AutoCAD 2021 (q1): (angtof "90" 4) 1.5708
        [InlineData("90", 4, "1.5708")]
        // AutoCAD 2021 (q1): (angtof "1e400" 0) nil
        [InlineData("1e400", 0, "nil")]
        // AutoCAD 2021 (q1): (angtof "-0" 0) 0.0
        [InlineData("-0", 0, "0.0")]
        // AutoCAD 2021 (q1): (angtof "720" 0) 0.0
        [InlineData("720", 0, "0.0")]
        // AutoCAD 2021 (q1): (angtof "-1e-20" 0) 0.0
        [InlineData("-1e-20", 0, "0.0")]
        public void AnAngleTyped_IsReadAsAutoCadReadsIt(string text, int mode, string printed)
        {
            AssertReads(printed, LispUnits.Angtof(text, mode));
        }

        [Fact]
        public void AnAngtofModeOutsideZeroToFour_IsRefusedWithTheMeasuredWording()
        {
            // AutoCAD 2021 (p2): (angtof "90" 5) ; error: bad argument value: angtof mode: 5
            Assert.Equal(
                "angtof mode: 5",
                Assert.Throws<ArgumentOutOfRangeException>(() => LispUnits.Angtof("90", 5)).Message
            );
            // AutoCAD 2021 (q1): (angtof "1.5" -1) ; error: bad argument value: angtof mode: -1
            Assert.Equal(
                "angtof mode: -1",
                Assert.Throws<ArgumentOutOfRangeException>(() => LispUnits.Angtof("1.5", -1)).Message
            );
        }

        [Fact]
        public void AnAngleOfNegativeZero_ReadsAsPositiveNought()
        {
            // AutoCAD 2021 (q1): (angtof "-0" 0) 0.0, which prints as 0.0 and not -0.0
            double? read = LispUnits.Angtof("-0", 0);

            Assert.NotNull(read);
            Assert.True(BitConverter.DoubleToInt64Bits(read.Value) >= 0L);
        }

        /// <summary>
        /// A number as AutoCAD printed it (six significant digits, so within half a unit of the sixth) or "nil" for none.
        /// </summary>
        private static void AssertReads(string printed, double? actual)
        {
            if (printed == "nil")
            {
                Assert.Null(actual);
                return;
            }

            Assert.NotNull(actual);
            double expected = double.Parse(printed, CultureInfo.InvariantCulture);
            Assert.Equal(expected, actual.Value, Math.Max(Math.Abs(expected), 1E-3) * 5.1E-6);
        }
    }
}
