using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GeometryHelper.Viewer.Views;
using HelixToolkit.Wpf.SharpDX;
using Microsoft.Win32;

namespace GeometryHelper.Viewer
{
    public partial class MainWindow
    {
        private static readonly Brush GapBrush = new SolidColorBrush(Color.FromRgb(0xB8, 0xBC, 0xC4));
        private HelixToolkit.SharpDX.Core.DefaultEffectsManager _effects;

        #region Views

        /// <summary>
        /// Gets the active view: the one the keys act on, and the one the mouse was last over.
        /// </summary>
        private ViewPane Active => _panes[_layout.Active];

        private ViewPane PaneOf(object viewport) => _panes.FirstOrDefault(p => p != null && ReferenceEquals(p.Viewport, viewport));

        /// <summary>
        /// Takes a view as it is now, with the clip planes that cut the scene in it.
        /// </summary>
        private ScreenMap MapOf(ViewPane pane)
            => new ScreenMap(pane.Camera, _model.Primitives?.Bounds ?? default(Bounds3), pane.Width, pane.Height, _clips);

        private ScreenMap MakeMap() => MapOf(Active);

        private void EnsurePane(int slot)
        {
            if (_panes[slot] != null)
            {
                return;
            }

            var pane = new ViewPane(slot, _layout.Slots[slot], _model, _effects);
            pane.View.Grid = GridMenu.IsChecked == true;
            pane.View.WorkPlane = _workPlane;
            pane.Viewport.PreviewMouseDown += OnViewMouseDown;
            pane.Viewport.PreviewMouseMove += OnViewMouseMove;
            pane.Viewport.PreviewMouseUp += OnViewMouseUp;
            pane.Viewport.PreviewMouseWheel += OnViewMouseWheel;
            pane.Viewport.SizeChanged += OnViewSizeChanged;
            pane.Viewport.MouseLeave += OnViewMouseLeave;
            pane.View.OriginChanged += (s, e) =>
            {
                pane.Overlay.Rebuild();
                UpdateCamera(pane, false);
            };

            _overlay.Add(pane.Overlay);
            Panes.Children.Add(pane.Frame);
            _panes[slot] = pane;
        }

        /// <summary>
        /// Puts the window in the state of the layout: makes the views that are shown and have no viewport yet, shows and hides, marks the active one, and fits what is new to the scene.
        /// </summary>
        private void ShowLayout()
        {
            foreach (int slot in _layout.Shown)
            {
                EnsurePane(slot);
            }

            bool several = _layout.Count > 1;

            for (int slot = 0; slot < ViewLayout.MaxViews; slot++)
            {
                ViewPane pane = _panes[slot];

                if (pane == null)
                {
                    continue;
                }

                bool shown = _layout.Shown.Contains(slot);

                // A view that is not shown lets go of its geometry and its overlay, and takes them again when it is shown.
                if (shown && !pane.Attached)
                {
                    pane.View.Attach();
                    _overlay.Add(pane.Overlay);
                }
                else if (!shown && pane.Attached)
                {
                    _overlay.Remove(pane.Overlay);
                    pane.Detach();
                }

                pane.Frame.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
                pane.ShowActive(several && slot == _layout.Active);
                pane.UpdateCaption(several && shown);
            }

            // What shows between two views is a grey line; one view has nothing behind it, as the single view always had none.
            Panes.Background = several ? GapBrush : null;
            Panes.InvalidateMeasure();
            Panes.UpdateLayout();

            // A view that is shown for the first time looks at the whole scene, from the direction it was made for.
            if (_model.Scene != null)
            {
                foreach (int slot in _layout.UnfittedShown())
                {
                    _panes[slot].Camera.Fit(_model.VisibleBounds(), _panes[slot].Aspect);
                    _panes[slot].Fitted = true;
                }
            }

            UpdateAllCameras();

            // A view that is shown now has the gap lens at its own zoom; one that is not has none.
            RefreshLens(true);
            LinkMenu.IsChecked = Active.Slot.Linked;
            UpdateModeText();
        }

