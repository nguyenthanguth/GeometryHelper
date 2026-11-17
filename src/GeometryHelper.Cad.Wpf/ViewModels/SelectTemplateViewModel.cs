using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>What the Select Template dialog ended with.</summary>
    public enum SelectTemplateChoice
    {
        /// <summary>Nothing was chosen: Cancel.</summary>
        Cancel,

        /// <summary>A template file was chosen, from the list or by Browse.</summary>
        File,

        /// <summary>Open with no Template - Metric.</summary>
        Metric,

        /// <summary>Open with no Template - Imperial.</summary>
        Imperial,
    }

    /// <summary>A template of the list of Select Template: its file and how the list shows it.</summary>
    public sealed class SelectTemplateRow
    {
        internal SelectTemplateRow(string path)
        {
            Path = path;
            Name = System.IO.Path.GetFileName(path);
            Kind =
                System.IO.Path.GetExtension(path).Equals(".dwt", StringComparison.OrdinalIgnoreCase)
                    ? "Drawing template"
                : System.IO.Path.GetExtension(path).Equals(".dwg", StringComparison.OrdinalIgnoreCase) ? "Drawing"
                : "DXF";
        }

        /// <summary>Gets the full path of the file.</summary>
        public string Path { get; }

        /// <summary>Gets the name of the file with its extension.</summary>
        public string Name { get; }

        /// <summary>Gets what the file is: Drawing template, Drawing or DXF.</summary>
        public string Kind { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// AutoCAD 2021's Select Template dialog, shown by NEW: the templates of the template folder of Global settings listed (a
    /// .dwt, .dwg or .dxf each), Browse for a template elsewhere, Open to start from the one selected, and Open with no
    /// Template - Metric or Imperial for the presenter's own two. The dialog sets <see cref="Choice"/>, and
    /// <see cref="ChosenFile"/> where it is a file.
    /// </summary>
    public sealed class SelectTemplateViewModel : ObservableObject
    {
        private SelectTemplateRow _selected;
        private string _description = string.Empty;

        /// <summary>Makes the dialog over a folder and the templates found in it.</summary>
        /// <param name="folder">The template folder, as the dialog shows it.</param>
        /// <param name="files">The full paths of the templates in it, in the order they are listed.</param>
        /// <exception cref="ArgumentNullException">Thrown when the folder or the files are null.</exception>
        public SelectTemplateViewModel(string folder, IEnumerable<string> files)
        {
            Folder = folder ?? throw new ArgumentNullException(nameof(folder));
            Templates = (files ?? throw new ArgumentNullException(nameof(files)))
                .Select(f => new SelectTemplateRow(f))
                .ToList();
            _selected = Templates.FirstOrDefault();
            _description = DescriptionOfSelected();
            BrowseCommand = new DelegateCommand(_ => Browse());
        }

        /// <summary>Gets the template folder the list shows the templates of.</summary>
        public string Folder { get; }

        /// <summary>Gets the templates of the folder, in the order they are listed.</summary>
        public IReadOnlyList<SelectTemplateRow> Templates { get; }

        /// <summary>Gets whether the folder holds no template.</summary>
        public bool IsEmpty => Templates.Count == 0;

        /// <summary>Gets or sets the template selected in the list; the first at the start, null where there is none.</summary>
        public SelectTemplateRow SelectedTemplate
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    _description = DescriptionOfSelected();
                    Raise(nameof(CanOpen));
                    Raise(nameof(SelectedDescription));
                }
            }
        }

        /// <summary>Gets the description the selected template was saved with (<see cref="CadTemplate.DescriptionOf"/>); empty where it has none.</summary>
        public string SelectedDescription => _description;

        /// <summary>Gets whether Open can start from a template: one is selected.</summary>
        public bool CanOpen => _selected != null;

        /// <summary>Gets what the dialog ended with; <see cref="SelectTemplateChoice.Cancel"/> until a button chose.</summary>
        public SelectTemplateChoice Choice { get; private set; }

        /// <summary>Gets the file of the template chosen where <see cref="Choice"/> is <see cref="SelectTemplateChoice.File"/>; null for any other.</summary>
        public string ChosenFile { get; private set; }

        /// <summary>
        /// Gets or sets how the window picks a template elsewhere: given the template folder it shows an Open dialog of
        /// Drawing Template (*.dwt), Drawing (*.dwg) and DXF (*.dxf) and returns the file; null where it was cancelled. Not set,
        /// Browse does nothing.
        /// </summary>
        public Func<string, string> BrowseFile { get; set; }

        /// <summary>Gets the command of Browse: the file chosen in the window's Open dialog taken as the choice, the dialog then closing.</summary>
        public ICommand BrowseCommand { get; }

        /// <summary>Gets the event raised when a choice was made and the dialog is to close.</summary>
        public event EventHandler Chosen;

        /// <summary>Chooses the template selected in the list, as Open does; nothing where none is selected.</summary>
        /// <returns>Whether a template was chosen.</returns>
        public bool Open()
        {
            if (_selected == null)
            {
                return false;
            }

            return Choose(SelectTemplateChoice.File, _selected.Path);
        }

        /// <summary>Chooses Open with no Template - Metric.</summary>
        public void OpenMetric() => Choose(SelectTemplateChoice.Metric, null);

        /// <summary>Chooses Open with no Template - Imperial.</summary>
        public void OpenImperial() => Choose(SelectTemplateChoice.Imperial, null);

        private string DescriptionOfSelected() =>
            _selected == null ? string.Empty : CadTemplate.DescriptionOf(_selected.Path);

        private void Browse()
        {
            string file = BrowseFile?.Invoke(Folder);

            if (!string.IsNullOrWhiteSpace(file))
            {
                Choose(SelectTemplateChoice.File, file);
            }
        }

        private bool Choose(SelectTemplateChoice choice, string file)
        {
            Choice = choice;
            ChosenFile = file;
            Chosen?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }
}
