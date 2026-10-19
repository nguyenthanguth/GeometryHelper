using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Viewer.Views
{
    /// <summary>
    /// What every view of the window shares: the scene and what it is drawn with, the clip planes that have cut it, which items are hidden, which are
    /// selected, and which one the cursor is over. A view draws from it and changes nothing in it; the window changes it, and every view hears.
    /// </summary>
    public sealed class SceneModel
    {
        private readonly HashSet<int> _selected = new HashSet<int>();
        private readonly HashSet<int> _hidden = new HashSet<int>();
        private readonly HashSet<string> _hiddenLayers = new HashSet<string>(StringComparer.Ordinal);
        private int _hover = -1;

        /// <summary>
        /// Gets the scene shown, or null.
        /// </summary>
        public Scene Scene { get; private set; }

        /// <summary>
        /// Gets what the scene is drawn with, before any clip plane has cut it, or null. Picking, snapping and the box select work on this, and
        /// cut it themselves, so that they see what the planes leave.
        /// </summary>
        public Primitives Primitives { get; private set; }

        /// <summary>
        /// Gets the scene as the clip planes leave it, with the section lines of the planes; never null once a scene is shown.
        /// </summary>
        public ClipResult Clip { get; private set; }

        /// <summary>
        /// Gets how many times a scene has been shown, so that work started for one scene can tell it is out of date.
        /// </summary>
        public int Generation { get; private set; }

        /// <summary>
        /// Gets the indexes of the selected items.
        /// </summary>
        public IReadOnlyCollection<int> Selected => _selected;

        /// <summary>
        /// Gets the item the cursor is over, or -1.
        /// </summary>
        public int Hover => _hover;

        /// <summary>
        /// Raised when a scene is shown in place of the one before: the views make their elements again.
        /// </summary>
        public event EventHandler SceneChanged;

        /// <summary>
        /// Raised when the selection or what is hidden has changed.
        /// </summary>
        public event EventHandler StateChanged;

        /// <summary>
        /// Raised when the item under the cursor has changed, with the one it was and the one it is.
        /// </summary>
        public event Action<int, int> HoverChanged;

        /// <summary>
        /// Raised when the clip planes have cut the scene again: the views make their geometry again.
        /// </summary>
        public event EventHandler ClipChanged;

        /// <summary>
        /// Shows a scene, replacing the one before and everything chosen in it.
        /// </summary>
        /// <param name="scene">The scene.</param>
        /// <param name="primitives">What it is drawn with.</param>
        /// <param name="clip">The scene as the clip planes in force leave it.</param>
        public void Show(Scene scene, Primitives primitives, ClipResult clip)
        {
            Scene = scene;
            Primitives = primitives;
            Clip = clip;
            Generation++;
            _hidden.Clear();
            _hiddenLayers.Clear();
            _selected.Clear();
            _hover = -1;

            foreach (Layer layer in scene.Layers.Where(l => !l.Visible))
            {
                _hiddenLayers.Add(layer.Name);
            }

            SceneChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Takes the scene away.
        /// </summary>
        public void Clear()
        {
            Scene = null;
            Primitives = null;
            Clip = null;
            Generation++;
            _hidden.Clear();
            _hiddenLayers.Clear();
            _selected.Clear();
            _hover = -1;
            SceneChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Puts the scene as the clip planes leave it in place of the one before.
        /// </summary>
        /// <param name="clip">The cut scene.</param>
        public void SetClip(ClipResult clip)
        {
            Clip = clip;
            ClipChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Gets whether an item is hidden by itself, not counting its layer.
        /// </summary>
        public bool IsItemHidden(int index) => _hidden.Contains(index);

        /// <summary>
        /// Gets whether a layer is hidden.
        /// </summary>
        public bool IsLayerHidden(string layer) => _hiddenLayers.Contains(layer);

        /// <summary>
        /// Gets whether an item is shown: not hidden itself, and not on a hidden layer.
        /// </summary>
        public bool IsVisible(int index)
            => Scene != null && !_hidden.Contains(index) && !_hiddenLayers.Contains(Scene.Items[index].Layer);

        /// <summary>
        /// Shows or hides an item, apart from its layer.
        /// </summary>
        public void SetItemHidden(int index, bool hidden)
        {
            if (hidden ? _hidden.Add(index) : _hidden.Remove(index))
            {
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Shows or hides several items at once, telling the views once.
        /// </summary>
        public void SetItemsHidden(IEnumerable<int> items, bool hidden)
        {
            bool changed = false;

            foreach (int index in items)
            {
                changed |= hidden ? _hidden.Add(index) : _hidden.Remove(index);
            }

            if (changed)
            {
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Shows or hides a layer.
        /// </summary>
        public void SetLayerHidden(string layer, bool hidden)
        {
            if (hidden ? _hiddenLayers.Add(layer) : _hiddenLayers.Remove(layer))
            {
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Shows every item and layer again.
        /// </summary>
        public void ShowAll()
        {
            _hidden.Clear();
            _hiddenLayers.Clear();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Replaces the selection.
        /// </summary>
        public void Select(IEnumerable<int> items)
        {
            _selected.Clear();

            foreach (int index in items)
            {
                if (Scene != null && index >= 0 && index < Scene.Items.Count)
                {
                    _selected.Add(index);
                }
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Sets the item the cursor is over; -1 for none.
        /// </summary>
        public void SetHover(int index)
        {
            if (Scene == null || index == _hover)
            {
                return;
            }

            int before = _hover;
            _hover = index >= 0 && index < Scene.Items.Count ? index : -1;
            HoverChanged?.Invoke(before, _hover);
        }

        /// <summary>
        /// Gets the box round some items.
        /// </summary>
        public Bounds3 BoundsOf(IEnumerable<int> items)
        {
            Bounds3 bounds = default(Bounds3);

            foreach (int index in items)
            {
                bounds = bounds.Include(Primitives.Items[index].Bounds);
            }

            return bounds;
        }

        /// <summary>
        /// Gets the box round the visible items.
        /// </summary>
        public Bounds3 VisibleBounds()
            => Primitives == null ? default(Bounds3) : BoundsOf(Enumerable.Range(0, Primitives.Items.Count).Where(IsVisible));
    }
}
