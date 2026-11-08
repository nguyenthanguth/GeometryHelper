using System.Windows;
using System.Windows.Controls.Primitives;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>
    /// A chip of a choice, as the presets Part, Assembly and General: checked while its content is the value chosen. A click
    /// runs its command and does not check it by itself, so that the chip shows what the view model chose.
    /// </summary>
    public sealed class ChoiceChip : ToggleButton
    {
        /// <summary>Identifies the <see cref="Chosen"/> property.</summary>
        public static readonly DependencyProperty ChosenProperty = DependencyProperty.Register(
            nameof(Chosen), typeof(object), typeof(ChoiceChip), new PropertyMetadata(null, (d, e) => ((ChoiceChip)d).Sync()));

        /// <summary>Gets or sets the value chosen; the chip is checked while its content equals it.</summary>
        public object Chosen
        {
            get => GetValue(ChosenProperty);
            set => SetValue(ChosenProperty, value);
        }

        /// <inheritdoc/>
        protected override void OnContentChanged(object oldContent, object newContent)
        {
            base.OnContentChanged(oldContent, newContent);
            Sync();
        }

        /// <summary>Does not check the chip: the command chooses, and <see cref="Chosen"/> says.</summary>
        protected override void OnToggle()
        {
        }

        private void Sync() => SetCurrentValue(IsCheckedProperty, Equals(Content, Chosen));
    }
}
