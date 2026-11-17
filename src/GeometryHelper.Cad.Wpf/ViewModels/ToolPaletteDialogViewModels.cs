using System;
using System.Globalization;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// A one-line question of the tool palettes: the name of a new or renamed palette or tool, or the words of a line of text.
    /// OK waits for a name that is not blank.
    /// </summary>
    public sealed class ToolPaletteNameViewModel : ObservableObject
    {
        private string _name;

        /// <summary>Makes the dialog.</summary>
        /// <param name="title">The title: "Rename Palette", "New Palette", "Add Text".</param>
        /// <param name="label">The words over the box: "Palette name:".</param>
        /// <param name="name">The name the box starts with.</param>
        /// <exception cref="ArgumentNullException">Thrown when the title or the label is null.</exception>
        public ToolPaletteNameViewModel(string title, string label, string name)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Label = label ?? throw new ArgumentNullException(nameof(label));
            _name = name ?? string.Empty;
        }

        /// <summary>Gets the title.</summary>
        public string Title { get; }

        /// <summary>Gets the words over the box.</summary>
        public string Label { get; }

        /// <summary>Gets or sets the name as typed.</summary>
        public string Name
        {
            get => _name;
            set
            {
                if (Set(ref _name, value ?? string.Empty))
                {
                    Raise(nameof(CanOk));
                }
            }
        }

        /// <summary>Gets whether OK can be pressed: the name is not blank.</summary>
        public bool CanOk => !string.IsNullOrWhiteSpace(_name);
    }

    /// <summary>
    /// The View Options dialog of a tool palette: how its tools are listed (icon only, icon with text, or list view), the size
    /// of the icons, and whether the choice is for the palette shown or for all of them.
    /// </summary>
    public sealed class ToolPaletteViewOptionsViewModel : ObservableObject
    {
        private int _iconSize;

        /// <summary>Makes the dialog on the view a palette has.</summary>
        /// <param name="style">The style it lists its tools in.</param>
        /// <param name="iconSize">The size of its icons, in pixels.</param>
        public ToolPaletteViewOptionsViewModel(CadToolViewStyle style, int iconSize)
        {
            Style = style;
            _iconSize = Math.Max(CadToolPalette.MinIconSize, Math.Min(iconSize, CadToolPalette.MaxIconSize));
        }

        /// <summary>Gets or sets how the tools are listed.</summary>
        public CadToolViewStyle Style { get; set; }

        /// <summary>Gets or sets whether the tools are listed as icons alone.</summary>
        public bool IsIconOnly
        {
            get => Style == CadToolViewStyle.IconOnly;
            set => Choose(value, CadToolViewStyle.IconOnly);
        }

        /// <summary>Gets or sets whether the tools are listed as icons with their names under them.</summary>
        public bool IsIconWithText
        {
            get => Style == CadToolViewStyle.IconWithText;
            set => Choose(value, CadToolViewStyle.IconWithText);
        }

        /// <summary>Gets or sets whether the tools are listed as rows.</summary>
        public bool IsList
        {
            get => Style == CadToolViewStyle.List;
            set => Choose(value, CadToolViewStyle.List);
        }

        /// <summary>Gets or sets the size of the icons, in pixels, 16 to 96; one outside is kept inside.</summary>
        public int IconSize
        {
            get => _iconSize;
            set
            {
                if (
                    Set(
                        ref _iconSize,
                        Math.Max(CadToolPalette.MinIconSize, Math.Min(value, CadToolPalette.MaxIconSize))
                    )
                )
                {
                    Raise(nameof(IconSizeText));
                }
            }
        }

        /// <summary>Gets the icon size as the dialog shows it: <c>32 px</c>.</summary>
        public string IconSizeText => _iconSize.ToString(CultureInfo.InvariantCulture) + " px";

        /// <summary>Gets or sets whether the choice is for every palette, not only the one shown.</summary>
        public bool ApplyToAll { get; set; }

        private void Choose(bool chosen, CadToolViewStyle style)
        {
            if (!chosen)
            {
                return;
            }

            Style = style;
            Raise(nameof(IsIconOnly));
            Raise(nameof(IsIconWithText));
            Raise(nameof(IsList));
        }
    }
}
