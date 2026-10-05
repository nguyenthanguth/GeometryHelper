using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// Booleans of solids with a contact distance: a face of the second body lying parallel to a face of the first, a few
    /// thousandths into or off it, is put onto it, so that parts drawn against each other are cut as touching; and bodies
    /// taken out one after another, each result checked; see <see cref="SolidBooleanOptions"/>.
    /// </summary>
    public class ContactBooleanTests
    {
        private static readonly Tolerance Fine = Tolerance.Default;

        private static readonly SolidBooleanOptions Plain = new SolidBooleanOptions(Fine);

        private static readonly SolidBooleanOptions Touching = new SolidBooleanOptions(Fine, 0.01);

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        private static GeoSolid3 Slab() => Box(0, 0, 0, 1000, 1000, 300);

        [Fact]
        public void ATool5MicronsInsideTheSubjectsFace_LeavesASkinWithoutContact_AndNoneWithIt()
        {
            // The tool's face at x = 0.005 faces the way the slab's face at x = 0 does, and runs across all of it: cut within
            // a thousandth, the slab keeps a skin of itself 0.005 thick, two faces of 0.3 square metres each.
            GeoSolid3 tool = Box(0.005, -100, -100, 500, 1100, 400);

            Assert.True(Slab().TrySubtract(tool, out GeoSolid3 plain, Plain, out BooleanOutcome plainOutcome));
            Assert.Equal(BooleanOutcome.Made, plainOutcome);
            Assert.Equal(2, plain.SplitShells(Fine).Length);
            Assert.True(plain.GetSurfaceArea(Fine) > 1.9E6 + 5.9E5);

            Assert.True(Slab().TrySubtract(tool, out GeoSolid3 touching, Touching, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.True(touching.Validate(Fine).IsValid, touching.Validate(Fine).ToString());
            Assert.Single(touching.SplitShells(Fine));
            Assert.Equal(500.0 * 1000.0 * 300.0, touching.GetVolume(Fine), 3);
            Assert.Equal(2.0 * (500.0 * 1000.0 + 500.0 * 300.0 + 1000.0 * 300.0), touching.GetSurfaceArea(Fine), 3);
        }

        [Fact]
        public void ATool5MicronsInsideOverPartOfTheFace_LeavesNoFlapWithContact()
        {
            // Over 600 of the slab's 1 000 only: without contact the skin stays on the slab where the tool ends, a flap.
            GeoSolid3 tool = Box(0.005, -100, -100, 500, 600, 400);
            double exact = 1000.0 * 1000.0 * 300.0 - 500.0 * 600.0 * 300.0;
            double area = 2.0 * (1000.0 * 1000.0 - 500.0 * 600.0) + 300.0 * (500 + 600 + 500 + 1000 + 1000 + 400);

            Assert.True(Slab().TrySubtract(tool, out GeoSolid3 plain, Plain, out _));
            Assert.True(plain.GetSurfaceArea(Fine) > area + 2.0 * 600.0 * 300.0 * 0.99);

            Assert.True(Slab().TrySubtract(tool, out GeoSolid3 touching, Touching, out _));
            Assert.True(touching.Validate(Fine).IsValid, touching.Validate(Fine).ToString());
            Assert.Equal(exact, touching.GetVolume(Fine), 3);
            Assert.Equal(area, touching.GetSurfaceArea(Fine), 3);
        }

        [Fact]
        public void TwoBodies5MicronsApart_UniteAsOneWithContact()
        {
            GeoSolid3 beside = Box(1000.005, 0, 0, 2000, 1000, 300);

            Assert.True(Slab().TryUnion(beside, out GeoSolid3 plain, Plain, out _));
            Assert.Equal(2, plain.SplitShells(Fine).Length);

            Assert.True(Slab().TryUnion(beside, out GeoSolid3 touching, Touching, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Single(touching.SplitShells(Fine));
            Assert.Equal(2000.0 * 1000.0 * 300.0, touching.GetVolume(Fine), 3);
            Assert.Equal(2.0 * (2000.0 * 1000.0 + 2000.0 * 300.0 + 1000.0 * 300.0), touching.GetSurfaceArea(Fine), 3);
        }

        [Fact]
        public void TwoBodies5MicronsIntoEachOther_ShareNothingWithContact()
        {
            GeoSolid3 into = Box(999.995, 0, 0, 2000, 1000, 300);

            Assert.True(Slab().TryIntersect(into, out GeoSolid3 sliver, Plain, out _));
            Assert.Equal(0.005 * 1000.0 * 300.0, sliver.GetVolume(Fine), 3);

            Assert.False(Slab().TryIntersect(into, out _, Touching, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.Empty, outcome);
        }

        [Fact]
        public void AFaceFurtherThanTheContact_IsCutAsItIs()
        {
            // Two hundredths in is material, not noise, and stays.
            GeoSolid3 tool = Box(0.02, -100, -100, 500, 1100, 400);

            Assert.True(Slab().TrySubtract(tool, out GeoSolid3 left, Touching, out _));
            Assert.Equal(2, left.SplitShells(Fine).Length);
            Assert.Equal(500.0 * 1000.0 * 300.0 + 0.02 * 1000.0 * 300.0, left.GetVolume(Fine), 3);
        }

        [Fact]
        public void AFaceAtASlantPartlyFurtherThanTheContact_IsNotMoved()
        {
            // The tool's face runs from 0.002 inside the slab's at y = -100 to 0.03 inside at y = 1100.
            var plan = new[] { new GeoPoint3(0.002, -100, 0), new GeoPoint3(500, -100, 0), new GeoPoint3(500, 1100, 0), new GeoPoint3(0.03, 1100, 0) };
            GeoSolid3 tool = Prism(plan, -100, 400);

            Assert.True(Slab().TrySubtract(tool, out GeoSolid3 plain, Plain, out _));
            Assert.True(Slab().TrySubtract(tool, out GeoSolid3 touching, Touching, out _));
            Assert.Equal(plain.GetVolume(Fine), touching.GetVolume(Fine), 6);
        }

        [Fact]
        public void NoContactAndNoFallback_IsTheBooleanWithinTheTolerance()
        {
            GeoSolid3 tool = Box(300, 300, -100, 700, 700, 400);

            Assert.True(Slab().TrySubtract(tool, out GeoSolid3 withOptions, Plain, out BooleanOutcome a));
            Assert.True(Slab().TrySubtract(tool, out GeoSolid3 withTolerance, Fine, out BooleanOutcome b));
            Assert.Equal(b, a);
            Assert.Equal(withTolerance, withOptions);
        }

        [Fact]
        public void TakingAwayOneAfterAnother_TakesEachAsTouchingWhereItTouches()
        {
            // Four blocks round a slab's edges, each a few thousandths into or off it, and one through its middle.
            GeoSolid3[] tools =
            {
                Box(-500, -100, -100, 0.004, 1100, 400),
                Box(999.997, -100, -100, 1500, 1100, 400),
                Box(-100, -500, -100, 1100, 0.006, 400),
                Box(-100, 999.992, -100, 1100, 1500, 400),
                Box(0.003, 400, 0.002, 1000, 600, 299.995),
            };

            Assert.True(Slab().TrySubtractAll(tools, out GeoSolid3 left, Touching, out SubtractReport report));
            Assert.Equal(5, report.Tools);
            Assert.Equal(5, report.Taken);
            Assert.Empty(report.Skipped);
            Assert.True(left.Validate(Fine).IsValid, left.Validate(Fine).ToString());
            Assert.Equal(2, left.SplitShells(Fine).Length);
            Assert.Equal(1000.0 * 800.0 * 300.0, left.GetVolume(Fine), 3);
            Assert.Equal(2.0 * 2.0 * (1000.0 * 400.0 + 1000.0 * 300.0 + 400.0 * 300.0), left.GetSurfaceArea(Fine), 3);
        }

        [Fact]
        public void TakingAwayATool_ThatTakesAll_LeavesNothing()
        {
            GeoSolid3[] tools = { Box(300, 300, -100, 700, 700, 400), Box(-1, -1, -1, 1001, 1001, 301), Box(0, 0, 0, 10, 10, 10) };

            Assert.False(Slab().TrySubtractAll(tools, out GeoSolid3 left, Touching, out SubtractReport report));
            Assert.Null(left);
            Assert.Equal(2, report.Tools);
            Assert.Equal(2, report.Taken);
        }

        [Fact]
        public void TakingAwayATool_WhoseCutIsNotValid_SkipsIt()
        {
            // A block through the slab with a side missing is no body; what it leaves of the slab is open, and the slab is
            // kept as it was.
            GeoSolid3 block = Box(300, 300, -100, 700, 700, 400);
            var open = new GeoSolid3(block.Faces.Where(face => face.Normal.X > -0.5));
            GeoSolid3[] tools = { open, Box(-100, -100, -100, 100, 1100, 400) };

            Assert.True(Slab().TrySubtractAll(tools, out GeoSolid3 left, Touching, out SubtractReport report));
            Assert.Equal(new[] { 0 }, report.Skipped);
            Assert.Equal(1, report.Taken);
            Assert.True(left.Validate(Fine).IsValid, left.Validate(Fine).ToString());
            Assert.Equal(900.0 * 1000.0 * 300.0, left.GetVolume(Fine), 3);
        }

        [Fact]
        public void AFallback_LeavesAValidResultAsItIs()
        {
            var withFallback = new SolidBooleanOptions(Fine, 0.01, new Tolerance(0.01, 0.01, Tolerance.DefaultEqualAngleRad, 0.01));
            GeoSolid3 tool = Box(300, 300, -100, 700, 700, 400);

            Assert.True(Slab().TrySubtract(tool, out GeoSolid3 a, withFallback, out _));
            Assert.True(Slab().TrySubtract(tool, out GeoSolid3 b, Touching, out _));
            Assert.Equal(b, a);
        }

        [Fact]
        public void Options_RefuseAContactThatIsNoDistance()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SolidBooleanOptions(Fine, -0.01));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SolidBooleanOptions(Fine, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SolidBooleanOptions(Fine, double.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => Slab().TrySubtract(Slab(), out _, (SolidBooleanOptions)null, out _));
            Assert.Throws<ArgumentException>(() => Slab().TrySubtractAll(new GeoSolid3[] { null }, out _, Touching, out _));
        }

        [Fact]
        public void Options_AreEqualByWhatTheyHold()
        {
            var fallback = new Tolerance(0.01, 0.01, Tolerance.DefaultEqualAngleRad, 0.01);
            Assert.Equal(new SolidBooleanOptions(Fine, 0.01, fallback), new SolidBooleanOptions(Fine, 0.01, fallback));
            Assert.Equal(new SolidBooleanOptions(Fine, 0.01, fallback).GetHashCode(), new SolidBooleanOptions(Fine, 0.01, fallback).GetHashCode());
            Assert.NotEqual(new SolidBooleanOptions(Fine, 0.01), new SolidBooleanOptions(Fine, 0.02));
            Assert.NotEqual(new SolidBooleanOptions(Fine, 0.01), new SolidBooleanOptions(Fine, 0.01, fallback));
            Assert.Equal(new SolidBooleanOptions(Fine), SolidBooleanOptions.Default);
            Assert.Contains("Contact: 0.01", new SolidBooleanOptions(Fine, 0.01).ToString(), StringComparison.Ordinal);
        }

        private static GeoSolid3 Prism(IList<GeoPoint3> plan, double z0, double z1)
        {
            var exact = new Tolerance(1E-6, 1E-10, Tolerance.DefaultEqualAngleRad, 1E-6);
            var down = plan.Select(p => new GeoPoint3(p.X, p.Y, z0)).ToArray();
            var up = plan.Select(p => new GeoPoint3(p.X, p.Y, z1)).ToArray();
            var faces = new List<GeoFace3>
            {
                new GeoFace3(new GeoPolygon3(down.Reverse().ToArray(), exact), null, exact),
                new GeoFace3(new GeoPolygon3(up, exact), null, exact),
            };

            for (int i = 0; i < plan.Count; i++)
            {
                int j = (i + 1) % plan.Count;
                faces.Add(new GeoFace3(new GeoPolygon3(new[] { down[i], down[j], up[j], up[i] }, exact), null, exact));
            }

            return new GeoSolid3(faces);
        }
    }
}
