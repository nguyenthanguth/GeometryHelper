using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper
{
    /// <summary>
    /// How much a body weighs, where its weight is centred, and how it resists turning.
    /// </summary>
    /// <remarks>
    /// The moments and products are about axes through the centroid parallel to the world's. A product of
    /// inertia is the integral of the two coordinates multiplied, so <see cref="Ixy"/> is ∫(x - cx)(y - cy) dm and
    /// appears negated off the diagonal of <see cref="GetInertiaTensor"/>. The principal moments are the tensor's
    /// eigenvalues, smallest first, and the principal axes turn with them.
    /// </remarks>
    public sealed class MassProperties3
    {
        private readonly double[] _moments;
        private readonly GeoVector3[] _axes;

        internal MassProperties3(double density, double volume, GeoPoint3 centroid, double surfaceArea,
            double ixx, double iyy, double izz, double ixy, double iyz, double izx, double[] moments, GeoVector3[] axes)
        {
            Density = density;
            Volume = volume;
            Centroid = centroid;
            SurfaceArea = surfaceArea;
            Ixx = ixx;
            Iyy = iyy;
            Izz = izz;
            Ixy = ixy;
            Iyz = iyz;
            Izx = izx;
            _moments = moments;
            _axes = axes;
        }

        /// <summary>Gets the density the mass and the moments were worked out for.</summary>
        public double Density { get; }

        /// <summary>Gets the volume of the material, openings taken out.</summary>
        public double Volume { get; }

        /// <summary>Gets the mass: the density times the volume.</summary>
        public double Mass => Density * Volume;

        /// <summary>Gets the centre of the material's volume, which is its centre of mass.</summary>
        public GeoPoint3 Centroid { get; }

        /// <summary>Gets the area of where the material ends, the walls of its openings included.</summary>
        public double SurfaceArea { get; }

        /// <summary>Gets the moment of inertia about the axis through the centroid parallel to X.</summary>
        public double Ixx { get; }

        /// <summary>Gets the moment of inertia about the axis through the centroid parallel to Y.</summary>
        public double Iyy { get; }

        /// <summary>Gets the moment of inertia about the axis through the centroid parallel to Z.</summary>
        public double Izz { get; }

        /// <summary>Gets the product of inertia ∫(x - cx)(y - cy) dm.</summary>
        public double Ixy { get; }

        /// <summary>Gets the product of inertia ∫(y - cy)(z - cz) dm.</summary>
        public double Iyz { get; }

        /// <summary>Gets the product of inertia ∫(z - cz)(x - cx) dm.</summary>
        public double Izx { get; }

        /// <summary>Gets the principal moments of inertia, smallest first.</summary>
        public IReadOnlyList<double> PrincipalMoments => _moments;

        /// <summary>Gets the principal axes, in the order of <see cref="PrincipalMoments"/>, unit length and right-handed.</summary>
        public IReadOnlyList<GeoVector3> PrincipalAxes => _axes;

        /// <summary>
        /// Gets the inertia tensor about the centroid: the moments on the diagonal, the products negated off it.
        /// </summary>
        /// <returns>A new 3 × 3 array, rows and columns in the order X, Y, Z.</returns>
        public double[,] GetInertiaTensor()
        {
            return new double[,]
            {
                { Ixx, -Ixy, -Izx },
                { -Ixy, Iyy, -Iyz },
                { -Izx, -Iyz, Izz },
            };
        }

        /// <summary>
        /// Gets the moment of inertia about an axis through a point, by way of the parallel axis theorem.
        /// </summary>
        /// <param name="point">A point on the axis.</param>
        /// <param name="direction">The direction of the axis.</param>
        /// <returns>The moment about that axis.</returns>
        /// <exception cref="ArgumentException">Thrown when the direction has no length.</exception>
        public double GetMomentAbout(GeoPoint3 point, GeoVector3 direction)
        {
            if (!direction.TryGetNormal(out GeoVector3 a))
            {
                throw new ArgumentException("An axis needs a direction of some length.", nameof(direction));
            }

            double about = Ixx * a.X * a.X + Iyy * a.Y * a.Y + Izz * a.Z * a.Z
                - 2.0 * (Ixy * a.X * a.Y + Iyz * a.Y * a.Z + Izx * a.Z * a.X);

            GeoVector3 off = point.GetVectorTo(Centroid);
            double reach = off.CrossProduct(a).Length;

            return about + Mass * reach * reach;
        }

        /// <inheritdoc/>
        public override string ToString()
            => string.Format(CultureInfo.InvariantCulture, "MassProperties3[Volume: {0:0.###}, Mass: {1:0.###}, Centroid: {2}]", Volume, Mass, Centroid);
    }
}
