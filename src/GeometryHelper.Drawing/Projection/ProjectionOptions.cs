using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Projection
{
    /// <summary>
    /// How a view is projected: which edges count as smooth, how deep the view reaches, and where a section cuts it.
    /// </summary>
    public sealed class ProjectionOptions
    {
        /// <summary>
        /// Gets the default smooth angle, 30 degrees: a hole faceted into 12 sides or more is one curved surface.
        /// </summary>
        public const double DefaultSmoothAngleRad = Math.PI / 6.0;

        /// <summary>
        /// Gets the options of a plain view: a smooth angle of 30 degrees, no limit on depth and no section.
        /// </summary>
        public static ProjectionOptions Default { get; } = new ProjectionOptions();

        /// <summary>
        /// Initializes the options of a view.
        /// </summary>
        /// <param name="smoothAngleRad">
        /// The largest angle, in radians, between the normals of two faces of a body that are taken as facets of one curved
        /// surface; see <see cref="SmoothAngleRad"/>.
        /// </param>
        /// <param name="nearDepth">The depth, along the view's Z axis, nearest the viewer that an object has to reach behind.</param>
        /// <param name="farDepth">The depth furthest from the viewer that an object has to reach in front of.</param>
        /// <param name="sectionPlane">The plane a section view cuts the objects by; null for a plain view.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the smooth angle is not a number from nought up to a right angle, or a depth is NaN, or the near depth
        /// lies behind the far one.
        /// </exception>
        public ProjectionOptions(
            double smoothAngleRad = DefaultSmoothAngleRad,
            double nearDepth = double.PositiveInfinity,
            double farDepth = double.NegativeInfinity,
            GeoPlane3? sectionPlane = null
        )
        {
            if (!(smoothAngleRad >= 0.0) || !(smoothAngleRad <= Math.PI / 2.0))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(smoothAngleRad),
                    smoothAngleRad,
                    "A smooth angle has to be a number from nought up to a right angle."
                );
            }

            if (double.IsNaN(nearDepth) || double.IsNaN(farDepth) || !(nearDepth >= farDepth))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(nearDepth),
                    nearDepth,
                    "The near depth has to be a number no further back than the far depth."
                );
            }

            SmoothAngleRad = smoothAngleRad;
            NearDepth = nearDepth;
            FarDepth = farDepth;
            SectionPlane = sectionPlane;
        }

        /// <summary>
        /// Gets the largest angle, in radians, between the normals of two faces that meet at an edge and are taken as facets
        /// of one curved surface.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Such an edge is smooth: it is drawn only where it is a silhouette, one of its faces turned to the viewer and the
        /// other turned away or seen edge-on. An edge whose faces meet at more than this angle is sharp, and always drawn. A
        /// tube faceted into 24 sides meets at 15 degrees, so at the default of 30 degrees it is seen across as two long
        /// lines, not 13; a chamfer at 45 degrees stays two lines.
        /// </para>
        /// <para>
        /// The same angle says which chains of short lines are fitted back into arcs: a chain turning by no more than it at
        /// each corner, whose corners lie on one circle within the point tolerance. A hole of 22 faceted into 24 sides comes
        /// out as one circle of 22; a hexagon, turning by 60 degrees, stays six lines.
        /// </para>
        /// <para>
        /// Two faces meeting within the angle of the tolerance are one flat face split, and their edge is never drawn as a
        /// sharp one, whatever this angle. Faces meeting at the angle itself are smooth: a bar of 12 sides, its facets 30
        /// degrees apart, is one round bar at the default.
        /// </para>
        /// <para>
        /// The angle reads the faces, not what they were made from, and that has two traps. A bar faceted more coarsely than
        /// the angle, 8 sides at 45 degrees, shows every facet line, and its ends stay octagons. And a real corner meeting
        /// within the angle is read as smooth: a stirrup of 12 sides swept round a square corner meets at the mitre of the
        /// bend at 21 degrees as well as at 30 between its facets, so both are smooth at the default and the bend is drawn by
        /// its silhouette only; a beam kinked by 10 degrees at a haunch shows the kink only where it is a silhouette. No
        /// one angle reads both a coarse round bar and a gentle kink right.
        /// </para>
        /// </remarks>
        public double SmoothAngleRad { get; }

        /// <summary>
        /// Gets the depth, along the view's Z axis, nearest the viewer: an object lying wholly in front of it is left out.
        /// Positive infinity, the default, leaves nothing out.
        /// </summary>
        /// <remarks>
        /// An object reaching into the range between the far and the near depth is drawn whole, as Tekla Structures draws the
        /// parts its view depth takes in; it is not cut at either depth.
        /// </remarks>
        public double NearDepth { get; }

        /// <summary>
        /// Gets the depth, along the view's Z axis, furthest from the viewer: an object lying wholly behind it is left out.
        /// Negative infinity, the default, leaves nothing out.
        /// </summary>
        public double FarDepth { get; }

        /// <summary>
        /// Gets the plane a section view cuts the objects by, or null for a plain view.
        /// </summary>
        /// <remarks>
        /// What lies between the plane and the viewer is taken away, whichever way the plane's normal points; the faces the
        /// plane cuts out of each object are the view's section faces, and their outlines are drawn as
        /// <see cref="EdgeKind.SectionOutline"/>. A plane seen edge-on, its normal square to the view's Z axis within the
        /// vector tolerance, has no side away from the viewer and is refused by <see cref="ViewProjector"/>.
        /// </remarks>
        public GeoPlane3? SectionPlane { get; }
    }
}
