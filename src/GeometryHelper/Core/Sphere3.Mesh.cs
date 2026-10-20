using System;
using GeometryHelper.Geometry;
using GeometryHelper.Internal;

namespace GeometryHelper.Core
{
    // A ball meshed as an icosahedron whose twenty faces are each cut into n by n triangles by lines parallel to their sides, every
    // corner then pushed out to the sphere. The corners are worked out from the integer counts of the three corners of the
    // icosahedron face they lie between, summed in the order of the corners' indices, so that a corner on an edge between two
    // faces comes out the same to the bit from both and the mesh is closed with no weld.
    static partial class Sphere3
    {
        /// <summary>
        /// The share of the radius the chord tolerance is when none is given, as for a circle.
        /// </summary>
        private const double AutomaticShare = Tessellation.AutomaticChordRatio;

        /// <summary>
        /// The most triangles a ball is meshed into, as many as a curve's chords.
        /// </summary>
        internal const int MostTriangles = Tessellation.MostSegments;

        /// <summary>
        /// The most pieces an edge of the icosahedron is cut into: twenty n by n triangles come to no more than the most.
        /// </summary>
        private static readonly int MostFrequency = (int)Math.Floor(Math.Sqrt(MostTriangles / 20.0));

        private static readonly double Phi = (1.0 + Math.Sqrt(5.0)) / 2.0;

        // The corners of the icosahedron, on three golden rectangles, all the same distance from the centre.
        private static readonly double[][] Corners =
        {
            new[] { -1.0, Phi, 0.0 }, new[] { 1.0, Phi, 0.0 }, new[] { -1.0, -Phi, 0.0 }, new[] { 1.0, -Phi, 0.0 },
            new[] { 0.0, -1.0, Phi }, new[] { 0.0, 1.0, Phi }, new[] { 0.0, -1.0, -Phi }, new[] { 0.0, 1.0, -Phi },
            new[] { Phi, 0.0, -1.0 }, new[] { Phi, 0.0, 1.0 }, new[] { -Phi, 0.0, -1.0 }, new[] { -Phi, 0.0, 1.0 }
        };

        // The twenty faces as corner indices, each turned below to run counter-clockwise seen from outside.
        private static readonly int[][] Faces = TurnOutwards(new[]
        {
            new[] { 0, 11, 5 }, new[] { 0, 5, 1 }, new[] { 0, 1, 7 }, new[] { 0, 7, 10 }, new[] { 0, 10, 11 },
            new[] { 1, 5, 9 }, new[] { 5, 11, 4 }, new[] { 11, 10, 2 }, new[] { 10, 7, 6 }, new[] { 7, 1, 8 },
            new[] { 3, 9, 4 }, new[] { 3, 4, 2 }, new[] { 3, 2, 6 }, new[] { 3, 6, 8 }, new[] { 3, 8, 9 },
            new[] { 4, 9, 5 }, new[] { 2, 4, 11 }, new[] { 6, 2, 10 }, new[] { 8, 6, 7 }, new[] { 9, 8, 1 }
        });

        private static int[][] TurnOutwards(int[][] faces)
        {
            foreach (int[] face in faces)
            {
                double[] a = Corners[face[0]];
                double[] b = Corners[face[1]];
                double[] c = Corners[face[2]];
                double x = (b[1] - a[1]) * (c[2] - a[2]) - (b[2] - a[2]) * (c[1] - a[1]);
                double y = (b[2] - a[2]) * (c[0] - a[0]) - (b[0] - a[0]) * (c[2] - a[2]);
                double z = (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]);

                if (x * (a[0] + b[0] + c[0]) + y * (a[1] + b[1] + c[1]) + z * (a[2] + b[2] + c[2]) < 0.0)
                {
                    int swap = face[1];
                    face[1] = face[2];
                    face[2] = swap;
                }
            }

            return faces;
        }

