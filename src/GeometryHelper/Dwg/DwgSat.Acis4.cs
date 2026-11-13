using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// The SAT of R2000, ACIS 4.00, turned into the ASM records of R2004 and back. Of the same body, AutoCAD 2021 writes
    /// R2000's text with no <c>asmheader</c> (so every pointer stands one lower), with no history on a record (a whole
    /// number of -1 and a pointer after the first value of the ASM record, the whole number alone on an attribute and on a
    /// transform), with no parameters and no convexity on an edge, with a text as its length and the word (<c>13 text</c>,
    /// not <c>@13 text</c>), with no fourth enum on the name attribute of a loft, and with no <c>full</c> before the spline
    /// of an exact curve or surface; every other value is the same. Going up, the parameters of an edge are worked out from
    /// its vertices and its curve (a line's from its root along its direction, an ellipse's as the angle of the vertex from
    /// the major axis, a spline's from its first and last knot where its ends are the vertices), the way AutoCAD 2021 does
    /// as it reads such a file; the convexity is <c>unknown</c>, the only word R2000 can give it.
    /// </summary>
    internal static partial class DwgSat
    {
        /// <summary>The version of the modeler an ACIS 4.00 SAT gives in its head.</summary>
        private const int Acis4Version = 400;

        /// <summary>The records that carry a history number but no history pointer: the attributes and the transform.</summary>
        private static bool HasNoHistoryPointer(string name) =>
            name == "transform" || name.EndsWith("-attrib", StringComparison.Ordinal);

        /// <summary>The subtypes of a spline whose ASM form starts with the word <c>full</c> that ACIS 4.00 leaves out.</summary>
        private static bool HasFullWord(string subtype) =>
            subtype == "exactcur" || subtype == "parcur" || subtype == "surfintcur" || subtype == "exactsur";

        /// <summary>The subtypes of a spline that ACIS 4.00 has, by the names R2004 gives them (R2007 renamed four of them).</summary>
        private static bool IsOfAcis4(string subtype) => HasFullWord(subtype) || subtype == "exppc" || subtype == "ref";

        /// <summary>The grammar of a record in ACIS 4.00: that of ASM without the history number and pointer.</summary>
        private static string Acis4Grammar(string name, string grammar)
        {
            if (name == "int64_attrib-name_attrib-gen-attrib")
            {
                return "* e(2=copy) e(4=keep_all) e(1=ignore) *";
            }

            return grammar.StartsWith("r i r ", StringComparison.Ordinal) ? "r " + grammar.Substring(6) : grammar;
        }

        /// <summary>
        /// A record of ACIS 4.00 as the values of the ASM record of R2004 it becomes: pointers one higher, the history
        /// after the first value, the parameters (nought, to be worked out when all records are read) and the convexity
        /// on an edge, the fourth enum on the name attribute, the word <c>full</c> in the subtypes that have it; null where
        /// the record is none of these.
        /// </summary>
        private static List<RawToken> LiftRecord(List<RawToken> raw)
        {
            if (raw.Count < 2 || raw[0].IsText || !IsPointer(raw[1]))
            {
                return null;
            }

            string name = raw[0].Value;
            var lifted = new List<RawToken>(raw.Count + 8) { raw[0], Shifted(raw[1]), new RawToken(false, "-1") };

            if (!HasNoHistoryPointer(name))
            {
                lifted.Add(new RawToken(false, "$-1"));
            }

            if (name == "edge")
            {
                // attrib start end coedge curve sense: attrib, history, start, its parameter, end, its parameter, the rest, convexity.
                if (
                    raw.Count != 7
                    || !IsPointer(raw[2])
                    || !IsPointer(raw[3])
                    || !IsPointer(raw[4])
                    || !IsPointer(raw[5])
                )
                {
                    return null;
                }

                lifted.Add(Shifted(raw[2]));
                lifted.Add(new RawToken(false, "0"));
                lifted.Add(Shifted(raw[3]));
                lifted.Add(new RawToken(false, "0"));
                lifted.Add(Shifted(raw[4]));
                lifted.Add(Shifted(raw[5]));
                lifted.Add(raw[6]);
                lifted.Add(new RawToken(true, "unknown"));
                return lifted;
            }

            for (int i = 2; i < raw.Count; i++)
            {
                RawToken token = raw[i];

                if (token.IsText)
                {
                    return null;
                }

                if (name == "int64_attrib-name_attrib-gen-attrib" && token.Value == "ignore")
                {
                    // ... ignore 13 loft_end_face 0: the text stands as its length and the words, and ASM has an enum before it.
                    lifted.Add(token);

                    if (
                        i + 2 >= raw.Count
                        || !int.TryParse(
                            raw[i + 1].Value,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out int length
                        )
                    )
                    {
                        return null;
                    }

                    string text = raw[i + 2].Value;
                    int next = i + 3;

                    while (text.Length < length && next < raw.Count - 1)
                    {
                        text += " " + raw[next++].Value;
                    }

                    if (text.Length != length)
                    {
                        return null;
                    }

                    lifted.Add(new RawToken(false, "copy"));
                    lifted.Add(new RawToken(true, text));
                    i = next - 1;
                    continue;
                }

                lifted.Add(IsPointer(token) ? Shifted(token) : token);

                if (token.Value == "{" && i + 1 < raw.Count && HasFullWord(raw[i + 1].Value))
                {
                    lifted.Add(raw[++i]);
                    lifted.Add(new RawToken(false, "full"));
                }
            }

            return lifted;
        }

        private static bool IsPointer(RawToken token) =>
            !token.IsText
            && token.Value.Length > 1
            && token.Value[0] == '$'
            && int.TryParse(
                token.Value.Substring(1),
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out int _
            );

        /// <summary>A pointer one record on, for the asmheader that ACIS 4.00 has not (a pointer to nothing stays).</summary>
        private static RawToken Shifted(RawToken pointer)
        {
            int target = int.Parse(
                pointer.Value.Substring(1),
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture
            );
            return target < 0
                ? pointer
                : new RawToken(false, "$" + (target + 1).ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Completes the records of an ACIS 4.00 SAT read as ASM's: the asmheader in front (its version that of the modeler
        /// in the head), the head's version and roots those of R2004, the parameters of every edge. False where an edge's
        /// parameters cannot be worked out.
        /// </summary>
        private static bool Finish(SatBody read)
        {
            string modeler = read.Strings[1];
            string[] words = modeler.Split(' ');
            string version = words.Length > 1 && words[0] == "ASM" ? words[1] : "226.0.0.65535";
            read.Records.Insert(
                0,
                new List<Token>
                {
                    new Token { Kind = 'N', Text = "asmheader" },
                    new Token { Kind = 'r', Whole = -1 },
                    new Token { Kind = 'i', Whole = -1 },
                    new Token { Kind = 's', Text = version },
                }
            );
            read.Roots = 2;

            for (int i = 0; i < read.Records.Count; i++)
            {
                if (NameOf(read.Records[i]) == "edge" && !EdgeParameters(read.Records, read.Records[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The parameters of an edge on its curve, where the vertices stand: a line gives the distance of each along its
        /// direction from its root, an ellipse the angle of the first from its major axis and the second after it (a full turn
        /// on from the first where both are one point), a spline the first and last knot where it begins at the first vertex
        /// and ends at the second, an edge with no curve 1 and 0. False where the edge runs the other way, or the curve is
        /// another or does not pass the vertices.
        /// </summary>
        private static bool EdgeParameters(List<List<Token>> records, List<Token> edge)
        {
            int v = FirstValue(edge);

            // r i r r d r d r r b s: the vertices, the parameters, the coedge, the curve and the sense.
            if (edge.Count != v + 11 || edge[v + 8].Kind != 'r' || edge[v + 9].Kind != 'b' || edge[v + 9].Whole != 0)
            {
                return false;
            }

            if (
                !PointOf(records, edge[v + 3].Whole, out double[] start)
                || !PointOf(records, edge[v + 5].Whole, out double[] end)
            )
            {
                return false;
            }

            double from;
            double to;

            if (edge[v + 8].Whole < 0)
            {
                from = 1.0;
                to = 0.0;
            }
            else if (
                edge[v + 8].Whole >= records.Count
                || !Range(records[(int)edge[v + 8].Whole], start, end, out from, out to)
            )
            {
                return false;
            }

            edge[v + 4] = new Token { Kind = 'd', Real = from == 0.0 ? 0.0 : from };
            edge[v + 6] = new Token { Kind = 'd', Real = to == 0.0 ? 0.0 : to };
            return true;
        }

        /// <summary>The position of the point of a vertex.</summary>
        private static bool PointOf(List<List<Token>> records, long vertex, out double[] point)
        {
            point = null;

            if (vertex < 0 || vertex >= records.Count)
            {
                return false;
            }

            List<Token> record = records[(int)vertex];
            int v = FirstValue(record);

            if (NameOf(record) != "vertex" || record.Count != v + 5 || record[v + 4].Kind != 'r')
            {
                return false;
            }

            long at = record[v + 4].Whole;

            if (at < 0 || at >= records.Count)
            {
                return false;
            }

            List<Token> found = records[(int)at];
            int w = FirstValue(found);

            if (NameOf(found) != "point" || found.Count != w + 4 || found[w + 3].Kind != 'P')
            {
                return false;
            }

            point = found[w + 3].Triple;
            return true;
        }

        private static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

        private static double[] Minus(double[] a, double[] b) => new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };

        private static bool Near(double[] a, double[] b)
        {
            double[] d = Minus(a, b);
            return Math.Sqrt(Dot(d, d)) < 1E-6;
        }

        /// <summary>The parameters at <paramref name="start"/> and <paramref name="end"/> on a curve record; false where it is a curve not known.</summary>
        private static bool Range(List<Token> curve, double[] start, double[] end, out double from, out double to)
        {
            from = 0.0;
            to = 0.0;
            int v = FirstValue(curve);
            string name = NameOf(curve);

            if (
                name == "straight-curve"
                && curve.Count >= v + 5
                && curve[v + 3].Kind == 'P'
                && curve[v + 4].Kind == 'V'
            )
            {
                double[] root = curve[v + 3].Triple;
                double[] direction = curve[v + 4].Triple;
                from = Dot(Minus(start, root), direction);
                to = Dot(Minus(end, root), direction);
                Snap(curve, v + 5, ref from, ref to);
                return from < to;
            }

            if (
                name == "ellipse-curve"
                && curve.Count >= v + 7
                && curve[v + 3].Kind == 'P'
                && curve[v + 4].Kind == 'V'
                && curve[v + 5].Kind == 'V'
                && curve[v + 6].Kind == 'd'
            )
            {
                double[] centre = curve[v + 3].Triple;
                double[] normal = curve[v + 4].Triple;
                double[] major = curve[v + 5].Triple;
                double ratio = curve[v + 6].Real;
                double[] minor =
                {
                    (normal[1] * major[2] - normal[2] * major[1]) * ratio,
                    (normal[2] * major[0] - normal[0] * major[2]) * ratio,
                    (normal[0] * major[1] - normal[1] * major[0]) * ratio,
                };

                if (!(Dot(major, major) > 0.0) || !(Dot(minor, minor) > 0.0))
                {
                    return false;
                }

                from = AngleOf(centre, major, minor, start);
                to = Near(start, end) ? from + 2.0 * Math.PI : AngleOf(centre, major, minor, end);

                while (to <= from)
                {
                    to += 2.0 * Math.PI;
                }

                Snap(curve, v + 7, ref from, ref to);
                return true;
            }

            return name == "intcurve-curve" && SplineRange(curve, start, end, out from, out to);
        }

        /// <summary>The angle of a point on an ellipse from its major axis, in [-pi, pi): a point on the axis behind the centre is -pi.</summary>
        private static double AngleOf(double[] centre, double[] major, double[] minor, double[] point)
        {
            double[] q = Minus(point, centre);
            double angle = Math.Atan2(Dot(q, minor) / Dot(minor, minor), Dot(q, major) / Dot(major, major));
            return angle >= Math.PI - 1E-9 ? angle - 2.0 * Math.PI : angle;
        }

        /// <summary>
        /// Sets a parameter that lies at a bound of the curve to the bound itself (the bounds are the pair of an <c>I</c> or an
        /// <c>F</c> and its value that end the curve's values, from <paramref name="at"/>): the angle of a vertex is a few
        /// 1E-15 off where the curve starts, and AutoCAD gives the edge the curve's own bound.
        /// </summary>
        private static void Snap(List<Token> curve, int at, ref double from, ref double to)
        {
            double? lower = null;
            double? upper = null;

            for (int bound = 0; bound < 2 && at < curve.Count && curve[at].Kind == 'b'; bound++)
            {
                double? value = null;

                if (curve[at].Whole == 1 && at + 1 < curve.Count && curve[at + 1].Kind == 'd')
                {
                    value = curve[++at].Real;
                }

                at++;

                if (bound == 0)
                {
                    lower = value;
                }
                else
                {
                    upper = value;
                }
            }

            if (lower.HasValue && Math.Abs(from - lower.Value) < 1E-9)
            {
                from = lower.Value;
            }

            if (upper.HasValue && Math.Abs(to - upper.Value) < 1E-9)
            {
                to = upper.Value;
            }
        }

        /// <summary>
        /// The number of the version of ASM that a curve of intersection of the modeler's own holds after its name: AutoCAD
        /// writes 20900 where it reads the curve from ACIS 4.00 (and 22600 where it made the curve itself).
        /// </summary>
        private static void Dated(List<List<Token>> records)
        {
            foreach (List<Token> record in records)
            {
                for (int i = 0; i + 2 < record.Count; i++)
                {
                    if (
                        record[i].Kind == '{'
                        && record[i + 1].Kind == 'N'
                        && (
                            record[i + 1].Text == "exact_int_cur"
                            || record[i + 1].Text == "int_int_cur"
                            || record[i + 1].Text == "par_int_cur"
                        )
                        && record[i + 2].Kind == 'i'
                        && record[i + 2].Whole == 22600
                    )
                    {
                        record[i + 2] = new Token { Kind = 'i', Whole = 20900 };
                    }
                }
            }
        }

        /// <summary>
        /// The first and last knot of the exact spline of a curve of intersection whose ends are the vertices; false for any
        /// other form of spline, a closed one, or one whose ends are not the vertices.
        /// </summary>
        private static bool SplineRange(List<Token> curve, double[] start, double[] end, out double from, out double to)
        {
            from = 0.0;
            to = 0.0;
            int open = curve.FindIndex(t => t.Kind == '{');

            if (
                open < 0
                || open + 6 >= curve.Count
                || (curve[open + 1].Text != "exactcur" && curve[open + 1].Text != "parcur")
                || curve[open + 2].Kind != 'e'
            )
            {
                return false;
            }

            int at = open + 3;
            bool rational = curve[at].Text == "nurbs";

            if (curve[at].Kind != 'N' || (!rational && curve[at].Text != "nubs"))
            {
                return false;
            }

            // degree, closure (0 open), count of knots, the knots with their multiplicities, the points.
            if (
                curve[at + 1].Kind != 'i'
                || curve[at + 2].Kind != 'e'
                || curve[at + 2].Whole != 0
                || curve[at + 3].Kind != 'i'
            )
            {
                return false;
            }

            long degree = curve[at + 1].Whole;
            long knots = curve[at + 3].Whole;
            int first = at + 4;
            long sum = 0;

            if (knots < 2 || first + 2 * knots > curve.Count)
            {
                return false;
            }

            for (long i = 0; i < knots; i++)
            {
                sum += curve[first + (int)(2 * i) + 1].Whole;
            }

            int size = rational ? 4 : 3;
            long points = sum - degree + 1;
            int firstPoint = first + (int)(2 * knots);

            if (points < 2 || firstPoint + points * size > curve.Count)
            {
                return false;
            }

            double[] begin = { curve[firstPoint].Real, curve[firstPoint + 1].Real, curve[firstPoint + 2].Real };
            int last = firstPoint + (int)((points - 1) * size);
            double[] finish = { curve[last].Real, curve[last + 1].Real, curve[last + 2].Real };

            if (!Near(begin, start) || !Near(finish, end))
            {
                return false;
            }

            from = curve[first].Real;
            to = curve[first + (int)(2 * (knots - 1))].Real;
            return from < to;
        }

        /// <summary>
        /// Turns the records of the form of R2004 into those of ACIS 4.00: the asmheader out, every pointer one lower, the
        /// history out of each record, the parameters and the convexity out of an edge, the fourth enum out of the name
        /// attribute, the word <c>full</c> out of the exact splines. False for a record ACIS 4.00 does not have (the tolerant
        /// ones, a history that is not -1) or a form that is not known.
        /// </summary>
        private static bool Lower(List<List<Token>> records)
        {
            if (records.Count == 0 || NameOf(records[0]) != "asmheader")
            {
                return false;
            }

            records.RemoveAt(0);

            foreach (List<Token> record in records)
            {
                string name = NameOf(record);
                int v = FirstValue(record);

                // The record that ends SAB (it is not written as SAT) keeps what it holds.
                if (name.StartsWith("End-of-", StringComparison.Ordinal))
                {
                    continue;
                }

                if (
                    name == "tcoedge-coedge"
                    || name == "tedge-edge"
                    || name == "tvertex-vertex"
                    || record.Count <= v + 1
                    || record[v + 1].Kind != 'i'
                    || record[v + 1].Whole != -1
                    || (
                        !HasNoHistoryPointer(name)
                        && (record.Count <= v + 2 || record[v + 2].Kind != 'r' || record[v + 2].Whole != -1)
                    )
                )
                {
                    return false;
                }

                for (int i = 0; i < record.Count; i++)
                {
                    if (record[i].Kind == 'r' && record[i].Whole > 0)
                    {
                        record[i] = new Token { Kind = 'r', Whole = record[i].Whole - 1 };
                    }
                }

                if (!HasNoHistoryPointer(name))
                {
                    record.RemoveAt(v + 2);
                }

                record.RemoveAt(v + 1);

                if (name == "edge")
                {
                    // r r d r d r r b s: each parameter follows its vertex, the convexity ends the record.
                    if (
                        record.Count != v + 9
                        || record[v + 2].Kind != 'd'
                        || record[v + 4].Kind != 'd'
                        || record[v + 8].Kind != 's'
                    )
                    {
                        return false;
                    }

                    record.RemoveAt(v + 8);
                    record.RemoveAt(v + 4);
                    record.RemoveAt(v + 2);
                }
                else if (name == "int64_attrib-name_attrib-gen-attrib")
                {
                    int enums = record.FindIndex(t => t.Kind == 'e');

                    if (
                        enums < 0
                        || enums + 4 >= record.Count
                        || record[enums + 3].Kind != 'e'
                        || record[enums + 4].Kind != 's'
                    )
                    {
                        return false;
                    }

                    record.RemoveAt(enums + 3);
                }

                for (int i = 0; i + 2 < record.Count; i++)
                {
                    // The subtypes of R2004 that ASM added to what ACIS 4.00 has (a swept or lofted spline surface) have no form there.
                    if (record[i].Kind == '{' && record[i + 1].Kind == 'N' && !IsOfAcis4(record[i + 1].Text))
                    {
                        return false;
                    }

                    if (record[i].Kind == '{' && record[i + 1].Kind == 'N' && HasFullWord(record[i + 1].Text))
                    {
                        if (record[i + 2].Kind != 'e' || record[i + 2].Whole != 0)
                        {
                            return false;
                        }

                        record.RemoveAt(i + 2);
                    }
                }
            }

            return true;
        }
    }
}
