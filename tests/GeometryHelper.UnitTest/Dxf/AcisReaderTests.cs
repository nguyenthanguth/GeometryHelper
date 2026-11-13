using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The planar bodies of the regions AutoCAD 2021 wrote (<see cref="AcisAutoCadSamples"/>), read from their SAT and SAB: the
    /// area and perimeter AutoCAD's <c>Region</c> gives for each are those of the body read, and what the writer writes is
    /// read back as it was.
    /// </summary>
    public class AcisReaderTests
    {
        private static byte[] Bytes(string hex)
        {
            var bytes = new byte[hex.Length / 2];

            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = byte.Parse(hex.Substring(2 * i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return bytes;
        }

        public static IEnumerable<object[]> Samples()
        {
            // The region's lines, the area and the perimeter AutoCAD's Region gave for it, and how many loops it has.
            yield return new object[] { AcisAutoCadSamples.Rect2000, 1200.0, 140.0, 1 };
            yield return new object[] { AcisAutoCadSamples.Rect2004, 1200.0, 140.0, 1 };
            yield return new object[] { AcisAutoCadSamples.Rect2010, 1200.0, 140.0, 1 };
            yield return new object[] { AcisAutoCadSamples.RectClockwise2004, 60.0, 32.0, 1 };
            yield return new object[] { AcisAutoCadSamples.Circle2000, 153.93804002589985, 43.982297150257104, 1 };
            yield return new object[] { AcisAutoCadSamples.Circle2010, 153.93804002589985, 43.982297150257104, 1 };
            yield return new object[] { AcisAutoCadSamples.Stadium2007, 378.53981633974485, 91.415926535897938, 1 };
            yield return new object[] { AcisAutoCadSamples.DShape2010, 557.07963267948969, 91.415926535897938, 1 };
            yield return new object[] { AcisAutoCadSamples.RectHole2000, 1121.4601836602551, 171.41592653589794, 2 };
            yield return new object[] { AcisAutoCadSamples.RectHole2007, 1121.4601836602551, 171.41592653589794, 2 };
            yield return new object[] { AcisAutoCadSamples.TwoHoles2010, 1121.4601836602551, 183.9822971502571, 3 };
            yield return new object[] { AcisAutoCadSamples.RectInRect2004, 1152.0, 168.0, 2 };
            yield return new object[] { AcisAutoCadSamples.Ellipse2000, 307.8760800517997, 67.81913754383379, 1 };
            yield return new object[]
            {
                AcisAutoCadSamples.EllipseRotated2000,
                251.32741228718345,
                65.090911890416749,
                1,
            };
            yield return new object[] { AcisAutoCadSamples.HalfEllipse2000, 78.539816339744831, 44.221120551369211, 1 };
            yield return new object[] { AcisAutoCadSamples.Tilted2004, 70.710678118654755, 41.462643699419729, 1 };
        }

        [Theory]
        [MemberData(nameof(Samples))]
        public void TheSatOfARegionAutoCadWrote_ReadsToTheAreaAndPerimeterAutoCadGives(
            string[] lines,
            double area,
            double perimeter,
            int loops
        )
        {
            AcisBody body = AcisReader.FromSat(lines);

            Assert.NotNull(body);
            Assert.Equal(area, body.Area, 6);
            Assert.Equal(perimeter, body.Perimeter, 6);
            Assert.Equal(loops, body.Faces.Sum(f => f.Loops.Count));
        }

        [Theory]
        [InlineData(nameof(AcisAutoCadSamples.Rect2018), 1200.0, 140.0, 1)]
        [InlineData(nameof(AcisAutoCadSamples.Circle2018), 153.93804002589985, 43.982297150257104, 1)]
        [InlineData(nameof(AcisAutoCadSamples.RectHole2018), 1121.4601836602551, 171.41592653589794, 2)]
        [InlineData(nameof(AcisAutoCadSamples.Stadium2013), 378.53981633974485, 91.415926535897938, 1)]
        public void TheSabOfARegionAutoCadWrote_ReadsToTheAreaAndPerimeterAutoCadGives(
            string sample,
            double area,
            double perimeter,
            int loops
        )
        {
            string hex = (string)
                typeof(AcisAutoCadSamples)
                    .GetField(sample, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    .GetValue(null);

            AcisBody body = AcisReader.FromSab(Bytes(hex));

            Assert.NotNull(body);
            Assert.Equal(area, body.Area, 6);
            Assert.Equal(perimeter, body.Perimeter, 6);
            Assert.Equal(loops, body.Faces.Sum(f => f.Loops.Count));
        }

        [Fact]
        public void ARegionWithAHole_HasItsHoleRunningClockwise()
        {
            AcisBody body = AcisReader.FromSat(AcisAutoCadSamples.RectHole2007);

            AcisFace face = Assert.Single(body.Faces);
            Assert.Equal(1.0, face.Normal.Z, 12);
            Assert.Equal(new[] { true, false }, face.Loops.Select(l => l.IsHole).ToArray());
        }

        [Fact]
        public void ARegionAutoCadMadeFromAClockwiseRectangle_IsReadWithItsNormalUp()
        {
            // AutoCAD stores it on a plane facing down with the face reversed; the face looks up all the same.
            AcisFace face = AcisReader.FromSat(AcisAutoCadSamples.RectClockwise2004).Faces[0];

            Assert.Equal(1.0, face.Normal.Z, 12);
            Assert.False(face.Loops[0].IsHole);
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        [InlineData("AC1024")]
        public void TheSatWeWrite_ReadsBackAsTheBodyItCameFrom(string version)
        {
            var body = AcisBody.Create(
                new[]
                {
                    AcisCurve.Line(new AcisVec(0, 0, 0), new AcisVec(30, 0, 0)),
                    AcisCurve.Arc(new AcisVec(30, 5, 0), new AcisVec(0, 0, 1), new AcisVec(30, 0, 0), Math.PI),
                    AcisCurve.Line(new AcisVec(30, 10, 0), new AcisVec(0, 10, 0)),
                    AcisCurve.Arc(new AcisVec(0, 5, 0), new AcisVec(0, 0, 1), new AcisVec(0, 10, 0), Math.PI),
                },
                new IReadOnlyList<AcisCurve>[]
                {
                    new[] { AcisCurve.Whole(new AcisVec(15, 5, 0), new AcisVec(0, 0, 1), new AcisVec(3, 0, 0), 1.0) },
                    new[] { AcisCurve.Whole(new AcisVec(8, 5, 0), new AcisVec(0, 0, 1), new AcisVec(2, 0, 0), 0.5) },
                },
                1E-3
            );
            AcisFormat format = AcisFormat.For(version);

            AcisBody back = AcisReader.FromSat(AcisWriter.Sat(body.Records(format), format));

            Assert.Equal(body.Area, back.Area, 9);
            Assert.Equal(body.Perimeter, back.Perimeter, 9);
            Assert.Equal(3, back.Faces[0].Loops.Count);
        }

        [Theory]
        [InlineData("AC1027")]
        [InlineData("AC1032")]
        public void TheSabWeWrite_ReadsBackAsTheBodyItCameFrom(string version)
        {
            var body = AcisBody.Create(
                new[]
                {
                    AcisCurve.Line(new AcisVec(0, 0, 0), new AcisVec(40, 0, 0)),
                    AcisCurve.Line(new AcisVec(40, 0, 0), new AcisVec(40, 30, 0)),
                    AcisCurve.Line(new AcisVec(40, 30, 0), new AcisVec(0, 30, 0)),
                    AcisCurve.Line(new AcisVec(0, 30, 0), new AcisVec(0, 0, 0)),
                },
                new IReadOnlyList<AcisCurve>[]
                {
                    new[] { AcisCurve.Whole(new AcisVec(20, 15, 0), new AcisVec(0, 0, 1), new AcisVec(5, 0, 0), 1.0) },
                },
                1E-3
            );
            AcisFormat format = AcisFormat.For(version);

            AcisBody back = AcisReader.FromSab(
                AcisWriter.Sab(body.Records(format), format, "Fri Nov 13 08:05:36 2026")
            );

            Assert.Equal(1121.4601836602551, back.Area, 9);
            Assert.Equal(171.41592653589794, back.Perimeter, 9);
        }

        [Fact]
        public void DataOfAnotherSurface_IsNotRead()
        {
            string[] cone =
            {
                "20800 3 2 0 ",
                "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
                "25.4 1e-06 1e-10 ",
                "asmheader $-1 -1 @13 226.0.0.65535 #",
                "body $-1 -1 $-1 $2 $-1 $-1 #",
                "lump $-1 -1 $-1 $-1 $3 $1 #",
                "shell $-1 -1 $-1 $-1 $-1 $4 $-1 $2 #",
                "face $-1 -1 $-1 $-1 $-1 $3 $-1 $5 forward double out #",
                "cone-surface $-1 -1 $-1 0 0 0 0 0 1 1 0 0 1 0 0 0 forward I I I I #",
            };

            Assert.Null(AcisReader.FromSat(cone));
        }
    }
}
