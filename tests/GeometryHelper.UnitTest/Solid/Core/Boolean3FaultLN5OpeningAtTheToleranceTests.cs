using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// An opening whose faces stand the point tolerance off the body's, to the last bit, is cut in closed, as an opening a
    /// hair nearer or further is.
    /// </summary>
    /// <remarks>
    /// The cut read a corner of the body on one side of a plane of the opening by its distance, 0.0010000000000000009, and
    /// within the point tolerance of where its edge crossed the plane by where that crossing was rounded to, 0.0009999999999976694
    /// off it; the piece of the face between them lost a corner, and the slab under the plane came out open. A box 100 by 60 by
    /// 40 with an opening 0.1 across at (0.001, 0.001, 0.001) came out open by 25 faces and 7.66 cubic millimetres short, and
    /// of 4 000 such openings a hair either side of the tolerance off one to three faces, turned or not, 1 241 came out
    /// open. A corner is on the plane now, when openings are cut in only, where it stands off it by no more than the planar
    /// tolerance and what rounding the coordinates can add: the opening is read as touching the
    /// faces, and taken with the outside, or as standing clear of them, and cut, and none comes out open.
    /// </remarks>
    public class Boolean3FaultLN5OpeningAtTheToleranceTests
    {
        private static readonly Tolerance Default = Tolerance.Default;

        private static readonly GeoSolid3 Body = new GeoObb3(new GeoPoint3(50, 30, 20), 100, 60, 40).ToSolid();

        /// <summary>The body less what it holds of a cube 0.1 across at the corner, the corner cut away to the faces.</summary>
        private const double Merged = 240000.0 - 0.101 * 0.101 * 0.101;

        private static GeoSolid3 Opening(double off) => new GeoObb3(new GeoPoint3(off + 0.05, off + 0.05, off + 0.05), 0.1, 0.1, 0.1).ToSolid();

        [Theory]
        [InlineData(1E-3)]
        [InlineData(1E-3 + 1E-15)]
        [InlineData(1E-3 - 1E-15)]
        [InlineData(1E-3 + 1E-12)]
        public void AnOpeningTheToleranceOffThreeFaces_IsCutInClosed(double off)
        {
            var withOpening = new GeoSolid3(Body.Faces, new[] { Opening(off) });

            Assert.True(withOpening.TryCutOpenings(out GeoSolid3 material, Default));
            Assert.True(withOpening.TryCutOpenings(out GeoSolid3 again, Default));

            SolidValidation3 validation = material.Validate(Default);
            Assert.True(validation.IsClosed);
            Assert.True(validation.IsValid);

            double volume = material.GetVolume(Default);
            Assert.True(Math.Abs(volume - Merged) < 1E-9 || Math.Abs(volume - (240000.0 - 0.001)) < 1E-9, volume.ToString("R"));
            Assert.Equal(volume, again.GetVolume(Default));
        }

        [Fact]
        public void OpeningsAHairEitherSideOfTheTolerance_TurnedAndNot_AreAllCutInClosed()
        {
            // An even spread of offsets within a few parts in a billion of the tolerance, off one, two or three faces, the
            // whole turned by up to 0.006 rad or not.
            var open = new List<string>();
            int index = 0;

            foreach (int near in new[] { 1, 2, 3 })
            {
                foreach (double angle in new[] { 0.0, 0.0001, 0.0061 })
                {
                    for (int k = -4; k <= 4; k++)
                    {
                        double off = 1E-3 * (1 + k * 1E-12);
                        double size = 0.3 + 0.4 * ((index++ * 7) % 11);
                        GeoPoint3 low = new GeoPoint3(off, near >= 2 ? off : 20, near >= 3 ? off : 15);
                        GeoSolid3 opening = new GeoObb3(new GeoPoint3(low.X + size / 2, low.Y + size / 2, low.Z + size / 2), size, size, size).ToSolid();
                        GeoSolid3 body = Body;

                        if (angle != 0.0)
                        {
                            GeoTransform3 turn = GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(0.3, 0.5, 0.8).Normalize(), angle);
                            body = body.TransformBy(turn);
                            opening = opening.TransformBy(turn);
                        }

                        if (!new GeoSolid3(body.Faces, new[] { opening }).TryCutOpenings(out GeoSolid3 material, Default)
                            || !material.Validate(Default).IsClosed)
                        {
                            open.Add($"near {near}, turned {angle}, off {off:R}, size {size:R}");
                        }
                    }
                }
            }

            Assert.Empty(open);
        }
    }
}
