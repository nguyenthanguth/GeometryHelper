using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The Missing SHX files dialog, shown once as a drawing opens whose text styles name SHX fonts not found, or whose
    /// styles and MTEXTs name TrueType fonts the machine has not (then titled Missing Fonts), through a function the
    /// workspace sets, so that a test drives it with none; the replacements chosen in it kept for the drawing while it is
    /// open, through a change of Global settings too.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private readonly ConditionalWeakTable<CadDocument, SortedDictionary<string, string>> _shxReplacements =
            new ConditionalWeakTable<CadDocument, SortedDictionary<string, string>>();

        /// <summary>
        /// Gets or sets how the workspace shows the Missing SHX files dialog: true when OK was pressed, the replacements chosen
        /// then applied. Null shows none, as in a test or a screenshot; the command line says the missing files either way.
        /// </summary>
        public Func<MissingShxViewModel, bool> ShowMissingShx { get; set; }

        /// <summary>
        /// Shows the Missing SHX files dialog for a drawing just opened, where Global settings show it and its text styles name
        /// SHX fonts not found, or TrueType fonts the machine has not (listed, with what they are drawn in, and not chosen): OK draws the files in the replacements chosen and keeps them for the drawing; Do not show again
        /// with OK turns the dialog off in Global settings and saves them; Cancel keeps the FONTALT replacement.
        /// </summary>
        private void OfferMissingFonts(CadDocument document)
        {
            if (ShowMissingShx == null || !_settings.ShowMissingShxDialog)
            {
                return;
            }

            IReadOnlyList<CadMissingFont> missing = document.FindMissingFonts();
            IReadOnlyList<string> trueType = document.FindMissingTrueTypeFonts();

            if (missing.Count == 0 && trueType.Count == 0)
            {
                return;
            }

            var dialog = new MissingShxViewModel(document, missing, trueType, _settings.DrawingFont);

            if (!ShowMissingShx(dialog))
            {
                return;
            }

            SortedDictionary<string, string> kept = _shxReplacements.GetValue(document, d => new SortedDictionary<
                string,
                string
            >(StringComparer.OrdinalIgnoreCase));

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
                catch (Exception ex)
                    when (ex is IOException
                        || ex is UnauthorizedAccessException
                        || ex is ArgumentException
                        || ex is NotSupportedException
                    )
                {
                    GeometryHelperLog.Warn(
                        "MainViewModel: Global settings were not saved after Do not show again.",
                        ex
                    );
                }
            }
        }

        /// <summary>
        /// Reads the font lists the Text Style dialog and the MTEXT editor show ahead (the TrueType families of the machine and
        /// the SHX files of the folders Global settings give), on threads of their own at below normal priority, and gives back
        /// at once. The app calls it once its window has drawn, and again where Global settings change the font folders.
        /// </summary>
        /// <returns>The task, complete when the lists are read.</returns>
        internal Task WarmUpFonts() => FontListWarmUp.Start(_settings.ToFonts().FontFolders);

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
