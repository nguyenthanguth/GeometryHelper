using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>One line of AutoCAD's PDF Layers dialog: a layer of the PDF and whether it is on.</summary>
    public sealed class PdfLayerRowViewModel : ObservableObject
    {
        private bool _isOn;

        internal PdfLayerRowViewModel(string name, bool isOn)
        {
            Name = name;
            _isOn = isOn;
        }

        /// <summary>Gets the name of the layer, as the PDF gives it.</summary>
        public string Name { get; }

        /// <summary>Gets or sets whether the layer is on: its content is drawn.</summary>
        public bool IsOn
        {
            get => _isOn;
            set
            {
                if (Set(ref _isOn, value))
                {
                    Raise(nameof(State));
                }
            }
        }

        /// <summary>Gets the state in words, On or Off, as the dialog's State column says it.</summary>
        public string State => _isOn ? "On" : "Off";
    }

    /// <summary>
    /// AutoCAD's PDF Layers dialog, which PDFLAYERS shows over the underlay picked: the layers of the PDF in its order, each
    /// switched on or off for this underlay. OK writes the layers that differ from the state the PDF opens with into the underlay.
    /// </summary>
    public sealed class PdfLayersViewModel : ObservableObject
    {
        private readonly CadPdfLayersRequest _request;

        /// <summary>Makes the dialog over the request of PDFLAYERS.</summary>
        /// <param name="request">The request.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public PdfLayersViewModel(CadPdfLayersRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            Rows = request.LayerNames.Select(name => new PdfLayerRowViewModel(name, request.IsOn(name))).ToList();
        }

        /// <summary>Gets the layers of the PDF.</summary>
        public IReadOnlyList<PdfLayerRowViewModel> Rows { get; }

        /// <summary>Gets the title: PDF Layers.</summary>
        public string Title => "PDF Layers";

        /// <summary>Gets the line under the title: the underlay's name.</summary>
        public string Caption => _request.UnderlayName;

        /// <summary>Switches every layer on or off.</summary>
        /// <param name="on">Whether they are to be on.</param>
        public void SetAll(bool on)
        {
            foreach (PdfLayerRowViewModel row in Rows)
            {
                row.IsOn = on;
            }
        }

        /// <summary>Writes what the dialog holds into the request and says what it was closed with.</summary>
        /// <param name="action">OK or Cancel.</param>
        public void Close(CadDialogAction action)
        {
            if (action == CadDialogAction.Ok)
            {
                _request.Overrides.Clear();

                foreach (PdfLayerRowViewModel row in Rows)
                {
                    if (row.IsOn != _request.IsOnByDefault(row.Name))
                    {
                        _request.Overrides[row.Name] = row.IsOn;
                    }
                }
            }

            _request.Action = action;
        }
    }
}
