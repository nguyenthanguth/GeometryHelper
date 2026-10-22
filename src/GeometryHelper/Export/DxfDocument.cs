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
        private readonly DxfEntity[] _entities;

        internal DxfDocument(DxfEntity[] entities, string[] layers, DxfUnits units, string version, IReadOnlyDictionary<string, int> skipped, string[] warnings)
        {
            _entities = entities;
            Entities = Array.AsReadOnly(entities);
            Layers = Array.AsReadOnly(layers);
            Units = units;
            Version = version;
            Skipped = skipped;
            Warnings = Array.AsReadOnly(warnings);
        }

        /// <summary>
        /// Gets the entities read, each with its shape, in the order of the file. One entity of the file can give more than
        /// one: a 3DFACE whose four corners are not in one plane is two triangles, both with its handle.
        /// </summary>
        public IReadOnlyList<DxfEntity> Entities { get; }

        /// <summary>
        /// Gets the names of the layers: those of the layer table in the order of the file, then any an entity is on that
        /// the table does not list, in the order they are first met. Two names that differ only in case are one layer, as
        /// in AutoCAD, named as first written.
        /// </summary>
        public IReadOnlyList<string> Layers { get; }

        /// <summary>
        /// Gets the unit the file says its drawing is in (<c>$INSUNITS</c>): <see cref="DxfUnits.Millimetres"/> for a file
        /// <see cref="DxfWriter"/> wrote, <see cref="DxfUnits.Unitless"/> where the file says none.
        /// </summary>
        public DxfUnits Units { get; }

        /// <summary>
        /// Gets the version of AutoCAD the file is written for (<c>$ACADVER</c>), such as <c>AC1015</c> for R2000; null
        /// where the file does not say.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Gets, for each type of entity some of which were not read, how many were not, in the order of the names of the
        /// types: the entities of the types that are not read at all (TEXT, INSERT, HATCH and the like) and those that have
        /// no shape to them, such as a LINE of no length or a CIRCLE of no radius.
        /// </summary>
        public IReadOnlyDictionary<string, int> Skipped { get; }

        /// <summary>
        /// Gets what the reader has to say about the file, each a sentence: a part of it that is not read, such as the
        /// entities of its blocks, or one read otherwise than it might be, such as a spline given by its fit points alone.
        /// </summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>
        /// Gets the entities on a layer, in the order of the file.
        /// </summary>
        /// <param name="layer">The name of the layer. Names are compared as AutoCAD compares them, letters of either case the
        /// same, so <c>Steel</c> finds the entities on <c>STEEL</c>.</param>
        /// <returns>The entities on that layer, a new list on each call; empty where there are none.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public IReadOnlyList<DxfEntity> OnLayer(string layer)
        {
            if (layer == null)
            {
                throw new ArgumentNullException(nameof(layer));
            }

            var entities = new List<DxfEntity>();

            foreach (DxfEntity entity in _entities)
            {
                if (string.Equals(entity.Layer, layer, StringComparison.OrdinalIgnoreCase))
                {
                    entities.Add(entity);
                }
            }

            return entities;
        }

        /// <summary>
        /// Gets the shapes of a type, in the order of the file.
        /// </summary>
        /// <typeparam name="T">The type of shape, such as <see cref="Geometry.GeoPolylineArc2"/> or <see cref="Geometry.GeoTriangle3"/>.</typeparam>
        /// <returns>The shapes of the entities whose shape is of that type, a new list on each call.</returns>
        public IReadOnlyList<T> OfType<T>()
        {
            var shapes = new List<T>();

            foreach (DxfEntity entity in _entities)
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
