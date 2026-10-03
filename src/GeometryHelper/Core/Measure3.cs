using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The volume, mass, centroid and surface area of a body, by the method asked, and every method side by side.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A body is measured over its faces. The divergence theorem makes its volume, centroid and moments sums over the
    /// triangles of its surface, each taken as a tetrahedron to the middle of the body's box, so that a part far from
    /// the origin loses nothing to the size of its coordinates. Over flat faces the sums are exact, and every
    /// <see cref="VolumeMethod"/> gives the same answer but for the rounding. A face a hair out of flat, as the planar
    /// tolerance lets one be, is no one surface, and there the methods part; <see cref="Compare(GeoSolid3, Tolerance)"/>
    /// says by how much, and how far the faces close.
    /// </para>
    /// <para>
    /// The volume, the mass, the centroid and the surface area are the material's: every opening is cut in first, so a
    /// bolt hole comes out of the weight, moves the centroid, and its walls add to the surface. A body its openings take
    /// whole holds nothing. The material is the one <see cref="GeoSolid3.GetVolume(Tolerance)"/> measures, cut once for
    /// a tolerance and kept by the body, so measuring it every way costs one cut. An opening that cannot be cut in is
    /// left in the material, the log warns of it, and <see cref="MeasureComparison3.OpeningsCut"/> says so.
    /// </para>
    /// <para>
    /// A density is the mass of a unit of volume in the model's units: for a model in millimetres weighed in kilograms,
    /// kilograms per cubic millimetre, which for steel is 7.85E-6.
    /// </para>
    /// </remarks>
    public static class Measure3
    {
        #region Volume

        /// <summary>
        /// Gets the volume of a body's material by a method, using the default tolerance.
        /// </summary>
        public static double Volume(GeoSolid3 solid, VolumeMethod method) => Volume(solid, method, Tolerance.Global);

        /// <summary>
        /// Gets the volume of a body's material by a method, its openings cut in first.
        /// </summary>
        /// <param name="solid">The body, closed; the volume of a surface that does not close means nothing.</param>
        /// <param name="method">How the faces are read.</param>
        /// <param name="tolerance">The tolerance the openings are cut in and the faces broken into triangles within.</param>
        /// <returns>The volume, positive whichever way the faces are wound; nought where the openings take all of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the body is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the method is not one of <see cref="VolumeMethod"/>.</exception>
        public static double Volume(GeoSolid3 solid, VolumeMethod method, Tolerance tolerance)
        {
            Check(method);
            GeoSolid3 material = Material(solid, tolerance, out _);

            return material == null ? 0.0 : Mass3.Volume(Mass3.Triangles(material, method, tolerance), material.GetAabb().Center);
        }

        #endregion

        #region Mass and centroid

        /// <summary>
        /// Gets the mass of a body's material at a density by a method, using the default tolerance.
        /// </summary>
        public static double Mass(GeoSolid3 solid, double density, VolumeMethod method) => Mass(solid, density, method, Tolerance.Global);

        /// <summary>
        /// Gets the mass of a body's material at a density by a method: the density times its volume.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="density">The mass of a unit of volume; in kilograms per cubic millimetre, steel is 7.85E-6.</param>
        /// <param name="method">How the faces are read.</param>
        /// <param name="tolerance">The tolerance the openings are cut in and the faces broken into triangles within.</param>
        /// <returns>The mass; nought where the openings take all of the body.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the body is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the density is not a positive number, or the method is not one of <see cref="VolumeMethod"/>.
        /// </exception>
        public static double Mass(GeoSolid3 solid, double density, VolumeMethod method, Tolerance tolerance)
        {
            Guard.Positive(density, nameof(density), "A density has to be a positive number.");

            return density * Volume(solid, method, tolerance);
        }

        /// <summary>
        /// Gets the centre of a body's material by a method, using the default tolerance.
        /// </summary>
        public static GeoPoint3 Centroid(GeoSolid3 solid, VolumeMethod method) => Centroid(solid, method, Tolerance.Global);

        /// <summary>
        /// Gets the centre of a body's material by a method, which is its centre of mass.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="method">How the faces are read.</param>
        /// <param name="tolerance">The tolerance the openings are cut in and the faces broken into triangles within.</param>
        /// <returns>The centroid; the middle of the body's box where it holds no volume.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the body is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the method is not one of <see cref="VolumeMethod"/>.</exception>
        public static GeoPoint3 Centroid(GeoSolid3 solid, VolumeMethod method, Tolerance tolerance)
            => MassProperties(solid, 1.0, method, tolerance).Centroid;

        /// <summary>
        /// Gets the mass properties of a body's material at a density by a method, using the default tolerance.
        /// </summary>
        public static MassProperties3 MassProperties(GeoSolid3 solid, double density, VolumeMethod method)
            => MassProperties(solid, density, method, Tolerance.Global);

        /// <summary>
        /// Gets the mass properties of a body's material at a density by a method: volume, mass, centroid, the moments and
        /// products of inertia about the centroid, and the principal moments and axes.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="density">The mass of a unit of volume; in kilograms per cubic millimetre, steel is 7.85E-6.</param>
        /// <param name="method">How the faces are read.</param>
        /// <param name="tolerance">The tolerance the openings are cut in and the faces broken into triangles within.</param>
        /// <returns>The properties; none, at the middle of the body's box, where the openings take all of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the body is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the density is not a positive number, or the method is not one of <see cref="VolumeMethod"/>.
        /// </exception>
        public static MassProperties3 MassProperties(GeoSolid3 solid, double density, VolumeMethod method, Tolerance tolerance)
        {
            Guard.Positive(density, nameof(density), "A density has to be a positive number.");
            Check(method);
            GeoSolid3 material = Material(solid, tolerance, out _);

            return material == null
                ? Mass3.Nothing(density, method, solid.GetAabb().Center, 0.0)
                : Mass3.Of(material, density, method, tolerance);
        }

        #endregion

        #region Surface area

        /// <summary>
        /// Gets the area of where a body's material ends by a method, using the default tolerance.
        /// </summary>
        public static double SurfaceArea(GeoSolid3 solid, AreaMethod method) => SurfaceArea(solid, method, Tolerance.Global);

        /// <summary>
        /// Gets the area of where a body's material ends by a method: its openings cut in first, their walls included.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="method">How the faces are read.</param>
        /// <param name="tolerance">The tolerance the openings are cut in and the faces broken into triangles within.</param>
        /// <returns>The area; nought where the openings take all of the body.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the body is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the method is not one of <see cref="AreaMethod"/>.</exception>
        public static double SurfaceArea(GeoSolid3 solid, AreaMethod method, Tolerance tolerance)
        {
            Check(method);
            GeoSolid3 material = Material(solid, tolerance, out _);

            return material == null ? 0.0 : Area(material, method, tolerance);
        }

        private static double Area(GeoSolid3 body, AreaMethod method, Tolerance tolerance)
        {
            double total = 0.0;

            foreach (GeoFace3 face in body.Faces)
            {
                if (method == AreaMethod.Faces)
                {
                    total += face.Area;
                    continue;
                }

                foreach (GeoTriangle3 triangle in face.TriangulateSurface(tolerance))
                {
                    total += triangle.Area;
                }
            }

            return total;
        }

        #endregion

        #region Comparing

        /// <summary>
        /// Measures a body's material every way, using the default tolerance.
        /// </summary>
        public static MeasureComparison3 Compare(GeoSolid3 solid) => Compare(solid, Tolerance.Global);

        /// <summary>
        /// Measures a body's material every way: its volume, centroid and surface area by each method, how far apart they
        /// come, and how far its faces close.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="tolerance">The tolerance the openings are cut in and the faces broken into triangles within.</param>
        /// <returns>The measures, side by side.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the body is null.</exception>
        /// <remarks>
        /// How far the faces close is how far the volume moves when it is measured from each corner of the body's box
        /// instead of its middle, by <see cref="VolumeMethod.Surface"/>: where the faces close, the parts of the
        /// tetrahedra outside the body cancel wherever they are measured from, and only the rounding is left. Faces that
        /// meet within the tolerance but not on one another, as copies of one edge a few thousandths apart, leave gaps
        /// the volume holds or not depending on where it is measured from.
        /// </remarks>
        public static MeasureComparison3 Compare(GeoSolid3 solid, Tolerance tolerance)
        {
            GeoSolid3 material = Material(solid, tolerance, out bool cut);
            var methods = (VolumeMethod[])Enum.GetValues(typeof(VolumeMethod));
            var areas = (AreaMethod[])Enum.GetValues(typeof(AreaMethod));
            var volumes = new double[methods.Length];
            var centroids = new GeoPoint3[methods.Length];
            var surfaces = new double[areas.Length];

            if (material == null)
            {
                for (int m = 0; m < methods.Length; m++)
                {
                    centroids[m] = solid.GetAabb().Center;
                }

                return new MeasureComparison3(volumes, centroids, surfaces, 0.0, cut);
            }

            GeoAabb3 box = material.GetAabb();
            GeoPoint3 origin = box.Center;

            foreach (VolumeMethod method in methods)
            {
                double[] sums = Mass3.Integrate(Mass3.Triangles(material, method, tolerance), origin);
                volumes[(int)method] = sums[0];
                centroids[(int)method] = sums[0] > 0.0 ? origin.Add(new GeoVector3(sums[1], sums[2], sums[3]).Divide(sums[0])) : origin;
            }

            foreach (AreaMethod method in areas)
            {
                surfaces[(int)method] = Area(material, method, tolerance);
            }

            // The same triangles measured from the middle of the box and from each of its corners.
            var triangles = Mass3.Triangles(material, VolumeMethod.Surface, tolerance).ToList();
            double least = double.MaxValue, most = double.MinValue;

            foreach (GeoPoint3 from in new[] { origin }.Concat(Corners(box)))
            {
                double volume = Mass3.Volume(triangles, from);
                least = Math.Min(least, volume);
                most = Math.Max(most, volume);
            }

            return new MeasureComparison3(volumes, centroids, surfaces, most - least, cut);
        }

        private static IEnumerable<GeoPoint3> Corners(GeoAabb3 box)
        {
            for (int k = 0; k < 8; k++)
            {
                yield return new GeoPoint3(
                    (k & 1) == 0 ? box.Min.X : box.Max.X,
                    (k & 2) == 0 ? box.Min.Y : box.Max.Y,
                    (k & 4) == 0 ? box.Min.Z : box.Max.Z);
            }
        }

        #endregion

        #region Material

        /// <summary>
        /// The material of a body, the body's own cut: its faces with every opening cut in; null where the openings take
        /// all of it; and any opening that cannot be cut in left in, warned of, with <paramref name="cut"/> false.
        /// </summary>
        private static GeoSolid3 Material(GeoSolid3 solid, Tolerance tolerance, out bool cut)
        {
            Check(solid);

            return solid.GetMaterial(tolerance, out cut);
        }

        private static void Check(GeoSolid3 solid)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }
        }

        private static void Check(VolumeMethod method)
        {
            if (method < VolumeMethod.Fan || method > VolumeMethod.FlatFaces)
            {
                throw new ArgumentOutOfRangeException(nameof(method), method, "Unknown way of reading the faces of a body.");
            }
        }

        private static void Check(AreaMethod method)
        {
            if (method < AreaMethod.Faces || method > AreaMethod.Surface)
            {
                throw new ArgumentOutOfRangeException(nameof(method), method, "Unknown way of reading the faces of a body.");
            }
        }

        #endregion
    }
}
