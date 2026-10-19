using System;
using System.Threading;
using System.Threading.Tasks;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// The geometry that views share: four views with the same scene, cut and origin are given one build.
    /// </summary>
    public class GeometryCacheTests
    {
        // A cut of a scene with one point in it: the key a view holds the geometry of a scene under.
        private static ClipResult Cut() => ClipResult.Build(Primitives.Build(new SceneWriter().Add(new GeoPoint3(1, 2, 3)).Scene), ClipSet.Empty);

        [Fact]
        public async Task FourViewsThatTakeTheSameThingNow_AreGivenOneBuild_AndShareIt()
        {
            var cache = new GeometryCache<int[]>();
            ClipResult clip = Cut();
            int made = 0;
            Func<int[]> make = () => { made++; return new[] { 7 }; };

            GeometryCache<int[]>.Entry[] taken = new GeometryCache<int[]>.Entry[4];

            for (int i = 0; i < 4; i++)
            {
                taken[i] = cache.Take(1, clip, new Vec3(1, 2, 3), make, true);
            }

            Assert.Equal(1, made);
            Assert.Equal(1, cache.Builds);
            Assert.All(taken, t => Assert.Same(taken[0], t));
            Assert.Same(await taken[0].Made, await taken[3].Made);
            Assert.Equal(4, taken[0].Users);
        }

        [Fact]
        public void AnotherOrigin_AnotherCut_OrAnotherScene_IsAnotherBuild()
        {
            var cache = new GeometryCache<int[]>();
            ClipResult clip = Cut();
            ClipResult other = Cut();
            Func<int[]> make = () => new[] { 1 };

            cache.Take(1, clip, new Vec3(0, 0, 0), make, true);
            cache.Take(1, clip, new Vec3(0, 0, 1), make, true);
            cache.Take(1, other, new Vec3(0, 0, 0), make, true);
            cache.Take(2, clip, new Vec3(0, 0, 0), make, true);
            cache.Take(1, clip, new Vec3(0, 0, 0), make, true);

            Assert.Equal(4, cache.Builds);
            Assert.Equal(4, cache.Count);
        }

        [Fact]
        public void ASetNoViewHolds_IsDropped_AndOneThatIsHeldStays()
        {
            var cache = new GeometryCache<int[]>();
            ClipResult clip = Cut();
            Func<int[]> make = () => new[] { 1 };
            var first = cache.Take(1, clip, default(Vec3), make, true);
            var second = cache.Take(1, clip, default(Vec3), make, true);

            cache.Release(first);
            Assert.Equal(1, cache.Count);

            cache.Release(second);
            Assert.Equal(0, cache.Count);

            // Asked for again, it is made again; letting go of nothing changes nothing.
            cache.Release(null);
            cache.Take(1, clip, default(Vec3), make, true);
            Assert.Equal(2, cache.Builds);
        }

        [Fact]
        public async Task ViewsThatWaitForGeometryUnderWayOnAnotherThread_GetThatBuild_AndOneWantedNowMakesItsOwn()
        {
            var cache = new GeometryCache<int[]>();
            ClipResult clip = Cut();
            var gate = new ManualResetEventSlim(false);
            int made = 0;
            Func<int[]> slow = () => { Interlocked.Increment(ref made); gate.Wait(); return new[] { 5 }; };

            var first = cache.Take(1, clip, default(Vec3), slow, false);
            var second = cache.Take(1, clip, default(Vec3), slow, false);

            // The first is not done, and a view that wants it now does not wait for it.
            Assert.False(first.Made.IsCompleted);
            var now = cache.Take(1, clip, default(Vec3), () => new[] { 9 }, true);
            Assert.NotSame(first, now);
            Assert.Equal(9, (await now.Made)[0]);

            gate.Set();
            Assert.Same(first, second);
            Assert.Equal(5, (await first.Made)[0]);
            Assert.Equal(1, made);
        }

        [Fact]
        public void ATakeWithNothingToMakeItWith_IsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => new GeometryCache<int[]>().Take(1, Cut(), default(Vec3), null, true));
        }
    }
}
