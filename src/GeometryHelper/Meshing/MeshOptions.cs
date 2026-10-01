using System;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Meshing
{
    /// <summary>
    /// How a closed shape of the plane is meshed: the kind of faces, and for a grid the size of its cells, the joints
    /// between them and where the grid stands.
    /// <para>
    /// The options are immutable, so one instance can be shared between threads and kept as a setting. The factories
    /// give the usual ones: <see cref="Triangles"/>, <see cref="Grid(double, double, double, GridAlignment, GridAlignment)"/>,
    /// <see cref="Strips(double)"/> and <see cref="Convex"/>; the constructor gives the rest, a turned grid among them.
    /// </para>
    /// </summary>
    public sealed class MeshOptions : IEquatable<MeshOptions>
    {
        private const string SizeMessage = "A size has to be a finite number, and cannot be negative.";

        /// <summary>
        /// Triangles on the shape's own corners.
        /// </summary>
        public static MeshOptions Triangles { get; } = new MeshOptions(MeshKind.Triangles);

        /// <summary>
        /// Convex pieces, few of them.
        /// </summary>
        public static MeshOptions Convex { get; } = new MeshOptions(MeshKind.Convex);

        /// <summary>
        /// Strips along a direction.
        /// </summary>
        /// <param name="angleRad">The direction the strips run along, counter-clockwise from the X axis.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle is not a finite number.</exception>
        public static MeshOptions Strips(double angleRad) => new MeshOptions(MeshKind.Strips, angleRad: angleRad);

        /// <summary>
        /// A grid of cells along the X and Y axes, or along a rectangle's own sides, standing against the shape as the
        /// alignments say.
        /// </summary>
        /// <param name="cellWidth">The size of a cell along the grid's first axis.</param>
        /// <param name="cellHeight">The size of a cell along the grid's second axis.</param>
        /// <param name="joint">The gap between two neighbouring cells, along both axes; nought for none.</param>
        /// <param name="alignU">Where the grid stands along its first axis.</param>
        /// <param name="alignV">Where the grid stands along its second axis.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when a cell size is not a finite number above nought, the joint is negative or not a finite number, or
        /// an alignment is not one of <see cref="GridAlignment"/>.
        /// </exception>
        public static MeshOptions Grid(double cellWidth, double cellHeight, double joint = 0.0, GridAlignment alignU = GridAlignment.Start, GridAlignment alignV = GridAlignment.Start)
            => new MeshOptions(MeshKind.Grid, cellWidth, cellHeight, joint, alignU: alignU, alignV: alignV);

        /// <summary>
        /// A grid of cells along the X and Y axes, or along a rectangle's own sides, with a cell starting at a point.
        /// </summary>
        /// <param name="cellWidth">The size of a cell along the grid's first axis.</param>
        /// <param name="cellHeight">The size of a cell along the grid's second axis.</param>
        /// <param name="origin">Where a cell has its first corner, the one it starts from along both axes.</param>
        /// <param name="joint">The gap between two neighbouring cells, along both axes; nought for none.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when a cell size is not a finite number above nought, the joint is negative or not a finite number, or
        /// the origin is not a finite point.
        /// </exception>
        public static MeshOptions Grid(double cellWidth, double cellHeight, GeoPoint2 origin, double joint = 0.0)
            => new MeshOptions(MeshKind.Grid, cellWidth, cellHeight, joint, origin: origin);

        /// <summary>
        /// Initializes mesh options.
        /// </summary>
        /// <param name="kind">The kind of faces.</param>
        /// <param name="cellWidth">
        /// The size of a grid cell along the grid's first axis. Above nought for <see cref="MeshKind.Grid"/>, and read
        /// by no other kind.
        /// </param>
        /// <param name="cellHeight">
        /// The size of a grid cell along the grid's second axis. Above nought for <see cref="MeshKind.Grid"/>, and read
        /// by no other kind.
        /// </param>
        /// <param name="joint">
        /// The gap between two neighbouring cells of a grid, along both axes, as the joints between panels or tiles: the
        /// cells stand <paramref name="cellWidth"/> plus the joint apart. There is no gap at the shape's boundary, where
        /// the cells are cut to it. Nought for none.
        /// </param>
        /// <param name="angleRad">
        /// The direction of a grid's first axis, or that strips run along, counter-clockwise from the X axis. Null runs
        /// it along the X axis, and along a rectangle's own width.
        /// </param>
        /// <param name="alignU">Where a grid stands along its first axis, when no <paramref name="origin"/> is given.</param>
        /// <param name="alignV">Where a grid stands along its second axis, when no <paramref name="origin"/> is given.</param>
        /// <param name="origin">
        /// Where a cell of a grid has its first corner, the one it starts from along both axes. Given, it takes the place
        /// of the alignments.
        /// </param>
        /// <param name="chordTolerance">
        /// The largest gap allowed between a chord and the arc it stands for, for a loop with arcs or a circle, in
        /// drawing units. Nought picks the automatic share of each radius.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the kind or an alignment is not a defined value, a size, the joint or the chord tolerance is
        /// negative or not a finite number, a grid has a cell size of nought, or the angle or the origin is not finite.
        /// </exception>
        public MeshOptions(
            MeshKind kind,
            double cellWidth = 0.0,
            double cellHeight = 0.0,
            double joint = 0.0,
            double? angleRad = null,
            GridAlignment alignU = GridAlignment.Start,
            GridAlignment alignV = GridAlignment.Start,
            GeoPoint2? origin = null,
            double chordTolerance = 0.0)
        {
            if (kind != MeshKind.Triangles && kind != MeshKind.Grid && kind != MeshKind.Strips && kind != MeshKind.Convex)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown kind of mesh.");
            }

            Guard.NonNegative(cellWidth, nameof(cellWidth), SizeMessage);
            Guard.NonNegative(cellHeight, nameof(cellHeight), SizeMessage);
            Guard.NonNegative(joint, nameof(joint), "A joint has to be a finite number, and cannot be negative.");
            Guard.NonNegative(chordTolerance, nameof(chordTolerance), "A chord tolerance has to be a finite number, and cannot be negative.");

            if (kind == MeshKind.Grid)
            {
                Guard.Positive(cellWidth, nameof(cellWidth), "A grid needs cells of some width.");
                Guard.Positive(cellHeight, nameof(cellHeight), "A grid needs cells of some height.");
            }

            if (angleRad.HasValue)
            {
                Guard.Finite(angleRad.Value, nameof(angleRad), "An angle has to be a number.");
            }

            CheckAlignment(alignU, nameof(alignU));
            CheckAlignment(alignV, nameof(alignV));

            if (origin.HasValue && !origin.Value.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(origin), origin.Value, "An origin has to be a finite point.");
            }

            Kind = kind;
            CellWidth = cellWidth;
            CellHeight = cellHeight;
            Joint = joint;
            AngleRad = angleRad;
            AlignU = alignU;
            AlignV = alignV;
            Origin = origin;
            ChordTolerance = chordTolerance;
        }

        /// <summary>
        /// Gets the kind of faces.
        /// </summary>
        public MeshKind Kind { get; }

        /// <summary>
        /// Gets the size of a grid cell along the grid's first axis; nought for a kind that is not a grid.
        /// </summary>
        public double CellWidth { get; }

        /// <summary>
        /// Gets the size of a grid cell along the grid's second axis; nought for a kind that is not a grid.
        /// </summary>
        public double CellHeight { get; }

        /// <summary>
        /// Gets the gap between two neighbouring cells of a grid, along both axes; nought for none.
        /// </summary>
        /// <remarks>
        /// A joint no wider than the point tolerance the mesh is made within is none: the cells meet.
        /// </remarks>
        public double Joint { get; }

        /// <summary>
        /// Gets the direction of a grid's first axis, or that strips run along, counter-clockwise from the X axis; null
        /// when it runs along the X axis, and along a rectangle's own width.
        /// </summary>
        public double? AngleRad { get; }

        /// <summary>
        /// Gets where a grid stands along its first axis, when no <see cref="Origin"/> is given.
        /// </summary>
        public GridAlignment AlignU { get; }

        /// <summary>
        /// Gets where a grid stands along its second axis, when no <see cref="Origin"/> is given.
        /// </summary>
        public GridAlignment AlignV { get; }

        /// <summary>
        /// Gets where a cell of a grid has its first corner, which takes the place of the alignments; null when the
        /// alignments place the grid.
        /// </summary>
        public GeoPoint2? Origin { get; }

        /// <summary>
        /// Gets the largest gap allowed between a chord and the arc it stands for; nought for the automatic share of each
        /// radius.
        /// </summary>
        public double ChordTolerance { get; }

        private static void CheckAlignment(GridAlignment alignment, string name)
        {
            if (alignment != GridAlignment.Start && alignment != GridAlignment.End && alignment != GridAlignment.CenterCell && alignment != GridAlignment.CenterJoint)
            {
                throw new ArgumentOutOfRangeException(name, alignment, "Unknown grid alignment.");
            }
        }

        /// <summary>
        /// Determines whether another set of options gives the same mesh.
        /// </summary>
        public bool Equals(MeshOptions other)
        {
            return other != null
                && Kind == other.Kind
                && CellWidth.Equals(other.CellWidth)
                && CellHeight.Equals(other.CellHeight)
                && Joint.Equals(other.Joint)
                && Nullable.Equals(AngleRad, other.AngleRad)
                && AlignU == other.AlignU
                && AlignV == other.AlignV
                && Nullable.Equals(Origin, other.Origin)
                && ChordTolerance.Equals(other.ChordTolerance);
        }

        /// <summary>
        /// Determines whether the specified object is an equal set of options.
        /// </summary>
        public override bool Equals(object obj) => obj is MeshOptions other && Equals(other);

        /// <summary>
        /// Returns the hash code for these options.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = hash * 397 ^ CellWidth.GetHashCode();
                hash = hash * 397 ^ CellHeight.GetHashCode();
                hash = hash * 397 ^ Joint.GetHashCode();
                hash = hash * 397 ^ AngleRad.GetHashCode();
                hash = hash * 397 ^ (int)AlignU;
                hash = hash * 397 ^ (int)AlignV;
                hash = hash * 397 ^ Origin.GetHashCode();
                hash = hash * 397 ^ ChordTolerance.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Describes the options.
        /// </summary>
        public override string ToString()
        {
            string angle = AngleRad.HasValue ? AngleRad.Value.ToString(CultureInfo.InvariantCulture) : "along the shape";
            string chord = ChordTolerance > 0.0 ? ChordTolerance.ToString(CultureInfo.InvariantCulture) : "automatic";

            if (Kind != MeshKind.Grid)
            {
                return string.Format(CultureInfo.InvariantCulture, "(Kind: {0}, AngleRad: {1}, ChordTolerance: {2})", Kind, angle, chord);
            }

            string place = Origin.HasValue ? "Origin: " + Origin.Value : string.Format(CultureInfo.InvariantCulture, "Align: {0}/{1}", AlignU, AlignV);

            return string.Format(
                CultureInfo.InvariantCulture,
                "(Kind: Grid, Cell: {0} x {1}, Joint: {2}, AngleRad: {3}, {4}, ChordTolerance: {5})",
                CellWidth,
                CellHeight,
                Joint,
                angle,
                place,
                chord);
        }
    }
}
