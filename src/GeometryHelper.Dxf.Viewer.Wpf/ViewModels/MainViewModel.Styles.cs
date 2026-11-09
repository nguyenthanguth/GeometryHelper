using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The style forms (C10): STYLE and DIMSTYLE asking the window for the Text Style dialog and the Dimension Style Manager,
    /// and the style boxes of the Annotate tab: the current text style (<c>$TEXTSTYLE</c>),
    /// the text height TEXT and MTEXT offer, the current dimension style (<c>$DIMSTYLE</c>) and the current multileader style
    /// (CMLEADERSTYLE), the text and dimension lists ending in "Text style…" and "Dimension style…", which open the dialog.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>The last item of the text style box, which opens the Text Style dialog.</summary>
        public const string ManageTextStyles = "Text style…";

        /// <summary>The last item of the dimension style box, which opens the Dimension Style Manager.</summary>
        public const string ManageDimensionStyles = "Dimension style…";

        /// <summary>The last item of the multileader style box, which opens the Multileader Style Manager.</summary>
        public const string ManageMLeaderStyles = "Multileader style…";

        /// <summary>
        /// Raised by STYLE (ST), DIMSTYLE (D, DST, DDIM), TABLESTYLE (TS) and MLEADERSTYLE (MLS), and by the boxes' last items:
        /// the window shows the dialog named, "STYLE", "DIMSTYLE", "TABLESTYLE" or "MLEADERSTYLE".
        /// </summary>
        public event EventHandler<string> StyleDialogRequested;

        /// <summary>Gets the text styles of the drawing for the text style box, then <see cref="ManageTextStyles"/>; empty with no drawing.</summary>
        public IReadOnlyList<string> TextStyleNames
            => _current == null ? Array.Empty<string>() : CadTextStyleEditor.Styles(_current.Document.Database).Select(s => s.Name).Concat(new[] { ManageTextStyles }).ToList();

        /// <summary>
        /// Gets or sets the current text style, as the text style box shows and sets it (one undo step); set to
        /// <see cref="ManageTextStyles"/>, the Text Style dialog is asked for and the box keeps the style it had.
        /// </summary>
        public string CurrentTextStyle
        {
            get => _current == null ? null : CadTextStyleEditor.Current(_current.Document.Database);
            set
            {
                if (_current == null || value == null || string.Equals(value, CurrentTextStyle, StringComparison.Ordinal))
                {
                    return;
                }

                if (value == ManageTextStyles)
                {
                    RequestStyleDialog("STYLE");
                }
                else
                {
                    SayRefused(CadTextStyleEditor.SetCurrent(_current.Document, value));
                }

                RaiseStyles();
            }
        }

        /// <summary>
        /// Gets or sets the text height the box shows, the one TEXT and MTEXT offer next ("text.height" of the command line);
        /// a text that is not a number above nought is not taken.
        /// </summary>
        public string TextHeight
        {
            get => _current == null ? string.Empty
                : (_current.Session.Remembered.TryGetValue("text.height", out double height) ? height : SafeTextSize(_current.Document.Database)).ToString("0.####", CultureInfo.InvariantCulture);
            set
            {
                if (_current != null && double.TryParse((value ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double height) && height > 0.0 && !double.IsInfinity(height))
                {
                    _current.Session.Remembered["text.height"] = height;
                }

                Raise(nameof(TextHeight));
            }
        }

        /// <summary>Gets the dimension styles of the drawing for the dimension style box, then <see cref="ManageDimensionStyles"/>.</summary>
        public IReadOnlyList<string> DimStyleNames
            => _current == null ? Array.Empty<string>() : CadDimStyleEditor.Styles(_current.Document.Database).Select(s => s.Name).Concat(new[] { ManageDimensionStyles }).ToList();

        /// <summary>
        /// Gets or sets the current dimension style, as the dimension style box shows and sets it (one undo step); set to
        /// <see cref="ManageDimensionStyles"/>, the Dimension Style Manager is asked for.
        /// </summary>
        public string CurrentDimStyle
        {
            get => _current == null ? null : CadDimStyleEditor.Current(_current.Document.Database);
            set
            {
                if (_current == null || value == null || string.Equals(value, CurrentDimStyle, StringComparison.Ordinal))
                {
                    return;
                }

                if (value == ManageDimensionStyles)
                {
                    RequestStyleDialog("DIMSTYLE");
                }
                else
                {
                    SayRefused(CadDimStyleEditor.SetCurrent(_current.Document, value));
                }

                RaiseStyles();
            }
        }

        /// <summary>Gets the multileader styles of the drawing (ACAD_MLEADERSTYLE), in the order of their names; empty where it has none.</summary>
        public IReadOnlyList<string> MLeaderStyleNames
        {
            get
            {
                DxfDBDictionary styles = null;

                try
                {
                    styles = _current?.Document.Database.MLeaderStyleDictionary;
                }
                catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException)
                {
                    styles = null;
                }

                return styles == null ? Array.Empty<string>() : styles.Select(e => e.Key).OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ThenBy(k => k, StringComparer.Ordinal).ToList();
            }
        }

        /// <summary>Gets whether the drawing has multileader styles, so that their box can be opened.</summary>
        public bool HasMLeaderStyles => MLeaderStyleNames.Count > 0;

        /// <summary>Gets the items of the text style box: the styles, the current one ticked, then "Text style…" under a line.</summary>
        public IReadOnlyList<StyleBoxItemViewModel> TextStyleItems => Items(TextStyleNames, CurrentTextStyle, ManageTextStyles);

        /// <summary>Gets the items of the dimension style box: the styles, the current one ticked, a model drawing's marked "model", then "Dimension style…".</summary>
        public IReadOnlyList<StyleBoxItemViewModel> DimStyleItems => Items(DimStyleNames, CurrentDimStyle, ManageDimensionStyles);

        /// <summary>Gets the items of the multileader style box: the styles, the current one ticked, then "Multileader style…" under a line.</summary>
        public IReadOnlyList<StyleBoxItemViewModel> MLeaderStyleItems
            => _current == null ? Array.Empty<StyleBoxItemViewModel>() : Items(MLeaderStyleNames.Concat(new[] { ManageMLeaderStyles }).ToList(), CurrentMLeaderStyle, ManageMLeaderStyles);

        /// <summary>
        /// Gets or sets the current multileader style (CMLEADERSTYLE), as the multileader style box shows and sets it (one undo
        /// step); set to <see cref="ManageMLeaderStyles"/>, the Multileader Style Manager is asked for.
        /// </summary>
        public string CurrentMLeaderStyle
        {
            get
            {
                try
                {
                    return _current?.Document.Database.MLeaderstyle;
                }
                catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException)
                {
                    return null;
                }
            }

            set
            {
                if (_current != null && value == ManageMLeaderStyles)
                {
                    RequestStyleDialog("MLEADERSTYLE");
                }
                else if (_current != null && value != null && MLeaderStyleNames.Contains(value) && !string.Equals(value, CurrentMLeaderStyle, StringComparison.Ordinal))
                {
                    SayRefused(CadMLeaderStyleEditor.SetCurrent(_current.Document, value));
                }

                RaiseStyles();
            }
        }

        /// <summary>Makes the Text Style dialog's view model over the drawing shown.</summary>
        /// <returns>The view model; null with no drawing.</returns>
        public TextStyleViewModel CreateTextStyle() => _current == null ? null : new TextStyleViewModel(_current.Document);

        /// <summary>Makes the Dimension Style Manager's view model over the drawing shown.</summary>
        /// <returns>The view model; null with no drawing or one of no dimension style.</returns>
        public DimStyleManagerViewModel CreateDimStyleManager()
            => _current == null || CadDimStyleEditor.Current(_current.Document.Database) == null ? null : new DimStyleManagerViewModel(_current.Document);

        /// <summary>Makes the Multileader Style Manager's view model over the drawing shown, Standard added to a drawing of no multileader style.</summary>
        /// <returns>The view model; null with no drawing.</returns>
        public MLeaderStyleManagerViewModel CreateMLeaderStyleManager() => _current == null ? null : new MLeaderStyleManagerViewModel(_current.Document);

        /// <summary>Asks the window for a style dialog; with no window, says so on the command line.</summary>
        /// <param name="name">STYLE, DIMSTYLE, TABLESTYLE or MLEADERSTYLE.</param>
        /// <returns>Whether the window was asked.</returns>
        internal bool RequestStyleDialog(string name)
        {
            if (StyleDialogRequested == null)
            {
                Say((name == "STYLE" ? "The Text Style dialog" : name == "TABLESTYLE" ? "The Table Style dialog" : name == "MLEADERSTYLE" ? "The Multileader Style Manager" : "The Dimension Style Manager") + " is shown by the window.");
                return false;
            }

            StyleDialogRequested(this, name);
            return true;
        }

        /// <summary>STYLE or DIMSTYLE asked for its dialog: the window shows it once the command has ended; with no window, STYLE runs -STYLE's prompts.</summary>
        private void OnStyleRequest(CadUiRequestEventArgs e)
        {
            if (StyleDialogRequested == null)
            {
                return;
            }

            string name = e.Name;
            _afterCommand = () => RequestStyleDialog(name);
            e.Handled = true;
        }

        /// <summary>The style boxes and the image frame box read again: the drawing shown changed, or another is shown.</summary>
        private void RaiseStyles()
        {
            Raise(nameof(ImageFrameItems));
            Raise(nameof(CurrentImageFrame));
            Raise(nameof(TextStyleNames));
            Raise(nameof(TextStyleItems));
            Raise(nameof(CurrentTextStyle));
            Raise(nameof(TextHeight));
            Raise(nameof(DimStyleNames));
            Raise(nameof(DimStyleItems));
            Raise(nameof(CurrentDimStyle));
            Raise(nameof(MLeaderStyleNames));
            Raise(nameof(MLeaderStyleItems));
            Raise(nameof(HasMLeaderStyles));
            Raise(nameof(CurrentMLeaderStyle));
        }

        /// <summary>The items of a style box from its names, the dialog's item last where the names end in it.</summary>
        private static IReadOnlyList<StyleBoxItemViewModel> Items(IReadOnlyList<string> names, string current, string dialog)
            => names.Select(n => n == dialog
                    ? new StyleBoxItemViewModel(n, false, string.Empty, true)
                    : new StyleBoxItemViewModel(n, string.Equals(n, current, StringComparison.OrdinalIgnoreCase), CadDimStyleEditor.IsModelStyle(n) ? "model" : string.Empty, false))
                .ToList();

        private void SayRefused(CadEditResult result)
        {
            if (!result.Succeeded)
            {
                Say(result.Message);
            }
        }

        private static double SafeTextSize(DxfDatabase database)
        {
            try
            {
                return database.Textsize;
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is FormatException)
            {
                return 2.5;
            }
        }
    }

    /// <summary>
    /// An item of a style box of the Annotate tab: a style's name, ticked where it is current, with a hint after it ("model"
    /// for a style a model drawing makes); or the last item, which opens the style dialog, under a line.
    /// </summary>
    public sealed class StyleBoxItemViewModel
    {
        internal StyleBoxItemViewModel(string name, bool isCurrent, string hint, bool isDialog)
        {
            Name = name;
            IsCurrent = isCurrent;
            Hint = hint;
            IsDialog = isDialog;
        }

        /// <summary>Gets the name: the style's, or the dialog item's words, as "Dimension style…".</summary>
        public string Name { get; }

        /// <summary>Gets whether the style is the current one, ticked.</summary>
        public bool IsCurrent { get; }

        /// <summary>Gets the hint after the name: "model" for GH_MODEL_10 and its like; empty for the others.</summary>
        public string Hint { get; }

        /// <summary>Gets whether the item opens the style dialog rather than naming a style.</summary>
        public bool IsDialog { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }
}
