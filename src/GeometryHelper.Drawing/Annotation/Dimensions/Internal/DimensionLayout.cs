using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// A dimension a rule asks for, before and after it is placed. Each end has two points at its place along the axis,
    /// within the tolerance of each other: the one furthest towards the side above or on the right of the view, and the one
    /// furthest towards the side below or on the left, so that the extension lines are the shortest on whichever side the
    /// dimension goes.
    /// </summary>
    internal sealed class DimensionCandidate
    {
        private readonly DimensionPoint _fromAbove;
        private readonly DimensionPoint _toAbove;
        private readonly DimensionPoint _fromBelow;
        private readonly DimensionPoint _toBelow;
        private readonly GeoVector3 _along;

        public DimensionCandidate(DimensionKind kind, int axis, GeoVector3 along, DimensionPoint fromAbove, DimensionPoint toAbove, DimensionPoint fromBelow, DimensionPoint toBelow, int count, string text)
        {
            Kind = kind;
            Axis = axis;
            _along = along;
            _fromAbove = fromAbove;
            _toAbove = toAbove;
            _fromBelow = fromBelow;
            _toBelow = toBelow;
            Count = count;
            Text = text;
            Side = 1;
        }

        public DimensionKind Kind { get; }

        /// <summary>0 for a dimension along the view's X, 1 along its Y.</summary>
        public int Axis { get; }

        /// <summary>1 where the dimension goes above or on the right of the view, -1 below or on the left.</summary>
        public int Side { get; set; }

        /// <summary>The lower point of a linear dimension, the origin of a cumulative or ordinate one, on the dimension's side.</summary>
        public DimensionPoint From => FromOn(Side);

        /// <summary>The higher point of a linear dimension, the point a cumulative or ordinate one measures to, on its side.</summary>
        public DimensionPoint To => ToOn(Side);

        public int Count { get; }

        /// <summary>The distance between the model points of the dimension's side along the axis.</summary>
        public double Value => Math.Abs(_along.DotProduct(From.ModelPoint.GetVectorTo(To.ModelPoint)));

        public string Text { get; }

        public DimensionPoint FromOn(int side) => side > 0 ? _fromAbove : _fromBelow;

        public DimensionPoint ToOn(int side) => side > 0 ? _toAbove : _toBelow;

        public bool IsOrdinate => Kind == DimensionKind.Cumulative || Kind == DimensionKind.Ordinate;

        public double Low => Along(From);

        public double High => Along(To);

        public DimensionShape Shape { get; set; }

        public int Tier { get; set; }

        public double Along(DimensionPoint p) => Axis == 0 ? p.ViewPoint.X : p.ViewPoint.Y;

        public double Across(DimensionPoint p) => Axis == 0 ? p.ViewPoint.Y : p.ViewPoint.X;
    }

    /// <summary>
    /// Places dimensions in tiers outside the box of a view's lines, each where it overlaps none placed before it: dimensions
    /// along X above the view, else below; along Y on its right, else on its left.
    /// </summary>
    /// <remarks>
    /// A group, the chain of one axis or the dimensions of one row of holes, is placed on one tier together where it can be,
    /// on the nearest tier of the first side where every one of it fits; else each of it alone. A text goes in the middle of
    /// its dimension line where it fits between the extension lines with a gap each side, else past the line's upper end,
    /// else past its lower end. A dimension placed outermost goes on a tier beyond every tier used on its side, and those
    /// placed after it on that side stay inside it. A dimension that fits on no tier of either side is left out.
    /// </remarks>
    internal sealed class DimensionLayout
    {
        private const int MaxTiers = 24;

        private readonly double _minX;
        private readonly double _minY;
        private readonly double _maxX;
        private readonly double _maxY;
        private readonly double _height;
        private readonly double _gap;
        private readonly double _offPoint;
        private readonly double _pastLine;
        private readonly double _arrow;
        private readonly double _spacing;
        private readonly double _charWidth;
        private readonly double _tol;
        private readonly List<DimensionCandidate> _placed = new List<DimensionCandidate>();
        private readonly int[] _outer = { -1, -1, -1, -1 };

        // The tier of the outermost dimension of each axis and side, below which those placed after it stay.
        private readonly int[] _cap = { int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue };

        public DimensionLayout(GeoRectangle2 bounds, DimensionSettings settings, double tol)
        {
            _minX = bounds.Center.X - 0.5 * bounds.Width;
            _maxX = bounds.Center.X + 0.5 * bounds.Width;
            _minY = bounds.Center.Y - 0.5 * bounds.Height;
            _maxY = bounds.Center.Y + 0.5 * bounds.Height;
            _height = settings.TextHeight * settings.Scale;
            _gap = 0.25 * _height;
            _offPoint = 0.25 * _height;
            _pastLine = 0.5 * _height;
            _arrow = _height;
            _spacing = settings.TierSpacing * settings.Scale;
            _charWidth = 0.8 * _height;
            _tol = tol;
        }

        /// <summary>Gets the dimensions placed, in the order they were placed.</summary>
        public List<DimensionCandidate> Placed => _placed;

        /// <summary>Gets the dimensions that found no place, in the order they were tried.</summary>
        public List<DimensionCandidate> Dropped { get; } = new List<DimensionCandidate>();

        /// <summary>Gets the height of the text in the view.</summary>
        public double TextHeight => _height;

        /// <summary>Gets how far a tier stands off the box of the view.</summary>
        public double Offset(int tier) => (tier + 1) * _spacing;

        /// <summary>Places a group of dimensions of one axis, together where it can, else one by one.</summary>
        public void Place(List<DimensionCandidate> group, bool outermost)
        {
            if (group.Count == 0 || TryPlace(group, outermost))
            {
                return;
            }

            foreach (DimensionCandidate candidate in group)
            {
                if (group.Count == 1 || !TryPlace(new List<DimensionCandidate> { candidate }, outermost))
                {
                    Dropped.Add(candidate);
                }
            }
        }

        private bool TryPlace(List<DimensionCandidate> group, bool outermost)
        {
            int axis = group[0].Axis;

            for (int sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                int side = sideIndex == 0 ? 1 : -1;
                int first = outermost ? _outer[2 * axis + sideIndex] + 1 : 0;
                int last = outermost ? first + MaxTiers : Math.Min(MaxTiers, _cap[2 * axis + sideIndex]);

                for (int tier = first; tier < last; tier++)
                {
                    var shapes = new DimensionShape[group.Count];
                    bool fits = true;

                    for (int i = 0; i < group.Count && fits; i++)
                    {
                        shapes[i] = Fit(group[i], side, tier, shapes, i);
                        fits = shapes[i] != null;
                    }

                    if (!fits)
                    {
                        continue;
                    }

                    for (int i = 0; i < group.Count; i++)
                    {
                        group[i].Shape = shapes[i];
                        group[i].Tier = tier;
                        group[i].Side = side;
                        _placed.Add(group[i]);
                    }

                    _outer[2 * axis + sideIndex] = Math.Max(_outer[2 * axis + sideIndex], tier);

                    if (outermost)
                    {
                        _cap[2 * axis + sideIndex] = Math.Min(_cap[2 * axis + sideIndex], tier);
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>The first way of drawing a dimension on a tier that overlaps nothing placed, nor the group's own before it.</summary>
        private DimensionShape Fit(DimensionCandidate candidate, int side, int tier, DimensionShape[] group, int count)
        {
            for (int option = 0; option < 3; option++)
            {
                DimensionShape shape = Shape(candidate, side, tier, option);

                if (shape != null && IsFree(shape, group, count))
                {
                    return shape;
                }
            }

            return null;
        }

        private bool IsFree(DimensionShape shape, DimensionShape[] group, int count)
        {
            foreach (DimensionCandidate placed in _placed)
            {
                if (DimensionShape.Conflict(shape, placed.Shape, _tol) != null)
                {
                    return false;
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (DimensionShape.Conflict(shape, group[i], _tol) != null)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// A dimension drawn on a tier of a side, its text in the middle (option 0), past its upper end (1) or past its lower
        /// end (2); null where the option does not apply.
        /// </summary>
        private DimensionShape Shape(DimensionCandidate c, int side, int tier, int option)
        {
            double off = Offset(tier);
            double h = _height;
            double w = c.Text.Length * _charWidth;
            bool alongX = c.Axis == 0;
            double line = alongX ? (side > 0 ? _maxY + off : _minY - off) : (side > 0 ? _maxX + off : _minX - off);

            if (c.IsOrdinate)
            {
                if (option > 0)
                {
                    return null;
                }

                GeoPoint2 p = c.ToOn(side).ViewPoint;
                double start = side > 0 ? line + _gap : line - _gap - w;

                if (alongX)
                {
                    var leader = new GeoLine2(p.X, p.Y + side * _offPoint, p.X, line);
                    return new DimensionShape(null, new[] { leader }, p.X - 0.5 * h, start, p.X + 0.5 * h, start + w, 0.5 * Math.PI);
                }
                else
                {
                    var leader = new GeoLine2(p.X + side * _offPoint, p.Y, line, p.Y);
                    return new DimensionShape(null, new[] { leader }, start, p.Y - 0.5 * h, start + w, p.Y + 0.5 * h, 0.0);
                }
            }

            double lo = c.Along(c.FromOn(side)), hi = c.Along(c.ToOn(side));
            double textLow;

            switch (option)
            {
                case 0:
                    if (hi - lo < w + 2.0 * _gap)
                    {
                        return null;
                    }

                    textLow = 0.5 * (lo + hi) - 0.5 * w;
                    break;
                case 1:
                    textLow = hi + _arrow + _gap;
                    break;
                default:
                    textLow = lo - _arrow - _gap - w;
                    break;
            }

            GeoPoint2 a = c.FromOn(side).ViewPoint, b = c.ToOn(side).ViewPoint;

            if (alongX)
            {
                var dimensionLine = new GeoLine2(lo, line, hi, line);
                GeoLine2[] extensions =
                {
                    new GeoLine2(a.X, a.Y + side * _offPoint, a.X, line + side * _pastLine),
                    new GeoLine2(b.X, b.Y + side * _offPoint, b.X, line + side * _pastLine),
                };

                // The text stands above its line, read from below, whichever side of the view the line is on.
                return new DimensionShape(dimensionLine, extensions, textLow, line + _gap, textLow + w, line + _gap + h, 0.0);
            }
            else
            {
                var dimensionLine = new GeoLine2(line, lo, line, hi);
                GeoLine2[] extensions =
                {
                    new GeoLine2(a.X + side * _offPoint, a.Y, line + side * _pastLine, a.Y),
                    new GeoLine2(b.X + side * _offPoint, b.Y, line + side * _pastLine, b.Y),
                };

                // Read from the right, the text's top is towards -X: it stands on the left of its line.
                return new DimensionShape(dimensionLine, extensions, line - _gap - h, textLow, line - _gap, textLow + w, 0.5 * Math.PI);
            }
        }
    }
}
