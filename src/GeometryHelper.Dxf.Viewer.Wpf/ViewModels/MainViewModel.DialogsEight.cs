using System;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// LAYOUT Template's and SAveas's dialogs: Select Template From File and Create Drawing File through
    /// <see cref="AskExportFile"/>, then Insert Layout(s) and Template Options, each shown through a function the workspace sets.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>Gets or sets how the workspace shows Insert Layout(s): true when OK was pressed.</summary>
        public Func<InsertLayoutViewModel, bool> ShowInsertLayout { get; set; }

        /// <summary>Gets or sets how the workspace shows Template Options: true when OK was pressed.</summary>
        public Func<TemplateOptionsViewModel, bool> ShowTemplateOptions { get; set; }

        /// <summary>LAYOUT's requests: the template file, the layouts to bring in, the options of a template saved; with no function set each is left to the command line.</summary>
        private void OnLayoutTemplateRequested(CadUiRequestEventArgs e)
        {
            switch (e.Payload)
            {
                case CadExportJob _:
                    OnExportFileRequested(e);
                    break;
                case CadInsertLayoutRequest insert when ShowInsertLayout != null:
                    var layouts = new InsertLayoutViewModel(insert);

                    if (ShowInsertLayout(layouts))
                    {
                        layouts.Accept();
                    }

                    e.Handled = true;
                    break;
                case CadTemplateOptionsRequest options when ShowTemplateOptions != null:
                    var template = new TemplateOptionsViewModel(options);

                    if (ShowTemplateOptions(template))
                    {
                        template.Accept();
                    }

                    e.Handled = true;
                    break;
            }
        }
    }
}
