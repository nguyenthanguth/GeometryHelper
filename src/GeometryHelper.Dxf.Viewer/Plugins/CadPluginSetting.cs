using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace GeometryHelper.Dxf.Viewer.Plugins
{
    /// <summary>What kind of value a plugin's setting holds, and so which box the presenter draws for it.</summary>
    public enum CadSettingKind
    {
        /// <summary>A number with a fraction, a <c>double</c>: a text height of 2.5.</summary>
        Number,

        /// <summary>A whole number, an <c>int</c>: a start number of 1.</summary>
        Integer,

        /// <summary>Yes or no, a <c>bool</c>: a check box.</summary>
        Boolean,

        /// <summary>A text, a <c>string</c>: a layer name.</summary>
        Text,

        /// <summary>One of a list, an enumeration: a drop-down of its names.</summary>
        Choice,
    }

    /// <summary>
    /// One setting a plugin declares with <see cref="CadSettingAttribute"/>: its label, kind, default and range, as the
    /// presenter draws them, and its value, read from and written to the plugin's property. A text height declared with
    /// <c>Minimum = 0.1</c> and set to 0.05 is refused, and the plugin keeps the height it had.
    /// </summary>
    public sealed class CadPluginSetting
    {
        private readonly PropertyInfo _property;
        private readonly object _target;

        internal CadPluginSetting(PropertyInfo property, object target, CadSettingAttribute attribute, CadSettingKind kind)
        {
            _property = property;
            _target = target;
            Name = property.Name;
            Label = attribute.Label;
            Description = attribute.Description;
            Kind = kind;
            Minimum = attribute.Minimum;
            Maximum = attribute.Maximum;
            Choices = kind == CadSettingKind.Choice ? Enum.GetNames(property.PropertyType) : Array.Empty<string>();
            Default = Value;
        }

        /// <summary>Gets the name of the property: TextHeight.</summary>
        public string Name { get; }

        /// <summary>Gets the label the presenter shows: "Text height".</summary>
        public string Label { get; }

        /// <summary>Gets what the setting does; null for nothing.</summary>
        public string Description { get; }

        /// <summary>Gets the kind of value it holds.</summary>
        public CadSettingKind Kind { get; }

        /// <summary>Gets the value the property had when the plugin was loaded: a <c>double</c>, an <c>int</c>, a <c>bool</c>, a <c>string</c> or, for a choice, the name of the value.</summary>
        public object Default { get; }

        /// <summary>Gets the least value of a number, inclusive; minus infinity for none.</summary>
        public double Minimum { get; }

        /// <summary>Gets the greatest value of a number, inclusive; infinity for none.</summary>
        public double Maximum { get; }

        /// <summary>Gets the names a choice offers, in the order the enumeration declares them; empty for any other kind.</summary>
        public IReadOnlyList<string> Choices { get; }

        /// <summary>
        /// Gets or sets the value: a <c>double</c>, an <c>int</c>, a <c>bool</c>, a <c>string</c> or, for a choice, the name
        /// of the value. A value of another type is converted as the invariant culture reads it, so "2.5" sets a number.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the value set is null for any kind but a text.</exception>
        /// <exception cref="ArgumentException">Thrown when the value set cannot be read as the setting's kind, or names no choice.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a number set is not finite or lies outside <see cref="Minimum"/> and <see cref="Maximum"/>.</exception>
        public object Value
        {
            get
            {
                object value = _property.GetValue(_target, null);
                return Kind == CadSettingKind.Choice ? value?.ToString() : value;
            }

            set => _property.SetValue(_target, Convert(value), null);
        }

        /// <inheritdoc/>
        public override string ToString() => Label + " = " + System.Convert.ToString(Value, CultureInfo.InvariantCulture);

        /// <summary>The kind of a property's type, or null where a setting cannot have it.</summary>
        internal static CadSettingKind? KindOf(Type type)
        {
            if (type == typeof(double))
            {
                return CadSettingKind.Number;
            }

            if (type == typeof(int))
            {
                return CadSettingKind.Integer;
            }

            if (type == typeof(bool))
            {
                return CadSettingKind.Boolean;
            }

            if (type == typeof(string))
            {
                return CadSettingKind.Text;
            }

            return type.IsEnum ? CadSettingKind.Choice : (CadSettingKind?)null;
        }

        private object Convert(object value)
        {
            if (value == null)
            {
                if (Kind == CadSettingKind.Text)
                {
                    return null;
                }

                throw new ArgumentNullException(nameof(value), "The setting " + Label + " needs a value.");
            }

            switch (Kind)
            {
                case CadSettingKind.Number:
                    return InRange(ToNumber(value));
                case CadSettingKind.Integer:
                    double whole = InRange(ToNumber(value));

                    if (whole != Math.Floor(whole) || whole < int.MinValue || whole > int.MaxValue)
                    {
                        throw new ArgumentException("The setting " + Label + " is a whole number, not " + Text(value) + ".", nameof(value));
                    }

                    return (int)whole;
                case CadSettingKind.Boolean:
                    if (value is bool flag)
                    {
                        return flag;
                    }

                    if (bool.TryParse(Text(value), out flag))
                    {
                        return flag;
                    }

                    throw new ArgumentException("The setting " + Label + " is yes or no, not " + Text(value) + ".", nameof(value));
                case CadSettingKind.Text:
                    return Text(value);
                default:
                    string name = Text(value);

                    foreach (string choice in Choices)
                    {
                        if (string.Equals(choice, name, StringComparison.OrdinalIgnoreCase))
                        {
                            return Enum.Parse(_property.PropertyType, choice);
                        }
                    }

                    throw new ArgumentException("The setting " + Label + " is one of " + string.Join(", ", Choices) + ", not " + name + ".", nameof(value));
            }
        }

        private double ToNumber(object value)
        {
            double number;

            if (value is double d)
            {
                number = d;
            }
            else if (value is int i)
            {
                number = i;
            }
            else if (!double.TryParse(Text(value), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            {
                throw new ArgumentException("The setting " + Label + " is a number, not " + Text(value) + ".", nameof(value));
            }

            return number;
        }

        private double InRange(double number)
        {
            if (double.IsNaN(number) || double.IsInfinity(number) || number < Minimum || number > Maximum)
            {
                throw new ArgumentOutOfRangeException(nameof(Value), number, "The setting " + Label + " lies from " + Text(Minimum) + " to " + Text(Maximum) + ".");
            }

            return number;
        }

        private static string Text(object value) => System.Convert.ToString(value, CultureInfo.InvariantCulture);
    }
}
