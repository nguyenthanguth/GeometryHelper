using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace GeometryHelper.Cad.Wpf
{
    public partial class CadWorkspace
    {
        /// <summary>
        /// Opens a hyperlink's web address, mail address or file with the program of the system for it, as a double click in the
        /// file manager would.
        /// </summary>
        /// <param name="address">The address or the full path.</param>
        /// <returns>Whether the system took it.</returns>
        private static bool OpenWithSystem(string address)
        {
            try
            {
                Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
                return true;
            }
            catch (Exception exception)
                when (exception is Win32Exception
                    || exception is InvalidOperationException
                    || exception is FileNotFoundException
                )
            {
                GeometryHelperLog.Warn("CadWorkspace: the hyperlink " + address + " was not opened.", exception);
                return false;
            }
        }
    }
}
