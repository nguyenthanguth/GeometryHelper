using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>What a field of the New, Modify and Override dialogs of dimension styles is: a group's title, a number, an angle, a box to tick, a list, a text.</summary>
    public enum DimFieldKind
    {
        /// <summary>The title of a group of fields, as "Dimension lines".</summary>
        Header,

        /// <summary>A number, typed.</summary>
        Number,

        /// <summary>An angle in degrees, typed; kept in radians.</summary>
        Angle,

        /// <summary>A box ticked or not.</summary>
        Flag,

        /// <summary>One of a list.</summary>
        Choice,

        /// <summary>A text, typed.</summary>
        Text,
    }

    /// <summary>One item of a list field: its words and the value it stands for.</summary>
    public sealed class DimChoice
    {
        internal DimChoice(string label, object value)
        {
            Label = label;
            Value = value;
        }

        /// <summary>Gets the words, as "Closed filled".</summary>
        public string Label { get; }

        /// <summary>Gets the value it sets.</summary>
        public object Value { get; }

        /// <inheritdoc/>
        public override string ToString() => Label;
    }

    /// <summary>
    /// A field of the dialogs of a dimension style, as the design lays them out: its label with the dimension variable it sets
    /// beside it (as DIMASZ), and its value read from and written to the dialog's working values, a number typed as text; greyed
    /// where the values make it mean nothing, as Length is while fixed length extension lines are off.
    /// </summary>
    public sealed class DimFieldViewModel : ObservableObject
    {
        private readonly Func<object> _get;
        private readonly Action<object> _set;
        private readonly Action _changed;
        private readonly Func<bool> _enabled;
        private string _text;

        internal DimFieldViewModel(
            DimFieldKind kind,
            string label,
            string variable,
            Func<object> get,
            Action<object> set,
            IReadOnlyList<DimChoice> choices,
            Action changed,
            Func<bool> enabled = null,
            string shown = null
        )
        {
            Kind = kind;
            Label = label;
            Variable = variable;
            VariableText = shown ?? variable;
            _get = get;
            _set = set;
            _changed = changed;
            _enabled = enabled;
            Choices = choices ?? Array.Empty<DimChoice>();
            _text = TextOf(get?.Invoke());
        }

        /// <summary>Gets what the field is.</summary>
        public DimFieldKind Kind { get; }

        /// <summary>Gets the label, as "Arrow size:".</summary>
        public string Label { get; }

        /// <summary>Gets the variable it sets, as DIMASZ; empty for a group's title.</summary>
        public string Variable { get; }

        /// <summary>Gets the variables shown beside the label: the variable, or the two a field sets, as "DIMTIH · DIMTOH".</summary>
        public string VariableText { get; }

        /// <summary>Gets whether the field can be changed: false where the values make it mean nothing, as the design greys it.</summary>
        public bool IsEnabled => _enabled == null || _enabled();

        /// <summary>Gets whether the "…" of the Text style field is shown, which opens the Text Style dialog.</summary>
        public bool HasTextStyleButton => Variable == "DIMTXSTY";

        /// <summary>
        /// Gets the swatch of a colour field as 0xRRGGBB: the colour chosen, white for ByBlock and ByLayer as the design draws
        /// them; null for a field of no colour, or a fill of none.
        /// </summary>
        public int? SwatchRgb =>
            _get?.Invoke() is DxfColor colour
                ? (colour.IsByBlock || colour.IsByLayer ? 0xFFFFFF : colour.ColorValue & 0xFFFFFF)
                : (int?)null;

        /// <summary>Gets the items of a list field.</summary>
        public IReadOnlyList<DimChoice> Choices { get; private set; }

        /// <summary>Gets or sets the text of a number, an angle or a text field, as typed; written when it reads.</summary>
        public string Text
        {
            get => _text;
            set
            {
                if (!Set(ref _text, value ?? string.Empty))
                {
                    return;
                }

                Raise(nameof(IsValid));

                if (Kind == DimFieldKind.Text)
                {
                    _set(_text);
                }
                else if (
                    double.TryParse(_text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                    && !double.IsNaN(number)
                    && !double.IsInfinity(number)
                )
                {
                    _set(Kind == DimFieldKind.Angle ? number * Math.PI / 180.0 : number);
                }
                else
                {
                    return;
                }

                _changed();
            }
        }

        /// <summary>Gets whether the text typed reads as a number, for a number or an angle field; true for the others.</summary>
        public bool IsValid =>
            (Kind != DimFieldKind.Number && Kind != DimFieldKind.Angle)
            || double.TryParse((_text ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _);

        /// <summary>Gets or sets whether a box field is ticked.</summary>
        public bool IsChecked
        {
            get => _get?.Invoke() is bool on && on;
            set
            {
                _set(value);
                Raise(nameof(IsChecked));
                _changed();
            }
        }

        /// <summary>Gets or sets the item of a list field: that of the value the field reads, matched by its text.</summary>
        public DimChoice SelectedChoice
        {
            get
            {
                string now = CadDimStyleEditor.Text(_get?.Invoke());
                return Choices.FirstOrDefault(c =>
                    string.Equals(CadDimStyleEditor.Text(c.Value), now, StringComparison.OrdinalIgnoreCase)
                );
            }
            set
            {
                if (value == null)
                {
                    return;
                }

                _set(value.Value);
                Raise(nameof(SelectedChoice));
                _changed();
            }
        }

        /// <summary>Reads the value again, after another field set it.</summary>
        internal void Refresh()
        {
            string now = TextOf(_get?.Invoke());

            // A number being typed ("2." on its way to 2.5) is kept while it reads as the value it set.
            bool same =
                Kind == DimFieldKind.Text
                    ? _text == now
                    : double.TryParse(
                        (_text ?? string.Empty).Trim(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double typed
                    )
                        && TextOf(Kind == DimFieldKind.Angle ? typed * Math.PI / 180.0 : typed) == now;

            if (!same)
            {
                _text = now;
                Raise(nameof(Text));
                Raise(nameof(IsValid));
            }

            Raise(nameof(IsChecked));
            Raise(nameof(SelectedChoice));
            Raise(nameof(IsEnabled));
            Raise(nameof(SwatchRgb));
        }

        /// <summary>Gives the list new items, the drawing's text styles after the Text Style dialog.</summary>
        internal void SetChoices(IReadOnlyList<DimChoice> choices)
        {
            Choices = choices;
            Raise(nameof(Choices));
            Raise(nameof(SelectedChoice));
        }

        private string TextOf(object value)
        {
            switch (value)
            {
                case double number when Kind == DimFieldKind.Angle:
                    return (number * 180.0 / Math.PI).ToString("0.####", CultureInfo.InvariantCulture);
                case double number:
                    return number.ToString("0.0###", CultureInfo.InvariantCulture);
                default:
                    return value as string ?? CadDimStyleEditor.Text(value);
            }
        }

        /// <inheritdoc/>
        public override string ToString() =>
            Label
            + " "
            + (
                Kind == DimFieldKind.Flag ? IsChecked.ToString()
                : Kind == DimFieldKind.Choice ? SelectedChoice?.Label
                : _text
            );
    }
}
