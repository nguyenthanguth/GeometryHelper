using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// <c>entmakex</c>, and <c>entmake</c> of the two objects that have no place in a space: an XRECORD and a DICTIONARY. Such
    /// an object is made with no owner (its 330 is 0 in the list <c>entget</c> gives), and is kept in the drawing, found by its
    /// name, until a dictionary takes it (<c>dictadd</c>) or <c>entdel</c> erases it from one. <c>entmakex</c> gives the
    /// name of what it made, <c>entmake</c> the list it was given. AutoCAD 2021's console, probes d2 and d3 of C4b.
    /// </summary>
    internal static partial class LispDrawing
    {
        /// <summary>
        /// <c>(entmakex list)</c>: makes what <c>entmake</c> makes and gives its name, not the list: the entity of the last of the
        /// current space, the record of a table by the name it was given, an XRECORD or a DICTIONARY with no owner. nil where
        /// <c>entmake</c> gives nil. A thing that is no list is <c>bad list in entity tail</c>. What <c>entmake</c> makes
        /// from several calls (a polyline with its vertices, a block reference with attributes, a block) is not made by
        /// <c>entmakex</c>, which gives nil.
        /// </summary>
        private static object EntMakeX(LispInterpreter lisp, CadCommandSession session, object list)
        {
            if (list != null && !(list is LispCons))
            {
                throw new LispException("bad list in entity tail: " + LispInterpreter.Print(list, true));
            }

            if (TryMakeObject(session, list, true, out object madeObject))
            {
                return madeObject;
            }

            if (!LispEntityData.TryRead(list, out List<KeyValuePair<int, object>> given))
            {
                return null;
            }

            int typeAt = given.FindIndex(p => p.Key != -1);

            if (!(typeAt >= 0 && given[typeAt].Key == 0 && given[typeAt].Value is string typed))
            {
                return null;
            }

            string type = typed.ToUpperInvariant();
            CadDocument document = session.Document;

            if (
                IsMadeInSeveralCalls(type, given)
                || PendingOf(document).Complex != null
                || PendingOf(document).Block != null
            )
            {
                return null;
            }

            if (EntMake(lisp, session, list) == null)
            {
                return null;
            }

            if (IsRecordType(type))
            {
                string name = given.Find(p => p.Key == 2).Value as string ?? string.Empty;
                int at = IndexOfRecord(RecordsOf(document, type), name);
                return at < 0 ? null : new LispEntityName(RecordsOf(document, type)[at].Handle);
            }

            return EntLast(session);
        }

        /// <summary>Whether <c>entmake</c> makes a type from several calls: the parts and ends of a complex entity, a block and its end, and a block reference that has attributes.</summary>
        private static bool IsMadeInSeveralCalls(string type, List<KeyValuePair<int, object>> given)
        {
            switch (type)
            {
                case "POLYLINE":
                case "VERTEX":
                case "ATTRIB":
                case "SEQEND":
                case "BLOCK":
                case "ENDBLK":
                    return true;
                case "INSERT":
                    return given.Exists(p => p.Key == 66 && p.Value is int attributes && attributes != 0);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Makes an XRECORD or a DICTIONARY of a list, as <c>entmake</c> and <c>entmakex</c> do, when the list is of one; false for
        /// a list of anything else. The list must have the subclass marker <c>(100 . "AcDbXrecord")</c> or
        /// <c>(100 . "AcDbDictionary")</c> (else nil) and after it the groups of the object: of an XRECORD any of the codes 1 to
        /// 369 and the like that hold a value (a group of a kind its code does not hold is the error <c>bad DXF group</c> with the
        /// group, as is a code of 1000 or more or below 0; a second 100, a 0 or a 999 makes nil); of a DICTIONARY the flags
        /// 280 and 281 and the entries, each a 3 and the name of an object (350 or 360). The groups before the marker (-1, 5, 330)
        /// are not kept. An XRECORD has the flag <c>(280 . 1)</c> ahead of its data, as AutoCAD lists it; its points have three
        /// coordinates, a real is a real, and a group of 60 to 79 or 270 to 289 is a short whole number (<c>(71 . 100000)</c> is
        /// -31072). A DICTIONARY lists its entries by name and takes the objects of them for its own. The result is the name of
        /// the object where <c>wantName</c> is set, as for <c>entmakex</c>, else the list as <c>entmake</c> gives it; null where
        /// nothing was made.
        /// </summary>
        private static bool TryMakeObject(CadCommandSession session, object list, bool wantName, out object result)
        {
            result = null;
            string type = ObjectTypeOf(list);

            if (type == null)
            {
                return false;
            }

            string marker = type == "XRECORD" ? "AcDbXrecord" : "AcDbDictionary";
            var head = new List<LispCons>();
            var body = new List<LispCons>();
            bool marked = false;

            for (object cell = list; cell is LispCons item; cell = item.Cdr)
            {
                if (!(item.Car is LispCons group) || !(group.Car is int code))
                {
                    return true;
                }

                if (!marked && code == 100)
                {
                    if (!(group.Cdr is string said) || !string.Equals(said, marker, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    marked = true;
                }
                else if (marked)
                {
                    body.Add(group);
                }
                else if (code != 0)
                {
                    head.Add(group);
                }
            }

            if (!marked)
            {
                return true;
            }

            foreach (LispCons group in head)
            {
                int code = (int)group.Car;

                if (
                    ((code == -1 || code == 330) && !(group.Cdr is LispEntityName))
                    || (code == 5 && !(group.Cdr is string))
                )
                {
                    throw BadGroup(group);
                }
            }

            List<KeyValuePair<int, string>> groups = type == "XRECORD" ? XrecordGroups(body) : null;
            List<DictEntry> entries = type == "DICTIONARY" ? DictionaryGroups(body, out groups) : null;

            if (groups == null)
            {
                return true;
            }

            string made = null;
            CadEditResult applied = ApplyToObjects(session, "ENTMAKE", type, s =>
            {
                DxfFile file = s.Database.File;

                try
                {
                    DxfItem item = file.AddObject(type, null, b =>
                    {
                        b.Group(330, "0");
                        b.Group(100, marker);

                        // AutoCAD lists the flag of cloning first; a first group of the list's own is that flag.
                        if (type == "XRECORD" && file.Supports(1015) && !(body.Count > 0 && (int)body[0].Car == 280))
                        {
                            b.Group(280, "1");
                        }

                        foreach (KeyValuePair<int, string> group in groups)
                        {
                            b.Group(group.Key, group.Value);
                        }
                    });
                    made = item.Handle;

                    foreach (DictEntry entry in entries ?? new List<DictEntry>())
                    {
                        DxfItem named = file.ObjectItem(entry.Handle);

                        if (named != null)
                        {
                            Adopt(file, made, named, false);
                        }
                    }
                }
                catch (InvalidOperationException exception)
                {
                    GeometryHelperLog.Debug("LispDrawing: entmake of a " + type + " was refused: " + exception.Message);
                }
            });

            if (applied.Succeeded && made != null)
            {
                result = wantName ? new LispEntityName(made) : list;
            }

            return true;
        }

        /// <summary>The type of a list that is the list of an XRECORD or a DICTIONARY, its first group but those of the name -1 being <c>(0 . "XRECORD")</c> or <c>(0 . "DICTIONARY")</c>; null for any other.</summary>
        private static string ObjectTypeOf(object list)
        {
            for (object cell = list; cell is LispCons item; cell = item.Cdr)
            {
                if (!(item.Car is LispCons group) || !(group.Car is int code))
                {
                    return null;
                }

                if (code == -1)
                {
                    continue;
                }

                string type = code == 0 && group.Cdr is string text ? text.ToUpperInvariant() : null;
                return type == "XRECORD" || type == "DICTIONARY" ? type : null;
            }

            return null;
        }

        private static LispException BadGroup(LispCons group) =>
            new LispException("bad DXF group: " + LispInterpreter.Print(group, true));

        /// <summary>
        /// The groups of an XRECORD's data as a file holds them. null for a list that makes nothing (a second 100, a 0 or a 999);
        /// a code outside 1 to 999, or a value of another kind than its code holds, is <c>bad DXF group</c>.
        /// </summary>
        private static List<KeyValuePair<int, string>> XrecordGroups(List<LispCons> body)
        {
            var pairs = new List<KeyValuePair<int, object>>();

            foreach (LispCons group in body)
            {
                int code = (int)group.Car;

                if (code == 100 || code == 0 || code == 999)
                {
                    return null;
                }

                if (code < 0 || code > 999 || !TryKind(code, out DataKind kind))
                {
                    throw BadGroup(group);
                }

                pairs.Add(LispEntityData.Pair(code, ValueOfData(group, kind)));
            }

            return LispEntityData.ToGroups("XRECORD", pairs);
        }

        private enum DataKind
        {
            Text,
            Short,
            Long,
            Real,
            Point,
            Pointer,
        }

        /// <summary>The kind of value a group of an XRECORD's data holds by its code; false for a code that holds none.</summary>
        private static bool TryKind(int code, out DataKind kind)
        {
            kind = DataKind.Text;

            if ((code >= 1 && code <= 9) || (code >= 102 && code <= 109) || (code >= 300 && code <= 319))
            {
                return true;
            }

            if ((code >= 410 && code <= 419) || (code >= 430 && code <= 439) || (code >= 470 && code <= 479))
            {
                return true;
            }

            if ((code >= 10 && code <= 18) || (code >= 110 && code <= 112) || code == 210)
            {
                kind = DataKind.Point;
            }
            else if ((code >= 38 && code <= 59) || (code >= 113 && code <= 149) || (code >= 460 && code <= 469))
            {
                kind = DataKind.Real;
            }
            else if (
                (code >= 60 && code <= 79)
                || (code >= 170 && code <= 179)
                || (code >= 270 && code <= 299)
                || (code >= 370 && code <= 389)
                || (code >= 400 && code <= 409)
            )
            {
                kind = DataKind.Short;
            }
            else if ((code >= 90 && code <= 99) || (code >= 420 && code <= 429) || (code >= 440 && code <= 459))
            {
                kind = DataKind.Long;
            }
            else if ((code >= 320 && code <= 369) || (code >= 390 && code <= 399) || code == 480 || code == 481)
            {
                kind = DataKind.Pointer;
            }
            else
            {
                return false;
            }

            return true;
        }

        /// <summary>The value of a group as the file keeps it, of the kind its code holds; <c>bad DXF group</c> for a value of another kind.</summary>
        private static object ValueOfData(LispCons group, DataKind kind)
        {
            object value = group.Cdr;

            switch (kind)
            {
                case DataKind.Text when value is string:
                    return value;
                case DataKind.Short when value is int whole:
                    return (int)(short)whole;
                case DataKind.Long when value is int whole:
                    return whole;
                case DataKind.Real when value is int whole:
                    return (double)whole;
                case DataKind.Real when value is double:
                    return value;
                case DataKind.Pointer when value is LispEntityName:
                    return value;
                case DataKind.Point when value is LispCons:
                    var coordinates = new List<double>(3);

                    for (object cell = value; cell is LispCons next; cell = next.Cdr)
                    {
                        if (next.Car is int x)
                        {
                            coordinates.Add(x);
                        }
                        else if (next.Car is double y)
                        {
                            coordinates.Add(y);
                        }
                        else
                        {
                            throw BadGroup(group);
                        }
                    }

                    if (coordinates.Count < 2 || coordinates.Count > 3)
                    {
                        throw BadGroup(group);
                    }

                    return new[] { coordinates[0], coordinates[1], coordinates.Count > 2 ? coordinates[2] : 0.0 };
                default:
                    throw BadGroup(group);
            }
        }

        /// <summary>
        /// The groups of a DICTIONARY and the entries of it, sorted by name: the flags 280 and 281 (0 and 1 where the list has
        /// none) and for each entry a 3 and a 350 or 360. null for a list that makes nothing: any other group, a name with no
        /// object after it, or an object with no name; <c>bad DXF group</c> for an object that is no name of an entity.
        /// </summary>
        private static List<DictEntry> DictionaryGroups(List<LispCons> body, out List<KeyValuePair<int, string>> groups)
        {
            groups = null;
            var entries = new List<DictEntry>();
            int hard = 0;
            int clone = 1;
            string name = null;

            foreach (LispCons group in body)
            {
                int code = (int)group.Car;

                switch (code)
                {
                    case 280:
                    case 281:
                        if (!(group.Cdr is int flag))
                        {
                            throw BadGroup(group);
                        }

                        if (code == 280)
                        {
                            hard = flag;
                        }
                        else
                        {
                            clone = flag;
                        }

                        break;
                    case 3:
                        if (!(group.Cdr is string text))
                        {
                            throw BadGroup(group);
                        }

                        if (name != null)
                        {
                            return null;
                        }

                        name = text;
                        break;
                    case 350:
                    case 360:
                        if (!(group.Cdr is LispEntityName named))
                        {
                            throw BadGroup(group);
                        }

                        if (name == null)
                        {
                            return null;
                        }

                        entries.Insert(PlaceFor(entries, name), new DictEntry(name, named.Handle, code));
                        name = null;
                        break;
                    default:
                        return null;
                }
            }

            if (name != null)
            {
                return null;
            }

            groups = new List<KeyValuePair<int, string>>
            {
                new KeyValuePair<int, string>(280, hard.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new KeyValuePair<int, string>(281, clone.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            };

            foreach (DictEntry entry in entries)
            {
                groups.Add(new KeyValuePair<int, string>(3, entry.Name));
                groups.Add(new KeyValuePair<int, string>(entry.Code, entry.Handle));
            }

            return entries;
        }
    }
}
