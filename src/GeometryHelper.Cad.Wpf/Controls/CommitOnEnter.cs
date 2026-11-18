using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The attached property that makes a box of the ribbon (or of a palette) behave as AutoCAD 2021's: typing a value and
    /// pressing Enter sets it at once, Esc puts the old value back, and leaving the box (Tab or a click) sets it as before.
    /// </summary>
    /// <remarks>
    /// A box's text is bound with <c>UpdateSourceTrigger=LostFocus</c>, so that a half-typed number (the "1" on its way to
    /// "10") never reaches the drawing and one Enter is one undo step; but the keyboard stays in the box after Enter, so
    /// nothing ever left it and the value waited until the maintainer clicked the canvas. With <see cref="EnabledProperty"/>
    /// set, Enter writes the text to its binding source (the view model then changes the drawing and the canvas redraws) and
    /// selects the whole text, keeping the keyboard in the box; Esc writes the source's value back to the text and selects it.
    /// Both keys are handled, so neither reaches the command line or a default button. After either,
    /// <see cref="FinishedEvent"/> bubbles from the box, for the one that wants the keyboard back (the Text Editor tab gives it
    /// to the words being edited). A box with <c>AcceptsReturn</c> (more than one line) is left alone.
    /// </remarks>
    public static class CommitOnEnter
    {
        /// <summary>Identifies the <c>CommitOnEnter.Enabled</c> attached property.</summary>
        public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
            "Enabled",
            typeof(bool),
            typeof(CommitOnEnter),
            new PropertyMetadata(false, OnEnabledChanged)
        );

        /// <summary>Identifies the <c>CommitOnEnter.Finished</c> attached event, raised by a box after Enter or Esc.</summary>
        public static readonly RoutedEvent FinishedEvent = EventManager.RegisterRoutedEvent(
            "Finished",
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(CommitOnEnter)
        );

        /// <summary>Gets whether <paramref name="element"/> sets its value on Enter and restores it on Esc.</summary>
        /// <param name="element">The box.</param>
        /// <returns>True where the behaviour is on.</returns>
        public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

        /// <summary>Sets whether <paramref name="element"/> sets its value on Enter and restores it on Esc.</summary>
        /// <param name="element">The box.</param>
        /// <param name="value">True to turn the behaviour on.</param>
        public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);

        /// <summary>Adds a handler of <see cref="FinishedEvent"/> to <paramref name="element"/>.</summary>
        /// <param name="element">The element above the boxes.</param>
        /// <param name="handler">The handler.</param>
        public static void AddFinishedHandler(DependencyObject element, RoutedEventHandler handler) =>
            (element as UIElement)?.AddHandler(FinishedEvent, handler);

        /// <summary>Removes a handler of <see cref="FinishedEvent"/> from <paramref name="element"/>.</summary>
        /// <param name="element">The element above the boxes.</param>
        /// <param name="handler">The handler.</param>
        public static void RemoveFinishedHandler(DependencyObject element, RoutedEventHandler handler) =>
            (element as UIElement)?.RemoveHandler(FinishedEvent, handler);

        private static void OnEnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is TextBox box)
            {
                box.PreviewKeyDown -= OnKeyDown;

                if ((bool)e.NewValue)
                {
                    box.PreviewKeyDown += OnKeyDown;
                }
            }
        }

        private static void OnKeyDown(object sender, KeyEventArgs e)
        {
            var box = (TextBox)sender;

            if (box.AcceptsReturn || (e.Key != Key.Enter && e.Key != Key.Escape))
            {
                return;
            }

            var binding = box.GetBindingExpression(TextBox.TextProperty);

            if (e.Key == Key.Enter)
            {
                binding?.UpdateSource();
            }
            else
            {
                binding?.UpdateTarget();
            }

            box.SelectAll();
            e.Handled = true;
            box.RaiseEvent(new RoutedEventArgs(FinishedEvent, box));
        }
    }
}
