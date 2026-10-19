using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using HelixToolkit.Wpf.SharpDX;

namespace GeometryHelper.Viewer.Views
{
    /// <summary>
    /// One 3D view of the window: its viewport, its elements for the scene, its overlay, its cameras and what it needs to move its render origin. The
    /// camera, the render mode and the link of the view are those of its <see cref="ViewSlot"/>, which the layout holds and tests.
    /// </summary>
    internal sealed class ViewPane
    {
        private static readonly string[] Names = { "Plan", "Front", "Right", "Iso" };
        private static readonly Brush ActiveBrush = new SolidColorBrush(Color.FromRgb(0x2F, 0x7F, 0xED));

        internal ViewPane(int number, ViewSlot slot, SceneModel model, HelixToolkit.SharpDX.Core.EffectsManager effects)
        {
            Number = number;
            Slot = slot;

            Viewport = new Viewport3DX
            {
                BackgroundColor = Color.FromRgb(0xE9, 0xEC, 0xF1),
                ShowCoordinateSystem = true,
                ShowViewCube = false,
                UseDefaultGestures = false,
                IsInertiaEnabled = false,
                IsRotationEnabled = false,
                IsPanEnabled = false,
                IsZoomEnabled = false,
                EnableSwapChainRendering = false,
                MSAA = HelixToolkit.SharpDX.Core.MSAALevel.Four,
                CoordinateSystemLabelForeground = Color.FromRgb(0x30, 0x30, 0x30),
                CoordinateSystemHorizontalPosition = -0.85,
                CoordinateSystemVerticalPosition = -0.8,
                CoordinateSystemSize = 1.6,
                EffectsManager = effects,
                Camera = Perspective,
            };
            Viewport.Items.Add(new AmbientLight3D { Color = Color.FromRgb(0x70, 0x70, 0x70) });
            Headlight = new DirectionalLight3D { Color = Color.FromRgb(0xE0, 0xE0, 0xE0), Direction = new System.Windows.Media.Media3D.Vector3D(0, 0, -1) };
            Viewport.Items.Add(Headlight);

            Band = new Rectangle { Visibility = Visibility.Collapsed, StrokeThickness = 1.2 };
            var canvas = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
            canvas.Children.Add(Band);

            Caption = new TextBlock
            {
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(6, 4, 0, 0),
                Padding = new Thickness(5, 1, 5, 1),
                Background = new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30)),
                Visibility = Visibility.Collapsed,
            };

            var content = new Grid();
            content.Children.Add(Viewport);
            content.Children.Add(canvas);
            content.Children.Add(Caption);
            Outline = new Rectangle { Stroke = ActiveBrush, StrokeThickness = 3.0, Fill = null, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
            content.Children.Add(Outline);
            Frame = content;
            Frame.Tag = number;

            View = new SceneView(Viewport, model) { Mode = slot.Mode };
            Overlay = new OverlayView(Viewport, View);
        }

        /// <summary>
        /// Gets the number of the view, from 0 to 3, which is its place in the layout.
        /// </summary>
        internal int Number { get; }

        /// <summary>
        /// Gets the camera, render mode and link of the view.
        /// </summary>
        internal ViewSlot Slot { get; }

        /// <summary>
        /// Gets the camera of the view.
        /// </summary>
        internal ViewCamera Camera => Slot.Camera;

        /// <summary>
        /// Gets the element the window puts in its panel: the viewport with the rubber band, the caption and the outline of the active view over it.
        /// </summary>
        internal Grid Frame { get; }

        /// <summary>
        /// Gets the outline drawn over the edge of the view when it is the active one of several, which takes no room from the viewport.
        /// </summary>
        internal Rectangle Outline { get; }

        /// <summary>
        /// Gets the viewport.
        /// </summary>
        internal Viewport3DX Viewport { get; }

        /// <summary>
        /// Gets the light that shines along the line of sight.
        /// </summary>
        internal DirectionalLight3D Headlight { get; }

        /// <summary>
        /// Gets the rectangle the box select draws.
        /// </summary>
        internal Rectangle Band { get; }

        /// <summary>
        /// Gets the elements of the scene in this view.
        /// </summary>
        internal SceneView View { get; }

        /// <summary>
        /// Gets what is drawn over the scene in this view.
        /// </summary>
        internal OverlayView Overlay { get; }

        /// <summary>
        /// Gets the camera with perspective that is given to the viewport when the camera of the view has some.
        /// </summary>
        internal PerspectiveCamera Perspective { get; } = new PerspectiveCamera { FieldOfView = 45.0 };

        /// <summary>
        /// Gets the camera with none.
        /// </summary>
        internal OrthographicCamera Orthographic { get; } = new OrthographicCamera();

        /// <summary>
        /// Gets the text over the view saying which it is.
        /// </summary>
        internal TextBlock Caption { get; }

        /// <summary>
        /// Gets or sets the timer that moves the render origin once the camera has rested.
        /// </summary>
        internal DispatcherTimer RebaseTimer { get; set; }

        /// <summary>
        /// Gets or sets the camera the plan was switched to from, to switch back; null when the view is not the plan switched to.
        /// </summary>
        internal ViewCamera Saved3D { get; set; }

        /// <summary>
        /// Gets or sets whether the view has been fitted to the scene that is shown, which the layout keeps and a new scene clears for every view.
        /// </summary>
        internal bool Fitted
        {
            get => Slot.Fitted;
            set => Slot.Fitted = value;
        }

        /// <summary>
        /// Gets whether the view holds the elements of the scene: a view that is not shown lets go of them, and takes them again when it is.
        /// </summary>
        internal bool Attached => View.IsAttached;

        /// <summary>
        /// Lets go of the elements of the scene and of the overlay, which a view that is not shown has no use for: its geometry is released, and made again when the view is shown.
        /// </summary>
        internal void Detach()
        {
            RebaseTimer?.Stop();
            Overlay.Clear();
            View.Detach();
        }

        /// <summary>
        /// Gets the width of the viewport.
        /// </summary>
        internal double Width => Viewport.ActualWidth;

        /// <summary>
        /// Gets the height of the viewport.
        /// </summary>
        internal double Height => Viewport.ActualHeight;

        /// <summary>
        /// Gets the width of the viewport over its height.
        /// </summary>
        internal double Aspect => Viewport.ActualHeight > 0.0 ? Viewport.ActualWidth / Viewport.ActualHeight : 1.5;

        /// <summary>
        /// Gets or sets how the view draws the scene.
        /// </summary>
        internal RenderMode Mode
        {
            get => Slot.Mode;
            set
            {
                Slot.Mode = value;
                View.Mode = value;
            }
        }

        /// <summary>
        /// Shows the view as the active one or not.
        /// </summary>
        internal void ShowActive(bool active)
        {
            Outline.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Writes which view this is over it, or takes the text away: a window with one view has no use for it.
        /// </summary>
        internal void UpdateCaption(bool show)
        {
            Caption.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            Caption.Text = Names[Number] + " - " + Slot.Mode + (Camera.Orthographic ? ", ortho" : ", perspective") + (Slot.Linked ? ", linked" : string.Empty);
        }

        /// <summary>
        /// Lets go of what the view holds.
        /// </summary>
        internal void Dispose()
        {
            RebaseTimer?.Stop();
            View.Dispose();
            Viewport.Dispose();
        }
    }
}
