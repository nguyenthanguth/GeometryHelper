using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// A symbol of geometric dimensioning and tolerancing as the dialogs draw it: the paths of <see cref="CadGdtGlyphs"/> in
    /// their box of 24, and the letter of a material condition in its circle.
    /// </summary>
    public sealed class GdtGlyphViewModel
    {
        private static readonly Dictionary<char, GdtGlyphViewModel> Made = new Dictionary<char, GdtGlyphViewModel>();

        private GdtGlyphViewModel(char? code)
        {
            Code = code;

            if (code.HasValue)
            {
                Name = CadGdtGlyphs.NameOf(code.Value);
                Stroke = Parse(CadGdtGlyphs.StrokeOf(code.Value));
                Fill = Parse(CadGdtGlyphs.FillOf(code.Value));
                Letter = CadGdtGlyphs.LetterOf(code.Value);
            }
            else
            {
                Name = "Blank";
            }
        }

        /// <summary>Gets the letter of the gdt font; null for the blank, which draws nothing.</summary>
        public char? Code { get; }

        /// <summary>Gets the name, as "Position"; "Blank" for none.</summary>
        public string Name { get; }

        /// <summary>Gets the strokes, in the box of 24; null for none.</summary>
        public System.Windows.Media.Geometry Stroke { get; }

        /// <summary>Gets the filled parts, the arrowheads of a runout; null for none.</summary>
        public System.Windows.Media.Geometry Fill { get; }

        /// <summary>Gets the letter in the circle of a material condition or the projected zone; null for a symbol of none.</summary>
        public string Letter { get; }

        /// <summary>Gets whether a letter stands in a circle.</summary>
        public bool HasLetter => Letter != null;

        /// <summary>The glyph of a letter of the gdt font, or the blank for null, made once.</summary>
        /// <param name="code">The letter.</param>
        /// <returns>The glyph.</returns>
        public static GdtGlyphViewModel Of(char? code)
        {
            char key = code ?? '\0';

            lock (Made)
            {
                if (!Made.TryGetValue(key, out GdtGlyphViewModel glyph))
                {
                    glyph = new GdtGlyphViewModel(code);
                    Made[key] = glyph;
                }

                return glyph;
            }
        }

        private static System.Windows.Media.Geometry Parse(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            System.Windows.Media.Geometry geometry = System.Windows.Media.Geometry.Parse(path);
            geometry.Freeze();
            return geometry;
        }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>A choice of the Symbol or the Material Condition dialog: its glyph, its name and whether it is the one in use.</summary>
    public sealed class GdtChoiceViewModel
    {
        /// <summary>Makes a choice.</summary>
        /// <param name="glyph">The glyph drawn.</param>
        /// <param name="label">The name, as its tooltip says it: "Position", "M: maximum material condition (MMC)".</param>
        /// <param name="isChosen">Whether it is the one in use, framed.</param>
        public GdtChoiceViewModel(GdtGlyphViewModel glyph, string label, bool isChosen)
        {
            Glyph = glyph;
            Label = label;
            IsChosen = isChosen;
        }

        /// <summary>Gets the glyph drawn.</summary>
        public GdtGlyphViewModel Glyph { get; }

        /// <summary>Gets the name.</summary>
        public string Label { get; }

        /// <summary>Gets whether it is the one in use.</summary>
        public bool IsChosen { get; }

        /// <inheritdoc/>
        public override string ToString() => Label;
    }

    /// <summary>A box of the Geometric Tolerance dialog that holds a material condition, shown as its letter in a circle.</summary>
    public abstract class MaterialCellViewModel : ObservableObject
    {
        private CadMaterialCondition _material;

        /// <summary>Gets or sets the material condition.</summary>
        public CadMaterialCondition Material
        {
            get => _material;
            set
            {
                if (Set(ref _material, value))
                {
                    Raise(nameof(MaterialGlyph));
                    Raise(nameof(HasMaterial));
                }
            }
        }

        /// <summary>Gets whether a material condition is chosen.</summary>
        public bool HasMaterial => _material != CadMaterialCondition.None;

        /// <summary>Gets the glyph of the material condition: M, L or S in a circle; the blank for none.</summary>
        public GdtGlyphViewModel MaterialGlyph => GdtGlyphViewModel.Of(GeometricToleranceViewModel.CodeOf(_material));
    }

    /// <summary>A tolerance box of a row: the diameter sign, the value and the material condition.</summary>
    public sealed class ToleranceCellViewModel : MaterialCellViewModel
    {
        private bool _diameter;
        private string _value = string.Empty;

        /// <summary>Gets or sets whether the diameter sign stands before the value; the box before it toggles it.</summary>
        public bool Diameter
        {
            get => _diameter;
            set => Set(ref _diameter, value);
        }

        /// <summary>Gets or sets the value, as typed.</summary>
        public string Value
        {
            get => _value;
            set => Set(ref _value, value ?? string.Empty);
        }

        /// <summary>Gets the glyph of the diameter sign.</summary>
        public GdtGlyphViewModel DiameterGlyph => GdtGlyphViewModel.Of('n');
    }

    /// <summary>A datum box of a row: the reference letter and the material condition.</summary>
    public sealed class DatumCellViewModel : MaterialCellViewModel
    {
        private string _letter = string.Empty;

        /// <summary>Gets or sets the reference letter, as typed.</summary>
        public string Letter
        {
            get => _letter;
            set => Set(ref _letter, value ?? string.Empty);
        }
    }

    /// <summary>A row of the Geometric Tolerance dialog: the symbol, two tolerances and three datums.</summary>
    public sealed class ToleranceRowViewModel : ObservableObject
    {
        private char? _symbol;

        /// <summary>Gets or sets the symbol, its letter of the gdt font; null for none.</summary>
        public char? Symbol
        {
            get => _symbol;
            set
            {
                if (_symbol != value)
                {
                    _symbol = value;
                    Raise(nameof(Symbol));
                    Raise(nameof(SymbolGlyph));
                }
            }
        }

        /// <summary>Gets the glyph of the symbol; the blank for none.</summary>
        public GdtGlyphViewModel SymbolGlyph => GdtGlyphViewModel.Of(_symbol);

        /// <summary>Gets the first tolerance.</summary>
        public ToleranceCellViewModel Tolerance1 { get; } = new ToleranceCellViewModel();

        /// <summary>Gets the second tolerance.</summary>
        public ToleranceCellViewModel Tolerance2 { get; } = new ToleranceCellViewModel();

        /// <summary>Gets the three datums, the primary first.</summary>
        public IReadOnlyList<DatumCellViewModel> Datums { get; } =
            new[] { new DatumCellViewModel(), new DatumCellViewModel(), new DatumCellViewModel() };
    }

    /// <summary>
    /// AutoCAD 2021's Geometric Tolerance dialog over the request TOLERANCE, LEADER, QLEADER or TEXTEDIT hands the window:
    /// two rows of a symbol, two tolerances and three datums, the height of a projected zone with its P, and the datum
    /// identifier, every box empty when it opens for a new frame, a frame's own when TEXTEDIT or a double click opens it. The
    /// symbol box opens the Symbol dialog, a box after a value or a letter the Material Condition dialog; the box before a
    /// tolerance value toggles the diameter sign, and the Projected Tolerance Zone box its P. OK writes the boxes into the
    /// request, whose words the frame is drawn by; Cancel leaves it.
    /// </summary>
    public sealed class GeometricToleranceViewModel : ObservableObject
    {
        private string _height = string.Empty;
        private bool _projectedZone;
        private string _datumIdentifier = string.Empty;

        /// <summary>
        /// Makes the dialog over a request, its boxes as the request holds them: empty for TOLERANCE, LEADER and QLEADER, the
        /// frame's for TEXTEDIT and a double click on one.
        /// </summary>
        /// <param name="request">The request of TOLERANCE, LEADER, QLEADER or TEXTEDIT.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public GeometricToleranceViewModel(CadToleranceRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));

            for (int r = 0; r < Rows.Count; r++)
            {
                ToleranceRowViewModel row = Rows[r];
                CadToleranceRow source = request.Rows[r];
                row.Symbol = source.Symbol;
                Fill(source.Tolerance1, row.Tolerance1);
                Fill(source.Tolerance2, row.Tolerance2);

                for (int d = 0; d < row.Datums.Count; d++)
                {
                    row.Datums[d].Letter = source.Datums[d].Letter;
                    row.Datums[d].Material = source.Datums[d].Material;
                }
            }

            _height = request.Height ?? string.Empty;
            _projectedZone = request.ProjectedZone;
            _datumIdentifier = request.DatumIdentifier ?? string.Empty;
        }

        /// <summary>Gets the request.</summary>
        public CadToleranceRequest Request { get; }

        /// <summary>Gets the two rows, the first on top.</summary>
        public IReadOnlyList<ToleranceRowViewModel> Rows { get; } =
            new[] { new ToleranceRowViewModel(), new ToleranceRowViewModel() };

        /// <summary>
        /// Gets the symbols of the Symbol dialog, in AutoCAD's order: position, concentricity, symmetry, parallelism,
        /// perpendicularity, angularity, cylindricity, flatness, circularity, straightness, profile of a surface, profile of a
        /// line, circular runout, total runout, then the blank.
        /// </summary>
        public static IReadOnlyList<GdtGlyphViewModel> Symbols { get; } =
            "jrifbagceudkht".Select(c => GdtGlyphViewModel.Of(c)).Concat(new[] { GdtGlyphViewModel.Of(null) }).ToList();

        /// <summary>Gets the material conditions of the Material Condition dialog, in AutoCAD's order: M, L, S, then the blank.</summary>
        public static IReadOnlyList<CadMaterialCondition> Materials { get; } =
            new[]
            {
                CadMaterialCondition.Maximum,
                CadMaterialCondition.Least,
                CadMaterialCondition.RegardlessOfSize,
                CadMaterialCondition.None,
            };

        /// <summary>Gets or sets the height of a projected tolerance zone, as typed.</summary>
        public string Height
        {
            get => _height;
            set => Set(ref _height, value ?? string.Empty);
        }

        /// <summary>Gets or sets whether the projected tolerance zone's P is written after the height.</summary>
        public bool ProjectedZone
        {
            get => _projectedZone;
            set => Set(ref _projectedZone, value);
        }

        /// <summary>Gets the glyph of the projected tolerance zone, P in a circle.</summary>
        public GdtGlyphViewModel ProjectedZoneGlyph => GdtGlyphViewModel.Of('p');

        /// <summary>Gets or sets the datum identifier, as typed.</summary>
        public string DatumIdentifier
        {
            get => _datumIdentifier;
            set => Set(ref _datumIdentifier, value ?? string.Empty);
        }

        /// <summary>The letter of the gdt font of a material condition; null for none.</summary>
        /// <param name="material">The material condition.</param>
        /// <returns>m, l or s; null for none.</returns>
        public static char? CodeOf(CadMaterialCondition material)
        {
            switch (material)
            {
                case CadMaterialCondition.Maximum:
                    return 'm';
                case CadMaterialCondition.Least:
                    return 'l';
                case CadMaterialCondition.RegardlessOfSize:
                    return 's';
                default:
                    return null;
            }
        }

        /// <summary>OK: the boxes written into the request, its action OK.</summary>
        public void Ok()
        {
            for (int r = 0; r < Rows.Count; r++)
            {
                ToleranceRowViewModel row = Rows[r];
                CadToleranceRow target = Request.Rows[r];
                target.Symbol = row.Symbol;
                Copy(row.Tolerance1, target.Tolerance1);
                Copy(row.Tolerance2, target.Tolerance2);

                for (int d = 0; d < row.Datums.Count; d++)
                {
                    target.Datums[d].Letter = row.Datums[d].Letter;
                    target.Datums[d].Material = row.Datums[d].Material;
                }
            }

            Request.Height = _height;
            Request.ProjectedZone = _projectedZone;
            Request.DatumIdentifier = _datumIdentifier;
            Request.Action = CadDialogAction.Ok;
        }

        /// <summary>Cancel: nothing written.</summary>
        public void Cancel() => Request.Action = CadDialogAction.Cancel;

        private static void Fill(CadToleranceValue source, ToleranceCellViewModel cell)
        {
            cell.Diameter = source.Diameter;
            cell.Value = source.Value;
            cell.Material = source.Material;
        }

        private static void Copy(ToleranceCellViewModel cell, CadToleranceValue target)
        {
            target.Diameter = cell.Diameter;
            target.Value = cell.Value;
            target.Material = cell.Material;
        }
    }
}
