using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The MTEXT editor's Paragraph dialog, as AutoCAD 2021's and the design draw it: tab stops, indents, alignment,
    /// paragraph spacing and line spacing. Its logic is <see cref="ParagraphViewModel"/>.
    /// </summary>
    public partial class ParagraphWindow : Window
    {
        /// <summary>Makes the dialog over its view model.</summary>
        /// <param name="model">The view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public ParagraphWindow(ParagraphViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            DataContext = model;
            DialogFault.Follow(this, model, () => model.FaultBox);
            Loaded += (s, e) => PositionBox.Focus();
        }

        /// <summary>Gets the view model.</summary>
        public ParagraphViewModel Model { get; }

        /// <summary>Shows the dialog over a window.</summary>
        /// <param name="owner">The window it is shown over; null for none.</param>
        /// <param name="model">The view model.</param>
        /// <returns>True for OK.</returns>
        public static bool Show(Window owner, ParagraphViewModel model) =>
            new ParagraphWindow(model) { Owner = owner }.ShowDialog() == true;

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (Model.Ok())
            {
                DialogResult = true;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

        /// <summary>Enter in the position box is Add.</summary>
        private void OnPositionKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PositionBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                Model.AddCommand.Execute(null);
                e.Handled = true;
            }
        }

        /// <summary>Delete in the list is Remove.</summary>
        private void OnStopKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                Model.RemoveCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    /// <summary>The box a dialog refused a value of: a red border and the caret, until another is at fault.</summary>
    internal static class DialogFault
    {
        private static readonly Brush Red = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x7A)));

        /// <summary>Marks the box whose Tag is the model's fault box each time it changes.</summary>
        internal static void Follow(Window window, INotifyPropertyChanged model, Func<string> fault)
        {
            model.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "FaultBox")
                {
                    Mark(window, fault());
                }
            };
        }

        private static void Mark(DependencyObject root, string box)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);

                if (child is TextBox text && text.Tag is string tag)
                {
                    if (tag == box)
                    {
                        text.BorderBrush = Red;
                        text.Focus();
                        text.SelectAll();
                    }
                    else
                    {
                        text.ClearValue(Control.BorderBrushProperty);
                    }
                }

                Mark(child, box);
            }
        }

        private static Brush Freeze(Brush brush)
        {
            brush.Freeze();
            return brush;
        }
    }
}
