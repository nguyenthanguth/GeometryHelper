using System.Linq;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The settings of SPELL, as AutoCAD 2021's Check Spelling Settings dialog gives them: what is included and which words
    /// are passed over. A word of one letter is always passed over.
    /// </summary>
    public sealed class CadSpellSettings
    {
        /// <summary>Gets or sets whether the text of dimensions is checked; the presenter checks none, whatever it says.</summary>
        public bool DimensionText { get; set; } = true;

        /// <summary>Gets or sets whether the attributes of block references are checked.</summary>
        public bool BlockAttributes { get; set; } = true;

        /// <summary>Gets or sets whether the text of external references is checked; the presenter checks none, whatever it says.</summary>
        public bool ExternalReferences { get; set; }

        /// <summary>Gets whether the presenter can check the text of dimensions: no, so the box is greyed.</summary>
        public bool CanCheckDimensionText => false;

        /// <summary>Gets whether the presenter can check the text of external references: no, so the box is greyed.</summary>
        public bool CanCheckExternalReferences => false;

        /// <summary>Gets or sets whether a word with a capital first and the rest small, "Autocad", is passed over.</summary>
        public bool IgnoreCapitalized { get; set; }

        /// <summary>Gets or sets whether a word of small and capital letters mixed otherwise, "MicroStation", is passed over.</summary>
        public bool IgnoreMixedCase { get; set; }

        /// <summary>Gets or sets whether a word all in capitals, "AUTODESK", is passed over; it is until set.</summary>
        public bool IgnoreUppercase { get; set; } = true;

        /// <summary>Gets or sets whether a word with a digit, "Gr8", is passed over; it is until set.</summary>
        public bool IgnoreWithNumbers { get; set; } = true;

        /// <summary>Gets or sets whether a word with a mark of punctuation inside it, "don't", is passed over.</summary>
        public bool IgnorePunctuation { get; set; }

        /// <summary>Makes a copy.</summary>
        /// <returns>The copy.</returns>
        public CadSpellSettings Clone() => (CadSpellSettings)MemberwiseClone();

        /// <summary>Whether a word is passed over by these settings.</summary>
        internal bool IsPassedOver(string word)
        {
            if (word.Length < 2)
            {
                return true;
            }

            if (word.Any(char.IsDigit))
            {
                return IgnoreWithNumbers;
            }

            if (IgnorePunctuation && word.Any(char.IsPunctuation))
            {
                return true;
            }

            var letters = word.Where(char.IsLetter).ToList();

            if (letters.Count == 0)
            {
                return false;
            }

            bool allUpper = letters.All(char.IsUpper);

            if (allUpper)
            {
                return IgnoreUppercase;
            }

            bool capitalized = char.IsUpper(letters[0]) && letters.Skip(1).All(char.IsLower);

            if (capitalized)
            {
                return IgnoreCapitalized;
            }

            return IgnoreMixedCase && letters.Any(char.IsUpper);
        }
    }
}
