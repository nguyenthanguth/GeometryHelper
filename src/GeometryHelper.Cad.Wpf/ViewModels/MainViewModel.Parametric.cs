using System;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The Parametric ribbon tab, as AutoCAD 2021's for what the presenter has: Geometric (Auto Constrain, the twelve
    /// geometric constraints, Show All, Hide All, Show/Hide), Dimensional (Linear, Aligned, Angular, Radius, Diameter, Convert,
    /// Constraint Form, Show Dynamic Constraints) and Manage (Delete Constraints, Parameters Manager, Constraint Settings). A
    /// button starts its command through <see cref="MainViewModel.StartCommandCommand"/>, as the buttons of Home do; Show All
    /// and Hide All work on the bars of the working space through the document.
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
            model.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.HasDocument))
                {
                    (ShowAllCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                    (HideAllCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                }
            };
        }

        /// <summary>Gets the window's view model, whose commands the buttons run.</summary>
        public MainViewModel Model { get; }

        /// <summary>Gets the command of Show All: every constraint bar of the working space shown.</summary>
        public ICommand ShowAllCommand { get; }

        /// <summary>Gets the command of the panel's launcher: Constraint Settings.</summary>
        public ICommand SettingsCommand { get; }

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

        /// <summary>CONSTRAINTSETTINGS' dialog: over the session's settings, OK giving the command those the boxes hold.</summary>
        private void OnConstraintSettingsRequested(CadUiRequestEventArgs e)
        {
            if (ShowConstraintSettings == null || !(e.Payload is CadConstraintSettingsRequest request))
            {
                return;
            }

            var dialog = new ConstraintSettingsViewModel(request);
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

        /// <summary>Redraws the canvas where the constraint bars changed without the drawing changing.</summary>
        private void OnConstraintBarsChanged(object sender, EventArgs e) => Revision++;
    }
}
