using System;
using System.Globalization;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Inspection Dimension dialog, which DIMINSPECT shows, over the request its command hands the window: the shape
    /// of the frame (Round, Angular or None), the label and the inspection rate, and Remove Inspection where a dimension
    /// selected has one. A label left empty shows none; a rate left empty is 100%. OK writes the three into every dimension
    /// selected.
    /// </summary>
    public sealed class DimInspectViewModel : ObservableObject
    {
        /// <summary>Makes the dialog over a request.</summary>
        /// <param name="request">The request of DIMINSPECT.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public DimInspectViewModel(CadDimInspectRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
        }

        /// <summary>Gets the request.</summary>
        public CadDimInspectRequest Request { get; }

        /// <summary>Gets the title of the window.</summary>
        public string WindowTitle => "Inspection Dimension";

        /// <summary>Gets what the dialog says it is for: "1 dimension selected." or "3 dimensions selected."</summary>
        public string Selected =>
            Request.DimensionCount.ToString(CultureInfo.InvariantCulture)
            + (Request.DimensionCount == 1 ? " dimension selected." : " dimensions selected.");

        /// <summary>Gets whether Remove Inspection is offered: a dimension selected has an inspection.</summary>
        public bool CanRemove => Request.HasInspection;

        /// <summary>Gets or sets whether the frame has round ends.</summary>
        public bool IsRound
        {
            get => Request.Shape == CadInspectionShape.Round;
            set => Choose(value, CadInspectionShape.Round);
        }

        /// <summary>Gets or sets whether the frame has pointed ends.</summary>
        public bool IsAngular
        {
            get => Request.Shape == CadInspectionShape.Angular;
            set => Choose(value, CadInspectionShape.Angular);
        }

        /// <summary>Gets or sets whether there is no frame.</summary>
        public bool IsNone
        {
            get => Request.Shape == CadInspectionShape.None;
            set => Choose(value, CadInspectionShape.None);
        }

        /// <summary>Gets or sets the label; empty for none.</summary>
        public string Label
        {
            get => Request.Label;
            set
            {
                Request.Label = value ?? string.Empty;
                Raise(nameof(Label));
            }
        }

        /// <summary>Gets or sets the inspection rate as the words shown: 100% where empty.</summary>
        public string Rate
        {
            get => Request.Rate;
            set
            {
                Request.Rate = value ?? string.Empty;
                Raise(nameof(Rate));
            }
        }

        /// <summary>OK: the request is answered OK; a label that has words is shown, and an empty rate is 100%.</summary>
        /// <returns>True: the dialog may close.</returns>
        public bool Accept()
        {
            Request.Label = (Request.Label ?? string.Empty).Trim();
            Request.ShowLabel = Request.Label.Length > 0;
            Request.Rate = string.IsNullOrWhiteSpace(Request.Rate) ? "100%" : Request.Rate.Trim();
            Request.Action = CadDialogAction.Ok;
            return true;
        }

        /// <summary>Remove Inspection: the request answered OK with the inspection of the dimensions to be taken out.</summary>
        public void RemoveInspection()
        {
            Request.Remove = true;
            Request.Action = CadDialogAction.Ok;
        }

        /// <summary>Cancel: the request is answered Cancel and nothing is written.</summary>
        public void Cancel() => Request.Action = CadDialogAction.Cancel;

        private void Choose(bool chosen, CadInspectionShape shape)
        {
            if (!chosen || Request.Shape == shape)
            {
                return;
            }

            Request.Shape = shape;
            Raise(nameof(IsRound));
            Raise(nameof(IsAngular));
            Raise(nameof(IsNone));
        }
    }
}
