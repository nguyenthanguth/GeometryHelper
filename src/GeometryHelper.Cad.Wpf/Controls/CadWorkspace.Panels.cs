using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The panels beside the canvas: DRAWINGS (with an extension's panel on its canvas side) and PROPERTIES, each docked left
    /// or right and as wide as the settings say. Dragging a panel's title to the other half of the window docks it there;
    /// dragging its inner edge makes it wider or narrower, the canvas kept 400 pixels wide at least. Both are kept in the
    /// settings, which the window saves, so the panels come back where they were left.
    /// </summary>
    public partial class CadWorkspace
    {
        private const double CanvasMinWidth = 400.0;
        private const double TitleDragDistance = 8.0;
        private FrameworkElement _titleHeld;
        private Point _titleFrom;
        private bool _titleDragging;

        /// <summary>Docks the panels on the sides the settings say and gives them their widths.</summary>
        private void ApplyPanelLayout()
        {
            if (_model == null)
            {
                return;
            }

            GlobalSettings settings = _model.Settings;
            FileListPanel.Width = settings.FileListWidth;
            PropertiesPanel.Width = settings.PropertiesWidth;
            SidePanel.Width = settings.SidePanelWidth;

            Dock files = settings.FileListOnRight ? Dock.Right : Dock.Left;
            DockPanel.SetDock(FileListDock, files);
            DockPanel.SetDock(SidePanelDock, files);
            DockPanel.SetDock(PropertiesDock, settings.PropertiesOnLeft ? Dock.Left : Dock.Right);
            Face(FileListPanel, FileListEdge, !settings.FileListOnRight);
            Face(SidePanel, SidePanelEdge, !settings.FileListOnRight);
            Face(PropertiesPanel, PropertiesEdge, settings.PropertiesOnLeft);
        }

        /// <summary>A panel docked left has its border and its edge on its right, toward the canvas; one docked right on its left.</summary>
        private static void Face(Border panel, Thumb edge, bool onLeft)
        {
            panel.BorderThickness = onLeft ? new Thickness(0, 0, 1, 0) : new Thickness(1, 0, 0, 0);
            edge.HorizontalAlignment = onLeft ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }

        private Border PanelOf(object tag)
        {
            switch (tag as string)
            {
                case "FileList":
                    return FileListPanel;
                case "SidePanel":
                    return SidePanel;
                default:
                    return PropertiesPanel;
            }
        }

        /// <summary>
        /// An inner edge dragged: the panel is made wider or narrower, DRAWINGS and PROPERTIES between 180 and 600 pixels and
        /// an extension's panel between 160 and 1 200, none growing past what leaves the canvas 400.
        /// </summary>
        private void OnPanelEdgeDrag(object sender, DragDeltaEventArgs e)
        {
            var edge = (Thumb)sender;
            Border panel = PanelOf(edge.Tag);
            double grow =
                edge.HorizontalAlignment == HorizontalAlignment.Right ? e.HorizontalChange : -e.HorizontalChange;

            if (grow > 0.0)
            {
                grow = Math.Min(grow, Math.Max(0.0, DockArea.ActualWidth - DockedWidth() - CanvasMinWidth));
            }

            double min = panel == SidePanel ? 160.0 : 180.0;
            double max = panel == SidePanel ? 1200.0 : 600.0;
            panel.Width = Math.Max(min, Math.Min(max, panel.Width + grow));
        }

        private double DockedWidth() => Shown(FileListDock) + Shown(SidePanelDock) + Shown(PropertiesDock);

        private static double Shown(FrameworkElement dock) =>
            dock.Visibility == Visibility.Visible ? dock.ActualWidth : 0.0;

        /// <summary>An edge let go: the widths are kept in the settings.</summary>
        private void OnPanelEdgeDone(object sender, DragCompletedEventArgs e)
        {
            if (_model != null)
            {
                KeepPanelLayout(_model.Settings.FileListOnRight, _model.Settings.PropertiesOnLeft);
            }
        }

        private void KeepPanelLayout(bool fileListOnRight, bool propertiesOnLeft) =>
            _model.KeepPanelLayout(
                FileListPanel.Width,
                PropertiesPanel.Width,
                SidePanel.Width,
                fileListOnRight,
                propertiesOnLeft
            );

        /// <summary>A panel's title pressed: held, to be dragged to a side.</summary>
        private void OnPanelTitleDown(object sender, MouseButtonEventArgs e)
        {
            var title = (FrameworkElement)sender;

            if (title.CaptureMouse())
            {
                _titleHeld = title;
                _titleFrom = e.GetPosition(DockArea);
                _titleDragging = false;
                e.Handled = true;
            }
        }

        /// <summary>A held title moved: past 8 pixels the side it would dock on is shown.</summary>
        private void OnPanelTitleMove(object sender, MouseEventArgs e)
        {
            if (_titleHeld == null || !ReferenceEquals(sender, _titleHeld))
            {
                return;
            }

            Point at = e.GetPosition(DockArea);

            if (
                !_titleDragging
                && Math.Abs(at.X - _titleFrom.X) < TitleDragDistance
                && Math.Abs(at.Y - _titleFrom.Y) < TitleDragDistance
            )
            {
                return;
            }

            _titleDragging = true;
            DockHint.Width = PanelOf(_titleHeld.Tag).Width;
            DockHint.HorizontalAlignment = OnRightHalf(at) ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            DockHint.Visibility = Visibility.Visible;
        }

        /// <summary>A held title let go: dropped on the right half of the window it docks right, on the left half left.</summary>
        private void OnPanelTitleUp(object sender, MouseButtonEventArgs e)
        {
            if (_titleHeld == null || !ReferenceEquals(sender, _titleHeld))
            {
                return;
            }

            FrameworkElement title = _titleHeld;
            bool dropped = _titleDragging;
            bool right = OnRightHalf(e.GetPosition(DockArea));
            title.ReleaseMouseCapture();
            EndTitleDrag();
            e.Handled = true;

            if (!dropped || _model == null)
            {
                return;
            }

            GlobalSettings settings = _model.Settings;

            if (Equals(title.Tag, "FileList"))
            {
                KeepPanelLayout(right, settings.PropertiesOnLeft);
            }
            else
            {
                KeepPanelLayout(settings.FileListOnRight, !right);
            }
        }

        private void OnPanelTitleLost(object sender, MouseEventArgs e)
        {
            if (ReferenceEquals(sender, _titleHeld))
            {
                EndTitleDrag();
            }
        }

        private void EndTitleDrag()
        {
            _titleHeld = null;
            _titleDragging = false;
            DockHint.Visibility = Visibility.Collapsed;
        }

        private bool OnRightHalf(Point at) => at.X >= DockArea.ActualWidth / 2.0;
    }
}
