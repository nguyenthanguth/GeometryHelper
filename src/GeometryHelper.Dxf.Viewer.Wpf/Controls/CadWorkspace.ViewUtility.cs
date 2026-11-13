using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// Batch 4 of acad.pgp in the workspace: the Viewports, Object Selection Filters and QuickCalc windows, the tiles of model
    /// space laid out round the canvas, and the Quick Properties panel near the cursor.
    /// </summary>
    public partial class CadWorkspace
    {
        private QuickPropertiesWindow _quickProperties;

        /// <summary>Gives the view model the windows of batch 4, and follows its tiles and its Quick Properties panel.</summary>
        private void AttachViewUtility()
        {
            MainViewModel model = _model;
            model.ShowViewports = dialog => ViewportsWindow.Show(Window.GetWindow(this), dialog);
            model.ShowSelectionFilters = dialog => SelectionFiltersWindow.Show(Window.GetWindow(this), dialog);
            model.ShowQuickCalc = palette => QuickCalcWindow.ShowPalette(Window.GetWindow(this), palette);

            // Round 2: Rename, Lineweight Settings, Check Spelling and Boundary Creation.
            model.ShowRename = dialog => RenameWindow.Show(Window.GetWindow(this), dialog);
            model.ShowLineweightSettings = dialog => LineweightSettingsWindow.Show(Window.GetWindow(this), dialog);
            model.ShowCheckSpelling = dialog => CheckSpellingWindow.Show(Window.GetWindow(this), dialog);
            model.ShowBoundaryCreation = dialog => BoundaryCreationWindow.Show(Window.GetWindow(this), dialog);
            TilesView.Attach(model, CanvasView.Options);
            model.TilesChanged += (s, e) => LayOutTiles();
            model.PropertyChanged += OnViewUtilityChanged;
            TilesView.SizeChanged += (s, e) => LayOutTiles();
            LayOutTiles();
        }

        private void OnViewUtilityChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.Document):
                case nameof(MainViewModel.Space):
                    LayOutTiles();
                    break;
                case nameof(MainViewModel.Revision):
                    // The other tiles show the drawing too: drawn again once the canvas has.
                    if (_model?.ModelTiles?.IsTiled == true)
                    {
                        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(TilesView.Refresh));
                    }

                    break;
                case nameof(MainViewModel.IsQuickPropertiesShown):
                    ShowQuickProperties(_model.IsQuickPropertiesShown);
                    break;
            }
        }

        /// <summary>The canvas laid over the current tile, the other tiles drawn behind it; the whole area where model space is one tile.</summary>
        private void LayOutTiles()
        {
            Rect place = TilesView.CurrentPlace;
            ActiveTile.Margin =
                place.IsEmpty || TilesView.ActualWidth <= 0
                    ? new Thickness(0)
                    : new Thickness(
                        place.Left,
                        place.Top,
                        Math.Max(0, TilesView.ActualWidth - place.Right),
                        Math.Max(0, TilesView.ActualHeight - place.Bottom)
                    );
            TilesView.Refresh();
        }

        /// <summary>The Quick Properties panel shown up and right of the cursor, as AutoCAD's QPLOCATION 0 places it, or hidden.</summary>
        private void ShowQuickProperties(bool shown)
        {
            Window owner = Window.GetWindow(this);

            if (!shown || owner == null)
            {
                _quickProperties?.Hide();
                return;
            }

            if (_quickProperties == null)
            {
                _quickProperties = new QuickPropertiesWindow(_model) { Owner = owner };
            }

            Point cursor = PointToScreen(Mouse.GetPosition(this));
            PresentationSource source = PresentationSource.FromVisual(this);
            Matrix toDip = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            Point at = toDip.Transform(cursor);
            _quickProperties.Left = at.X + 30;
            _quickProperties.Top = at.Y - 90;
            _quickProperties.Show();
        }
    }
}
