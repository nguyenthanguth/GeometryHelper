using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>A row of the LayerWalk list: the layer's name, shown dimmed where nothing is on it.</summary>
    public sealed class LayerWalkRowViewModel
    {
        internal LayerWalkRowViewModel(CadLayerWalkRow row)
        {
            Row = row;
        }

        /// <summary>Gets the row of the request.</summary>
        public CadLayerWalkRow Row { get; }

        /// <summary>Gets the layer's name.</summary>
        public string Name => Row.Name;

        /// <summary>Gets whether anything is on the layer; the list dims a layer that is not used, the one Purge Layers deletes.</summary>
        public bool IsUsed => Row.IsUsed;
    }

    /// <summary>
    /// AutoCAD's LayerWalk dialog over LAYWALK's request: the layers of the drawing, narrowed by the filter box (wild cards), the
    /// ones selected the only ones shown, Select Objects to pick layers on the canvas, Purge Layers for the unused ones and
    /// Restore on exit to put every layer back as it was when it closes.
    /// </summary>
    public sealed class LayerWalkViewModel : ObservableObject
    {
        private string _message = string.Empty;

        /// <summary>Makes the dialog's logic over a request.</summary>
        /// <param name="request">The request of LAYWALK.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public LayerWalkViewModel(CadLayerWalkRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Refill();
        }

        /// <summary>Gets the request.</summary>
        public CadLayerWalkRequest Request { get; }

        /// <summary>Gets the layers listed: those the filter lets through.</summary>
        public ObservableCollection<LayerWalkRowViewModel> Rows { get; } =
            new ObservableCollection<LayerWalkRowViewModel>();

        /// <summary>Gets or sets the filter, a wild-card pattern such as <c>WALL*</c>; empty lists every layer.</summary>
        public string Filter
        {
            get => Request.Filter;
            set
            {
                if (!string.Equals(Request.Filter, (value ?? string.Empty).Trim(), StringComparison.Ordinal))
                {
                    Request.Filter = value;
                    Raise(nameof(Filter));
                    Refill();
                }
            }
        }

        /// <summary>Gets or sets whether closing the dialog puts the layers back as they were.</summary>
        public bool RestoreOnExit
        {
            get => Request.RestoreOnExit;
            set
            {
                if (Request.RestoreOnExit != value)
                {
                    Request.RestoreOnExit = value;
                    Raise(nameof(RestoreOnExit));
                }
            }
        }

        /// <summary>Gets what the last change said where it was refused, or what Purge Layers did; empty for nothing.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        /// <summary>Gets the line under the list: how many layers are listed and how many are walked.</summary>
        public string Summary =>
            Rows.Count.ToString(CultureInfo.InvariantCulture)
            + (Rows.Count == 1 ? " layer" : " layers")
            + ", "
            + Request.Selected.Count.ToString(CultureInfo.InvariantCulture)
            + " selected";

        /// <summary>Gets the names of the layers walked, which the window selects again when it is shown after Select Objects.</summary>
        public IReadOnlyList<string> Selected => Request.Selected;

        /// <summary>The list selected: the layers named are the only ones shown, none selected shows every layer as it was.</summary>
        /// <param name="names">The names of the rows selected.</param>
        public void Select(IReadOnlyCollection<string> names)
        {
            Message = Request.Walk(names) ? string.Empty : Request.Message;
            Raise(nameof(Summary));
        }

        /// <summary>Select objects: closes the dialog for the command to ask on the canvas, and show it again with their layers selected.</summary>
        public void SelectObjects() => Request.Action = CadDialogAction.SelectObjects;

        /// <summary>Close: the dialog closes, the layers put back where Restore on exit is on.</summary>
        public void Close() => Request.Action = CadDialogAction.Cancel;

        /// <summary>Purge Layers: deletes the unused layers among those selected, and says how many.</summary>
        /// <param name="names">The names of the rows selected.</param>
        /// <returns>How many layers were deleted.</returns>
        public int Purge(IReadOnlyCollection<string> names)
        {
            IReadOnlyList<string> purged = Request.Purge(names);
            Refill();
            Message =
                purged.Count == 0 ? "No unused layer is selected."
                : purged.Count == 1 ? "1 layer purged."
                : purged.Count.ToString(CultureInfo.InvariantCulture) + " layers purged.";
            return purged.Count;
        }

        /// <summary>Raised after the list is made again, for the window to select the layers walked in it once more.</summary>
        public event EventHandler Refilled;

        /// <summary>Gets whether the list is being made again, so that the selection it loses is not a change of the walk.</summary>
        public bool IsRefilling { get; private set; }

        private void Refill()
        {
            IsRefilling = true;

            try
            {
                Rows.Clear();

                foreach (CadLayerWalkRow row in Request.Listed)
                {
                    Rows.Add(new LayerWalkRowViewModel(row));
                }
            }
            finally
            {
                IsRefilling = false;
            }

            Raise(nameof(Summary));
            Refilled?.Invoke(this, EventArgs.Empty);
        }
    }
}
