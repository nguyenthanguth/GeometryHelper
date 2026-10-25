using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    public static partial class DxfReader
    {
        private sealed partial class Reading
        {
            private const int DeepestNesting = 64;
            private const long MostCells = 1048576;
            private const string Flattened = "flattened onto a line or a point by the INSERT that places it";

            private static readonly IReadOnlyList<string> NoNames = Array.AsReadOnly(new string[0]);

            private readonly List<Block> _blocks = new List<Block>();
            private readonly Dictionary<string, Block> _blockNames = new Dictionary<string, Block>(StringComparer.OrdinalIgnoreCase);
            private readonly List<KeyValuePair<int, Record>> _pending = new List<KeyValuePair<int, Record>>();
            private readonly List<Block> _active = new List<Block>();
            private readonly List<string> _missingNames = new List<string>();
            private readonly List<string> _againNames = new List<string>();
            private List<DxfShape> _into;
            private Block _block;
            private Placement _place;
            private Look? _inherit;
            private IReadOnlyList<string> _path = NoNames;
            private IReadOnlyList<string> _handles = NoNames;
            private int _missingInserts;
            private int _selfInserts;
            private string _firstLoop;
            private int _deepInserts;
            private int _xrefInserts;
            private int _layoutEntities;
            private int _hugeArrays;
            private readonly Budget _budget;
            private readonly Sizing _sizing;
            private readonly bool _forBlocks;
            private readonly bool _unsized;
            private bool _approved;
            private int _treeStart;
            private bool _stopped;

            /// <summary>
            /// A reading of the blocks of a drawing on their own, for <see cref="DxfDocument.Blocks"/>: the options, the layers,
            /// the blocks and the scale of the drawing's, its own entities and its own counts, which the drawing does not take.
            /// </summary>
            private Reading(Reading drawing)
            {
                _planAs2D = drawing._planAs2D;
                _toMillimetres = drawing._toMillimetres;
                _tolerance = drawing._tolerance;
                _keepGroups = drawing._keepGroups;
                _expandBlocks = drawing._expandBlocks;
                _interned = drawing._interned;
                _layers = drawing._layers;
                _layerNames = drawing._layerNames;
                _blocks = drawing._blocks;
                _blockNames = drawing._blockNames;
                _scale = drawing._scale;
                _unitCode = drawing._unitCode;
                _budget = drawing._budget;
                _sizing = drawing._sizing;
                _unsized = drawing._unsized;
                _forBlocks = true;
                _into = _entities;
            }

            #region The blocks section

            /// <summary>
            /// A block of the file: its BLOCK record, which holds its name, flags and base point, and its records up to its
            /// ENDBLK, each whole.
            /// </summary>
            private sealed class Block
            {
                internal Block(string name, Record header)
                {
                    Name = name;
                    Header = header;
                    Flags = header.Int(70, 0);
                    Gathering = new Gathering(Items.Add);
                    IsLayout = name.StartsWith("*Model_Space", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("*Paper_Space", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("$Model_Space", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("$Paper_Space", StringComparison.OrdinalIgnoreCase);
                }

                internal string Name { get; }

                internal Record Header { get; }

                internal int Flags { get; }

                internal List<Record> Items { get; } = new List<Record>();

                internal Gathering Gathering { get; }

                /// <summary>
                /// At most how many entities the block places, its INSERTs counted through; -1 until worked out, -2 while it is.
                /// </summary>
                internal long Size { get; set; } = -1;

                /// <summary>
                /// The size worked out for an INSERT of the block where the walk met a block inside itself, or was stopped at the
                /// bound on the blocks a reading works out, and <see cref="Size"/> was not kept; -1 until worked out. It holds for
                /// every INSERT of the block: the walk is the same each time, and one stopped would stop again, the bound being
                /// spent.
                /// </summary>
                internal long RootSize { get; set; } = -1;

                /// <summary>Whether <see cref="RootSize"/> is that of a walk stopped at the bound, the size not known.</summary>
                internal bool RootStopped { get; set; }

                /// <summary>Whether the block is that of a layout, model space or a sheet of paper space, which no INSERT places.</summary>
                internal bool IsLayout { get; }

                /// <summary>Whether the block is an external reference (flag 4), whose drawing is another file.</summary>
                internal bool IsExternal => (Flags & 4) != 0;
            }

            /// <summary>
            /// Takes a record of the BLOCKS section: a BLOCK opens a block, which its records belong to until its ENDBLK. A
            /// block of a name already defined is read and left out, the first kept, as AutoCAD keeps names one to a block.
            /// </summary>
            private void BlockRecord(Record r)
            {
                if (r.Type == "BLOCK")
                {
                    EndOfBlock();
                    string name = Decode((r.Text(2) ?? r.Text(3) ?? string.Empty).Trim());
                    _block = new Block(name, r);

                    if (_blockNames.ContainsKey(name))
                    {
                        _againNames.Add(name);
                    }
                    else
                    {
                        _blockNames.Add(name, _block);
                        _blocks.Add(_block);
                    }

                    return;
                }

                if (r.Type == "ENDBLK")
                {
                    EndOfBlock();
                    return;
                }

                _block?.Gathering.Add(r);
            }

            private void EndOfBlock()
            {
                _block?.Gathering.Close();
                _block = null;
            }

            #endregion

            #region Placing

            /// <summary>
            /// Where an INSERT puts the entities of its block: the affine map from the block's coordinates to the world's, a
            /// matrix of three rows by four, the last column the shift. Null stands for the identity, so an INSERT that puts
            /// its block where it is reads the block's entities as the drawing's own are read, to the bit.
            /// </summary>
            private sealed class Placement
            {
                private readonly double[] _m;

                private Placement(double[] m)
                {
                    _m = m;
                    Mirrors = m[0] * (m[5] * m[10] - m[6] * m[9]) - m[1] * (m[4] * m[10] - m[6] * m[8]) + m[2] * (m[4] * m[9] - m[5] * m[8]) < 0.0;
                }

                /// <summary>Whether the placement turns space over, as a mirror does: the determinant of its linear part below nought.</summary>
                internal bool Mirrors { get; }

                /// <summary>
                /// The placement of a map after another, outer one; null where the two together leave every point where it is,
                /// to the bit.
                /// </summary>
                internal static Placement Then(Placement outer, double[] m)
                {
                    double[] c = m;

                    if (outer != null)
                    {
                        double[] o = outer._m;
                        c = new double[12];

                        for (int row = 0; row < 3; row++)
                        {
                            for (int column = 0; column < 4; column++)
                            {
                                double sum = o[row * 4] * m[column] + o[row * 4 + 1] * m[4 + column] + o[row * 4 + 2] * m[8 + column];
                                c[row * 4 + column] = column == 3 ? sum + o[row * 4 + 3] : sum;
                            }
                        }
                    }

                    for (int i = 0; i < 12; i++)
                    {
                        if (double.IsNaN(c[i]) || double.IsInfinity(c[i]))
                        {
                            throw new NotFinite();
                        }
                    }

                    bool identity = c[0] == 1.0 && c[1] == 0.0 && c[2] == 0.0 && c[3] == 0.0
                        && c[4] == 0.0 && c[5] == 1.0 && c[6] == 0.0 && c[7] == 0.0
                        && c[8] == 0.0 && c[9] == 0.0 && c[10] == 1.0 && c[11] == 0.0;

                    return identity ? null : new Placement(c);
                }

                /// <summary>
                /// The point placed. One the placement takes past the largest double leaves its entity out, as one with a number
                /// that is not finite.
                /// </summary>
                internal GeoPoint3 Apply(GeoPoint3 p)
                {
                    double[] m = _m;
                    var placed = new GeoPoint3(
                        m[0] * p.X + m[1] * p.Y + m[2] * p.Z + m[3],
                        m[4] * p.X + m[5] * p.Y + m[6] * p.Z + m[7],
                        m[8] * p.X + m[9] * p.Y + m[10] * p.Z + m[11]);

                    if (!IsFinite(placed))
                    {
                        throw new NotFinite();
                    }

                    return placed;
                }

                /// <summary>A direction placed, the shift left out.</summary>
                internal GeoVector3 Linear(GeoVector3 v)
                {
                    double[] m = _m;
                    return new GeoVector3(m[0] * v.X + m[1] * v.Y + m[2] * v.Z, m[4] * v.X + m[5] * v.Y + m[6] * v.Z, m[8] * v.X + m[9] * v.Y + m[10] * v.Z);
                }
            }

            /// <summary>A point of the block placed where the INSERTs being read put it; as it is in the drawing itself.</summary>
            private GeoPoint3 Place(GeoPoint3 p) => _place == null ? p : _place.Apply(p);

            /// <summary>
            /// The map of an entity's own plane to the world: the system of its extrusion, then the placement of the INSERTs
            /// it is read through. Its axes are the images of the plane's x and y, square and of one length under a turn, an
            /// even scaling and a mirror, and its normal their cross product made unit, so it follows a mirror: an arc that
            /// runs counter-clockwise about it is the image of the arc of the block, running the way the image runs.
            /// </summary>
            private readonly struct Frame
            {
                private readonly Ocs _ocs;
                private readonly Placement _place;

                internal Frame(Ocs ocs, Placement place)
                {
                    _ocs = ocs;
                    _place = place;
                    U = place.Linear(ocs.U);
                    V = place.Linear(ocs.V);

                    GeoVector3 n = U.CrossProduct(V);
                    double length = n.Length;

                    // A placement that keeps the floor the floor gives its normal straight up or down, to the bit.
                    Collapsed = !(length > 1E-12 * U.Length * V.Length) || double.IsInfinity(length);
                    Normal = Collapsed
                        ? default(GeoVector3)
                        : n.X == 0.0 && n.Y == 0.0 ? new GeoVector3(0.0, 0.0, n.Z > 0.0 ? 1.0 : -1.0) : new GeoVector3(n.X / length, n.Y / length, n.Z / length);
                }

                /// <summary>The image of the plane's x axis.</summary>
                internal GeoVector3 U { get; }

                /// <summary>The image of the plane's y axis.</summary>
                internal GeoVector3 V { get; }

                /// <summary>The normal of the placed plane, of unit length; nought where the placement flattens the plane.</summary>
                internal GeoVector3 Normal { get; }

                /// <summary>Whether the placement flattens the plane onto a line or a point.</summary>
                internal bool Collapsed { get; }

                /// <summary>Whether the placed plane is the floor, its normal straight up or down and its axes level, to the bit.</summary>
                internal bool OnFloor => !Collapsed && Normal.X == 0.0 && Normal.Y == 0.0 && U.Z == 0.0 && V.Z == 0.0;

                internal GeoPoint3 World(GeoPoint3 p) => _place.Apply(_ocs.World(p));

                /// <summary>
                /// Whether a circle of a radius in the plane is placed as a circle: where its axes come out so nearly one length
                /// and square that the circle placed whole lands within the point tolerance of where the placement puts each of
                /// its points, by the rule of <see cref="GeoTransform3.Transform(GeoCircle3)"/>, or within 1E-12 of its size,
                /// as a turn rounds.
                /// </summary>
                internal bool Round(double radius, Tolerance tolerance)
                {
                    double alongU = U.Length;
                    double alongV = V.Length;
                    double gap = GeoTransform2.RoundGap(alongU, alongV, U.DotProduct(V), radius);

                    return !Collapsed && (gap <= tolerance.EqualPoint || gap <= 1E-12 * radius * Math.Max(alongU, alongV));
                }
            }

            /// <summary>
            /// Reads an INSERT: the entities of its block placed by it, each cell of a MINSERT's array in turn, the rows one
            /// after another, as ezdxf takes them. One that cannot be placed is counted in <see cref="DxfDocument.Skipped"/>
            /// and the rest read: one of a block the file does not define, of an external reference or of a layout, of a block
            /// inside itself, at once or through others, and one nested more than 64 deep.
            /// </summary>
            private void Expand(Record insert)
            {
                if (!_expandBlocks)
                {
                    Skip(insert.Type, "not placed, as DxfReadOptions.ExpandBlocks asks");
                    return;
                }

                string name = Decode((insert.Text(2) ?? string.Empty).Trim());

                if (name.Length == 0 || !_blockNames.TryGetValue(name, out Block block))
                {
                    Skip(insert.Type, "of a block the file does not define");
                    _missingInserts++;

                    if (!_missingNames.Contains(name))
                    {
                        _missingNames.Add(name);
                    }

                    return;
                }

                if (block.IsLayout)
                {
                    Skip(insert.Type, "of the block of a layout");
                    return;
                }

                if (block.IsExternal)
                {
                    Skip(insert.Type, "of an external reference");
                    _xrefInserts++;
                    return;
                }

                if (_active.Contains(block))
                {
                    Skip(insert.Type, "of a block inside itself");
                    _selfInserts++;

                    if (_firstLoop == null)
                    {
                        var names = _active.ConvertAll(b => b.Name);
                        names.Add(block.Name);
                        _firstLoop = string.Join(" > ", names);
                    }

                    return;
                }

                if (_active.Count >= DeepestNesting)
                {
                    Skip(insert.Type, "nested more than 64 deep");
                    _deepInserts++;
                    return;
                }

                List<Placement> cells;
                bool counts = !_approved;

                try
                {
                    if (insert.Unreadable() != null || block.Header.Unreadable() != null)
                    {
                        throw new NotFinite();
                    }

                    // An INSERT placed inside one already let through is counted in it.
                    if (counts)
                    {
                        bool stopped = false;
                        long need = _unsized ? 0 : Times(CellCount(insert), RootSizeOf(block, out stopped));

                        if (need > _budget.Left && stopped)
                        {
                            Skip(insert.Type, "whose size was not worked out, the reading having worked out as many blocks as it works out");
                            _sizing.Refuse(_forBlocks);
                            return;
                        }

                        if (need > _budget.Left)
                        {
                            Skip(insert.Type, "past DxfReadOptions.MaxPlacedEntities");
                            _budget.Refuse(need, _forBlocks);
                            return;
                        }
                    }

                    cells = Cells(insert, block);
                }
                catch (NotFinite)
                {
                    Skip(insert.Type, NotFiniteReason);
                    return;
                }
                catch (TooManyCells)
                {
                    Skip(insert.Type, "an array of more than 1 048 576 cells");
                    _hugeArrays++;
                    return;
                }

                Look look = LookOf(insert);
                string handle = insert.Text(5)?.Trim();

                Placement place = _place;
                Look? inherit = _inherit;
                IReadOnlyList<string> path = _path;
                IReadOnlyList<string> handles = _handles;

                _inherit = look;
                _path = Appended(path, block.Name);
                _handles = Appended(handles, string.IsNullOrEmpty(handle) ? null : handle);
                _active.Add(block);
                int before = _into.Count;
                Mark mark = null;
                bool past = false;

                if (counts)
                {
                    _approved = true;
                    _treeStart = before;
                    mark = new Mark(this);
                }

                try
                {
                    foreach (Placement cell in cells)
                    {
                        _place = cell;

                        foreach (Record item in block.Items)
                        {
                            Item(item, true);
                        }
                    }
                }
                catch (PastTheBound) when (counts)
                {
                    // The INSERT is taken back whole, with what its placing did to the counts and the layers.
                    _into.RemoveRange(before, _into.Count - before);
                    mark.Restore(this);
                    past = true;
                }
                finally
                {
                    if (counts)
                    {
                        _approved = false;
                        _budget.Left -= _into.Count - before;
                    }

                    _active.RemoveAt(_active.Count - 1);
                    _place = place;
                    _inherit = inherit;
                    _path = path;
                    _handles = handles;
                }

                if (past)
                {
                    Skip(insert.Type, "past DxfReadOptions.MaxPlacedEntities");
                    _budget.Refuse(_budget.Left + 1, _forBlocks);
                }
            }

            /// <summary>An entity placed past what the bound on the entities placed from blocks leaves.</summary>
            private sealed class PastTheBound : Exception
            {
            }

            /// <summary>
            /// What a reading has counted and the layers it has met, taken before an INSERT is placed, so that an INSERT taken
            /// back leaves them as they were.
            /// </summary>
            private sealed class Mark
            {
                private readonly int _layers;
                private readonly KeyValuePair<string, int>[] _skipped;
                private readonly KeyValuePair<string, KeyValuePair<string, int>[]>[] _reasons;
                private readonly KeyValuePair<string, KeyValuePair<int, Exception>>[] _failures;
                private readonly int[] _counts;
                private readonly int _missingNames;
                private readonly string _firstLoop;

                internal Mark(Reading r)
                {
                    _layers = r._layers.Count;
                    _skipped = new List<KeyValuePair<string, int>>(r._skipped).ToArray();
                    _reasons = new List<KeyValuePair<string, SortedDictionary<string, int>>>(r._reasons).ConvertAll(p => new KeyValuePair<string, KeyValuePair<string, int>[]>(p.Key, new List<KeyValuePair<string, int>>(p.Value).ToArray())).ToArray();
                    _failures = new List<KeyValuePair<string, Failure>>(r._failures).ConvertAll(p => new KeyValuePair<string, KeyValuePair<int, Exception>>(p.Key, new KeyValuePair<int, Exception>(p.Value.Count, p.Value.First))).ToArray();
                    _counts = Counts(r);
                    _missingNames = r._missingNames.Count;
                    _firstLoop = r._firstLoop;
                }

                private static int[] Counts(Reading r) => new[]
                {
                    r._fitSplines, r._periodicSplines, r._openedLoops, r._droppedFaces, r._looseFaces, r._badLook, r._refusedSplines,
                    r._loweredFitSplines, r._missingInserts, r._selfInserts, r._deepInserts, r._xrefInserts, r._hugeArrays,
                };

                internal void Restore(Reading r)
                {
                    for (int i = r._layers.Count - 1; i >= _layers; i--)
                    {
                        r._layerNames.Remove(r._layers[i]);
                        r._layers.RemoveAt(i);
                    }

                    r._skipped.Clear();

                    foreach (KeyValuePair<string, int> pair in _skipped)
                    {
                        r._skipped.Add(pair.Key, pair.Value);
                    }

                    r._reasons.Clear();

                    foreach (KeyValuePair<string, KeyValuePair<string, int>[]> type in _reasons)
                    {
                        var reasons = new SortedDictionary<string, int>(StringComparer.Ordinal);

                        foreach (KeyValuePair<string, int> reason in type.Value)
                        {
                            reasons.Add(reason.Key, reason.Value);
                        }

                        r._reasons.Add(type.Key, reasons);
                    }

                    r._failures.Clear();

                    foreach (KeyValuePair<string, KeyValuePair<int, Exception>> failure in _failures)
                    {
                        r._failures.Add(failure.Key, new Failure { Count = failure.Value.Key, First = failure.Value.Value });
                    }

                    int k = 0;
                    r._fitSplines = _counts[k++];
                    r._periodicSplines = _counts[k++];
                    r._openedLoops = _counts[k++];
                    r._droppedFaces = _counts[k++];
                    r._looseFaces = _counts[k++];
                    r._badLook = _counts[k++];
                    r._refusedSplines = _counts[k++];
                    r._loweredFitSplines = _counts[k++];
                    r._missingInserts = _counts[k++];
                    r._selfInserts = _counts[k++];
                    r._deepInserts = _counts[k++];
                    r._xrefInserts = _counts[k++];
                    r._hugeArrays = _counts[k];
                    r._missingNames.RemoveRange(_missingNames, r._missingNames.Count - _missingNames);
                    r._firstLoop = _firstLoop;
                }
            }

            private static IReadOnlyList<string> Appended(IReadOnlyList<string> list, string name)
            {
                var longer = new string[list.Count + 1];

                for (int i = 0; i < list.Count; i++)
                {
                    longer[i] = list[i];
                }

                longer[list.Count] = name;
                return Array.AsReadOnly(longer);
            }

            /// <summary>
            /// The placements of an INSERT, one to each cell of its array, after the placement it is read through: the system
            /// of its extrusion (210) times the shift to its insertion point (10, 20, 30, in that system) times the turn about
            /// z by its rotation (50, degrees) times its scales (41, 42, 43, each 1 where not given) times the shift of the
            /// block's base point to the origin. A MINSERT's cell of column c and row r (of 70 columns and 71 rows, each 1
            /// where not given or below 1) is shifted by c times the column spacing (44) and r times the row spacing (45)
            /// along the turned axes, not scaled, as AutoCAD and ezdxf place it; two cells shifted alike are placed once. A
            /// turn by a whole number of quarter turns is exact.
            /// </summary>
            private List<Placement> Cells(Record insert, Block block)
            {
                var ocs = new Ocs(Extrusion(insert));
                TryPoint(insert, 0, out GeoPoint3 at);
                TryPoint(block.Header, 0, out GeoPoint3 basePoint);

                double sx = insert.Real(41, 1.0);
                double sy = insert.Real(42, 1.0);
                double sz = insert.Real(43, 1.0);
                double degrees = insert.Real(50, 0.0) % 360.0;
                double cos;
                double sin;

                if (degrees % 90.0 == 0.0)
                {
                    int quarter = ((int)(degrees / 90.0) % 4 + 4) % 4;
                    cos = quarter == 0 ? 1.0 : quarter == 2 ? -1.0 : 0.0;
                    sin = quarter == 1 ? 1.0 : quarter == 3 ? -1.0 : 0.0;
                }
                else
                {
                    double radians = degrees * Math.PI / 180.0;
                    cos = Math.Cos(radians);
                    sin = Math.Sin(radians);
                }

                GeoVector3 u = ocs.U;
                GeoVector3 v = ocs.V;
                GeoVector3 n = ocs.Normal;

                // The columns of the linear part: the turned and scaled axes of the INSERT's system.
                var x = new GeoVector3((cos * u.X + sin * v.X) * sx, (cos * u.Y + sin * v.Y) * sx, (cos * u.Z + sin * v.Z) * sx);
                var y = new GeoVector3((cos * v.X - sin * u.X) * sy, (cos * v.Y - sin * u.Y) * sy, (cos * v.Z - sin * u.Z) * sy);
                var z = new GeoVector3(n.X * sz, n.Y * sz, n.Z * sz);

                double columnSpacing = Scaled(insert.Real(44, 0.0));
                double rowSpacing = Scaled(insert.Real(45, 0.0));
                Grid(insert, out int columns, out int rows);

                if ((long)columns * rows > MostCells)
                {
                    throw new TooManyCells();
                }

                var cells = new List<Placement>(columns * rows);

                for (int row = 0; row < rows; row++)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        double ox = column * columnSpacing;
                        double oy = row * rowSpacing;
                        GeoPoint3 origin = ocs.World(new GeoPoint3(at.X + cos * ox - sin * oy, at.Y + sin * ox + cos * oy, at.Z));
                        double[] m =
                        {
                            x.X, y.X, z.X, origin.X - (x.X * basePoint.X + y.X * basePoint.Y + z.X * basePoint.Z),
                            x.Y, y.Y, z.Y, origin.Y - (x.Y * basePoint.X + y.Y * basePoint.Y + z.Y * basePoint.Z),
                            x.Z, y.Z, z.Z, origin.Z - (x.Z * basePoint.X + y.Z * basePoint.Y + z.Z * basePoint.Z),
                        };

                        cells.Add(Placement.Then(_place, m));
                    }
                }

                return cells;
            }

            /// <summary>
            /// The columns and rows of a MINSERT's array, each 1 where not given or below 1; a spacing of nought shifts its cells
            /// alike, and they are placed once.
            /// </summary>
            private static void Grid(Record insert, out int columns, out int rows)
            {
                columns = insert.Real(44, 0.0) == 0.0 ? 1 : Math.Max(1, insert.Int(70, 1));
                rows = insert.Real(45, 0.0) == 0.0 ? 1 : Math.Max(1, insert.Int(71, 1));
            }

            /// <summary>How many cells of an INSERT are placed: 1, or those of a MINSERT's array; none past 1 048 576.</summary>
            private static long CellCount(Record insert)
            {
                Grid(insert, out int columns, out int rows);
                long cells = (long)columns * rows;
                return cells > MostCells ? 0 : cells;
            }

            /// <summary>
            /// At most how many entities a block places: one for each entity of a type that is read, two for a 3DFACE, which may
            /// be two triangles, as many as its vertices for a polyline, which may be its edges, and for an INSERT its cells times
            /// its block's; nought for one that is not placed. A block met again while it is being worked out is inside itself
            /// there and is not placed again, so it counts nought; but that nought holds only on the way it was met by, so a size
            /// worked out through it is worked out again each time, and only one that met no such block is kept. The size worked
            /// out from the block of an INSERT is kept for its other INSERTs even so: the blocks counted nought on that walk are
            /// those being placed when it is, which refuse them too. Once 1 000 000 blocks have been worked out in the reading,
            /// in all, no more is: a size that needs one more is taken as the largest long and said to be stopped, and its INSERT
            /// is not placed, nor the other INSERTs of its block, whose walk would stop again. The sums stop at the largest long.
            /// </summary>
            private long RootSizeOf(Block block, out bool stopped)
            {
                if (block.RootSize >= 0)
                {
                    stopped = block.RootStopped;
                    return block.RootSize;
                }

                _stopped = false;
                long size = SizeOf(block, out bool met);

                if (met)
                {
                    block.RootSize = size;
                    block.RootStopped = _stopped;
                }

                stopped = _stopped;
                return size;
            }

            private long SizeOf(Block block, out bool met)
            {
                met = false;

                if (block.Size >= 0)
                {
                    return block.Size;
                }

                if (block.Size == -2)
                {
                    met = true;
                    return 0;
                }

                if (_sizing.Worked >= _sizing.Most)
                {
                    _stopped = true;
                    met = true;
                    return long.MaxValue;
                }

                _sizing.Worked++;
                block.Size = -2;
                long size = 0;

                foreach (Record item in block.Items)
                {
                    size = Plus(size, ItemSize(item, ref met));
                }

                block.Size = met ? -1 : size;
                return size;
            }

            private long ItemSize(Record r, ref bool met)
            {
                switch (r.Type)
                {
                    case "POINT":
                    case "LINE":
                    case "RAY":
                    case "XLINE":
                    case "CIRCLE":
                    case "ARC":
                    case "ELLIPSE":
                    case "SPLINE":
                    case "SOLID":
                    case "TRACE":
                    case "TEXT":
                    case "MTEXT":
                        return 1;

                    case "3DFACE":
                        return 2;

                    case "LWPOLYLINE":
                        int vertices = 0;

                        for (int i = 0; i < r.Count; i++)
                        {
                            vertices += r.CodeAt(i) == 10 ? 1 : 0;
                        }

                        return Math.Max(1, vertices);

                    case "POLYLINE":
                        return Math.Max(1, r.Following.Count);

                    case "INSERT":
                        string name = Decode((r.Text(2) ?? string.Empty).Trim());

                        if (!_expandBlocks || r.Unreadable() != null || name.Length == 0 || !_blockNames.TryGetValue(name, out Block block) || block.IsLayout || block.IsExternal)
                        {
                            return 0;
                        }

                        long size = SizeOf(block, out bool through);
                        met |= through;
                        return Times(CellCount(r), size);

                    default:
                        return 0;
                }
            }

            /// <summary>
            /// The working out of the sizes of blocks in a reading, shared by the drawing and the blocks read on their own: at
            /// most how many blocks are worked out in the reading, how many have been, and the INSERTs left out for it.
            /// </summary>
            private sealed class Sizing
            {
                internal Sizing(int most)
                {
                    Most = most;
                }

                internal int Most { get; }

                internal long Worked { get; set; }

                internal int Refused { get; private set; }

                internal int RefusedInBlocks { get; private set; }

                internal void Refuse(bool inBlocks)
                {
                    Refused++;
                    RefusedInBlocks += inBlocks ? 1 : 0;
                }
            }

            private static long Plus(long a, long b) => a > long.MaxValue - b ? long.MaxValue : a + b;

            private static long Times(long a, long b) => a == 0 || b == 0 ? 0 : a > long.MaxValue / b ? long.MaxValue : a * b;

            /// <summary>
            /// How many entities the INSERTs may still place from blocks in a reading, shared by the drawing and the blocks read
            /// on their own, and the INSERTs left out for it.
            /// </summary>
            private sealed class Budget
            {
                internal Budget(int most)
                {
                    Most = most;
                    Left = most;
                }

                internal int Most { get; }

                internal long Left { get; set; }

                internal int Refused { get; private set; }

                internal int RefusedInBlocks { get; private set; }

                internal long Wanted { get; private set; }

                internal void Refuse(long need, bool inBlocks)
                {
                    Refused++;
                    RefusedInBlocks += inBlocks ? 1 : 0;
                    Wanted = Plus(Wanted, need);
                }
            }

            /// <summary>An array of a MINSERT of more cells than are placed.</summary>
            private sealed class TooManyCells : Exception
            {
            }

            /// <summary>
            /// Places the INSERTs of the drawing, each where it stood among the entities, now that every block is in: a file
            /// may define its blocks after the entities that place them. Gives how many entities they placed.
            /// </summary>
            private int ExpandPending()
            {
                if (_pending.Count == 0)
                {
                    return 0;
                }

                var all = new List<DxfShape>(_entities.Count);
                _into = all;
                int from = 0;

                foreach (KeyValuePair<int, Record> pending in _pending)
                {
                    for (; from < pending.Key; from++)
                    {
                        all.Add(_entities[from]);
                    }

                    Expand(pending.Value);
                }

                for (; from < _entities.Count; from++)
                {
                    all.Add(_entities[from]);
                }

                return all.Count - _entities.Count;
            }

            /// <summary>
            /// The blocks for <see cref="DxfDocument.Blocks"/>, each read on its own, in its own coordinates, an INSERT inside
            /// it placed within it; what is left out of them is not counted, the counts being of the drawing. The entities of
            /// the blocks of the layouts are counted for a warning.
            /// </summary>
            private DxfBlock[] ReadBlocks()
            {
                var blocks = new List<DxfBlock>(_blocks.Count);

                foreach (Block block in _blocks)
                {
                    if (block.IsLayout)
                    {
                        _layoutEntities += block.Items.Count;
                        continue;
                    }

                    var own = new Reading(this);
                    own._active.Add(block);

                    foreach (Record item in block.Items)
                    {
                        own.Item(item, true);
                    }

                    GeoPoint3 basePoint = default(GeoPoint3);

                    try
                    {
                        if (block.Header.Unreadable() == null)
                        {
                            own.TryPoint(block.Header, 0, out basePoint);
                        }
                    }
                    catch (NotFinite)
                    {
                        basePoint = default(GeoPoint3);
                    }

                    blocks.Add(new DxfBlock(block.Name, basePoint, block.Flags, own._entities.ToArray()));
                }

                return blocks.ToArray();
            }

            private void BlockWarnings()
            {
                Warn(_againNames.Count, " block was defined again under a name already defined, and the first definition kept: " + Names(_againNames) + ".", " blocks were defined again under names already defined, and the first definitions kept: " + Names(_againNames) + ".");
                Warn(_missingInserts, " INSERT names a block the file does not define and was left out: " + Names(_missingNames) + ".", " INSERTs name blocks the file does not define and were left out: " + Names(_missingNames) + ".");
                Warn(_selfInserts, " INSERT of a block inside itself was not placed again, so as not to place it without end: " + _firstLoop + ".", " INSERTs of blocks inside themselves were not placed again, so as not to place them without end: " + _firstLoop + " the first.");
                Warn(_deepInserts, " INSERT nested more than 64 deep was not placed.", " INSERTs nested more than 64 deep were not placed.");
                Warn(_hugeArrays, " MINSERT of more than 1 048 576 cells was not placed.", " MINSERTs of more than 1 048 576 cells were not placed.");

                if (_budget.Refused > 0)
                {
                    string wanted = _budget.Wanted == long.MaxValue ? "more than " + Number(long.MaxValue) : Number(_budget.Wanted);
                    string inBlocks = _budget.RefusedInBlocks > 0 ? ", " + Number(_budget.RefusedInBlocks) + " of them inside the blocks read for Blocks" : string.Empty;

                    _warnings.Add(Number(_budget.Refused) + (_budget.Refused == 1 ? " INSERT was" : " INSERTs were") + " not placed" + inBlocks + ": the entities placed from blocks would have passed DxfReadOptions.MaxPlacedEntities, " + Number(_budget.Most) + ", had they placed the " + wanted + " more they would have taken.");
                }

                if (_sizing.Refused > 0)
                {
                    string inBlocks = _sizing.RefusedInBlocks > 0 ? ", " + Number(_sizing.RefusedInBlocks) + " of them inside the blocks read for Blocks" : string.Empty;

                    _warnings.Add(Number(_sizing.Refused) + (_sizing.Refused == 1 ? " INSERT was" : " INSERTs were") + " not placed" + inBlocks + ": the reading had worked out the sizes of " + Number(_sizing.Most) + " blocks, as many as it works out, and not yet what " + (_sizing.Refused == 1 ? "its block places" : "their blocks place") + ", so " + (_sizing.Refused == 1 ? "it" : "they") + " could not be held to DxfReadOptions.MaxPlacedEntities.");
                }
                Warn(_xrefInserts, " INSERT of an external reference was not placed: the drawing it refers to is another file, which is not read.", " INSERTs of external references were not placed: the drawings they refer to are other files, which are not read.");
                Warn(_layoutEntities, " entity of the blocks of the layouts (*Model_Space, *Paper_Space) was not read: the drawing is read from its ENTITIES section.", " entities of the blocks of the layouts (*Model_Space, *Paper_Space) were not read: the drawing is read from its ENTITIES section.");
            }

            /// <summary>Names in the order they were met, the first ten of them.</summary>
            private static string Names(List<string> names)
                => string.Join(", ", names.GetRange(0, Math.Min(10, names.Count))) + (names.Count > 10 ? " and " + Number(names.Count - 10) + " more" : string.Empty);

            #endregion

            #region Shapes placed

            /// <summary>
            /// A CIRCLE placed: a circle where the placement keeps its plane round, at its radius times the stretch; else the
            /// ellipse it becomes, exactly, its semi-axes the images of two radii square to each other.
            /// </summary>
            private void PlacedCircle(Record r, Frame frame, GeoPoint3 centre, double radius)
            {
                if (frame.Collapsed)
                {
                    Skip(r.Type, Flattened);
                    return;
                }

                GeoPoint3 c = frame.World(centre);
                bool flat = _planAs2D && frame.OnFloor && c.Z == 0.0;

                if (frame.Round(radius, _tolerance))
                {
                    double placed = radius * frame.U.Length;

                    if (!(placed > _tolerance.EqualPoint))
                    {
                        Skip(r.Type, "of no radius");
                        return;
                    }

                    Add(r, flat ? (object)new GeoCircle2(Plan(c), placed) : new GeoCircle3(c, frame.Normal, placed));
                    return;
                }

                Add(r, Stretched(c, frame.U.Multiply(radius), frame.V.Multiply(radius), frame.Normal, flat));
            }

            /// <summary>
            /// An ARC placed: an arc where the placement keeps its plane round, from where its start lands through its sweep
            /// about the placed normal, which a mirror turns over; on the floor, asked for, the arc of the plane, clockwise
            /// where it is mirrored. Else the spline that is exactly the part of the ellipse it becomes.
            /// </summary>
            private void PlacedArc(Record r, Frame frame, GeoPoint3 centre, double radius, double startDegrees, double endDegrees)
            {
                if (frame.Collapsed)
                {
                    Skip(r.Type, Flattened);
                    return;
                }

                double start = startDegrees * Math.PI / 180.0;
                double end = endDegrees * Math.PI / 180.0;
                double sweepDegrees = (endDegrees - startDegrees) % 360.0;

                if (sweepDegrees <= 0.0)
                {
                    sweepDegrees += 360.0;
                }

                double sweep = sweepDegrees * Math.PI / 180.0;
                GeoPoint3 c = frame.World(centre);
                bool flat = _planAs2D && frame.OnFloor && c.Z == 0.0;

                if (!frame.Round(radius, _tolerance))
                {
                    Add(r, ArcPieces(c, frame.U, frame.V, radius, radius, start, sweep, flat));
                    return;
                }

                double placed = radius * frame.U.Length;

                if (!(placed > _tolerance.EqualPoint))
                {
                    Skip(r.Type, "of no radius");
                    return;
                }

                if (flat)
                {
                    // The angle a of the plane is the turn of its x axis plus a, or less a where it is mirrored.
                    double turn = Math.Atan2(frame.U.Y, frame.U.X);

                    Add(r, frame.Normal.Z > 0.0
                        ? new GeoArc2(Plan(c), placed, turn + start, turn + end)
                        : new GeoArc2(Plan(c), placed, turn - start, turn - end, true));
                    return;
                }

                GeoPoint3 first = frame.World(new GeoPoint3(centre.X + radius * Math.Cos(start), centre.Y + radius * Math.Sin(start), centre.Z));
                Add(r, GeoArc3.FromCircle(c, frame.Normal, placed, first, sweep));
            }

            /// <summary>
            /// An ELLIPSE placed: the ellipse whose semi-axes are the images of its own, exactly, under any placement that does
            /// not flatten its plane, and a part of it the spline that is exactly that part.
            /// </summary>
            private void PlacedEllipse(Record r, GeoPoint3 centre, GeoVector3 normal, GeoVector3 axis, double major, double minor, double from, double span, bool whole)
            {
                EllipseAxes(normal, axis, major, out GeoVector3 u, out GeoVector3 w);
                GeoVector3 placedU = _place.Linear(u);
                GeoVector3 placedW = _place.Linear(w);
                GeoPoint3 c = _place.Apply(centre);
                GeoVector3 along = placedU.Multiply(major);
                GeoVector3 across = placedW.Multiply(minor);
                GeoVector3 cross = along.CrossProduct(across);
                double area = cross.Length;

                if (!(area > 1E-12 * along.Length * across.Length) || double.IsInfinity(area))
                {
                    Skip(r.Type, Flattened);
                    return;
                }

                bool level = cross.X == 0.0 && cross.Y == 0.0;
                GeoVector3 n = level ? new GeoVector3(0.0, 0.0, cross.Z > 0.0 ? 1.0 : -1.0) : new GeoVector3(cross.X / area, cross.Y / area, cross.Z / area);
                bool flat = _planAs2D && level && c.Z == 0.0 && placedU.Z == 0.0 && placedW.Z == 0.0;

                Add(r, whole ? Stretched(c, along, across, n, flat) : ArcPieces(c, placedU, placedW, major, minor, from, span, flat));
            }

            /// <summary>
            /// The ellipse of two conjugate semi-axes, the images of two square to each other: its radii and its major axis are
            /// their singular values and direction, as <see cref="GeoEllipse3.TransformBy(GeoTransform3)"/> finds them.
            /// </summary>
            private static object Stretched(GeoPoint3 centre, GeoVector3 along, GeoVector3 across, GeoVector3 normal, bool flat)
            {
                if (flat)
                {
                    var flatAlong = new GeoVector2(along.X, along.Y);
                    var flatAcross = new GeoVector2(across.X, across.Y);
                    return Ellipse2.FromSemiAxes(Plan(centre), flatAlong, flatAcross, Math.Abs(flatAlong.X * flatAcross.Y - flatAlong.Y * flatAcross.X), out _);
                }

                return Ellipse3.FromSemiAxes(centre, normal, along, across, along.CrossProduct(across).Length);
            }

            /// <summary>
            /// A LWPOLYLINE or 2D POLYLINE placed: where the placement keeps its plane round for the largest of its arcs, the
            /// chain or loop of its placed vertices with its bulges, the arcs about the placed normal; else, with arcs, its
            /// edges one by one.
            /// </summary>
            private void PlacedInPlane(Record r, Frame frame, List<GeoPoint3> points, List<double> bulges, bool closed)
            {
                int count = points.Count;
                int edges = closed ? count : count - 1;
                double largest = 0.0;
                bool arcs = false;

                for (int i = 0; i < edges; i++)
                {
                    double b = bulges[i];

                    if (b != 0.0)
                    {
                        GeoPoint3 p = points[i];
                        GeoPoint3 q = points[(i + 1) % count];
                        double chord = Math.Sqrt((q.X - p.X) * (q.X - p.X) + (q.Y - p.Y) * (q.Y - p.Y));
                        largest = Math.Max(largest, chord * (1.0 + b * b) / (4.0 * Math.Abs(b)));
                        arcs = true;
                    }
                }

                if (arcs && frame.Collapsed)
                {
                    Skip(r.Type, Flattened);
                    return;
                }

                if (arcs && !frame.Round(largest, _tolerance))
                {
                    Pieces(r, frame, points, bulges, edges);
                    return;
                }

                List<GeoPoint3> world = points.ConvertAll(frame.World);
                bool flat = _planAs2D && frame.OnFloor && world.TrueForAll(p => p.Z == 0.0);

                if (flat && frame.Normal.Z < 0.0)
                {
                    for (int i = 0; i < bulges.Count; i++)
                    {
                        bulges[i] = -bulges[i];
                    }
                }

                Chain(r, world, bulges, closed, flat, frame.Normal);
            }

            /// <summary>
            /// The edges of a chain with arcs that a placement stretches, in their order, each an entity with the look and
            /// the handle of the polyline: a segment for a straight edge, and for an arc edge the spline that is exactly the
            /// part of the ellipse the arc becomes. An edge whose ends land within the point tolerance of each other is left
            /// out.
            /// </summary>
            private void Pieces(Record r, Frame frame, List<GeoPoint3> points, List<double> bulges, int edges)
            {
                int count = points.Count;
                int made = 0;

                for (int i = 0; i < edges; i++)
                {
                    GeoPoint3 p = points[i];
                    GeoPoint3 q = points[(i + 1) % count];
                    GeoPoint3 a = frame.World(p);
                    GeoPoint3 e = frame.World(q);

                    if (a.IsEqualTo(e, _tolerance))
                    {
                        continue;
                    }

                    double b = bulges[i];

                    if (b == 0.0)
                    {
                        bool level = _planAs2D && frame.OnFloor && a.Z == 0.0 && e.Z == 0.0;
                        Add(r, level ? (object)new GeoLine2(Plan(a), Plan(e)) : new GeoLine3(a, e));
                        made++;
                        continue;
                    }

                    // A clockwise arc of the plane, from s through -t, is the counter-clockwise one from -s through t about the
                    // y axis turned over.
                    GeoArc2 arc = GeoArc2.FromBulge(new GeoPoint2(p.X, p.Y), new GeoPoint2(q.X, q.Y), b);
                    GeoPoint3 c = frame.World(new GeoPoint3(arc.Center.X, arc.Center.Y, p.Z));
                    bool flat = _planAs2D && frame.OnFloor && c.Z == 0.0;

                    Add(r, arc.SweptAngle > 0.0
                        ? ArcPieces(c, frame.U, frame.V, arc.Radius, arc.Radius, arc.StartAngle, arc.SweptAngle, flat)
                        : ArcPieces(c, frame.U, frame.V.Multiply(-1.0), arc.Radius, arc.Radius, -arc.StartAngle, -arc.SweptAngle, flat));
                    made++;
                }

                if (made == 0)
                {
                    Skip(r.Type, "with fewer than two distinct vertices");
                }
            }

            /// <summary>
            /// A spline of the block placed, its control points carried, its knots and weights kept; of the plane where it lies
            /// on the floor, asked for. False where the placement squeezes it onto a point.
            /// </summary>
            private bool TryPlaceSpline(GeoSpline3 spline, out object placed)
            {
                placed = null;
                List<GeoPoint3> points = new List<GeoPoint3>(spline.ControlPoints).ConvertAll(_place.Apply);
                IReadOnlyList<double> weights = spline.IsRational ? spline.Weights : null;

                try
                {
                    placed = FlatInWorld(points)
                        ? (object)new GeoSpline2(spline.Degree, points.ConvertAll(Plan), spline.Knots, weights)
                        : new GeoSpline3(spline.Degree, points, spline.Knots, weights);
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }

            #endregion
        }
    }
}
