using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using GeometryHelper.Internal;

namespace GeometryHelper.Dxf
{
    public sealed partial class DxfMLeader
    {
        /// <summary>
        /// Gets the breaks of a leader line, as AutoCAD 2021's DIMBREAK keeps them in the line (90, 11 and 12 of its LEADER_LINE):
        /// for each the segment of the line it is in and where it starts and ends, in the order the record has them. A line no
        /// DIMBREAK has touched has none.
        /// </summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <returns>The breaks.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index.</exception>
        public IReadOnlyList<DxfLeaderBreak> GetBreaks(int leaderLineIndex) =>
            FindLine(Read(), leaderLineIndex, out _).Breaks().AsReadOnly();

        /// <summary>
        /// Sets the breaks of a leader line, in place of those it has, as AutoCAD 2021 writes them: grouped by segment, the
        /// segments in order, after the line's vertices. <see cref="Explode"/> gives the line whole, as AutoCAD's does;
        /// <see cref="ExplodeBroken"/> gives it cut at the breaks, as AutoCAD draws it. A line of one segment, from (0, 0, 0) to
        /// (100, 0, 0), given a break from (39.9375, 0, 0) to (40.0625, 0, 0) is drawn in two, to 39.9375 and from 40.0625.
        /// </summary>
        /// <param name="leaderLineIndex">The index of the line.</param>
        /// <param name="breaks">The breaks; none takes those it has away.</param>
        /// <exception cref="ArgumentNullException">Thrown when the breaks are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the MULTILEADER has no line of the index.</exception>
        /// <exception cref="ArgumentException">Thrown when a break has a negative segment or a point that is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        public void SetBreaks(int leaderLineIndex, IEnumerable<DxfLeaderBreak> breaks)
        {
            if (breaks == null)
            {
                throw new ArgumentNullException(nameof(breaks));
            }

            List<DxfLeaderBreak> given = breaks.ToList();

            foreach (DxfLeaderBreak item in given)
            {
                Guard.Argument(item.Segment >= 0, nameof(breaks), "A break is in a segment from nought.");
                Guard.Argument(
                    item.Start.IsValid && item.End.IsValid,
                    nameof(breaks),
                    "The points of a break have to be finite numbers."
                );
            }

            Rec rec = Read();
            FindLine(rec, leaderLineIndex, out _).SetBreaks(given);
            Write(rec);
        }

        /// <summary>
        /// Gets the entities the multileader breaks into, as <see cref="Explode"/> gives them, but with each leader line cut at its
        /// breaks (<see cref="GetBreaks"/>), as AutoCAD draws a multileader that DIMBREAK has broken: AutoCAD 2021's
        /// <c>Explode</c> gives the lines whole. A line from (0, 0, 0) to (100, 0, 0) broken from 40 to 50 gives two polylines,
        /// from the back of its arrow to 40 and from 50 to the end. A line splined, and the arrows, doglegs and words, are as
        /// <see cref="Explode"/> gives them.
        /// </summary>
        /// <returns>The entities, in no file, to be added to a drawing by <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown as <see cref="Explode"/> says.</exception>
        public IReadOnlyList<DxfEntity> ExplodeBroken() => ExplodeParts(true);

        /// <summary>
        /// The vertices of a line cut into the stretches its breaks leave: the stretch of a segment between the start and the end of
        /// each break in it is left out, the rest drawn on across the vertices where no break stops it; the points of a stretch
        /// that ends at a vertex are the vertex itself, to the bit.
        /// </summary>
        private static List<List<GeoPoint3>> Pieces(List<GeoPoint3> vertices, List<DxfLeaderBreak> breaks)
        {
            const double near = 1E-9;
            var pieces = new List<List<GeoPoint3>>();
            List<GeoPoint3> current = null;

            for (int k = 0; k + 1 < vertices.Count; k++)
            {
                GeoPoint3 a = vertices[k];
                GeoPoint3 b = vertices[k + 1];
                double length = a.DistanceTo(b);

                if (length <= near)
                {
                    continue;
                }

                GeoVector3 way = a.GetVectorTo(b).Multiply(1.0 / length);
                var gaps = new List<KeyValuePair<double, double>>();

                foreach (DxfLeaderBreak item in breaks.Where(i => i.Segment == k))
                {
                    double from = Math.Max(0.0, Math.Min(length, a.GetVectorTo(item.Start).DotProduct(way)));
                    double to = Math.Max(0.0, Math.Min(length, a.GetVectorTo(item.End).DotProduct(way)));
                    gaps.Add(new KeyValuePair<double, double>(Math.Min(from, to), Math.Max(from, to)));
                }

                gaps.Sort((x, y) => x.Key != y.Key ? x.Key.CompareTo(y.Key) : x.Value.CompareTo(y.Value));
                double at = 0.0;
                var drawn = new List<KeyValuePair<double, double>>();

                foreach (KeyValuePair<double, double> gap in gaps)
                {
                    if (gap.Key > at + near)
                    {
                        drawn.Add(new KeyValuePair<double, double>(at, gap.Key));
                    }

                    at = Math.Max(at, gap.Value);
                }

                if (at < length - near)
                {
                    drawn.Add(new KeyValuePair<double, double>(at, length));
                }

                foreach (KeyValuePair<double, double> stretch in drawn)
                {
                    GeoPoint3 end = stretch.Value >= length - near ? b : a.Add(way.Multiply(stretch.Value));

                    if (current != null && stretch.Key <= near)
                    {
                        current.Add(end);
                    }
                    else
                    {
                        if (current != null)
                        {
                            pieces.Add(current);
                        }

                        current = new List<GeoPoint3>
                        {
                            stretch.Key <= near ? a : a.Add(way.Multiply(stretch.Key)),
                            end,
                        };
                    }

                    if (stretch.Value < length - near)
                    {
                        pieces.Add(current);
                        current = null;
                    }
                }

                // A segment drawn nowhere ends the stretch before it.
                if (drawn.Count == 0 && current != null)
                {
                    pieces.Add(current);
                    current = null;
                }
            }

            if (current != null)
            {
                pieces.Add(current);
            }

            return pieces;
        }
    }
}
