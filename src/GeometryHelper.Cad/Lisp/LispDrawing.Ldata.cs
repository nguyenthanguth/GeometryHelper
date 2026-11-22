using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// <c>vlax-ldata-put</c>, <c>-get</c>, <c>-delete</c>, <c>-list</c> and <c>-test</c>: AutoLISP data kept in the drawing. Where
    /// AutoCAD keeps it (probes l1 and l2 of C4b): a dictionary named by a string, which lives in the root dictionary and is
    /// made by the first put; or, for an entity name or a VLA-OBJECT, the extension dictionary of that object, which is made
    /// the same way. Each datum is an object in the dictionary under its key (a key is a string or a symbol, any characters,
    /// letters of either case the same, ordered as the entries of a dictionary are), and <c>vlax-ldata-list</c> gives them last
    /// first. A datum is the text of its value, as <c>prin1</c> prints it, in group 300; a real is kept as a group 40 and an
    /// entity name as a group 340, the text naming them by a marker, <c>(&amp;VLO-R . 0)</c> and <c>(&amp;VLO-E . 0)</c>, so that
    /// nothing is lost to the digits printed. AutoCAD's object of the datum, VLO-VL_U, is an XRECORD here, with the groups it has
    /// (90 -64512, 91 the length of the text, 92 0 or 1 where there is a marker): a class of their own is not written by the DWG
    /// writer, an XRECORD is, so that the data survives a save as DXF or DWG and an open.
    /// </summary>
    internal static partial class LispDrawing
    {
        /// <summary>The selection sets <c>vlax-ldata-put</c> was given, by the dictionary and the key: AutoCAD keeps them for the session and not in the drawing.</summary>
        private static readonly ConditionalWeakTable<LispInterpreter, Dictionary<string, object>> KeptSets =
            new ConditionalWeakTable<LispInterpreter, Dictionary<string, object>>();

        private const string RealMarker = "&VLO-R";
        private const string NameMarker = "&VLO-E";

        private static void RegisterLdata(LispInterpreter lisp, CadCommandSession session, DrawingState state)
        {
            Dictionary<string, object> sets = KeptSets.GetValue(lisp, _ => new Dictionary<string, object>());

            lisp.Define("VLAX-LDATA-PUT", 3, 4, a =>
            {
                string key = KeyOf(a[1], a[0]);
                return lisp.OnOwner(() => LdataPut(lisp, session, state, sets, a[0], key, a[2]));
            });
            lisp.Define("VLAX-LDATA-GET", 2, 4, a =>
            {
                string key = KeyOf(a[1], a[0]);
                object otherwise = a.Length > 2 ? a[2] : null;
                return lisp.OnOwner(() => LdataGet(lisp, session, state, sets, a[0], key, otherwise));
            });
            lisp.Define("VLAX-LDATA-DELETE", 2, 3, a =>
            {
                string key = KeyOf(a[1], a[0]);
                return lisp.OnOwner(() => LdataDelete(lisp, session, state, sets, a[0], key));
            });
            lisp.Define("VLAX-LDATA-LIST", 1, 2, a =>
            {
                CheckOwner(a[0]);
                return lisp.OnOwner(() => LdataList(lisp, session, state, sets, a[0]));
            });
            lisp.Define("VLAX-LDATA-TEST", 1, 2, a => IsSerializable(a[0]) ? lisp.Intern("T") : null);
        }

        #region The functions

        /// <summary>The first argument of the five functions: a string, an entity name or a VLA-OBJECT; any other is <c>bad argument type: must be VLA-OBJECT/lentity or dictionary name</c>.</summary>
        private static void CheckOwner(object owner)
        {
            if (!(owner is string) && !(owner is LispEntityName) && !(owner is LispVlaObject))
            {
                throw new LispException(
                    "bad argument type: must be VLA-OBJECT/lentity or dictionary name: "
                        + LispInterpreter.Print(owner, true)
                );
            }
        }

        /// <summary>The key of a datum: a string, or a symbol by its name; anything else is <c>bad argument type: (or stringp symbolp)</c>, and the first argument is looked at first.</summary>
        private static string KeyOf(object key, object owner)
        {
            CheckOwner(owner);

            if (key is string text)
            {
                return text;
            }

            if (key is LispSymbol symbol)
            {
                return symbol.Name;
            }

            return key == null ? "NIL" : throw LispInterpreter.TypeError("(or stringp symbolp)", key, colon: true);
        }

        /// <summary>
        /// <c>(vlax-ldata-put owner key data [private])</c>: keeps the data under the key, in place of what the key had, and gives
        /// the data. The owner is a dictionary name, made if the drawing has none, or an object, whose extension dictionary is
        /// made if it has none. A name that cannot be a dictionary's is <c>can't create dictionary</c>; a function, which is
        /// no data, is <c>Visual LISP: Illegal import/export object</c>. The last argument is read and has no effect (probe l2).
        /// A selection set is kept for the session.
        /// </summary>
        private static object LdataPut(
            LispInterpreter lisp,
            CadCommandSession session,
            DrawingState state,
            Dictionary<string, object> sets,
            object owner,
            string key,
            object data
        )
        {
            var reals = new List<double>();
            var names = new List<string>();
            string text = data is LispSelectionSet ? string.Empty : Serialize(lisp, data, reals, names);
            string dictionaryHandle = null;

            // A dictionary that cannot be made, or an object that is not there, is refused before anything is done.
            DataDictionary(session.Document, state, owner, false, out string refused);

            if (refused != null)
            {
                throw new LispException(refused);
            }

            ApplyToObjects(session, "VLAX-LDATA-PUT", key, s =>
            {
                DxfFile file = s.Database.File;
                DxfItem dictionary = DataDictionary(s.Document, state, owner, true, out _);

                // A key AutoCAD cannot name an entry by is not kept, and the data is still the answer (probe l1).
                if (dictionary == null || !IsDictionaryName(key))
                {
                    return;
                }

                dictionaryHandle = dictionary.Handle;
                DictEntry old = EntriesOf(file, dictionary)
                    .Find(e => string.Equals(e.Name, key, StringComparison.OrdinalIgnoreCase));

                if (old != null && DxfDBObject.OfObject(dictionary) is DxfDBDictionary holder)
                {
                    holder.Remove(old.Name);
                }

                string handle = file.AddObject("XRECORD", dictionary.Handle, b =>
                {
                    b.Group(100, "AcDbXrecord");

                    if (file.Supports(1015))
                    {
                        b.Group(280, "1");
                    }

                    b.Group(90, "-64512");
                    b.Group(91, text.Length.ToString(CultureInfo.InvariantCulture));

                    if (text.Length > 0)
                    {
                        b.Group(92, reals.Count + names.Count > 0 ? "1" : "0");
                    }

                    foreach (double real in reals)
                    {
                        b.Group(40, LispEntityData.Number(real));
                    }

                    foreach (string name in names)
                    {
                        b.Group(340, name);
                    }

                    if (text.Length > 0)
                    {
                        b.Group(300, text);
                    }
                }).Handle;
                InsertEntry(file, dictionary, key, handle);
            });

            if (dictionaryHandle != null)
            {
                string kept = dictionaryHandle + "/" + key.ToLowerInvariant();

                if (data is LispSelectionSet)
                {
                    sets[kept] = data;
                }
                else
                {
                    sets.Remove(kept);
                }
            }

            return data;
        }

        /// <summary>
        /// <c>(vlax-ldata-get owner key [default])</c>: the data kept under the key; the default (nil where it is not given) for a
        /// dictionary or a key that is not there. Data that is nil is nil, not the default.
        /// </summary>
        private static object LdataGet(
            LispInterpreter lisp,
            CadCommandSession session,
            DrawingState state,
            Dictionary<string, object> sets,
            object owner,
            string key,
            object otherwise
        )
        {
            DxfFile file = session.Document.Database.File;
            DxfItem dictionary = DataDictionary(session.Document, state, owner, false, out _);
            DictEntry entry =
                dictionary == null
                    ? null
                    : EntriesOf(file, dictionary)
                        .Find(e => string.Equals(e.Name, key, StringComparison.OrdinalIgnoreCase));
            DxfItem datum = entry == null ? null : file.ObjectItem(entry.Handle);

            if (datum == null)
            {
                return otherwise;
            }

            return sets.TryGetValue(dictionary.Handle + "/" + key.ToLowerInvariant(), out object kept)
                ? kept
                : Deserialize(lisp, file, datum);
        }

        /// <summary>
        /// <c>(vlax-ldata-delete owner key)</c>: erases the datum of the key and gives <c>T</c>; nil where there is none, or no
        /// dictionary to hold it.
        /// </summary>
        private static object LdataDelete(
            LispInterpreter lisp,
            CadCommandSession session,
            DrawingState state,
            Dictionary<string, object> sets,
            object owner,
            string key
        )
        {
            DxfFile file = session.Document.Database.File;
            DxfItem dictionary = DataDictionary(session.Document, state, owner, false, out _);
            DictEntry entry =
                dictionary == null
                    ? null
                    : EntriesOf(file, dictionary)
                        .Find(e => string.Equals(e.Name, key, StringComparison.OrdinalIgnoreCase));

            if (entry == null)
            {
                return null;
            }

            string handle = dictionary.Handle;
            ApplyToObjects(session, "VLAX-LDATA-DELETE", key, s =>
            {
                DxfItem holder = s.Database.File.ObjectItem(handle);

                if (holder != null && DxfDBObject.OfObject(holder) is DxfDBDictionary dictionaryObject)
                {
                    dictionaryObject.Remove(entry.Name);
                }
            });
            sets.Remove(handle + "/" + key.ToLowerInvariant());
            return lisp.Intern("T");
        }

        /// <summary>
        /// <c>(vlax-ldata-list owner)</c>: the data of the owner as a list of the key and the data, <c>("k" . 5)</c> and for data
        /// that is a list <c>("k" 1 2)</c>, the last entry of the dictionary first; nil where there is none.
        /// </summary>
        private static object LdataList(
            LispInterpreter lisp,
            CadCommandSession session,
            DrawingState state,
            Dictionary<string, object> sets,
            object owner
        )
        {
            DxfFile file = session.Document.Database.File;
            DxfItem dictionary = DataDictionary(session.Document, state, owner, false, out _);
            var pairs = new List<object>();

            if (dictionary != null)
            {
                List<DictEntry> entries = EntriesOf(file, dictionary);

                for (int i = entries.Count - 1; i >= 0; i--)
                {
                    DxfItem datum = file.ObjectItem(entries[i].Handle);

                    if (datum != null)
                    {
                        string kept = dictionary.Handle + "/" + entries[i].Name.ToLowerInvariant();
                        pairs.Add(
                            new LispCons(
                                entries[i].Name,
                                sets.TryGetValue(kept, out object set) ? set : Deserialize(lisp, file, datum)
                            )
                        );
                    }
                }
            }

            return LispInterpreter.ListOf(pairs);
        }

        #endregion

        #region Where the data is kept

        /// <summary>
        /// The dictionary an owner keeps its data in: for a string the dictionary of that name in the root dictionary; for an
        /// entity name or a VLA-OBJECT the extension dictionary of the object. With <paramref name="create"/> one that is not
        /// there is made; null otherwise, or where the owner names nothing, and <paramref name="refused"/> then says why.
        /// </summary>
        private static DxfItem DataDictionary(
            CadDocument document,
            DrawingState state,
            object owner,
            bool create,
            out string refused
        )
        {
            DxfFile file = document.Database.File;
            refused = null;

            if (owner is string name)
            {
                DxfItem root = file.HoldsObjects ? file.ObjectItem(file.RootDictionaryHandle()) : null;

                if (root == null)
                {
                    refused = "can't create dictionary: " + LispInterpreter.Print(name, true);
                    return null;
                }

                DictEntry entry = EntriesOf(file, root)
                    .Find(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
                DxfItem found = entry == null ? null : file.ObjectItem(entry.Handle);

                if (found != null && found.Type == "DICTIONARY")
                {
                    return found;
                }

                if (!IsDictionaryName(name) || entry != null)
                {
                    refused = "can't create dictionary: " + LispInterpreter.Print(name, true);
                    return null;
                }

                if (!create)
                {
                    return null;
                }

                DxfItem made = file.AddObject("DICTIONARY", root.Handle, b =>
                {
                    b.Group(100, "AcDbDictionary");
                    b.Group(280, "0");
                    b.Group(281, "1");
                });
                InsertEntry(file, root, name, made.Handle);
                return made;
            }

            string handle = owner is LispEntityName ename ? ename.Handle : ((LispVlaObject)owner).Key;
            DxfItem item = string.IsNullOrEmpty(handle) ? null : FindItem(document, state.Resolve(handle));

            if (item == null)
            {
                refused = "bad argument type: must be VLA-OBJECT/lentity or dictionary name: nil";
                return null;
            }

            string extension = file.ExtensionDictionaryOf(item);
            DxfItem dictionaryItem = extension == null ? null : file.ObjectItem(extension);

            if (dictionaryItem == null && create)
            {
                dictionaryItem = DxfTable.MakeExtensionDictionary(file, item, true);
            }

            return dictionaryItem;
        }

        #endregion

        #region The text of a datum

        /// <summary>Whether a value can be kept in a drawing: a whole number, a real, a string, a symbol, an entity name, nil, and a list of them; not a selection set, a function or a VLA-OBJECT.</summary>
        private static bool IsSerializable(object value)
        {
            switch (value)
            {
                case null:
                case int _:
                case double _:
                case string _:
                case LispSymbol _:
                case LispEntityName _:
                    return true;
                case LispCons cons:
                    for (object cell = cons; cell != null; )
                    {
                        if (cell is LispCons item)
                        {
                            if (!IsSerializable(item.Car))
                            {
                                return false;
                            }

                            cell = item.Cdr;
                        }
                        else
                        {
                            return IsSerializable(cell);
                        }
                    }

                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The text of a datum as <c>prin1</c> prints it; a real and an entity name in it are replaced by <c>(&amp;VLO-R . n)</c>
        /// and <c>(&amp;VLO-E . n)</c>, n their place among the reals and the names the groups 40 and 340 hold. nil is no text
        /// at all. A value that cannot be kept (a function, a VLA-OBJECT) is <c>Visual LISP: Illegal import/export object</c>.
        /// </summary>
        private static string Serialize(LispInterpreter lisp, object value, List<double> reals, List<string> names)
        {
            if (value == null)
            {
                return string.Empty;
            }

            var text = new StringBuilder();
            AppendDatum(lisp, value, text, reals, names);
            return text.ToString();
        }

        private static void AppendDatum(
            LispInterpreter lisp,
            object value,
            StringBuilder text,
            List<double> reals,
            List<string> names
        )
        {
            switch (value)
            {
                case double real:
                    text.Append("(" + RealMarker + " . " + reals.Count.ToString(CultureInfo.InvariantCulture) + ")");
                    reals.Add(real);
                    break;
                case LispEntityName name:
                    text.Append("(" + NameMarker + " . " + names.Count.ToString(CultureInfo.InvariantCulture) + ")");
                    names.Add(name.Handle);
                    break;
                case LispCons cons:
                    text.Append('(');
                    object cell = cons;

                    while (cell is LispCons item)
                    {
                        AppendDatum(lisp, item.Car, text, reals, names);
                        cell = item.Cdr;

                        if (cell is LispCons)
                        {
                            text.Append(' ');
                        }
                    }

                    if (cell != null)
                    {
                        text.Append(" . ");
                        AppendDatum(lisp, cell, text, reals, names);
                    }

                    text.Append(')');
                    break;
                case null:
                    text.Append("nil");
                    break;
                case int _:
                case string _:
                case LispSymbol _:
                    text.Append(LispInterpreter.Print(value, true));
                    break;
                default:
                    throw new LispException(
                        "Visual LISP: Illegal import/export object " + LispInterpreter.Print(value, true)
                    );
            }
        }

        /// <summary>The value a datum holds, read from its text with its reals and names put back in; nil for a datum of no text.</summary>
        private static object Deserialize(LispInterpreter lisp, DxfFile file, DxfItem datum)
        {
            string text = string.Empty;
            var reals = new List<double>();
            var names = new List<string>();

            foreach (DxfTypedValue group in file.TailOf(datum, 0))
            {
                if (group.TypeCode == 300)
                {
                    text = group.Value;
                }
                else if (group.TypeCode == 40)
                {
                    reals.Add(double.Parse(group.Value, NumberStyles.Float, CultureInfo.InvariantCulture));
                }
                else if (group.TypeCode == 340)
                {
                    names.Add(group.Value.Trim());
                }
            }

            if (text.Length == 0)
            {
                return null;
            }

            new LispReader(text, lisp.Intern, lisp.Intern("QUOTE")).TryRead(out object form);
            return Restore(form, reals, names);
        }

        /// <summary>A form read from the text of a datum with each <c>(&amp;VLO-R . n)</c> and <c>(&amp;VLO-E . n)</c> replaced by the n-th real or name.</summary>
        private static object Restore(object form, List<double> reals, List<string> names)
        {
            if (!(form is LispCons cons))
            {
                return form;
            }

            if (cons.Car is LispSymbol marker && cons.Cdr is int at)
            {
                if (marker.Name == RealMarker && at >= 0 && at < reals.Count)
                {
                    return reals[at];
                }

                if (marker.Name == NameMarker && at >= 0 && at < names.Count)
                {
                    return new LispEntityName(names[at]);
                }
            }

            return new LispCons(Restore(cons.Car, reals, names), Restore(cons.Cdr, reals, names));
        }

        #endregion
    }
}
