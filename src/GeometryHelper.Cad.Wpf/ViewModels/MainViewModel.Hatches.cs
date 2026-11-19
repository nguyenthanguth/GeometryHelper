using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The contextual Hatch Creation tab, shown while HATCH or GRADIENT runs with HPDLGMODE 2 (the Hatch dialog is shown with
    /// HPDLGMODE 1, the command line alone with 0), and the contextual Hatch Editor tab, shown while hatches are selected and no
    /// command runs, as AutoCAD 2021 shows them, with the values of the hatch to change in them (<see cref="HatchTabViewModel"/>).
    /// </summary>
    public sealed partial class MainViewModel
    {
        private CadHatchTab _hatchCreationTab;
        private CadHatchTab _hatchEditorTab;
        private HatchTabViewModel _hatchTab;

        // The hatches an option of the Hatch Editor tab runs -HATCHEDIT on, selected again once it ends: the command clears them.
        private List<string> _hatchKept;
        private bool _hatchRestoring;

        /// <summary>Gets the contextual Hatch Creation tab, shown while HATCH or GRADIENT runs.</summary>
        public CadWorkspaceExtension HatchCreationTab
        {
            get
            {
                if (_hatchCreationTab == null)
                {
                    _hatchCreationTab = new CadHatchTab("Hatch Creation");
                    _builtInTabs.Add(_hatchCreationTab);
                }

                return _hatchCreationTab;
            }
        }

        /// <summary>Gets the contextual Hatch Editor tab, shown while hatches are selected.</summary>
        public CadWorkspaceExtension HatchEditorTab
        {
            get
            {
                if (_hatchEditorTab == null)
                {
                    _hatchEditorTab = new CadHatchTab("Hatch Editor");
                    _builtInTabs.Add(_hatchEditorTab);
                }

                return _hatchEditorTab;
            }
        }

        /// <summary>Gets what the Hatch Creation or Hatch Editor tab shown does to its hatches; null while neither is shown.</summary>
        public HatchTabViewModel HatchTab => _hatchTab;

        /// <summary>
        /// Starts -HATCHEDIT on hatches and answers its prompts one after the other, as the buttons of the Hatch Editor tab do:
        /// the hatches are selected again once the command ends, so that the tab stays; with no answers given, HATCHEDIT (the
        /// Hatch dialog) runs instead.
        /// </summary>
        /// <param name="handles">The handles of the hatches.</param>
        /// <param name="answers">What to answer the options, as typed; null for the Hatch dialog.</param>
        internal void RunHatchEdit(IReadOnlyList<string> handles, string[] answers)
        {
            if (_current == null)
            {
                return;
            }

            _hatchKept = handles.ToList();
            StartCommand(answers == null ? "HATCHEDIT" : "-HATCHEDIT");

            // HATCHEDIT asks for its one hatch whatever was selected before it.
            if (answers == null && IsCommandRunning)
            {
                _current.Session.FeedSelection(new[] { handles[0] });
            }

            foreach (string answer in answers ?? Array.Empty<string>())
            {
                if (!IsCommandRunning)
                {
                    break;
                }

                Execute(answer);
            }
        }

        /// <summary>
        /// Shows the Hatch Creation tab where HATCH or GRADIENT runs with HPDLGMODE 2, the Hatch Editor tab where hatches are
        /// selected and no command runs, and keeps it in step with the drawing; hides it otherwise.
        /// </summary>
        private void SyncHatchTab()
        {
            if (_current == null)
            {
                HideHatchTab();
                return;
            }

            CadCommandSession session = _current.Session;
            CadDocument document = _current.Document;

            // An option of the Hatch Editor tab runs a command that clears the selection: the tab stays while it runs, and the
            // hatches are selected again as it ends.
            if (_hatchKept != null)
            {
                if (session.IsRunning || _hatchRestoring)
                {
                    return;
                }

                List<string> kept = _hatchKept.Where(h => document.FindEntity(h) is DxfHatch).ToList();
                _hatchKept = null;
                _hatchRestoring = true;

                try
                {
                    document.Selection.Set(kept);
                }
                finally
                {
                    _hatchRestoring = false;
                }
            }

            double mode = session.Remembered.TryGetValue(CadCommands.HatchDialogModeKey, out double dialogMode)
                ? dialogMode
                : 2.0;

            if (session.IsRunning)
            {
                bool hatching = session.Current.Name == "HATCH" || session.Current.Name == "GRADIENT";

                if (hatching && mode == 2.0)
                {
                    ShowHatchTab(null);
                }
                else
                {
                    HideHatchTab();
                }

                return;
            }

            List<string> hatches = document
                .Selection.Handles.Where(h => document.FindEntity(h) is DxfHatch && IsEditableAsItsKind(h))
                .OrderBy(h => h, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (hatches.Count == 0)
            {
                HideHatchTab();
            }
            else
            {
                ShowHatchTab(hatches);
            }
        }

        private void ShowHatchTab(IReadOnlyList<string> hatches)
        {
            if (
                _hatchTab != null
                && (
                    hatches == null
                        ? _hatchTab.IsCreation
                        : _hatchTab.IsEditor
                            && _hatchTab.Handles.SequenceEqual(hatches, StringComparer.OrdinalIgnoreCase)
                )
            )
            {
                _hatchTab.Refresh();
                return;
            }

            HideHatchTab();
            _hatchTab = new HatchTabViewModel(this, hatches);
            CadHatchTab tab = (CadHatchTab)(hatches == null ? HatchCreationTab : HatchEditorTab);
            tab.Show(_hatchTab);
            SelectedTab = tab;
            Raise(nameof(HatchTab));
        }

        /// <summary>Hides the Hatch Creation and Hatch Editor tabs.</summary>
        private void HideHatchTab()
        {
            if (_hatchTab == null)
            {
                return;
            }

            _hatchTab = null;

            foreach (CadHatchTab tab in new[] { _hatchCreationTab, _hatchEditorTab })
            {
                if (tab != null && tab.IsTabVisible)
                {
                    tab.Hide();

                    if (SelectedTab == tab || SelectedTab == null)
                    {
                        SelectedTab = null;
                    }
                }
            }

            Raise(nameof(HatchTab));
        }
    }

    /// <summary>A contextual Hatch Creation or Hatch Editor tab, in green as the others, shown while its hatches or its command are there.</summary>
    internal sealed class CadHatchTab : CadWorkspaceExtension
    {
        private readonly string _header;

        public CadHatchTab(string header) => _header = header;

        public override string TabHeader => _header;

        public void Show(HatchTabViewModel hatch)
        {
            TabContent = hatch;
            IsTabVisible = true;
        }

        public void Hide()
        {
            IsTabVisible = false;
            TabContent = null;
        }
    }
}
