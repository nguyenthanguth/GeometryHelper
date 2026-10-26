using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Geometry;

namespace GeometryHelper.Benchmarks.Scenarios
{
    /// <summary>
    /// Offsets and unions of the plane that Clipper2, which resolves them, once did with the square of their size: a
    /// comb offset with its teeth pointing up, squares united side by side in a row, and a zigzag united with its minima
    /// at many heights.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The comb has 20 000 teeth 2 wide and 100 long, 2 apart, pointing up from a back 100 deep, offset by -0.5 with
    /// mitred corners: each tooth's tip looked left along Clipper2's list of edges, past every tooth not yet done, for the
    /// nearest edge of an output path. Measured at 27340a1 it took 4.9 seconds, and 0.39 once that walk was kept short.
    /// </para>
    /// <para>
    /// The squares are 20 000 of side 10, side by side along X: every horizontal segment on the strip's top and bottom
    /// walked the whole 20 000 points along it, and the points left along that line were then taken out of a list one at
    /// a time, 20 seconds in all, now 0.32. The zigzag has 100 000 vertices 10 apart, its local minima at 50 heights a
    /// unit apart and its maxima at 30, under a back; each minimum went into the list of edges by a walk from the nearest
    /// one done before it, past the whole list again for every doubling of those done, 2.8 seconds, now 1.5. All three
    /// sign the same as they did at 27340a1.
    /// </para>
    /// </remarks>
    internal static class PlaneScenarios
    {
        private const int CombTeeth = 20000;
        private const int Squares = 20000;
        private const int ZigzagVertices = 100000;

        private static readonly Tolerance Within = Tolerance.Default;

        /// <summary>
        /// Every case of the plane.
        /// </summary>
        internal static IEnumerable<BenchmarkCase> All()
        {
            yield return new BenchmarkCase("Plane.Offset.CombUp", 4 * CombTeeth + 4, "corners", true, CombUp);
            yield return new BenchmarkCase("Plane.Union.Squares", Squares, "polygons", true, SquaresInARow);
            yield return new BenchmarkCase("Plane.Union.Zigzag", ZigzagVertices + 3, "corners", true, Zigzag);
        }

        /// <summary>
        /// The comb, its teeth pointing up from its back, offset inwards by a half.
        /// </summary>
        private static Fixture CombUp()
        {
            // Counter-clockwise: along the tips at y 0 and the gaps' ends at y -100, then the back's underside at -200.
            var corners = new List<GeoPoint2> { new GeoPoint2(0.0, 0.0) };
            double x = 0.0;

            for (int i = 0; i < CombTeeth; i++)
            {
                corners.Add(new GeoPoint2(x + 2.0, 0.0));
                corners.Add(new GeoPoint2(x + 2.0, 100.0));
                corners.Add(new GeoPoint2(x + 4.0, 100.0));
                corners.Add(new GeoPoint2(x + 4.0, 0.0));
                x += 4.0;
            }

            corners.Add(new GeoPoint2(x + 2.0, 0.0));
            corners.Add(new GeoPoint2(x + 2.0, 200.0));
            corners.Add(new GeoPoint2(0.0, 200.0));
            corners = corners.Select(p => new GeoPoint2(p.X, -p.Y)).Reverse().ToList();

            var comb = new GeoPolygon2(corners);

            return Fixture.Of(() => Offset2.Offset(comb, -0.5, OffsetOptions.Default, Within), Sign, Describe, null);
        }

        /// <summary>
        /// The squares of side 10, each sharing its sides with the next.
        /// </summary>
        private static Fixture SquaresInARow()
        {
            var squares = new List<GeoPolygon2>(Squares);

            for (int i = 0; i < Squares; i++)
            {
                double x = 10.0 * i;
                squares.Add(new GeoPolygon2(new List<GeoPoint2>
                {
                    new GeoPoint2(x, 0.0), new GeoPoint2(x + 10.0, 0.0), new GeoPoint2(x + 10.0, 10.0), new GeoPoint2(x, 10.0),
                }));
            }

            return Fixture.Of(() => Boolean2.Union(squares, Within), Sign, Describe, null);
        }

        /// <summary>
        /// The zigzag over a back, its minima at heights 1 000 to 1 049 and its maxima at 0 to 29, from a fixed pattern.
        /// </summary>
        private static Fixture Zigzag()
        {
            var corners = new List<GeoPoint2>(ZigzagVertices + 3);

            for (int i = 0; i <= ZigzagVertices; i++)
            {
                double y = i % 2 == 0 ? i / 2 % 30 : 1000 + i / 2 % 50;
                corners.Add(new GeoPoint2(10.0 * i, y));
            }

            corners.Add(new GeoPoint2(10.0 * ZigzagVertices, 2000.0));
            corners.Add(new GeoPoint2(0.0, 2000.0));
            GeoPolygon2[] zigzag = { new GeoPolygon2(corners) };

            return Fixture.Of(() => Boolean2.Union(zigzag, Within), Sign, Describe, null);
        }

        /// <summary>
        /// Signs polygons: how many, then each one's corners in order, to the last bit.
        /// </summary>
        private static void Sign(ResultSignature signature, GeoPolygon2[] polygons)
        {
            signature.Add(polygons.Length);

            foreach (GeoPolygon2 polygon in polygons)
            {
                Sign(signature, polygon);
            }
        }

        /// <summary>
        /// Signs faces: how many, then each one's boundary and its holes in order, every corner to the last bit.
        /// </summary>
        private static void Sign(ResultSignature signature, GeoFace2[] faces)
        {
            signature.Add(faces.Length);

            foreach (GeoFace2 face in faces)
            {
                Sign(signature, face.Boundary);
                signature.Add(face.Holes.Count);

                foreach (GeoPolygon2 hole in face.Holes)
                {
                    Sign(signature, hole);
                }
            }
        }

        private static void Sign(ResultSignature signature, GeoPolygon2 polygon)
        {
            signature.Add(polygon.Vertices.Count);

            foreach (GeoPoint2 corner in polygon.Vertices)
            {
                signature.Add(corner);
            }
        }

        private static string Describe(GeoPolygon2[] polygons)
            => Report.Count(polygons.Length) + " polygons of " + Report.Count(polygons.Sum(p => (long)p.Vertices.Count)) + " corners";

        private static string Describe(GeoFace2[] faces)
            => Report.Count(faces.Length) + " faces of " + Report.Count(faces.Sum(f => (long)f.Boundary.Vertices.Count + f.Holes.Sum(h => h.Vertices.Count))) + " corners";
    }
}
