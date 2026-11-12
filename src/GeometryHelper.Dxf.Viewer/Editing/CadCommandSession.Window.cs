using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The implied window of the Command prompt, as AutoCAD 2021's.</summary>
    public sealed partial class CadCommandSession
    {
        private static readonly string[] WindowKeywords = { "Fence", "WPolygon", "CPolygon" };

        private CadCommand _windowCommand;
        private CadPrompt _windowFirst;

        /// <summary>
        /// Starts the implied window, as a click on nothing at the Command prompt starts AutoCAD's: the command line asks
        /// "Specify opposite corner or [Fence/WPolygon/CPolygon]:", a point given as a click or typed ends the box, left to
        /// right a window and right to left a crossing, and the objects it takes are selected. F, WP or CP turns the box into a
        /// fence or a polygon whose first point is the corner, asking "Specify next fence point or [Undo]:" or "Specify
        /// endpoint of line or [Undo]:" until Enter. A box from (0, 0) with the opposite corner typed 110,110 selects the lines
        /// wholly inside it. The history keeps the prompt after "Command:", as AutoCAD's does, and Esc ends it with
        /// "*Cancel*". The command run last stays the one Enter runs again.
        /// </summary>
        /// <param name="corner">The first corner, in the coordinates of the space.</param>
        /// <returns>False, starting nothing, where a command is running.</returns>
        public bool StartWindow(GeoPoint2 corner)
        {
            if (IsRunning)
            {
                return false;
            }

            _windowCommand = new CadCommand("SELECT", c => WindowBody(c, corner));
            Run(_windowCommand, Array.Empty<string>());
            return true;
        }

        /// <summary>Gets whether the implied window runs (<see cref="StartWindow"/>), at its opposite corner or as the fence or polygon it was turned into: the canvas snaps none of its points.</summary>
        public bool IsWindowing => Current != null && ReferenceEquals(Current, _windowCommand);

        /// <summary>Gets whether the implied window asks its opposite corner, "Specify opposite corner or [Fence/WPolygon/CPolygon]:".</summary>
        public bool IsWindowOpen => IsWindowing && ReferenceEquals(Prompt, _windowFirst);

        /// <summary>
        /// Ends the implied window at its opposite corner without selecting, the prompt kept in the history as a click answers
        /// it: the canvas that picked the corner selects with the box it shows, Shift and a twisted viewport with it.
        /// </summary>
        /// <returns>Whether the window was open.</returns>
        public bool EndWindow()
        {
            if (!IsWindowOpen)
            {
                return false;
            }

            Write(Prompt.Text);
            Finish(CadCommandOutcome.Completed);
            return true;
        }

        /// <summary>The body of the implied window: the opposite corner, or the fence or polygon a keyword turns it into.</summary>
        private IEnumerable<CadPrompt> WindowBody(CadCommandContext c, GeoPoint2 corner)
        {
            var points = new List<GeoPoint2> { corner };
            CadInput input;

            while (true)
            {
                _windowFirst = CadPrompt.ForPoint("Specify opposite corner", null, WindowKeywords);
                _windowFirst.Band = new CadSelectionBand(CadSelectionBandKind.Box, new List<GeoPoint2>(points));
                yield return _windowFirst;
                input = c.Input;

                if (input.IsPoint || input.Kind == CadInputKind.Keyword)
                {
                    break;
                }

                c.Write("Invalid window specification.");
            }

            _windowFirst = null;
            PickIndex index = Document.GetPickIndex(Document.WorkingSpace);

            if (input.IsPoint)
            {
                GeoPoint2 far = input.Point;
                var box = new DisplayBox(
                    Math.Min(corner.X, far.X),
                    Math.Min(corner.Y, far.Y),
                    Math.Max(corner.X, far.X),
                    Math.Max(corner.Y, far.Y)
                );
                SelectWhole(far.X < corner.X ? index.Crossing(box) : index.Window(box));
                yield break;
            }

            bool fence = input.Is("Fence");
            CadSelectionBandKind kind =
                fence ? CadSelectionBandKind.Fence
                : input.Is("WPolygon") ? CadSelectionBandKind.WindowPolygon
                : CadSelectionBandKind.CrossingPolygon;

            while (true)
            {
                CadPrompt next = CadPrompt.ForPoint(
                    fence ? "Specify next fence point" : "Specify endpoint of line",
                    points[points.Count - 1],
                    new[] { "Undo" }
                );
                next.Band = new CadSelectionBand(kind, new List<GeoPoint2>(points));
                yield return next;
                input = c.Input;

                if (input.IsPoint)
                {
                    points.Add(input.Point);
                }
                else if (input.Is("Undo"))
                {
                    if (points.Count > 1)
                    {
                        points.RemoveAt(points.Count - 1);
                    }
                }
                else
                {
                    break;
                }
            }

            if (fence && points.Count >= 2)
            {
                SelectWhole(index.Fence(points));
            }
            else if (!fence && points.Count >= 3)
            {
                SelectWhole(
                    kind == CadSelectionBandKind.WindowPolygon
                        ? index.WindowPolygon(points)
                        : index.CrossingPolygon(points)
                );
            }
        }

        /// <summary>Selects the objects the implied window took, with their groups: with PICKADD 0 in place of those selected, else added to them.</summary>
        private void SelectWhole(IEnumerable<string> found)
        {
            if (Document.PickAdd == 0)
            {
                Document.Selection.Clear();
            }

            Document.Selection.Add(Document.WithGroups(found));
        }
    }
}
