using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Up to six clip planes. What is kept is what lies on the kept side of every plane that is on, which is a convex region: a wedge, a slab, a
    /// box. A set never changes: adding, replacing or removing a plane gives a new set. The cutting is done in double precision on the shapes
    /// themselves, so what is drawn, what is picked and what is snapped to all see the same cut.
    /// </summary>
    /// <remarks>
    /// A set has one <see cref="Slack"/>, and everything that asks it something goes by that one: a point no further outside a plane than the slack is
    /// ON the plane, and is kept. A face that lies in a plane, to the rounding of its corners, is kept whole whichever side the plane keeps, and is drawn,
    /// picked, snapped to, selected by a box and cut for its section by the same rule. A point beyond the slack is cut at the plane itself, not at the
    /// slack: a line cut by a plane is as long as the plane leaves it.
    /// </remarks>
    public sealed class ClipSet
    {
        /// <summary>
        /// The most planes a set holds: six, the faces of a box.
        /// </summary>
        public const int MaxPlanes = 6;

        private static readonly ClipSet _empty = new ClipSet(new ClipPlane[0], 0.0);
        private readonly ClipPlane[] _planes;
        private readonly ClipPlane[] _active;
        private readonly double _slack;

        /// <summary>
        /// Initializes a set with no slack of its own: applied to an item, it takes the slack the box of that item gives
        /// (<see cref="SlackFor"/>); a set made for a scene with <see cref="ForScene"/> keeps the scene's.
        /// </summary>
        /// <param name="planes">The planes, in order.</param>
        /// <exception cref="ArgumentNullException">Thrown when the planes or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown when there are more than <see cref="MaxPlanes"/>.</exception>
        public ClipSet(IEnumerable<ClipPlane> planes)
            : this(planes, 0.0)
        {
        }

        private ClipSet(IEnumerable<ClipPlane> planes, double slack)
        {
            if (planes == null)
            {
                throw new ArgumentNullException(nameof(planes));
            }

            _planes = planes.ToArray();

            if (_planes.Length > MaxPlanes)
            {
                throw new ArgumentException("A set holds at most " + MaxPlanes + " clip planes, not " + _planes.Length + ".", nameof(planes));
            }

            if (_planes.Any(p => p == null))
            {
                throw new ArgumentNullException(nameof(planes), "A clip plane in the set is null.");
            }

            _active = _planes.Where(p => p.Enabled).ToArray();
            _slack = slack;
        }

        /// <summary>
        /// Gets the set with no plane, which cuts nothing.
        /// </summary>
        public static ClipSet Empty => _empty;

        /// <summary>
        /// Gets the planes, in order, those that are off included.
        /// </summary>
        public IReadOnlyList<ClipPlane> Planes => _planes;

        /// <summary>
        /// Gets how many planes the set holds.
        /// </summary>
        public int Count => _planes.Length;

        /// <summary>
        /// Gets whether any plane is on, and so whether the set cuts anything.
        /// </summary>
        public bool IsActive => _active.Length > 0;

        /// <summary>
        /// Gets how far outside a plane a point may be and still be on it, in millimetres; nought for a set made without a scene.
        /// </summary>
        public double Slack => _slack;

        /// <summary>
        /// Gets the slack a scene is cut with: a billionth of its size and of a unit, and a trillionth of its largest coordinate, which is more than the rounding
        /// of a point of it and less than anything a person draws. The drawing, the picking, the snaps, the box select and the sections all take it from here.
        /// </summary>
        /// <param name="scene">The box round the scene; with none the slack is nought.</param>
        public static double SlackFor(Bounds3 scene)
        {
            if (scene.IsEmpty)
            {
                return 0.0;
            }

            double largest = Math.Max(
                Math.Max(Math.Max(Math.Abs(scene.Min.X), Math.Abs(scene.Min.Y)), Math.Abs(scene.Min.Z)),
                Math.Max(Math.Max(Math.Abs(scene.Max.X), Math.Abs(scene.Max.Y)), Math.Abs(scene.Max.Z)));
            return 1E-9 * (1.0 + scene.Size.Length) + 1E-12 * largest;
        }

        /// <summary>
        /// Gets the set with another slack.
        /// </summary>
        /// <param name="slack">How far outside a plane a point may be and be on it, in millimetres.</param>
        /// <exception cref="ArgumentException">Thrown when the slack is negative or not a number.</exception>
        public ClipSet WithSlack(double slack)
        {
            if (!(slack >= 0.0) || double.IsInfinity(slack))
            {
                throw new ArgumentException("The slack must be a finite number not below nought.", nameof(slack));
            }

            return new ClipSet(_planes, slack);
        }

        /// <summary>
        /// Gets the set with the slack of a scene, <see cref="SlackFor"/>.
        /// </summary>
        /// <param name="scene">The box round the scene.</param>
        public ClipSet ForScene(Bounds3 scene) => WithSlack(SlackFor(scene));

        /// <summary>
        /// Gets the set with a plane added at the end.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <exception cref="InvalidOperationException">Thrown when the set holds six planes already.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the plane is null.</exception>
        public ClipSet Add(ClipPlane plane)
        {
            if (plane == null)
            {
                throw new ArgumentNullException(nameof(plane));
            }

            if (_planes.Length >= MaxPlanes)
            {
                throw new InvalidOperationException("The set holds " + MaxPlanes + " clip planes already; remove one first.");
            }

            return new ClipSet(_planes.Concat(new[] { plane }), _slack);
        }

        /// <summary>
        /// Gets the set with one plane replaced.
        /// </summary>
        /// <param name="index">The place of the plane in <see cref="Planes"/>.</param>
        /// <param name="plane">The plane that takes its place.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no plane at the index.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the plane is null.</exception>
        public ClipSet Replace(int index, ClipPlane plane)
        {
            if (index < 0 || index >= _planes.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (plane == null)
            {
                throw new ArgumentNullException(nameof(plane));
            }

            ClipPlane[] copy = (ClipPlane[])_planes.Clone();
            copy[index] = plane;
            return new ClipSet(copy, _slack);
        }

        /// <summary>
        /// Gets the set without one plane.
        /// </summary>
        /// <param name="index">The place of the plane in <see cref="Planes"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no plane at the index.</exception>
        public ClipSet Remove(int index)
        {
            if (index < 0 || index >= _planes.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return new ClipSet(_planes.Where((p, i) => i != index), _slack);
        }

        /// <summary>
        /// Makes the six planes of a box that keep what is inside it: the clip box of a selection.
        /// </summary>
        /// <param name="box">The box; it must not be empty.</param>
        /// <param name="margin">How far each face is put out from the box, in millimetres, so that what lies on the box is not cut by a rounding; it may be nought.</param>
        /// <returns>The set of six planes, in the order -X, +X, -Y, +Y, -Z, +Z faces, each keeping the side towards the middle of the box.</returns>
        /// <exception cref="ArgumentException">Thrown when the box is empty or the margin is negative.</exception>
        public static ClipSet FromBox(Bounds3 box, double margin = 0.0)
        {
            if (box.IsEmpty)
            {
                throw new ArgumentException("A clip box needs a box with something in it.", nameof(box));
            }

            if (!(margin >= 0.0) || double.IsInfinity(margin))
            {
                throw new ArgumentException("The margin of a clip box must be a finite number not below nought.", nameof(margin));
            }

            Vec3 c = box.Center;
            return new ClipSet(new[]
            {
                new ClipPlane(new Vec3(box.Min.X, c.Y, c.Z), new Vec3(1.0, 0.0, 0.0), -margin),
                new ClipPlane(new Vec3(box.Max.X, c.Y, c.Z), new Vec3(-1.0, 0.0, 0.0), -margin),
                new ClipPlane(new Vec3(c.X, box.Min.Y, c.Z), new Vec3(0.0, 1.0, 0.0), -margin),
                new ClipPlane(new Vec3(c.X, box.Max.Y, c.Z), new Vec3(0.0, -1.0, 0.0), -margin),
                new ClipPlane(new Vec3(c.X, c.Y, box.Min.Z), new Vec3(0.0, 0.0, 1.0), -margin),
                new ClipPlane(new Vec3(c.X, c.Y, box.Max.Z), new Vec3(0.0, 0.0, -1.0), -margin),
            });
        }

        /// <summary>
        /// Gets whether a point is kept: on the kept side of every plane that is on, or on the plane, which is within the <see cref="Slack"/> of it.
        /// </summary>
        /// <param name="point">The point.</param>
        public bool Contains(Vec3 point)
        {
            for (int i = 0; i < _active.Length; i++)
            {
                if (_active[i].SignedDistance(point) < -_slack)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Cuts a segment to the part that is kept: a part no further outside a plane than the slack is kept whole, and a part beyond it is cut at the plane itself.
        /// </summary>
        /// <param name="a">One end.</param>
        /// <param name="b">The other end.</param>
        /// <param name="from">Where the part kept starts: <paramref name="a"/> itself where it is kept, to the bit.</param>
        /// <param name="to">Where it ends: <paramref name="b"/> itself where it is kept.</param>
        /// <returns>false when none of the segment is kept.</returns>
        public bool ClipSegment(Vec3 a, Vec3 b, out Vec3 from, out Vec3 to)
        {
            double t0 = 0.0;
            double t1 = 1.0;

            foreach (ClipPlane plane in _active)
            {
                double da = plane.SignedDistance(a);
                double db = plane.SignedDistance(b);
                bool inA = da >= -_slack;
                bool inB = db >= -_slack;

                if (!inA && !inB)
                {
                    from = a;
                    to = b;
                    return false;
                }

                if (!inA)
                {
                    t0 = Math.Max(t0, Crossing(da, db));
                }
                else if (!inB)
                {
                    t1 = Math.Min(t1, Crossing(da, db));
                }
            }

            if (t0 > t1)
            {
                from = a;
                to = b;
                return false;
            }

            Vec3 d = b - a;
            from = t0 == 0.0 ? a : a + d * t0;
            to = t1 == 1.0 ? b : a + d * t1;
            return true;
        }

        /// <summary>
        /// Cuts a convex polygon, a triangle, to the part that is kept.
        /// </summary>
        /// <param name="corners">The corners, in order.</param>
        /// <returns>The corners of what is kept, in the same order round; none where all is cut away.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the corners are null.</exception>
        public List<Vec3> ClipPolygon(IReadOnlyList<Vec3> corners)
        {
            if (corners == null)
            {
                throw new ArgumentNullException(nameof(corners));
            }

            var polygon = new List<Vec3>(corners);

            foreach (ClipPlane plane in _active)
            {
                polygon = CutBy(polygon, plane);

                if (polygon.Count == 0)
                {
                    break;
                }
            }

            return polygon;
        }

        /// <summary>
        /// Cuts a convex polygon to the part that is kept by every plane that is on but one, which is how a plane is drawn: the square laid on it, cut to
        /// what the other planes leave, so that the six planes of a box are its six faces.
        /// </summary>
        /// <param name="corners">The corners, in order.</param>
        /// <param name="plane">The number of the plane left out, in <see cref="Planes"/>.</param>
        /// <returns>The corners of what is kept; none where all is cut away.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the corners are null.</exception>
        public List<Vec3> ClipPolygonExcept(IReadOnlyList<Vec3> corners, int plane) => Without(plane).ClipPolygon(corners);

        /// <summary>
        /// Cuts what one item is drawn with: the points and labels outside are dropped, the lines are cut to the parts inside, and the triangles
        /// are cut and the pieces cut again into triangles that keep the normal of the triangle they came from. A triangle whose corners are all on a plane, within the
        /// slack, is kept whole.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>The item itself where no plane is on; otherwise a copy holding what is kept.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the item is null.</exception>
        public ItemPrimitives Apply(ItemPrimitives item) => Apply(item, CancellationToken.None);

        /// <summary>
        /// Cuts what one item is drawn with as <see cref="Apply(ItemPrimitives)"/> does, and stops when asked to, between thousands of triangles.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="cancel">Stops the work.</param>
        /// <exception cref="ArgumentNullException">Thrown when the item is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the work is cancelled.</exception>
        public ItemPrimitives Apply(ItemPrimitives item, CancellationToken cancel)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (!IsActive)
            {
                return item;
            }

            // A set made without a scene has no slack of its own, and takes the one the box of the item gives; a set made for a scene, as the window makes them, keeps its own.
            if (_slack == 0.0 && !item.Bounds.IsEmpty)
            {
                return ForScene(item.Bounds).Apply(item, cancel);
            }

            var kept = new ItemPrimitives { ItemIndex = item.ItemIndex, Bounds = item.Bounds, Warning = item.Warning };
            kept.LibraryLog.AddRange(item.LibraryLog);
            kept.Points.AddRange(item.Points.Where(p => Contains(p)));
            kept.Labels.AddRange(item.Labels.Where(l => Contains(l.At)));

            foreach (DrawPolyline line in item.Polylines)
            {
                ClipPolyline(line, kept.Polylines);
            }

            int count = 0;

            foreach (DrawTriangle t in item.Triangles)
            {
                if ((++count & 1023) == 0)
                {
                    cancel.ThrowIfCancellationRequested();
                }

                ClipTriangle(t, kept.Triangles);
            }

            return kept;
        }

        /// <summary>
        /// Cuts the whole scene as <see cref="Apply(ItemPrimitives)"/> cuts one item. The box round the scene stays what it was, so that the
        /// view does not move when a plane does.
        /// </summary>
        /// <param name="primitives">What the scene is drawn with.</param>
        /// <returns>The primitives themselves where no plane is on.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the primitives are null.</exception>
        public Primitives Apply(Primitives primitives)
        {
            if (primitives == null)
            {
                throw new ArgumentNullException(nameof(primitives));
            }

            if (!IsActive)
            {
                return primitives;
            }

            var cut = new Primitives { Bounds = primitives.Bounds };
            cut.Warnings.AddRange(primitives.Warnings);

            foreach (ItemPrimitives item in primitives.Items)
            {
                cut.Items.Add(Apply(item));
            }

            return cut;
        }

        /// <summary>
        /// Gets the planes that are on, leaving out one: what the cap lines of that plane are cut by.
        /// </summary>
        internal ClipSet Without(int index) => new ClipSet(_planes.Where((p, i) => i != index), _slack);

        /// <summary>
        /// Gets where along a segment, from 0 to 1, it crosses a plane, given the distances of its ends: the end that is inside by the slack and outside by a
        /// hair is at the end, not beyond it.
        /// </summary>
        private static double Crossing(double da, double db) => Math.Max(0.0, Math.Min(1.0, da / (da - db)));

        /// <summary>
        /// Cuts a line to the runs of it that are kept, a run being a chain of whole pieces with a cut piece at either end. A closed line that is cut
        /// where it begins and ends is one run across its start, where it is one piece.
        /// </summary>
        internal void ClipPolyline(DrawPolyline line, List<DrawPolyline> into)
        {
            Vec3[] points = line.Points;
            int n = points.Length;

            if (n == 1)
            {
                if (Contains(points[0]))
                {
                    into.Add(line);
                }

                return;
            }

            int segments = line.Closed ? n : n - 1;
            var runs = new List<List<Vec3>>();
            var run = new List<Vec3>();
            bool cut = false;
            bool firstStartKept = false;
            bool lastEndKept = false;

            void Flush()
            {
                if (run.Count > 1 && !(run.Count == 2 && run[0].Equals(run[1])))
                {
                    runs.Add(run);
                }

                run = new List<Vec3>();
            }

            for (int s = 0; s < segments; s++)
            {
                Vec3 a = points[s];
                Vec3 b = points[(s + 1) % n];

                if (!ClipSegment(a, b, out Vec3 from, out Vec3 to))
                {
                    cut = true;
                    lastEndKept = false;
                    Flush();
                    continue;
                }

                bool startKept = from.Equals(a);
                bool endKept = to.Equals(b);
                cut |= !startKept || !endKept;

                if (run.Count > 0 && startKept && run[run.Count - 1].Equals(a))
                {
                    run.Add(to);
                }
                else
                {
                    Flush();

                    if (s == 0)
                    {
                        firstStartKept = startKept;
                    }

                    run.Add(from);
                    run.Add(to);
                }

                lastEndKept = endKept;

                if (!endKept)
                {
                    Flush();
                }
            }

            if (!cut)
            {
                into.Add(line);
                return;
            }

            Flush();

            // The line began and ended in kept parts of one piece: the last run and the first are one run across the start of the loop.
            if (line.Closed && runs.Count > 1 && firstStartKept && lastEndKept && runs[0][0].Equals(points[0]) && runs[runs.Count - 1][runs[runs.Count - 1].Count - 1].Equals(points[0]))
            {
                List<Vec3> last = runs[runs.Count - 1];
                last.AddRange(runs[0].Skip(1));
                runs.RemoveAt(0);
            }

            foreach (List<Vec3> kept in runs)
            {
                into.Add(new DrawPolyline(kept.ToArray(), false));
            }
        }

        private void ClipTriangle(DrawTriangle t, List<DrawTriangle> into)
        {
            bool allKept = true;

            foreach (ClipPlane plane in _active)
            {
                double da = plane.SignedDistance(t.A);
                double db = plane.SignedDistance(t.B);
                double dc = plane.SignedDistance(t.C);
                bool inA = da >= -_slack;
                bool inB = db >= -_slack;
                bool inC = dc >= -_slack;

                if (!inA && !inB && !inC)
                {
                    return;
                }

                allKept &= inA && inB && inC;
            }

            if (allKept)
            {
                into.Add(t);
                return;
            }

            List<Vec3> piece = ClipPolygon(new[] { t.A, t.B, t.C });

            // The pieces are a convex fan; each triangle of it keeps the normal of the whole, so the light on a cut face does not change with the cut.
            for (int i = 1; i + 1 < piece.Count; i++)
            {
                into.Add(new DrawTriangle(piece[0], piece[i], piece[i + 1], t.Normal));
            }
        }

        private List<Vec3> CutBy(List<Vec3> corners, ClipPlane plane)
        {
            var kept = new List<Vec3>(corners.Count + 2);

            void Add(Vec3 p)
            {
                if (kept.Count == 0 || !kept[kept.Count - 1].Equals(p))
                {
                    kept.Add(p);
                }
            }

            for (int i = 0; i < corners.Count; i++)
            {
                Vec3 a = corners[i];
                Vec3 b = corners[(i + 1) % corners.Count];
                double da = plane.SignedDistance(a);
                double db = plane.SignedDistance(b);
                bool inA = da >= -_slack;
                bool inB = db >= -_slack;

                if (inA)
                {
                    Add(a);
                }

                if (inA != inB)
                {
                    double t = Crossing(da, db);
                    Add(t == 0.0 ? a : t == 1.0 ? b : a + (b - a) * t);
                }
            }

            if (kept.Count > 1 && kept[0].Equals(kept[kept.Count - 1]))
            {
                kept.RemoveAt(kept.Count - 1);
            }

            return kept;
        }
    }
}
