using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A GROUP of a DXF file, as AutoCAD's <c>Group</c>: a named list of entities of the drawing, picked together where it is
    /// selectable, kept in the group dictionary (<see cref="DxfDatabase.GroupDictionary"/>, ACAD_GROUP) under its name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>db.GroupDictionary.SetAt("BEAMS", new DxfGroup("Beams of grid A", true))</c> adds the group BEAMS, and
    /// <c>group.Append(beam)</c> puts an entity in it: the entity's handle is written last among the group's (340), and the
    /// group's handle into the entity's list of reactors (330 in <c>{ACAD_REACTORS</c>), as AutoCAD writes a member. Taken out,
    /// the entity leaves the list and the group its reactors.
    /// </para>
    /// <para>
    /// The members are the entities the group lists that the drawing has: an entity erased leaves the group at once, as it
    /// leaves AutoCAD's, and the file saved lists it no longer. A group left empty is kept, named or anonymous, as AutoCAD
    /// 2021 keeps it whatever emptied it: its last member erased, <see cref="Clear"/>, <see cref="RemoveAt(int)"/>, or never
    /// given one; the groups G_ERASE, G_REMOVE and G_NEW_EMPTY so emptied, and the anonymous *A1, *A2 and *A3, are all in
    /// the drawing it saves, each of no member.
    /// </para>
    /// <para>
    /// The look of every member is set at once by <see cref="SetColorIndex"/>, <see cref="SetLayer"/> and the others: a
    /// group of a line and a circle given the colour 1 has both red.
    /// </para>
    /// </remarks>
    public sealed class DxfGroup : DxfDBObject
    {
        // The groups of AcDbGroup in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 300, 70, 71, 340 });

        private static readonly int[] MemberCodes = { 340 };

        private string _description = string.Empty;
        private List<DxfItem> _kept;
        private HashSet<DxfItem> _keptSet;
        private int _keptEnd = -1;
        private byte[] _keptBytes;
        private int _keptStart;
        private int _keptRemoved;
        private bool _selectable = true;
        private bool _anonymous;

        /// <summary>
        /// Initializes a group made in code, with no description, selectable, to be added to the group dictionary by
        /// <see cref="DxfDBDictionary.SetAt"/>, as AutoCAD's <c>Group()</c> is.
        /// </summary>
        public DxfGroup()
        {
        }

        /// <summary>
        /// Initializes a group made in code with its description, as AutoCAD's <c>Group(description, selectable)</c>:
        /// <c>new DxfGroup("Beams of grid A", true)</c>.
        /// </summary>
        /// <param name="description">The description (group 300).</param>
        /// <param name="selectable">Whether the group is picked as a whole (group 71).</param>
        /// <exception cref="ArgumentNullException">Thrown when the description is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the description has a line break in it.</exception>
        public DxfGroup(string description, bool selectable)
        {
            _description = CheckedText(description, nameof(description));
            _selectable = selectable;
        }

        internal DxfGroup(DxfItem item)
            : base(item)
        {
        }

        /// <summary>
        /// Gets or sets the name of the group, as AutoCAD's <c>Name</c>: the name of its entry in the dictionary that holds it,
        /// such as <c>BEAMS</c>, or <c>*A1</c> for an anonymous group; null for a group in no dictionary.
        /// </summary>
        /// <value>
        /// Set, the entry renamed, as <see cref="DxfDBDictionary.SetName"/> renames one; an anonymous group given a name is
        /// anonymous no longer (group 70 of 0). The name the group has changes nothing.
        /// </value>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name set is one AutoCAD does not take, or names another entry of
        /// the dictionary.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the group is in no dictionary, or is erased.</exception>
        public string Name
        {
            get => Dictionary()?.NameOf(this);

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (string.Equals(value, Name, StringComparison.Ordinal))
                {
                    return;
                }

                DxfSymbolTable.CheckName(value, nameof(value));
                InFile("be renamed");
                DxfDBDictionary dictionary = Dictionary() ?? throw new InvalidOperationException("The group " + Handle + " is in no dictionary to be named in.");
                dictionary.Rename(this, value);

                if (IsAnonymous)
                {
                    SetFlag(70, false);
                }
            }
        }

        /// <summary>Gets or sets the description of the group (group 300), as AutoCAD's <c>Description</c>; empty where the record gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the description set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the description set has a line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the group is erased.</exception>
        public string Description
        {
            get => Item == null ? _description : DxfReader.Decode(Item.File.ValueOf(Item, 300) ?? string.Empty);

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (string.Equals(value, Description, StringComparison.Ordinal))
                {
                    return;
                }

                CheckedText(value, nameof(value));

                if (Item == null)
                {
                    _description = value;
                    return;
                }

                InFile("be described").SetValues(Item, new[] { new KeyValuePair<int, string>(300, value) }, Order, false);
            }
        }

        /// <summary>
        /// Gets or sets whether the group is picked as a whole when one of its entities is (group 71 of 1), as AutoCAD's
        /// <c>Selectable</c>; true where the record gives none.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the group is erased.</exception>
        public bool Selectable
        {
            get => Item == null ? _selectable : Flag(71, true);

            set
            {
                if (value == Selectable)
                {
                    return;
                }

                if (Item == null)
                {
                    _selectable = value;
                    return;
                }

                SetFlag(71, value);
            }
        }

        /// <summary>Takes the flag of an anonymous group off (group 70 of 0), the group being given a name; nothing for one that is not anonymous.</summary>
        internal void Named()
        {
            if (Item != null && IsAnonymous)
            {
                SetFlag(70, false);
            }
        }

        /// <summary>Gets whether the group is anonymous (group 70 of 1), as AutoCAD's <c>IsAnonymous</c>: named <c>*A1</c>, <c>*A2</c> and on by the drawing.</summary>
        public bool IsAnonymous => Item == null ? _anonymous : Flag(70, false);

        /// <summary>
        /// Gets whether the group cannot be reached, as AutoCAD's <c>IsNotAccessible</c>: AutoCAD says so of a group of an
        /// external reference loaded into the drawing; the groups of a DXF file are its own, so false.
        /// </summary>
        public bool IsNotAccessible => false;

        /// <summary>
        /// Makes the group anonymous, as AutoCAD's <c>SetAnonymous</c>: its flag set (group 70 of 1) and its entry renamed the
        /// first <c>*A1</c>, <c>*A2</c> and on its dictionary has not; nothing where it is anonymous. A group made in code is made
        /// anonymous when it is added.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the group is erased.</exception>
        public void SetAnonymous()
        {
            if (IsAnonymous)
            {
                return;
            }

            if (Item == null)
            {
                _anonymous = true;
                return;
            }

            InFile("be made anonymous");
            DxfDBDictionary dictionary = Dictionary();
            SetFlag(70, true);
            dictionary?.Rename(this, dictionary.AnonymousName());
        }

        /// <summary>Gets how many entities the group has, as AutoCAD's <c>NumEntities</c>: those it lists that the drawing has, an erased one not counted.</summary>
        public int NumEntities => Members().Count;

        /// <summary>
        /// Gets the entities of the group in its order, as AutoCAD's <c>GetAllEntityIds</c> gives their ids: the entities
        /// themselves, those it lists that the drawing has.
        /// </summary>
        /// <returns>The entities, a new array.</returns>
        public DxfEntity[] GetAllEntityIds() => Members().ToArray();

        /// <summary>Gets whether an entity is in the group, as AutoCAD's <c>Has</c>.</summary>
        /// <param name="entity">The entity.</param>
        /// <returns>Whether it is.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        public bool Has(DxfEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            return IndexOf(Members(), entity) >= 0;
        }

        /// <summary>Gets where an entity stands in the group, as AutoCAD's <c>GetIndex</c>: 0 for the first.</summary>
        /// <param name="entity">The entity.</param>
        /// <returns>Its index.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the entity is not in the group, as AutoCAD answers eInvalidInput.</exception>
        public int GetIndex(DxfEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            int at = IndexOf(Members(), entity);

            if (at < 0)
            {
                throw new ArgumentException("The " + entity + " is not in the group.", nameof(entity));
            }

            return at;
        }

        /// <summary>Puts an entity last in the group, as AutoCAD's <c>Append</c>, the group written into its reactors.</summary>
        /// <param name="entity">The entity, of the group's drawing and not in the group.</param>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the entity is in the group already, as AutoCAD answers eAlreadyInGroup,
        /// or is not an entity of the group's drawing: erased, of another file, made in code and not added, or a record that
        /// follows another, as a VERTEX.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the group is in no file, or is erased.</exception>
        public void Append(DxfEntity entity) => InsertAt(int.MaxValue, Single(entity, nameof(entity)));

        /// <summary>
        /// Puts entities last in the group, in their order, as AutoCAD's <c>Append</c> of a collection, in one writing of the
        /// group: for a large group, the way to fill it. One at a time is fine for groups as drawings have them, 8 000 entities in
        /// half a second, but each copies the group's record, so that 32 000 take some 15 seconds where this takes a few hundredths.
        /// </summary>
        /// <param name="entities">The entities.</param>
        /// <exception cref="ArgumentNullException">Thrown when the entities or one of them are null.</exception>
        /// <exception cref="ArgumentException">Thrown when one is in the group already or given twice, or is not an entity of the
        /// group's drawing; none is put in then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the group is in no file, or is erased.</exception>
        public void Append(IEnumerable<DxfEntity> entities) => InsertAt(int.MaxValue, entities);

        /// <summary>Puts an entity first in the group, as AutoCAD's <c>Prepend</c>.</summary>
        /// <param name="entity">The entity, of the group's drawing and not in the group.</param>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the entity is in the group already, or is not an entity of the group's drawing.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the group is in no file, or is erased.</exception>
        public void Prepend(DxfEntity entity) => InsertAt(0, Single(entity, nameof(entity)));

        /// <summary>Puts entities first in the group, in their order, as AutoCAD's <c>Prepend</c> of a collection.</summary>
        /// <param name="entities">The entities.</param>
        /// <exception cref="ArgumentNullException">Thrown when the entities or one of them are null.</exception>
        /// <exception cref="ArgumentException">Thrown when one is in the group already or given twice, or is not an entity of the group's drawing.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the group is in no file, or is erased.</exception>
        public void Prepend(IEnumerable<DxfEntity> entities) => InsertAt(0, entities);

        /// <summary>Puts an entity in the group before the one at an index, as AutoCAD's <c>InsertAt</c>; the count of the group puts it last.</summary>
        /// <param name="index">The index, from 0 to <see cref="NumEntities"/>.</param>
        /// <param name="entity">The entity, of the group's drawing and not in the group.</param>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside 0 to <see cref="NumEntities"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when the entity is in the group already, or is not an entity of the group's drawing.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the group is in no file, or is erased.</exception>
        public void InsertAt(int index, DxfEntity entity)
        {
            CheckIndex(index, Members().Count, nameof(index));
            InsertAt(index, Single(entity, nameof(entity)));
        }

        /// <summary>Puts entities in the group, in their order, before the one at an index, as AutoCAD's <c>InsertAt</c> of a collection.</summary>
        /// <param name="index">The index, from 0 to <see cref="NumEntities"/>.</param>
        /// <param name="entities">The entities.</param>
        /// <exception cref="ArgumentNullException">Thrown when the entities or one of them are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside 0 to <see cref="NumEntities"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when one is in the group already or given twice, or is not an entity of the group's drawing.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the group is in no file, or is erased.</exception>
        public void InsertAt(int index, IEnumerable<DxfEntity> entities)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            DxfFile file = InFile("be given entities");

            // Appended last to a group whose members are kept: the new groups 340 put in after the last, the record not read again.
            if (index == int.MaxValue && Kept())
            {
                AppendKept(entities, file);
                return;
            }

            List<DxfEntity> members = Members();

            if (index != int.MaxValue)
            {
                CheckIndex(index, members.Count, nameof(index));
            }

            var added = new List<DxfEntity>();
            var taken = new HashSet<DxfItem>();
            members.ForEach(m => taken.Add(m.Item));

            foreach (DxfEntity entity in entities)
            {
                CheckMember(entity, file, nameof(entities));

                if (!taken.Add(entity.Item))
                {
                    throw new ArgumentException("The " + entity + " is in the group already, as AutoCAD answers eAlreadyInGroup.", nameof(entities));
                }

                added.Add(entity);
            }

            if (added.Count == 0)
            {
                return;
            }

            members.InsertRange(Math.Min(index, members.Count), added);
            Write(members);

            foreach (DxfEntity entity in added)
            {
                file.SetReactor(entity.Item, Handle, true);
            }
        }

        /// <summary>Takes an entity out of the group, as AutoCAD's <c>Remove</c>, and the group out of its reactors.</summary>
        /// <param name="entity">The entity.</param>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the entity is not in the group.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the group is in no file, or is erased.</exception>
        public void Remove(DxfEntity entity) => Remove(Single(entity, nameof(entity)));

        /// <summary>Takes entities out of the group, as AutoCAD's <c>Remove</c> of a collection.</summary>
        /// <param name="entities">The entities.</param>
        /// <exception cref="ArgumentNullException">Thrown when the entities or one of them are null.</exception>
        /// <exception cref="ArgumentException">Thrown when one is not in the group, or is given twice; none is taken out then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the group is in no file, or is erased.</exception>
        public void Remove(IEnumerable<DxfEntity> entities) => RemoveAt(0, entities);

        /// <summary>Takes the entity at an index out of the group, as AutoCAD's <c>RemoveAt</c>.</summary>
        /// <param name="index">The index, from 0 to <see cref="NumEntities"/> less one.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside the group.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the group is in no file, or is erased.</exception>
        public void RemoveAt(int index)
        {
            List<DxfEntity> members = Members();
            CheckIndex(index, members.Count - 1, nameof(index));
            RemoveAt(index, new[] { members[index] });
        }

        /// <summary>
        /// Takes entities out of the group, each found at or after an index, as AutoCAD's <c>RemoveAt</c> of an index and a
        /// collection.
        /// </summary>
        /// <param name="index">The index the search for each begins at.</param>
        /// <param name="entities">The entities.</param>
        /// <exception cref="ArgumentNullException">Thrown when the entities or one of them are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside 0 to <see cref="NumEntities"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when one is not in the group at or after the index, or is given twice; none
        /// is taken out then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the group is in no file, or is erased.</exception>
        public void RemoveAt(int index, IEnumerable<DxfEntity> entities)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            DxfFile file = InFile("lose entities");
            List<DxfEntity> members = Members();
            CheckIndex(index, members.Count, nameof(index));
            var taken = new List<DxfEntity>();

            foreach (DxfEntity entity in entities)
            {
                if (entity == null)
                {
                    throw new ArgumentNullException(nameof(entities));
                }

                int at = IndexOf(members, entity);

                if (at < index || IndexOf(taken, entity) >= 0)
                {
                    throw new ArgumentException("The " + entity + " is not in the group at or after " + index.ToString(CultureInfo.InvariantCulture) + ", or is given twice.", nameof(entities));
                }

                taken.Add(entity);
            }

            if (taken.Count == 0)
            {
                return;
            }

            foreach (DxfEntity entity in taken)
            {
                members.RemoveAt(IndexOf(members, entity));
            }

            Write(members);

            foreach (DxfEntity entity in taken)
            {
                file.SetReactor(entity.Item, Handle, false);
            }
        }

        /// <summary>Puts an entity in the place of one of the group, as AutoCAD's <c>Replace</c>; nothing where the two are the same.</summary>
        /// <param name="oldEntity">The entity of the group.</param>
        /// <param name="newEntity">The entity that takes its place, of the group's drawing and not in the group.</param>
        /// <exception cref="ArgumentNullException">Thrown when an entity is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the old entity is not in the group, or the new one is in it already or is
        /// not an entity of the group's drawing.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the group is in no file, or is erased.</exception>
        public void Replace(DxfEntity oldEntity, DxfEntity newEntity)
        {
            if (oldEntity == null)
            {
                throw new ArgumentNullException(nameof(oldEntity));
            }

            if (newEntity == null)
            {
                throw new ArgumentNullException(nameof(newEntity));
            }

            if (ReferenceEquals(oldEntity.Item, newEntity.Item))
            {
                return;
            }

            DxfFile file = InFile("be changed");
            List<DxfEntity> members = Members();
            int at = IndexOf(members, oldEntity);

            if (at < 0)
            {
                throw new ArgumentException("The " + oldEntity + " is not in the group.", nameof(oldEntity));
            }

            CheckMember(newEntity, file, nameof(newEntity));

            if (IndexOf(members, newEntity) >= 0)
            {
                throw new ArgumentException("The " + newEntity + " is in the group already, as AutoCAD answers eAlreadyInGroup.", nameof(newEntity));
            }

            members[at] = newEntity;
            Write(members);
            file.SetReactor(oldEntity.Item, Handle, false);
            file.SetReactor(newEntity.Item, Handle, true);
        }

        /// <summary>Turns the order of the group round, the last first, as AutoCAD's <c>Reverse</c>; nothing for a group of fewer than two.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the group is in no file, or is erased.</exception>
        public void Reverse()
        {
            InFile("be reordered");
            List<DxfEntity> members = Members();
            members.Reverse();
            Write(members);
        }

        /// <summary>
        /// Moves a run of the group's entities to another place in it, as AutoCAD's <c>Transfer</c>: the run of
        /// <paramref name="numItems"/> from <paramref name="fromIndex"/> taken out and put back so that its first stands at
        /// <paramref name="toIndex"/>, the others keeping their order. A group of A, B, C, D given (0, 2, 2) is C, D, A, B.
        /// </summary>
        /// <param name="fromIndex">The index of the run's first entity.</param>
        /// <param name="toIndex">The index its first entity has afterwards, from 0 to <see cref="NumEntities"/> less the run's length.</param>
        /// <param name="numItems">How many entities the run has, nought or more.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the run is not within the group, or the place is not.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the group is in no file, or is erased.</exception>
        public void Transfer(int fromIndex, int toIndex, int numItems)
        {
            InFile("be reordered");
            List<DxfEntity> members = Members();

            if (numItems < 0 || fromIndex < 0 || fromIndex > members.Count - numItems)
            {
                throw new ArgumentOutOfRangeException(nameof(fromIndex), fromIndex, "The run of " + numItems.ToString(CultureInfo.InvariantCulture) + " from " + fromIndex.ToString(CultureInfo.InvariantCulture) + " is not within the group of " + members.Count.ToString(CultureInfo.InvariantCulture) + ".");
            }

            if (toIndex < 0 || toIndex > members.Count - numItems)
            {
                throw new ArgumentOutOfRangeException(nameof(toIndex), toIndex, "The run's first entity stands from 0 to " + (members.Count - numItems).ToString(CultureInfo.InvariantCulture) + ".");
            }

            List<DxfEntity> run = members.GetRange(fromIndex, numItems);
            members.RemoveRange(fromIndex, numItems);
            members.InsertRange(toIndex, run);
            Write(members);
        }

        /// <summary>
        /// Takes every entity out of the group, as AutoCAD's <c>Clear</c>, and the group out of their reactors; the group is
        /// kept, empty, named or anonymous, as AutoCAD 2021 keeps it.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the group is in no file, or is erased.</exception>
        public void Clear() => RemoveAt(0, Members());

        /// <summary>Gives every entity of the group a colour, as AutoCAD's <c>SetColor</c>, as <see cref="DxfEntity.Color"/> sets one.</summary>
        /// <param name="color">The colour: by layer, by block, of the index or a true colour.</param>
        /// <exception cref="ArgumentNullException">Thrown when the colour is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour is of another kind; nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the group is erased, or a true colour is set in a file older than R2004.</exception>
        public void SetColor(DxfColor color)
        {
            if (color == null)
            {
                throw new ArgumentNullException(nameof(color));
            }

            if (color.ColorMethod != DxfColorMethod.ByLayer && color.ColorMethod != DxfColorMethod.ByBlock && color.ColorMethod != DxfColorMethod.ByAci && color.ColorMethod != DxfColorMethod.ByColor)
            {
                throw new ArgumentException("An entity of DXF is by layer, by block, of the index or of a true colour: " + color.ColorMethod + " is none of them.", nameof(color));
            }

            List<DxfEntity> members = Checked("be given a colour");

            if (color.ColorMethod == DxfColorMethod.ByColor && members.Count > 0)
            {
                Item.File.Requires(1018, "true colours", "R2004 (AC1018)");
            }

            members.ForEach(e => e.Color = color);
        }

        /// <summary>Gives every entity of the group a colour of AutoCAD's index, as AutoCAD's <c>SetColorIndex</c>: 1 to 255, 0 for BYBLOCK and 256 for BYLAYER.</summary>
        /// <param name="color">The colour, from 0 to 256.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the colour is below 0 or above 256; nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the group is erased.</exception>
        public void SetColorIndex(int color)
        {
            if (color < 0 || color > 256)
            {
                throw new ArgumentOutOfRangeException(nameof(color), color, "A colour of the index is from 0 to 256.");
            }

            Checked("be given a colour").ForEach(e => e.ColorIndex = color);
        }

        /// <summary>Puts every entity of the group on a layer, as AutoCAD's <c>SetLayer</c>, as <see cref="DxfEntity.Layer"/> sets one: a layer the table does not list is added.</summary>
        /// <param name="value">The name of the layer.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is one AutoCAD does not take; nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the group is erased.</exception>
        public void SetLayer(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            DxfSymbolTable.CheckName(value, nameof(value));
            Checked("be put on a layer").ForEach(e => e.Layer = value);
        }

        /// <summary>Gives every entity of the group a linetype, as AutoCAD's <c>SetLinetype</c>, as <see cref="DxfEntity.Linetype"/> sets one.</summary>
        /// <param name="value">The name of a linetype of the file's linetype table, or BYLAYER or BYBLOCK.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the linetype is not in the file's linetype table; nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the group is erased.</exception>
        public void SetLinetype(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            List<DxfEntity> members = Checked("be given a linetype");

            if (members.Count > 0 && !Item.File.LinetypeListed(value))
            {
                throw new ArgumentException("The linetype " + value + " is not in the file's linetype table.", nameof(value));
            }

            members.ForEach(e => e.Linetype = value);
        }

        /// <summary>Gives every entity of the group a scale of its linetype, as AutoCAD's <c>SetLinetypeScale</c>.</summary>
        /// <param name="value">The scale, a finite number above nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale is not a finite number above nought; nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the group is erased.</exception>
        public void SetLinetypeScale(double value)
        {
            Guard.Positive(value, nameof(value), "A linetype scale has to be a finite number above nought.");
            Checked("be given a linetype scale").ForEach(e => e.LinetypeScale = value);
        }

        /// <summary>Shows or hides every entity of the group, as AutoCAD's <c>SetVisibility</c>, as <see cref="DxfEntity.Visible"/> sets it.</summary>
        /// <param name="value">Whether they are drawn.</param>
        /// <exception cref="InvalidOperationException">Thrown when the group is erased.</exception>
        public void SetVisibility(bool value) => Checked("be shown or hidden").ForEach(e => e.Visible = value);

        /// <summary>
        /// Erases the group, as AutoCAD's <c>Erase</c>: it is taken out of the file with the entry of its dictionary, its
        /// entities kept; their lists of reactors no longer hold it once the file is saved.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the group is in no file, or is erased.</exception>
        public override void Erase() => DxfDBDictionary.EraseFromOwner(this, InFile("be erased"));

        /// <summary>Gives the group as a line for debugging: its type, its handle, its name and how many entities it has.</summary>
        /// <returns>The line, such as <c>GROUP 2F BEAMS, 3 entities</c>.</returns>
        public override string ToString() => "GROUP " + (Handle ?? string.Empty) + " " + (Name ?? string.Empty) + ", " + NumEntities.ToString(CultureInfo.InvariantCulture) + " entities";

        /// <summary>The description a group made in code is written with.</summary>
        internal string MadeDescription => _description;

        /// <summary>Whether a group made in code is written selectable.</summary>
        internal bool MadeSelectable => _selectable;

        /// <summary>Whether a group made in code is written anonymous.</summary>
        internal bool MadeAnonymous => _anonymous;

        /// <summary>The dictionary that holds the group: its owner, where that is a dictionary that names it; null otherwise.</summary>
        private DxfDBDictionary Dictionary()
        {
            if (!IsResident)
            {
                return null;
            }

            DxfItem owner = Item.File.ObjectItem(OwnerHandle);
            return owner != null && OfObject(owner) is DxfDBDictionary dictionary && dictionary.NameOf(this) != null ? dictionary : null;
        }

        /// <summary>The entities the group lists that the drawing has, in its order, each the object that stands for it now.</summary>
        private List<DxfEntity> Members()
        {
            List<DxfItem> items = MemberItems();
            var members = new List<DxfEntity>(items.Count);

            foreach (DxfItem item in items)
            {
                members.Add(DxfEntity.Of(item));
            }

            return members;
        }

        /// <summary>
        /// The records of the entities the group lists that the drawing has, in its order, each once: read from the group's
        /// record and kept while that record is the same and nothing has been taken out of the file, so that appending one at a
        /// time does not read every member again.
        /// </summary>
        private List<DxfItem> MemberItems()
        {
            if (!IsResident || Item.File.Database == null || IsErased)
            {
                return new List<DxfItem>();
            }

            DxfFile file = Item.File;
            DxfFile.Rec record = file.RecordAt(Item, 0);

            if (_kept != null && ReferenceEquals(record.Bytes, _keptBytes) && record.Start == _keptStart && file.RemovedCount == _keptRemoved)
            {
                return new List<DxfItem>(_kept);
            }

            DxfDatabase database = file.Database;
            var items = new List<DxfItem>();

            // Each entity once, in the order the group first lists it: the set is for finding, the list gives the order.
            var seen = new HashSet<DxfItem>();

            foreach (DxfTypedValue group in file.ObjectGroupsOf(Item))
            {
                if (group.TypeCode == 340 && !file.WasRemoved(group.Value))
                {
                    DxfItem item = database.ItemByHandle(group.Value);

                    if (item != null && seen.Add(item))
                    {
                        items.Add(item);
                    }
                }
            }

            Keep(items);
            return new List<DxfItem>(items);
        }

        /// <summary>Keeps the members as read or written, with what the group's record and the file are then.</summary>
        private void Keep(List<DxfItem> items)
        {
            DxfFile.Rec record = Item.File.RecordAt(Item, 0);
            _kept = new List<DxfItem>(items);
            _keptSet = new HashSet<DxfItem>(items);
            _keptBytes = record.Bytes;
            _keptStart = record.Start;
            _keptRemoved = Item.File.RemovedCount;

            // Where a group 340 put last goes: after the last of them, else after the last group of the record's own part.
            var places = new List<int>();
            IReadOnlyList<DxfTypedValue> own = Item.File.OwnGroupsOf(Item, 0, places);
            _keptEnd = own.Count == 0 ? -1 : places[2 * own.Count - 1];

            for (int i = own.Count - 1; i >= 0; i--)
            {
                if (own[i].TypeCode == 340)
                {
                    _keptEnd = places[2 * i + 1];
                    break;
                }
            }
        }

        /// <summary>Whether the members kept are those of the group's record as it stands, nothing taken out of the file since.</summary>
        private bool Kept()
        {
            if (_kept == null || _keptEnd < 0)
            {
                return false;
            }

            DxfFile.Rec record = Item.File.RecordAt(Item, 0);
            return ReferenceEquals(record.Bytes, _keptBytes) && record.Start == _keptStart && Item.File.RemovedCount == _keptRemoved;
        }

        /// <summary>
        /// Appends entities after the members kept, as <see cref="InsertAt(int, IEnumerable{DxfEntity})"/> appends them: each
        /// checked first, then their groups 340 put in after the last of the record in one writing of its bytes, which are
        /// copied and not read again, and the group written into their reactors.
        /// </summary>
        private void AppendKept(IEnumerable<DxfEntity> entities, DxfFile file)
        {
            var added = new List<DxfEntity>();
            var taken = new HashSet<DxfItem>();

            foreach (DxfEntity entity in entities)
            {
                CheckMember(entity, file, nameof(entities));

                if (_keptSet.Contains(entity.Item) || !taken.Add(entity.Item))
                {
                    throw new ArgumentException("The " + entity + " is in the group already, as AutoCAD answers eAlreadyInGroup.", nameof(entities));
                }

                added.Add(entity);
            }

            if (added.Count == 0)
            {
                return;
            }

            var groups = new List<KeyValuePair<int, string>>(added.Count);
            added.ForEach(e => groups.Add(new KeyValuePair<int, string>(340, e.Handle)));
            int written = file.SpliceRecord(Item, 0, _keptEnd, _keptEnd, groups, false);
            DxfFile.Rec record = file.RecordAt(Item, 0);
            _keptBytes = record.Bytes;
            _keptStart = record.Start;
            _keptEnd += written;

            foreach (DxfEntity entity in added)
            {
                _kept.Add(entity.Item);
                _keptSet.Add(entity.Item);
                file.SetReactor(entity.Item, Handle, true);
            }
        }

        /// <summary>The members, checked to be those of a group in a file and not erased, for a change to all of them.</summary>
        private List<DxfEntity> Checked(string what)
        {
            InFile(what);
            return Members();
        }

        /// <summary>Writes the members as the group's groups 340, in their order; nothing where they are as they were.</summary>
        private void Write(List<DxfEntity> members)
        {
            var groups = new KeyValuePair<int, string>[members.Count];
            var items = new List<DxfItem>(members.Count);

            for (int i = 0; i < members.Count; i++)
            {
                groups[i] = new KeyValuePair<int, string>(340, members[i].Handle);
                items.Add(members[i].Item);
            }

            Item.File.ReplaceOwn(Item, 0, MemberCodes, groups, Order, false);
            Keep(items);
        }

        private bool Flag(int code, bool fallback)
            => int.TryParse(Item.File.ValueOf(Item, code)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value != 0 : fallback;

        private void SetFlag(int code, bool value)
            => InFile("be changed").SetValues(Item, new[] { new KeyValuePair<int, string>(code, value ? "1" : "0") }, Order, false);

        /// <summary>Checks that an entity can be a member: of this file, not erased, its own record with a handle.</summary>
        private static void CheckMember(DxfEntity entity, DxfFile file, string parameter)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(parameter);
            }

            if (!entity.IsResident || entity.IsErased || entity.Item.File != file || entity.Item.Parent != null || entity.Handle == null)
            {
                throw new ArgumentException("A group holds entities of its own drawing, not erased and not following another: the " + entity + " is not one.", parameter);
            }
        }

        private static IEnumerable<DxfEntity> Single(DxfEntity entity, string parameter)
            => entity == null ? throw new ArgumentNullException(parameter) : new[] { entity };

        private static int IndexOf(List<DxfEntity> entities, DxfEntity entity) => entities.FindIndex(e => ReferenceEquals(e.Item, entity.Item));

        private static void CheckIndex(int index, int most, string parameter)
        {
            if (index < 0 || index > most)
            {
                throw new ArgumentOutOfRangeException(parameter, index, "The index is from 0 to " + most.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }

        private static string CheckedText(string text, string parameter)
        {
            if (text == null)
            {
                throw new ArgumentNullException(parameter);
            }

            if (text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0)
            {
                throw new ArgumentException("The text is one line, with no line break in it.", parameter);
            }

            return text;
        }
    }
}
