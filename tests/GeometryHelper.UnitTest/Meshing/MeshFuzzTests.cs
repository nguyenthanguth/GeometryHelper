using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Meshing
{
    /// <summary>
    /// Random faces meshed in random ways, each mesh held to what a mesh promises: faces simple and counter-clockwise, the
    /// material's area, nothing across an edge no face runs back along but what is outside, and at points spread over
    /// the shape every point of the material in one face.
    /// </summary>
    public class MeshFuzzTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        /// <summary>
        /// The seeds that failed once, out of 323 000, each for a reason since put right:
        /// <list type="bullet">
        /// <item>a corner of a sliver of material near the side of a face across it, taken as standing on it, which
        /// pulled the face over its neighbour (325, 1075, 2292);</item>
        /// <item>two sides running back along one line and overlapping only in part (231, 607, 1282);</item>
        /// <item>a hole's corner touching another hole's side, which left the material between them one face touching
        /// itself there (863, 1082, 1702, 1996, 2568, 2727, 2822);</item>
        /// <item>a piece of a few thousandths of a square millimetre dropped as no area (1323, 5728, 17839);</item>
        /// <item>strip lines within the tolerance of each other, whose points were joined across the sliver between
        /// (11553, 14440, 20203);</item>
        /// <item>a crossing handed back by the cutting as it was rounded, a step of its grid off the same crossing
        /// computed again (303, 366, 7896, 10794, 19103);</item>
        /// <item>the area of a needle of a face far out, summed from the origin (17144);</item>
        /// <item>a cell reaching into the material by less than the tolerance left out, and the sliver it would have
        /// covered with it (166187), or one a cell whole within the tolerance laid over as the whole rectangle, a little
        /// outside the material (158057);</item>
        /// <item>Clipper2's crossing of a cell's side with a long edge four steps of its grid off, past where it was
        /// restored from, so that the cell beside met it at a point of its own (28529, 35295, 38449, 98384, 113129, 113640,
        /// 129807, 137957, 150352, 171964, 172650, 177900, 216067, 222605);</item>
        /// <item>the triangles of the faces, left to the surface's meshing and its tolerance, which dropped corners in a
        /// row and slivers, and kept a needle of no area (1010, 1110, 1217, 1253, 6571, 14835).</item>
        /// </list>
        /// </summary>
        public static IEnumerable<object[]> FailedOnce()
        {
            foreach (int seed in new[]
            {
                231, 303, 325, 366, 394, 607, 863, 1075, 1082, 1282, 1323, 1702, 1996, 2292, 2568, 2727, 2822, 5728, 7896, 10794,
                11553, 14440, 17144, 17839, 19103, 20203, 28529, 35295, 38449, 98384, 113129, 113640, 129807, 137957, 150352,
                158057, 166187, 171964, 172650, 177900, 216067, 222605, 1010, 1110, 1217, 1253, 6571, 14835,
            })
            {
                yield return new object[] { seed };
            }
        }

        private static void AssertSound(int seed)
        {
            var random = new Random(seed);
            GeoFace2 face = MeshFuzz.RandomFace(random, out string shape);
            MeshOptions options = MeshFuzz.RandomOptions(random, out string how);
            string failure;

            try
            {
                failure = MeshFuzz.Check(face.ToMesh(options, Tolerance), face, options, Tolerance);
            }
            catch (Exception e)
            {
                failure = "threw " + e.GetType().Name + ": " + e.Message;
            }

            Assert.True(failure == null, $"seed {seed}: {shape}; {how}: {failure}");
        }

        [Theory]
        [MemberData(nameof(FailedOnce))]
        public void TheSeedsThatFailedOnceMeshSoundly(int seed) => AssertSound(seed);

        [Fact]
        public void FourHundredRandomFacesMeshSoundly()
        {
            for (int seed = 1; seed <= 400; seed++)
            {
                AssertSound(seed);
            }
        }
    }
}
