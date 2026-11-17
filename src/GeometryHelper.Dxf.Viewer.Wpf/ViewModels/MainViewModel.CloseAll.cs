using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>SAVEALL and CLOSEALL: every drawing the window has open saved, or closed with the question about those not saved.</summary>
    public sealed partial class MainViewModel
    {
        private void AddCloseAllWindowCommands(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("SAVEALL", c => AfterTheCommand(() => SaveAllDocuments())));
            registry.Add(new CadCommand("CLOSEALL", c => AfterTheCommand(() => CloseAllDocuments())));
        }

        /// <summary>A command of the window that does its work once it has ended, as a command cannot save or close its own document while it runs.</summary>
        private IEnumerable<CadPrompt> AfterTheCommand(Action work)
        {
            _afterCommand = work;
            return Array.Empty<CadPrompt>();
        }

        /// <summary>
        /// Saves every open drawing that has changes not saved, as SAVEALL does: each to its file, one with no file asking its
        /// Save As (the window's dialog), in the order of their files; the drawing shown before stays the one shown. Says how
        /// many were saved.
        /// </summary>
        /// <returns>Whether every one of them was saved; false where a save was refused or cancelled.</returns>
        public bool SaveAllDocuments()
        {
            KnownFile shown = _current;
            List<KnownFile> changed = _files
                .Values.Where(f => f.Document != null && f.Document.IsModified)
                .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(f => f.Path, StringComparer.Ordinal)
                .ToList();
            int saved = 0;
            bool all = true;

            foreach (KnownFile file in changed)
            {
                if (SaveFile(file))
                {
                    saved++;
                }
                else
                {
                    all = false;
                }
            }

            if (shown != null && shown != _current && shown.Document != null)
            {
                Show(shown);
            }

            Say(
                changed.Count == 0 ? "No drawing has changes to save."
                : saved == 1 ? "Saved 1 drawing."
                : "Saved " + Count(saved) + " drawings."
            );
            return all;
        }
    }
}
