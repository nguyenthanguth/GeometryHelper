using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// The dictionaries of the drawing as AutoLISP reaches them: <c>namedobjdict</c>, <c>dictsearch</c>, <c>dictnext</c>,
    /// <c>dictadd</c>, <c>dictremove</c> and <c>dictrename</c>, and the objects they hold, an XRECORD or a DICTIONARY made by
    /// <c>entmakex</c> or <c>entmake</c> (<see cref="TryMakeObject"/>). A dictionary lists its entries by name, letters of either
    /// case the same, in the order of the lower-case names; an object made and not yet added has no owner (its 330 is 0, as
    /// AutoCAD's list shows) and takes the dictionary as its owner, and as its reactor, when it is added. All measured in
    /// AutoCAD 2021's console, probes d1 to d3 of C4b.
    /// </summary>
    internal static partial class LispDrawing
    {
        /// <summary>One entry of a dictionary: its name, the handle of the object it names and the group that names it (350 or 360).</summary>
        private sealed class DictEntry
        {
            internal DictEntry(string name, string handle, int code)
            {
                Name = name;
                Handle = handle;
                Code = code;
            }

            internal string Name { get; }

            internal string Handle { get; }

            internal int Code { get; }
        }

        /// <summary>Where <c>dictnext</c> is in each dictionary the interpreter has walked: the index of the entry it gives next, by the handle of the dictionary.</summary>
        private static readonly ConditionalWeakTable<LispInterpreter, Dictionary<string, int>> Walks =
            new ConditionalWeakTable<LispInterpreter, Dictionary<string, int>>();

        static partial void RegisterDictionaries(LispInterpreter lisp, CadCommandSession session)
        {
            DrawingState state = StateOf(lisp);
            Dictionary<string, int> walk = Walks.GetValue(lisp, _ => new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase
            ));

            lisp.Define("NAMEDOBJDICT", 0, 0, a => lisp.OnOwner(() => NamedObjects(session)));
            lisp.Define("DICTSEARCH", 2, 3, a =>
            {
                LispEntityName dictionary = NameOf(a[0]);
                string key = LispInterpreter.Str(a[1]);
                bool setNext = a.Length > 2 && a[2] != null;
                return lisp.OnOwner(() => DictSearch(session, state, walk, dictionary, key, setNext));
            });
            lisp.Define("DICTNEXT", 1, 2, a =>
            {
                LispEntityName dictionary = NameOf(a[0]);
                bool rewind = a.Length > 1 && a[1] != null;
                return lisp.OnOwner(() => DictNext(session, state, walk, dictionary, rewind));
            });
            lisp.Define("DICTADD", 3, 3, a =>
            {
                LispEntityName dictionary = NameOf(a[0]);
                string key = LispInterpreter.Str(a[1]);
                LispEntityName value = NameOf(a[2]);
                return lisp.OnOwner(() => DictAdd(session, dictionary, key, value));
            });
            lisp.Define("DICTREMOVE", 2, 2, a =>
            {
                LispEntityName dictionary = NameOf(a[0]);
                string key = LispInterpreter.Str(a[1]);
                return lisp.OnOwner(() => DictRemove(session, dictionary, key));
            });
            lisp.Define("DICTRENAME", 3, 3, a =>
            {
                LispEntityName dictionary = NameOf(a[0]);
                string oldName = LispInterpreter.Str(a[1]);
                string newName = LispInterpreter.Str(a[2]);
                return lisp.OnOwner(() => DictRename(session, dictionary, oldName, newName));
            });
            RegisterLdata(lisp, session, state);
        }

        #region The functions

        /// <summary><c>(namedobjdict)</c>: the name of the drawing's root dictionary, which names every other; nil where the drawing has none (a file of R12).</summary>
        private static object NamedObjects(CadCommandSession session)
        {
            DxfFile file = session.Document.Database.File;
            DxfItem root = file.HoldsObjects ? file.ObjectItem(file.RootDictionaryHandle()) : null;
            return root == null ? null : new LispEntityName(root.Handle);
        }

        /// <summary>
        /// <c>(dictsearch dict name [setnext])</c>: the list <c>entget</c> gives for the object the dictionary's entry of the name
        /// names, the name taken in letters of either case and with its spaces as they are; nil where the dictionary has none, or
        /// the name given is no dictionary. With <c>setnext</c> the next <c>dictnext</c> gives the entry after this one.
        /// </summary>
        private static object DictSearch(
            CadCommandSession session,
            DrawingState state,
            Dictionary<string, int> walk,
            LispEntityName dictionary,
            string key,
            bool setNext
        )
        {
            DxfFile file = session.Document.Database.File;
            DxfItem item = DictionaryOf(session.Document, dictionary.Handle);

            if (item == null)
            {
                return null;
            }

            List<DictEntry> entries = EntriesOf(file, item);
            int at = entries.FindIndex(e => string.Equals(e.Name, key, StringComparison.OrdinalIgnoreCase));
            DxfItem target = at < 0 ? null : file.ObjectItem(entries[at].Handle);

            if (target == null)
            {
                return null;
            }

            if (setNext)
            {
                walk[item.Handle] = at + 1;
            }

            return EntGet(session, state, new LispEntityName(target.Handle), null);
        }

        /// <summary>
        /// <c>(dictnext dict [rewind])</c>: the list <c>entget</c> gives for the object of the next entry of the dictionary, each
        /// dictionary keeping its own place; nil past the last. With <c>rewind</c> the walk starts at the first entry.
        /// </summary>
        private static object DictNext(
            CadCommandSession session,
            DrawingState state,
            Dictionary<string, int> walk,
            LispEntityName dictionary,
            bool rewind
        )
        {
            DxfFile file = session.Document.Database.File;
            DxfItem item = DictionaryOf(session.Document, dictionary.Handle);

            if (item == null)
            {
                return null;
            }

            if (rewind)
            {
                walk[item.Handle] = 0;
            }

            List<DictEntry> entries = EntriesOf(file, item);
            int at = walk.TryGetValue(item.Handle, out int next) ? next : 0;

            while (at < entries.Count)
            {
                DxfItem target = file.ObjectItem(entries[at++].Handle);

                if (target != null)
                {
                    walk[item.Handle] = at;
                    return EntGet(session, state, new LispEntityName(target.Handle), null);
                }
            }

            walk[item.Handle] = at;
            return null;
        }

        /// <summary>
        /// <c>(dictadd dict name object)</c>: adds an entry of the name for an object of the OBJECTS section (an XRECORD or a
        /// DICTIONARY <c>entmakex</c> made, one of the drawing's) and gives the name of the object. nil where the dictionary has
        /// an entry of the name already, letters of either case the same, the name is empty, has a space at either end or one of
        /// <c>&lt; &gt; / \ " : ; ? * | , = `</c> in it (a <c>*</c> may begin it), the dictionary is the object itself, the object is an
        /// entity, or the first is no dictionary. The entry stands among the others ordered by name. An object with no owner
        /// takes the dictionary as its owner and reactor; one with an owner keeps it, and the same object may have two names.
        /// </summary>
        private static object DictAdd(
            CadCommandSession session,
            LispEntityName dictionary,
            string key,
            LispEntityName value
        )
        {
            CadDocument document = session.Document;
            DxfFile file = document.Database.File;
            DxfItem item = DictionaryOf(document, dictionary.Handle);
            DxfItem target = file.HoldsObjects ? file.ObjectItem(value.Handle) : null;

            if (
                item == null
                || target == null
                || string.Equals(item.Handle, target.Handle, StringComparison.OrdinalIgnoreCase)
                || !IsDictionaryName(key)
                || EntriesOf(file, item).Exists(e => string.Equals(e.Name, key, StringComparison.OrdinalIgnoreCase))
            )
            {
                return null;
            }

            string dictionaryHandle = item.Handle;
            string targetHandle = target.Handle;
            CadEditResult result = ApplyToObjects(session, "DICTADD", key, s =>
            {
                DxfFile f = s.Database.File;
                DxfItem into = f.ObjectItem(dictionaryHandle);
                DxfItem added = f.ObjectItem(targetHandle);

                if (into == null || added == null)
                {
                    return;
                }

                Adopt(f, dictionaryHandle, added);
                InsertEntry(f, into, key, targetHandle);
            });
            return result.Succeeded ? new LispEntityName(targetHandle) : null;
        }

        /// <summary>
        /// <c>(dictremove dict name)</c>: takes the entry of the name out of the dictionary and gives the name of the object it
        /// named, which is not erased and keeps its owner; nil where there is no such entry or no such dictionary.
        /// </summary>
        private static object DictRemove(CadCommandSession session, LispEntityName dictionary, string key)
        {
            CadDocument document = session.Document;
            DxfFile file = document.Database.File;
            DxfItem item = DictionaryOf(document, dictionary.Handle);
            List<DictEntry> entries = item == null ? null : EntriesOf(file, item);
            int at =
                entries == null
                    ? -1
                    : entries.FindIndex(e => string.Equals(e.Name, key, StringComparison.OrdinalIgnoreCase));

            if (at < 0)
            {
                return null;
            }

            string dictionaryHandle = item.Handle;
            string targetHandle = entries[at].Handle;
            ApplyToObjects(session, "DICTREMOVE", key, s =>
            {
                DxfFile f = s.Database.File;
                DxfItem from = f.ObjectItem(dictionaryHandle);

                if (from != null)
                {
                    TakeEntryOut(f, from, key);
                }
            });
            return new LispEntityName(targetHandle);
        }

        /// <summary>
        /// <c>(dictrename dict old new)</c>: gives the entry of the old name the new one and gives the new name as it was typed;
        /// nil where there is no entry of the old name, the new name is the old, names another entry (letters of either case the
        /// same) or is no name a dictionary takes (see <see cref="DictAdd"/>). The entry takes its place among the others by the
        /// new name.
        /// </summary>
        private static object DictRename(
            CadCommandSession session,
            LispEntityName dictionary,
            string oldName,
            string newName
        )
        {
            CadDocument document = session.Document;
            DxfFile file = document.Database.File;
            DxfItem item = DictionaryOf(document, dictionary.Handle);
            List<DictEntry> entries = item == null ? null : EntriesOf(file, item);
            int at =
                entries == null
                    ? -1
                    : entries.FindIndex(e => string.Equals(e.Name, oldName, StringComparison.OrdinalIgnoreCase));

            if (
                at < 0
                || !IsDictionaryName(newName)
                || string.Equals(entries[at].Name, newName, StringComparison.Ordinal)
                || entries.Exists(e =>
                    e != entries[at] && string.Equals(e.Name, newName, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return null;
            }

            string dictionaryHandle = item.Handle;
            string targetHandle = entries[at].Handle;
            string was = entries[at].Name;
            CadEditResult result = ApplyToObjects(session, "DICTRENAME", newName, s =>
            {
                DxfFile f = s.Database.File;
                DxfItem inside = f.ObjectItem(dictionaryHandle);

                if (inside == null)
                {
                    return;
                }

                List<DictEntry> list = EntriesOf(f, inside);
                list.RemoveAll(e => string.Equals(e.Name, was, StringComparison.Ordinal));
                list.Insert(PlaceFor(list, newName), new DictEntry(newName, targetHandle, entries[at].Code));
                WriteEntries(f, inside, list);
            });
            return result.Succeeded ? newName : null;
        }

        #endregion

        #region Dictionaries and their entries

        /// <summary>The DICTIONARY of the OBJECTS section a handle names; null for none, or for an object of another kind.</summary>
        private static DxfItem DictionaryOf(CadDocument document, string handle)
        {
            DxfFile file = document.Database.File;
            DxfItem item = file.HoldsObjects ? file.ObjectItem(handle) : null;
            return item != null && (item.Type == "DICTIONARY" || item.Type == "ACDBDICTIONARYWDFLT") ? item : null;
        }

        /// <summary>Whether a handle names an object of the OBJECTS section.</summary>
        private static bool IsObject(CadDocument document, string handle)
        {
            DxfFile file = document.Database.File;
            return file.HoldsObjects && file.ObjectItem(handle) != null;
        }

        /// <summary>
        /// The entries of a dictionary in the order of its record: each its name, the handle it names and the group that names it.
        /// </summary>
        private static List<DictEntry> EntriesOf(DxfFile file, DxfItem dictionary)
        {
            var entries = new List<DictEntry>();
            string name = null;

            foreach (DxfTypedValue group in file.TailOf(dictionary, 0))
            {
                if (group.TypeCode == 3)
                {
                    name = group.Value;
                }
                else if ((group.TypeCode == 350 || group.TypeCode == 360) && name != null)
                {
                    entries.Add(new DictEntry(name, group.Value.Trim(), group.TypeCode));
                    name = null;
                }
            }

            return entries;
        }

        /// <summary>Whether a dictionary owns its entries hard (group 280 of 1): its entries are named by groups 360, not 350.</summary>
        private static bool IsHard(DxfFile file, DxfItem dictionary) =>
            file.TailOf(dictionary, 0).Exists(g => g.TypeCode == 280 && g.Value.Trim() == "1");

        /// <summary>
        /// Writes the entries of a dictionary into its record, in their order, in the place of the ones it has; what the record
        /// holds before the first entry and after the last (a default's own group of an ACDBDICTIONARYWDFLT) is kept as it is.
        /// </summary>
        private static void WriteEntries(DxfFile file, DxfItem dictionary, List<DictEntry> entries)
        {
            List<DxfTypedValue> tail = file.TailOf(dictionary, 0);
            int first = -1;
            int after = 0;

            for (int i = 0; i + 1 < tail.Count; i++)
            {
                if (tail[i].TypeCode == 3 && (tail[i + 1].TypeCode == 350 || tail[i + 1].TypeCode == 360))
                {
                    first = first < 0 ? i : first;
                    after = i + 2;
                    i++;
                }
            }

            if (first < 0)
            {
                // No entry yet: they go after the flags that open the dictionary, before anything else it holds.
                first = tail.FindLastIndex(g => g.TypeCode == 280 || g.TypeCode == 281) + 1;
                first = first == 0 ? tail.Count : first;
                after = first;
            }

            var groups = new List<KeyValuePair<int, string>>();

            foreach (DictEntry entry in entries)
            {
                groups.Add(new KeyValuePair<int, string>(3, entry.Name));
                groups.Add(new KeyValuePair<int, string>(entry.Code, entry.Handle));
            }

            for (int i = after; i < tail.Count; i++)
            {
                groups.Add(new KeyValuePair<int, string>(tail[i].TypeCode, tail[i].Value));
            }

            file.ReplaceTail(dictionary, first, groups);
        }

        /// <summary>Puts an entry among the others, before the first whose name (in lower case) comes after its own, as AutoCAD keeps them; the group is 360 in a dictionary that owns its entries hard.</summary>
        private static void InsertEntry(DxfFile file, DxfItem dictionary, string name, string handle)
        {
            List<DictEntry> entries = EntriesOf(file, dictionary);
            entries.Insert(PlaceFor(entries, name), new DictEntry(name, handle, IsHard(file, dictionary) ? 360 : 350));
            WriteEntries(file, dictionary, entries);
        }

        /// <summary>Takes the entry of a name out of a dictionary, and the dictionary out of the reactors of the object it named where no other entry of it names it.</summary>
        private static void TakeEntryOut(DxfFile file, DxfItem dictionary, string name)
        {
            List<DictEntry> entries = EntriesOf(file, dictionary);
            int at = entries.FindIndex(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

            if (at < 0)
            {
                return;
            }

            string handle = entries[at].Handle;
            entries.RemoveAt(at);
            WriteEntries(file, dictionary, entries);
            DxfItem named = file.ObjectItem(handle);

            if (
                named != null
                && !entries.Exists(e => string.Equals(e.Handle, handle, StringComparison.OrdinalIgnoreCase))
            )
            {
                file.SetReactor(named, dictionary.Handle, false);
            }
        }

        /// <summary>Where an entry of a name goes among the others: before the first whose lower-case name comes after the lower-case one, last where none does.</summary>
        private static int PlaceFor(List<DictEntry> entries, string name)
        {
            string folded = name.ToLowerInvariant();

            for (int i = 0; i < entries.Count; i++)
            {
                if (string.CompareOrdinal(entries[i].Name.ToLowerInvariant(), folded) > 0)
                {
                    return i;
                }
            }

            return entries.Count;
        }

        /// <summary>
        /// Gives an entity brought back by <c>entdel</c> the extension dictionary the entity was erased with: the dictionary stayed in
        /// the drawing, its owner the erased entity (AutoCAD 2021, probe m/entmod d1), and is the same object after, its owner the
        /// entity that is there now.
        /// </summary>
        private static void GiveExtensionBack(DxfItem entity, string erasedHandle, string dictionaryHandle)
        {
            DxfFile file = entity.File;
            DxfItem dictionary = file.HoldsObjects ? file.ObjectItem(dictionaryHandle) : null;

            if (dictionary == null)
            {
                return;
            }

            file.SetExtensionDictionary(entity, dictionaryHandle);
            DxfField.Reown(file, dictionary, erasedHandle, entity.Handle);
        }

        /// <summary>
        /// Gives an object that has no owner (its 330 is 0) the dictionary for its owner and its reactor, as AutoCAD does when
        /// <c>dictadd</c> takes it (the dictionary <c>entmakex</c> makes with entries gives its objects its name as owner and no
        /// reactor); an object with an owner is left as it is.
        /// </summary>
        private static void Adopt(DxfFile file, string dictionaryHandle, DxfItem target, bool reactor = true)
        {
            string owner = file.OwnerOf(target);

            if (owner == null || owner.Trim() == "0")
            {
                DxfField.Reown(file, target, "0", dictionaryHandle);

                if (reactor)
                {
                    file.SetReactor(target, dictionaryHandle, true);
                }
            }
        }

        /// <summary>
        /// Whether a text can be the name of an entry of a dictionary (probe d3): not empty, no space at either end, none of the
        /// characters <c>&lt; &gt; / \ " : ; ? | , = `</c>, no control character, and a <c>*</c> only as the first.
        /// </summary>
        private static bool IsDictionaryName(string name)
        {
            if (name.Length == 0 || name[0] == ' ' || name[name.Length - 1] == ' ')
            {
                return false;
            }

            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];

                if (c < ' ' || "<>/\\\":;?|,=`".IndexOf(c) >= 0 || (c == '*' && i > 0))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Makes an edit of the objects of the OBJECTS section, one step of the evaluation; the record of it names no entity.</summary>
        private static CadEditResult ApplyToObjects(
            CadCommandSession session,
            string command,
            string named,
            Action<CadEditScope> edit
        )
        {
            session.OpenLispStep();
            session.HoldLispStep();
            return session.Apply(
                command,
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, command, null, named) },
                edit
            );
        }

        /// <summary>
        /// <c>entdel</c> of an object of the OBJECTS section: an object a dictionary holds is erased and its entry taken out; the
        /// root dictionary is not erased (nil), and an object no dictionary holds stays, as AutoCAD answers (probe d3). Gives the
        /// name.
        /// </summary>
        private static object EraseObject(CadCommandSession session, LispEntityName name, string handle)
        {
            DxfFile file = session.Document.Database.File;
            DxfItem item = file.ObjectItem(handle);

            if (item == null || string.Equals(handle, file.RootDictionaryHandle(), StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string ownerHandle = file.OwnerOf(item);
            DxfItem owner = ownerHandle == null ? null : file.ObjectItem(ownerHandle);

            // The extension dictionary of an entity is erased by itself, its owner keeping the pointer (AutoCAD 2021, probe m/entmod d1).
            if (
                owner == null
                && item.Type == "DICTIONARY"
                && FindItem(session.Document, ownerHandle) is DxfItem entity
                && string.Equals(file.ExtensionDictionaryOf(entity), handle, StringComparison.OrdinalIgnoreCase)
            )
            {
                ApplyToObjects(session, "ERASE", handle, s =>
                    s.Database.File.RemoveRecord(s.Database.File.ObjectItem(handle))
                );
                return name;
            }

            if (
                owner == null
                || !(DxfDBObject.OfObject(owner) is DxfDBDictionary holder)
                || !holder.Contains(DxfDBObject.OfObject(item))
            )
            {
                return name;
            }

            ApplyToObjects(session, "ERASE", handle, s =>
            {
                DxfFile f = s.Database.File;
                DxfItem gone = f.ObjectItem(handle);

                if (gone != null)
                {
                    DxfDBDictionary.EraseFromOwner(DxfDBObject.OfObject(gone), f);
                }
            });
            return name;
        }

        #endregion
    }
}
