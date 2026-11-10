using System;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// One row of the DRAWINGS list: the header of a folder ("D:\Plans\Workshop A · DXF"), or a file in it with a line of
    /// detail ("DXF · 2 418 entities · 2 layouts") and a dot when it has unsaved changes.
    /// </summary>
    public sealed class FileRowViewModel : ObservableObject
    {
        private bool _isCurrent;
        private string _detail;
        private bool _hasUnsavedChanges;

        /// <summary>Makes a row.</summary>
        /// <param name="isHeader">Whether the row is the header of a folder.</param>
        /// <param name="label">The folder or the name of the file.</param>
        /// <param name="path">The full path of the file; null for a header.</param>
        /// <param name="detail">The line under the name; null for none.</param>
        public FileRowViewModel(bool isHeader, string label, string path, string detail)
        {
            IsHeader = isHeader;
            Label = label ?? string.Empty;
            Path = path;
            _detail = detail;
        }

        /// <summary>Gets whether the row is the header of a folder.</summary>
        public bool IsHeader { get; }

        /// <summary>Gets the folder, or the name of the file.</summary>
        public string Label { get; }

        /// <summary>Gets the full path of the file; null for a header.</summary>
        public string Path { get; }

        /// <summary>Gets the line under the name, such as "DXF · 412 entities"; null for none.</summary>
        public string Detail
        {
            get => _detail;
            internal set
            {
                if (Set(ref _detail, value))
                {
                    Raise(nameof(HasDetail));
                }
            }
        }

        /// <summary>Gets whether there is a line under the name.</summary>
        public bool HasDetail => !string.IsNullOrEmpty(_detail);

        /// <summary>Gets whether the file is the one the canvas shows.</summary>
        public bool IsCurrent
        {
            get => _isCurrent;
            internal set => Set(ref _isCurrent, value);
        }

        /// <summary>Gets whether the file has changes not saved, shown as a blue dot.</summary>
        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            internal set => Set(ref _hasUnsavedChanges, value);
        }

        /// <summary>Gets the short text at the right of the row, as a model drawing's sheet size "A2"; null for none.</summary>
        public string Right { get; internal set; }

        /// <summary>Gets the dot: blue edited, amber out of date, green up to date, none.</summary>
        public CadFileState State
        {
            get => _state;
            internal set => Set(ref _state, value);
        }

        private CadFileState _state;

        /// <summary>Gets whether the file is open, so that its row shows the small x that closes it (CLOSE).</summary>
        public bool IsOpen { get; internal set; }

        /// <summary>Gets whether the row is selected in the list: a click selects it, Ctrl and Shift add to the selection; a double click opens it.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            internal set => Set(ref _isSelected, value);
        }

        private bool _isSelected;

        /// <inheritdoc/>
        public override string ToString() => Label;
    }

    /// <summary>A tab under the canvas: Model, or a layout.</summary>
    public sealed class SpaceTabViewModel : ObservableObject
    {
        private bool _isActive;

        /// <summary>Makes a tab.</summary>
        /// <param name="space">The space it shows.</param>
        public SpaceTabViewModel(CadSpace space)
        {
            Space = space ?? throw new ArgumentNullException(nameof(space));
        }

        /// <summary>Gets the space.</summary>
        public CadSpace Space { get; }

        /// <summary>Gets the name on the tab: Model for the Block Editor's space, as AutoCAD shows the one tab there.</summary>
        public string Name => Space.IsBlockEditor ? "Model" : Space.Name;

        /// <summary>Gets whether the canvas shows this space.</summary>
        public bool IsActive
        {
            get => _isActive;
            internal set => Set(ref _isActive, value);
        }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>A toggle of the status bar, as GRID or OSNAP: a flag for now, which nothing reads yet.</summary>
    public sealed class StatusToggleViewModel : ObservableObject
    {
        private bool _isOn;

        /// <summary>Makes a toggle.</summary>
        /// <param name="name">The name shown, as GRID.</param>
        /// <param name="isOn">Whether it starts on.</param>
        public StatusToggleViewModel(string name, bool isOn)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _isOn = isOn;
            ToggleCommand = new DelegateCommand(_ => IsOn = !IsOn);
        }

        /// <summary>Gets the name shown.</summary>
        public string Name { get; }

        /// <summary>Gets or sets whether it is on.</summary>
        public bool IsOn
        {
            get => _isOn;
            set => Set(ref _isOn, value);
        }

        /// <summary>Gets the command that switches it, as its function key does.</summary>
        public ICommand ToggleCommand { get; }

        /// <inheritdoc/>
        public override string ToString() => Name + (IsOn ? " on" : " off");
    }
}
