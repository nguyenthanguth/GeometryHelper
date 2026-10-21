using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A void thicker than the tolerance and thinner than twice it is cut, and one no thicker than the tolerance vanishes.
    /// </summary>
    /// <remarks>
    /// A cell of the cut no thicker than twice the point tolerance is placed by the middle of its thickness, which lies within
    /// the tolerance of both its sides, and was read against the other body within the tolerance: on its skin, not in it. So a
    /// box 40 by 30 and 0.0015 thick taken out of a cube of 100 left the cube whole, 1.8 cubic millimetres too much, and so
    /// did one 0.002 thick, whose middle is the tolerance from both its sides; the common part of the two was nothing, and the
    /// two joined held the void twice. Such a middle is read within a quarter of the cell's thickness now, as it was found, and
    /// one found further than the tolerance inside its cell within half the tolerance. A void no thicker than the tolerance is
    /// within the tolerance of nothing, and the cube less it is the cube.
    /// </remarks>
    public class Boolean3FaultLN5ThinVoidTests
    {
        private static readonly Tolerance Default = Tolerance.Default;

        private static readonly GeoObb3 Cube = new GeoObb3(new GeoPoint3(0, 0, 0), 100, 100, 100);

        private static GeoObb3 Void(double thickness) => new GeoObb3(new GeoPoint3(3, 4, 5), 40, 30, thickness);

        private static GeoObb3 TurnedVoid(double thickness)
            => new GeoObb3(new GeoPoint3(3, 4, 5), 40, 30, thickness, new GeoVector3(1, 0.3, 0.2), new GeoVector3(-0.2, 1, 0.1));

        [Theory]
        [InlineData(0.0011)]
        [InlineData(0.0015)]
        [InlineData(0.0019)]
        [InlineData(0.002)]
        public void AVoidThickerThanTheTolerance_IsCutOutOfTheBox(double thickness)
        {
            Assert.True(Cube.TrySubtract(Void(thickness), out GeoSolid3 left, Default));

            Assert.True(left.Validate(Default).IsValid);
            Assert.Equal(1000000.0 - 1200.0 * thickness, left.GetVolume(Default), 6);
        }

        [Theory]
        [InlineData(0.0011)]
        [InlineData(0.0015)]
        [InlineData(0.002)]
        public void AVoidThickerThanTheTolerance_IsCutOutOfTheBody_TurnedOrNot(double thickness)
        {
            Assert.True(Cube.ToSolid().TrySubtract(Void(thickness).ToSolid(), out GeoSolid3 left, Default));
            Assert.True(Cube.TrySubtract(TurnedVoid(thickness), out GeoSolid3 turned, Default));

            Assert.Equal(1000000.0 - 1200.0 * thickness, left.GetVolume(Default), 6);
            Assert.Equal(1000000.0 - 1200.0 * thickness, turned.GetVolume(Default), 6);
            Assert.True(turned.Validate(Default).IsValid);
        }

        [Theory]
        [InlineData(0.0002)]
        [InlineData(0.0005)]
        [InlineData(0.0009)]
        public void AVoidNoThickerThanTheTolerance_Vanishes(double thickness)
        {
            Assert.True(Cube.TrySubtract(Void(thickness), out GeoSolid3 left, Default));
            Assert.True(Cube.TrySubtract(TurnedVoid(thickness), out GeoSolid3 turned, Default));

            Assert.Equal(1000000.0, left.GetVolume(Default), 6);
            Assert.Equal(1000000.0, turned.GetVolume(Default), 6);
            Assert.Equal(6, left.Faces.Count);
        }

        [Fact]
        public void AnOpeningThickerThanTheTolerance_IsCutIn()
        {
            var withOpening = new GeoSolid3(Cube.ToSolid().Faces, new[] { Void(0.0015).ToSolid() });

            Assert.True(withOpening.TryCutOpenings(out GeoSolid3 material, Default));

            Assert.True(material.Validate(Default).IsValid);
            Assert.Equal(999998.2, material.GetVolume(Default), 6);
        }

        [Fact]
        public void TheCommonPartOfTheBoxAndAThinVoid_IsTheVoid()
        {
            Assert.True(Cube.TryIntersect(Void(0.0015), out GeoSolid3 common, Default));

            Assert.Equal(1.8, common.GetVolume(Default), 9);
        }

        [Fact]
        public void ABlockRunningAThinChipIntoTheTop_IsJoinedHoldingTheChipOnce()
        {
            // A block 40 by 20 by 20, its floor 0.0015 below the top of the cube.
            var block = new GeoObb3(new GeoPoint3(0, 0, 60 - 0.0015 / 2), 40, 20, 20 + 0.0015);

            Assert.True(Cube.TryUnion(block, out GeoSolid3 joined, Default));
            Assert.True(Cube.TrySubtract(block, out GeoSolid3 chipped, Default));

            Assert.Equal(1000000.0 + 16000.0, joined.GetVolume(Default), 6);
            Assert.Equal(1000000.0 - 800.0 * 0.0015, chipped.GetVolume(Default), 6);
        }
    }
}
