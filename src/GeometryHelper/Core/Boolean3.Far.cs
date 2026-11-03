using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Booleans of bodies standing far out from the origin, worked out near it.
    /// </summary>
    /// <remarks>
    /// A boolean reads a corner as on a plane within the tolerance and what rounding adds to it, a trillionth of the size of
    /// the coordinates (see <see cref="Splition3.Rounding"/>). A thousand million out that is a thousandth itself, and walls
    /// of a body and its openings standing a fraction of the tolerance apart could no longer be told apart: an L 100 by 80
    /// by 50 a hundred million out, with two blind openings whose walls stood 0.0009 apart, less a box across both walls,
    /// held 130 189.39 within a thousandth, where it holds 130 189.32, and the box less the L 28 457.23, where it holds
    /// 28 457.68. So such bodies are moved near the origin first, by whole coordinates, the boolean worked out there as it
    /// is worked out near the origin, and what it gives moved back out. The cut of a body's openings, its material as its
    /// volume, area, centroid and mass read it, a section of it and its cells are worked out so too.
    /// </remarks>
    public static partial class Boolean3
    {
        #region Far out

        /// <summary>
        /// How far out the bodies of a boolean must reach before they are moved near the origin for it: 2^20, a little over
        /// a kilometre.
        /// </summary>
        private const double FarReach = 1048576.0;

        /// <summary>
        /// How many times the size of their box the bodies of a boolean must reach out before they are moved near the origin
        /// for it.
        /// </summary>
        private const double FarTimesSize = 1024.0;

        /// <summary>
        /// How far from the origin, as a part of how far out the bodies reach, the least corner of their box is put in the
        /// frame they are worked out in: a 256th, so that a trillionth of the coordinates there, which a boolean reads a corner
        /// on a plane within, is still at least thirty times what rounding moved their corners by where they stand.
        /// </summary>
        /// <remarks>
        /// The corners of bodies far out were rounded where they stand, to half a ten-millionth a thousand million out, and
        /// walls meant to lie in one plane, of two openings or of an opening and the body, stand that far off it. Put at the
        /// origin, where a trillionth of the coordinates is a ten-thousand-millionth, such walls read as standing apart, and
        /// the walls of openings a fraction of the tolerance apart could not be told from them: of 1 196 unions, common parts
        /// and differences ten million to a thousand million out at a thousandth, 241 were wrong, and 190 were so moved to the
        /// least corner, 14 put a 256th of the reach out, 8 a 512th and 41 a 1 024th.
        /// </remarks>
        private const double FarLift = 1.0 / 256.0;

        /// <summary>
        /// How fine a tolerance, as a part of how far out the bodies reach, a boolean may be asked within and still be moved
        /// near the origin for it: a trillionth of the coordinates in the frame, what rounding can move a corner off a plane
        /// there.
        /// </summary>
        /// <remarks>
        /// Finer, the corners carry more rounding from where they stand than the frame allows for: moved near the origin at
        /// nought, a trillionth and a thousand-millionth, 58 of 5 292 such booleans that were right came out wrong or were not
        /// worked out. There the boolean raises the tolerance to the rounding where the bodies stand, as before; see
        /// BelowTheRounding.
        /// </remarks>
        private const double FarFinest = 1E-12 * FarLift;

        /// <summary>
        /// The frame a boolean of bodies far out is worked out in: their coordinates less an origin on each axis, the least
        /// corner of the box of all of them less <see cref="FarLift"/> of their reach, where every coordinate of theirs along
        /// it lies between half and twice that origin, so that each difference is exact (Sterbenz's lemma), and a corner
        /// moved in and back out is the one given, to the bit. Only the corners the boolean makes are rounded, once, as they
        /// are moved out: to about a ten-millionth of a millimetre a thousand million out, ten thousand times finer than the
        /// default tolerance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Bodies are moved only where they reach more than <see cref="FarReach"/> out and more than
        /// <see cref="FarTimesSize"/> times the size of their box, and where neither the point nor the planar tolerance asked
        /// is finer than <see cref="FarFinest"/> of how far out they reach: otherwise a boolean is worked out where the bodies
        /// stand, as it was, to the bit. Bodies moved into a frame are not moved again by what the boolean asks of them. A
        /// face or a body the boolean hands back untouched goes out as it was given, the very one; a polygon moved in has its
        /// normal and area measured again where it lands, as a body built there has them.
        /// </para>
        /// <para>
        /// The cut of a body's openings, the material its volume, area, centroid and mass are measured on, a section of it and
        /// its cells are worked out in a frame of the body alone, by the same rules; a volume and an area are the same in the
        /// frame as out of it, and what has a place, a corner, a plane, a box, is moved back.
        /// </para>
        /// </remarks>
        internal sealed class FarFrame
        {
            private readonly GeoVector3 _origin;
            private readonly GeoVector3 _in;

            // What each body moved in was moved in as, so that one given twice is moved once; and what each body and face moved
            // in was given as, so that one handed back untouched goes out as it came. Read by reference, never enumerated.
            private readonly Dictionary<GeoSolid3, GeoSolid3> _moved = new Dictionary<GeoSolid3, GeoSolid3>(SameObject<GeoSolid3>.Comparer);
            private readonly Dictionary<GeoSolid3, GeoSolid3> _givenBodies = new Dictionary<GeoSolid3, GeoSolid3>(SameObject<GeoSolid3>.Comparer);
            private readonly Dictionary<GeoFace3, GeoFace3> _givenFaces = new Dictionary<GeoFace3, GeoFace3>(SameObject<GeoFace3>.Comparer);

            // How many booleans on this thread are being worked out in a frame.
            [ThreadStatic]
            private static int _working;

            private FarFrame(GeoVector3 origin)
            {
                _origin = origin;
                _in = origin.Negate();
            }

            /// <summary>
            /// Marks that a boolean on this thread is worked out in a frame, so that what it asks of bodies already moved is not
            /// moved again; <see cref="Leave"/> undoes it.
            /// </summary>
            internal static void Enter() => _working++;

            /// <summary>
            /// Marks that a boolean on this thread worked out in a frame is done.
            /// </summary>
            internal static void Leave() => _working--;

            /// <summary>
            /// Works something out in a frame: marks it so, as <see cref="Enter"/> does, for as long as it takes.
            /// </summary>
            /// <param name="work">What is worked out, on bodies already moved into the frame.</param>
            /// <returns>What it gives, still in the frame.</returns>
            internal static T Inside<T>(Func<T> work)
            {
                Enter();

                try
                {
                    return work();
                }
                finally
                {
                    Leave();
                }
            }

            /// <summary>
            /// Gets the frame a body is worked out in, where it stands far out.
            /// </summary>
            /// <param name="body">The body.</param>
            /// <param name="tolerance">The tolerance it is worked out within.</param>
            /// <param name="frame">The frame; null when the method returns false.</param>
            /// <returns>true where the body stands far out, as <see cref="FarFrame"/> says; otherwise, false.</returns>
            internal static bool TryFor(GeoSolid3 body, Tolerance tolerance, out FarFrame frame)
                => TryFor(body, new GeoSolid3[0], tolerance, out frame);

            /// <summary>
            /// Gets the frame two bodies are worked out in, where they stand far out.
            /// </summary>
            /// <param name="first">The first body.</param>
            /// <param name="second">The second body.</param>
            /// <param name="tolerance">The tolerance the boolean is asked within.</param>
            /// <param name="frame">The frame; null when the method returns false.</param>
            /// <returns>true where the two stand far out, as <see cref="FarFrame"/> says; otherwise, false.</returns>
            internal static bool TryFor(GeoSolid3 first, GeoSolid3 second, Tolerance tolerance, out FarFrame frame)
                => TryFor(first, new[] { second }, tolerance, out frame);

            /// <summary>
            /// Gets the frame a body and others are worked out in, where they stand far out together.
            /// </summary>
            /// <param name="first">The first body.</param>
            /// <param name="others">The others; a null among them is passed over.</param>
            /// <param name="tolerance">The tolerance the booleans are asked within.</param>
            /// <param name="frame">The frame; null when the method returns false.</param>
            /// <returns>true where the bodies stand far out, as <see cref="FarFrame"/> says; otherwise, false.</returns>
            internal static bool TryFor(GeoSolid3 first, IEnumerable<GeoSolid3> others, Tolerance tolerance, out FarFrame frame)
            {
                frame = null;

                // Bodies moved into a frame are not moved again: they can still stand a 256th of the reach out, more than a
                // thousand times their size, and moved again their corners would come within less of the origin than the
                // rounding they carry allows.
                if (_working > 0)
                {
                    return false;
                }

                GeoAabb3 box = BoxOf(first);

                foreach (GeoSolid3 other in others)
                {
                    if (other != null)
                    {
                        box = box.Union(BoxOf(other));
                    }
                }

                if (box.IsEmpty)
                {
                    return false;
                }

                GeoPoint3 min = box.Min;
                GeoPoint3 max = box.Max;
                double reach = Math.Max(
                    Math.Max(Math.Max(Math.Abs(min.X), Math.Abs(max.X)), Math.Max(Math.Abs(min.Y), Math.Abs(max.Y))),
                    Math.Max(Math.Abs(min.Z), Math.Abs(max.Z)));
                double size = Math.Max(max.X - min.X, Math.Max(max.Y - min.Y, max.Z - min.Z));

                if (!(reach > FarReach && reach > FarTimesSize * size) || Math.Min(tolerance.EqualPoint, tolerance.EqualPlanar) < FarFinest * reach)
                {
                    return false;
                }

                double lift = FarLift * reach;
                frame = new FarFrame(new GeoVector3(Corner(min.X, max.X, lift), Corner(min.Y, max.Y, lift), Corner(min.Z, max.Z, lift)));
                return true;
            }

            /// <summary>
            /// Gets the origin along an axis, the least coordinate less the lift, where every coordinate from the least to the
            /// greatest lies within twice it and over half of it, so that each less it is exact; nought otherwise, the axis not
            /// moved.
            /// </summary>
            private static double Corner(double min, double max, double lift)
            {
                double origin = min - lift;

                return (origin > 0.0 && min >= 0.5 * origin && max <= 2.0 * origin) || (origin < 0.0 && max <= 0.5 * origin && min >= 2.0 * origin)
                    ? origin
                    : 0.0;
            }

            /// <summary>
            /// Gets the box of a body's faces and of its openings', which can reach past them.
            /// </summary>
            private static GeoAabb3 BoxOf(GeoSolid3 body)
            {
                GeoAabb3 box = body.GetAabb();

                foreach (GeoSolid3 opening in body.Openings)
                {
                    box = box.Union(BoxOf(opening));
                }

                return box;
            }

            /// <summary>
            /// Works out a boolean of two bodies in the frame, and moves what it gives back out.
            /// </summary>
            internal bool Work(Operation operation, GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, Tolerance tolerance, out BooleanOutcome outcome, out GeoSolid3 open)
            {
                bool made;
                Enter();

                try
                {
                    made = operation(In(first), In(second), out result, tolerance, out outcome, out open);
                }
                finally
                {
                    Leave();
                }

                result = Out(result);
                open = Out(open);
                return made;
            }

            /// <summary>
            /// Moves a body given into the frame, its openings with it; null stays null.
            /// </summary>
            internal GeoSolid3 In(GeoSolid3 given)
            {
                if (given == null)
                {
                    return null;
                }

                if (_moved.TryGetValue(given, out GeoSolid3 moved))
                {
                    return moved;
                }

                var faces = new GeoFace3[given.Faces.Count];

                for (int i = 0; i < faces.Length; i++)
                {
                    GeoFace3 face = given.Faces[i];
                    faces[i] = face.MovedBy(_in, true);
                    _givenFaces[faces[i]] = face;
                }

                var openings = new GeoSolid3[given.Openings.Count];

                for (int i = 0; i < openings.Length; i++)
                {
                    openings[i] = In(given.Openings[i]);
                }

                moved = new GeoSolid3(faces, openings);
                _moved[given] = moved;
                _givenBodies[moved] = given;
                return moved;
            }

            /// <summary>
            /// Moves a body out of the frame, its openings with it: a body or a face moved in goes out as it was given; null
            /// stays null.
            /// </summary>
            internal GeoSolid3 Out(GeoSolid3 moved)
            {
                if (moved == null)
                {
                    return null;
                }

                if (_givenBodies.TryGetValue(moved, out GeoSolid3 given))
                {
                    return given;
                }

                var faces = new GeoFace3[moved.Faces.Count];

                for (int i = 0; i < faces.Length; i++)
                {
                    GeoFace3 face = moved.Faces[i];
                    faces[i] = _givenFaces.TryGetValue(face, out GeoFace3 was) ? was : face.MovedBy(_origin, false);
                }

                var openings = new GeoSolid3[moved.Openings.Count];

                for (int i = 0; i < openings.Length; i++)
                {
                    openings[i] = Out(moved.Openings[i]);
                }

                return new GeoSolid3(faces, openings);
            }

            /// <summary>
            /// Moves a plane into the frame: its point, coordinate by coordinate, and its normal as it is.
            /// </summary>
            internal GeoPlane3 In(GeoPlane3 plane) => GeoPlane3.FromUnitNormal(plane.Origin.Add(_in), plane.Normal);

            /// <summary>
            /// Moves a placement of a grid into the frame: the point a cell starts at, where it has one; its axes as they are.
            /// </summary>
            internal MeshPlacement3 In(MeshPlacement3 placement)
                => placement.Origin.HasValue ? placement.At(placement.Origin.Value.Add(_in)) : placement;

            /// <summary>
            /// Moves faces out of the frame, as the faces of a body are moved out.
            /// </summary>
            internal GeoFace3[] Out(GeoFace3[] moved)
            {
                var faces = new GeoFace3[moved.Length];

                for (int i = 0; i < faces.Length; i++)
                {
                    faces[i] = _givenFaces.TryGetValue(moved[i], out GeoFace3 was) ? was : moved[i].MovedBy(_origin, false);
                }

                return faces;
            }

            /// <summary>
            /// Moves a box out of the frame: its centre, coordinate by coordinate; its axes and sizes as they are.
            /// </summary>
            internal GeoObb3 Out(GeoObb3 box)
                => new GeoObb3(box.CoordinateSystem.WithOrigin(box.Center.Add(_origin)), box.SizeX, box.SizeY, box.SizeZ);

            /// <summary>
            /// Moves a grid of cells out of the frame: its origin, each cell's box and each cell's material; where the cells
            /// start and end along its axes, measured from its origin, and what each holds, as they are.
            /// </summary>
            internal GeoCellGrid3 Out(GeoCellGrid3 grid)
                => grid.Moved(grid.Frame.WithOrigin(grid.Frame.Origin.Add(_origin)), cell => cell.Moved(Out, Out));

            /// <summary>
            /// Moves what a run of cuts reports out of the frame: the place of each repair of a cut closed.
            /// </summary>
            internal SubtractReport Out(SubtractReport report)
            {
                var closings = new SolidClosing3[report.Closings.Count];

                for (int i = 0; i < closings.Length; i++)
                {
                    SolidClosing3 closing = report.Closings[i];
                    var repairs = new SolidRepair3[closing.Repairs.Count];

                    for (int k = 0; k < repairs.Length; k++)
                    {
                        SolidRepair3 repair = closing.Repairs[k];
                        repairs[k] = new SolidRepair3(repair.Kind, repair.Location.Add(_origin), repair.Size);
                    }

                    GeoPoint3? at = closing.FailureLocation;
                    closings[i] = new SolidClosing3(repairs, closing.AddedArea, closing.VolumeChange, closing.Failure, at.HasValue ? at.Value.Add(_origin) : (GeoPoint3?)null);
                }

                return new SubtractReport(report.Tools, report.Taken, report.Skipped, report.WithinFallback, report.Closed, closings, report.RefusedByVolume);
            }
        }

        /// <summary>
        /// Compares objects by reference alone, for the bodies and faces a <see cref="FarFrame"/> moves.
        /// </summary>
        private sealed class SameObject<T> : IEqualityComparer<T>
            where T : class
        {
            /// <summary>
            /// Gets the one comparer.
            /// </summary>
            internal static readonly SameObject<T> Comparer = new SameObject<T>();

            /// <summary>
            /// Determines whether two references are to the same object.
            /// </summary>
            public bool Equals(T x, T y) => ReferenceEquals(x, y);

            /// <summary>
            /// Gets the hash of the object itself, whatever its type says of its value.
            /// </summary>
            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }

        #endregion
    }
}
