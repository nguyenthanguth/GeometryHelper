using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Clash
{
    /// <summary>
    /// One pair of parts a clash check found, and what they do to each other.
    /// </summary>
    public sealed class ClashResult
    {
        private static readonly GeoSolid3[] NoOverlaps = new GeoSolid3[0];
        private static readonly GeoFace3[] NoContact = new GeoFace3[0];

        private ClashResult(int first, int second, ClashKind kind, GeoPoint3 location)
        {
            First = first;
            Second = second;
            Kind = kind;
            Location = location;
            Overlaps = NoOverlaps;
            Contact = NoContact;
        }

        /// <summary>
        /// Gets the index of the first part of the pair: in the one list checked, the lower index; with two
        /// lists, the index in the first.
        /// </summary>
        public int First { get; }

        /// <summary>
        /// Gets the index of the second part of the pair: in the one list checked, the higher index; with two
        /// lists, the index in the second.
        /// </summary>
        public int Second { get; }

        /// <summary>
        /// Gets what the two do to each other.
        /// </summary>
        public ClashKind Kind { get; }

        /// <summary>
        /// Gets where the clash is: the centre of the volume shared, the centre of the contact, the middle of the
        /// gap left, or for a pair that could not be checked, the middle of the space the two boxes share.
        /// </summary>
        public GeoPoint3 Location { get; }

        /// <summary>
        /// Gets the regions the two share, one body per region, for a <see cref="ClashKind.Hard"/> clash; empty
        /// otherwise.
        /// </summary>
        public IReadOnlyList<GeoSolid3> Overlaps { get; private set; }

        /// <summary>
        /// Gets how much volume the two share; nought unless the clash is <see cref="ClashKind.Hard"/>.
        /// </summary>
        public double Volume { get; private set; }

        /// <summary>
        /// Gets where the two lie against each other, face to face, for a <see cref="ClashKind.Touch"/>; empty
        /// where they meet only along an edge or at a corner, and for every other kind.
        /// </summary>
        public IReadOnlyList<GeoFace3> Contact { get; private set; }

        /// <summary>
        /// Gets the area of <see cref="Contact"/>.
        /// </summary>
        public double ContactArea { get; private set; }

        /// <summary>
        /// Gets how far apart the two are: the gap for <see cref="ClashKind.Clearance"/>, nought otherwise.
        /// </summary>
        public double Distance { get; private set; }

        /// <summary>
        /// Gets the shortest segment across the gap for <see cref="ClashKind.Clearance"/>; null otherwise.
        /// </summary>
        public GeoLine3? Gap { get; private set; }

        /// <summary>
        /// Gets what went wrong checking the pair, for <see cref="ClashKind.Unresolved"/>; null otherwise.
        /// </summary>
        public Exception Error { get; private set; }

        internal static ClashResult Hard(int first, int second, GeoSolid3[] overlaps)
        {
            double volume = 0.0;
            double x = 0.0, y = 0.0, z = 0.0;

            foreach (GeoSolid3 overlap in overlaps)
            {
                double piece = overlap.Volume;
                GeoPoint3 centre = overlap.Centroid;

                volume += piece;
                x += centre.X * piece;
                y += centre.Y * piece;
                z += centre.Z * piece;
            }

            GeoPoint3 location = volume > 0.0 ? new GeoPoint3(x / volume, y / volume, z / volume) : overlaps[0].Centroid;

            return new ClashResult(first, second, ClashKind.Hard, location)
            {
                Overlaps = overlaps,
                Volume = volume,
            };
        }

        internal static ClashResult Touch(int first, int second, GeoFace3[] contact, GeoPoint3 touching)
        {
            double area = 0.0;
            double x = 0.0, y = 0.0, z = 0.0;

            foreach (GeoFace3 patch in contact)
            {
                double piece = patch.Area;
                GeoPoint3 centre = patch.Centroid;

                area += piece;
                x += centre.X * piece;
                y += centre.Y * piece;
                z += centre.Z * piece;
            }

            GeoPoint3 location = area > 0.0 ? new GeoPoint3(x / area, y / area, z / area) : touching;

            return new ClashResult(first, second, ClashKind.Touch, location)
            {
                Contact = contact,
                ContactArea = area,
            };
        }

        internal static ClashResult Near(int first, int second, GeoLine3 gap)
        {
            return new ClashResult(first, second, ClashKind.Clearance, gap.MidPoint)
            {
                Distance = gap.Length,
                Gap = gap,
            };
        }

        internal static ClashResult Unresolved(int first, int second, GeoPoint3 location, Exception error)
        {
            return new ClashResult(first, second, ClashKind.Unresolved, location)
            {
                Error = error,
            };
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            switch (Kind)
            {
                case ClashKind.Hard:
                    return string.Format(CultureInfo.InvariantCulture, "ClashResult[{0}-{1} Hard, volume {2:0.###} in {3} piece(s)]", First, Second, Volume, Overlaps.Count);
                case ClashKind.Touch:
                    return string.Format(CultureInfo.InvariantCulture, "ClashResult[{0}-{1} Touch, contact {2:0.###}]", First, Second, ContactArea);
                case ClashKind.Clearance:
                    return string.Format(CultureInfo.InvariantCulture, "ClashResult[{0}-{1} Clearance, {2:0.###} apart]", First, Second, Distance);
                default:
                    return string.Format(CultureInfo.InvariantCulture, "ClashResult[{0}-{1} Unresolved: {2}]", First, Second, Error?.Message);
            }
        }
    }
}
