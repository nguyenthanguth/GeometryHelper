using System;
using System.Collections.Generic;

namespace GeometryHelper.Export
{
    /// <summary>
    /// What <see cref="DxfReader"/> read from a DXF file: the shapes of its entities in file order, its layers, its unit and
    /// version, and what it left out.
    /// </summary>
    /// <remarks>
    /// Nothing in it follows the order of a dictionary: the entities and the layers are in the order of the file, and
    /// <see cref="Skipped"/> in the order of the names of the types.
    /// </remarks>
    public sealed class DxfDocument
    {
        private readonly DxfShape[] _entities;
        private readonly Dictionary<string, DxfLayer> _layerNames = new Dictionary<string, DxfLayer>(StringComparer.OrdinalIgnoreCase);

        internal DxfDocument(DxfShape[] entities, string[] layers, DxfLayer[] layerTable, DxfBlock[] blocks, DxfUnits units, string version, IReadOnlyDictionary<string, int> skipped, string[] warnings)
        {
            _entities = entities;

            // The table's names differ by more than case already; the first is kept all the same.
            foreach (DxfLayer layer in layerTable)
            {
                if (!_layerNames.ContainsKey(layer.Name))
                {
                    _layerNames.Add(layer.Name, layer);
                }
            }

            Shapes = Array.AsReadOnly(entities);
            Layers = Array.AsReadOnly(layers);
            LayerTable = Array.AsReadOnly(layerTable);
            Blocks = Array.AsReadOnly(blocks);
            Units = units;
            Version = version;
            Skipped = skipped;
            Warnings = Array.AsReadOnly(warnings);
        }

        /// <summary>
        /// Gets how many blocks the reading worked out, in all, for the sizes of the INSERTs it placed or left out; for the
        /// tests of that work alone.
        /// </summary>
        internal long SizedBlocks { get; set; }

        /// <summary>
        /// Gets the shapes read, each with its look, in the order of the file, an INSERT's where the INSERT stands: the
        /// entities of its block placed, in the order of the block. One entity of the file can give more than one: a 3DFACE
        /// whose four corners are not in one plane is two triangles, both with its handle, and a block of 50 entities placed
        /// by 10 000 INSERTs is 500 000.
        /// </summary>
        public IReadOnlyList<DxfShape> Shapes { get; }

        /// <summary>
        /// Gets the names of the layers: those of the layer table in the order of the file, then any an entity is on that
        /// the table does not list, in the order they are first met. Two names that differ only in case are one layer, as
        /// in AutoCAD, named as first written.
        /// </summary>
        public IReadOnlyList<string> Layers { get; }

        /// <summary>
        /// Gets the layers of the layer table, in the order of the table, each with its colour, linetype and lineweight and
        /// whether it is off, frozen, locked and plotted. A record of a name that an earlier one differs from only in case is
        /// left out, the first kept, as <see cref="Layers"/> keeps the first spelling; a layer an entity is on that the table
        /// does not list is in <see cref="Layers"/> and not here.
        /// </summary>
        public IReadOnlyList<DxfLayer> LayerTable { get; }

        /// <summary>
        /// Gets the blocks the file defines, in the order of the file, each with its own entities in its own coordinates;
        /// the INSERTs of the drawing are read in <see cref="Shapes"/> as the entities they place. The blocks of the
        /// layouts, <c>*Model_Space</c> and <c>*Paper_Space</c>, are not among them.
        /// </summary>
        public IReadOnlyList<DxfBlock> Blocks { get; }

        /// <summary>
        /// Gets the unit the file says its drawing is in (<c>$INSUNITS</c>): <see cref="DxfUnits.Millimeters"/> for a file
        /// <see cref="DxfWriter"/> wrote, <see cref="DxfUnits.Undefined"/> where the file says none.
        /// </summary>
        public DxfUnits Units { get; }

        /// <summary>
        /// Gets the version of AutoCAD the file is written for (<c>$ACADVER</c>), such as <c>AC1015</c> for R2000; null
        /// where the file does not say.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Gets, for each type of entity some of which were not read, how many were not, in the order of the names of the
        /// types: the entities of the types that are not read at all (HATCH, DIMENSION, ATTRIB and the like) and those that
        /// have no shape to them, such as a LINE of no length, a CIRCLE of no radius or a TEXT of no height, each time a block
        /// places it; and the
        /// INSERTs that place nothing, of a block the file does not define or of one inside itself among them.
        /// </summary>
        public IReadOnlyDictionary<string, int> Skipped { get; }

        /// <summary>
        /// Gets what the reader has to say about the file, each a sentence: a part of it that is not read, such as the
        /// entities of paper space or an INSERT of a block the file does not define, or one read otherwise than it might be,
        /// such as a spline given by its fit points alone.
        /// </summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>
        /// Gets the shapes on a layer, in the order of the file.
        /// </summary>
        /// <param name="layer">The name of the layer. Names are compared as AutoCAD compares them, letters of either case the
        /// same, so <c>Steel</c> finds the entities on <c>STEEL</c>.</param>
        /// <returns>The shapes on that layer, a new list on each call; empty where there are none.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public IReadOnlyList<DxfShape> OnLayer(string layer)
        {
            if (layer == null)
            {
                throw new ArgumentNullException(nameof(layer));
            }

            var entities = new List<DxfShape>();

            foreach (DxfShape entity in _entities)
            {
                if (string.Equals(entity.Layer, layer, StringComparison.OrdinalIgnoreCase))
                {
                    entities.Add(entity);
                }
            }

            return entities;
        }

        /// <summary>
        /// Gets a layer of the layer table by its name.
        /// </summary>
        /// <param name="name">The name of the layer. Names are compared as AutoCAD compares them, letters of either case the
        /// same, so <c>Steel</c> finds the layer <c>STEEL</c>.</param>
        /// <returns>The layer of the table of that name; null where the table has none, as for a layer only an entity names.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public DxfLayer GetLayer(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return _layerNames.TryGetValue(name, out DxfLayer layer) ? layer : null;
        }

        /// <summary>
        /// Gets the shapes of a type, in the order of the file.
        /// </summary>
        /// <typeparam name="T">The type of shape, such as <see cref="Geometry.GeoPolylineArc2"/> or <see cref="Geometry.GeoTriangle3"/>.</typeparam>
        /// <returns>The shapes of the entities whose shape is of that type, a new list on each call.</returns>
        public IReadOnlyList<T> OfType<T>()
        {
            var shapes = new List<T>();

            foreach (DxfShape entity in _entities)
            {
                if (entity.Shape is T shape)
                {
                    shapes.Add(shape);
                }
            }

            return shapes;
        }
    }
}
