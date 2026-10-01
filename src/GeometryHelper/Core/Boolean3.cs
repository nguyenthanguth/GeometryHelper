using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Combines solids: the union of two bodies, the part they share, and one taken out of the other.
    /// <para>
    /// The method is the same one <c>Splition3</c> uses to cut a plate against a body, carried up a
    /// dimension. One body is divided by the planes of the other's faces that come near it, which leaves
    /// cells that are each wholly inside or wholly outside the other body, since the surface of a body never
    /// leaves the planes of its own faces. The cells wanted for the operation are then glued back together:
    /// a face shared by two kept cells appears twice, once each way round, and dropping both leaves exactly
    /// the outer skin.
    /// </para>
    /// <para>
    /// Dividing A by the planes of B already lays a face along every part of the surface of B that runs
    /// through A. That is why an intersection is just the cells of A that fall inside B, and a difference
    /// just the cells of A that fall outside it: the walls of the cavity are already there, and adding the
    /// faces of B on top of them would describe the same surface twice.
    /// </para>
    /// <para>
    /// So every operation cuts one body only, the one fewer planes cut, and never by its own faces: they bound
    /// it already, and cutting by them is what took a bent bar apart into thousands of cells, since every
    /// plane of a bend runs on through the rest of the bar. A union keeps the part of the cut body beyond the
    /// other and the other whole; a difference cutting the body taken away keeps the body taken from whole,
    /// and the part of the other within it turned inside out. Where the whole body and the cells meet, their
    /// faces lie back to back in one plane, cut differently, and cancel by the area they share. Where a plane
    /// crosses a cell and leaves it whole, the other body is cut instead, and failing that both are cut by
    /// every plane of both, as they once always were.
    /// </para>
    /// <para>
    /// Every operation reports <c>false</c> when the answer is nothing at all — two bodies that do not
    /// touch have no shared part, and a body wholly swallowed by another leaves nothing behind. That is
    /// an outcome rather than a failure, which is why it comes back as <c>false</c> with no result rather
    /// than as an exception or an empty body.
    /// </para>
    /// <para>
    /// An opening on either body is honoured. A union or a difference cuts the openings into both bodies
    /// first (<see cref="TryCutOpenings(GeoSolid3, out GeoSolid3, Tolerance)"/>), so their faces are where
    /// their material ends; an intersection adds the planes of its own openings near the other to its knives,
    /// so no cell straddles a wall, and drops the cells filling them as not being material. Either way the
    /// result carries the cavity as real geometry rather than as an opening of its own. Two bodies too far
    /// apart to meet are the exception — nothing is cut there, and each keeps the openings it came with.
    /// </para>
    /// </summary>
    public static partial class Boolean3
    {
        /// <summary>
        /// Joins two solids into one, using the default tolerance.
        /// </summary>
        public static bool TryUnion(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result)
        {
            return TryUnion(first, second, out result, Tolerance.Global);
        }

        /// <summary>
        /// Joins two solids into one, within a tolerance.
        /// </summary>
        /// <param name="first">The first body.</param>
        /// <param name="second">The second body.</param>
        /// <param name="result">The combined body.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>false when the two could not be combined into a closed body, which is logged.</returns>
        /// <remarks>
        /// Two bodies that do not touch still combine: the result is one solid carrying both shells, which
        /// measures and answers containment correctly because each shell is closed and wound outwards.
        /// </remarks>
        public static bool TryUnion(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, Tolerance tolerance)
        {
            Guard(first, second);

            try
            {
                return Unite(first, second, out result, tolerance);
            }
            catch (Exception exception) when (IsUnworkable(exception))
            {
                return Unworkable("union", exception, out result);
            }
        }

        private static bool Unite(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, Tolerance tolerance)
        {
            result = null;

            if (!first.GetAabb().CollidesWith(second.GetAabb(), tolerance))
            {
                // Nothing to resolve: the two shells simply sit side by side. Neither body reaches the
                // other, so whatever each has carved out of itself is still carved out of the pair.
                List<GeoFace3> apart = new List<GeoFace3>(first.Faces);
                apart.AddRange(second.Faces);

                List<GeoSolid3> carved = new List<GeoSolid3>(first.Openings);
                carved.AddRange(second.Openings);

                result = new GeoSolid3(apart, carved);
                return true;
            }

            tolerance = ForWork(tolerance);
            first = FlatForWork(first, tolerance);
            second = FlatForWork(second, tolerance);

            // One body cut, beyond the other, and the other whole.
            GeoSolid3 openCut = null;

            if (TryCutOpenings(first, out GeoSolid3 a, tolerance) && TryCutOpenings(second, out GeoSolid3 b, tolerance))
            {
                bool? found = CombineCuttingOne(a, b, true, tolerance, out result, out _);

                if (found.HasValue)
                {
                    return found.Value;
                }

                openCut = result;
            }

            List<GeoPlane3> planes = SharedPlanes(first, second, tolerance);

            List<GeoFace3> kept = new List<GeoFace3>();

            // All of the first body, and only the part of the second that reaches beyond it: the shared
            // region belongs to the union once, and it is already carried by the first.
            kept.AddRange(FacesOfCells(SplitIntoCells(first, planes, tolerance), first, tolerance));

            kept.AddRange(FacesOfCells(SplitIntoCells(second, planes, tolerance), second, first, false, tolerance));

            return GlueOrKeep(kept, openCut, tolerance, out result);
        }

        /// <summary>
        /// Gets the part two solids have in common, using the default tolerance.
        /// </summary>
        public static bool TryIntersect(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result)
        {
            return TryIntersect(first, second, out result, Tolerance.Global);
        }

        /// <summary>
        /// Gets the part two solids have in common, within a tolerance.
        /// </summary>
        /// <returns>false when the two bodies share no volume, or when it cannot be worked out, which is logged.</returns>
        /// <remarks>
        /// Only one body is cut, and only by the planes of the other that come near it and of its own openings:
        /// those are what leave every cell wholly inside or wholly outside the other, and wholly material or wholly
        /// carved out. Its own outer faces bound it already and need not cut it. On a bent bar they would: every
        /// plane of a bend runs on through the rest of the bar, and a bar with a hook came out in thousands of
        /// cells, twenty seconds against a plate. The body cut is the one fewer planes cut, so a bar crossing a
        /// beam is cut by a handful of the beam's planes rather than the beam by the hundreds of the bar's.
        /// </remarks>
        public static bool TryIntersect(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, Tolerance tolerance)
        {
            Guard(first, second);

            try
            {
                return Share(first, second, out result, tolerance);
            }
            catch (Exception exception) when (IsUnworkable(exception))
            {
                return Unworkable("intersection", exception, out result);
            }
        }

        private static bool Share(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, Tolerance tolerance)
        {
            result = null;

            if (!first.GetAabb().CollidesWith(second.GetAabb(), tolerance))
            {
                return false;
            }

            tolerance = ForWork(tolerance);
            first = FlatForWork(first, tolerance);
            second = FlatForWork(second, tolerance);

            List<GeoPlane3> cuttingFirst = KnivesFor(first, second, tolerance);
            List<GeoPlane3> cuttingSecond = KnivesFor(second, first, tolerance);
            bool firstIsCut = cuttingFirst.Count <= cuttingSecond.Count;

            GeoSolid3 cut = firstIsCut ? first : second;
            GeoSolid3 other = firstIsCut ? second : first;
            List<GeoPlane3> knives = firstIsCut ? cuttingFirst : cuttingSecond;
            List<GeoPlane3> otherKnives = firstIsCut ? cuttingSecond : cuttingFirst;

            List<GeoFace3> kept = CellsInside(cut, other, knives, tolerance, out bool clean);
            bool otherWayAffordable = otherKnives.Count <= 2 * knives.Count + 16;

            // A plane can still cross a cell and leave it whole where the cut does not close — a body running
            // through itself, say — and the cell is then judged by one point for both sides of the plane. Cut the
            // other way, the trouble falls elsewhere, so the other body is cut before that is settled for, when that
            // costs about the same: a bar's hundreds of planes would cut a beam into thousands of cells again.
            // Slivers were the common cause, and the cut keeps those now; see LoopAssembly.ForPieces.
            if (!clean && otherWayAffordable)
            {
                List<GeoFace3> otherWay = CellsInside(other, cut, otherKnives, tolerance, out bool otherClean);

                if (otherClean)
                {
                    kept = otherWay;
                }
            }

            if (!TryGlue(kept, tolerance, out result))
            {
                return false;
            }

            // Cut cleanly and still open, two closed bodies can close cut the other way round, as a difference can;
            // see CombineCuttingOne.
            if (clean && otherWayAffordable && !result.IsClosed(tolerance) && first.IsClosed(tolerance) && second.IsClosed(tolerance)
                && TryGlue(CellsInside(other, cut, otherKnives, tolerance, out bool closedClean), tolerance, out GeoSolid3 otherResult)
                && closedClean && otherResult.IsClosed(tolerance))
            {
                result = otherResult;
            }

            return true;
        }

        /// <summary>
        /// Collects the faces of the cells of one body that lie inside another, the body cut by the planes given.
        /// </summary>
        /// <param name="body">The body to cut.</param>
        /// <param name="other">The body whose inside is wanted.</param>
        /// <param name="knives">The planes to cut by; see <see cref="KnivesFor"/>.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="clean">false when a plane crossed a cell and still left it whole.</param>
        private static List<GeoFace3> CellsInside(GeoSolid3 body, GeoSolid3 other, List<GeoPlane3> knives, Tolerance tolerance, out bool clean)
            => FacesOfCells(SplitIntoCells(body, knives, tolerance, out clean), body, other, true, tolerance);

        /// <summary>
        /// Takes one solid out of another, using the default tolerance.
        /// </summary>
        public static bool TrySubtract(GeoSolid3 subject, GeoSolid3 tool, out GeoSolid3 result)
        {
            return TrySubtract(subject, tool, out result, Tolerance.Global);
        }

        /// <summary>
        /// Takes one solid out of another, within a tolerance.
        /// </summary>
        /// <param name="subject">The body to cut material from.</param>
        /// <param name="tool">The body to remove.</param>
        /// <param name="result">What is left of the subject.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// false when nothing is left, because the tool swallowed the subject whole, or when it cannot be worked out,
        /// which is logged.
        /// </returns>
        /// <remarks>
        /// The walls of the cavity are not taken from the tool; they are already there. Dividing the
        /// subject by the planes of the tool lays a face along every part of the surface of the tool that
        /// passes through it, so keeping the cells that fall outside the tool keeps those faces with them.
        /// A tool that misses the subject changes nothing, and the subject comes back unaltered.
        /// </remarks>
        public static bool TrySubtract(GeoSolid3 subject, GeoSolid3 tool, out GeoSolid3 result, Tolerance tolerance)
        {
            Guard(subject, tool);

            try
            {
                return TakeAway(subject, tool, out result, tolerance);
            }
            catch (Exception exception) when (IsUnworkable(exception))
            {
                return Unworkable("difference", exception, out result);
            }
        }

        private static bool TakeAway(GeoSolid3 subject, GeoSolid3 tool, out GeoSolid3 result, Tolerance tolerance)
        {
            result = null;

            if (!subject.GetAabb().CollidesWith(tool.GetAabb(), tolerance))
            {
                result = subject;
                return true;
            }

            GeoSolid3 given = subject;
            tolerance = ForWork(tolerance);
            subject = FlatForWork(subject, tolerance);
            tool = FlatForWork(tool, tolerance);

            // One body cut: the subject beyond the tool, or the subject whole less the tool within it.
            GeoSolid3 openCut = null;

            if (TryCutOpenings(subject, out GeoSolid3 a, tolerance) && TryCutOpenings(tool, out GeoSolid3 b, tolerance))
            {
                bool? found = CombineCuttingOne(a, b, false, tolerance, out result, out bool untouched);

                if (untouched)
                {
                    // A tool with no material within the subject's takes none of it, as one too far away to meet it
                    // takes none.
                    result = given;
                    return true;
                }

                if (found.HasValue)
                {
                    return found.Value;
                }

                openCut = result;
            }

            List<GeoPlane3> planes = SharedPlanes(subject, tool, tolerance);

            List<GeoFace3> kept = FacesOfCells(SplitIntoCells(subject, planes, tolerance), subject, tool, false, tolerance);

            return GlueOrKeep(kept, openCut, tolerance, out result);
        }

        /// <summary>
        /// Glues the cells of both bodies cut by every plane of both, unless that does no better than an open result
        /// already found by cutting one of them, which is then kept, as it was before cutting both was tried.
        /// </summary>
        private static bool GlueOrKeep(List<GeoFace3> kept, GeoSolid3 openCut, Tolerance tolerance, out GeoSolid3 result)
        {
            if (TryGlue(kept, tolerance, out result) && (openCut == null || result.IsClosed(tolerance)))
            {
                return true;
            }

            result = openCut;
            return openCut != null;
        }

        /// <summary>
        /// Joins two bodies without openings, or takes the second out of the first, cutting one of them only.
        /// </summary>
        /// <param name="a">The first body; for a difference, the one material is taken from.</param>
        /// <param name="b">The second body; for a difference, the one taken away.</param>
        /// <param name="union">true for the union, false for <paramref name="a"/> less <paramref name="b"/>.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="result">The result, when there is one.</param>
        /// <param name="untouched">
        /// For a difference, true when a clean cut found no material of either body within the other: the second takes
        /// nothing from the first, and <paramref name="result"/> is left null for the caller to hand the first back as
        /// it came.
        /// </param>
        /// <returns>
        /// Whether anything is left, as the public methods report it; null when neither body could be cut cleanly, or
        /// two closed bodies came out open whichever was cut, for the caller to cut both by every plane of both
        /// instead. An open result found on the way is then left in <paramref name="result"/>.
        /// </returns>
        /// <remarks>
        /// The body cut is the one fewer planes of the other come near, as for an intersection. A union keeps the
        /// cells of it beyond the other, and the other whole. For a difference, cutting the body material is taken
        /// from keeps its cells beyond the other; cutting the body taken away keeps the other whole and the cells
        /// within it turned inside out, which are the walls of the cavity and take away the part of the whole
        /// body's faces it covers. Where the whole body meets the cells, the two lie back to back in one plane,
        /// each cut its own way, and <see cref="CancelBackToBack"/> takes from both the area they share.
        /// <para>
        /// A difference that finds nothing of the one body within the other takes nothing, and is not glued back
        /// together from the cells at all. Glued, it gave back only what the cutting rounded: two slabs side by side
        /// within a hundredth of a millimetre of each other, the larger cut along seventy metres by planes of the
        /// smaller a few thousandths off its own faces, came back twenty thousand cubic millimetres larger than it was.
        /// </para>
        /// </remarks>
        private static bool? CombineCuttingOne(GeoSolid3 a, GeoSolid3 b, bool union, Tolerance tolerance, out GeoSolid3 result, out bool untouched)
        {
            result = null;
            untouched = false;

            List<GeoPlane3> cuttingA = PlanesNear(b, a.GetAabb(), tolerance);
            List<GeoPlane3> cuttingB = PlanesNear(a, b.GetAabb(), tolerance);
            bool aFirst = cuttingA.Count <= cuttingB.Count;
            GeoSolid3 open = null;

            for (int attempt = 0; attempt < 2; attempt++)
            {
                bool cutA = aFirst == (attempt == 0);
                List<GeoPlane3> knives = cutA ? cuttingA : cuttingB;

                // The other way round only when it costs about the same: a bar's hundreds of planes would cut a
                // beam into thousands of cells again.
                if (attempt == 1 && knives.Count > 2 * (cutA ? cuttingB : cuttingA).Count + 16)
                {
                    break;
                }

                GeoSolid3 cut = cutA ? a : b;
                GeoSolid3 whole = cutA ? b : a;
                bool within = !union && !cutA;

                List<GeoFace3> kept = FacesOfCells(SplitIntoCells(cut, knives, tolerance, out bool clean), cut, whole, within, tolerance, out bool anyInside);

                if (!clean)
                {
                    continue;
                }

                if (!union && !anyInside)
                {
                    untouched = true;
                    return true;
                }

                if (within)
                {
                    for (int i = 0; i < kept.Count; i++)
                    {
                        kept[i] = kept[i].Flip();
                    }
                }

                if (union || within)
                {
                    kept.AddRange(whole.Faces);
                }

                if (!TryGlue(kept, tolerance, out result))
                {
                    return false;
                }

                // Cut cleanly and still open, two closed bodies can close cut the other way round, or both cut: a beam
                // turned a hundredth of a degree off the axes, less an opening, came out open with the beam cut and
                // closed with the opening cut. The open one is kept in case nothing does better.
                if (result.IsClosed(tolerance) || !a.IsClosed(tolerance) || !b.IsClosed(tolerance))
                {
                    return true;
                }

                open = open ?? result;
            }

            result = open;
            return null;
        }

        #region Machinery

        /// <summary>
        /// Whether an exception is one a boolean that cannot be worked out throws: a shape the work built refused by its
        /// constructor, or a direction asked of a vector with none. A boolean that tries says so by returning false.
        /// </summary>
        /// <remarks>
        /// Thrown out of a boolean, one of these took down whatever asked for it: the conversion of a whole IFC model,
        /// when the GeoSolid3 boolean cut the openings of one beam in it. The known causes are fixed; this keeps an
        /// unknown one to the boolean it happens in, and says so in the log.
        /// </remarks>
        internal static bool IsUnworkable(Exception exception)
            => exception is ArgumentException || exception is InvalidOperationException;

        /// <summary>
        /// Reports a boolean that could not be worked out: false, nothing made, and a warning with what was thrown.
        /// </summary>
        private static bool Unworkable(string operation, Exception exception, out GeoSolid3 result)
        {
            GeometryHelperLog.Warn($"A solid {operation} could not be worked out and is reported as not made.", exception);
            result = null;
            return false;
        }

        /// <summary>
        /// Rejects null arguments for every operation in one place.
        /// </summary>
        private static void Guard(GeoSolid3 first, GeoSolid3 second)
        {
            if (first == null)
            {
                throw new ArgumentNullException(nameof(first));
            }

            if (second == null)
            {
                throw new ArgumentNullException(nameof(second));
            }
        }

        /// <summary>
        /// Gets the tolerance a boolean cuts and glues within: the one given, with a planar threshold no wider than
        /// the point one.
        /// </summary>
        /// <remarks>
        /// The cut and the glue have to agree. A plane is taken to leave a cell whole when no corner of the cell
        /// stands off it by more than the planar tolerance, so the cell can stand past it by as much, and the glue
        /// matches corners within the point tolerance. A planar tolerance wider than the point one leaves more
        /// than the glue can close: two faces of a bent bar half a degree apart, whose far corners come within
        /// five hundredths of each other's planes, came out open at a planar tolerance of five hundredths and a
        /// point tolerance of one.
        /// </remarks>
        internal static Tolerance ForWork(Tolerance tolerance)
        {
            return tolerance.EqualPlanar <= tolerance.EqualPoint
                ? tolerance
                : new Tolerance(tolerance.EqualPoint, tolerance.EqualVector, tolerance.EqualAngleRad, tolerance.EqualPoint);
        }

        /// <summary>
        /// Gets a body as a boolean works on it: every face flat within the planar tolerance of the work.
        /// </summary>
        /// <remarks>
        /// A face can be flat only to the wider planar tolerance a caller allowed: a modeller's cut leaves some a
        /// few hundredths out, as Tekla Structures left the top face of a notched beam 0.04 mm out at one corner.
        /// Cut at the tighter tolerance of the work, a piece of such a face is refused as not flat and leaves a
        /// hole. It is split into triangles on its own corners first instead, each exactly flat, which keeps the
        /// body closed and moves nothing. A body with every face flat enough comes back as it is.
        /// </remarks>
        internal static GeoSolid3 FlatForWork(GeoSolid3 solid, Tolerance work)
        {
            List<GeoFace3> faces = null;

            for (int i = 0; i < solid.Faces.Count; i++)
            {
                GeoFace3 face = solid.Faces[i];

                if (IsFlatWithin(face, work.EqualPlanar))
                {
                    faces?.Add(face);
                    continue;
                }

                if (faces == null)
                {
                    faces = new List<GeoFace3>(solid.Faces.Count + 4);

                    for (int j = 0; j < i; j++)
                    {
                        faces.Add(solid.Faces[j]);
                    }
                }

                if (!EarClipping.TryTriangulate(face, work, out GeoTriangle3[] triangles))
                {
                    // Nothing better to offer: the face goes in as it is, and the cut does what it can with it.
                    faces.Add(face);
                    continue;
                }

                foreach (GeoTriangle3 triangle in triangles)
                {
                    try
                    {
                        faces.Add(triangle.ToFace3(work));
                    }
                    catch (ArgumentException)
                    {
                        // A triangle with no area covers nothing, and its neighbours meet along its edges without it.
                    }
                }
            }

            List<GeoSolid3> openings = null;

            for (int i = 0; i < solid.Openings.Count; i++)
            {
                GeoSolid3 flat = FlatForWork(solid.Openings[i], work);

                if (openings == null && !ReferenceEquals(flat, solid.Openings[i]))
                {
                    openings = new List<GeoSolid3>(solid.Openings.Count);

                    for (int j = 0; j < i; j++)
                    {
                        openings.Add(solid.Openings[j]);
                    }
                }

                openings?.Add(flat);
            }

            if (faces == null && openings == null)
            {
                return solid;
            }

            return new GeoSolid3(faces ?? new List<GeoFace3>(solid.Faces), openings ?? new List<GeoSolid3>(solid.Openings));
        }

        /// <summary>
        /// Determines whether every corner of a face, holes and all, lies within a distance of its plane.
        /// </summary>
        private static bool IsFlatWithin(GeoFace3 face, double planar)
        {
            GeoPlane3 plane = face.GetPlane();

            foreach (GeoPoint3 corner in face.Boundary.Vertices)
            {
                if (Math.Abs(plane.SignedDistanceTo(corner)) > planar)
                {
                    return false;
                }
            }

            foreach (GeoPolygon3 hole in face.Holes)
            {
                foreach (GeoPoint3 corner in hole.Vertices)
                {
                    if (Math.Abs(plane.SignedDistanceTo(corner)) > planar)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Collects the face planes of both bodies, with duplicates removed.
        /// </summary>
        /// <remarks>
        /// Both bodies are cut by this one set rather than each by the other. That costs a few more cells
        /// and buys the thing that makes gluing work: where the two bodies meet, both sides have been
        /// carved by the same knives, so the two faces at the interface are the same polygon and cancel.
        /// </remarks>
        private static List<GeoPlane3> SharedPlanes(GeoSolid3 first, GeoSolid3 second, Tolerance tolerance)
        {
            List<GeoPlane3> planes = Splition3.CollectFacePlanes(first, tolerance);

            foreach (GeoPlane3 plane in Splition3.CollectFacePlanes(second, tolerance))
            {
                bool known = false;

                foreach (GeoPlane3 existing in planes)
                {
                    if (existing.IsEqualTo(plane, tolerance) || existing.IsEqualTo(plane.Flip(), tolerance))
                    {
                        known = true;
                        break;
                    }
                }

                if (!known)
                {
                    planes.Add(plane);
                }
            }

            return planes;
        }

        /// <summary>
        /// Gets the planes that cut a body into cells each wholly inside or wholly outside another, and each wholly
        /// material or wholly carved out: those of the other's faces and openings that come near the body, and
        /// those of the body's own openings that come near the other.
        /// </summary>
        /// <remarks>
        /// Near the body, the surface of the other lies on the planes of its faces that come near the body, so a
        /// cell crossing none of them does not cross that surface, and one point inside the cell says which side of
        /// it the whole cell is on. The body's own openings need cutting along only where the other is: a cell
        /// anywhere else is outside the other, and dropped whatever it holds.
        /// </remarks>
        private static List<GeoPlane3> KnivesFor(GeoSolid3 body, GeoSolid3 other, Tolerance tolerance)
        {
            var planes = new List<GeoPlane3>();

            AddPlanesNear(other, body.GetAabb(), planes, tolerance);

            foreach (GeoSolid3 opening in body.Openings)
            {
                AddPlanesNear(opening, other.GetAabb(), planes, tolerance);
            }

            return planes;
        }

        /// <summary>
        /// Gets the distinct planes of the faces of a body, and of its openings, that come near a box.
        /// </summary>
        private static List<GeoPlane3> PlanesNear(GeoSolid3 solid, GeoAabb3 box, Tolerance tolerance)
        {
            var planes = new List<GeoPlane3>();
            AddPlanesNear(solid, box, planes, tolerance);
            return planes;
        }

        /// <summary>
        /// Adds to a list the planes not already in it of the faces of a body, and of its openings, that come near
        /// a box.
        /// </summary>
        private static void AddPlanesNear(GeoSolid3 solid, GeoAabb3 box, List<GeoPlane3> planes, Tolerance tolerance)
        {
            foreach (GeoFace3 face in solid.Faces)
            {
                if (!face.GetAabb().CollidesWith(box, tolerance))
                {
                    continue;
                }

                GeoPlane3 plane = face.GetPlane();
                bool known = false;

                foreach (GeoPlane3 existing in planes)
                {
                    if (existing.IsEqualTo(plane, tolerance) || existing.IsEqualTo(plane.Flip(), tolerance))
                    {
                        known = true;
                        break;
                    }
                }

                if (!known)
                {
                    planes.Add(plane);
                }
            }

            foreach (GeoSolid3 opening in solid.Openings)
            {
                AddPlanesNear(opening, box, planes, tolerance);
            }
        }

        /// <summary>
        /// Divides a body by a set of planes, so that no piece straddles any of them.
        /// </summary>
        /// <remarks>
        /// A plane that does not actually cut a cell leaves it alone, so the number of pieces grows only
        /// with the number of planes that really pass through the body rather than with how many were
        /// offered.
        /// </remarks>
        private static List<GeoSolid3> SplitIntoCells(GeoSolid3 subject, List<GeoPlane3> planes, Tolerance tolerance)
            => SplitIntoCells(subject, planes, tolerance, out _);

        /// <summary>
        /// Divides a body by a set of planes, saying whether every plane that crossed a piece divided it.
        /// </summary>
        /// <param name="subject">The body to divide.</param>
        /// <param name="planes">The planes to divide it by.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="clean">false when a plane had corners of a piece beyond the tolerance on both sides and still left it whole.</param>
        private static List<GeoSolid3> SplitIntoCells(GeoSolid3 subject, List<GeoPlane3> planes, Tolerance tolerance, out bool clean)
        {
            clean = true;

            // The gross boundary, with whatever the body has carved out of it left behind. An opening is
            // a region to be classified, not a property the pieces should inherit; the planes bounding it
            // are among the knives, so the cells it covers come out separately and are dropped later.
            List<GeoSolid3> cells = new List<GeoSolid3> { new GeoSolid3(subject.Faces) };

            foreach (GeoPlane3 plane in planes)
            {
                List<GeoSolid3> divided = new List<GeoSolid3>();

                foreach (GeoSolid3 cell in cells)
                {
                    if (Splition3.TrySplitBy(cell, plane, out GeoSolid3 above, out GeoSolid3 below, tolerance))
                    {
                        divided.Add(above);
                        divided.Add(below);
                    }
                    else if (!Crosses(cell, plane, tolerance))
                    {
                        divided.Add(cell);
                    }
                    else
                    {
                        // A cell can be in pieces: a plane before took the middle out of a bent bar and left its two
                        // ends as one cell. A plane passing between the pieces crosses none of them, so there is no rim
                        // to cap and nothing to cut, though there are corners on both sides of it. Each piece goes to
                        // its own side, or is cut; only a piece the plane crosses and cannot cut is left unclean.
                        foreach (GeoSolid3 piece in Shells3.Split(cell, tolerance))
                        {
                            if (Splition3.TrySplitBy(piece, plane, out GeoSolid3 pieceAbove, out GeoSolid3 pieceBelow, tolerance))
                            {
                                divided.Add(pieceAbove);
                                divided.Add(pieceBelow);
                            }
                            else
                            {
                                divided.Add(piece);
                                clean = clean && (!Crosses(piece, plane, tolerance) || IsSliver(piece, tolerance));
                            }
                        }
                    }
                }

                cells = divided;
            }

            return OnePieceEach(cells, tolerance);
        }

        /// <summary>
        /// Checks whether a piece is thinner than twice the point tolerance, so that no point of it is further than that
        /// from its skin.
        /// </summary>
        /// <remarks>
        /// A plane crossing such a piece may leave nothing on one side wide enough to be a polygon, and fail to cut it.
        /// That leaves it no worse judged than it is anyway, by the middle of its thickness; a cut counted unclean for it
        /// was thrown away whole. A slab cut by the planes of its neighbour kept a wedge a hundredth of a millimetre at
        /// its widest, three metres long, which two planes crossed and could not cut, and the difference went on to cut
        /// both slabs by every plane of both: sixteen seconds, and sixty-nine thousand cubic millimetres more than the
        /// two share; the other way round, it came out open.
        /// </remarks>
        private static bool IsSliver(GeoSolid3 piece, Tolerance tolerance) => !TryGetInteriorPoint(piece, tolerance, out _);

        /// <summary>
        /// Checks whether a plane has corners of a body beyond the tolerance on both sides of it.
        /// </summary>
        private static bool Crosses(GeoSolid3 solid, GeoPlane3 plane, Tolerance tolerance)
        {
            bool above = false, below = false;

            foreach (GeoFace3 face in solid.Faces)
            {
                foreach (GeoPoint3 corner in face.Boundary.Vertices)
                {
                    double distance = plane.SignedDistanceTo(corner);

                    above = above || distance > tolerance.EqualPlanar;
                    below = below || distance < -tolerance.EqualPlanar;

                    if (above && below)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Separates every cell into the pieces of material that do not touch.
        /// </summary>
        /// <remarks>
        /// A cell is judged by one point of it, which is only safe when the cell is one piece. A plane lying
        /// exactly along the wall of a hole does not cut the strip either side of the hole — there is no
        /// material on the far side of it to cut — so that strip comes out as one cell in two pieces, and a
        /// single point decides for both. Subtracting a box that overlapped an existing hole once threw away a
        /// whole block of material nowhere near the box that way.
        /// </remarks>
        private static List<GeoSolid3> OnePieceEach(List<GeoSolid3> cells, Tolerance tolerance)
        {
            var pieces = new List<GeoSolid3>(cells.Count);

            foreach (GeoSolid3 cell in cells)
            {
                pieces.AddRange(Shells3.Split(cell, tolerance));
            }

            return pieces;
        }

        /// <summary>
        /// Collects the faces of every cell that is material of the body it came from.
        /// </summary>
        /// <param name="cells">The cells to sort.</param>
        /// <param name="owner">The body the cells were cut from.</param>
        /// <param name="tolerance">The tolerance.</param>
        private static List<GeoFace3> FacesOfCells(List<GeoSolid3> cells, GeoSolid3 owner, Tolerance tolerance)
        {
            List<GeoFace3> faces = new List<GeoFace3>();

            foreach (GeoSolid3 cell in cells)
            {
                if (IsMaterial(cell, owner, tolerance, out _))
                {
                    faces.AddRange(cell.Faces);
                }
            }

            return faces;
        }

        /// <summary>
        /// Collects the faces of the cells that are material of their own body and lie on the wanted side
        /// of another one.
        /// </summary>
        /// <param name="cells">The cells to sort.</param>
        /// <param name="owner">The body the cells were cut from.</param>
        /// <param name="against">The body deciding inside from outside.</param>
        /// <param name="wantInside">true to keep the cells within that body, false to keep those beyond it.</param>
        /// <param name="tolerance">The tolerance.</param>
        private static List<GeoFace3> FacesOfCells(List<GeoSolid3> cells, GeoSolid3 owner, GeoSolid3 against, bool wantInside, Tolerance tolerance)
            => FacesOfCells(cells, owner, against, wantInside, tolerance, out _);

        /// <summary>
        /// Collects the faces of the cells that are material of their own body and lie on the wanted side of another
        /// one, saying whether any of that material lies within the other.
        /// </summary>
        /// <param name="cells">The cells to sort.</param>
        /// <param name="owner">The body the cells were cut from.</param>
        /// <param name="against">The body deciding inside from outside.</param>
        /// <param name="wantInside">true to keep the cells within that body, false to keep those beyond it.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="anyInside">Whether a cell of material lies within <paramref name="against"/>.</param>
        private static List<GeoFace3> FacesOfCells(List<GeoSolid3> cells, GeoSolid3 owner, GeoSolid3 against, bool wantInside, Tolerance tolerance, out bool anyInside)
        {
            List<GeoFace3> faces = new List<GeoFace3>();
            anyInside = false;

            foreach (GeoSolid3 cell in cells)
            {
                if (!IsMaterial(cell, owner, tolerance, out GeoPoint3 sample))
                {
                    continue;
                }

                bool within = Containment3.Locate(against, sample, tolerance) == PointLocation.Inside;
                anyInside |= within;

                if (within == wantInside)
                {
                    faces.AddRange(cell.Faces);
                }
            }

            return faces;
        }

        /// <summary>
        /// Checks whether a cell holds material of the body it was cut from, and finds a point inside it.
        /// </summary>
        /// <remarks>
        /// The cells come from the gross boundary, so one of them can be filling an opening. Every plane
        /// bounding an opening is among the knives, so no cell straddles the wall of one and a single
        /// sample settles the whole cell.
        /// <para>
        /// Dropping such a cell is what carves the opening into the result. The face between a cell that
        /// is kept and one that is dropped is traversed once rather than twice, so it survives the gluing
        /// and becomes the wall of the cavity — which is why the result needs no openings of its own.
        /// </para>
        /// </remarks>
        private static bool IsMaterial(GeoSolid3 cell, GeoSolid3 owner, Tolerance tolerance, out GeoPoint3 sample)
        {
            // A cell thinner than twice the point tolerance has no point further than that from its own skin, so the
            // search finds none in it; it is material all the same, and the middle of its thickness places it. Taken
            // for empty, a sliver a plane left along a face took its share of the body away with it: of two slabs
            // side by side, a difference took thirty-eight thousand cubic millimetres more than the two share.
            if (!TryGetInteriorPoint(cell, tolerance, out sample) && !TryGetMidpointOfThinBody(cell, tolerance, out sample))
            {
                return false;
            }

            // Every cell was cut from the body, so with nothing carved out of it there is no way for one
            // to be anything but material, and the test is skipped rather than paid for.
            if (owner.Openings.Count == 0)
            {
                return true;
            }

            // Only an opening takes material away. Asked of the whole body, the middle of a sliver along its skin lies
            // within the tolerance of a face and reads as on it rather than in it.
            foreach (GeoSolid3 opening in owner.Openings)
            {
                if (Containment3.Locate(opening, sample, tolerance) == PointLocation.Inside)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Finds a point strictly inside a body.
        /// </summary>
        /// <remarks>
        /// The centroid of a concave body can fall outside it, so it is checked rather than trusted. When
        /// it fails, the search steps a little way inwards from the middle of each surface triangle, along
        /// the inward normal. The step shrinks on each round because a body can be thinner in one place
        /// than the first step assumes, and a step that overshoots comes out the far side.
        /// </remarks>
        internal static bool TryGetInteriorPoint(GeoSolid3 solid, Tolerance tolerance, out GeoPoint3 point)
        {
            point = GeoPoint3.Origin;

            GeoPoint3 centroid = solid.Centroid;

            if (Containment3.Locate(solid, centroid, tolerance) == PointLocation.Inside)
            {
                point = centroid;
                return true;
            }

            double reach = solid.GetAabb().Diagonal.Length;

            if (reach <= 0.0)
            {
                return false;
            }

            GeoTriangle3[] mesh = solid.Triangulate(tolerance);

            for (double fraction = 1E-2; fraction >= 1E-5; fraction *= 0.1)
            {
                foreach (GeoTriangle3 triangle in mesh)
                {
                    if (triangle.IsDegenerate(tolerance))
                    {
                        continue;
                    }

                    // The direction from the area the tolerance just let through: the triangle's own Normal is found at
                    // the default tolerance, and threw for a triangle finer than that but not than this one.
                    GeoVector3 area = triangle.GetAreaVector();
                    GeoPoint3 candidate = triangle.Centroid.Subtract(area.Multiply(reach * fraction / area.Length));

                    if (Containment3.Locate(solid, candidate, tolerance) == PointLocation.Inside)
                    {
                        point = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Finds a point inside a body too thin for <see cref="TryGetInteriorPoint"/> to find one: the middle of its
        /// thickness under the middle of one of its largest surface triangles.
        /// </summary>
        /// <remarks>
        /// A ray from inside a triangle of a closed skin, run along the inward normal, is in the body from the triangle to
        /// where it next meets the skin, so halfway there it is as deep in the body as that ray goes. The point is checked
        /// all the same, within a tolerance a quarter of that depth: a skin wound the wrong way somewhere sends the ray out
        /// of the body instead.
        /// </remarks>
        internal static bool TryGetMidpointOfThinBody(GeoSolid3 solid, Tolerance tolerance, out GeoPoint3 point)
        {
            point = GeoPoint3.Origin;

            GeoTriangle3[] mesh = solid.Triangulate(tolerance);
            var areas = new double[mesh.Length];
            var order = new int[mesh.Length];

            for (int i = 0; i < mesh.Length; i++)
            {
                areas[i] = -mesh[i].GetAreaVector().Length;
                order[i] = i;
            }

            Array.Sort(areas, order);

            // The skin itself is where the ray starts; a crossing that near it is the ray leaving its own triangle.
            double start = 1E-9 * (1.0 + solid.GetAabb().Diagonal.Length);

            for (int k = 0; k < Math.Min(order.Length, 8); k++)
            {
                GeoTriangle3 triangle = mesh[order[k]];
                GeoVector3 area = triangle.GetAreaVector();

                if (area.Length <= 0.0)
                {
                    continue;
                }

                GeoVector3 inward = area.Multiply(-1.0 / area.Length);
                var ray = new GeoRay3(triangle.Centroid, inward);
                double depth = double.MaxValue;

                foreach (GeoFace3 face in solid.Faces)
                {
                    if (face.TryIntersectWith(ray, out GeoPoint3 hit, tolerance))
                    {
                        double along = triangle.Centroid.GetVectorTo(hit).DotProduct(inward);

                        if (along > start && along < depth)
                        {
                            depth = along;
                        }
                    }
                }

                if (depth == double.MaxValue)
                {
                    continue;
                }

                GeoPoint3 candidate = triangle.Centroid.Add(inward.Multiply(depth / 2.0));
                double fine = depth / 4.0;
                var within = new Tolerance(fine, Math.Min(tolerance.EqualVector, fine), tolerance.EqualAngleRad, fine);

                if (Containment3.Locate(solid, candidate, within) == PointLocation.Inside)
                {
                    point = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Glues a collection of cell faces into one closed body.
        /// </summary>
        /// <remarks>
        /// A face between two cells that were both kept is interior to the result, and it appears twice
        /// among the collected faces, once each way round. Dropping both leaves exactly the outer skin —
        /// when the two copies match vertex for vertex, which they need not; see
        /// <see cref="CancelBackToBack"/> for the pairs that do not. The survivors are then merged where they
        /// are coplanar and touching, which undoes the subdivision the cutting introduced.
        /// </remarks>
        private static bool TryGlue(List<GeoFace3> faces, Tolerance tolerance, out GeoSolid3 result)
        {
            result = null;

            List<GeoFace3> skin = CancelBackToBack(DropMatchedPairs(faces, tolerance), tolerance);

            if (skin.Count < 4)
            {
                return false;
            }

            result = Merge3.CoplanarFaces(new GeoSolid3(skin), tolerance);
            return true;
        }

        /// <summary>
        /// Drops each face together with the first later face that is it turned over, vertex for vertex: the two
        /// sides of a wall between two cells that were both kept.
        /// </summary>
        /// <remarks>
        /// Two such faces have the same corners, so the corners of their boxes lie within the tolerance of each
        /// other. The faces are filed by the low corner of their box, and each is compared only with the later
        /// faces filed near its own — in the order, and by the test, that comparing it with every later face would
        /// use, so the same pairs are dropped without comparing every face with every other.
        /// </remarks>
        internal static List<GeoFace3> DropMatchedPairs(List<GeoFace3> faces, Tolerance tolerance)
        {
            int count = faces.Count;
            bool[] dropped = new bool[count];
            var boxes = new GeoAabb3[count];
            var corners = new PointGrid(tolerance);

            for (int j = 0; j < count; j++)
            {
                boxes[j] = faces[j].GetAabb();
                corners.Add(boxes[j].Min, j);
            }

            var near = new List<int>();

            for (int i = 0; i < count; i++)
            {
                if (dropped[i])
                {
                    continue;
                }

                corners.Near(boxes[i].Min, near);

                foreach (int j in near)
                {
                    if (j <= i || dropped[j] || faces[j].Boundary.VertexCount != faces[i].Boundary.VertexCount)
                    {
                        continue;
                    }

                    if (faces[i].Boundary.IsEqualTo(faces[j].Boundary.Flip(), tolerance))
                    {
                        dropped[i] = true;
                        dropped[j] = true;
                        break;
                    }
                }
            }

            var kept = new List<GeoFace3>(count);

            for (int i = 0; i < count; i++)
            {
                if (!dropped[i])
                {
                    kept.Add(faces[i]);
                }
            }

            return kept;
        }

        /// <summary>
        /// Takes from the faces still lying back to back in one plane the area they share.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The two copies of a face between kept cells need not match vertex for vertex. A cut that reaches the
        /// cell on one side and not the one on the other leaves one copy in two pieces, or with a point along its
        /// edge that the other copy lacks, and the exact match lets both through. They would stand inside the
        /// body as a sheet of no thickness: the volume never notices, since the two cancel, but everything that
        /// reads the boundary does — a point a millimetre from the sheet measured a millimetre to the boundary,
        /// the surface mesh carried the sheet, and splitting the body into pieces failed on it.
        /// </para>
        /// <para>
        /// Whatever two faces lying back to back share is inside the body however it was cut, so it goes from
        /// both, and what is left of either is boundary.
        /// </para>
        /// <para>
        /// Two faces lie back to back when either lies in the plane of the other, and each is cut in its own plane,
        /// the other laid out in it (<see cref="SubtractLaidOut"/>): a small face a hair out of a long one's plane
        /// lies in it, but the long one's far end stands off the small one's by more than the tolerance.
        /// </para>
        /// </remarks>
        internal static List<GeoFace3> CancelBackToBack(List<GeoFace3> faces, Tolerance tolerance)
        {
            int count = faces.Count;
            var normals = new GeoVector3[count];
            var boxes = new GeoAabb3[count];
            var planes = new GeoPlane3[count];
            var order = new int[count];
            var lows = new double[count];

            for (int i = 0; i < count; i++)
            {
                normals[i] = faces[i].Boundary.Normal;
                boxes[i] = faces[i].GetAabb();
                planes[i] = faces[i].GetPlane();
                order[i] = i;
                lows[i] = boxes[i].Min.X;
            }

            double speck = tolerance.EqualPoint * tolerance.EqualPoint;

            // Two faces lying against each other have boxes that meet, so they overlap along X: sorted by where
            // their boxes start along X, each face need only be tried against those starting before its box ends.
            // The pairs found are then taken in the order trying every pair would have met them, since the order a
            // face's partners are listed in is the order they are taken away from it.
            Array.Sort(lows, order);
            double reach = 2.0 * tolerance.EqualPoint;
            List<(int, int)> pairs = null;

            for (int a = 0; a < count; a++)
            {
                int first = order[a];

                for (int b = a + 1; b < count && lows[b] <= boxes[first].Max.X + reach; b++)
                {
                    int i = Math.Min(first, order[b]), j = Math.Max(first, order[b]);

                    if (normals[i].DotProduct(normals[j]) >= 0.0 || !boxes[i].CollidesWith(boxes[j], tolerance))
                    {
                        continue;
                    }

                    // Asked both ways round: a small face a hair out of a long one's plane lies in it, while the long
                    // one's far end stands off the small one's, and asked one way the pair was found or missed by which
                    // of the two came first. What they share is measured in the plane of the one the other lies in.
                    bool jInI = LiesIn(planes[i], faces[j], tolerance);

                    if (!jInI && !LiesIn(planes[j], faces[i], tolerance)
                        || AreaOf(jInI ? Intersect(faces[i], faces[j], tolerance) : Intersect(faces[j], faces[i], tolerance)) <= speck)
                    {
                        continue;
                    }

                    (pairs = pairs ?? new List<(int, int)>()).Add((i, j));
                }
            }

            if (pairs == null)
            {
                return faces;
            }

            pairs.Sort();
            var against = new List<int>[count];

            foreach ((int i, int j) in pairs)
            {
                (against[i] = against[i] ?? new List<int>()).Add(j);
                (against[j] = against[j] ?? new List<int>()).Add(i);
            }

            var kept = new List<GeoFace3>(count);

            for (int i = 0; i < count; i++)
            {
                if (against[i] == null)
                {
                    kept.Add(faces[i]);
                    continue;
                }

                var left = new List<GeoFace3> { faces[i] };

                foreach (int j in against[i])
                {
                    var rest = new List<GeoFace3>();

                    foreach (GeoFace3 piece in left)
                    {
                        rest.AddRange(SubtractLaidOut(piece, faces[j], tolerance));
                    }

                    left = rest;
                }

                foreach (GeoFace3 piece in left)
                {
                    if (piece.Area > speck)
                    {
                        kept.Add(piece);
                    }
                }
            }

            return kept;
        }

        private static double AreaOf(GeoFace3[] faces)
        {
            double area = 0.0;

            foreach (GeoFace3 face in faces)
            {
                area += face.Area;
            }

            return area;
        }

        #endregion
    }
}
