using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper
{
    /// <summary>
    /// What <see cref="GeoSolid3.Validate(Tolerance)"/> found of the faces of a body: whether they bound one, and each
    /// thing wrong with them, or worth knowing, and where.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="GeoSolid3.IsClosed(Tolerance)"/> says whether the faces close; this says where they do not, and what
    /// else it finds on the way. The edges are matched as it matches them, by overlap along a line within the point
    /// tolerance, so the two agree on whether the body is closed: <see cref="IsClosed"/> is its answer, with every stretch
    /// it left open in <see cref="Issues"/>. It also reads which way the faces meeting on a stretch run it, which
    /// <c>IsClosed</c> does not: a face wound the wrong way round closes the body for it, and is a
    /// <see cref="SolidIssueKind.SameWayEdge"/> on each of its edges here.
    /// </para>
    /// <para>
    /// The faces are read, the openings not: each opening is a body of its own, and is checked by asking it.
    /// </para>
    /// </remarks>
    public sealed class SolidValidation3
    {
        private readonly SolidIssue3[] _issues;

        private SolidValidation3(SolidIssue3[] issues, double signedVolume)
        {
            _issues = issues;
            SignedVolume = signedVolume;
            IsClosed = !issues.Any(i => i.Kind == SolidIssueKind.OpenEdge);
            IsWoundAlike = !issues.Any(i => i.Kind == SolidIssueKind.SameWayEdge);
            IsValid = !issues.Any(i => i.Kind <= SolidIssueKind.NoVolume);
        }

        /// <summary>
        /// Gets whether the faces bound a body every boolean and measure can read: closed, wound alike and outwards, and
        /// enclosing more than the tolerance; no issue of the first four kinds of <see cref="SolidIssueKind"/>.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Gets whether the faces close: no <see cref="SolidIssueKind.OpenEdge"/>, as
        /// <see cref="GeoSolid3.IsClosed(Tolerance)"/> answers.
        /// </summary>
        public bool IsClosed { get; }

        /// <summary>
        /// Gets whether the faces meeting on each stretch of edge run it as many times one way as the other: no
        /// <see cref="SolidIssueKind.SameWayEdge"/>.
        /// </summary>
        public bool IsWoundAlike { get; }

        /// <summary>
        /// Gets the volume the faces enclose, the openings not cut out: positive where they are wound outwards, negative
        /// where inwards, and of no meaning where they do not close.
        /// </summary>
        public double SignedVolume { get; }

        /// <summary>
        /// Gets everything found, in the order of <see cref="SolidIssueKind"/>, and of each kind in the order the faces
        /// come.
        /// </summary>
        public IReadOnlyList<SolidIssue3> Issues => _issues;

        /// <summary>
        /// Checks the faces of a body.
        /// </summary>
        internal static SolidValidation3 Of(GeoSolid3 solid, Tolerance tolerance)
        {
            IReadOnlyList<GeoFace3> faces = solid.Faces;
            var issues = new List<SolidIssue3>();

            foreach (Shells3.Stretch stretch in Shells3.UnevenStretches(faces, tolerance))
            {
                int count = stretch.Forward + stretch.Backward;
                SolidIssueKind kind = count % 2 != 0 ? SolidIssueKind.OpenEdge
                    : stretch.Forward != stretch.Backward ? SolidIssueKind.SameWayEdge
                    : SolidIssueKind.NonManifoldEdge;

                issues.Add(SolidIssue3.OnEdge(kind, new GeoLine3(stretch.Start, stretch.End), stretch.Faces, stretch.Forward, stretch.Backward));
            }

            double area = 0.0;

            for (int f = 0; f < faces.Count; f++)
            {
                GeoFace3 face = faces[f];
                double perimeter = 0.0;
                area += face.Area;

                AddShortEdges(face.Boundary, f, tolerance, issues, ref perimeter);

                foreach (GeoPolygon3 hole in face.Holes)
                {
                    AddShortEdges(hole, f, tolerance, issues, ref perimeter);
                }

                if (face.Area <= 0.5 * tolerance.EqualPoint * perimeter)
                {
                    issues.Add(SolidIssue3.OfFace(SolidIssueKind.SliverFace, f, face.Centroid, perimeter > 0.0 ? 2.0 * face.Area / perimeter : 0.0));
                }

                double off = OffFlat(face);

                if (off > tolerance.EqualPlanar)
                {
                    issues.Add(SolidIssue3.OfFace(SolidIssueKind.NotFlatFace, f, face.Centroid, off));
                }
            }

            double volume = solid.GetSignedVolume();
            GeoPoint3 middle = solid.GetAabb().Center;

            // As Shells3 reads a group of faces enclosing nothing: no thicker than the tolerance on average.
            if (Math.Abs(volume) <= tolerance.EqualPoint * area)
            {
                issues.Add(SolidIssue3.OfBody(SolidIssueKind.NoVolume, middle, volume));
            }
            else if (volume < 0.0)
            {
                issues.Add(SolidIssue3.OfBody(SolidIssueKind.InsideOut, middle, volume));
            }

            // Stable, so each kind keeps the order the faces gave it.
            SolidIssue3[] sorted = issues.Select((issue, index) => (issue, index))
                .OrderBy(x => x.issue.Kind).ThenBy(x => x.index).Select(x => x.issue).ToArray();

            return new SolidValidation3(sorted, volume);
        }

        private static void AddShortEdges(GeoPolygon3 ring, int face, Tolerance tolerance, List<SolidIssue3> issues, ref double perimeter)
        {
            for (int i = 0; i < ring.VertexCount; i++)
            {
                GeoPoint3 from = ring[i];
                GeoPoint3 to = ring[(i + 1) % ring.VertexCount];
                double length = from.DistanceTo(to);
                perimeter += length;

                if (length <= tolerance.EqualPoint)
                {
                    issues.Add(SolidIssue3.ShortOf(face, new GeoLine3(from, to)));
                }
            }
        }

        /// <summary>
        /// How far the corner of a face furthest off the plane of its first corner stands off it, as the booleans read it.
        /// </summary>
        private static double OffFlat(GeoFace3 face)
        {
            GeoPlane3 plane = face.GetPlane();
            double off = 0.0;

            foreach (GeoPoint3 corner in face.Boundary.Vertices)
            {
                off = Math.Max(off, Math.Abs(plane.SignedDistanceTo(corner)));
            }

            foreach (GeoPolygon3 hole in face.Holes)
            {
                foreach (GeoPoint3 corner in hole.Vertices)
                {
                    off = Math.Max(off, Math.Abs(plane.SignedDistanceTo(corner)));
                }
            }

            return off;
        }

        /// <summary>
        /// Says whether the body is valid, its volume, and how many of each kind were found, in invariant culture.
        /// </summary>
        public override string ToString()
        {
            string counts = string.Join(", ", _issues.GroupBy(i => i.Kind).Select(g => string.Format(CultureInfo.InvariantCulture, "{0} {1}", g.Key, g.Count())));

            return string.Format(
                CultureInfo.InvariantCulture,
                "SolidValidation3[{0}, volume {1:G6}{2}]",
                IsValid ? "valid" : "not valid",
                SignedVolume,
                counts.Length > 0 ? "; " + counts : string.Empty);
        }
    }
}
