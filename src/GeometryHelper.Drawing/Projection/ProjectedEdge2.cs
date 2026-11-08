using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Projection
{
    /// <summary>
    /// One line of a view, in view millimetres at 1:1: a straight segment, or an arc or a whole circle fitted to the facets
    /// of a curved surface; seen or hidden; and the objects it is an edge of.
    /// </summary>
    /// <remarks>
    /// Where edges of two objects fall on one another, as the edges of two plates lying edge to edge do, the line is drawn
    /// once and lists both objects. A line is either seen or hidden along the whole of its length.
    /// </remarks>
    public sealed class ProjectedEdge2
    {
        private const double FullTurn = 2.0 * Math.PI;

        private readonly GeoLine2 _line;
        private readonly GeoArc2 _arc;
        private readonly string[] _objectIds;

        /// <summary>
        /// Initializes a straight line of a view.
        /// </summary>
        internal ProjectedEdge2(GeoLine2 line, EdgeVisibility visibility, EdgeKind kind, string[] objectIds)
        {
            _line = line;
            _objectIds = objectIds;
            Visibility = visibility;
            Kind = kind;
        }

        /// <summary>
        /// Initializes an arc or a circle of a view.
        /// </summary>
        internal ProjectedEdge2(GeoArc2 arc, EdgeVisibility visibility, EdgeKind kind, string[] objectIds)
        {
            _arc = arc;
            _objectIds = objectIds;
            IsArc = true;
            Visibility = visibility;
            Kind = kind;
        }

        /// <summary>
        /// Gets a value indicating whether the line is an arc or a whole circle rather than a straight segment.
        /// </summary>
        public bool IsArc { get; }

        /// <summary>
        /// Gets a value indicating whether the line is a whole circle: the rim of a hole, or a tube seen along its axis.
        /// </summary>
        public bool IsCircle => IsArc && Math.Abs(Math.Abs(_arc.SweptAngle) - FullTurn) <= 1E-12;

        /// <summary>
        /// Gets the straight segment.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the line is an arc.</exception>
        public GeoLine2 Line => IsArc ? throw new InvalidOperationException("This line is an arc; read Arc.") : _line;

        /// <summary>
        /// Gets the arc, counter-clockwise; a whole circle starts and ends at the angle nought.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the line is a straight segment.</exception>
        public GeoArc2 Arc => IsArc ? _arc : throw new InvalidOperationException("This line is straight; read Line.");

        /// <summary>
        /// Gets the point the line starts at: the end of a segment lower in X, then in Y; the start of an arc going
        /// counter-clockwise.
        /// </summary>
        public GeoPoint2 StartPoint => IsArc ? _arc.StartPoint : _line.StartPoint;

        /// <summary>
        /// Gets the point the line ends at; for a whole circle, its start.
        /// </summary>
        public GeoPoint2 EndPoint => IsArc ? _arc.EndPoint : _line.EndPoint;

        /// <summary>
        /// Gets the length of the line.
        /// </summary>
        public double Length => IsArc ? _arc.Length : _line.Length;

        /// <summary>
        /// Gets whether the line is seen or hidden.
        /// </summary>
        public EdgeVisibility Visibility { get; }

        /// <summary>
        /// Gets why the line is drawn: a sharp edge, a silhouette or the outline of a section.
        /// </summary>
        public EdgeKind Kind { get; }

        /// <summary>
        /// Gets the id of the object the line belongs to: the lowest, compared ordinally, of <see cref="ObjectIds"/>.
        /// </summary>
        public string ObjectId => _objectIds[0];

        /// <summary>
        /// Gets the ids of every object whose edge the line is, in ordinal order: one, or more where edges of objects that
        /// touch fall on one another.
        /// </summary>
        public IReadOnlyList<string> ObjectIds => _objectIds;

        /// <inheritdoc/>
        public override string ToString()
            => IsArc
                ? $"Edge2({Visibility} {Kind} arc C({_arc.Center.X:0.###}, {_arc.Center.Y:0.###}) R{_arc.Radius:0.###} of {ObjectId})"
                : $"Edge2({Visibility} {Kind} ({_line.StartPoint.X:0.###}, {_line.StartPoint.Y:0.###})-({_line.EndPoint.X:0.###}, {_line.EndPoint.Y:0.###}) of {ObjectId})";
    }
}
