using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>A node of the View Manager's tree: Current, a heading (Model Views, Layout Views, Preset Views) or a named view.</summary>
    public sealed class ViewNodeViewModel
    {
        internal ViewNodeViewModel(string name, int level, CadViewRow row, bool heading, bool enabled)
        {
            Name = name;
            Level = level;
            Row = row;
            IsHeading = heading;
            IsEnabled = enabled;
        }

        /// <summary>Gets the name shown.</summary>
        public string Name { get; }

        /// <summary>Gets the depth in the tree, nought for a heading or Current.</summary>
        public int Level { get; }

        /// <summary>Gets the left margin of the row, 10 and 18 a level.</summary>
        public double Indent => 10 + (Level * 18);

        /// <summary>Gets the view; null for a heading, and Current's for Current.</summary>
        public CadViewRow Row { get; }

        /// <summary>Gets whether it is a heading.</summary>
        public bool IsHeading { get; }

        /// <summary>Gets whether it can be chosen: Preset Views is greyed, a drawing here being 2D.</summary>
        public bool IsEnabled { get; }

        /// <summary>Gets the mark before a heading: ▾ for one open, ▸ for Preset Views.</summary>
        public string Twisty => IsHeading ? (IsEnabled ? "▾" : "▸") : string.Empty;

        /// <summary>Gets whether it is a named view, which Set Current, Update Layers, Edit Boundaries and Delete need.</summary>
        public bool IsNamed => Row != null && !IsHeading && Level > 0;
    }

    /// <summary>A row of the View Manager's properties: a heading, or a label and its value, greyed where it is read only.</summary>
    public sealed class ViewPropertyViewModel
    {
        internal ViewPropertyViewModel(string label, string value, bool readOnly, bool heading)
        {
            Label = label;
            Value = value;
            IsReadOnly = readOnly;
            IsHeading = heading;
        }

        /// <summary>Gets the label, as Height.</summary>
        public string Label { get; }

        /// <summary>Gets the value, as 9 000.00.</summary>
        public string Value { get; }

        /// <summary>Gets whether the value is read only, greyed: the camera, perspective, clipping and the 3D settings.</summary>
        public bool IsReadOnly { get; }

        /// <summary>Gets whether the row is a heading: General, View, Clipping.</summary>
        public bool IsHeading { get; }
    }

    /// <summary>
    /// AutoCAD's View Manager, which VIEW shows, over the request its command hands the window: the tree of Current, Model
    /// Views, Layout Views and Preset Views (greyed), the properties of the node chosen, Set Current, New…, Update Layers,
    /// Edit Boundaries… and Delete. The name, height and width of a named view are typed in and written on Apply or OK, with
    /// the view Set Current chose restored; Cancel takes every change back.
    /// </summary>
    public sealed class ViewManagerViewModel : ObservableObject
    {
        private readonly ObservableCollection<ViewNodeViewModel> _nodes = new ObservableCollection<ViewNodeViewModel>();
        private ObservableCollection<ViewPropertyViewModel> _properties = new ObservableCollection<ViewPropertyViewModel>();
        private ViewNodeViewModel _selected;
        private string _name = string.Empty;
        private string _height = string.Empty;
        private string _width = string.Empty;

        /// <summary>Makes the dialog over a request.</summary>
        /// <param name="request">The request of VIEW.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public ViewManagerViewModel(CadViewManagerRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Refresh();
        }

        /// <summary>Gets the request.</summary>
        public CadViewManagerRequest Request { get; }

        /// <summary>Gets or sets how the window shows New View; true where its Define window closed it, so that this dialog closes too.</summary>
        public Func<NewViewViewModel, bool> ShowNewView { get; set; }

        /// <summary>Gets the nodes of the tree, in its order.</summary>
        public ObservableCollection<ViewNodeViewModel> Nodes => _nodes;

        /// <summary>Gets the properties of the node chosen.</summary>
        public ObservableCollection<ViewPropertyViewModel> Properties => _properties;

        /// <summary>Gets the heading of the properties, as Properties of Stair core.</summary>
        public string PropertiesTitle => "Properties of " + (_selected?.Name ?? "Current");

        /// <summary>Gets or sets the node chosen; a heading or Preset Views is not taken.</summary>
        public ViewNodeViewModel Selected
        {
            get => _selected;
            set
            {
                if (value != null && (value.IsHeading || !value.IsEnabled))
                {
                    Raise(nameof(Selected));
                    return;
                }

                if (Set(ref _selected, value))
                {
                    Request.Selected = value != null && value.IsNamed ? value.Name : null;
                    Show();
                }
            }
        }

        /// <summary>Gets whether a named view is chosen, which all but New… need.</summary>
        public bool IsNamedChosen => _selected != null && _selected.IsNamed;

        /// <summary>Gets or sets the name of the view chosen, as typed; written on Apply or OK.</summary>
        public string ViewName { get => _name; set => Set(ref _name, value ?? string.Empty); }

        /// <summary>Gets or sets the height of the view chosen, as typed.</summary>
        public string ViewHeight { get => _height; set => Set(ref _height, value ?? string.Empty); }

        /// <summary>Gets or sets the width of the view chosen, as typed.</summary>
        public string ViewWidth { get => _width; set => Set(ref _width, value ?? string.Empty); }

        /// <summary>Gets the view Set Current chose, restored on Apply or OK; empty for none.</summary>
        public string ToRestore => Request.ToRestore ?? string.Empty;

        /// <summary>Gets what the last change said where it was refused; empty where none was.</summary>
        public string Message => Request.Message ?? string.Empty;

        /// <summary>Set Current: the view chosen restored on Apply or OK.</summary>
        public void SetCurrent()
        {
            if (IsNamedChosen)
            {
                Request.ToRestore = _selected.Name;
                Raise(nameof(ToRestore));
            }
        }

        /// <summary>New…: New View over the request; its Define window closes both dialogs for the command line.</summary>
        /// <returns>Whether the dialog is to close for the pick.</returns>
        public bool New()
        {
            if (ShowNewView == null)
            {
                return false;
            }

            Request.IsNewViewOpen = true;
            bool picked = ShowNewView(new NewViewViewModel(Request));
            Request.IsNewViewOpen = picked;
            Refresh();
            return picked;
        }

        /// <summary>Update Layers: the layers as they stand saved as the view chosen's layer snapshot.</summary>
        /// <returns>Whether they were.</returns>
        public bool UpdateLayers() => IsNamedChosen && Changed(Request.UpdateLayers());

        /// <summary>Edit Boundaries…: the dialog closed for the command line to ask two corners.</summary>
        /// <returns>Whether it is to close.</returns>
        public bool EditBoundaries()
        {
            if (!IsNamedChosen)
            {
                return false;
            }

            Request.Pick = "Boundaries";
            Request.Action = CadDialogAction.SelectObjects;
            return true;
        }

        /// <summary>Delete: the view chosen taken out with its layer snapshot.</summary>
        /// <returns>Whether it was.</returns>
        public bool Delete() => IsNamedChosen && Changed(Request.Delete());

        /// <summary>Apply: the name, height and width typed written, and the view Set Current chose restored, the dialog left open.</summary>
        /// <returns>Whether all were written.</returns>
        public bool Apply()
        {
            if (!Write())
            {
                return false;
            }

            return Request.ToRestore == null || Changed(Request.Apply());
        }

        /// <summary>OK: the name, height and width typed written; the command restores the view Set Current chose.</summary>
        /// <returns>Whether the dialog closes.</returns>
        public bool Ok()
        {
            if (!Write())
            {
                return false;
            }

            Request.Action = CadDialogAction.Ok;
            return true;
        }

        /// <summary>Cancel: every change taken back.</summary>
        public void Cancel() => Request.Action = CadDialogAction.Cancel;

        private bool Write()
        {
            if (!IsNamedChosen)
            {
                return true;
            }

            CadViewRow row = _selected.Row;

            if (!string.Equals(_name.Trim(), row.Name, StringComparison.Ordinal) && !Changed(Request.Rename(_name)))
            {
                return false;
            }

            if (!double.TryParse(_height.Replace(" ", string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out double height)
                || !double.TryParse(_width.Replace(" ", string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out double width))
            {
                Request.Resize(double.NaN, double.NaN);
                Raise(nameof(Message));
                return false;
            }

            CadViewRow now = Request.Views.FirstOrDefault(v => string.Equals(v.Name, Request.Selected, StringComparison.OrdinalIgnoreCase)) ?? row;
            bool same = Number(height) == Number(now.Height) && Number(width) == Number(now.Width);
            return same || Changed(Request.Resize(height, width));
        }

        private bool Changed(bool done)
        {
            Refresh();
            Raise(nameof(Message));
            Raise(nameof(ToRestore));
            return done;
        }

        /// <summary>The tree made again from the request, the view of <see cref="CadViewManagerRequest.Selected"/> chosen, else Current.</summary>
        private void Refresh()
        {
            string chosen = Request.Selected;
            _nodes.Clear();
            _nodes.Add(new ViewNodeViewModel("Current", 0, Request.CurrentView, false, true));
            _nodes.Add(new ViewNodeViewModel("Model Views", 0, null, true, true));

            foreach (CadViewRow row in Request.Views.Where(v => !v.IsPaperspace))
            {
                _nodes.Add(new ViewNodeViewModel(row.Name, 1, row, false, true));
            }

            _nodes.Add(new ViewNodeViewModel("Layout Views", 0, null, true, true));

            foreach (CadViewRow row in Request.Views.Where(v => v.IsPaperspace))
            {
                _nodes.Add(new ViewNodeViewModel(row.Name, 1, row, false, true));
            }

            _nodes.Add(new ViewNodeViewModel("Preset Views", 0, null, true, false));
            _selected = _nodes.FirstOrDefault(n => n.IsNamed && string.Equals(n.Name, chosen, StringComparison.OrdinalIgnoreCase)) ?? _nodes[0];
            Request.Selected = _selected.IsNamed ? _selected.Name : null;
            Raise(nameof(Selected));
            Show();
        }

        /// <summary>The properties of the node chosen, and its name, height and width in the boxes.</summary>
        private void Show()
        {
            CadViewRow row = _selected?.Row;
            var list = new List<ViewPropertyViewModel>
            {
                Heading("General"),
                Row("Name", _selected?.Name ?? "Current", !IsNamedChosen),
                Row("Category", "<None>", true),
                Row("UCS", "World", true),
                Row("Layer snapshot", row != null && row.HasLayerSnapshot ? "Yes" : "No", true),
                Row("Annotation scale", "1:1", true),
                Row("Visual Style", "2D Wireframe", true),
                Row("Background", "<None>", true),
                Row("Live Section", "<None>", true),
                Heading("View"),
                Row("Camera X", "0.00", true),
                Row("Camera Y", "0.00", true),
                Row("Camera Z", "1.00", true),
                Row("Target X", Number(row?.CenterX ?? 0.0), true),
                Row("Target Y", Number(row?.CenterY ?? 0.0), true),
                Row("Height", Number(row?.Height ?? 0.0), !IsNamedChosen),
                Row("Width", Number(row?.Width ?? 0.0), !IsNamedChosen),
                Row("Perspective", "Off", true),
                Row("Twist", Number(row?.Twist ?? 0.0), true),
                Heading("Clipping"),
                Row("Front plane", "0.00", true),
                Row("Back plane", "0.00", true),
                Row("Clipping", "Off", true),
            };

            _properties = new ObservableCollection<ViewPropertyViewModel>(list);
            _name = _selected?.Name ?? string.Empty;
            _height = row == null ? string.Empty : Number(row.Height).Replace(" ", string.Empty);
            _width = row == null ? string.Empty : Number(row.Width).Replace(" ", string.Empty);
            Raise(nameof(Properties));
            Raise(nameof(PropertiesTitle));
            Raise(nameof(IsNamedChosen));
            Raise(nameof(ViewName));
            Raise(nameof(ViewHeight));
            Raise(nameof(ViewWidth));
        }

        private static ViewPropertyViewModel Heading(string label) => new ViewPropertyViewModel(label, string.Empty, true, true);

        private static ViewPropertyViewModel Row(string label, string value, bool readOnly) => new ViewPropertyViewModel(label, value, readOnly, false);

        /// <summary>A number as the board writes it, two places, thousands set apart by a space: 14 400.00.</summary>
        private static string Number(double value) => value.ToString("#,0.00", new NumberFormatInfo { NumberGroupSeparator = " ", NumberDecimalSeparator = "." });
    }

    /// <summary>
    /// AutoCAD's New View / Shot Properties of the View Manager: the view's name, its category, its boundary (Current
    /// display, or Define window, which closes the dialogs for two corners on the command line), Save layer snapshot with
    /// view, UCS World; the 3D settings and Shot Properties greyed. A used name asks whether to redefine the view.
    /// </summary>
    public sealed class NewViewViewModel : ObservableObject
    {
        /// <summary>Makes the dialog over the request of VIEW.</summary>
        /// <param name="request">The request.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public NewViewViewModel(CadViewManagerRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
        }

        /// <summary>Gets the request.</summary>
        public CadViewManagerRequest Request { get; }

        /// <summary>Gets or sets how the window asks whether to redefine the view of a name used; true redefines it.</summary>
        public Func<string, bool> AskRedefine { get; set; }

        /// <summary>Gets or sets the view's name.</summary>
        public string ViewName { get => Request.NewName; set { Request.NewName = value ?? string.Empty; Raise(nameof(ViewName)); } }

        /// <summary>Gets or sets whether the boundary is the display (Current display).</summary>
        public bool CurrentDisplay
        {
            get => Request.NewWindow == null && !_window;
            set
            {
                if (value)
                {
                    _window = false;
                    Request.NewWindow = null;
                    RaiseBoundary();
                }
            }
        }

        /// <summary>Gets or sets whether the boundary is a window picked (Define window).</summary>
        public bool DefineWindow
        {
            get => Request.NewWindow != null || _window;
            set
            {
                if (value)
                {
                    _window = true;
                    RaiseBoundary();
                }
            }
        }

        private bool _window;

        /// <summary>Gets the size of the window picked, as Window 14 400 × 9 000; empty where none is.</summary>
        public string WindowText => Request.NewWindow == null ? string.Empty
            : "Window " + Request.NewWindow.Value.Width.ToString("#,0.##", Spaced) + " × " + Request.NewWindow.Value.Height.ToString("#,0.##", Spaced);

        /// <summary>Gets or sets whether a layer snapshot is saved with the view.</summary>
        public bool SaveLayerSnapshot { get => Request.NewLayerSnapshot; set { Request.NewLayerSnapshot = value; Raise(nameof(SaveLayerSnapshot)); } }

        /// <summary>Gets what the last OK said where it was refused; empty where none was.</summary>
        public string Message => Request.Message ?? string.Empty;

        private static NumberFormatInfo Spaced => new NumberFormatInfo { NumberGroupSeparator = " ", NumberDecimalSeparator = "." };

        /// <summary>Define window's pick: the dialogs closed for the command line to ask two corners.</summary>
        public void Pick()
        {
            _window = true;
            Request.Pick = "NewWindow";
            Request.Action = CadDialogAction.SelectObjects;
        }

        /// <summary>OK: the view saved, a used name redefined where <see cref="AskRedefine"/> says so.</summary>
        /// <returns>Whether it was saved, so that the dialog closes.</returns>
        public bool Ok()
        {
            string name = (Request.NewName ?? string.Empty).Trim();
            bool used = Request.Views.Any(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
            bool redefine = used && AskRedefine != null && AskRedefine(name);

            if (used && !redefine && AskRedefine != null)
            {
                return false;
            }

            if (_window && Request.NewWindow == null)
            {
                Pick();
                return false;
            }

            bool done = Request.New(redefine);
            Raise(nameof(Message));
            return done;
        }

        /// <summary>Cancel: the window picked forgotten.</summary>
        public void Cancel()
        {
            Request.NewWindow = null;
            Request.IsNewViewOpen = false;
        }

        private void RaiseBoundary()
        {
            Raise(nameof(CurrentDisplay));
            Raise(nameof(DefineWindow));
            Raise(nameof(WindowText));
        }
    }
}
