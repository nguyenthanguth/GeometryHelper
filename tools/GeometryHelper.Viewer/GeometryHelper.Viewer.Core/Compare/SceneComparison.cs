using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// What became of an item between two scenes.
    /// </summary>
    public enum CompareStatus
    {
        /// <summary>The item is on both sides and its numbers are the same to the bit.</summary>
        Identical,

        /// <summary>The item is on both sides and its kind or its numbers differ.</summary>
        Changed,

        /// <summary>The item is in the left scene only.</summary>
        OnlyLeft,

        /// <summary>The item is in the right scene only.</summary>
        OnlyRight,

        /// <summary>The item is on both sides and the numbers that rebuild it are the same to the bit, but the numbers the two builds of the library gave for it differ.</summary>
        NumbersDiffer,
    }

    /// <summary>
    /// How the two copies of an item were found to be the same item.
    /// </summary>
    public enum CompareMatch
    {
        /// <summary>The item has no partner.</summary>
        None,

        /// <summary>Same layer and same name, the name being one the script gave; where several items share them, the first on the left with the first on the right, and so on.</summary>
        Name,

        /// <summary>Left over by name, on the same layer and of the same kind, and its numbers are the same to the bit on both sides.</summary>
        Data,

        /// <summary>Left over by name and by data, on the same layer and of the same kind, taken in the order they come.</summary>
        Order,
    }

    /// <summary>
    /// One row of a comparison: an item of one scene, or of both, and how the two differ.
    /// </summary>
    public sealed class ComparedItem
    {
        /// <summary>
        /// Gets the number of the row, from 1.
        /// </summary>
        public int Number { get; internal set; }

        /// <summary>
        /// Gets what became of the item.
        /// </summary>
        public CompareStatus Status { get; internal set; }

        /// <summary>
        /// Gets how the two copies were paired.
        /// </summary>
        public CompareMatch Match { get; internal set; }

        /// <summary>
        /// Gets the layer, which is the same on both sides for a paired item.
        /// </summary>
        public string Layer { get; internal set; }

        /// <summary>
        /// Gets the name of the item: the left's, or the right's where only the right has it.
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Gets the kind of the item: the left's, or the right's where only the right has it.
        /// </summary>
        public string Kind { get; internal set; }

        /// <summary>
        /// Gets the kind on the right where it is not the kind on the left, otherwise null.
        /// </summary>
        public string RightKind { get; internal set; }

        /// <summary>
        /// Gets the index of the item in the left scene, or -1.
        /// </summary>
        public int LeftIndex { get; internal set; } = -1;

        /// <summary>
        /// Gets the index of the item in the right scene, or -1.
        /// </summary>
        public int RightIndex { get; internal set; } = -1;

        /// <summary>
        /// Gets whether the item comes in another place among the paired items on the right than on the left; a paired item that moved is not a changed one.
        /// </summary>
        public bool Reordered { get; internal set; }

        /// <summary>
        /// Gets whether the numbers that rebuild the shape are the same to the bit on both sides. False for an item on one side only.
        /// </summary>
        public bool DataIdentical { get; internal set; }

        /// <summary>
        /// Gets whether the numbers have the same layout on both sides: the same kind, the same lists of the same lengths. Where they do not, the
        /// vertices were added or taken away.
        /// </summary>
        public bool SameShape { get; internal set; }

        /// <summary>
        /// Gets how many numbers differ by a bit or more; 0 where the layout differs.
        /// </summary>
        public int DifferingNumbers { get; internal set; }

        /// <summary>
        /// Gets the largest difference between two numbers of the data, where the layout is the same and some differ; otherwise NaN.
        /// </summary>
        public double MaxDelta { get; internal set; } = double.NaN;

        /// <summary>
        /// Gets the numbers the left build gave for the item, or null where it is not on the left.
        /// </summary>
        public ItemNumbers Left { get; internal set; }

        /// <summary>
        /// Gets the numbers the right build gave for the item, or null where it is not on the right.
        /// </summary>
        public ItemNumbers Right { get; internal set; }

        /// <summary>
        /// Gets the difference in words, every number to ten significant digits: "Length +5890.82, Area -3.2E-5", or what changed in the data.
        /// Empty for an identical item.
        /// </summary>
        public string Difference { get; internal set; } = string.Empty;
    }

    /// <summary>
    /// Two scenes laid side by side: every item of both, paired where it is the same item, with how the pair differs. The pairing is by layer and
    /// name first; what is left over on a layer is paired by kind in the order it comes. The comparison of the numbers is of the bits: two
    /// numbers that differ in the last place make the item changed, and the table says by how much.
    /// </summary>
    public sealed class SceneComparison
    {
        internal SceneComparison(IReadOnlyList<ComparedItem> items, IReadOnlyList<string> notes)
        {
            Items = items;
            Notes = notes;
        }

        /// <summary>
        /// Gets the rows: the items of the left scene in their order, those that are only on the right after them in theirs.
        /// </summary>
        public IReadOnlyList<ComparedItem> Items { get; }

        /// <summary>
        /// Gets what is worth saying about the two scenes as wholes, such as that they were written at different tolerances.
        /// </summary>
        public IReadOnlyList<string> Notes { get; }

        /// <summary>
        /// Gets the number of rows with a status.
        /// </summary>
        /// <param name="status">The status.</param>
        public int Count(CompareStatus status) => Items.Count(i => i.Status == status);

        /// <summary>
        /// Gets how many paired items moved to another place in the order.
        /// </summary>
        public int ReorderedCount => Items.Count(i => i.Reordered);

        /// <summary>
        /// Gets whether the two scenes hold the same items in the same numbers: nothing changed, nothing on one side only.
        /// </summary>
        public bool AreIdentical => Items.All(i => i.Status == CompareStatus.Identical);

        /// <summary>
        /// Gets one line that counts the rows by status.
        /// </summary>
        public string Summary
            => Items.Count.ToString(CultureInfo.InvariantCulture) + " item(s): "
               + Count(CompareStatus.Identical).ToString(CultureInfo.InvariantCulture) + " identical, "
               + Count(CompareStatus.Changed).ToString(CultureInfo.InvariantCulture) + " changed, "
               + Count(CompareStatus.OnlyLeft).ToString(CultureInfo.InvariantCulture) + " only left, "
               + Count(CompareStatus.OnlyRight).ToString(CultureInfo.InvariantCulture) + " only right"
               + (Count(CompareStatus.NumbersDiffer) > 0 ? ", " + Count(CompareStatus.NumbersDiffer).ToString(CultureInfo.InvariantCulture) + " with other numbers" : string.Empty)
               + (ReorderedCount > 0 ? ", " + ReorderedCount.ToString(CultureInfo.InvariantCulture) + " moved" : string.Empty);

        /// <summary>
        /// Pairs the items of two scenes and compares each pair.
        /// </summary>
        /// <param name="left">The left scene.</param>
        /// <param name="right">The right scene.</param>
        /// <returns>The comparison.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a scene is null.</exception>
        public static SceneComparison Compare(Scene left, Scene right)
        {
            if (left == null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right == null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            int[] partner = new int[left.Items.Count];
            bool[] taken = new bool[right.Items.Count];
            CompareMatch[] how = new CompareMatch[left.Items.Count];

            for (int i = 0; i < partner.Length; i++)
            {
                partner[i] = -1;
            }

            // First pass: the same layer and the same name, where the script gave the name. A name the writer made up is the kind and how many came before it, which moves
            // when an earlier layer holds one more or fewer; the first of several on the left with the first of several on the right.
            var byName = new Dictionary<string, Queue<int>>(StringComparer.Ordinal);

            for (int i = 0; i < left.Items.Count; i++)
            {
                if (!left.Items[i].AutoName)
                {
                    Enqueue(byName, left.Items[i].Layer + "\u0001" + left.Items[i].Name, i);
                }
            }

            for (int j = 0; j < right.Items.Count; j++)
            {
                if (!right.Items[j].AutoName && byName.TryGetValue(right.Items[j].Layer + "\u0001" + right.Items[j].Name, out Queue<int> queue) && queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    partner[i] = j;
                    taken[j] = true;
                    how[i] = CompareMatch.Name;
                }
            }

            // Second pass: what is left, on the same layer and of the same kind, whose numbers are the same to the bit.
            var byData = new Dictionary<string, Queue<int>>(StringComparer.Ordinal);

            for (int i = 0; i < left.Items.Count; i++)
            {
                if (partner[i] < 0)
                {
                    Enqueue(byData, left.Items[i].Layer + "\u0001" + left.Items[i].Kind + "\u0001" + left.Items[i].Data.GetRawText(), i);
                }
            }

            for (int j = 0; j < right.Items.Count; j++)
            {
                if (!taken[j] && byData.TryGetValue(right.Items[j].Layer + "\u0001" + right.Items[j].Kind + "\u0001" + right.Items[j].Data.GetRawText(), out Queue<int> queue) && queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    var same = new DataDiff();
                    same.Walk(left.Items[i].Data, right.Items[j].Data);

                    if (same.Same)
                    {
                        partner[i] = j;
                        taken[j] = true;
                        how[i] = CompareMatch.Data;
                    }

                    // Where the text was the same and the bits were not (a number written two ways) the left item stays unpaired and falls to the pass by order.
                }
            }

            // Third pass: what is left, on the same layer and of the same kind, in the order it comes.
            var byKind = new Dictionary<string, Queue<int>>(StringComparer.Ordinal);

            for (int i = 0; i < left.Items.Count; i++)
            {
                if (partner[i] < 0)
                {
                    Enqueue(byKind, left.Items[i].Layer + "\u0001" + left.Items[i].Kind, i);
                }
            }

            for (int j = 0; j < right.Items.Count; j++)
            {
                if (!taken[j] && byKind.TryGetValue(right.Items[j].Layer + "\u0001" + right.Items[j].Kind, out Queue<int> queue) && queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    partner[i] = j;
                    taken[j] = true;
                    how[i] = CompareMatch.Order;
                }
            }

            bool[] moved = Reordered(partner);
            var rows = new List<ComparedItem>();

            for (int i = 0; i < left.Items.Count; i++)
            {
                rows.Add(partner[i] < 0
                    ? OneSided(left.Items[i], i, -1, CompareStatus.OnlyLeft)
                    : Pair(left.Items[i], right.Items[partner[i]], i, partner[i], how[i], moved[i]));
            }

            for (int j = 0; j < right.Items.Count; j++)
            {
                if (!taken[j])
                {
                    rows.Add(OneSided(right.Items[j], -1, j, CompareStatus.OnlyRight));
                }
            }

            for (int n = 0; n < rows.Count; n++)
            {
                rows[n].Number = n + 1;
            }

            return new SceneComparison(rows, NotesOf(left, right));
        }

        private static void Enqueue(Dictionary<string, Queue<int>> queues, string key, int index)
        {
            if (!queues.TryGetValue(key, out Queue<int> queue))
            {
                queue = new Queue<int>();
                queues[key] = queue;
            }

            queue.Enqueue(index);
        }

        /// <summary>
        /// Flags the paired items that are out of order: the right indexes, read down the left items that have a partner, should climb, and the
        /// items outside their longest climbing run are the ones that moved.
        /// </summary>
        private static bool[] Reordered(int[] partner)
        {
            var moved = new bool[partner.Length];
            var order = new List<int>();

            for (int i = 0; i < partner.Length; i++)
            {
                if (partner[i] >= 0)
                {
                    order.Add(i);
                }
            }

            // Patience sorting: tails[k] is the position, in order, of the smallest right index that ends a climb of length k + 1.
            var tails = new List<int>();
            int[] previous = new int[order.Count];

            for (int p = 0; p < order.Count; p++)
            {
                int value = partner[order[p]];
                int low = 0;
                int high = tails.Count;

                while (low < high)
                {
                    int middle = (low + high) / 2;

                    if (partner[order[tails[middle]]] < value)
                    {
                        low = middle + 1;
                    }
                    else
                    {
                        high = middle;
                    }
                }

                previous[p] = low > 0 ? tails[low - 1] : -1;

                if (low == tails.Count)
                {
                    tails.Add(p);
                }
                else
                {
                    tails[low] = p;
                }
            }

            var onClimb = new bool[order.Count];

            for (int p = tails.Count > 0 ? tails[tails.Count - 1] : -1; p >= 0; p = previous[p])
            {
                onClimb[p] = true;
            }

            for (int p = 0; p < order.Count; p++)
            {
                moved[order[p]] = !onClimb[p];
            }

            return moved;
        }

        private static ComparedItem OneSided(SceneItem item, int leftIndex, int rightIndex, CompareStatus status)
        {
            ItemNumbers numbers = RunnerMeasures.Read(item);

            return new ComparedItem
            {
                Status = status,
                Match = CompareMatch.None,
                Layer = item.Layer,
                Name = item.Name,
                Kind = item.Kind,
                LeftIndex = leftIndex,
                RightIndex = rightIndex,
                Left = status == CompareStatus.OnlyLeft ? numbers : null,
                Right = status == CompareStatus.OnlyRight ? numbers : null,
                Difference = status == CompareStatus.OnlyLeft ? "only in the left scene" : "only in the right scene",
            };
        }

        private static ComparedItem Pair(SceneItem left, SceneItem right, int leftIndex, int rightIndex, CompareMatch match, bool moved)
        {
            bool sameKind = string.Equals(left.Kind, right.Kind, StringComparison.Ordinal);
            var diff = new DataDiff();

            if (sameKind)
            {
                diff.Walk(left.Data, right.Data);
            }
            else
            {
                diff.Same = false;
                diff.Shape = false;
            }

            // The numbers each build gave are compared too: a measuring fix in one build changes them with the data the same.
            List<string> measures = MeasureDifferences(left, right);
            CompareStatus status = !diff.Same ? CompareStatus.Changed : measures.Count > 0 ? CompareStatus.NumbersDiffer : CompareStatus.Identical;

            var row = new ComparedItem
            {
                Status = status,
                Match = match,
                Layer = left.Layer,
                Name = left.Name,
                Kind = left.Kind,
                RightKind = sameKind ? null : right.Kind,
                LeftIndex = leftIndex,
                RightIndex = rightIndex,
                Reordered = moved,
                DataIdentical = diff.Same,
                SameShape = diff.Shape,
                DifferingNumbers = diff.Shape ? diff.Differing : 0,
                MaxDelta = diff.Shape && !diff.Same ? diff.Max : double.NaN,
                Left = RunnerMeasures.Read(left),
                Right = RunnerMeasures.Read(right),
            };

            if (status != CompareStatus.Identical)
            {
                var parts = new List<string>();

                if (row.RightKind != null)
                {
                    parts.Add("kind " + row.Kind + " -> " + row.RightKind);
                }

                parts.AddRange(measures);

                if (!diff.Same && row.RightKind == null)
                {
                    parts.Add(!row.SameShape
                        ? "the numbers have a different layout (vertices added or taken away)"
                        : row.DifferingNumbers.ToString(CultureInfo.InvariantCulture) + " number(s) differ, by at most " + row.MaxDelta.ToString("G10", CultureInfo.InvariantCulture));
                }

                row.Difference = string.Join(", ", parts);
            }

            return row;
        }

        // The keys of the numbers a build gives, in the order they are told, with what each is called.
        private static readonly string[][] MeasureKeys =
        {
            new[] { "vertices", "Vertices" },
            new[] { "length", "Length" },
            new[] { "area", "Area" },
            new[] { "volume", "Volume" },
            new[] { "surfaceArea", "Surface area" },
            new[] { "closed", "Closed" },
            new[] { "note", "Measure note" },
            new[] { "error", "Measure error" },
        };

        /// <summary>
        /// Lists where the numbers the two builds gave for an item differ, to the bit: a number as a signed difference, a number only one build gave, a note
        /// that one build measured at 1E-12, and an error one build had.
        /// </summary>
        private static List<string> MeasureDifferences(SceneItem left, SceneItem right)
        {
            var told = new List<string>();

            foreach (string[] key in MeasureKeys)
            {
                left.Properties.TryGetValue(RunnerMeasures.Prefix + key[0], out string a);
                right.Properties.TryGetValue(RunnerMeasures.Prefix + key[0], out string b);

                if (string.Equals(a, b, StringComparison.Ordinal))
                {
                    continue;
                }

                bool numeric = key[0] != "closed" && key[0] != "note" && key[0] != "error";

                if (numeric && double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out double x) && double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
                {
                    if (BitConverter.DoubleToInt64Bits(x) != BitConverter.DoubleToInt64Bits(y))
                    {
                        told.Add(key[1] + " " + Signed(y - x));
                    }
                }
                else
                {
                    told.Add(key[1] + ": left " + (a == null ? "none" : "'" + a + "'") + ", right " + (b == null ? "none" : "'" + b + "'"));
                }
            }

            return told;
        }

        private static string Signed(double value) => (value >= 0.0 ? "+" : string.Empty) + value.ToString("G10", CultureInfo.InvariantCulture);

        private static IReadOnlyList<string> NotesOf(Scene left, Scene right)
        {
            var notes = new List<string>();

            if (!Same(left.EqualPoint, right.EqualPoint) || !Same(left.EqualVector, right.EqualVector) || !Same(left.EqualAngleRad, right.EqualAngleRad) || !Same(left.EqualPlanar, right.EqualPlanar))
            {
                notes.Add(
                    "the scenes were written at different tolerances: left " + Describe(left) + ", right " + Describe(right));
            }

            foreach (string kind in left.UnknownKinds.Union(right.UnknownKinds, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
            {
                notes.Add("the kind '" + kind + "' is compared by its numbers but not drawn");
            }

            return notes;
        }

        private static bool Same(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

        private static string Describe(Scene scene)
            => "point " + scene.EqualPoint.ToString("G10", CultureInfo.InvariantCulture) + ", vector " + scene.EqualVector.ToString("G10", CultureInfo.InvariantCulture)
               + ", angle " + scene.EqualAngleRad.ToString("G10", CultureInfo.InvariantCulture) + ", planar " + scene.EqualPlanar.ToString("G10", CultureInfo.InvariantCulture);

        /// <summary>
        /// Compares two <see cref="JsonElement"/>s number by number, by the bits of each number.
        /// </summary>
        private sealed class DataDiff
        {
            internal bool Same = true;
            internal bool Shape = true;
            internal int Differing;
            internal double Max;

            internal void Walk(JsonElement a, JsonElement b)
            {
                if (a.ValueKind != b.ValueKind)
                {
                    Same = false;
                    Shape = false;
                    return;
                }

                switch (a.ValueKind)
                {
                    case JsonValueKind.Number:
                    {
                        double x = a.GetDouble();
                        double y = b.GetDouble();

                        if (BitConverter.DoubleToInt64Bits(x) != BitConverter.DoubleToInt64Bits(y))
                        {
                            Same = false;
                            Differing++;
                            double delta = Math.Abs(x - y);

                            if (delta > Max)
                            {
                                Max = delta;
                            }
                        }

                        break;
                    }

                    case JsonValueKind.String:
                        if (!string.Equals(a.GetString(), b.GetString(), StringComparison.Ordinal))
                        {
                            Same = false;
                            Differing++;
                        }

                        break;

                    case JsonValueKind.Array:
                    {
                        if (a.GetArrayLength() != b.GetArrayLength())
                        {
                            Same = false;
                            Shape = false;
                            return;
                        }

                        JsonElement.ArrayEnumerator ea = a.EnumerateArray().GetEnumerator();
                        JsonElement.ArrayEnumerator eb = b.EnumerateArray().GetEnumerator();

                        while (ea.MoveNext() && eb.MoveNext())
                        {
                            Walk(ea.Current, eb.Current);
                        }

                        break;
                    }

                    case JsonValueKind.Object:
                    {
                        int count = 0;

                        foreach (JsonProperty property in a.EnumerateObject())
                        {
                            count++;

                            if (b.TryGetProperty(property.Name, out JsonElement other))
                            {
                                Walk(property.Value, other);
                            }
                            else
                            {
                                Same = false;
                                Shape = false;
                            }
                        }

                        if (count != b.EnumerateObject().Count())
                        {
                            Same = false;
                            Shape = false;
                        }

                        break;
                    }
                }
            }
        }
    }
}
