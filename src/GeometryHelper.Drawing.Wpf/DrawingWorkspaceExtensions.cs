using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using GeometryHelper.Dxf.Viewer.Wpf;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>
    /// The hook that puts the model drawing (Part B) into the presenter's window (Part A) through its extension seam: the
    /// Drawing ribbon tab, the commands, the file list and Properties, and the dialogs shown over the window.
    /// </summary>
    /// <remarks>
    /// <code>
    /// var model = new MainViewModel();
    /// var window = new MainWindow(model);
    /// DrawingWorkspace drawings = window.WorkspaceView.AddModelDrawings(model, DrawingWorkspaceExtensions.DefaultSources(Tolerance.Default), Tolerance.Default);
    /// </code>
    /// </remarks>
    public static class DrawingWorkspaceExtensions
    {
        /// <summary>
        /// Adds the model drawing to a presenter's workspace: a <see cref="DrawingWorkspace"/> added to its view model, the
        /// Drawing tab's groups as its ribbon content, and each dialog it asks for shown over the workspace's window once the
        /// command that asked has ended.
        /// </summary>
        /// <param name="workspace">The workspace control.</param>
        /// <param name="model">Its view model.</param>
        /// <param name="sources">The sources drawings are made from, in the order the New drawing dialog lists them.</param>
        /// <param name="tolerance">The tolerance drawings are made within.</param>
        /// <returns>The model drawing.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the workspace, the view model or the sources are null.</exception>
        public static DrawingWorkspace AddModelDrawings(this CadWorkspace workspace, MainViewModel model, IEnumerable<IDrawingSourceProvider> sources, Tolerance tolerance)
        {
            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            var drawings = new DrawingWorkspace(model, sources, tolerance);
            drawings.TabContent = new DrawingRibbonTab { DataContext = drawings };
            drawings.Deferrer = work => workspace.Dispatcher.BeginInvoke(DispatcherPriority.Normal, work);
            drawings.DialogRequested += (s, e) => workspace.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => Show(workspace, e.Model)));
            return drawings;
        }

        /// <summary>
        /// Gives the sources the standalone app offers: the test bodies, the test bodies changed (to try Update from model),
        /// and Tekla Structures and IFC files, not connected.
        /// </summary>
        /// <param name="tolerance">The tolerance the test bodies are built within.</param>
        /// <returns>The sources.</returns>
        public static IReadOnlyList<IDrawingSourceProvider> DefaultSources(Tolerance tolerance) => new IDrawingSourceProvider[]
        {
            new TestBodiesSourceProvider(tolerance),
            new TestBodiesSourceProvider(tolerance, changed: true),
            UnconnectedSourceProvider.TeklaStructures,
            UnconnectedSourceProvider.IfcFile,
        };

        /// <summary>Makes the window of a dialog's view model, not shown.</summary>
        /// <param name="model">The view model: of New drawing, Update from model, Drawing settings, Export or a message.</param>
        /// <returns>The window.</returns>
        /// <exception cref="ArgumentException">Thrown when the view model is of no dialog known.</exception>
        public static DrawingDialogWindow CreateDialog(DialogViewModel model)
        {
            switch (model)
            {
                case NewDrawingViewModel newDrawing:
                    return new NewDrawingDialog(newDrawing);
                case UpdateFromModelViewModel update:
                    return new UpdateFromModelDialog(update);
                case DrawingSettingsViewModel settings:
                    return new DrawingSettingsDialog(settings);
                case ExportViewModel export:
                    return new ExportDialog(export);
                case MessageViewModel message:
                    return new MessageDialog(message);
                default:
                    throw new ArgumentException("No dialog shows a " + (model?.GetType().Name ?? "null") + ".", nameof(model));
            }
        }

        private static void Show(CadWorkspace workspace, DialogViewModel model)
        {
            DrawingDialogWindow dialog = CreateDialog(model);
            Window owner = Window.GetWindow(workspace);

            if (owner != null && owner.IsVisible)
            {
                dialog.Owner = owner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            if (model is UpdateFromModelViewModel update)
            {
                _ = update.PreviewAsync();
            }

            dialog.ShowDialog();
        }
    }
}
