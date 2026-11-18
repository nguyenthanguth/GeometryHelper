using System;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// What <see cref="MainViewModel.DraftingSettingsRequested"/> carries: the tab of Drafting Settings to show first, so that
    /// OSNAP opens Object Snap and DSETTINGS the tab used last, as AutoCAD 2021 does.
    /// </summary>
    internal sealed class DraftingSettingsRequestedEventArgs : EventArgs
    {
        internal DraftingSettingsRequestedEventArgs(string tab) => Tab = tab;

        /// <summary>Gets the tab to show, as <see cref="DraftingSettingsViewModel.ObjectSnapTab"/>.</summary>
        internal string Tab { get; }
    }
}
