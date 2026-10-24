using System;
using System.Collections;
using System.Collections.Generic;

namespace GeometryHelper.Export
{
    /// <summary>
    /// The entities of a space of a <see cref="DxfFile"/>, model space, in the order of the file: read as a list, added to
    /// and removed from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Add(object, string)"/> writes a shape as a new entity at the end of the ENTITIES section, with a handle of
    /// its own above every handle of the file and the model space as its owner, as AutoCAD writes one: a
    /// <see cref="Geometry.GeoCircle2"/> of radius 5 on the layer holes is a CIRCLE on holes. <see cref="Remove(DxfItem)"/>
    /// takes an entity out with every record of it, a POLYLINE with its vertices and its end, an INSERT with its attributes
    /// and theirs, and its handle is not given again.
    /// </para>
    /// <para>
    /// The list is not to be changed while it is walked: an enumerator walking it when an entity is added or removed throws
    /// on its next step, as a <see cref="List{T}"/>'s does.
    /// </para>
    /// </remarks>
    internal sealed class DxfSpace : IReadOnlyList<DxfItem>
    {
        private readonly DxfFile _file;
        private readonly List<DxfItem> _items;
        private int _removed;
        private int _version;

        internal DxfSpace(DxfFile file, List<DxfItem> items, DxfSpaceKind kind = DxfSpaceKind.Model, DxfFileBlock block = null)
        {
            _file = file;
            _items = items;
            Kind = kind;
            Block = block;

            foreach (DxfItem item in items)
            {
                item.Space = this;
            }
        }

        /// <summary>Which space the list is: model space, paper space, or the entities of a block.</summary>
        internal DxfSpaceKind Kind { get; }

        /// <summary>The block whose entities the list holds; null for model space and paper space.</summary>
        internal DxfFileBlock Block { get; }

        /// <summary>The <see cref="DxfBlockTableRecord"/> that stands for the space, once one is made.</summary>
        internal DxfBlockTableRecord Record { get; set; }

        /// <summary>
        /// Gets how many entities the space holds.
        /// </summary>
        public int Count => _items.Count - _removed;

        /// <summary>
        /// Gets the entity at an index, in the order of the file, those added after the rest.
        /// </summary>
        /// <param name="index">The index, from nought.</param>
        /// <returns>The entity.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is below nought or not below <see cref="Count"/>.</exception>
        public DxfItem this[int index]
        {
            get
            {
                Compact();
                return _items[index];
            }
        }

        /// <summary>
        /// Adds a shape as a new entity of the space, at the end of the ENTITIES section, on a layer and with no look of its
        /// own, its colour, linetype and lineweight those of the layer.
        /// </summary>
        /// <param name="shape">The shape, any <see cref="DxfWriter"/> writes as one entity without a tolerance, or a <see cref="DxfText"/>.</param>
        /// <param name="layer">The layer, as it is to be written; layer 0 when none is given. A layer the layer table does not
        /// list is added to it, in the colour 7 with the linetype CONTINUOUS, and the log says so.</param>
        /// <returns>The new entity, the last of the space.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the shape is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the shape is not one the writer writes without a tolerance, is written
        /// as more than one entity, or as a type the file's version cannot hold, as <see cref="DxfItem.Shape"/> refuses it; or
        /// when the layer's name is empty, has a space at either end, or has a character AutoCAD does not take in a name.</exception>
        /// <remarks>
        /// The entity is written as <see cref="DxfWriter"/> writes the shape, in the file's own line ends, padding and code
        /// page, with a handle above every handle of the file and <c>$HANDSEED</c> above it, the owner the model space's
        /// BLOCK_RECORD from R13 on, and the extents of the header grown to hold it. A chain of the plane in a file of R12 is a
        /// POLYLINE with its vertices, exact.
        /// </remarks>
        public DxfItem Add(object shape, string layer = null) => _file.Add(this, shape, layer, null, null, null, null, 0.0, 0);

