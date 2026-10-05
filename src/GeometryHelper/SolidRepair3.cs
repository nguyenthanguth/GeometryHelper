using System.Globalization;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper
{
    /// <summary>
    /// One change <see cref="GeoSolid3.TryClose(out GeoSolid3, SolidClosingOptions, out SolidClosing3)"/> made to a body to
    /// close it: what it was, where, and how large.
    /// </summary>
    public sealed class SolidRepair3
    {
        /// <summary>
        /// Initializes a change.
        /// </summary>
        /// <param name="kind">What was done.</param>
        /// <param name="location">Where, as <see cref="Location"/> reads it for the kind.</param>
        /// <param name="size">How large, as <see cref="Size"/> reads it for the kind.</param>
        internal SolidRepair3(SolidRepairKind kind, GeoPoint3 location, double size)
        {
            Kind = kind;
            Location = location;
            Size = size;
        }

        /// <summary>
        /// Gets what was done.
        /// </summary>
        public SolidRepairKind Kind { get; }

        /// <summary>
        /// Gets where: the centroid of the face dropped or turned over, the point the corners were welded to, the corner put
        /// on an edge where it now stands, or the centroid of the faces filling a hole.
        /// </summary>
        public GeoPoint3 Location { get; }

        /// <summary>
        /// Gets how large it was: the area dropped, turned over or filled; the furthest a corner of the group welded moved;
        /// or how far the corner put on an edge stood off it.
        /// </summary>
        public double Size { get; }

        /// <summary>
        /// Says what was done, how large and where, in invariant culture.
        /// </summary>
        public override string ToString()
        {
            string at = string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###}, {2:0.###})", Location.X, Location.Y, Location.Z);

            switch (Kind)
            {
                case SolidRepairKind.Weld:
                    return string.Format(CultureInfo.InvariantCulture, "SolidRepair3[{0} moving {1:G4} at {2}]", Kind, Size, at);
                case SolidRepairKind.SplitEdge:
                    return string.Format(CultureInfo.InvariantCulture, "SolidRepair3[{0} {1:G4} off the edge at {2}]", Kind, Size, at);
                default:
                    return string.Format(CultureInfo.InvariantCulture, "SolidRepair3[{0} of {1:G4} in area at {2}]", Kind, Size, at);
            }
        }
    }
}
