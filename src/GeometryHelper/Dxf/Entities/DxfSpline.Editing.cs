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
        // The most degree the library reads a spline of when it is fitted or rebuilt.
        private const int MostDegree = 10;

        // The most degree Elevate order reaches: its order of 26, as AutoCAD's SPLINEDIT takes at most.
        private const int MostElevatedDegree = 25;

        /// <summary>
        /// Gets or sets the NURBS data in one value, as AutoCAD's <c>NurbsData</c>: degree, flags, control points, knots,
        /// weights and the tolerances of the points (group 43) and knots (42). A spline given by its fit points alone gives
        /// the data of the spline <see cref="DxfReader"/> passes through them, the cubic AutoCAD fits; so does one whose fit
        /// points are not its control points, as AutoCAD reads it, the record's control points another curve.
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
                if (ReadsAsFit(out DxfNurbsData refit))
                {
                    return refit;
                }

                bool rational = RecordRational;
                return new DxfNurbsData(
                    RecordDegree,
                    rational,
                    Closed,
                    FlaggedPeriodic,
                    RecordPoints,
                    RecordKnots,
                    rational ? RecordWeights : null,
                    GetDouble(43, 0.0),
                    GetDouble(42, 0.0)
                );
            }
            set
            {
                if (Same(value, NurbsData))
                {
                    return;
                }

                Guard.NonNegative(
                    value.ControlPointTolerance,
                    nameof(value),
                    "A tolerance has to be a finite number, nought or more."
                );
                Guard.NonNegative(
                    value.KnotTolerance,
                    nameof(value),
                    "A tolerance has to be a finite number, nought or more."
                );
                SetNurbsData(
                    value.Degree,
                    value.Rational,
                    value.Closed,
                    value.Periodic,
                    value.GetControlPoints(),
                    value.GetKnots(),
                    value.GetWeights()
                );
                SetDouble(43, value.ControlPointTolerance, 0.0);
                SetDouble(42, value.KnotTolerance, 0.0);
            }
        }

        /// <summary>
        /// Gets or sets the fit data in one value, as AutoCAD's <c>FitData</c>: the fit points, the degree, the fit tolerance
        /// (group 44), the tangents at the ends (12 and 13) and whether they are given, and the parameters the flags of group 70
        /// say, as AutoCAD 2021 read them (its D32b run): by the square root of the chord for 64 (its 1096), one step each for
        /// 128 (1160), its own for 256 (1288, a knot put in), else by the chord. A spline not periodic whose fit points are the
        /// points at its knots gives its derivatives at its ends as its tangents (<see cref="StartFitTangent"/>).
        /// </summary>
        /// <value>
        /// Set, the fit points and the rest written, and the control points worked out anew through them, as AutoCAD keeps both:
        /// the cubic AutoCAD 2021 fits through them, whatever degree is asked, each point on it at its knot, its knots the
        /// lengths of the chords from the first point, at an end of a tangent its derivative that tangent at the length given
        /// (written as given), and at an end of none, or of one of no length, its curve bent no more. Through (0, 0, 0),
        /// (30, 40, 0) and (60, 0, 0) it is the cubic of the control points (0, 0), (10, 20), (30, 60), (50, 20) and (60, 0) from
        /// 0 to 100, as AutoCAD fitted it, and through five points, alone or given tangents of length 1 or 5, AutoCAD's control
        /// points to 1E-13 (its D32 run); two points give the straight cubic of 4 points a third apart. Points one after another
        /// within 1E-10 are taken as one, as AutoCAD took two points a trillionth apart, and kept two a millionth apart. It
        /// passes through every point: a fit tolerance above 1E-10, within which AutoCAD fits a curve that passes near the points
        /// (1.5 times the tolerance off at most in its D32 run), is written but not fitted to. A spline closed, or set periodic data, is closed through its first point again, its curve bent no
        /// more at the seam where no tangents are given, and stays closed, as AutoCAD 2021 kept its closed cubic of 70 1035
        /// given fit data not periodic, saving it 70 1067 or, of no tangents, 70 1065 (its 17-l).
        /// The flags 32 and 1024 of fit data are set, and the flags 1 and 2 as <see cref="SetNurbsData"/> gives them. Periodic
        /// data give AutoCAD's periodic cubic through the points taken round, of whatever degree they ask, each point on the
        /// curve at its knot, the knots the lengths of the chords, clamped into 3 and the count of the points as control points,
        /// written with the flag 2048 and no fit points, tangents or fit tolerance, its fit points the points at its knots by
        /// the flag 1024: as AutoCAD wrote 70 3115 for its 1035 through (0, 0), (40, 1), (50, 30) and (10, 25), its control
        /// points the library's to 1E-14, and the same cubic through (0, 0), (40, 0), (50, 30) and (10, 25) asked a degree 2
        /// (its 17-v). Three points give the cubic too, of 3 control points to a period (AutoCAD's through (0, 0), (30, 0) and
        /// (20, 15), its D32 ring3-periodic), two a line there and back. Where two of the points are one, within 1E-3, which
        /// make no periodic spline, the data give the closed spline above. Parameters by the square root of the chord or one
        /// step each (SqrtChord, Uniform) give the same cubic, its knots going up by those, written as AutoCAD 2021 writes it
        /// with no fit points, tangents or fit tolerance and the flag 64 or 128 in place of 32, its fit points the points at its
        /// knots by the flag 1024: through five points the knots 0, 1, 2, 3 and 4 and AutoCAD's control points to 3E-14, saved
        /// 70 1160 (its D32b run). Data equal to the data read changes nothing.
        /// </value>
        /// <exception cref="ArgumentException">Thrown when there are fewer than two points, a point or a tangent is not finite,
        /// or the points are all one.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the degree is outside 1 to 10, or the tolerance below nought.</exception>
        /// <exception cref="NotSupportedException">Thrown for parameters of the program's own (CustomParameterization), which the library does not fit by.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on a spline that is erased.</exception>
        public DxfFitData FitData
        {
            get
            {
                bool tangents =
                    GetString(12) != null
                    || GetString(22) != null
                    || GetString(32) != null
                    || GetString(13) != null
                    || GetString(23) != null
                    || GetString(33) != null;
                GeoVector3 start = StartFitTangent;
                GeoVector3 end = EndFitTangent;
                return new DxfFitData(FitPoints, Degree, FitTolerance)
                {
                    StartTangent = start,
                    EndTangent = end,
                    TangentsExist = tangents || start.Length > 0.0 || end.Length > 0.0,
                    KnotParam = KnotParameters(GetInt(70, 0)),
                    Periodic = FlaggedPeriodic,
                };
            }
            set
            {
                if (Same(value, FitData))
                {
                    return;
                }

                if (value.KnotParam == DxfKnotParameterizationEnum.CustomParameterization)
                {
                    throw new NotSupportedException(
                        "A spline is fitted through its points by the chord, its square root or one step each: the library fits by no parameters of its own."
                    );
                }

                Guard.NonNegative(
                    value.FitTolerance,
                    nameof(value),
                    "A tolerance has to be a finite number, nought or more."
                );
                WriteFit(
                    value.GetFitPoints(),
                    value.Degree,
                    value.FitTolerance,
                    value.TangentsExist,
                    value.StartTangent,
                    value.EndTangent,
                    value.Periodic || Closed,
                    value.Periodic,
                    value.KnotParam,
                    nameof(value)
                );
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
                    throw new InvalidOperationException(
                        "The SPLINE has no control points, and no fit points the reader passes a spline through."
                    );
                }

                return new DxfSpline(
                    data.Degree,
                    data.Rational,
                    data.Closed,
                    data.Periodic,
                    data.GetControlPoints(),
                    data.GetKnots(),
                    data.GetWeights(),
                    data.ControlPointTolerance,
                    data.KnotTolerance
                );
            }
        }

        /// <summary>
        /// Raises the degree of the spline, as AutoCAD's <c>ElevateDegree</c>: the curve kept exactly, a knot standing as many
        /// times more as the degree is raised, so that the spline is as smooth everywhere as it was; its fit data taken out,
        /// as it is given by its control points from then on. A cubic of 4 points raised to 4 has 5 points, the knots
        /// 0, 0, 0, 0, 0, 1, 1, 1, 1, 1. Raised to 25, the most (AutoCAD's order of 26), it has 26 points and is the same curve:
        /// the cubic from (0, 0) by (10, 20) and (30, 20) to (40, 0), raised to 12, has the 13 points that AutoCAD 2021 gave,
        /// the second (2.5, 5) and the third (5.4545, 9.0909), as it has at 11, 15 and 25. The spline's own measures
        /// (<see cref="GetPointAtParameter"/>, <see cref="GetDistanceAtParameter"/>) work at any of these degrees, and so
        /// does <see cref="ToGeoSpline3"/>, whose spline of degree 25 has the length and the points of the cubic it was raised from.
        /// </summary>
        /// <param name="newDegree">The degree, above the spline's, at most 25; the spline's own changes nothing.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the degree is below the spline's or above 25.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no data that make a spline, or is erased.</exception>
        public void ElevateDegree(int newDegree)
        {
            Clamped(out int p, out List<double> u, out List<double[]> pw);

            if (newDegree == p)
            {
                return;
            }

            if (newDegree < p || newDegree > MostElevatedDegree)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(newDegree),
                    newDegree,
                    "A spline's degree is raised from its own, "
                        + p.ToString(CultureInfo.InvariantCulture)
                        + ", to 25 at most."
                );
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
                throw new ArgumentException(
                    "A control point has to be of finite numbers: " + controlPoint + " is not.",
                    nameof(controlPoint)
                );
            }

            Clamped(out int p, out List<double> u, out List<double[]> pw);
            double start = u[p];
            double end = u[pw.Count];

            if (!(knotParam > start && knotParam < end) || DxfNurbsEdit.Multiplicity(u, knotParam) >= p)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(knotParam),
                    knotParam,
                    "A knot is put inside the spline, which runs from "
                        + start.ToString("R", CultureInfo.InvariantCulture)
                        + " to "
                        + end.ToString("R", CultureInfo.InvariantCulture)
                        + ", and where it stands fewer times than the degree."
                );
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
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    index,
                    "The spline has "
                        + pw.Count.ToString(CultureInfo.InvariantCulture)
                        + " control points, from nought."
                );
            }

            if (pw.Count - 1 < p + 1)
            {
                throw new InvalidOperationException(
                    "A spline of degree "
                        + p.ToString(CultureInfo.InvariantCulture)
                        + " keeps "
                        + (p + 1).ToString(CultureInfo.InvariantCulture)
                        + " control points at least, and this one has "
                        + pw.Count.ToString(CultureInfo.InvariantCulture)
                        + "."
                );
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
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    index,
                    "A fit point goes in from nought to the count of the fit points, "
                        + fit.Count.ToString(CultureInfo.InvariantCulture)
                        + "."
                );
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
                throw new InvalidOperationException(
                    "A spline through fit points keeps two at least, and this one has "
                        + fit.Count.ToString(CultureInfo.InvariantCulture)
                        + "."
                );
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
        /// Brings the fit data up to the control points, as AutoCAD 2021's <c>UpdateFitData</c> does: it takes them out, the
        /// spline given by its control points from then on, as <see cref="PurgeFitData"/>. AutoCAD held no fit data after it on
        /// its cubic through five points, nor on its cubic of 5 control points, and saved both 70 8 (its D32 run); fit points of
        /// the curve at its knots, which AutoCAD fits anew when it reads them, would not give the curve back.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no data that make a spline, or is erased.</exception>
        public void UpdateFitData()
        {
            Nurbs();
            PurgeFitData();
        }

        /// <summary>
        /// Moves the control points so that the spline passes a point at a parameter in a direction there, as AutoCAD's
        /// <c>ModifyPositionAndTangent</c>: the points the span of the parameter is made of each moved the least that does it
        /// (the change of least squares that meets the point and the derivative), the weights kept. A spline of fit data keeps
        /// its points at its knots as fit points, of parameters of its own, as AutoCAD 2021 saved its cubic through five points
        /// so moved 70 1288 and its closed one 1289 (its D32b run).
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
            bool fitted = HasFitRecord();
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
            double g00 = 0.0,
                g01 = 0.0,
                g11 = 0.0;

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

            if (fitted)
            {
                FitAtItsKnots();
            }
        }

        /// <summary>Whether the record holds fit data: fit points (groups 11), or the flag 1024 of fit points at its knots.</summary>
        private bool HasFitRecord() => Points(11).Count > 0 || (GetInt(70, 0) & FitByKnots) != 0;

        /// <summary>
        /// Gives the spline its points at its knots as fit points, of parameters of its own (the flags 1024 and 256 of group 70,
        /// no groups 11), as AutoCAD 2021 kept the fit data of its cubic through five points moved by
        /// <c>ModifyPositionAndTangent</c> (saved 70 1288, its closed one 1289) and of the pieces <c>GetSplitCurves</c> cut it
        /// into (its D32b run).
        /// </summary>
        private void FitAtItsKnots() => SetInt(70, GetInt(70, 0) | FitByKnots | FitAtKnots, 0);

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
                throw new ArgumentOutOfRangeException(
                    nameof(degree),
                    degree,
                    "A spline is rebuilt of a degree from 1 to 10."
                );
            }

            if (numCtrlPts < degree + 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(numCtrlPts),
                    numCtrlPts,
                    "A spline of degree "
                        + degree.ToString(CultureInfo.InvariantCulture)
                        + " has "
                        + (degree + 1).ToString(CultureInfo.InvariantCulture)
                        + " control points at least."
                );
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
        /// <param name="bConvertAsArcs">Whether its segments are arcs, each turning as the spline's tangent turns along it; lines where false, and for a spline that is not flat.</param>
        /// <param name="bToLWPolyline">Whether a flat spline gives a <see cref="DxfPolyline"/>; a <see cref="DxfPolyline2d"/> where false.</param>
        /// <returns>The polyline, of the spline's look, in no file.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no data that make a spline.</exception>
        public DxfCurve ToPolyline(bool bConvertAsArcs, bool bToLWPolyline) =>
            ToPolylineWithPrecision(10, bConvertAsArcs, bToLWPolyline);

        /// <summary>
        /// Gets the polyline of a count of vertices that runs along the spline, the library's own reading of AutoCAD's
        /// <c>ToPolyline</c>, as <see cref="ToPolyline(int, bool, bool)"/> says: of arcs, a <see cref="DxfPolyline"/> where the
        /// spline is flat.
        /// </summary>
        /// <param name="numOfVertices">How many vertices, 2 or more, set evenly along the spline's length.</param>
        /// <returns>The polyline, of the spline's look, in no file.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the count is below 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no data that make a spline.</exception>
        public DxfCurve ToPolyline(int numOfVertices) => ToPolyline(numOfVertices, true, true);

        /// <summary>
        /// Gets the polyline of a count of vertices that runs along the spline, the library's own reading of AutoCAD's
        /// <c>ToPolyline</c>: the vertices set evenly along the spline's length, a closed spline's last on its first, which the
        /// closed polyline joins back to; each segment a line, or the arc through its ends and the spline's point halfway between
        /// them. A quarter of a circle of radius 100 made a spline, given 3 vertices and arcs, is two arcs of radius 100 to within
        /// the rounding. AutoCAD 2021 keeps a different promise: it was seen to give the cubic of one span 33 vertices for every
        /// count from 2 to 50 and 65 for 99, the cubic of two spans 50 for every count from 3 to 99, and to refuse the smallest
        /// counts (2 for two spans, 2 and 3 for three); the count it is given is not the count it makes.
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
                throw new ArgumentOutOfRangeException(
                    nameof(numOfVertices),
                    numOfVertices,
                    "A polyline has two vertices at least."
                );
            }

            DxfNurbs nurbs = Nurbs();
            double length = nurbs.LooseLength;
            var parameters = new List<double>(numOfVertices);
            var middles = new List<double>(numOfVertices);

            for (int i = 0; i < numOfVertices; i++)
            {
                parameters.Add(
                    i == 0 ? nurbs.Start
                    : i == numOfVertices - 1 ? nurbs.End
                    : nurbs.ParameterAtLooseLength(length * i / (numOfVertices - 1))
                );

                if (i > 0)
                {
                    middles.Add(nurbs.ParameterAtLooseLength(length * (i - 0.5) / (numOfVertices - 1)));
                }
            }

            return Polyline(nurbs, parameters, middles, bConvertAsArcs, bToLWPolyline, false, null);
        }

        /// <summary>Gets the polyline that runs along the spline within a precision, as AutoCAD's <c>ToPolylineWithPrecision</c>, of arcs, a <see cref="DxfPolyline"/> where the spline is flat.</summary>
        /// <param name="precision">The precision, from 0 to 99, as <see cref="ToPolylineWithPrecision(int, bool, bool)"/> reads it.</param>
        /// <returns>The polyline, of the spline's look, in no file.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the precision is outside 0 to 99.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no data that make a spline.</exception>
        public DxfCurve ToPolylineWithPrecision(int precision) => ToPolylineWithPrecision(precision, true, true);

        /// <summary>
        /// Gets the polyline that runs along the spline within a precision, as AutoCAD's <c>ToPolylineWithPrecision</c>. Of
        /// lines, as AutoCAD 2021 makes it: the spline's whole parameter range, from its start to its end, halved again and
        /// again until the spline's points halfway, a quarter and three quarters along each piece's parameter are no farther from
        /// the chord's points there than the larger of one over the spline's sharpest bend and a ten-thousandth of its length,
        /// over the square of one more than the precision, max(1 / M, L / 10 000) / (p + 1)^2, M the largest length of the
        /// spline's second derivative by its parameter; each piece a segment. AutoCAD's 156 counts at hand, of cubics, a
        /// quadratic, a quartic, quintics and rational cubics, at 0 to 99 (its plugin runs v4 to v7 and D32), are the rule's but
        /// 9, each within 3 of AutoCAD's, 7 of them of rational cubics: a quintic Bezier gives 86, 478 and 7 929 vertices at 0, 5
        /// and 99 and the same 10 times its size, a hairpin cubic of 100 by 1 over the knots 0 to 1 129 at 0, where its length
        /// decides, and over 0 to 100 5, where its bend does, and an S of three spans whose middle lies on its chord 173.
        /// The rule gives AutoCAD's
        /// counts of vertices for the three cubics of its plugin runs v4 and v5 at every precision tried: the cubic of one span on
        /// (0, 0), (10, 10), (20, 10), (30, 0), whose bend is 60 everywhere, 33, 65, 129, 257 and 4 097 vertices at 0, 1, 3, 10
        /// and 99; the cubic of two spans on (0, 40), (10, 50), (20, 50), (30, 40), (40, 30), 50 at 0, 513 at 10 and 4 750 at
        /// 99; the cubic of three spans on (0, 0), (10, 10), (20, 10), (30, 0), (40, -10), (50, 0), 78 at 0 and 7 562 at 99. The
        /// sharpest bend is read at the ends of each span of the knots and at seven points between, which for a cubic, whose
        /// second derivative runs straight along a span, is its largest; a piece is halved no more than 20 times over and no finer
        /// than the rounding of the spline's largest coordinate, and a straight spline is one segment. The rule gives AutoCAD's
        /// counts for a quadratic of three spans and a cubic of a double knot too (29, 342 and 2 732, and 76, 933 and 8 011 at 0,
        /// 10 and 99), a spline of degree 5 (96 at 0) and a rational one (130 at 0, 1 378 at 10 where AutoCAD gives 1 377). A
        /// closed spline has its last vertex on its first, the
        /// polyline closed; one closed, or of the flag 2 of its 70 (the periodic of the DXF reference, which AutoCAD writes on its
        /// closed splines of a smooth seam), that runs through fit points, or that AutoCAD keeps by its fit points, the flag 1024
        /// of its 70 set (its 3115 of a periodic cubic, written with no fit points), has each of its knots, one after another, in
        /// the place of the vertex of the halving nearest it, the later of two as near, a knot taking the place another took
        /// before it, as AutoCAD 2021 gave them: the closed cubic through (0, 0), (40, 0), (50, 30) and (10, 25), its knots the lengths of the
        /// chords, is those four points and the first again at 0, and 48 vertices at 10, the range halved into 64ths and the knot
        /// 40 standing in the place of the 64th at 39.05; through (0, 0), (40, 0), (40.5, 0.5), (50, 30) and (10, 25) it is at
        /// 0 the vertices at 0, 40.71, 71.70, 112.01 and its end, the knot 40.71 in the place the knot 40 took. The same spline
        /// of no fit points and no flag 1024, its 70 of 3, is the halving alone, as AutoCAD 2021 gave it: (0, 0), (34.42, -3.51),
        /// (50.86, 28.40), (18.15, 30.49) and the first again at 0. AutoCAD's closed cubic not periodic through (0, 0), (30, 0),
        /// (30, 15) and (20, 15), a corner at its seam (70 1065), has its knots in place as well: at 0 (0, 0), (22.66, -5.01), (30,
        /// 15), (20, 15) and the first again, the knots 45 and 55 for the quarters 40 and 60 (its D32 run, where 35 of the 36
        /// polylines of six such cubics at 0 to 10 are the library's). Each arc is bent as the piece the halving made, its first,
        /// from (0, 0) to (40, 0) at 0, by the turn of the tangent over the first quarter of the range. Of arcs, for a flat spline, the same vertices, each
        /// segment the arc that turns as the spline's tangent turns along it, its bulge the tangent of a quarter of that turn,
        /// as AutoCAD 2021 makes them: the cubic of one span at 0 gives 33 vertices, its first arc, from (0, 0) to (0.9375, 0.908), of bulge
        /// -0.00806, the spline's tangent turning from 45 degrees to 43.15 along it. A spline not periodic of n control points
        /// is cut instead into (n - 3) + (n - 2) k pieces even in its parameter, k half the precision rounded down, where that
        /// is more pieces than the halving gives, as AutoCAD 2021 cut them where and as the rule says in each of 448 polylines of
        /// sixteen closed cubics, of fit points and of control points, and 80 of eight open curves of few pieces (its D32b run): a
        /// closed one has no last vertex on its first, and of its knots only as many as its vertices less two are put in place,
        /// so that the closed cubic through (0, 0), (40, 0), (40.5, 0.5), (50, 30) and (10, 25) is at 0 its vertices at 0,
        /// 40.71, 55.57 (two fifths of the range), 71.70 and 111.15; the straight cubic of 4 points is 1 segment at 0 and 1, 3 at
        /// 2 and 3; the quintic Bezier over 0 to 50 is cut in thirds at 0.
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
            DxfNurbsData data = NurbsData;
            GeoAabb3 box = Box(data.GetControlPoints());
            double largest = Math.Max(
                Math.Max(
                    Math.Max(Math.Abs(box.Min.X), Math.Abs(box.Max.X)),
                    Math.Max(Math.Abs(box.Min.Y), Math.Abs(box.Max.Y))
                ),
                Math.Max(Math.Abs(box.Min.Z), Math.Abs(box.Max.Z))
            );
            var parameters = new List<double> { nurbs.Start };
            var middles = new List<double>();
            double bend = SharpestBend(nurbs, data.GetKnots());
            // The larger of one over the sharpest bend and a ten-thousandth of the length, as AutoCAD's counts give it (D32).
            double loose = Math.Max(bend > 0.0 ? 1.0 / bend : double.PositiveInfinity, nurbs.LooseLength / 10000.0);
            double within = Math.Max(1E-9 * (1.0 + largest), loose / ((precision + 1.0) * (precision + 1.0)));
            Halve(
                nurbs,
                nurbs.Start,
                nurbs.End,
                nurbs.PointAt(nurbs.Start),
                nurbs.PointAt(nurbs.End),
                within,
                0,
                parameters,
                middles
            );

            // A closed spline or one of the flag 2, of fit points or kept by AutoCAD by its fit points (the flag 1024), has its knots
            // where the vertices nearest them were, each arc still bent by the turn along the piece the halving made; the last
            // vertex of a closed spline is kept on its first.
            int flags = GetInt(70, 0);
            bool byFitPoints = (flags & FitByKnots) != 0 || Points(11).Count > 0;
            bool keepLast = true;
            int placed = int.MaxValue;

            // A spline not periodic is cut evenly instead where that gives more pieces than the halving, (n - 3) + (n - 2) times
            // half the precision rounded down for n control points, as AutoCAD 2021 cut its closed cubics and its quintic over
            // 0 to 50 (its D32b run): a closed one is left with no last vertex on its first, and of its knots only as many less
            // two as it has vertices are put in place.
            int count = data.GetControlPoints().Count;
            int even = (count - 3) + (count - 2) * (precision / 2);

            if (even > parameters.Count - 1 && !IsPeriodic)
            {
                parameters.Clear();
                middles.Clear();
                double span = nurbs.End - nurbs.Start;

                for (int i = 0; i <= even; i++)
                {
                    parameters.Add(i == even ? nurbs.End : nurbs.Start + span * i / even);

                    if (i > 0)
                    {
                        middles.Add(nurbs.Start + span * (i - 0.5) / even);
                    }
                }

                keepLast = false;
                placed = even - 2;
            }

            List<double> places =
                (flags & 3) != 0 && byFitPoints
                    ? PlaceKnots(parameters, data.GetKnots(), nurbs.Start, nurbs.End, placed)
                    : parameters;

            // Arcs are bent by the turn of the spline's tangent along each segment, not through its point halfway.
            return Polyline(
                nurbs,
                parameters,
                bConvertAsArcs ? null : middles,
                bConvertAsArcs,
                bToLWPolyline,
                keepLast,
                places
            );
        }

        /// <summary>
        /// Gets the pieces the spline is cut into at parameters, as AutoCAD's <c>GetSplitCurves</c>: splines of the same look in
        /// no file, each the part of the spline between two cuts exactly, its knots those of the spline there with the cut
        /// standing as many times as the degree and one at its ends; an open spline from its start to the first parameter, on
        /// to the next and to its end, a closed one from each to the next and from the last on round its start to the first,
        /// given whole where it is cut at fewer than two places. A parameter at an end or within the point tolerance along the
        /// spline of another cuts nothing. A spline of fit data gives pieces whose fit points are their points at their knots,
        /// of parameters of their own (70 1288), as AutoCAD 2021 gave its cubic through five points cut at 43.36 two pieces of 3
        /// and 4 such fit points, the first leaving along the spline's derivative (1.11, -0.34, 0) (its D32b run).
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
            List<double> cuts = DxfCurveTools.Cuts(parameters, start, end, closed, nurbs.LengthBetween, x =>
                Within(x - start, end - start, nameof(parameters))
            );
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
            bool fitted = HasFitRecord();

            foreach (KeyValuePair<List<double>, List<double[]>> piece in pieces)
            {
                var copy = first == null ? (DxfSpline)Copy() : (DxfSpline)first.Clone();
                copy.WriteClamped(p, piece.Key, piece.Value);

                if (fitted)
                {
                    copy.FitAtItsKnots();
                }

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
                throw new InvalidOperationException(
                    "A spline that does not lie in one plane has no side to be offset to: give the plane's normal."
                );
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
        public override IReadOnlyList<DxfCurve> GetOffsetCurvesGivenPlaneNormal(
            GeoVector3 planeNormal,
            double offsetDist
        )
        {
            if (
                !planeNormal.IsValid
                || !(planeNormal.Length > 0.0)
                || !DxfCurveTools.TryPlane(NurbsData.GetControlPoints(), planeNormal, out GeoVector3 normal)
                || !normal.IsParallelTo(planeNormal, Tolerance.Default)
            )
            {
                throw new ArgumentException(
                    "A spline is offset in a plane it lies in: the normal " + planeNormal + " is not square to it.",
                    nameof(planeNormal)
                );
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
                throw new ArgumentOutOfRangeException(
                    nameof(newParameter),
                    newParameter,
                    "The parameter lies on the spline: one past an end extends it."
                );
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
                throw new ArgumentException(
                    "A point has to be of finite numbers: " + toPoint + " is not.",
                    nameof(toPoint)
                );
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
                throw new ArgumentException(
                    "The point "
                        + toPoint
                        + " is not on the spline run on past its "
                        + (extendStart ? "start" : "end")
                        + ".",
                    nameof(toPoint)
                );
            }

            if (!(s > 1.0))
            {
                throw new ArgumentException(
                    "The point "
                        + toPoint
                        + " is on the spline itself, not past its "
                        + (extendStart ? "start" : "end")
                        + ".",
                    nameof(toPoint)
                );
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
                throw new InvalidOperationException(
                    "The SPLINE has no control points, and no fit points the reader passes a spline through."
                );
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
        /// weight is not 1, closed where it starts where it ends, of the flag 2048 of a periodic spline no longer.
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
                var groups = new List<KeyValuePair<int, string>>
                {
                    new KeyValuePair<int, string>(70, kept.ToString(CultureInfo.InvariantCulture)),
                };

                if ((kept & 8) == 0 && (flags & 8) != 0)
                {
                    groups.AddRange(
                        new[]
                        {
                            new KeyValuePair<int, string>(210, null),
                            new KeyValuePair<int, string>(220, null),
                            new KeyValuePair<int, string>(230, null),
                        }
                    );
                }

                SetGroups(groups.ToArray());
            }
        }

        /// <summary>The fit points, refused where the spline has none.</summary>
        private List<GeoPoint3> FitList()
        {
            if (!HasFitData)
            {
                throw new InvalidOperationException(
                    "The spline has no fit points: it is given by its control points alone."
                );
            }

            return new List<GeoPoint3>(FitPoints);
        }

        /// <summary>Fits the spline anew through fit points, its other fit data kept; parameters of its own are taken by the chord.</summary>
        private void Refit(List<GeoPoint3> fit, string parameter)
        {
            DxfFitData data = FitData;
            DxfKnotParameterizationEnum parameters =
                data.KnotParam == DxfKnotParameterizationEnum.CustomParameterization
                    ? DxfKnotParameterizationEnum.Chord
                    : data.KnotParam;
            WriteFit(
                fit,
                data.Degree,
                data.FitTolerance,
                data.TangentsExist,
                data.StartTangent,
                data.EndTangent,
                data.Periodic || Closed,
                false,
                parameters,
                parameter
            );
        }

        /// <summary>
        /// How the flags of group 70 say fit points are given their parameters, as AutoCAD 2021 read them (its D32b run): 256 its
        /// own (its cubic given a knot, 1288), 128 one step each (1160), 64 the square root of the chord (1096), else the chord.
        /// </summary>
        private static DxfKnotParameterizationEnum KnotParameters(int flags) =>
            (flags & FitAtKnots) != 0
                ? DxfKnotParameterizationEnum.CustomParameterization
                : DxfNurbsEdit.Parameters(flags);

        /// <summary>The flag of group 70 AutoCAD writes for fit points of parameters: 32 by the chord, 64 its square root, 128 one step each.</summary>
        private static int KnotFlag(DxfKnotParameterizationEnum parameters) =>
            parameters == DxfKnotParameterizationEnum.Uniform ? FitByStep
            : parameters == DxfKnotParameterizationEnum.SqrtChord ? FitBySquareRoot
            : FitMade;

        /// <summary>Writes fit data and the control points of the spline through them, as <see cref="FitData"/> says.</summary>
        private void WriteFit(
            IReadOnlyList<GeoPoint3> fit,
            int degree,
            double fitTolerance,
            bool tangents,
            GeoVector3 startTangent,
            GeoVector3 endTangent,
            bool closed,
            bool periodic,
            DxfKnotParameterizationEnum parameters,
            string parameter
        )
        {
            if (
                parameters != DxfKnotParameterizationEnum.Uniform
                && parameters != DxfKnotParameterizationEnum.SqrtChord
            )
            {
                parameters = DxfKnotParameterizationEnum.Chord;
            }

            if (degree < 1 || degree > MostDegree)
            {
                throw new ArgumentOutOfRangeException(
                    parameter,
                    degree,
                    "A spline is fitted of a degree from 1 to 10."
                );
            }

            if (fit.Count < 2)
            {
                throw new ArgumentException(
                    "A spline is fitted through two points at least, and was given "
                        + fit.Count.ToString(CultureInfo.InvariantCulture)
                        + ".",
                    parameter
                );
            }

            foreach (GeoPoint3 point in fit)
            {
                if (!point.IsValid)
                {
                    throw new ArgumentException(
                        "A fit point has to be of finite numbers: " + point + " is not.",
                        parameter
                    );
                }
            }

            bool hasStart = tangents && startTangent.IsValid && startTangent.Length > 0.0;
            bool hasEnd = tangents && endTangent.IsValid && endTangent.Length > 0.0;

            if (tangents && (!startTangent.IsValid || !endTangent.IsValid))
            {
                throw new ArgumentException("A tangent has to be of finite numbers.", parameter);
            }

            var through = new List<GeoPoint3>(fit);

            if (closed && fit.Count > 2 && !through[0].IsEqualTo(through[through.Count - 1], Tolerance.Default))
            {
                through.Add(through[0]);
            }

            if (Distinct(through) < 2)
            {
                throw new ArgumentException(
                    "The fit points are all one point, and a spline through them has no length.",
                    parameter
                );
            }

            if (periodic && WritePeriodic(fit, parameters))
            {
                return;
            }

            if (
                !DxfNurbsEdit.InterpolateCubic(
                    through,
                    hasStart ? startTangent : None,
                    hasEnd ? endTangent : None,
                    parameters,
                    out List<GeoPoint3> kept,
                    out List<double> knots,
                    out List<GeoPoint3> points
                )
            )
            {
                throw new ArgumentException(
                    "The fit points make no cubic through them: its system has no solution.",
                    parameter
                );
            }

            if (parameters != DxfKnotParameterizationEnum.Chord)
            {
                // AutoCAD 2021 writes a spline fitted by the square roots of the chords or one step each with no fit points,
                // tangents or fit tolerance, its fit points those at its knots by the flag 1024 (its D32b 1096 and 1160).
                bool closedFit = closed && points[0].IsEqualTo(points[points.Count - 1], Tolerance.Default);
                SetNurbsData(3, false, closedFit, false, points, knots, null);
                SetString(44, null);
                SetInt(70, GetInt(70, 0) | KnotFlag(parameters) | FitByKnots, 0);
                return;
            }

            List<List<KeyValuePair<int, string>>> was = Points(11);
            var written = new List<List<KeyValuePair<int, string>>>(kept.Count);

            for (int i = 0; i < kept.Count; i++)
            {
                written.Add(Point(11, kept[i], i < was.Count ? was[i] : null, parameter));
            }

            bool closedCurve = closed && points[0].IsEqualTo(points[points.Count - 1], Tolerance.Default);
            SetNurbsData(3, false, closedCurve, false, points, knots, null);
            SetInt(70, GetInt(70, 0) | FitMade | FitByKnots, 0);
            WritePoints(11, FitCodes, written, 74);
            var groups = new List<KeyValuePair<int, string>>();

            // The tangents as given, of their own length: AutoCAD keeps and fits by the length (its 17-l, of 0.82).
            if (hasStart)
            {
                groups.AddRange(
                    PointGroups(
                        12,
                        new GeoPoint3(startTangent.X, startTangent.Y, startTangent.Z),
                        new GeoPoint3(0.0, 0.0, 0.0)
                    )
                );
            }

            if (hasEnd)
            {
                groups.AddRange(
                    PointGroups(
                        13,
                        new GeoPoint3(endTangent.X, endTangent.Y, endTangent.Z),
                        new GeoPoint3(0.0, 0.0, 0.0)
                    )
                );
            }

            SetGroups(groups.ToArray());
            SetDouble(44, fitTolerance, 0.0);
        }

        /// <summary>
        /// Closes a spline of control points as AutoCAD 2021's SPLINEDIT Close does: its control points taken as the points of
        /// one period, uniform knots a step apart, the curve closed round its seam smoothly and written in the periodic form
        /// (the flags 2048 and 2, closed, the curve clamped at the seam, so that a cubic of 7 control points has 10 and the knots
        /// 0, 0, 0, 0, 1 to 6, 7, 7, 7, 7). The knots it had are gone, as AutoCAD's are; weights are kept with their points.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no control points, or fewer than its degree and one.</exception>
        internal void ClosePeriodic()
        {
            int p = Degree;
            IReadOnlyList<GeoPoint3> polygon = ControlPoints;
            bool rational = IsRational;
            IReadOnlyList<double> weights = rational ? Weights : null;
            int m = polygon.Count;

            if (m < p + 1)
            {
                throw new InvalidOperationException(
                    "A spline of degree "
                        + p.ToString(CultureInfo.InvariantCulture)
                        + " has "
                        + (p + 1).ToString(CultureInfo.InvariantCulture)
                        + " control points at least to be closed, and this one has "
                        + m.ToString(CultureInfo.InvariantCulture)
                        + "."
                );
            }

            // The knots of the curve as it runs on round, a period before and after: the floating point k is the control point
            // (k + m - p) mod m of the period, as InterpolatePeriodic lays them.
            var u = new List<double>(m + 2 * p + 1);

            for (int i = m - p; i < m; i++)
            {
                u.Add(i - m);
            }

            for (int i = 0; i <= m; i++)
            {
                u.Add(i);
            }

            for (int i = 1; i <= p; i++)
            {
                u.Add(i + m);
            }

            var pw = new List<double[]>(m + p);

            for (int k = 0; k < m + p; k++)
            {
                int j = (k + m - p) % m;
                double w = rational ? weights[j] : 1.0;
                pw.Add(new[] { polygon[j].X * w, polygon[j].Y * w, polygon[j].Z * w, w });
            }

            DxfNurbsEdit.Clamp(p, u, pw);
            DxfNurbsEdit.Split(pw, out List<GeoPoint3> points, out List<double> split);
            SetNurbsData(p, rational, true, true, points, u, rational ? split : null);
            SetInt(70, GetInt(70, 0) | PeriodicFlag, 0);
        }

        /// <summary>
        /// Opens a closed spline of control points as AutoCAD 2021's SPLINEDIT Open does: the control points of one period of a
        /// periodic spline (those <see cref="ClosePeriodic"/> was given) back as the points of an open spline, a closed one that
        /// is not periodic keeping its points, with uniform knots a step apart, clamped; the curve is the uniform one of those
        /// points, whatever knots it had, as AutoCAD's. Its weights go with their points.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the spline has no control points, or fewer than its degree and one.</exception>
        internal void OpenPeriodic()
        {
            int p = Degree;
            bool rational = IsRational;
            List<GeoPoint3> polygon = new List<GeoPoint3>(ControlPoints);
            List<double> weights = rational ? new List<double>(Weights) : null;

            if (IsPeriodic)
            {
                Clamped(out _, out List<double> u, out List<double[]> pw);
                List<double[]> period = DxfNurbsEdit.PeriodicPolygon(p, u, pw);

                if (period != null)
                {
                    DxfNurbsEdit.Split(period, out polygon, out List<double> split);
                    weights = rational ? split : null;
                }
            }

            int m = polygon.Count;

            if (m < p + 1)
            {
                throw new InvalidOperationException(
                    "A spline of degree "
                        + p.ToString(CultureInfo.InvariantCulture)
                        + " has "
                        + (p + 1).ToString(CultureInfo.InvariantCulture)
                        + " control points at least, and this one has "
                        + m.ToString(CultureInfo.InvariantCulture)
                        + "."
                );
            }

            var knots = new List<double>(m + p + 1);

            for (int i = 0; i <= p; i++)
            {
                knots.Add(0.0);
            }

            for (int i = 1; i < m - p; i++)
            {
                knots.Add(i);
            }

            for (int i = 0; i <= p; i++)
            {
                knots.Add(m - p);
            }

            SetNurbsData(p, rational, false, false, polygon, knots, weights);
        }

        /// <summary>
        /// Writes the periodic spline through fit points taken round, the last back to the first, as AutoCAD 2021 writes one
        /// (its 17-l): the cubic of <see cref="DxfNurbsEdit.InterpolatePeriodic"/> whatever degree the fit data ask, as AutoCAD
        /// wrote its cubic asked a degree 2 (17-v), the degree lowered to the points less one where they are fewer, closed, the
        /// flags 2048 of a periodic spline, 1024 of fit data and 32 of fit data set, and no fit points, tangents or fit
        /// tolerance, its points at its knots being the fit points. Three points give the cubic too, as AutoCAD's through (0, 0),
        /// (30, 0) and (20, 15) (its D32 ring3-periodic); the flag 64 or 128 in place of the 32 where the knots go by the square
        /// roots of the chords or one step each (its D32b 3147 and 3211). False, nothing written, where the points make no
        /// periodic spline: two of them as one, which give its system no solution.
        /// </summary>
        private bool WritePeriodic(IReadOnlyList<GeoPoint3> fit, DxfKnotParameterizationEnum parameters)
        {
            var ring = new List<GeoPoint3>(fit);

            if (ring.Count > 2 && ring[0].IsEqualTo(ring[ring.Count - 1], Tolerance.Default))
            {
                ring.RemoveAt(ring.Count - 1);
            }

            int used = ring.Count >= 3 ? 3 : ring.Count - 1;

            if (
                Distinct(ring) < ring.Count
                || !DxfNurbsEdit.InterpolatePeriodic(
                    ring,
                    used,
                    parameters,
                    out List<double> knots,
                    out List<GeoPoint3> points
                )
            )
            {
                return false;
            }

            SetNurbsData(used, false, true, true, points, knots, null);
            SetInt(70, GetInt(70, 0) | PeriodicFlag | FitByKnots | KnotFlag(parameters), 0);
            SetString(44, null);
            return true;
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
                return new[]
                {
                    new KeyValuePair<int, string>(code, null),
                    new KeyValuePair<int, string>(code + 10, null),
                    new KeyValuePair<int, string>(code + 20, null),
                };
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
            DxfSplineOffset.Offset(
                nurbs,
                data.GetKnots(),
                data.Degree,
                closed,
                normal,
                distance,
                out List<GeoPoint3> points,
                out List<double> knots,
                out bool closedOut
            );
            copy.SetNurbsData(3, false, closedOut, false, points, knots, null);
            return new DxfCurve[] { copy };
        }

        /// <summary>Refuses a closed spline, which has no end to be extended.</summary>
        private void Open()
        {
            if (Closed || FlaggedPeriodic)
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
                GeoVector3 d2 = new GeoVector3(
                    cp.X - 2.0 * c.X + cm.X,
                    cp.Y - 2.0 * c.Y + cm.Y,
                    cp.Z - 2.0 * c.Z + cm.Z
                ).Divide(h * h);
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
        private static List<KeyValuePair<List<double>, List<double[]>>> Cut(
            int p,
            List<double> u,
            List<double[]> pw,
            List<double> cuts
        )
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
        private static KeyValuePair<List<double>, List<double[]>> Join(
            int p,
            KeyValuePair<List<double>, List<double[]>> a,
            KeyValuePair<List<double>, List<double[]>> b
        )
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

        /// <summary>
        /// The bulge of the arc of a segment from one parameter of the spline to the next, in the plane of a normal: the tangent
        /// of a quarter of the angle the spline's tangent turns through from the segment's start to its end, as AutoCAD 2021
        /// bends the arcs of <c>ToPolylineWithPrecision</c>, the arriving tangent read on the span before a knot; nought where
        /// the spline stands still at either end.
        /// </summary>
        private static double Turn(DxfNurbs nurbs, GeoVector3 normal, double a, double b)
        {
            nurbs.Evaluate(a, out _, out GeoVector3 leaving, out _);
            nurbs.EvaluateArriving(b, out _, out GeoVector3 arriving, out _);
            GeoPoint3 from = WorldToOcs(normal, new GeoPoint3(leaving.X, leaving.Y, leaving.Z));
            GeoPoint3 to = WorldToOcs(normal, new GeoPoint3(arriving.X, arriving.Y, arriving.Z));

            if (!(from.X * from.X + from.Y * from.Y > 0.0) || !(to.X * to.X + to.Y * to.Y > 0.0))
            {
                return 0.0;
            }

            double turn = Math.Atan2(to.Y, to.X) - Math.Atan2(from.Y, from.X);
            turn =
                turn > Math.PI ? turn - 2.0 * Math.PI
                : turn <= -Math.PI ? turn + 2.0 * Math.PI
                : turn;
            return Math.Tan(turn / 4.0);
        }

        /// <summary>
        /// Halves a piece of the spline's parameter range until the spline's points halfway, a quarter and three quarters along
        /// it lie within the tolerance of its chord's there, as AutoCAD's <c>ToPolylineWithPrecision</c> does, adding the end and the middle parameter
        /// of each piece kept, in order; a piece halved 20 times over is kept as it is.
        /// </summary>
        private static void Halve(
            DxfNurbs nurbs,
            double a,
            double b,
            GeoPoint3 pa,
            GeoPoint3 pb,
            double tolerance,
            int depth,
            List<double> parameters,
            List<double> middles
        )
        {
            double m = 0.5 * (a + b);
            GeoPoint3 pm = nurbs.PointAt(m);
            var chord = new GeoPoint3(0.5 * (pa.X + pb.X), 0.5 * (pa.Y + pb.Y), 0.5 * (pa.Z + pb.Z));

            if (depth >= 20 || (pm.DistanceTo(chord) <= tolerance && Quarters(nurbs, a, b, pa, pb) <= tolerance))
            {
                parameters.Add(b);
                middles.Add(m);
                return;
            }

            Halve(nurbs, a, m, pa, pm, tolerance, depth + 1, parameters, middles);
            Halve(nurbs, m, b, pm, pb, tolerance, depth + 1, parameters, middles);
        }

        /// <summary>
        /// The larger distance of the curve at a quarter and at three quarters of a piece's parameter from its chord there, which
        /// AutoCAD judges a piece by besides its middle: an S of three spans whose middle lies on its chord gave it 173 vertices
        /// at 0, not one segment (its D32 run).
        /// </summary>
        private static double Quarters(DxfNurbs nurbs, double a, double b, GeoPoint3 pa, GeoPoint3 pb)
        {
            GeoPoint3 q1 = nurbs.PointAt(a + 0.25 * (b - a));
            GeoPoint3 q3 = nurbs.PointAt(a + 0.75 * (b - a));
            var c1 = new GeoPoint3(0.75 * pa.X + 0.25 * pb.X, 0.75 * pa.Y + 0.25 * pb.Y, 0.75 * pa.Z + 0.25 * pb.Z);
            var c3 = new GeoPoint3(0.25 * pa.X + 0.75 * pb.X, 0.25 * pa.Y + 0.75 * pb.Y, 0.25 * pa.Z + 0.75 * pb.Z);
            return Math.Max(q1.DistanceTo(c1), q3.DistanceTo(c3));
        }

        /// <summary>
        /// The parameters the vertices of a periodic spline of fit points stand at, as AutoCAD 2021's
        /// <c>ToPolylineWithPrecision</c> puts them: those the halving gave, each knot within the range, one after another, in
        /// the place of the one nearest it, the ends aside, the later of two as near, a knot placed there before replaced; at
        /// most a count of them, the first. The halving's parameters, which bend the arcs, are kept.
        /// </summary>
        private static List<double> PlaceKnots(
            List<double> parameters,
            IReadOnlyList<double> knots,
            double start,
            double end,
            int most
        )
        {
            var places = new List<double>(parameters);
            double previous = start;
            int taken = 0;

            foreach (double knot in knots)
            {
                if (!(knot > previous) || !(knot < end))
                {
                    continue;
                }

                if (taken++ >= most)
                {
                    break;
                }

                previous = knot;
                int nearest = -1;

                // The vertex of the halving nearest the knot, the later of two as near; a knot placed there before is replaced.
                for (int i = 1; i < parameters.Count - 1; i++)
                {
                    if (nearest < 0 || Math.Abs(parameters[i] - knot) <= Math.Abs(parameters[nearest] - knot))
                    {
                        nearest = i;
                    }
                }

                if (nearest >= 0)
                {
                    places[nearest] = knot;
                }
            }

            return places;
        }

        /// <summary>
        /// The largest length of the spline's second derivative by its parameter, read at the ends of each span of its knots,
        /// just short of the end for the span's own side of a knot, and at seven points between.
        /// </summary>
        private static double SharpestBend(DxfNurbs nurbs, IReadOnlyList<double> knots)
        {
            double sharpest = 0.0;
            double from = nurbs.Start;

            foreach (double knot in knots)
            {
                if (!(knot > from) || knot > nurbs.End)
                {
                    continue;
                }

                for (int k = 0; k <= 8; k++)
                {
                    double t = k < 8 ? from + (knot - from) * k / 8.0 : knot - (knot - from) * 1E-9;
                    nurbs.Evaluate(t, out _, out _, out GeoVector3 second);
                    sharpest = Math.Max(sharpest, second.Length);
                }

                nurbs.Evaluate(knot, out _, out _, out GeoVector3 atKnot);
                sharpest = Math.Max(sharpest, atKnot.Length);
                from = knot;
            }

            return sharpest;
        }

        /// <summary>
        /// The polyline through the spline's points at parameters, each segment a line or an arc: the arc through the point at
        /// its middle parameter, or where no middles are given the arc bent by the turn of the spline's tangent along it; each
        /// vertex the point at its place, which is its parameter where no places are given. A closed polyline's last vertex,
        /// on its first, is left out, or kept where asked.
        /// </summary>
        private DxfCurve Polyline(
            DxfNurbs nurbs,
            List<double> parameters,
            List<double> middles,
            bool arcs,
            bool lightweight,
            bool keepLast,
            List<double> places
        )
        {
            var world = new List<GeoPoint3>(parameters.Count);

            foreach (double t in places ?? parameters)
            {
                world.Add(nurbs.PointAt(t));
            }

            bool closed = Closed && world[0].IsEqualTo(world[world.Count - 1], Tolerance.Default) && world.Count > 2;
            bool flat = DxfCurveTools.TryPlane(NurbsData.GetControlPoints(), Direction(210), out GeoVector3 normal);
            int count = closed && !keepLast ? world.Count - 1 : world.Count;
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

                    if (arcs && i + 1 < parameters.Count && middles == null)
                    {
                        bulges.Add(Turn(nurbs, normal, parameters[i], parameters[i + 1]));
                    }
                    else if (arcs && i + 1 < parameters.Count)
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

                    var polyline = new DxfPolyline2d(
                        DxfPoly2dType.SimplePoly,
                        points,
                        elevation,
                        closed,
                        0.0,
                        0.0,
                        bulges
                    );
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
            double minX = double.PositiveInfinity,
                minY = double.PositiveInfinity,
                minZ = double.PositiveInfinity;
            double maxX = double.NegativeInfinity,
                maxY = double.NegativeInfinity,
                maxZ = double.NegativeInfinity;

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
        private static bool Same(DxfNurbsData a, DxfNurbsData b) =>
            a.Degree == b.Degree
            && a.Rational == b.Rational
            && a.Closed == b.Closed
            && a.Periodic == b.Periodic
            && Same(a.ControlPointTolerance, b.ControlPointTolerance)
            && Same(a.KnotTolerance, b.KnotTolerance)
            && SamePoints(a.GetControlPoints(), b.GetControlPoints())
            && SameNumbers(a.GetKnots(), b.GetKnots())
            && SameNumbers(a.GetWeights(), b.GetWeights());

        /// <summary>Whether two fit data are the same, number for number to the bit.</summary>
        private static bool Same(DxfFitData a, DxfFitData b) =>
            a.Degree == b.Degree
            && a.TangentsExist == b.TangentsExist
            && a.KnotParam == b.KnotParam
            && a.Periodic == b.Periodic
            && Same(a.FitTolerance, b.FitTolerance)
            && SamePoints(a.GetFitPoints(), b.GetFitPoints())
            && Same(a.StartTangent.X, b.StartTangent.X)
            && Same(a.StartTangent.Y, b.StartTangent.Y)
            && Same(a.StartTangent.Z, b.StartTangent.Z)
            && Same(a.EndTangent.X, b.EndTangent.X)
            && Same(a.EndTangent.Y, b.EndTangent.Y)
            && Same(a.EndTangent.Z, b.EndTangent.Z);
    }
}
