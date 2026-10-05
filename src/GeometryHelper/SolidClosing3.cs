using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper
{
    /// <summary>
    /// What <see cref="GeoSolid3.TryClose(out GeoSolid3, SolidClosingOptions, out SolidClosing3)"/> did to close a body:
    /// each change it made, the area it added and the volume it moved, or why it could not close it and where.
    /// </summary>
    /// <remarks>
    /// A body closed reports every change in the order it was made, and none where it was valid already. A body that could
    /// not be closed reports no change, since none was kept: the reason, of the first step that could not go on, and a point
    /// at the trouble.
    /// </remarks>
    public sealed class SolidClosing3
    {
        private static readonly SolidRepair3[] NoRepairs = new SolidRepair3[0];

        /// <summary>
        /// Initializes a report.
        /// </summary>
        /// <param name="repairs">Each change made, in the order it was made.</param>
        /// <param name="addedArea">The area of the faces added.</param>
        /// <param name="volumeChange">The volume moved, as <see cref="VolumeChange"/> reads it.</param>
        /// <param name="failure">Why the body could not be closed; <see cref="ClosingFailure.None"/> where it was.</param>
        /// <param name="failureLocation">A point at the trouble; null where the body was closed.</param>
        internal SolidClosing3(IReadOnlyList<SolidRepair3> repairs, double addedArea, double volumeChange, ClosingFailure failure, GeoPoint3? failureLocation)
        {
            Repairs = repairs;
            AddedArea = addedArea;
            VolumeChange = volumeChange;
            Failure = failure;
            FailureLocation = failureLocation;
        }

        /// <summary>
        /// Gets each change made, in the order it was made; empty where nothing was done, the body being valid already or
        /// not closed.
        /// </summary>
        public IReadOnlyList<SolidRepair3> Repairs { get; }

        /// <summary>
        /// Gets the area of the faces added, those stitching cracks and those filling holes; nought where none was.
        /// </summary>
        public double AddedArea { get; }

        /// <summary>
        /// Gets the volume of the body closed less the volume of the faces given, the openings not cut out of either;
        /// nought where nothing was done.
        /// </summary>
        /// <remarks>
        /// Faces that do not close enclose no volume of their own, and the faces given are measured from the middle of
        /// their box, each the fan of its boundary from its first corner less those of its holes, whichever way round the
        /// whole is wound: for a body closed within the tolerance that is its volume. A box with a face left out is
        /// measured as five pyramids on its middle, the sixth missing: a cube of side 1 closed by its sixth face gains a
        /// sixth of its volume, and one only welded shut moves by no more than the gap times the area it touched.
        /// </remarks>
        public double VolumeChange { get; }

        /// <summary>
        /// Gets why the body could not be closed: <see cref="ClosingFailure.None"/> where it was closed, or was valid already.
        /// </summary>
        public ClosingFailure Failure { get; }

        /// <summary>
        /// Gets a point at the trouble where the body could not be closed, such as the middle of an open edge or of the
        /// hole that could not be filled; null where it was closed.
        /// </summary>
        public GeoPoint3? FailureLocation { get; }

        /// <summary>
        /// The report of a body valid already: nothing done.
        /// </summary>
        internal static SolidClosing3 AsItWas() => new SolidClosing3(NoRepairs, 0.0, 0.0, ClosingFailure.None, null);

        /// <summary>
        /// The report of a body that could not be closed: nothing kept, why, and where.
        /// </summary>
        /// <param name="failure">Why; not <see cref="ClosingFailure.None"/>.</param>
        /// <param name="at">A point at the trouble.</param>
        internal static SolidClosing3 Failed(ClosingFailure failure, GeoPoint3 at) => new SolidClosing3(NoRepairs, 0.0, 0.0, failure, at);

        /// <summary>
        /// Says whether the body was closed, with how many changes of each kind, the area added and the volume moved, or why
        /// not and where, in invariant culture.
        /// </summary>
        public override string ToString()
        {
            if (Failure != ClosingFailure.None)
            {
                GeoPoint3 at = FailureLocation ?? GeoPoint3.Origin;

                return string.Format(
                    CultureInfo.InvariantCulture,
                    "SolidClosing3[not closed, {0} at ({1:0.###}, {2:0.###}, {3:0.###})]",
                    Failure,
                    at.X,
                    at.Y,
                    at.Z);
            }

            if (Repairs.Count == 0)
            {
                return "SolidClosing3[closed as it was]";
            }

            // In the order each kind was first made.
            string counts = string.Join(", ", Repairs.GroupBy(r => r.Kind).Select(g => string.Format(CultureInfo.InvariantCulture, "{0} {1}", g.Key, g.Count())));

            return string.Format(
                CultureInfo.InvariantCulture,
                "SolidClosing3[closed; {0}; area added {1:G6}, volume moved {2:G6}]",
                counts,
                AddedArea,
                VolumeChange);
        }
    }
}
