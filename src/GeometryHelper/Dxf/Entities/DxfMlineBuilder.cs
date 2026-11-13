using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Builds an MLINE as AutoCAD writes it, from the points it is drawn through, its style, scale and justification, and
    /// builds the record anew after an edit, as AutoCAD's <c>Mline</c> (<c>AppendSegment</c>, <c>MoveVertexAt</c>,
    /// <c>IsClosed</c>) does: its direction, miter and the offset of each element at every vertex worked out as AutoCAD works
    /// them out. A multiline is a <see cref="DxfUnknownEntity"/> whose <see cref="DxfUnknownEntity.DxfName"/> is MLINE.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A multiline of the style STANDARD (two elements 0.5 either side of the line) at scale 1, justified at its top, through
    /// (0, 0), (100, 0) and (100, 100), is written as AutoCAD writes it: the style's name (2) and handle (340), scale (40),
    /// justification (70: 0 top, 1 zero, 2 bottom), flags (71: 1, plus 2 closed, 4 start caps left off, 8 end caps left off),
    /// vertex count (72), element count (73), the first vertex (10) and the normal (210); then at each vertex its point (11),
    /// the direction of the segment it starts (12; the last vertex has that of the segment before it), its unit miter (13) and
    /// for each element the count (74) and parameters (41) and 75 of 0. At the corner (100, 0) the miter is (-0.7071, 0.7071),
    /// the half angle's cosine 0.7071, and the elements' first parameters 0 and -1.4142 (the offset of the element from the
    /// top, 0 and -1, divided by the cosine); the straight vertices give 0 and -1. At scale 20 and justified at its middle
    /// they are 10 and -10, and justified at its bottom 20 and 0.
    /// </para>
    /// <para>
    /// The miter of an inner vertex is the bisector of the normals of the two segments, that of an open start the direction
    /// turned by the style's start angle, that of an end the direction of its last segment turned by the end angle; the
    /// cosine in the division is the miter's against the normal of the segment, never less than 1E-4 in magnitude, as
    /// AutoCAD's. After the first parameter of an element come the stretches it is drawn along the segment, a start and a
    /// stop by turns, measured from where the element stands at the vertex: a start of nought and no stop is the whole
    /// segment, as a multiline is written, and the cuts of MLEDIT are the gaps between a stop and the next start. These are
    /// read from an existing multiline and written back with it, so that <see cref="Rebuild"/> after a point is moved leaves
    /// the cuts of the segments that stay where they were.
    /// </para>
    /// </remarks>
    public sealed class DxfMlineBuilder
    {
        // AutoCAD never divides an element's offset by a cosine of less than this.
        private const double LeastCosine = 1E-4;

        private readonly List<GeoPoint3> _points = new List<GeoPoint3>();

        // For each vertex and each element, the parameters after the first (the offset): the stretches of the segment the vertex starts.
        private readonly List<List<List<double>>> _tails = new List<List<List<double>>>();

        private DxfMlineStyle _style;

        // The style's elements and angles as they stood when it was taken, read once for all the vertices.
        private IReadOnlyList<DxfMlineStyleElement> _elements = Array.Empty<DxfMlineStyleElement>();
        private double _startAngle = Math.PI / 2.0;
        private double _endAngle = Math.PI / 2.0;
        private double _scale = 1.0;
        private GeoVector3 _normal = new GeoVector3(0.0, 0.0, 1.0);

        /// <summary>
        /// Initializes a builder of a multiline of a style, as AutoCAD's <c>new Mline()</c> with its <c>Style</c> set: scale 1,
        /// justified at its top, open, the normal (0, 0, 1), with no points yet.
        /// </summary>
        /// <param name="style">The style of a drawing, of at least one element.</param>
        /// <exception cref="ArgumentNullException">Thrown when the style is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the style is not of a drawing.</exception>
        public DxfMlineBuilder(DxfMlineStyle style)
        {
            Style = style;
        }

        /// <summary>
        /// Initializes a builder from a multiline of a drawing: its style, scale, justification, flags, normal and points, and
        /// the stretches of its elements, as its record holds them.
        /// </summary>
        /// <param name="mline">The MLINE, a <see cref="DxfUnknownEntity"/> of that type, in a drawing that has its style.</param>
        /// <exception cref="ArgumentNullException">Thrown when the MLINE is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the entity is not an MLINE.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MLINE's drawing has no MLINESTYLE of its handle or name,
        /// or a vertex holds another count of elements than the style, as <see cref="DxfUnknownEntity.Explode"/> refuses it.</exception>
        public DxfMlineBuilder(DxfUnknownEntity mline)
        {
            if (mline == null)
            {
                throw new ArgumentNullException(nameof(mline));
            }

            Guard.Argument(mline.DxfName == "MLINE", nameof(mline), "The entity is not an MLINE.");
            DxfMline record = DxfMline.Read(mline.MlineGroups());
            Take(mline.FindMlineStyle(record));
            int count = _elements.Count;
            IReadOnlyList<DxfTypedValue> groups = mline.MlineGroups();
            _scale = Number(DxfFollowers.Text(groups, 40), 1.0);
            int justification = DxfFollowers.Whole(groups, 70, 0);
            Justification =
                justification == 1 ? DxfMlineJustification.Zero
                : justification == 2 ? DxfMlineJustification.Bottom
                : DxfMlineJustification.Top;
            IsClosed = (record.Flags & 2) != 0;
            SuppressStartCaps = (record.Flags & 4) != 0;
            SuppressEndCaps = (record.Flags & 8) != 0;
            _normal = mline.MlineNormal();

            foreach (DxfMline.Vertex vertex in record.Vertices)
            {
                if (vertex.Parameters.Count != count)
                {
                    throw new InvalidOperationException(
                        "The MLINE "
                            + (mline.Handle ?? string.Empty)
                            + " holds "
                            + vertex.Parameters.Count.ToString(CultureInfo.InvariantCulture)
                            + " elements at a vertex, and its style "
                            + _style.Name
                            + " "
                            + count.ToString(CultureInfo.InvariantCulture)
                            + "."
                    );
                }

                _points.Add(new GeoPoint3(vertex.At[0], vertex.At[1], vertex.At[2]));
                var tails = new List<List<double>>();

                foreach (List<double> parameters in vertex.Parameters)
                {
                    tails.Add(parameters.GetRange(Math.Min(1, parameters.Count), Math.Max(0, parameters.Count - 1)));
                }

                _tails.Add(tails);
            }
        }

        /// <summary>
        /// Gets or sets the style the multiline is drawn by, as AutoCAD's <c>Style</c>: its name and handle are written into the
        /// record, its elements and angles decide the offsets.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the style set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the style set is not one of a drawing, or has no element.</exception>
        public DxfMlineStyle Style
        {
            get => _style;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                Guard.Argument(
                    value.Handle != null && value.Elements.Count > 0,
                    nameof(value),
                    "The style of a multiline is one of a drawing, with at least one element."
                );

                if (_style != null && value.Elements.Count != _elements.Count)
                {
                    _tails.Clear();
                }

                Take(value);
            }
        }

        /// <summary>
        /// Gets or sets the scale (group 40), as AutoCAD's <c>Scale</c>: the offsets of the style are multiplied by it, so that
        /// STANDARD, 1 wide, is 20 wide at 20. Nought and a negative scale are taken, as AutoCAD takes them (the elements stand
        /// on the other side with a negative one).
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the scale set is not a finite number.</exception>
        public double Scale
        {
            get => _scale;
            set
            {
                Guard.Argument(Guard.IsFinite(value), nameof(value), "The scale of a multiline is a finite number.");
                _scale = value;
            }
        }

        /// <summary>Gets or sets which line of the multiline stands on the points (group 70), as AutoCAD's <c>Justification</c>; top for a new one.</summary>
        public DxfMlineJustification Justification { get; set; }

        /// <summary>Gets or sets whether the multiline closes on its first point (flag 2 of group 71), as AutoCAD's <c>IsClosed</c>; it takes three points or more.</summary>
        public bool IsClosed { get; set; }

        /// <summary>Gets or sets whether the caps of the style are left off the start (flag 4), as AutoCAD's <c>SupressStartCaps</c>.</summary>
        public bool SuppressStartCaps { get; set; }

        /// <summary>Gets or sets whether the caps of the style are left off the end (flag 8), as AutoCAD's <c>SupressEndCaps</c>.</summary>
        public bool SuppressEndCaps { get; set; }

        /// <summary>
        /// Gets or sets the normal of the plane the multiline lies in (210, 220, 230), as AutoCAD's <c>Normal</c>; (0, 0, 1) for a
        /// new one. The elements stand to the left of a segment, which is the normal crossed with the segment's direction.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the normal set has no length or is not of finite numbers.</exception>
        public GeoVector3 Normal
        {
            get => _normal;
            set
            {
                double length = value.Length;
                Guard.Argument(
                    value.IsValid && length > 0.0 && !double.IsInfinity(length),
                    nameof(value),
                    "The normal of a multiline has to be a direction of finite numbers and some length: "
                        + value
                        + " is not."
                );
                _normal = value;
            }
        }

        /// <summary>
        /// Gets the points the multiline passes through, in order, as AutoCAD's <c>AppendSegment</c> adds them and
        /// <c>MoveVertexAt</c> moves them; edit the list, then <see cref="Build"/> or <see cref="Rebuild"/>. The stretches of
        /// the elements read from an MLINE stay with the points while their number is the same.
        /// </summary>
        public IList<GeoPoint3> Points => _points;

        /// <summary>
        /// Moves the point at an index, as AutoCAD's <c>MoveVertexAt</c> moves it (a grip dragged, a vertex stretched): a
        /// segment that keeps its way keeps the cuts of its elements where they were along it, and one that turns is drawn
        /// whole again. A multiline through (0, 0), (100, 0) and (100, 100) at scale 20, its second segment cut from 30 to 60,
        /// whose vertex (100, 0) goes to (100, -10), keeps that cut at 40 to 70 from the moved vertex, where it was; its first
        /// segment, which turns, is whole. A stretch that starts at the vertex stays there.
        /// </summary>
        /// <param name="index">The index of the point, from nought.</param>
        /// <param name="point">Where it goes.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not one of a point.</exception>
        public void MoveVertexAt(int index, GeoPoint3 point)
        {
            if (index < 0 || index >= _points.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    "The multiline has "
                        + _points.Count.ToString(CultureInfo.InvariantCulture)
                        + " points: "
                        + index.ToString(CultureInfo.InvariantCulture)
                        + " is not the index of one."
                );
            }

            MoveVertices(new Dictionary<int, GeoPoint3> { [index] = point });
        }

        /// <summary>
        /// Moves some points at once, as STRETCH moves the vertices inside its window: each segment that keeps its way keeps its
        /// cuts where they were along it, a stretch from the element's start staying at the start, and one that turns is whole.
        /// </summary>
        internal void MoveVertices(IReadOnlyDictionary<int, GeoPoint3> moves)
        {
            List<List<List<double>>> kept = Kept();
            int n = _points.Count;
            int segments = SegmentCount;
            int count = ElementCount;

            // The ends of an open multiline hold the stretches past its last point, along its last segment.
            int places = IsClosed ? segments : segments + 1;
            var ways = new GeoVector3[places];
            var starts = new GeoPoint3[places][];

            for (int i = 0; i < places; i++)
            {
                int segment = Math.Min(i, segments - 1);
                ways[i] = SegmentDirection(segment);
                starts[i] = new GeoPoint3[count];

                for (int e = 0; e < count; e++)
                {
                    starts[i][e] = ElementPoint(i, e);
                }
            }

            foreach (KeyValuePair<int, GeoPoint3> move in moves)
            {
                _points[move.Key] = move.Value;
            }

            for (int i = 0; i < places; i++)
            {
                int segment = Math.Min(i, segments - 1);
                GeoPoint3 from = _points[segment];
                GeoPoint3 to = _points[(segment + 1) % n];

                // A segment of no length has no way: its tails are left for Check to refuse the points.
                if (from.DistanceTo(to) <= 0.0)
                {
                    continue;
                }

                GeoVector3 way = SegmentDirection(segment);

                double turned = Math.Sqrt(
                    (way.X - ways[i].X) * (way.X - ways[i].X)
                        + (way.Y - ways[i].Y) * (way.Y - ways[i].Y)
                        + (way.Z - ways[i].Z) * (way.Z - ways[i].Z)
                );

                if (turned > Tolerance.Default.EqualVector)
                {
                    kept[i] = Whole(count);
                    continue;
                }

                for (int e = 0; e < count; e++)
                {
                    GeoPoint3 now = ElementPoint(i, e);
                    double shift =
                        (now.X - starts[i][e].X) * way.X
                        + (now.Y - starts[i][e].Y) * way.Y
                        + (now.Z - starts[i][e].Z) * way.Z;
                    List<double> tail = kept[i][e];

                    for (int k = 0; k < tail.Count; k++)
                    {
                        // A stretch that starts at the element's start, or before it, stays where it starts.
                        if (k == 0 && tail[0] <= 0.0)
                        {
                            continue;
                        }

                        tail[k] -= shift;
                    }

                    if (i < segments)
                    {
                        SetRuns(i, e, RunsOf(i, e));
                    }
                }
            }
        }

        /// <summary>
        /// Writes a new MLINE of the style, scale, justification, flags and points, as AutoCAD writes one, not yet in a
        /// drawing: <c>AppendEntity</c> it to a block. Its layer, colour and linetype are the entity's own, given as for any
        /// entity.
        /// </summary>
        /// <returns>The entity.</returns>
        /// <exception cref="ArgumentException">Thrown when there are fewer than two points (three for a closed multiline), two
        /// in a row are within the point tolerance of <see cref="Tolerance.Default"/>, or one is not of finite numbers.</exception>
        public DxfUnknownEntity Build() =>
            new DxfUnknownEntity(
                DxfFile.Detached("MLINE", Groups(false).ConvertAll(g => new DxfTypedValue(g.Key, g.Value)))
            );

        /// <summary>
        /// Writes the record of an MLINE anew from the points, style, scale, justification and flags, as AutoCAD rewrites it
        /// when a vertex is moved: its head and every vertex, the layer, colour, linetype and extended data kept.
        /// </summary>
        /// <param name="mline">The MLINE to rewrite, in a drawing or not.</param>
        /// <exception cref="ArgumentNullException">Thrown when the MLINE is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the entity is not an MLINE, or the points are not ones <see cref="Build"/> takes.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MLINE is erased.</exception>
        public void Rebuild(DxfUnknownEntity mline)
        {
            if (mline == null)
            {
                throw new ArgumentNullException(nameof(mline));
            }

            Guard.Argument(mline.DxfName == "MLINE", nameof(mline), "The entity is not an MLINE.");
            mline.ReplaceMline(Groups(true));
        }

        /// <summary>The groups of the record after its marker, in AutoCAD's order; the marker first for a new entity.</summary>
        private List<KeyValuePair<int, string>> Groups(bool withoutMarker)
        {
            Check();
            IReadOnlyList<DxfMlineStyleElement> elements = _elements;
            int count = elements.Count;
            int n = _points.Count;
            double startAngle = _startAngle;
            double endAngle = _endAngle;
            var groups = new List<KeyValuePair<int, string>>();

            if (!withoutMarker)
            {
                groups.Add(new KeyValuePair<int, string>(100, "AcDbMline"));
            }

            int flags = 1 | (IsClosed ? 2 : 0) | (SuppressStartCaps ? 4 : 0) | (SuppressEndCaps ? 8 : 0);
            groups.Add(new KeyValuePair<int, string>(2, _style.Name));
            groups.Add(new KeyValuePair<int, string>(340, _style.Handle));
            groups.Add(new KeyValuePair<int, string>(40, Numbers.Exact(_scale + 0.0)));
            groups.Add(Whole(70, (int)Justification));
            groups.Add(Whole(71, flags));
            groups.Add(Whole(72, n));
            groups.Add(Whole(73, count));
            AddPoint(groups, 10, _points[0].X, _points[0].Y, _points[0].Z);
            double length = _normal.Length;
            AddPoint(groups, 210, _normal.X / length, _normal.Y / length, _normal.Z / length);

            for (int i = 0; i < n; i++)
            {
                Vertex(i, startAngle, endAngle, out double[] along, out double[] miter, out double cosine);
                AddPoint(groups, 11, _points[i].X, _points[i].Y, _points[i].Z);
                AddPoint(groups, 12, along[0], along[1], along[2]);
                AddPoint(groups, 13, miter[0], miter[1], miter[2]);
                List<List<double>> tails = Kept()[i];

                for (int e = 0; e < count; e++)
                {
                    List<double> tail = tails[e];
                    groups.Add(Whole(74, 1 + tail.Count));
                    groups.Add(new KeyValuePair<int, string>(41, Numbers.Exact(Offset(e, elements) / cosine + 0.0)));

                    foreach (double stretch in tail)
                    {
                        groups.Add(new KeyValuePair<int, string>(41, Numbers.Exact(stretch + 0.0)));
                    }

                    groups.Add(Whole(75, 0));
                }
            }

            return groups;
        }

        private static KeyValuePair<int, string> Whole(int code, int value) =>
            new KeyValuePair<int, string>(code, value.ToString(CultureInfo.InvariantCulture));

        // A point's three groups: the code, then 10 and 20 more.
        private static void AddPoint(List<KeyValuePair<int, string>> groups, int code, double x, double y, double z)
        {
            groups.Add(new KeyValuePair<int, string>(code, Numbers.Exact(x + 0.0)));
            groups.Add(new KeyValuePair<int, string>(code + 10, Numbers.Exact(y + 0.0)));
            groups.Add(new KeyValuePair<int, string>(code + 20, Numbers.Exact(z + 0.0)));
        }

        private static double Number(string text, double fallback) =>
            Numbers.TryRead(text, out double value) && Guard.IsFinite(value) ? value : fallback;

        /// <summary>Takes a style and reads its elements and angles once.</summary>
        private void Take(DxfMlineStyle style)
        {
            _style = style;
            _elements = style.Elements;
            _startAngle = style.StartAngle;
            _endAngle = style.EndAngle;
        }

        /// <summary>Refuses points AutoCAD would draw nothing through.</summary>
        private void Check()
        {
            int n = _points.Count;
            Guard.Argument(
                n >= (IsClosed ? 3 : 2),
                nameof(Points),
                "A multiline passes through two points or more, a closed one through three: "
                    + n.ToString(CultureInfo.InvariantCulture)
                    + " were given."
            );
            double tolerance = Tolerance.Default.EqualPoint;

            for (int i = 0; i < n; i++)
            {
                GeoPoint3 point = _points[i];
                Guard.Argument(
                    Guard.IsFinite(point.X) && Guard.IsFinite(point.Y) && Guard.IsFinite(point.Z),
                    nameof(Points),
                    "A point of a multiline has to be of finite numbers: " + point + " is not."
                );

                if (i + 1 < n || IsClosed)
                {
                    GeoPoint3 next = _points[(i + 1) % n];
                    Guard.Argument(
                        point.DistanceTo(next) > tolerance,
                        nameof(Points),
                        "Two points of a multiline in a row are apart by more than the point tolerance: "
                            + point
                            + " and "
                            + next
                            + " are not."
                    );
                }
            }
        }

        /// <summary>The offset of an element from the line the points stand on: its offset less that of the justified line, times the scale.</summary>
        private double Offset(int element, IReadOnlyList<DxfMlineStyleElement> elements)
        {
            double least = double.PositiveInfinity;
            double most = double.NegativeInfinity;

            foreach (DxfMlineStyleElement each in elements)
            {
                least = Math.Min(least, each.Offset);
                most = Math.Max(most, each.Offset);
            }

            double line =
                Justification == DxfMlineJustification.Top ? most
                : Justification == DxfMlineJustification.Bottom ? least
                : 0.0;
            return (elements[element].Offset - line) * _scale;
        }

        /// <summary>The unit direction of a segment, from the point at its index to the next.</summary>
        private double[] Along(int segment)
        {
            GeoPoint3 from = _points[segment];
            GeoPoint3 to = _points[(segment + 1) % _points.Count];
            double dx = to.X - from.X;
            double dy = to.Y - from.Y;
            double dz = to.Z - from.Z;
            double length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            return new[] { dx / length, dy / length, dz / length };
        }

        /// <summary>The unit normal of a segment, to its left: the normal of the plane crossed with the direction.</summary>
        private double[] Left(double[] along)
        {
            double length = _normal.Length;
            double nx = _normal.X / length;
            double ny = _normal.Y / length;
            double nz = _normal.Z / length;
            double x = ny * along[2] - nz * along[1];
            double y = nz * along[0] - nx * along[2];
            double z = nx * along[1] - ny * along[0];
            double size = Math.Sqrt(x * x + y * y + z * z);
            return size > 0.0 ? new[] { x / size, y / size, z / size } : new[] { x, y, z };
        }

        /// <summary>
        /// The direction, the unit miter and the cosine the offsets are divided by at a vertex, as AutoCAD works them out: the
        /// bisector of the two normals at an inner vertex, the direction turned by the style's angle at the start and end of an
        /// open multiline.
        /// </summary>
        private void Vertex(
            int i,
            double startAngle,
            double endAngle,
            out double[] along,
            out double[] miter,
            out double cosine
        )
        {
            int n = _points.Count;
            bool first = !IsClosed && i == 0;
            bool last = !IsClosed && i == n - 1;
            double[] before = last || first ? null : Along((i + n - 1) % n);
            double[] after = last ? null : Along(i);
            along = last ? Along(i - 1) : after;
            double[] side = Left(along);

            if (first || last)
            {
                double angle = first ? startAngle : endAngle;
                double c = Math.Cos(angle);
                double s = Math.Sin(angle);
                miter = new[] { along[0] * c + side[0] * s, along[1] * c + side[1] * s, along[2] * c + side[2] * s };
            }
            else
            {
                double[] one = Left(before);
                double[] two = Left(after);
                double x = one[0] + two[0];
                double y = one[1] + two[1];
                double z = one[2] + two[2];
                double size = Math.Sqrt(x * x + y * y + z * z);
                miter = size > 0.0 ? new[] { x / size, y / size, z / size } : two;
            }

            double[] across = last || first ? side : Left(before);
            cosine = miter[0] * across[0] + miter[1] * across[1] + miter[2] * across[2];

            if (Math.Abs(cosine) < LeastCosine)
            {
                cosine = cosine < 0.0 ? -LeastCosine : LeastCosine;
            }
        }

        /// <summary>A stretch of an element along a segment: from where it starts to where it stops, measured from where the element stands at the vertex the segment starts; the stop is infinity for one that runs to the end.</summary>
        internal readonly struct Run
        {
            internal Run(double from, double to)
            {
                From = from;
                To = to;
            }

            /// <summary>Where the stretch starts; below nought only for the first stretch of the first segment of an open multiline, which runs back past the first vertex.</summary>
            internal double From { get; }

            /// <summary>Where the stretch stops; infinity where it runs to the end of the segment.</summary>
            internal double To { get; }
        }

        /// <summary>Multiplies every stretch of every element by a factor, as a multiline's cuts follow it when it is scaled.</summary>
        internal void ScaleStretches(double factor)
        {
            foreach (List<List<double>> vertex in Kept())
            {
                foreach (List<double> tail in vertex)
                {
                    for (int i = 0; i < tail.Count; i++)
                    {
                        tail[i] *= factor;
                    }
                }
            }
        }

        /// <summary>The number of elements of the style.</summary>
        internal int ElementCount => _elements.Count;

        /// <summary>The number of segments: one fewer than the points, as many as the points where the multiline is closed.</summary>
        internal int SegmentCount => IsClosed ? _points.Count : Math.Max(0, _points.Count - 1);

        /// <summary>
        /// How far an element stands from the line the points are on, to the left of the direction of the multiline: its offset
        /// less that of the justified line, times the scale, before the division by a miter's cosine.
        /// </summary>
        internal double ElementShift(int element) => Offset(element, _elements);

        /// <summary>The unit direction of a segment, from its first point to its second.</summary>
        internal GeoVector3 SegmentDirection(int segment)
        {
            double[] along = Along(segment);
            return new GeoVector3(along[0], along[1], along[2]);
        }

        /// <summary>The unit normal to the left of a segment: the plane's normal crossed with the segment's direction.</summary>
        internal GeoVector3 SegmentLeft(int segment)
        {
            double[] left = Left(Along(segment));
            return new GeoVector3(left[0], left[1], left[2]);
        }

        /// <summary>Where an element stands at a vertex: the vertex moved along its miter by the element's first parameter there.</summary>
        internal GeoPoint3 ElementPoint(int vertex, int element)
        {
            Vertex(vertex, _startAngle, _endAngle, out double[] _, out double[] miter, out double cosine);
            double length = ElementShift(element) / cosine;
            GeoPoint3 at = _points[vertex];
            return new GeoPoint3(at.X + miter[0] * length, at.Y + miter[1] * length, at.Z + miter[2] * length);
        }

        /// <summary>The stretches an element is drawn in along a segment, in order; none where the element is cut away whole.</summary>
        internal List<Run> RunsOf(int segment, int element)
        {
            List<double> tail = Kept()[segment][element];
            var runs = new List<Run>();

            for (int i = 0; i < tail.Count; i += 2)
            {
                runs.Add(new Run(tail[i], i + 1 < tail.Count ? tail[i + 1] : double.PositiveInfinity));
            }

            return runs;
        }

        /// <summary>
        /// Sets the stretches an element is drawn in along a segment: sorted, those that touch or overlap made one, an empty one
        /// left out; written as a start and a stop by turns, the last stop left off where it runs to the end.
        /// </summary>
        internal void SetRuns(int segment, int element, IEnumerable<Run> runs)
        {
            var sorted = new List<Run>();

            foreach (Run run in runs)
            {
                if (run.To > run.From)
                {
                    sorted.Add(run);
                }
            }

            sorted.Sort(
                (a, b) =>
                {
                    int by = a.From.CompareTo(b.From);
                    return by != 0 ? by : a.To.CompareTo(b.To);
                }
            );
            var tail = new List<double>();
            double from = 0.0;
            double to = 0.0;
            bool open = false;

            foreach (Run run in sorted)
            {
                if (open && run.From <= to)
                {
                    to = Math.Max(to, run.To);
                    continue;
                }

                if (open)
                {
                    Write(tail, from, to);
                }

                from = run.From;
                to = run.To;
                open = true;
            }

            if (open)
            {
                Write(tail, from, to);
            }

            Kept()[segment][element] = tail;
        }

        private static void Write(List<double> tail, double from, double to)
        {
            tail.Add(from);

            if (!double.IsPositiveInfinity(to))
            {
                tail.Add(to);
            }
        }

        /// <summary>How far an element runs past the last vertex of an open multiline, where a corner joint or a tee left it; nought for none.</summary>
        internal double EndExtension(int element)
        {
            List<double> tail = Kept()[_points.Count - 1][element];
            return tail.Count >= 2 ? Math.Max(0.0, tail[1]) : 0.0;
        }

        /// <summary>Sets how far an element runs past the last vertex of an open multiline, as AutoCAD writes it after a corner joint or a tee: nothing for none, a start of nought and the stop for some.</summary>
        internal void SetEndExtension(int element, double extension) =>
            Kept()[_points.Count - 1][element] =
                extension > 0.0 ? new List<double> { 0.0, extension } : new List<double>();

        /// <summary>
        /// Puts a point into the multiline before the point at an index (after the last one, as the multiline's closing
        /// segment, at the count of points), dividing the stretches of the segment it falls on at the point, so that a cut stays
        /// where it was.
        /// </summary>
        internal void InsertVertex(int index, GeoPoint3 point)
        {
            List<List<List<double>>> kept = Kept();
            int segment = index - 1;
            int count = ElementCount;
            var start = new GeoPoint3[count];

            for (int e = 0; e < count; e++)
            {
                start[e] = ElementPoint(segment, e);
            }

            double[] along = Along(segment);
            var halves = new List<List<Run>[]>();

            for (int e = 0; e < count; e++)
            {
                halves.Add(new List<Run>[] { RunsOf(segment, e), null });
            }

            _points.Insert(index, point);
            kept.Insert(index, Whole(count));

            for (int e = 0; e < count; e++)
            {
                GeoPoint3 there = ElementPoint(index, e);
                double cut =
                    (there.X - start[e].X) * along[0]
                    + (there.Y - start[e].Y) * along[1]
                    + (there.Z - start[e].Z) * along[2];
                var before = new List<Run>();
                var after = new List<Run>();

                foreach (Run run in halves[e][0])
                {
                    if (run.From < cut)
                    {
                        before.Add(new Run(run.From, Math.Min(run.To, cut)));
                    }

                    if (run.To > cut)
                    {
                        after.Add(new Run(Math.Max(run.From, cut) - cut, run.To - cut));
                    }
                }

                SetRuns(segment, e, before);
                SetRuns(index, e, after);
            }
        }

        /// <summary>
        /// Keeps the multiline from its start to a segment and ends it at a point, where MLEDIT's tee or corner joint trims or
        /// extends the end that is picked: the points after the segment are given up, the point put last, its elements stopping
        /// where the caller says (<see cref="SetEndExtension"/>, <see cref="SetRuns"/>), the end caps left off.
        /// </summary>
        internal void KeepStart(int segment, GeoPoint3 end)
        {
            List<List<List<double>>> kept = Kept();
            int count = ElementCount;

            while (_points.Count > segment + 1)
            {
                _points.RemoveAt(_points.Count - 1);
                kept.RemoveAt(kept.Count - 1);
            }

            _points.Add(end);
            var ends = new List<List<double>>();

            for (int e = 0; e < count; e++)
            {
                ends.Add(new List<double>());
            }

            kept.Add(ends);
            IsClosed = false;
            SuppressEndCaps = true;
        }

        /// <summary>
        /// Keeps the multiline from a segment to its end and starts it at a point, as <see cref="KeepStart"/> ends it: the points
        /// before the segment are given up, the point put first, and the stretches of the segment kept where they were along
        /// the elements; the start caps left off.
        /// </summary>
        internal void KeepEnd(int segment, GeoPoint3 start)
        {
            List<List<List<double>>> kept = Kept();
            int count = ElementCount;
            double[] along = Along(segment);
            var before = new GeoPoint3[count];
            var runs = new List<List<Run>>();

            for (int e = 0; e < count; e++)
            {
                before[e] = ElementPoint(segment, e);
                runs.Add(RunsOf(segment, e));
            }

            _points.RemoveRange(0, segment);
            _points[0] = start;
            kept.RemoveRange(0, segment);
            kept[0] = Whole(count);

            for (int e = 0; e < count; e++)
            {
                GeoPoint3 now = ElementPoint(0, e);
                double shift =
                    (now.X - before[e].X) * along[0]
                    + (now.Y - before[e].Y) * along[1]
                    + (now.Z - before[e].Z) * along[2];
                List<Run> moved = runs[e].ConvertAll(r => new Run(r.From - shift, r.To - shift));

                // What the element did not cover before its first vertex is open to run on over, as a tee runs a stub back to the line.
                if (moved.Count > 0 && runs[e][0].From <= 0.0)
                {
                    moved[0] = new Run(double.NegativeInfinity, moved[0].To);
                }

                SetRuns(0, e, moved);
            }

            IsClosed = false;
            SuppressStartCaps = true;
        }

        /// <summary>The entities an MLINE built here breaks into, drawn by the style of this builder, for a preview of one in no drawing.</summary>
        internal IReadOnlyList<DxfEntity> Explode(DxfUnknownEntity built) =>
            built.ExplodeMline(DxfMline.Style.Read(_style.Item.File.ObjectGroupsOf(_style.Item)));

        /// <summary>Takes out the point at an index; the stretches of the two segments it joined are given up, the whole of the one that now runs from the point before it to the point after it.</summary>
        internal void RemoveVertex(int index)
        {
            List<List<List<double>>> kept = Kept();
            _points.RemoveAt(index);
            kept.RemoveAt(index);
            int before = (index + _points.Count - 1) % _points.Count;

            if (!IsClosed && index == 0)
            {
                return;
            }

            if (!IsClosed && index > _points.Count - 1)
            {
                return;
            }

            for (int e = 0; e < ElementCount; e++)
            {
                kept[before][e] = new List<double> { 0.0 };
            }
        }

        /// <summary>The stretches of every vertex, made whole for the points and elements there are now: those kept while the count of points and elements is the same, else every segment whole.</summary>
        private List<List<List<double>>> Kept()
        {
            int count = ElementCount;
            bool fits = _tails.Count == _points.Count;

            for (int i = 0; fits && i < _tails.Count; i++)
            {
                fits = _tails[i].Count == count;
            }

            if (!fits)
            {
                _tails.Clear();

                for (int i = 0; i < _points.Count; i++)
                {
                    _tails.Add(Whole(count));
                }
            }

            return _tails;
        }

        private static List<List<double>> Whole(int count)
        {
            var whole = new List<List<double>>();

            for (int e = 0; e < count; e++)
            {
                whole.Add(new List<double> { 0.0 });
            }

            return whole;
        }
    }
}
