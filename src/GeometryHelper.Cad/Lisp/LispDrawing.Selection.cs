using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// <c>ssget</c> with its modes and filters, and <c>sslength</c>, <c>ssname</c>, <c>ssadd</c>, <c>ssdel</c> and
    /// <c>ssmemb</c>. A selection set holds the handles of its entities, in the order they were taken. Every set <c>ssget</c>
    /// gives becomes the Previous selection, which <c>(ssget "P")</c> and the <c>P</c> typed at "Select objects:" give again.
    /// </summary>
    internal static partial class LispDrawing
    {
        static partial void RegisterSelection(LispInterpreter lisp, CadCommandSession session)
        {
            DrawingState state = StateOf(lisp);

            lisp.Define("SSGET", 0, -1, a => SsGet(lisp, session, state, a));
            lisp.Define("SSLENGTH", 1, 1, a => SetOf(a[0]).Handles.Count);
            lisp.Define("SSNAME", 2, 2, a =>
            {
                LispSelectionSet set = SetOf(a[0]);
                int at = LispInterpreter.Int(a[1]);
                return at < 0 || at >= set.Handles.Count ? null : new LispEntityName(set.Handles[at]);
            });
            lisp.Define("SSADD", 0, 2, a => SsAdd(a));
            lisp.Define("SSDEL", 2, 2, a =>
            {
                LispEntityName name = NameOf(a[0]);
                LispSelectionSet set = SetOf(a[1]);
                return set.Remove(name.Handle) ? set : null;
            });
            lisp.Define("SSMEMB", 2, 2, a =>
            {
                LispEntityName name = NameOf(a[0]);
                return SetOf(a[1]).Contains(name.Handle) ? name : null;
            });
        }

        /// <summary>The set an argument has to be: <c>bad argument type: lselsetp nil</c> for anything else (q4).</summary>
        private static LispSelectionSet SetOf(object value) =>
            value as LispSelectionSet ?? throw LispInterpreter.TypeError("lselsetp", value);

        /// <summary><c>(ssadd)</c> a new empty set, <c>(ssadd e)</c> a new set of it, <c>(ssadd e ss)</c> adds it to the set in place and gives the set.</summary>
        private static object SsAdd(object[] a)
        {
            if (a.Length == 0)
            {
                return new LispSelectionSet(new string[0]);
            }

            LispEntityName name = NameOf(a[0]);

            if (a.Length == 1)
            {
                return new LispSelectionSet(new[] { name.Handle });
            }

            LispSelectionSet set = SetOf(a[1]);
            set.Add(name.Handle);
            return set;
        }

        #region ssget

        /// <summary>What <c>ssget</c> was asked: how to find the objects and which of them to keep.</summary>
        internal sealed class Request
        {
            internal string Mode { get; set; }

            internal bool Interactive { get; set; }

            internal bool Single { get; set; }

            internal bool Locked { get; set; }

            internal GeoPoint2? Point { get; set; }

            internal GeoPoint2? Corner { get; set; }

            internal List<GeoPoint2> Points { get; set; }

            internal LispSelectionFilter Filter { get; set; }
        }

        private static object SsGet(LispInterpreter lisp, CadCommandSession session, DrawingState state, object[] a)
        {
            Tolerance tolerance = session.Tolerance;

            if (!TryRequest(a, tolerance, out Request request))
            {
                return null;
            }

            if (request.Interactive)
            {
                // The objects selected before the routine ran are the answer, as AutoCAD 2021 takes them: nothing is asked.
                if (TakeImplied(lisp, session, request) is LispSelectionSet implied)
                {
                    return implied;
                }

                LispAsk ask = lisp.NewAsk(LispAskKind.Selection, null);
                ask.Single = request.Single;

                if (request.Locked)
                {
                    ask.Locked = handle => OnLockedLayer(session, handle);
                }

                if (request.Filter != null)
                {
                    ask.Accepts = handle => Accepted(session, request.Filter, handle);
                }

                object answer = lisp.Ask(ask);

                if (answer is string keyword)
                {
                    return keyword;
                }

                return lisp.OnOwner(() => Taken(session, answer as List<string> ?? new List<string>()));
            }

            return lisp.OnOwner(() => Taken(session, Find(session, request)));
        }

        /// <summary>
        /// The pick-first selection as an <c>ssget</c> that asks takes it (AutoCAD 2021, console, probes of the pickfirstc1 task), while
        /// PICKFIRST is not 0: the objects that pass the filter are the set, "N found" is written (not for <c>":S"</c>) with "N was on
        /// a locked layer." for those <c>":L"</c> leaves out, the set becomes the Previous selection, and the selection of the document
        /// is cleared, filter or no. Where no object is left the user is asked, after the "0 found".
        /// </summary>
        /// <returns>The set; null where nothing is selected, PICKFIRST is 0, or no object is left, and the user is to be asked.</returns>
        private static object TakeImplied(LispInterpreter lisp, CadCommandSession session, Request request)
        {
            var taken = new List<string>();
            int locked = 0;

            bool any = (bool)
                lisp.OnOwner(() =>
                {
                    if (session.Document.PickFirst == 0)
                    {
                        return false;
                    }

                    List<string> selected = PickFirstHandles(session);

                    if (selected.Count == 0)
                    {
                        return false;
                    }

                    foreach (string handle in selected)
                    {
                        if (request.Filter != null && !Accepted(session, request.Filter, handle))
                        {
                            continue;
                        }

                        if (request.Locked && OnLockedLayer(session, handle))
                        {
                            locked++;
                        }
                        else
                        {
                            taken.Add(handle);
                        }
                    }

                    session.Document.Selection.Clear();
                    session.RememberSelection(taken);

                    var said = new List<string>();

                    if (!request.Single)
                    {
                        said.Add((taken.Count + locked).ToString(CultureInfo.InvariantCulture) + " found");
                    }

                    if (locked > 0)
                    {
                        said.Add(
                            locked.ToString(CultureInfo.InvariantCulture)
                                + (locked == 1 ? " was" : " were")
                                + " on a locked layer."
                        );
                    }

                    session.WriteLispLines(said);
                    return true;
                });

            if (!any)
            {
                return null;
            }

            return taken.Count == 0 ? null : new LispSelectionSet(taken);
        }

        /// <summary>Reads the arguments of <c>ssget</c>: <c>[mode] [points] [filter]</c>; false for arguments that ask for nothing.</summary>
        private static bool TryRequest(object[] a, Tolerance tolerance, out Request request)
        {
            request = new Request();
            int next = 0;
            object filter = null;

            if (a.Length > 0 && a[0] is string text)
            {
                string mode = text.Trim().ToUpperInvariant().TrimStart('_');
                request.Mode = mode;
                next = 1;

                // AutoCAD 2021 (console, l2p pb): (ssget "_X:L") is "bad ssget mode string"; :L is for the prompt alone.
                if (!mode.StartsWith(":", StringComparison.Ordinal) && mode.IndexOf(":L", StringComparison.Ordinal) > 0)
                {
                    throw new LispException("bad ssget mode string");
                }

                if (mode.StartsWith(":", StringComparison.Ordinal))
                {
                    request.Interactive = true;
                    request.Single = mode.IndexOf('S') > 0;
                    request.Locked = mode.IndexOf('L') > 0;
                    filter = a.Length > 1 ? a[1] : null;

                    // AutoCAD 2021 (console, l2p pb): (ssget "_:L" "x") is "bad point argument".
                    if (request.Locked && filter is string)
                    {
                        throw new LispException("bad point argument");
                    }

                    return LispSelectionFilter.TryParse(filter, tolerance, out LispSelectionFilter parsed)
                        && Assign(request, parsed);
                }

                switch (mode)
                {
                    case "X":
                    case "L":
                    case "P":
                    case "I":
                        filter = a.Length > 1 ? a[1] : null;
                        break;
                    case "W":
                    case "C":
                        if (
                            a.Length < 3
                            || (request.Point = Point2(a[1])) == null
                            || (request.Corner = Point2(a[2])) == null
                        )
                        {
                            return false;
                        }

                        filter = a.Length > 3 ? a[3] : null;
                        break;
                    case "WP":
                    case "CP":
                    case "F":
                        if (a.Length < 2 || !TryPoints(a[1], out List<GeoPoint2> ring))
                        {
                            return false;
                        }

                        request.Points = ring;
                        filter = a.Length > 2 ? a[2] : null;
                        break;
                    default:
                        return false;
                }
            }
            else if (a.Length > 0 && IsPointList(a[0]))
            {
                request.Mode = "PICK";
                request.Point = Point2(a[0]);
                filter = a.Length > 1 ? a[1] : null;
            }
            else
            {
                // A filter alone, or nothing: the user is asked.
                request.Interactive = true;
                filter = a.Length > 0 ? a[0] : null;
            }

            if (next < 0 || !LispSelectionFilter.TryParse(filter, tolerance, out LispSelectionFilter read))
            {
                return false;
            }

            return Assign(request, read);
        }

        private static bool Assign(Request request, LispSelectionFilter filter)
        {
            request.Filter = filter;
            return true;
        }

        private static bool IsPointList(object value) =>
            value is LispCons cons && (cons.Car is int || cons.Car is double);

        private static GeoPoint2? Point2(object value)
        {
            GeoPoint3? point = PointOf(value);
            return point.HasValue ? new GeoPoint2(point.Value.X, point.Value.Y) : (GeoPoint2?)null;
        }

        private static bool TryPoints(object list, out List<GeoPoint2> points)
        {
            points = new List<GeoPoint2>();

            if (!(list is LispCons))
            {
                return false;
            }

            foreach (object item in LispInterpreter.ItemsOf(list))
            {
                GeoPoint2? point = Point2(item);

                if (!point.HasValue)
                {
                    return false;
                }

                points.Add(point.Value);
            }

            return true;
        }

        /// <summary>The set an <c>ssget</c> gives for the handles it found: nil for none, else the set, which is the Previous selection from now on.</summary>
        private static object Taken(CadCommandSession session, List<string> handles)
        {
            if (handles.Count == 0)
            {
                return null;
            }

            session.RememberSelection(handles);
            return new LispSelectionSet(handles);
        }

        /// <summary>Whether the object of a handle is on a locked layer, for <c>ssget</c> with <c>:L</c>; owner thread only.</summary>
        private static bool OnLockedLayer(CadCommandSession session, string handle)
        {
            DxfEntity entity = session.Document.FindEntity(handle);
            return entity != null && CadLayerEditor.IsLocked(session.Document.Database, entity.Layer);
        }

        /// <summary>Whether the filter keeps the object of a handle; owner thread only.</summary>
        internal static bool Accepted(CadCommandSession session, LispSelectionFilter filter, string handle)
        {
            DxfItem item = FindItem(session.Document, handle);

            if (item == null)
            {
                return false;
            }

            if (filter.OnlyType)
            {
                return filter.Accepts(new[] { LispEntityData.Pair(0, item.Type) }, new string[0]);
            }

            List<KeyValuePair<int, object>> groups = GroupsOf(session.Document, item);
            var applications = new List<string>();

            foreach (var application in LispEntityData.ReadExtended(item.Groups))
            {
                applications.Add(application.Key);
            }

            return filter.Accepts(groups, applications);
        }

        /// <summary>The groups of an object as <c>entget</c> gives them without its name: <c>(0 . type)</c> first.</summary>
        internal static List<KeyValuePair<int, object>> GroupsOf(CadDocument document, DxfItem item)
        {
            List<KeyValuePair<int, object>> pairs = LispEntityData.Read(item.Groups, item.Type);

            if (item.Parent == null && item.Space != null)
            {
                pairs = Listed(document, item, pairs);
            }

            pairs.Insert(0, LispEntityData.Pair(0, item.Type));
            return pairs;
        }

        /// <summary>
        /// The width and height the words of an MTEXT take, measured in the fonts of the document as the drawing is drawn in them
        /// (the folders of <see cref="CadDocument.Fonts"/> and the Fonts folder of Windows) where the drawing has no search path of its
        /// own, as the table editor and the multileader commands measure; as the record has them where no font measures the words.
        /// </summary>
        private static void MeasureWords(CadDocument document, DxfMText text, out double width, out double height)
        {
            DxfDatabase database = document.Database;
            string searched = database.FontSearchPath;
            bool borrow = document.Fonts != null && string.IsNullOrEmpty(searched);

            try
            {
                if (borrow)
                {
                    database.FontSearchPath = string.Join(
                        ";",
                        document.Fonts.Folders.Concat(
                            new[] { Environment.GetFolderPath(Environment.SpecialFolder.Fonts) }
                        )
                    );
                }

                width = text.ActualWidth;
                height = text.ActualHeight;
            }
            finally
            {
                if (borrow)
                {
                    database.FontSearchPath = searched;
                }
            }
        }

        /// <summary>Gives a group the value, in its place where the list has one of its code, else last.</summary>
        private static void SetOrAdd(List<KeyValuePair<int, object>> pairs, int code, object value)
        {
            int at = pairs.FindIndex(p => p.Key == code);

            if (at >= 0)
            {
                pairs[at] = LispEntityData.Pair(code, value);
            }
            else
            {
                pairs.Add(LispEntityData.Pair(code, value));
            }
        }

        /// <summary>
        /// The groups of an entity of a space or of a block definition, or of a record that follows one (a vertex, an attribute,
        /// the end of a sequence), as <c>entget</c> gives them: <see cref="LispEntityData.Complete"/> with the owner, the space the
        /// entity is in (no <c>410</c> in a block definition) and, where the record has none, the groups the presenter holds
        /// elsewhere: an MTEXT's extents (42 and 43, always measured by its own font where the drawing's fonts measure the words,
        /// as AutoCAD 2021 measures them: 24.1667 and 3.33333 for "mtext body" at 2.5 in txt) and its direction and angle, a vertex of a 3D polyline's flag 32. A SEQEND ends with
        /// <c>(-2 . the entity it ends)</c>, as AutoCAD 2021 lists it (console, l2p pa). <paramref name="pairs"/> is the caller's own list
        /// from <see cref="LispEntityData.Read"/>, which this adds to.
        /// </summary>
        internal static List<KeyValuePair<int, object>> Listed(
            CadDocument document,
            DxfItem item,
            List<KeyValuePair<int, object>> pairs
        )
        {
            DxfItem top = item.Parent ?? item;
            CadSpace space = document.FindSpace(top.Handle);
            bool inBlock = space == null;
            string layer = pairs.Find(p => p.Key == 8).Value as string;

            if (item.Type == "MTEXT" && DxfEntity.Of(item) is DxfMText text)
            {
                // AutoCAD works the extents out of the words each time and ignores a 42 and a 43 a list gives (console, l2p2 pf: 99 and
                // 88 given, 24.1667 and 3.33333 listed); where no font measures the words they are the record's own.
                MeasureWords(document, text, out double wide, out double high);
                SetOrAdd(pairs, 42, wide);
                SetOrAdd(pairs, 43, high);

                if (!pairs.Exists(p => p.Key == 11))
                {
                    GeoVector3 direction = text.Direction;
                    pairs.Add(LispEntityData.Pair(11, new[] { direction.X, direction.Y, direction.Z }));
                }

                if (!pairs.Exists(p => p.Key == 50))
                {
                    pairs.Add(LispEntityData.Pair(50, text.Rotation));
                }
            }
            else if (
                item.Type == "VERTEX"
                && item.Parent != null
                && !pairs.Exists(p => p.Key == 70)
                && LispEntityData
                    .Read(item.Parent.Groups, item.Parent.Type)
                    .Exists(p => p.Key == 70 && p.Value is int flags && (flags & 8) != 0)
            )
            {
                pairs.Add(LispEntityData.Pair(70, 32));
            }

            pairs = LispEntityData.Complete(
                item.Type,
                pairs,
                item.File.OwnerOf(item),
                inBlock ? null : space?.Name ?? "Model",
                space != null && !space.IsModel,
                layer
            );

            if (item.Type == "SEQEND" && item.Parent != null)
            {
                pairs.Add(LispEntityData.Pair(-2, new LispEntityName(item.Parent.Handle)));
            }

            return pairs;
        }

        /// <summary>
        /// The groups of the BLOCK that opens a block definition (<c>(entget (tblobjname "BLOCK" name))</c>) as AutoCAD 2021 lists
        /// them (console, l2p pa): the owner and handle, <c>AcDbEntity</c> with 67 and the layer, <c>AcDbBlockBegin</c> with the
        /// flags and the base point, the first entity of the block (-2), then the name (2) and the path (1, empty for a block that
        /// is no reference to a file; the name the block was given for a block made by <c>entmake</c>, <c>*U</c> for an anonymous one).
        /// A block with no entity has its ENDBLK as its -2 (AutoCAD 2021, console, l2p2 pf).
        /// </summary>
        internal static List<KeyValuePair<int, object>> BlockEntityList(
            CadDocument document,
            DxfItem head,
            List<KeyValuePair<int, object>> pairs
        )
        {
            object owner = null;
            object name = string.Empty;
            object path = string.Empty;
            object layer = "0";
            object flags = 0;
            object origin = new[] { 0.0, 0.0, 0.0 };

            foreach (KeyValuePair<int, object> pair in pairs)
            {
                switch (pair.Key)
                {
                    case 330 when owner == null:
                        owner = pair.Value;
                        break;
                    case 8:
                        layer = pair.Value;
                        break;
                    case 2:
                        name = pair.Value;
                        break;
                    case 70:
                        flags = pair.Value;
                        break;
                    case 1:
                        path = pair.Value;
                        break;
                    case 10 when pair.Value is double[] point:
                        origin = new[] { point[0], point[1], point.Length > 2 ? point[2] : 0.0 };
                        break;
                }
            }

            var result = new List<KeyValuePair<int, object>>
            {
                LispEntityData.Pair(330, owner ?? new LispEntityName(head.File.OwnerOf(head) ?? "0")),
                LispEntityData.Pair(5, head.Handle),
                LispEntityData.Pair(100, "AcDbEntity"),
                LispEntityData.Pair(67, 0),
                LispEntityData.Pair(8, layer),
                LispEntityData.Pair(100, "AcDbBlockBegin"),
                LispEntityData.Pair(70, flags),
                LispEntityData.Pair(10, origin),
            };

            DxfBlockTable table = document.Database.BlockTable;
            string text = (string)name;
            bool first = false;

            if (table.Has(text) && table[text] is DxfBlockTableRecord block)
            {
                foreach (DxfItem entity in Items(block))
                {
                    result.Add(LispEntityData.Pair(-2, new LispEntityName(entity.Handle)));
                    first = true;
                    break;
                }
            }

            // A block with no entity names its ENDBLK as its first (AutoCAD 2021, console, l2p2 pf).
            if (!first && EndOf(document, text) is LispEntityName end)
            {
                result.Add(LispEntityData.Pair(-2, end));
            }

            result.Add(LispEntityData.Pair(2, name));
            result.Add(LispEntityData.Pair(1, path));
            return result;
        }

        /// <summary>The name of the ENDBLK of a block, the first entity (-2) of a block that holds none; null where it has no ENDBLK with a handle.</summary>
        internal static LispEntityName EndOf(CadDocument document, string block) =>
            document.Database.File.Blocks.TryGetValue(block, out DxfFileBlock found)
            && found.Tail != null
            && !found.Tail.IsRemoved
            && !string.IsNullOrEmpty(found.Tail.Handle)
                ? new LispEntityName(found.Tail.Handle)
                : null;

        /// <summary>
        /// The groups of the ENDBLK that closes a block definition as AutoCAD 2021 lists them (console, l2p2 pf): the owner and
        /// handle, <c>AcDbEntity</c> with 67 and the layer, then <c>AcDbBlockEnd</c>.
        /// </summary>
        internal static List<KeyValuePair<int, object>> EndBlockEntityList(
            DxfItem tail,
            List<KeyValuePair<int, object>> pairs
        )
        {
            object owner = pairs.Find(p => p.Key == 330).Value;
            object layer = pairs.Find(p => p.Key == 8).Value ?? "0";
            return new List<KeyValuePair<int, object>>
            {
                LispEntityData.Pair(330, owner ?? new LispEntityName(tail.File.OwnerOf(tail) ?? "0")),
                LispEntityData.Pair(5, tail.Handle),
                LispEntityData.Pair(100, "AcDbEntity"),
                LispEntityData.Pair(67, 0),
                LispEntityData.Pair(8, layer),
                LispEntityData.Pair(100, "AcDbBlockEnd"),
            };
        }

        /// <summary>The handles a request finds in the drawing, in the order the set has them.</summary>
        internal static List<string> Find(CadCommandSession session, Request request)
        {
            CadDocument document = session.Document;
            var found = new List<string>();

            switch (request.Mode)
            {
                case "X":
                    foreach (string handle in Everything(document))
                    {
                        found.Add(handle);
                    }

                    break;
                case "PICK":
                    GeoPoint2 at = session.Ucs.ToWorld(request.Point.Value);

                    foreach (
                        string handle in document
                            .GetPickIndex(document.WorkingSpace)
                            .Pick(at.X, at.Y, session.PickAperture)
                    )
                    {
                        found.Add(handle);
                        break;
                    }

                    break;
                case "W":
                case "C":
                    GeoPoint2 first = session.Ucs.ToWorld(request.Point.Value);
                    GeoPoint2 second = session.Ucs.ToWorld(request.Corner.Value);
                    var box = new DisplayBox(
                        Math.Min(first.X, second.X),
                        Math.Min(first.Y, second.Y),
                        Math.Max(first.X, second.X),
                        Math.Max(first.Y, second.Y)
                    );
                    PickIndex window = document.GetPickIndex(document.WorkingSpace);
                    found.AddRange(
                        session.TakenByWindow(request.Mode == "W" ? window.Window(box) : window.Crossing(box), true)
                    );
                    break;
                case "WP":
                case "CP":
                case "F":
                    var ring = new List<GeoPoint2>();

                    foreach (GeoPoint2 point in request.Points)
                    {
                        ring.Add(session.Ucs.ToWorld(point));
                    }

                    PickIndex index = document.GetPickIndex(document.WorkingSpace);
                    found.AddRange(
                        request.Mode == "F" ? index.Fence(ring)
                        : request.Mode == "WP" ? session.TakenByWindow(index.WindowPolygon(ring), true)
                        : session.TakenByWindow(index.CrossingPolygon(ring), false)
                    );
                    break;
                case "L":
                    string last = LastVisible(document);

                    if (last != null)
                    {
                        found.Add(last);
                    }

                    break;
                case "P":
                    foreach (string handle in session.PreviousSelection)
                    {
                        if (
                            document.FindEntity(handle) != null
                            && ReferenceEquals(document.FindSpace(handle), document.WorkingSpace)
                        )
                        {
                            found.Add(handle);
                        }
                    }

                    break;
                case "I":
                    var implied = new List<ulong>();

                    foreach (string handle in document.Selection.Handles)
                    {
                        if (
                            ulong.TryParse(
                                handle,
                                NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture,
                                out ulong value
                            )
                        )
                        {
                            implied.Add(value);
                        }
                    }

                    implied.Sort();

                    foreach (ulong value in implied)
                    {
                        found.Add(value.ToString("X", CultureInfo.InvariantCulture));
                    }

                    break;
            }

            if (request.Filter == null)
            {
                return found;
            }

            var kept = new List<string>();

            foreach (string handle in found)
            {
                if (Accepted(session, request.Filter, handle))
                {
                    kept.Add(handle);
                }
            }

            return kept;
        }

        /// <summary>The handles of every entity of every space, the newest first.</summary>
        private static List<string> Everything(CadDocument document)
        {
            var all = new List<KeyValuePair<ulong, string>>();

            foreach (CadSpace space in document.Spaces)
            {
                foreach (DxfItem item in Items(space.Block))
                {
                    ulong.TryParse(item.Handle, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong value);
                    all.Add(new KeyValuePair<ulong, string>(value, item.Handle));
                }
            }

            all.Sort(
                (x, y) =>
                {
                    int order = y.Key.CompareTo(x.Key);
                    return order != 0 ? order : string.CompareOrdinal(x.Value, y.Value);
                }
            );

            var handles = new List<string>(all.Count);

            foreach (KeyValuePair<ulong, string> pair in all)
            {
                handles.Add(pair.Value);
            }

            return handles;
        }

        /// <summary>The object made last of those the current space shows, by the highest handle, as AutoCAD's Last takes it; null for none.</summary>
        private static string LastVisible(CadDocument document)
        {
            string last = null;
            ulong highest = 0;

            foreach (DisplayItem2 item in document.GetPickIndex(document.WorkingSpace).List.Items)
            {
                string handle = item.Handle;

                if (
                    string.IsNullOrEmpty(handle)
                    || !ulong.TryParse(handle, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong value)
                )
                {
                    continue;
                }

                if ((last == null || value > highest) && document.FindEntity(handle) != null)
                {
                    last = handle;
                    highest = value;
                }
            }

            return last;
        }

        #endregion
    }
}
