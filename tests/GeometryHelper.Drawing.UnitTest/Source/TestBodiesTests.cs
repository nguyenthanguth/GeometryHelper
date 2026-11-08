using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Drawing.UnitTest
{
    public class TestBodiesTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        public static IEnumerable<object[]> Bodies => new[] { "TC1", "TC2", "TC3", "TC4", "TC5", "TC6", "TC7", "TC8", "TC9" }.Select(n => new object[] { n });

        private static TestBody Make(string name)
        {
            switch (name)
            {
                case "TC1": return TestBodies.TC1();
                case "TC2": return TestBodies.TC2();
                case "TC3": return TestBodies.TC3();
                case "TC4": return TestBodies.TC4();
                case "TC5": return TestBodies.TC5();
                case "TC6": return TestBodies.TC6();
                case "TC7": return TestBodies.TC7();
                case "TC8": return TestBodies.TC8();
                default: return TestBodies.TC9();
            }
        }

        [Theory]
        [MemberData(nameof(Bodies))]
        public void EachObjectOfATestBodyIsClosedAndValidAndHoldsTheVolumeWorkedOutByHand(string name)
        {
            foreach (TestPart part in Make(name).Parts)
            {
                AssertPart(part);
            }
        }

        [Theory]
        [MemberData(nameof(Bodies))]
        public void EachObjectOfATestBodyHasTheEdgesWorkedOutByHand(string name)
        {
            foreach (TestPart part in Make(name).Parts)
            {
                Assert.Equal(part.Object.Id + ": " + part.Edges, part.Object.Id + ": " + CountEdges(part.Object.Solid));
            }
        }

        [Theory]
        [MemberData(nameof(Bodies))]
        public void TheCentreOfEachHoleIsOutsideTheMaterialAndAPointBesideItInside(string name)
        {
            foreach (TestPart part in Make(name).Parts)
            {
                foreach (GeoPoint3 centre in part.HoleCentres)
                {
                    // Beside the hole, a quarter of its diameter out from its wall, along the frame's X or Y, whichever
                    // is square to the axis of the hole.
                    GeoVector3 across = part.Object.Frame.XAxis;
                    GeoPoint3 beside = centre.Add(across.Multiply(part.HoleDiameter * 0.75));

                    Assert.False(part.Object.Solid.Contains(centre, Tolerance), part.Object.Id + " at " + centre);
                    Assert.True(part.Object.Solid.Contains(beside, Tolerance), part.Object.Id + " at " + beside);
                }
            }
        }

        [Fact]
        public void TheHoleCentresOfTC1And2AreWhereThePlanPutsThem()
        {
            TestPart plate = TestBodies.TC1()["TC1-plate"];
            Assert.Equal(22.0, plate.HoleDiameter);
            Assert.Equal(4, plate.HoleCentres.Count);
            Assert.Contains(plate.HoleCentres, c => c.IsEqualTo(new GeoPoint3(250, 150, 10), Tolerance));

            TestPart endPlate = TestBodies.TC2()["TC2-endplate"];
            Assert.Equal(6, endPlate.HoleCentres.Count);
            Assert.Contains(endPlate.HoleCentres, c => c.IsEqualTo(new GeoPoint3(3010, -50, 100), Tolerance));
        }

        [Fact]
        public void ASmallFrameOfTC10IsClosedAndValidAndEachAssemblyHoldsWhatTC5Holds()
        {
            TestBody frame = TestBodies.TC10(3, 2);

            Assert.Equal(60, frame.Parts.Count);
            foreach (TestPart part in frame.Parts)
            {
                AssertPart(part);
            }

            Assert.Equal(6 * TestBodies.TC5().Volume, frame.Volume, 3);
        }

        [Fact]
        public void TheFullFrameOfTC10Has200AssembliesOf10ObjectsWithIdsInGridOrder()
        {
            TestBody frame = TestBodies.TC10();
            DrawingSource source = frame.ToSource();

            Assert.Equal(2000, source.Objects.Count);
            Assert.Equal(200, source.Objects.Count(o => o.IsMain));
            Assert.Equal("TC10-000-000-base", source.Objects[0].Id);
            Assert.Equal("TC10-009-019-stiffener-4", source.Objects[1999].Id);

            // The last is the stiffener on the web's -Y side of the assembly in the far corner of the grid.
            GeoAabb3 box = source.Objects[1999].Solid.GetAabb();
            Assert.True(box.Min.IsEqualTo(new GeoPoint3(6000 * 19 - 5, 6000 * 9 - 200, 25), Tolerance), box.ToString());
            Assert.True(box.Max.IsEqualTo(new GeoPoint3(6000 * 19 + 5, 6000 * 9 - 4.5, 175), Tolerance), box.ToString());
            Assert.Equal(200 * TestBodies.TC5().Volume, frame.Volume, 1);
        }

        private static void AssertPart(TestPart part)
        {
            GeoSolid3 solid = part.Object.Solid;
            SolidValidation3 check = solid.Validate(Tolerance);

            Assert.True(check.IsValid, part.Object.Id + ": " + check);
            Assert.True(solid.IsClosed(Tolerance), part.Object.Id);

            // A volume within what the faces moved by the point tolerance would sweep.
            double slack = Tolerance.EqualPoint * solid.GetSurfaceArea(Tolerance);
            Assert.InRange(solid.GetVolume(Tolerance), part.Volume - slack, part.Volume + slack);
        }

        /// <summary>
        /// The edges of a body: its faces with coplanar neighbours merged, each loop counted by its corners off the line
        /// through the corners either side, each edge then counted from both its faces.
        /// </summary>
        private static int CountEdges(GeoSolid3 solid)
        {
            int ends = 0;

            foreach (GeoFace3 face in solid.MergeCoplanarFaces(Tolerance).Faces)
            {
                ends += Corners(face.Boundary);

                foreach (GeoPolygon3 hole in face.Holes)
                {
                    ends += Corners(hole);
                }
            }

            Assert.Equal(0, ends % 2);
            return ends / 2;
        }

        private static int Corners(GeoPolygon3 loop)
        {
            IReadOnlyList<GeoPoint3> v = loop.Vertices;
            int count = 0;

            for (int i = 0; i < v.Count; i++)
            {
                GeoPoint3 previous = v[(i + v.Count - 1) % v.Count];
                GeoVector3 along = previous.GetVectorTo(v[(i + 1) % v.Count]);
                double off = previous.GetVectorTo(v[i]).CrossProduct(along).Length / along.Length;

                if (off > Tolerance.EqualPoint)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
