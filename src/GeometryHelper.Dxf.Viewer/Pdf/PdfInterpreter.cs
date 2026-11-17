using System;
using System.Collections.Generic;
using System.Text;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Runs the content stream of a PDF page and gathers what it draws as a <see cref="PdfScene"/>: paths filled and stroked
    /// (lines, curves cut into short pieces, rectangles), colours in every space the reader knows, the clip paths, forms placed
    /// by <c>Do</c>, pictures (inline or placed), optional content (layers) and text, as outlines of an embedded TrueType font or
    /// as words in a font of the machine. Shadings, tiling patterns, Type 3 fonts and fonts of CFF or Type 1 outlines are not
    /// drawn as such; each is noted in <see cref="PdfScene.Dropped"/>.
    /// </summary>
    internal sealed class PdfInterpreter
    {
        private const int MostItems = 3_000_000;
        private const int DeepestForm = 12;
        private const double CapHeight = 0.716;

        private readonly PdfDocument _document;
        private readonly PdfFile _file;
        private readonly PdfPage _page;
        private readonly PdfScene _scene = new PdfScene();
        private readonly Dictionary<object, PdfFont> _fonts = new Dictionary<object, PdfFont>();
        private readonly Dictionary<int, byte[]> _pictures = new Dictionary<int, byte[]>();
        private readonly Stack<PdfOc> _marked = new Stack<PdfOc>();
        private readonly Stack<State> _saved = new Stack<State>();
        private State _state;
        private PdfOc _oc;
        private double[] _tm = Identity();
        private double[] _tlm = Identity();
        private int _inline;

        // The path being built, in page points.
        private readonly List<double[]> _lines = new List<double[]>();
        private readonly List<bool> _closed = new List<bool>();
        private List<double> _current = new List<double>();
        private double _startX;
        private double _startY;
        private int _pendingClip; // 0 none, 1 nonzero, 2 even-odd

        private sealed class State
        {
            public double[] Ctm;
            public PdfColorSpace FillSpace = PdfColorSpace.Gray;
            public PdfColorSpace StrokeSpace = PdfColorSpace.Gray;
            public int FillRgb;
            public int StrokeRgb;
            public bool FillPattern;
            public bool StrokePattern;
            public double LineWidth = 1.0;
            public double[] Dash;
            public double Phase;
            public PdfClip Clip;
            public double FillAlpha = 1.0;
            public double StrokeAlpha = 1.0;
            public PdfFont Font;
            public double FontSize = 1.0;
            public double CharSpace;
            public double WordSpace;
            public double Scale = 1.0;
            public double Leading;
            public double Rise;
            public int Render;

            public State Copy() => (State)MemberwiseClone();
        }

        /// <summary>Makes an interpreter of one page of a document.</summary>
        public PdfInterpreter(PdfDocument document, PdfPage page)
        {
            _document = document;
            _file = document.File;
            _page = page;
        }

        /// <summary>Runs the page's content.</summary>
        /// <returns>The scene; an empty one for a page whose content cannot be read.</returns>
        public PdfScene Run()
        {
            _scene.Width = _page.Width;
            _scene.Height = _page.Height;
            _state = new State { Ctm = PageMatrix() };

            try
            {
                object contents = _file.Resolve(_page.Dictionary.TryGetValue("Contents", out object c) ? c : null);
                var bytes = new List<byte>();

                if (contents is PdfStream single)
                {
                    bytes.AddRange(_file.Decode(single));
                }
                else if (contents is List<object> list)
                {
                    foreach (object part in list)
                    {
                        PdfStream stream = _file.Stream(part);

                        if (stream != null)
                        {
                            bytes.AddRange(_file.Decode(stream));
                            bytes.Add((byte)'\n');
                        }
                    }
                }

                Execute(bytes.ToArray(), _page.Resources, 0);
            }
            catch (PdfException)
            {
                _scene.Dropped.Add("content that could not be read");
            }

            return _scene;
        }

        private double[] PageMatrix()
        {
            double w = _page.X1 - _page.X0;
            double h = _page.Y1 - _page.Y0;
            double[] move = { 1, 0, 0, 1, -_page.X0, -_page.Y0 };

            switch (_page.Rotate)
            {
                case 90:
                    return Mul(move, new double[] { 0, -1, 1, 0, 0, w });
                case 180:
                    return Mul(move, new double[] { -1, 0, 0, -1, w, h });
                case 270:
                    return Mul(move, new double[] { 0, 1, -1, 0, h, 0 });
                default:
                    return move;
            }
        }

        #region Matrices

        private static double[] Identity() => new double[] { 1, 0, 0, 1, 0, 0 };

        /// <summary>The matrix that applies <paramref name="m"/> first and <paramref name="n"/> after it.</summary>
        private static double[] Mul(double[] m, double[] n) =>
            new[]
            {
                m[0] * n[0] + m[1] * n[2],
                m[0] * n[1] + m[1] * n[3],
                m[2] * n[0] + m[3] * n[2],
                m[2] * n[1] + m[3] * n[3],
                m[4] * n[0] + m[5] * n[2] + n[4],
                m[4] * n[1] + m[5] * n[3] + n[5],
            };

        private static double X(double[] m, double x, double y) => m[0] * x + m[2] * y + m[4];

        private static double Y(double[] m, double x, double y) => m[1] * x + m[3] * y + m[5];

        #endregion

        #region The loop

        private void Execute(byte[] bytes, PdfDict resources, int depth)
        {
            var parser = new PdfParser(bytes, 0, bytes.Length, false);
            var operands = new List<object>();
            int savedDepth = _saved.Count;
            int markedDepth = _marked.Count;
            PdfOc oc = _oc;

            while (_scene.Items.Count < MostItems)
            {
                object item;

                try
                {
                    item = parser.Read();
                }
                catch (PdfException)
                {
                    break;
                }

                if (item == null && parser.AtEnd)
                {
                    break;
                }

                if (item is PdfKeyword keyword)
                {
                    if (keyword.Value == "BI")
                    {
                        InlineImage(parser, bytes, resources);
                    }
                    else
                    {
                        try
                        {
                            Operator(keyword.Value, operands, resources, depth);
                        }
                        catch (PdfException)
                        {
                            _scene.Dropped.Add("operators that could not be read");
                        }
                        catch (IndexOutOfRangeException)
                        {
                            _scene.Dropped.Add("operators that could not be read");
                        }
                        catch (InvalidCastException)
                        {
                            _scene.Dropped.Add("operators that could not be read");
                        }
                    }

                    operands.Clear();
                    continue;
                }

                if (operands.Count < 512)
                {
                    operands.Add(item);
                }
            }

            // A form or a page that leaves its saves and its marked content open does not leak them to what follows.
            while (_saved.Count > savedDepth)
            {
                _state = _saved.Pop();
            }

            while (_marked.Count > markedDepth)
            {
                _marked.Pop();
            }

            _oc = oc;
        }

        private static double N(List<object> operands, int index) =>
            index >= 0 && index < operands.Count && operands[index] is double d ? d : 0.0;

        private void Operator(string op, List<object> a, PdfDict resources, int depth)
        {
            switch (op)
            {
                // The graphics state.
                case "q":
                    if (_saved.Count < 256)
                    {
                        _saved.Push(_state.Copy());
                    }

                    break;
                case "Q":
                    if (_saved.Count > 0)
                    {
                        _state = _saved.Pop();
                    }

                    break;
                case "cm":
                    if (a.Count >= 6)
                    {
                        _state.Ctm = Mul(new[] { N(a, 0), N(a, 1), N(a, 2), N(a, 3), N(a, 4), N(a, 5) }, _state.Ctm);
                    }

                    break;
                case "w":
                    _state.LineWidth = Math.Abs(N(a, 0));
                    break;
                case "d":
                    SetDash(a.Count > 0 ? a[0] as List<object> : null, N(a, 1));
                    break;
                case "gs":
                    if (a.Count > 0 && a[0] is PdfName extName)
                    {
                        ExtGState(
                            _file.Dict(_file.Get(_file.Dict(_file.Get(resources, "ExtGState")), extName.Value)),
                            resources
                        );
                    }

                    break;

                // Paths.
                case "m":
                    MoveTo(N(a, 0), N(a, 1));
                    break;
                case "l":
                    LineTo(N(a, 0), N(a, 1));
                    break;
                case "c":
                    CurveTo(N(a, 0), N(a, 1), N(a, 2), N(a, 3), N(a, 4), N(a, 5));
                    break;
                case "v":
                {
                    double cx = _current.Count >= 2 ? _current[_current.Count - 2] : 0.0;
                    double cy = _current.Count >= 2 ? _current[_current.Count - 1] : 0.0;
                    double[] inverse = Invert(_state.Ctm);
                    CurveTo(X(inverse, cx, cy), Y(inverse, cx, cy), N(a, 0), N(a, 1), N(a, 2), N(a, 3));
                    break;
                }
                case "y":
                    CurveTo(N(a, 0), N(a, 1), N(a, 2), N(a, 3), N(a, 2), N(a, 3));
                    break;
                case "h":
                    ClosePath();
                    break;
                case "re":
                    Rectangle(N(a, 0), N(a, 1), N(a, 2), N(a, 3));
                    break;
                case "S":
                    Paint(false, false, true);
                    break;
                case "s":
                    ClosePath();
                    Paint(false, false, true);
                    break;
                case "f":
                case "F":
                    Paint(true, false, false);
                    break;
                case "f*":
                    Paint(true, true, false);
                    break;
                case "B":
                    Paint(true, false, true);
                    break;
                case "B*":
                    Paint(true, true, true);
                    break;
                case "b":
                    ClosePath();
                    Paint(true, false, true);
                    break;
                case "b*":
                    ClosePath();
                    Paint(true, true, true);
                    break;
                case "n":
                    Paint(false, false, false);
                    break;
                case "W":
                    _pendingClip = 1;
                    break;
                case "W*":
                    _pendingClip = 2;
                    break;

                // Colour.
                case "g":
                    SetFill(PdfColorSpace.Gray, new[] { N(a, 0) });
                    break;
                case "G":
                    SetStroke(PdfColorSpace.Gray, new[] { N(a, 0) });
                    break;
                case "rg":
                    SetFill(PdfColorSpace.Rgb, new[] { N(a, 0), N(a, 1), N(a, 2) });
                    break;
                case "RG":
                    SetStroke(PdfColorSpace.Rgb, new[] { N(a, 0), N(a, 1), N(a, 2) });
                    break;
                case "k":
                    SetFill(PdfColorSpace.Cmyk, new[] { N(a, 0), N(a, 1), N(a, 2), N(a, 3) });
                    break;
                case "K":
                    SetStroke(PdfColorSpace.Cmyk, new[] { N(a, 0), N(a, 1), N(a, 2), N(a, 3) });
                    break;
                case "cs":
                    if (a.Count > 0)
                    {
                        PdfColorSpace fill = PdfColorSpace.Parse(_file, a[0], resources);
                        SetFill(fill, fill.Initial);
                    }

                    break;
                case "CS":
                    if (a.Count > 0)
                    {
                        PdfColorSpace stroke = PdfColorSpace.Parse(_file, a[0], resources);
                        SetStroke(stroke, stroke.Initial);
                    }

                    break;
                case "sc":
                case "scn":
                    SetColor(a, true);
                    break;
                case "SC":
                case "SCN":
                    SetColor(a, false);
                    break;
                case "sh":
                    _scene.Dropped.Add("shadings");
                    break;

                // Text.
                case "BT":
                    _tm = Identity();
                    _tlm = Identity();
                    break;
                case "ET":
                    break;
                case "Tc":
                    _state.CharSpace = N(a, 0);
                    break;
                case "Tw":
                    _state.WordSpace = N(a, 0);
                    break;
                case "Tz":
                    _state.Scale = N(a, 0) / 100.0;
                    break;
                case "TL":
                    _state.Leading = N(a, 0);
                    break;
                case "Ts":
                    _state.Rise = N(a, 0);
                    break;
                case "Tr":
                    _state.Render = (int)N(a, 0) % 4;
                    break;
                case "Tf":
                    if (a.Count >= 2 && a[0] is PdfName fontName)
                    {
                        _state.Font = FontOf(resources, fontName.Value);
                        _state.FontSize = N(a, 1);
                    }

                    break;
                case "Td":
                    NextLine(N(a, 0), N(a, 1));
                    break;
                case "TD":
                    _state.Leading = -N(a, 1);
                    NextLine(N(a, 0), N(a, 1));
                    break;
                case "Tm":
                    if (a.Count >= 6)
                    {
                        _tm = new[] { N(a, 0), N(a, 1), N(a, 2), N(a, 3), N(a, 4), N(a, 5) };
                        _tlm = (double[])_tm.Clone();
                    }

                    break;
                case "T*":
                    NextLine(0.0, -_state.Leading);
                    break;
                case "Tj":
                    if (a.Count > 0 && a[0] is PdfString shown)
                    {
                        Show(new List<object> { shown });
                    }

                    break;
                case "TJ":
                    if (a.Count > 0 && a[0] is List<object> pieces)
                    {
                        Show(pieces);
                    }

                    break;
                case "'":
                    NextLine(0.0, -_state.Leading);

                    if (a.Count > 0 && a[0] is PdfString quoted)
                    {
                        Show(new List<object> { quoted });
                    }

                    break;
                case "\"":
                    _state.WordSpace = N(a, 0);
                    _state.CharSpace = N(a, 1);
                    NextLine(0.0, -_state.Leading);

                    if (a.Count > 2 && a[2] is PdfString doubleQuoted)
                    {
                        Show(new List<object> { doubleQuoted });
                    }

                    break;

                // Marked content (optional content layers).
                case "BDC":
                {
                    PdfOc oc = null;

                    if (a.Count >= 2 && a[0] is PdfName tag && tag.Value == "OC")
                    {
                        object property = a[1];

                        if (property is PdfName propertyName)
                        {
                            PdfDict properties = _file.Dict(_file.Get(resources, "Properties"));
                            property =
                                properties != null && properties.TryGetValue(propertyName.Value, out object found)
                                    ? found
                                    : null;
                        }

                        oc = OcOf(property);
                    }

                    _marked.Push(_oc);
                    _oc = oc ?? _oc;
                    break;
                }
                case "BMC":
                    _marked.Push(_oc);
                    break;
                case "EMC":
                    if (_marked.Count > 0)
                    {
                        _oc = _marked.Pop();
                    }

                    break;

                // Things placed.
                case "Do":
                    if (a.Count > 0 && a[0] is PdfName xobject)
                    {
                        DoXObject(xobject.Value, resources, depth);
                    }

                    break;
            }
        }

        #endregion

        #region State

        private void SetDash(List<object> array, double phase)
        {
            double scale = Scale(_state.Ctm);

            if (array == null || array.Count == 0)
            {
                _state.Dash = null;
                _state.Phase = 0.0;
                return;
            }

            var dash = new double[array.Count];
            double sum = 0.0;

            for (int i = 0; i < dash.Length; i++)
            {
                dash[i] = Math.Abs(_file.Number(array[i])) * scale;
                sum += dash[i];
            }

            _state.Dash = sum > 0.0 ? dash : null;
            _state.Phase = phase * scale;
        }

        private void ExtGState(PdfDict dictionary, PdfDict resources)
        {
            if (dictionary == null)
            {
                return;
            }

            if (dictionary.TryGetValue("LW", out object lw))
            {
                _state.LineWidth = Math.Abs(_file.Number(lw, _state.LineWidth));
            }

            if (dictionary.TryGetValue("D", out object d) && _file.Array(d) is List<object> pair && pair.Count >= 2)
            {
                SetDash(_file.Array(pair[0]), _file.Number(pair[1]));
            }

            if (dictionary.TryGetValue("ca", out object ca))
            {
                _state.FillAlpha = _file.Number(ca, 1.0);
            }

            if (dictionary.TryGetValue("CA", out object stroke))
            {
                _state.StrokeAlpha = _file.Number(stroke, 1.0);
            }

            if (dictionary.TryGetValue("Font", out object f) && _file.Array(f) is List<object> font && font.Count >= 2)
            {
                _state.Font = FontOfObject(font[0]);
                _state.FontSize = _file.Number(font[1], _state.FontSize);
            }
        }

        private void SetFill(PdfColorSpace space, double[] colour)
        {
            _state.FillSpace = space;
            _state.FillPattern = space.IsPattern;
            _state.FillRgb = space.IsPattern ? 0x808080 : space.ToRgb(colour);
        }

        private void SetStroke(PdfColorSpace space, double[] colour)
        {
            _state.StrokeSpace = space;
            _state.StrokePattern = space.IsPattern;
            _state.StrokeRgb = space.IsPattern ? 0x808080 : space.ToRgb(colour);
        }

        private void SetColor(List<object> a, bool fill)
        {
            PdfColorSpace space = fill ? _state.FillSpace : _state.StrokeSpace;

            if (space.IsPattern)
            {
                _scene.Dropped.Add("pattern fills");
                return;
            }

            var colour = new double[Math.Max(space.Components, 1)];

            for (int i = 0; i < colour.Length && i < a.Count; i++)
            {
                colour[i] = N(a, i);
            }

            if (fill)
            {
                SetFill(space, colour);
            }
            else
            {
                SetStroke(space, colour);
            }
        }

        private static double Scale(double[] m) => Math.Sqrt(Math.Abs(m[0] * m[3] - m[1] * m[2]));

        private static double[] Invert(double[] m)
        {
            double det = m[0] * m[3] - m[1] * m[2];

            if (Math.Abs(det) < 1E-12)
            {
                return Identity();
            }

            return new[]
            {
                m[3] / det,
                -m[1] / det,
                -m[2] / det,
                m[0] / det,
                (m[2] * m[5] - m[3] * m[4]) / det,
                (m[1] * m[4] - m[0] * m[5]) / det,
            };
        }

        #endregion

        #region Paths

        private void MoveTo(double x, double y)
        {
            EndSubpath(false);
            double[] m = _state.Ctm;
            _startX = X(m, x, y);
            _startY = Y(m, x, y);
            _current.Add(_startX);
            _current.Add(_startY);
        }

        private void LineTo(double x, double y)
        {
            double[] m = _state.Ctm;

            if (_current.Count == 0)
            {
                MoveTo(x, y);
                return;
            }

            _current.Add(X(m, x, y));
            _current.Add(Y(m, x, y));
        }

        private void CurveTo(double x1, double y1, double x2, double y2, double x3, double y3)
        {
            double[] m = _state.Ctm;

            if (_current.Count == 0)
            {
                MoveTo(x1, y1);
            }

            double x0 = _current[_current.Count - 2];
            double y0 = _current[_current.Count - 1];
            double ax = X(m, x1, y1);
            double ay = Y(m, x1, y1);
            double bx = X(m, x2, y2);
            double by = Y(m, x2, y2);
            double cx = X(m, x3, y3);
            double cy = Y(m, x3, y3);
            double length = Dist(x0, y0, ax, ay) + Dist(ax, ay, bx, by) + Dist(bx, by, cx, cy);
            int pieces = Math.Max(4, Math.Min(48, 4 + (int)(length / 6.0)));

            for (int i = 1; i <= pieces; i++)
            {
                double t = (double)i / pieces;
                double u = 1.0 - t;
                double w0 = u * u * u;
                double w1 = 3.0 * u * u * t;
                double w2 = 3.0 * u * t * t;
                double w3 = t * t * t;
                _current.Add(w0 * x0 + w1 * ax + w2 * bx + w3 * cx);
                _current.Add(w0 * y0 + w1 * ay + w2 * by + w3 * cy);
            }
        }

        private static double Dist(double ax, double ay, double bx, double by) =>
            Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

        private void ClosePath()
        {
            if (_current.Count >= 2)
            {
                EndSubpath(true);
                _current.Add(_startX);
                _current.Add(_startY);
            }
        }

        private void Rectangle(double x, double y, double w, double h)
        {
            EndSubpath(false);
            double[] m = _state.Ctm;
            var points = new[]
            {
                X(m, x, y),
                Y(m, x, y),
                X(m, x + w, y),
                Y(m, x + w, y),
                X(m, x + w, y + h),
                Y(m, x + w, y + h),
                X(m, x, y + h),
                Y(m, x, y + h),
            };
            _lines.Add(points);
            _closed.Add(true);
            _startX = points[0];
            _startY = points[1];
            _current = new List<double>();
        }

        /// <summary>Moves the subpath being built to the list.</summary>
        private void EndSubpath(bool closed)
        {
            if (_current.Count >= 2)
            {
                _lines.Add(_current.ToArray());
                _closed.Add(closed);
            }

            _current = new List<double>();
        }

        private void Paint(bool fill, bool evenOdd, bool stroke)
        {
            EndSubpath(false);

            if (_lines.Count > 0)
            {
                if (fill)
                {
                    EmitFill(evenOdd);
                }

                if (stroke)
                {
                    EmitStroke();
                }

                if (_pendingClip != 0)
                {
                    ApplyClip();
                }
            }
            else if (_pendingClip != 0)
            {
                // A clip with an empty path hides everything that follows.
                _state.Clip = new PdfClip(new double[] { 0, 0, 0, 0, 0, 0, 0, 0 }, _state.Clip);
            }

            _pendingClip = 0;
            _lines.Clear();
            _closed.Clear();
            _current = new List<double>();
        }

        private void EmitFill(bool evenOdd)
        {
            if (_state.FillAlpha <= 0.0)
            {
                return;
            }

            if (_state.FillPattern)
            {
                _scene.Dropped.Add("pattern fills");
                return;
            }

            var area = new PdfFill
            {
                Rgb = _state.FillRgb,
                EvenOdd = evenOdd,
                Clip = _state.Clip,
                Oc = _oc,
            };

            foreach (double[] ring in _lines)
            {
                if (ring.Length >= 6)
                {
                    area.Rings.Add(ring);
                }
            }

            if (area.Rings.Count > 0)
            {
                _scene.Items.Add(area);
            }
        }

        private void EmitStroke()
        {
            if (_state.StrokeAlpha <= 0.0)
            {
                return;
            }

            if (_state.StrokePattern)
            {
                _scene.Dropped.Add("pattern strokes");
                return;
            }

            var line = new PdfStroke
            {
                Rgb = _state.StrokeRgb,
                Width = _state.LineWidth * Scale(_state.Ctm),
                Dash = _state.Dash,
                Phase = _state.Phase,
                Clip = _state.Clip,
                Oc = _oc,
            };

            for (int i = 0; i < _lines.Count; i++)
            {
                line.Lines.Add(_lines[i]);
                line.Closed.Add(_closed[i]);
            }

            _scene.Items.Add(line);
        }

        private void ApplyClip()
        {
            double[] ring;

            if (_lines.Count == 1 && _lines[0].Length >= 6)
            {
                ring = _lines[0];
            }
            else
            {
                // Several pieces: their box, as a clip of many rectangles is as often as not one rectangle's width apart.
                double minX = double.PositiveInfinity;
                double minY = double.PositiveInfinity;
                double maxX = double.NegativeInfinity;
                double maxY = double.NegativeInfinity;

                foreach (double[] piece in _lines)
                {
                    for (int i = 0; i + 1 < piece.Length; i += 2)
                    {
                        minX = Math.Min(minX, piece[i]);
                        maxX = Math.Max(maxX, piece[i]);
                        minY = Math.Min(minY, piece[i + 1]);
                        maxY = Math.Max(maxY, piece[i + 1]);
                    }
                }

                ring = new[] { minX, minY, maxX, minY, maxX, maxY, minX, maxY };
            }

            _state.Clip = new PdfClip((double[])ring.Clone(), _state.Clip);
        }

        #endregion

        #region Text

        private PdfFont FontOf(PdfDict resources, string name)
        {
            PdfDict fonts = _file.Dict(_file.Get(resources, "Font"));
            return fonts != null && fonts.TryGetValue(name, out object font) ? FontOfObject(font) : null;
        }

        private PdfFont FontOfObject(object spec)
        {
            object key = spec is PdfRef reference ? (object)reference.Number : _file.Resolve(spec);

            if (key == null)
            {
                return null;
            }

            if (!_fonts.TryGetValue(key, out PdfFont font))
            {
                PdfDict dictionary = _file.Dict(spec);
                font = dictionary == null ? null : PdfFont.Load(_file, dictionary);
                _fonts[key] = font;
            }

            return font;
        }

        private void NextLine(double tx, double ty)
        {
            _tlm = Mul(new[] { 1.0, 0.0, 0.0, 1.0, tx, ty }, _tlm);
            _tm = (double[])_tlm.Clone();
        }

        private void Show(List<object> pieces)
        {
            PdfFont font = _state.Font;

            if (font == null)
            {
                return;
            }

            if (font.IsType3)
            {
                _scene.Dropped.Add("Type 3 fonts");
            }

            var words = new StringBuilder();
            double[] origin = null;
            double advance = 0.0;
            bool outlines = font.HasOutlines;
            double size = _state.FontSize;

            foreach (object piece in pieces)
            {
                if (piece is double adjust)
                {
                    double shift = -adjust / 1000.0 * size * _state.Scale;
                    _tm = Mul(new[] { 1.0, 0.0, 0.0, 1.0, shift, 0.0 }, _tm);

                    // A gap of a quarter of an em or more starts words of their own, as a table's columns are set in one TJ.
                    if (shift > 0.25 * Math.Abs(size) * _state.Scale)
                    {
                        EmitWords(font, words.ToString(), origin, advance);
                        words.Clear();
                        origin = null;
                        advance = 0.0;
                    }
                    else
                    {
                        advance += shift;
                    }

                    continue;
                }

                if (!(piece is PdfString text))
                {
                    continue;
                }

                foreach (int code in font.Codes(text.Bytes))
                {
                    double[] trm = Mul(
                        new[] { size * _state.Scale, 0.0, 0.0, size, 0.0, _state.Rise },
                        Mul(_tm, _state.Ctm)
                    );

                    if (outlines)
                    {
                        EmitGlyph(font, code, trm);
                    }
                    else if (_state.Render != 3)
                    {
                        origin = origin ?? trm;
                        words.Append(font.TextOf(code));
                    }

                    double width = font.Width(code) / 1000.0 * size + _state.CharSpace;

                    if (code == 32 && !font.IsTwoByte)
                    {
                        width += _state.WordSpace;
                    }

                    double step = width * _state.Scale;
                    _tm = Mul(new[] { 1.0, 0.0, 0.0, 1.0, step, 0.0 }, _tm);
                    advance += step;
                }
            }

            EmitWords(font, words.ToString(), origin, advance);
        }

        /// <summary>Keeps the words of a font with no outlines, set at the pen's matrix of their first letter.</summary>
        private void EmitWords(PdfFont font, string words, double[] origin, double advance)
        {
            if (origin == null || words.Length == 0 || _state.FillAlpha <= 0.0 || font.IsType3)
            {
                return;
            }

            double ux = Math.Sqrt(origin[0] * origin[0] + origin[1] * origin[1]);
            double uy = Math.Sqrt(origin[2] * origin[2] + origin[3] * origin[3]);

            if (!(uy > 0.0) || !(ux > 0.0))
            {
                return;
            }

            _scene.Dropped.Add("text of fonts with no TrueType program, drawn in the font of the same name");
            _scene.Items.Add(
                new PdfWords
                {
                    Text = words,
                    X = origin[4],
                    Y = origin[5],
                    Height = CapHeight * uy,
                    Rotation = Math.Atan2(origin[1], origin[0]),
                    Width = Math.Abs(advance) * Scale(_state.Ctm),
                    Family = font.Family,
                    Rgb = _state.FillRgb,
                    Clip = _state.Clip,
                    Oc = _oc,
                }
            );
        }

        private void EmitGlyph(PdfFont font, int code, double[] trm)
        {
            if (_state.Render == 3)
            {
                return;
            }

            IReadOnlyList<double[]> contours = font.Outline(code);

            if (contours == null || contours.Count == 0)
            {
                return;
            }

            double unit = 1.0 / font.UnitsPerEm;
            var rings = new List<double[]>(contours.Count);

            foreach (double[] contour in contours)
            {
                var ring = new double[contour.Length];

                for (int i = 0; i + 1 < contour.Length; i += 2)
                {
                    ring[i] = X(trm, contour[i] * unit, contour[i + 1] * unit);
                    ring[i + 1] = Y(trm, contour[i] * unit, contour[i + 1] * unit);
                }

                rings.Add(ring);
            }

            if (_state.Render == 0 || _state.Render == 2)
            {
                if (_state.FillAlpha > 0.0)
                {
                    _scene.Items.Add(
                        new PdfFill
                        {
                            Rings = rings,
                            Rgb = _state.FillRgb,
                            EvenOdd = true,
                            Clip = _state.Clip,
                            Oc = _oc,
                        }
                    );
                }
            }

            if (_state.Render == 1 || _state.Render == 2)
            {
                var stroke = new PdfStroke
                {
                    Rgb = _state.StrokeRgb,
                    Width = _state.LineWidth * Scale(_state.Ctm),
                    Clip = _state.Clip,
                    Oc = _oc,
                };

                foreach (double[] ring in rings)
                {
                    stroke.Lines.Add(ring);
                    stroke.Closed.Add(true);
                }

                _scene.Items.Add(stroke);
            }
        }

        #endregion

        #region Things placed

        private PdfOc OcOf(object spec)
        {
            object resolved = _file.Resolve(spec);
            PdfDict dictionary = _file.Dict(resolved);

            if (dictionary == null)
            {
                return null;
            }

            string type = _file.Name(_file.Get(dictionary, "Type"));

            if (type == "OCMD")
            {
                object groups = dictionary.TryGetValue("OCGs", out object g) ? g : null;
                var numbers = new List<int>();

                if (groups is PdfRef one)
                {
                    numbers.Add(one.Number);
                }
                else if (_file.Resolve(groups) is List<object> list)
                {
                    foreach (object item in list)
                    {
                        if (item is PdfRef r)
                        {
                            numbers.Add(r.Number);
                        }
                    }
                }

                return new PdfOc(numbers.ToArray(), _file.Name(_file.Get(dictionary, "P")), _oc);
            }

            return spec is PdfRef reference ? new PdfOc(new[] { reference.Number }, "AnyOn", _oc) : null;
        }

        private void DoXObject(string name, PdfDict resources, int depth)
        {
            PdfDict xobjects = _file.Dict(_file.Get(resources, "XObject"));

            if (
                xobjects == null
                || !xobjects.TryGetValue(name, out object spec)
                || !(_file.Resolve(spec) is PdfStream stream)
            )
            {
                return;
            }

            PdfDict dictionary = stream.Dictionary;
            PdfOc oc = _oc;

            if (dictionary.TryGetValue("OC", out object ocSpec))
            {
                PdfOc own = OcOf(ocSpec);

                if (own != null)
                {
                    _oc = own;
                }
            }

            try
            {
                string subtype = _file.Name(_file.Get(dictionary, "Subtype"));

                if (subtype == "Form")
                {
                    DoForm(stream, resources, depth);
                }
                else if (subtype == "Image")
                {
                    DoImage(stream, spec is PdfRef r ? r.Number : -1, resources);
                }
            }
            finally
            {
                _oc = oc;
            }
        }

        private void DoForm(PdfStream stream, PdfDict resources, int depth)
        {
            if (depth >= DeepestForm)
            {
                return;
            }

            State before = _state.Copy();
            PdfDict dictionary = stream.Dictionary;
            double[] matrix = _file.Numbers(_file.Get(dictionary, "Matrix"));

            if (matrix != null && matrix.Length >= 6)
            {
                _state.Ctm = Mul(
                    new[] { matrix[0], matrix[1], matrix[2], matrix[3], matrix[4], matrix[5] },
                    _state.Ctm
                );
            }

            double[] box = _file.Numbers(_file.Get(dictionary, "BBox"));

            if (box != null && box.Length >= 4)
            {
                double[] m = _state.Ctm;
                var ring = new[]
                {
                    X(m, box[0], box[1]),
                    Y(m, box[0], box[1]),
                    X(m, box[2], box[1]),
                    Y(m, box[2], box[1]),
                    X(m, box[2], box[3]),
                    Y(m, box[2], box[3]),
                    X(m, box[0], box[3]),
                    Y(m, box[0], box[3]),
                };
                _state.Clip = new PdfClip(ring, _state.Clip);
            }

            PdfDict own = _file.Dict(_file.Get(dictionary, "Resources"));

            try
            {
                Execute(_file.Decode(stream), own ?? resources, depth + 1);
            }
            finally
            {
                _state = before;
            }
        }

        private void DoImage(PdfStream stream, int number, PdfDict resources)
        {
            byte[] bytes;

            if (number >= 0 && _pictures.TryGetValue(number, out bytes))
            {
                // Made once, drawn as often as it is placed.
            }
            else
            {
                byte[] data = _file.Decode(stream, out string filter, out _);
                bytes = PdfImages.Make(
                    _file,
                    stream.Dictionary,
                    data,
                    filter,
                    resources,
                    _state.FillRgb,
                    out string why
                );

                if (bytes == null && why != null)
                {
                    _scene.Dropped.Add(why);
                }

                if (number >= 0 && !(_file.Get(stream.Dictionary, "ImageMask") is bool))
                {
                    _pictures[number] = bytes;
                }
            }

            if (bytes != null && _state.FillAlpha > 0.0)
            {
                _scene.Items.Add(
                    new PdfPicture
                    {
                        Matrix = (double[])_state.Ctm.Clone(),
                        Bytes = bytes,
                        Key = number >= 0 ? "pdf-" + number : "pdf-inline-" + (++_inline),
                        Clip = _state.Clip,
                        Oc = _oc,
                    }
                );
            }
        }

        private void InlineImage(PdfParser parser, byte[] bytes, PdfDict resources)
        {
            var dictionary = new PdfDict();

            while (true)
            {
                object key = parser.Read();

                if (key == null && parser.AtEnd)
                {
                    return;
                }

                if (key is PdfKeyword k && k.Value == "ID")
                {
                    break;
                }

                if (!(key is PdfName name))
                {
                    continue;
                }

                dictionary[name.Value] = parser.Read();
            }

            int start = parser.Position;

            if (start < bytes.Length && PdfParser.IsWhite(bytes[start]))
            {
                start++;
            }

            int width = (int)
                _file.Number(
                    dictionary.TryGetValue("W", out object w) ? w
                    : dictionary.TryGetValue("Width", out object w2) ? w2
                    : null
                );
            int height = (int)
                _file.Number(
                    dictionary.TryGetValue("H", out object h) ? h
                    : dictionary.TryGetValue("Height", out object h2) ? h2
                    : null
                );
            bool filtered = dictionary.ContainsKey("F") || dictionary.ContainsKey("Filter");
            int end = -1;

            if (!filtered && width > 0 && height > 0)
            {
                bool mask =
                    (dictionary.TryGetValue("IM", out object im) || dictionary.TryGetValue("ImageMask", out im))
                    && im is bool b
                    && b;
                object spaceSpec =
                    dictionary.TryGetValue("CS", out object cs) ? cs
                    : dictionary.TryGetValue("ColorSpace", out cs) ? cs
                    : null;
                int components =
                    mask ? 1
                    : spaceSpec == null ? 1
                    : PdfColorSpace.Parse(_file, spaceSpec, resources).Components;
                int bits = mask
                    ? 1
                    : (int)
                        _file.Number(
                            dictionary.TryGetValue("BPC", out object bpc) ? bpc
                                : dictionary.TryGetValue("BitsPerComponent", out bpc) ? bpc
                                : null,
                            8
                        );
                long length = (long)((width * components * bits + 7) / 8) * height;

                if (start + length <= bytes.Length)
                {
                    end = (int)(start + length);
                }
            }

            if (end < 0)
            {
                end = start;

                while (end + 1 < bytes.Length)
                {
                    if (
                        bytes[end] == 'E'
                        && bytes[end + 1] == 'I'
                        && end > start
                        && PdfParser.IsWhite(bytes[end - 1])
                        && (
                            end + 2 >= bytes.Length
                            || PdfParser.IsWhite(bytes[end + 2])
                            || PdfParser.IsDelimiter(bytes[end + 2])
                        )
                    )
                    {
                        end--;
                        break;
                    }

                    end++;
                }

                if (end + 1 >= bytes.Length)
                {
                    end = bytes.Length;
                }
            }

            var data = new byte[Math.Max(0, Math.Min(end, bytes.Length) - start)];
            Buffer.BlockCopy(bytes, start, data, 0, data.Length);

            // Past the data, up to and including the EI.
            int p = Math.Min(end, bytes.Length);

            while (p + 1 < bytes.Length && !(bytes[p] == 'E' && bytes[p + 1] == 'I'))
            {
                p++;
            }

            parser.Position = Math.Min(bytes.Length, p + 2);

            try
            {
                var stream = new PdfStream(dictionary, data, 0, data.Length);
                DoImage(stream, -1, resources);
            }
            catch (PdfException)
            {
                _scene.Dropped.Add("pictures that could not be read");
            }
        }

        #endregion
    }
}
