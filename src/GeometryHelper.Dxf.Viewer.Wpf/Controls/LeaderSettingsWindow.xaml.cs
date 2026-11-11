using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Leader Settings, which QLEADER's Settings shows: Annotation, Leader Line &amp; Arrow and Attachment. User
    /// Arrow... of the arrowhead list opens <see cref="SelectArrowBlockWindow"/> over it, the dialog dimmed under it. Its logic
    /// is <see cref="LeaderSettingsViewModel"/>.
    /// </summary>
    public partial class LeaderSettingsWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public LeaderSettingsWindow(LeaderSettingsViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            model.PropertyChanged += OnModelChanged;
            Closed += (sender, e) => model.PropertyChanged -= OnModelChanged;
        }

        /// <summary>Gets the view model.</summary>
        public LeaderSettingsViewModel Model { get; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>Shows the dialog over a window; the action it closed with is in the request.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True: the request is answered.</returns>
        public static bool Show(Window owner, LeaderSettingsViewModel model)
        {
            new LeaderSettingsWindow(model) { Owner = owner }.ShowDialog();
            return true;
        }

        /// <summary>User Arrow... picked: Select Custom Arrow Block shown once the list has closed, the dialog dimmed and blocked under it.</summary>
        private void OnModelChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(LeaderSettingsViewModel.IsChoosingUserArrow) || !Model.IsChoosingUserArrow)
            {
                return;
            }

            Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(() =>
                {
                    if (!Model.IsChoosingUserArrow || !IsVisible)
                    {
                        return;
                    }

                    using (DialogDim.Over(Root))
                    {
                        SelectArrowBlockWindow.Show(this, Model);
                    }
                })
            );
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnMorePoints(object sender, RoutedEventArgs e) => Model.MorePoints();

        private void OnFewerPoints(object sender, RoutedEventArgs e) => Model.FewerPoints();

        private void OnOk(object sender, RoutedEventArgs e)
        {
            // A box being typed in gives its text before the settings are taken.
            (Keyboard.FocusedElement as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

            if (Model.Ok())
            {
                DialogResult = true;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Model.Cancel();
            DialogResult = false;
        }
    }
}
