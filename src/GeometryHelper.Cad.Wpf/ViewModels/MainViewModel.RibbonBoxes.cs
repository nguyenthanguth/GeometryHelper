using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// One choice of the Lineweight box of Home › Properties: its name as the Properties palette writes it (ByLayer, ByBlock,
    /// Default, 0.25 mm) and the thickness in pixels of the line the box draws beside it.
    /// </summary>
    public sealed class LineweightItemViewModel
    {
        internal LineweightItemViewModel(string name, double thickness)
        {
            Name = name;
            Thickness = thickness;
        }

        /// <summary>Gets the name: "ByLayer", "ByBlock", "Default" or "0.25 mm".</summary>
        public string Name { get; }

        /// <summary>Gets the thickness of the sample line, in pixels: the width at 96 to the inch, at least 1 and at most 8.</summary>
        public double Thickness { get; }
    }

    /// <summary>
    /// The boxes of the ribbon that read and set what new objects take: Lineweight (CELWEIGHT) and Transparency
    /// (CETRANSPARENCY) of Home › Properties, the Text Layer (TEXTLAYER) of Annotate › Text and the Dim Layer (DIMLAYER) of
    /// Annotate › Dimensions, the Find Text box of Annotate › Text, and the settings the frames and the snap of underlays of
    /// Insert › Reference show.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>The first item of the Text Layer and Dim Layer boxes: new text or dimensions go on the current layer.</summary>
        public const string UseCurrentLayer = "Use Current";

        // The widths the LWEIGHT dialog lists, in hundredths of a millimetre.
        private static readonly int[] LineweightWidths =
        {
            0,
            5,
            9,
            13,
            15,
            18,
            20,
            25,
            30,
            35,
            40,
            50,
            53,
            60,
            70,
            80,
            90,
            100,
            106,
            120,
            140,
            158,
            200,
            211,
        };

        private static readonly IReadOnlyList<LineweightItemViewModel> LineweightChoices = new[]
        {
            new LineweightItemViewModel("ByLayer", 1.0),
            new LineweightItemViewModel("ByBlock", 1.0),
            new LineweightItemViewModel("Default", 1.0),
        }
            .Concat(LineweightWidths.Select(w => new LineweightItemViewModel(WidthName(w), WidthThickness(w))))
            .ToList();

        private static readonly IReadOnlyList<string> TransparencyChoices = new[] { "ByLayer", "ByBlock" }
            .Concat(Enumerable.Range(0, 10).Select(i => (i * 10).ToString(CultureInfo.InvariantCulture)))
            .ToList();

        private DelegateCommand _findTextCommand;
        private string _findWhatNext;
        private string _findText = string.Empty;

        /// <summary>Gets the choices of the Lineweight box: ByLayer, ByBlock, Default, then 0.00 mm to 2.11 mm as the LWEIGHT dialog lists them.</summary>
        public IReadOnlyList<LineweightItemViewModel> LineweightItems => LineweightChoices;

        /// <summary>
        /// Gets or sets the lineweight of new objects, the drawing's CELWEIGHT, as the Lineweight box of Properties shows it:
        /// ByLayer, ByBlock, Default or a width as "0.25 mm"; empty with no drawing open. Setting one sets CELWEIGHT as one undo
        /// step, and a name that is none of these is refused on the command line.
        /// </summary>
        public string CurrentLineweight
        {
            get
            {
                if (_current == null)
                {
                    return string.Empty;
                }

                DxfLineWeight weight = _current.Document.Database.Celweight;
                return weight == DxfLineWeight.ByLayer ? "ByLayer"
                    : weight == DxfLineWeight.ByBlock ? "ByBlock"
                    : weight == DxfLineWeight.ByLineWeightDefault ? "Default"
                    : WidthName((int)weight);
            }
            set
            {
                if (_current == null || value == null || value == CurrentLineweight)
                {
                    return;
                }

                SayRefused(CadPropertyEditor.Set(_current.Document, null, "Lineweight", value));
                RaiseRibbonBoxes();
            }
        }

        /// <summary>
        /// Gets the choices of the Transparency box: ByLayer, ByBlock, then 0, 10 to 90 per cent, and the transparency of new
        /// objects where it is another (a CETRANSPARENCY of 35 is listed after 90).
        /// </summary>
        public IReadOnlyList<string> CurrentTransparencyOptions
        {
            get
            {
                string current = CurrentTransparency;
                return current.Length == 0 || TransparencyChoices.Contains(current)
                    ? TransparencyChoices
                    : TransparencyChoices.Concat(new[] { current }).ToList();
            }
        }

        /// <summary>
        /// Gets or sets the transparency of new objects, the drawing's CETRANSPARENCY, as the Transparency box of Properties
        /// shows it: ByLayer, ByBlock or the per cent, "30"; empty with no drawing open. Setting one sets CETRANSPARENCY as one
        /// undo step: the lines drawn after it carry it.
        /// </summary>
        public string CurrentTransparency
        {
            get =>
                _current == null
                    ? string.Empty
                    : CadProperties.TransparencyText(_current.Document.Database.Cetransparency);
            set
            {
                if (_current == null || value == null || value == CurrentTransparency)
                {
                    return;
                }

                SayRefused(CadPropertyEditor.Set(_current.Document, null, "Transparency", value));
                RaiseRibbonBoxes();
            }
        }

        /// <summary>Gets the choices of the Text Layer box: <see cref="UseCurrentLayer"/>, then the layers of the drawing in the order of their names.</summary>
        public IReadOnlyList<string> TextLayerOptions => LayerChoices();

        /// <summary>Gets the choices of the Dim Layer box: <see cref="UseCurrentLayer"/>, then the layers of the drawing in the order of their names.</summary>
        public IReadOnlyList<string> DimLayerOptions => LayerChoices();

        /// <summary>
        /// Gets or sets the layer new text goes on, the session's TEXTLAYER, as the Text Layer box shows it: a layer of the
        /// drawing, or <see cref="UseCurrentLayer"/> where TEXT and MTEXT use the current layer. It is a setting of the
        /// session, so setting it is no undo step.
        /// </summary>
        public string CurrentTextLayer
        {
            get => _current == null ? null : LayerChoice(_current.Session.TextLayer);
            set
            {
                if (_current == null || value == null || value == CurrentTextLayer)
                {
                    return;
                }

                _current.Session.TextLayer = ChosenLayer(value);
                RaiseRibbonSettings();
            }
        }

        /// <summary>
        /// Gets or sets the layer new dimensions go on, the session's DIMLAYER, as the Dim Layer box shows it: a layer of the
        /// drawing, or <see cref="UseCurrentLayer"/> where dimensions use the current layer.
        /// </summary>
        public string CurrentDimLayer
        {
            get => _current == null ? null : LayerChoice(_current.Session.DimLayer);
            set
            {
                if (_current == null || value == null || value == CurrentDimLayer)
                {
                    return;
                }

                _current.Session.DimLayer = ChosenLayer(value);
                RaiseRibbonSettings();
            }
        }

        /// <summary>Gets or sets the words typed in the Find Text box of Annotate › Text; they stay there after Enter.</summary>
        public string FindText
        {
            get => _findText;
            set => Set(ref _findText, value ?? string.Empty);
        }

        /// <summary>
        /// Gets the command of Enter in the Find Text box: FIND started, its Find and Replace dialog opened with
        /// <see cref="FindText"/> in Find what, as AutoCAD's Find Text box starts FIND with the words typed.
        /// </summary>
        public ICommand FindTextCommand =>
            _findTextCommand
            ?? (
                _findTextCommand = new DelegateCommand(_ =>
                {
                    _findWhatNext = FindText.Trim();
                    StartCommand("FIND");
                    _findWhatNext = null;
                })
            );

        /// <summary>Gets the line IMAGEFRAME with the setting the drawing has, "IMAGEFRAME 1": what the frames button of Insert › Reference runs, which sets it as it is.</summary>
        public string ImageFrameOption => "IMAGEFRAME " + ImageFrame.ToString(CultureInfo.InvariantCulture);

        /// <summary>Gets the label of the snap button of Insert › Reference: "Snap to Underlays ON" or "Snap to Underlays OFF", as PDFOSNAP is.</summary>
        public string SnapToUnderlaysLabel => "Snap to Underlays " + (IsSnapToUnderlaysOn ? "ON" : "OFF");

        /// <summary>Gets the line PDFOSNAP with the setting the drawing has, "PDFOSNAP 1": what the snap button runs, which sets it as it is.</summary>
        public string SnapToUnderlaysOption => "PDFOSNAP " + (IsSnapToUnderlaysOn ? "1" : "0");

        private bool IsSnapToUnderlaysOn => _current != null && _current.Document.PdfOsnap;

        /// <summary>The words FIND's dialog is to open with, set while the Find Text box starts FIND; null for the dialog's own.</summary>
        private string FindWhatNext => string.IsNullOrEmpty(_findWhatNext) ? null : _findWhatNext;

        /// <summary>The boxes that read the drawing's settings, and the lists of layers, read again: the drawing changed or another is shown.</summary>
        private void RaiseRibbonBoxes()
        {
            Raise(nameof(CurrentLineweight));
            Raise(nameof(CurrentTransparencyOptions));
            Raise(nameof(CurrentTransparency));
            Raise(nameof(TextLayerOptions));
            Raise(nameof(DimLayerOptions));
            RaiseRibbonSettings();
        }

        /// <summary>The boxes of settings that no change of the drawing announces (TEXTLAYER, DIMLAYER, PDFOSNAP, IMAGEFRAME), read again after a command.</summary>
        private void RaiseRibbonSettings()
        {
            Raise(nameof(CurrentTextLayer));
            Raise(nameof(CurrentDimLayer));
            Raise(nameof(ImageFrameOption));
            Raise(nameof(SnapToUnderlaysLabel));
            Raise(nameof(SnapToUnderlaysOption));
        }

        private IReadOnlyList<string> LayerChoices() =>
            _current == null
                ? Array.Empty<string>()
                : new[] { UseCurrentLayer }
                    .Concat(
                        _current
                            .Document.Database.LayerTable.Select(l => l.Name)
                            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(n => n, StringComparer.Ordinal)
                    )
                    .ToList();

        /// <summary>What a box shows of the layer a setting names: the layer's own name where the drawing has it, else <see cref="UseCurrentLayer"/>.</summary>
        private string LayerChoice(string stored) =>
            !string.IsNullOrEmpty(stored) && _current.Document.Database.LayerTable.Has(stored)
                ? _current.Document.Database.LayerTable[stored].Name
                : UseCurrentLayer;

        /// <summary>What a setting keeps of the layer a box chose: null for the current layer, else the layer's name.</summary>
        private string ChosenLayer(string choice) =>
            choice == UseCurrentLayer || !_current.Document.Database.LayerTable.Has(choice)
                ? null
                : _current.Document.Database.LayerTable[choice].Name;

        private static string WidthName(int hundredths) =>
            (hundredths / 100.0).ToString("0.00", CultureInfo.InvariantCulture) + " mm";

        private static double WidthThickness(int hundredths) =>
            Math.Min(8.0, Math.Max(1.0, Math.Round(hundredths / 100.0 * 96.0 / 25.4)));
    }
}
