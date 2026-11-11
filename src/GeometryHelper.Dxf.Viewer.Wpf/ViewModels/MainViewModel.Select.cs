using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Quick Select dialog of QSELECT and the Find and Replace dialog of FIND, each shown through a function the workspace
    /// sets, so that a test drives them with none; with none, each command says the window shows it.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>Gets or sets how the workspace shows Quick Select: true when OK was pressed, the dialog then applied.</summary>
        public Func<QuickSelectViewModel, bool> ShowQuickSelect { get; set; }

        /// <summary>Gets or sets how the workspace shows Find and Replace, which works until it is closed.</summary>
        public Action<FindReplaceViewModel> ShowFindReplace { get; set; }

        /// <summary>
        /// Zooms the view to objects, as Find and Replace's Zoom: the space they are in shown, and the view on the box of what
        /// they draw, with a margin.
        /// </summary>
        /// <param name="handles">The handles; those the document has no entity of are passed over.</param>
        public void ZoomToObjects(IReadOnlyList<string> handles)
        {
            if (_current == null || handles == null)
            {
                return;
            }

            CadDocument document = _current.Document;
            CadSpace space = handles.Select(document.FindSpace).FirstOrDefault(s => s != null);

            if (space == null)
            {
                return;
            }

            if (space != document.CurrentSpace)
            {
                document.CurrentSpace = space;

                foreach (SpaceTabViewModel tab in SpaceTabs)
                {
                    tab.IsActive = tab.Space == space;
                }

                RaiseSpace();
            }

            PickIndex index = document.GetPickIndex(space);
            DisplayBox box = DisplayBox.Empty;

            foreach (string handle in handles)
            {
                foreach (int i in index.IndicesOf(handle))
                {
                    box = box.Union(index.List.Items[i].Bounds);
                }
            }

            if (box.IsEmpty)
            {
                return;
            }

            double margin = Math.Max(Math.Max(box.Width, box.Height) * 0.5, 1.0);
            ViewState state = _current.ViewOf(space, _canvasWidth, _canvasHeight);
            state.View.ZoomTo(box.Inflate(margin));
            state.FitPending = false;
            NotifyViewChangedByUser();
            Revision++;
            RefreshViewGroup();
        }

        /// <summary>QSELECT's request: the dialog shown where the workspace shows it, OK selecting.</summary>
        private void OnQuickSelectRequested(CadUiRequestEventArgs e)
        {
            CadDocument document = e.Document ?? _current?.Document;

            if (document == null || ShowQuickSelect == null)
            {
                return;
            }

            var dialog = new QuickSelectViewModel(document, _current?.Session);

            if (ShowQuickSelect(dialog))
            {
                dialog.Apply();
            }

            e.Handled = true;
        }

        /// <summary>FIND's request: the dialog shown where the workspace shows it.</summary>
        private void OnFindRequested(CadUiRequestEventArgs e)
        {
            CadDocument document = e.Document ?? _current?.Document;

            if (document == null || ShowFindReplace == null)
            {
                return;
            }

            ShowFindReplace(
                new FindReplaceViewModel(document, _current?.Session, e.Payload as IReadOnlyList<string>, ZoomToObjects)
            );
            e.Handled = true;
        }
    }
}
