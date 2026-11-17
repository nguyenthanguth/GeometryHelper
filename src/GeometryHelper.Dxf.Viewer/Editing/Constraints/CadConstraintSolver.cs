using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The kinds of object the constraint solver works on.</summary>
    internal enum CadShapeKind
    {
        /// <summary>A LINE: x1, y1, x2, y2.</summary>
        Line,

        /// <summary>A CIRCLE: centre x, centre y, radius.</summary>
        Circle,

        /// <summary>An ARC: centre x, centre y, radius, start x, start y, end x, end y (its ends are points, so that every number is a length).</summary>
        Arc,

        /// <summary>A light polyline of straight segments: x, y of each vertex.</summary>
        Polyline,
    }

    /// <summary>One object as the solver sees it: its kind and the numbers that place it.</summary>
    internal sealed class CadShape
    {
        internal CadShape(string handle, CadShapeKind kind, double[] values, bool closed = false)
        {
            Handle = handle ?? throw new ArgumentNullException(nameof(handle));
            Kind = kind;
            Values = values ?? throw new ArgumentNullException(nameof(values));
            Closed = closed;
        }

        /// <summary>The handle of the object.</summary>
        internal string Handle { get; }

        /// <summary>The kind.</summary>
        internal CadShapeKind Kind { get; }

        /// <summary>The numbers, as the kind lists them.</summary>
        internal double[] Values { get; }

        /// <summary>Whether a polyline is closed, its last segment running back to its first vertex.</summary>
        internal bool Closed { get; }

        /// <summary>The number of vertices of a polyline.</summary>
        internal int Vertices => Values.Length / 2;

        /// <summary>The number of segments of a polyline.</summary>
        internal int Segments => Closed ? Vertices : Vertices - 1;
    }

    /// <summary>What a solve came to.</summary>
    internal sealed class CadSolveResult
    {
        /// <summary>Whether every constraint holds at the values given back.</summary>
        internal bool Succeeded { get; set; }

        /// <summary>Why it did not; null where it did.</summary>
        internal string Message { get; set; }

        /// <summary>The numbers of each object solved, by handle (ordinal order); every object the set ties.</summary>
        internal SortedDictionary<string, double[]> Values { get; } =
            new SortedDictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The number of numbers the constraints may still move freely: the unknowns less the rank of the constraints.</summary>
        internal int Freedom { get; set; }

        /// <summary>The number of constraint equations.</summary>
        internal int Equations { get; set; }

        /// <summary>The rank of the equations; less than <see cref="Equations"/> where one repeats another.</summary>
        internal int Rank { get; set; }

        /// <summary>Whether some equation repeats others (or contradicts them): the set is over-constrained.</summary>
        internal bool Redundant => Rank < Equations;
    }

    /// <summary>
    /// A small solver for the constraints of a space, of the kind AutoCAD 2021's constraint solver is for the common cases:
    /// the numbers of the lines, circles, arcs and polylines are the unknowns, each constraint is an equation (or two), and
    /// the answer is the point nearest to where the objects stand that meets every equation. The distance is measured with a
    /// weight on each number, so that what an edit has just placed (a grip dragged, a line moved) stays and the rest moves.
    /// Each step is the least change of the linearised equations, Newton's method on a constrained least-squares problem; the
    /// same input always gives the same answer.
    /// </summary>
    internal static class CadConstraintSolver
    {
        private const double Tolerance = 1E-9;
        private const double Target = 1E-13;
        private const int Steps = 80;

        /// <summary>The weight of a number: what the caller gives each of the numbers of an object, 1 for every one not asked about.</summary>
        internal delegate double Weighter(string handle, int index);

        private sealed class Row
        {
            internal Func<double[], double> F;
            internal int[] Vars;
        }

        private struct Pt
        {
            internal int A;
            internal int B;

            internal double X(double[] v) => B < 0 ? v[A] : (v[A] + v[B]) / 2.0;

            internal double Y(double[] v) => B < 0 ? v[A + 1] : (v[A + 1] + v[B + 1]) / 2.0;

            internal void AddVars(List<int> vars)
            {
                vars.Add(A);
                vars.Add(A + 1);

                if (B >= 0)
                {
                    vars.Add(B);
                    vars.Add(B + 1);
                }
            }
        }

        private struct Seg
        {
            internal Pt P;
            internal Pt Q;
        }

        private struct Round
        {
            internal Pt C;
            internal int R;
        }

        private sealed class Problem
        {
            internal readonly List<double> X = new List<double>();
            internal readonly List<double> W = new List<double>();
            internal readonly Dictionary<string, int> At = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase
            );
            internal readonly Dictionary<string, CadShape> Shapes;
            internal readonly List<Row> Rows = new List<Row>();
            internal string Failure;

            internal Problem(IReadOnlyDictionary<string, CadShape> shapes)
            {
                Shapes = new Dictionary<string, CadShape>(StringComparer.OrdinalIgnoreCase);

                foreach (KeyValuePair<string, CadShape> pair in shapes)
                {
                    Shapes[pair.Key] = pair.Value;
                }
            }
        }

        /// <summary>
        /// Solves the constraints of a set for the objects given.
        /// </summary>
        /// <param name="set">The constraints.</param>
        /// <param name="shapes">Every object a constraint is tied to, by handle, where it stands now.</param>
        /// <param name="parameter">The value of a parameter by name, a length or an angle in degrees; null for none.</param>
        /// <param name="weight">The weight of each number of each object; null for 1 everywhere.</param>
        /// <returns>The result: the numbers solved, or why there are none.</returns>
        internal static CadSolveResult Solve(
            CadConstraintSet set,
            IReadOnlyDictionary<string, CadShape> shapes,
            Func<string, double?> parameter,
            Weighter weight
        )
        {
            var result = new CadSolveResult();
            var problem = new Problem(shapes);
            var handles = new List<string>();

            foreach (string handle in set.Handles())
            {
                if (!problem.Shapes.ContainsKey(handle))
                {
                    result.Message = "An object of the constraints is no longer in the drawing.";
                    return result;
                }

                handles.Add(handle);
            }

            foreach (string handle in handles)
            {
                CadShape shape = problem.Shapes[handle];
                problem.At[handle] = problem.X.Count;

                for (int i = 0; i < shape.Values.Length; i++)
                {
                    problem.X.Add(shape.Values[i]);
                    problem.W.Add(Math.Max(1E-12, weight == null ? 1.0 : weight(handle, i)));
                }

                // The ends of an arc lie on its circle.
                if (shape.Kind == CadShapeKind.Arc)
                {
                    int a = problem.At[handle];
                    AddRound(problem, a, a + 3);
                    AddRound(problem, a, a + 5);
                }
            }

            foreach (CadGeoConstraint constraint in set.Geometric)
            {
                if (!AddGeometric(problem, constraint))
                {
                    result.Message = problem.Failure ?? "A constraint is tied to an object it does not fit.";
                    return result;
                }
            }

            foreach (CadDimConstraint constraint in set.Dimensional)
            {
                if (constraint.IsReference)
                {
                    continue;
                }

                double? target = parameter?.Invoke(constraint.Name);

                if (!target.HasValue)
                {
                    result.Message = "The parameter " + constraint.Name + " has no value.";
                    return result;
                }

                if (!AddDimensional(problem, constraint, target.Value))
                {
                    result.Message = problem.Failure ?? "A constraint is tied to an object it does not fit.";
                    return result;
                }
            }

            double[] x = problem.X.ToArray();
            double[] x0 = (double[])x.Clone();
            double[] w = problem.W.ToArray();
            result.Equations = problem.Rows.Count;
            bool converged = true;

            foreach (List<int> component in Components(problem, x.Length))
            {
                if (!SolveComponent(problem, component, x, w))
                {
                    converged = false;
                }
            }

            // A number the solution left where it was but for a speck of rounding is put back exactly where it was and held
            // there, and the others are brought to meet the equations as closely as the numbers allow.
            if (converged)
            {
                for (int i = 0; i < x.Length; i++)
                {
                    if (Math.Abs(x[i] - x0[i]) <= 5E-10 * Math.Max(1.0, Math.Abs(x0[i])))
                    {
                        x[i] = x0[i];
                        w[i] = 1E40;
                    }
                }

                foreach (List<int> component in Components(problem, x.Length))
                {
                    if (!SolveComponent(problem, component, x, w))
                    {
                        converged = false;
                    }
                }
            }

            for (int i = 0; i < x.Length; i++)
            {
                if (Math.Abs(x[i]) < 1E-14)
                {
                    x[i] = 0.0;
                }
            }

            int rank = RankOf(problem.Rows, x, w);
            result.Rank = rank;
            result.Freedom = Math.Max(0, x.Length - rank);

            foreach (string handle in handles)
            {
                CadShape shape = problem.Shapes[handle];
                var values = new double[shape.Values.Length];
                Array.Copy(x, problem.At[handle], values, 0, values.Length);
                result.Values[handle] = values;
            }

            if (!converged)
            {
                result.Message = "Constraints cannot be satisfied.";
                return result;
            }

            string broken = Degenerate(problem, handles, x);

            if (broken != null)
            {
                result.Message = broken;
                return result;
            }

            result.Succeeded = true;
            return result;
        }

        /// <summary>Whether a line has shrunk to a point or a circle to nothing in the solution.</summary>
        private static string Degenerate(Problem problem, List<string> handles, double[] x)
        {
            foreach (string handle in handles)
            {
                CadShape shape = problem.Shapes[handle];
                int a = problem.At[handle];

                switch (shape.Kind)
                {
                    case CadShapeKind.Line:
                        if (Math.Abs(x[a + 2] - x[a]) + Math.Abs(x[a + 3] - x[a + 1]) < 1E-7)
                        {
                            return "Constraints cannot be satisfied.";
                        }

                        break;
                    case CadShapeKind.Circle:
                    case CadShapeKind.Arc:
                        if (x[a + 2] < 1E-7)
                        {
                            return "Constraints cannot be satisfied.";
                        }

                        break;
                    case CadShapeKind.Polyline:
                        for (int i = 0; i < shape.Segments; i++)
                        {
                            int p = a + 2 * i;
                            int q = a + 2 * ((i + 1) % shape.Vertices);

                            if (Math.Abs(x[q] - x[p]) + Math.Abs(x[q + 1] - x[p + 1]) < 1E-7)
                            {
                                return "Constraints cannot be satisfied.";
                            }
                        }

                        break;
                }
            }

            return null;
        }

        private static void AddRound(Problem problem, int c, int e)
        {
            problem.Rows.Add(
                new Row
                {
                    Vars = new[] { c, c + 1, c + 2, e, e + 1 },
                    F = v =>
                        Math.Sqrt((v[e] - v[c]) * (v[e] - v[c]) + (v[e + 1] - v[c + 1]) * (v[e + 1] - v[c + 1]))
                        - v[c + 2],
                }
            );
        }

        #region Resolving a reference

        private static bool TryPoint(Problem problem, CadRef r, out Pt point)
        {
            point = default;

            if (!problem.At.TryGetValue(r.Handle, out int a))
            {
                return false;
            }

            CadShape shape = problem.Shapes[r.Handle];

            switch (shape.Kind)
            {
                case CadShapeKind.Line:
                    switch (r.Part)
                    {
                        case CadPart.Start:
                            point = new Pt { A = a, B = -1 };
                            return true;
                        case CadPart.End:
                            point = new Pt { A = a + 2, B = -1 };
                            return true;
                        case CadPart.Mid:
                            point = new Pt { A = a, B = a + 2 };
                            return true;
                    }

                    return false;
                case CadShapeKind.Circle:
                    point = new Pt { A = a, B = -1 };
                    return r.Part == CadPart.Center;
                case CadShapeKind.Arc:
                    switch (r.Part)
                    {
                        case CadPart.Center:
                            point = new Pt { A = a, B = -1 };
                            return true;
                        case CadPart.Start:
                            point = new Pt { A = a + 3, B = -1 };
                            return true;
                        case CadPart.End:
                            point = new Pt { A = a + 5, B = -1 };
                            return true;
                    }

                    return false;
                case CadShapeKind.Polyline:
                    if (r.Segment < 0 || r.Segment >= shape.Segments)
                    {
                        return false;
                    }

                    int first = a + 2 * r.Segment;
                    int second = a + 2 * ((r.Segment + 1) % shape.Vertices);

                    switch (r.Part)
                    {
                        case CadPart.Start:
                            point = new Pt { A = first, B = -1 };
                            return true;
                        case CadPart.End:
                            point = new Pt { A = second, B = -1 };
                            return true;
                        case CadPart.Mid:
                            point = new Pt { A = first, B = second };
                            return true;
                    }

                    return false;
            }

            return false;
        }

        private static bool TrySeg(Problem problem, CadRef r, out Seg seg)
        {
            seg = default;

            if (r.Part != CadPart.Whole || !problem.At.TryGetValue(r.Handle, out int a))
            {
                return false;
            }

            CadShape shape = problem.Shapes[r.Handle];

            if (shape.Kind == CadShapeKind.Line)
            {
                seg = new Seg
                {
                    P = new Pt { A = a, B = -1 },
                    Q = new Pt { A = a + 2, B = -1 },
                };
                return true;
            }

            if (shape.Kind == CadShapeKind.Polyline && r.Segment >= 0 && r.Segment < shape.Segments)
            {
                int first = a + 2 * r.Segment;
                int second = a + 2 * ((r.Segment + 1) % shape.Vertices);
                seg = new Seg
                {
                    P = new Pt { A = first, B = -1 },
                    Q = new Pt { A = second, B = -1 },
                };
                return true;
            }

            return false;
        }

        private static bool TryRound(Problem problem, CadRef r, out Round round)
        {
            round = default;

            if (r.Part != CadPart.Whole || !problem.At.TryGetValue(r.Handle, out int a))
            {
                return false;
            }

            CadShape shape = problem.Shapes[r.Handle];

            if (shape.Kind != CadShapeKind.Circle && shape.Kind != CadShapeKind.Arc)
            {
                return false;
            }

            round = new Round
            {
                C = new Pt { A = a, B = -1 },
                R = a + 2,
            };
            return true;
        }

        #endregion

        #region Equations

        private static bool Fail(Problem problem, string why)
        {
            problem.Failure = why;
            return false;
        }

        private static int[] VarsOf(params Pt[] points)
        {
            var vars = new List<int>();

            foreach (Pt p in points)
            {
                p.AddVars(vars);
            }

            return vars.ToArray();
        }

        private static int[] VarsOf(Seg a, Seg b)
        {
            var vars = new List<int>();
            a.P.AddVars(vars);
            a.Q.AddVars(vars);
            b.P.AddVars(vars);
            b.Q.AddVars(vars);
            return vars.ToArray();
        }

        private static void AddRow(Problem problem, int[] vars, Func<double[], double> f) =>
            problem.Rows.Add(new Row { Vars = vars, F = f });

        private static bool AddGeometric(Problem problem, CadGeoConstraint c)
        {
            List<CadRef> refs = c.Refs;
            string bad = "A " + c.Label + " constraint is tied to objects it does not fit.";

            switch (c.Kind)
            {
                case CadGeoKind.Coincident:
                {
                    if (
                        refs.Count != 2
                        || !TryPoint(problem, refs[0], out Pt p)
                        || !TryPoint(problem, refs[1], out Pt q)
                    )
                    {
                        return Fail(problem, bad);
                    }

                    PointsEqual(problem, p, q);
                    return true;
                }

                case CadGeoKind.PointOnCurve:
                {
                    if (refs.Count != 2 || !TryPoint(problem, refs[0], out Pt p))
                    {
                        return Fail(problem, bad);
                    }

                    if (TrySeg(problem, refs[1], out Seg s))
                    {
                        OnLine(problem, p, s);
                        return true;
                    }

                    if (TryRound(problem, refs[1], out Round r))
                    {
                        AddRow(problem, new[] { p.A, p.A + 1, r.C.A, r.C.A + 1, r.R }, v =>
                            Math.Sqrt(Sq(p.X(v) - r.C.X(v)) + Sq(p.Y(v) - r.C.Y(v))) - v[r.R]
                        );
                        return true;
                    }

                    return Fail(problem, bad);
                }

                case CadGeoKind.Collinear:
                {
                    if (refs.Count != 2 || !TrySeg(problem, refs[0], out Seg a) || !TrySeg(problem, refs[1], out Seg b))
                    {
                        return Fail(problem, bad);
                    }

                    OnLine(problem, b.P, a);
                    OnLine(problem, b.Q, a);
                    return true;
                }

                case CadGeoKind.Concentric:
                {
                    if (
                        refs.Count != 2
                        || !TryRound(problem, refs[0], out Round a)
                        || !TryRound(problem, refs[1], out Round b)
                    )
                    {
                        return Fail(problem, bad);
                    }

                    PointsEqual(problem, a.C, b.C);
                    return true;
                }

                case CadGeoKind.Fix:
                {
                    if (
                        refs.Count != 1
                        || c.Place == null
                        || c.Place.Length < 2
                        || !TryPoint(problem, refs[0], out Pt p)
                    )
                    {
                        return Fail(problem, bad);
                    }

                    double fx = c.Place[0];
                    double fy = c.Place[1];
                    AddRow(problem, VarsOf(p), v => p.X(v) - fx);
                    AddRow(problem, VarsOf(p), v => p.Y(v) - fy);
                    return true;
                }

                case CadGeoKind.Parallel:
                case CadGeoKind.Perpendicular:
                {
                    if (refs.Count != 2 || !TrySeg(problem, refs[0], out Seg a) || !TrySeg(problem, refs[1], out Seg b))
                    {
                        return Fail(problem, bad);
                    }

                    bool parallel = c.Kind == CadGeoKind.Parallel;
                    AddRow(problem, VarsOf(a, b), v =>
                    {
                        double ux = a.Q.X(v) - a.P.X(v);
                        double uy = a.Q.Y(v) - a.P.Y(v);
                        double wx = b.Q.X(v) - b.P.X(v);
                        double wy = b.Q.Y(v) - b.P.Y(v);
                        double n = Math.Sqrt((ux * ux + uy * uy) * (wx * wx + wy * wy));

                        if (n < 1E-14)
                        {
                            return 0.0;
                        }

                        return parallel ? (ux * wy - uy * wx) / n : (ux * wx + uy * wy) / n;
                    });
                    return true;
                }

                case CadGeoKind.Horizontal:
                case CadGeoKind.Vertical:
                {
                    bool horizontal = c.Kind == CadGeoKind.Horizontal;

                    if (refs.Count == 1 && TrySeg(problem, refs[0], out Seg s))
                    {
                        AddRow(problem, VarsOf(s.P, s.Q), v => horizontal ? s.Q.Y(v) - s.P.Y(v) : s.Q.X(v) - s.P.X(v));
                        return true;
                    }

                    if (refs.Count == 2 && TryPoint(problem, refs[0], out Pt p) && TryPoint(problem, refs[1], out Pt q))
                    {
                        AddRow(problem, VarsOf(p, q), v => horizontal ? q.Y(v) - p.Y(v) : q.X(v) - p.X(v));
                        return true;
                    }

                    return Fail(problem, bad);
                }

                case CadGeoKind.Tangent:
                    return AddTangent(problem, c, bad);

                case CadGeoKind.Symmetric:
                    return AddSymmetric(problem, c, bad);

                case CadGeoKind.EqualLength:
                {
                    if (refs.Count != 2 || !TrySeg(problem, refs[0], out Seg a) || !TrySeg(problem, refs[1], out Seg b))
                    {
                        return Fail(problem, bad);
                    }

                    AddRow(problem, VarsOf(a, b), v =>
                        Math.Sqrt(Sq(a.Q.X(v) - a.P.X(v)) + Sq(a.Q.Y(v) - a.P.Y(v)))
                        - Math.Sqrt(Sq(b.Q.X(v) - b.P.X(v)) + Sq(b.Q.Y(v) - b.P.Y(v)))
                    );
                    return true;
                }

                case CadGeoKind.EqualRadius:
                {
                    if (
                        refs.Count != 2
                        || !TryRound(problem, refs[0], out Round a)
                        || !TryRound(problem, refs[1], out Round b)
                    )
                    {
                        return Fail(problem, bad);
                    }

                    AddRow(problem, new[] { a.R, b.R }, v => v[a.R] - v[b.R]);
                    return true;
                }
            }

            return Fail(problem, bad);
        }

        private static double Sq(double d) => d * d;

        private static void PointsEqual(Problem problem, Pt p, Pt q)
        {
            AddRow(problem, VarsOf(p, q), v => p.X(v) - q.X(v));
            AddRow(problem, VarsOf(p, q), v => p.Y(v) - q.Y(v));
        }

        private static void OnLine(Problem problem, Pt p, Seg s) => AddRow(problem, VarsOf(new[] { p, s.P, s.Q }), v =>
            {
                double dx = s.Q.X(v) - s.P.X(v);
                double dy = s.Q.Y(v) - s.P.Y(v);
                double n = Math.Sqrt(dx * dx + dy * dy);
                return n < 1E-14 ? 0.0 : (dx * (p.Y(v) - s.P.Y(v)) - dy * (p.X(v) - s.P.X(v))) / n;
            });

        private static bool AddTangent(Problem problem, CadGeoConstraint c, string bad)
        {
            if (c.Refs.Count != 2)
            {
                return Fail(problem, bad);
            }

            bool firstLine = TrySeg(problem, c.Refs[0], out Seg line);
            Round round;

            if (firstLine || TrySeg(problem, c.Refs[1], out line))
            {
                CadRef other = firstLine ? c.Refs[1] : c.Refs[0];

                if (!TryRound(problem, other, out round))
                {
                    return Fail(problem, bad);
                }

                int side = c.Side >= 0 ? 1 : -1;
                Seg l = line;
                Round r = round;
                AddRow(problem, new[] { l.P.A, l.P.A + 1, l.Q.A, l.Q.A + 1, r.C.A, r.C.A + 1, r.R }, v =>
                {
                    double dx = l.Q.X(v) - l.P.X(v);
                    double dy = l.Q.Y(v) - l.P.Y(v);
                    double n = Math.Sqrt(dx * dx + dy * dy);

                    if (n < 1E-14)
                    {
                        return 0.0;
                    }

                    double across = (dx * (r.C.Y(v) - l.P.Y(v)) - dy * (r.C.X(v) - l.P.X(v))) / n;
                    return side * across - v[r.R];
                });
                return true;
            }

            if (!TryRound(problem, c.Refs[0], out Round a) || !TryRound(problem, c.Refs[1], out Round b))
            {
                return Fail(problem, bad);
            }

            int kind = c.Side == 0 ? 1 : c.Side;
            AddRow(problem, new[] { a.C.A, a.C.A + 1, a.R, b.C.A, b.C.A + 1, b.R }, v =>
            {
                double dist = Math.Sqrt(Sq(a.C.X(v) - b.C.X(v)) + Sq(a.C.Y(v) - b.C.Y(v)));
                double wanted = kind == 1 ? v[a.R] + v[b.R] : (kind == -1 ? v[a.R] - v[b.R] : v[b.R] - v[a.R]);
                return dist - wanted;
            });
            return true;
        }

        private static bool AddSymmetric(Problem problem, CadGeoConstraint c, string bad)
        {
            if (c.Refs.Count != 3 || !TrySeg(problem, c.Refs[2], out Seg axis))
            {
                return Fail(problem, bad);
            }

            if (TryPoint(problem, c.Refs[0], out Pt p) && TryPoint(problem, c.Refs[1], out Pt q))
            {
                Mirror(problem, p, q, axis);
                return true;
            }

            if (TrySeg(problem, c.Refs[0], out Seg a) && TrySeg(problem, c.Refs[1], out Seg b))
            {
                Mirror(problem, a.P, b.P, axis);
                Mirror(problem, a.Q, b.Q, axis);
                return true;
            }

            if (TryRound(problem, c.Refs[0], out Round m) && TryRound(problem, c.Refs[1], out Round n))
            {
                Mirror(problem, m.C, n.C, axis);
                AddRow(problem, new[] { m.R, n.R }, v => v[m.R] - v[n.R]);
                return true;
            }

            return Fail(problem, bad);
        }

        private static void Mirror(Problem problem, Pt p, Pt q, Seg axis)
        {
            int[] vars = VarsOf(new[] { p, q, axis.P, axis.Q });

            // The middle of the two lies on the axis, and the way from one to the other is square to it.
            AddRow(problem, vars, v =>
            {
                double dx = axis.Q.X(v) - axis.P.X(v);
                double dy = axis.Q.Y(v) - axis.P.Y(v);
                double len = Math.Sqrt(dx * dx + dy * dy);

                if (len < 1E-14)
                {
                    return 0.0;
                }

                double mx = (p.X(v) + q.X(v)) / 2.0 - axis.P.X(v);
                double my = (p.Y(v) + q.Y(v)) / 2.0 - axis.P.Y(v);
                return (dx * my - dy * mx) / len;
            });
            AddRow(problem, vars, v =>
            {
                double dx = axis.Q.X(v) - axis.P.X(v);
                double dy = axis.Q.Y(v) - axis.P.Y(v);
                double len = Math.Sqrt(dx * dx + dy * dy);
                return len < 1E-14 ? 0.0 : (dx * (q.X(v) - p.X(v)) + dy * (q.Y(v) - p.Y(v))) / len;
            });
        }

        private static bool AddDimensional(Problem problem, CadDimConstraint c, double target)
        {
            string bad = "A " + c.Kind + " dimensional constraint is tied to objects it does not fit.";
            List<CadRef> refs = c.Refs;

            switch (c.Kind)
            {
                case CadDimKind.Horizontal:
                case CadDimKind.Vertical:
                case CadDimKind.Aligned:
                {
                    if (
                        refs.Count != 2
                        || !TryPoint(problem, refs[0], out Pt p)
                        || !TryPoint(problem, refs[1], out Pt q)
                    )
                    {
                        return Fail(problem, bad);
                    }

                    int sign = c.Direction >= 0 ? 1 : -1;
                    CadDimKind kind = c.Kind;
                    AddRow(problem, VarsOf(p, q), v =>
                    {
                        switch (kind)
                        {
                            case CadDimKind.Horizontal:
                                return sign * (q.X(v) - p.X(v)) - target;
                            case CadDimKind.Vertical:
                                return sign * (q.Y(v) - p.Y(v)) - target;
                            default:
                                return Math.Sqrt(Sq(q.X(v) - p.X(v)) + Sq(q.Y(v) - p.Y(v))) - target;
                        }
                    });
                    return true;
                }

                case CadDimKind.Angular:
                {
                    if (refs.Count != 2 || !TrySeg(problem, refs[0], out Seg a) || !TrySeg(problem, refs[1], out Seg b))
                    {
                        return Fail(problem, bad);
                    }

                    double radians = target * Math.PI / 180.0;
                    AddRow(problem, VarsOf(a, b), v =>
                    {
                        double ux = a.Q.X(v) - a.P.X(v);
                        double uy = a.Q.Y(v) - a.P.Y(v);
                        double wx = b.Q.X(v) - b.P.X(v);
                        double wy = b.Q.Y(v) - b.P.Y(v);
                        double turn = Math.Atan2(ux * wy - uy * wx, ux * wx + uy * wy) - radians;
                        turn %= 2.0 * Math.PI;

                        if (turn > Math.PI)
                        {
                            turn -= 2.0 * Math.PI;
                        }
                        else if (turn < -Math.PI)
                        {
                            turn += 2.0 * Math.PI;
                        }

                        return turn;
                    });
                    return true;
                }

                case CadDimKind.Radius:
                case CadDimKind.Diameter:
                {
                    if (refs.Count != 1 || !TryRound(problem, refs[0], out Round r))
                    {
                        return Fail(problem, bad);
                    }

                    double factor = c.Kind == CadDimKind.Diameter ? 2.0 : 1.0;
                    AddRow(problem, new[] { r.R }, v => factor * v[r.R] - target);
                    return true;
                }
            }

            return Fail(problem, bad);
        }

        #endregion

        #region Solving

        /// <summary>The groups of unknowns that some equation joins, each in ordinal order of its first unknown.</summary>
        private static List<List<int>> Components(Problem problem, int n)
        {
            var parent = new int[n];

            for (int i = 0; i < n; i++)
            {
                parent[i] = i;
            }

            int Find(int i)
            {
                while (parent[i] != i)
                {
                    parent[i] = parent[parent[i]];
                    i = parent[i];
                }

                return i;
            }

            foreach (Row row in problem.Rows)
            {
                for (int k = 1; k < row.Vars.Length; k++)
                {
                    int a = Find(row.Vars[0]);
                    int b = Find(row.Vars[k]);

                    if (a != b)
                    {
                        parent[Math.Max(a, b)] = Math.Min(a, b);
                    }
                }
            }

            var byRoot = new SortedDictionary<int, List<int>>();
            var used = new bool[n];

            foreach (Row row in problem.Rows)
            {
                foreach (int v in row.Vars)
                {
                    used[v] = true;
                }
            }

            for (int i = 0; i < n; i++)
            {
                if (!used[i])
                {
                    continue;
                }

                int root = Find(i);

                if (!byRoot.TryGetValue(root, out List<int> list))
                {
                    byRoot[root] = list = new List<int>();
                }

                list.Add(i);
            }

            return new List<List<int>>(byRoot.Values);
        }

        private static bool SolveComponent(Problem problem, List<int> vars, double[] x, double[] w)
        {
            var inside = new HashSet<int>(vars);
            var rows = new List<Row>();

            foreach (Row row in problem.Rows)
            {
                if (inside.Contains(row.Vars[0]))
                {
                    rows.Add(row);
                }
            }

            int m = rows.Count;
            int n = vars.Count;

            if (m == 0)
            {
                return true;
            }

            var local = new Dictionary<int, int>();

            for (int i = 0; i < n; i++)
            {
                local[vars[i]] = i;
            }

            double[] r = Residuals(rows, x);

            // The size of the things the equations tie, which a step may be a third of.
            double extent = 0.0;
            int counted = 0;

            foreach (KeyValuePair<string, CadShape> pair in problem.Shapes)
            {
                if (!problem.At.TryGetValue(pair.Key, out int start) || !inside.Contains(start))
                {
                    continue;
                }

                extent += SizeOf(pair.Value, x, start);
                counted++;
            }

            double reach = Math.Max(1E-6, counted == 0 ? 1.0 : 0.3 * extent / counted);

            for (int step = 0; step < Steps; step++)
            {
                if (MaxAbs(r) <= Target)
                {
                    return true;
                }

                double[,] j = Jacobian(rows, x, local, n);

                // The least change of the numbers (by weight) that meets the equations as they are linear here:
                // (J W^-1 J^T) lambda = r, and x_new = x - W^-1 J^T lambda.
                var a = new double[m, m];
                var rhs = new double[m];

                for (int p = 0; p < m; p++)
                {
                    rhs[p] = r[p];

                    for (int q = p; q < m; q++)
                    {
                        double sum = 0.0;

                        for (int k = 0; k < n; k++)
                        {
                            sum += j[p, k] * j[q, k] / w[vars[k]];
                        }

                        a[p, q] = sum;
                        a[q, p] = sum;
                    }
                }

                // A speck of each row's own size is added to its diagonal, so that rows that repeat one another can be solved and
                // a row that only heavy numbers can meet is not drowned by the lighter rows.
                for (int p = 0; p < m; p++)
                {
                    a[p, p] += 1E-11 * Math.Max(a[p, p], 1E-300);
                }

                double[] lambda = SolveLinear(a, rhs, m);

                if (lambda == null)
                {
                    return false;
                }

                var next = new double[n];

                for (int k = 0; k < n; k++)
                {
                    double back = 0.0;

                    for (int p = 0; p < m; p++)
                    {
                        back += j[p, k] * lambda[p];
                    }

                    next[k] = x[vars[k]] - back / w[vars[k]];
                }

                // A step is kept short of the size of what it moves, so that the objects are brought to the equations by the
                // nearest way and not thrown far where the equations are bent.
                double longest = 0.0;

                for (int k = 0; k < n; k++)
                {
                    longest = Math.Max(longest, Math.Abs(next[k] - x[vars[k]]));
                }

                if (longest > reach)
                {
                    double damp = reach / longest;

                    for (int k = 0; k < n; k++)
                    {
                        next[k] = x[vars[k]] + damp * (next[k] - x[vars[k]]);
                    }
                }

                // Take the whole step, or a half, a quarter ... while the equations do not come nearer.
                double before = MaxAbs(r);
                double scale = 1.0;
                var saved = new double[n];

                for (int k = 0; k < n; k++)
                {
                    saved[k] = x[vars[k]];
                }

                double[] after = null;

                for (int tries = 0; tries < 12; tries++)
                {
                    for (int k = 0; k < n; k++)
                    {
                        x[vars[k]] = saved[k] + scale * (next[k] - saved[k]);
                    }

                    after = Residuals(rows, x);

                    if (MaxAbs(after) < before)
                    {
                        break;
                    }

                    scale /= 2.0;
                }

                if (MaxAbs(after) >= before && MaxAbs(after) > Tolerance)
                {
                    for (int k = 0; k < n; k++)
                    {
                        x[vars[k]] = saved[k];
                    }

                    return MaxAbs(r) <= Tolerance;
                }

                r = after;
            }

            return MaxAbs(r) <= Tolerance;
        }

        /// <summary>How large an object is: the longer side of the box of a line or polyline, the radius of a circle or arc.</summary>
        private static double SizeOf(CadShape shape, double[] x, int at)
        {
            if (shape.Kind == CadShapeKind.Circle || shape.Kind == CadShapeKind.Arc)
            {
                return Math.Abs(x[at + 2]);
            }

            double lowX = double.MaxValue;
            double highX = double.MinValue;
            double lowY = double.MaxValue;
            double highY = double.MinValue;

            for (int i = 0; i + 1 < shape.Values.Length; i += 2)
            {
                lowX = Math.Min(lowX, x[at + i]);
                highX = Math.Max(highX, x[at + i]);
                lowY = Math.Min(lowY, x[at + i + 1]);
                highY = Math.Max(highY, x[at + i + 1]);
            }

            return Math.Max(highX - lowX, highY - lowY);
        }

        private static double[] Residuals(List<Row> rows, double[] x)
        {
            var r = new double[rows.Count];

            for (int i = 0; i < r.Length; i++)
            {
                r[i] = rows[i].F(x);
            }

            return r;
        }

        private static double MaxAbs(double[] r)
        {
            double max = 0.0;

            foreach (double d in r)
            {
                max = Math.Max(max, Math.Abs(d));
            }

            return max;
        }

        private static double[,] Jacobian(List<Row> rows, double[] x, Dictionary<int, int> local, int n)
        {
            var j = new double[rows.Count, n];

            for (int p = 0; p < rows.Count; p++)
            {
                foreach (int v in rows[p].Vars)
                {
                    double old = x[v];
                    double h = 1E-6 * Math.Max(1.0, Math.Abs(old));
                    x[v] = old + h;
                    double up = rows[p].F(x);
                    x[v] = old - h;
                    double down = rows[p].F(x);
                    x[v] = old;
                    j[p, local[v]] = (up - down) / (2.0 * h);
                }
            }

            return j;
        }

        /// <summary>Solves a x = b by Gaussian elimination with the largest pivot of each column; null where it is singular.</summary>
        private static double[] SolveLinear(double[,] a, double[] b, int m)
        {
            var x = new double[m];
            var rhs = (double[])b.Clone();

            for (int c = 0; c < m; c++)
            {
                int best = c;

                for (int r = c + 1; r < m; r++)
                {
                    if (Math.Abs(a[r, c]) > Math.Abs(a[best, c]))
                    {
                        best = r;
                    }
                }

                if (Math.Abs(a[best, c]) < 1E-300)
                {
                    return null;
                }

                if (best != c)
                {
                    for (int k = 0; k < m; k++)
                    {
                        double t = a[c, k];
                        a[c, k] = a[best, k];
                        a[best, k] = t;
                    }

                    double s = rhs[c];
                    rhs[c] = rhs[best];
                    rhs[best] = s;
                }

                for (int r = c + 1; r < m; r++)
                {
                    double f = a[r, c] / a[c, c];

                    if (f == 0.0)
                    {
                        continue;
                    }

                    for (int k = c; k < m; k++)
                    {
                        a[r, k] -= f * a[c, k];
                    }

                    rhs[r] -= f * rhs[c];
                }
            }

            for (int r = m - 1; r >= 0; r--)
            {
                double sum = rhs[r];

                for (int k = r + 1; k < m; k++)
                {
                    sum -= a[r, k] * x[k];
                }

                x[r] = sum / a[r, r];
            }

            return x;
        }

        /// <summary>
        /// The rank of the equations at a place: how many of them are independent, by Gram-Schmidt on their rows with each
        /// column scaled by the weight of its number, a row counting where it leaves more than a thousandth of its length.
        /// </summary>
        private static int RankOf(List<Row> rows, double[] x, double[] w)
        {
            if (rows.Count == 0)
            {
                return 0;
            }

            int n = x.Length;
            var basis = new List<double[]>();

            foreach (Row row in rows)
            {
                var d = new double[n];

                foreach (int v in row.Vars)
                {
                    double old = x[v];
                    double h = 1E-6 * Math.Max(1.0, Math.Abs(old));
                    x[v] = old + h;
                    double up = row.F(x);
                    x[v] = old - h;
                    double down = row.F(x);
                    x[v] = old;
                    d[v] = (up - down) / (2.0 * h);
                }

                double length = Norm(d);

                if (length < 1E-12)
                {
                    continue;
                }

                for (int k = 0; k < n; k++)
                {
                    d[k] /= length;
                }

                foreach (double[] b in basis)
                {
                    double dot = 0.0;

                    for (int k = 0; k < n; k++)
                    {
                        dot += d[k] * b[k];
                    }

                    for (int k = 0; k < n; k++)
                    {
                        d[k] -= dot * b[k];
                    }
                }

                double left = Norm(d);

                if (left > 1E-3)
                {
                    for (int k = 0; k < n; k++)
                    {
                        d[k] /= left;
                    }

                    basis.Add(d);
                }
            }

            return basis.Count;
        }

        private static double Norm(double[] d)
        {
            double s = 0.0;

            foreach (double v in d)
            {
                s += v * v;
            }

            return Math.Sqrt(s);
        }

        #endregion

        /// <summary>Writes a number as the history of the command line writes a length: four places.</summary>
        internal static string Text(double value) =>
            (Math.Abs(value) < 5E-5 ? 0.0 : value).ToString("0.####", CultureInfo.InvariantCulture);
    }
}
