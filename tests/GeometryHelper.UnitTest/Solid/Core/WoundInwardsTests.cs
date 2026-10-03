using System.Linq;
using GeometryHelper;
using GeometryHelper.Clash;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// A body wound inwards is the same body as wound outwards: every boolean, cut, contact and clash of it answers as of
    /// its outward self, and what they make is wound outwards.
    /// </summary>
    /// <remarks>
    /// The faces are expected to be wound outwards and nothing enforces it; the measures and the point queries already
    /// read a body either way. The booleans read the winding: the cells of a body wound inwards came back with its own
    /// faces facing in and the faces the cuts laid across them facing out, a tool wound inwards was taken for material,
    /// and two bodies lying face to face, one of them wound inwards, faced the same way and did not touch.
    /// </remarks>
    public class WoundInwardsTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(P(x0, y0, z0), P(x1, y1, z1)).ToObb().ToSolid();

        private static GeoSolid3 InsideOut(GeoSolid3 body) => new GeoSolid3(body.Faces.Select(f => f.Flip()), body.Openings.Select(InsideOut));

        private static GeoSolid3 Plate() => Box(0, 0, 0, 100, 100, 20);

        private static GeoSolid3 Wound(GeoSolid3 body, bool inwards) => inwards ? InsideOut(body) : body;

        private static void IsWoundOutwards(GeoSolid3 body)
        {
            Assert.True(body.IsClosed(Tolerance), $"{body} is not closed");
            Assert.True(ReferenceEquals(body, body.TurnOutwards()), $"{body} is wound inwards");
        }

        /// <summary>
        /// A tool through the plate, across its edge, inside it and through its top, with what the plate less it, the two
        /// together and their common part hold.
        /// </summary>
        private static void Tool(string name, out GeoSolid3 tool, out double difference, out double union, out double common)
        {
            switch (name)
            {
                case "through":
                    tool = Box(40, 40, -1, 60, 60, 21);
                    difference = 192000.0; union = 200800.0; common = 8000.0;
                    break;
                case "edge":
                    tool = Box(90, 40, -1, 110, 60, 21);
                    difference = 196000.0; union = 204800.0; common = 4000.0;
                    break;
                case "inside":
                    tool = Box(40, 40, 5, 60, 60, 15);
                    difference = 196000.0; union = 200000.0; common = 4000.0;
                    break;
                default:
                    tool = Box(40, 40, 10, 60, 60, 30);
                    difference = 196000.0; union = 204000.0; common = 4000.0;
                    break;
            }
        }

        public static TheoryData<string, bool, bool> Booleans()
        {
            var cases = new TheoryData<string, bool, bool>();

            foreach (string tool in new[] { "through", "edge", "inside", "top" })
            {
                cases.Add(tool, true, false);
                cases.Add(tool, false, true);
                cases.Add(tool, true, true);
            }

            return cases;
        }

        [Theory]
        [MemberData(nameof(Booleans))]
        public void ABooleanOfABodyWoundInwardsIsThatOfItsOutwardSelf(string name, bool plateInwards, bool toolInwards)
        {
            Tool(name, out GeoSolid3 tool, out double difference, out double union, out double common);
            GeoSolid3 a = Wound(Plate(), plateInwards), b = Wound(tool, toolInwards);

            Assert.True(Boolean3.TrySubtract(a, b, out GeoSolid3 rest, Tolerance));
            Assert.Equal(difference, rest.GetVolume(Tolerance), 6);
            IsWoundOutwards(rest);

            Assert.True(Boolean3.TryUnion(a, b, out GeoSolid3 both, Tolerance));
            Assert.Equal(union, both.GetVolume(Tolerance), 6);
            IsWoundOutwards(both);

            Assert.True(Boolean3.TryIntersect(a, b, out GeoSolid3 shared, Tolerance));
            Assert.Equal(common, shared.GetVolume(Tolerance), 6);
            IsWoundOutwards(shared);

            GeoSolid3 piece = Assert.Single(Boolean3.Intersect(a, b, Tolerance));
            Assert.Equal(common, piece.GetVolume(Tolerance), 6);
            IsWoundOutwards(piece);
        }

        [Fact]
        public void BodiesApartWoundEitherWayJoinIntoBoth()
        {
            // Side by side, the two were joined face for face, and the one wound inwards took its volume off the other's.
            Assert.True(Boolean3.TryUnion(Box(0, 0, 0, 10, 10, 10), InsideOut(Box(20, 0, 0, 30, 10, 10)), out GeoSolid3 both, Tolerance));

            Assert.Equal(2000.0, both.GetVolume(Tolerance), 6);
            Assert.Equal(2, both.SplitShells(Tolerance).Length);
            IsWoundOutwards(both);
        }

        [Fact]
        public void ABodyWoundInwardsSplitsIntoTheHalvesOfItsOutwardSelf()
        {
            // The halves were 50 000 and 16 667 for 150 000 and 50 000.
            GeoPlane3 cutter = new GeoPlane3(P(0, 0, 5), GeoVector3.ZAxis);

            Assert.True(InsideOut(Plate()).TrySplitBy(cutter, out GeoSolid3 above, out GeoSolid3 below, Tolerance));
            Assert.Equal(150000.0, above.GetVolume(Tolerance), 6);
            Assert.Equal(50000.0, below.GetVolume(Tolerance), 6);
            IsWoundOutwards(above);
            IsWoundOutwards(below);

            // An opening wound inwards in it as well: each half carries its part, and holds the plate's less the hole's.
            GeoSolid3 pierced = InsideOut(Plate().WithOpenings(new[] { Box(40, 40, -1, 60, 60, 21) }));

            Assert.True(Splition3.TrySplitBy(pierced, cutter, out GeoSolid3 upper, out GeoSolid3 lower, Tolerance));
            Assert.Equal(150000.0 - 20.0 * 20 * 15, upper.GetVolume(Tolerance), 6);
            Assert.Equal(50000.0 - 20.0 * 20 * 5, lower.GetVolume(Tolerance), 6);
            Assert.Single(upper.Openings);
            Assert.True(upper.Openings[0].GetSignedVolume() > 0.0);
        }

        public static TheoryData<bool, bool> EitherWound => new TheoryData<bool, bool> { { true, false }, { false, true }, { true, true } };

        [Theory]
        [MemberData(nameof(EitherWound))]
        public void BodiesWoundInwardsLieAgainstEachOtherAsTheirOutwardSelvesDo(bool plateInwards, bool blockInwards)
        {
            // A block standing on the plate: they lie against each other over its foot, 40 by 40, facing up out of the plate.
            GeoSolid3 a = Wound(Plate(), plateInwards), b = Wound(Box(20, 20, 20, 60, 60, 40), blockInwards);

            Assert.True(Boolean3.TryGetContact(a, b, out GeoFace3[] contact, Tolerance));
            Assert.Equal(1600.0, contact.Sum(f => f.Area), 6);
            Assert.All(contact, f => Assert.True(f.Normal.Z > 0.5, $"{f} faces {f.Normal}"));

            Assert.True(new GeoPreparedSolid3(a, Tolerance).TryGetContact(new GeoPreparedSolid3(b, Tolerance), out GeoFace3[] prepared, Tolerance));
            Assert.Equal(1600.0, prepared.Sum(f => f.Area), 6);
        }

        [Theory]
        [MemberData(nameof(EitherWound))]
        public void AClashOfBodiesWoundInwardsIsThatOfTheirOutwardSelves(bool plateInwards, bool ductInwards)
        {
            // The duct through the plate takes 20 by 20 by 20 of it; with one of them wound inwards the clash held a third.
            GeoSolid3 a = Wound(Plate(), plateInwards), b = Wound(Box(40, 40, -1, 60, 60, 21), ductInwards);

            ClashResult clash = Assert.Single(Clash3.Find(new[] { a, b }, ClashOptions.Default, Tolerance));
            Assert.Equal(ClashKind.Hard, clash.Kind);
            Assert.Equal(8000.0, clash.Volume, 6);
            Assert.Equal(20.0, clash.Depth, 6);

            var prepared = new GeoPreparedSolid3(a, Tolerance);
            Assert.True(ReferenceEquals(prepared.Material, prepared.Material.TurnOutwards()), "the prepared material is wound inwards");
            GeoSolid3 shared = Assert.Single(prepared.Intersect(new GeoPreparedSolid3(b, Tolerance), Tolerance));
            Assert.Equal(8000.0, shared.GetVolume(Tolerance), 6);
        }
    }
}
