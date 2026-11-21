using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// What the warning of SECURELOAD 1 shows about a file of code outside the trusted folders, in the words of AutoCAD 2021's
    /// task dialog "Security - Unsigned Executable File": the file's name, its folder and the day it was last written, beside
    /// the lines that never change (the publisher is unknown, there is no issuer), and the answer given: Always Load, Load
    /// Once or Do Not Load, which is also what Esc and closing the window mean.
    /// </summary>
    public sealed class LoadSecurityViewModel
    {
        /// <summary>Makes the warning's state for a file.</summary>
        /// <param name="path">The file's full path; a file that is not there shows an empty date.</param>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        public LoadSecurityViewModel(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            Rows = new[]
            {
                new KeyValuePair<string, string>("Name:", Path.GetFileName(path)),
                new KeyValuePair<string, string>("Publisher:", "Unknown Publisher"),
                new KeyValuePair<string, string>("Location:", Path.GetDirectoryName(path) ?? string.Empty),
                new KeyValuePair<string, string>("Issuer:", "None"),
                new KeyValuePair<string, string>("Date:", DateOf(path)),
            };
            AlwaysLoadCommand = new DelegateCommand(_ => Give(CadLoadAnswer.AlwaysLoad));
            LoadOnceCommand = new DelegateCommand(_ => Give(CadLoadAnswer.LoadOnce));
            DoNotLoadCommand = new DelegateCommand(_ => Give(CadLoadAnswer.DoNotLoad));
        }

        /// <summary>Raised when a button gives its answer, which the window closes on.</summary>
        public event EventHandler Answered;

        /// <summary>Gets the title of the window: Security - Unsigned Executable File.</summary>
        public string Title => "Security - Unsigned Executable File";

        /// <summary>Gets the main instruction under the title, which asks what to do with the file.</summary>
        public string Instruction =>
            "The publisher of this executable file could not be verified and the file is not located in a trusted folder. What do you want to do?";

        /// <summary>Gets the line under the rows: the file must come from a source the user trusts, in a trusted folder.</summary>
        public string Advice =>
            "Make sure that this executable file comes from a trusted source and is located in a trusted folder.";

        /// <summary>Gets the text of the link under the buttons, which opens the guide's section on trusted folders.</summary>
        public string FootnoteLink => "How do I decide what files to load?";

        /// <summary>Gets the address the link opens: the guide's section "Trusted folders and SECURELOAD".</summary>
        public string FootnoteUrl => LispHelp.TrustedFolders;

        /// <summary>Gets the label of the button that loads the file now and whenever its bytes are the same: Always Load.</summary>
        public string AlwaysLoadText => "Always Load";

        /// <summary>Gets the label of the button that loads the file this once: Load Once.</summary>
        public string LoadOnceText => "Load Once";

        /// <summary>Gets the label of the button that does not load it: Do Not Load.</summary>
        public string DoNotLoadText => "Do Not Load";

        /// <summary>
        /// Gets the rows under the instruction as label and value: Name (the file with its extension, as
        /// <c>area-table.lsp</c>), Publisher (Unknown Publisher: the presenter loads no signed code), Location (the file's
        /// folder, not the file), Issuer (None) and Date, the day and time the file was last written as the current culture's
        /// short date, two spaces and the hour and minute on a 24-hour clock: <c>11/21/2026  21:59</c> in the United States.
        /// The date is empty for a file that is not there.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, string>> Rows { get; }

        /// <summary>Gets or sets the answer: Do Not Load until a button says otherwise.</summary>
        public CadLoadAnswer Answer { get; set; } = CadLoadAnswer.DoNotLoad;

        /// <summary>Gets the command of Always Load.</summary>
        public ICommand AlwaysLoadCommand { get; }

        /// <summary>Gets the command of Load Once.</summary>
        public ICommand LoadOnceCommand { get; }

        /// <summary>Gets the command of Do Not Load.</summary>
        public ICommand DoNotLoadCommand { get; }

        private void Give(CadLoadAnswer answer)
        {
            Answer = answer;
            Answered?.Invoke(this, EventArgs.Empty);
        }

        private static string DateOf(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return string.Empty;
                }

                DateTime written = File.GetLastWriteTime(path);
                return written.ToString("d", CultureInfo.CurrentCulture)
                    + "  "
                    + written.ToString("HH:mm", CultureInfo.CurrentCulture);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                return string.Empty;
            }
        }
    }
}
