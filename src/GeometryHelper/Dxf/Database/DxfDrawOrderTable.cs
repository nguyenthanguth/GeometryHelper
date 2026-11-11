using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The draw order of a block, as AutoCAD's <c>DrawOrderTable</c>: the SORTENTSTABLE kept under ACAD_SORTENTS in the
    /// extension dictionary of the block's BLOCK_RECORD, which gives an entity a sort handle (5) after its own (331). The
    /// entities are drawn in the order of their sort handles, the lowest first and so at the back; an entity the table does
    /// not list sorts by its own handle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A block of a line 2A, a circle 2B and a hatch 2C drawn over them, whose hatch is sent to the back by
    /// <c>table.MoveToBottom(new[] { hatch })</c>, is drawn hatch, line, circle: the three take the sort handles 2A, 2B and 2C
    /// in that order, so the table lists the hatch with 2A, the line with 2B and the circle with 2C, as AutoCAD writes them.
    /// The sort handles are those the moved entities had between them, given out again in the new order, so that no two
    /// entities share one and an entity the table does not list keeps its place.
    /// </para>
    /// <para>
    /// <see cref="DxfBlockTableRecord.DrawOrderTable"/> gives the table of a block, and
    /// <see cref="DxfBlockTableRecord.CreateDrawOrderTable"/> makes one where it has none.
    /// </para>
    /// </remarks>
    public sealed class DxfDrawOrderTable : DxfDBObject
    {
        /// <summary>The name of the table in the extension dictionary of its block, as AutoCAD names it.</summary>
        internal const string Key = "ACAD_SORTENTS";

        // The groups of AcDbSortentsTable in the order AutoCAD writes them: the block, then each entity and its sort handle.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 330, 331, 5 });

        private static readonly int[] PairCodes = { 331, 5 };

        private DxfBlockTableRecord _block;

        internal DxfDrawOrderTable(DxfItem item)
            : base(item) { }

        /// <summary>Gets the block whose entities the table orders; null for a table read on its own, outside its block.</summary>
        public DxfBlockTableRecord Block => _block;

        /// <summary>Gives the table the block it orders.</summary>
        internal DxfDrawOrderTable For(DxfBlockTableRecord block)
        {
            _block = block;
            return this;
        }

        /// <summary>
        /// Gets the entities of the block in the order they are drawn, as AutoCAD's <c>GetFullDrawOrder</c>: the back first.
        /// Two entities of one sort handle, which a damaged table may give, are drawn in the order of the block.
        /// </summary>
        /// <returns>The entities, a new list on each call.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the table has no block.</exception>
        public IReadOnlyList<DxfEntity> GetFullDrawOrder()
        {
            List<DxfEntity> entities = Entities(out ulong[] keys);
            var places = new int[entities.Count];

            for (int i = 0; i < places.Length; i++)
            {
                places[i] = i;
            }

            Array.Sort(places, (a, b) =>
            {
                int byKey = keys[a].CompareTo(keys[b]);
                return byKey != 0 ? byKey : a.CompareTo(b);
            });

            var ordered = new List<DxfEntity>(places.Length);

            foreach (int place in places)
            {
                ordered.Add(entities[place]);
            }

            return ordered.AsReadOnly();
        }

        /// <summary>Brings entities of the block to the front, as AutoCAD's <c>MoveToTop</c>, keeping their order among themselves.</summary>
        /// <param name="entities">The entities, of the block.</param>
        /// <exception cref="ArgumentNullException">Thrown when the entities are null.</exception>
        /// <exception cref="ArgumentException">Thrown when an entity is null or not of the block.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table has no block, or is erased.</exception>
        public void MoveToTop(IEnumerable<DxfEntity> entities) => Reorder(entities, null, true);

        /// <summary>Sends entities of the block to the back, as AutoCAD's <c>MoveToBottom</c>, keeping their order among themselves.</summary>
        /// <param name="entities">The entities, of the block.</param>
        /// <exception cref="ArgumentNullException">Thrown when the entities are null.</exception>
        /// <exception cref="ArgumentException">Thrown when an entity is null or not of the block.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table has no block, or is erased.</exception>
        public void MoveToBottom(IEnumerable<DxfEntity> entities) => Reorder(entities, null, false);

        /// <summary>Puts entities of the block just over another, as AutoCAD's <c>MoveAbove</c>, keeping their order among themselves.</summary>
        /// <param name="entities">The entities, of the block.</param>
        /// <param name="target">The entity they go over, of the block and not among them.</param>
        /// <exception cref="ArgumentNullException">Thrown when the entities or the target are null.</exception>
        /// <exception cref="ArgumentException">Thrown when an entity is null or not of the block, or the target is not of the
        /// block or is among the entities.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table has no block, or is erased.</exception>
        public void MoveAbove(IEnumerable<DxfEntity> entities, DxfEntity target) =>
            Reorder(entities, target ?? throw new ArgumentNullException(nameof(target)), true);

        /// <summary>Puts entities of the block just under another, as AutoCAD's <c>MoveBelow</c>, keeping their order among themselves.</summary>
        /// <param name="entities">The entities, of the block.</param>
        /// <param name="target">The entity they go under, of the block and not among them.</param>
        /// <exception cref="ArgumentNullException">Thrown when the entities or the target are null.</exception>
        /// <exception cref="ArgumentException">Thrown when an entity is null or not of the block, or the target is not of the
        /// block or is among the entities.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table has no block, or is erased.</exception>
        public void MoveBelow(IEnumerable<DxfEntity> entities, DxfEntity target) =>
            Reorder(entities, target ?? throw new ArgumentNullException(nameof(target)), false);

        /// <inheritdoc/>
        public override string ToString() => "SORTENTSTABLE " + (Handle ?? string.Empty);

        /// <summary>The entities of the block in its order, each with its sort key: its sort handle, or its own where the table lists none.</summary>
        private List<DxfEntity> Entities(out ulong[] keys)
        {
            if (_block == null)
            {
                throw new InvalidOperationException(
                    "The draw order table "
                        + (Handle ?? string.Empty)
                        + " has no block: read it through DxfBlockTableRecord.DrawOrderTable."
                );
            }

            var sorts = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);

            if (Item != null && !IsErased)
            {
                string entity = null;

                foreach (DxfTypedValue group in Item.File.ObjectGroupsOf(Item))
                {
                    if (group.TypeCode == 331)
                    {
                        entity = (group.Value ?? string.Empty).Trim();
                    }
                    else if (group.TypeCode == 5 && entity != null)
                    {
                        if (TryHex(group.Value, out ulong key))
                        {
                            sorts[entity] = key;
                        }

                        entity = null;
                    }
                }
            }

            var entities = new List<DxfEntity>();
            var found = new List<ulong>();

            foreach (DxfEntity entity in _block)
            {
                entities.Add(entity);
                ulong own = TryHex(entity.Handle, out ulong value) ? value : 0UL;
                found.Add(entity.Handle != null && sorts.TryGetValue(entity.Handle, out ulong sort) ? sort : own);
            }

            keys = found.ToArray();
            return entities;
        }

        /// <summary>Puts entities at the front or the back, or over or under a target, and gives the sort handles out again in the new order.</summary>
        private void Reorder(IEnumerable<DxfEntity> entities, DxfEntity target, bool over)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            DxfFile file = InFile("be reordered");
            IReadOnlyList<DxfEntity> order = GetFullDrawOrder();
            List<DxfEntity> unordered = Entities(out ulong[] keys);
            var moving = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var mine = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfEntity entity in unordered)
            {
                if (entity.Handle != null)
                {
                    mine.Add(entity.Handle);
                }
            }

            foreach (DxfEntity entity in entities)
            {
                if (entity == null || entity.Handle == null || !mine.Contains(entity.Handle))
                {
                    throw new ArgumentException(
                        "An entity to reorder is null or not of the block " + (_block.Name ?? string.Empty) + ".",
                        nameof(entities)
                    );
                }

                moving.Add(entity.Handle);
            }

            if (
                target != null
                && (target.Handle == null || !mine.Contains(target.Handle) || moving.Contains(target.Handle))
            )
            {
                throw new ArgumentException(
                    "The entity to reorder against is not of the block "
                        + (_block.Name ?? string.Empty)
                        + ", or is among those moved.",
                    nameof(target)
                );
            }

            if (moving.Count == 0)
            {
                return;
            }

            var rest = new List<DxfEntity>();
            var moved = new List<DxfEntity>();

            foreach (DxfEntity entity in order)
            {
                (moving.Contains(entity.Handle) ? moved : rest).Add(entity);
            }

            int at;

            if (target == null)
            {
                at = over ? rest.Count : 0;
            }
            else
            {
                at =
                    rest.FindIndex(e => string.Equals(e.Handle, target.Handle, StringComparison.OrdinalIgnoreCase))
                    + (over ? 1 : 0);
            }

            rest.InsertRange(at, moved);
            Array.Sort(keys);
            var groups = new List<KeyValuePair<int, string>>();

            for (int i = 0; i < rest.Count; i++)
            {
                if (!TryHex(rest[i].Handle, out ulong own) || own != keys[i])
                {
                    groups.Add(new KeyValuePair<int, string>(331, rest[i].Handle));
                    groups.Add(new KeyValuePair<int, string>(5, keys[i].ToString("X", CultureInfo.InvariantCulture)));
                }
            }

            file.ReplaceOwn(Item, 0, PairCodes, groups, Order, false);
        }

        private static bool TryHex(string text, out ulong value)
        {
            value = 0UL;
            return text != null
                && ulong.TryParse(text.Trim(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);
        }
    }
}
