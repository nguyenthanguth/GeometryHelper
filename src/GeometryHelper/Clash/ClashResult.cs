using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper;
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

        // Measured when first asked for: a report of a thousand hard clashes need not fit a box round every overlap
        // to say what it shares.
        private Lazy<double> _depth;

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
        /// otherwise, and for a bar checked by its centre line, whose clashes are measured and not built. A clash
        /// reported as touching for being shallower than <see cref="ClashOptions.MinimumDepth"/> or smaller than
        /// <see cref="ClashOptions.MinimumVolume"/> keeps them.
        /// </summary>
        public IReadOnlyList<GeoSolid3> Overlaps { get; private set; }

        /// <summary>
        /// Gets how much volume the two share; nought unless the clash is <see cref="ClashKind.Hard"/>, and for a bar
        /// checked by its centre line. A clash reported as touching for being too shallow or too small keeps it.
        /// </summary>
        public double Volume { get; private set; }

        /// <summary>
        /// Gets how deep the two run into each other, for a <see cref="ClashKind.Hard"/> clash; nought otherwise.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For two bodies it is the least thickness of the region they share, the smallest side of the least box
        /// round it (<see cref="GeoObb3.Fit(IEnumerable{GeoPoint3})"/>), and of the deepest region where they share
        /// more than one: a bar grazing a flange by half a millimetre is half a millimetre deep however long the graze,
        /// and a bar through a plate as deep as the thinner of the two. For a region of no simple shape it is an
        /// estimate, a close one.
        /// </para>
        /// <para>
        /// For a bar checked by its centre line (<see cref="ClashBar"/>) it is how far the part reaches into the bar,
        /// across it. Where the centre line stays outside the part it is the radius less the centre line's nearest
        /// approach, exactly. Where the centre line runs inside, it is the radius and as far again as the centre line
        /// runs beneath the part's surface, at most the diameter, measured at points an eighth of a radius apart and so
        /// to within a sixteenth of the radius (on a stretch inside over 512 radii long, the points are spread wider).
        /// A plate thinner than the bar that the bar passes through is the radius and half the plate deep, where two
        /// bodies read the least thickness of what they share, the plate's own: however thin, the plate cuts the bar
        /// through.
        /// </para>
        /// <para>
        /// A clash reported as touching for being shallower than <see cref="ClashOptions.MinimumDepth"/> or smaller than
        /// <see cref="ClashOptions.MinimumVolume"/> keeps the depth it was found with.
        /// </para>
        /// </remarks>
        public double Depth => _depth?.Value ?? 0.0;

        /// <summary>
        /// Gets how much of a bar's centre line runs inside the part, for a bar checked by its centre line
        /// (<see cref="ClashBar"/>); nought where the centre line stays outside, and for two bodies.
        /// </summary>
        /// <remarks>
        /// A bar through a plate runs inside it for the plate's thickness, or more if it crosses at a slant; a bar cast
        /// along a flange, for as long as it runs in the steel.
        /// </remarks>
        public double LengthInside { get; private set; }

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

        internal static ClashResult Hard(int first, int second, GeoSolid3[] overlaps, Tolerance tolerance)
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
                _depth = new Lazy<double>(() => Deepest(overlaps, tolerance)),
            };
        }

        /// <summary>
        /// A bar checked by its centre line running into a part: how deep, and how much of the centre line runs inside.
        /// </summary>
        internal static ClashResult BarHard(int first, int second, GeoPoint3 location, double depth, double lengthInside)
        {
            return new ClashResult(first, second, ClashKind.Hard, location)
            {
                _depth = new Lazy<double>(() => depth),
                LengthInside = lengthInside,
            };
        }

        /// <summary>
        /// A bar checked by its centre line touching a part, the part as far from the centre line as the radius.
        /// </summary>
        internal static ClashResult BarTouch(int first, int second, GeoPoint3 location)
            => new ClashResult(first, second, ClashKind.Touch, location);

        /// <summary>
        /// A hard clash the options take as touching, for being too shallow or too small: reported as touching, with
        /// what it was found with kept.
        /// </summary>
        internal static ClashResult Shallow(ClashResult hard)
        {
            return new ClashResult(hard.First, hard.Second, ClashKind.Touch, hard.Location)
            {
                Overlaps = hard.Overlaps,
                Volume = hard.Volume,
                _depth = hard._depth,
                LengthInside = hard.LengthInside,
            };
        }

        /// <summary>
        /// The least thickness of the deepest of the regions two bodies share.
        /// </summary>
        private static double Deepest(GeoSolid3[] overlaps, Tolerance tolerance)
        {
            double deepest = 0.0;

            foreach (GeoSolid3 overlap in overlaps)
            {
                deepest = Math.Max(deepest, LeastThickness(overlap, tolerance));
            }

            return deepest;
        }

        /// <summary>
        /// The smallest side of the least box round a region, which for a sliver is how thick it is.
        /// </summary>
        private static double LeastThickness(GeoSolid3 region, Tolerance tolerance)
        {
            var corners = new List<GeoPoint3>();

            foreach (GeoFace3 face in region.Faces)
            {
                corners.AddRange(face.Boundary.Vertices);
            }

            try
            {
                GeoObb3 box = GeoObb3.Fit(corners, tolerance);
                return Math.Min(box.SizeX, Math.Min(box.SizeY, box.SizeZ));
            }
            catch (ArgumentException)
            {
                // A region too thin for a hull to stand on is as thin as its square box says.
                GeoAabb3 square = region.GetAabb();
                return Math.Min(square.Max.X - square.Min.X, Math.Min(square.Max.Y - square.Min.Y, square.Max.Z - square.Min.Z));
            }
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
                    return Overlaps.Count > 0
                        ? string.Format(CultureInfo.InvariantCulture, "ClashResult[{0}-{1} Hard, volume {2:0.###} in {3} piece(s), {4:0.###} deep]", First, Second, Volume, Overlaps.Count, Depth)
                        : string.Format(CultureInfo.InvariantCulture, "ClashResult[{0}-{1} Hard, {2:0.###} deep, {3:0.###} of the centre line inside]", First, Second, Depth, LengthInside);
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
