using System;
using GeometryHelper.Cad.Lisp;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// What AutoLISP's <c>setenv</c> and <c>setcfg</c> keep, as AutoCAD keeps it in the registry: one <see cref="LispEnvironment"/>
    /// for the whole app, loaded from Global settings and given to the session of every drawing, so that a name set in one
    /// drawing is there in the others and in the next run; each change a routine makes is saved at once. Also the two requests
    /// of AutoLISP's <c>graphscr</c> and <c>textscr</c> for the text window.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private readonly LispEnvironment _lispEnvironment = new LispEnvironment();

        // Called once by the constructor: the settings in, and a change a routine makes written back to them and saved.
        private void StartLispEnvironment()
        {
            UseLispEnvironmentSettings();
            _lispEnvironment.Changed += OnLispEnvironmentChanged;
        }

        /// <summary>Gives the store the entries of the settings in use, without writing them back.</summary>
        private void UseLispEnvironmentSettings() => _lispEnvironment.Load(_settings.LispEnvironment);

        private void OnLispEnvironmentChanged(object sender, EventArgs e) =>
            ChangeSettings(s => s.SetLispEnvironment(_lispEnvironment.Entries), "a SETENV or SETCFG of a routine");

        /// <summary>GRAPHSCR: puts the text window away where it is open; it is handled where the workspace listens to it.</summary>
        private void OnGraphScreenRequested(CadUiRequestEventArgs e)
        {
            if (_textWindow != null)
            {
                _textWindow.RequestClose();
                e.Handled = true;
            }
        }
    }
}
