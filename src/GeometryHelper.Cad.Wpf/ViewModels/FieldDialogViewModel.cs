using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>A date format of the Field dialog's list: the date as the format writes it now, and the format.</summary>
    internal sealed class FieldDateChoice
    {
        internal FieldDateChoice(string sample, string format)
        {
            Sample = sample;
            Format = format;
        }

        /// <summary>Gets the date as the format writes it now.</summary>
        public string Sample { get; }

        /// <summary>Gets the format, by its letters.</summary>
        public string Format { get; }

        /// <inheritdoc/>
        public override string ToString() => Sample;
    }

    /// <summary>
    /// The logic of the Field dialog of FIELD and of the text editor's Insert Field: the category and the name chosen, the options
    /// the field offers, and the field expression and its preview that come of them, all of the request the dialog answers
    /// (<see cref="CadFieldRequest"/>). The window sets what the user chose and reads what came of it.
    /// </summary>
    internal sealed class FieldDialogViewModel : ObservableObject
    {
        private static readonly string[] PrecisionChoices = { "Current", "0", "1", "2", "3", "4", "5", "6", "7", "8" };
        private static readonly string[] SeparatorChoices = { "None", "Comma", "Space" };
        private string _factor;

        /// <summary>Makes the dialog's logic over its request.</summary>
        /// <param name="request">The request.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        internal FieldDialogViewModel(CadFieldRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            _factor = Request.Factor.ToString("0.######", CultureInfo.InvariantCulture);
            DateChoices = request
                .DateChoices.Select(f => new FieldDateChoice(
                    CadFieldFormat.Date(DateTime.Now, f, CultureInfo.CurrentCulture),
                    f
                ))
                .ToList();
        }

        /// <summary>Gets the request the dialog answers.</summary>
        internal CadFieldRequest Request { get; }

        /// <summary>Gets the categories of the Field category box.</summary>
        public IReadOnlyList<string> Categories => CadFieldRequest.CategoryNames;

        /// <summary>Gets or sets the category listed.</summary>
        public string Category
        {
            get => Request.Category;
            set
            {
                Request.SetCategory(value);
                Changed();
            }
        }

        /// <summary>Gets the field names of the category.</summary>
        public IReadOnlyList<CadFieldName> Names => CadFieldRequest.NamesOf(Request.Category);

        /// <summary>Gets or sets the field name chosen.</summary>
        public CadFieldName SelectedName
        {
            get => Request.Name;
            set
            {
                if (value != null && !ReferenceEquals(value, Request.Name))
                {
                    Request.Choose(value);
                    Changed();
                }
            }
        }

        /// <summary>Gets what the field chosen shows, in a line.</summary>
        public string Description => Request.Name.Description;

        /// <summary>Gets whether the field is a date: its formats are listed.</summary>
        public bool IsDate => Request.Name.Type == CadFieldType.Date;

        /// <summary>Gets whether the field has a list of formats of its own: the case of words, the parts of a file name.</summary>
        public bool HasFormats => Request.Formats.Count > 0;

        /// <summary>Gets whether the field is a number or a property that is one: units, precision, factor, prefix, suffix and separator.</summary>
        public bool IsNumber => Request.IsNumberField;

        /// <summary>Gets whether the field is a property of an object.</summary>
        public bool IsObject => Request.Name.Type == CadFieldType.Object;

        /// <summary>Gets whether the field is an expression, a formula, a DIESEL expression or a system variable by its name.</summary>
        public bool IsExpression =>
            Request.Name.Type == CadFieldType.Formula
            || Request.Name.Type == CadFieldType.Diesel
            || Request.Name.Type == CadFieldType.SystemVariable;

        /// <summary>Gets the label of the expression box.</summary>
        public string ExpressionLabel =>
            Request.Name.Type == CadFieldType.Formula ? "Formula:"
            : Request.Name.Type == CadFieldType.Diesel ? "DIESEL expression:"
            : "System variable name:";

        /// <summary>Gets the dates as each format of the list writes them now.</summary>
        public IReadOnlyList<FieldDateChoice> DateChoices { get; }

        /// <summary>Gets or sets the date format chosen from the list.</summary>
        public FieldDateChoice SelectedDate
        {
            get => DateChoices.FirstOrDefault(d => d.Format == Request.DateFormat);
            set
            {
                if (value != null)
                {
                    DateFormat = value.Format;
                }
            }
        }

        /// <summary>Gets or sets the date format typed, by its letters: dd/MM/yyyy.</summary>
        public string DateFormat
        {
            get => Request.DateFormat;
            set
            {
                Request.DateFormat = value ?? string.Empty;
                Raise(nameof(DateFormat));
                Raise(nameof(SelectedDate));
                Updated();
            }
        }

        /// <summary>Gets the formats listed for the field.</summary>
        public IReadOnlyList<CadFieldChoice> Formats => Request.Formats;

        /// <summary>Gets or sets the format chosen from the list.</summary>
        public CadFieldChoice SelectedFormat
        {
            get => Request.Formats.Count > Request.FormatIndex ? Request.Formats[Request.FormatIndex] : null;
            set
            {
                int at = value == null ? -1 : Request.Formats.ToList().IndexOf(value);

                if (at >= 0)
                {
                    Request.FormatIndex = at;
                    Updated();
                }
            }
        }

        /// <summary>Gets the units listed for a number.</summary>
        public IReadOnlyList<CadFieldChoice> Units => Request.Units;

        /// <summary>Gets or sets the units chosen.</summary>
        public CadFieldChoice SelectedUnits
        {
            get => Request.Units.Count > Request.UnitsIndex ? Request.Units[Request.UnitsIndex] : null;
            set
            {
                int at = value == null ? -1 : Request.Units.ToList().IndexOf(value);

                if (at >= 0)
                {
                    Request.UnitsIndex = at;
                    Updated();
                }
            }
        }

        /// <summary>Gets the precisions listed: the drawing's, then 0 to 8 places.</summary>
        public IReadOnlyList<string> Precisions => PrecisionChoices;

        /// <summary>Gets or sets the precision chosen.</summary>
        public string Precision
        {
            get =>
                Request.Precision < 0 ? PrecisionChoices[0] : Request.Precision.ToString(CultureInfo.InvariantCulture);
            set
            {
                Request.Precision = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int places)
                    ? places
                    : -1;
                Raise(nameof(Precision));
                Updated();
            }
        }

        /// <summary>Gets or sets the factor typed a number is multiplied by; a text that is no number leaves the factor as it was.</summary>
        public string FactorText
        {
            get => _factor;
            set
            {
                _factor = value ?? string.Empty;

                if (
                    double.TryParse(_factor, NumberStyles.Float, CultureInfo.InvariantCulture, out double factor)
                    && factor != 0.0
                )
                {
                    Request.Factor = factor;
                    Updated();
                }

                Raise(nameof(FactorText));
            }
        }

        /// <summary>Gets or sets the words before a number.</summary>
        public string Prefix
        {
            get => Request.Prefix;
            set
            {
                Request.Prefix = value ?? string.Empty;
                Raise(nameof(Prefix));
                Updated();
            }
        }

        /// <summary>Gets or sets the words after a number.</summary>
        public string Suffix
        {
            get => Request.Suffix;
            set
            {
                Request.Suffix = value ?? string.Empty;
                Raise(nameof(Suffix));
                Updated();
            }
        }

        /// <summary>Gets the thousands separators listed.</summary>
        public IReadOnlyList<string> Separators => SeparatorChoices;

        /// <summary>Gets or sets the thousands separator chosen.</summary>
        public string Separator
        {
            get =>
                Request.Thousands == ',' ? SeparatorChoices[1]
                : Request.Thousands == ' ' ? SeparatorChoices[2]
                : SeparatorChoices[0];
            set
            {
                Request.Thousands =
                    value == SeparatorChoices[1] ? ','
                    : value == SeparatorChoices[2] ? ' '
                    : '\0';
                Raise(nameof(Separator));
                Updated();
            }
        }

        /// <summary>Gets what object an Object field reads: its class and handle, or that none is picked.</summary>
        public string ObjectText =>
            Request.ObjectHandle == null ? "No object selected"
            : Request.ObjectKind == null ? "Object " + Request.ObjectHandle + " is not in the drawing"
            : Request.ObjectKind + " " + Request.ObjectHandle;

        /// <summary>Gets the properties of the object picked.</summary>
        public IReadOnlyList<CadFieldProperty> PropertyChoices => Request.Properties;

        /// <summary>Gets or sets the property chosen.</summary>
        public CadFieldProperty SelectedProperty
        {
            get => Request.Property;
            set
            {
                if (value != null)
                {
                    Request.ChooseProperty(value.Name);
                    Changed();
                }
            }
        }

        /// <summary>Gets or sets the expression of a formula or a DIESEL expression, or the name of a system variable.</summary>
        public string Expression
        {
            get => Request.Expression;
            set
            {
                Request.Expression = value ?? string.Empty;
                Raise(nameof(Expression));
                Updated();
            }
        }

        /// <summary>Gets the field expression the dialog holds, in its markers.</summary>
        public string Code => Request.Code;

        /// <summary>Gets the words the field shows now.</summary>
        public string Preview => Request.Preview;

        /// <summary>Gets whether the dialog holds a field to give: OK is enabled.</summary>
        public bool CanOk => Request.Code.Length > 0;

        /// <summary>Closes the dialog with an action: the request is answered with it.</summary>
        /// <param name="action">How it closed.</param>
        internal void Close(CadFieldAction action) => Request.Action = action;

        /// <summary>Everything the field chosen shows has changed: its options, its expression and its preview.</summary>
        private void Changed()
        {
            foreach (
                string name in new[]
                {
                    nameof(Category),
                    nameof(Names),
                    nameof(SelectedName),
                    nameof(Description),
                    nameof(IsDate),
                    nameof(HasFormats),
                    nameof(IsNumber),
                    nameof(IsObject),
                    nameof(IsExpression),
                    nameof(ExpressionLabel),
                    nameof(DateFormat),
                    nameof(SelectedDate),
                    nameof(Formats),
                    nameof(SelectedFormat),
                    nameof(Units),
                    nameof(SelectedUnits),
                    nameof(ObjectText),
                    nameof(PropertyChoices),
                    nameof(SelectedProperty),
                    nameof(Expression),
                }
            )
            {
                Raise(name);
            }

            Updated();
        }

        /// <summary>The expression and the preview changed.</summary>
        private void Updated()
        {
            Raise(nameof(Code));
            Raise(nameof(Preview));
            Raise(nameof(CanOk));
        }
    }
}
