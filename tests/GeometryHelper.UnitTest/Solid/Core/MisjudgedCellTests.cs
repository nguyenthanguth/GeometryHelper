using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// A cell a plane crossed and could not cut is judged by one point, and is misjudged where the other body lies one way to
    /// its material on one side of the plane and the other way to the rest; the check for it needs a point on each side.
    /// </summary>
    public class MisjudgedCellTests
    {
        private static readonly Tolerance Fine = Tolerance.Default;

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        private static readonly GeoPlane3 NearTheTop = new GeoPlane3(new GeoPoint3(0, 0, 9.5), GeoVector3.ZAxis);

        [Fact]
        public void APointAboveAPlaneNearTheTopOfABox_IsFound()
        {
            // The only triangles of the box above the plane are its top's, and the middle of the box under them is below
            // the plane: the point is the middle of what lies above it.
            Assert.True(Boolean3.TryGetPointBeside(Box(0, 0, 0, 10, 10, 10), NearTheTop, 1.0, Fine, out GeoPoint3 above));
            Assert.InRange(above.Z, 9.5 + Fine.EqualPlanar, 10.0);

            Assert.True(Boolean3.TryGetPointBeside(Box(0, 0, 0, 10, 10, 10), NearTheTop, -1.0, Fine, out GeoPoint3 below));
            Assert.InRange(below.Z, 0.0, 9.5 - Fine.EqualPlanar);
        }

        [Fact]
        public void ACellWhoseTopOnlyLiesBeyondTheOther_IsMisjudged()
        {
            // A slab of a Tekla model less a tool whose sloping top a plane of the slab's could not cut came out 18 litres
            // short: the cell along the plane was judged by a point within the tool, and the check found no point of it
            // beyond the tool's top, 131 millimetres thick there, and took the cut for consistent.
            GeoSolid3 cell = Box(0, 0, 0, 10, 10, 10);
            GeoSolid3 tool = Box(-1, -1, -1, 11, 11, 9.5);

            Assert.True(Boolean3.Misjudged(new List<GeoSolid3> { cell }, new List<GeoPlane3> { NearTheTop }, cell, tool, false, Fine));
        }

        [Fact]
        public void CutByEveryPlaneOfBoth_AMisjudgedCellKeepsTheOpenResultOfCuttingOneBody()
        {
            // A slab of a Tekla model less a tool beneath it, cut one body at a time, came out open either way, the tool cut
            // holding what it should; cut by every plane of both, the slab kept a cell the tool's top could not cut, judged
            // within the tool, and the cells glued closed and three litres short. The open result is kept then, for the weld
            // to close; where the cells are not misjudged, what they glue into closed is taken over it, as before.
            List<GeoFace3> cells = Box(0, 0, 0, 10, 10, 10).Faces.ToList();
            var open = new GeoSolid3(Box(0, 0, 0, 10, 10, 9).Faces.Skip(1));

            Assert.True(Boolean3.GlueOrKeep(new List<GeoFace3>(cells), null, open, true, Fine, out GeoSolid3 kept));
            Assert.Same(open, kept);

            Assert.True(Boolean3.GlueOrKeep(new List<GeoFace3>(cells), null, open, false, Fine, out GeoSolid3 glued));
            Assert.True(glued.IsClosed(Fine));
            Assert.Equal(1000.0, glued.GetVolume(Fine), 6);
        }

        [Fact]
        public void ACellLyingTheSameWayToTheOtherOnBothSides_IsNotMisjudged()
        {
            GeoSolid3 cell = Box(0, 0, 0, 10, 10, 10);

            Assert.False(Boolean3.Misjudged(new List<GeoSolid3> { cell }, new List<GeoPlane3> { NearTheTop }, cell, Box(-1, -1, -1, 11, 11, 11), false, Fine));
            Assert.False(Boolean3.Misjudged(new List<GeoSolid3> { cell }, new List<GeoPlane3> { NearTheTop }, cell, Box(20, 20, 20, 30, 30, 30), false, Fine));
        }
    }
}
