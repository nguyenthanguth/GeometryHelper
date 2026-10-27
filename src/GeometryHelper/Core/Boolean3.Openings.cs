using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Cutting a body's openings into it, so that its faces are the surface of its material.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="GeoSolid3"/> keeps its openings as whole bodies subtracted from it, not as holes cut in
    /// its faces: a plate with a bolt hole has a top face that is a whole square, and a separate box where the
    /// hole is. That is the right way to hold a model read from Tekla or IFC, and the wrong thing to hand to
    /// anything that reads the faces as the boundary of the material — a pin through the hole crosses that
    /// whole square and looks like it touches the plate.
    /// </para>
    /// <para>
    /// This turns such a body into one with no openings, whose faces are exactly where the material ends: the
    /// outer faces with the parts over each opening taken away, and the walls of each opening where they run
    /// through the body. It is the boolean machinery with one operand — split the body into cells, keep the
    /// cells that are material, glue them — and <see cref="Containment3"/> already knows which cells are
    /// material, nested openings included.
    /// </para>
    /// <para>
    /// <b>Each opening cuts only the cells near it.</b> The knives are the face planes of an opening, and a
    /// plane is infinite: slicing the whole body by every one of them turns a plate with twenty bolt holes into
    /// a grid of a thousand cells. A cell whose box does not meet an opening's box is wholly outside that
    /// opening already, so it is left alone, and the work grows with the number of openings rather than with
    /// its square.
    /// </para>
    /// </remarks>
    public static partial class Boolean3
    {
        #region Cutting the openings in

        /// <summary>
        /// Turns a body with openings into one without, whose faces are the surface of its material.
        /// </summary>
        public static bool TryCutOpenings(GeoSolid3 solid, out GeoSolid3 material) => TryCutOpenings(solid, out material, Tolerance.Global);

        /// <summary>
        /// Turns a body with openings into one without, whose faces are the surface of its material, within a
        /// tolerance.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="material">
        /// The body with every opening cut into it, or the body itself when it has none; null when the method
        /// returns false.
        /// </param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true when some material is left; false when the openings take all of it, which is an outcome and not
        /// an error, as it is for the booleans, or when the cut cannot be worked out, which is logged.
        /// </returns>
        /// <remarks>
        /// The material of a closed body with closed openings is closed as <see cref="GeoSolid3.Validate(Tolerance)"/> reads
        /// it, as a boolean's is, or not given. Where cutting the openings in at once leaves it open, it is welded within the
        /// tolerance, and where that does not close it the openings are taken out one at a time by a difference, which tries
        /// more ways of cutting and keeps only what closes; where one will not come out so, the method returns false. They are
        /// taken out so too where a plane of an opening runs through the body and will not cut it, leaving a piece part in the
        /// opening and part out of it, which cut in at once would be kept or dropped whole. A plate 300 by 200 by 12 with two
        /// blind holes ending 0.015 under its top, turned and far from the origin, came out of the cut within a hundredth open
        /// by four edges 0.015 long; the holes are taken out one at a time, and it closes, holding 711 120.61 as
        /// <see cref="GeoSolid3.GetVolume(Tolerance)"/> measures it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the body is null.</exception>
        public static bool TryCutOpenings(GeoSolid3 solid, out GeoSolid3 material, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            return TryCutOpenings(solid, solid.Openings, out material, tolerance);
        }

        /// <summary>
        /// Cuts some of a body's openings into it and leaves the rest out of the answer.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="openings">The openings to cut; the others are not carried over.</param>
        /// <param name="material">The body with those openings cut into it.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when some material is left; otherwise, or when the cut cannot be worked out, which is logged, false.</returns>
        /// <remarks>
        /// For a question asked near a probe, only the openings whose box meets the probe's box can change the
        /// answer, so there is no reason to pay for the rest. The caller is the one who knows which those are.
        /// </remarks>
        internal static bool TryCutOpenings(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, out GeoSolid3 material, Tolerance tolerance)
        {
            if (TryCutOpenings(solid, openings, out material, tolerance, out Exception failure))
            {
                return true;
            }

            return failure != null && Unworkable("cut of openings", failure, out material);
        }

        /// <summary>
        /// Cuts some of a body's openings into it, handing back a cut that cannot be worked out rather than warning of it.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="openings">The openings to cut; the others are not carried over.</param>
        /// <param name="material">The body with those openings cut into it; null when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="failure">What was thrown where the cut could not be worked out, or why it would not close; null otherwise.</param>
        /// <returns>
        /// true when some material is left, closed as <see cref="GeoSolid3.Validate(Tolerance)"/> reads it where the body and
        /// the openings are; false when the openings take all of it, or the cut cannot be worked out so.
        /// </returns>
        internal static bool TryCutOpenings(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, out GeoSolid3 material, Tolerance tolerance, out Exception failure)
            => TryCutOpenings(solid, openings, out material, tolerance, out failure, out _);

        /// <summary>
        /// Cuts some of a body's openings into it as <see cref="TryCutOpenings(GeoSolid3, IReadOnlyList{GeoSolid3}, out GeoSolid3, Tolerance, out Exception)"/>
        /// does, handing back too what cutting them in at once gave where no closed material could be had.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="openings">The openings to cut; the others are not carried over.</param>
        /// <param name="material">The body with those openings cut into it; null when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="failure">What was thrown where the cut could not be worked out, or why it would not close; null otherwise.</param>
        /// <param name="open">
        /// What cutting the openings in at once gave, open or with a piece judged whole, where it was left for no closed
        /// material; null otherwise.
        /// </param>
        private static bool TryCutOpenings(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, out GeoSolid3 material, Tolerance tolerance, out Exception failure, out GeoSolid3 open)
        {
            open = null;

            if (!TryCutOpeningsAtOnce(solid, openings, out material, tolerance, out failure, out bool judgedWhole) && !judgedWhole)
            {
                return false;
            }

            GeoSolid3 atOnce = material;

            try
            {
                // A cut at once that left a piece of the body part in an opening and part out of it is taken out one at a
                // time, as GetVolume takes it out; see JudgedWhole.
                if (judgedWhole ? TakesOutJudgedWhole(solid, openings, ref material, tolerance, out failure) : ClosesOrTakesOut(solid, openings, ref material, tolerance, out failure))
                {
                    return true;
                }
            }
            catch (Exception exception) when (IsUnworkable(exception))
            {
                failure = exception;
                material = null;
            }

            if (failure != null)
            {
                open = atOnce;
            }

            return false;
        }

        /// <summary>
        /// Gets the material a query or a writer reads of a body for some of its openings: the material
        /// <see cref="TryCutOpenings(GeoSolid3, IReadOnlyList{GeoSolid3}, out GeoSolid3, Tolerance, out Exception)"/> gives, and
        /// where no closed material can be had, what cutting the openings in at once gave, open or with a piece of the body part
        /// in an opening and part out, as it was read before, with a warning.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="openings">The openings to cut.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The material; null where the openings take all of it, or the cut cannot be worked out at all, which is logged.</returns>
        internal static GeoSolid3 MaterialToRead(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, Tolerance tolerance)
        {
            if (TryCutOpenings(solid, openings, out GeoSolid3 material, tolerance, out Exception failure, out GeoSolid3 open))
            {
                return material;
            }

            if (open != null)
            {
                GeometryHelperLog.Warn("Boolean3: the openings could not be cut in closed, nor taken out one at a time; the material cut in at once is read instead.", failure);
                return open;
            }

            if (failure != null)
            {
                Unworkable("cut of openings", failure, out _);
            }

            return null;
        }

        /// <summary>
        /// Cuts every opening of a body into it at once, as a boolean reads its operands, whether what comes of it closes or
        /// not; a cut that cannot be worked out is logged.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="material">The body with its openings cut in, or the body itself when it has none; null when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when some material is left; otherwise, or when the cut cannot be worked out, false.</returns>
        /// <remarks>
        /// A boolean closes what it gives itself, and cuts both bodies by every plane of both where cutting one does not, so
        /// it takes the cut as it comes: handed a plate whose twelve openings the tolerance inside its faces were taken out
        /// one at a time, the union of the plate and a box at its corner went that way and came out wound two ways,
        /// 718 137.69 where it holds 722 622.74. A cut that left a piece of the body part in an opening and part out of it
        /// (see <see cref="JudgedWhole"/>) is taken as it comes too, as it was before that was looked for, and noted in the
        /// debug log: the boolean of such an operand can be off by the piece.
        /// </remarks>
        internal static bool TryCutOpeningsAtOnce(GeoSolid3 solid, out GeoSolid3 material, Tolerance tolerance)
        {
            if (TryCutOpeningsAtOnce(solid, solid.Openings, out material, tolerance, out Exception failure, out bool judgedWhole))
            {
                if (judgedWhole)
                {
                    GeometryHelperLog.Debug("Boolean3: cutting the openings of an operand in at once left a piece of it part in an opening and part out; the boolean reads the cut as it came.");
                }

                return true;
            }

            return failure != null && Unworkable("cut of openings", failure, out material);
        }

        /// <summary>
        /// Cuts some of a body's openings into it at once, whether what comes of it closes or not, handing back a cut that
        /// cannot be worked out rather than warning of it.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="openings">The openings to cut; the others are not carried over.</param>
        /// <param name="material">The body with those openings cut into it; null when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="failure">What was thrown where the cut could not be worked out; null otherwise.</param>
        /// <param name="judgedWhole">
        /// Whether a plane of an opening that would not cut a piece of the body left it part in the opening and part out, and
        /// the piece was judged whole by one point of it (see <see cref="JudgedWhole"/>): the material is given as it was cut,
        /// and is not to be trusted.
        /// </param>
        /// <returns>true when some material is left; false when the openings take all of it, or the cut cannot be worked out.</returns>
        internal static bool TryCutOpeningsAtOnce(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, out GeoSolid3 material, Tolerance tolerance, out Exception failure, out bool judgedWhole)
        {
            failure = null;
            judgedWhole = false;

            if (openings.Count == 0)
            {
                material = solid.Openings.Count == 0 ? solid : new GeoSolid3(solid.Faces);
                return true;
            }

            try
            {
                return CutOpenings(solid, openings, out material, tolerance, out judgedWhole);
            }
            catch (Exception exception) when (IsUnworkable(exception))
            {
                failure = exception;
                material = null;
                return false;
            }
        }

        /// <summary>
        /// Takes one opening out of a body by a difference, handing back a cut that cannot be worked out rather than
        /// warning of it.
        /// </summary>
        /// <param name="body">The body, without openings.</param>
        /// <param name="opening">The opening; the material inside it, its own openings, stays.</param>
        /// <param name="material">The body less the opening; null when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="failure">What was thrown where the cut could not be worked out; null otherwise.</param>
        /// <returns>true when some material is left; false when the opening takes all of it, or the cut cannot be worked out.</returns>
        /// <remarks>
        /// The way back where cutting every opening in at once cannot be worked out or leaves the material open: a
        /// difference tries more ways of cutting and keeps the one that closes, and an opening that will not come out
        /// keeps none of the others in.
        /// </remarks>
        internal static bool TryTakeOut(GeoSolid3 body, GeoSolid3 opening, out GeoSolid3 material, Tolerance tolerance, out Exception failure)
        {
            failure = null;

            try
            {
                if (TakeAway(body, opening, out material, tolerance, out string unworkable))
                {
                    return true;
                }

                if (unworkable != null)
                {
                    failure = new InvalidOperationException($"The opening could not be taken out: cutting it {unworkable}.");
                }

                return false;
            }
            catch (Exception exception) when (IsUnworkable(exception))
            {
                failure = exception;
                material = null;
                return false;
            }
        }

        /// <summary>
        /// Cuts some of a body's openings into it at once: parts the body by the planes of the openings, keeps the pieces
        /// that are material and glues them.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="openings">The openings to cut.</param>
        /// <param name="material">The body with the openings cut in; null when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="judgedWhole">
        /// Whether a piece was left part in an opening and part out of it by a plane that would not cut it, and judged whole;
        /// see <see cref="JudgedWhole"/>. The material is given all the same, as it was cut.
        /// </param>
        /// <returns>true when some material is left; false when the openings take all of it.</returns>
        private static bool CutOpenings(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, out GeoSolid3 material, Tolerance tolerance, out bool judgedWhole)
        {
            // The body carrying only the openings being cut, so that a cell is judged against exactly those, and
            // both as the cut works on them, wound outwards and flat; see ForWork and FlatForWork.
            tolerance = ForWork(tolerance);
            GeoSolid3 owner = FlatForWork(new GeoSolid3(solid.Faces, openings), tolerance);
            GeoSolid3 gross = new GeoSolid3(owner.Faces);
            openings = owner.Openings;

            List<GeoSolid3> cells = new List<GeoSolid3> { gross };
            List<(GeoPlane3 Plane, GeoSolid3 Opening)> uncut = null;
            bool? closed = null;

            foreach (GeoSolid3 opening in openings)
            {
                GeoAabb3 reach = opening.GetAabb();

                foreach (GeoPlane3 plane in Splition3.CollectFacePlanes(opening, tolerance))
                {
                    List<GeoSolid3> divided = new List<GeoSolid3>(cells.Count + 1);

                    foreach (GeoSolid3 cell in cells)
                    {
                        // A cell out of the opening's reach is wholly outside it, so none of its planes need
                        // cut it: that is what keeps a plate with many holes from becoming a grid. A corner is read
                        // as on a plane within what rounding adds to the tolerance; see Splition3.OnPlaneWithinRounding.
                        bool reached = cell.GetAabb().CollidesWith(reach, tolerance);

                        if (reached
                            && Splition3.TrySplitCell(cell, plane, out GeoSolid3 above, out GeoSolid3 below, Splition3.OnPlaneWithinRounding(cell, tolerance)))
                        {
                            divided.Add(above);
                            divided.Add(below);
                        }
                        else
                        {
                            // A plane running through a cell it would not cut is kept, to see whether a cell it leaves is
                            // part in the opening and part out; see JudgedWhole. The cells of a body that does not close
                            // hold no volume to weigh, and are judged as they were.
                            if (reached && Straddles(cell, plane, tolerance, out _, out _)
                                && (closed ?? (closed = Shells3.Closes(gross, tolerance)).Value))
                            {
                                (uncut = uncut ?? new List<(GeoPlane3, GeoSolid3)>()).Add((plane, opening));
                            }

                            divided.Add(cell);
                        }
                    }

                    cells = divided;
                }
            }

            List<GeoSolid3> pieces = OnePieceEach(cells, tolerance);
            judgedWhole = uncut != null && JudgedWhole(pieces, uncut, tolerance);

            var owners = new List<int>();
            List<GeoFace3> faces = FacesOfCells(pieces, owner, tolerance, owners);
            return TryGlue(faces, owners, tolerance, out material);
        }

        /// <summary>
        /// The most planes of one opening running through a piece uncut whose eighths are read, the first to come: the threes
        /// of them grow as the cube of their number, and an opening crossing a piece with many more planes than a box has is a
        /// round cut into facets, a cone, a countersink or a ball, not the corner of a box the eighths are read for: the 128
        /// facets of a cone make 341 376 threes, every one meeting at its tip.
        /// </summary>
        private const int MostPlanesForEighths = 16;

        /// <summary>
        /// Whether a piece of the body, a plane of an opening running through it uncut, lies part in that opening and part
        /// out of it, which judged whole by one point puts the one part on the side of the other.
        /// </summary>
        /// <param name="pieces">The pieces of the body, each in one piece.</param>
        /// <param name="uncut">The planes that ran through a cell and would not cut it, each with the opening it is of.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A plane can run through a cell and the cut still be refused, where the rim it leaves does not close: a half of a
        /// body, cut near a face, is often closed only within the tolerance, and one crossed by the floor of a box opening
        /// left a rim open by a gap 0.007 wide. Left whole, such a piece is judged by one point and kept or dropped with the
        /// opening: a hull cut within a hundredth by a plane 0.019 inside a face, a box 3.5 by 3.5 by 5 standing through the
        /// cut, left a half holding 1 465.33 cubic millimetres where it holds 1 434.77, the box left in, with every opening
        /// said to be cut out. A piece is read so where the middle of its part on one side of the plane is in the opening and
        /// of the other out of it, and, where two planes of one opening run through it uncut, the middle of one of the four
        /// parts they make is in it and of another out: a cube 10 across under the corner of a box 4 by 4 lying along an
        /// edge of it, both planes uncut, holds 40 of the box, and each plane alone leaves both its parts out of the box. Where
        /// three that meet at a point run through it uncut, the eight parts they make are read so too: a cube 10 across with
        /// the corner of a box 7 across standing in it, x, y and z = 5 uncut, holds 125 of the box, and each plane alone or
        /// any two leave the middles of all their parts out of it; the threes are read of the first planes of an opening only
        /// (see <see cref="MostPlanesForEighths"/>). A piece the plane runs through clear of the opening, or only grazes or
        /// takes a sliver off (see <see cref="Straddles"/>), is judged as before.
        /// </remarks>
        internal static bool JudgedWhole(List<GeoSolid3> pieces, List<(GeoPlane3 Plane, GeoSolid3 Opening)> uncut, Tolerance tolerance)
        {
            var crossing = new List<(GeoPlane3 Plane, GeoSolid3 Opening)>();
            var corners = new List<(GeoPlane3 Plane, GeoSolid3 Opening)>();

            foreach (GeoSolid3 piece in pieces)
            {
                crossing.Clear();
                corners.Clear();

                foreach ((GeoPlane3 plane, GeoSolid3 opening) in uncut)
                {
                    if (!piece.GetAabb().CollidesWith(opening.GetAabb(), tolerance)
                        || !Straddles(piece, plane, tolerance, out GeoPoint3 aboveMiddle, out GeoPoint3 belowMiddle))
                    {
                        continue;
                    }

                    if (IsIn(opening, aboveMiddle, tolerance) != IsIn(opening, belowMiddle, tolerance))
                    {
                        return true;
                    }

                    foreach ((GeoPlane3 other, GeoSolid3 of) in crossing)
                    {
                        if (ReferenceEquals(of, opening) && QuartersPart(piece, other, plane, opening, tolerance))
                        {
                            return true;
                        }
                    }

                    // The eighths of the first planes of an opening only, each once, though a plane comes once for every
                    // cell it would not cut; see MostPlanesForEighths.
                    int earlier = 0;
                    bool again = false;

                    foreach ((GeoPlane3 other, GeoSolid3 of) in corners)
                    {
                        if (ReferenceEquals(of, opening))
                        {
                            earlier++;
                            again |= other.Equals(plane);
                        }
                    }

                    if (!again && earlier < MostPlanesForEighths)
                    {
                        for (int i = 0; i < corners.Count; i++)
                        {
                            if (!ReferenceEquals(corners[i].Opening, opening))
                            {
                                continue;
                            }

                            for (int j = i + 1; j < corners.Count; j++)
                            {
                                if (ReferenceEquals(corners[j].Opening, opening) && EighthsPart(piece, corners[i].Plane, corners[j].Plane, plane, opening, tolerance))
                                {
                                    return true;
                                }
                            }
                        }

                        corners.Add((plane, opening));
                    }

                    crossing.Add((plane, opening));
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a point lies inside a body, off its skin.
        /// </summary>
        private static bool IsIn(GeoSolid3 body, GeoPoint3 point, Tolerance tolerance)
            => Containment3.Locate(body, point, tolerance) == PointLocation.Inside;

        /// <summary>
        /// Whether, of the four parts two planes make of a body, one holding more than the tolerance times the body's area has
        /// its middle in an opening and another its middle out of it.
        /// </summary>
        private static bool QuartersPart(GeoSolid3 body, GeoPlane3 first, GeoPlane3 second, GeoSolid3 opening, Tolerance tolerance)
        {
            double least = tolerance.EqualPoint * body.GrossSurfaceArea;
            bool someIn = false;
            bool someOut = false;

            foreach (double firstSide in new[] { 1.0, -1.0 })
            {
                foreach (double secondSide in new[] { 1.0, -1.0 })
                {
                    double volume = Quarter(body, first, firstSide, second, secondSide, out GeoPoint3 middle);

                    if (Math.Abs(volume) <= least)
                    {
                        continue;
                    }

                    if (IsIn(opening, middle, tolerance))
                    {
                        someIn = true;
                    }
                    else
                    {
                        someOut = true;
                    }
                }
            }

            return someIn && someOut;
        }

        /// <summary>
        /// Whether, of the eight parts three planes meeting at a point make of a body, one holding more than the tolerance
        /// times the body's area has its middle in an opening and another its middle out of it.
        /// </summary>
        /// <remarks>
        /// The parts are read as cones from the point the planes meet at, so that none of the sections closing a part adds to
        /// it. Planes two of which are parallel, or which meet along a line, meet at no one point, and are left to the quarters
        /// of each two. Nor are planes read that meet out of the opening's box, grown by the tolerance: what they leave is a
        /// corner the opening does not have.
        /// </remarks>
        private static bool EighthsPart(GeoSolid3 body, GeoPlane3 first, GeoPlane3 second, GeoPlane3 third, GeoSolid3 opening, Tolerance tolerance)
        {
            GeoVector3 normal = first.Normal;
            GeoVector3 secondNormal = second.Normal;
            GeoVector3 thirdNormal = third.Normal;
            double corner = normal.DotProduct(secondNormal.CrossProduct(thirdNormal));

            if (corner * corner <= 1E-12)
            {
                return false;
            }

            // The point on all three, from a point on the first: nought along its normal, and along each other normal the
            // height of that plane.
            GeoPoint3 origin = first.Origin;
            double secondHeight = secondNormal.DotProduct(origin.GetVectorTo(second.Origin));
            double thirdHeight = thirdNormal.DotProduct(origin.GetVectorTo(third.Origin));
            GeoPoint3 apex = origin.Add(thirdNormal.CrossProduct(normal).Multiply(secondHeight / corner)
                .Add(normal.CrossProduct(secondNormal).Multiply(thirdHeight / corner)));

            if (!opening.GetAabb().Contains(apex, tolerance))
            {
                return false;
            }

            double least = tolerance.EqualPoint * body.GrossSurfaceArea;
            bool someIn = false;
            bool someOut = false;

            foreach (double firstSide in new[] { 1.0, -1.0 })
            {
                foreach (double secondSide in new[] { 1.0, -1.0 })
                {
                    foreach (double thirdSide in new[] { 1.0, -1.0 })
                    {
                        double volume = Part(body, apex, origin, normal, firstSide, secondNormal, secondSide, thirdNormal, thirdSide, out GeoVector3 moment);

                        if (Math.Abs(volume) <= least)
                        {
                            continue;
                        }

                        if (IsIn(opening, origin.Add(moment.Multiply(1.0 / volume)), tolerance))
                        {
                            someIn = true;
                        }
                        else
                        {
                            someOut = true;
                        }
                    }
                }
            }

            return someIn && someOut;
        }

        /// <summary>
        /// Gets the volume of the part of a body on one side of each of two planes, and its centroid.
        /// </summary>
        /// <remarks>
        /// Planes that meet are read from a point on the line they meet along, so that neither section closing the part adds
        /// to it. Parallel planes leave a half-space or a slab, and a slab is the part beyond its nearer plane less the part
        /// beyond its further one.
        /// </remarks>
        private static double Quarter(GeoSolid3 body, GeoPlane3 first, double firstSide, GeoPlane3 second, double secondSide, out GeoPoint3 middle)
        {
            GeoVector3 normal = first.Normal;
            GeoVector3 along = normal.CrossProduct(second.Normal);
            double meeting = along.DotProduct(along);
            GeoPoint3 origin = first.Origin;
            double volume;
            GeoVector3 moment;

            if (meeting > 1E-12)
            {
                GeoPoint3 apex = origin.Add(along.CrossProduct(normal).Multiply(second.Normal.DotProduct(origin.GetVectorTo(second.Origin)) / meeting));
                volume = Part(body, apex, origin, normal, firstSide, second.Normal, secondSide, default, 0.0, out moment);
            }
            else
            {
                // Along the first normal: each side is at least or at most a height.
                double height = normal.DotProduct(origin.GetVectorTo(second.Origin));
                double facing = normal.DotProduct(second.Normal) > 0.0 ? secondSide : -secondSide;
                double lowest = double.NegativeInfinity;
                double highest = double.PositiveInfinity;

                if (firstSide > 0.0)
                {
                    lowest = 0.0;
                }
                else
                {
                    highest = 0.0;
                }

                if (facing > 0.0)
                {
                    lowest = Math.Max(lowest, height);
                }
                else
                {
                    highest = Math.Min(highest, height);
                }

                if (lowest >= highest)
                {
                    middle = origin;
                    return 0.0;
                }

                var none = new GeoVector3(0.0, 0.0, 0.0);
                volume = 0.0;
                moment = none;

                if (!double.IsInfinity(lowest))
                {
                    volume += Part(body, origin.Add(normal.Multiply(lowest)), origin, normal, 1.0, none, 0.0, none, 0.0, out GeoVector3 beyond);
                    moment = moment.Add(beyond);
                }

                if (!double.IsInfinity(highest))
                {
                    // Under the highest where nothing is under the lowest; otherwise the part over it taken away.
                    double side = double.IsInfinity(lowest) ? -1.0 : 1.0;
                    double part = Part(body, origin.Add(normal.Multiply(highest)), origin, normal, side, none, 0.0, none, 0.0, out GeoVector3 over);
                    volume += side < 0.0 ? part : -part;
                    moment = side < 0.0 ? moment.Add(over) : moment.Subtract(over);
                }
            }

            middle = volume != 0.0 ? origin.Add(moment.Multiply(1.0 / volume)) : origin;
            return volume;
        }

        /// <summary>
        /// Whether a body holds more on each side of a plane than the tolerance times its area, and where the middle of each
        /// part is.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="aboveMiddle">The centroid of the part on the side the plane's normal points to.</param>
        /// <param name="belowMiddle">The centroid of the part on the other side.</param>
        /// <remarks>
        /// No more than the tolerance times the area on one side, a body judged whole by one point puts no more than that on
        /// the wrong side of the plane, as a body grazed or taken a sliver off. A cell a cut left open by a crack a hair wide
        /// is weighed all the same: the strip missing changes each part by a third of its area times its distance from the
        /// point the parts are read from, 0.47 cubic millimetres for a crack 20 by 0.007 standing 10 from it.
        /// </remarks>
        private static bool Straddles(GeoSolid3 body, GeoPlane3 plane, Tolerance tolerance, out GeoPoint3 aboveMiddle, out GeoPoint3 belowMiddle)
        {
            GeoPoint3 apex = plane.Origin;
            GeoVector3 normal = plane.Normal;
            aboveMiddle = apex;
            belowMiddle = apex;

            // A body with every corner on one side of the plane has nothing on the other.
            double highest = double.NegativeInfinity;
            double lowest = double.PositiveInfinity;

            foreach (GeoFace3 face in body.Faces)
            {
                foreach (GeoPoint3 corner in face.Boundary.Vertices)
                {
                    double height = apex.GetVectorTo(corner).DotProduct(normal);
                    highest = Math.Max(highest, height);
                    lowest = Math.Min(lowest, height);
                }
            }

            if (!(highest > 0.0) || !(lowest < 0.0))
            {
                return false;
            }

            // Each part is the faces clipped to its side, read as cones from a point in the plane: the section closing each
            // part lies in the plane and adds nothing, so neither needs it.
            var none = new GeoVector3(0.0, 0.0, 0.0);
            double above = Part(body, apex, apex, normal, 1.0, none, 0.0, none, 0.0, out GeoVector3 aboveMoment);
            double below = Part(body, apex, apex, normal, -1.0, none, 0.0, none, 0.0, out GeoVector3 belowMoment);

            aboveMiddle = above != 0.0 ? apex.Add(aboveMoment.Multiply(1.0 / above)) : apex;
            belowMiddle = below != 0.0 ? apex.Add(belowMoment.Multiply(1.0 / below)) : apex;

            double least = Math.Min(Math.Abs(above), Math.Abs(below));
            return least > 0.0 && least > tolerance.EqualPoint * body.GrossSurfaceArea;
        }

        /// <summary>
        /// Gets the signed volume of the part of a body on one side of a plane, and of a second and a third where they are
        /// given, read as cones from a point on every plane given, and its moment about a point.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <param name="apex">A point on each plane, the tip of the cones.</param>
        /// <param name="reference">The point the moment is taken about.</param>
        /// <param name="normal">The normal of the first plane.</param>
        /// <param name="side">1 for the side the normal points to, -1 for the other.</param>
        /// <param name="second">The normal of the second plane; ignored where its side is nought.</param>
        /// <param name="secondSide">1 or -1 for a side of the second plane; nought for no second plane.</param>
        /// <param name="third">The normal of the third plane; ignored where its side is nought.</param>
        /// <param name="thirdSide">1 or -1 for a side of the third plane; nought for no third plane.</param>
        /// <param name="moment">The integral of the place less the reference point over the part.</param>
        private static double Part(GeoSolid3 body, GeoPoint3 apex, GeoPoint3 reference, GeoVector3 normal, double side, GeoVector3 second, double secondSide, GeoVector3 third, double thirdSide, out GeoVector3 moment)
        {
            GeoVector3 offset = reference.GetVectorTo(apex).Multiply(4.0);
            double volume = 0.0;
            moment = new GeoVector3(0.0, 0.0, 0.0);

            foreach (GeoFace3 face in body.Faces)
            {
                volume += ConeOfClipped(face.Boundary.Vertices, apex, offset, normal, side, second, secondSide, third, thirdSide, ref moment, 1.0);

                // A hole is wound the way its boundary is, so its cone is taken away.
                foreach (GeoPolygon3 hole in face.Holes)
                {
                    volume += ConeOfClipped(hole.Vertices, apex, offset, normal, side, second, secondSide, third, thirdSide, ref moment, -1.0);
                }
            }

            // Six times the volume, and the moment 24 times its own.
            moment = moment.Multiply(1.0 / 24.0);
            return volume / 6.0;
        }

        /// <summary>
        /// Gets six times the signed volume of the cone from a point to the part of a flat ring on one side of a plane through
        /// that point, and of a second and a third where they are given, and adds to a moment that of the cone, times 24.
        /// </summary>
        /// <param name="ring">The corners of the ring.</param>
        /// <param name="apex">A point in each plane, the tip of the cone.</param>
        /// <param name="offset">Four times the way from the point the moment is taken about to the apex.</param>
        /// <param name="normal">The normal of the plane.</param>
        /// <param name="side">1 for the side the normal points to, -1 for the other.</param>
        /// <param name="second">The normal of the second plane.</param>
        /// <param name="secondSide">1 or -1 for a side of the second plane; nought for none.</param>
        /// <param name="third">The normal of the third plane.</param>
        /// <param name="thirdSide">1 or -1 for a side of the third plane; nought for none.</param>
        /// <param name="moment">The moment, added to.</param>
        /// <param name="sign">1 to add the cone, -1 to take it away.</param>
        private static double ConeOfClipped(IReadOnlyList<GeoPoint3> ring, GeoPoint3 apex, GeoVector3 offset, GeoVector3 normal, double side, GeoVector3 second, double secondSide, GeoVector3 third, double thirdSide, ref GeoVector3 moment, double sign)
        {
            var corners = new List<GeoVector3>(ring.Count);

            foreach (GeoPoint3 corner in ring)
            {
                corners.Add(apex.GetVectorTo(corner));
            }

            List<GeoVector3> kept = ClipTo(corners, normal, side);

            if (secondSide != 0.0)
            {
                kept = ClipTo(kept, second, secondSide);
            }

            if (thirdSide != 0.0)
            {
                kept = ClipTo(kept, third, thirdSide);
            }

            double volume = 0.0;

            for (int i = 1; i + 1 < kept.Count; i++)
            {
                double tetrahedron = sign * kept[0].DotProduct(kept[i].CrossProduct(kept[i + 1]));
                volume += tetrahedron;
                moment = moment.Add(kept[0].Add(kept[i]).Add(kept[i + 1]).Add(offset).Multiply(tetrahedron));
            }

            return volume;
        }

        /// <summary>
        /// Clips a flat ring, its corners taken from a point in a plane, to one side of that plane.
        /// </summary>
        private static List<GeoVector3> ClipTo(List<GeoVector3> ring, GeoVector3 normal, double side)
        {
            var kept = new List<GeoVector3>(ring.Count + 2);

            for (int i = 0; i < ring.Count; i++)
            {
                GeoVector3 from = ring[i];
                GeoVector3 to = ring[(i + 1) % ring.Count];
                double fromHeight = side * from.DotProduct(normal);
                double toHeight = side * to.DotProduct(normal);

                if (fromHeight >= 0.0)
                {
                    kept.Add(from);
                }

                if ((fromHeight >= 0.0) != (toHeight >= 0.0))
                {
                    kept.Add(from.Add(to.Subtract(from).Multiply(fromHeight / (fromHeight - toHeight))));
                }
            }

            return kept;
        }

        /// <summary>
        /// Takes the material of a body with its openings cut in at once where it closes as <see cref="GeoSolid3.Validate(Tolerance)"/>
        /// reads it, or where the body or an opening does not; otherwise welds it within the tolerance, and where that does not
        /// close it takes the openings out of the body one at a time by a difference, as a boolean closes what it gives.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="openings">The openings cut.</param>
        /// <param name="material">What cutting them in at once gave; what is taken, or null where none is.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="failure">Why no closed material could be had; null otherwise.</param>
        /// <returns>false where the openings take all of the material, or no closed material could be had.</returns>
        /// <remarks>
        /// The weld is taken only where it moves the volume no further than moving the corners it moves by the tolerance can
        /// (<see cref="Weld3.SealedAsValidated"/>), and leaves no more than the body and no less than the body less every
        /// opening whole, each within the tolerance times the openings' area.
        /// </remarks>
        private static bool ClosesOrTakesOut(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, ref GeoSolid3 material, Tolerance tolerance, out Exception failure)
        {
            failure = null;
            Tolerance work = ForWork(tolerance);

            if (openings.Count == 0 || Shells3.Closes(material, work) || !Shells3.Closes(solid, work))
            {
                return true;
            }

            double gross = Math.Abs(solid.GrossVolume);
            double taken = 0.0;
            double skin = 0.0;

            foreach (GeoSolid3 opening in openings)
            {
                if (!Shells3.Closes(opening, work))
                {
                    return true;
                }

                taken += Math.Abs(opening.GrossVolume);
                skin += opening.GrossSurfaceArea;
            }

            GeoSolid3 welded = Weld3.SealedAsValidated(material, work, out _);
            double slack = work.EqualPoint * skin + 1E-9 * (gross + taken);
            double volume = welded.GrossVolume;

            if (Shells3.Closes(welded, work) && volume <= gross + slack && volume >= gross - taken - slack)
            {
                ClosedByWeld++;
                material = welded;
                return true;
            }

            GeometryHelperLog.Debug("Boolean3: cutting the openings in at once left the material open to Validate, and a weld did not close it; they are taken out one at a time.");
            return TakeOutOneAtATime(solid, openings, "Cutting the openings in left the material open", ref material, tolerance, out failure);
        }

        /// <summary>
        /// Takes the openings of a body out one at a time by a difference, where cutting them in at once left a piece of it
        /// part in an opening and part out of it; see <see cref="JudgedWhole"/>.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="openings">The openings cut.</param>
        /// <param name="material">What cutting them in at once gave, null where it left nothing; what is taken, or null where none is.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="failure">Why no material could be had; null otherwise.</param>
        /// <returns>false where the openings take all of the material, or no closed material could be had.</returns>
        private static bool TakesOutJudgedWhole(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, ref GeoSolid3 material, Tolerance tolerance, out Exception failure)
        {
            GeometryHelperLog.Debug("Boolean3: cutting the openings in at once left a piece of the body part in an opening and part out; they are taken out one at a time.");
            return TakeOutOneAtATime(solid, openings, "Cutting the openings in at once left a piece of the body part in an opening and part out", ref material, tolerance, out failure);
        }

        /// <summary>
        /// Takes the openings out of a body one at a time by a difference, the way back where cutting them in at once gave no
        /// material to trust.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="openings">The openings to take out.</param>
        /// <param name="why">What the cut at once gave, to begin the reason a failure gives.</param>
        /// <param name="material">What cutting them in at once gave, null where it left nothing; the body less the openings, or null where none is.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="failure">Why no material could be had; null where none was wanted.</param>
        /// <returns>false where the openings take all of the body, or one could not be taken out closed.</returns>
        private static bool TakeOutOneAtATime(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, string why, ref GeoSolid3 material, Tolerance tolerance, out Exception failure)
        {
            failure = null;
            TakenOutOneAtATime++;
            GeoSolid3 remaining = new GeoSolid3(solid.Faces);

            foreach (GeoSolid3 opening in openings)
            {
                if (TryTakeOut(remaining, opening, out GeoSolid3 left, tolerance, out Exception failed))
                {
                    remaining = left;
                }
                else
                {
                    // Not coming out closed; or taking all that is left, where the cut at once left material, which the two
                    // cannot both be, and is not believed.
                    failure = failed != null
                        ? new InvalidOperationException(why + ", and an opening could not be taken out of it closed.", failed)
                        : material != null
                            ? new InvalidOperationException(why + ", and taken out one at a time an opening took all that was left of it.")
                            : null;
                    material = null;
                    return false;
                }
            }

            material = remaining;
            return true;
        }

        /// <summary>
        /// A test hook: how many cuts of openings on this thread came out open and were welded closed, so that a test can say
        /// which way a cut went. Nothing in the library reads it.
        /// </summary>
        [ThreadStatic]
        internal static int ClosedByWeld;

        /// <summary>
        /// A test hook: how many cuts of openings on this thread came out open and would not weld closed, or left a piece of
        /// the body part in an opening and part out, and had their openings taken out one at a time, so that a test can say
        /// which way a cut went. Nothing in the library reads it.
        /// </summary>
        [ThreadStatic]
        internal static int TakenOutOneAtATime;

        #endregion
    }
}