        private void SetLayout(int count)
        {
            _layout.SetCount(count);
            ShowLayout();
            SetMessage(count == 1 ? "one view" : count + " views");
        }

        /// <summary>
        /// Makes a view the active one: the keys act on it, and it has the border.
        /// </summary>
        private void Activate(ViewPane pane)
        {
            if (_layout.Active == pane.Number || !_layout.Shown.Contains(pane.Number))
            {
                return;
            }

            _layout.Activate(pane.Number);
            bool several = _layout.Count > 1;

            foreach (ViewPane each in _panes.Where(p => p != null))
            {
                each.ShowActive(several && each.Number == _layout.Active);
            }

            LinkMenu.IsChecked = pane.Slot.Linked;
            UpdateModeText();
        }

        private void ToggleLink()
        {
            ViewPane pane = Active;
            pane.Slot.Linked = !pane.Slot.Linked;
            LinkMenu.IsChecked = pane.Slot.Linked;
            pane.UpdateCaption(_layout.Count > 1);
            SetMessage(pane.Slot.Linked ? "view linked: it zooms and pans with the other linked views" : "view not linked");

            if (pane.Slot.Linked)
            {
                // The view takes the others with it from now on; the others do not wait for the next move to come to it.
                UpdateCamera();
            }
        }

        private void OnLinkMenu(object sender, RoutedEventArgs e)
        {
            if (Active.Slot.Linked != (LinkMenu.IsChecked == true))
            {
                ToggleLink();
            }
        }

        private void SetGrid(bool on)
        {
            foreach (ViewPane pane in _panes.Where(p => p != null))
            {
                pane.View.Grid = on;
            }
        }

        private void InvalidateViews()
        {
            foreach (ViewPane pane in _panes.Where(p => p != null))
            {
                pane.Viewport.InvalidateRender();
            }
        }

        /// <summary>
        /// Cuts a scene with the clip planes that were in force when its loading began, on another thread where there are any. A set that is put in force while the scene
        /// loads is cut for the scene once it is shown, by <see cref="CutAfterShow"/>.
        /// </summary>
        private Task<ClipResult> ClipAsync(Primitives primitives, ClipSet clips)
        {
            bool caps = _clipCaps;
            return clips.IsActive ? Task.Run(() => ClipResult.Build(primitives, clips, caps)) : Task.FromResult(ClipResult.Build(primitives, clips, caps));
        }

        /// <summary>
        /// Called when a scene has been shown that was cut by a set of planes: if the planes in force are not that set, they changed while the scene loaded, and it is cut again.
        /// </summary>
        private void CutAfterShow(ClipSet used)
        {
            if (!ReferenceEquals(used, _clips))
            {
                ApplyClipsAsync();
            }
        }

        /// <summary>
        /// Waits until no view is making geometry for the graphics card, so that a picture shows what was asked of the scene.
        /// </summary>
        private async Task SettleAsync()
        {
            for (int tries = 0; tries < 200 && (_clipRunning || _panes.Any(p => p != null && p.View.IsRebasing)); tries++)
            {
                await Task.Delay(50);
            }
        }

        private async Task RebaseAllAsync()
        {
            foreach (int slot in _layout.Shown)
            {
                await RebaseNowAsync(_panes[slot]);
            }
        }

        #endregion

        #region Saved views

        private static void CopyLook(ViewCamera to, ViewCamera from)
        {
            to.Target = from.Target;
            to.Distance = from.Distance;
            to.Azimuth = from.Azimuth;
            to.Elevation = from.Elevation;
            to.Orthographic = from.Orthographic;
        }

