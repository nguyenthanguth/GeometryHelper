using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A tiled viewport of model space, as a record of AutoCAD's VPORT table: where it lies in the drawing area, from
    /// (0, 0) at the lower left to (1, 1) at the upper right (groups 10, 20, 11 and 21), and the view it shows (its centre,
    /// 12 and 22, and its height, 40, its width the height times the aspect, 41).
    /// </summary>
    public sealed class CadTiledViewport
    {
        internal CadTiledViewport(
            int id,
            double left,
            double bottom,
            double right,
            double top,
            GeoPoint2 center,
            double height,
            double width
        )
        {
            Id = id;
            Left = left;
            Bottom = bottom;
            Right = right;
            Top = top;
            Center = center;
            Height = height;
            Width = width;
        }

        /// <summary>Gets the number of the viewport, as AutoCAD's CVPORT gives the current one: 2, 3, 4 and on.</summary>
        public int Id { get; }

        /// <summary>Gets the left edge, a share of the drawing area's width from 0 to 1.</summary>
        public double Left { get; }

        /// <summary>Gets the bottom edge, a share of the drawing area's height from 0 to 1.</summary>
        public double Bottom { get; }

        /// <summary>Gets the right edge.</summary>
        public double Right { get; }

        /// <summary>Gets the top edge.</summary>
        public double Top { get; }

        /// <summary>Gets or sets the centre of the view, in drawing units; the window sets it as the view moves.</summary>
        public GeoPoint2 Center { get; set; }

        /// <summary>Gets or sets the height of the view, in drawing units.</summary>
        public double Height { get; set; }

        /// <summary>Gets or sets the width of the view, in drawing units.</summary>
        public double Width { get; set; }

        /// <summary>Gets the corners as -VPORTS ? writes them: "0.5000,0.0000 1.0000,1.0000".</summary>
        public string Corners => F(Left) + "," + F(Bottom) + " " + F(Right) + "," + F(Top);

        internal CadTiledViewport With(int id, double left, double bottom, double right, double top) =>
            new CadTiledViewport(id, left, bottom, right, top, Center, Height, Width);

        internal CadTiledViewport Copy() => With(Id, Left, Bottom, Right, Top);

        internal bool Same(CadTiledViewport other) =>
            Math.Abs(Left - other.Left) < 1E-9
            && Math.Abs(Bottom - other.Bottom) < 1E-9
            && Math.Abs(Right - other.Right) < 1E-9
            && Math.Abs(Top - other.Top) < 1E-9;

        private static string F(double value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The tiled viewports model space is shown through, as AutoCAD's VPORTS makes them: the <c>*Active</c> records of the
    /// drawing's VPORT table, the current one first (CVPORT), and the configurations saved under a name (records of that
    /// name). Read from the drawing when first asked for; each change by a command is one undo step; the views and the
    /// tiles are written into the drawing when it is saved.
    /// </summary>
    /// <remarks>
    /// -VPORTS 2 Vertical splits the current viewport in two: the current one is the right half, the new one the left, each
    /// showing what the current one showed, the whole of its view fitted into the new shape. SIngle keeps the current
    /// viewport alone; Save and Restore keep and bring back a configuration by name.
    /// </remarks>
    public sealed class CadTiledViewports
    {
        /// <summary>The most tiles AutoCAD makes in model space, its MAXACTVP of 64.</summary>
        public const int MaxTiles = 64;

        /// <summary>The name AutoCAD keeps the tiles under while Toggle shows the current one alone.</summary>
        internal const string ToggledName = "*Multiple";

        private readonly CadDocument _document;
        private readonly List<CadTiledViewport> _tiles = new List<CadTiledViewport>();
        private readonly SortedDictionary<string, List<CadTiledViewport>> _saved = new SortedDictionary<
            string,
            List<CadTiledViewport>
        >(StringComparer.OrdinalIgnoreCase);
        private bool _changed;

        internal CadTiledViewports(CadDocument document)
        {
            _document = document;
            Read(document.Database);
        }

        /// <summary>Raised when the tiles change, or another becomes the current one.</summary>
        public event EventHandler Changed;

        /// <summary>Gets the tiles, the current one first, the others in the order they were made.</summary>
        public IReadOnlyList<CadTiledViewport> Tiles => _tiles;

        /// <summary>Gets the current tile, the one picks, zooms and commands work in.</summary>
        public CadTiledViewport Current => _tiles[0];

        /// <summary>Gets whether model space is shown through more than one tile.</summary>
        public bool IsTiled => _tiles.Count > 1;

        /// <summary>Gets the names of the configurations saved, in ordinal order, AutoCAD's own <c>*Multiple</c> left out.</summary>
        public IReadOnlyList<string> Names =>
            _saved.Keys.Where(n => !n.StartsWith("*", StringComparison.Ordinal)).ToList();

        /// <summary>
        /// Gets or sets the width of the drawing area over its height, which the window sets as it is sized: a new tile's view
        /// is fitted to its shape by it. 1.6 until the window sets it.
        /// </summary>
        public double AreaAspect { get; set; } = 1.6;

        /// <summary>
        /// Gets or sets what reads the view the window shows in the current tile into it (its centre, width and height), called
        /// before the tiles change and before they are saved, so that a new tile starts from the view as it is; null where no
        /// window shows them.
        /// </summary>
        public Action<CadTiledViewport> ReadView { get; set; }

        /// <summary>Gets the tiles of a configuration saved, the one current when it was saved first; null for none of the name.</summary>
        /// <param name="name">The name, letters of either case the same.</param>
        /// <returns>The tiles.</returns>
        public IReadOnlyList<CadTiledViewport> Saved(string name) =>
            name != null && _saved.TryGetValue(name, out List<CadTiledViewport> tiles) ? tiles : null;

        /// <summary>
        /// Makes another tile the current one, as a click in it does in AutoCAD: no undo step, as switching viewports is not.
        /// </summary>
        /// <param name="id">The number of the tile.</param>
        /// <returns>Whether there is such a tile; nothing changes for the current one.</returns>
        public bool MakeCurrent(int id)
        {
            int index = _tiles.FindIndex(t => t.Id == id);

            if (index < 0)
            {
                return false;
            }

            if (index > 0)
            {
                ReadView?.Invoke(Current);
                CadTiledViewport tile = _tiles[index];
                _tiles.RemoveAt(index);
                _tiles.Insert(0, tile);
                _changed = true;
                Changed?.Invoke(this, EventArgs.Empty);
            }

            return true;
        }

        /// <summary>
        /// Splits the current tile, or the whole drawing area, into the configuration given, as -VPORTS 2, 3 and 4 and the
        /// Viewports dialog do: <paramref name="count"/> 2 with Horizontal or Vertical, 3 with Horizontal, Vertical, Above,
        /// Below, Left or Right, 4 into four equal tiles. One undo step.
        /// </summary>
        /// <param name="count">2, 3 or 4.</param>
        /// <param name="arrangement">The arrangement keyword; ignored for 4.</param>
        /// <param name="whole">True to split the whole drawing area (Apply to: Display), the other tiles going; false to split
        /// the current tile only (Apply to: Current Viewport, as -VPORTS does).</param>
        /// <returns>Null where done, else what AutoCAD says: "The specified division would create too many viewports.".</returns>
        public string Split(int count, string arrangement, bool whole)
        {
            if (count < 2 || count > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "A viewport is split into 2, 3 or 4.");
            }

            List<double[]> pieces = Pieces(count, arrangement ?? string.Empty);

            if (pieces == null)
            {
                throw new ArgumentException("No such arrangement: " + arrangement + ".", nameof(arrangement));
            }

            return Split(pieces, whole);
        }

        /// <summary>
        /// The standard configurations of the Viewports dialog, in the order of its list: Single, Two: Vertical, Two:
        /// Horizontal, Three: Right, Three: Left, Three: Above, Three: Below, Three: Vertical, Three: Horizontal, Four: Equal,
        /// Four: Right and Four: Left.
        /// </summary>
        public static IReadOnlyList<string> StandardNames { get; } =
            Array.AsReadOnly(
                new[]
                {
                    "Single",
                    "Two: Vertical",
                    "Two: Horizontal",
                    "Three: Right",
                    "Three: Left",
                    "Three: Above",
                    "Three: Below",
                    "Three: Vertical",
                    "Three: Horizontal",
                    "Four: Equal",
                    "Four: Right",
                    "Four: Left",
                }
            );

        /// <summary>
        /// Gets the tiles of a standard configuration as shares of the area split, (0, 0) to (1, 1), the one that stays current
        /// first, for the dialog's preview; null for a name of none.
        /// </summary>
        /// <param name="name">A name of <see cref="StandardNames"/>.</param>
        /// <returns>The boxes.</returns>
        public static IReadOnlyList<DisplayBox> PiecesOf(string name)
        {
            List<double[]> pieces = name == "Single" ? Rows(new[] { 0.0, 0.0, 1.0, 1.0 }) : StandardPieces(name);
            return pieces?.Select(p => new DisplayBox(p[0], p[1], p[2], p[3])).ToList();
        }

        /// <summary>
        /// Applies a standard configuration of the Viewports dialog to the whole drawing area (Apply to: Display) or to the
        /// current tile (Apply to: Current Viewport); Single with Display keeps the current tile alone, with Current Viewport
        /// changes nothing. One undo step.
        /// </summary>
        /// <param name="name">A name of <see cref="StandardNames"/>.</param>
        /// <param name="whole">Whether the whole drawing area is split.</param>
        /// <returns>Null where done or nothing to do, else what AutoCAD says.</returns>
        /// <exception cref="ArgumentException">Thrown when the name is none of <see cref="StandardNames"/>.</exception>
        public string SplitAs(string name, bool whole)
        {
            if (name == "Single")
            {
                if (whole)
                {
                    Single();
                }

                return null;
            }

            List<double[]> pieces = StandardPieces(name);

            if (pieces == null)
            {
                throw new ArgumentException("No standard configuration " + name + ".", nameof(name));
            }

            return Split(pieces, whole);
        }

        private static List<double[]> StandardPieces(string name)
        {
            string[] words = (name ?? string.Empty).Split(new[] { ": " }, StringSplitOptions.None);

            if (words.Length != 2)
            {
                return null;
            }

            int count =
                words[0] == "Two" ? 2
                : words[0] == "Three" ? 3
                : words[0] == "Four" ? 4
                : 0;
            return count == 0 ? null : Pieces(count, words[1] == "Equal" ? string.Empty : words[1]);
        }

        private string Split(List<double[]> pieces, bool whole)
        {
            ReadView?.Invoke(Current);
            int kept = whole ? 0 : _tiles.Count - 1;

            if (kept + pieces.Count > MaxTiles)
            {
                return "The specified division would create too many viewports.";
            }

            CadTiledViewport current = Current;
            double l = whole ? 0.0 : current.Left;
            double b = whole ? 0.0 : current.Bottom;
            double w = (whole ? 1.0 : current.Right) - l;
            double h = (whole ? 1.0 : current.Top) - b;

            Change(() =>
            {
                List<CadTiledViewport> others = whole ? new List<CadTiledViewport>() : _tiles.Skip(1).ToList();
                var made = new List<CadTiledViewport>();
                var used = new HashSet<int>(others.Select(t => t.Id)) { current.Id };

                for (int i = 0; i < pieces.Count; i++)
                {
                    double[] p = pieces[i];
                    int id = i == 0 ? current.Id : Free(used);
                    used.Add(id);
                    CadTiledViewport tile = current.With(id, l + p[0] * w, b + p[1] * h, l + p[2] * w, b + p[3] * h);
                    Fit(tile, current);
                    made.Add(tile);
                }

                _tiles.Clear();
                _tiles.Add(made[0]);

                // The current tile first, then those there were, then the new ones, as AutoCAD lists them.
                _tiles.AddRange(others);
                _tiles.AddRange(made.Skip(1));
            });
            return null;
        }

        /// <summary>Shows model space through the current tile alone, as -VPORTS SIngle; nothing where it is alone already. One undo step.</summary>
        /// <returns>Whether anything changed.</returns>
        public bool Single()
        {
            if (!IsTiled)
            {
                return false;
            }

            ReadView?.Invoke(Current);
            Change(() => KeepCurrentAlone());
            return true;
        }

        /// <summary>
        /// Toggle of -VPORTS: the current tile alone, the others kept under <c>*Multiple</c>, where there are several; the
        /// tiles kept brought back where it is alone and some are kept. One undo step.
        /// </summary>
        /// <returns>Whether anything changed.</returns>
        public bool Toggle()
        {
            ReadView?.Invoke(Current);

            if (IsTiled)
            {
                Change(() =>
                {
                    _saved[ToggledName] = _tiles.Select(t => t.Copy()).ToList();
                    KeepCurrentAlone();
                });
                return true;
            }

            if (!_saved.ContainsKey(ToggledName))
            {
                return false;
            }

            Change(() => Bring(_saved[ToggledName]));
            return true;
        }

        /// <summary>Saves the tiles under a name, replacing a configuration of that name, as -VPORTS Save. One undo step.</summary>
        /// <param name="name">The name.</param>
        /// <exception cref="ArgumentException">Thrown when the name is empty.</exception>
        public void Save(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A configuration is saved under a name.", nameof(name));
            }

            ReadView?.Invoke(Current);
            Change(() => _saved[name.Trim()] = _tiles.Select(t => t.Copy()).ToList());
        }

        /// <summary>Brings back a configuration saved, the views saved in it, its first tile current, as -VPORTS Restore. One undo step.</summary>
        /// <param name="name">The name.</param>
        /// <returns>Whether a configuration of the name was there.</returns>
        public bool Restore(string name)
        {
            IReadOnlyList<CadTiledViewport> saved = Saved(name);

            if (saved == null)
            {
                return false;
            }

            Change(() => Bring(saved));
            return true;
        }

        /// <summary>Deletes the configurations whose names match a pattern (wild cards and commas taken), as -VPORTS Delete. One undo step.</summary>
        /// <param name="pattern">The pattern.</param>
        /// <returns>How many were deleted.</returns>
        public int Delete(string pattern)
        {
            List<string> names = Names.Where(n => CadWildcard.IsMatch(n, pattern ?? string.Empty, true)).ToList();

            if (names.Count > 0)
            {
                Change(() => names.ForEach(n => _saved.Remove(n)));
            }

            return names.Count;
        }

        /// <summary>
        /// Joins two tiles into one, as -VPORTS Join: the dominant one keeps its number and its view and takes the place of
        /// both. One undo step.
        /// </summary>
        /// <param name="dominant">The number of the tile that stays.</param>
        /// <param name="other">The number of the tile joined to it.</param>
        /// <returns>Null where joined, else what AutoCAD says: "The selected viewports must be distinct." or "The selected
        /// viewports do not form a rectangle.".</returns>
        public string Join(int dominant, int other)
        {
            ReadView?.Invoke(Current);
            CadTiledViewport a = _tiles.Find(t => t.Id == dominant);
            CadTiledViewport b = _tiles.Find(t => t.Id == other);

            if (a == null || b == null || a == b)
            {
                return "The selected viewports must be distinct.";
            }

            bool sideBySide =
                Near(a.Bottom, b.Bottom) && Near(a.Top, b.Top) && (Near(a.Right, b.Left) || Near(b.Right, a.Left));
            bool stacked =
                Near(a.Left, b.Left) && Near(a.Right, b.Right) && (Near(a.Top, b.Bottom) || Near(b.Top, a.Bottom));

            if (!sideBySide && !stacked)
            {
                return "The selected viewports do not form a rectangle.";
            }

            Change(() =>
            {
                CadTiledViewport joined = a.With(
                    a.Id,
                    Math.Min(a.Left, b.Left),
                    Math.Min(a.Bottom, b.Bottom),
                    Math.Max(a.Right, b.Right),
                    Math.Max(a.Top, b.Top)
                );
                Fit(joined, a);
                int at = _tiles.IndexOf(a);
                _tiles[at] = joined;
                _tiles.Remove(b);

                if (at != 0)
                {
                    _tiles.Remove(joined);
                    _tiles.Insert(0, joined);
                }
            });
            return null;
        }

        /// <summary>Writes "Current configuration:" and the configurations matching a pattern, as -VPORTS ? does.</summary>
        /// <param name="pattern">The names to list; "*" for all.</param>
        /// <returns>The lines.</returns>
        public IReadOnlyList<string> List(string pattern)
        {
            var lines = new List<string> { "Current configuration:" };

            foreach (CadTiledViewport tile in _tiles)
            {
                lines.Add("id# " + tile.Id.ToString(CultureInfo.InvariantCulture));
                lines.Add("   corners: " + tile.Corners);
            }

            foreach (string name in Names.Where(n => CadWildcard.IsMatch(n, pattern ?? "*", true)))
            {
                lines.Add("Configuration \"" + name + "\":");
                lines.AddRange(_saved[name].Select(t => "    " + t.Corners));
            }

            return lines;
        }

        /// <summary>The tiles and configurations as text, for an undo step to put back.</summary>
        internal string State()
        {
            var text = new StringBuilder();
            Write(text, string.Empty, _tiles);

            foreach (KeyValuePair<string, List<CadTiledViewport>> pair in _saved)
            {
                Write(text, pair.Key, pair.Value);
            }

            return text.ToString();
        }

        /// <summary>Puts back the tiles and configurations <see cref="State"/> wrote.</summary>
        internal void Load(string state)
        {
            _tiles.Clear();
            _saved.Clear();

            foreach (string line in state.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] f = line.Split('|');
                double D(int i) => double.Parse(f[i], CultureInfo.InvariantCulture);
                var tile = new CadTiledViewport(
                    int.Parse(f[1], CultureInfo.InvariantCulture),
                    D(2),
                    D(3),
                    D(4),
                    D(5),
                    new GeoPoint2(D(6), D(7)),
                    D(8),
                    D(9)
                );

                if (f[0].Length == 0)
                {
                    _tiles.Add(tile);
                }
                else
                {
                    if (!_saved.TryGetValue(f[0], out List<CadTiledViewport> list))
                    {
                        _saved[f[0]] = list = new List<CadTiledViewport>();
                    }

                    list.Add(tile);
                }
            }

            _changed = true;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Writes the tiles and the configurations into the drawing's VPORT table where they changed since it was read, as
        /// AutoCAD saves them: the current tile into the first <c>*Active</c> record, which keeps the snap and grid, the others
        /// and each configuration as copies of it with their own corners and views.
        /// </summary>
        internal void WriteTo(DxfDatabase database)
        {
            if (IsTiled && ReadView != null)
            {
                ReadView(Current);
                _changed = true;
            }

            if (!_changed)
            {
                return;
            }

            DxfFile file = database.File;
            List<DxfItem> records = file.TableItems("VPORT").Where(i => !i.IsRemoved).ToList();
            DxfItem first = records.FirstOrDefault(i =>
                string.Equals(file.ValueOf(i, 2)?.Trim(), "*Active", StringComparison.OrdinalIgnoreCase)
            );

            if (first == null)
            {
                return;
            }

            foreach (DxfItem item in records)
            {
                if (item != first)
                {
                    file.RemoveTableRecord(item, "VPORT");
                }
            }

            IReadOnlyList<DxfTypedValue> template = file.GroupsOf(first);
            file.SetValues(first, Values(Current, null), Order, false);

            foreach (CadTiledViewport tile in _tiles.Skip(1))
            {
                Add(file, template, "*Active", tile);
            }

            foreach (KeyValuePair<string, List<CadTiledViewport>> pair in _saved)
            {
                foreach (CadTiledViewport tile in pair.Value)
                {
                    Add(file, template, pair.Key, tile);
                }
            }

            _changed = false;
        }

        // The groups of AcDbViewportTableRecord the tiles write, in the order AutoCAD 2021 gives them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(
            new[]
            {
                100,
                100,
                2,
                70,
                10,
                20,
                11,
                21,
                12,
                22,
                13,
                23,
                14,
                24,
                15,
                25,
                16,
                26,
                36,
                17,
                27,
                37,
                40,
                41,
                42,
            }
        );

        private static void Add(DxfFile file, IReadOnlyList<DxfTypedValue> template, string name, CadTiledViewport tile)
        {
            Dictionary<int, string> values = Values(tile, name).ToDictionary(p => p.Key, p => p.Value);
            file.AddTableRecord("VPORT", "AcDbViewportTableRecord", b =>
            {
                foreach (DxfTypedValue group in template)
                {
                    int code = group.TypeCode;

                    if (
                        code == 0
                        || code == 5
                        || code == 100
                        || code == 102
                        || code == 330
                        || code == 360
                        || code >= 1000
                    )
                    {
                        continue;
                    }

                    b.Group(code, values.TryGetValue(code, out string value) ? value : group.Value);
                }
            });
        }

        private static List<KeyValuePair<int, string>> Values(CadTiledViewport tile, string name)
        {
            var values = new List<KeyValuePair<int, string>>();

            if (name != null)
            {
                values.Add(new KeyValuePair<int, string>(2, name));
            }

            void Put(int code, double value) =>
                values.Add(new KeyValuePair<int, string>(code, Numbers.Exact(value + 0.0)));

            Put(10, tile.Left);
            Put(20, tile.Bottom);
            Put(11, tile.Right);
            Put(21, tile.Top);
            Put(12, tile.Center.X);
            Put(22, tile.Center.Y);
            Put(40, tile.Height);
            Put(41, tile.Height > 0.0 ? tile.Width / tile.Height : 1.0);
            return values;
        }

        private void Read(DxfDatabase database)
        {
            DxfFile file = database.File;

            foreach (DxfItem item in file.TableItems("VPORT").Where(i => !i.IsRemoved))
            {
                string name = file.ValueOf(item, 2)?.Trim() ?? string.Empty;
                double R(int code, double fallback) =>
                    Numbers.TryRead(file.ValueOf(item, code), out double v) && Guard.IsFinite(v) ? v : fallback;
                double height = R(40, 10.0);
                double width = height * R(41, 1.0);
                bool active = string.Equals(name, "*Active", StringComparison.OrdinalIgnoreCase);
                List<CadTiledViewport> list = active ? _tiles : null;

                if (!active && name.Length > 0)
                {
                    if (!_saved.TryGetValue(name, out list))
                    {
                        _saved[name] = list = new List<CadTiledViewport>();
                    }
                }

                if (list == null)
                {
                    continue;
                }

                int id = active ? _tiles.Count + 2 : 0;
                list.Add(
                    new CadTiledViewport(
                        id,
                        R(10, 0.0),
                        R(20, 0.0),
                        R(11, 1.0),
                        R(21, 1.0),
                        new GeoPoint2(R(12, 0.0), R(22, 0.0)),
                        height,
                        width
                    )
                );
            }

            if (_tiles.Count == 0)
            {
                _tiles.Add(new CadTiledViewport(2, 0.0, 0.0, 1.0, 1.0, GeoPoint2.Origin, 10.0, 10.0));
            }
        }

        private void Change(Action change)
        {
            string before = State();
            string after = null;
            _document.ApplySetting(
                "VPORTS",
                "VPORTS",
                State,
                () =>
                {
                    change();
                    _changed = true;
                    after = State();
                },
                (document, forward) => document.TiledViewports.Load(forward ? after : before),
                false
            );
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void KeepCurrentAlone()
        {
            CadTiledViewport current = Current;
            CadTiledViewport alone = current.With(current.Id, 0.0, 0.0, 1.0, 1.0);
            Fit(alone, current);
            _tiles.Clear();
            _tiles.Add(alone);
        }

        private void Bring(IReadOnlyList<CadTiledViewport> saved)
        {
            _tiles.Clear();
            var used = new HashSet<int>();

            foreach (CadTiledViewport tile in saved)
            {
                int id = tile.Id >= 2 && used.Add(tile.Id) ? tile.Id : Free(used);
                used.Add(id);
                _tiles.Add(tile.With(id, tile.Left, tile.Bottom, tile.Right, tile.Top));
            }
        }

        /// <summary>Fits the view a tile showed into the shape of the tile made from it, its centre kept, as AutoCAD does.</summary>
        private void Fit(CadTiledViewport tile, CadTiledViewport from)
        {
            double aspect = (tile.Right - tile.Left) * AreaAspect / Math.Max(1E-9, tile.Top - tile.Bottom);
            double width = from.Width;
            double height = from.Height;

            if (width > height * aspect)
            {
                height = width / aspect;
            }
            else
            {
                width = height * aspect;
            }

            tile.Center = from.Center;
            tile.Width = width;
            tile.Height = height;
        }

        private static int Free(HashSet<int> used)
        {
            int id = 2;

            while (used.Contains(id))
            {
                id++;
            }

            return id;
        }

        private static bool Near(double a, double b) => Math.Abs(a - b) < 1E-9;

        private static void Write(StringBuilder text, string name, IEnumerable<CadTiledViewport> tiles)
        {
            foreach (CadTiledViewport t in tiles)
            {
                text.Append(name)
                    .Append('|')
                    .Append(t.Id.ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(
                        string.Join(
                            "|",
                            new[]
                            {
                                t.Left,
                                t.Bottom,
                                t.Right,
                                t.Top,
                                t.Center.X,
                                t.Center.Y,
                                t.Height,
                                t.Width,
                            }.Select(v => v.ToString("R", CultureInfo.InvariantCulture))
                        )
                    )
                    .Append('\n');
            }
        }

        /// <summary>The pieces of a split, as shares of the tile split (left, bottom, right, top), the one that stays current first and the new ones in the order AutoCAD 2021 numbers them.</summary>
        private static List<double[]> Pieces(int count, string arrangement)
        {
            const double Third = 1.0 / 3.0;
            string a = arrangement.ToUpperInvariant();

            switch (count)
            {
                case 2:
                    return a == "HORIZONTAL" ? Rows(new[] { 0.0, 0.5, 1.0, 1.0 }, new[] { 0.0, 0.0, 1.0, 0.5 })
                        : a == "VERTICAL" ? Rows(new[] { 0.5, 0.0, 1.0, 1.0 }, new[] { 0.0, 0.0, 0.5, 1.0 })
                        : null;
                case 3:
                    switch (a)
                    {
                        case "HORIZONTAL":
                            return Rows(
                                new[] { 0.0, 2 * Third, 1.0, 1.0 },
                                new[] { 0.0, 0.0, 1.0, Third },
                                new[] { 0.0, Third, 1.0, 2 * Third }
                            );
                        case "VERTICAL":
                            return Rows(
                                new[] { 2 * Third, 0.0, 1.0, 1.0 },
                                new[] { 0.0, 0.0, Third, 1.0 },
                                new[] { Third, 0.0, 2 * Third, 1.0 }
                            );
                        case "ABOVE":
                            return Rows(
                                new[] { 0.0, 0.5, 1.0, 1.0 },
                                new[] { 0.0, 0.0, 0.5, 0.5 },
                                new[] { 0.5, 0.0, 1.0, 0.5 }
                            );
                        case "BELOW":
                            return Rows(
                                new[] { 0.0, 0.0, 1.0, 0.5 },
                                new[] { 0.5, 0.5, 1.0, 1.0 },
                                new[] { 0.0, 0.5, 0.5, 1.0 }
                            );
                        case "LEFT":
                            return Rows(
                                new[] { 0.0, 0.0, 0.5, 1.0 },
                                new[] { 0.5, 0.5, 1.0, 1.0 },
                                new[] { 0.5, 0.0, 1.0, 0.5 }
                            );
                        case "RIGHT":
                            return Rows(
                                new[] { 0.5, 0.0, 1.0, 1.0 },
                                new[] { 0.0, 0.5, 0.5, 1.0 },
                                new[] { 0.0, 0.0, 0.5, 0.5 }
                            );
                        default:
                            return null;
                    }
                default:
                    // Four: Right and Four: Left of the dialog: the big half current, the other half in three, top first.
                    if (a == "RIGHT" || a == "LEFT")
                    {
                        double x0 = a == "RIGHT" ? 0.0 : 0.5;
                        double big = a == "RIGHT" ? 0.5 : 0.0;
                        return Rows(
                            new[] { big, 0.0, big + 0.5, 1.0 },
                            new[] { x0, 2 * Third, x0 + 0.5, 1.0 },
                            new[] { x0, Third, x0 + 0.5, 2 * Third },
                            new[] { x0, 0.0, x0 + 0.5, Third }
                        );
                    }

                    return a.Length > 0
                        ? null
                        : Rows(
                            new[] { 0.5, 0.0, 1.0, 0.5 },
                            new[] { 0.5, 0.5, 1.0, 1.0 },
                            new[] { 0.0, 0.5, 0.5, 1.0 },
                            new[] { 0.0, 0.0, 0.5, 0.5 }
                        );
            }
        }

        private static List<double[]> Rows(params double[][] rows) => new List<double[]>(rows);
    }
}
