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
        /// more ways of cutting and keeps only what closes; where one will not come out so, or what they leave is not valid as
        /// <c>Validate</c> reads it, wound two ways where the differences met, the method returns false. Each difference reads
        /// a corner as on a plane within the planar tolerance and the rounding of the coordinates, a trillionth of their size,
        /// as cutting them in at once does: within the tolerance alone, a plate 300 by 200 by 12, turned and far from the
        /// origin, with twelve holes ending 0.01 under its top to within a millionth, cut within a hundredth, came out valid
        /// and 130.70 cubic millimetres short of the plate less every hole through; it holds 666 668.11, within what its
        /// skins can leave. They are taken out so too where a plane of an opening runs through the body and will not cut
        /// it, leaving a piece part in the
        /// opening and part out of it, which cut in at once would be kept or dropped whole. A plate 300 by 200 by 12 with two
        /// blind holes ending 0.015 under its top, turned and far from the origin, came out of the cut within a hundredth open
        /// by four edges 0.015 long; the holes are taken out one at a time, and it closes, holding 711 120.61 as
        /// <see cref="GeoSolid3.GetVolume(Tolerance)"/> measures it. Cut in together, each opening keeps or loses a skin the
        /// tolerance thin as it would cut in alone: a plate with twelve holes ending 0.01 under its top to within a
        /// millionth, one 2.0E-6 nearer the top, cut within a hundredth, kept a skin over that one that the plane of another
        /// hole's floor cut off the whole plate, and came out valid and 3.71 cubic millimetres over the most its skins can
        /// leave; it holds what they can leave now. Where the point tolerance is below the rounding of the
        /// coordinates, a quadrillionth of the largest of them, the openings are cut within that rounding, as the queries read
        /// them; a planar tolerance below it alone changes nothing. Where cutting them in together leaves the material open they
        /// are cut in at once one at a time, each kept only where it closes, not taken out by a difference, which within the
        /// rounding can come back closed and wrong, and the corners the cut leaves a rounding apart are made one where that
        /// closes the material within the tolerance given. Cut at a tolerance of nought, a plate 200 by 100 by 3 turned about
        /// in space, with a hole 20 by 20 through it, came back as a shell of 7 faces, its bottom lost, and the method returned
        /// true; it comes back closed, holding 58 800. Walls of two openings, or of an opening and the body, standing apart
        /// within the tolerance of one plane are cut within a finer tolerance, where what that gives closes within the one
        /// asked: an L 100 by 80 by 50, a million out, with two blind openings 27 apart whose walls stood 0.0009 apart
        /// across it, cut within a thousandth, held 153 674.08 where it holds 153 673.56, the second opening's wall cut in
        /// the plane of the first's. See <see cref="GeoSolid3.TryCutOpenings(out GeoSolid3, Tolerance)"/>
        /// for what it refuses and what the measures of the body read at nought.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the body is null.</exception>
        public static bool TryCutOpenings(GeoSolid3 solid, out GeoSolid3 material, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            return TryCutOpenings(solid, out material, tolerance, out Exception failure)
                || (failure != null && Unworkable("cut of openings", failure, out material));
        }

        /// <summary>
        /// Cuts every opening of a body into it as <see cref="TryCutOpenings(GeoSolid3, out GeoSolid3, Tolerance)"/> does, within
        /// the rounding of the coordinates where the point tolerance is below it, handing back a cut that cannot be worked out
        /// rather than warning of it.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="material">The body with every opening cut into it, or the body itself when it has none; null when the method returns false.</param>
        /// <param name="tolerance">The tolerance asked.</param>
        /// <param name="failure">What was thrown where the cut could not be worked out, or why it would not close; null otherwise.</param>
        /// <returns>true when some material is left; false when the openings take all of it, or the cut cannot be worked out.</returns>
        internal static bool TryCutOpenings(GeoSolid3 solid, out GeoSolid3 material, Tolerance tolerance, out Exception failure)
        {
            // A point tolerance below the rounding of the coordinates is raised to it, as the queries raise it (see Material3);
            // a planar one below it alone is not, and the cut is as it was.
            Tolerance within = Material3.ToRounding(solid, tolerance);
            bool withinRounding = within.EqualPoint > tolerance.EqualPoint;

            if (!TryCutOpenings(solid, solid.Openings, out material, withinRounding ? within : tolerance, withinRounding, out failure, out _))
            {
                return false;
            }

            // The faces cut within the rounding meet on corners as far apart as it; made one, the material closes within the
            // tolerance given too.
            if (withinRounding && material != null && !material.ClosesByCorners(tolerance)
                && Weld3.TryWeld(material, within, tolerance, out GeoSolid3 welded))
            {
                material = welded;
            }

            return true;
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
            => TryCutOpenings(solid, openings, out material, tolerance, false, out failure, out _);

        /// <summary>
        /// Cuts some of a body's openings into it as <see cref="TryCutOpenings(GeoSolid3, IReadOnlyList{GeoSolid3}, out GeoSolid3, Tolerance, out Exception)"/>
        /// does, handing back too what cutting them in at once gave where no closed material could be had.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="openings">The openings to cut; the others are not carried over.</param>
        /// <param name="material">The body with those openings cut into it; null when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="withinRounding">Whether the tolerance is the rounding of the coordinates; see <see cref="TakeOutOneAtATime"/>.</param>
        /// <param name="failure">What was thrown where the cut could not be worked out, or why it would not close; null otherwise.</param>
        /// <param name="open">
        /// What cutting the openings in at once gave, open or with a piece judged whole, where it was left for no closed
        /// material; null otherwise.
        /// </param>
        private static bool TryCutOpenings(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, out GeoSolid3 material, Tolerance tolerance, bool withinRounding, out Exception failure, out GeoSolid3 open)
        {
            open = null;
            failure = null;

            if (TryCutWallsApart(solid, openings, out material, tolerance))
            {
                return true;
            }

            return TryCutOpeningsWithin(solid, openings, out material, tolerance, withinRounding, out failure, out open);
        }

        /// <summary>
        /// Cuts some of a body's openings into it within a finer tolerance where walls stand apart within the tolerance of one
        /// plane (see <see cref="FinerForWallsApart(GeoSolid3, IReadOnlyList{GeoSolid3}, Tolerance, out Tolerance)"/>), as
        /// <see cref="TryCutOpenings(GeoSolid3, IReadOnlyList{GeoSolid3}, out GeoSolid3, Tolerance, out Exception)"/> cuts them,
        /// taking what that gives where it closes within the tolerance asked.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="openings">The openings to cut.</param>
        /// <param name="material">The body with the openings cut into it; null when the method returns false.</param>
        /// <param name="tolerance">The tolerance asked.</param>
        /// <returns>true where such walls stand apart and the material so cut closes; otherwise, false, and nothing is cut.</returns>
        internal static bool TryCutWallsApart(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, out GeoSolid3 material, Tolerance tolerance)
        {
            material = null;

            if (!FinerForWallsApart(solid, openings, tolerance, out Tolerance finer) || !ClosesAsFinely(solid, openings, tolerance, finer)
                || !TryCutOpeningsWithin(solid, openings, out GeoSolid3 fine, finer, false, out _, out _)
                || fine == null
                || !Shells3.Closes(fine, ForWork(tolerance)))
            {
                return false;
            }

            material = fine;
            return true;
        }

        /// <summary>
        /// Works out a union, a common part or a difference of two bodies within a finer tolerance where walls of one of them
        /// and its openings stand apart within the tolerance of one plane (see
        /// <see cref="FinerForWallsApart(GeoSolid3, GeoSolid3, Tolerance, out Tolerance)"/>): the openings of each cut into it
        /// within that tolerance, as <see cref="TryCutOpenings(GeoSolid3, out GeoSolid3, Tolerance)"/> cuts them, and the
        /// operation worked out on the two so cut; what it gives is taken where it is made and closes within the tolerance
        /// asked.
        /// </summary>
        /// <param name="operation">The operation, as the public methods work it out within the tolerance given.</param>
        /// <param name="first">The first body; for a difference, the one material is taken from.</param>
        /// <param name="second">The second body; for a difference, the one taken away.</param>
        /// <param name="result">What the operation gives; null when the method returns false.</param>
        /// <param name="tolerance">The tolerance asked.</param>
        /// <param name="open">What came out open where two closed bodies gave nothing closed, for a closing to try.</param>
        /// <returns>
        /// true where such walls stand apart and the operation so worked out made a body that closes; otherwise, false, and the
        /// operation is worked out as before.
        /// </returns>
        /// <remarks>
        /// A boolean cuts the openings of a body into it at once, and the plane of one opening cut the whole of what it reached:
        /// a plane of another within the tolerance of it was read as that plane, however far away, as the cut of openings read
        /// it. An L 100 by 80 by 50 with two blind openings 27 apart whose walls stood 0.0009 apart across it, less a box 65 by
        /// 20 by 40 across both walls, within a thousandth or a hundredth, held 130 189.39 one way round and 130 189.26 the
        /// other, where it holds 130 189.32, and the box less the L 28 457.23 and 28 457.88, where it holds 28 457.68. Of 4 800
        /// unions, common parts and differences of prisms with two or three such openings, turned about in space or not, at the
        /// origin and a thousand to a thousand million out, and boxes, at 1E-3 and 1E-2, 1 363 that were wrong are right now,
        /// every one a million out and nearer among them, and none that was right moved, but in its last bits; at a millionth
        /// and below, where no walls stood so near, nothing changed, to the bit. Where a wall of the other body lies within the
        /// tolerance of the plane of a wall of either, or the finer tolerance would come within a few roundings of the
        /// coordinates (see <see cref="FinerRoundings"/>), the boolean is worked out as before: of 4 800 more, a face of each
        /// box a tenth to six tenths of the tolerance off the plane of a wall, 5 changed, all to right.
        /// </remarks>
        private static bool WithWallsApart(Operation operation, GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, Tolerance tolerance, out GeoSolid3 open)
        {
            result = null;
            open = null;

            if (first.Openings.Count == 0 && second.Openings.Count == 0
                || !FinerForWallsApart(first, second, tolerance, out Tolerance finer)
                || !(finer.EqualPlanar >= FinerRoundings * Math.Max(Splition3.Rounding(first), Splition3.Rounding(second)))
                || !ClosesAsFinely(first, first.Openings, tolerance, finer) || !ClosesAsFinely(second, second.Openings, tolerance, finer)
                || !MaterialWithin(first, finer, tolerance, out GeoSolid3 a) || !MaterialWithin(second, finer, tolerance, out GeoSolid3 b)
                || !operation(a, b, out GeoSolid3 made, finer, out BooleanOutcome outcome, out GeoSolid3 left)
                || outcome != BooleanOutcome.Made || made == null || !Shells3.Closes(made, ForWork(tolerance)))
            {
                return false;
            }

            result = made;
            open = left;
            return true;
        }

        /// <summary>
        /// How many times what rounding can move a corner off a plane, a trillionth of the size of the coordinates, the finer
        /// tolerance a boolean is worked out within where walls stand apart (see <see cref="WithWallsApart"/>) has to be; a
        /// boolean whose finer tolerance would be less is worked out as before.
        /// </summary>
        /// <remarks>
        /// Of 4 800 unions, common parts and differences of prisms with such openings and boxes, at 1E-3 and 1E-2, 3 that were
        /// right came out wrong within a finer tolerance two to four times that trillionth, all ten million and more out, the
        /// worst 0.98 off a thousand million out. None does within eight times it, and up to a million out every one still
        /// comes out right; 354 further out that came out right within the finer tolerance are worked out as before, and
        /// wrong.
        /// </remarks>
        private const double FinerRoundings = 8.0;

        /// <summary>
        /// A body with its openings cut into it within a finer tolerance, as
        /// <see cref="TryCutOpenings(GeoSolid3, out GeoSolid3, Tolerance)"/> cuts them, where what that gives closes within
        /// the tolerance asked; the body itself where it has none.
        /// </summary>
        private static bool MaterialWithin(GeoSolid3 solid, Tolerance finer, Tolerance tolerance, out GeoSolid3 material)
        {
            material = solid;

            if (solid.Openings.Count == 0)
            {
                return true;
            }

            return TryCutOpeningsWithin(solid, solid.Openings, out material, finer, false, out _, out _)
                && material != null && Shells3.Closes(material, ForWork(tolerance));
        }

        /// <summary>
        /// Whether the body and each opening that close within the tolerance asked close within the finer one too: one closed
        /// only within the tolerance, by a crack or corners further apart than the finer one, is not cut within it.
        /// </summary>
        /// <remarks>
        /// A cube 10 across cracked 0.0001 wide, less two openings 0.0022 apart, cut within the finer tolerance that their
        /// walls asked for, came back closed within a thousandth and holding the whole cube, 999.998 where it holds 749.88.
        /// </remarks>
        private static bool ClosesAsFinely(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, Tolerance tolerance, Tolerance finer)
        {
            Tolerance work = ForWork(tolerance);
            Tolerance fine = ForWork(finer);

            if (Shells3.Closes(solid, work) && !Shells3.Closes(solid, fine))
            {
                return false;
            }

            foreach (GeoSolid3 opening in openings)
            {
                if (Shells3.Closes(opening, work) && !Shells3.Closes(opening, fine))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Cuts some of a body's openings into it within the tolerance given, as
        /// <see cref="TryCutOpenings(GeoSolid3, IReadOnlyList{GeoSolid3}, out GeoSolid3, Tolerance, bool, out Exception, out GeoSolid3)"/>
        /// does where no walls stand apart within the tolerance of one plane.
        /// </summary>
        private static bool TryCutOpeningsWithin(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, out GeoSolid3 material, Tolerance tolerance, bool withinRounding, out Exception failure, out GeoSolid3 open)
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
                if (judgedWhole
                    ? TakesOutJudgedWhole(solid, openings, ref material, tolerance, withinRounding, out failure)
                    : ClosesOrTakesOut(solid, openings, ref material, tolerance, withinRounding, out failure))
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
        /// Determines whether two walls of a body and its openings, of two openings or of an opening and the body, standing
        /// apart, lie within the tolerance of one plane and further apart than the rounding of the coordinates, and gives the
        /// tolerance they are cut within then: one between the gap of those walls and the most that walls that meet, or a face
        /// off flat, stand off one plane within the tolerance.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="openings">The openings to cut.</param>
        /// <param name="tolerance">The tolerance asked.</param>
        /// <param name="finer">The tolerance to cut within; the one asked where the method returns false.</param>
        /// <returns>
        /// true where such walls stand apart, the narrowest gap between them more than four times the widest of walls that
        /// meet; otherwise, false.
        /// </returns>
        /// <remarks>
        /// The planes of an opening cut the whole cell they reach, and a plane of another opening within the tolerance of the
        /// first then finds that cell's corners on it, and leaves it whole: its wall is cut in the first one's plane, however
        /// far away. An L 100 by 80 by 50, a million out, with two blind openings 27 apart whose walls stood 0.0009 apart
        /// across the L, cut within a thousandth, held 153 674.08 where it holds 153 673.56, its section 0.014 off, and with
        /// the openings the other way round 153 673.30. Walls that meet within the tolerance are made one plane by the cut, as
        /// they are meant to be, and a face off flat is laid flat, so the finer tolerance stays wider than either; where it
        /// cannot, the walls are cut as before.
        /// </remarks>
        internal static bool FinerForWallsApart(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, Tolerance tolerance, out Tolerance finer)
            => FinerForWallsApart(new[] { (solid, openings) }, tolerance, out finer);

        /// <summary>
        /// Determines whether two walls of one of two bodies and its openings stand apart within the tolerance of one plane,
        /// as <see cref="FinerForWallsApart(GeoSolid3, IReadOnlyList{GeoSolid3}, Tolerance, out Tolerance)"/> reads them,
        /// where no wall of either body or its openings lies within the tolerance of the plane of a wall of the other; and
        /// gives the tolerance a boolean of the two is worked out within then.
        /// </summary>
        /// <param name="first">The first body of the boolean.</param>
        /// <param name="second">The second body of the boolean.</param>
        /// <param name="tolerance">The tolerance asked.</param>
        /// <param name="finer">The tolerance to work within; the one asked where the method returns false.</param>
        /// <returns>
        /// true where such walls stand apart, as for one body, and no wall of the one lies so near the plane of a wall of the
        /// other; otherwise, false.
        /// </returns>
        /// <remarks>
        /// A wall of one body within the tolerance of the plane of a wall of the other is read by the boolean as it was, worked
        /// out within the tolerance: of 4 800 booleans of prisms with such openings and boxes whose face stood a tenth to six
        /// tenths of the tolerance off the plane of a wall of the prism or of an opening, 12 that came out as the two are came
        /// out otherwise within the finer tolerance, the box's face put onto the wall in part, as the material cut alone
        /// reads it. Such a boolean is worked out as before, and none moves but to right.
        /// </remarks>
        internal static bool FinerForWallsApart(GeoSolid3 first, GeoSolid3 second, Tolerance tolerance, out Tolerance finer)
            => FinerForWallsApart(new[] { (first, first.Openings), (second, second.Openings) }, tolerance, out finer);

        /// <summary>
        /// Determines whether two walls of one of some bodies and its openings stand apart within the tolerance of one plane,
        /// where no wall of one of the bodies lies within it of the plane of a wall of another. See
        /// <see cref="FinerForWallsApart(GeoSolid3, IReadOnlyList{GeoSolid3}, Tolerance, out Tolerance)"/>.
        /// </summary>
        private static bool FinerForWallsApart((GeoSolid3 Solid, IReadOnlyList<GeoSolid3> Openings)[] bodies, Tolerance tolerance, out Tolerance finer)
        {
            finer = tolerance;

            if (Array.TrueForAll(bodies, body => body.Openings.Count == 0))
            {
                return false;
            }

            Tolerance work = ForWork(tolerance);
            double rounding = 0.0;
            GeoAabb3 box = GeoAabb3.Empty;

            foreach ((GeoSolid3 solid, _) in bodies)
            {
                rounding = Math.Max(rounding, Splition3.Rounding(solid));
                box = box.Union(solid.GetAabb());
            }

            double onPlane = work.EqualPlanar + rounding;

            if (box.IsEmpty || !(work.EqualPlanar > 4.0 * rounding))
            {
                return false;
            }

            // A face with no plane, its corners on a line, is no wall.
            var walls = new List<(GeoFace3 Face, GeoPlane3 Plane, int Body, int Owner)>();

            for (int n = 0; n < bodies.Length; n++)
            {
                (GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings) = bodies[n];

                foreach (GeoFace3 face in solid.Faces)
                {
                    if (face.Normal.Length > 0.5)
                    {
                        walls.Add((face, face.GetPlane(), n, -1));
                    }
                }

                for (int i = 0; i < openings.Count; i++)
                {
                    foreach (GeoFace3 face in openings[i].Faces)
                    {
                        if (face.Normal.Length > 0.5)
                        {
                            walls.Add((face, face.GetPlane(), n, i));
                        }
                    }
                }
            }

            // Each wall twice, by how far the middle of the box stands off its plane either way round, so that two planes
            // within the tolerance of one over the box come within it of each other in one of the two orders; see
            // Splition3.IsOnePlaneOver.
            GeoPoint3 middle = box.Center;
            GeoPoint3[] corners = box.GetCorners();
            var keys = new (double Key, int Wall, bool Turned)[2 * walls.Count];

            for (int k = 0; k < walls.Count; k++)
            {
                double height = walls[k].Plane.SignedDistanceTo(middle);
                keys[2 * k] = (height, k, false);
                keys[2 * k + 1] = (-height, k, true);
            }

            Array.Sort(keys, (a, b) =>
            {
                int byKey = a.Key.CompareTo(b.Key);

                if (byKey != 0)
                {
                    return byKey;
                }

                int byWall = a.Wall.CompareTo(b.Wall);
                return byWall != 0 ? byWall : a.Turned.CompareTo(b.Turned);
            });

            double apart = double.PositiveInfinity;
            double together = rounding;

            for (int a = 0; a < keys.Length; a++)
            {
                (GeoFace3 face, GeoPlane3 plane, int body, int owner) = walls[keys[a].Wall];

                for (int b = a + 1; b < keys.Length && keys[b].Key - keys[a].Key <= onPlane; b++)
                {
                    (GeoFace3 otherFace, GeoPlane3 other, int otherBody, int otherOwner) = walls[keys[b].Wall];

                    // Each pair is met with one of them as it is: walls turned the same way both as they are, walls turned
                    // opposite ways one as it is and the other turned, whichever comes first. Met only with the first as it
                    // is, walls turned opposite ways were met only where rounding put their heights one way: an L's inner wall
                    // and an opening's wall turned the other way 0.0004 off its plane, 5 along it, were not met at all, and
                    // the L measured 0.09 too little.
                    if (keys[b].Wall == keys[a].Wall || keys[a].Turned && keys[b].Turned)
                    {
                        continue;
                    }

                    double sign = keys[a].Turned != keys[b].Turned ? -1.0 : 1.0;

                    if (plane.Normal.DotProduct(other.Normal) * sign < 0.5)
                    {
                        continue;
                    }

                    double least = double.PositiveInfinity;
                    double most = 0.0;

                    foreach (GeoPoint3 corner in corners)
                    {
                        double gap = Math.Abs(plane.SignedDistanceTo(corner) - sign * other.SignedDistanceTo(corner));
                        least = Math.Min(least, gap);
                        most = Math.Max(most, gap);
                    }

                    if (most > onPlane)
                    {
                        continue;
                    }

                    // A wall of one body within the tolerance of the plane of a wall of the other leaves the boolean as it
                    // was, but where the two lie in one plane to the rounding of the coordinates, a quadrillionth of their
                    // size, which no finer tolerance reads otherwise.
                    if (body != otherBody)
                    {
                        if (most > 1E-3 * rounding)
                        {
                            return false;
                        }

                        continue;
                    }

                    if (most <= rounding)
                    {
                        continue;
                    }

                    // Walls of the body, of one opening, or that meet, are made one plane by the cut; walls of two apart,
                    // or of an opening and the body apart, are not to be. Walls facing each other across the gap meet: they
                    // stand no further apart than it.
                    if (owner != otherOwner && face.DistanceTo(otherFace, tolerance) > most + work.EqualPoint)
                    {
                        apart = Math.Min(apart, least);
                    }
                    else
                    {
                        together = Math.Max(together, most);
                    }
                }
            }

            if (double.IsPositiveInfinity(apart))
            {
                return false;
            }

            // A face off flat is laid flat within the tolerance; the finer one has to lay it flat too.
            foreach ((GeoFace3 face, GeoPlane3 plane, _, _) in walls)
            {
                together = Math.Max(together, OffFlat(face.Boundary, plane));

                foreach (GeoPolygon3 hole in face.Holes)
                {
                    together = Math.Max(together, OffFlat(hole, plane));
                }
            }

            double level = Math.Sqrt(together * apart);

            if (!(apart > 4.0 * together) || !(level < work.EqualPlanar))
            {
                return false;
            }

            finer = new Tolerance(Math.Min(level, tolerance.EqualPoint), tolerance.EqualVector, tolerance.EqualAngleRad, Math.Min(level, tolerance.EqualPlanar));
            return true;
        }

        /// <summary>
        /// How far the furthest corner of a ring stands off a plane.
        /// </summary>
        private static double OffFlat(GeoPolygon3 ring, GeoPlane3 plane)
        {
            double most = 0.0;

            foreach (GeoPoint3 corner in ring.Vertices)
            {
                most = Math.Max(most, Math.Abs(plane.SignedDistanceTo(corner)));
            }

            return most;
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
        /// <remarks>
        /// Where the point tolerance is below the rounding of the coordinates the openings are cut within it, as
        /// <see cref="TryCutOpenings(GeoSolid3, out GeoSolid3, Tolerance)"/> cuts them: cut at nought, a plate 200 by 100 by 3
        /// turned about in space, with a hole 20 by 20 through it, was meshed by <see cref="GeoSolid3.TriangulateSurface(Tolerance)"/>
        /// into triangles holding 29 999.99 where it holds 58 800, and a point in the middle of the hole was projected 1.5 from
        /// it, onto a face across the hole, where its wall stands 10 off.
        /// </remarks>
        internal static GeoSolid3 MaterialToRead(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, Tolerance tolerance)
        {
            // A point tolerance below the rounding of the coordinates is raised to it, and the openings cut within it as
            // TryCutOpenings cuts them; Material3 hands its queries' tolerance in raised already, and they read as they did.
            Tolerance within = Material3.ToRounding(solid, tolerance);
            bool withinRounding = within.EqualPoint > tolerance.EqualPoint;

            if (TryCutOpenings(solid, openings, out GeoSolid3 material, withinRounding ? within : tolerance, withinRounding, out Exception failure, out GeoSolid3 open))
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
        /// <param name="onPlaneWithinRounding">
        /// Whether the difference reads a corner as on a plane within the planar tolerance and the rounding of the body's
        /// coordinates, as the cut of openings does (see <see cref="Splition3.OnPlaneWithinRounding"/>); see
        /// <see cref="TakeOutOneAtATime"/>.
        /// </param>
        /// <returns>true when some material is left; false when the opening takes all of it, or the cut cannot be worked out.</returns>
        /// <remarks>
        /// The way back where cutting every opening in at once cannot be worked out or leaves the material open: a
        /// difference tries more ways of cutting and keeps the one that closes, and an opening that will not come out
        /// keeps none of the others in.
        /// </remarks>
        internal static bool TryTakeOut(GeoSolid3 body, GeoSolid3 opening, out GeoSolid3 material, Tolerance tolerance, out Exception failure, bool onPlaneWithinRounding = false)
        {
            failure = null;

            try
            {
                if (TakeAway(body, opening, out material, tolerance, out string unworkable, out _, null, onPlaneWithinRounding))
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

            // A skin a plane of another opening cut off, thinner than the tolerance over this one, goes with it, as it would
            // were this one cut in alone; see IsAlongOpening.
            var owners = new List<int>();
            List<GeoFace3> faces = FacesOfCells(pieces, owner, tolerance, owners, 0, true);
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
        /// <param name="read">
        /// Whether the middle of a part of a piece is read, given the piece and the middle; every one is where null. The middle
        /// of a part of a piece that is not convex can lie outside it; see <see cref="Misjudged"/>.
        /// </param>
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
        internal static bool JudgedWhole(List<GeoSolid3> pieces, List<(GeoPlane3 Plane, GeoSolid3 Opening)> uncut, Tolerance tolerance, Func<GeoSolid3, GeoPoint3, bool> read = null)
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

                    if (IsIn(opening, aboveMiddle, tolerance) != IsIn(opening, belowMiddle, tolerance)
                        && (read == null || read(piece, aboveMiddle) && read(piece, belowMiddle)))
                    {
                        return true;
                    }

                    foreach ((GeoPlane3 other, GeoSolid3 of) in crossing)
                    {
                        if (ReferenceEquals(of, opening) && QuartersPart(piece, other, plane, opening, tolerance, read))
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
                                if (ReferenceEquals(corners[j].Opening, opening) && EighthsPart(piece, corners[i].Plane, corners[j].Plane, plane, opening, tolerance, read))
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
        private static bool QuartersPart(GeoSolid3 body, GeoPlane3 first, GeoPlane3 second, GeoSolid3 opening, Tolerance tolerance, Func<GeoSolid3, GeoPoint3, bool> read)
            => PartsApart(Quarters(body, first, second), body, opening, tolerance, read);

        /// <summary>
        /// Whether, of the parts of a body given, one holding more than the tolerance times the body's area has its middle in
        /// an opening and another its middle out of it, each middle read only where <paramref name="read"/> takes it.
        /// </summary>
        private static bool PartsApart(List<(double Volume, GeoPoint3 Middle)> parts, GeoSolid3 body, GeoSolid3 opening, Tolerance tolerance, Func<GeoSolid3, GeoPoint3, bool> read)
        {
            double least = tolerance.EqualPoint * body.GrossSurfaceArea;
            bool someIn = false;
            bool someOut = false;

            foreach ((double volume, GeoPoint3 middle) in parts)
            {
                if (Math.Abs(volume) <= least)
                {
                    continue;
                }

                // A middle is read only where it would tell something not known yet: the reading costs a search of the body.
                bool isIn = IsIn(opening, middle, tolerance);

                if ((isIn ? someIn : someOut) || read != null && !read(body, middle))
                {
                    continue;
                }

                someIn |= isIn;
                someOut |= !isIn;
            }

            return someIn && someOut;
        }

        /// <summary>
        /// The two parts a plane makes of a body, the side its normal points to first, each with its volume and its middle.
        /// </summary>
        private static List<(double Volume, GeoPoint3 Middle)> Halves(GeoSolid3 body, GeoPlane3 plane)
        {
            GeoPoint3 apex = NearestTo(plane, body);
            var none = new GeoVector3(0.0, 0.0, 0.0);
            var parts = new List<(double Volume, GeoPoint3 Middle)>(2);

            foreach (double side in new[] { 1.0, -1.0 })
            {
                double volume = Part(body, apex, apex, plane.Normal, side, none, 0.0, none, 0.0, out GeoVector3 moment);
                parts.Add((volume, volume != 0.0 ? apex.Add(moment.Multiply(1.0 / volume)) : apex));
            }

            return parts;
        }

        /// <summary>
        /// The four parts two planes make of a body, each with its volume and its middle; see <see cref="Quarter"/>.
        /// </summary>
        private static List<(double Volume, GeoPoint3 Middle)> Quarters(GeoSolid3 body, GeoPlane3 first, GeoPlane3 second)
        {
            var parts = new List<(double Volume, GeoPoint3 Middle)>(4);

            foreach (double firstSide in new[] { 1.0, -1.0 })
            {
                foreach (double secondSide in new[] { 1.0, -1.0 })
                {
                    double volume = Quarter(body, first, firstSide, second, secondSide, out GeoPoint3 middle);
                    parts.Add((volume, middle));
                }
            }

            return parts;
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
        private static bool EighthsPart(GeoSolid3 body, GeoPlane3 first, GeoPlane3 second, GeoPlane3 third, GeoSolid3 opening, Tolerance tolerance, Func<GeoSolid3, GeoPoint3, bool> read)
            => Meet(body, first, second, third, out GeoPoint3 apex, out GeoPoint3 origin) && opening.GetAabb().Contains(apex, tolerance)
                && PartsApart(Eighths(body, apex, origin, first, second, third), body, opening, tolerance, read);

        /// <summary>
        /// Finds the point three planes meet at, and the point of the first nearest the middle of a body's box, the parts of
        /// the body are read from; false where two are parallel or the three meet along a line.
        /// </summary>
        private static bool Meet(GeoSolid3 body, GeoPlane3 first, GeoPlane3 second, GeoPlane3 third, out GeoPoint3 apex, out GeoPoint3 origin)
        {
            GeoVector3 normal = first.Normal;
            GeoVector3 secondNormal = second.Normal;
            GeoVector3 thirdNormal = third.Normal;
            double corner = normal.DotProduct(secondNormal.CrossProduct(thirdNormal));
            apex = GeoPoint3.Origin;
            origin = GeoPoint3.Origin;

            if (corner * corner <= 1E-12)
            {
                return false;
            }

            // The point on all three, from a point on the first: nought along its normal, and along each other normal the
            // height of that plane.
            origin = NearestTo(first, body);
            double secondHeight = secondNormal.DotProduct(origin.GetVectorTo(second.Origin));
            double thirdHeight = thirdNormal.DotProduct(origin.GetVectorTo(third.Origin));
            apex = origin.Add(thirdNormal.CrossProduct(normal).Multiply(secondHeight / corner)
                .Add(normal.CrossProduct(secondNormal).Multiply(thirdHeight / corner)));
            return true;
        }

        /// <summary>
        /// The eight parts three planes meeting at a point make of a body, each with its volume and its middle, read as cones
        /// from that point; see <see cref="Meet"/>.
        /// </summary>
        private static List<(double Volume, GeoPoint3 Middle)> Eighths(GeoSolid3 body, GeoPoint3 apex, GeoPoint3 origin, GeoPlane3 first, GeoPlane3 second, GeoPlane3 third)
        {
            var parts = new List<(double Volume, GeoPoint3 Middle)>(8);

            foreach (double firstSide in new[] { 1.0, -1.0 })
            {
                foreach (double secondSide in new[] { 1.0, -1.0 })
                {
                    foreach (double thirdSide in new[] { 1.0, -1.0 })
                    {
                        double volume = Part(body, apex, origin, first.Normal, firstSide, second.Normal, secondSide, third.Normal, thirdSide, out GeoVector3 moment);
                        parts.Add((volume, origin.Add(moment.Multiply(1.0 / volume))));
                    }
                }
            }

            return parts;
        }

        /// <summary>
        /// Whether, of the six parts three planes two of which are parallel make of a body, one holding more than the tolerance
        /// times the body's area has its middle in an opening and another its middle out of it.
        /// </summary>
        /// <remarks>
        /// Such planes meet at no one point, and <see cref="EighthsPart"/> leaves them: the parts are those short of both
        /// parallel planes, between them and beyond both, on each side of the third, each read as the part beyond a height
        /// along the normal of the first, on that side of the third, less the part beyond a greater one, as
        /// <see cref="Quarter"/> reads two parallel planes. A box dipping 0.088 into an arm of a U, its sides x = 9.74 and
        /// 19.14 and its floor z = 7.1, holds 2.55 of it between its sides and over its floor, a part no two of the planes
        /// part from the rest.
        /// </remarks>
        private static bool SlabsPart(GeoSolid3 body, GeoPlane3 a, GeoPlane3 b, GeoPlane3 c, GeoSolid3 opening, Tolerance tolerance, Func<GeoSolid3, GeoPoint3, bool> read)
            => Slabs(body, a, b, c) is List<(double Volume, GeoPoint3 Middle)> parts && PartsApart(parts, body, opening, tolerance, read);

        /// <summary>
        /// The six parts three planes two of which are parallel make of a body, each with its volume and its middle, on the
        /// side of the third its normal points to first, short of both parallel planes, between them and beyond both; null
        /// where no two are parallel, or the third is parallel to them too. See <see cref="SlabsPart"/>.
        /// </summary>
        private static List<(double Volume, GeoPoint3 Middle)> Slabs(GeoSolid3 body, GeoPlane3 a, GeoPlane3 b, GeoPlane3 c)
        {
            bool Parallel(GeoPlane3 one, GeoPlane3 another)
            {
                GeoVector3 across = one.Normal.CrossProduct(another.Normal);
                return across.DotProduct(across) <= 1E-12;
            }

            GeoPlane3 first = a, second = b, third = c;

            if (Parallel(b, c))
            {
                first = b;
                second = c;
                third = a;
            }
            else if (Parallel(a, c))
            {
                second = c;
                third = b;
            }
            else if (!Parallel(a, b))
            {
                return null;
            }

            GeoVector3 normal = first.Normal;
            GeoVector3 along = normal.CrossProduct(third.Normal);
            double meeting = along.DotProduct(along);

            if (meeting <= 1E-12)
            {
                return null;
            }

            // Heights along the first normal from a point on the first: nought for it, and the height of the second.
            GeoPoint3 origin = NearestTo(first, body);
            double height = normal.DotProduct(origin.GetVectorTo(second.Origin));
            double low = Math.Min(0.0, height);
            double high = Math.Max(0.0, height);
            var parts = new List<(double Volume, GeoPoint3 Middle)>(6);

            // The part beyond a height along the first normal, or short of it, on one side of the third, read as cones from
            // the point of the third at that height nearest the origin.
            double Beyond(double level, double side, double thirdSide, out GeoVector3 moment)
            {
                GeoPoint3 point = origin.Add(normal.Multiply(level));
                GeoPoint3 apex = point.Add(along.CrossProduct(normal).Multiply(third.Normal.DotProduct(point.GetVectorTo(third.Origin)) / meeting));
                return Part(body, apex, origin, normal, side, third.Normal, thirdSide, default, 0.0, out moment);
            }

            foreach (double thirdSide in new[] { 1.0, -1.0 })
            {
                double shortOfLow = Beyond(low, -1.0, thirdSide, out GeoVector3 shortOfLowMoment);
                double beyondLow = Beyond(low, 1.0, thirdSide, out GeoVector3 beyondLowMoment);
                double beyondHigh = Beyond(high, 1.0, thirdSide, out GeoVector3 beyondHighMoment);
                double between = beyondLow - beyondHigh;
                parts.Add((shortOfLow, origin.Add(shortOfLowMoment.Multiply(1.0 / shortOfLow))));
                parts.Add((between, origin.Add(beyondLowMoment.Subtract(beyondHighMoment).Multiply(1.0 / between))));
                parts.Add((beyondHigh, origin.Add(beyondHighMoment.Multiply(1.0 / beyondHigh))));
            }

            return parts;
        }

        /// <summary>
        /// The most parts the planes crossing a cell are read as making by <see cref="Grid"/>: eight of each of three families,
        /// the planes of a box crossing a cell seven times each way.
        /// </summary>
        private const int MostGridParts = 512;

        /// <summary>
        /// The parts planes make of a body where they fall into no more than three families of parallel planes, whose ways
        /// meet at a point where there are three, each part with its volume and its middle; null for more families, three
        /// meeting along a line, or more parts than <see cref="MostGridParts"/>.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <param name="planes">The planes.</param>
        /// <remarks>
        /// Each part lies between two planes of each family, or beyond the last, and is read, as <see cref="Quarter"/> reads
        /// two parallel planes, from the parts beyond one plane of each of some of the families: a slab is what lies beyond its
        /// nearer plane less what lies beyond its further one, and each part beyond is read as cones from the point where its
        /// planes meet nearest the middle of the body's box, so that none of the sections closing it adds to it. Planes within
        /// the rounding of the coordinates of each other along their way are read as one. A comb 50 by 10 by 10 with two
        /// notches 10 wide, its bottom cracked under the sides of two boxes dipping 0.088 into the tooth beside each notch, is
        /// one cell crossed by the four sides and the floor the boxes share, ten parts.
        /// </remarks>
        private static List<(double Volume, GeoPoint3 Middle)> Grid(GeoSolid3 body, List<GeoPlane3> planes)
        {
            GeoPoint3 origin = body.GetAabb().Center;
            var normals = new List<GeoVector3>(3);
            var heights = new List<List<double>>(3);

            foreach (GeoPlane3 plane in planes)
            {
                int family = -1;

                for (int f = 0; f < normals.Count && family < 0; f++)
                {
                    GeoVector3 across = normals[f].CrossProduct(plane.Normal);
                    family = across.DotProduct(across) <= 1E-12 ? f : -1;
                }

                if (family < 0)
                {
                    if (normals.Count == 3)
                    {
                        return null;
                    }

                    normals.Add(plane.Normal);
                    heights.Add(new List<double>());
                    family = normals.Count - 1;
                }

                heights[family].Add(normals[family].DotProduct(origin.GetVectorTo(plane.Origin)));
            }

            double corner = normals.Count == 3 ? normals[0].DotProduct(normals[1].CrossProduct(normals[2])) : 1.0;

            if (corner * corner <= 1E-12)
            {
                return null;
            }

            // Each family's heights in order, those within the rounding of the one before read as it.
            double rounding = Splition3.Rounding(body);
            int count = 1;

            foreach (List<double> family in heights)
            {
                family.Sort();

                for (int i = family.Count - 1; i > 0; i--)
                {
                    if (family[i] - family[i - 1] <= rounding)
                    {
                        family.RemoveAt(i);
                    }
                }

                count *= family.Count + 1;
            }

            if (count > MostGridParts)
            {
                return null;
            }

            // The part beyond plane chosen[f] of each family f, none where it is -1, its volume and moment about the origin.
            var none = new GeoVector3(0.0, 0.0, 0.0);
            var beyond = new Dictionary<int, (double Volume, GeoVector3 Moment)>();

            (double Volume, GeoVector3 Moment) Beyond(int[] chosen)
            {
                int key = 0;

                for (int f = normals.Count - 1; f >= 0; f--)
                {
                    key = key * (heights[f].Count + 1) + chosen[f] + 1;
                }

                if (beyond.TryGetValue(key, out (double Volume, GeoVector3 Moment) known))
                {
                    return known;
                }

                var ways = new List<GeoVector3>(3);
                var levels = new List<double>(3);

                for (int f = 0; f < normals.Count; f++)
                {
                    if (chosen[f] >= 0)
                    {
                        ways.Add(normals[f]);
                        levels.Add(heights[f][chosen[f]]);
                    }
                }

                (double Volume, GeoVector3 Moment) part;

                if (ways.Count == 0)
                {
                    double over = Part(body, origin, origin, normals[0], 1.0, none, 0.0, none, 0.0, out GeoVector3 overMoment);
                    double under = Part(body, origin, origin, normals[0], -1.0, none, 0.0, none, 0.0, out GeoVector3 underMoment);
                    part = (over + under, overMoment.Add(underMoment));
                }
                else
                {
                    // The point on every plane chosen nearest the origin.
                    GeoVector3 offset;

                    if (ways.Count == 1)
                    {
                        offset = ways[0].Multiply(levels[0]);
                    }
                    else if (ways.Count == 2)
                    {
                        double cosine = ways[0].DotProduct(ways[1]);
                        double sine = 1.0 - cosine * cosine;
                        offset = ways[0].Multiply((levels[0] - cosine * levels[1]) / sine).Add(ways[1].Multiply((levels[1] - cosine * levels[0]) / sine));
                    }
                    else
                    {
                        offset = ways[1].CrossProduct(ways[2]).Multiply(levels[0] / corner)
                            .Add(ways[2].CrossProduct(ways[0]).Multiply(levels[1] / corner))
                            .Add(ways[0].CrossProduct(ways[1]).Multiply(levels[2] / corner));
                    }

                    double volume = Part(body, origin.Add(offset), origin, ways[0], 1.0,
                        ways.Count > 1 ? ways[1] : none, ways.Count > 1 ? 1.0 : 0.0,
                        ways.Count > 2 ? ways[2] : none, ways.Count > 2 ? 1.0 : 0.0, out GeoVector3 moment);
                    part = (volume, moment);
                }

                beyond[key] = part;
                return part;
            }

            // Each part, between planes j - 1 and j of each family, is what lies beyond the first of them, or all of it where
            // j is nought, less what lies beyond the second, where j is not the last; over the families, every way of taking
            // one of the two, signed.
            var parts = new List<(double Volume, GeoPoint3 Middle)>(count);
            var at = new int[normals.Count];
            var chosen = new int[normals.Count];

            for (int p = 0; p < count; p++)
            {
                int rest = p;

                for (int f = 0; f < normals.Count; f++)
                {
                    at[f] = rest % (heights[f].Count + 1);
                    rest /= heights[f].Count + 1;
                }

                double volume = 0.0;
                GeoVector3 moment = none;

                for (int term = 0; term < 1 << normals.Count; term++)
                {
                    double sign = 1.0;
                    bool empty = false;

                    for (int f = 0; f < normals.Count; f++)
                    {
                        bool upper = (term >> f & 1) == 1;
                        empty |= upper && at[f] == heights[f].Count;
                        chosen[f] = upper ? at[f] : at[f] - 1;
                        sign = upper ? -sign : sign;
                    }

                    if (!empty)
                    {
                        (double partVolume, GeoVector3 partMoment) = Beyond(chosen);
                        volume += sign * partVolume;
                        moment = moment.Add(partMoment.Multiply(sign));
                    }
                }

                parts.Add((volume, origin.Add(moment.Multiply(1.0 / volume))));
            }

            return parts;
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
            GeoPoint3 origin = NearestTo(first, body);
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
        /// Gets the point of a plane nearest the middle of a body's box, the point the parts of the body are read from.
        /// </summary>
        /// <remarks>
        /// The cones the parts are read as are summed from that point, and from a point far off they are large and cancel to
        /// a part of the size of the body, their rounding left behind: a prism 17 km long, the origins of its planes at its
        /// far corners up to 8.4 km off, put the middle of a quarter of a cube 10 across it ran through 2.7 km off along the prism.
        /// </remarks>
        private static GeoPoint3 NearestTo(GeoPlane3 plane, GeoSolid3 body)
        {
            GeoPoint3 middle = body.GetAabb().Center;
            return middle.Add(plane.Normal.Multiply(-plane.SignedDistanceTo(middle)));
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
            GeoPoint3 apex = NearestTo(plane, body);
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
        /// <param name="withinRounding">Whether the tolerance is the rounding of the coordinates; see <see cref="TakeOutOneAtATime"/>.</param>
        /// <param name="failure">Why no closed material could be had; null otherwise.</param>
        /// <returns>false where the openings take all of the material, or no closed material could be had.</returns>
        /// <remarks>
        /// The weld is taken only where it moves the volume no further than moving the corners it moves by the tolerance can
        /// (<see cref="Weld3.SealedAsValidated"/>), and leaves no more than the body and no less than the body less every
        /// opening whole, each within the tolerance times the openings' area.
        /// </remarks>
        private static bool ClosesOrTakesOut(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, ref GeoSolid3 material, Tolerance tolerance, bool withinRounding, out Exception failure)
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
            return TakeOutOneAtATime(solid, openings, "Cutting the openings in left the material open", ref material, tolerance, withinRounding, out failure);
        }

        /// <summary>
        /// Takes the openings of a body out one at a time by a difference, where cutting them in at once left a piece of it
        /// part in an opening and part out of it; see <see cref="JudgedWhole"/>.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="openings">The openings cut.</param>
        /// <param name="material">What cutting them in at once gave, null where it left nothing; what is taken, or null where none is.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="withinRounding">Whether the tolerance is the rounding of the coordinates; see <see cref="TakeOutOneAtATime"/>.</param>
        /// <param name="failure">Why no material could be had; null otherwise.</param>
        /// <returns>false where the openings take all of the material, or no closed material could be had.</returns>
        private static bool TakesOutJudgedWhole(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, ref GeoSolid3 material, Tolerance tolerance, bool withinRounding, out Exception failure)
        {
            GeometryHelperLog.Debug("Boolean3: cutting the openings in at once left a piece of the body part in an opening and part out; they are taken out one at a time.");
            return TakeOutOneAtATime(solid, openings, "Cutting the openings in at once left a piece of the body part in an opening and part out", ref material, tolerance, withinRounding, out failure);
        }

        /// <summary>
        /// Takes the openings out of a body one at a time by a difference, or within the rounding of the coordinates cuts them
        /// in at once one at a time, the way back where cutting them in at once together gave no material to trust.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="openings">The openings to take out.</param>
        /// <param name="why">What the cut at once gave, to begin the reason a failure gives.</param>
        /// <param name="material">What cutting them in at once gave, null where it left nothing; the body less the openings, or null where none is.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="withinRounding">
        /// Whether the tolerance is the rounding of the coordinates, a caller's below it raised to it, where a difference cannot
        /// be trusted (see <see cref="TryCutIn"/>).
        /// </param>
        /// <param name="failure">Why no material could be had; null where none was wanted.</param>
        /// <returns>
        /// false where the openings take all of the body, or one could not be taken out closed, or what they leave is not
        /// valid as <see cref="GeoSolid3.Validate(Tolerance)"/> reads it.
        /// </returns>
        /// <remarks>
        /// Each difference comes out closed, and reads a corner as on a plane within the planar tolerance and the rounding of
        /// the coordinates besides, a trillionth of their size, as the cut of openings does (see
        /// <see cref="Splition3.OnPlaneWithinRounding"/>), all through: its cells, its faces and what it closes. Within the
        /// tolerance alone, a plate 300 by 200 by 12, turned and far from the origin, less a hole 22 across whose floor lay
        /// 0.001 under its top to 2E-15, within a thousandth, had its sides cut along a line falling from the top to the floor
        /// across the plate, each face within the tolerance of where it belongs. With twelve such holes, one of them 1.8E-7
        /// deeper, the twelve differences came out wound two ways at the top, which <c>Validate</c> reads as not valid,
        /// holding 666 653.98; they hold 666 657.36, eleven holes through and one under a skin 0.0010002 thick. With twelve
        /// 0.01 under to within a millionth, cut within a hundredth, a plate came out valid and 130.70 cubic millimetres short
        /// of the plate less every hole through; it holds 666 668.11, within what its skins can leave. Read so where it parts
        /// the cells only, the difference reads the rest otherwise: of 600 such plates cut so, 4 that are right came out
        /// wrong and 10 were refused. With the point tolerance widened by the rounding too, a cube 10 across, cracked, less a
        /// box, cut within a hundredth, came back 0.32 cubic millimetres short where it is exact. A difference read so that
        /// will not come out is worked out within the tolerance alone, as it was: a plate with twelve boxes hidden 0.001
        /// inside its faces, near the origin, had one that came out open whichever way it was cut, and was refused; taken
        /// out so, it holds what its skins can leave. What the differences leave is still refused where it is not valid.
        /// </remarks>
        private static bool TakeOutOneAtATime(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, string why, ref GeoSolid3 material, Tolerance tolerance, bool withinRounding, out Exception failure)
        {
            failure = null;
            TakenOutOneAtATime++;
            GeoSolid3 remaining = new GeoSolid3(solid.Faces);

            foreach (GeoSolid3 opening in openings)
            {
                // A difference read within the rounding that will not come out is worked out within the tolerance alone.
                if (withinRounding
                    ? TryCutIn(remaining, opening, out GeoSolid3 left, tolerance, out Exception failed)
                    : TryTakeOut(remaining, opening, out left, tolerance, out failed, true)
                        || (failed != null && TryTakeOut(remaining, opening, out left, tolerance, out failed)))
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

            // Each difference comes out closed, and the chain of them can still come out wound two ways; see the remarks. Cut in
            // at once within the rounding, the material is as it was.
            if (!withinRounding && !remaining.ValidateForWork(ForWork(tolerance)).IsValid)
            {
                failure = new InvalidOperationException(why + ", and taken out one at a time the openings left it not valid.");
                material = null;
                return false;
            }

            material = remaining;
            return true;
        }

        /// <summary>
        /// Cuts one opening into a body at once, keeping it only where it closes and no piece was judged whole: the way back,
        /// within the rounding of the coordinates, where cutting every opening in at once gave no material to trust.
        /// </summary>
        /// <param name="body">The body, without openings.</param>
        /// <param name="opening">The opening.</param>
        /// <param name="material">The body less the opening; null when the method returns false.</param>
        /// <param name="tolerance">The tolerance, the rounding of the coordinates.</param>
        /// <param name="failure">What was thrown where the cut could not be worked out, or why it would not close; null otherwise.</param>
        /// <returns>true when some material is left, closed; false when the opening takes all of it, or the cut will not close.</returns>
        /// <remarks>
        /// A difference cut within the rounding came out closed and wrong: a plate 200 by 100 by 3, turned about in space and
        /// some 700 out, with two blind holes, lost nothing to the first hole taken out by a difference, and held 55 407.53
        /// where it holds 53 641.32. Cut in at once, one at a time, the holes take 1 766.21 and 4 592.47 out of it, as they do.
        /// </remarks>
        private static bool TryCutIn(GeoSolid3 body, GeoSolid3 opening, out GeoSolid3 material, Tolerance tolerance, out Exception failure)
        {
            if (!TryCutOpeningsAtOnce(body, new[] { opening }, out material, tolerance, out failure, out bool judgedWhole))
            {
                return false;
            }

            if (judgedWhole || !Shells3.Closes(material, ForWork(tolerance)))
            {
                failure = new InvalidOperationException("Cut in within the rounding of the coordinates, the opening left the material open, or a piece of it part in the opening and part out.");
                material = null;
                return false;
            }

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
