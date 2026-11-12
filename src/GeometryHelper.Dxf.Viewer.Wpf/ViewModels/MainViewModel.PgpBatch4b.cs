using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// Batch 4 of acad.pgp's commands that are the window's own: QUIT (and EXIT), COMMANDLINE and COMMANDLINEHIDE, HIDEPALETTES
    /// and SHOWPALETTES (Ctrl+Shift+H), and -OSNAP, each in the words of AutoCAD 2021.
    /// </summary>
    public sealed partial class MainViewModel
    {
        // The object snaps -OSNAP names in "Current osnap modes", as AutoCAD 2021 writes them (probed: 167 is End,Mid,Cen,Int,Perp).
        private static readonly KeyValuePair<CadSnapModes, string>[] SnapShortNames =
        {
            new KeyValuePair<CadSnapModes, string>(CadSnapModes.Endpoint, "End"),
            new KeyValuePair<CadSnapModes, string>(CadSnapModes.Midpoint, "Mid"),
            new KeyValuePair<CadSnapModes, string>(CadSnapModes.Center, "Cen"),
            new KeyValuePair<CadSnapModes, string>(CadSnapModes.Node, "Node"),
            new KeyValuePair<CadSnapModes, string>(CadSnapModes.Quadrant, "Quad"),
            new KeyValuePair<CadSnapModes, string>(CadSnapModes.Intersection, "Int"),
            new KeyValuePair<CadSnapModes, string>(CadSnapModes.Insertion, "Ins"),
            new KeyValuePair<CadSnapModes, string>(CadSnapModes.Perpendicular, "Perp"),
            new KeyValuePair<CadSnapModes, string>(CadSnapModes.Tangent, "Tan"),
            new KeyValuePair<CadSnapModes, string>(CadSnapModes.Nearest, "Nea"),
            new KeyValuePair<CadSnapModes, string>(CadSnapModes.GeometricCenter, "GCen"),
            new KeyValuePair<CadSnapModes, string>(CadSnapModes.ApparentIntersection, "App"),
            new KeyValuePair<CadSnapModes, string>(CadSnapModes.Extension, "Ext"),
            new KeyValuePair<CadSnapModes, string>(CadSnapModes.Parallel, "Par"),
        };

        // Which of Properties, the Drawings list and the command line were shown when HIDEPALETTES hid them; null while none is hidden by it.
        private bool[] _palettesHiddenFrom;

        /// <summary>
        /// Raised by QUIT or EXIT typed: the window closes once the command has ended, and so asks, as it does when closed by its
        /// x, about the drawings with changes not saved (<see cref="ConfirmExit"/>).
        /// </summary>
        public event EventHandler QuitRequested;

        /// <summary>Gets whether HIDEPALETTES has hidden Properties, the Drawings list and the command line, and SHOWPALETTES has not yet shown them again.</summary>
        public bool ArePalettesHidden => _palettesHiddenFrom != null;

        private void AddBatchFourBWindowCommands(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("QUIT", c => RunWindow(Quit)));
            registry.Add(new CadCommand("COMMANDLINE", c => RunWindow(() => IsCommandLineShown = true)));
            registry.Add(new CadCommand("COMMANDLINEHIDE", c => RunWindow(() => IsCommandLineShown = false)));
            registry.Add(new CadCommand("HIDEPALETTES", c => RunWindow(HidePalettes)));
            registry.Add(new CadCommand("SHOWPALETTES", c => RunWindow(ShowPalettes)));
            registry.Add(new CadCommand("-OSNAP", OsnapTyped));
        }

        private static IEnumerable<CadPrompt> RunWindow(Action action)
        {
            action();
            return Array.Empty<CadPrompt>();
        }

        /// <summary>
        /// Runs a command of the window that needs no drawing, typed or as an alias, with none open: QUIT and EXIT, COMMANDLINE
        /// and CLI, COMMANDLINEHIDE, HIDEPALETTES and POFF, SHOWPALETTES and PON.
        /// </summary>
        private bool RunWindowCommandWithoutDocument(string word)
        {
            switch (word.ToUpperInvariant())
            {
                case "QUIT":
                case "EXIT":
                    Quit();
                    return true;
                case "COMMANDLINE":
                case "CLI":
                    IsCommandLineShown = true;
                    return true;
                case "COMMANDLINEHIDE":
                    IsCommandLineShown = false;
                    return true;
                case "HIDEPALETTES":
                case "POFF":
                    HidePalettes();
                    return true;
                case "SHOWPALETTES":
                case "PON":
                    ShowPalettes();
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>QUIT: the window asked to close; with no window listening, it says so.</summary>
        private void Quit()
        {
            if (QuitRequested == null)
            {
                Say("QUIT is done by the window; its x closes it.");
            }
            else
            {
                QuitRequested(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// HIDEPALETTES: Properties, the Drawings list and the command line hidden, each remembered as it was; the ribbon stays,
        /// and a second HIDEPALETTES keeps what the first remembered.
        /// </summary>
        private void HidePalettes()
        {
            if (_palettesHiddenFrom != null)
            {
                return;
            }

            _palettesHiddenFrom = new[] { IsPropertiesPanelShown, IsFileListShown, IsCommandLineShown };
            IsPropertiesPanelShown = false;
            IsFileListShown = false;
            IsCommandLineShown = false;
            Raise(nameof(ArePalettesHidden));
        }

        /// <summary>SHOWPALETTES: what HIDEPALETTES hid shown again, each as it was; nothing where none was hidden by it.</summary>
        private void ShowPalettes()
        {
            bool[] was = _palettesHiddenFrom;

            if (was == null)
            {
                return;
            }

            _palettesHiddenFrom = null;
            IsPropertiesPanelShown = was[0];
            IsFileListShown = was[1];
            IsCommandLineShown = was[2];
            Raise(nameof(ArePalettesHidden));
        }

        /// <summary>
        /// -OSNAP: "Current osnap modes: End,Mid" where any are on, then "Enter list of object snap modes:", END,MID or NONe,
        /// the running snaps replaced by those named, each by its first three letters or more of its word; a list with a name
        /// that is none of them is refused, "Invalid object snap modes.", and asked again. Enter ends it, the snaps kept; whether
        /// OSNAP is on is left as it is.
        /// </summary>
        private IEnumerable<CadPrompt> OsnapTyped(CadCommandContext c)
        {
            string current = string.Join(
                ",",
                SnapShortNames.OrderBy(n => (int)n.Key).Where(n => (SnapModes & n.Key) == n.Key).Select(n => n.Value)
            );

            if (current.Length > 0)
            {
                c.Write("Current osnap modes: " + current);
            }

            while (true)
            {
                yield return CadPrompt.ForString("Enter list of object snap modes");

                if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Trim().Length == 0)
                {
                    yield break;
                }

                var modes = CadSnapModes.None;
                var unbuilt = new List<string>();
                bool valid = true;

                foreach (string token in c.Input.Text.Split(','))
                {
                    string typed = token.Trim().ToUpperInvariant();

                    if (typed.Length >= 3 && "NONE".StartsWith(typed, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    SnapModeInfo info = SnapModeTable.FirstOrDefault(i =>
                        typed.Length >= 3
                        && typed.StartsWith(i.Keyword, StringComparison.Ordinal)
                        && i.Word.StartsWith(typed, StringComparison.Ordinal)
                    );

                    if (info == null)
                    {
                        valid = false;
                        break;
                    }

                    if (info.IsBuilt)
                    {
                        modes |= info.Mode;
                    }
                    else
                    {
                        unbuilt.Add(info.Name);
                    }
                }

                if (!valid)
                {
                    c.Write("Invalid object snap modes.");
                    continue;
                }

                foreach (string name in unbuilt)
                {
                    c.Write(name + " object snap is not built yet.");
                }

                SnapModes = modes;
                yield break;
            }
        }
    }
}
