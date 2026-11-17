namespace GeometryHelper.Cad
{
    /// <summary>
    /// How the MTEXT editor's Find and Replace dialog matches words, as AutoCAD 2021's has it: Match case, Whole words only, Use
    /// wildcards, Match diacritics and Match half/full width forms. With none set the search ignores capitals, finds words
    /// inside longer ones, takes every character as itself, treats a letter and the same letter with an accent as one, and a
    /// full-width letter and its ordinary form as one.
    /// </summary>
    public sealed class CadMTextFindOptions
    {
        /// <summary>Gets or sets whether capitals must match: "Plate" is not found as "plate".</summary>
        public bool MatchCase { get; set; }

        /// <summary>Gets or sets whether a match must be whole words: "bolt" is found in "a bolt." but not in "bolted".</summary>
        public bool WholeWords { get; set; }

        /// <summary>
        /// Gets or sets whether the words searched for are a wildcard pattern (<c>*</c> any run, <c>?</c> one character, <c>#</c>
        /// a digit, <c>@</c> a letter, <c>[AB]</c> one of those, a back quote before a character that character itself), as
        /// FIND's: <c>M2?</c> finds M20 and M24.
        /// </summary>
        public bool UseWildcards { get; set; }

        /// <summary>Gets or sets whether accents must match: "cafe" is not found as "café".</summary>
        public bool MatchDiacritics { get; set; }

        /// <summary>Gets or sets whether half-width and full-width forms must match: "ABC" is not found as "ＡＢＣ".</summary>
        public bool MatchHalfFullWidth { get; set; }

        /// <summary>Makes a copy.</summary>
        /// <returns>The copy.</returns>
        public CadMTextFindOptions Clone() => (CadMTextFindOptions)MemberwiseClone();
    }
}
