using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Unions of an I column whose faces come in many strips, every edge of its profile cut into collinear pieces, and an I
    /// beam framing a fifth of the tolerance into its flange, the beam's flange running past the column's edge: the union
    /// holds what the union of the same column in one piece holds, whatever the number of strips.
    /// </summary>
    public class Boolean3FaultBigColTests
    {
        private static GeoPolygon2 Profile(double h, double b, double tw, double tf, int pieces)
        {
            var corners = new List<GeoPoint2>
            {
                new GeoPoint2(-b / 2, -h / 2), new GeoPoint2(b / 2, -h / 2), new GeoPoint2(b / 2, -h / 2 + tf), new GeoPoint2(tw / 2, -h / 2 + tf),
                new GeoPoint2(tw / 2, h / 2 - tf), new GeoPoint2(b / 2, h / 2 - tf), new GeoPoint2(b / 2, h / 2), new GeoPoint2(-b / 2, h / 2),
                new GeoPoint2(-b / 2, h / 2 - tf), new GeoPoint2(-tw / 2, h / 2 - tf), new GeoPoint2(-tw / 2, -h / 2 + tf), new GeoPoint2(-b / 2, -h / 2 + tf),
            };
            var profile = new List<GeoPoint2>();

            for (int i = 0; i < corners.Count; i++)
            {
                GeoPoint2 p = corners[i];
                GeoPoint2 q = corners[(i + 1) % corners.Count];

                for (int j = 0; j < pieces; j++)
                {
                    profile.Add(new GeoPoint2(p.X + (q.X - p.X) * j / pieces, p.Y + (q.Y - p.Y) * j / pieces));
                }
            }

            return new GeoPolygon2(profile);
        }

        [Theory]
        [InlineData(1, false)]
        [InlineData(150, false)]
        [InlineData(300, true)]
        public void AColumnWhoseFacesComeInStrips_IsJoinedToABeamFramingIntoItsFlangeWithoutLosingVolume(int pieces, bool beamFirst)
        {
            // A column 300 by 300, web 11, flanges 19, 4 000 long, every edge of its profile cut into pieces, and a beam 500 by
            // 180, web 10, flanges 16, 3 000 long, its end 0.002 into the column's flange and its flanges 60 past the column's
            // edge, turned. The union in one piece holds the two less 18.96; in 150 and 300 strips it held the two less 25.80 and
            // 98.92, closed and valid, where the overlap is 17.04 and the beam's whole end 20.88: the weld closing the step of the
            // beam's end took a corner of the column's face to the beam's, off the face's plane, weighing it by the triangle the
            // corner makes with its neighbours, a strip apart, where the face reaches the whole flange.
            var tolerance = new Tolerance(0.01, 1E-5, 1E-5, 0.01);
            const double d = 0.002;
            GeoTransform3 turn = GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(-0.012101969920146283, 0.49921442428567187, -0.049937243363790795), 4.260549139352771);
            GeoSolid3 column = GeoSolid3.Extrude(Profile(300, 300, 11, 19, pieces), new GeoCoordinateSystem3(GeoPoint3.Origin, new GeoVector3(0, 1, 0), new GeoVector3(-1, 0, 0)), 4000, tolerance).TransformBy(turn);
            GeoSolid3 beam = GeoSolid3.Extrude(Profile(500, 180, 10, 16, 1), new GeoCoordinateSystem3(new GeoPoint3(150 - d, 120, 3000), new GeoVector3(0, 1, 0), new GeoVector3(0, 0, 1)), 3000, tolerance).TransformBy(turn);
            double two = 14282.0 * 4000 + 10440.0 * 3000;

            GeoSolid3 union;
            Assert.True(beamFirst ? beam.TryUnion(column, out union, tolerance) : column.TryUnion(beam, out union, tolerance));
            Assert.True(union.Validate(tolerance).IsValid, union.Validate(tolerance).ToString());

            // Between the two less the beam's whole end, moved onto the column's face, and the two less their overlap.
            Assert.InRange(union.GetVolume(tolerance), two - d * 10440 - 1E-3, two - d * 8520 + 1E-3);
        }
    }
}
