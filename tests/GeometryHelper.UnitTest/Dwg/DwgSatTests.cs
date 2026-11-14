using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Enums;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The ACIS data of solids and regions of DWGs of R2004 to R2010, which AutoCAD keeps as SAB: read into the SAT AutoCAD
    /// writes in the DXF of those versions, line for line; and the SAT of R2000, which the DWG keeps as text.
    /// </summary>
    /// <remarks>
    /// acis_AC10xx was made by probe GhW4b in AutoCAD 2021: a box 30 by 20 by 10, a sphere of radius 8, a cylinder of radius
    /// 5 and a region of a circle of radius 7. splines_AC10xx by probe GhSat: a closed spline through (0, 30), (5, 36),
    /// (10, 31) and (4, 25) extruded 7 up, and a solid lofted through a circle of radius 5, an ellipse 14 by 7 and a circle
    /// of radius 3, whose SAT holds splines over many lines and a line of 493 characters.
    /// </remarks>
    public class DwgSatTests : IDisposable
    {
        private readonly List<(GeometryHelperLogLevel Level, string Message)> _written =
            new List<(GeometryHelperLogLevel Level, string Message)>();

        public DwgSatTests()
        {
            GeometryHelperLog.Enable = true;
            GeometryHelperLog.Writer = (level, message, exception) => _written.Add((level, message));
        }

        public void Dispose()
        {
            GeometryHelperLog.Writer = null;
            GeometryHelperLog.Enable = true;
        }

        [Theory]
        [InlineData("acis_AC1018", 4)]
        [InlineData("acis_AC1024", 4)]
        [InlineData("splines_AC1015", 2)]
        [InlineData("splines_AC1018", 2)]
        [InlineData("splines_AC1021", 2)]
        [InlineData("splines_AC1024", 2)]
        public void TheSolidsOfR2000ToR2010_AreRead_IntoTheSatAutoCadWritesOfThem(string drawing, int solids)
        {
            byte[] dxf = DwgReader.ToDxf(DwgAnswerKey.Dwg(drawing));
            DwgAnswerKey.Result result = DwgAnswerKey.Compare(DwgAnswerKey.Key(drawing), dxf);

            Assert.Empty(result.Differences);
            Assert.False(result.Missing.ContainsKey("3DSOLID"));
            Assert.False(result.Missing.ContainsKey("REGION"));
            Assert.Equal(solids, DwgAnswerKey.Parse(dxf).Count(r => r.Type == "3DSOLID" || r.Type == "REGION"));
            Assert.DoesNotContain(_written, line => line.Level == GeometryHelperLogLevel.Warn);
        }

        [Fact]
        public void ALineOfSatPast255Characters_IsWrittenInPiecesOfGroup3_EndedByAGroup1()
        {
            // The loft's line of 22 discontinuities is 493 characters: a piece of 255 under 3, the last 238 under 1.
            DwgAnswerKey.Record loft = DwgAnswerKey
                .Parse(DwgReader.ToDxf(DwgAnswerKey.Dwg("splines_AC1018")))
                .Single(r => r.Type == "3DSOLID" && r.Groups.Any(g => g.Key == 3));
            string codes = string.Concat(
                loft.Groups.Select(g =>
                    g.Key == 3 ? "3"
                    : g.Key == 1 ? "1"
                    : "."
                )
            );

            Assert.Contains("31", codes);
            Assert.DoesNotContain("3.", codes);
            Assert.All(loft.Groups.Where(g => g.Key == 3), g => Assert.Equal(255, g.Value.Length));
        }

        [Theory]
        [InlineData(25.4, "25.39999999999999858")]
        [InlineData(1E-06, "9.999999999999999547e-07")]
        [InlineData(1E-10, "1.000000000000000036e-10")]
        [InlineData(30.000000000000007, "30.00000000000000711")]
        [InlineData(-2.5673907444456745E-16, "-2.5673907444456745e-16")]
        [InlineData(123456.789, "123456.7890000000043")]
        [InlineData(0.0001, "0.0001000000000000000048")]
        [InlineData(1.234E-05, "1.234000000000000044e-05")]
        [InlineData(1234567890123456789.0, "1234567890123456768")]
        [InlineData(1E+19, "1e+19")]
        [InlineData(1E+20, "1e+20")]
        [InlineData(4.94065645841247E-324, "4.940656458412465442e-324")]
        [InlineData(2.5, "2.5")]
        [InlineData(-7.0, "-7")]
        [InlineData(0.0, "0")]
        [InlineData(-0.0, "-0")]
        public void ARealOfSat_IsWritten_AsCsPercent19gWritesIt(double value, string written)
        {
            Assert.Equal(written, DwgSat.Number(value));
        }

        /// <summary>
        /// A SAB of R2010 of a body and a transform of no turn, the last flag of the transform <paramref name="shear"/>, the
        /// body's record named <paramref name="body"/>.
        /// </summary>
        private static byte[] Sab(bool shear = false, string body = "body")
        {
            var bytes = new List<byte>(Encoding.ASCII.GetBytes("ACIS BinaryFile"));

            void Int(int value) => bytes.AddRange(BitConverter.GetBytes(value));
            void Tagged(byte tag, int value)
            {
                bytes.Add(tag);
                Int(value);
            }
            void Text(byte tag, string value)
            {
                bytes.Add(tag);
                bytes.Add((byte)value.Length);
                bytes.AddRange(Encoding.ASCII.GetBytes(value));
            }
            void Real(byte tag, params double[] values)
            {
                bytes.Add(tag);

                foreach (double value in values)
                {
                    bytes.AddRange(BitConverter.GetBytes(value));
                }
            }

            Int(21500);
            Int(0);
            Int(1);
            Int(0);
            Text(7, "Autodesk AutoCAD");
            Text(7, "ASM 226.0.0.65535 NT");
            Text(7, "Thu Nov 12 12:13:31 2026");
            Real(6, 25.4);
            Real(6, 1E-06);
            Real(6, 1E-10);
            Text(0x0D, "asmheader");
            Tagged(0x0C, -1);
            Tagged(4, -1);
            Text(7, "226.0.0.65535");
            bytes.Add(0x11);
            Text(0x0D, body);
            Tagged(0x0C, -1);
            Tagged(4, -1);

            for (int i = 0; i < 4; i++)
            {
                Tagged(0x0C, i == 2 ? 2 : -1);
            }

            bytes.Add(0x11);
            Text(0x0D, "transform");
            Tagged(0x0C, -1);
            Tagged(4, -1);
            Real(0x14, 1, 0, 0);
            Real(0x14, 0, 1, 0);
            Real(0x14, 0, 0, 1);
            Real(0x14, 60, -0.0, 0);
            Real(6, 1);
            bytes.Add(0x0B);
            bytes.Add(0x0B);
            bytes.Add(shear ? (byte)0x0A : (byte)0x0B);
            bytes.Add(0x11);
            Text(0x0E, "End");
            Text(0x0E, "of");
            Text(0x0E, "ACIS");
            Text(0x0D, "data");
            return bytes.ToArray();
        }

        [Fact]
        public void ASab_IsWritten_AsSatWithItsHeadAndAWordForEachFlag()
        {
            Assert.Equal(
                "21500 3 1 0           \n"
                    + "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  \n"
                    + "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 \n"
                    + "asmheader $-1 -1 @13 226.0.0.65535 #\n"
                    + "body $-1 -1 $-1 $-1 $2 $-1 #\n"
                    + "transform $-1 -1 1 0 0 0 1 0 0 0 1 60 -0 0 1 no_rotate no_reflect no_shear #\n",
                DwgSat.ToSat(Sab())
            );
        }

        [Fact]
        public void ASabWithAShear_IsWrittenWithTheWordShear()
        {
            Assert.Contains(" 1 no_rotate no_reflect shear #\n", DwgSat.ToSat(Sab(shear: true)));
        }

        [Fact]
        public void ASabOfARecordNotKnown_IsRefused_ForNoSatIsWrittenThatIsNotAutoCads()
        {
            Assert.Null(DwgSat.ToSat(Sab(body: "blob")));
            Assert.Null(DwgSat.ToSat(Sab().Take(Sab().Length - 5).ToArray()));
        }

        [Fact]
        public void ASab_IsReadFromTheBitsOfTheEntity_ToTheRecordThatEndsIt()
        {
            // The entity keeps the SAB at no byte boundary: 3 bits in, and data of the entity after it.
            byte[] sab = Sab();
            var bits = new DwgBitWriter();
            bits.Bit(true);
            bits.Bit(false);
            bits.Bit(true);
            bits.Bytes(sab);
            bits.Bytes(new byte[] { 0xA5, 0x5A });
            var reader = new DwgBitReader(bits.ToArray());
            reader.Bit();
            reader.Bit();
            reader.Bit();

            Assert.Equal(sab, DwgSat.Read(reader));
            Assert.Equal(0xA5, reader.RawChar());
            Assert.Null(DwgSat.Read(new DwgBitReader(Encoding.ASCII.GetBytes("ACIS TextFile and more bytes"))));
        }
    }
}
