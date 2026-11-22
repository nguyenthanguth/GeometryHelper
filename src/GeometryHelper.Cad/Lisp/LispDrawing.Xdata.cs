using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// The extended data of an object as AutoLISP sees it: <c>regapp</c>, which registers the application name that data is
    /// kept under, <c>xdroom</c> and <c>xdsize</c>, which count its bytes against the 16 383 an object may hold, and the rules
    /// <c>entmod</c> and <c>entmake</c> follow for a group -3 (AutoCAD 2021, probes x1 to x3 of C4b): an application not
    /// registered is refused with <c>Invalid application name in 1001 group</c>, the data written is put first among the
    /// object's, and an application given with nothing after its name loses its data.
    /// </summary>
    internal static partial class LispDrawing
    {
        /// <summary>The bytes of extended data an object may hold.</summary>
        private const int XdataLimit = 16383;

        static partial void RegisterXdata(LispInterpreter lisp, CadCommandSession session)
        {
            DrawingState state = StateOf(lisp);

            lisp.Define("REGAPP", 1, 1, a => RegApp(lisp, session, LispInterpreter.Str(a[0])));
            lisp.Define("XDROOM", 1, 1, a =>
            {
                LispEntityName name = NameOf(a[0]);
                return lisp.OnOwner(() => XdRoom(session, state, name));
            });
            lisp.Define("XDSIZE", 1, 1, a => lisp.OnOwner(() => XdSize(session, a[0])));
        }

        #region regapp

        /// <summary>
        /// <c>(regapp name)</c>: adds the name to the table of applications. Gives the name where it was added, and nil where the
        /// table has it already (letters of either case the same) or the name cannot be one (empty, or with one of
        /// <c>&lt; &gt; / \ " : ; ? * | , = `</c> in it); AutoCAD 2021, probe x1.
        /// </summary>
        private static object RegApp(LispInterpreter lisp, CadCommandSession session, string name)
        {
            var given = new List<KeyValuePair<int, object>>
            {
                LispEntityData.Pair(0, "APPID"),
                LispEntityData.Pair(100, "AcDbSymbolTableRecord"),
                LispEntityData.Pair(100, "AcDbRegAppTableRecord"),
                LispEntityData.Pair(2, name),
                LispEntityData.Pair(70, 0),
            };

            // A name with one of the characters no table takes is nil, a comma and a semicolon among them (probe x1).
            if (name.IndexOfAny(new[] { '<', '>', '/', '\\', '"', ':', ';', '?', '*', '|', ',', '=', '`' }) >= 0)
            {
                return null;
            }

            return lisp.OnOwner(() => MakeRecord(lisp, session, "APPID", given, name));
        }

        #endregion

        #region xdroom and xdsize

        /// <summary>
        /// <c>(xdroom ename)</c>: how many bytes of extended data an object can still take, 16 383 less what it holds, as
        /// <see cref="XdataSize(IReadOnlyList{DxfTypedValue})"/> counts it; nil for a name that names nothing. An entity, a record
        /// of a table and an object of the OBJECTS section all take it (probe x1: a layer with a 12-byte text gives 16 371).
        /// </summary>
        private static object XdRoom(CadCommandSession session, DrawingState state, LispEntityName name)
        {
            DxfItem item = FindItem(session.Document, state.Resolve(name.Handle));
            return item == null ? null : (object)(XdataLimit - XdataSize(item.File.XDataOf(item)));
        }

        /// <summary>
        /// The bytes the extended data of a record takes, as AutoCAD counts them (probes x1 to x3): an application with any data
        /// takes 3 for its name and then each group takes its code and its value: a string 3 and two for each character, a
        /// brace 2, a layer's name 3, binary data 2 and a byte for each two digits, a handle 9, a point, a world position,
        /// displacement or direction 25, a real, distance or scale 9, an integer 3 and a long 5. The text "B-12" under one
        /// application takes 3 + 3 + 2 * 4 = 14.
        /// </summary>
        private static int XdataSize(IReadOnlyList<DxfTypedValue> groups)
        {
            int total = 0;
            int application = 0;

            foreach (DxfTypedValue group in groups)
            {
                if (group.TypeCode == 1001)
                {
                    total += application > 0 ? 3 + application : 0;
                    application = 0;
                    continue;
                }

                application += GroupSize(group.TypeCode, group.Value);
            }

            return total + (application > 0 ? 3 + application : 0);
        }

        /// <summary>The bytes of one group of extended data: <see cref="XdataSize(IReadOnlyList{DxfTypedValue})"/> says what each takes; the second and third coordinates of a point take none of their own.</summary>
        private static int GroupSize(int code, string value)
        {
            switch (code)
            {
                case 1000:
                    return 3 + 2 * value.Length;
                case 1002:
                    return 2;
                case 1003:
                    return 3;
                case 1004:
                    return 2 + value.Trim().Length / 2;
                case 1005:
                    return 9;
                case 1010:
                case 1011:
                case 1012:
                case 1013:
                    return 25;
                case 1040:
                case 1041:
                case 1042:
                    return 9;
                case 1070:
                    return 3;
                case 1071:
                    return 5;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// <c>(xdsize list)</c>: the bytes a list of extended data would take, the list as the value of a group -3 is,
        /// <c>(("APP" (1000 . "abc")))</c>. nil where an application is not registered or a layer is not in the drawing. A list
        /// that is not made of applications and groups of the right kinds is <c>bad xdata list</c> with the list, and binary data
        /// of other characters than hexadecimal digits is <c>invalid character in 1004 group</c> (probes x1 to x3). The list
        /// <c>(("APPA" (1000 . "abc")))</c> takes 12, <c>(("APPA"))</c> takes none.
        /// </summary>
        private static object XdSize(CadCommandSession session, object list)
        {
            if (!TryApplications(list, out List<KeyValuePair<string, List<KeyValuePair<int, object>>>> applications))
            {
                throw new LispException("bad xdata list: " + LispInterpreter.Print(list, true));
            }

            CadDocument document = session.Document;
            int total = 0;
            var counted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, List<KeyValuePair<int, object>>> application in applications)
            {
                foreach (KeyValuePair<int, object> entry in application.Value)
                {
                    if (entry.Key == 1004 && !IsHex((string)entry.Value))
                    {
                        throw new LispException("invalid character in 1004 group");
                    }
                }
            }

            foreach (KeyValuePair<string, List<KeyValuePair<int, object>>> application in applications)
            {
                if (!IsRegistered(document, application.Key))
                {
                    return null;
                }

                if (!counted.Add(application.Key))
                {
                    continue;
                }

                int data = 0;

                foreach (KeyValuePair<int, object> entry in application.Value)
                {
                    if (entry.Key == 1003 && !document.Database.LayerTable.Has((string)entry.Value))
                    {
                        return null;
                    }

                    data += entry.Value is string text
                        ? GroupSize(entry.Key, text)
                        : GroupSize(entry.Key, string.Empty);
                }

                total += application.Value.Count == 0 ? 0 : 3 + data;
            }

            return total;
        }

        /// <summary>Whether the table of applications has a name, letters of either case the same.</summary>
        private static bool IsRegistered(CadDocument document, string name) =>
            IndexOfRecord(RecordsOf(document, "APPID"), name) >= 0;

        private static bool IsHex(string text)
        {
            foreach (char c in text.Trim())
            {
                if (!Uri.IsHexDigit(c))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The applications of a list of extended data, each its name and its groups as pairs of a code and a value (a string, a
        /// number, or the three coordinates of a point); false for a list that is not one: an item that is not a name followed by
        /// groups, a code outside 1000 to 1071 or one that holds no value (1006 to 1009, 1014 to 1039, 1043 to 1069), or a value
        /// of the wrong kind for its code.
        /// </summary>
        private static bool TryApplications(
            object list,
            out List<KeyValuePair<string, List<KeyValuePair<int, object>>>> applications
        )
        {
            applications = new List<KeyValuePair<string, List<KeyValuePair<int, object>>>>();
            object cell = list;

            while (cell is LispCons item)
            {
                if (!(item.Car is LispCons entry) || !(entry.Car is string name))
                {
                    return false;
                }

                var groups = new List<KeyValuePair<int, object>>();
                object rest = entry.Cdr;

                while (rest is LispCons next)
                {
                    if (!TryGroup(next.Car, out KeyValuePair<int, object> group))
                    {
                        return false;
                    }

                    groups.Add(group);
                    rest = next.Cdr;
                }

                if (rest != null)
                {
                    return false;
                }

                applications.Add(new KeyValuePair<string, List<KeyValuePair<int, object>>>(name, groups));
                cell = item.Cdr;
            }

            return cell == null && applications.Count > 0;
        }

        /// <summary>A group of extended data of a list: its code and its value, a string, a real or whole number, or a point as three coordinates; false where it is no group of 1000 to 1071 or its value is of another kind.</summary>
        private static bool TryGroup(object item, out KeyValuePair<int, object> group)
        {
            group = default;

            if (!(item is LispCons cons) || !(cons.Car is int code))
            {
                return false;
            }

            object value = cons.Cdr;

            switch (code)
            {
                case 1000:
                case 1001:
                case 1002:
                case 1003:
                case 1004:
                case 1005:
                    if (!(value is string))
                    {
                        return false;
                    }

                    break;
                case 1010:
                case 1011:
                case 1012:
                case 1013:
                    var coordinates = new List<double>(3);
                    object cell = value;

                    while (cell is LispCons next)
                    {
                        if (next.Car is int whole)
                        {
                            coordinates.Add(whole);
                        }
                        else if (next.Car is double real)
                        {
                            coordinates.Add(real);
                        }
                        else
                        {
                            return false;
                        }

                        cell = next.Cdr;
                    }

                    if (cell != null || coordinates.Count < 2 || coordinates.Count > 3)
                    {
                        return false;
                    }

                    value = new[] { coordinates[0], coordinates[1], coordinates.Count > 2 ? coordinates[2] : 0.0 };
                    break;
                case 1040:
                case 1041:
                case 1042:
                case 1071:
                    if (value is int integer)
                    {
                        value = (double)integer;
                    }
                    else if (!(value is double))
                    {
                        return false;
                    }

                    break;
                case 1070:
                    if (!(value is int))
                    {
                        return false;
                    }

                    break;
                default:
                    return false;
            }

            group = LispEntityData.Pair(code, value);
            return true;
        }

        #endregion

        #region A group -3 of entmod and entmake

        /// <summary>The extended data a list given to <c>entmod</c> or <c>entmake</c> asks to be written: the groups of the last group -3, and the names every group -3 gave.</summary>
        private sealed class XdataChange
        {
            /// <summary>The groups to write, the application of each opened by its 1001, those with no data left out.</summary>
            internal List<DxfTypedValue> Written { get; } = new List<DxfTypedValue>();

            /// <summary>The applications whose data the object loses before these are written: every one a group -3 names.</summary>
            internal HashSet<string> Named { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Reads the groups -3 of a list given to <c>entmod</c> or <c>entmake</c> (probes x1 to x3). A value that is no list of
        /// applications is the error <c>bad DXF group: (-3 ...)</c>; an application not in the table of applications writes
        /// <c>Invalid application name in 1001 group</c>, a brace left open or shut too often <c>Improper nesting in application
        /// data.</c>, a code of no meaning (1099) <c>Unrecognized field type</c>, a handle (1005) of no object nothing, and the
        /// call gives nil, false here. Where the list has several groups -3 the applications of the last are written and those
        /// of every one lose their old data. True with a null change for a list with none.
        /// </summary>
        private static bool TryXdata(
            LispInterpreter lisp,
            CadDocument document,
            List<KeyValuePair<int, object>> given,
            out XdataChange change
        )
        {
            change = null;

            foreach (KeyValuePair<int, object> pair in given)
            {
                if (pair.Key != -3)
                {
                    continue;
                }

                change = change ?? new XdataChange();
                change.Written.Clear();

                if (pair.Value == null)
                {
                    continue;
                }

                if (HasUnknownCode(pair.Value))
                {
                    lisp.Write("Unrecognized field type");
                    return false;
                }

                if (!TryApplications(pair.Value, out var applications))
                {
                    throw new LispException(
                        "bad DXF group: " + LispInterpreter.Print(new LispCons(-3, pair.Value), true)
                    );
                }

                foreach (KeyValuePair<string, List<KeyValuePair<int, object>>> application in applications)
                {
                    if (!IsRegistered(document, application.Key))
                    {
                        lisp.Write("Invalid application name in 1001 group");
                        return false;
                    }

                    if (!BracesAreShut(application.Value))
                    {
                        lisp.Write("Improper nesting in application data.");
                        return false;
                    }
                }

                foreach (KeyValuePair<string, List<KeyValuePair<int, object>>> application in applications)
                {
                    if (!AddApplication(lisp, document, change, application))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>Whether a list of extended data has a group whose code is 1000 or more and none of those that hold a value (1000 to 1005, 1010 to 1013, 1040 to 1042, 1070, 1071).</summary>
        private static bool HasUnknownCode(object list)
        {
            for (object cell = list; cell is LispCons item; cell = item.Cdr)
            {
                if (!(item.Car is LispCons entry))
                {
                    continue;
                }

                for (object rest = entry.Cdr; rest is LispCons next; rest = next.Cdr)
                {
                    if (next.Car is LispCons group && group.Car is int code && code >= 1000 && !IsXdataCode(code))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsXdataCode(int code) =>
            (code >= 1000 && code <= 1005)
            || (code >= 1010 && code <= 1013)
            || (code >= 1040 && code <= 1042)
            || code == 1070
            || code == 1071;

        /// <summary>Whether the braces (1002) of one application's groups are shut as they are opened, none shut before it was opened.</summary>
        private static bool BracesAreShut(List<KeyValuePair<int, object>> groups)
        {
            int depth = 0;

            foreach (KeyValuePair<int, object> group in groups)
            {
                if (group.Key != 1002)
                {
                    continue;
                }

                string brace = ((string)group.Value).Trim();
                depth +=
                    brace == "{" ? 1
                    : brace == "}" ? -1
                    : 0;

                if (depth < 0)
                {
                    return false;
                }
            }

            return depth == 0;
        }

        /// <summary>
        /// Puts the groups of one application of a group -3 into the change, the application of the name the group opens (its old
        /// data is dropped even where it is given none); a 1001 among its groups starts that application there. A 1001 of an
        /// application not registered writes the message of <see cref="TryXdata"/>, and a handle (1005) the drawing has no object
        /// of writes none; both refuse the change, false.
        /// </summary>
        private static bool AddApplication(
            LispInterpreter lisp,
            CadDocument document,
            XdataChange change,
            KeyValuePair<string, List<KeyValuePair<int, object>>> application
        )
        {
            change.Named.Add(application.Key.Trim());
            var groups = new List<DxfTypedValue>();
            string current = application.Key;

            foreach (KeyValuePair<int, object> group in application.Value)
            {
                if (group.Key == 1001)
                {
                    string other = (string)group.Value;

                    if (!IsRegistered(document, other))
                    {
                        lisp.Write("Invalid application name in 1001 group");
                        return false;
                    }

                    FlushApplication(change, current, groups);
                    change.Named.Add(other.Trim());
                    current = other;
                    groups = new List<DxfTypedValue>();
                    continue;
                }

                if (group.Key == 1005 && FindItem(document, ((string)group.Value).Trim()) == null)
                {
                    return false;
                }

                foreach (KeyValuePair<int, string> text in LispEntityData.ToGroups(string.Empty, new[] { group }))
                {
                    groups.Add(new DxfTypedValue(text.Key, text.Value));
                }
            }

            FlushApplication(change, current, groups);
            return true;
        }

        /// <summary>Adds an application's groups to the ones to write, opened by its name, where it has any; one of that name written before is dropped, the later one standing.</summary>
        private static void FlushApplication(XdataChange change, string name, List<DxfTypedValue> groups)
        {
            if (groups.Count == 0)
            {
                return;
            }

            int at = 0;

            while (at < change.Written.Count)
            {
                if (
                    change.Written[at].TypeCode == 1001
                    && string.Equals(change.Written[at].Value, name, StringComparison.OrdinalIgnoreCase)
                )
                {
                    int end = at + 1;

                    while (end < change.Written.Count && change.Written[end].TypeCode != 1001)
                    {
                        end++;
                    }

                    change.Written.RemoveRange(at, end - at);
                    continue;
                }

                at++;
            }

            change.Written.Add(new DxfTypedValue(1001, name));
            change.Written.AddRange(groups);
        }

        /// <summary>
        /// Writes the extended data of a change into a record: the applications the change names lose their old data, then the
        /// groups of the change come first and the data of the other applications after them, in the order they had (probe x2:
        /// the application written last is the first <c>entget</c> gives).
        /// </summary>
        private static void WriteXdata(DxfFile file, DxfItem item, XdataChange change)
        {
            var groups = new List<DxfTypedValue>(change.Written);
            bool keeping = false;

            foreach (DxfTypedValue group in file.XDataOf(item))
            {
                if (group.TypeCode == 1001)
                {
                    keeping = !change.Named.Contains(group.Value.Trim());
                }

                if (keeping)
                {
                    groups.Add(group);
                }
            }

            file.SetXData(item, groups);
        }

        #endregion
    }
}
