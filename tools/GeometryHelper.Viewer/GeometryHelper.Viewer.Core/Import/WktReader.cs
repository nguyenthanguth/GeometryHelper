using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Reads well-known text: one geometry after another, in any mix of lines. POINT, LINESTRING, POLYGON, their MULTI forms,
    /// GEOMETRYCOLLECTION, POLYHEDRALSURFACE, TIN and TRIANGLE are read, with or without Z, with an M left out, and EMPTY gives nothing.
    /// </summary>
    internal sealed class WktReader
    {
        private const string Layer = "wkt";

        private static readonly string[] Known =
        {
            "GEOMETRYCOLLECTION", "MULTILINESTRING", "MULTIPOLYGON", "POLYHEDRALSURFACE", "LINESTRING", "MULTIPOINT", "POLYGON", "TRIANGLE", "POINT", "TIN",
        };

        private readonly string _text;
        private readonly ImportBuilder _b;
        private readonly CancellationToken _cancel;
        private readonly List<Action> _pending = new List<Action>();
        private int _at;
        private int _depth;

        // What the geometry being read says it has: 0 not said yet, 2 or 3 coordinates a point, and how many numbers a point has in the file.
        private int _dimension;
        private bool _dimensionFixed;

        private WktReader(string text, ImportBuilder builder, CancellationToken cancel)
        {
            _text = text;
            _b = builder;
            _cancel = cancel;
        }

        internal static Scene Read(string text, string title, CancellationToken cancel)
        {
            var b = new ImportBuilder(title);
            var reader = new WktReader(text, b, cancel);
            reader.ReadAll();
            return b.Finish();
        }

        private void ReadAll()
        {
            int statements = 0;

            while (true)
            {
                _cancel.ThrowIfCancellationRequested();
                SkipBlank();

                if (_at >= _text.Length)
                {
                    break;
                }

                int start = _at;
                _pending.Clear();

                try
                {
                    Statement();
                }
                catch (FormatException e)
                {
                    // A statement that is not well-formed is left out whole; the ones before it stay. A file in which none is read is no WKT.
                    if (statements == 0 && _b.Scene.Items.Count == 0)
                    {
                        throw;
                    }

                    _b.Warn("WKT: reading stopped at " + Where(start) + ": " + e.Message);
                    return;
                }

                foreach (Action add in _pending)
                {
                    add();
                }

                statements++;
            }
        }

        #region The text

        private void SkipBlank()
        {
            while (_at < _text.Length)
            {
                char c = _text[_at];

                if (char.IsWhiteSpace(c) || c == ';')
                {
                    _at++;
                }
                else if (c == '#' || (c == '-' && _at + 1 < _text.Length && _text[_at + 1] == '-') || (c == '/' && _at + 1 < _text.Length && _text[_at + 1] == '/'))
                {
                    // A comment to the end of its line.
                    while (_at < _text.Length && _text[_at] != '\n')
                    {
                        _at++;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        private void SkipSpace()
        {
            while (_at < _text.Length && char.IsWhiteSpace(_text[_at]))
            {
                _at++;
            }
        }

        private string Where(int position)
        {
            int line = 1;
            int column = 1;

            for (int i = 0; i < position && i < _text.Length; i++)
            {
                if (_text[i] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }

            return "line " + line.ToString(CultureInfo.InvariantCulture) + ", column " + column.ToString(CultureInfo.InvariantCulture);
        }

        private FormatException Problem(string what) => new FormatException(what + " at " + Where(_at));

        private bool Peek(char c)
        {
            SkipSpace();
            return _at < _text.Length && _text[_at] == c;
        }

        private void Expect(char c)
        {
            SkipSpace();

            if (_at >= _text.Length || _text[_at] != c)
            {
                throw Problem("'" + c + "' expected");
            }

            _at++;
        }

        private string Word()
        {
            SkipSpace();
            int start = _at;

            while (_at < _text.Length && (char.IsLetter(_text[_at]) || _text[_at] == '_'))
            {
                _at++;
            }

            return _text.Substring(start, _at - start).ToUpperInvariant();
        }

        #endregion

        #region Geometries

        private void Statement()
        {
            SkipSpace();

            // SRID=4326;POINT(...) from PostGIS' extended form.
            if (string.Compare(_text, _at, "SRID=", 0, 5, StringComparison.OrdinalIgnoreCase) == 0)
            {
                while (_at < _text.Length && _text[_at] != ';')
                {
                    _at++;
                }

                _at++;
            }

            Geometry();
        }

        private void Geometry()
        {
            if (++_depth > 64)
            {
                throw Problem("collections nested more than 64 deep");
            }

            try
            {
                GeometryBody();
            }
            finally
            {
                _depth--;
            }
        }

        private void GeometryBody()
        {
            string word = Word();

            if (word.Length == 0)
            {
                throw Problem("a geometry name (POINT, POLYGON, ...) expected");
            }

            // The Z, M or ZM may be glued to the name, POINTZ, or stand after it, POINT Z.
            string name = word;
            string tag = string.Empty;

            foreach (string known in Known)
            {
                if (word.StartsWith(known, StringComparison.Ordinal) && (word.Length == known.Length || word.Substring(known.Length) == "Z" || word.Substring(known.Length) == "M" || word.Substring(known.Length) == "ZM"))
                {
                    name = known;
                    tag = word.Substring(known.Length);
                    break;
                }
            }

            if (tag.Length == 0)
            {
                SkipSpace();
                int save = _at;
                string next = Word();

                if (next == "Z" || next == "M" || next == "ZM")
                {
                    tag = next;
                }
                else
                {
                    _at = save;
                }
            }

            _dimensionFixed = tag.Length > 0;
            _dimension = tag == "Z" || tag == "ZM" ? 3 : tag == "M" ? 2 : 0;

            SkipSpace();
            int afterTag = _at;

            if (Array.IndexOf(Known, name) < 0)
            {
                // A curve or a surface this reader does not know has its whole parenthesis skipped, and said; a word that is followed by neither
                // that nor EMPTY is no geometry at all.
                if (string.Compare(_text, afterTag, "EMPTY", 0, 5, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    _at = afterTag + 5;
                }
                else if (afterTag < _text.Length && _text[afterTag] == '(')
                {
                    SkipGroup();
                }
                else
                {
                    throw Problem("'" + word.ToLowerInvariant() + "' is not the name of a geometry");
                }

                _b.Warn("WKT: " + name + " is not read (the geometries read are POINT, LINESTRING, POLYGON, the MULTI forms, GEOMETRYCOLLECTION, POLYHEDRALSURFACE, TIN and TRIANGLE)");
                return;
            }

            if (string.Compare(_text, afterTag, "EMPTY", 0, 5, StringComparison.OrdinalIgnoreCase) == 0)
            {
                _at = afterTag + 5;
                return;
            }

            switch (name)
            {
                case "POINT":
                    Expect('(');
                    {
                        Vec3 p = Coordinate();
                        Expect(')');
                        int dim = _dimension;
                        _pending.Add(() => _b.Add(dim == 3 ? "GeoPoint3" : "GeoPoint2", ImportData.Point(p, dim), Layer, null, null, "WKT POINT"));
                    }

                    break;

                case "LINESTRING":
                    LineString();
                    break;

                case "POLYGON":
                case "TRIANGLE":
                    Polygon(name);
                    break;

                case "MULTIPOINT":
                    MultiPoint();
                    break;

                case "MULTILINESTRING":
                    Expect('(');

                    do
                    {
                        LineString();
                    }
                    while (Comma());

                    Expect(')');
                    break;

                case "MULTIPOLYGON":
                    Expect('(');

                    do
                    {
                        Polygon("POLYGON");
                    }
                    while (Comma());

                    Expect(')');
                    break;

                case "POLYHEDRALSURFACE":
                case "TIN":
                    Surface(name);
                    break;

                case "GEOMETRYCOLLECTION":
                    Expect('(');

                    do
                    {
                        Geometry();
                    }
                    while (Comma());

                    Expect(')');
                    break;
            }

        }

        private bool Comma()
        {
            if (Peek(','))
            {
                _at++;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Skips an unknown geometry: whatever follows up to and through the parenthesis that closes the first one.
        /// </summary>
        private void SkipGroup()
        {
            SkipSpace();
            int depth = 0;

            while (_at < _text.Length)
            {
                char c = _text[_at++];

                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    depth--;

                    if (depth <= 0)
                    {
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Reads one point: two numbers, or three, or four with an M, as the geometry's tag says or, with none, as the first point does.
        /// </summary>
        private Vec3 Coordinate()
        {
            var numbers = new List<double>(4);

            while (true)
            {
                SkipSpace();
                int start = _at;

                while (_at < _text.Length && (char.IsLetterOrDigit(_text[_at]) || _text[_at] == '.' || _text[_at] == '+' || _text[_at] == '-'))
                {
                    _at++;
                }

                if (start == _at)
                {
                    break;
                }

                if (!ImportNumber.TryParse(_text.Substring(start, _at - start), out double value))
                {
                    _at = start;
                    throw Problem("a number expected");
                }

                numbers.Add(value);
            }

            int dimension = _dimensionFixed ? _dimension : numbers.Count == 2 ? 2 : 3;

            if (numbers.Count < dimension || numbers.Count < 2 || numbers.Count > 4)
            {
                throw Problem("a point of " + (_dimensionFixed && _dimension == 3 ? "three" : "two or three") + " numbers expected, " + numbers.Count.ToString(CultureInfo.InvariantCulture) + " found");
            }

            if (!_dimensionFixed && _dimension != 0 && _dimension != dimension)
            {
                throw Problem("points of two and three numbers in one geometry");
            }

            _dimension = dimension;
            return new Vec3(numbers[0], numbers[1], dimension == 3 ? numbers[2] : 0.0);
        }

        private List<Vec3> Coordinates()
        {
            var points = new List<Vec3>();
            Expect('(');

            do
            {
                points.Add(Coordinate());
            }
            while (Comma());

            Expect(')');
            return points;
        }

        private void LineString()
        {
            if (!Peek('('))
            {
                // MULTILINESTRING (EMPTY, ...) has members with nothing in them.
                Word();
                return;
            }

            List<Vec3> points = Coordinates();
            int dim = _dimension;

            if (points.Count < 2)
            {
                _b.Warn("WKT: a LINESTRING of fewer than two points was left out");
                return;
            }

            _pending.Add(() =>
            {
                bool segment = points.Count == 2;
                string kind = (segment ? "GeoLine" : "GeoPolyline") + dim.ToString(CultureInfo.InvariantCulture);
                _b.Add(kind, segment ? ImportData.Line(points[0], points[1], dim) : ImportData.Chain(points, dim), Layer, null, null, "WKT LINESTRING");
            });
        }

        private void MultiPoint()
        {
            Expect('(');

            do
            {
                // Both MULTIPOINT ((1 2), (3 4)) and MULTIPOINT (1 2, 3 4) are written.
                bool wrapped = Peek('(');

                if (wrapped)
                {
                    _at++;
                }

                Vec3 p = Coordinate();

                if (wrapped)
                {
                    Expect(')');
                }

                int dim = _dimension;
                _pending.Add(() => _b.Add(dim == 3 ? "GeoPoint3" : "GeoPoint2", ImportData.Point(p, dim), Layer, null, null, "WKT MULTIPOINT"));
            }
            while (Comma());

            Expect(')');
        }

        /// <summary>
        /// Reads the rings of a polygon, each with the point that closes it taken off, as the format writes a ring closed.
        /// </summary>
        private List<Vec3[]> Rings(string what)
        {
            var rings = new List<Vec3[]>();
            Expect('(');

            do
            {
                List<Vec3> ring = Coordinates();

                if (ring.Count >= 2 && new BitKey(ring[0]).Equals(new BitKey(ring[ring.Count - 1])))
                {
                    ring.RemoveAt(ring.Count - 1);
                }
                else
                {
                    _b.Warn("WKT: a ring of a " + what + " that does not return to its first point was kept as it is");
                }

                rings.Add(ring.ToArray());
            }
            while (Comma());

            Expect(')');
            return rings;
        }

        private void Polygon(string name)
        {
            if (!Peek('('))
            {
                Word();
                return;
            }

            List<Vec3[]> rings = Rings(name);
            int dim = _dimension;

            if (rings.Count == 0 || rings[0].Length < 3)
            {
                _b.Warn("WKT: a " + name + " whose outer ring has fewer than three points was left out");
                return;
            }

            if (name == "TRIANGLE" && rings.Count == 1 && rings[0].Length == 3)
            {
                _pending.Add(() => _b.Add("GeoTriangle" + dim.ToString(CultureInfo.InvariantCulture), ImportData.Triangle(rings[0][0], rings[0][1], rings[0][2], dim), Layer, null, null, "WKT TRIANGLE"));
                return;
            }

            // A ring that is too small to be one is a hole the library could not take; it is left out and said.
            Vec3[][] holes = rings.Skip(1).Where(r => r.Length >= 3).ToArray();

            if (holes.Length < rings.Count - 1)
            {
                _b.Warn("WKT: a hole of fewer than three points was left out");
            }

            _pending.Add(() =>
            {
                string number = dim.ToString(CultureInfo.InvariantCulture);

                if (holes.Length == 0)
                {
                    _b.Add("GeoPolygon" + number, ImportData.Chain(rings[0], dim), Layer, null, null, "WKT " + name);
                }
                else
                {
                    _b.Add("GeoFace" + number, ImportData.Face(rings[0], holes, dim), Layer, null, null, "WKT " + name);
                }
            });
        }

        /// <summary>
        /// Reads a POLYHEDRALSURFACE or a TIN: a patch is a polygon, and the patches share their corners where they are the same to the bit.
        /// </summary>
        private void Surface(string name)
        {
            Expect('(');
            var weld = new MeshWelder();
            bool innerRings = false;
            int dim = 3;

            do
            {
                List<Vec3[]> rings = Rings(name + " patch");
                dim = _dimension;

                if (rings.Count == 0 || rings[0].Length < 3)
                {
                    _b.Warn("WKT: a patch of a " + name + " with fewer than three corners was left out");
                    continue;
                }

                innerRings |= rings.Count > 1;
                weld.Faces.Add(rings[0].Select(weld.Vertex).ToArray());
            }
            while (Comma());

            Expect(')');

            if (innerRings)
            {
                _b.Warn("WKT: the inner rings of the patches of a " + name + " were left out");
            }

            if (weld.Faces.Count > 0)
            {
                _pending.Add(() => _b.Add("GeoMesh" + dim.ToString(CultureInfo.InvariantCulture), ImportData.Mesh(weld.Points, weld.Faces, dim), Layer, null, null, "WKT " + name));
            }
        }

        #endregion
    }
}
