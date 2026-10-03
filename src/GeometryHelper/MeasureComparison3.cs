using System;
using System.Globalization;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper
{
    /// <summary>
    /// A body's material measured every way: its volume, centroid and surface area by each method, how far apart they
    /// come, and how far its faces close.
    /// </summary>
    /// <remarks>
    /// Over flat faces that close, every method gives the same, and every spread is the rounding. A spread above the
    /// rounding says how far a measure can be trusted, and why. <see cref="VolumeSpread"/> comes of faces a hair out of
    /// flat, which no one surface is: the methods read them as different surfaces, and none of them is the body's.
    /// <see cref="ReferenceSpread"/> comes of faces that meet within the tolerance but not on one another, whose gaps the
    /// volume holds or not depending on where it is measured from.
    /// </remarks>
    public sealed class MeasureComparison3
    {
        private readonly double[] _volumes;
        private readonly GeoPoint3[] _centroids;
        private readonly double[] _areas;

        internal MeasureComparison3(double[] volumes, GeoPoint3[] centroids, double[] areas, double referenceSpread, bool openingsCut)
        {
            _volumes = volumes;
            _centroids = centroids;
            _areas = areas;
            ReferenceSpread = referenceSpread;
            OpeningsCut = openingsCut;

            double least = double.MaxValue, most = double.MinValue, apart = 0.0;

            for (int i = 0; i < volumes.Length; i++)
            {
                least = Math.Min(least, volumes[i]);
                most = Math.Max(most, volumes[i]);

                for (int j = i + 1; j < centroids.Length; j++)
                {
                    apart = Math.Max(apart, centroids[i].DistanceTo(centroids[j]));
                }
            }

            VolumeSpread = most - least;
            CentroidSpread = apart;
            AreaSpread = Math.Abs(areas[(int)AreaMethod.Surface] - areas[(int)AreaMethod.Faces]);
        }

        /// <summary>Gets the volume of the material read by a method.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the method is not one of <see cref="VolumeMethod"/>.</exception>
        public double GetVolume(VolumeMethod method) => _volumes[Index(method)];

        /// <summary>Gets the centre of the material read by a method; the middle of its box where it holds no volume.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the method is not one of <see cref="VolumeMethod"/>.</exception>
        public GeoPoint3 GetCentroid(VolumeMethod method) => _centroids[Index(method)];

        /// <summary>Gets the area of where the material ends read by a method, the walls of its openings included.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the method is not one of <see cref="AreaMethod"/>.</exception>
        public double GetSurfaceArea(AreaMethod method)
        {
            if (method < AreaMethod.Faces || method > AreaMethod.Surface)
            {
                throw new ArgumentOutOfRangeException(nameof(method), method, "Unknown way of reading the faces of a body.");
            }

            return _areas[(int)method];
        }

        /// <summary>Gets how far apart the volumes of the methods come, the largest less the smallest.</summary>
        public double VolumeSpread { get; }

        /// <summary>Gets how far apart the centroids of the methods come, the two furthest apart.</summary>
        public double CentroidSpread { get; }

        /// <summary>Gets how far apart the surface areas of the methods come.</summary>
        public double AreaSpread { get; }

        /// <summary>
        /// Gets how far the volume read by <see cref="VolumeMethod.Surface"/> moves when it is measured from each corner of
        /// the material's box instead of its middle, the largest less the smallest: the rounding where the faces close.
        /// </summary>
        public double ReferenceSpread { get; }

        /// <summary>
        /// Gets whether every opening was cut in, or there were none: false where one could not be, and was measured as
        /// material, as the log warns.
        /// </summary>
        public bool OpeningsCut { get; }

        private static int Index(VolumeMethod method)
        {
            if (method < VolumeMethod.Fan || method > VolumeMethod.FlatFaces)
            {
                throw new ArgumentOutOfRangeException(nameof(method), method, "Unknown way of reading the faces of a body.");
            }

            return (int)method;
        }

        /// <inheritdoc/>
        public override string ToString()
            => string.Format(
                CultureInfo.InvariantCulture,
                "MeasureComparison3[Volume: Fan {0:0.###}, Surface {1:0.###}, FlatFaces {2:0.###}, spread {3:G3}, reference spread {4:G3}; Area: Faces {5:0.###}, Surface {6:0.###}{7}]",
                _volumes[(int)VolumeMethod.Fan],
                _volumes[(int)VolumeMethod.Surface],
                _volumes[(int)VolumeMethod.FlatFaces],
                VolumeSpread,
                ReferenceSpread,
                _areas[(int)AreaMethod.Faces],
                _areas[(int)AreaMethod.Surface],
                OpeningsCut ? string.Empty : "; openings not cut");
    }
}