        /// <summary>
        /// Gets the chord tolerance a ball is meshed within, which is the automatic share of the radius where none is given.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the ball is not valid.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is negative or not a finite number.</exception>
        internal static double MeshTolerance(GeoSphere3 sphere, double chordTolerance)
        {
            // A default ball has no radius, and a centre that is not a number would put every corner nowhere.
            Guard.Positive(sphere.Radius, "radius", "A sphere to be meshed must have a positive radius.");

            if (!sphere.Center.IsValid)
            {
                throw new ArgumentException("A sphere to be meshed must have a centre that is a number.", "sphere");
            }

            Guard.NonNegative(chordTolerance, nameof(chordTolerance), "The chord tolerance must be zero (automatic) or a positive distance.");

            return chordTolerance > 0.0 ? chordTolerance : sphere.Radius * AutomaticShare;
        }

        /// <summary>
        /// Meshes a ball into triangles, counter-clockwise seen from outside, none of them further from the sphere than the chord
        /// tolerance.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is negative or not a finite number, or needs more than 1 048 576 triangles.</exception>
        internal static GeoTriangle3[] Triangulate(GeoSphere3 sphere, double chordTolerance)
        {
            double tolerance = MeshTolerance(sphere, chordTolerance);
            int n = FrequencyFor(sphere.Radius, tolerance, chordTolerance);
            var triangles = new GeoTriangle3[20 * n * n];
            int count = 0;

            foreach (int[] face in Faces)
            {
                GeoPoint3[] grid = Grid(sphere, face, n);

                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n - i; j++)
                    {
                        triangles[count++] = new GeoTriangle3(grid[Slot(n, i, j)], grid[Slot(n, i + 1, j)], grid[Slot(n, i, j + 1)]);

                        if (i + j < n - 1)
                        {
                            triangles[count++] = new GeoTriangle3(grid[Slot(n, i + 1, j)], grid[Slot(n, i + 1, j + 1)], grid[Slot(n, i, j + 1)]);
                        }
                    }
                }
            }

            return triangles;
        }

        /// <summary>
        /// Gets the faces of the mesh of a ball, each a triangle with its normal and area given, for a body.
        /// </summary>
        internal static GeoFace3[] FacesOf(GeoSphere3 sphere, double chordTolerance)
        {
            GeoTriangle3[] triangles = Triangulate(sphere, chordTolerance);
            var faces = new GeoFace3[triangles.Length];

            for (int i = 0; i < triangles.Length; i++)
            {
                GeoVector3 twice = triangles[i].GetAreaVector();
                GeoPoint3[] corners = { triangles[i].A, triangles[i].B, triangles[i].C };

                faces[i] = new GeoFace3(GeoPolygon3.FromValidated(corners, twice.Multiply(1.0 / twice.Length), 0.5 * twice.Length));
            }

            return faces;
        }

        // Where the corner with the counts (i, j) lies in the list of a face's corners, row by row.
        private static int Slot(int n, int i, int j) => i * (n + 1) - i * (i - 1) / 2 + j;

        // The corners of one face cut n ways, on the sphere: counts of the face's three corners that sum to n, the first corner's
        // being what is left.
        private static GeoPoint3[] Grid(GeoSphere3 sphere, int[] face, int n)
        {
            var grid = new GeoPoint3[(n + 1) * (n + 2) / 2];

            for (int i = 0; i <= n; i++)
            {
                for (int j = 0; j <= n - i; j++)
                {
                    grid[Slot(n, i, j)] = Corner(sphere, face, n - i - j, i, j);
                }
            }

            return grid;
        }

        // A corner of the mesh, on the sphere, in the direction of the sum of the icosahedron's corners weighed by their counts. The
        // sum runs in the order of the corners' indices whichever face asks, so two faces sharing an edge work out its corners alike.
        private static GeoPoint3 Corner(GeoSphere3 sphere, int[] face, int countA, int countB, int countC)
        {
            int[] index = { face[0], face[1], face[2] };
            int[] count = { countA, countB, countC };

            for (int a = 0; a < 2; a++)
            {
                for (int b = a + 1; b < 3; b++)
                {
                    if (index[b] < index[a])
                    {
                        int swap = index[a];
                        index[a] = index[b];
                        index[b] = swap;
                        swap = count[a];
                        count[a] = count[b];
                        count[b] = swap;
                    }
                }
            }

            double x = 0.0;
            double y = 0.0;
            double z = 0.0;

            for (int k = 0; k < 3; k++)
            {
                x += count[k] * Corners[index[k]][0];
                y += count[k] * Corners[index[k]][1];
                z += count[k] * Corners[index[k]][2];
            }

            double length = Math.Sqrt(x * x + y * y + z * z);

            return new GeoPoint3(
                sphere.Center.X + sphere.Radius * (x / length),
                sphere.Center.Y + sphere.Radius * (y / length),
                sphere.Center.Z + sphere.Radius * (z / length));
        }

        // The fewest pieces to cut an edge of the icosahedron into so that no triangle strays further than the tolerance, found from
        // the largest of the triangles of one face (the twenty are the same under a turn) and never more than the mesh may have.
        private static int FrequencyFor(double radius, double tolerance, double asked)
        {
            // The sag of the chord of a triangle is at most R^2 / (r + sqrt(r^2 - R^2)) for its circumradius R, and a frequency n
            // cuts the largest triangle to about 0.77 of the radius over n, so the sag is about 0.3 r / n^2.
            double wanted = Math.Sqrt(0.3 * radius / tolerance);
            int n = wanted >= MostFrequency ? MostFrequency : Math.Max(1, (int)Math.Floor(0.95 * wanted));

            while (n <= MostFrequency && !Holds(radius, tolerance, n))
            {
                n++;
            }

            if (n > MostFrequency)
            {
                throw new ArgumentOutOfRangeException(
                    "chordTolerance",
                    asked,
                    "A chord tolerance of " + tolerance.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                    + " would cut the sphere into more than " + MostTriangles + " triangles; ask for a larger one.");
            }

            while (n > 1 && Holds(radius, tolerance, n - 1))
            {
                n--;
            }

            return n;
        }

        // Whether every triangle of the first face, cut n ways, lies within the tolerance of the sphere. The unit sphere is cut
        // and the tolerance scaled with it, so that the answer does not depend on where the ball is.
        private static bool Holds(double radius, double tolerance, int n)
        {
            var unit = new GeoSphere3(GeoPoint3.Origin, 1.0);
            double share = tolerance / radius;
            int[] face = Faces[0];
            GeoPoint3[] grid = Grid(unit, face, n);

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n - i; j++)
                {
                    if (Sag(grid[Slot(n, i, j)], grid[Slot(n, i + 1, j)], grid[Slot(n, i, j + 1)]) > share)
                    {
                        return false;
                    }

                    if (i + j < n - 1 && Sag(grid[Slot(n, i + 1, j)], grid[Slot(n, i + 1, j + 1)], grid[Slot(n, i, j + 1)]) > share)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // How far the plane of a triangle with its corners on the unit sphere stands inside it at the nearest: one less the distance of
        // the plane from the centre, R^2 / (1 + h) for the circumradius R and h = sqrt(1 - R^2). The largest gap between the triangle
        // and the sphere is no more than this, and is this where the circumcentre lies in the triangle.
        private static double Sag(GeoPoint3 a, GeoPoint3 b, GeoPoint3 c)
        {
            double ab = Distance3.DistanceTo(a, b);
            double bc = Distance3.DistanceTo(b, c);
            double ca = Distance3.DistanceTo(c, a);
            double twiceArea = a.GetVectorTo(b).CrossProduct(a.GetVectorTo(c)).Length;
            double circumradius = ab * bc * ca / (2.0 * twiceArea);
            double squared = circumradius * circumradius;

            return squared >= 1.0 ? 1.0 : squared / (1.0 + Math.Sqrt(1.0 - squared));
        }
    }
}
