using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <content>
    /// The editing of a spline as AutoCAD's: its NURBS and fit data as values, control and fit points put in and taken out, the
    /// degree raised, the spline rebuilt, made to pass a point, cut, run on past its ends, offset and made a polyline.
    /// </content>
    public sealed partial class DxfSpline
    {
        // The most degree the library reads a spline of.
        private const int MostDegree = 10;

        /// <summary>
        /// Gets or sets the NURBS data in one value, as AutoCAD's <c>NurbsData</c>: degree, flags, control points, knots,
        /// weights and the tolerances of the points (group 43) and knots (42). A spline given by its fit points alone gives
        /// the data of the spline <see cref="DxfReader"/> passes through them.
        /// </summary>
        /// <value>
        /// Set, the data written as <see cref="SetNurbsData"/> writes it, its fit data taken out, and the two tolerances; data
        /// equal to the data read, number for number to the bit, changes nothing.
        /// </value>
        /// <exception cref="ArgumentException">Thrown when the data set does not make a spline, as <see cref="SetNurbsData"/> says.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the degree set is below 1, a number is not finite, or a tolerance is below nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a spline that is erased.</exception>
        public DxfNurbsData NurbsData
        {
            get
            {
                double pointTolerance = GetDouble(43, 0.0);
                double knotTolerance = GetDouble(42, 0.0);
                IReadOnlyList<GeoPoint3> points = ControlPoints;

                if (points.Count > 0 || !(GetGeCurve() is GeoSpline3 spline))
                {
                    bool rational = IsRational;
                    return new DxfNurbsData(Degree, rational, Closed, IsPeriodic, points, Knots, rational ? Weights : null, pointTolerance, knotTolerance);
                }

                return new DxfNurbsData(spline.Degree, spline.IsRational, Closed, IsPeriodic, spline.ControlPoints, spline.Knots, spline.IsRational ? spline.Weights : null, pointTolerance, knotTolerance);
            }

            set
            {
                if (Same(value, NurbsData))
                {
                    return;
                }

                Guard.NonNegative(value.ControlPointTolerance, nameof(value), "A tolerance has to be a finite number, nought or more.");
                Guard.NonNegative(value.KnotTolerance, nameof(value), "A tolerance has to be a finite number, nought or more.");
                SetNurbsData(value.Degree, value.Rational, value.Closed, value.Periodic, value.GetControlPoints(), value.GetKnots(), value.GetWeights());
                SetDouble(43, value.ControlPointTolerance, 0.0);
                SetDouble(42, value.KnotTolerance, 0.0);
            }
        }

        /// <summary>
        /// Gets or sets the fit data in one value, as AutoCAD's <c>FitData</c>: the fit points, the degree, the fit tolerance
        /// (group 44), the tangents at the ends (12 and 13) and whether they are given, and the parameters by the chord.
        /// </summary>
        /// <value>
        /// Set, the fit points and the rest written, and the control points worked out anew through them, as AutoCAD keeps both:
        /// the spline through the points by <see cref="GeoSpline3.Interpolate(IEnumerable{GeoPoint3}, int, Tolerance)"/>
        /// (with the tangents where given), its knots the lengths of the chords from the first point, so that a spline through
        /// (0, 0, 0), (30, 40, 0) and (60, 0, 0) runs from 0 to 100. It passes through every point, which is within any fit
        /// tolerance. A periodic spline is closed through its first point again, its tangent there the direction from the
        /// point before to the point after; with fewer points than the degree asks for, the degree is lowered to what they
        /// carry, as <see cref="DxfReader"/> reads such a spline, and written so. Data equal to the data read changes nothing.
        /// </value>
        /// <exception cref="ArgumentException">Thrown when there are fewer than two points, a point or a tangent is not finite,
        /// or the points are all one.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the degree is outside 1 to 10, or the tolerance below nought.</exception>
        /// <exception cref="NotSupportedException">Thrown for parameters other than by the chord, which the library does not fit by.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a spline that is erased.</exception>
        public DxfFitData FitData
        {
            get
            {
                bool tangents = GetString(12) != null || GetString(22) != null || GetString(32) != null || GetString(13) != null || GetString(23) != null || GetString(33) != null;
                return new DxfFitData(FitPoints, Degree, FitTolerance)
                {
                    StartTangent = StartFitTangent,
                    EndTangent = EndFitTangent,
                    TangentsExist = tangents,
                    KnotParam = DxfKnotParameterizationEnum.Chord,
                    Periodic = IsPeriodic,
                };
            }

            set
            {
                if (Same(value, FitData))
                {
                    return;
                }

                if (value.KnotParam != DxfKnotParameterizationEnum.Chord && value.KnotParam != DxfKnotParameterizationEnum.NotDefinedParameterization)
                {
                    throw new NotSupportedException("A spline is fitted through its points by the chord: the library fits by no other parameters.");
                }

                Guard.NonNegative(value.FitTolerance, nameof(value), "A tolerance has to be a finite number, nought or more.");
                WriteFit(value.GetFitPoints(), value.Degree, value.FitTolerance, value.TangentsExist, value.StartTangent, value.EndTangent, value.Periodic, nameof(value));
            }
        }

        /// <summary>
        /// Gets a spline of this one's NURBS data, as AutoCAD's <c>Spline</c> gives one for a spline: a new <see cref="DxfSpline"/>
        /// in no file, on layer 0 with no look of its own; the fit data are not carried.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no data that make a spline.</exception>
        public override DxfSpline Spline
        {
            get
            {
                DxfNurbsData data = NurbsData;

                if (data.GetControlPoints().Count == 0)
                {
                    throw new InvalidOperationException("The SPLINE has no control points, and no fit points the reader passes a spline through.");
                }

                return new DxfSpline(data.Degree, data.Rational, data.Closed, data.Periodic, data.GetControlPoints(), data.GetKnots(), data.GetWeights(), data.ControlPointTolerance, data.KnotTolerance);
            }
        }

        /// <summary>
        /// Raises the degree of the spline, as AutoCAD's <c>ElevateDegree</c>: the curve kept exactly, a knot standing as many
        /// times more as the degree is raised, so that the spline is as smooth everywhere as it was; its fit data taken out,
        /// as it is given by its control points from then on. A cubic of 4 points raised to 4 has 5 points, the knots
        /// 0, 0, 0, 0, 0, 1, 1, 1, 1, 1.
        /// </summary>
        /// <param name="newDegree">The degree, above the spline's, at most 10; the spline's own changes nothing.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the degree is below the spline's or above 10.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no data that make a spline, or is erased.</exception>
        public void ElevateDegree(int newDegree)
        {
            Clamped(out int p, out List<double> u, out List<double[]> pw);

            if (newDegree == p)
            {
                return;
            }

            if (newDegree < p || newDegree > MostDegree)
            {
                throw new ArgumentOutOfRangeException(nameof(newDegree), newDegree, "A spline's degree is raised from its own, " + p.ToString(CultureInfo.InvariantCulture) + ", to 10 at most.");
            }

            DxfNurbsEdit.ElevateDegree(p, u, pw, newDegree - p, out List<double> uh, out List<double[]> qw);
            WriteClamped(newDegree, uh, qw);
        }

        /// <summary>
        /// Puts a control point in, as AutoCAD's <c>InsertControlPointAt</c>: the knot added to the knots, and the point with its
        /// weight put in among the control points where that knot works most, the middle of the points the span of the knot
        /// moves (for a cubic the second of the three, so that a cubic of 4 points from (0, 0) to (30, 0) given (15, 10) at 0.5
        /// has it as its third point of 5). The curve changes with the point, as AutoCAD's; the fit data are taken out.
        /// </summary>
        /// <param name="knotParam">The knot, inside the spline's domain; it may stand already, but not as many times as the degree.</param>
        /// <param name="controlPoint">The point, in the world.</param>
        /// <param name="weight">Its weight, above nought; a spline not rational is made so where it is not 1.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the knot is outside the domain or stands as many times as
        /// the degree already, or a number is not finite, or the weight not above nought.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no data that make a spline, or is erased.</exception>
        public void InsertControlPointAt(double knotParam, GeoPoint3 controlPoint, double weight)
        {
            Guard.Finite(knotParam, nameof(knotParam), "A knot has to be a finite number.");
            Guard.Positive(weight, nameof(weight), "A weight has to be a finite number above nought.");

            if (!controlPoint.IsValid)
            {
                throw new ArgumentException("A control point has to be of finite numbers: " + controlPoint + " is not.", nameof(controlPoint));
            }

            Clamped(out int p, out List<double> u, out List<double[]> pw);
            double start = u[p];
            double end = u[pw.Count];

            if (!(knotParam > start && knotParam < end) || DxfNurbsEdit.Multiplicity(u, knotParam) >= p)
            {
                throw new ArgumentOutOfRangeException(nameof(knotParam), knotParam, "A knot is put inside the spline, which runs from " + start.ToString("R", CultureInfo.InvariantCulture) + " to " + end.ToString("R", CultureInfo.InvariantCulture) + ", and where it stands fewer times than the degree.");
            }

            int k = DxfNurbsEdit.Span(p, u, pw.Count, knotParam);
            int at = k - (p - 1) / 2;
            u.Insert(k + 1, knotParam);
            pw.Insert(at, new[] { controlPoint.X * weight, controlPoint.Y * weight, controlPoint.Z * weight, weight });
            WriteClamped(p, u, pw);
        }

        /// <summary>
        /// Takes a control point out, as AutoCAD's <c>RemoveControlPointAt</c>: the point and its weight, and the knot inside the
        /// spline its putting in would have added, that of the span the point works most in, so that the spline stays one; the
        /// curve changes, and the fit data are taken out.
        /// </summary>
        /// <param name="index">The index of the point, from nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no point of the index.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline has as few points as its degree and one, which it
        /// keeps, has no data that make a spline, or is erased.</exception>
        public void RemoveControlPointAt(int index)
        {
            Clamped(out int p, out List<double> u, out List<double[]> pw);

            if (index < 0 || index >= pw.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The spline has " + pw.Count.ToString(CultureInfo.InvariantCulture) + " control points, from nought.");
            }

            if (pw.Count - 1 < p + 1)
            {
                throw new InvalidOperationException("A spline of degree " + p.ToString(CultureInfo.InvariantCulture) + " keeps " + (p + 1).ToString(CultureInfo.InvariantCulture) + " control points at least, and this one has " + pw.Count.ToString(CultureInfo.InvariantCulture) + ".");
            }

            int n = pw.Count - 1;
            int knot = Math.Max(p + 1, Math.Min(n, index + (p - 1) / 2 + 1));
            u.RemoveAt(knot);
            pw.RemoveAt(index);
            WriteClamped(p, u, pw);
        }

        /// <summary>
        /// Puts a fit point in, as AutoCAD's <c>InsertFitPointAt</c>: before the point of the index, or after the last where the
        /// index is their count, and the spline fitted through the points anew, as <see cref="FitData"/> fits it.
        /// </summary>
        /// <param name="index">Where it goes, from nought to <see cref="NumFitPoints"/>.</param>
        /// <param name="point">The point, in the world.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is below nought or above the count.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no fit points, or is erased.</exception>
        public void InsertFitPointAt(int index, GeoPoint3 point)
        {
            List<GeoPoint3> fit = FitList();

            if (index < 0 || index > fit.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "A fit point goes in from nought to the count of the fit points, " + fit.Count.ToString(CultureInfo.InvariantCulture) + ".");
            }

            fit.Insert(index, point);
            Refit(fit, nameof(point));
        }

        /// <summary>Takes a fit point out, as AutoCAD's <c>RemoveFitPointAt</c>, and fits the spline through the others anew.</summary>
        /// <param name="index">The index of the point, from nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no point of the index.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no fit points, or two, which it keeps, or is erased.</exception>
        public void RemoveFitPointAt(int index)
        {
            List<GeoPoint3> fit = FitList();
            CheckIndex(Points(11), index);

            if (fit.Count <= 2)
            {
                throw new InvalidOperationException("A spline through fit points keeps two at least, and this one has " + fit.Count.ToString(CultureInfo.InvariantCulture) + ".");
            }

            fit.RemoveAt(index);
            Refit(fit, nameof(index));
        }

        /// <summary>Moves a fit point, as AutoCAD's <c>SetFitPointAt</c>, and fits the spline through the points anew; a point set where it is changes nothing.</summary>
        /// <param name="index">The index of the point, from nought.</param>
        /// <param name="point">Where it is to be, in the world.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no point of the index.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no fit points, or is erased.</exception>
        public void SetFitPointAt(int index, GeoPoint3 point)
        {
            List<GeoPoint3> fit = FitList();
            CheckIndex(Points(11), index);

            if (Same(fit[index].X, point.X) && Same(fit[index].Y, point.Y) && Same(fit[index].Z, point.Z))
            {
                return;
            }

            fit[index] = point;
            Refit(fit, nameof(point));
        }

        /// <summary>
        /// Gives the spline fit data of its control points, as AutoCAD's <c>UpdateFitData</c>: its fit points the points of the
        /// curve at each knot from its start to its end, and the tangents at its ends the directions it starts and ends in, of
        /// unit length; the control points kept. A cubic of 4 points from (0, 0, 0) to (30, 0, 0) has the fit points of its
        /// start and end.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no data that make a spline, or is erased.</exception>
        public void UpdateFitData()
        {
            DxfNurbs nurbs = Nurbs();
            var fit = new List<GeoPoint3>();
            IReadOnlyList<double> knots = NurbsData.GetKnots();
            double last = double.NaN;

            foreach (double knot in knots)
            {
                if (knot >= nurbs.Start && knot <= nurbs.End && !Same(knot, last))
                {
                    fit.Add(nurbs.PointAt(knot));
                    last = knot;
                }
            }

            nurbs.Evaluate(nurbs.Start, out _, out GeoVector3 start, out _);
            nurbs.Evaluate(nurbs.End, out _, out GeoVector3 end, out _);
            List<List<KeyValuePair<int, string>>> was = Points(11);
            var written = new List<List<KeyValuePair<int, string>>>(fit.Count);

            for (int i = 0; i < fit.Count; i++)
            {
                written.Add(Point(11, fit[i], i < was.Count ? was[i] : null, "fit"));
            }

            WritePoints(11, FitCodes, written, 74);
            var tangents = new List<KeyValuePair<int, string>>();
            tangents.AddRange(TangentGroups(12, start));
            tangents.AddRange(TangentGroups(13, end));
            SetGroups(tangents.ToArray());
        }

        /// <summary>
        /// Moves the control points so that the spline passes a point at a parameter in a direction there, as AutoCAD's
        /// <c>ModifyPositionAndTangent</c>: the points the span of the parameter is made of each moved the least that does it
        /// (the change of least squares that meets the point and the derivative), the weights kept, the fit data taken out.
        /// </summary>
        /// <param name="param">The parameter, from <see cref="DxfCurve.StartParam"/> to <see cref="DxfCurve.EndParam"/>.</param>
        /// <param name="point">The point the spline is to pass there, in the world.</param>
        /// <param name="deriv">The derivative it is to have there, by the parameter, as <see cref="GetFirstDerivative(double)"/> gives one.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        /// <exception cref="ArgumentException">Thrown when the point or the derivative is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no data that make a spline, or is erased.</exception>
        public void ModifyPositionAndTangent(double param, GeoPoint3 point, GeoVector3 deriv)
        {
            if (!point.IsValid || !deriv.IsValid)
            {
                throw new ArgumentException("A point and a derivative have to be of finite numbers.", nameof(point));
            }

            Clamped(out int p, out List<double> u, out List<double[]> pw);
            double start = u[p];
            double end = u[pw.Count];
            Within(param - start, end - start, nameof(param));
            double t = Math.Max(start, Math.Min(end, param));
            int span = DxfNurbsEdit.Span(p, u, pw.Count, t);
            double[,] ders = DxfNurbsEdit.BasisDerivatives(p, u, span, t, 1);
            double weight = 0.0;
            double weightRate = 0.0;

            for (int j = 0; j <= p; j++)
            {
                weight += ders[0, j] * pw[span - p + j][3];
                weightRate += ders[1, j] * pw[span - p + j][3];
            }

            var r = new double[p + 1];
            var rr = new double[p + 1];
            double[] now = new double[3];
            double[] rate = new double[3];

            for (int j = 0; j <= p; j++)
            {
                double[] q = pw[span - p + j];
                double w = q[3];
                r[j] = ders[0, j] * w / weight;
                rr[j] = (ders[1, j] * w * weight - ders[0, j] * w * weightRate) / (weight * weight);

                for (int c = 0; c < 3; c++)
                {
                    now[c] += r[j] * q[c] / w;
                    rate[c] += rr[j] * q[c] / w;
                }
            }

            double[] wantPoint = { point.X - now[0], point.Y - now[1], point.Z - now[2] };
            double[] wantRate = { deriv.X - rate[0], deriv.Y - rate[1], deriv.Z - rate[2] };
            double g00 = 0.0, g01 = 0.0, g11 = 0.0;

            for (int j = 0; j <= p; j++)
            {
                g00 += r[j] * r[j];
                g01 += r[j] * rr[j];
                g11 += rr[j] * rr[j];
            }

            double det = g00 * g11 - g01 * g01;
            bool both = Math.Abs(det) > 1E-12 * Math.Max(1.0, g00 * g11);

            for (int j = 0; j <= p; j++)
            {
                double[] q = pw[span - p + j];
                double w = q[3];
                var moved = new double[4];
                moved[3] = w;

                for (int c = 0; c < 3; c++)
                {
                    double change;

                    if (both)
                    {
                        double l0 = (g11 * wantPoint[c] - g01 * wantRate[c]) / det;
                        double l1 = (g00 * wantRate[c] - g01 * wantPoint[c]) / det;
                        change = r[j] * l0 + rr[j] * l1;
                    }
                    else
                    {
                        change = r[j] * wantPoint[c] / g00;
                    }

                    moved[c] = (q[c] / w + change) * w;
                }

                pw[span - p + j] = moved;
            }

            WriteClamped(p, u, pw);
        }

        /// <summary>
        /// Gives the spline another degree and count of control points that run as near the curve as they can, as AutoCAD's
        /// <c>Rebuild</c>: the spline of least squares through points of the curve taken at even steps of its parameter (ten for
        /// each control point and at least 200), each fitted at its own parameter, so that the ends are where they were and the
        /// domain the same; not rational, its fit data taken out. A cubic 13 long of 5 points with an inner knot at 0.49, rebuilt
        /// as a cubic of 20 points, keeps within 0.003 of the old curve. The least squares are solved over the band of their
        /// equations, a point of the curve lying under degree + 1 control points: a spline of 1 000 spans is rebuilt of 5 000
        /// points in 0.05 s.
        /// </summary>
        /// <param name="degree">The degree, from 1 to 10.</param>
        /// <param name="numCtrlPts">The count of control points, the degree and one at least.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the degree is outside 1 to 10, or the count below the degree and one.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no data that make a spline, or is erased.</exception>
        public void Rebuild(int degree, int numCtrlPts)
        {
            if (degree < 1 || degree > MostDegree)
            {
                throw new ArgumentOutOfRangeException(nameof(degree), degree, "A spline is rebuilt of a degree from 1 to 10.");
            }

            if (numCtrlPts < degree + 1)
            {
                throw new ArgumentOutOfRangeException(nameof(numCtrlPts), numCtrlPts, "A spline of degree " + degree.ToString(CultureInfo.InvariantCulture) + " has " + (degree + 1).ToString(CultureInfo.InvariantCulture) + " control points at least.");
            }

            DxfNurbs nurbs = Nurbs();
            double start = nurbs.Start;
            double end = nurbs.End;
            int samples = Math.Max(200, 10 * numCtrlPts);
            var q = new List<GeoPoint3>(samples + 1);
            var ub = new List<double>(samples + 1);

            if (!(nurbs.LooseLength > 0.0))
            {
                throw new InvalidOperationException("The spline stands still: it has no length to be rebuilt along.");
            }

            // The points of the curve at even steps of its own parameter, each fitted at that parameter, so that the new
            // spline runs over the old domain as the old one does.
            for (int k = 0; k <= samples; k++)
            {
                double t = k == samples ? end : start + (end - start) * k / samples;
                q.Add(nurbs.PointAt(t));
                ub.Add(t);
            }

            DxfNurbsEdit.LeastSquares(q, ub, degree, numCtrlPts, out List<double> knots, out List<GeoPoint3> points);
            bool closed = Closed && points[0].IsEqualTo(points[points.Count - 1], Tolerance.Default);
            SetNurbsData(degree, false, closed, false, points, knots, null);
        }

        /// <summary>
        /// Whether the spline runs farther than a distance: its length, measured span by span to 1E-9 of a unit, above it, as a
        /// polyline's is judged; not measured where its control points spread wider than the distance already. For telling a
        /// curve projected to a point, with no walk along it to the bit: a zigzag within a box 0.0006 across can run 0.008.
        /// </summary>
        internal bool RunsFartherThan(double distance)
        {
            DxfNurbs nurbs = Nurbs();
            return !nurbs.SpreadWithin(distance) || nurbs.LooseLength > distance;
        }

        /// <summary>
        /// Gets the polyline that runs along the spline, as AutoCAD's <c>ToPolyline</c>: as <see cref="ToPolylineWithPrecision(int, bool, bool)"/>
        /// of precision 10, of arcs, a <see cref="DxfPolyline"/> where the spline is flat.
        /// </summary>
        /// <returns>The polyline, of the spline's look, in no file.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no data that make a spline.</exception>
        public DxfCurve ToPolyline() => ToPolylineWithPrecision(10, true, true);

        /// <summary>Gets the polyline that runs along the spline, as AutoCAD's <c>ToPolyline</c>, as <see cref="ToPolylineWithPrecision(int, bool, bool)"/> of precision 10.</summary>
        /// <param name="bConvertAsArcs">Whether its segments are arcs, each through the spline's point halfway along it; lines where false, and for a spline that is not flat.</param>
        /// <param name="bToLWPolyline">Whether a flat spline gives a <see cref="DxfPolyline"/>; a <see cref="DxfPolyline2d"/> where false.</param>
        /// <returns>The polyline, of the spline's look, in no file.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no data that make a spline.</exception>
        public DxfCurve ToPolyline(bool bConvertAsArcs, bool bToLWPolyline) => ToPolylineWithPrecision(10, bConvertAsArcs, bToLWPolyline);

        /// <summary>Gets the polyline of a count of vertices that runs along the spline, as AutoCAD's <c>ToPolyline</c>: of arcs, a <see cref="DxfPolyline"/> where the spline is flat.</summary>
        /// <param name="numOfVertices">How many vertices, 2 or more, set evenly along the spline's length.</param>
        /// <returns>The polyline, of the spline's look, in no file.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the count is below 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no data that make a spline.</exception>
        public DxfCurve ToPolyline(int numOfVertices) => ToPolyline(numOfVertices, true, true);

        /// <summary>
        /// Gets the polyline of a count of vertices that runs along the spline, as AutoCAD's <c>ToPolyline</c>: the vertices set
        /// evenly along the spline's length, a closed spline's last on its first, which the closed polyline joins back to; each
        /// segment a line, or the arc through its ends and the spline's point halfway between them. A quarter of a circle of
        /// radius 100 made a spline, given 3 vertices and arcs, is two arcs of radius 100 to within the rounding.
        /// </summary>
        /// <param name="numOfVertices">How many vertices, 2 or more.</param>
        /// <param name="bConvertAsArcs">Whether its segments are arcs; lines where false, and for a spline that is not flat.</param>
        /// <param name="bToLWPolyline">Whether a flat spline gives a <see cref="DxfPolyline"/>; a <see cref="DxfPolyline2d"/> where false. A spline that is not flat gives a <see cref="DxfPolyline3d"/>.</param>
        /// <returns>The polyline, of the spline's look, in no file.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the count is below 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no data that make a spline.</exception>
        public DxfCurve ToPolyline(int numOfVertices, bool bConvertAsArcs, bool bToLWPolyline)
        {
            if (numOfVertices < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(numOfVertices), numOfVertices, "A polyline has two vertices at least.");
            }

            DxfNurbs nurbs = Nurbs();
            double length = nurbs.LooseLength;
            var parameters = new List<double>(numOfVertices);
            var middles = new List<double>(numOfVertices);

            for (int i = 0; i < numOfVertices; i++)
            {
                parameters.Add(i == 0 ? nurbs.Start : i == numOfVertices - 1 ? nurbs.End : nurbs.ParameterAtLooseLength(length * i / (numOfVertices - 1)));

                if (i > 0)
                {
                    middles.Add(nurbs.ParameterAtLooseLength(length * (i - 0.5) / (numOfVertices - 1)));
                }
            }

            return Polyline(nurbs, parameters, middles, bConvertAsArcs, bToLWPolyline);
        }

        /// <summary>Gets the polyline that runs along the spline within a precision, as AutoCAD's <c>ToPolylineWithPrecision</c>, of arcs, a <see cref="DxfPolyline"/> where the spline is flat.</summary>
        /// <param name="precision">The precision, from 0 to 99, as <see cref="ToPolylineWithPrecision(int, bool, bool)"/> reads it.</param>
        /// <returns>The polyline, of the spline's look, in no file.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the precision is outside 0 to 99.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no data that make a spline.</exception>
        public DxfCurve ToPolylineWithPrecision(int precision) => ToPolylineWithPrecision(precision, true, true);

        /// <summary>
        /// Gets the polyline that runs along the spline within a precision, as AutoCAD's <c>ToPolylineWithPrecision</c>: each
        /// span of the knots halved until no point of the spline between a segment's ends is farther from it than the
        /// spline's size (the diagonal of the box of its control points) times 1E-3 times 10 to the power of minus a tenth of
        /// the precision, so that precision 0 keeps within a thousandth of the size and each 10 more within a tenth of that,
        /// down to a floor of the larger of 1E-6 times the size and 1E-9 times one more than the largest coordinate, which
        /// precisions above 30 reach, so that every precision from about 30 up gives the same polyline (precision 99 keeps a
        /// spline 10 000 across within 0.01, not 1E-10 of it); each segment a
        /// line, or the arc through its ends and the spline's point halfway between them. How far a segment strays is judged
        /// at three points of the spline between its ends, a quarter, half and three quarters of the way along its parameter
        /// (a quarter and three quarters for an arc, which passes the half), not everywhere. A spline 10 000 across made a
        /// polyline of precision 10 strays about 1 from it at most. The way AutoCAD reads a precision is not known: this is the
        /// library's reading.
        /// </summary>
        /// <param name="precision">The precision, from 0 to 99.</param>
        /// <param name="bConvertAsArcs">Whether its segments are arcs; lines where false, and for a spline that is not flat.</param>
        /// <param name="bToLWPolyline">Whether a flat spline gives a <see cref="DxfPolyline"/>; a <see cref="DxfPolyline2d"/> where false. A spline that is not flat gives a <see cref="DxfPolyline3d"/>.</param>
        /// <returns>The polyline, of the spline's look, in no file.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the precision is outside 0 to 99.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no data that make a spline.</exception>
        public DxfCurve ToPolylineWithPrecision(int precision, bool bConvertAsArcs, bool bToLWPolyline)
        {
            if (precision < 0 || precision > 99)
            {
                throw new ArgumentOutOfRangeException(nameof(precision), precision, "A precision is from 0 to 99.");
            }

            DxfNurbs nurbs = Nurbs();
            IReadOnlyList<GeoPoint3> control = NurbsData.GetControlPoints();
            GeoAabb3 box = Box(control);
            double size = box.Min.DistanceTo(box.Max);
            double largest = Math.Max(Math.Max(Math.Max(Math.Abs(box.Min.X), Math.Abs(box.Max.X)), Math.Max(Math.Abs(box.Min.Y), Math.Abs(box.Max.Y))), Math.Max(Math.Abs(box.Min.Z), Math.Abs(box.Max.Z)));
            double floor = Math.Max(1E-6 * size, 1E-9 * (1.0 + largest));
            double tolerance = Math.Max(floor, size * 1E-3 * Math.Pow(10.0, -precision / 10.0));
            bool arcs = bConvertAsArcs && DxfCurveTools.TryPlane(control, Direction(210), out _);
            var parameters = new List<double> { nurbs.Start };
            var middles = new List<double>();
            IReadOnlyList<double> knots = NurbsData.GetKnots();
            double from = nurbs.Start;

            foreach (double knot in knots)
            {
                if (knot > from && knot <= nurbs.End)
                {
                    Divide(nurbs, from, knot, arcs, tolerance, 0, parameters, middles);
                    from = knot;
                }
            }

            return Polyline(nurbs, parameters, middles, arcs, bToLWPolyline);
        }

        /// <summary>
        /// Gets the pieces the spline is cut into at parameters, as AutoCAD's <c>GetSplitCurves</c>: splines of the same look in
        /// no file, each the part of the spline between two cuts exactly, its knots those of the spline there with the cut
        /// standing as many times as the degree and one at its ends; an open spline from its start to the first parameter, on
        /// to the next and to its end, a closed one from each to the next and from the last on round its start to the first,
        /// given whole where it is cut at fewer than two places. A parameter at an end or within the point tolerance along the
        /// spline of another cuts nothing; the fit data are not carried.
        /// </summary>
        /// <param name="parameters">The knot values to cut at, from <see cref="DxfCurve.StartParam"/> to <see cref="DxfCurve.EndParam"/>.</param>
        /// <returns>The pieces, in the order of the spline.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameters are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a parameter is past either end.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no data that make a spline.</exception>
        public override IReadOnlyList<DxfCurve> GetSplitCurves(IReadOnlyList<double> parameters)
        {
            Clamped(out int p, out List<double> u, out List<double[]> pw);
            DxfNurbs nurbs = Nurbs();
            double start = u[p];
            double end = u[pw.Count];
            bool closed = Closed && StartPoint.IsEqualTo(EndPoint, Tolerance.Default);
            List<double> cuts = DxfCurveTools.Cuts(parameters, start, end, closed, nurbs.LengthBetween, x => Within(x - start, end - start, nameof(parameters)));
            var pieces = new List<KeyValuePair<List<double>, List<double[]>>>();

            if (closed)
            {
                if (cuts.Count < 2)
                {
                    return new DxfCurve[] { (DxfSpline)Copy() };
                }

                var parts = Cut(p, u, pw, cuts);

                if (cuts[0] == start)
                {
                    // Cut at the start too: the pieces meet there, and none runs round it.
                    pieces.AddRange(parts);
                }
                else
                {
                    // The last part runs on round the start into the first: joined at the start, the curve's own point.
                    KeyValuePair<List<double>, List<double[]>> round = Join(p, parts[parts.Count - 1], parts[0]);

                    for (int i = 1; i < parts.Count - 1; i++)
                    {
                        pieces.Add(parts[i]);
                    }

                    pieces.Add(round);
                }
            }
            else
            {
                pieces.AddRange(Cut(p, u, pw, cuts));
            }

            // The first piece a copy of the spline, the others copies of that one, which holds no more than its piece: a spline of
            // a thousand spans cut a thousand times is not copied whole a thousand times.
            var result = new List<DxfCurve>(pieces.Count);
            DxfSpline first = null;

            foreach (KeyValuePair<List<double>, List<double[]>> piece in pieces)
            {
                var copy = first == null ? (DxfSpline)Copy() : (DxfSpline)first.Clone();
                copy.WriteClamped(p, piece.Key, piece.Value);
                first = first ?? copy;
                result.Add(copy);
            }

            return result;
        }

        /// <summary>
        /// Gets the spline at a distance from this one in its plane, as AutoCAD's <c>GetOffsetCurves</c>: to the right of its
        /// direction seen from the tip of its normal where the distance is above nought, outwards for a spline running
        /// counter-clockwise, as <see cref="GetOffsetCurvesGivenPlaneNormal"/> offsets it about that normal. The normal is the
        /// spline's own (210) where it lies square to it, else that of the plane it lies in.
        /// </summary>
        /// <param name="offsetDist">The distance.</param>
        /// <returns>The one spline, of the same look, in no file.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline does not lie in one plane, or has no data that make a spline.</exception>
        public override IReadOnlyList<DxfCurve> GetOffsetCurves(double offsetDist)
        {
            if (!DxfCurveTools.TryPlane(NurbsData.GetControlPoints(), Direction(210), out GeoVector3 normal))
            {
                throw new InvalidOperationException("A spline that does not lie in one plane has no side to be offset to: give the plane's normal.");
            }

            return Offset(normal, offsetDist);
        }

        /// <summary>
        /// Gets the spline at a distance from this one in the plane of a normal, as AutoCAD's
        /// <c>GetOffsetCurvesGivenPlaneNormal</c>: each point moved square to the spline's direction there within the plane, to
        /// the right of that direction seen from the tip of the normal where the distance is above nought, and a cubic fitted to
        /// the points so moved. The spline is cut at its corners, knots standing as many times as its degree where its direction
        /// turns (the knots of a circle's spline, where it runs on smoothly, are none), and each smooth
        /// piece fitted on its own: through points of the true offset, four to a span of the knots at first and twice as many
        /// in a span, up to 512, until the fit keeps within the point tolerance (1E-3) of the true offset both ways halfway
        /// between each two points, leaving and reaching the piece's ends in the spline's own directions there. The pieces are
        /// joined as a polyline's offsets are: run on along their ends to where those meet past a corner they part at, cut
        /// back to where they cross at one they overlap at, so that the L of degree 2 from (0, 0) to (10, 0) to (10, 10) offset
        /// by 1 has its corner at (11, -1). A spline with no corner gives a cubic over its own domain; one with corners, a cubic
        /// over the length along it from the spline's start. A span that does not keep within the tolerance at 512 points is
        /// given as fitted, and where the spline bends tighter than the distance the offset folds back on itself and is given as
        /// it folds.
        /// </summary>
        /// <param name="planeNormal">The normal of the plane, square to the spline.</param>
        /// <param name="offsetDist">The distance.</param>
        /// <returns>The one spline, of the same look, in no file; a copy for a distance of nought.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not finite.</exception>
        /// <exception cref="ArgumentException">Thrown when the normal has no length, or the spline does not lie in a plane square to it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no data that make a spline.</exception>
        public override IReadOnlyList<DxfCurve> GetOffsetCurvesGivenPlaneNormal(GeoVector3 planeNormal, double offsetDist)
        {
            if (!planeNormal.IsValid || !(planeNormal.Length > 0.0) || !DxfCurveTools.TryPlane(NurbsData.GetControlPoints(), planeNormal, out GeoVector3 normal) || !normal.IsParallelTo(planeNormal, Tolerance.Default))
            {
                throw new ArgumentException("A spline is offset in a plane it lies in: the normal " + planeNormal + " is not square to it.", nameof(planeNormal));
            }

            return Offset(planeNormal.Normalize(), offsetDist);
        }

        /// <summary>
        /// Runs the spline on past one of its ends to a parameter, as AutoCAD's <c>Extend</c>: the polynomial of its end span
        /// carried on, so that the spline keeps its curve and runs on as smoothly as it ran; below
        /// <see cref="DxfCurve.StartParam"/> it starts earlier, above <see cref="DxfCurve.EndParam"/> it ends later. The fit
        /// data are taken out.
        /// </summary>
        /// <param name="newParameter">The knot value to reach, below the start or above the end.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter lies on the spline, or is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline is closed, which has no end, has no data that make a spline, is erased, or is rational and a weight would reach nought so far on ("cannot run on: a weight would reach nought").</exception>
        public override void Extend(double newParameter)
        {
            Guard.Finite(newParameter, nameof(newParameter), "A parameter has to be a finite number.");
            Clamped(out int p, out List<double> u, out List<double[]> pw);
            Open();

            if (newParameter >= u[p] && newParameter <= u[pw.Count])
            {
                throw new ArgumentOutOfRangeException(nameof(newParameter), newParameter, "The parameter lies on the spline: one past an end extends it.");
            }

            DxfNurbsEdit.Extend(p, u, pw, newParameter);
            WriteClamped(p, u, pw);
        }

        /// <summary>
        /// Runs the spline on past one of its ends to a point on its run carried on, as AutoCAD's <c>Extend</c>: as
        /// <see cref="Extend(double)"/> to the parameter of the point there, found by Newton's steps along the polynomial of
        /// the end span.
        /// </summary>
        /// <param name="extendStart">Whether the start is moved back, else the end on.</param>
        /// <param name="toPoint">The point, within the point tolerance of the spline run on past that end.</param>
        /// <exception cref="ArgumentException">Thrown when the point is not on the spline run on, or is on the spline itself.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline is closed, has no data that make a spline, is erased, or is rational and a weight would reach nought so far on.</exception>
        public override void Extend(bool extendStart, GeoPoint3 toPoint)
        {
            if (!toPoint.IsValid)
            {
                throw new ArgumentException("A point has to be of finite numbers: " + toPoint + " is not.", nameof(toPoint));
            }

            Clamped(out int p, out List<double> u, out List<double[]> pw);
            Open();
            double end = extendStart ? u[0] : u[u.Count - 1];
            double inner = end;

            foreach (double knot in extendStart ? (IEnumerable<double>)u : Reversed(u))
            {
                if (knot != end)
                {
                    inner = knot;
                    break;
                }
            }

            var trial = new List<double>(u);
            var points = new List<double[]>(pw);

            if (DxfNurbsEdit.Multiplicity(trial, inner) < p && inner != trial[0] && inner != trial[trial.Count - 1])
            {
                DxfNurbsEdit.InsertKnot(p, trial, points, inner, p - DxfNurbsEdit.Multiplicity(trial, inner));
            }

            var bezier = new double[p + 1][];

            for (int j = 0; j <= p; j++)
            {
                bezier[j] = extendStart ? points[p - j] : points[points.Count - 1 - p + j];
            }

            double s = Nearest(bezier, toPoint, out GeoPoint3 nearest);

            if (!nearest.IsEqualTo(toPoint, Tolerance.Default))
            {
                throw new ArgumentException("The point " + toPoint + " is not on the spline run on past its " + (extendStart ? "start" : "end") + ".", nameof(toPoint));
            }

            if (!(s > 1.0))
            {
                throw new ArgumentException("The point " + toPoint + " is on the spline itself, not past its " + (extendStart ? "start" : "end") + ".", nameof(toPoint));
            }

            DxfNurbsEdit.Extend(p, u, pw, inner + s * (end - inner));
            WriteClamped(p, u, pw);
        }

        /// <summary>The spline's data made clamped over its domain, its control points in homogeneous form.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no data that make a spline.</exception>
        internal void Clamped(out int p, out List<double> u, out List<double[]> pw)
        {
            DxfNurbsData data = NurbsData;

            if (data.GetControlPoints().Count == 0)
            {
                throw new InvalidOperationException("The SPLINE has no control points, and no fit points the reader passes a spline through.");
            }

            bool rational = data.Rational && data.GetWeights().Count > 0;
            Nurbs();
            p = data.Degree;
            u = new List<double>(data.GetKnots());
            pw = DxfNurbsEdit.Homogeneous(data.GetControlPoints(), rational ? data.GetWeights() : null);
            DxfNurbsEdit.Clamp(p, u, pw);
        }

        /// <summary>
        /// Writes clamped data in homogeneous form as the spline's, as <see cref="SetNurbsData"/> writes it: rational where a
        /// weight is not 1, closed where it was and still starts where it ends, periodic no longer.
        /// </summary>
        private void WriteClamped(int p, List<double> u, List<double[]> pw)
        {
            DxfNurbsEdit.Split(pw, out List<GeoPoint3> points, out List<double> weights);
            bool rational = weights.Exists(w => w != 1.0);
            bool closed = Closed && points[0].IsEqualTo(points[points.Count - 1], Tolerance.Default);
            SetNurbsData(p, rational, closed, false, points, u, rational ? weights : null);
        }

        /// <summary>
        /// Writes clamped data in homogeneous form as the spline's, as <see cref="WriteClamped"/> writes it, closed or not as
        /// given; a spline the record says is planar (flag 8 of group 70) and is no longer has that flag and its normal taken out,
        /// and one it says is linear (flag 16) and is no longer, that flag.
        /// </summary>
        internal void TakeJoined(int p, List<double> u, List<double[]> pw, bool closed, bool planar, bool linear)
        {
            DxfNurbsEdit.Split(pw, out List<GeoPoint3> points, out List<double> weights);
            bool rational = weights.Exists(w => w != 1.0);
            SetNurbsData(p, rational, closed, false, points, u, rational ? weights : null);
            int flags = GetInt(70, 0);
            int kept = flags & ~(planar ? 0 : 8) & ~(linear ? 0 : 16);

            if (kept != flags)
            {
                var groups = new List<KeyValuePair<int, string>> { new KeyValuePair<int, string>(70, kept.ToString(CultureInfo.InvariantCulture)) };

                if ((kept & 8) == 0 && (flags & 8) != 0)
                {
                    groups.AddRange(new[] { new KeyValuePair<int, string>(210, null), new KeyValuePair<int, string>(220, null), new KeyValuePair<int, string>(230, null) });
                }

                SetGroups(groups.ToArray());
            }
        }

        /// <summary>The fit points, refused where the spline has none.</summary>
        private List<GeoPoint3> FitList()
        {
            if (!HasFitData)
            {
                throw new InvalidOperationException("The spline has no fit points: it is given by its control points alone.");
            }

            return new List<GeoPoint3>(FitPoints);
        }

        /// <summary>Fits the spline anew through fit points, its other fit data kept.</summary>
        private void Refit(List<GeoPoint3> fit, string parameter)
        {
            DxfFitData data = FitData;
            WriteFit(fit, data.Degree, data.FitTolerance, data.TangentsExist, data.StartTangent, data.EndTangent, data.Periodic || Closed, parameter);
        }

        /// <summary>Writes fit data and the control points of the spline through them, as <see cref="FitData"/> says.</summary>
        private void WriteFit(IReadOnlyList<GeoPoint3> fit, int degree, double fitTolerance, bool tangents, GeoVector3 startTangent, GeoVector3 endTangent, bool closed, string parameter)
        {
            if (degree < 1 || degree > MostDegree)
            {
                throw new ArgumentOutOfRangeException(parameter, degree, "A spline is fitted of a degree from 1 to 10.");
            }

            if (fit.Count < 2)
            {
                throw new ArgumentException("A spline is fitted through two points at least, and was given " + fit.Count.ToString(CultureInfo.InvariantCulture) + ".", parameter);
            }

            foreach (GeoPoint3 point in fit)
            {
                if (!point.IsValid)
                {
                    throw new ArgumentException("A fit point has to be of finite numbers: " + point + " is not.", parameter);
                }
            }

            bool hasStart = tangents && startTangent.IsValid && startTangent.Length > 0.0;
            bool hasEnd = tangents && endTangent.IsValid && endTangent.Length > 0.0;

            if (tangents && (!startTangent.IsValid || !endTangent.IsValid))
            {
                throw new ArgumentException("A tangent has to be of finite numbers.", parameter);
            }

            var through = new List<GeoPoint3>(fit);
            GeoVector3 first = hasStart ? startTangent : default(GeoVector3);
            GeoVector3 last = hasEnd ? endTangent : default(GeoVector3);

            if (closed && fit.Count > 2)
            {
                if (!through[0].IsEqualTo(through[through.Count - 1], Tolerance.Default))
                {
                    through.Add(through[0]);
                }

                // Round the seam the spline runs on as from the point before to the point after.
                GeoVector3 seam = through[through.Count - 2].GetVectorTo(through[1]);

                if (!hasStart && !hasEnd && seam.Length > 0.0)
                {
                    first = seam;
                    last = seam;
                }
            }

            bool ends = first.Length > 0.0 && last.Length > 0.0;
            int distinct = Distinct(through);

            if (distinct < 2)
            {
                throw new ArgumentException("The fit points are all one point, and a spline through them has no length.", parameter);
            }

            int used = ends ? Math.Max(2, Math.Min(degree, distinct + 1)) : Math.Min(degree, distinct - 1);
            GeoSpline3 spline = ends
                ? GeoSpline3.Interpolate(through, first, last, used, Tolerance.Default)
                : GeoSpline3.Interpolate(through, used, Tolerance.Default);
            double chord = 0.0;

            for (int i = 1; i < through.Count; i++)
            {
                chord += through[i - 1].DistanceTo(through[i]);
            }

            var knots = new List<double>(spline.Knots.Count);

            foreach (double knot in spline.Knots)
            {
                knots.Add(knot * chord);
            }

            List<List<KeyValuePair<int, string>>> was = Points(11);
            var written = new List<List<KeyValuePair<int, string>>>(fit.Count);

            for (int i = 0; i < fit.Count; i++)
            {
                written.Add(Point(11, fit[i], i < was.Count ? was[i] : null, parameter));
            }

            bool closedCurve = closed && spline.StartPoint.IsEqualTo(spline.EndPoint, Tolerance.Default);
            SetNurbsData(used, false, closedCurve, false, spline.ControlPoints, knots, null);
            WritePoints(11, FitCodes, written, 74);
            var groups = new List<KeyValuePair<int, string>>();

            if (hasStart)
            {
                groups.AddRange(TangentGroups(12, startTangent));
            }

            if (hasEnd)
            {
                groups.AddRange(TangentGroups(13, endTangent));
            }

            SetGroups(groups.ToArray());
            SetDouble(44, fitTolerance, 0.0);
        }

        /// <summary>
        /// Writes the points the spline was fitted through and its tangents at the ends (11 with their count 74, 12 and 13 of
        /// unit length) and the fit tolerance 1E-10 (44), its control points and knots kept as they are, as AutoCAD 2021's
        /// <c>Explode</c> gives the spline of a splined leader: the three vertices of the leader through (150, 0), (160, 10)
        /// and (170, 10), the first moved to the arrow's back, leaving along (0.7071, 0.7071) and reaching along (1, 0).
        /// </summary>
        internal void KeepFit(IReadOnlyList<GeoPoint3> fit, GeoVector3 startTangent, GeoVector3 endTangent)
        {
            var written = new List<List<KeyValuePair<int, string>>>(fit.Count);

            foreach (GeoPoint3 point in fit)
            {
                written.Add(Point(11, point, null, nameof(fit)));
            }

            WritePoints(11, FitCodes, written, 74);
            var groups = new List<KeyValuePair<int, string>>();
            groups.AddRange(TangentGroups(12, startTangent));
            groups.AddRange(TangentGroups(13, endTangent));
            SetGroups(groups.ToArray());
            SetDouble(44, 1E-10, 0.0);
        }

        /// <summary>The groups of a tangent at an end (12 or 13 and the codes 10 and 20 above), of unit length.</summary>
        private KeyValuePair<int, string>[] TangentGroups(int code, GeoVector3 tangent)
        {
            if (!(tangent.Length > 0.0))
            {
                return new[] { new KeyValuePair<int, string>(code, null), new KeyValuePair<int, string>(code + 10, null), new KeyValuePair<int, string>(code + 20, null) };
            }

            GeoVector3 unit = tangent.Normalize();
            return PointGroups(code, new GeoPoint3(unit.X, unit.Y, unit.Z), new GeoPoint3(0.0, 0.0, 0.0));
        }

        /// <summary>The spline at a distance in the plane of a normal, fitted as <see cref="GetOffsetCurvesGivenPlaneNormal"/> says.</summary>
        private IReadOnlyList<DxfCurve> Offset(GeoVector3 normal, double distance)
        {
            Guard.Finite(distance, nameof(distance), "An offset has to be a finite number.");
            DxfNurbs nurbs = Nurbs();
            var copy = (DxfSpline)Copy();

            if (distance == 0.0)
            {
                return new DxfCurve[] { copy };
            }

            DxfNurbsData data = NurbsData;
            bool closed = Closed && nurbs.PointAt(nurbs.Start).IsEqualTo(nurbs.PointAt(nurbs.End), Tolerance.Default);
            DxfSplineOffset.Offset(nurbs, data.GetKnots(), data.Degree, closed, normal, distance, out List<GeoPoint3> points, out List<double> knots, out bool closedOut);
            copy.SetNurbsData(3, false, closedOut, false, points, knots, null);
            return new DxfCurve[] { copy };
        }

        /// <summary>Refuses a closed spline, which has no end to be extended.</summary>
        private void Open()
        {
            if (Closed || IsPeriodic)
            {
                throw new InvalidOperationException("A closed spline has no end to be extended.");
            }
        }

        /// <summary>
        /// The share along a Bézier piece of homogeneous points (0 its first, 1 its last, past 1 run on) of its point nearest a
        /// point, from past its last by Newton's steps on the distance squared; and that point.
        /// </summary>
        private static double Nearest(double[][] bezier, GeoPoint3 target, out GeoPoint3 nearest)
        {
            GeoPoint3 Point(double s)
            {
                double[] q = DxfNurbsEdit.BezierAt(bezier, s);
                return new GeoPoint3(q[0] / q[3], q[1] / q[3], q[2] / q[3]);
            }

            GeoPoint3 end = Point(1.0);
            double h = 1E-6;
            GeoVector3 speed = Point(1.0 - h).GetVectorTo(end).Divide(h);
            double s = speed.Length > 0.0 ? 1.0 + end.DistanceTo(target) / speed.Length : 1.0;

            for (int i = 0; i < 100; i++)
            {
                GeoPoint3 c = Point(s);
                GeoPoint3 cp = Point(s + h);
                GeoPoint3 cm = Point(s - h);
                GeoVector3 d1 = cm.GetVectorTo(cp).Divide(2.0 * h);
                GeoVector3 d2 = new GeoVector3(cp.X - 2.0 * c.X + cm.X, cp.Y - 2.0 * c.Y + cm.Y, cp.Z - 2.0 * c.Z + cm.Z).Divide(h * h);
                GeoVector3 r = target.GetVectorTo(c);
                double f = r.DotProduct(d1);
                double g = d1.DotProduct(d1) + r.DotProduct(d2);

                if (!(g > 0.0))
                {
                    break;
                }

                double next = s - f / g;

                if (Math.Abs(next - s) <= 1E-14 * Math.Max(1.0, Math.Abs(s)))
                {
                    s = next;
                    break;
                }

                s = next;
            }

            nearest = Point(s);
            return s;
        }

        /// <summary>The knots in the other order.</summary>
        private static IEnumerable<double> Reversed(List<double> u)
        {
            for (int i = u.Count - 1; i >= 0; i--)
            {
                yield return u[i];
            }
        }

        /// <summary>Cuts clamped data at parameters inside it, in increasing order: each piece's knots and homogeneous points.</summary>
        private static List<KeyValuePair<List<double>, List<double[]>>> Cut(int p, List<double> u, List<double[]> pw, List<double> cuts)
        {
            var pieces = new List<KeyValuePair<List<double>, List<double[]>>>();
            var knots = new List<double>(u);
            var points = new List<double[]>(pw);

            foreach (double cut in cuts)
            {
                if (!(cut > knots[0] && cut < knots[knots.Count - 1]))
                {
                    continue;
                }

                int times = p - DxfNurbsEdit.Multiplicity(knots, cut);

                if (times > 0)
                {
                    DxfNurbsEdit.InsertKnot(p, knots, points, cut, times);
                }

                int r = knots.IndexOf(cut);
                var left = knots.GetRange(0, r + p);
                left.Add(cut);
                var right = new List<double> { cut };
                right.AddRange(knots.GetRange(r, knots.Count - r));
                pieces.Add(new KeyValuePair<List<double>, List<double[]>>(left, points.GetRange(0, r)));
                knots = right;
                points = points.GetRange(r - 1, points.Count - r + 1);
            }

            pieces.Add(new KeyValuePair<List<double>, List<double[]>>(knots, points));
            return pieces;
        }

        /// <summary>Two clamped pieces of one degree joined where the first ends and the second starts, the second's knots moved on to follow the first's and its weights scaled to meet.</summary>
        private static KeyValuePair<List<double>, List<double[]>> Join(int p, KeyValuePair<List<double>, List<double[]>> a, KeyValuePair<List<double>, List<double[]>> b)
        {
            List<double> ua = a.Key;
            List<double> ubk = b.Key;
            double end = ua[ua.Count - 1];
            double shift = end - ubk[0];
            var knots = ua.GetRange(0, ua.Count - (p + 1));

            for (int i = 0; i < p; i++)
            {
                knots.Add(end);
            }

            for (int i = p + 1; i < ubk.Count; i++)
            {
                knots.Add(ubk[i] + shift);
            }

            var points = new List<double[]>(a.Value);
            double scale = a.Value[a.Value.Count - 1][3] / b.Value[0][3];

            for (int i = 1; i < b.Value.Count; i++)
            {
                double[] q = b.Value[i];
                points.Add(new[] { q[0] * scale, q[1] * scale, q[2] * scale, q[3] * scale });
            }

            return new KeyValuePair<List<double>, List<double[]>>(knots, points);
        }

        /// <summary>Halves a stretch of the parameter until its segment keeps within a tolerance of the spline, adding the ends of the segments.</summary>
        private static void Divide(DxfNurbs nurbs, double a, double b, bool arcs, double tolerance, int depth, List<double> parameters, List<double> middles)
        {
            double m = 0.5 * (a + b);
            GeoPoint3 pa = nurbs.PointAt(a);
            GeoPoint3 pb = nurbs.PointAt(b);
            GeoPoint3 pm = nurbs.PointAt(m);
            bool near = depth >= 24;

            if (!near)
            {
                near = true;

                foreach (double share in new[] { 0.25, 0.5, 0.75 })
                {
                    if (arcs && share == 0.5)
                    {
                        continue;
                    }

                    GeoPoint3 q = nurbs.PointAt(a + (b - a) * share);
                    double gap = arcs ? ArcGap(pa, pm, pb, q) : new GeoLine3(pa, pb).GetClosestPointOnBoundary(q).DistanceTo(q);

                    if (!(gap <= tolerance))
                    {
                        near = false;
                        break;
                    }
                }
            }

            if (near)
            {
                parameters.Add(b);
                middles.Add(m);
                return;
            }

            Divide(nurbs, a, m, arcs, tolerance, depth + 1, parameters, middles);
            Divide(nurbs, m, b, arcs, tolerance, depth + 1, parameters, middles);
        }

        /// <summary>How far a point is from the circle through three points, or from the line through the outer two where they are on one.</summary>
        private static double ArcGap(GeoPoint3 a, GeoPoint3 m, GeoPoint3 b, GeoPoint3 q)
        {
            GeoVector3 ab = a.GetVectorTo(b);
            GeoVector3 am = a.GetVectorTo(m);
            GeoVector3 n = ab.CrossProduct(am);
            double nn = n.LengthSquared;

            if (!(nn > 1E-24 * ab.LengthSquared * am.LengthSquared))
            {
                return new GeoLine3(a, b).GetClosestPointOnBoundary(q).DistanceTo(q);
            }

            // The centre of the circle through a, m and b.
            GeoVector3 c = n.CrossProduct(ab).Multiply(am.LengthSquared).Add(am.CrossProduct(n).Multiply(ab.LengthSquared)).Divide(2.0 * nn);
            GeoPoint3 centre = a.Add(c);
            return Math.Abs(centre.DistanceTo(q) - c.Length);
        }

        /// <summary>The polyline through the spline's points at parameters, each segment a line or the arc through the point at its middle parameter.</summary>
        private DxfCurve Polyline(DxfNurbs nurbs, List<double> parameters, List<double> middles, bool arcs, bool lightweight)
        {
            var world = new List<GeoPoint3>(parameters.Count);

            foreach (double t in parameters)
            {
                world.Add(nurbs.PointAt(t));
            }

            bool closed = Closed && world[0].IsEqualTo(world[world.Count - 1], Tolerance.Default) && world.Count > 2;
            bool flat = DxfCurveTools.TryPlane(NurbsData.GetControlPoints(), Direction(210), out GeoVector3 normal);
            int count = closed ? world.Count - 1 : world.Count;
            DxfCurve result;

            if (!flat)
            {
                var points = world.GetRange(0, count);
                result = new DxfPolyline3d(DxfPoly3dType.SimplePoly, points, closed);
            }
            else
            {
                var plane = new List<GeoPoint2>(count);
                var bulges = new List<double>(count);
                double elevation = WorldToOcs(normal, world[0]).Z;

                for (int i = 0; i < count; i++)
                {
                    GeoPoint3 a = WorldToOcs(normal, world[i]);
                    plane.Add(new GeoPoint2(a.X, a.Y));

                    if (arcs && i < middles.Count)
                    {
                        GeoPoint3 m = WorldToOcs(normal, nurbs.PointAt(middles[i]));
                        GeoPoint3 b = WorldToOcs(normal, world[i + 1]);
                        bulges.Add(DxfCurveTools.Bulge(a.X, a.Y, m.X, m.Y, b.X, b.Y));
                    }
                    else
                    {
                        bulges.Add(0.0);
                    }
                }

                if (lightweight)
                {
                    var polyline = new DxfPolyline();
                    polyline.Normal = normal;
                    polyline.Elevation = elevation;
                    polyline.SetVertices(plane, bulges, closed);
                    result = polyline;
                }
                else
                {
                    var points = new List<GeoPoint3>(count);

                    foreach (GeoPoint2 q in plane)
                    {
                        points.Add(new GeoPoint3(q.X, q.Y, elevation));
                    }

                    var polyline = new DxfPolyline2d(DxfPoly2dType.SimplePoly, points, elevation, closed, 0.0, 0.0, bulges);
                    polyline.Normal = normal;
                    result = polyline;
                }
            }

            result.SetPropertiesFrom(this);
            return result;
        }

        /// <summary>The box of points.</summary>
        private static GeoAabb3 Box(IReadOnlyList<GeoPoint3> points)
        {
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity, minZ = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity, maxZ = double.NegativeInfinity;

            foreach (GeoPoint3 p in points)
            {
                minX = Math.Min(minX, p.X);
                minY = Math.Min(minY, p.Y);
                minZ = Math.Min(minZ, p.Z);
                maxX = Math.Max(maxX, p.X);
                maxY = Math.Max(maxY, p.Y);
                maxZ = Math.Max(maxZ, p.Z);
            }

            return new GeoAabb3(new GeoPoint3(minX, minY, minZ), new GeoPoint3(maxX, maxY, maxZ));
        }

        /// <summary>How many points are left when one within the point tolerance of the one before is dropped.</summary>
        private static int Distinct(List<GeoPoint3> points)
        {
            int count = 0;
            GeoPoint3 last = default(GeoPoint3);

            foreach (GeoPoint3 p in points)
            {
                if (count == 0 || !last.IsEqualTo(p, Tolerance.Default))
                {
                    count++;
                    last = p;
                }
            }

            return count;
        }

        /// <summary>Whether two NURBS data are the same, number for number to the bit.</summary>
        private static bool Same(DxfNurbsData a, DxfNurbsData b)
            => a.Degree == b.Degree && a.Rational == b.Rational && a.Closed == b.Closed && a.Periodic == b.Periodic
                && Same(a.ControlPointTolerance, b.ControlPointTolerance) && Same(a.KnotTolerance, b.KnotTolerance)
                && SamePoints(a.GetControlPoints(), b.GetControlPoints()) && SameNumbers(a.GetKnots(), b.GetKnots()) && SameNumbers(a.GetWeights(), b.GetWeights());

        /// <summary>Whether two fit data are the same, number for number to the bit.</summary>
        private static bool Same(DxfFitData a, DxfFitData b)
            => a.Degree == b.Degree && a.TangentsExist == b.TangentsExist && a.KnotParam == b.KnotParam && a.Periodic == b.Periodic
                && Same(a.FitTolerance, b.FitTolerance) && SamePoints(a.GetFitPoints(), b.GetFitPoints())
                && Same(a.StartTangent.X, b.StartTangent.X) && Same(a.StartTangent.Y, b.StartTangent.Y) && Same(a.StartTangent.Z, b.StartTangent.Z)
                && Same(a.EndTangent.X, b.EndTangent.X) && Same(a.EndTangent.Y, b.EndTangent.Y) && Same(a.EndTangent.Z, b.EndTangent.Z);
    }
}
