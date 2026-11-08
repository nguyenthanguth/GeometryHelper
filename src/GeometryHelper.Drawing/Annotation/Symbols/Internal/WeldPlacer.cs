using System;
using System.Collections.Generic;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// The placing of <see cref="WeldSymbol"/>.Find: each symbol's knee tried at places round its weld, the nearest first,
    /// until the symbol is clear of the dimensions, of the boxes and lines kept clear of, and of the symbols placed before it.
    /// </summary>
    internal static class WeldPlacer
    {
        /// <summary>The farthest a knee is tried off its weld, across and up, in half heights of the text: thirty heights.</summary>
        private const int Farthest = 60;

        /// <summary>The farthest a knee is tried off its weld while the symbol is kept clear of the view's lines too: eight heights.</summary>
        private const int Near = 16;

        /// <summary>
        /// The knees tried, in half heights of the text across and up off the weld: from two heights each way, the arrow no
        /// flatter than one in two and no steeper than two in one, the nearest first; of two as near, the lower first. The
        /// first is the rule's own place, two heights up and two to the right.
        /// </summary>
        private static readonly (int Across, int Up)[] Offsets = MakeOffsets();

        /// <summary>The index of the last of <see cref="Offsets"/> no farther than <see cref="Near"/> either way.</summary>
        private static readonly int LastNear = Array.FindLastIndex(Offsets, o => Math.Max(o.Across, o.Up) <= Near);

        /// <summary>
        /// The ways round a weld a knee is tried, in turn at each offset: up and right, up and left, down and right, down and
        /// left; a knee left of its weld has its reference line run to the left, so that the arrow leaves the line's end.
        /// </summary>
        private static readonly (int X, int Y, bool Leftward)[] Ways = { (1, 1, false), (-1, 1, true), (1, -1, false), (-1, -1, true) };

        /// <summary>
        /// Places the symbols in the order given: each at the first place clear of everything within <see cref="Near"/> and
        /// clear of the view's lines too, else at the first place clear of everything, else where the rule put it, named in
        /// the list of those stuck.
        /// </summary>
        internal static List<WeldSymbol> Place(List<WeldSymbol> ruled, ProjectedView2 view, List<DrawingDimension> dimensions, List<GeoRectangle2> boxes, List<GeoLine2> lines, Tolerance tolerance, out List<string> stuck)
        {
            double tol = tolerance.EqualPoint;
            var placed = new List<WeldSymbol>(ruled.Count);
            stuck = new List<string>();
            List<GeoLine2> viewLines = ruled.Count == 0 ? null : MarkPlacer.ViewLines(view, ruled[0].TextHeight, tol);

            foreach (WeldSymbol symbol in ruled)
            {
                WeldSymbol best = Search(symbol, placed, dimensions, boxes, lines, viewLines, tol);

                if (best == null)
                {
                    stuck.Add(symbol.Id);
                    best = symbol;
                }

                placed.Add(best);
            }

            return placed;
        }

        /// <summary>The first place of a symbol clear of everything and of the view's lines near its weld; else the first clear of everything; else null.</summary>
        private static WeldSymbol Search(WeldSymbol symbol, List<WeldSymbol> placed, List<DrawingDimension> dimensions, List<GeoRectangle2> boxes, List<GeoLine2> lines, List<GeoLine2> viewLines, double tol)
        {
            double half = 0.5 * symbol.TextHeight;
            WeldSymbol fallback = null;

            for (int i = 0; i < Offsets.Length; i++)
            {
                if (i > LastNear && fallback != null)
                {
                    return fallback;
                }

                (int across, int up) = Offsets[i];

                foreach ((int x, int y, bool leftward) in Ways)
                {
                    var knee = new GeoPoint2(symbol.ArrowTip.X + x * across * half, symbol.ArrowTip.Y + y * up * half);
                    WeldSymbol at = i == 0 && x == 1 && y == 1 ? symbol : symbol.At(knee, leftward);

                    if (!Clear(at, placed, dimensions, boxes, lines, tol))
                    {
                        continue;
                    }

                    if (Math.Max(across, up) > Near)
                    {
                        fallback = fallback ?? at;
                        continue;
                    }

                    GeoRectangle2 bounds = at.Bounds;

                    if (!viewLines.Exists(l => Box2.Crosses(bounds, l.StartPoint, l.EndPoint, tol)))
                    {
                        return at;
                    }

                    fallback = fallback ?? at;
                }
            }

            return fallback;
        }

        /// <summary>
        /// Whether a symbol, its box with 0.4 of the text's height round it, is clear of the dimensions, the boxes and
        /// lines kept clear of, and the symbols placed before, and its arrow crosses no text, box or symbol.
        /// </summary>
        private static bool Clear(WeldSymbol symbol, List<WeldSymbol> placed, List<DrawingDimension> dimensions, List<GeoRectangle2> boxes, List<GeoLine2> lines, double tol)
        {
            GeoRectangle2 bounds = symbol.Bounds;
            double margin = 0.8 * symbol.TextHeight;
            var room = new GeoRectangle2(bounds.Center, bounds.Width + margin, bounds.Height + margin);

            if (DimensionClash(symbol, room, dimensions, tol, null)
                || Marker.SymbolClash(symbol.Id, room, symbol.Knee, symbol.ArrowTip, boxes, lines, tol, null))
            {
                return false;
            }

            foreach (WeldSymbol other in placed)
            {
                if (Marker.MarkClash(symbol.Id, room, symbol.Knee, symbol.ArrowTip, other.Id, other.Bounds, other.Knee, other.ArrowTip, tol, null))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Adds a line for each way a symbol, taken as a box, meets the dimensions as they are drawn: the box over a
        /// dimension's text, its line or an extension line, its arrow across a dimension's text; with no list, says whether
        /// there was one. A dimension line shorter than two arrows is taken run on by an arrow at each end, as it is drawn
        /// with its arrows outside.
        /// </summary>
        internal static bool DimensionClash(WeldSymbol symbol, GeoRectangle2 box, List<DrawingDimension> dimensions, double tol, List<string> found)
        {
            bool any = false;

            foreach (DrawingDimension d in dimensions)
            {
                var hits = new List<string>();

                if (Box2.Overlap(box, d.TextBox, tol))
                {
                    hits.Add(" over the text of dimension ");
                }

                if (d.DimensionLine.HasValue)
                {
                    GeoLine2 line = Drawn(d.DimensionLine.Value, d.TextHeight);

                    if (Box2.Crosses(box, line.StartPoint, line.EndPoint, tol))
                    {
                        hits.Add(" over the line of dimension ");
                    }
                }

                foreach (GeoLine2 extension in d.ExtensionLines)
                {
                    if (Box2.Crosses(box, extension.StartPoint, extension.EndPoint, tol))
                    {
                        hits.Add(" over an extension line of dimension ");
                        break;
                    }
                }

                if (Box2.Crosses(d.TextBox, symbol.Knee, symbol.ArrowTip, tol))
                {
                    hits.Add(" arrow across the text of dimension ");
                }

                if (hits.Count == 0)
                {
                    continue;
                }

                if (found == null)
                {
                    return true;
                }

                any = true;
                hits.ForEach(h => found.Add(symbol.Id + h + d.Id));
            }

            return any;
        }

        /// <summary>A dimension line as it is drawn: run on by an arrow's length, the text's height, at each end where it is shorter than two.</summary>
        private static GeoLine2 Drawn(GeoLine2 line, double arrow)
        {
            double length = line.Length;

            if (length >= 2.0 * arrow || !(length > 0.0))
            {
                return line;
            }

            double ux = (line.EndPoint.X - line.StartPoint.X) / length * arrow, uy = (line.EndPoint.Y - line.StartPoint.Y) / length * arrow;
            return new GeoLine2(line.StartPoint.X - ux, line.StartPoint.Y - uy, line.EndPoint.X + ux, line.EndPoint.Y + uy);
        }

        private static (int Across, int Up)[] MakeOffsets()
        {
            var offsets = new List<(int Across, int Up)>();

            for (int across = 4; across <= Farthest; across++)
            {
                for (int up = 4; up <= Farthest; up++)
                {
                    if (up <= 2 * across && across <= 2 * up)
                    {
                        offsets.Add((across, up));
                    }
                }
            }

            // Whole numbers, and no two alike, so the order is a full one.
            offsets.Sort((a, b) =>
            {
                int c = (a.Across * a.Across + a.Up * a.Up).CompareTo(b.Across * b.Across + b.Up * b.Up);
                c = c != 0 ? c : a.Up.CompareTo(b.Up);
                return c != 0 ? c : a.Across.CompareTo(b.Across);
            });

            return offsets.ToArray();
        }
    }
}
