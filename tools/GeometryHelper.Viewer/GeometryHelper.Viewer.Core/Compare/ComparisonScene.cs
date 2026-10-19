using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Which side of a comparison the viewer shows.
    /// </summary>
    public enum CompareView
    {
        /// <summary>Both sides, laid over each other.</summary>
        Both,

        /// <summary>The left side alone, with what is the same on both.</summary>
        Left,

        /// <summary>The right side alone, with what is the same on both.</summary>
        Right,
    }

    /// <summary>
    /// Which scene an item of a <see cref="ComparisonScene"/> was taken from.
    /// </summary>
    public enum CompareSide
    {
        /// <summary>The item is the same on both sides; the left's copy is drawn.</summary>
        Both,

        /// <summary>The item is the left scene's.</summary>
        Left,

        /// <summary>The item is the right scene's.</summary>
        Right,
    }

    /// <summary>
    /// The two scenes of a comparison as the one scene the viewer shows: what is the same on both sides is drawn once in grey, a changed item
    /// is drawn twice, the left's red and dashed and the right's green, an item whose data is the same but whose numbers the two builds gave differently is drawn once in yellow,
    /// and an item on one side only is drawn in a colour of its own.
    /// </summary>
    public sealed class ComparisonScene
    {
        /// <summary>The colour of an item that is the same on both sides.</summary>
        public const string IdenticalColor = "#9A9A9A";

        /// <summary>The colour of the left copy of a changed item.</summary>
        public const string ChangedLeftColor = "#D62728";

        /// <summary>The colour of the right copy of a changed item.</summary>
        public const string ChangedRightColor = "#2CA02C";

        /// <summary>The colour of an item that is on the left only.</summary>
        public const string OnlyLeftColor = "#E8890C";

        /// <summary>The colour of an item that is on the right only.</summary>
        public const string OnlyRightColor = "#1F77B4";

        /// <summary>The colour of an item whose data is the same on both sides and whose numbers, as the two builds gave them, are not.</summary>
        public const string NumbersDifferColor = "#C9A400";

        /// <summary>The name of the layer of the items that are the same on both sides.</summary>
        public const string IdenticalLayer = "identical";

        /// <summary>The name of the layer of the items whose data is the same and whose numbers differ.</summary>
        public const string NumbersDifferLayer = "numbers differ";

        /// <summary>The name of the layer of the left copies of changed items.</summary>
        public const string ChangedLeftLayer = "changed - left";

        /// <summary>The name of the layer of the right copies of changed items.</summary>
        public const string ChangedRightLayer = "changed - right";

        /// <summary>The name of the layer of the items that are on the left only.</summary>
        public const string OnlyLeftLayer = "only left";

        /// <summary>The name of the layer of the items that are on the right only.</summary>
        public const string OnlyRightLayer = "only right";

        /// <summary>
        /// The most pieces the dashed lines of the left copies are cut into before they are drawn whole instead.
        /// </summary>
        public const int MaxDashes = 400000;

        private readonly List<CompareSide> _sides = new List<CompareSide>();
        private readonly List<int> _rowOf = new List<int>();
        private readonly List<List<int>> _itemsOfRow = new List<List<int>>();
        private readonly List<int> _dashed = new List<int>();

        private Scene _left;
        private Scene _right;

        private ComparisonScene(Scene scene, SceneComparison comparison)
        {
            Scene = scene;
            Comparison = comparison;
        }

        /// <summary>
        /// Gets the scene to show.
        /// </summary>
        public Scene Scene { get; }

        /// <summary>
        /// Gets the comparison it was made from.
        /// </summary>
        public SceneComparison Comparison { get; }

        /// <summary>
        /// Gets the side the n-th item of <see cref="Scene"/> was taken from.
        /// </summary>
        /// <param name="index">The index of the item in the scene.</param>
        public CompareSide SideOf(int index) => _sides[index];

        /// <summary>
        /// Gets the row of the comparison the n-th item of <see cref="Scene"/> belongs to, from 0.
        /// </summary>
        /// <param name="index">The index of the item in the scene.</param>
        public int RowOf(int index) => _rowOf[index];

        /// <summary>
        /// Gets the item a copy in <see cref="Scene"/> was made from, with the scene it is in: the item as its side's script made it, with the numbers that side's
        /// build gave, to be read at that side's tolerance. A copy of what both sides share is taken from the right, the build under test.
        /// </summary>
        /// <param name="index">The index of the item in <see cref="Scene"/>.</param>
        public KeyValuePair<Scene, SceneItem> SourceOf(int index)
        {
            ComparedItem row = Comparison.Items[_rowOf[index]];

            return _sides[index] == CompareSide.Left
                ? new KeyValuePair<Scene, SceneItem>(_left, _left.Items[row.LeftIndex])
                : new KeyValuePair<Scene, SceneItem>(_right, _right.Items[row.RightIndex]);
        }

        /// <summary>
        /// Gets the indexes in <see cref="Scene"/> of the items of a row: one for an item that is the same on both sides or on one side only,
        /// two for a changed one, the left copy first.
        /// </summary>
        /// <param name="row">The row, from 0.</param>
        public IReadOnlyList<int> ItemsOfRow(int row) => _itemsOfRow[row];

        /// <summary>
        /// Gets the indexes of the items drawn dashed: the left copies of the changed items.
        /// </summary>
        public IReadOnlyList<int> DashedItems => _dashed;

        /// <summary>
        /// Gets the indexes of the items a view hides: the right side's own items when the left is shown, the left side's own when the right is.
        /// </summary>
        /// <param name="view">The view.</param>
        public IReadOnlyList<int> HiddenFor(CompareView view)
        {
            var hidden = new List<int>();

            for (int i = 0; i < _sides.Count; i++)
            {
                if ((view == CompareView.Left && _sides[i] == CompareSide.Right) || (view == CompareView.Right && _sides[i] == CompareSide.Left))
                {
                    hidden.Add(i);
                }
            }

            return hidden;
        }

        /// <summary>
        /// Lays two scenes over each other according to their comparison.
        /// </summary>
        /// <param name="comparison">The comparison of <paramref name="left"/> and <paramref name="right"/>.</param>
        /// <param name="left">The left scene.</param>
        /// <param name="right">The right scene.</param>
        /// <returns>The scene to show. It is written at the tolerance of the left scene.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static ComparisonScene Build(SceneComparison comparison, Scene left, Scene right)
        {
            if (comparison == null)
            {
                throw new ArgumentNullException(nameof(comparison));
            }

            if (left == null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right == null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            var scene = new Scene { Title = "compare: " + (left.Title ?? "left") + " | " + (right.Title ?? "right") };
            scene.SetTolerance(left.Tolerance);
            var result = new ComparisonScene(scene, comparison) { _left = left, _right = right };

            // The layers in the order a person reads them; one that nothing is on is not made.
            var used = new HashSet<string>(StringComparer.Ordinal);
            var layers = new[]
            {
                new KeyValuePair<string, string>(IdenticalLayer, IdenticalColor),
                new KeyValuePair<string, string>(NumbersDifferLayer, NumbersDifferColor),
                new KeyValuePair<string, string>(ChangedLeftLayer, ChangedLeftColor),
                new KeyValuePair<string, string>(ChangedRightLayer, ChangedRightColor),
                new KeyValuePair<string, string>(OnlyLeftLayer, OnlyLeftColor),
                new KeyValuePair<string, string>(OnlyRightLayer, OnlyRightColor),
            };

            foreach (ComparedItem row in comparison.Items)
            {
                switch (row.Status)
                {
                    case CompareStatus.Identical: used.Add(IdenticalLayer); break;
                    case CompareStatus.NumbersDiffer: used.Add(NumbersDifferLayer); break;
                    case CompareStatus.Changed: used.Add(ChangedLeftLayer); used.Add(ChangedRightLayer); break;
                    case CompareStatus.OnlyLeft: used.Add(OnlyLeftLayer); break;
                    default: used.Add(OnlyRightLayer); break;
                }
            }

            foreach (KeyValuePair<string, string> layer in layers.Where(l => used.Contains(l.Key)))
            {
                scene.Layers.Add(new Layer { Name = layer.Key, Color = layer.Value });
            }

            for (int r = 0; r < comparison.Items.Count; r++)
            {
                ComparedItem row = comparison.Items[r];
                var indexes = new List<int>();
                result._itemsOfRow.Add(indexes);

                switch (row.Status)
                {
                    case CompareStatus.Identical:
                        result.Add(left.Items[row.LeftIndex], IdenticalLayer, CompareSide.Both, r, row, indexes);
                        break;

                    case CompareStatus.NumbersDiffer:
                        result.Add(left.Items[row.LeftIndex], NumbersDifferLayer, CompareSide.Both, r, row, indexes);
                        break;

                    case CompareStatus.Changed:
                        result._dashed.Add(scene.Items.Count);
                        result.Add(left.Items[row.LeftIndex], ChangedLeftLayer, CompareSide.Left, r, row, indexes);
                        result.Add(right.Items[row.RightIndex], ChangedRightLayer, CompareSide.Right, r, row, indexes);
                        break;

                    case CompareStatus.OnlyLeft:
                        result.Add(left.Items[row.LeftIndex], OnlyLeftLayer, CompareSide.Left, r, row, indexes);
                        break;

                    default:
                        result.Add(right.Items[row.RightIndex], OnlyRightLayer, CompareSide.Right, r, row, indexes);
                        break;
                }
            }

            return result;
        }

        private void Add(SceneItem source, string layer, CompareSide side, int rowIndex, ComparedItem row, List<int> indexes)
        {
            var item = new SceneItem
            {
                Name = source.Name,
                AutoName = source.AutoName,
                Layer = layer,
                Kind = source.Kind,
                Data = source.Data,
            };

            foreach (KeyValuePair<string, string> pair in source.Properties)
            {
                item.Properties[pair.Key] = pair.Value;
            }

            item.Properties["compare.row"] = row.Number.ToString(CultureInfo.InvariantCulture);
            item.Properties["compare.status"] = row.Status.ToString();
            item.Properties["compare.side"] = side.ToString();

            if (row.Difference.Length > 0)
            {
                item.Properties["compare.difference"] = row.Difference;
            }

            indexes.Add(Scene.Items.Count);
            _sides.Add(side);
            _rowOf.Add(rowIndex);
            Scene.Items.Add(item);
        }

        /// <summary>
        /// Cuts the lines of the dashed items into dashes, in place: a dash is a hundred and fiftieth of the diagonal of the scene and the gap
        /// is as long. Their surfaces and points are left alone.
        /// </summary>
        /// <param name="primitives">The primitives of <see cref="Scene"/>.</param>
        /// <returns>
        /// A warning when the lines would fall into more than <see cref="MaxDashes"/> pieces and were left whole, otherwise null.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the primitives are null.</exception>
        public string ApplyDashes(Primitives primitives)
        {
            if (primitives == null)
            {
                throw new ArgumentNullException(nameof(primitives));
            }

            if (primitives.Bounds.IsEmpty || _dashed.Count == 0)
            {
                return null;
            }

            double dash = primitives.Bounds.Size.Length / 150.0;

            if (!(dash > 0.0))
            {
                return null;
            }

            double total = 0.0;

            foreach (int index in _dashed)
            {
                foreach (DrawPolyline line in primitives.Items[index].Polylines)
                {
                    total += LengthOf(line);
                }
            }

            double pieces = total / dash / 2.0;

            if (pieces > MaxDashes)
            {
                return "the changed items have " + total.ToString("G6", CultureInfo.InvariantCulture) + " mm of line, " + pieces.ToString("F0", CultureInfo.InvariantCulture) + " dashes at this size of scene: they are drawn whole";
            }

            foreach (int index in _dashed)
            {
                ItemPrimitives item = primitives.Items[index];
                var cut = new List<DrawPolyline>();

                foreach (DrawPolyline line in item.Polylines)
                {
                    Dash(line, dash, cut);
                }

                item.Polylines.Clear();
                item.Polylines.AddRange(cut);
            }

            return null;
        }

        private static IReadOnlyList<Vec3> Chain(DrawPolyline line)
        {
            var points = new List<Vec3>(line.Points);

            if (line.Closed && points.Count > 1)
            {
                points.Add(points[0]);
            }

            return points;
        }

        private static double LengthOf(DrawPolyline line)
        {
            IReadOnlyList<Vec3> points = Chain(line);
            double length = 0.0;

            for (int i = 1; i < points.Count; i++)
            {
                length += (points[i] - points[i - 1]).Length;
            }

            return length;
        }

        /// <summary>
        /// Walks a chain, drawing for a dash and skipping for a gap, the walk carrying on from one piece into the next.
        /// </summary>
        private static void Dash(DrawPolyline line, double dash, List<DrawPolyline> into)
        {
            IReadOnlyList<Vec3> points = Chain(line);

            if (points.Count < 2)
            {
                into.Add(line);
                return;
            }

            bool drawing = true;
            double left = dash;
            var current = new List<Vec3> { points[0] };

            for (int i = 1; i < points.Count; i++)
            {
                Vec3 from = points[i - 1];
                Vec3 to = points[i];
                double length = (to - from).Length;
                double done = 0.0;

                while (length - done > left)
                {
                    done += left;
                    Vec3 at = from + (to - from) * (done / length);

                    if (drawing)
                    {
                        current.Add(at);
                        into.Add(new DrawPolyline(current.ToArray(), false));
                        current.Clear();
                    }
                    else
                    {
                        current.Clear();
                        current.Add(at);
                    }

                    drawing = !drawing;
                    left = dash;
                }

                left -= length - done;

                if (drawing)
                {
                    current.Add(to);
                }
            }

            if (drawing && current.Count > 1)
            {
                into.Add(new DrawPolyline(current.ToArray(), false));
            }
        }
    }
}
