using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// Drawing templates, as AutoCAD 2021 has them: NEW asks for a template in Select Template (the typed form with FILEDIA 0),
    /// QNEW and the quick access New start from the default template of Global settings, and a drawing is saved as a template
    /// with Save As.
    /// </summary>
    public sealed partial class MainViewModel
    {
        // The templates AutoCAD ships, by the names it gives them: typed at NEW, they stand for the presenter's own two.
        private const string MetricTemplateName = "acadiso";
        private const string ImperialTemplateName = "acad";

        private bool _saveAsTemplate;

        /// <summary>
        /// Gets or sets how the window shows Select Template: true when a template, or Open with no Template, was chosen, the
        /// choice then in <see cref="SelectTemplateViewModel.Choice"/>. Not set, NEW starts the drawing QNEW does.
        /// </summary>
        public Func<SelectTemplateViewModel, bool> ShowSelectTemplate { get; set; }

        /// <summary>
        /// Starts a new drawing from the template chosen in Select Template, as NEW does: the templates of the template folder of
        /// Global settings listed, Browse for another file, and Open with no Template - Metric or Imperial. Cancel starts none.
        /// Where the window has no such dialog the drawing is the one <see cref="NewDrawing()"/> makes.
        /// </summary>
        /// <returns>The document; null where the dialog was cancelled or the template could not be read.</returns>
        public CadDocument NewDrawingChoosingTemplate()
        {
            if (ShowSelectTemplate == null)
            {
                return NewDrawing();
            }

            string folder = _settings.EffectiveTemplateFolder;
            var dialog = new SelectTemplateViewModel(folder, CadTemplate.FindIn(folder));

            if (!ShowSelectTemplate(dialog))
            {
                return null;
            }

            switch (dialog.Choice)
            {
                case SelectTemplateChoice.File:
                    return NewDrawingFrom(dialog.ChosenFile);
                case SelectTemplateChoice.Metric:
                    return NewDrawingWithNoTemplate(true);
                case SelectTemplateChoice.Imperial:
                    return NewDrawingWithNoTemplate(false);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Makes a new drawing the presenter's own, as AutoCAD's Open with no Template: the metric template (millimetres, ISO-25)
        /// or the imperial one (inches, Standard), with the layer 0 and no other layer (a drawing's layers come from its template), named Drawing1.dwg and
        /// then on, listed under Open drawings until it is saved.
        /// </summary>
        /// <param name="metric">Whether the drawing is metric; false for imperial.</param>
        /// <returns>The document.</returns>
        public CadDocument NewDrawingWithNoTemplate(bool metric)
        {
            // The template's GRIDMODE is the window's, so that a new drawing shows the grid as Global settings say.
            DxfDatabase database = metric ? CadTemplate.CreateMetric() : CadTemplate.CreateImperial();
            database.ViewportTable["*Active"].GridEnabled = _settings.Grid;

            string name = NextDrawingName();
            CadDocument document = CadDocument.From(database, name);
            OpenDocument(document, name);
            return document;
        }

        /// <summary>
        /// Makes a new drawing from a template file, a .dwt, a .dwg or a .dxf: the drawing takes the template's layers, styles,
        /// blocks, layouts with their page setups, and variables, and is named Drawing1.dwg and then on, with no file of its own
        /// until it is saved. A template that cannot be read is said on the command line and starts nothing.
        /// </summary>
        /// <param name="template">The path of the template.</param>
        /// <returns>The document; null where the template could not be read.</returns>
        public CadDocument NewDrawingFrom(string template)
        {
            DxfDatabase database;

            try
            {
                database = CadDrawingFile.Open(Path.GetFullPath(template));
            }
            catch (Exception ex)
                when (ex is IOException
                    || ex is UnauthorizedAccessException
                    || ex is ArgumentException
                    || ex is NotSupportedException
                    || ex is FormatException
                )
            {
                Say("Cannot read the template " + template + ": " + ex.Message);
                return null;
            }

            string name = NextDrawingName();
            CadDocument document = CadDocument.From(database, name);
            OpenDocument(document, name);
            Say("Started from the template " + Path.GetFileName(template) + ".");
            return document;
        }

        /// <summary>
        /// The file of the template QNEW starts from, as Global settings name it (Default Template File Name for QNEW), found as
        /// typed or in the template folder; null where none is named, or where the one named is not there, said on the command line.
        /// </summary>
        private string FindQnewTemplate()
        {
            string named = _settings.QnewTemplate;

            if (named.Length == 0)
            {
                return null;
            }

            string file = CadTemplate.Find(named, _settings.EffectiveTemplateFolder);

            if (file == null)
            {
                Say("The default template " + named + " for QNEW is not found; the built-in template is used.");
            }

            return file;
        }

        /// <summary>The name of the next new drawing: Drawing1.dwg, then Drawing2.dwg, one no drawing open holds.</summary>
        private string NextDrawingName()
        {
            string name;

            do
            {
                _newDrawings++;
                name = "Drawing" + _newDrawings.ToString(CultureInfo.InvariantCulture) + ".dwg";
            } while (
                _files.ContainsKey(name)
                || _files.Values.Any(f =>
                    f.Document != null && string.Equals(f.Document.Title, name, StringComparison.OrdinalIgnoreCase)
                )
            );

            return name;
        }

        /// <summary>
        /// NEW: with FILEDIA 1 the Select Template dialog once the command has ended; with FILEDIA 0 "Enter template file name
        /// or [. (for none)] &lt;acadiso.dwt&gt;:", the file as typed or in the template folder (.dwt where it has no
        /// extension), Enter taking the default template of Global settings or the built-in metric one, acadiso and acad
        /// standing for the built-in metric and imperial templates where no file of the name is found, and a period asking
        /// "Enter measurement [English/Metric] &lt;Metric&gt;:" for a drawing of no template. The drawing is shown once the
        /// command has ended.
        /// </summary>
        private IEnumerable<CadPrompt> NewBody(CadCommandContext c)
        {
            if (c.Session.FileDialogs)
            {
                _afterCommand = () => NewDrawingChoosingTemplate();
                yield break;
            }

            string qnew = _settings.QnewTemplate;
            yield return CadPrompt.ForString(
                "Enter template file name or [. (for none)]",
                qnew.Length > 0 ? Path.GetFileName(qnew) : MetricTemplateName + ".dwt"
            );
            string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim().Trim('"') : string.Empty;

            if (typed == ".")
            {
                yield return CadPrompt.ForKeyword("Enter measurement", new[] { "English", "Metric" }, "Metric");
                bool metric = !c.Input.Is("English");
                _afterCommand = () => NewDrawingWithNoTemplate(metric);
                yield break;
            }

            if (typed.Length == 0)
            {
                _afterCommand = () => NewDrawing();
                yield break;
            }

            string folder = _settings.EffectiveTemplateFolder;
            string file = CadTemplate.Find(typed, folder);

            if (file == null)
            {
                string stem = Path.GetFileNameWithoutExtension(typed);
                bool builtIn = !Path.IsPathRooted(typed) && (typed == stem || typed == stem + ".dwt");

                if (builtIn && string.Equals(stem, MetricTemplateName, StringComparison.OrdinalIgnoreCase))
                {
                    _afterCommand = () => NewDrawingWithNoTemplate(true);
                }
                else if (builtIn && string.Equals(stem, ImperialTemplateName, StringComparison.OrdinalIgnoreCase))
                {
                    _afterCommand = () => NewDrawingWithNoTemplate(false);
                }
                else
                {
                    c.Write(
                        "\""
                            + Path.GetFileName(Path.HasExtension(typed) ? typed : typed + ".dwt")
                            + "\": Can't find file."
                    );
                }

                yield break;
            }

            _afterCommand = () => NewDrawingFrom(file);
        }

        /// <summary>QNEW: a new drawing from the default template of Global settings, the built-in metric one where none is set, with no question asked.</summary>
        private IEnumerable<CadPrompt> QnewBody(CadCommandContext c)
        {
            _afterCommand = () => NewDrawing();
            return Array.Empty<CadPrompt>();
        }

        /// <summary>
        /// Application menu's Save As › Drawing Template: Save Drawing As with the type AutoCAD Drawing Template (*.dwt) chosen
        /// and the template folder of Global settings shown; with no such dialog, SAVEAS typed.
        /// </summary>
        public void SaveAsTemplate()
        {
            if (_current == null)
            {
                return;
            }

            if (SaveAsRequested == null)
            {
                StartCommand("SAVEAS");
                return;
            }

            _saveAsTemplate = true;
            SaveAsRequested(this, EventArgs.Empty);
            _saveAsTemplate = false;
        }

        /// <summary>
        /// The Template Options a drawing saved to a file as a template is given: where the file is the drawing's own (QSAVE of
        /// a template) the description and measurement it has, asking nothing; else the dialog's answer, the description the
        /// drawing has and its measurement offered; null where it was cancelled. With no dialog the offer is taken.
        /// </summary>
        private CadTemplateOptionsRequest AskTemplateOptions(string path)
        {
            CadDocument document = _current.Document;
            var options = new CadTemplateOptionsRequest(
                System.IO.Path.GetFullPath(path),
                document.Database.Measurement == DxfMeasurementValue.Metric
            )
            {
                Description = document.TemplateDescription,
                IsAccepted = true,
            };

            if (
                ShowTemplateOptions == null
                || string.Equals(document.FileName, options.FileName, StringComparison.OrdinalIgnoreCase)
            )
            {
                return options;
            }

            var dialog = new TemplateOptionsViewModel(options);

            if (!ShowTemplateOptions(dialog))
            {
                return null;
            }

            dialog.Accept();
            return options;
        }
    }
}
