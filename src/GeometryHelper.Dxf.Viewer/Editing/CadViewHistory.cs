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
}
