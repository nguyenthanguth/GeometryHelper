using System;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    public sealed partial class GeoSolid3
    {
        #region Mass properties and sections

        /// <summary>
        /// Gets the mass properties of the material at a density of one, using the default tolerance.
        /// </summary>
        public MassProperties3 GetMassProperties() => GetMassProperties(1.0, Tolerance.Global);

        /// <summary>
        /// Gets the mass properties of the material at a density, using the default tolerance.
        /// </summary>
        public MassProperties3 GetMassProperties(double density) => GetMassProperties(density, Tolerance.Global);

        /// <summary>
        /// Gets the mass properties of the material at a density: volume, mass, centroid, the moments and products
        /// of inertia about the centroid, and the principal moments and axes.
        /// </summary>
        /// <param name="density">The mass of a unit of volume; in kilograms per cubic millimetre, steel is 7.85E-6.</param>
        /// <param name="tolerance">The tolerance the openings are cut in and the surface meshed to.</param>
        /// <returns>The properties; openings are taken out of every one of them, and a body they take whole holds none.</returns>
        /// <remarks>
        /// Every integral is exact for the body's faces, taken over the surface by the divergence theorem, and the
        /// body is read as its material: a plate's bolt holes come out of its weight and move its centroid. The faces
        /// are read as the triangles lying in them, <see cref="VolumeMethod.Surface"/>; <see cref="Measure3"/> reads them
        /// other ways too, and sets the ways side by side.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the density is not a positive number.</exception>
        public MassProperties3 GetMassProperties(double density, Tolerance tolerance) => Measure3.MassProperties(this, density, VolumeMethod.Surface, tolerance);

        /// <summary>
        /// Gets where a plane cuts the material, using the default tolerance.
        /// </summary>
        public GeoFace3[] Section(GeoPlane3 plane) => Section(plane, Tolerance.Global);

        /// <summary>
        /// Gets where a plane cuts the material: the faces the cut leaves, holes and all, facing along the plane's
        /// normal.
        /// </summary>
        /// <param name="plane">The cutting plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>One face per region of the cut; empty where the plane misses the body or only grazes it.</returns>
        /// <remarks>
        /// The material is cut, so a hole the plane passes through is a hole in the section, and a body in two
        /// pieces gives two faces. A plane lying along a face of the body touches it without cutting it, and gives
        /// nothing there: the section is where the body has material on both sides of the plane.
        /// </remarks>
        public GeoFace3[] Section(GeoPlane3 plane, Tolerance tolerance)
        {
            return Splition3.Section(Material3.Whole(this, tolerance), plane, tolerance);
        }

        #endregion
    }
}