        /// <summary>
        /// Saves the active view in the scene under a name asked for, in place of a view that has the name.
        /// </summary>
        private void SaveActiveView()
        {
            if (_model.Scene == null)
            {
                SetMessage("open a scene first");
                return;
            }

            string name = NameDialog.Ask(this, "Save the view", "Name of the view:", "view " + (_model.Scene.Views.Count + 1).ToString(CultureInfo.InvariantCulture));

            if (!string.IsNullOrWhiteSpace(name))
            {
                SaveActiveViewAs(name.Trim());
            }
        }

        private void SaveActiveViewAs(string name)
        {
            SavedView view = SavedView.FromCamera(name, Active.Camera, Active.Aspect);
            int at = _model.Scene.Views.FindIndex(v => string.Equals(v.Name, name, StringComparison.Ordinal));

            if (at >= 0)
            {
                _model.Scene.Views[at] = view;
            }
            else
            {
                _model.Scene.Views.Add(view);
            }

            RebuildSavedViewsMenu();
            KeepViewFile(view);
            Log("view saved: " + name + " (eye " + view.Eye + ", target " + view.Target + (view.Orthographic ? ", ortho, " + view.Width.ToString("G6", CultureInfo.InvariantCulture) + " wide" : ", perspective") + "). Save the scene to keep it in the file.");
            SetMessage("view saved: " + name);
        }

        private void RestoreSavedView(SavedView view)
        {
            ViewPane pane = Active;
            CopyLook(pane.Camera, view.ToCamera(pane.Aspect));
            pane.Saved3D = null;
            UpdateCamera();
            pane.UpdateCaption(_layout.Count > 1);
            SetMessage("view: " + view.Name);
        }

        /// <summary>
        /// Opens the active view at the first view the scene has saved, which is how a repro opens at the spot it is about.
        /// </summary>
        private void RestoreFirstSavedView()
        {
            if (_model.Scene != null && _model.Scene.Views.Count > 0)
            {
                RestoreSavedView(_model.Scene.Views[0]);
                Log("opened at the saved view '" + _model.Scene.Views[0].Name + "'.");
            }
        }

        private void RebuildSavedViewsMenu()
        {
            SavedViewsMenu.Items.Clear();
            IReadOnlyList<SavedView> views = (IReadOnlyList<SavedView>)_model.Scene?.Views ?? new SavedView[0];

            foreach (SavedView view in views)
            {
                SavedView captured = view;
                var item = new MenuItem { Header = view.Name.Replace("_", "__") };
                item.Click += (s, e) => RestoreSavedView(captured);
                SavedViewsMenu.Items.Add(item);
            }

            SavedViewsMenu.IsEnabled = SavedViewsMenu.Items.Count > 0;
        }

        #endregion

        #region Screenshots

        private async Task ScreenshotWithDialogAsync()
        {
            if (_model.Scene == null)
            {
                SetMessage("open a scene first");
                return;
            }

            ScreenshotOptions options = ScreenshotDialog.Ask(this, _settings.Screenshot ?? new ScreenshotOptions(), _layout.Count > 1);

            if (options == null)
            {
                return;
            }

            _settings.Screenshot = options;
            var dialog = new SaveFileDialog
            {
                Title = "Save the picture",
                Filter = "PNG picture (*.png)|*.png",
                FileName = "view.png",
                InitialDirectory = _settings.LastFolder != null && Directory.Exists(_settings.LastFolder) ? _settings.LastFolder : null,
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                string note = await SaveScreenshotAsync(dialog.FileName, options);
                Log("picture written to " + dialog.FileName + " (" + note + ")");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                Log("Cannot save the picture " + dialog.FileName + ": " + e.Message);
            }
        }

        /// <summary>
        /// Takes the picture of the views and writes it as a PNG.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <param name="options">How the picture is taken.</param>
        /// <param name="frames">How many frames, a tenth of a second each, the graphics card is given first to draw what was last asked of it; a picture that has waited already asks for none.</param>
        /// <returns>What the picture is: its size in pixels, the views in it and what is behind them.</returns>
        private async Task<string> SaveScreenshotAsync(string path, ScreenshotOptions options, int frames = 6)
        {
            await SettleAsync();
            BitmapSource picture = await CaptureAsync(options, frames);
            string folder = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(folder);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(picture));

