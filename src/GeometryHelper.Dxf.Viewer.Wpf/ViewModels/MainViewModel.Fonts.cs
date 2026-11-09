using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Missing SHX files dialog, shown once as a drawing opens whose text styles name SHX fonts not found, through a
    /// function the workspace sets, so that a test drives it with none; the replacements chosen in it kept for the drawing
    /// while it is open, through a change of Global settings too.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private readonly ConditionalWeakTable<CadDocument, SortedDictionary<string, string>> _shxReplacements = new ConditionalWeakTable<CadDocument, SortedDictionary<string, string>>();

        /// <summary>
        /// Gets or sets how the workspace shows the Missing SHX files dialog: true when OK was pressed, the replacements chosen
        /// then applied. Null shows none, as in a test or a screenshot; the command line says the missing files either way.
        /// </summary>
        public Func<MissingShxViewModel, bool> ShowMissingShx { get; set; }

        /// <summary>
        /// Shows the Missing SHX files dialog for a drawing just opened, where Global settings show it and its text styles name
        /// SHX fonts not found: OK draws the files in the replacements chosen and keeps them for the drawing; Do not show again
        /// with OK turns the dialog off in Global settings and saves them; Cancel keeps the FONTALT replacement.
        /// </summary>
        private void OfferMissingFonts(CadDocument document)
        {
            if (ShowMissingShx == null || !_settings.ShowMissingShxDialog)
            {
                return;
            }

            IReadOnlyList<CadMissingFont> missing = document.FindMissingFonts();

            if (missing.Count == 0)
            {
                return;
            }

            var dialog = new MissingShxViewModel(document, missing);

            if (!ShowMissingShx(dialog))
            {
                return;
            }

            SortedDictionary<string, string> kept = _shxReplacements.GetValue(document, d => new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase));

            foreach (KeyValuePair<string, string> pair in dialog.Apply())
            {
                kept[pair.Key] = pair.Value;
            }

            if (dialog.DoNotShowAgain)
            {
                _settings.ShowMissingShxDialog = false;

                try
                {
                    _settings.Save(SettingsPath);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
                {
                    GeometryHelperLog.Warn("MainViewModel: Global settings were not saved after Do not show again.", ex);
                }
            }
        }

        /// <summary>The fonts of a document as Global settings give them, with the replacements the Missing SHX files dialog chose for it.</summary>
        private CadFonts FontsOf(CadDocument document)
        {
            CadFonts fonts = _settings.ToFonts(SkiaCanvasRenderer.HasFamily);

            if (_shxReplacements.TryGetValue(document, out SortedDictionary<string, string> kept))
            {
                foreach (KeyValuePair<string, string> pair in kept)
                {
                    fonts = fonts.WithSubstitution(pair.Key, pair.Value);
                }
            }

            return fonts;
        }
    }
}