        /// <summary>
        /// Adds an entity as a new one of the space, at the end of the ENTITIES section: its shape, on its layer, in its
        /// colour, true colour, linetype and lineweight and with its thickness, each written where it has one, as
        /// <see cref="DxfWriter.Add(DxfShape)"/> writes them.
        /// </summary>
        /// <param name="entity">The entity: one <see cref="DxfReader"/> read from another file, or one made in code, such as
        /// <c>new DxfShape(plate, "steel", color: 1)</c>.</param>
        /// <returns>The new entity, the last of the space.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        /// <exception cref="ArgumentException">Thrown when its shape cannot be written as <see cref="Add(object, string)"/>
        /// says, its layer's name is one AutoCAD does not take, or its linetype is not in the file's linetype table, BYLAYER
        /// and BYBLOCK aside; or when it has a lineweight in a file older than R2000 or a true colour in one older than R2004,
        /// which have none.</exception>
        /// <remarks>
        /// Its <see cref="DxfShape.Groups"/> are not written, and its handle is a new one: only what its shape and its look say
        /// is. The edges a 3DFACE it was read from hid are hidden again.
        /// </remarks>
        public DxfItem Add(DxfShape entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            return _file.Add(this, entity.Shape, entity.Layer, entity.Color, entity.TrueColor, entity.Linetype, entity.LineWeight, entity.Thickness, entity.HiddenEdges);
        }

        /// <summary>
        /// Removes an entity from the space and the file, with every record of it.
        /// </summary>
        /// <param name="item">The entity.</param>
        /// <returns>Whether it was removed: false where it is not of this space, or was removed before.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        /// <remarks>
        /// <para>
        /// A POLYLINE goes with its VERTEX records and its SEQEND, an INSERT with its ATTRIB records and their SEQEND. The
        /// handles of its records are not given to an entity added after, and what pointed at them is seen to when the file is
        /// saved: a GROUP no longer lists the entity, a list of reactors no longer holds it, nor a SORTENTSTABLE, and an object
        /// it owned, such as its extension dictionary or the reactor of an IMAGE, is removed with it; any other record that
        /// points at it keeps the pointer, and the log says which, in one line.
        /// </para>
        /// <para>
        /// The extents of the header are left as they are, as AutoCAD leaves them until it is asked to zoom to the extents.
        /// </para>
        /// </remarks>
        public bool Remove(DxfItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            return _file.Remove(this, item);
        }

        /// <summary>
        /// Gets an enumerator over the entities, in the order of the file.
        /// </summary>
        /// <returns>The enumerator.</returns>
        /// <exception cref="InvalidOperationException">Thrown on a step after an entity was added or removed.</exception>
        public IEnumerator<DxfItem> GetEnumerator()
        {
            Compact();
            int version = _version;

            for (int i = 0; i < _items.Count; i++)
            {
                if (version != _version)
                {
                    throw new InvalidOperationException("The entities of the space were changed while they were walked.");
                }

                yield return _items[i];
            }

            if (version != _version)
            {
                throw new InvalidOperationException("The entities of the space were changed while they were walked.");
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>The entities of the space as they stand, in a list of their own that later changes leave as it is.</summary>
        internal DxfItem[] Snapshot()
        {
            Compact();
            return _items.ToArray();
        }

        /// <summary>An entity added to the space, last.</summary>
        internal void Append(DxfItem item)
        {
            item.Space = this;
            _items.Add(item);
            _version++;
        }

        /// <summary>An entity removed from the space: left in the list, marked, until the list is next read.</summary>
        internal void Removed()
        {
            _removed++;
            _version++;
        }

        /// <summary>Takes the entities removed out of the list, once, before it is read by index or walked.</summary>
        private void Compact()
        {
            if (_removed == 0)
            {
                return;
            }

            _items.RemoveAll(item => item.IsRemoved);
            _removed = 0;
        }
    }

    /// <summary>Which space a <see cref="DxfSpace"/> is.</summary>
    internal enum DxfSpaceKind
    {
        /// <summary>Model space: the entities of the ENTITIES section not in paper space.</summary>
        Model,

        /// <summary>Paper space: the entities of the ENTITIES section whose group 67 is 1, those of the layout last shown.</summary>
        Paper,

        /// <summary>The entities of a block, between its BLOCK and its ENDBLK.</summary>
        Block,
    }
}
