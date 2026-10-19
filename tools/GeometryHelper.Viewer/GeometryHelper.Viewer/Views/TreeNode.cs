using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;

namespace GeometryHelper.Viewer.Views
{
    /// <summary>
    /// A line of the scene tree: a layer, or an item on it.
    /// </summary>
    public sealed class TreeNode : INotifyPropertyChanged
    {
        private readonly Action<TreeNode, bool> _checked;
        private bool _isChecked = true;
        private bool _isExpanded = true;
        private bool _isSelected;

        internal TreeNode(string header, Brush swatch, int itemIndex, string layer, Action<TreeNode, bool> onChecked)
        {
            Header = header;
            Swatch = swatch;
            ItemIndex = itemIndex;
            Layer = layer;
            _checked = onChecked;
        }

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the text of the line.
        /// </summary>
        public string Header { get; }

        /// <summary>
        /// Gets the colour swatch of the line.
        /// </summary>
        public Brush Swatch { get; }

        /// <summary>
        /// Gets the index of the item in the scene, or -1 for a layer.
        /// </summary>
        public int ItemIndex { get; }

        /// <summary>
        /// Gets the name of the layer of the line, or of the layer an item is on.
        /// </summary>
        public string Layer { get; }

        /// <summary>
        /// Gets the lines below this one.
        /// </summary>
        public ObservableCollection<TreeNode> Children { get; } = new ObservableCollection<TreeNode>();

        /// <summary>
        /// Gets or sets whether the line is shown; changing it tells the view.
        /// </summary>
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    Raise(nameof(IsChecked));
                    _checked?.Invoke(this, value);
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the line is open.
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                Raise(nameof(IsExpanded));
            }
        }

        /// <summary>
        /// Gets or sets whether the line is selected.
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    Raise(nameof(IsSelected));
                }
            }
        }

        /// <summary>
        /// Sets the check without telling the view, for a change that came from the view.
        /// </summary>
        internal void SetCheckedQuietly(bool value)
        {
            if (_isChecked != value)
            {
                _isChecked = value;
                Raise(nameof(IsChecked));
            }
        }

        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// A row of the properties pane.
    /// </summary>
    public sealed class PropertyRow
    {
        /// <summary>
        /// Initializes a row.
        /// </summary>
        public PropertyRow(string name, string value)
        {
            Name = name;
            Value = value;
        }

        /// <summary>
        /// Gets the name of the property.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets its value as text.
        /// </summary>
        public string Value { get; }
    }
}
