using System;
using System.Globalization;

namespace GeometryHelper.Meshing
{
    /// <summary>
    /// How a body is cut into cells: how each of the grid's three axes is divided, the joint between neighbouring cells, and
    /// how near a corner of the body a cut is moved onto it.
    /// <para>
    /// The options are immutable, so one instance can be shared between threads and kept as a setting. The factories give
    /// the usual ones: <see cref="Grid(double, double, double, double, GridAlignment)"/> for cells of a size,
    /// <see cref="Layers(double, double, GridAlignment)"/> for lifts up the grid's Z axis, and
    /// <see cref="Divide(int, int, int, double)"/> for equal cells by count; the constructor gives the rest, each axis its
    /// own <see cref="CellAxis"/>. Which way the axes run, and where a cell starts, is the <see cref="MeshPlacement3"/>'s.
    /// </para>
    /// </summary>
    public sealed class CellOptions3 : IEquatable<CellOptions3>
    {
        /// <summary>
        /// Cells of a size along each axis, standing against the body as the alignment says along all three.
        /// </summary>
        /// <param name="sizeX">The size of a cell along the grid's X axis; nought leaves the body whole that way.</param>
        /// <param name="sizeY">The size of a cell along the grid's Y axis; nought leaves the body whole that way.</param>
        /// <param name="sizeZ">The size of a cell along the grid's Z axis; nought leaves the body whole that way.</param>
        /// <param name="joint">The gap between two neighbouring cells, along each axis divided; nought for none.</param>
        /// <param name="alignment">Where the cells stand along each axis divided, when the placement gives no origin.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when a size is negative or not a finite number, the joint is negative or not a finite number, or the
        /// alignment is not one of <see cref="GridAlignment"/>.
        /// </exception>
        public static CellOptions3 Grid(double sizeX, double sizeY, double sizeZ, double joint = 0.0, GridAlignment alignment = GridAlignment.Start)
            => new CellOptions3(Axis(sizeX, nameof(sizeX), alignment), Axis(sizeY, nameof(sizeY), alignment), Axis(sizeZ, nameof(sizeZ), alignment), joint);

        /// <summary>
        /// Layers of a thickness up the grid's Z axis, the body left whole along X and Y, as the lifts of a pour.
        /// </summary>
        /// <param name="thickness">The thickness of a layer.</param>
        /// <param name="joint">The gap between two layers; nought for none.</param>
        /// <param name="alignment">Where the layers stand, when the placement gives no origin: from the bottom by default.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the thickness is not a finite number above nought, the joint is negative or not a finite number, or
        /// the alignment is not one of <see cref="GridAlignment"/>.
        /// </exception>
        public static CellOptions3 Layers(double thickness, double joint = 0.0, GridAlignment alignment = GridAlignment.Start)
            => new CellOptions3(CellAxis.Whole, CellAxis.Whole, CellAxis.BySize(thickness, alignment), joint);

        /// <summary>
        /// As many equal cells along each axis as asked, from the body's first side to its far one.
        /// </summary>
        /// <param name="countX">How many cells along the grid's X axis; one leaves the body whole that way.</param>
        /// <param name="countY">How many cells along the grid's Y axis; one leaves the body whole that way.</param>
        /// <param name="countZ">How many cells along the grid's Z axis; one leaves the body whole that way.</param>
        /// <param name="joint">The gap between two neighbouring cells, along each axis divided; nought for none.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a count is below one, or the joint is negative or not a finite number.</exception>
        public static CellOptions3 Divide(int countX, int countY, int countZ, double joint = 0.0)
            => new CellOptions3(CellAxis.ByCount(countX), CellAxis.ByCount(countY), CellAxis.ByCount(countZ), joint);

