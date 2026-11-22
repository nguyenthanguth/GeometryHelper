using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// What the View ribbon tab shows of the drawing and the window, as AutoCAD 2021's: the UCS Icon toggle (lit while UCSICON is
    /// on), the named view box (Unsaved View, then the views of the space shown, a pick restoring one as -VIEW Restore does), New
    /// View and Viewport Configuration's Named button (the View Manager and the Viewports dialog opened on the page AutoCAD's
    /// button names) and the launcher of Interface (Global settings on Appearance). The tab binds them through
    /// <see cref="ViewRibbonViewModel.Model"/>.
    /// </summary>
    public sealed partial class MainViewModel
    {
        // The first item of the named view box, which no view of the drawing takes the place of.
        private const string UnsavedView = "Unsaved View";

        private IReadOnlyList<string> _namedViewItems = new[] { UnsavedView };
        private string _selectedNamedView = UnsavedView;

        // What the window showed just after a view was picked, and in which space: the box keeps its pick only while the window
        // still shows that. Null while the pick is being made, and for Unsaved View.
        private DisplayBox? _pickedViewBox;
        private CadSpace _pickedViewSpace;
        private CadDocument _namedViewsOf;
        private int _namedViewsAt = -1;
        private bool _namedViewsInModel;
        private bool _viewTabHooked;

        // What New View and Named leave to the dialog the command is about to show: the View Manager opened on New View, the
        // Viewports dialog opened on its Named Viewports tab. Set for the length of the command's start only.
        private bool _openNewViewFirst;
        private bool _openNamedViewports;

        private DelegateCommand _newViewCommand;
        private DelegateCommand _namedViewportsCommand;
        private DelegateCommand _appearanceSettingsCommand;

        /// <summary>
        /// Gets or sets whether the UCS icon is shown, as UCSICON On and Off say: the UCS Icon toggle of View is lit while it is,
        /// and a press runs UCSICON with the other answer. False with no drawing open.
        /// </summary>
        public bool IsUcsIconOn
        {
            get
            {
                HookViewTab();
                return _current != null && _current.Document.Ucs.IconOn;
            }
            set
            {
                if (_current != null && value != IsUcsIconOn)
                {
                    StartCommandWithOption(value ? "UCSICON ON" : "UCSICON OFF");
                }
            }
        }

        /// <summary>
        /// Gets the items of the named view box: Unsaved View, then the names of the views of the space shown, model views in
        /// model space and layout views in a layout, letters of either case alike, ties by ordinal order. Unsaved View alone with
        /// no drawing open.
        /// </summary>
        public IReadOnlyList<string> NamedViewItems
        {
            get
            {
                HookViewTab();
                ReadNamedViews(false);
                return _namedViewItems;
            }
        }

        /// <summary>
        /// Gets or sets the item of the named view box shown: Unsaved View until a view is picked. Setting a view's name restores it
        /// as -VIEW Restore does, so that the window shows what the view saved; Unsaved View, or a name the box lost, restores
        /// nothing. A zoom or a pan that leaves the window showing another part of the drawing than the view picked puts the box
        /// back on Unsaved View, so that the same view can be picked again.
        /// </summary>
        public string SelectedNamedView
        {
            get => _selectedNamedView;
            set
            {
                string name = string.IsNullOrEmpty(value) ? UnsavedView : value;
                _pickedViewBox = null;

                if (!Set(ref _selectedNamedView, name) || name == UnsavedView || _current == null)
                {
                    return;
                }

                StartCommand("-VIEW");
                Execute("R");
                Execute(name);

                if (_selectedNamedView == name)
                {
                    _pickedViewSpace = Space;
                    _pickedViewBox = View?.VisibleBox;
                }
            }
        }

        /// <summary>
        /// Gets the command of New View on the View tab: VIEW started, the View Manager opening on its New View dialog, as
        /// AutoCAD's NEWVIEW does (the presenter has no command of that name). Its parameter is not read.
        /// </summary>
        public ICommand NewViewCommand
        {
            get
            {
                HookViewTab();
                return _newViewCommand
                    ?? (
                        _newViewCommand = new DelegateCommand(
                            _ =>
                            {
                                _openNewViewFirst = true;

                                try
                                {
                                    StartCommand("VIEW");
                                }
                                finally
                                {
                                    _openNewViewFirst = false;
                                }
                            },
                            _ => HasDocument
                        )
                    );
            }
        }

        /// <summary>
        /// Gets the command of Named on the View tab: VPORTS started, the Viewports dialog opening on its Named Viewports tab.
        /// Its parameter is not read.
        /// </summary>
        public ICommand NamedViewportsCommand
        {
            get
            {
                HookViewTab();
                return _namedViewportsCommand
                    ?? (
                        _namedViewportsCommand = new DelegateCommand(
                            _ =>
                            {
                                _openNamedViewports = true;

                                try
                                {
                                    StartCommand("VPORTS");
                                }
                                finally
                                {
                                    _openNamedViewports = false;
                                }
                            },
                            _ => HasDocument
                        )
                    );
            }
        }

        /// <summary>Gets the command of the launcher of View > Interface: Global settings opened on its Appearance page.</summary>
        public ICommand AppearanceSettingsCommand =>
            _appearanceSettingsCommand
            ?? (_appearanceSettingsCommand = new DelegateCommand(_ => RequestSettings("Appearance")));

        /// <summary>Starts following the window, once: the UCS icon and the views of the drawing change under commands typed too.</summary>
        private void HookViewTab()
        {
            if (!_viewTabHooked)
            {
                _viewTabHooked = true;
                PropertyChanged += OnViewTabPropertyChanged;
            }
        }

        private void OnViewTabPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Revision))
            {
                Raise(nameof(IsUcsIconOn));
                ReadNamedViews(true);
                ForgetPickedViewIfMoved();
            }
            else if (e.PropertyName == nameof(HasDocument))
            {
                _newViewCommand?.RaiseCanExecuteChanged();
                _namedViewportsCommand?.RaiseCanExecuteChanged();
                Raise(nameof(IsUcsIconOn));
                ReadNamedViews(true);
            }
        }

        /// <summary>
        /// Puts the named view box back on Unsaved View where the window no longer shows what the view picked showed: another
        /// space, or a part of it whose centre or size has moved by more than a hundred-thousandth of the larger side. A view the
        /// user has panned or zoomed is then picked again to restore it, which a box still on that view would not do.
        /// </summary>
        private void ForgetPickedViewIfMoved()
        {
            if (!_pickedViewBox.HasValue || _selectedNamedView == UnsavedView)
            {
                return;
            }

            DisplayBox? now = ReferenceEquals(Space, _pickedViewSpace) ? View?.VisibleBox : null;

            if (now.HasValue && SameBox(_pickedViewBox.Value, now.Value))
            {
                return;
            }

            _pickedViewBox = null;
            _selectedNamedView = UnsavedView;
            Raise(nameof(SelectedNamedView));
        }

        /// <summary>Whether a box shows what another shows, within a hundred-thousandth of the larger side: a move under a pixel is none.</summary>
        private static bool SameBox(DisplayBox a, DisplayBox b)
        {
            double tolerance = Tolerance.Default.EqualVector * Math.Max(a.Width, a.Height);
            return Math.Abs(a.MinX - b.MinX) <= tolerance
                && Math.Abs(a.MinY - b.MinY) <= tolerance
                && Math.Abs(a.MaxX - b.MaxX) <= tolerance
                && Math.Abs(a.MaxY - b.MaxY) <= tolerance;
        }

        /// <summary>
        /// Reads the names of the views again where the drawing, its edits or the space shown have changed since they were last read;
        /// a list that comes out the same is left as it is, so that an open box and its pick stay.
        /// </summary>
        /// <param name="raise">Whether to tell the box of a change; the first read of a binding does not need it.</param>
        private void ReadNamedViews(bool raise)
        {
            CadDocument document = Document;
            bool inModel = IsModelSpace;

            if (
                ReferenceEquals(document, _namedViewsOf)
                && DrawingRevision == _namedViewsAt
                && inModel == _namedViewsInModel
            )
            {
                return;
            }

            bool sameDocument = ReferenceEquals(document, _namedViewsOf);
            _namedViewsOf = document;
            _namedViewsAt = DrawingRevision;
            _namedViewsInModel = inModel;
            var names = new List<string>();

            if (document != null)
            {
                foreach (DxfViewTableRecord view in document.Database.ViewTable)
                {
                    if (view.IsPaperspaceView == !inModel)
                    {
                        names.Add(view.Name);
                    }
                }

                names.Sort(
                    (a, b) =>
                    {
                        int order = StringComparer.OrdinalIgnoreCase.Compare(a, b);
                        return order != 0 ? order : string.CompareOrdinal(a, b);
                    }
                );
            }

            names.Insert(0, UnsavedView);

            if (!names.SequenceEqual(_namedViewItems, StringComparer.Ordinal))
            {
                _namedViewItems = names;

                if (raise)
                {
                    Raise(nameof(NamedViewItems));
                }
            }

            // Another drawing, or a view the drawing no longer has, leaves the box on Unsaved View.
            if (_selectedNamedView != UnsavedView && (!sameDocument || !names.Contains(_selectedNamedView)))
            {
                _selectedNamedView = UnsavedView;

                if (raise)
                {
                    Raise(nameof(SelectedNamedView));
                }
            }
        }
    }
}
