using System;
using System.Collections.Generic;
using System.Globalization;
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
                LispAsk ask = lisp.NewAsk(LispAskKind.Selection, null);
                ask.Single = request.Single;

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

                if (mode.StartsWith(":", StringComparison.Ordinal))
                {
                    request.Interactive = true;
                    request.Single = mode.IndexOf('S') > 0;
                    filter = a.Length > 1 ? a[1] : null;
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
                CadSpace space = document.FindSpace(item.Handle);
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

            pairs.Insert(0, LispEntityData.Pair(0, item.Type));
            return pairs;
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
                    found.AddRange(request.Mode == "W" ? window.Window(box) : window.Crossing(box));
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
                        : request.Mode == "WP" ? index.WindowPolygon(ring)
                        : index.CrossingPolygon(ring)
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
