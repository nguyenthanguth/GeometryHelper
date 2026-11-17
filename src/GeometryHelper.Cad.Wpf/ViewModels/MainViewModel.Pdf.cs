using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// PDF underlays selected on the canvas: the contextual PDF Underlay tab, shown while the objects selected are underlays
    /// of PDFs alone and no command runs, as AutoCAD 2021 shows it, with the values of the underlays to change in it
    /// (<see cref="PdfTabViewModel"/>).
    /// </summary>
    public sealed partial class MainViewModel
    {
        private CadPdfTab _pdfTab;
        private PdfTabViewModel _pdfs;

        /// <summary>Gets the contextual PDF Underlay tab, shown while PDF underlays are selected.</summary>
        public CadWorkspaceExtension PdfTab
        {
            get
            {
                if (_pdfTab == null)
                {
                    _pdfTab = new CadPdfTab();
                    _builtInTabs.Add(_pdfTab);
                }

                return _pdfTab;
            }
        }

        /// <summary>Gets the PDF underlays selected now and what the PDF Underlay tab does to them; null where none are.</summary>
        public PdfTabViewModel PdfEditor => _pdfs;

        /// <summary>
        /// Shows the PDF Underlay tab where the objects selected are PDF underlays alone and no command runs, and keeps it in
        /// step with the drawing; hides it where they are not.
        /// </summary>
        /// <param name="handles">The handles of the objects selected, in ordinal order.</param>
        private void SyncPdfTab(IReadOnlyList<string> handles)
        {
            if (
                _current == null
                || _current.Session.IsRunning
                || handles == null
                || handles.Count == 0
                || handles.Any(h => !(_current.Document.FindEntity(h) is DxfPdfUnderlay))
            )
            {
                HidePdfTab();
                return;
            }

            if (_pdfs != null && _pdfs.Handles.SequenceEqual(handles, StringComparer.OrdinalIgnoreCase))
            {
                _pdfs.Refresh();
                return;
            }

            _pdfs = new PdfTabViewModel(this, handles);
            ((CadPdfTab)PdfTab).Show(_pdfs);
            SelectedTab = PdfTab;
            Raise(nameof(PdfEditor));
        }

        /// <summary>Hides the PDF Underlay tab.</summary>
        private void HidePdfTab()
        {
            if (_pdfs == null)
            {
                return;
            }

            _pdfs = null;

            if (_pdfTab != null && _pdfTab.IsTabVisible)
            {
                _pdfTab.Hide();

                if (SelectedTab == _pdfTab || SelectedTab == null)
                {
                    SelectedTab = null;
                }
            }

            Raise(nameof(PdfEditor));
        }
    }

    /// <summary>The contextual PDF Underlay tab, in green as a contextual tab, shown while PDF underlays are selected.</summary>
    internal sealed class CadPdfTab : CadWorkspaceExtension
    {
        public override string TabHeader => "PDF Underlay";

        public void Show(PdfTabViewModel pdfs)
        {
            TabContent = pdfs;
            IsTabVisible = true;
        }

        public void Hide()
        {
            IsTabVisible = false;
            TabContent = null;
        }
    }
}
