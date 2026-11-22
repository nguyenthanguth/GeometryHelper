using System;
using System.Collections.Generic;
using System.IO;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// The functions of AutoCAD's menus and help that a routine may call and the presenter has little to answer with, as
    /// AutoCAD 2021's console answers them (probe m/func): it loads no CUI menu groups, so <c>menugroup</c> and every
    /// <c>menucmd</c> but the DIESEL one give nil, <c>vmon</c> gives nil, <c>setfunhelp</c> remembers nothing and gives the
    /// function's name, and <c>help</c> gives the file it was asked for and hands it to the window
    /// (<see cref="HelpOpener"/>), which opens it with the program Windows gives its type. <c>acdimenableupdate</c> keeps its
    /// flag and gives the one it had (the dimensions are drawn again at once here whatever it says). <c>fnsplitl</c> gives the
    /// full path of a file name in three pieces.
    /// </summary>
    public sealed partial class LispInterpreter
    {
        /// <summary>
        /// Gets or sets what opens a help file, for <c>help</c>: it is given the full path of a <c>.chm</c>, <c>.htm</c>,
        /// <c>.html</c> or <c>.pdf</c> file that is there, and the window opens it with the program of the system. Null for an
        /// interpreter with no window, where <c>help</c> opens nothing.
        /// </summary>
        internal Action<string> HelpOpener { get; set; }

        private static readonly string[] HelpExtensions = { ".chm", ".htm", ".html", ".pdf" };

        // The flag of acdimenableupdate: T until a routine turns it off.
        private bool _dimensionsUpdate = true;

        private void RegisterMenus()
        {
            Define("MENUGROUP", 1, 1, a =>
            {
                Str(a[0]);
                return null;
            });
            Define("VMON", 0, 0, a => null);
            Define("ACDIMENABLEUPDATE", 0, -1, DimensionUpdate);
            Define("SETFUNHELP", 1, 4, a => StringsThenFirst(a));
            Define("FNSPLITL", 1, 1, a => SplitFileName(Str(a[0])));
            Define("HELP", 0, 3, Help);
        }

        /// <summary>
        /// <c>(acdimenableupdate [flag])</c> gives the flag it had, T or nil, and sets it to T for any argument but nil and to nil
        /// for nil; with no argument it only gives it (AutoCAD 2021, probe m/func f3).
        /// </summary>
        private object DimensionUpdate(object[] a)
        {
            bool before = _dimensionsUpdate;

            if (a.Length > 0)
            {
                _dimensionsUpdate = a[0] != null;
            }

            return Bool(before);
        }

        /// <summary>The first argument, every argument having to be a string, as <c>setfunhelp</c> and <c>help</c> want them.</summary>
        private static string StringsThenFirst(object[] a)
        {
            foreach (object item in a)
            {
                Str(item);
            }

            return (string)a[0];
        }

        /// <summary>
        /// <c>(fnsplitl "c:/a/b.lsp")</c> gives <c>("C:\\a\\" "b" ".lsp")</c> (AutoCAD 2021, probe m/func f3): the folder of the
        /// file as a full path with backslashes, the drive upper case, the name, and the extension from its last dot; a name
        /// with no folder has the empty string for it. Nil for a folder (a name that ends in a separator), a name with no
        /// name before its extension, an empty one and a drive alone. The spaces at the end are dropped.
        /// </summary>
        private object SplitFileName(string given)
        {
            string text = given.TrimEnd();
            int separator = text.LastIndexOfAny(Separators);
            bool hasDrive = text.Length >= 2 && text[1] == ':' && char.IsLetter(text[0]);
            int start = separator >= 0 ? separator + 1 : (hasDrive ? 2 : 0);
            string file = text.Substring(start);

            if (file.Length == 0)
            {
                return null;
            }

            int dot = file.LastIndexOf('.');
            string name = dot < 0 ? file : file.Substring(0, dot);

            if (name.Length == 0)
            {
                return null;
            }

            string folder = string.Empty;

            if (separator >= 0 || hasDrive)
            {
                try
                {
                    string place = separator >= 0 ? text.Substring(0, separator + 1) : text.Substring(0, 2) + "\\";
                    folder = Path.GetFullPath(Resolve(place.Replace('/', '\\')));
                }
                catch (Exception e)
                    when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException)
                {
                    return null;
                }

                if (!folder.EndsWith("\\", StringComparison.Ordinal))
                {
                    folder += "\\";
                }

                if (folder.Length > 1 && folder[1] == ':')
                {
                    folder = char.ToUpperInvariant(folder[0]) + folder.Substring(1);
                }
            }

            return ListOf(new List<object> { folder, name, dot < 0 ? string.Empty : file.Substring(dot) });
        }

        /// <summary>
        /// <c>(help ["file" ["topic" ["command"]]])</c> gives its first argument, every argument having to be a string, and hands
        /// a help file of <c>.chm</c>, <c>.htm</c>, <c>.html</c> or <c>.pdf</c> that is there to the window; any other, or none,
        /// opens nothing, and with no argument it gives nil (AutoCAD 2021, probe m/func f2).
        /// </summary>
        private object Help(object[] a)
        {
            if (a.Length == 0)
            {
                return null;
            }

            string file = StringsThenFirst(a);
            string path = Resolve(file);

            if (Array.IndexOf(HelpExtensions, Path.GetExtension(file).ToLowerInvariant()) >= 0 && File.Exists(path))
            {
                HelpOpener?.Invoke(Path.GetFullPath(path));
            }

            return file;
        }
    }
}
