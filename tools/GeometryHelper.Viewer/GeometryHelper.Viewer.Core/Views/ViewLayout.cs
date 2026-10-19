using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// The place a view takes in the window, in pixels from the top left corner.
    /// </summary>
    public readonly struct ViewCell
    {
        /// <summary>
        /// Initializes a cell.
        /// </summary>
        public ViewCell(int slot, double x, double y, double width, double height)
        {
            Slot = slot;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Gets the number of the view, from 0 to 3.
        /// </summary>
        public int Slot { get; }

        /// <summary>
        /// Gets the left edge.
        /// </summary>
        public double X { get; }

        /// <summary>
        /// Gets the top edge.
        /// </summary>
        public double Y { get; }

        /// <summary>
        /// Gets the width.
        /// </summary>
        public double Width { get; }

        /// <summary>
        /// Gets the height.
        /// </summary>
        public double Height { get; }

        /// <summary>
        /// Gets whether a place is inside the cell.
        /// </summary>
        public bool Contains(double x, double y) => x >= X && x < X + Width && y >= Y && y < Y + Height;
    }

    /// <summary>
    /// One of the four views of the window: its own camera and its own render mode, and whether it follows the active view.
    /// </summary>
    public sealed class ViewSlot
    {
        internal ViewSlot(StandardView view)
        {
            Camera.SetView(view);
        }

        /// <summary>
        /// Gets the camera of the view, which the view moves in place.
        /// </summary>
        public ViewCamera Camera { get; } = new ViewCamera();

        /// <summary>
        /// Gets or sets how the view draws the scene.
        /// </summary>
        public RenderMode Mode { get; set; } = RenderMode.ShadedEdges;

        /// <summary>
        /// Gets or sets whether the view follows the active view in zoom and pan: it looks at the same point at the same scale, from its own direction.
        /// </summary>
        public bool Linked { get; set; }

        /// <summary>
        /// Gets or sets whether the view has been fitted to the scene that is shown; a view that has not is fitted when it is shown. A new scene
        /// takes it from every view, those not shown too.
        /// </summary>
        public bool Fitted { get; set; }
    }

    /// <summary>
    /// What a layout is remembered by: the number of views shown, the active one, and the render mode and link of each of the four.
    /// </summary>
    public sealed class ViewLayoutState
    {
        /// <summary>
        /// Gets or sets how many views are shown: 1, 2 or 4.
        /// </summary>
        public int Count { get; set; } = 1;

        /// <summary>
        /// Gets or sets the number of the active view, from 0 to 3.
        /// </summary>
        public int Active { get; set; } = 3;

        /// <summary>
        /// Gets or sets the render mode of each of the four views, by name.
        /// </summary>
        public string[] Modes { get; set; }

        /// <summary>
        /// Gets or sets whether each of the four views is linked.
        /// </summary>
        public bool[] Linked { get; set; }
    }

    /// <summary>
    /// The window's views: up to four, each with a camera and a render mode of its own, sharing one scene. One view or two or four are shown;
    /// the others keep their cameras for when they are shown again. It holds no screen type, so what goes where is decided and tested here.
    /// </summary>
    /// <remarks>
    /// The four views start as the plan, the front, the right and the iso, in that order, in a grid of two by two: plan and front above, right and iso
    /// below. One view is the active one, which is the iso at the start, so a window with one view is what it always was. A layout of one view shows the
    /// active one; of two shows the plan and the active one (the plan and the front where the plan is active); of four shows all of them.
    /// </remarks>
    public sealed class ViewLayout
    {
        /// <summary>
        /// The number of views: four.
        /// </summary>
        public const int MaxViews = 4;

        private readonly ViewSlot[] _slots =
        {
            new ViewSlot(StandardView.Top),
            new ViewSlot(StandardView.Front),
            new ViewSlot(StandardView.Right),
            new ViewSlot(StandardView.Iso),
        };

        private int[] _shown = { 3 };
        private int _active = 3;

        /// <summary>
        /// Gets the four views, whether they are shown or not.
        /// </summary>
        public IReadOnlyList<ViewSlot> Slots => _slots;

        /// <summary>
        /// Gets how many views are shown: 1, 2 or 4.
        /// </summary>
        public int Count => _shown.Length;

        /// <summary>
        /// Gets the numbers of the views that are shown, in the order of their cells: left to right, top to bottom.
        /// </summary>
        public IReadOnlyList<int> Shown => _shown;

        /// <summary>
        /// Gets the number of the active view, which is one of those shown.
        /// </summary>
        public int Active => _active;

        /// <summary>
        /// Gets the active view.
        /// </summary>
        public ViewSlot ActiveSlot => _slots[_active];

        /// <summary>
        /// Shows one, two or four views. The active view stays active.
        /// </summary>
        /// <param name="count">1, 2 or 4.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for any other number.</exception>
        public void SetCount(int count)
        {
            switch (count)
            {
                case 1:
                    _shown = new[] { _active };
                    break;

                case 2:
                    // The plan is the one to look at beside it, unless it is the one looked at; then it is the front.
                    _shown = new[] { 0, _active == 0 ? 1 : _active };
                    break;

                case 4:
                    _shown = new[] { 0, 1, 2, 3 };
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(count), count, "A layout shows 1, 2 or 4 views.");
            }
        }

        /// <summary>
        /// Makes a view the active one.
        /// </summary>
        /// <param name="slot">The number of the view; it has to be one that is shown.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the view is not shown.</exception>
        public void Activate(int slot)
        {
            if (Array.IndexOf(_shown, slot) < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "The view is not shown.");
            }

            _active = slot;
        }

        /// <summary>
        /// Gets the cells of the views that are shown.
        /// </summary>
        /// <param name="width">The width of the area the views share, in pixels.</param>
        /// <param name="height">The height of the area.</param>
        /// <param name="gap">The space between two views, in pixels.</param>
        /// <returns>One cell for each view shown, in the order of <see cref="Shown"/>: one view takes the whole area, two stand side by side, four make two rows of two.</returns>
        public IReadOnlyList<ViewCell> Cells(double width, double height, double gap = 2.0)
        {
            double w = Math.Max(0.0, width);
            double h = Math.Max(0.0, height);
            var cells = new List<ViewCell>(_shown.Length);

            if (_shown.Length == 1)
            {
                cells.Add(new ViewCell(_shown[0], 0.0, 0.0, w, h));
            }
            else if (_shown.Length == 2)
            {
                double half = Math.Max(0.0, (w - gap) * 0.5);
                cells.Add(new ViewCell(_shown[0], 0.0, 0.0, half, h));
                cells.Add(new ViewCell(_shown[1], half + gap, 0.0, half, h));
            }
            else
            {
                double halfW = Math.Max(0.0, (w - gap) * 0.5);
                double halfH = Math.Max(0.0, (h - gap) * 0.5);

                for (int i = 0; i < 4; i++)
                {
                    cells.Add(new ViewCell(_shown[i], (i % 2) * (halfW + gap), (i / 2) * (halfH + gap), halfW, halfH));
                }
            }

            return cells;
        }

        /// <summary>
        /// Gets the view at a place of the area they share.
        /// </summary>
        /// <param name="x">Across, in pixels.</param>
        /// <param name="y">Down, in pixels.</param>
        /// <param name="width">The width of the area.</param>
        /// <param name="height">The height of the area.</param>
        /// <param name="gap">The space between two views.</param>
        /// <returns>The number of the view, or -1 where the place is in the gap between two or outside the area.</returns>
        public int SlotAt(double x, double y, double width, double height, double gap = 2.0)
        {
            foreach (ViewCell cell in Cells(width, height, gap))
            {
                if (cell.Contains(x, y))
                {
                    return cell.Slot;
                }
            }

            return -1;
        }

        /// <summary>
        /// Makes the views that are linked look at what a linked view looks at, from their own direction: they take its target and its distance, so
        /// that one pixel is the same length in all of them. Linked views move together, whichever of them is moved; a view that is not linked moves alone
        /// and takes nothing with it.
        /// </summary>
        /// <param name="source">The number of the view that moved, which is not itself moved.</param>
        /// <returns>The numbers of the views that were moved, in order; none where the view that moved is not linked.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no view of that number.</exception>
        public IReadOnlyList<int> SyncLinked(int source)
        {
            if (source < 0 || source >= MaxViews)
            {
                throw new ArgumentOutOfRangeException(nameof(source));
            }

            var moved = new List<int>();

            if (!_slots[source].Linked)
            {
                return moved;
            }

            ViewCamera from = _slots[source].Camera;

            foreach (int slot in _shown)
            {
                if (slot == source || !_slots[slot].Linked)
                {
                    continue;
                }

                ViewCamera camera = _slots[slot].Camera;

                if (!camera.Target.Equals(from.Target) || camera.Distance != from.Distance)
                {
                    camera.Target = from.Target;
                    camera.Distance = from.Distance;
                    moved.Add(slot);
                }
            }

            return moved;
        }

        /// <summary>
        /// Says that every view, the ones not shown too, has still to be fitted to the scene: a new scene has been opened.
        /// </summary>
        public void ClearFitted()
        {
            foreach (ViewSlot slot in _slots)
            {
                slot.Fitted = false;
            }
        }

        /// <summary>
        /// Gets the numbers of the views that are shown and have still to be fitted to the scene, in the order of their cells.
        /// </summary>
        public IReadOnlyList<int> UnfittedShown() => _shown.Where(s => !_slots[s].Fitted).ToList();

        /// <summary>
        /// Takes what a layout is remembered by.
        /// </summary>
        public ViewLayoutState Capture() => new ViewLayoutState
        {
            Count = Count,
            Active = _active,
            Modes = _slots.Select(s => s.Mode.ToString()).ToArray(),
            Linked = _slots.Select(s => s.Linked).ToArray(),
        };

        /// <summary>
        /// Puts back what <see cref="Capture"/> took. What does not make sense is left as it is: a count that is not 1, 2 or 4, an active view that is not
        /// one of the four, a render mode that is not one.
        /// </summary>
        /// <param name="state">The state; null changes nothing.</param>
        public void Restore(ViewLayoutState state)
        {
            if (state == null)
            {
                return;
            }

            for (int i = 0; i < MaxViews; i++)
            {
                if (state.Modes != null && i < state.Modes.Length && Enum.TryParse(state.Modes[i], out RenderMode mode) && Enum.IsDefined(typeof(RenderMode), mode))
                {
                    _slots[i].Mode = mode;
                }

                if (state.Linked != null && i < state.Linked.Length)
                {
                    _slots[i].Linked = state.Linked[i];
                }
            }

            if (state.Active >= 0 && state.Active < MaxViews)
            {
                _active = state.Active;
            }

            if (state.Count == 1 || state.Count == 2 || state.Count == 4)
            {
                SetCount(state.Count);
            }
            else
            {
                SetCount(Count);
            }
        }
    }
}
