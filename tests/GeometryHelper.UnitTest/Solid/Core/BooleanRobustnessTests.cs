using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Booleans on bodies as a model gives them: far from the origin, turned off the axes, cut again and again. Each has
    /// to give a closed body or say it cannot give one; none may throw.
    /// </summary>
    public class BooleanRobustnessTests
    {
        [Fact]
        public void TheBeamTakesEachOfItsOpeningsInTurn()
        {
            // IfcConvert cuts openings this way when the geometry engine cannot: one after another, each from what the last
            // one left. Seven of the sixteen threw, the intersection with the fourth threw as well, and of those that went
            // through, ten left the beam open.
            Tolerance tolerance = Tolerance.Default;
            GeoSolid3 beam = BeamWithOpenings.Read(BeamWithOpenings.Beam);

            for (int k = 0; k < BeamWithOpenings.Openings.Length; k++)
            {
                GeoSolid3 opening = BeamWithOpenings.Read(BeamWithOpenings.Openings[k]);
                double common = beam.TryIntersect(opening, out GeoSolid3 shared, tolerance) ? shared.GetVolume() : 0.0;

                Assert.True(beam.TrySubtract(opening, out GeoSolid3 rest, tolerance), $"opening {k}");

                // What the opening takes away is what the two have in common.
                Assert.InRange(beam.GetVolume() - rest.GetVolume(), common - 5E-5 * beam.GetVolume(), common + 5E-5 * beam.GetVolume());

                // The fifteenth left a seam where two faces of the web run a hundredth of a millimetre apart, the point
                // tolerance itself, and the sixteenth was cut from that; slivers thinner than twice the tolerance are
                // kept and judged by their middle now, and the beam stays closed throughout.
                Assert.True(rest.IsClosed(tolerance), $"open after opening {k}");

                beam = rest;
            }

            // The geometry engine, cutting all sixteen on the exact B-rep, leaves 25 137 804 mm3.
            Assert.InRange(beam.GetVolume(), 25137804.0 * 0.9999, 25137804.0 * 1.0001);
        }

        [Fact]
        public void AnInteriorPointIsFoundPastTrianglesFinerThanTheDefaultTolerance()
        {
            // A U 100 across and 0.2 thick, its middle outside it, with one corner cut off 0.02 mm: the face that leaves is
            // two triangles too small for the default tolerance to give a direction, and they come first. Worked at a
            // ten-thousandth, the search for a point inside took their normals at the default, and threw.
            var fine = new Tolerance(1E-4, 1E-4, Tolerance.DefaultEqualAngleRad, 1E-4);
            GeoPoint3[] outline =
            {
                P(0.02, 0), P(100, 0), P(100, 100), P(80, 100), P(80, 20), P(20, 20), P(20, 100), P(0, 100), P(0, 0.02),
            };
            GeoSolid3 u = Prism(outline, 0.2, fine);

            Assert.NotEqual(PointLocation.Inside, u.Locate(u.GetCentroid(), fine));
            Assert.True(Boolean3.TryGetInteriorPoint(u, fine, out GeoPoint3 inside));
            Assert.Equal(PointLocation.Inside, u.Locate(inside, fine));
        }

        private static GeoPoint3 P(double x, double y) => new GeoPoint3(x, y, 0.0);

        /// <summary>
        /// A prism standing on an outline wound counter-clockwise, the side face of the last edge first.
        /// </summary>
        private static GeoSolid3 Prism(GeoPoint3[] outline, double height, Tolerance tolerance)
        {
            var up = new GeoVector3(0, 0, height);
            var faces = new List<GeoFace3>();

            for (int i = outline.Length - 1; i >= 0; i--)
            {
                GeoPoint3 a = outline[i];
                GeoPoint3 b = outline[(i + 1) % outline.Length];
                faces.Add(new GeoFace3(new GeoPolygon3(new[] { a, b, b.Add(up), a.Add(up) }, tolerance), null, tolerance));
            }

            faces.Add(new GeoFace3(new GeoPolygon3(outline.Reverse(), tolerance), null, tolerance));
            faces.Add(new GeoFace3(new GeoPolygon3(outline.Select(p => p.Add(up)), tolerance), null, tolerance));
            return new GeoSolid3(faces);
        }
    }
}
