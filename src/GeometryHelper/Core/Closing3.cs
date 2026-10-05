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
    /// same body back says nothing was done. Made so far are the cleaning, the turning, the reading of the loops and the
    /// welding, and of the filling only its refusal of a hole where no fill is allowed. The faces are cleaned: those
    /// covering nothing within the tolerance dropped, a face given twice taken once, and of a face and a copy of it lying
    /// back to back, the one wound against the faces round it dropped. They are turned so that each shell is wound alike
    /// and outwards, a shell inside another inwards, as a cavity. An edge left open past a fin, a face standing off the
    /// surface, stops the closing there. Edges left open running back alongside each other are the two sides of a gap, and
    /// the whole body is welded shut before any loop of it is read as a hole: corners across the gaps made one and corners
    /// standing on open edges put on them, within the least reach that closes the body, up to the widest gap allowed; a gap
    /// wider is a gap too wide. What is left open is followed round into loops, a corner two holes meet at stopping the
    /// closing there, and a hole stops it where no fill is allowed. Any other body not valid after the welding is reported
    /// still open.
    /// </para>
    /// <para>
    /// Nothing thrown for a reason of the geometry leaves this: a shape the work builds refused by its constructor, or a
    /// direction asked of a vector with none, is reported as a body still open, and the log says what was thrown, as the
    /// booleans report one they could not work out.
    /// </para>
    /// </remarks>
    internal static partial class Closing3
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
                return Close(solid, options, out closed, out report);
            }
            catch (Exception exception) when (Boolean3.IsUnworkable(exception))
            {
                GeometryHelperLog.Warn($"GeoSolid3: closing a body within {solid.GetAabb()} could not be worked out, and it is reported as still open.", exception);
                return Fail(ClosingFailure.StillOpen, solid.GetAabb().Center, out closed, out report);
            }
        }

        /// <summary>
        /// Takes a body through the steps, each only where the ones before did not close it.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="options">How it is closed.</param>
        /// <param name="closed">The body closed; null when the method returns false.</param>
        /// <param name="report">What was done, or why not and where.</param>
        /// <returns>true when what comes out is valid within the options' tolerance.</returns>
        private static bool Close(GeoSolid3 solid, SolidClosingOptions options, out GeoSolid3 closed, out SolidClosing3 report)
        {
            SolidValidation3 check = solid.Validate(options.Tolerance);

            if (check.IsValid)
            {
                closed = solid;
                report = SolidClosing3.AsItWas();
                return true;
            }

            var work = new Work(solid, options, check);

            // The faces cleaned, and turned so that each shell is wound alike and outwards.
            DropFacesOfNoArea(work);
            work.Stretches = FindStretches(work.Faces, work.Dropped, work.Tolerance);
            DropCopies(work);

            if (!TryTurnAlike(work))
            {
                return Refused(work, out closed, out report);
            }

            if (work.Repairs.Count > 0 && TryTake(work, out closed, out report))
            {
                return true;
            }

            // The edges left open; a fin among them stops the closing before anything is welded or filled, since a fill
            // across the loop round it would be the fin again the other way round.
            if (!TryReadOpenEdges(work, out List<Rim> rims))
            {
                return Refused(work, out closed, out report);
            }

            // The gaps welded shut, the whole body before any loop of it is read as a hole, within the least reach that
            // closes them.
            List<GeoFace3> faces = work.Kept();

            if (TryWeldGaps(work, ref faces, ref rims, out closed, out report))
            {
                return true;
            }

            // What is left open, followed round into loops.
            if (!TryChainLoops(rims, work.Tolerance, out List<Loop> loops, out ClosingFailure failure, out GeoPoint3 at))
            {
                work.Refuse(failure, at);
                return Refused(work, out closed, out report);
            }

            RefuseHolesNotToBeFilled(work, loops);

            return Refused(work, out closed, out report);
        }

        /// <summary>
        /// Builds the body the work has come to and takes it where it is valid within the tolerance; it is kept as the body
        /// stood at, either way.
        /// </summary>
        /// <param name="work">The work.</param>
        /// <param name="closed">The body; null when the method returns false.</param>
        /// <param name="report">The report of what was done; null when the method returns false.</param>
        /// <returns>true when the body is valid.</returns>
        private static bool TryTake(Work work, out GeoSolid3 closed, out SolidClosing3 report)
        {
            closed = null;
            report = null;
            GeoSolid3 body = work.Build();

            if (body == null)
            {
                return false;
            }

            SolidValidation3 check = body.Validate(work.Tolerance);
            work.Current = body;
            work.CurrentCheck = check;

            if (!check.IsValid)
            {
                return false;
            }

            // A ring running out to a corner and straight back reads valid, and is no face of a body.
            if (NeedleAt(body.Faces, work.Tolerance.EqualPoint, out GeoPoint3 tip))
            {
                work.Refuse(ClosingFailure.StillOpen, tip);
                return false;
            }

            closed = body;
            report = new SolidClosing3(work.Repairs.ToArray(), work.AddedArea, VolumeChange(work.Solid, body), ClosingFailure.None, null);
            return true;
        }

        /// <summary>
        /// Gives a body up for the first reason a step found, or as still open, at the trouble of the body as the steps left
        /// it, where none was found.
        /// </summary>
        /// <param name="work">The work.</param>
        /// <param name="closed">Set to null.</param>
        /// <param name="report">The report of no change, the reason and the point.</param>
        /// <returns>false.</returns>
        private static bool Refused(Work work, out GeoSolid3 closed, out SolidClosing3 report)
        {
            return work.Failure != ClosingFailure.None
                ? Fail(work.Failure, work.FailureAt, out closed, out report)
                : Fail(ClosingFailure.StillOpen, TroubleAt(work.CurrentCheck, work.Current), out closed, out report);
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
        internal static double SignedVolumeOf(IReadOnlyList<GeoFace3> faces) => SignedVolumeAbout(faces, BoxOf(faces).Center);

        /// <summary>
        /// The volume faces enclose measured from a point, as <see cref="SignedVolumeOf"/> measures it from the middle of
        /// their box: so that two sets of faces that do not close can be set side by side, measured from one point.
        /// </summary>
        /// <param name="faces">The faces.</param>
        /// <param name="apex">The point.</param>
        private static double SignedVolumeAbout(IReadOnlyList<GeoFace3> faces, GeoPoint3 apex)
        {
            double total = 0.0;

            foreach (GeoFace3 face in faces)
            {
                total += Fan(face.Boundary, apex);

                // A hole is wound as the boundary is, so its fan is taken away.
                foreach (GeoPolygon3 hole in face.Holes)
                {
                    total -= Fan(hole, apex);
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

        /// <summary>
        /// A body on its way through the steps: its faces as given, which are dropped and which turned over, what was done,
        /// and the first reason found it cannot be closed.
        /// </summary>
        private sealed class Work
        {
            /// <summary>
            /// Begins the work on a body.
            /// </summary>
            /// <param name="solid">The body as given.</param>
            /// <param name="options">How it is closed.</param>
            /// <param name="check">What <see cref="GeoSolid3.Validate(Tolerance)"/> found of it within the options' tolerance.</param>
            internal Work(GeoSolid3 solid, SolidClosingOptions options, SolidValidation3 check)
            {
                Solid = solid;
                Options = options;
                Tolerance = options.Tolerance;
                Faces = solid.Faces;
                Dropped = new bool[Faces.Count];
                Turned = new bool[Faces.Count];
                Current = solid;
                CurrentCheck = check;
            }

            /// <summary>Gets the body as given.</summary>
            internal GeoSolid3 Solid { get; }

            /// <summary>Gets how it is closed.</summary>
            internal SolidClosingOptions Options { get; }

            /// <summary>Gets the tolerance it is judged within.</summary>
            internal Tolerance Tolerance { get; }

            /// <summary>Gets its faces as given, by their index.</summary>
            internal IReadOnlyList<GeoFace3> Faces { get; }

            /// <summary>Gets, for each face, whether it is dropped.</summary>
            internal bool[] Dropped { get; }

            /// <summary>Gets, for each face, whether it is turned over.</summary>
            internal bool[] Turned { get; }

            /// <summary>Gets each change made, in the order it was made.</summary>
            internal List<SolidRepair3> Repairs { get; } = new List<SolidRepair3>();

            /// <summary>Gets or sets the area of the faces added.</summary>
            internal double AddedArea { get; set; }

            /// <summary>Gets or sets the stretches the edges of the faces lie along, of the faces not of no area.</summary>
            internal List<Stretch> Stretches { get; set; }

            /// <summary>Gets or sets the last body built, the body as given until one is.</summary>
            internal GeoSolid3 Current { get; set; }

            /// <summary>Gets or sets what <see cref="GeoSolid3.Validate(Tolerance)"/> found of the last body built.</summary>
            internal SolidValidation3 CurrentCheck { get; set; }

            /// <summary>Gets the first reason found the body cannot be closed; none until one is.</summary>
            internal ClosingFailure Failure { get; private set; }

            /// <summary>Gets a point at that trouble.</summary>
            internal GeoPoint3 FailureAt { get; private set; }

            /// <summary>
            /// Notes a reason the body cannot be closed, and where, unless one was noted before: the first step that could not
            /// go on gives the reason.
            /// </summary>
            /// <param name="failure">Why.</param>
            /// <param name="at">A point at the trouble.</param>
            internal void Refuse(ClosingFailure failure, GeoPoint3 at)
            {
                if (Failure == ClosingFailure.None)
                {
                    Failure = failure;
                    FailureAt = at;
                }
            }

            /// <summary>
            /// Builds the body of the faces kept, each turned over where it is to be, in the order given, the openings
            /// carried as they are; null where fewer than four faces are kept.
            /// </summary>
            internal GeoSolid3 Build()
            {
                List<GeoFace3> kept = Kept();

                return kept.Count < 4 ? null : new GeoSolid3(kept, Solid.Openings);
            }

            /// <summary>
            /// Gets the faces kept, each turned over where it is to be, in the order given.
            /// </summary>
            internal List<GeoFace3> Kept()
            {
                var kept = new List<GeoFace3>(Faces.Count);

                for (int f = 0; f < Faces.Count; f++)
                {
                    if (!Dropped[f])
                    {
                        kept.Add(Turned[f] ? Faces[f].Flip() : Faces[f]);
                    }
                }

                return kept;
            }
        }
    }
}
