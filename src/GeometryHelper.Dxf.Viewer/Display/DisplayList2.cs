using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What one space of a drawing, model space or a layout, is drawn as: its items in drawing order, the box that holds
    /// them, and the layers they sit on. It is made once from the entities and kept until one changes.
    /// </summary>
    public sealed class DisplayList2
    {
        private readonly List<DisplayItem2> _items = new List<DisplayItem2>();
        private DisplayBox _bounds = DisplayBox.Empty;

        /// <summary>Gets the items in the order they are drawn, the first at the back.</summary>
        public IReadOnlyList<DisplayItem2> Items => _items;

        /// <summary>Gets the box that holds every item.</summary>
        public DisplayBox Bounds => _bounds;

        /// <summary>Gets the number of items.</summary>
        public int Count => _items.Count;

        /// <summary>Adds an item at the front.</summary>
        /// <param name="item">The item.</param>
        public void Add(DisplayItem2 item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            _items.Add(item);
            _bounds = _bounds.Union(item.Bounds);
        }
    }

    /// <summary>
    /// A viewport of a layout: a window onto model space at a scale, which the canvas draws by clipping to its frame and
    /// drawing the model-space items under the viewport's transformation.
    /// </summary>
    public sealed class DisplayViewport2 : DisplayItem2
    {
        private readonly DisplayBox _bounds;

        /// <summary>Makes a viewport.</summary>
        /// <param name="centerX">The X of the centre of the frame, in paper units.</param>
        /// <param name="centerY">The Y of the centre of the frame, in paper units.</param>
        /// <param name="width">The width of the frame in paper units.</param>
        /// <param name="height">The height of the frame in paper units.</param>
        /// <param name="viewCenterX">The X of the point of model space at the centre of the frame.</param>
        /// <param name="viewCenterY">The Y of the point of model space at the centre of the frame.</param>
        /// <param name="scale">The paper length of one unit of model space: a viewport at 1:20 has 0.05.</param>
        /// <param name="twist">The angle the view is turned by, in radians.</param>
        /// <param name="model">The model-space items it shows.</param>
        /// <param name="frozenLayers">The layers frozen in this viewport, which it leaves out; may be null.</param>
        /// <param name="style">How its frame is drawn.</param>
        /// <param name="handle">The handle of the viewport entity.</param>
        /// <param name="layer">The layer of the viewport entity.</param>
        public DisplayViewport2(double centerX, double centerY, double width, double height, double viewCenterX, double viewCenterY, double scale, double twist, DisplayList2 model, ISet<string> frozenLayers, DisplayStyle style, string handle, string layer)
            : base(style, handle, layer)
        {
            CenterX = centerX;
            CenterY = centerY;
            Width = width;
            Height = height;
            ViewCenterX = viewCenterX;
            ViewCenterY = viewCenterY;
            Scale = scale;
            Twist = twist;
            Model = model ?? throw new ArgumentNullException(nameof(model));
            FrozenLayers = frozenLayers ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _bounds = new DisplayBox(centerX - width / 2.0, centerY - height / 2.0, centerX + width / 2.0, centerY + height / 2.0);
        }

        /// <summary>Gets the X of the centre of the frame, in paper units.</summary>
        public double CenterX { get; }

        /// <summary>Gets the Y of the centre of the frame, in paper units.</summary>
        public double CenterY { get; }

        /// <summary>Gets the width of the frame in paper units.</summary>
        public double Width { get; }

        /// <summary>Gets the height of the frame in paper units.</summary>
        public double Height { get; }

        /// <summary>Gets the X of the point of model space at the centre of the frame.</summary>
        public double ViewCenterX { get; }

        /// <summary>Gets the Y of the point of model space at the centre of the frame.</summary>
        public double ViewCenterY { get; }

        /// <summary>Gets the paper length of one unit of model space.</summary>
        public double Scale { get; }

        /// <summary>Gets the angle the view is turned by, in radians.</summary>
        public double Twist { get; }

        /// <summary>Gets the model-space items the viewport shows.</summary>
        public DisplayList2 Model { get; }

        /// <summary>Gets the layers frozen in this viewport.</summary>
        public ISet<string> FrozenLayers { get; }

        /// <summary>Gets the transformation from model space to the paper, as the viewport shows it.</summary>
        public Affine2 ModelToPaper
        {
            get
            {
                Affine2 toView = Affine2.Translation(-ViewCenterX, -ViewCenterY);
                Affine2 turn = Affine2.ScaleRotate(Scale, Scale, -Twist);
                return toView.Then(turn).Then(Affine2.Translation(CenterX, CenterY));
            }
        }

        /// <inheritdoc/>
        public override DisplayBox Bounds => _bounds;

        /// <inheritdoc/>
        public override double DistanceTo(double x, double y)
        {
            double dx = Math.Min(Math.Abs(x - _bounds.MinX), Math.Abs(x - _bounds.MaxX));
            double dy = Math.Min(Math.Abs(y - _bounds.MinY), Math.Abs(y - _bounds.MaxY));
            bool insideX = x >= _bounds.MinX && x <= _bounds.MaxX;
            bool insideY = y >= _bounds.MinY && y <= _bounds.MaxY;

            if (insideX && insideY)
            {
                return Math.Min(dx, dy);
            }

            if (insideX)
            {
                return dy;
            }

            return insideY ? dx : Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
