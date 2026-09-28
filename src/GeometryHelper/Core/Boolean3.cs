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
        /// <returns>false when the two could not be combined into a closed body.</returns>
        /// <remarks>
        /// Two bodies that do not touch still combine: the result is one solid carrying both shells, which
        /// measures and answers containment correctly because each shell is closed and wound outwards.
        /// </remarks>
        public static bool TryUnion(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, Tolerance tolerance)
        {
            Guard(first, second);

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

            // One body cut, beyond the other, and the other whole.
            if (TryCutOpenings(first, out GeoSolid3 a, tolerance) && TryCutOpenings(second, out GeoSolid3 b, tolerance))
            {
                bool? found = CombineCuttingOne(a, b, true, tolerance, out result);

                if (found.HasValue)
                {
                    return found.Value;
                }
            }

            List<GeoPlane3> planes = SharedPlanes(first, second, tolerance);

            List<GeoFace3> kept = new List<GeoFace3>();

            // All of the first body, and only the part of the second that reaches beyond it: the shared
            // region belongs to the union once, and it is already carried by the first.
            kept.AddRange(FacesOfCells(SplitIntoCells(first, planes, tolerance), first, tolerance));

            kept.AddRange(FacesOfCells(SplitIntoCells(second, planes, tolerance), second, first, false, tolerance));

            return TryGlue(kept, tolerance, out result);
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
        /// <returns>false when the two bodies share no volume.</returns>
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

            result = null;

            if (!first.GetAabb().CollidesWith(second.GetAabb(), tolerance))
            {
                return false;
            }

            List<GeoPlane3> cuttingFirst = KnivesFor(first, second, tolerance);
            List<GeoPlane3> cuttingSecond = KnivesFor(second, first, tolerance);
            bool firstIsCut = cuttingFirst.Count <= cuttingSecond.Count;

            GeoSolid3 cut = firstIsCut ? first : second;
            GeoSolid3 other = firstIsCut ? second : first;
            List<GeoPlane3> knives = firstIsCut ? cuttingFirst : cuttingSecond;
            List<GeoPlane3> otherKnives = firstIsCut ? cuttingSecond : cuttingFirst;

            List<GeoFace3> kept = CellsInside(cut, other, knives, tolerance, out bool clean);

            // A plane can still cross a cell and leave it whole where the cut does not close — a body running
            // through itself, say — and the cell is then judged by one point for both sides of the plane. Cut the
            // other way, the trouble falls elsewhere, so the other body is cut before that is settled for, when that
            // costs about the same: a bar's hundreds of planes would cut a beam into thousands of cells again.
            // Slivers were the common cause, and the cut keeps those now; see LoopAssembly.ForPieces.
            if (!clean && otherKnives.Count <= 2 * knives.Count + 16)
            {
                List<GeoFace3> otherWay = CellsInside(other, cut, otherKnives, tolerance, out bool otherClean);

                if (otherClean)
                {
                    kept = otherWay;
                }
            }

            return TryGlue(kept, tolerance, out result);
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
        /// <returns>false when nothing is left, because the tool swallowed the subject whole.</returns>
        /// <remarks>
        /// The walls of the cavity are not taken from the tool; they are already there. Dividing the
        /// subject by the planes of the tool lays a face along every part of the surface of the tool that
        /// passes through it, so keeping the cells that fall outside the tool keeps those faces with them.
        /// A tool that misses the subject changes nothing, and the subject comes back unaltered.
        /// </remarks>
        public static bool TrySubtract(GeoSolid3 subject, GeoSolid3 tool, out GeoSolid3 result, Tolerance tolerance)
        {
            Guard(subject, tool);

            result = null;

            if (!subject.GetAabb().CollidesWith(tool.GetAabb(), tolerance))
            {
                result = subject;
                return true;
            }

            // One body cut: the subject beyond the tool, or the subject whole less the tool within it.
            if (TryCutOpenings(subject, out GeoSolid3 a, tolerance) && TryCutOpenings(tool, out GeoSolid3 b, tolerance))
            {
                bool? found = CombineCuttingOne(a, b, false, tolerance, out result);

                if (found.HasValue)
                {
                    return found.Value;
                }
            }

            List<GeoPlane3> planes = SharedPlanes(subject, tool, tolerance);

            List<GeoFace3> kept = FacesOfCells(SplitIntoCells(subject, planes, tolerance), subject, tool, false, tolerance);

            return TryGlue(kept, tolerance, out result);
        }

        /// <summary>
        /// Joins two bodies without openings, or takes the second out of the first, cutting one of them only.
        /// </summary>
        /// <param name="a">The first body; for a difference, the one material is taken from.</param>
        /// <param name="b">The second body; for a difference, the one taken away.</param>
        /// <param name="union">true for the union, false for <paramref name="a"/> less <paramref name="b"/>.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="result">The result, when there is one.</param>
        /// <returns>
        /// Whether anything is left, as the public methods report it; null when neither body could be cut cleanly,
        /// for the caller to cut both by every plane of both instead.
        /// </returns>
        /// <remarks>
        /// The body cut is the one fewer planes of the other come near, as for an intersection. A union keeps the
        /// cells of it beyond the other, and the other whole. For a difference, cutting the body material is taken
        /// from keeps its cells beyond the other; cutting the body taken away keeps the other whole and the cells
        /// within it turned inside out, which are the walls of the cavity and take away the part of the whole
        /// body's faces it covers. Where the whole body meets the cells, the two lie back to back in one plane,
        /// each cut its own way, and <see cref="CancelBackToBack"/> takes from both the area they share.
        /// </remarks>
        private static bool? CombineCuttingOne(GeoSolid3 a, GeoSolid3 b, bool union, Tolerance tolerance, out GeoSolid3 result)
        {
            result = null;

            List<GeoPlane3> cuttingA = PlanesNear(b, a.GetAabb(), tolerance);
            List<GeoPlane3> cuttingB = PlanesNear(a, b.GetAabb(), tolerance);
            bool aFirst = cuttingA.Count <= cuttingB.Count;

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

                List<GeoFace3> kept = FacesOfCells(SplitIntoCells(cut, knives, tolerance, out bool clean), cut, whole, within, tolerance);

                if (!clean)
                {
                    continue;
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

                return TryGlue(kept, tolerance, out result);
            }

            return null;
        }

        #region Machinery

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
                                clean = clean && !Crosses(piece, plane, tolerance);
                            }
                        }
                    }
                }

                cells = divided;
            }

            return OnePieceEach(cells, tolerance);
        }

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
        {
            List<GeoFace3> faces = new List<GeoFace3>();

            foreach (GeoSolid3 cell in cells)
            {
                if (!IsMaterial(cell, owner, tolerance, out GeoPoint3 sample))
                {
                    continue;
                }

                bool within = Containment3.Locate(against, sample, tolerance) == PointLocation.Inside;

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
            if (!TryGetInteriorPoint(cell, tolerance, out sample))
            {
                return false;
            }

            // Every cell was cut from the body, so with nothing carved out of it there is no way for one
            // to be anything but material, and the test is skipped rather than paid for.
            if (owner.Openings.Count == 0)
            {
                return true;
            }

            return Containment3.Locate(owner, sample, tolerance) == PointLocation.Inside;
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
        private static bool TryGetInteriorPoint(GeoSolid3 solid, Tolerance tolerance, out GeoPoint3 point)
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

                    GeoPoint3 candidate = triangle.Centroid.Subtract(triangle.Normal.Multiply(reach * fraction));

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

                    if (normals[i].DotProduct(normals[j]) >= 0.0
                        || !boxes[i].CollidesWith(boxes[j], tolerance)
                        || !LiesIn(planes[i], faces[j], tolerance)
                        || AreaOf(Intersect(faces[i], faces[j], tolerance)) <= speck)
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
                        rest.AddRange(Subtract(piece, faces[j], tolerance));
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
