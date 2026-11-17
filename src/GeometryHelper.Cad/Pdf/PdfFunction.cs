using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// A function of a PDF, the kind a tint transform of a spot colour is: sampled (type 0), exponential (2), stitching (3) or a
    /// small PostScript calculator (4). It takes some numbers and gives some numbers; one that cannot be read gives none.
    /// </summary>
    internal abstract class PdfFunction
    {
        private double[] _domain;
        private double[] _range;

        /// <summary>Gives the outputs for some inputs, clipped to the function's range.</summary>
        public double[] Evaluate(double[] input)
        {
            var x = new double[input.Length];

            for (int i = 0; i < x.Length; i++)
            {
                double v = input[i];

                if (_domain != null && 2 * i + 1 < _domain.Length)
                {
                    v = Math.Max(_domain[2 * i], Math.Min(_domain[2 * i + 1], v));
                }

                x[i] = v;
            }

            double[] y = Compute(x);

            if (_range != null)
            {
                for (int i = 0; i < y.Length && 2 * i + 1 < _range.Length; i++)
                {
                    y[i] = Math.Max(_range[2 * i], Math.Min(_range[2 * i + 1], y[i]));
                }
            }

            return y;
        }

        /// <summary>The outputs for inputs already clipped to the domain.</summary>
        protected abstract double[] Compute(double[] x);

        /// <summary>Reads a function object; null where it is none the reader knows.</summary>
        public static PdfFunction Parse(PdfFile file, object spec, int depth = 0)
        {
            if (depth > 8)
            {
                return null;
            }

            object resolved = file.Resolve(spec);
            PdfDict dictionary = file.Dict(resolved);

            if (dictionary == null)
            {
                return null;
            }

            int type = (int)file.Number(file.Get(dictionary, "FunctionType"), -1);
            PdfFunction function;

            switch (type)
            {
                case 0:
                    function = resolved is PdfStream stream ? Sampled.Make(file, stream) : null;
                    break;
                case 2:
                    function = Exponential.Make(file, dictionary);
                    break;
                case 3:
                    function = Stitching.Make(file, dictionary, depth);
                    break;
                case 4:
                    function = resolved is PdfStream program ? Calculator.Make(file, program) : null;
                    break;
                default:
                    return null;
            }

            if (function != null)
            {
                function._domain = file.Numbers(file.Get(dictionary, "Domain"));
                function._range = file.Numbers(file.Get(dictionary, "Range"));
            }

            return function;
        }

        private sealed class Exponential : PdfFunction
        {
            private double[] _c0;
            private double[] _c1;
            private double _n;

            public static Exponential Make(PdfFile file, PdfDict dictionary)
            {
                double[] c0 = file.Numbers(file.Get(dictionary, "C0")) ?? new[] { 0.0 };
                double[] c1 = file.Numbers(file.Get(dictionary, "C1")) ?? new[] { 1.0 };
                return new Exponential
                {
                    _c0 = c0,
                    _c1 = c1,
                    _n = file.Number(file.Get(dictionary, "N"), 1.0),
                };
            }

            protected override double[] Compute(double[] x)
            {
                double t = x.Length > 0 ? x[0] : 0.0;
                double p = _n == 1.0 ? t : Math.Pow(t, _n);
                int n = Math.Min(_c0.Length, _c1.Length);
                var y = new double[n];

                for (int i = 0; i < n; i++)
                {
                    y[i] = _c0[i] + p * (_c1[i] - _c0[i]);
                }

                return y;
            }
        }

        private sealed class Stitching : PdfFunction
        {
            private List<PdfFunction> _functions;
            private double[] _bounds;
            private double[] _encode;
            private double[] _domain0;

            public static Stitching Make(PdfFile file, PdfDict dictionary, int depth)
            {
                List<object> list = file.Array(file.Get(dictionary, "Functions"));

                if (list == null || list.Count == 0)
                {
                    return null;
                }

                var functions = new List<PdfFunction>();

                foreach (object item in list)
                {
                    PdfFunction f = Parse(file, item, depth + 1);

                    if (f == null)
                    {
                        return null;
                    }

                    functions.Add(f);
                }

                double[] domain = file.Numbers(file.Get(dictionary, "Domain")) ?? new[] { 0.0, 1.0 };
                return new Stitching
                {
                    _functions = functions,
                    _bounds = file.Numbers(file.Get(dictionary, "Bounds")) ?? new double[0],
                    _encode = file.Numbers(file.Get(dictionary, "Encode")) ?? new double[0],
                    _domain0 = domain,
                };
            }

            protected override double[] Compute(double[] x)
            {
                double t = x.Length > 0 ? x[0] : 0.0;
                int k = 0;

                while (k < _bounds.Length && k < _functions.Count - 1 && t >= _bounds[k])
                {
                    k++;
                }

                double low = k == 0 ? _domain0[0] : _bounds[k - 1];
                double high = k < _bounds.Length ? _bounds[k] : _domain0[1];
                double e0 = 2 * k + 1 < _encode.Length ? _encode[2 * k] : 0.0;
                double e1 = 2 * k + 1 < _encode.Length ? _encode[2 * k + 1] : 1.0;
                double u = high == low ? e0 : e0 + (t - low) * (e1 - e0) / (high - low);
                return _functions[k].Evaluate(new[] { u });
            }
        }

        private sealed class Sampled : PdfFunction
        {
            private int[] _size;
            private int _bits;
            private double[] _encode;
            private double[] _decode;
            private double[] _domain1;
            private int _outputs;
            private byte[] _samples;

            public static Sampled Make(PdfFile file, PdfStream stream)
            {
                PdfDict d = stream.Dictionary;
                double[] size = file.Numbers(file.Get(d, "Size"));
                double[] range = file.Numbers(file.Get(d, "Range"));
                double[] domain = file.Numbers(file.Get(d, "Domain"));

                if (size == null || range == null || domain == null || size.Length == 0 || size.Length > 4)
                {
                    return null;
                }

                var f = new Sampled
                {
                    _size = Array.ConvertAll(size, s => Math.Max(1, (int)s)),
                    _bits = (int)file.Number(file.Get(d, "BitsPerSample"), 8),
                    _outputs = range.Length / 2,
                    _domain1 = domain,
                    _samples = file.Decode(stream),
                };
                f._encode = file.Numbers(file.Get(d, "Encode"));

                if (f._encode == null)
                {
                    f._encode = new double[2 * size.Length];

                    for (int i = 0; i < size.Length; i++)
                    {
                        f._encode[2 * i + 1] = f._size[i] - 1;
                    }
                }

                f._decode = file.Numbers(file.Get(d, "Decode")) ?? range;
                return f._bits == 8 || f._bits == 16 || f._bits == 32 ? f : null;
            }

            private double Sample(int[] index, int output)
            {
                long at = 0;
                long stride = 1;

                for (int i = 0; i < _size.Length; i++)
                {
                    at += index[i] * stride;
                    stride *= _size[i];
                }

                long bit = (at * _outputs + output) * _bits;
                int bytePosition = (int)(bit / 8);
                int width = _bits / 8;

                if (bytePosition + width > _samples.Length)
                {
                    return 0.0;
                }

                double value = 0.0;

                for (int i = 0; i < width; i++)
                {
                    value = value * 256.0 + _samples[bytePosition + i];
                }

                return value / (Math.Pow(2.0, _bits) - 1.0);
            }

            protected override double[] Compute(double[] x)
            {
                int m = _size.Length;
                var low = new int[m];
                var frac = new double[m];

                for (int i = 0; i < m; i++)
                {
                    double d0 = _domain1[2 * i];
                    double d1 = _domain1[2 * i + 1];
                    double t = d1 == d0 ? 0.0 : (x[Math.Min(i, x.Length - 1)] - d0) / (d1 - d0);
                    double e = _encode[2 * i] + t * (_encode[2 * i + 1] - _encode[2 * i]);
                    e = Math.Max(0.0, Math.Min(_size[i] - 1, e));
                    low[i] = Math.Min((int)Math.Floor(e), Math.Max(0, _size[i] - 2));
                    frac[i] = _size[i] == 1 ? 0.0 : e - low[i];
                }

                var y = new double[_outputs];
                var index = new int[m];

                for (int o = 0; o < _outputs; o++)
                {
                    double sum = 0.0;

                    // Multilinear: every corner of the cell, weighted.
                    for (int corner = 0; corner < (1 << m); corner++)
                    {
                        double weight = 1.0;

                        for (int i = 0; i < m; i++)
                        {
                            bool up = (corner & (1 << i)) != 0;
                            weight *= up ? frac[i] : 1.0 - frac[i];
                            index[i] = Math.Min(low[i] + (up ? 1 : 0), _size[i] - 1);
                        }

                        if (weight > 0.0)
                        {
                            sum += weight * Sample(index, o);
                        }
                    }

                    y[o] = _decode[2 * o] + sum * (_decode[2 * o + 1] - _decode[2 * o]);
                }

                return y;
            }
        }

        private sealed class Calculator : PdfFunction
        {
            private List<object> _program;

            public static Calculator Make(PdfFile file, PdfStream stream)
            {
                byte[] bytes = file.Decode(stream);
                var parser = new PdfParser(bytes, 0, bytes.Length, false);

                if (!(parser.Read() is PdfKeyword open) || open.Value != "{")
                {
                    return null;
                }

                return new Calculator { _program = Block(parser) };
            }

            private static List<object> Block(PdfParser parser)
            {
                var block = new List<object>();

                while (true)
                {
                    object item = parser.Read();

                    if (item == null && parser.AtEnd)
                    {
                        break;
                    }

                    if (item is PdfKeyword keyword)
                    {
                        if (keyword.Value == "}")
                        {
                            break;
                        }

                        if (keyword.Value == "{")
                        {
                            block.Add(Block(parser));
                            continue;
                        }
                    }

                    block.Add(item);
                }

                return block;
            }

            protected override double[] Compute(double[] x)
            {
                var stack = new List<double>(x);
                Run(_program, stack, 0);
                return stack.ToArray();
            }

            private static double Pop(List<double> stack)
            {
                if (stack.Count == 0)
                {
                    return 0.0;
                }

                double v = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                return v;
            }

            private static void Run(List<object> block, List<double> stack, int depth)
            {
                if (depth > 16)
                {
                    return;
                }

                for (int i = 0; i < block.Count; i++)
                {
                    object item = block[i];

                    if (item is double number)
                    {
                        stack.Add(number);
                        continue;
                    }

                    if (item is bool flag)
                    {
                        stack.Add(flag ? 1.0 : 0.0);
                        continue;
                    }

                    if (item is List<object> branch)
                    {
                        // A block is run by the "if" or "ifelse" that follows it.
                        if (i + 1 < block.Count && block[i + 1] is PdfKeyword ifKeyword && ifKeyword.Value == "if")
                        {
                            if (Pop(stack) != 0.0)
                            {
                                Run(branch, stack, depth + 1);
                            }

                            i++;
                        }
                        else if (
                            i + 2 < block.Count
                            && block[i + 1] is List<object> other
                            && block[i + 2] is PdfKeyword elseKeyword
                            && elseKeyword.Value == "ifelse"
                        )
                        {
                            Run(Pop(stack) != 0.0 ? branch : other, stack, depth + 1);
                            i += 2;
                        }

                        continue;
                    }

                    if (!(item is PdfKeyword op))
                    {
                        continue;
                    }

                    double a;
                    double b;

                    switch (op.Value)
                    {
                        case "add":
                            b = Pop(stack);
                            a = Pop(stack);
                            stack.Add(a + b);
                            break;
                        case "sub":
                            b = Pop(stack);
                            a = Pop(stack);
                            stack.Add(a - b);
                            break;
                        case "mul":
                            b = Pop(stack);
                            a = Pop(stack);
                            stack.Add(a * b);
                            break;
                        case "div":
                            b = Pop(stack);
                            a = Pop(stack);
                            stack.Add(b == 0.0 ? 0.0 : a / b);
                            break;
                        case "idiv":
                            b = Pop(stack);
                            a = Pop(stack);
                            stack.Add(b == 0.0 ? 0.0 : Math.Truncate(a / b));
                            break;
                        case "mod":
                            b = Pop(stack);
                            a = Pop(stack);
                            stack.Add(b == 0.0 ? 0.0 : a % b);
                            break;
                        case "neg":
                            stack.Add(-Pop(stack));
                            break;
                        case "abs":
                            stack.Add(Math.Abs(Pop(stack)));
                            break;
                        case "ceiling":
                            stack.Add(Math.Ceiling(Pop(stack)));
                            break;
                        case "floor":
                            stack.Add(Math.Floor(Pop(stack)));
                            break;
                        case "round":
                            stack.Add(Math.Floor(Pop(stack) + 0.5));
                            break;
                        case "truncate":
                        case "cvi":
                            stack.Add(Math.Truncate(Pop(stack)));
                            break;
                        case "cvr":
                            break;
                        case "sqrt":
                            stack.Add(Math.Sqrt(Math.Max(0.0, Pop(stack))));
                            break;
                        case "sin":
                            stack.Add(Math.Sin(Pop(stack) * Math.PI / 180.0));
                            break;
                        case "cos":
                            stack.Add(Math.Cos(Pop(stack) * Math.PI / 180.0));
                            break;
                        case "atan":
                            b = Pop(stack);
                            a = Pop(stack);
                            double angle = Math.Atan2(a, b) * 180.0 / Math.PI;
                            stack.Add(angle < 0.0 ? angle + 360.0 : angle);
                            break;
                        case "exp":
                            b = Pop(stack);
                            a = Pop(stack);
                            stack.Add(Math.Pow(a, b));
                            break;
                        case "ln":
                            stack.Add(Math.Log(Math.Max(1E-300, Pop(stack))));
                            break;
                        case "log":
                            stack.Add(Math.Log10(Math.Max(1E-300, Pop(stack))));
                            break;
                        case "eq":
                            b = Pop(stack);
                            a = Pop(stack);
                            stack.Add(a == b ? 1.0 : 0.0);
                            break;
                        case "ne":
                            b = Pop(stack);
                            a = Pop(stack);
                            stack.Add(a != b ? 1.0 : 0.0);
                            break;
                        case "gt":
                            b = Pop(stack);
                            a = Pop(stack);
                            stack.Add(a > b ? 1.0 : 0.0);
                            break;
                        case "ge":
                            b = Pop(stack);
                            a = Pop(stack);
                            stack.Add(a >= b ? 1.0 : 0.0);
                            break;
                        case "lt":
                            b = Pop(stack);
                            a = Pop(stack);
                            stack.Add(a < b ? 1.0 : 0.0);
                            break;
                        case "le":
                            b = Pop(stack);
                            a = Pop(stack);
                            stack.Add(a <= b ? 1.0 : 0.0);
                            break;
                        case "and":
                            b = Pop(stack);
                            a = Pop(stack);
                            stack.Add((long)a & (long)b);
                            break;
                        case "or":
                            b = Pop(stack);
                            a = Pop(stack);
                            stack.Add((long)a | (long)b);
                            break;
                        case "xor":
                            b = Pop(stack);
                            a = Pop(stack);
                            stack.Add((long)a ^ (long)b);
                            break;
                        case "not":
                            a = Pop(stack);
                            stack.Add(
                                a == 0.0 ? 1.0
                                : a == 1.0 ? 0.0
                                : ~(long)a
                            );
                            break;
                        case "bitshift":
                            b = Pop(stack);
                            a = Pop(stack);
                            stack.Add(b >= 0 ? (long)a << (int)b : (long)a >> (int)-b);
                            break;
                        case "true":
                            stack.Add(1.0);
                            break;
                        case "false":
                            stack.Add(0.0);
                            break;
                        case "pop":
                            Pop(stack);
                            break;
                        case "dup":
                            a = Pop(stack);
                            stack.Add(a);
                            stack.Add(a);
                            break;
                        case "exch":
                            b = Pop(stack);
                            a = Pop(stack);
                            stack.Add(b);
                            stack.Add(a);
                            break;
                        case "copy":
                            int n = (int)Pop(stack);

                            if (n > 0 && n <= stack.Count)
                            {
                                stack.AddRange(stack.GetRange(stack.Count - n, n));
                            }

                            break;
                        case "index":
                            int k = (int)Pop(stack);
                            stack.Add(k >= 0 && k < stack.Count ? stack[stack.Count - 1 - k] : 0.0);
                            break;
                        case "roll":
                            int j = (int)Pop(stack);
                            int count = (int)Pop(stack);

                            if (count > 1 && count <= stack.Count)
                            {
                                List<double> top = stack.GetRange(stack.Count - count, count);
                                stack.RemoveRange(stack.Count - count, count);
                                j = ((j % count) + count) % count;

                                for (int q = 0; q < count; q++)
                                {
                                    stack.Add(top[(q - j + count) % count]);
                                }
                            }

                            break;
                    }
                }
            }
        }
    }
}
