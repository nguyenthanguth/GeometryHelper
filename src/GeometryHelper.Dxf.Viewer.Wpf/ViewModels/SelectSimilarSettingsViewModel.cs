using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>One check box of the Select Similar Settings dialog: the property's words and whether it has to match.</summary>
    public sealed class SelectSimilarOptionViewModel : ObservableObject
    {
        private bool _isChecked;

        internal SelectSimilarOptionViewModel(string name, int bit, bool isChecked)
        {
            Name = name;
            Bit = bit;
            _isChecked = isChecked;
        }

        /// <summary>Gets the words beside the box: "Linetype scale".</summary>
        public string Name { get; }

        /// <summary>Gets the bit it stands for in SELECTSIMILARMODE.</summary>
        public int Bit { get; }

        /// <summary>Gets or sets whether the property has to be the same for an object to be taken.</summary>
        public bool IsChecked
        {
            get => _isChecked;
            set => Set(ref _isChecked, value);
        }
    }

    /// <summary>
    /// The Select Similar Settings dialog of SELECTSIMILAR's SEttings, as AutoCAD 2021 has it: Color, Layer, Linetype, Linetype
    /// scale, Lineweight, Plot style, Object style and Name as check boxes, those that must match, checked as SELECTSIMILARMODE
    /// has them; with none checked only the kind of object is matched. OK gives the mode back through <see cref="Mode"/>.
    /// </summary>
    public sealed class SelectSimilarSettingsViewModel : ObservableObject
    {
        /// <summary>Makes the dialog over the bits of SELECTSIMILARMODE.</summary>
        /// <param name="mode">The bits.</param>
        public SelectSimilarSettingsViewModel(int mode)
        {
            Options = CadSelectSimilar
                .Settings.Select(s => new SelectSimilarOptionViewModel(s.Key, s.Value, (mode & s.Value) != 0))
                .ToList();
        }

        /// <summary>Gets the boxes, in the order AutoCAD lists them.</summary>
        public IReadOnlyList<SelectSimilarOptionViewModel> Options { get; }

        /// <summary>Gets the words that tell what the boxes mean.</summary>
        public string Note =>
            "Objects of the same type are selected, and of those only the ones that match every property ticked.";

        /// <summary>Gets the bits of SELECTSIMILARMODE the boxes make: the bit of each box checked.</summary>
        public int Mode => Options.Where(o => o.IsChecked).Aggregate(0, (bits, o) => bits | o.Bit);
    }
}
