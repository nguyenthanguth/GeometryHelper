using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The mass properties of a body, integrated exactly over its faces.
    /// </summary>
    /// <remarks>
    /// The divergence theorem turns each integral over the volume — of 1, x, y, z, and their squares and products
    /// — into a sum over the triangles of its surface, worked in closed form after Eberly's "Polyhedral Mass
    /// Properties". The coordinates are taken from the middle of the body's box, so a part far from the origin
    /// loses nothing to the size of its coordinates. The material is what is integrated: openings come out.
    /// </remarks>
    internal static class Mass3
    {
        internal static MassProperties3 Of(GeoSolid3 solid, double density, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            Guard.Positive(density, nameof(density), "A density has to be a positive number.");

            GeoSolid3 material = Material3.Whole(solid, tolerance);
            GeoPoint3 origin = material.GetAabb().Center;
            var sums = new double[10];

            foreach (GeoTriangle3 triangle in material.Triangulate(tolerance))
            {
                Accumulate(sums, origin.GetVectorTo(triangle.A), origin.GetVectorTo(triangle.B), origin.GetVectorTo(triangle.C));
            }

            double[] scale = { 1.0 / 6, 1.0 / 24, 1.0 / 24, 1.0 / 24, 1.0 / 60, 1.0 / 60, 1.0 / 60, 1.0 / 120, 1.0 / 120, 1.0 / 120 };

            // A surface wound inwards integrates to the negative of everything; the body is the same body.
            double sign = sums[0] < 0.0 ? -1.0 : 1.0;

            for (int i = 0; i < sums.Length; i++)
            {
                sums[i] *= scale[i] * sign;
            }

            double volume = sums[0];
            double surface = 0.0;

            foreach (GeoFace3 face in material.Faces)
            {
                surface += face.Area;
            }

            if (!(volume > 0.0))
            {
                return new MassProperties3(density, 0.0, origin, surface, 0, 0, 0, 0, 0, 0, new double[3],
                    new[] { GeoVector3.XAxis, GeoVector3.YAxis, GeoVector3.ZAxis });
            }

            double cx = sums[1] / volume, cy = sums[2] / volume, cz = sums[3] / volume;

            double ixx = density * (sums[5] + sums[6] - volume * (cy * cy + cz * cz));
            double iyy = density * (sums[4] + sums[6] - volume * (cz * cz + cx * cx));
            double izz = density * (sums[4] + sums[5] - volume * (cx * cx + cy * cy));
            double ixy = density * (sums[7] - volume * cx * cy);
            double iyz = density * (sums[8] - volume * cy * cz);
            double izx = density * (sums[9] - volume * cz * cx);

            var tensor = new double[,]
            {
                { ixx, -ixy, -izx },
                { -ixy, iyy, -iyz },
                { -izx, -iyz, izz },
            };

            Eigen(tensor, out double[] moments, out GeoVector3[] axes);

            return new MassProperties3(density, volume, origin.Add(new GeoVector3(cx, cy, cz)), surface,
                ixx, iyy, izz, ixy, iyz, izx, moments, axes);
        }

        /// <summary>
        /// Adds one triangle's share of the ten volume integrals, before their constant factors.
        /// </summary>
        private static void Accumulate(double[] sums, GeoVector3 p0, GeoVector3 p1, GeoVector3 p2)
        {
            double a1 = p1.X - p0.X, b1 = p1.Y - p0.Y, c1 = p1.Z - p0.Z;
            double a2 = p2.X - p0.X, b2 = p2.Y - p0.Y, c2 = p2.Z - p0.Z;
            double d0 = b1 * c2 - b2 * c1;
            double d1 = a2 * c1 - a1 * c2;
            double d2 = a1 * b2 - a2 * b1;

            Terms(p0.X, p1.X, p2.X, out double f1x, out double f2x, out double f3x, out double g0x, out double g1x, out double g2x);
            Terms(p0.Y, p1.Y, p2.Y, out _, out double f2y, out double f3y, out double g0y, out double g1y, out double g2y);
            Terms(p0.Z, p1.Z, p2.Z, out _, out double f2z, out double f3z, out double g0z, out double g1z, out double g2z);

            sums[0] += d0 * f1x;
            sums[1] += d0 * f2x;
            sums[2] += d1 * f2y;
            sums[3] += d2 * f2z;
            sums[4] += d0 * f3x;
            sums[5] += d1 * f3y;
            sums[6] += d2 * f3z;
            sums[7] += d0 * (p0.Y * g0x + p1.Y * g1x + p2.Y * g2x);
            sums[8] += d1 * (p0.Z * g0y + p1.Z * g1y + p2.Z * g2y);
            sums[9] += d2 * (p0.X * g0z + p1.X * g1z + p2.X * g2z);
        }

        private static void Terms(double w0, double w1, double w2, out double f1, out double f2, out double f3, out double g0, out double g1, out double g2)
        {
            double t0 = w0 + w1;
            f1 = t0 + w2;
            double t1 = w0 * w0;
            double t2 = t1 + w1 * t0;
            f2 = t2 + w2 * f1;
            f3 = w0 * t1 + w1 * t2 + w2 * f2;
            g0 = f2 + w0 * (f1 + w0);
            g1 = f2 + w1 * (f1 + w1);
            g2 = f2 + w2 * (f1 + w2);
        }

        /// <summary>
        /// The eigenvalues of a symmetric 3 × 3 matrix, smallest first, and their eigenvectors, right-handed —
        /// by Jacobi rotations, which converge on a matrix this small in a handful of sweeps.
        /// </summary>
        private static void Eigen(double[,] matrix, out double[] values, out GeoVector3[] vectors)
        {
            var a = (double[,])matrix.Clone();
            var v = new double[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
            double size = Math.Abs(a[0, 0]) + Math.Abs(a[1, 1]) + Math.Abs(a[2, 2]);

            for (int sweep = 0; sweep < 64; sweep++)
            {
                double off = Math.Abs(a[0, 1]) + Math.Abs(a[0, 2]) + Math.Abs(a[1, 2]);

                if (off <= 1E-15 * size)
                {
                    break;
                }

                foreach ((int p, int q) in new[] { (0, 1), (0, 2), (1, 2) })
                {
                    if (Math.Abs(a[p, q]) <= 1E-300)
                    {
                        continue;
                    }

                    double theta = (a[q, q] - a[p, p]) / (2.0 * a[p, q]);
                    double t = Math.Sign(theta == 0.0 ? 1.0 : theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1.0));
                    double c = 1.0 / Math.Sqrt(t * t + 1.0);
                    double s = t * c;

                    for (int k = 0; k < 3; k++)
                    {
                        double akp = a[k, p], akq = a[k, q];
                        a[k, p] = c * akp - s * akq;
                        a[k, q] = s * akp + c * akq;
                    }

                    for (int k = 0; k < 3; k++)
                    {
                        double apk = a[p, k], aqk = a[q, k];
                        a[p, k] = c * apk - s * aqk;
                        a[q, k] = s * apk + c * aqk;
                    }

                    for (int k = 0; k < 3; k++)
                    {
                        double vkp = v[k, p], vkq = v[k, q];
                        v[k, p] = c * vkp - s * vkq;
                        v[k, q] = s * vkp + c * vkq;
                    }
                }
            }

            var order = new List<int> { 0, 1, 2 };
            order.Sort((i, j) => a[i, i].CompareTo(a[j, j]));

            values = new double[3];
            vectors = new GeoVector3[3];

            for (int n = 0; n < 3; n++)
            {
                int i = order[n];
                values[n] = a[i, i];
                vectors[n] = new GeoVector3(v[0, i], v[1, i], v[2, i]).Normalize();
            }

            // The third axis follows from the first two, so the set is right-handed.
            vectors[2] = vectors[0].CrossProduct(vectors[1]).Normalize();
        }
    }
}
