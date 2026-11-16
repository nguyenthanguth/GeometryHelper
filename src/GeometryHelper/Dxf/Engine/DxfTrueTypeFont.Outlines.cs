using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The outlines of the letters of a TrueType font, read from its <c>glyf</c> table for the commands that turn words into
    /// lines (TXTEXP): each letter as closed contours of straight pieces, in the font's units, the curves of the outline
    /// (quadratic Béziers, whose off-curve points the font leaves between on-curve ones) cut into six pieces each.
    /// </summary>
    internal sealed partial class DxfTrueTypeFont
    {
        /// <summary>How many straight pieces a curve of the outline is cut into.</summary>
        private const int CurvePieces = 6;

        /// <summary>The deepest a letter made of other letters is followed, which keeps a font that makes a letter of itself from running without end.</summary>
        private const int DeepestComposite = 8;

        /// <summary>
        /// The outline of the letter of a Unicode number (its byte at U+F000 on in a font of symbols), each contour a closed
        /// run of points as x, y, x, y in the font's units, y up, the last point not repeating the first. The outer contours of a
        /// letter run clockwise and its holes counter-clockwise, as the font draws them. Empty for a letter of no outline, a space;
        /// null where the font maps the number to none.
        /// </summary>
        /// <param name="number">The Unicode number.</param>
        /// <returns>The contours.</returns>
        internal IReadOnlyList<double[]> OutlineOf(int number)
        {
            if (number <= 0)
            {
                return null;
            }

            int index = IndexOf(number);

            if (index <= 0)
            {
                return null;
            }

            var contours = new List<double[]>();
            AddOutline(index, contours, new Placing(1.0, 0.0, 0.0, 1.0, 0.0, 0.0), 0);
            return contours;
        }

        /// <summary>The bytes of a letter's record in <c>glyf</c>: where it starts and ends; false for a letter of no outline or one past the table.</summary>
        private bool Record(int index, out int at, out int end)
        {
            long from = _longLoca ? U32(_bytes, _loca + (4 * index)) : 2L * U16(_bytes, _loca + (2 * index));
            long to = _longLoca ? U32(_bytes, _loca + (4 * index) + 4) : 2L * U16(_bytes, _loca + (2 * index) + 2);
            at = 0;
            end = 0;

            if (!(to > from + 10) || _glyf + to > _glyfEnd)
            {
                return false;
            }

            at = _glyf + (int)from;
            end = _glyf + (int)to;
            return true;
        }

        private void AddOutline(int index, List<double[]> into, Placing placing, int depth)
        {
            if (index < 0 || index >= _letters || depth > DeepestComposite || !Record(index, out int at, out int end))
            {
                return;
            }

            int contours = S16(_bytes, at);

            if (contours >= 0)
            {
                AddSimple(at, end, contours, into, placing);
            }
            else
            {
                AddComposite(at + 10, end, into, placing, depth);
            }
        }

        /// <summary>The contours of a letter of its own outline: their end points, flags and coordinates as the table lays them.</summary>
        private void AddSimple(int at, int end, int contours, List<double[]> into, Placing placing)
        {
            if (contours == 0)
            {
                return;
            }

            int ends = at + 10;
            int count = U16(_bytes, ends + (2 * (contours - 1))) + 1;
            int p = ends + (2 * contours);
            p += 2 + U16(_bytes, p);

            if (count <= 0 || p > end)
            {
                return;
            }

            var flags = new byte[count];

            for (int i = 0; i < count; )
            {
                if (p >= end)
                {
                    return;
                }

                byte flag = _bytes[p++];
                flags[i++] = flag;

                if ((flag & 8) != 0)
                {
                    if (p >= end)
                    {
                        return;
                    }

                    int repeat = _bytes[p++];

                    while (repeat-- > 0 && i < count)
                    {
                        flags[i++] = flag;
                    }
                }
            }

            var xs = new double[count];
            var ys = new double[count];
            double value = 0.0;

            for (int i = 0; i < count; i++)
            {
                if (
                    p
                        + (
                            (flags[i] & 2) != 0 ? 1
                            : (flags[i] & 16) != 0 ? 0
                            : 2
                        )
                    > end
                )
                {
                    return;
                }

                if ((flags[i] & 2) != 0)
                {
                    value += (flags[i] & 16) != 0 ? _bytes[p] : -_bytes[p];
                    p++;
                }
                else if ((flags[i] & 16) == 0)
                {
                    value += S16(_bytes, p);
                    p += 2;
                }

                xs[i] = value;
            }

            value = 0.0;

            for (int i = 0; i < count; i++)
            {
                if (
                    p
                        + (
                            (flags[i] & 4) != 0 ? 1
                            : (flags[i] & 32) != 0 ? 0
                            : 2
                        )
                    > end
                )
                {
                    return;
                }

                if ((flags[i] & 4) != 0)
                {
                    value += (flags[i] & 32) != 0 ? _bytes[p] : -_bytes[p];
                    p++;
                }
                else if ((flags[i] & 32) == 0)
                {
                    value += S16(_bytes, p);
                    p += 2;
                }

                ys[i] = value;
            }

            int first = 0;

            for (int c = 0; c < contours; c++)
            {
                int last = U16(_bytes, ends + (2 * c));

                if (last >= first && last < count)
                {
                    double[] contour = Flatten(xs, ys, flags, first, last, placing);

                    if (contour.Length >= 6)
                    {
                        into.Add(contour);
                    }
                }

                first = last + 1;
            }
        }

        /// <summary>The byte of the file at a place, nought past its end, so that a font cut short does not throw.</summary>
        private int ByteAt(int at) => at >= 0 && at < _bytes.Length ? _bytes[at] : 0;

        /// <summary>The parts of a letter made of others: each a letter placed by a scale and a move.</summary>
        private void AddComposite(int p, int end, List<double[]> into, Placing placing, int depth)
        {
            while (p + 4 <= end)
            {
                int flags = U16(_bytes, p);
                int part = U16(_bytes, p + 2);
                p += 4;
                double dx;
                double dy;

                if ((flags & 1) != 0)
                {
                    dx = (flags & 2) != 0 ? S16(_bytes, p) : U16(_bytes, p);
                    dy = (flags & 2) != 0 ? S16(_bytes, p + 2) : U16(_bytes, p + 2);
                    p += 4;
                }
                else
                {
                    dx = (flags & 2) != 0 ? (sbyte)ByteAt(p) : ByteAt(p);
                    dy = (flags & 2) != 0 ? (sbyte)ByteAt(p + 1) : ByteAt(p + 1);
                    p += 2;
                }

                // A part placed by matching points, not by a move, is placed where it stands.
                if ((flags & 2) == 0)
                {
                    dx = 0.0;
                    dy = 0.0;
                }

                double a = 1.0,
                    b = 0.0,
                    c = 0.0,
                    d = 1.0;

                if ((flags & 8) != 0)
                {
                    a = d = S16(_bytes, p) / 16384.0;
                    p += 2;
                }
                else if ((flags & 0x40) != 0)
                {
                    a = S16(_bytes, p) / 16384.0;
                    d = S16(_bytes, p + 2) / 16384.0;
                    p += 4;
                }
                else if ((flags & 0x80) != 0)
                {
                    a = S16(_bytes, p) / 16384.0;
                    b = S16(_bytes, p + 2) / 16384.0;
                    c = S16(_bytes, p + 4) / 16384.0;
                    d = S16(_bytes, p + 6) / 16384.0;
                    p += 8;
                }

                AddOutline(part, into, placing.Then(new Placing(a, b, c, d, dx, dy)), depth + 1);

                if ((flags & 0x20) == 0)
                {
                    break;
                }
            }
        }

        /// <summary>One contour as straight pieces: an off-curve point is the control point of a quadratic curve, two in a row having an on-curve point halfway between them.</summary>
        private static double[] Flatten(double[] xs, double[] ys, byte[] flags, int first, int last, Placing placing)
        {
            // The points of the contour, an on-curve point between two off-curve ones made where the font leaves it out.
            var px = new List<double>();
            var py = new List<double>();
            var on = new List<bool>();
            int n = last - first + 1;

            for (int i = 0; i < n; i++)
            {
                int now = first + i;
                int next = first + ((i + 1) % n);
                bool isOn = (flags[now] & 1) != 0;
                px.Add(xs[now]);
                py.Add(ys[now]);
                on.Add(isOn);

                if (!isOn && (flags[next] & 1) == 0)
                {
                    px.Add((xs[now] + xs[next]) / 2.0);
                    py.Add((ys[now] + ys[next]) / 2.0);
                    on.Add(true);
                }
            }

            // Start from an on-curve point: the first one found, the contour turned round to it.
            int start = on.IndexOf(true);

            if (start < 0)
            {
                return new double[0];
            }

            int m = px.Count;
            var result = new List<double>();

            void Put(double x, double y)
            {
                placing.Apply(x, y, out double tx, out double ty);
                result.Add(tx);
                result.Add(ty);
            }

            Put(px[start], py[start]);

            for (int k = 1; k <= m; k++)
            {
                int at = (start + k) % m;

                if (on[at])
                {
                    if (k < m)
                    {
                        Put(px[at], py[at]);
                    }

                    continue;
                }

                int to = (start + k + 1) % m;
                int from = (start + k - 1) % m;

                for (int piece = 1; piece < CurvePieces; piece++)
                {
                    double t = piece / (double)CurvePieces;
                    double u = 1.0 - t;
                    Put(
                        (u * u * px[from]) + (2.0 * u * t * px[at]) + (t * t * px[to]),
                        (u * u * py[from]) + (2.0 * u * t * py[at]) + (t * t * py[to])
                    );
                }
            }

            return result.ToArray();
        }

        /// <summary>A scale, turn and move of a part of a letter: x' = a x + c y + e, y' = b x + d y + f.</summary>
        private readonly struct Placing
        {
            private readonly double _a,
                _b,
                _c,
                _d,
                _e,
                _f;

            internal Placing(double a, double b, double c, double d, double e, double f)
            {
                _a = a;
                _b = b;
                _c = c;
                _d = d;
                _e = e;
                _f = f;
            }

            /// <summary>This placing after another: a point is put by the other first.</summary>
            internal Placing Then(Placing inner) =>
                new Placing(
                    (_a * inner._a) + (_c * inner._b),
                    (_b * inner._a) + (_d * inner._b),
                    (_a * inner._c) + (_c * inner._d),
                    (_b * inner._c) + (_d * inner._d),
                    (_a * inner._e) + (_c * inner._f) + _e,
                    (_b * inner._e) + (_d * inner._f) + _f
                );

            internal void Apply(double x, double y, out double tx, out double ty)
            {
                tx = (_a * x) + (_c * y) + _e;
                ty = (_b * x) + (_d * y) + _f;
            }
        }
    }
}
