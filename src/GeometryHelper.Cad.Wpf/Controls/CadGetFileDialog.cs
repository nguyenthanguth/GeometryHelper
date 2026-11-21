using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The file dialog of AutoLISP's <c>getfiled</c>: an open dialog, or a save dialog where the request is for a new file, with
    /// the extensions the routine named as its filter and the starting name or folder it gave.
    /// </summary>
    internal static class CadGetFileDialog
    {
        /// <summary>
        /// Shows the dialog: Save where flag 1 is set (asking before it overwrites a file unless flag 32 is), Open otherwise.
        /// </summary>
        /// <param name="owner">The window the dialog belongs to.</param>
        /// <param name="request">What <c>getfiled</c> asked.</param>
        /// <returns>The path chosen; null when the user cancelled.</returns>
        internal static string Show(Window owner, CadGetFileRequest request)
        {
            bool save = (request.Flags & 1) != 0;
            FileDialog dialog = save
                ? new SaveFileDialog { OverwritePrompt = (request.Flags & 32) == 0 }
                : (FileDialog)new OpenFileDialog { CheckFileExists = true };
            dialog.Title = request.Title;
            dialog.Filter = FilterOf(request);
            StartOf(request, out string folder, out string name);
            dialog.FileName = name;

            if (folder.Length > 0)
            {
                dialog.InitialDirectory = folder;
            }

            string first = ExtensionsOf(request).FirstOrDefault();

            if (save && first != null)
            {
                dialog.DefaultExt = first;
            }

            return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
        }

        /// <summary>
        /// The filter of the dialog: the extensions the routine gave as one entry (<c>dwg;dxf</c> gives
        /// <c>*.dwg;*.dxf</c>), and <c>All files (*.*)</c> after it, or alone where the routine gave none or asked for any
        /// extension with flag 4.
        /// </summary>
        /// <param name="request">What <c>getfiled</c> asked.</param>
        /// <returns>The filter, in the form the Windows dialogs take.</returns>
        internal static string FilterOf(CadGetFileRequest request)
        {
            List<string> extensions = ExtensionsOf(request);
            const string all = "All files (*.*)|*.*";

            if (extensions.Count == 0)
            {
                return all;
            }

            string patterns = string.Join(";", extensions.Select(e => "*." + e));
            string entry = string.Join(", ", extensions) + " files (" + patterns + ")|" + patterns;
            return (request.Flags & 4) != 0 ? entry + "|" + all : entry;
        }

        /// <summary>
        /// Where the dialog starts: with flag 16 the default is a folder, and so is a default that names a folder that exists;
        /// else the folder of the default and its file name.
        /// </summary>
        /// <param name="request">What <c>getfiled</c> asked.</param>
        /// <param name="folder">The folder to start in; empty for the dialog's own.</param>
        /// <param name="name">The file name to offer; empty for none.</param>
        internal static void StartOf(CadGetFileRequest request, out string folder, out string name)
        {
            folder = string.Empty;
            name = string.Empty;
            string given = request.DefaultName;

            if (string.IsNullOrWhiteSpace(given))
            {
                return;
            }

            try
            {
                if ((request.Flags & 16) != 0 || Directory.Exists(given))
                {
                    folder = Directory.Exists(given) ? given : string.Empty;
                    return;
                }

                string directory = Path.GetDirectoryName(given);
                folder = !string.IsNullOrEmpty(directory) && Directory.Exists(directory) ? directory : string.Empty;
                name = Path.GetFileName(given);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException)
            {
                folder = string.Empty;
                name = string.Empty;
            }
        }

        /// <summary>The extensions named, without dots or stars, in the order given, none twice.</summary>
        private static List<string> ExtensionsOf(CadGetFileRequest request)
        {
            var found = new List<string>();

            foreach (string part in (request.Extension ?? string.Empty).Split(';'))
            {
                string extension = part.Trim().TrimStart('*').TrimStart('.');

                if (extension.Length > 0 && !found.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    found.Add(extension);
                }
            }

            return found;
        }
    }
}
