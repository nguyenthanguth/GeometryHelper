using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using Xunit;

namespace GeometryHelper.UnitTest.Meshing
{
    /// <summary>
    /// Random flat shapes of space meshed and random bodies cut into cells, held to every promise: the seeds that once found
    /// a fault, and a run of others.
    /// </summary>
    public class Mesh3FuzzTests
    {
        /// <summary>
        /// Flat shapes whose holes stood a hair off the plane, whose points in a hole the face once said were inside it.
        /// </summary>
        public static IEnumerable<object[]> PlanarSeedsThatFailed()
            => new[] { 2012653, 2026925, 2042229 }.Select(s => new object[] { s });

        /// <summary>
        /// Bodies whose cutting once went wrong: a cut between the arms of a piece refused (1000015 to 1000297), volume lost
        /// to the tolerance band (2003059, 2014595), a body pinched along an edge refused (2011745), a ray grazing a hole's
        /// rim (2005903, 2016607), a tip a hair past a cut (4015293, 4017907, 5015694), a hole on a rim taken for material
        /// (4017446), the point of a needle (9015982), a hull with a face smaller than a polygon (13001387), the edge of a
        /// blade past a cut, its corners each counted once (32019733), and a corner of a piece on a cut, which leaves a sliver
        /// smaller than a square of the tolerance between edges that cancel (51003689).
        /// </summary>
        public static IEnumerable<object[]> CellSeedsThatFailed()
            => new[]
            {
                1000015, 1000061, 1000065, 1000086, 1000103, 1000132, 1000172, 1000193, 1000220, 1000230, 1000280, 1000297,
                2003059, 2005903, 2011745, 2014595, 2016607, 4015293, 4017446, 4017907, 5015694, 9015982, 13001387,
                32019733, 51003689,
            }.Select(s => new object[] { s });

        /// <summary>
        /// Boxes cut as boxes and as bodies that once came apart: a plate on the line through its middle given to the cell
        /// either side by the rounding (61002179), cells by count thinner than four point tolerances cut against the box's
        /// sides alone (61000640), a plate wholly in a joint (61002363), and a thin box far out measured by its faces
        /// (61002673).
        /// </summary>
        public static IEnumerable<object[]> BoxSeedsThatFailed()
            => new[] { 61000640, 61002179, 61002363, 61002673 }.Select(s => new object[] { s });

        /// <summary>A column with a ledge whose piece, a hair out of flat, was refused once cut (62005190).</summary>
        public static IEnumerable<object[]> ThinSeedsThatFailed()
            => new[] { 62005190 }.Select(s => new object[] { s });

        public static IEnumerable<object[]> PlanarRun() => Enumerable.Range(1, 400).Select(s => new object[] { s });

        public static IEnumerable<object[]> CellRun() => Enumerable.Range(1, 200).Select(s => new object[] { s });

        public static IEnumerable<object[]> BoxRun() => Enumerable.Range(1, 150).Select(s => new object[] { s });

        public static IEnumerable<object[]> ThinRun() => Enumerable.Range(1, 150).Select(s => new object[] { s });

        public static IEnumerable<object[]> ArcRun() => Enumerable.Range(1, 200).Select(s => new object[] { s });

        [Theory]
        [MemberData(nameof(ArcRun))]
        public void ALoopWithArcsOfSpaceMeshesSoundly(int seed)
        {
            Assert.Null(MeshFuzz3.ArcsOne(seed, out _));
        }

        [Theory]
        [MemberData(nameof(BoxSeedsThatFailed))]
        [MemberData(nameof(BoxRun))]
        public void ABoxIsCutAsTheBodyItIs(int seed)
        {
            Assert.Null(Quietly(() => MeshFuzz3.BoxesOne(seed, out _)));
        }

        [Theory]
        [MemberData(nameof(ThinSeedsThatFailed))]
        [MemberData(nameof(ThinRun))]
        public void ABodyWithAPartThinnerThanTheSnapDistanceIsCutIntoSoundCells(int seed)
        {
            Assert.Null(Quietly(() => MeshFuzz3.ThinOne(seed, out _)));
        }

        /// <summary>What a case says is wrong, or that it warned of something.</summary>
        private static string Quietly(System.Func<string> run)
        {
            var warnings = new List<string>();
            GeometryHelperLog.Writer = (level, message, exception) =>
            {
                if (level == GeometryHelperLogLevel.Warn)
                {
                    lock (warnings)
                    {
                        warnings.Add(message);
                    }
                }
            };

            try
            {
                return run() ?? (warnings.Count > 0 ? "warned: " + warnings[0] : null);
            }
            finally
            {
                GeometryHelperLog.Writer = null;
            }
        }

        [Theory]
        [MemberData(nameof(PlanarSeedsThatFailed))]
        [MemberData(nameof(PlanarRun))]
        public void AFlatShapeOfSpaceMeshesSoundly(int seed)
        {
            Assert.Null(MeshFuzz3.PlanarOne(seed, out _));
        }

        [Theory]
        [MemberData(nameof(CellSeedsThatFailed))]
        [MemberData(nameof(CellRun))]
        public void ABodyIsCutIntoSoundCells(int seed)
        {
            var warnings = new List<string>();
            GeometryHelperLog.Writer = (level, message, exception) =>
            {
                if (level == GeometryHelperLogLevel.Warn)
                {
                    lock (warnings)
                    {
                        warnings.Add(message);
                    }
                }
            };

            try
            {
                Assert.Null(MeshFuzz3.CellsOne(seed, out _));
                Assert.Empty(warnings);
            }
            finally
            {
                GeometryHelperLog.Writer = null;
            }
        }
    }
}
