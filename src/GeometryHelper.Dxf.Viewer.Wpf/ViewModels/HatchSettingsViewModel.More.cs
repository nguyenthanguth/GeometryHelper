using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The options of the Hatch dialog that -HATCH asks typed: a gradient of one colour with its Shade to Tint slider, the
    /// hatch colour, background colour, layer and transparency a new hatch takes, the hatch origin (Use current origin,
    /// Specified origin, Default to boundary extents and its corner, Store as default origin) and the gap tolerance.
    /// </summary>
    public sealed partial class HatchSettingsViewModel
    {
        private const string UseCurrent = "Use Current";

        /// <summary>The colours of the colour boxes after Use Current or None: ByLayer, ByBlock and the seven named ones.</summary>
        private static readonly string[] NamedColors =
        {
            "ByLayer",
            "ByBlock",
            "Red",
            "Yellow",
            "Green",
            "Cyan",
            "Blue",
            "Magenta",
            "White",
        };

        private static readonly string[] CornerNames =
        {
            "Bottom left",
            "Bottom right",
            "Top right",
            "Top left",
            "Center",
        };

        private static readonly string[] TransparencyNames = { UseCurrent, "ByLayer", "ByBlock", "Transparency value" };

        private bool? _specified;
        private bool _storeAsDefault;
        private double _storedX = double.NaN;
        private double _storedY;
        private string _transparencyValue;
        private string _gapTolerance;

        /// <summary>Gets or sets whether the gradient runs from one colour to its shade or tint, rather than between two colours.</summary>
        public bool IsOneColor
        {
            get => Settings.GradientOneColor;
            set
            {
                if (Settings.GradientOneColor != value)
                {
                    Settings.GradientOneColor = value;
                    Raise(nameof(IsOneColor));
                    Raise(nameof(IsTwoColor));
                    Refresh();
                }
            }
        }

        /// <summary>Gets or sets whether the gradient runs between two colours.</summary>
        public bool IsTwoColor
        {
            get => !Settings.GradientOneColor;
            set => IsOneColor = !value;
        }

        /// <summary>Gets or sets the Shade to Tint slider of a gradient of one colour, 0 black, 0.5 the colour, 1 white (GFCLRLUM).</summary>
        public double Tint
        {
            get => Settings.GradientTint;
            set
            {
                double tint = Math.Max(0.0, Math.Min(1.0, value));

                if (!tint.Equals(Settings.GradientTint))
                {
                    Settings.GradientTint = tint;
                    Raise(nameof(Tint));
                    Refresh();
                }
            }
        }

        /// <summary>Gets the hatch colours offered: Use Current, ByLayer, ByBlock and the seven named ones.</summary>
        public IReadOnlyList<string> HatchColors { get; } = new[] { UseCurrent }.Concat(NamedColors).ToList();

        /// <summary>Gets the background colours offered: None, then the named ones.</summary>
        public IReadOnlyList<string> BackgroundColors { get; } = new[] { "None" }.Concat(NamedColors).ToList();

        /// <summary>Gets or sets the colour a new hatch is drawn in; Use Current for the current colour (HPCOLOR ".").</summary>
        public string HatchColor
        {
            get => Settings.Color == null ? UseCurrent : ColorText(Settings.Color);
            set
            {
                Settings.Color = value == null || value == UseCurrent ? null : ColorOf(value);
                Raise(nameof(HatchColor));
            }
        }

        /// <summary>Gets or sets the colour behind the lines of the pattern; None for none (HPBACKGROUNDCOLOR ".").</summary>
        public string BackgroundColor
        {
            get => Settings.BackgroundColor == null ? "None" : ColorText(Settings.BackgroundColor);
            set
            {
                Settings.BackgroundColor = value == null || value == "None" ? null : ColorOf(value);
                Raise(nameof(BackgroundColor));
                Refresh();
            }
        }

        /// <summary>Gets the layers offered: Use Current, then the drawing's by name.</summary>
        public IReadOnlyList<string> Layers =>
            new[] { UseCurrent }
                .Concat(
                    _document
                        .Database.LayerTable.Select(l => l.Name)
                        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(n => n, StringComparer.Ordinal)
                )
                .ToList();

        /// <summary>Gets or sets the layer a new hatch is on; Use Current for the current layer (HPLAYER ".").</summary>
        public string Layer
        {
            get => Settings.Layer ?? UseCurrent;
            set
            {
                Settings.Layer = value == null || value == UseCurrent ? null : value;
                Raise(nameof(Layer));
            }
        }

        /// <summary>Gets the transparency choices: Use Current, ByLayer, ByBlock and Transparency value.</summary>
        public IReadOnlyList<string> Transparencies => TransparencyNames;

        /// <summary>Gets or sets how much a new hatch lets show through, as the box names it (HPTRANSPARENCY).</summary>
        public string Transparency
        {
            get =>
                Settings.Transparency == null ? UseCurrent
                : Settings.Transparency.Value.IsByLayer ? "ByLayer"
                : Settings.Transparency.Value.IsByBlock ? "ByBlock"
                : TransparencyNames[3];
            set
            {
                switch (Array.IndexOf(TransparencyNames, value))
                {
                    case 1:
                        Settings.Transparency = DxfTransparency.ByLayer;
                        break;
                    case 2:
                        Settings.Transparency = DxfTransparency.ByBlock;
                        break;
                    case 3:
                        Settings.Transparency = Alpha(Percent());
                        break;
                    default:
                        Settings.Transparency = null;
                        break;
                }

                Raise(nameof(Transparency));
                Raise(nameof(HasTransparencyValue));
            }
        }

        /// <summary>Gets whether a transparency value is typed.</summary>
        public bool HasTransparencyValue => Transparency == TransparencyNames[3];

        /// <summary>Gets or sets the transparency value, a whole number from 0 to 90 per cent; another is refused in <see cref="Message"/>.</summary>
        public string TransparencyValue
        {
            get => _transparencyValue ?? Percent().ToString(CultureInfo.InvariantCulture);
            set
            {
                string typed = (value ?? string.Empty).Trim();

                if (
                    !int.TryParse(typed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int percent)
                    || percent < 0
                    || percent > 90
                )
                {
                    Message = "Requires an integer between 0 and 90.";
                    return;
                }

                Message = string.Empty;
                _transparencyValue = typed;
                Settings.Transparency = Alpha(percent);
                Raise(nameof(TransparencyValue));
                Raise(nameof(Transparency));
                Raise(nameof(HasTransparencyValue));
            }
        }

        /// <summary>Gets or sets whether the pattern is laid from the current origin (HPORIGIN), the Origin X, Y kept as they are.</summary>
        public bool IsCurrentOrigin
        {
            get => !IsSpecifiedOrigin;
            set => IsSpecifiedOrigin = !value;
        }

        /// <summary>Gets or sets whether the pattern is laid from an origin of its own: Origin X, Y or a corner of the boundary's extents.</summary>
        public bool IsSpecifiedOrigin
        {
            get => _specified ?? Settings.OriginMode != CadHatchOriginMode.Current;
            set
            {
                Stored();
                _specified = value;

                if (!value)
                {
                    Settings.OriginMode = CadHatchOriginMode.Current;
                    OriginX = _storedX.ToString("0.####", CultureInfo.InvariantCulture);
                    OriginY = _storedY.ToString("0.####", CultureInfo.InvariantCulture);
                }

                Raise(nameof(IsSpecifiedOrigin));
                Raise(nameof(IsCurrentOrigin));
                Raise(nameof(OriginX));
                Raise(nameof(OriginY));
                Raise(nameof(DefaultToExtents));
            }
        }

        /// <summary>Gets or sets whether a specified origin is a corner, or the middle, of the box round the hatch (HPORIGINMODE 1 to 5).</summary>
        public bool DefaultToExtents
        {
            get => Settings.OriginMode != CadHatchOriginMode.Current;
            set
            {
                Settings.OriginMode = value
                    ? (CadHatchOriginMode)(Math.Max(0, Array.IndexOf(CornerNames, Corner)) + 1)
                    : CadHatchOriginMode.Current;

                if (value)
                {
                    IsSpecifiedOrigin = true;
                }

                Raise(nameof(DefaultToExtents));
            }
        }

        /// <summary>Gets the corners offered: Bottom left, Bottom right, Top right, Top left and Center.</summary>
        public IReadOnlyList<string> Corners => CornerNames;

        private string _corner;

        /// <summary>Gets or sets the corner of the boundary's extents a specified origin is laid from.</summary>
        public string Corner
        {
            get =>
                _corner
                ?? (
                    Settings.OriginMode == CadHatchOriginMode.Current
                        ? CornerNames[0]
                        : CornerNames[(int)Settings.OriginMode - 1]
                );
            set
            {
                _corner = Array.IndexOf(CornerNames, value) >= 0 ? value : CornerNames[0];

                if (Settings.OriginMode != CadHatchOriginMode.Current)
                {
                    Settings.OriginMode = (CadHatchOriginMode)(Array.IndexOf(CornerNames, _corner) + 1);
                }

                Raise(nameof(Corner));
            }
        }

        /// <summary>
        /// Gets or sets whether a specified Origin X, Y becomes the current origin for the hatches after this one; off, it lays
        /// this HATCH's hatches alone, as AutoCAD's "Store as default origin". A corner of the extents is kept either way, as
        /// AutoCAD keeps HPORIGINMODE.
        /// </summary>
        public bool StoreAsDefault
        {
            get => _storeAsDefault;
            set => Set(ref _storeAsDefault, value);
        }

        /// <summary>Gets whether Click to set new origin is offered: by HATCH and by HATCHEDIT, each asking "Specify origin point:".</summary>
        public bool CanPickOrigin { get; }

        /// <summary>
        /// Gets whether the dialog closed for Click to set new origin: HATCH asks "Specify origin point:" and shows this dialog
        /// again with the point taken by <see cref="TakeOrigin"/>.
        /// </summary>
        public bool IsPickingOrigin { get; private set; }

        /// <summary>Click to set new origin: the dialog closes to have the origin picked; nothing where it is not offered.</summary>
        public void ClickToSetOrigin() => IsPickingOrigin = CanPickOrigin;

        /// <summary>
        /// The origin picked after Click to set new origin: Specified origin on, Default to boundary extents off, the point as
        /// Origin X, Y.
        /// </summary>
        /// <param name="x">The X of the point.</param>
        /// <param name="y">The Y of the point.</param>
        public void TakeOrigin(double x, double y)
        {
            IsPickingOrigin = false;
            DefaultToExtents = false;
            IsSpecifiedOrigin = true;
            OriginX = x.ToString("0.####", CultureInfo.InvariantCulture);
            OriginY = y.ToString("0.####", CultureInfo.InvariantCulture);
            Raise(nameof(OriginX));
            Raise(nameof(OriginY));
        }

        /// <summary>The origin was not picked (Esc at "Specify origin point:"): the dialog is shown again as it was.</summary>
        public void KeepOrigin() => IsPickingOrigin = false;

        /// <summary>Gets whether the origin given is for this HATCH alone: specified, not a corner, not stored as default.</summary>
        public bool IsOriginOnce => IsSpecifiedOrigin && !DefaultToExtents && !_storeAsDefault;

        /// <summary>Gets the current origin as the dialog found it, which an origin for this HATCH alone gives back afterwards.</summary>
        public double StoredOriginX
        {
            get
            {
                Stored();
                return _storedX;
            }
        }

        /// <summary>Gets the current origin's Y as the dialog found it.</summary>
        public double StoredOriginY
        {
            get
            {
                Stored();
                return _storedY;
            }
        }

        /// <summary>Gets or sets the gap tolerance, from 0 to 5000 drawing units (HPGAPTOL); another is refused in <see cref="Message"/>.</summary>
        public string GapTolerance
        {
            get => _gapTolerance ?? Settings.GapTolerance.ToString("0.####", CultureInfo.InvariantCulture);
            set
            {
                string typed = (value ?? string.Empty).Trim();

                if (
                    !double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out double gap)
                    || double.IsNaN(gap)
                    || double.IsInfinity(gap)
                )
                {
                    Message = "Requires numeric value.";
                    return;
                }

                if (gap < 0.0 || gap > 5000.0)
                {
                    Message = "Value must be between 0 and 5000.";
                    return;
                }

                Message = string.Empty;
                _gapTolerance = typed;
                Settings.GapTolerance = gap;
                Raise(nameof(GapTolerance));
            }
        }

        private void Stored()
        {
            if (double.IsNaN(_storedX))
            {
                _storedX = Settings.OriginX;
                _storedY = Settings.OriginY;
            }
        }

        private int Percent()
        {
            DxfTransparency? now = Settings.Transparency;
            return now.HasValue && now.Value.IsByAlpha
                ? (int)Math.Round(100.0 - now.Value.Alpha * 100.0 / 255.0, MidpointRounding.AwayFromZero)
                : 0;
        }

        private static DxfTransparency Alpha(int percent) =>
            new DxfTransparency((byte)Math.Floor(255.0 * (100 - percent) / 100.0));

        private static string ColorText(DxfColor color)
        {
            if (color.IsByLayer)
            {
                return "ByLayer";
            }

            if (color.IsByBlock)
            {
                return "ByBlock";
            }

            int index = color.ColorIndex;
            return index >= 1 && index <= 7 ? NamedColors[index + 1] : index.ToString(CultureInfo.InvariantCulture);
        }

        private static DxfColor ColorOf(string name)
        {
            int listed = Array.IndexOf(NamedColors, name);
            return listed == 0 ? DxfColor.ByLayer
                : listed == 1 ? DxfColor.ByBlock
                : listed > 1 ? DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)(listed - 1))
                : DxfColor.ByLayer;
        }
    }
}
