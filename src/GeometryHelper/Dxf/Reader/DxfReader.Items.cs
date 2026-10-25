using System;
using System.Collections.Generic;
using System.Text;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    public static partial class DxfReader
    {
        /// <summary>
        /// The shapes the reader makes of one entity of a file read on its own, for <see cref="DxfFile"/>: as it reads an
        /// entity of the drawing, its own coordinates kept, with no INSERT placing it and no paper space left out. Empty where
        /// the reader reads no shape of it, as for a HATCH or a LINE of no length; more than one where it reads more, as the two
        /// triangles of a 3DFACE out of flat. Not for an INSERT, which <see cref="InsertOf"/> reads.
        /// </summary>
        internal static List<object> ShapesOf(Record record, Tolerance tolerance)
        {
            var reading = new Reading(new DxfReadOptions { Tolerance = tolerance, KeepGroups = false });
            reading.One(record);
            return reading.Shapes();
        }

        /// <summary>The layer of an entity as the reader names it: its group 8, its escapes read as letters, 0 where it gives none.</summary>
        internal static string LayerOf(Record record)
        {
            string layer = Decode((record.Text(8) ?? string.Empty).Trim());
            return layer.Length == 0 ? "0" : layer;
        }

        /// <summary>A name as the reader reads one of a table or an entity: the spaces around it taken off and its escapes read as letters.</summary>
        internal static string NameOf(string written) => Decode(written.Trim());

        /// <summary>
        /// An INSERT as it stands in the file, for <see cref="DxfFile"/>; null where a number of it is not a finite number, as
        /// the reader leaves such an INSERT out.
        /// </summary>
        internal static DxfInsert InsertOf(Record insert, Func<Placing> placed)
            => new Reading(new DxfReadOptions { KeepGroups = false }).Insert(insert, placed);

        /// <summary>
        /// The base point and flags of a BLOCK record as the reader reads them: the point (10, 20, 30), the origin where a
        /// number of the record is not a finite number, and the flags (70), nought where it gives none.
        /// </summary>
        internal static void BlockOf(Record block, out GeoPoint3 basePoint, out int flags)
        {
            basePoint = default(GeoPoint3);
            flags = block.Int(70, 0);

            if (block.Unreadable() == null)
            {
                new Reading(new DxfReadOptions { KeepGroups = false }).BasePoint(block, out basePoint);
            }
        }

        /// <summary>What the reader placed of one INSERT, and what it has to say of what it left out.</summary>
        internal sealed class Placing
        {
            internal Placing(IReadOnlyList<DxfShape> entities, IReadOnlyList<string> warnings, Exception failure)
            {
                Entities = entities;
                Warnings = warnings;
                Failure = failure;
            }

            internal IReadOnlyList<DxfShape> Entities { get; }

            internal IReadOnlyList<string> Warnings { get; }

            /// <summary>The first exception an entity of the block threw, which left it out; null where none did.</summary>
            internal Exception Failure { get; }
        }

        /// <summary>
        /// The placing of the INSERTs of a file one at a time, for <see cref="DxfFile"/>: a reading that has the layer table
        /// and the blocks of the file, read once, and works out the sizes of the blocks for all of them together, as one
        /// reading of the file does; each INSERT placed in a reading of its own that shares them, with a bound of its own on
        /// the entities it places and the spellings of the layers of its own, those of the table first and then those of the
        /// entities of the drawing, as the reader has them when it places the INSERTs of the drawing. One INSERT is placed at
        /// a time.
        /// </summary>
        internal sealed class Placer
        {
            private readonly Reading _file;

            internal Placer(IEnumerable<Record> layers, IEnumerable<string> drawingLayers, IEnumerable<IEnumerable<Record>> blockSections, Tolerance tolerance)
            {
                _file = new Reading(new DxfReadOptions { Tolerance = tolerance });
                _file.TakeBlocks(layers, drawingLayers, blockSections);
            }

            /// <summary>
            /// What the reader places of an INSERT: the entities of its block placed where it puts them, with the look they
            /// take from it, in the coordinates of the drawing, or in those of the block that holds it, which it is placed
            /// within as the reader reads that block's own entities.
            /// </summary>
            internal Placing Place(Record insert, string ownerBlock)
            {
                lock (_file)
                {
                    return _file.PlaceAlone(insert, ownerBlock);
                }
            }
        }

        /// <summary>The layers of LAYER records of the layer table, as the reader reads them into <see cref="DxfDocument.LayerTable"/>.</summary>
        internal static DxfLayer[] LayerTableOf(IEnumerable<Record> layers)
        {
            var reading = new Reading(new DxfReadOptions { KeepGroups = false });
            return reading.TableOf(layers);
        }

        /// <summary>The name of a BLOCK record, as the reader names the block.</summary>
        internal static string BlockNameOf(Record block) => Decode((block.Text(2) ?? block.Text(3) ?? string.Empty).Trim());

        private sealed partial class Reading
        {
            /// <summary>Reads one entity on its own, as one of the drawing with no paper space left out.</summary>
            internal void One(Record record) => Item(record, true);

            internal List<object> Shapes() => _entities.ConvertAll(e => e.Shape);

            internal void BasePoint(Record block, out GeoPoint3 basePoint)
            {
                try
                {
                    TryPoint(block, 0, out basePoint);
                }
                catch (NotFinite)
                {
                    basePoint = default(GeoPoint3);
                }
            }

            /// <summary>
            /// The INSERT of a record: the name of its block, its insertion point taken to the world from the system of its
            /// extrusion, its extrusion, its turn in radians brought into a turn, its scales, and the columns, rows and
            /// spacings of its array, each as the file gives it or as the DXF reference has it where it does not.
            /// </summary>
            internal DxfInsert Insert(Record r, Func<Placing> placed)
            {
                if (r.Unreadable() != null)
                {
                    return null;
                }

                try
                {
                    var ocs = new Ocs(Extrusion(r));
                    TryPoint(r, 0, out GeoPoint3 at);
                    string name = Decode((r.Text(2) ?? string.Empty).Trim());

                    return new DxfInsert(
                        name,
                        ocs.World(at),
                        ocs.Normal,
                        DxfText.Turn(r.Real(50, 0.0) * Math.PI / 180.0),
                        r.Real(41, 1.0),
                        r.Real(42, 1.0),
                        r.Real(43, 1.0),
                        r.Int(70, 1),
                        r.Int(71, 1),
                        r.Real(44, 0.0),
                        r.Real(45, 0.0),
                        placed);
                }
                catch (NotFinite)
                {
                    return null;
                }
            }

            /// <summary>
            /// A reading of one INSERT of a file, for <see cref="Placer"/>: the options, the blocks and the working out of
            /// their sizes of the file's reading, the spellings of its layers copied, and its own entities, counts and bound
            /// on what it places.
            /// </summary>
            private Reading(Reading file, Budget budget)
            {
                _planAs2D = file._planAs2D;
                _toMillimetres = file._toMillimetres;
                _tolerance = file._tolerance;
                _keepGroups = file._keepGroups;
                _expandBlocks = file._expandBlocks;
                _interned = file._interned;
                _layers = new List<string>(file._layers);
                _layerNames = new Dictionary<string, string>(file._layerNames, StringComparer.OrdinalIgnoreCase);
                _blocks = file._blocks;
                _blockNames = file._blockNames;
                _scale = file._scale;
                _unitCode = file._unitCode;
                _budget = budget;
                _sizing = file._sizing;
                _unsized = file._unsized;
                _into = _entities;
            }

            /// <summary>
            /// Takes the layer table, the layers of the entities of the drawing in their order, and the blocks of a file, each
            /// section of blocks ending the block open in it, as its ENDSEC does.
            /// </summary>
            internal void TakeBlocks(IEnumerable<Record> layers, IEnumerable<string> drawingLayers, IEnumerable<IEnumerable<Record>> blockSections)
            {
                foreach (Record layer in layers)
                {
                    TableLayer(layer);
                }

                foreach (string layer in drawingLayers)
                {
                    LayerName(layer);
                }

                foreach (IEnumerable<Record> section in blockSections)
                {
                    foreach (Record r in section)
                    {
                        BlockRecord(r);
                    }

                    EndOfBlock();
                }
            }

            /// <summary>Places one INSERT in a reading of its own that shares this one's blocks, with a bound of its own.</summary>
            internal Placing PlaceAlone(Record insert, string ownerBlock) => new Reading(this, new Budget(_budget.Most)).PlaceOne(insert, ownerBlock);

            private Placing PlaceOne(Record insert, string ownerBlock)
            {
                if (ownerBlock != null && _blockNames.TryGetValue(ownerBlock, out Block owner))
                {
                    _active.Add(owner);
                }

                Expand(insert);
                BlockWarnings();
                EntityWarnings(false);

                // What was left out, type by type, with the reasons, as the reader logs it.
                foreach (KeyValuePair<string, SortedDictionary<string, int>> type in _reasons)
                {
                    var why = new StringBuilder();

                    foreach (KeyValuePair<string, int> reason in type.Value)
                    {
                        why.Append(why.Length == 0 ? string.Empty : ", ").Append(Number(reason.Value)).Append(' ').Append(reason.Key);
                    }

                    _warnings.Add(Number(_skipped[type.Key]) + " " + type.Key + " not placed: " + why + ".");
                }

                Exception failure = null;

                foreach (KeyValuePair<string, Failure> failed in _failures)
                {
                    failure = failure ?? failed.Value.First;
                }

                return new Placing(_entities.AsReadOnly(), _warnings.AsReadOnly(), failure);
            }

            internal DxfLayer[] TableOf(IEnumerable<Record> layers)
            {
                foreach (Record layer in layers)
                {
                    TableLayer(layer);
                }

                return _layerTable.ToArray();
            }
        }
    }
}
