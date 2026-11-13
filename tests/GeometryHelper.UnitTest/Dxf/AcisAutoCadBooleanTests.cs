using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The regions AutoCAD 2021 made by booleans and moved (<see cref="AcisAutoCadBooleanSamples"/>), read as AutoCAD reads them:
    /// the area and perimeter it gives, every face of a body of two lumps, the transform record a move, turn or mirror leaves
    /// beside the geometry, and the arcs of a notch counter-clockwise about the face's normal as AutoCAD's Explode gives them.
    /// </summary>
    public class AcisAutoCadBooleanTests
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
            // The lines of the SAT, AutoCAD's area and perimeter, how many faces and how many loops.
            yield return new object[] { AcisAutoCadBooleanSamples.Lens2000, 122.836969861, 41.887902048, 1, 1 };
            yield return new object[] { AcisAutoCadBooleanSamples.Notch2000, 1042.920367321, 151.415926536, 1, 1 };
            yield return new object[] { AcisAutoCadBooleanSamples.NotchMoved2000, 1042.920367321, 151.415926536, 1, 1 };
            yield return new object[]
            {
                AcisAutoCadBooleanSamples.NotchMirrored2000,
                1042.920367321,
                151.415926536,
                1,
                1,
            };
            yield return new object[]
            {
                AcisAutoCadBooleanSamples.NotchRotated2004,
                1042.920367321,
                151.415926536,
                1,
                1,
            };
            yield return new object[] { AcisAutoCadBooleanSamples.Disjoint2000, 1514.159265359, 202.831853072, 2, 2 };
            yield return new object[] { AcisAutoCadBooleanSamples.Island2004, 2201.06192983, 450.265482457, 2, 3 };
        }

        [Theory]
        [MemberData(nameof(Samples))]
        public void TheSatOfARegionAutoCadMadeByABoolean_ReadsToAutoCadsAreaAndPerimeter(
            string[] lines,
            double area,
            double perimeter,
            int faces,
            int loops
        )
        {
            AcisBody body = AcisReader.FromSat(lines);

            Assert.NotNull(body);
            Assert.Equal(area, body.Area, 6);
            Assert.Equal(perimeter, body.Perimeter, 6);
            Assert.Equal(faces, body.Faces.Count);
            Assert.Equal(loops, body.Faces.Sum(f => f.Loops.Count));
        }

        [Fact]
        public void TheSabOfARegionOfTwoLumps_ReadsBothFaces()
        {
            AcisBody body = AcisReader.FromSab(Bytes(AcisAutoCadBooleanSamples.Disjoint2013));

            Assert.NotNull(body);
            Assert.Equal(2, body.Faces.Count);
            Assert.Equal(1514.159265359, body.Area, 6);
        }

        [Fact]
        public void TheNotchAutoCadCutWithACircle_HasItsArcCounterClockwiseAboutTheNormal_AsItsExplodeGivesIt()
        {
            var shape = new DxfRegionShape(AcisReader.FromSat(AcisAutoCadBooleanSamples.Notch2000));

            DxfArc arc = Assert.Single(shape.Loops[0].Curves.OfType<DxfArc>());
            Assert.Equal(40.0, arc.Center.X, 9);
            Assert.Equal(15.0, arc.Center.Y, 9);
            Assert.Equal(10.0, arc.Radius, 9);
            Assert.Equal(Math.PI / 2.0, arc.StartAngle, 6);
            Assert.Equal(3.0 * Math.PI / 2.0, arc.EndAngle, 6);
            Assert.Equal(1.0, arc.Normal.Z, 12);
            Assert.Equal(5, shape.Loops[0].Curves.OfType<DxfLine>().Count());
        }

        [Fact]
        public void ARegionAutoCadMovedBy100And50_IsReadAtItsNewPlace()
        {
            var shape = new DxfRegionShape(AcisReader.FromSat(AcisAutoCadBooleanSamples.NotchMoved2000));

            DxfArc arc = Assert.Single(shape.Loops[0].Curves.OfType<DxfArc>());
            Assert.Equal(140.0, arc.Center.X, 9);
            Assert.Equal(65.0, arc.Center.Y, 9);
            DxfLine first = shape.Loops[0].Curves.OfType<DxfLine>().First();
            Assert.Equal(140.0, first.StartPoint.X, 9);
            Assert.Equal(75.0, first.StartPoint.Y, 9);
            Assert.Equal(1.0, shape.Normal.Z, 12);
        }

        [Fact]
        public void ARegionAutoCadTurnedBy30Degrees_IsReadTurned_OfAHistoryFormatToo()
        {
            var shape = new DxfRegionShape(AcisReader.FromSat(AcisAutoCadBooleanSamples.NotchRotated2004));

            DxfArc arc = Assert.Single(shape.Loops[0].Curves.OfType<DxfArc>());
            Assert.Equal(27.141016151, arc.Center.X, 8);
            Assert.Equal(32.990381057, arc.Center.Y, 8);
            Assert.Equal(2.094395102, arc.StartAngle, 8);
            Assert.Equal(5.235987756, arc.EndAngle, 8);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ARegionAutoCadMirroredAcrossX50_HasItsNormalDown_AndItsLoopInTheSameOrder(bool binary)
        {
            AcisBody body = binary
                ? AcisReader.FromSab(Bytes(AcisAutoCadBooleanSamples.NotchMirrored2018))
                : AcisReader.FromSat(AcisAutoCadBooleanSamples.NotchMirrored2000);
            var shape = new DxfRegionShape(body);

            Assert.Equal(-1.0, shape.Normal.Z, 12);
            DxfArc arc = Assert.Single(shape.Loops[0].Curves.OfType<DxfArc>());
            Assert.Equal(60.0, arc.Center.X, 9);
            Assert.Equal(15.0, arc.Center.Y, 9);
            Assert.Equal(-1.0, arc.Normal.Z, 12);
            Assert.Equal(Math.PI / 2.0, arc.StartAngle, 6);
            Assert.Equal(3.0 * Math.PI / 2.0, arc.EndAngle, 6);
            DxfLine first = shape.Loops[0].Curves.OfType<DxfLine>().First();
            Assert.Equal(60.0, first.StartPoint.X, 9);
            Assert.Equal(25.0, first.StartPoint.Y, 9);
            Assert.Equal(60.0, first.EndPoint.X, 9);
            Assert.Equal(30.0, first.EndPoint.Y, 9);
        }

        [Fact]
        public void ALineWhoseLoopRunsAgainstItsEdge_IsExplodedInTheDirectionOfItsEdge_AsAutoCadsExplodeGivesIt()
        {
            var shape = new DxfRegionShape(AcisReader.FromSat(AcisAutoCadBooleanSamples.EllipseInBox2000));

            // The loop runs up the right side, counter-clockwise; AutoCAD explodes the line the way its edge runs, down.
            DxfLine[] lines = shape.Loops[0].Curves.OfType<DxfLine>().ToArray();
            Assert.Equal(2, lines.Length);
            Assert.Equal(40.0, lines[0].StartPoint.X, 9);
            Assert.Equal(22.453559925, lines[0].StartPoint.Y, 8);
            Assert.Equal(7.546440075, lines[0].EndPoint.Y, 8);
            Assert.Equal(0.0, lines[1].StartPoint.X, 9);
            Assert.Equal(7.546440075, lines[1].StartPoint.Y, 8);
            Assert.Equal(735.978990736, shape.Area, 6);
        }

        [Fact]
        public void TheLensOfR2000_HasTwoArcsOfRadius10_CutByTheVerticesOfItsEdges()
        {
            var shape = new DxfRegionShape(AcisReader.FromSat(AcisAutoCadBooleanSamples.Lens2000));

            DxfArc[] arcs = shape.Loops[0].Curves.OfType<DxfArc>().ToArray();
            Assert.Equal(2, arcs.Length);
            Assert.All(arcs, a => Assert.Equal(10.0, a.Radius, 9));
            Assert.Equal(new[] { 0.0, 10.0 }, arcs.Select(a => Math.Round(a.Center.X, 9)).OrderBy(x => x).ToArray());
            Assert.Equal(2.0 * Math.PI / 3.0, arcs.Sum(a => a.TotalAngle) / 2.0, 6);
        }
    }
}
