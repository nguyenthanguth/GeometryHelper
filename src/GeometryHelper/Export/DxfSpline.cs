using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A SPLINE of a DXF file, as AutoCAD's <c>Spline</c>: a NURBS curve of its <see cref="Degree"/>, control points, knots and
    /// weights, in the world, or given by the points it passes through alone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The control points are groups 10, 20 and 30, the knots 40 and the weights 41, each counted in groups 73, 72 and 41's
    /// count; the fit points 11, 21 and 31, counted in 74. The parameter is AutoCAD's, the knot value: a cubic of five control
    /// points with the knots 0, 0, 0, 0, 1, 2, 2, 2, 2 runs from <see cref="DxfCurve.StartParam"/> 0 to
    /// <see cref="DxfCurve.EndParam"/> 2, and a closed periodic spline from its knot of index degree to that of index count of
    /// its points. A spline given by its fit points alone is walked on the spline <see cref="DxfReader"/> passes through them,
    /// whose parameters are its own.
    /// </para>
    /// <para>
    /// A file of R12 has no SPLINE: <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/> refuses one there.
    /// </para>
    /// </remarks>
    public sealed class DxfSpline : DxfCurve
    {
        // The groups of AcDbSpline in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 210, 220, 230, 70, 71, 72, 73, 74, 42, 43, 44, 12, 22, 32, 13, 23, 33, 40, 41, 10, 20, 30, 11, 21, 31 });

        private static readonly IReadOnlyCollection<int> ControlCodes = Array.AsReadOnly(new[] { 10, 20, 30 });

        private static readonly IReadOnlyCollection<int> FitCodes = Array.AsReadOnly(new[] { 11, 21, 31 });

        private static readonly IReadOnlyCollection<int> KnotCodes = Array.AsReadOnly(new[] { 40 });

        private static readonly IReadOnlyCollection<int> WeightCodes = Array.AsReadOnly(new[] { 41 });

        private static readonly GeoVector3 None = new GeoVector3(0.0, 0.0, 0.0);

        /// <summary>
        /// Initializes a spline of degree 3 with no points, as AutoCAD's <c>new Spline()</c>, on layer 0 in no file; give it its
        /// control points, knots and weights.
        /// </summary>
        public DxfSpline()
            : base("SPLINE", Head(3, 0, 1E-10, 1E-10))
        {
        }

        /// <summary>
        /// Initializes a spline of its control points, as AutoCAD's <c>new Spline(degree, rational, closed, periodic,
        /// controlPoints, knots, weights, controlPointTolerance, knotTolerance)</c>, on layer 0 in no file until it is added to one.
        /// </summary>
        /// <param name="degree">The degree, 1 or more.</param>
        /// <param name="rational">Whether the weights are written (flag 4 of group 70).</param>
        /// <param name="closed">Whether it is closed (flag 1).</param>
        /// <param name="periodic">Whether it is periodic (flag 2).</param>
        /// <param name="controlPoints">The control points, in the world.</param>
        /// <param name="knots">The knots, as many as the points and the degree and one.</param>
        /// <param name="weights">The weights, one to each point where the spline is rational; empty or null otherwise.</param>
        /// <param name="controlPointTolerance">The tolerance of the control points (group 43).</param>
        /// <param name="knotTolerance">The tolerance of the knots (group 42).</param>
        /// <exception cref="ArgumentNullException">Thrown when the points or the knots are null.</exception>
        /// <exception cref="ArgumentException">Thrown when a point is not finite, or the counts do not make a spline.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the degree is below 1, or a number is not finite.</exception>
        public DxfSpline(int degree, bool rational, bool closed, bool periodic, IEnumerable<GeoPoint3> controlPoints, IEnumerable<double> knots, IEnumerable<double> weights, double controlPointTolerance, double knotTolerance)
            : base("SPLINE", Head(degree, (closed ? 1 : 0) | (periodic ? 2 : 0) | (rational ? 4 : 0), knotTolerance, controlPointTolerance))
        {
            if (controlPoints == null)
            {
                throw new ArgumentNullException(nameof(controlPoints));
            }

            if (knots == null)
            {
                throw new ArgumentNullException(nameof(knots));
            }

            SetNurbsData(degree, rational, closed, periodic, controlPoints, knots, weights);
        }

        private DxfSpline(DxfItem item)
            : base(item)
        {
        }

        #region The spline

        /// <summary>Gets or sets the degree (group 71), as AutoCAD's <c>Degree</c>; 3 where the record gives none.</summary>
        /// <value>
        /// Set, 1 or more. A spline of control points is given, with it, a clamped uniform knot vector for the same points over
        /// the span of knots it ran over, as AutoCAD's change of degree gives one, and is no longer periodic (flag 2 of group 70
        /// taken off): a cubic of 5 points made of degree 4 has the knots 0, 0, 0, 0, 0, 1, 1, 1, 1, 1 over its span from 0 to
        /// 1. Its points and weights are kept, so the curve changes with the degree. A spline given by its fit points alone has
        /// its group 71 alone set.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the degree set is below 1.</exception>
        /// <exception cref="ArgumentException">Thrown when the spline has fewer control points than the degree set and one.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a spline that is erased.</exception>
        public int Degree
        {
            get => GetInt(71, 3);

            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A spline is of degree 1 or more.");
                }

                if (value == Degree)
                {
                    return;
                }

                int count = NumControlPoints;

                if (count == 0)
                {
                    SetInt(71, value, 3);
                    return;
                }

                if (count < value + 1)
                {
                    throw new ArgumentException("A spline of degree " + value.ToString(CultureInfo.InvariantCulture) + " has " + (value + 1).ToString(CultureInfo.InvariantCulture) + " control points at least, and this one has " + count.ToString(CultureInfo.InvariantCulture) + ".", nameof(value));
                }

                double start = 0.0;
                double end = 1.0;
                IReadOnlyList<double> knots = Knots;
                int degree = Degree;

                if (knots.Count == count + degree + 1 && knots[count] > knots[degree])
                {
                    start = knots[degree];
                    end = knots[count];
                }

                var uniform = new List<double>(count + value + 1);

                for (int i = 0; i <= value; i++)
                {
                    uniform.Add(start);
                }

                for (int i = 1; i < count - value; i++)
                {
                    uniform.Add(start + (end - start) * i / (count - value));
                }

                for (int i = 0; i <= value; i++)
                {
                    uniform.Add(end);
                }

                int flags = GetInt(70, 0);
                var head = new List<KeyValuePair<int, string>> { new KeyValuePair<int, string>(71, value.ToString(CultureInfo.InvariantCulture)) };

                if ((flags & 2) != 0)
                {
                    head.Add(new KeyValuePair<int, string>(70, (flags & ~2).ToString(CultureInfo.InvariantCulture)));
                }

                SetGroups(head.ToArray());
                WriteNumbers(40, KnotCodes, uniform, 72);
            }
        }

        /// <summary>Gets how many control points the spline has, as AutoCAD's <c>NumControlPoints</c>: its groups 10.</summary>
        public int NumControlPoints => Points(10).Count;

        /// <summary>Gets how many fit points the spline has, as AutoCAD's <c>NumFitPoints</c>: its groups 11.</summary>
        public int NumFitPoints => Points(11).Count;

        /// <summary>Gets whether the spline has fit points, as AutoCAD's <c>HasFitData</c>.</summary>
        public bool HasFitData => NumFitPoints > 0;

        /// <summary>Gets whether the spline has neither control points nor fit points, as AutoCAD's <c>IsNull</c>.</summary>
        public bool IsNull => NumControlPoints == 0 && NumFitPoints == 0;

        /// <summary>Gets whether the spline has weights (flag 4 of group 70), as AutoCAD's <c>IsRational</c>.</summary>
        public bool IsRational => (GetInt(70, 0) & 4) != 0;

        /// <summary>Gets whether the record says the spline lies in a plane (flag 8 of group 70), as AutoCAD's <c>IsPlanar</c>.</summary>
        public bool IsPlanar => (GetInt(70, 0) & 8) != 0;

        /// <summary>Gets whether the spline is closed (flag 1 of group 70), as AutoCAD's <c>Closed</c>.</summary>
        public override bool Closed => (GetInt(70, 0) & 1) != 0;

        /// <summary>Gets whether the spline is periodic (flag 2 of group 70), as AutoCAD's <c>IsPeriodic</c>.</summary>
        public override bool IsPeriodic => (GetInt(70, 0) & 2) != 0;

        /// <summary>Gets how the spline is given, as AutoCAD's <c>Type</c>: by its fit points where it has some, else by its control points.</summary>
        public DxfSplineType Type => HasFitData ? DxfSplineType.FitPoints : DxfSplineType.ControlPoints;

        /// <summary>
        /// Gets or sets the control points (groups 10, 20, 30), in the world, a new list on each call; their count (73) with them.
        /// </summary>
        /// <value>
        /// Set, the points, as many as the knots ask for (their count less the degree and one) and as the weights are where the
        /// spline has some: the knots and weights are not changed with them. <see cref="SetNurbsData"/> sets points of another
        /// count with their knots.
        /// </value>
        /// <exception cref="ArgumentNullException">Thrown when the points set are null.</exception>
        /// <exception cref="ArgumentException">Thrown when a point set is not finite, or their count disagrees with the knots or
        /// the weights, which would leave the spline no spline.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a spline that is erased.</exception>
        public IReadOnlyList<GeoPoint3> ControlPoints
        {
            get => Read(Points(10));

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (SamePoints(value, ControlPoints))
                {
                    return;
                }

                CheckNurbs(Degree, value.Count, Knots, Weights.Count, nameof(value));
                List<List<KeyValuePair<int, string>>> points = Points(10);
                var written = new List<List<KeyValuePair<int, string>>>(value.Count);

                for (int i = 0; i < value.Count; i++)
                {
                    written.Add(Point(10, value[i], i < points.Count ? points[i] : null, nameof(value)));
                }

                WritePoints(10, ControlCodes, written, 73);
            }
        }

        /// <summary>Gets or sets the knots (groups 40), a new list on each call; their count (72) with them.</summary>
        /// <value>
        /// Set, as many as the control points and the degree and one, none going down: a cubic of 5 points has 9. The points are
        /// not changed with them; <see cref="SetNurbsData"/> sets knots of another count with their points.
        /// </value>
        /// <exception cref="ArgumentNullException">Thrown when the knots set are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a knot set is not finite.</exception>
        /// <exception cref="ArgumentException">Thrown when their count disagrees with the control points and the degree, or a
        /// knot is below the one before it, which would leave the spline no spline.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a spline that is erased.</exception>
        public IReadOnlyList<double> Knots
        {
            get => NumberList(40);

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (SameNumbers(value, Knots))
                {
                    return;
                }

                CheckNurbs(Degree, NumControlPoints, value, Weights.Count, nameof(value));
                WriteNumbers(40, KnotCodes, value, 72);
            }
        }

        /// <summary>
        /// Gets or sets the weights (groups 41), a new list on each call: empty for a spline that is not rational, as the file has
        /// none.
        /// </summary>
        /// <value>Set, one to each control point, or none; flag 4 of group 70 said with them.</value>
        /// <exception cref="ArgumentNullException">Thrown when the weights set are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a weight set is not above nought.</exception>
        /// <exception cref="ArgumentException">Thrown when the weights are neither none nor one to each control point.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a spline that is erased.</exception>
        public IReadOnlyList<double> Weights
        {
            get => NumberList(41);

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (SameNumbers(value, Weights))
                {
                    return;
                }

                foreach (double weight in value)
                {
                    Guard.Positive(weight, nameof(value), "A weight has to be a finite number above nought.");
                }

                CheckNurbs(Degree, NumControlPoints, Knots, value.Count, nameof(value));
                WriteWeights(value);
            }
        }

        /// <summary>
        /// Gives the spline its whole NURBS data at once, as setting AutoCAD's <c>NurbsData</c> does: degree, flags, control
        /// points, knots and weights, checked together and written in one go, its fit data (fit points and end tangents) taken
        /// out, as the spline is given by its control points from then on. A cubic of 4 points from (0, 0, 0) to (4, 0, 0) takes
        /// the knots 0, 0, 0, 0, 1, 1, 1, 1.
        /// </summary>
        /// <param name="degree">The degree, 1 or more.</param>
        /// <param name="rational">Whether the weights are written (flag 4 of group 70).</param>
        /// <param name="closed">Whether it is closed (flag 1).</param>
        /// <param name="periodic">Whether it is periodic (flag 2).</param>
        /// <param name="controlPoints">The control points, in the world, the degree and one at least.</param>
        /// <param name="knots">The knots, as many as the points and the degree and one, none going down.</param>
        /// <param name="weights">The weights, one to each point above nought where the spline is rational; not read, and may be
        /// null, where it is not.</param>
        /// <exception cref="ArgumentNullException">Thrown when the points or the knots are null, or the weights of a rational spline.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the degree is below 1, or a knot or weight is not finite, or a
        /// weight not above nought.</exception>
        /// <exception cref="ArgumentException">Thrown when a point is not finite, or the counts do not make a spline.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline is erased.</exception>
        public void SetNurbsData(int degree, bool rational, bool closed, bool periodic, IEnumerable<GeoPoint3> controlPoints, IEnumerable<double> knots, IEnumerable<double> weights)
        {
            if (degree < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(degree), degree, "A spline is of degree 1 or more.");
            }

            if (controlPoints == null)
            {
                throw new ArgumentNullException(nameof(controlPoints));
            }

            if (knots == null)
            {
                throw new ArgumentNullException(nameof(knots));
            }

            if (rational && weights == null)
            {
                throw new ArgumentNullException(nameof(weights));
            }

            var points = new List<GeoPoint3>(controlPoints);
            var knotList = new List<double>(knots);
            var weightList = rational ? new List<double>(weights) : new List<double>();

            foreach (double knot in knotList)
            {
                Guard.Finite(knot, nameof(knots), "A knot has to be a finite number.");
            }

            foreach (double weight in weightList)
            {
                Guard.Positive(weight, nameof(weights), "A weight has to be a finite number above nought.");
            }

            if (rational && weightList.Count != points.Count)
            {
                throw new ArgumentException("A rational spline has a weight for each control point: " + weightList.Count.ToString(CultureInfo.InvariantCulture) + " for " + points.Count.ToString(CultureInfo.InvariantCulture) + ".", nameof(weights));
            }

            if (points.Count < degree + 1)
            {
                throw new ArgumentException("A spline of degree " + degree.ToString(CultureInfo.InvariantCulture) + " has " + (degree + 1).ToString(CultureInfo.InvariantCulture) + " control points at least, and was given " + points.Count.ToString(CultureInfo.InvariantCulture) + ".", nameof(controlPoints));
            }

            CheckNurbs(degree, points.Count, knotList, weightList.Count, nameof(knots));
            List<List<KeyValuePair<int, string>>> written = Rewritten(points);
            int flags = (GetInt(70, 0) & ~7) | (closed ? 1 : 0) | (periodic ? 2 : 0) | (rational ? 4 : 0);
            SetInt(70, flags, 0);
            SetInt(71, degree, 3);
            WriteNumbers(40, KnotCodes, knotList, 72);
            WriteNumbers(41, WeightCodes, weightList, -1);
            WritePoints(10, ControlCodes, written, 73);
            RemoveFitData();
        }

        /// <summary>Gets the fit points (groups 11, 21, 31), in the world, a new list on each call.</summary>
        public IReadOnlyList<GeoPoint3> FitPoints => Read(Points(11));

        /// <summary>Gets or sets the tolerance the spline is fitted to its fit points within (group 44), as AutoCAD's <c>FitTolerance</c>; nought where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance set is below nought or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a spline that is erased.</exception>
        public double FitTolerance
        {
            get => GetDouble(44, 0.0);

            set
            {
                Guard.NonNegative(value, nameof(value), "A tolerance has to be a finite number, nought or more.");
                SetDouble(44, value, 0.0);
            }
        }

        /// <summary>Gets the direction the spline starts in at its first fit point (groups 12, 22, 32), as AutoCAD's <c>StartFitTangent</c>; of no length where the record gives none.</summary>
        public GeoVector3 StartFitTangent => Direction(12);

        /// <summary>Gets the direction the spline ends in at its last fit point (groups 13, 23, 33), as AutoCAD's <c>EndFitTangent</c>; of no length where the record gives none.</summary>
        public GeoVector3 EndFitTangent => Direction(13);

        /// <summary>Gets a control point, as AutoCAD's <c>GetControlPointAt</c>.</summary>
        /// <param name="index">The index of the point, from nought.</param>
        /// <returns>The point, in the world.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no point of the index.</exception>
        public GeoPoint3 GetControlPointAt(int index) => Read(At(Points(10), index));

        /// <summary>Moves a control point, as AutoCAD's <c>SetControlPointAt</c>: its groups written anew, the others kept.</summary>
        /// <param name="index">The index of the point, from nought.</param>
        /// <param name="point">Where it is to be, in the world.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no point of the index.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline is erased.</exception>
        public void SetControlPointAt(int index, GeoPoint3 point)
        {
            List<List<KeyValuePair<int, string>>> points = Points(10);
            points[CheckIndex(points, index)] = Point(10, point, points[index], nameof(point));
            WritePoints(10, ControlCodes, points, 73);
        }

        /// <summary>Gets a fit point, as AutoCAD's <c>GetFitPointAt</c>.</summary>
        /// <param name="index">The index of the point, from nought.</param>
        /// <returns>The point, in the world.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no point of the index.</exception>
        public GeoPoint3 GetFitPointAt(int index) => Read(At(Points(11), index));

        /// <summary>Gets the weight of a control point, as AutoCAD's <c>WeightAt</c>: 1 for every point of a spline that is not rational.</summary>
        /// <param name="index">The index of the point, from nought.</param>
        /// <returns>The weight.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no point of the index.</exception>
        public double WeightAt(int index)
        {
            CheckIndex(Points(10), index);
            IReadOnlyList<double> weights = Weights;
            return index < weights.Count ? weights[index] : 1.0;
        }

        /// <summary>
        /// Sets the weight of a control point, as AutoCAD's <c>SetWeightAt</c>: a spline that was not rational is made so, every
        /// other point weighing 1.
        /// </summary>
        /// <param name="index">The index of the point, from nought.</param>
        /// <param name="weight">The weight, above nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no point of the index, or the weight is not above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline is erased.</exception>
        public void SetWeightAt(int index, double weight)
        {
            Guard.Positive(weight, nameof(weight), "A weight has to be a finite number above nought.");
            int count = Points(10).Count;

            if (index < 0 || index >= count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The spline has " + count.ToString(CultureInfo.InvariantCulture) + " control points, from nought.");
            }

            if (Same(weight, WeightAt(index)) && (IsRational || weight == 1.0))
            {
                return;
            }

            var weights = new List<double>(Weights);

            while (weights.Count < count)
            {
                weights.Add(1.0);
            }

            weights[index] = weight;
            Weights = weights;
        }

        /// <summary>
        /// Inserts a knot, as AutoCAD's <c>InsertKnot</c>, by Boehm's rule: one control point more, the curve the same. A spline
        /// with weights has them carried too.
        /// </summary>
        /// <param name="param">The knot, inside the spline's domain, from <see cref="DxfCurve.StartParam"/> to <see cref="DxfCurve.EndParam"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the knot is outside the domain, or not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no control points, its data do not make a spline, or it is erased.</exception>
        public void InsertKnot(double param)
        {
            Guard.Finite(param, nameof(param), "A knot has to be a finite number.");
            IReadOnlyList<GeoPoint3> points = ControlPoints;

            if (points.Count == 0)
            {
                throw new InvalidOperationException("A spline given by its fit points alone has no knots of its own to insert one into.");
            }

            DxfNurbs nurbs = Nurbs();

            if (param < nurbs.Start || param > nurbs.End)
            {
                throw new ArgumentOutOfRangeException(nameof(param), param, "The knot lies outside the spline, which runs from " + nurbs.Start.ToString("R", CultureInfo.InvariantCulture) + " to " + nurbs.End.ToString("R", CultureInfo.InvariantCulture) + ".");
            }

            int p = Degree;
            var u = new List<double>(Knots);
            bool rational = IsRational;
            IReadOnlyList<double> weights = Weights;
            int k = p;

            if (u.FindAll(x => x == param).Count >= p)
            {
                throw new ArgumentOutOfRangeException(nameof(param), param, "The knot stands as many times as the degree already: one more would break the curve.");
            }

            while (k + 1 < points.Count && u[k + 1] <= param)
            {
                k++;
            }

            var h = new List<double[]>(points.Count + 1);

            for (int i = 0; i < points.Count; i++)
            {
                double w = rational ? weights[i] : 1.0;
                h.Add(new[] { points[i].X * w, points[i].Y * w, points[i].Z * w, w });
            }

            var inserted = new List<double[]>(points.Count + 1);

            for (int i = 0; i <= points.Count; i++)
            {
                if (i <= k - p)
                {
                    inserted.Add(h[i]);
                }
                else if (i > k)
                {
                    inserted.Add(h[i - 1]);
                }
                else
                {
                    double a = (param - u[i]) / (u[i + p] - u[i]);
                    var q = new double[4];

                    for (int c = 0; c < 4; c++)
                    {
                        q[c] = (1.0 - a) * h[i - 1][c] + a * h[i][c];
                    }

                    inserted.Add(q);
                }
            }

            u.Insert(k + 1, param);
            var newPoints = new List<GeoPoint3>(inserted.Count);
            var newWeights = new List<double>(inserted.Count);

            foreach (double[] q in inserted)
            {
                newPoints.Add(new GeoPoint3(q[0] / q[3], q[1] / q[3], q[2] / q[3]));
                newWeights.Add(q[3]);
            }

            WriteNumbers(40, KnotCodes, u, 72);
            WritePoints(10, ControlCodes, Rewritten(newPoints), 73);

            if (rational)
            {
                WriteNumbers(41, WeightCodes, newWeights, -1);
            }
        }

        /// <summary>
        /// Takes the fit data out, as AutoCAD's <c>PurgeFitData</c>: the fit points, their count, and the tangents at the ends; the
        /// spline is given by its control points from then on. A spline with no control points keeps its fit points.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the spline is erased.</exception>
        public void PurgeFitData()
        {
            if (NumControlPoints == 0)
            {
                return;
            }

            RemoveFitData();
        }

        /// <summary>Takes the fit points, their count and the tangents at the ends out; nothing where there are none.</summary>
        private void RemoveFitData()
        {
            if (!HasFitData && GetString(12) == null && GetString(13) == null && GetInt(74, 0) == 0)
            {
                return;
            }

            WritePoints(11, FitCodes, new List<List<KeyValuePair<int, string>>>(), 74);
            SetGroups(
                new KeyValuePair<int, string>(12, null), new KeyValuePair<int, string>(22, null), new KeyValuePair<int, string>(32, null),
                new KeyValuePair<int, string>(13, null), new KeyValuePair<int, string>(23, null), new KeyValuePair<int, string>(33, null));
        }

        /// <summary>The groups of points to be written, those of the points the spline has kept where a coordinate is the same to the bit.</summary>
        private List<List<KeyValuePair<int, string>>> Rewritten(IReadOnlyList<GeoPoint3> points)
        {
            List<List<KeyValuePair<int, string>>> was = Points(10);
            var written = new List<List<KeyValuePair<int, string>>>(points.Count);

            for (int i = 0; i < points.Count; i++)
            {
                written.Add(Point(10, points[i], i < was.Count ? was[i] : null, "controlPoints"));
            }

            return written;
        }

        /// <summary>Whether two lists of numbers are the same, number for number to the bit.</summary>
        private static bool SameNumbers(IReadOnlyList<double> a, IReadOnlyList<double> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (!Same(a[i], b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Whether two lists of points are the same, coordinate for coordinate to the bit.</summary>
        private static bool SamePoints(IReadOnlyList<GeoPoint3> a, IReadOnlyList<GeoPoint3> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (!Same(a[i].X, b[i].X) || !Same(a[i].Y, b[i].Y) || !Same(a[i].Z, b[i].Z))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Writes the weights and says with flag 4 of group 70 whether there are any.</summary>
        private void WriteWeights(IReadOnlyList<double> weights)
        {
            WriteNumbers(41, WeightCodes, weights, -1);
            int flags = GetInt(70, 0);
            SetInt(70, weights.Count > 0 ? flags | 4 : flags & ~4, 0);
        }

        /// <summary>
        /// Refuses NURBS data whose counts disagree: knots as many as the control points and the degree and one, none going
        /// down, and the weights none or one to each point. A spline of no control points, no knots and no weights, as one given
        /// by its fit points alone, is let be.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the counts disagree.</exception>
        private static void CheckNurbs(int degree, int points, IReadOnlyList<double> knots, int weights, string parameter)
        {
            if (points == 0 && knots.Count == 0 && weights == 0)
            {
                return;
            }

            if (knots.Count != points + degree + 1)
            {
                throw new ArgumentException("A spline of degree " + degree.ToString(CultureInfo.InvariantCulture) + " and " + points.ToString(CultureInfo.InvariantCulture) + " control points has " + (points + degree + 1).ToString(CultureInfo.InvariantCulture) + " knots, and would have " + knots.Count.ToString(CultureInfo.InvariantCulture) + ": set them together with SetNurbsData.", parameter);
            }

            if (weights != 0 && weights != points)
            {
                throw new ArgumentException("A spline has a weight for each control point or none, and would have " + weights.ToString(CultureInfo.InvariantCulture) + " for " + points.ToString(CultureInfo.InvariantCulture) + ".", parameter);
            }

            for (int i = 1; i < knots.Count; i++)
            {
                if (!(knots[i] >= knots[i - 1]))
                {
                    throw new ArgumentException("The knots of a spline do not go down, and the knot " + i.ToString(CultureInfo.InvariantCulture) + " would be below the one before it.", parameter);
                }
            }
        }

        #endregion

        #region The curve

        /// <summary>Gets the parameter at the start: the knot of index degree, as AutoCAD's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the spline's data do not make a spline.</exception>
        public override double StartParam => Nurbs().Start;

        /// <summary>Gets the parameter at the end: the knot of index count of the control points, as AutoCAD's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the spline's data do not make a spline.</exception>
        public override double EndParam => Nurbs().End;

        /// <summary>Gets the point at a parameter, a knot value, as AutoCAD's <c>GetPointAtParameter</c>.</summary>
        /// <param name="value">The parameter, from <see cref="StartParam"/> to <see cref="EndParam"/>.</param>
        /// <returns>The point.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        public override GeoPoint3 GetPointAtParameter(double value)
        {
            DxfNurbs nurbs = Nurbs();
            Within(value - nurbs.Start, nurbs.End - nurbs.Start, nameof(value));
            return nurbs.PointAt(value);
        }

        /// <summary>Gets the parameter at a point of the spline, as AutoCAD's <c>GetParameterAtPoint</c>.</summary>
        /// <param name="point">The point, within the point tolerance of the spline.</param>
        /// <returns>The parameter.</returns>
        /// <exception cref="ArgumentException">Thrown when the point is not on the spline.</exception>
        public override double GetParameterAtPoint(GeoPoint3 point) => ParameterOn(Nurbs(), point);

        /// <summary>Gets the length along the spline from its start to a parameter, as AutoCAD's <c>GetDistanceAtParameter</c>.</summary>
        /// <param name="value">The parameter, from <see cref="StartParam"/> to <see cref="EndParam"/>.</param>
        /// <returns>The distance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        public override double GetDistanceAtParameter(double value)
        {
            DxfNurbs nurbs = Nurbs();
            Within(value - nurbs.Start, nurbs.End - nurbs.Start, nameof(value));
            return nurbs.LengthTo(value);
        }

        /// <summary>Gets the parameter at a length along the spline from its start, as AutoCAD's <c>GetParameterAtDistance</c>.</summary>
        /// <param name="dist">The distance, from nought to the spline's length.</param>
        /// <returns>The parameter.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is past either end.</exception>
        public override double GetParameterAtDistance(double dist)
        {
            DxfNurbs nurbs = Nurbs();
            Within(dist, nurbs.Length, nameof(dist));
            return nurbs.ParameterAtLength(dist);
        }

        /// <summary>Gets the length along the spline from its start to a point of it, as AutoCAD's <c>GetDistAtPoint</c>.</summary>
        /// <param name="point">The point, within the point tolerance of the spline.</param>
        /// <returns>The distance.</returns>
        /// <exception cref="ArgumentException">Thrown when the point is not on the spline.</exception>
        public override double GetDistAtPoint(GeoPoint3 point)
        {
            DxfNurbs nurbs = Nurbs();
            return nurbs.LengthTo(ParameterOn(nurbs, point));
        }

        /// <summary>Gets the point at a length along the spline from its start, as AutoCAD's <c>GetPointAtDist</c>.</summary>
        /// <param name="value">The distance, from nought to the spline's length.</param>
        /// <returns>The point.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is past either end.</exception>
        public override GeoPoint3 GetPointAtDist(double value)
        {
            DxfNurbs nurbs = Nurbs();
            Within(value, nurbs.Length, nameof(value));
            return nurbs.PointAt(nurbs.ParameterAtLength(value));
        }

        /// <summary>Gets the point of the spline nearest a point, as AutoCAD's <c>GetClosestPointTo</c>: of the least parameter where two are as near.</summary>
        /// <param name="givenPoint">The point.</param>
        /// <param name="extend">Not read: the spline is taken as it is.</param>
        /// <returns>The nearest point.</returns>
        public override GeoPoint3 GetClosestPointTo(GeoPoint3 givenPoint, bool extend)
        {
            Nurbs().Closest(givenPoint, out GeoPoint3 nearest);
            return nearest;
        }

        /// <summary>Gets the derivative of the point by the knot parameter, as AutoCAD's <c>GetFirstDerivative</c>.</summary>
        /// <param name="value">The parameter, from <see cref="StartParam"/> to <see cref="EndParam"/>.</param>
        /// <returns>The derivative.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        public override GeoVector3 GetFirstDerivative(double value)
        {
            DxfNurbs nurbs = Nurbs();
            Within(value - nurbs.Start, nurbs.End - nurbs.Start, nameof(value));
            nurbs.Evaluate(value, out _, out GeoVector3 first, out _);
            return first;
        }

        /// <summary>Gets the second derivative of the point by the knot parameter, as AutoCAD's <c>GetSecondDerivative</c>.</summary>
        /// <param name="value">The parameter, from <see cref="StartParam"/> to <see cref="EndParam"/>.</param>
        /// <returns>The second derivative.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        public override GeoVector3 GetSecondDerivative(double value)
        {
            DxfNurbs nurbs = Nurbs();
            Within(value - nurbs.Start, nurbs.End - nurbs.Start, nameof(value));
            nurbs.Evaluate(value, out _, out _, out GeoVector3 second);
            return second;
        }

        /// <summary>
        /// Turns the spline to run from its end to its start, as AutoCAD's <c>ReverseCurve</c>: its control points, weights and fit
        /// points in the other order, each knot u made a + b - u of the first and last and their order turned, the tangents at
        /// the ends swapped and turned.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the spline is erased.</exception>
        public override void ReverseCurve()
        {
            List<List<KeyValuePair<int, string>>> points = Points(10);
            List<List<KeyValuePair<int, string>>> fit = Points(11);
            IReadOnlyList<double> knots = Knots;
            var weights = new List<double>(Weights);
            GeoVector3 start = StartFitTangent;
            GeoVector3 end = EndFitTangent;
            bool hasStart = GetString(12) != null || GetString(22) != null || GetString(32) != null;
            bool hasEnd = GetString(13) != null || GetString(23) != null || GetString(33) != null;
            points.Reverse();
            fit.Reverse();
            weights.Reverse();
            var reversed = new List<double>(knots.Count);

            for (int i = knots.Count - 1; i >= 0; i--)
            {
                reversed.Add(knots[0] + knots[knots.Count - 1] - knots[i]);
            }

            WritePoints(10, ControlCodes, points, 73);
            WritePoints(11, FitCodes, fit, 74);
            WriteNumbers(40, KnotCodes, reversed, 72);

            if (weights.Count > 0)
            {
                WriteNumbers(41, WeightCodes, weights, -1);
            }

            var tangents = new List<KeyValuePair<int, string>>();

            if (hasEnd)
            {
                tangents.AddRange(PointGroups(12, new GeoPoint3(-end.X, -end.Y, -end.Z), new GeoPoint3(0.0, 0.0, 0.0)));
            }

            if (hasStart)
            {
                tangents.AddRange(PointGroups(13, new GeoPoint3(-start.X, -start.Y, -start.Z), new GeoPoint3(0.0, 0.0, 0.0)));
            }

            if (hasStart && !hasEnd)
            {
                tangents.AddRange(new[] { new KeyValuePair<int, string>(12, null), new KeyValuePair<int, string>(22, null), new KeyValuePair<int, string>(32, null) });
            }

            if (hasEnd && !hasStart)
            {
                tangents.AddRange(new[] { new KeyValuePair<int, string>(13, null), new KeyValuePair<int, string>(23, null), new KeyValuePair<int, string>(33, null) });
            }

            SetGroups(tangents.ToArray());
        }

        /// <summary>
        /// Gives the spline the degree, knots, control points and weights of a <see cref="GeoSpline3"/>, as AutoCAD's
        /// <c>SetFromGeCurve</c>: its fit data taken out.
        /// </summary>
        /// <param name="geCurve">The spline, a <see cref="GeoSpline3"/>.</param>
        /// <exception cref="ArgumentException">Thrown when the shape is not a <see cref="GeoSpline3"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline is erased.</exception>
        public override void SetFromGeCurve(object geCurve)
        {
            if (!(geCurve is GeoSpline3 spline))
            {
                throw new ArgumentException("A spline takes a GeoSpline3, and was given " + (geCurve == null ? "null" : "a " + geCurve.GetType().Name) + ".", nameof(geCurve));
            }

            SetNurbsData(spline.Degree, spline.IsRational, Closed, false, spline.ControlPoints, spline.Knots, spline.Weights);
        }

        /// <summary>
        /// Moves, turns, scales or mirrors the spline, as AutoCAD's <c>TransformBy</c>, as ezdxf carries one: its control points
        /// and fit points moved in the world, the tangents at its ends and its normal carried as directions, its knots and
        /// weights kept.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline is erased.</exception>
        public override void TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            if (DxfOcsTransform.IsIdentity(transform))
            {
                return;
            }

            foreach (int code in new[] { 10, 11 })
            {
                List<List<KeyValuePair<int, string>>> points = Points(code);

                for (int i = 0; i < points.Count; i++)
                {
                    points[i] = Point(code, transform.Transform(Read(points[i])), points[i], nameof(transform));
                }

                WritePoints(code, code == 10 ? ControlCodes : FitCodes, points, code == 10 ? 73 : 74);
            }

            var directions = new List<KeyValuePair<int, string>>();

            foreach (int code in new[] { 12, 13, 210 })
            {
                if (GetString(code) != null || GetString(code + 10) != null || GetString(code + 20) != null)
                {
                    GeoVector3 moved = transform.Transform(Direction(code));
                    directions.AddRange(PointGroups(code, new GeoPoint3(moved.X, moved.Y, moved.Z), new GeoPoint3(0.0, 0.0, 0.0)));
                }
            }

            SetGroups(directions.ToArray());
        }

        #endregion

        [DxfType("SPLINE")]
        internal static DxfEntity Create(DxfItem item) => new DxfSpline(item);

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        /// <summary>The spline to walk: of the record's control points, or where it has none, the one the reader passes through its fit points.</summary>
        private DxfNurbs Nurbs()
        {
            IReadOnlyList<GeoPoint3> points = ControlPoints;

            if (points.Count > 0)
            {
                return new DxfNurbs(Degree, Knots, points, IsRational ? Weights : null);
            }

            if (GetGeCurve() is GeoSpline3 spline)
            {
                return new DxfNurbs(spline.Degree, spline.Knots, spline.ControlPoints, spline.IsRational ? spline.Weights : null);
            }

            throw new InvalidOperationException("The SPLINE has no control points, and no fit points the reader passes a spline through.");
        }

        private static double ParameterOn(DxfNurbs nurbs, GeoPoint3 point)
        {
            double parameter = nurbs.Closest(point, out GeoPoint3 nearest);

            if (!nearest.IsEqualTo(point, Tolerance.Default))
            {
                throw new ArgumentException("The point " + point + " is not on the spline.", nameof(point));
            }

            return parameter;
        }

        private static IEnumerable<DxfTypedValue> Head(int degree, int flags, double knotTolerance, double controlPointTolerance)
        {
            if (degree < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(degree), degree, "A spline is of degree 1 or more.");
            }

            Guard.NonNegative(knotTolerance, nameof(knotTolerance), "A tolerance has to be a finite number, nought or more.");
            Guard.NonNegative(controlPointTolerance, nameof(controlPointTolerance), "A tolerance has to be a finite number, nought or more.");
            return new[]
            {
                new DxfTypedValue(100, "AcDbSpline"),
                new DxfTypedValue(70, flags.ToString(CultureInfo.InvariantCulture)),
                new DxfTypedValue(71, degree.ToString(CultureInfo.InvariantCulture)),
                new DxfTypedValue(72, "0"),
                new DxfTypedValue(73, "0"),
                new DxfTypedValue(74, "0"),
                new DxfTypedValue(42, Numbers.Exact(knotTolerance + 0.0)),
                new DxfTypedValue(43, Numbers.Exact(controlPointTolerance + 0.0)),
            };
        }

        /// <summary>A direction of the record (12, 13 or 210 and the codes 10 and 20 above), of no length where it gives none.</summary>
        private GeoVector3 Direction(int code)
        {
            GeoPoint3 p = GetPoint(code, new GeoPoint3(0.0, 0.0, 0.0));
            return new GeoVector3(p.X, p.Y, p.Z);
        }

        /// <summary>The points of a code (10 or 11) as the record gives them, each its groups of that code and those 10 and 20 above, as the reader reads them.</summary>
        private List<List<KeyValuePair<int, string>>> Points(int code)
        {
            var points = new List<List<KeyValuePair<int, string>>>();

            foreach (DxfTypedValue group in GetOwnGroups(0))
            {
                if (group.TypeCode == code)
                {
                    points.Add(new List<KeyValuePair<int, string>> { new KeyValuePair<int, string>(code, group.Value) });
                }
                else if ((group.TypeCode == code + 10 || group.TypeCode == code + 20) && points.Count > 0)
                {
                    points[points.Count - 1].Add(new KeyValuePair<int, string>(group.TypeCode, group.Value));
                }
            }

            return points;
        }

        private List<double> NumberList(int code)
        {
            var numbers = new List<double>();

            foreach (DxfTypedValue group in GetOwnGroups(0))
            {
                if (group.TypeCode == code)
                {
                    numbers.Add(Numbers.TryRead(group.Value, out double value) && Guard.IsFinite(value) ? value : 0.0);
                }
            }

            return numbers;
        }

        private static IReadOnlyList<GeoPoint3> Read(List<List<KeyValuePair<int, string>>> points)
        {
            var read = new List<GeoPoint3>(points.Count);

            foreach (List<KeyValuePair<int, string>> point in points)
            {
                read.Add(Read(point));
            }

            return read.AsReadOnly();
        }

        /// <summary>A point of the record, as the reader reads it: the last of each coordinate, a coordinate it leaves out nought.</summary>
        private static GeoPoint3 Read(List<KeyValuePair<int, string>> point)
        {
            var c = new double[3];

            foreach (KeyValuePair<int, string> group in point)
            {
                int axis = (group.Key % 100) / 10 - 1;
                c[axis] = Numbers.TryRead(group.Value, out double value) && Guard.IsFinite(value) ? value : 0.0;
            }

            return new GeoPoint3(c[0], c[1], c[2]);
        }

        /// <summary>The groups of a point of a code, those of a point it was kept where a coordinate is the same to the bit.</summary>
        private static List<KeyValuePair<int, string>> Point(int code, GeoPoint3 value, List<KeyValuePair<int, string>> was, string parameter)
        {
            if (!value.IsValid)
            {
                throw new ArgumentException("A point has to be of finite numbers: " + value + " is not.", parameter);
            }

            GeoPoint3 old = was == null ? default(GeoPoint3) : Read(was);
            double[] wanted = { value.X, value.Y, value.Z };
            double[] read = { old.X, old.Y, old.Z };
            var groups = new List<KeyValuePair<int, string>>(3);

            for (int i = 0; i < 3; i++)
            {
                int at = code + 10 * i;
                string text = was?.FindLast(g => g.Key == at).Value;
                groups.Add(new KeyValuePair<int, string>(at, text != null && Same(wanted[i], read[i]) ? text : Numbers.Exact(wanted[i] + 0.0)));
            }

            return groups;
        }

        private static List<KeyValuePair<int, string>> At(List<List<KeyValuePair<int, string>>> points, int index)
        {
            CheckIndex(points, index);
            return points[index];
        }

        private static int CheckIndex(List<List<KeyValuePair<int, string>>> points, int index)
        {
            if (index < 0 || index >= points.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The spline has " + points.Count.ToString(CultureInfo.InvariantCulture) + " such points, from nought.");
            }

            return index;
        }

        private void WritePoints(int code, IReadOnlyCollection<int> codes, List<List<KeyValuePair<int, string>>> points, int countCode)
        {
            var run = new List<KeyValuePair<int, string>>();

            foreach (List<KeyValuePair<int, string>> point in points)
            {
                run.AddRange(point);
            }

            ReplaceGroups(0, codes, run.ToArray());
            SetInt(countCode, points.Count, 0);
        }

        private void WriteNumbers(int code, IReadOnlyCollection<int> codes, IReadOnlyList<double> values, int countCode)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            List<double> now = NumberList(code);
            var texts = new List<string>();

            foreach (DxfTypedValue group in GetOwnGroups(0))
            {
                if (group.TypeCode == code)
                {
                    texts.Add(group.Value);
                }
            }

            var run = new KeyValuePair<int, string>[values.Count];

            for (int i = 0; i < values.Count; i++)
            {
                Guard.Finite(values[i], nameof(values), "A number has to be a finite number.");
                run[i] = new KeyValuePair<int, string>(code, i < now.Count && Same(values[i], now[i]) ? texts[i] : Numbers.Exact(values[i] + 0.0));
            }

            ReplaceGroups(0, codes, run);

            if (countCode > 0)
            {
                SetInt(countCode, values.Count, 0);
            }
        }
    }
}
