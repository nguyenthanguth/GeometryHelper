using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD 2021's Insert Layout(s) dialog over LAYOUT Template's request: the template's layouts in the order of its tabs,
    /// several picked at once, the first picked to start with. OK gives the request the names picked, in the list's order; OK
    /// with none picked brings nothing in.
    /// </summary>
    public sealed class InsertLayoutViewModel
    {
        private readonly CadInsertLayoutRequest _request;

        /// <summary>Makes the dialog over LAYOUT Template's request.</summary>
        /// <param name="request">The request.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public InsertLayoutViewModel(CadInsertLayoutRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            Layouts = request.LayoutNames.Select(n => new ObjectScaleChoiceViewModel(n)).ToList();

            if (Layouts.Count > 0)
            {
                Layouts[0].IsPicked = true;
            }
        }

        /// <summary>Gets the template file the layouts are read from.</summary>
        public string FileName => _request.FileName;

        /// <summary>Gets the template's layouts, each picked or not.</summary>
        public IReadOnlyList<ObjectScaleChoiceViewModel> Layouts { get; }

        /// <summary>Gets the names of the layouts picked, in the order of the list.</summary>
        public IReadOnlyList<string> PickedNames => Layouts.Where(l => l.IsPicked).Select(l => l.Name).ToList();

        /// <summary>Gives the request the layouts picked, as OK does.</summary>
        public void Accept()
        {
            _request.Picked.Clear();

            foreach (string name in PickedNames)
            {
                _request.Picked.Add(name);
            }

            _request.IsAccepted = true;
        }
    }

    /// <summary>
    /// AutoCAD 2021's Template Options dialog over LAYOUT SAveas's request for a .dwt: the description, the measurement,
    /// English or Metric, and New Layer Notification, shown greyed since the presenter keeps no reconciled layers. OK gives the
    /// request the description and the measurement.
    /// </summary>
    public sealed class TemplateOptionsViewModel : ObservableObject
    {
        /// <summary>The word of an English measurement, MEASUREMENT 0.</summary>
        public const string English = "English";

        /// <summary>The word of a metric measurement, MEASUREMENT 1.</summary>
        public const string Metric = "Metric";

        private readonly CadTemplateOptionsRequest _request;
        private string _description;
        private string _measurement;

        /// <summary>Makes the dialog over LAYOUT SAveas's request.</summary>
        /// <param name="request">The request.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public TemplateOptionsViewModel(CadTemplateOptionsRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _description = request.Description ?? string.Empty;
            _measurement = request.IsMetric ? Metric : English;
        }

        /// <summary>Gets the template file about to be written.</summary>
        public string FileName => _request.FileName;

        /// <summary>Gets or sets the description.</summary>
        public string Description
        {
            get => _description;
            set => Set(ref _description, value ?? string.Empty);
        }

        /// <summary>Gets the measurements listed: English and Metric.</summary>
        public IReadOnlyList<string> Measurements { get; } = new[] { English, Metric };

        /// <summary>Gets or sets the measurement chosen: <see cref="English"/> or <see cref="Metric"/>.</summary>
        public string Measurement
        {
            get => _measurement;
            set => Set(ref _measurement, value == Metric ? Metric : English);
        }

        /// <summary>Gives the request the description and the measurement, as OK does.</summary>
        public void Accept()
        {
            _request.Description = _description.Trim();
            _request.IsMetric = _measurement == Metric;
            _request.IsAccepted = true;
        }
    }
}
