using System;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Output ribbon tab, as AutoCAD 2021's basics: Plot (Plot, Page Setup Manager, Preview, Plot Stamp), Export to DWF/PDF
    /// (Export PDF, Export) and Import, each button starting its command through <see cref="MainViewModel.StartCommandCommand"/>.
    /// </summary>
    public sealed class OutputRibbonViewModel
    {
        internal OutputRibbonViewModel(MainViewModel model)
        {
            Model = model;
        }

        /// <summary>Gets the window's view model, whose commands the buttons run.</summary>
        public MainViewModel Model { get; }
    }

    /// <summary>The Output ribbon tab and the Annotation Object Scale dialog (OBJECTSCALE), shown through a function the workspace sets.</summary>
    public sealed partial class MainViewModel
    {
        private CadBuiltInTab _outputTab;

        /// <summary>Gets the Output ribbon tab, always shown, after View.</summary>
        public CadWorkspaceExtension OutputTab
        {
            get
            {
                if (_outputTab == null)
                {
                    _outputTab = new CadBuiltInTab("Output", new OutputRibbonViewModel(this));
                    _builtInTabs.Add(_outputTab);
                }

                return _outputTab;
            }
        }

        /// <summary>Gets or sets how the workspace shows Annotation Object Scale: true when OK was pressed.</summary>
        public Func<ObjectScaleViewModel, bool> ShowObjectScale { get; set; }

        /// <summary>OBJECTSCALE's request; with no function set it is left to the command line.</summary>
        private void OnObjectScaleRequested(CadUiRequestEventArgs e)
        {
            if (e.Payload is CadObjectScaleRequest request && ShowObjectScale != null)
            {
                var dialog = new ObjectScaleViewModel(request);

                if (ShowObjectScale(dialog))
                {
                    dialog.Accept();
                    request.IsAccepted = true;
                }

                e.Handled = true;
            }
        }
    }
}
