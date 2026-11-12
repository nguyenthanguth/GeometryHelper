using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The keys of the user's classes, <c>HKEY_CURRENT_USER\Software\Classes</c>, as the association reads and writes them:
    /// a key by its path under the classes, a value by its name, "" being the key's default.
    /// </summary>
    internal interface ICadUserClasses
    {
        /// <summary>Gets a value, null where the key or the value is missing.</summary>
        string GetValue(string key, string name);

        /// <summary>Sets a value, the key made where it is missing.</summary>
        void SetValue(string key, string name, string value);

        /// <summary>Deletes a value; a missing one is nothing to do.</summary>
        void DeleteValue(string key, string name);

        /// <summary>Deletes a key with every key under it; a missing one is nothing to do.</summary>
        void DeleteKey(string key);

        /// <summary>Tells Explorer the associations changed, so that it draws the files' icons anew.</summary>
        void NotifyShell();
    }

    /// <summary>
    /// Associates .dxf and .dwg files with the app for the user alone, under <c>HKEY_CURRENT_USER\Software\Classes</c>, with no
    /// administrator: the ProgID <see cref="ProgId"/> with the icon of a drawing file and the command that opens one, and the
    /// ProgID in the "Open with" list of each extension. An extension no program has is given to the app; one another program
    /// has is left to it, the app being one choice of Open with.
    /// </summary>
    internal static class CadFileAssociation
    {
        /// <summary>The ProgID of a drawing the app opens.</summary>
        internal const string ProgId = "GeometryHelper.Drawing";

        /// <summary>The extensions associated.</summary>
        internal static readonly string[] Extensions = { ".dxf", ".dwg" };

        /// <summary>Writes the association.</summary>
        /// <param name="classes">The user's classes.</param>
        /// <param name="appName">The app's name, shown in the type of a file: "Drawing (GeometryHelper DXF)".</param>
        /// <param name="exePath">The full path of the app's exe, which is given the file as its one argument.</param>
        /// <param name="iconPath">The icon of a drawing file: a .ico file, or an exe and the index of its icon.</param>
        internal static void Register(ICadUserClasses classes, string appName, string exePath, string iconPath)
        {
            classes.SetValue(ProgId, "", "Drawing (" + appName + ")");
            classes.SetValue(ProgId + @"\DefaultIcon", "", iconPath);
            classes.SetValue(ProgId + @"\shell\open\command", "", "\"" + exePath + "\" \"%1\"");

            foreach (string extension in Extensions)
            {
                classes.SetValue(extension + @"\OpenWithProgids", ProgId, "");

                if (string.IsNullOrEmpty(classes.GetValue(extension, "")))
                {
                    classes.SetValue(extension, "", ProgId);
                }
            }

            classes.NotifyShell();
        }

        /// <summary>Takes out what <see cref="Register"/> wrote, leaving what other programs wrote.</summary>
        /// <param name="classes">The user's classes.</param>
        internal static void Unregister(ICadUserClasses classes)
        {
            foreach (string extension in Extensions)
            {
                classes.DeleteValue(extension + @"\OpenWithProgids", ProgId);

                if (string.Equals(classes.GetValue(extension, ""), ProgId, StringComparison.OrdinalIgnoreCase))
                {
                    classes.DeleteValue(extension, "");
                }
            }

            classes.DeleteKey(ProgId);
            classes.NotifyShell();
        }
    }

    /// <summary>The user's classes in the registry, <c>HKEY_CURRENT_USER\Software\Classes</c>.</summary>
    internal sealed class CadRegistryUserClasses : ICadUserClasses
    {
        private const string Root = @"Software\Classes\";

        public string GetValue(string key, string name)
        {
            using (RegistryKey opened = Registry.CurrentUser.OpenSubKey(Root + key))
            {
                return opened?.GetValue(name) as string;
            }
        }

        public void SetValue(string key, string name, string value)
        {
            using (RegistryKey opened = Registry.CurrentUser.CreateSubKey(Root + key))
            {
                opened.SetValue(name, value, RegistryValueKind.String);
            }
        }

        public void DeleteValue(string key, string name)
        {
            using (RegistryKey opened = Registry.CurrentUser.OpenSubKey(Root + key, writable: true))
            {
                opened?.DeleteValue(name, throwOnMissingValue: false);
            }
        }

        public void DeleteKey(string key) =>
            Registry.CurrentUser.DeleteSubKeyTree(Root + key, throwOnMissingSubKey: false);

        public void NotifyShell() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); // SHCNE_ASSOCCHANGED, SHCNF_IDLIST

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
    }
}