            using (FileStream stream = File.Create(path))
            {
                encoder.Save(stream);
            }

            return picture.PixelWidth + " x " + picture.PixelHeight + ", " + (options.AllViews ? _layout.Count + " view(s)" : "the active view") + ", " + options.Background.ToString().ToLowerInvariant() + " background";
        }

        /// <summary>
        /// Draws each view that is in the picture at the size it has there, with a graphics card render of its own, and puts them in one picture where the
        /// window has them.
        /// </summary>
        private async Task<BitmapSource> CaptureAsync(ScreenshotOptions options, int frames)
        {
            // The graphics card draws on its own schedule; a few frames are given to it so that what the commands before this did is on the screen.
            for (int frame = 0; frame < frames; frame++)
            {
                await Task.Delay(100);
                InvalidateViews();
            }

            Panes.UpdateLayout();

            // One view at the size of the window over the colour of the window is what the window shows, and is taken from the window as pictures always were,
            // to the pixel; every other picture is drawn again by the graphics card at the size it has in the picture.
            if (_layout.Count == 1 && options.Scale == 1 && options.Background == ScreenshotBackground.Window)
            {
                ViewHost.UpdateLayout();
                var window = new RenderTargetBitmap((int)Math.Ceiling(ViewHost.ActualWidth), (int)Math.Ceiling(ViewHost.ActualHeight), 96.0, 96.0, PixelFormats.Pbgra32);
                window.Render(ViewHost);
                window.Freeze();
                return window;
            }

            IReadOnlyList<ViewCell> cells = options.Cells(_layout, Panes.ActualWidth, Panes.ActualHeight, ViewHostPanel.Gap);
            options.PictureSize(_layout, Panes.ActualWidth, Panes.ActualHeight, ViewHostPanel.Gap, out int width, out int height);
            var drawing = new DrawingVisual();

            using (DrawingContext context = drawing.RenderOpen())
            {
                if (options.Background == ScreenshotBackground.White)
                {
                    // The views are white; what shows between them, where there are several, is a light grey line so that the layout can be read.
                    context.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xD0, 0xD3, 0xD9)), null, new Rect(0, 0, width, height));
                }
                else if (options.Background == ScreenshotBackground.Window)
                {
                    context.DrawRectangle(GapBrush, null, new Rect(0, 0, width, height));
                }

                foreach (ViewCell cell in cells)
                {
                    // A view takes the pixels its cell touches, so that a window a fraction of a pixel wide has no row of the background along its edge.
                    ViewPane pane = _panes[cell.Slot];
                    int left = (int)Math.Round(cell.X);
                    int top = (int)Math.Round(cell.Y);
                    int w = Math.Max(1, Math.Min(width, (int)Math.Ceiling(cell.X + cell.Width - 1E-6)) - left);
                    int h = Math.Max(1, Math.Min(height, (int)Math.Ceiling(cell.Y + cell.Height - 1E-6)) - top);
                    BitmapSource image = options.Background == ScreenshotBackground.Transparent
                        ? await RenderTransparentAsync(pane, w, h)
                        : RenderView(pane, w, h, options.Background == ScreenshotBackground.White ? (Color?)Colors.White : null);
                    context.DrawImage(image, new Rect(left, top, w, h));
                }
            }

            var bitmap = new RenderTargetBitmap(width, height, 96.0, 96.0, PixelFormats.Pbgra32);
            bitmap.Render(drawing);
            bitmap.Freeze();

            // Drawing a view at another size may have left its viewport at that size; the window lays its views out again as they are.
            foreach (ViewPane pane in _panes.Where(p => p != null))
            {
                pane.Frame.InvalidateMeasure();
                pane.Viewport.InvalidateMeasure();
            }

            Panes.InvalidateMeasure();
            Panes.InvalidateArrange();
            Panes.UpdateLayout();
            return bitmap;
        }

        private static BitmapSource RenderView(ViewPane pane, int width, int height, Color? background)
        {
            Color keep = pane.Viewport.BackgroundColor;

            try
            {
                if (background.HasValue)
                {
                    pane.Viewport.BackgroundColor = background.Value;
                }

                BitmapSource image = pane.Viewport.RenderBitmap(width, height);
                image.Freeze();
                return image;
            }
            finally
            {
                pane.Viewport.BackgroundColor = keep;
            }
        }

        /// <summary>
        /// Takes a view over nothing. The graphics card has no alpha to give here, so the view is taken twice, over white and over black; a pixel the scene
        /// covers is the same in both, a pixel it does not cover is white in one and black in the other, and between them the alpha and the colour of every
        /// pixel are found: alpha = 1 - (white - black) / 255, and the colour is the black picture over the alpha.
        /// </summary>
        private async Task<BitmapSource> RenderTransparentAsync(ViewPane pane, int width, int height)
        {
            BitmapSource onWhite = RenderView(pane, width, height, Colors.White);
            await Task.Yield();
            BitmapSource onBlack = RenderView(pane, width, height, Colors.Black);
            return Matte(onWhite, onBlack);
        }

        /// <summary>
        /// Makes a picture with an alpha channel from the same picture over white and over black.
        /// </summary>
        private static BitmapSource Matte(BitmapSource onWhite, BitmapSource onBlack)
        {
            int width = onBlack.PixelWidth;
            int height = onBlack.PixelHeight;
            int stride = width * 4;
            var white = new byte[stride * height];
            var black = new byte[stride * height];
            new FormatConvertedBitmap(onWhite, PixelFormats.Bgra32, null, 0).CopyPixels(white, stride, 0);
            new FormatConvertedBitmap(onBlack, PixelFormats.Bgra32, null, 0).CopyPixels(black, stride, 0);
            var result = new byte[stride * height];

            for (int i = 0; i < result.Length; i += 4)
            {
                double covered = ((white[i] - black[i]) + (white[i + 1] - black[i + 1]) + (white[i + 2] - black[i + 2])) / (3.0 * 255.0);
                double alpha = Math.Max(0.0, Math.Min(1.0, 1.0 - covered));

                if (alpha < 1.0 / 512.0)
                {
                    continue;
                }

                result[i] = (byte)Math.Min(255.0, black[i] / alpha + 0.5);
                result[i + 1] = (byte)Math.Min(255.0, black[i + 1] / alpha + 0.5);
                result[i + 2] = (byte)Math.Min(255.0, black[i + 2] / alpha + 0.5);
                result[i + 3] = (byte)(alpha * 255.0 + 0.5);
            }

            BitmapSource matte = BitmapSource.Create(width, height, 96.0, 96.0, PixelFormats.Bgra32, null, result, stride);
            matte.Freeze();
            return matte;
        }

        /// <summary>
        /// Writes the picture of a headless run, with the views and the size and the background the command line gave, and ends the program: 0 when
        /// the file was read and the picture written, 1 when not.
        /// </summary>
        private async Task TakeScreenshotAsync(bool opened)
        {
            if (!opened)
            {
                Console.Error.WriteLine("FAILED to open " + (_command.File ?? "(no file)") + "; no picture.");
                Application.Current.Shutdown(1);
                return;
            }

            ViewHost.UpdateLayout();

            // A scene that has saved views opens at the first of them, and a picture of it is of that spot, so it is not fitted again; a --view given on the command line
            // is the view asked for, and wins over it.
            if (_model.Scene.Views.Count == 0 || _command.View != null)
            {
                FitAllViews();
            }

            await RebaseAllAsync();

            if (_command.Commands != null)
            {
                await RunCommandsAsync(_command.Commands);
                Console.WriteLine(Output.Text);
                Console.WriteLine("status: " + SnapText.Text + " | " + SnapsText.Text + " | " + SelectionText.Text + " | " + MessageText.Text);
            }

            // The graphics card draws the scene on its own schedule; a few frames are given to it before the picture is taken.
            for (int frame = 0; frame < 12; frame++)
            {
                await Task.Delay(150);
                InvalidateViews();
            }

            try
            {
                var options = new ScreenshotOptions { AllViews = true, Scale = _command.Scale, Background = _command.Background };
                string note = await SaveScreenshotAsync(_command.Screenshot, options, 0);
                Console.WriteLine("Wrote " + _command.Screenshot + " (" + note + ").");
                Application.Current.Shutdown(0);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is InvalidOperationException)
            {
                Console.Error.WriteLine("FAILED to write " + _command.Screenshot + ": " + e.Message);
                Application.Current.Shutdown(1);
            }
        }

        #endregion

        #region Headless commands

        /// <summary>
        /// Reads a place of the active view from the words of a command: two numbers, pixels of the view, or one word <c>@x,y,z</c>, a point of the scene taken to where it shows.
        /// </summary>
        private Point ParsePlace(string[] words, ref int at)
        {
            if (words[at].StartsWith("@", StringComparison.Ordinal))
            {
                double[] c = words[at++].Substring(1).Split(',').Select(w => double.Parse(w, CultureInfo.InvariantCulture)).ToArray();

                if (!MakeMap().TryProject(new Vec3(c[0], c[1], c.Length > 2 ? c[2] : 0.0), out double px, out double py, out _))
                {
                    throw new InvalidOperationException("the point " + words[at - 1] + " is not in the picture");
                }

                return new Point(px, py);
            }

            var p = new Point(double.Parse(words[at], CultureInfo.InvariantCulture), double.Parse(words[at + 1], CultureInfo.InvariantCulture));
            at += 2;
            return p;
        }

        private static int SlotOf(string word)
        {
            switch (word.ToLowerInvariant())
            {
                case "plan":
                case "top": return 0;
                case "front": return 1;
                case "right": return 2;
                case "iso": return 3;
                default:
                    return int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out int slot) && slot >= 0 && slot < ViewLayout.MaxViews ? slot : -1;
            }
        }

        private static bool TryMode(string word, out RenderMode mode)
        {
            switch (word.ToLowerInvariant())
            {
                case "wireframe": mode = RenderMode.Wireframe; return true;
                case "shaded": mode = RenderMode.Shaded; return true;
                case "edges": mode = RenderMode.ShadedEdges; return true;
                case "transparent": mode = RenderMode.Transparent; return true;
                default: mode = RenderMode.ShadedEdges; return false;
            }
        }

        /// <summary>
        /// Does the commands of <c>--do</c> that are about the views, the clip planes, the saved views and the pictures: <c>views 1|2|4</c>,
        /// <c>activeview plan|front|right|iso|0-3</c>, <c>viewmode view wireframe|shaded|edges|transparent</c>, <c>viewset view top|front|right|iso</c>,
        /// <c>grid on|off</c>, <c>viewsizes</c>, <c>cursor P</c> (the status bar readout for a place), <c>link view</c> (toggles), <c>clipselect n</c> (says where its handle is), <c>clipdrag n P P</c>, <c>clipoffset n d</c>, <c>clipoff n</c>, <c>clipon n</c>, <c>saveview name</c>, <c>restoreview name</c>,
        /// <c>shot file [all|active] [1|2|4] [window|white|transparent]</c> (and the work plane and the clip planes are set by <c>keys</c> and <c>click</c>, as a person does it).
        /// </summary>
        /// <returns>Whether the command was one of these.</returns>
        private async Task<bool> ViewCommandAsync(string[] words)
        {
            switch (words[0].ToLowerInvariant())
            {
                case "views":
                    SetLayout(int.Parse(words[1], CultureInfo.InvariantCulture));
                    return true;

                case "viewsizes":
                    foreach (int slot in _layout.Shown)
                    {
                        ViewPane pane = _panes[slot];
                        Log("view " + slot + (slot == _layout.Active ? " (active)" : string.Empty) + ": " + pane.Width.ToString("0.#", CultureInfo.InvariantCulture) + " x " + pane.Height.ToString("0.#", CultureInfo.InvariantCulture)
                            + ", " + pane.Mode + (pane.Camera.Orthographic ? ", ortho " + (2.0 * pane.Camera.Distance * pane.Camera.HalfHeightRatio * pane.Aspect).ToString("G6", CultureInfo.InvariantCulture) + " wide" : ", perspective") + (pane.Slot.Linked ? ", linked" : string.Empty)
                            + ", target " + pane.Camera.Target + ", distance " + pane.Camera.Distance.ToString("G6", CultureInfo.InvariantCulture) + ", render origin " + pane.View.Origin);
                    }

                    return true;

                case "cursor":
                {
                    // The readout of the status bar for a place: the offsets on the work plane, as the mouse would show them.
                    int at = 1;
                    ShowCursorPosition(Active, ParsePlace(words, ref at));
                    Log("cursor: " + CursorText.Text);
                    return true;
                }

                case "grid":
                    GridMenu.IsChecked = words.Length < 2 || !words[1].Equals("off", StringComparison.OrdinalIgnoreCase);
                    SetGrid(GridMenu.IsChecked == true);
                    return true;

                case "activeview":
                {
                    int slot = SlotOf(words[1]);

                    if (slot < 0 || !_layout.Shown.Contains(slot))
                    {
                        Log("--do: view " + words[1] + " is not shown");
                    }
                    else
                    {
                        Activate(_panes[slot]);
                    }

                    return true;
                }

                case "viewmode":
                {
                    int slot = SlotOf(words[1]);

                    if (slot < 0 || _panes[slot] == null || !TryMode(words[2], out RenderMode mode))
                    {
                        Log("--do: viewmode needs a view that is shown and a mode");
                    }
                    else
                    {
                        _panes[slot].Mode = mode;
                        _panes[slot].UpdateCaption(_layout.Count > 1);
                        UpdateModeText();
                    }

                    return true;
                }

                case "viewset":
                {
                    int slot = SlotOf(words[1]);
                    StandardView view;

                    switch (words[2].ToLowerInvariant())
                    {
                        case "top": view = StandardView.Top; break;
                        case "front": view = StandardView.Front; break;
                        case "right": view = StandardView.Right; break;
                        default: view = StandardView.Iso; break;
                    }

                    if (slot < 0 || _panes[slot] == null)
                    {
                        Log("--do: viewset needs a view that is shown");
                    }
                    else
                    {
                        _panes[slot].Camera.SetView(view);
                        _panes[slot].Saved3D = null;
                        UpdateCamera(_panes[slot], true);
                        _panes[slot].UpdateCaption(_layout.Count > 1);
                    }

                    return true;
                }

                case "link":
                {
                    int slot = SlotOf(words[1]);

                    if (slot < 0 || !_layout.Shown.Contains(slot))
                    {
                        Log("--do: view " + words[1] + " is not shown");
                    }
                    else
                    {
                        Activate(_panes[slot]);
                        ToggleLink();
                    }

                    return true;
                }

                case "clipselect":
                case "clipdrag":
                case "clipoffset":
                case "clipoff":
                case "clipon":
                    ClipCommand(words);
                    return true;

                case "clipsweep":
                {
                    // clipsweep n from to steps interval: the plane n is dragged from one offset to another in so many steps, a step every interval milliseconds as a mouse moves, and
                    // the output says how many cuts were shown while it went and how long the scene took to settle after it was let go.
                    if (words.Length < 6 || !int.TryParse(words[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) || number < 1 || number > _clips.Count)
                    {
                        Log("--do: clipsweep n from to steps interval needs the number of a clip plane");
                        return true;
                    }

                    double from = double.Parse(words[2], CultureInfo.InvariantCulture);
                    double to = double.Parse(words[3], CultureInfo.InvariantCulture);
                    int steps = int.Parse(words[4], CultureInfo.InvariantCulture);
                    int interval = int.Parse(words[5], CultureInfo.InvariantCulture);
                    int index = number - 1;
                    int shownBefore = _model.ClipsShown;
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    _clipDragIndex = index;

                    for (int step = 0; step <= steps; step++)
                    {
                        SetClips(_clips.Replace(index, _clips.Planes[index].WithOffset(from + (to - from) * step / steps)), index);
                        await Task.Delay(interval);
                    }

                    long dragged = clock.ElapsedMilliseconds;
                    int shownWhile = _model.ClipsShown - shownBefore;
                    EndClipDrag();
                    await SettleAsync();
                    Log("clipsweep: " + (steps + 1) + " moves of the plane " + interval + " ms apart took " + dragged + " ms and " + shownWhile + " cut(s) were shown while it moved; the full cut was in place " + (clock.ElapsedMilliseconds - dragged) + " ms after it was let go");
                    return true;
                }

                case "clipcaps":
                    ClipCapsMenu.IsChecked = words.Length < 2 || !words[1].Equals("off", StringComparison.OrdinalIgnoreCase);
                    OnClipCapsToggled(null, null);
                    return true;

                case "showtime":
                    Log("showtime: " + _model.ViewBuildMilliseconds.Count + " view(s) made their elements for the scene on the UI thread in "
                        + string.Join(" + ", _model.ViewBuildMilliseconds.Select(m => m.ToString("F1", CultureInfo.InvariantCulture))) + " ms = "
                        + _model.ViewBuildMilliseconds.Sum().ToString("F1", CultureInfo.InvariantCulture) + " ms, of which making the geometry " + string.Join(" + ", _model.GeometryMilliseconds.Select(m => m.ToString("F1", CultureInfo.InvariantCulture))) + " ms; " + _model.Geometry.Builds + " set(s) of geometry made");
                    return true;

                case "saveview":
                    if (_model.Scene == null)
                    {
                        Log("--do: no scene");
                    }
                    else
                    {
                        SaveActiveViewAs(string.Join(" ", words.Skip(1)));
                    }

                    return true;

                case "restoreview":
                {
                    string name = string.Join(" ", words.Skip(1));
                    SavedView view = _model.Scene?.Views.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.Ordinal));

                    if (view == null)
                    {
                        Log("--do: no saved view called '" + name + "'");
                    }
                    else
                    {
                        RestoreSavedView(view);
                    }

                    return true;
                }

                case "shot":
                {
                    var options = new ScreenshotOptions { AllViews = true, Scale = 1, Background = ScreenshotBackground.Window };

                    foreach (string word in words.Skip(2))
                    {
                        if (word.Equals("active", StringComparison.OrdinalIgnoreCase))
                        {
                            options.AllViews = false;
                        }
                        else if (word.Equals("all", StringComparison.OrdinalIgnoreCase))
                        {
                            options.AllViews = true;
                        }
                        else if (int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out int scale) && ScreenshotOptions.IsScale(scale))
                        {
                            options.Scale = scale;
                        }
                        else if (Enum.TryParse(word, true, out ScreenshotBackground background) && Enum.IsDefined(typeof(ScreenshotBackground), background))
                        {
                            options.Background = background;
                        }
                        else
                        {
                            Log("--do: shot does not know '" + word + "'");
                        }
                    }

                    string note = await SaveScreenshotAsync(words[1], options);
                    Log("picture written to " + words[1] + " (" + note + ")");
                    return true;
                }

                default:
                    return false;
            }
        }

        #endregion
    }
}
