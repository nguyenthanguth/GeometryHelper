using System.Collections.Generic;
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
