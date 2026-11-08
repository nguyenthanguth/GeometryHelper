using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>The base of the view models: raises <see cref="PropertyChanged"/> when a property set takes a new value.</summary>
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        /// <summary>Raised when a property has changed.</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Sets a field and raises <see cref="PropertyChanged"/> when its value changes.</summary>
        /// <typeparam name="T">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The new value.</param>
        /// <param name="name">The name of the property; the caller's by default.</param>
        /// <returns>True when the value changed.</returns>
        protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            Raise(name);
            return true;
        }

        /// <summary>Raises <see cref="PropertyChanged"/> for a property.</summary>
        /// <param name="name">The name of the property.</param>
        protected void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>A command that runs an action, as the buttons of the ribbon and the status bar bind to.</summary>
    public sealed class DelegateCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        /// <summary>Makes a command.</summary>
        /// <param name="execute">What it does, given the button's parameter.</param>
        /// <param name="canExecute">Whether it can run, given the parameter; null for always.</param>
        public DelegateCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>Raised when whether the command can run may have changed.</summary>
        public event EventHandler CanExecuteChanged;

        /// <summary>Gets whether the command can run.</summary>
        /// <param name="parameter">The parameter.</param>
        /// <returns>True when it can.</returns>
        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);

        /// <summary>Runs the command.</summary>
        /// <param name="parameter">The parameter.</param>
        public void Execute(object parameter) => _execute(parameter);

        /// <summary>Raises <see cref="CanExecuteChanged"/>.</summary>
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
