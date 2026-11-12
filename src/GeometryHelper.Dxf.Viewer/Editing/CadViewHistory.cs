using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The views ZOOM Previous goes back to, as AutoCAD keeps them: a list for model space, one for the paper of each layout,
    /// and one for each viewport of a layout, the newest last, 20 at most each. AutoCAD 2021's console, zooming one viewport
    /// 30 times, gives back 20 of them, and a viewport never zoomed answers "No previous view saved." while another has
    /// twelve; leaving for the paper and coming back keeps them.
    /// </summary>
    internal sealed class CadViewHistory
    {
        /// <summary>The views each list keeps.</summary>
        internal const int Kept = 20;

        private readonly Dictionary<CadSpace, List<DisplayBox>> _spaces = new Dictionary<CadSpace, List<DisplayBox>>();
        private readonly Dictionary<string, List<KeyValuePair<GeoPoint2, double>>> _viewports = new Dictionary<
            string,
            List<KeyValuePair<GeoPoint2, double>>
        >(System.StringComparer.OrdinalIgnoreCase);

        /// <summary>Keeps the box a space's view showed, before a zoom or a pan of it.</summary>
        internal void Add(CadSpace space, DisplayBox box) => Push(List(_spaces, space), box);

        /// <summary>Takes the box a space's view showed last; false where its list is empty.</summary>
        internal bool TryTake(CadSpace space, out DisplayBox box) =>
            Pop(_spaces.TryGetValue(space, out List<DisplayBox> list) ? list : null, out box);

        /// <summary>Keeps the view a viewport showed (its view centre and height), before a zoom or a pan of it.</summary>
        internal void Add(string viewport, GeoPoint2 center, double height) =>
            Push(List(_viewports, viewport), new KeyValuePair<GeoPoint2, double>(center, height));

        /// <summary>Takes the view a viewport showed last; false where its list is empty.</summary>
        internal bool TryTake(string viewport, out GeoPoint2 center, out double height)
        {
            bool taken = Pop(
                _viewports.TryGetValue(viewport, out List<KeyValuePair<GeoPoint2, double>> list) ? list : null,
                out KeyValuePair<GeoPoint2, double> view
            );
            center = view.Key;
            height = view.Value;
            return taken;
        }

        /// <summary>The boxes a space's list holds now, the oldest first.</summary>
        internal DisplayBox[] Boxes(CadSpace space) =>
            _spaces.TryGetValue(space, out List<DisplayBox> list) ? list.ToArray() : System.Array.Empty<DisplayBox>();

        /// <summary>Puts a space's list as it was, the boxes given.</summary>
        internal void Restore(CadSpace space, DisplayBox[] boxes) => Replace(List(_spaces, space), boxes);

        /// <summary>The views a viewport's list holds now, the oldest first.</summary>
        internal KeyValuePair<GeoPoint2, double>[] Views(string viewport) =>
            _viewports.TryGetValue(viewport, out List<KeyValuePair<GeoPoint2, double>> list)
                ? list.ToArray()
                : System.Array.Empty<KeyValuePair<GeoPoint2, double>>();

        /// <summary>Puts a viewport's list as it was, the views given.</summary>
        internal void Restore(string viewport, KeyValuePair<GeoPoint2, double>[] views) =>
            Replace(List(_viewports, viewport), views);

        /// <summary>A list with an item pushed on it, as <see cref="Add(CadSpace, DisplayBox)"/> pushes: 20 at most, the oldest dropped.</summary>
        internal static T[] Pushed<T>(T[] list, T item)
        {
            var pushed = new List<T>(list) { item };

            if (pushed.Count > Kept)
            {
                pushed.RemoveAt(0);
            }

            return pushed.ToArray();
        }

        private static void Replace<T>(List<T> list, T[] items)
        {
            list.Clear();
            list.AddRange(items);
        }

        private static List<T> List<TKey, T>(Dictionary<TKey, List<T>> lists, TKey key)
        {
            if (!lists.TryGetValue(key, out List<T> list))
            {
                list = new List<T>();
                lists[key] = list;
            }

            return list;
        }

        private static void Push<T>(List<T> list, T item)
        {
            list.Add(item);

            if (list.Count > Kept)
            {
                list.RemoveAt(0);
            }
        }

        private static bool Pop<T>(List<T> list, out T item)
        {
            if (list == null || list.Count == 0)
            {
                item = default;
                return false;
            }

            item = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
            return true;
        }
    }

    /// <summary>
    /// What a step of a view did to a ZOOM Previous list, kept on its record so that U and REDO take the list with them, as
    /// AutoCAD's do: AutoCAD 2021's console, on the paper of a layout, after ZOOM 2X three times, ZOOM Previous and U, gave the view of the last zoom
    /// back and a ZOOM Previous after it the one the first Previous had gone to; after ZOOM 2X three times and U, ZOOM Previous
    /// went to the view before the last, not to the one the U had just left. The list is that of a space or, where the record
    /// has a handle, of that viewport; the step joined with the ones after it (the wheel's) keeps the first list before and
    /// the last after.
    /// </summary>
    internal sealed class CadViewListChange
    {
        internal CadViewListChange(DisplayBox[] boxesBefore, DisplayBox[] boxesAfter)
        {
            BoxesBefore = boxesBefore;
            BoxesAfter = boxesAfter;
        }

        internal CadViewListChange(
            KeyValuePair<GeoPoint2, double>[] viewsBefore,
            KeyValuePair<GeoPoint2, double>[] viewsAfter
        )
        {
            ViewsBefore = viewsBefore;
            ViewsAfter = viewsAfter;
        }

        /// <summary>The boxes a space's list held before the step; null for a viewport's.</summary>
        internal DisplayBox[] BoxesBefore { get; }

        /// <summary>The boxes a space's list held after the step; null for a viewport's.</summary>
        internal DisplayBox[] BoxesAfter { get; }

        /// <summary>The views a viewport's list held before the step; null for a space's.</summary>
        internal KeyValuePair<GeoPoint2, double>[] ViewsBefore { get; }

        /// <summary>The views a viewport's list held after the step; null for a space's.</summary>
        internal KeyValuePair<GeoPoint2, double>[] ViewsAfter { get; }

        /// <summary>The change of a step joined by another: the list before the first, the list after the last.</summary>
        internal static CadViewListChange Join(CadViewListChange first, CadViewListChange last)
        {
            if (first == null || last == null)
            {
                return first ?? last;
            }

            return first.BoxesBefore != null
                ? new CadViewListChange(first.BoxesBefore, last.BoxesAfter)
                : new CadViewListChange(first.ViewsBefore, last.ViewsAfter);
        }
    }
}
