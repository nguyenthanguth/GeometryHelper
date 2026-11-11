using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// One row of the Properties panel: a label and a value that, unless read-only, is edited in place. Setting
    /// <see cref="Value"/> commits it through <see cref="CadPropertyEditor"/> as one undo step; a value refused is said on the
    /// command line and the row shows the value it had.
    /// </summary>
    public sealed class PropertyRowViewModel : ObservableObject
    {
        private readonly Func<PropertyRowViewModel, string, bool> _commit;
        private string _value;

        internal PropertyRowViewModel(
            CadPropertyRow row,
            IReadOnlyList<string> options,
            Func<PropertyRowViewModel, string, bool> commit
        )
        {
            Label = row.Label;
            _value = row.Value;
            IsReadOnly = row.IsReadOnly;

            // The drop-down shows the value the row has, one of its list or not.
            if (options != null && !System.Linq.Enumerable.Contains(options, _value))
            {
                var all = new List<string> { _value };
                all.AddRange(options);
                options = all;
            }

            Options = options;
            _commit = commit;
        }

        /// <summary>Gets the label, as Layer or Start X.</summary>
        public string Label { get; }

        /// <summary>Gets or sets the value as text; setting a new one commits it, and a refused one leaves the old value.</summary>
        public string Value
        {
            get => _value;
            set
            {
                string typed = value ?? string.Empty;

                if (IsReadOnly || string.Equals(typed, _value, StringComparison.Ordinal))
                {
                    return;
                }

                string old = _value;
                _value = typed;

                if (!_commit(this, typed))
                {
                    _value = old;
                }

                Raise(nameof(Value));
                Raise(nameof(Varies));
            }
        }

        /// <summary>Gets whether the value cannot be edited, drawn grey.</summary>
        public bool IsReadOnly { get; }

        /// <summary>Gets whether the selected entities differ in this property (*Varies*), drawn grey and slanted.</summary>
        public bool Varies => _value == CadPropertyRow.VariesText;

        /// <summary>Gets the values the drop-down offers, as the layers for Layer; null for a row typed into.</summary>
        public IReadOnlyList<string> Options { get; }

        /// <summary>Gets whether the row is an editable drop-down.</summary>
        public bool HasOptions => !IsReadOnly && Options != null;

        /// <summary>Gets whether the row is a box typed into.</summary>
        public bool IsTyped => !IsReadOnly && Options == null;

        /// <inheritdoc/>
        public override string ToString() => Label + " = " + _value;
    }

    /// <summary>A titled group of rows of the Properties panel, such as General or Geometry.</summary>
    public sealed class PropertyGroupViewModel
    {
        internal PropertyGroupViewModel(string title, IReadOnlyList<PropertyRowViewModel> rows)
        {
            Title = title;
            Rows = rows;
        }

        /// <summary>Gets the title.</summary>
        public string Title { get; }

        /// <summary>Gets the rows.</summary>
        public IReadOnlyList<PropertyRowViewModel> Rows { get; }

        /// <inheritdoc/>
        public override string ToString() => Title;
    }
}
