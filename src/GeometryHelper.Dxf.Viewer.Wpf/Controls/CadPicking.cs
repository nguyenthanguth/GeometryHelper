using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// What a click and a box do, as AutoCAD does with PICKADD on. With no command running: a click on an entity adds it,
    /// Shift+click adds or removes it, a click on nothing clears the selection; a box adds what it takes, and with Shift held
    /// removes it. With a command running: a click answers a point prompt with the snapped point, and a click or a box answers
    /// "Select objects:". The canvas and the view model both go through here, so a test of the one holds for the other.
    /// </summary>
    internal static class CadPicking
    {
        /// <summary>Gets whether the prompt waiting takes a point: a point, a distance or an angle.</summary>
        public static bool WantsPoint(CadCommandSession session)
        {
            CadPrompt prompt = session?.Prompt;
            return prompt != null && (prompt.Kind == CadPromptKind.Point || prompt.Kind == CadPromptKind.Distance || prompt.Kind == CadPromptKind.Angle);
        }

        /// <summary>Gets whether the prompt waiting takes objects: "Select objects:" or a prompt for one object.</summary>
        public static bool WantsObjects(CadCommandSession session)
        {
            CadPrompt prompt = session?.Prompt;
            return prompt != null && (prompt.Kind == CadPromptKind.Selection || prompt.Kind == CadPromptKind.Entity);
        }

        /// <summary>Gets the point a click at the cursor takes, snapped from the prompt's base point; null where no snap is given.</summary>
        public static CadSnapResult Snap(CadDocument document, CadSpace space, ObjectSnap snap, GeoPoint2 cursor, double aperture, GeoPoint2? basePoint)
        {
            return snap == null ? null : snap.Snap(document.GetPickIndex(space), cursor, aperture, basePoint);
        }

        /// <summary>A left click at a point of the drawing.</summary>
        /// <returns>Whether the selection or the command changed.</returns>
        public static bool Click(CadDocument document, CadSpace space, CadCommandSession session, ObjectSnap snap, GeoPoint2 cursor, double aperture, bool shift)
        {
            if (session != null && session.IsRunning)
            {
                session.PickAperture = aperture;

                if (WantsPoint(session))
                {
                    CadSnapResult snapped = Snap(document, space, snap, cursor, aperture, session.Prompt.BasePoint);
                    return session.Feed(snapped?.Point ?? cursor);
                }

                return WantsObjects(session) && session.Feed(cursor);
            }

            int before = document.Selection.Count;
            return Pick(document, space, cursor.X, cursor.Y, aperture, shift) != null || before != document.Selection.Count;
        }

        /// <summary>A window (left to right) or crossing (right to left) box dragged over the drawing.</summary>
        /// <returns>The handles the box took.</returns>
        public static IReadOnlyList<string> BoxDone(CadDocument document, CadSpace space, CadCommandSession session, DisplayBox box, bool crossing, bool shift)
        {
            if (session != null && session.IsRunning)
            {
                if (!WantsObjects(session))
                {
                    return new string[0];
                }

                PickIndex index = document.GetPickIndex(space);
                IReadOnlyList<string> found = crossing ? index.Crossing(box) : index.Window(box);
                session.FeedSelection(found);
                return found;
            }

            return Box(document, space, box, crossing, shift);
        }

        /// <summary>The area a ring of points encloses, positive where it runs counter-clockwise in drawing units (Y up).</summary>
        public static double SignedArea(IReadOnlyList<GeoPoint2> ring)
        {
            double twice = 0.0;

            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                twice += ring[j].X * ring[i].Y - ring[i].X * ring[j].Y;
            }

            return twice / 2.0;
        }

        /// <summary>
        /// Gets whether a lasso encloses enough to be one: a tenth of its bounding box at least. A drag along a line encloses
        /// nothing and leaves the box open for its second click, as before the lasso.
        /// </summary>
        public static bool IsLoop(IReadOnlyList<GeoPoint2> ring)
        {
            if (ring.Count < 3)
            {
                return false;
            }

            DisplayBox bounds = Bounds(ring);
            return Math.Abs(SignedArea(ring)) >= 0.1 * bounds.Width * bounds.Height && bounds.Width > 0.0 && bounds.Height > 0.0;
        }

        /// <summary>
        /// A lasso drawn over the drawing, closed back to its start: drawn clockwise it takes what lies wholly inside, as a
        /// window; counter-clockwise what it touches, as a crossing. It answers "Select objects:" as a box does.
        /// </summary>
        /// <returns>The handles the lasso took.</returns>
        public static IReadOnlyList<string> LassoDone(CadDocument document, CadSpace space, CadCommandSession session, IReadOnlyList<GeoPoint2> ring, bool shift)
        {
            bool running = session != null && session.IsRunning;

            if (running && !WantsObjects(session))
            {
                return new string[0];
            }

            IReadOnlyList<string> found = Lasso(document.GetPickIndex(space), ring, SignedArea(ring) > 0.0);

            if (running)
            {
                session.FeedSelection(found);
            }
            else if (shift)
            {
                document.Selection.Remove(found);
            }
            else
            {
                document.Selection.Add(found);
            }

            return found;
        }

        /// <summary>The entities a lasso takes: those whose every item lies inside it, or for a crossing those an item of which it touches; ordinal order.</summary>
        private static IReadOnlyList<string> Lasso(PickIndex index, IReadOnlyList<GeoPoint2> ring, bool crossing)
        {
            var handles = new SortedSet<string>(StringComparer.Ordinal);

            foreach (DisplayItem2 item in index.ItemsNear(Bounds(ring)))
            {
                handles.Add(item.Handle);
            }

            var found = new List<string>();

            foreach (string handle in handles)
            {
                bool any = false;
                bool all = true;

                foreach (int i in index.IndicesOf(handle))
                {
                    DisplayItem2 item = index.List.Items[i];
                    bool inside = Inside(item, ring, out bool touches);
                    any |= touches;
                    all &= inside;
                }

                if (crossing ? any : all)
                {
                    found.Add(handle);
                }
            }

            return found;
        }

        /// <summary>Whether an item lies wholly inside a ring: every vertex inside and no edge crossing the ring; and whether it touches the ring at all.</summary>
        private static bool Inside(DisplayItem2 item, IReadOnlyList<GeoPoint2> ring, out bool touches)
        {
            bool inside = true;
            touches = item is DisplayFill2 fill && fill.Contains(ring[0].X, ring[0].Y);

            foreach (KeyValuePair<double[], bool> chain in Chains(item))
            {
                double[] xy = chain.Key;
                int n = xy.Length / 2;

                for (int k = 0; k < n; k++)
                {
                    bool vertexIn = Contains(ring, xy[2 * k], xy[2 * k + 1]);
                    touches |= vertexIn;
                    inside &= vertexIn;
                    int next = k + 1 < n ? k + 1 : chain.Value && n > 2 ? 0 : -1;

                    if (next >= 0 && Crosses(ring, xy[2 * k], xy[2 * k + 1], xy[2 * next], xy[2 * next + 1]))
                    {
                        touches = true;
                        inside = false;
                    }
                }
            }

            return inside;
        }

        /// <summary>The outline of an item as chains of points, each with whether it closes: an arc flattened, a fill's rings, the box of a text or a viewport.</summary>
        private static IEnumerable<KeyValuePair<double[], bool>> Chains(DisplayItem2 item)
        {
            switch (item)
            {
                case DisplayPolyline2 chain:
                    yield return new KeyValuePair<double[], bool>(chain.Xy.ToArray(), chain.Closed);
                    break;
                case DisplaySegments2 segments:
                    for (int i = 0; i + 3 < segments.Xy.Count; i += 4)
                    {
                        yield return new KeyValuePair<double[], bool>(new[] { segments.Xy[i], segments.Xy[i + 1], segments.Xy[i + 2], segments.Xy[i + 3] }, false);
                    }

                    break;
                case DisplayArc2 arc:
                    yield return new KeyValuePair<double[], bool>(arc.Flatten(Math.Max(arc.Radius * 1E-3, 1E-9)), false);
                    break;
                case DisplayFill2 fill:
                    foreach (double[] ring in fill.Rings)
                    {
                        yield return new KeyValuePair<double[], bool>(ring, true);
                    }

                    break;
                default:
                    DisplayBox box = item.Bounds;

                    if (!box.IsEmpty)
                    {
                        yield return new KeyValuePair<double[], bool>(new[] { box.MinX, box.MinY, box.MaxX, box.MinY, box.MaxX, box.MaxY, box.MinX, box.MaxY }, true);
                    }

                    break;
            }
        }

        /// <summary>Whether a point lies inside a ring, by the even-odd rule.</summary>
        private static bool Contains(IReadOnlyList<GeoPoint2> ring, double x, double y)
        {
            bool inside = false;

            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                GeoPoint2 a = ring[i];
                GeoPoint2 b = ring[j];

                if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>Whether a segment crosses an edge of a ring, each through the other's line.</summary>
        private static bool Crosses(IReadOnlyList<GeoPoint2> ring, double ax, double ay, double bx, double by)
        {
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                GeoPoint2 c = ring[j];
                GeoPoint2 d = ring[i];

                if (Math.Max(ax, bx) < Math.Min(c.X, d.X) || Math.Min(ax, bx) > Math.Max(c.X, d.X) || Math.Max(ay, by) < Math.Min(c.Y, d.Y) || Math.Min(ay, by) > Math.Max(c.Y, d.Y))
                {
                    continue;
                }

                double d1 = Side(c.X, c.Y, d.X, d.Y, ax, ay);
                double d2 = Side(c.X, c.Y, d.X, d.Y, bx, by);
                double d3 = Side(ax, ay, bx, by, c.X, c.Y);
                double d4 = Side(ax, ay, bx, by, d.X, d.Y);

                if (((d1 > 0.0 && d2 < 0.0) || (d1 < 0.0 && d2 > 0.0)) && ((d3 > 0.0 && d4 < 0.0) || (d3 < 0.0 && d4 > 0.0)))
                {
                    return true;
                }
            }

            return false;
        }

        private static double Side(double ax, double ay, double bx, double by, double px, double py) => (bx - ax) * (py - ay) - (by - ay) * (px - ax);

        private static DisplayBox Bounds(IReadOnlyList<GeoPoint2> ring)
        {
            DisplayBox box = DisplayBox.Empty;

            foreach (GeoPoint2 point in ring)
            {
                box = box.Add(point.X, point.Y);
            }

            return box;
        }

        /// <summary>Picks at a point of the drawing with no command running.</summary>
        /// <returns>The handle picked, or null where nothing lies within the tolerance.</returns>
        public static string Pick(CadDocument document, CadSpace space, double x, double y, double tolerance, bool toggle)
        {
            IReadOnlyList<string> found = document.GetPickIndex(space).Pick(x, y, tolerance);

            if (found.Count == 0)
            {
                if (!toggle)
                {
                    document.Selection.Clear();
                }

                return null;
            }

            if (toggle)
            {
                document.Selection.Toggle(found[0]);
            }
            else
            {
                document.Selection.Add(new[] { found[0] });
            }

            return found[0];
        }

        /// <summary>
        /// Gets what ZOOM Extents shows of a space: what it draws, and for a layout its sheet of paper too, so that a layout
        /// opens on its whole sheet as AutoCAD opens one.
        /// </summary>
        public static DisplayBox Extents(CadDocument document, CadSpace space) => document.GetDisplay(space).Bounds.Union(space.PaperBounds);

        /// <summary>Selects by a box of the drawing with no command running: a window takes the entities wholly inside, a crossing those it touches.</summary>
        /// <returns>The handles the box took.</returns>
        public static IReadOnlyList<string> Box(CadDocument document, CadSpace space, DisplayBox box, bool crossing, bool remove)
        {
            PickIndex index = document.GetPickIndex(space);
            IReadOnlyList<string> found = crossing ? index.Crossing(box) : index.Window(box);

            if (remove)
            {
                document.Selection.Remove(found);
            }
            else
            {
                document.Selection.Add(found);
            }

            return found;
        }
    }
}