        /// <summary>
        /// Initializes cell options, each axis divided its own way.
        /// </summary>
        /// <param name="x">How the grid's X axis is divided.</param>
        /// <param name="y">How the grid's Y axis is divided.</param>
        /// <param name="z">How the grid's Z axis is divided.</param>
        /// <param name="joint">
        /// The gap between two neighbouring cells, along each axis divided, as the joints between blocks: the cells stand a
        /// size plus the joint apart. There is no gap at the body's boundary, where the cells are cut to it. Nought for none.
        /// </param>
        /// <param name="snapDistance">
        /// How near a corner of the body a cut has to come to be moved onto it, so that no cell is cut off thinner than this:
        /// the slice between goes to the cell beside. Nought, or anything less, takes the point tolerance, which every cut
        /// is snapped within, since a cut that close is a cut along the body's own face. With a joint the cuts bound the
        /// joints, and are snapped within the point tolerance only, so that no cell is carried into a joint. However small
        /// this is, no cut takes off less than four point tolerances: such a piece is at the scale of the tolerance.
        /// </param>
        /// <param name="maxDegreeOfParallelism">How many threads may cut at once; -1 for every processor.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the joint or the snap distance is negative or not a finite number, or the threads are neither -1 nor
        /// a positive count.
        /// </exception>
        public CellOptions3(CellAxis x, CellAxis y, CellAxis z, double joint = 0.0, double snapDistance = 0.0, int maxDegreeOfParallelism = -1)
        {
            Guard.NonNegative(joint, nameof(joint), "A joint has to be a finite number, and cannot be negative.");
            Guard.NonNegative(snapDistance, nameof(snapDistance), "A snap distance has to be a finite number, and cannot be negative.");

            if (maxDegreeOfParallelism == 0 || maxDegreeOfParallelism < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), maxDegreeOfParallelism, "Use -1 for every processor, or a positive count.");
            }

            X = x;
            Y = y;
            Z = z;
            Joint = joint;
            SnapDistance = snapDistance;
            MaxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        /// <summary>
        /// Gets how the grid's X axis is divided.
        /// </summary>
        public CellAxis X { get; }

        /// <summary>
        /// Gets how the grid's Y axis is divided.
        /// </summary>
        public CellAxis Y { get; }

        /// <summary>
        /// Gets how the grid's Z axis is divided.
        /// </summary>
        public CellAxis Z { get; }

        /// <summary>
        /// Gets the gap between two neighbouring cells, along each axis divided; nought for none.
        /// </summary>
        /// <remarks>
        /// A joint no wider than the point tolerance the cells are cut within is none: the cells meet.
        /// </remarks>
        public double Joint { get; }

        /// <summary>
        /// Gets how near a corner of the body a cut has to come to be moved onto it; nought for the point tolerance. A grid
        /// with joints snaps within the point tolerance only.
        /// </summary>
        public double SnapDistance { get; }

        /// <summary>
        /// Gets how many threads may cut at once; -1 for every processor.
        /// </summary>
        public int MaxDegreeOfParallelism { get; }

        /// <summary>
        /// Gets how one of the axes is divided: 0 for X, 1 for Y, 2 for Z.
        /// </summary>
        internal CellAxis AxisAt(int index) => index == 0 ? X : index == 1 ? Y : Z;

        /// <summary>
        /// Determines whether another set of options cuts a body the same way.
        /// </summary>
        public bool Equals(CellOptions3 other)
        {
            return other != null
                && X.Equals(other.X)
                && Y.Equals(other.Y)
                && Z.Equals(other.Z)
                && Joint.Equals(other.Joint)
                && SnapDistance.Equals(other.SnapDistance)
                && MaxDegreeOfParallelism == other.MaxDegreeOfParallelism;
        }

        /// <summary>
        /// Determines whether the specified object is an equal set of options.
        /// </summary>
        public override bool Equals(object obj) => obj is CellOptions3 other && Equals(other);

        /// <summary>
        /// Returns the hash code for these options.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = hash * 397 ^ Y.GetHashCode();
                hash = hash * 397 ^ Z.GetHashCode();
                hash = hash * 397 ^ Joint.GetHashCode();
                hash = hash * 397 ^ SnapDistance.GetHashCode();
                hash = hash * 397 ^ MaxDegreeOfParallelism;
                return hash;
            }
        }

        /// <summary>
        /// Describes the options.
        /// </summary>
        public override string ToString()
            => string.Format(CultureInfo.InvariantCulture, "(X: {0}, Y: {1}, Z: {2}, Joint: {3}, Snap: {4})", X, Y, Z, Joint, SnapDistance > 0.0 ? SnapDistance.ToString(CultureInfo.InvariantCulture) : "tolerance");

        private static CellAxis Axis(double size, string name, GridAlignment alignment)
        {
            Guard.NonNegative(size, name, "A size has to be a finite number, and cannot be negative.");
            return size > 0.0 ? CellAxis.BySize(size, alignment) : CellAxis.Whole;
        }
    }
}
