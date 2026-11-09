using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// What a click and a box do, as AutoCAD does with PICKADD on. With no command running: a click on an entity adds it,
    /// Shift+click adds or removes it, a click on nothing clears the selection; a box adds what it takes, and with Shift held
    /// removes it. With a command running: a click answers a point prompt with the snapped point, and a click, a box or a lasso
    /// answers "Select objects:", with Shift held taking out what it picks, as AutoCAD's. The canvas and the view model both go
    /// through here, so a test of the one holds for the other.
    /// </summary>
    internal static class CadPicking
    {
        /// <summary>
        /// Gets the objects SELECTIONCYCLING 2 lists for a click: those within the aperture, the nearest first and of those as
        /// near the one drawn on top first (<see cref="PickIndex.Pick"/>), where more than one lies there and the click would
        /// pick (no command, or "Select objects:" and the like); else none.
        /// </summary>
        public static IReadOnlyList<string> Overlapping(CadDocument document, CadSpace space, CadCommandSession session, GeoPoint2 at, double aperture)
        {
            if (document.SelectionCycling != 2 || (session != null && session.IsRunning && !WantsObjects(session)))
            {
                return Array.Empty<string>();
            }

            IReadOnlyList<string> found = document.GetPickIndex(space).Pick(at.X, at.Y, aperture);
            return found.Count > 1 ? found : (IReadOnlyList<string>)Array.Empty<string>();
        }

        /// <summary>The words of a row of the Selection list: the type of the object and its layer, "Line · WALLS".</summary>
        public static string CycleLabel(CadDocument document, string handle)
        {
            DxfEntity entity = document.FindEntity(handle);
            return entity == null ? handle : CadProperties.Describe(document, null, new[] { handle }).Label.Replace(" (1)", string.Empty) + " · " + (entity.Layer ?? "0");
        }

        /// <summary>An object chosen from the Selection list, taken as a click on it alone takes it: fed to "Select objects:", or selected with its group.</summary>
        /// <returns>Whether the selection or the command changed.</returns>
        public static bool Choose(CadDocument document, CadCommandSession session, string handle, bool shift)
        {
            if (session != null && session.IsRunning)
            {
                return WantsObjects(session) && session.FeedSelection(new[] { handle }, shift);
            }

            IReadOnlyList<string> group = document.WithGroups(new[] { handle });

            if (shift && document.Selection.Contains(handle))
            {
                document.Selection.Remove(group);
            }
            else
            {
                document.Selection.Add(group);
            }

            return true;
        }

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

        /// <summary>Gets the point a click at the cursor takes, snapped from the prompt's base point, the grid, ortho and polar along the space's UCS; null where no snap is given.</summary>
        public static CadSnapResult Snap(CadDocument document, CadSpace space, ObjectSnap snap, GeoPoint2 cursor, double aperture, GeoPoint2? basePoint)
        {
            if (snap == null)
            {
                return null;
            }

            snap.Ucs = document.Ucs.WorkingOf(space);
            return snap.Snap(document.GetPickIndex(space), cursor, aperture, basePoint);
        }

        /// <summary>The cursor has rested at a point prompt: a point is acquired or let go for Extension, Parallel and tracking (<see cref="ObjectSnap.Acquire"/>).</summary>
        /// <returns>Whether one was.</returns>
        public static bool Acquire(CadDocument document, CadSpace space, ObjectSnap snap, GeoPoint2 cursor, double aperture, GeoPoint2? basePoint)
        {
            if (snap == null)
            {
                return false;
            }

            snap.Ucs = document.Ucs.WorkingOf(space);
            return snap.Acquire(document.GetPickIndex(space), cursor, aperture, basePoint);
        }

        /// <summary>A left click at a point of the drawing.</summary>
        /// <returns>Whether the selection or the command changed.</returns>
        public static bool Click(CadDocument document, CadSpace space, CadCommandSession session, ObjectSnap snap, GeoPoint2 cursor, double aperture, bool shift, bool control = false)
        {
            if (session != null && session.IsRunning)
            {
                session.PickAperture = aperture;

                if (WantsPoint(session))
                {
                    CadSnapResult snapped = Snap(document, space, snap, cursor, aperture, session.Prompt.BasePoint);

                    // A click on the object an Extended Apparent Intersection offers picks it as the first of two, and takes no point.
                    if (snap != null && snap.TakeFirstObject(snapped))
                    {
                        return false;
                    }

                    // Ctrl held as the point is picked turns an arc of ARC or PLINE the other way, as in AutoCAD, and a grip
                    // edit's first point copies. The snap goes with its point, so that a dimension ties the point to its object.
                    return snapped != null && snapped.Handle != null ? session.Feed(snapped, control) : session.Feed(snapped?.Point ?? cursor, false, control);
                }

                return WantsObjects(session) && session.Feed(cursor, shift);
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
                session.FeedSelection(found, shift);
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

            PickIndex index = document.GetPickIndex(space);
            IReadOnlyList<string> found = SignedArea(ring) > 0.0 ? index.CrossingPolygon(ring) : index.WindowPolygon(ring);

            if (!running)
            {
                found = document.WithGroups(found);
            }

            if (running)
            {
                session.FeedSelection(found, shift);
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
                IReadOnlyList<string> group = document.WithGroups(new[] { found[0] });

                if (document.Selection.Contains(found[0]))
                {
                    document.Selection.Remove(group);
                }
                else
                {
                    document.Selection.Add(group);
                }
            }
            else
            {
                document.Selection.Add(document.WithGroups(new[] { found[0] }));
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
            IReadOnlyList<string> found = document.WithGroups(crossing ? index.Crossing(box) : index.Window(box));

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
