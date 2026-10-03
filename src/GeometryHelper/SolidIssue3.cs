using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper
{
    /// <summary>
    /// One thing <see cref="GeoSolid3.Validate(Tolerance)"/> found wrong with the faces of a body, or worth knowing about
    /// them, and where it is.
    /// </summary>
    public sealed class SolidIssue3
    {
        private static readonly int[] NoFaces = new int[0];

        private SolidIssue3(SolidIssueKind kind, GeoLine3? edge, int[] faces, GeoPoint3 location, double size, int forward, int backward)
        {
            Kind = kind;
            Edge = edge;
            Faces = faces;
            Location = location;
            Size = size;
            Forward = forward;
            Backward = backward;
        }

        /// <summary>
        /// Gets what it is.
        /// </summary>
        public SolidIssueKind Kind { get; }

        /// <summary>
        /// Gets the stretch of edge it is on, for <see cref="SolidIssueKind.OpenEdge"/>,
        /// <see cref="SolidIssueKind.SameWayEdge"/> and <see cref="SolidIssueKind.NonManifoldEdge"/>, and the edge, for
        /// <see cref="SolidIssueKind.ShortEdge"/>; null for the others.
        /// </summary>
        /// <remarks>
        /// A stretch lies on the line of the longest edge along it, from the first corner on that line to the next where
        /// the faces meeting on it change: a gap one face wide along a whole edge is one stretch, as long as the edge.
        /// </remarks>
        public GeoLine3? Edge { get; }

        /// <summary>
        /// Gets the faces it is of, by their index in <see cref="GeoSolid3.Faces"/>, lowest first: those meeting on the
        /// stretch, or the face with the edge, or the face itself; none for <see cref="SolidIssueKind.InsideOut"/> and
        /// <see cref="SolidIssueKind.NoVolume"/>, which are of the body.
        /// </summary>
        public IReadOnlyList<int> Faces { get; }

        /// <summary>
        /// Gets where it is: the middle of the stretch or the edge, the centroid of the face, or the middle of the body's
        /// box.
        /// </summary>
        public GeoPoint3 Location { get; }

        /// <summary>
        /// Gets how large it is: the length of the stretch or the edge; how wide a <see cref="SolidIssueKind.SliverFace"/>
        /// is on average, twice its area over its perimeter; how far the furthest corner of a
        /// <see cref="SolidIssueKind.NotFlatFace"/> stands off its plane; and the volume the faces enclose, signed, for
        /// <see cref="SolidIssueKind.InsideOut"/> and <see cref="SolidIssueKind.NoVolume"/>.
        /// </summary>
        public double Size { get; }

        /// <summary>
        /// Gets how many edges of the faces on the stretch run it from the start of <see cref="Edge"/> to its end, each
        /// taken with its face on the left; nought where there is no stretch. A stretch two faces wound alike meet on is
        /// run once each way.
        /// </summary>
        public int Forward { get; }

        /// <summary>
        /// Gets how many edges of the faces on the stretch run it from the end of <see cref="Edge"/> to its start; nought
        /// where there is no stretch.
        /// </summary>
        public int Backward { get; }

        internal static SolidIssue3 OnEdge(SolidIssueKind kind, GeoLine3 stretch, int[] faces, int forward, int backward)
            => new SolidIssue3(kind, stretch, faces, Middle(stretch), stretch.StartPoint.DistanceTo(stretch.EndPoint), forward, backward);

        internal static SolidIssue3 ShortOf(int face, GeoLine3 edge)
            => new SolidIssue3(SolidIssueKind.ShortEdge, edge, new[] { face }, Middle(edge), edge.StartPoint.DistanceTo(edge.EndPoint), 0, 0);

        internal static SolidIssue3 OfFace(SolidIssueKind kind, int face, GeoPoint3 centroid, double size)
            => new SolidIssue3(kind, null, new[] { face }, centroid, size, 0, 0);

        internal static SolidIssue3 OfBody(SolidIssueKind kind, GeoPoint3 middle, double volume)
            => new SolidIssue3(kind, null, NoFaces, middle, volume, 0, 0);

        private static GeoPoint3 Middle(GeoLine3 line)
            => new GeoPoint3(
                (line.StartPoint.X + line.EndPoint.X) * 0.5,
                (line.StartPoint.Y + line.EndPoint.Y) * 0.5,
                (line.StartPoint.Z + line.EndPoint.Z) * 0.5);

        /// <summary>
        /// Says what it is and where, in invariant culture.
        /// </summary>
        public override string ToString()
        {
            string at = string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###}, {2:0.###})", Location.X, Location.Y, Location.Z);
            string faces = Faces.Count == 1 ? "face " + Faces[0].ToString(CultureInfo.InvariantCulture) : "faces " + string.Join(", ", Faces);

            switch (Kind)
            {
                case SolidIssueKind.OpenEdge:
                case SolidIssueKind.SameWayEdge:
                case SolidIssueKind.NonManifoldEdge:
                    return string.Format(CultureInfo.InvariantCulture, "SolidIssue3[{0} {1:G4} long at {2}, {3}; {4} one way, {5} the other]", Kind, Size, at, faces, Forward, Backward);
                case SolidIssueKind.ShortEdge:
                    return string.Format(CultureInfo.InvariantCulture, "SolidIssue3[{0} {1:G4} long at {2}, {3}]", Kind, Size, at, faces);
                case SolidIssueKind.SliverFace:
                    return string.Format(CultureInfo.InvariantCulture, "SolidIssue3[{0} {1:G4} wide at {2}, {3}]", Kind, Size, at, faces);
                case SolidIssueKind.NotFlatFace:
                    return string.Format(CultureInfo.InvariantCulture, "SolidIssue3[{0} {1:G4} off flat at {2}, {3}]", Kind, Size, at, faces);
                default:
                    return string.Format(CultureInfo.InvariantCulture, "SolidIssue3[{0}, volume {1:G6}]", Kind, Size);
            }
        }
    }
}
