using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// <c>entget</c>, <c>entmod</c>, <c>entmake</c>, <c>entdel</c>, <c>entnext</c>, <c>entlast</c>, <c>entupd</c> and
    /// <c>handent</c>: the entities of the drawing as lists of groups. An entity is named by its handle. A change made outside
    /// any command opens the undo step of the evaluation first (<see cref="CadCommandSession.OpenLispStep"/>) and is made through
    /// <see cref="CadDocument.ApplyEdit"/>, so that it is undone as any command's change is.
    /// </summary>
    internal static partial class LispDrawing
    {
        /// <summary>What the functions of one interpreter keep between calls: the objects <c>entdel</c> took out, so that a second <c>entdel</c> brings them back, and the names they came back by.</summary>
        internal sealed class DrawingState
        {
            private readonly Dictionary<string, string> _renamed = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase
            );

            /// <summary>The objects <c>entdel</c> erased, by their handle: what is needed to put one back, and the space it stood in.</summary>
            internal Dictionary<string, KeyValuePair<CadClipboardData, string>> Erased { get; } =
                new Dictionary<string, KeyValuePair<CadClipboardData, string>>(StringComparer.OrdinalIgnoreCase);

            /// <summary>The handle an object has now: an entity brought back by <c>entdel</c> has a new one, and the name it had still reaches it.</summary>
            internal string Resolve(string handle)
            {
                for (int i = 0; i < 16 && _renamed.TryGetValue(handle, out string now); i++)
                {
                    handle = now;
                }

                return handle;
            }

            /// <summary>Notes that the object that had a handle has another now.</summary>
            internal void Rename(string was, string now) => _renamed[was] = now;
        }

        private static readonly ConditionalWeakTable<LispInterpreter, DrawingState> States =
            new ConditionalWeakTable<LispInterpreter, DrawingState>();

        /// <summary>What the functions of an interpreter keep between calls.</summary>
        internal static DrawingState StateOf(LispInterpreter lisp) => States.GetValue(lisp, _ => new DrawingState());

        static partial void RegisterEntities(LispInterpreter lisp, CadCommandSession session)
        {
            DrawingState state = StateOf(lisp);

            lisp.Define("ENTGET", 1, 2, a => EntGet(lisp, session, state, a));
            lisp.Define("ENTMOD", 1, 1, a => lisp.OnOwner(() => EntMod(session, state, a[0])));
            lisp.Define("ENTMAKE", 1, 1, a => lisp.OnOwner(() => EntMake(session, a[0])));
            lisp.Define("ENTDEL", 1, 1, a => EntDel(lisp, session, state, a[0]));
            lisp.Define("ENTNEXT", 0, 1, a => EntNext(lisp, session, state, a));
            lisp.Define("ENTLAST", 0, 0, a => lisp.OnOwner(() => EntLast(session)));
            lisp.Define("ENTUPD", 1, 1, a => EntUpd(lisp, session, state, a[0]));
            lisp.Define("HANDENT", 1, 1, a => HandEnt(lisp, session, state, LispInterpreter.Str(a[0])));
        }

        /// <summary>The name an argument has to be: <c>bad argument type: lentityp nil</c> for anything else.</summary>
        internal static LispEntityName NameOf(object value) =>
            value as LispEntityName ?? throw LispInterpreter.TypeError("lentityp", value);

        #region Finding

        /// <summary>
        /// The record of the object a handle names: an entity of a space, one of a block or a record that follows an entity (a
        /// vertex), or a record of a table; null for none or one erased. Owner thread only.
        /// </summary>
        internal static DxfItem FindItem(CadDocument document, string handle)
        {
            if (string.IsNullOrEmpty(handle))
            {
                return null;
            }

            DxfEntity entity = document.FindEntity(handle);

            if (entity != null)
            {
                return entity.Item;
            }

            DxfDatabase database = document.Database;

            foreach (string[] names in TableNames)
            {
                foreach (DxfItem record in database.File.TableItems(names[1]))
                {
                    if (!record.IsRemoved && SameHandle(record.Handle, handle))
                    {
                        return record;
                    }
                }
            }

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                if (IndexOf(block).Map.TryGetValue(handle, out KeyValuePair<int, int> at))
                {
                    DxfItem main = IndexOf(block).Items[at.Key];
                    DxfItem found = at.Value == 0 ? main : main.File.FollowerOf(main, at.Value);

                    if (!found.IsRemoved && SameHandle(found.Handle, handle))
                    {
                        return found;
                    }
                }
            }

            // A record that follows an entity and was added since the index of its block was made.
            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                foreach (DxfItem item in Items(block))
                {
                    DxfFile file = item.File;

                    for (int i = 1; i < file.RecordCount(item); i++)
                    {
                        DxfItem follower = file.FollowerOf(item, i);

                        if (SameHandle(follower.Handle, handle))
                        {
                            return follower;
                        }
                    }
                }
            }

            return null;
        }

        private static bool SameHandle(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        /// <summary>Where the entities of a block are: its records in order, and for each handle (of an entity or a record that follows one) the entity's place and which of its records it is.</summary>
        internal sealed class BlockIndex
        {
            internal DxfSpace Space { get; set; }

            internal int Version { get; set; }

            internal IReadOnlyList<DxfItem> Items { get; set; }

            internal int Count { get; set; }

            internal bool HasColumns { get; set; }

            internal Dictionary<string, KeyValuePair<int, int>> Map { get; set; }
        }

        private static readonly ConditionalWeakTable<DxfBlockTableRecord, BlockIndex> Indexes =
            new ConditionalWeakTable<DxfBlockTableRecord, BlockIndex>();

        /// <summary>
        /// The index of a block, made again where entities were added to it or taken out since: a walk of the entities of a
        /// block of 20 000, one <c>entnext</c> after another, would search the block from its start each time otherwise.
        /// </summary>
        internal static BlockIndex IndexOf(DxfBlockTableRecord block)
        {
            // The walk of a block looks through it for the columns of an MTEXT: the list of its entities, and its version, are asked first.
            DxfSpace now = block.EntitySpace;

            if (
                now != null
                && Indexes.TryGetValue(block, out BlockIndex current)
                && ReferenceEquals(current.Space, now)
                && current.Version == now.Version
                && current.Count == now.Count
                && !current.HasColumns
            )
            {
                return current;
            }

            IReadOnlyList<DxfItem> items = block.WalkedItems(out DxfSpace space, out int version, out bool hasColumns);

            var map = new Dictionary<string, KeyValuePair<int, int>>(items.Count, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < items.Count; i++)
            {
                DxfItem item = items[i];

                if (item.IsRemoved || string.IsNullOrEmpty(item.Handle))
                {
                    continue;
                }

                map[item.Handle] = new KeyValuePair<int, int>(i, 0);
                DxfFile file = item.File;
                int records = file.RecordCount(item);

                for (int r = 1; r < records; r++)
                {
                    string followed = file.FollowerOf(item, r).Handle;

                    if (!string.IsNullOrEmpty(followed))
                    {
                        map[followed] = new KeyValuePair<int, int>(i, r);
                    }
                }
            }

            var made = new BlockIndex
            {
                Space = space,
                Version = version,
                Items = items,
                Count = hasColumns ? -1 : items.Count,
                HasColumns = hasColumns,
                Map = map,
            };
            Indexes.Remove(block);
            Indexes.Add(block, made);
            return made;
        }

        /// <summary>The records of the entities of a block, in the order they were made, those erased left out.</summary>
        internal static IEnumerable<DxfItem> Items(DxfBlockTableRecord block)
        {
            IReadOnlyList<DxfItem> items = block.WalkedItems(out _, out _, out _);

            for (int i = 0; i < items.Count; i++)
            {
                if (!items[i].IsRemoved && !string.IsNullOrEmpty(items[i].Handle))
                {
                    yield return items[i];
                }
            }
        }

        #endregion

        #region entget

        private static object EntGet(LispInterpreter lisp, CadCommandSession session, DrawingState state, object[] a)
        {
            LispEntityName name = NameOf(a[0]);
            List<string> applications = null;

            if (a.Length > 1 && a[1] != null)
            {
                applications = new List<string>();

                foreach (object item in LispInterpreter.ItemsOf(a[1]))
                {
                    applications.Add(LispInterpreter.Str(item));
                }
            }

            return lisp.OnOwner(() => EntGet(session, state, name, applications));
        }

        /// <summary>The list <c>entget</c> gives for an object: nil where the drawing has none by that name.</summary>
        private static object EntGet(
            CadCommandSession session,
            DrawingState state,
            LispEntityName name,
            List<string> applications
        )
        {
            string handle = state.Resolve(name.Handle);
            DxfItem item = FindItem(session.Document, handle);

            if (item == null)
            {
                return null;
            }

            IReadOnlyList<DxfTypedValue> groups = item.Groups;
            List<KeyValuePair<int, object>> pairs = LispEntityData.Read(groups, item.Type);

            if (item.Parent == null && item.Space != null)
            {
                CadSpace space = session.Document.FindSpace(item.Handle);
                string layer = pairs.Find(p => p.Key == 8).Value as string;
                pairs = LispEntityData.Complete(
                    item.Type,
                    pairs,
                    item.File.OwnerOf(item),
                    space?.Name ?? "Model",
                    space != null && !space.IsModel,
                    layer
                );
            }
            else
            {
                // A record of a table has the owner before the handle, as an entity does in AutoCAD's list.
                int handleAt = pairs.FindIndex(p => p.Key == 5);

                if (handleAt >= 0 && handleAt + 1 < pairs.Count && pairs[handleAt + 1].Key == 330)
                {
                    KeyValuePair<int, object> owner = pairs[handleAt + 1];
                    pairs[handleAt + 1] = pairs[handleAt];
                    pairs[handleAt] = owner;
                }
            }

            List<KeyValuePair<string, List<KeyValuePair<int, object>>>> extended = null;

            if (applications != null && applications.Count > 0)
            {
                extended = new List<KeyValuePair<string, List<KeyValuePair<int, object>>>>();

                foreach (var application in LispEntityData.ReadExtended(groups))
                {
                    if (
                        applications.Contains("*")
                        || applications.Exists(n =>
                            string.Equals(n, application.Key, StringComparison.OrdinalIgnoreCase)
                        )
                    )
                    {
                        extended.Add(application);
                    }
                }
            }

            return LispEntityData.ToList(new LispEntityName(handle), item.Type, pairs, extended);
        }

        #endregion

        #region entlast, entnext, handent, entupd

        /// <summary>The name of the last entity made of the current space, nil for none.</summary>
        private static object EntLast(CadCommandSession session)
        {
            IReadOnlyList<DxfItem> items = IndexOf(session.Document.WorkingSpace.Block).Items;

            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (!items[i].IsRemoved && !string.IsNullOrEmpty(items[i].Handle))
                {
                    return new LispEntityName(items[i].Handle);
                }
            }

            return null;
        }

        private static object EntNext(LispInterpreter lisp, CadCommandSession session, DrawingState state, object[] a)
        {
            LispEntityName after = a.Length > 0 ? NameOf(a[0]) : null;
            return lisp.OnOwner(() => EntNext(session, state, after));
        }

        /// <summary>
        /// The entity after the one named in the space it is in, the records that follow a polyline or a block reference with
        /// attributes (its vertices, its attributes, its end) after it; with no name the first of the current space. Nil after the
        /// last.
        /// </summary>
        private static object EntNext(CadCommandSession session, DrawingState state, LispEntityName after)
        {
            CadDocument document = session.Document;

            if (after == null)
            {
                foreach (DxfItem item in Items(document.WorkingSpace.Block))
                {
                    return new LispEntityName(item.Handle);
                }

                return null;
            }

            string handle = state.Resolve(after.Handle);
            DxfItem found = FindItem(document, handle);

            if (found == null)
            {
                return null;
            }

            DxfItem main = found.Parent ?? found;
            DxfBlockTableRecord block = document.FindSpace(main.Handle)?.Block
                ?? (main.Space == null ? null : DxfEntity.Of(main)?.Owner as DxfBlockTableRecord);

            if (block == null)
            {
                return null;
            }

            BlockIndex index = IndexOf(block);

            if (!index.Map.TryGetValue(main.Handle, out KeyValuePair<int, int> at))
            {
                return null;
            }

            // The records that follow the entity come first, then the next entity of the block.
            DxfFile file = main.File;
            int record = found.Parent == null ? 0 : found.Index;

            if (record + 1 < file.RecordCount(main))
            {
                return new LispEntityName(file.FollowerOf(main, record + 1).Handle);
            }

            for (int i = at.Key + 1; i < index.Items.Count; i++)
            {
                DxfItem item = index.Items[i];

                if (!item.IsRemoved && !string.IsNullOrEmpty(item.Handle))
                {
                    return new LispEntityName(item.Handle);
                }
            }

            return null;
        }

        private static object EntUpd(LispInterpreter lisp, CadCommandSession session, DrawingState state, object value)
        {
            LispEntityName name = NameOf(value);
            return lisp.OnOwner(() =>
            {
                session.Document.Regen();
                return name;
            });
        }

        private static object HandEnt(
            LispInterpreter lisp,
            CadCommandSession session,
            DrawingState state,
            string text
        ) =>
            lisp.OnOwner(() =>
            {
                DxfItem item = FindItem(session.Document, state.Resolve(text.Trim()));
                return item == null ? null : new LispEntityName(item.Handle);
            });

        #endregion

        #region entdel

        private static object EntDel(LispInterpreter lisp, CadCommandSession session, DrawingState state, object value)
        {
            LispEntityName name = NameOf(value);
            return lisp.OnOwner(() => EntDel(session, state, name));
        }

        /// <summary>Erases the entity; where it is erased already and was erased by this function, brings it back.</summary>
        private static object EntDel(CadCommandSession session, DrawingState state, LispEntityName name)
        {
            CadDocument document = session.Document;
            string handle = state.Resolve(name.Handle);
            DxfEntity entity = document.FindEntity(handle);

            if (entity == null)
            {
                return state.Erased.TryGetValue(handle, out var kept) && Restore(session, state, handle, kept)
                    ? name
                    : null;
            }

            CadSpace space = document.FindSpace(handle);

            try
            {
                CadClipboardData data = CadCommands.CopyToClipboard(document, new[] { handle }, null);

                if (data != null)
                {
                    state.Erased[handle] = new KeyValuePair<CadClipboardData, string>(data, space?.Name);
                }
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                GeometryHelperLog.Warn("LispDrawing: the entity erased could not be kept to bring it back.", exception);
            }

            session.OpenLispStep();
            CadEditResult result = session.Apply(
                "ERASE",
                new[] { new CadEditRecord(CadEditRecordKind.Erased, handle) },
                s =>
                {
                    if (!s.IsHandled(handle))
                    {
                        s.Document.FindEntity(handle).Erase();
                    }
                }
            );

            return result.Succeeded ? name : null;
        }

        /// <summary>Puts an entity <c>entdel</c> erased back in the space it stood in, as OOPS does; it has a new handle, which the old name reaches.</summary>
        private static bool Restore(
            CadCommandSession session,
            DrawingState state,
            string handle,
            KeyValuePair<CadClipboardData, string> kept
        )
        {
            CadDocument document = session.Document;
            CadSpace space = Find(document, kept.Value);
            DxfDatabase fragment;

            try
            {
                fragment = DxfDatabase.Parse(kept.Key.Drawing);
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                GeometryHelperLog.Warn("LispDrawing: the entity erased could not be read back.", exception);
                return false;
            }

            var held = new List<DxfEntity>();

            foreach (DxfEntity entity in fragment.ModelSpace)
            {
                held.Add(entity);
            }

            string made = null;
            session.OpenLispStep();
            CadEditResult result = session.Apply("ERASE", null, s =>
            {
                foreach (DxfEntity copy in fragment.WblockCloneObjects(held, space.Block))
                {
                    if (!string.IsNullOrEmpty(copy.Handle) && !copy.IsErased)
                    {
                        s.Added(space, copy);
                        made = made ?? copy.Handle;
                    }
                }
            });

            if (!result.Succeeded || made == null)
            {
                return false;
            }

            state.Erased.Remove(handle);
            state.Rename(handle, made);
            return true;
        }

        private static CadSpace Find(CadDocument document, string name)
        {
            foreach (CadSpace space in document.Spaces)
            {
                if (string.Equals(space.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return space;
                }
            }

            return document.WorkingSpace;
        }

        #endregion

        #region entmod

        /// <summary>
        /// Changes an entity to the groups of a list, as <c>entmod</c> does: the list has the entity's name (group -1), and the groups
        /// it gives replace those of the same code, which the entity has in the order of its record, a group the entity lacks is put
        /// where AutoCAD's list has it. The groups AutoCAD derives are not taken: the handle, the owner, the space (67, 410) and the
        /// subclass markers. Gives the list, or nil where the list has no name, the entity is not there, its type is changed,
        /// or a value is of the wrong kind.
        /// </summary>
        private static object EntMod(CadCommandSession session, DrawingState state, object list)
        {
            if (!LispEntityData.TryRead(list, out List<KeyValuePair<int, object>> given))
            {
                return null;
            }

            int namePair = given.FindIndex(p => p.Key == -1);

            if (namePair < 0 || !(given[namePair].Value is LispEntityName name))
            {
                return null;
            }

            CadDocument document = session.Document;
            string handle = state.Resolve(name.Handle);
            DxfItem item = FindItem(document, handle);
            DxfItem owner = item?.Parent != null && item.Type == "ATTRIB" ? item.Parent : null;
            DxfEntity entity =
                item == null || (item.Space == null && owner == null) ? null : DxfEntity.Of(item) as DxfEntity;

            if (
                entity == null
                || (
                    item.Parent != null
                    && (owner == null || owner.Type != "INSERT" || owner.Parent != null || owner.Space == null)
                )
            )
            {
                return null;
            }

            int typePair = given.FindIndex(p => p.Key == 0);

            if (
                typePair >= 0
                && !(
                    given[typePair].Value is string type
                    && string.Equals(type, item.Type, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return null;
            }

            List<KeyValuePair<int, object>> record = LispEntityData.Read(item.Groups, item.Type);
            List<KeyValuePair<int, object>> changes = ChangesOf(given);

            if (!Fits(document, item.Type, changes))
            {
                return null;
            }

            LispEntityData.Apply(item.Type, record, changes);

            List<KeyValuePair<string, List<KeyValuePair<int, object>>>> extended = ExtendedOf(given);

            foreach (KeyValuePair<int, object> change in changes)
            {
                if (change.Key == 8)
                {
                    EnsureLayer(document, (string)change.Value);
                }
            }

            string failure = null;
            session.OpenLispStep();
            CadEditResult result = session.Apply(
                "ENTMOD",
                new[] { new CadEditRecord(CadEditRecordKind.Reshaped, owner?.Handle ?? handle) },
                s =>
                {
                    if (s.IsHandled(owner?.Handle ?? handle))
                    {
                        return;
                    }

                    // An attribute is written through its block reference, as the Properties palette writes it.
                    DxfEntity target =
                        owner == null ? s.Document.FindEntity(handle) : AttributeOf(s.Document, owner, handle);

                    if (target == null)
                    {
                        failure = "The attribute is not there.";
                        return;
                    }

                    if (target is DxfDimension dimension)
                    {
                        CadCommands.OwnPicture(s.Database, dimension);
                    }

                    target.Item.File.ReplaceTail(target.Item, 0, LispEntityData.ToGroups(target.Item.Type, record));

                    if (extended.Count > 0)
                    {
                        target.XData = ExtendedGroups(extended);
                    }

                    if (target is DxfDBText text)
                    {
                        AlignText(text);
                    }
                    else if (target is DxfDimension changed && !CadCommands.Redraw(changed))
                    {
                        failure = "The dimension cannot be drawn with the groups it was given.";
                    }
                }
            );

            return result.Succeeded && failure == null ? list : null;
        }

        /// <summary>The attribute of a block reference with a handle, found through the reference; null where it is not there or is erased.</summary>
        private static DxfAttributeReference AttributeOf(CadDocument document, DxfItem owner, string handle)
        {
            if (!(document.FindEntity(owner.Handle) is DxfBlockReference insert))
            {
                return null;
            }

            foreach (DxfAttributeReference attribute in insert.AttributeCollection)
            {
                if (!attribute.IsErased && string.Equals(attribute.Handle, handle, StringComparison.OrdinalIgnoreCase))
                {
                    return attribute;
                }
            }

            return null;
        }

        /// <summary>The groups of a list that change an entity: not its name, type, handle, owner, space, subclass markers or reactors.</summary>
        private static List<KeyValuePair<int, object>> ChangesOf(List<KeyValuePair<int, object>> given)
        {
            var changes = new List<KeyValuePair<int, object>>();
            int depth = 0;

            foreach (KeyValuePair<int, object> pair in given)
            {
                if (pair.Key == 102)
                {
                    depth += pair.Value is string text && text.StartsWith("{", StringComparison.Ordinal) ? 1 : -1;
                    continue;
                }

                if (depth > 0 || pair.Key == -1 || pair.Key == -3 || pair.Key == 0 || pair.Key == 5 || pair.Key == 330)
                {
                    continue;
                }

                if (pair.Key == 100 || pair.Key == 67 || pair.Key == 410)
                {
                    continue;
                }

                changes.Add(pair);
            }

            return changes;
        }

        /// <summary>The extended data of a list: the groups after -3, by application.</summary>
        private static List<KeyValuePair<string, List<KeyValuePair<int, object>>>> ExtendedOf(
            List<KeyValuePair<int, object>> given
        )
        {
            var applications = new List<KeyValuePair<string, List<KeyValuePair<int, object>>>>();
            int at = given.FindIndex(p => p.Key == -3);

            if (at < 0)
            {
                return applications;
            }

            foreach (object application in LispInterpreter.ItemsOf(given[at].Value))
            {
                List<object> entries = LispInterpreter.ItemsOf(application);

                if (entries.Count == 0 || !(entries[0] is string name))
                {
                    continue;
                }

                var groups = new List<KeyValuePair<int, object>>();

                for (int i = 1; i < entries.Count; i++)
                {
                    if (LispEntityData.TryRead(LispInterpreter.ListOf(new[] { entries[i] }), out var one))
                    {
                        groups.Add(one[0]);
                    }
                }

                applications.Add(new KeyValuePair<string, List<KeyValuePair<int, object>>>(name, groups));
            }

            return applications;
        }

        private static List<DxfTypedValue> ExtendedGroups(
            List<KeyValuePair<string, List<KeyValuePair<int, object>>>> applications
        )
        {
            var groups = new List<DxfTypedValue>();

            foreach (var application in applications)
            {
                groups.Add(new DxfTypedValue(1001, application.Key));

                foreach (KeyValuePair<int, string> group in LispEntityData.ToGroups("", application.Value))
                {
                    groups.Add(new DxfTypedValue(group.Key, group.Value));
                }
            }

            return groups;
        }

        /// <summary>Whether the layer, linetype and text style a list names are in the drawing (a layer that is not is made later, as AutoCAD makes one); false for a linetype or a text style that is not.</summary>
        private static bool Fits(CadDocument document, string type, List<KeyValuePair<int, object>> changes)
        {
            DxfDatabase database = document.Database;

            foreach (KeyValuePair<int, object> pair in changes)
            {
                string text = pair.Value as string;

                if (pair.Key == 8 && (text == null || text.Length == 0))
                {
                    return false;
                }

                if (
                    pair.Key == 6
                    && !string.Equals(text, "BYLAYER", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(text, "BYBLOCK", StringComparison.OrdinalIgnoreCase)
                    && !database.LinetypeTable.Has(text)
                )
                {
                    return false;
                }

                if (pair.Key == 7 && (type == "TEXT" || type == "MTEXT") && !database.TextStyleTable.Has(text))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Works out where the words of a text that is not left on its baseline start (its group 10) from the point it is held by
        /// (group 11), as AutoCAD does when an entity list gives a text a new alignment: the first point is the held point moved
        /// back by the width of the words for the centre (half) and the right (all) and the middle, and down by the height for
        /// the top (all of it), the middle (half of it) and the bottom (the depth of the ink below the baseline). A text held by
        /// both ends (aligned, fitted) keeps its first point. A text 2.5 high held at (50, 0) by its middle and left has its
        /// first point at (50, -1.25); the width comes from the drawing's fonts, nought where they do not measure the words.
        /// </summary>
        private static void AlignText(DxfDBText text)
        {
            int across = (int)text.HorizontalMode;
            int up = (int)text.VerticalMode;

            if ((across == 0 && up == 0) || ((across == 3 || across == 5) && up == 0))
            {
                return;
            }

            double height = text.Height;
            double width = 0.0;
            double depth = 0.0;

            if (
                (across == 1 || across == 2 || across == 4)
                && CadDimensionWords.TryMeasure(
                    text.TextStyle,
                    text.TextString,
                    height,
                    out double measured,
                    out _,
                    out double below
                )
            )
            {
                width = measured * text.WidthFactor;
                depth = below;
            }

            double along =
                across == 1 || across == 4 ? -width / 2.0
                : across == 2 ? -width
                : 0.0;
            double aside =
                across == 4 ? -height / 2.0
                : up == 1 ? depth
                : up == 2 ? -height / 2.0
                : up == 3 ? -height
                : 0.0;
            double cos = Math.Cos(text.Rotation);
            double sin = Math.Sin(text.Rotation);
            GeoPoint3 held = text.AlignmentPoint;
            GeoPoint3 first = text.Position;
            text.Position = new GeoPoint3(
                held.X + along * cos - aside * sin,
                held.Y + along * sin + aside * cos,
                first.Z
            );
        }

        /// <summary>Makes a layer that is not in the drawing, as AutoCAD does where a list names one: colour 7, linetype Continuous.</summary>
        private static void EnsureLayer(CadDocument document, string name)
        {
            DxfDatabase database = document.Database;

            if (!database.LayerTable.Has(name))
            {
                database.LayerTable.Add(new DxfLayerTableRecord { Name = name, ColorIndex = 7 });
            }
        }

        #endregion

        #region entmake

        private static readonly string[] Makeable =
        {
            "LINE",
            "POINT",
            "CIRCLE",
            "ARC",
            "ELLIPSE",
            "TEXT",
            "MTEXT",
            "LWPOLYLINE",
            "SOLID",
            "TRACE",
            "3DFACE",
            "RAY",
            "XLINE",
            "SPLINE",
            "INSERT",
        };

        /// <summary>The groups an entity cannot be made without.</summary>
        private static int[] RequiredOf(string type)
        {
            switch (type)
            {
                case "LINE":
                case "RAY":
                case "XLINE":
                    return new[] { 10, 11 };
                case "POINT":
                    return new[] { 10 };
                case "CIRCLE":
                    return new[] { 10, 40 };
                case "ARC":
                    return new[] { 10, 40, 50, 51 };
                case "ELLIPSE":
                    return new[] { 10, 11, 40 };
                case "TEXT":
                    return new[] { 10, 40, 1 };
                case "MTEXT":
                    return new[] { 10, 1 };
                case "LWPOLYLINE":
                    return new[] { 10 };
                case "SOLID":
                case "TRACE":
                case "3DFACE":
                    return new[] { 10, 11, 12 };
                case "SPLINE":
                    return new[] { 10 };
                case "INSERT":
                    return new[] { 2, 10 };
                default:
                    return new int[0];
            }
        }

        /// <summary>
        /// Makes an entity of a list of groups in the current space, as <c>entmake</c> does: the entity is made as the record of
        /// a small DXF and brought into the drawing as a paste does, so that the drawing's layers, linetypes and styles are the
        /// ones it uses (a layer that is not there is made, colour 7); the groups -1, 5 and 330 are ignored. Gives the list, or
        /// nil where the type cannot be made or a group it needs is missing.
        /// </summary>
        private static object EntMake(CadCommandSession session, object list)
        {
            if (!LispEntityData.TryRead(list, out List<KeyValuePair<int, object>> given))
            {
                return null;
            }

            // The name of an entity may come first, as it does in the list entget gives (runs/hhh makes a copy of one from its list).
            int typeAt = given.FindIndex(p => p.Key != -1);

            if (!(typeAt >= 0 && given[typeAt].Key == 0 && given[typeAt].Value is string typed))
            {
                return null;
            }

            string type = typed.ToUpperInvariant();

            if (Array.IndexOf(Makeable, type) < 0)
            {
                return null;
            }

            List<KeyValuePair<int, object>> groups = ChangesOf(given);
            CompletePoints(type, groups);

            // A text left on its baseline has no second point: AutoCAD keeps group 11 of its list at 0,0,0 whatever the list gave.
            if (
                type == "TEXT"
                && !groups.Exists(p => (p.Key == 72 || p.Key == 73) && p.Value is int justified && justified != 0)
            )
            {
                groups.RemoveAll(p => p.Key == 11);
            }

            foreach (int required in RequiredOf(type))
            {
                if (!groups.Exists(p => p.Key == required))
                {
                    return null;
                }
            }

            CadDocument document = session.Document;
            int layerAt = groups.FindIndex(p => p.Key == 8);
            string layer = layerAt >= 0 ? (string)groups[layerAt].Value : document.Database.Clayer;

            if (!Fits(document, type, groups))
            {
                return null;
            }

            groups.RemoveAll(p => p.Key == 8);
            CadSpace space = document.WorkingSpace;
            session.OpenLispStep();

            if (type == "INSERT")
            {
                return MakeInsert(session, space, layer, groups) ? list : null;
            }

            DxfDatabase fragment;

            try
            {
                fragment = DxfDatabase.Parse(Fragment(type, layer, groups));
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                GeometryHelperLog.Warn("LispDrawing: entmake could not read the entity it was given.", exception);
                return null;
            }

            var held = new List<DxfEntity>();

            foreach (DxfEntity made in fragment.ModelSpace)
            {
                held.Add(made);
            }

            if (held.Count != 1)
            {
                return null;
            }

            bool added = false;
            CadEditResult result = session.Apply("ENTMAKE", null, s =>
            {
                foreach (DxfEntity copy in fragment.WblockCloneObjects(held, space.Block))
                {
                    if (!string.IsNullOrEmpty(copy.Handle) && !copy.IsErased)
                    {
                        s.Added(space, copy);
                        added = true;

                        if (
                            copy is DxfDBText text
                            && (
                                text.HorizontalMode != DxfTextHorizontalMode.TextLeft
                                || text.VerticalMode != DxfTextVerticalMode.TextBase
                            )
                        )
                        {
                            AlignText(text);
                        }
                    }
                }
            });

            return result.Succeeded && added ? list : null;
        }

        /// <summary>A point of two coordinates gets the Z of 0, as AutoCAD keeps every point of an entity in three but the vertices of an LWPOLYLINE (<c>(10 0.0 20.0)</c> of a TEXT is <c>(10 0.0 20.0 0.0)</c> in its list).</summary>
        private static void CompletePoints(string type, List<KeyValuePair<int, object>> groups)
        {
            if (type == "LWPOLYLINE")
            {
                return;
            }

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].Value is double[] point && point.Length == 2)
                {
                    groups[i] = new KeyValuePair<int, object>(groups[i].Key, new[] { point[0], point[1], 0.0 });
                }
            }
        }

        /// <summary>The text of a DXF holding one entity, with the groups of a list, for the drawing to take as it takes any entity pasted.</summary>
        private static string Fragment(string type, string layer, List<KeyValuePair<int, object>> groups)
        {
            var body = new List<KeyValuePair<int, object>>(groups);
            List<KeyValuePair<int, object>> placed = LispEntityData.Marked(type, body);
            var text = new StringBuilder();

            void Group(int code, string value) =>
                text.Append(code.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Append('\n')
                    .Append(value.Replace("\r", string.Empty).Replace('\n', ' '))
                    .Append('\n');

            Group(0, "SECTION");
            Group(2, "HEADER");
            Group(9, "$ACADVER");
            Group(1, "AC1027");
            Group(0, "ENDSEC");
            Group(0, "SECTION");
            Group(2, "ENTITIES");
            Group(0, type);
            Group(100, "AcDbEntity");
            Group(8, layer);

            foreach (KeyValuePair<int, string> group in LispEntityData.ToGroups(type, placed))
            {
                Group(group.Key, group.Value);
            }

            Group(0, "ENDSEC");
            Group(0, "EOF");
            return text.ToString();
        }

        /// <summary>A block reference made of a list: its block, point, scales and turn; no attributes.</summary>
        private static bool MakeInsert(
            CadCommandSession session,
            CadSpace space,
            string layer,
            List<KeyValuePair<int, object>> groups
        )
        {
            DxfDatabase database = session.Document.Database;
            string blockName = groups.Find(p => p.Key == 2).Value as string;

            if (blockName == null || !database.BlockTable.Has(blockName))
            {
                return false;
            }

            double[] at = groups.Find(p => p.Key == 10).Value as double[];

            if (at == null || groups.Exists(p => p.Key == 66 && p.Value is int attributes && attributes != 0))
            {
                return false;
            }

            double Scale(int code) => groups.Find(p => p.Key == code).Value is double d ? d : 1.0;

            double rotation = groups.Find(p => p.Key == 50).Value is double r ? r : 0.0;
            EnsureLayer(session.Document, layer);
            CadEditResult result = session.Apply("ENTMAKE", null, s =>
            {
                var insert = new DxfBlockReference(
                    new GeoPoint3(at[0], at[1], at.Length > 2 ? at[2] : 0.0),
                    database.BlockTable[blockName] as DxfBlockTableRecord
                )
                {
                    Rotation = rotation,
                    ScaleFactors = new GeoVector3(Scale(41), Scale(42), Scale(43)),
                    Layer = layer,
                };
                s.Append(space, insert);
            });
            return result.Succeeded;
        }

        #endregion
    }
}
