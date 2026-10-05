using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Closes an open solid with the least change that does it, and says what it changed, or why it could not and where.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The steps are those <see cref="GeoSolid3.TryClose(out GeoSolid3, SolidClosingOptions, out SolidClosing3)"/> lists,
    /// each tried only where the ones before it did not close the body. The reason given for a body not closed is that of
    /// the first step that could not go on, where none after it closed the body either.
    /// </para>
    /// <para>
    /// A body valid within the tolerance already is handed back as it is, the same instance, and nothing is measured: the
    /// same body back says nothing was done. So far that is the only step made: any other body is reported still open, at
    /// the first thing <see cref="GeoSolid3.Validate(Tolerance)"/> finds that makes it not valid.
    /// </para>
    /// <para>
    /// Nothing thrown for a reason of the geometry leaves this: a shape the work builds refused by its constructor, or a
    /// direction asked of a vector with none, is reported as a body still open, and the log says what was thrown, as the
    /// booleans report one they could not work out.
    /// </para>
    /// </remarks>
    internal static class Closing3
    {
        /// <summary>
        /// Closes a body as the options say; see <see cref="GeoSolid3.TryClose(out GeoSolid3, SolidClosingOptions, out SolidClosing3)"/>.
        /// </summary>
        /// <param name="solid">The body; it is not changed.</param>
        /// <param name="closed">The body closed: the body itself where it was valid already; null when the method returns false.</param>
        /// <param name="options">How the body is closed.</param>
        /// <param name="report">Each change made, or why the body could not be closed and where.</param>
        /// <returns>true when what comes out is valid within the options' tolerance; otherwise false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the body or the options are null.</exception>
        internal static bool TryClose(GeoSolid3 solid, out GeoSolid3 closed, SolidClosingOptions options, out SolidClosing3 report)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            // Checked before the work, whose own argument exceptions are a body that could not be worked out.
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            try
            {
                SolidValidation3 check = solid.Validate(options.Tolerance);

                if (check.IsValid)
                {
                    closed = solid;
                    report = SolidClosing3.AsItWas();
                    return true;
                }

                return Fail(ClosingFailure.StillOpen, TroubleAt(check, solid), out closed, out report);
            }
            catch (Exception exception) when (Boolean3.IsUnworkable(exception))
            {
                GeometryHelperLog.Warn($"GeoSolid3: closing a body within {solid.GetAabb()} could not be worked out, and it is reported as still open.", exception);
                return Fail(ClosingFailure.StillOpen, solid.GetAabb().Center, out closed, out report);
            }
        }

        /// <summary>
        /// Gives a body up: nothing kept, why, and where.
        /// </summary>
        /// <param name="failure">Why; not <see cref="ClosingFailure.None"/>.</param>
        /// <param name="at">A point at the trouble.</param>
        /// <param name="closed">Set to null.</param>
        /// <param name="report">The report of no change, the reason and the point.</param>
        /// <returns>false.</returns>
        private static bool Fail(ClosingFailure failure, GeoPoint3 at, out GeoSolid3 closed, out SolidClosing3 report)
        {
            closed = null;
            report = SolidClosing3.Failed(failure, at);
            return false;
        }

        /// <summary>
        /// A point at what makes a body not valid: the middle of the first stretch of edge it is open along, or failing that
        /// where the first other thing that makes it not valid is, in the order <see cref="SolidValidation3.Issues"/> gives
        /// them; the middle of the body's box where there is nothing.
        /// </summary>
        /// <param name="check">What <see cref="GeoSolid3.Validate(Tolerance)"/> found of the body.</param>
        /// <param name="solid">The body.</param>
        internal static GeoPoint3 TroubleAt(SolidValidation3 check, GeoSolid3 solid)
        {
            // In the order of their kinds, the open edges first and those that make a body not valid before the rest.
            foreach (SolidIssue3 issue in check.Issues)
            {
                if (issue.Kind <= SolidIssueKind.NoVolume)
                {
                    return issue.Location;
                }
            }

            return solid.GetAabb().Center;
        }

        /// <summary>
        /// The volume a body closed holds more than the faces it was closed from held, as
        /// <see cref="SolidClosing3.VolumeChange"/> reads it: each measured by <see cref="SignedVolumeOf"/>, whichever way
        /// round it is wound, the openings not cut out.
        /// </summary>
        /// <param name="given">The body as it was given.</param>
        /// <param name="result">The body closed.</param>
        internal static double VolumeChange(GeoSolid3 given, GeoSolid3 result)
            => Math.Abs(SignedVolumeOf(result.Faces)) - Math.Abs(SignedVolumeOf(given.Faces));

        /// <summary>
        /// The volume faces enclose measured from the middle of their box: each the fan of its boundary from its first
        /// corner less the fans of its holes, as a body without openings is measured, the fans taken as cones to that point.
        /// </summary>
        /// <param name="faces">The faces; they need not close.</param>
        /// <returns>
        /// The volume, positive where the faces are wound outwards and negative where inwards: for faces closed within the
        /// tolerance, the volume they enclose.
        /// </returns>
        /// <remarks>
        /// Faces that close enclose the same volume measured from anywhere, and from the middle of their box every cone is
        /// the size of the body, as <see cref="Mass3"/> measures from there, so that nothing is lost to the size of the
        /// coordinates. Faces that do not close enclose a volume only from where they are measured, and the middle of their
        /// box is a point the same faces give in whatever order they come.
        /// </remarks>
        internal static double SignedVolumeOf(IReadOnlyList<GeoFace3> faces)
        {
            GeoAabb3 box = GeoAabb3.Empty;

            foreach (GeoFace3 face in faces)
            {
                box = box.Union(face.GetAabb());
            }

            GeoPoint3 middle = box.Center;
            double total = 0.0;

            foreach (GeoFace3 face in faces)
            {
                total += Fan(face.Boundary, middle);

                // A hole is wound as the boundary is, so its fan is taken away.
                foreach (GeoPolygon3 hole in face.Holes)
                {
                    total -= Fan(hole, middle);
                }
            }

            return total / 6.0;
        }

        /// <summary>
        /// Six times the volume of the fan of a ring from its first corner, each triangle of it taken as a tetrahedron to a
        /// point.
        /// </summary>
        /// <param name="ring">The ring.</param>
        /// <param name="apex">The point.</param>
        private static double Fan(GeoPolygon3 ring, GeoPoint3 apex)
        {
            IReadOnlyList<GeoPoint3> corners = ring.Vertices;
            GeoVector3 first = apex.GetVectorTo(corners[0]);
            double sum = 0.0;

            for (int i = 1; i + 1 < corners.Count; i++)
            {
                sum += first.TripleProduct(apex.GetVectorTo(corners[i]), apex.GetVectorTo(corners[i + 1]));
            }

            return sum;
        }
    }
}
