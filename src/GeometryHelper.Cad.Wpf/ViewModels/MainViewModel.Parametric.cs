using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The Parametric ribbon tab, as AutoCAD 2021's for what the presenter has: Geometric (Auto Constrain, the twelve
    /// geometric constraints, Show/Hide, Show All, Hide All), Dimensional (Linear with Horizontal and Vertical, Aligned, Radius,
    /// Diameter, Angular, Convert, Show/Hide, Show All, Hide All and, in its slide-out, the two constraint modes) and Manage
    /// (Delete Constraints, Parameters Manager). The launchers of Geometric and Dimensional open Constraint Settings on their
    /// tabs. A button starts its command through <see cref="MainViewModel.StartCommandCommand"/>, as the buttons of Home do;
    /// Show All and Hide All of Geometric work on the bars of the working space through the document.
    /// </summary>
    public sealed class ParametricRibbonViewModel : ObservableObject
    {
        internal ParametricRibbonViewModel(MainViewModel model)
        {
            Model = model;
            ShowAllCommand = new DelegateCommand(_ => model.ShowAllConstraintBars(true), _ => model.HasDocument);
            SettingsCommand = new DelegateCommand(
                _ => model.StartCommandCommand.Execute("CONSTRAINTSETTINGS"),
                _ => model.HasDocument
            );
            HideAllCommand = new DelegateCommand(_ => model.ShowAllConstraintBars(false), _ => model.HasDocument);
            DimensionalSettingsCommand = new DelegateCommand(
                _ => model.OpenConstraintSettings(1),
                _ => model.HasDocument
            );
            DisplayAllCommand = new DelegateCommand(
                p => model.DisplayAllDimensionalConstraints(((p as string) ?? string.Empty).EndsWith("Showall")),
                _ => model.HasDocument
            );
            model.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.HasDocument))
                {
                    (ShowAllCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                    (HideAllCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                    (DimensionalSettingsCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                    (DisplayAllCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                }

                // CCONSTRAINTFORM typed on the command line, or the drawing changed: the pressed mode follows.
                if (
                    e.PropertyName == nameof(MainViewModel.Revision)
                    || e.PropertyName == nameof(MainViewModel.HasDocument)
                )
                {
                    Raise(nameof(IsDynamicMode));
                    Raise(nameof(IsAnnotationalMode));
                }
            };
        }

        /// <summary>Gets the window's view model, whose commands the buttons run.</summary>
        public MainViewModel Model { get; }

        /// <summary>Gets the command of Show All: every constraint bar of the working space shown.</summary>
        public ICommand ShowAllCommand { get; }

        /// <summary>Gets the command of the launcher of Geometric: Constraint Settings on its Geometric tab.</summary>
        public ICommand SettingsCommand { get; }

        /// <summary>Gets the command of the launcher of Dimensional: Constraint Settings on its Dimensional tab.</summary>
        public ICommand DimensionalSettingsCommand { get; }

        /// <summary>
        /// Gets the command of Show All and Hide All of Dimensional, given "DCDISPLAY Showall" or "DCDISPLAY Hideall": DCDISPLAY run
        /// over every object of the drawing, answered Display or Hide.
        /// </summary>
        public ICommand DisplayAllCommand { get; }

        /// <summary>
        /// Gets or sets whether new dimensional constraints are dynamic (CCONSTRAINTFORM 0, the Dynamic Constraint Mode of the
        /// slide-out of Dimensional): true while that is the form in force. Setting true puts it in force; setting false changes
        /// nothing, one of the two modes always being in force.
        /// </summary>
        public bool IsDynamicMode
        {
            get => Model.Session != null && Model.Session.Constraints.Form == 0;
            set => SetForm(0, value);
        }

        /// <summary>
        /// Gets or sets whether new dimensional constraints are annotational (CCONSTRAINTFORM 1, the Annotational Constraint Mode
        /// of the slide-out of Dimensional): true while that is the form in force. Setting true puts it in force; setting false
        /// changes nothing, one of the two modes always being in force.
        /// </summary>
        public bool IsAnnotationalMode
        {
            get => Model.Session != null && Model.Session.Constraints.Form == 1;
            set => SetForm(1, value);
        }

        private void SetForm(int form, bool on)
        {
            if (on && Model.Session != null)
            {
                Model.Session.Constraints.Form = form;
            }

            Raise(nameof(IsDynamicMode));
            Raise(nameof(IsAnnotationalMode));
        }

        /// <summary>Gets the command of Hide All: every constraint bar of the working space hidden.</summary>
        public ICommand HideAllCommand { get; }
    }

    /// <summary>
    /// The Parametric tab, the Constraint Settings dialog of CONSTRAINTSETTINGS and the Parameters Manager palette of
    /// PARAMETERS, each shown through a function the workspace sets, so that a test drives them with none; with none,
    /// CONSTRAINTSETTINGS says the dialog is shown by the window.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private CadBuiltInTab _parametricTab;
        private ParametersPaletteViewModel _parameters;
        private int _constraintSettingsTab;

        /// <summary>Gets the Parametric ribbon tab, always shown, after Annotate and before View.</summary>
        public CadWorkspaceExtension ParametricTab
        {
            get
            {
                if (_parametricTab == null)
                {
                    _parametricTab = new CadBuiltInTab("Parametric", new ParametricRibbonViewModel(this));
                    _builtInTabs.Add(_parametricTab);
                }

                return _parametricTab;
            }
        }

        /// <summary>Gets or sets how the workspace shows the Constraint Settings dialog: true when OK was pressed, the settings then taken.</summary>
        public Func<ConstraintSettingsViewModel, bool> ShowConstraintSettings { get; set; }

        /// <summary>Gets or sets how the workspace shows (true) or closes (false) the Parameters Manager palette.</summary>
        public Action<ParametersPaletteViewModel, bool> ShowParameters { get; set; }

        /// <summary>
        /// The launchers of Geometric and Dimensional: CONSTRAINTSETTINGS started, the dialog opening on the tab given (0
        /// Geometric, 1 Dimensional), which the command itself, typed, opens on Geometric.
        /// </summary>
        /// <param name="tab">The tab.</param>
        internal void OpenConstraintSettings(int tab)
        {
            _constraintSettingsTab = tab;

            try
            {
                StartCommand("CONSTRAINTSETTINGS");
            }
            finally
            {
                _constraintSettingsTab = 0;
            }
        }

        /// <summary>CONSTRAINTSETTINGS' dialog: over the session's settings, OK giving the command those the boxes hold.</summary>
        private void OnConstraintSettingsRequested(CadUiRequestEventArgs e)
        {
            if (ShowConstraintSettings == null || !(e.Payload is CadConstraintSettingsRequest request))
            {
                return;
            }

            var dialog = new ConstraintSettingsViewModel(request) { Tab = _constraintSettingsTab };
            request.Action = CadDialogAction.Cancel;

            if (ShowConstraintSettings(dialog) && dialog.IsValid)
            {
                dialog.Commit();
            }

            e.Handled = true;
        }

        /// <summary>PARAMETERS (payload true) shows the Parameters Manager over the document, PARAMETERSCLOSE (false) closes it.</summary>
        private void OnParametersRequested(CadUiRequestEventArgs e)
        {
            if (ShowParameters == null)
            {
                return;
            }

            bool show = !(e.Payload is bool shown) || shown;
            _parameters = _parameters ?? new ParametersPaletteViewModel();
            _parameters.Document = Document;
            ShowParameters(_parameters, show);

            if (Session != null)
            {
                Session.ParametersShown = show;
            }

            e.Handled = true;
        }

        /// <summary>Makes the palette follow the document shown: called when the current drawing changes.</summary>
        private void FollowParametersDocument()
        {
            if (_parameters != null)
            {
                _parameters.Document = Document;
            }
        }

        /// <summary>The ribbon's Show All and Hide All: the bars of every constrained object of the working space shown or hidden for good.</summary>
        internal void ShowAllConstraintBars(bool show)
        {
            CadDocument document = Document;

            if (document == null)
            {
                return;
            }

            if (show)
            {
                document.ConstraintBars.ShowAll(document.WorkingSpace);
            }
            else
            {
                document.ConstraintBars.HideAll(document.WorkingSpace);
            }

            Revision++;
        }

        /// <summary>
        /// Show All and Hide All of Dimensional: DCDISPLAY run over every object of the drawing and answered Display or Hide, as
        /// AutoCAD's macro does with its Showall and Hideall; the objects selected before are selected again after it.
        /// </summary>
        /// <param name="show">True to show the dynamic dimensional constraints, false to hide them.</param>
        internal void DisplayAllDimensionalConstraints(bool show)
        {
            CadDocument document = Document;

            if (document == null)
            {
                return;
            }

            List<string> kept = document.Selection.Handles.OrderBy(h => h, StringComparer.Ordinal).ToList();
            document.Selection.Clear();
            StartCommand("DCDISPLAY");
            Execute("ALL");
            Enter();
            Execute(show ? "Display" : "Hide");

            if (kept.Count > 0)
            {
                document.Selection.Set(kept);
            }
        }

        /// <summary>Redraws the canvas where the constraint bars changed without the drawing changing.</summary>
        private void OnConstraintBarsChanged(object sender, EventArgs e) => Revision++;
    }
}
