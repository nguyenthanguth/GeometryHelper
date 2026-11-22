using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text.RegularExpressions;

namespace GeometryHelper.Cad.Lisp
{
    public sealed partial class LispInterpreter
    {
        private static readonly char[] Separators = { '\\', '/' };

        private readonly LispTextCursor _keyboard = new LispTextCursor("");
        private int _tempCounter = 1;

        // The files a routine opened and has not closed, in the order it opened them.
        private readonly List<LispFile> _openFiles = new List<LispFile>();

        /// <summary>
        /// Gets or sets the folder a file name with no folder is taken from, for <c>open</c>, <c>findfile</c>, <c>vl-file-*</c>,
        /// <c>vl-directory-files</c> and <c>vl-mkdir</c>: the process's current folder by default, which is where AutoCAD
        /// looks (the folder it started in, not the drawing's). Nothing changes the process's own current folder.
        /// </summary>
        internal Func<string> CurrentFolder { get; set; } = () => Environment.CurrentDirectory;

        /// <summary>
        /// Gets or sets what gives the folder of the drawing (<c>DWGPREFIX</c>), the second place <c>findfile</c> looks; null,
        /// or an empty answer, for a drawing never saved.
        /// </summary>
        internal Func<string> DrawingFolder { get; set; }

        /// <summary>
        /// Gets or sets what asks the user for a line of text, for <c>read-line</c> and <c>read-char</c> with no file; null
        /// for an interpreter with no keyboard, where they give nil. It returns null when the user gives up.
        /// </summary>
        internal Func<string> KeyboardLine { get; set; }

        /// <summary>
        /// Says whether a file lies where <c>findtrustedfile</c> may give it: the folders of the program and the trusted ones, or
        /// anywhere at SECURELOAD 0. Nothing is asked. Null, as it is until set, trusts every file.
        /// </summary>
        internal Func<string, bool> IsTrusted { get; set; }

        private void RegisterFiles()
        {
            Define("OPEN", 2, 3, Open);
            Define("CLOSE", 1, 1, Close);
            Define("READ-LINE", 0, 1, ReadLine);
            Define("READ-CHAR", 0, 1, ReadChar);
            Define("WRITE-LINE", 1, 2, WriteLine);
            Define("WRITE-CHAR", 1, 2, WriteChar);
            Define("FINDFILE", 1, 1, a => FindOnPath(Str(a[0])));
            Define("FINDTRUSTEDFILE", 1, 1, a => FindTrusted(Str(a[0])));
            Define("VL-FILENAME-BASE", 1, 1, a => FileNameBase(Str(a[0])));
            Define("VL-FILENAME-DIRECTORY", 1, 1, a => FileNameDirectory(Str(a[0])));
            Define("VL-FILENAME-EXTENSION", 1, 1, a => FileNameExtension(Str(a[0])));
            Define("VL-FILENAME-MKTEMP", 0, 3, MakeTempName);
            Define("VL-FILE-SIZE", 1, 1, a => FileSize(Str(a[0])));
            Define("VL-FILE-SYSTIME", 1, 1, a => FileSystemTime(Str(a[0])));
            Define("VL-FILE-DIRECTORY-P", 1, 1, a => Bool(Directory.Exists(Resolve(Str(a[0])))));
            Define("VL-FILE-COPY", 2, 3, a => CopyFile(Str(a[0]), Str(a[1]), a.Length > 2 && a[2] != null));
            Define("VL-FILE-RENAME", 2, 2, a => RenameFile(Str(a[0]), Str(a[1])));
            Define("VL-FILE-DELETE", 1, 1, a => DeleteFile(Str(a[0])));
            Define("VL-MKDIR", 1, 1, a => MakeFolder(Str(a[0])));
            Define("VL-DIRECTORY-FILES", 0, 3, ListFolder);
        }

        /// <summary>
        /// Takes a name as a place on disk: a relative name from <see cref="CurrentFolder"/>, any other as it was given.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>The name to hand to the file system.</returns>
        internal string Resolve(string name)
        {
            try
            {
                if (name.Length == 0 || Path.IsPathRooted(name))
                {
                    return name;
                }

                string folder = CurrentFolder?.Invoke();
                return string.IsNullOrEmpty(folder) ? name : Path.Combine(folder, name);
            }
            catch (ArgumentException)
            {
                return name;
            }
        }

        private static bool IoFailure(Exception exception) =>
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is ArgumentException
            || exception is NotSupportedException
            || exception is SecurityException;

        private bool Exists(string name)
        {
            string path = Resolve(name);

            try
            {
                return File.Exists(path) || Directory.Exists(path);
            }
            catch (Exception exception) when (IoFailure(exception))
            {
                return false;
            }
        }

        #region Open and close

        /// <summary>
        /// <c>(open name mode [encoding])</c>: a mode is <c>r</c>, <c>w</c> or <c>a</c> in either case, an encoding is
        /// <c>utf8</c> or <c>utf8-bom</c>; a file that cannot be opened is nil, and a mode or encoding that is none of
        /// those an error.
        /// </summary>
        private object Open(object[] a)
        {
            string name = Str(a[0]);
            string mode = Str(a[1]);
            string encoding = a.Length > 2 && a[2] != null ? Str(a[2]) : null;
            char letter = mode.Length > 0 ? char.ToLowerInvariant(mode[0]) : ' ';

            if (letter != 'r' && letter != 'w' && letter != 'a')
            {
                throw new LispException("unknown open mode: " + Print(mode, true));
            }

            if (encoding != null)
            {
                encoding = encoding.ToLowerInvariant();

                if (encoding != "utf8" && encoding != "utf8-bom")
                {
                    throw new LispException("unknown open mode: " + Print(a[2], true));
                }
            }

            LispFile file = mode.Length > 1 ? null : LispFile.Open(name, Resolve(name), letter, encoding);

            if (file != null)
            {
                _openFiles.Add(file);
            }

            return file;
        }

        private object Close(object[] a)
        {
            LispFile file = a[0] as LispFile ?? throw TypeError("streamp", a[0]);
            file.Close();
            _openFiles.Remove(file);
            return null;
        }

        /// <summary>
        /// Closes every file a routine opened and did not close, as AutoCAD does when the drawing closes: a file left open keeps
        /// its name locked, so a routine's temporary file could not be deleted, until the garbage collector let it go. A host
        /// calls this where it lets the document go, with no evaluation running or waiting.
        /// </summary>
        internal void CloseFiles()
        {
            foreach (LispFile file in _openFiles.ToArray())
            {
                file.Close();
            }

            _openFiles.Clear();
        }

        #endregion

        #region Reading and writing

        private object ReadLine(object[] a)
        {
            if (a.Length == 0 || a[0] == null)
            {
                return KeyboardReady() ? _keyboard.ReadLine() : null;
            }

            if (!(a[0] is LispFile file))
            {
                throw TypeError("FILE", a[0]);
            }

            return InputFile(file).ReadLine();
        }

        private object ReadChar(object[] a)
        {
            int code;

            if (a.Length == 0 || a[0] == null)
            {
                code = KeyboardReady() ? _keyboard.ReadChar() : -1;
            }
            else
            {
                if (!(a[0] is LispFile file))
                {
                    throw TypeError("FILE", a[0]);
                }

                code = InputFile(file).ReadChar();
            }

            return code < 0 ? null : (object)code;
        }

        /// <summary>
        /// A file to read from. A closed one is refused by the mode it was opened in, as AutoCAD does: opened to read it
        /// says <c>stream is closed</c>, opened to write or append it is not an input file at all, a type error of FILE.
        /// </summary>
        private static LispFile InputFile(LispFile file)
        {
            if (file.IsClosed)
            {
                throw file.CanRead ? ClosedError(file) : TypeError("FILE", file);
            }

            return file.CanRead ? file : throw TypeError("input-streamp", file, colon: true);
        }

        private static LispException ClosedError(LispFile file) =>
            new LispException("stream is closed: " + Print(file, true));

        /// <summary>Makes sure the keyboard buffer holds text, asking for a line when it is used up.</summary>
        private bool KeyboardReady()
        {
            if (!_keyboard.AtEnd)
            {
                return true;
            }

            string line = KeyboardLine?.Invoke();

            if (line == null)
            {
                return false;
            }

            _keyboard.Refill(line + "\n");
            return true;
        }

        /// <summary>
        /// Takes the file a write function was given as its argument at <paramref name="index"/>: null for none, or nil,
        /// which writes on the screen.
        /// </summary>
        private static LispFile OutputFile(object[] a, int index)
        {
            if (a.Length <= index || a[index] == null)
            {
                return null;
            }

            return OutputFile(a[index], streamTest: true);
        }

        /// <summary>
        /// A file to write to. <c>write-line</c> and <c>write-char</c> (<paramref name="streamTest"/>) refuse anything that is
        /// no output stream, a number or a file opened to read, closed or not, with <c>output-streamp</c>; <c>princ</c> and the
        /// others refuse a non-file, and a closed file opened to read, as a type error of FILE. A closed file opened to write
        /// or append says <c>stream is closed</c> to every one of them.
        /// </summary>
        private static LispFile OutputFile(object value, bool streamTest)
        {
            if (!(value is LispFile file))
            {
                throw streamTest ? TypeError("output-streamp", value, colon: true) : TypeError("FILE", value);
            }

            if (file.IsClosed && !file.CanRead)
            {
                throw ClosedError(file);
            }

            if (file.CanRead)
            {
                throw streamTest || !file.IsClosed
                    ? TypeError("output-streamp", file, colon: true)
                    : TypeError("FILE", file);
            }

            return file;
        }

        private object WriteLine(object[] a)
        {
            string text = Str(a[0]);
            LispFile file = OutputFile(a, 1);

            if (file == null)
            {
                Write(LispPrinter.Display(text) + "\n");
            }
            else
            {
                file.Write(text + "\n");
            }

            return a[0];
        }

        private object WriteChar(object[] a)
        {
            int code = Int(a[0]);
            LispFile file = OutputFile(a, 1);
            string text = code >= 1 && code <= 65535 ? ((char)code).ToString() : "";

            if (file == null)
            {
                Write(text);
            }
            else
            {
                file.Write(text);
            }

            return a[0];
        }

        #endregion

        #region Finding files

        /// <summary>
        /// <c>findfile</c>: a rooted name is looked for where it points; any other, a bare name or one with a relative folder
        /// (<c>Sub/x.txt</c>, <c>./x.lsp</c>), in the current folder, then the folder of the drawing, then the support folders.
        /// A file or a folder is found, and the answer is its full path. A name of a file of code
        /// (<see cref="CadLoadSecurity.IsCodeFile"/>) is looked for in the support folders alone unless
        /// <see cref="LegacyCodeSearch"/> says to look beside the drawing: <c>findfile "label.lsp"</c> and
        /// <c>findfile "Sub/label.lsp"</c> do not find the file lying next to the drawing, <c>findfile "label.txt"</c> does.
        /// </summary>
        private object FindOnPath(string name)
        {
            if (name.Length == 0 || Array.IndexOf(Separators, name[name.Length - 1]) >= 0)
            {
                return null;
            }

            try
            {
                if (Path.IsPathRooted(name))
                {
                    return Exists(name) ? Path.GetFullPath(Resolve(name)) : null;
                }

                var folders = new List<string>();

                if (!CadLoadSecurity.IsCodeFile(name) || (LegacyCodeSearch != null && LegacyCodeSearch()))
                {
                    folders.Add(CurrentFolder?.Invoke());
                    folders.Add(DrawingFolder?.Invoke());
                }

                folders.AddRange(_supportFolders);

                foreach (string folder in folders)
                {
                    if (string.IsNullOrEmpty(folder))
                    {
                        continue;
                    }

                    string candidate = Path.Combine(folder, name);

                    if (File.Exists(candidate) || Directory.Exists(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }
            }
            catch (Exception exception) when (IoFailure(exception)) { }

            return null;
        }

        /// <summary>
        /// <c>(findtrustedfile name)</c>: <c>findfile</c> limited to the files in a trusted folder (SECURELOAD, TRUSTEDPATHS: see
        /// <see cref="CadLoadSecurity.IsTrusted"/>); at SECURELOAD 0 every file is. A folder is never found, and no extension is
        /// added to the name (AutoCAD 2021, probe g13).
        /// </summary>
        private object FindTrusted(string name)
        {
            if (!(FindOnPath(name) is string found) || !File.Exists(found))
            {
                return null;
            }

            return IsTrusted == null || (bool)OnOwner(() => IsTrusted(found)) ? found : null;
        }

        #endregion

        #region File names

        private static int FileNameStart(string name)
        {
            int separator = name.LastIndexOfAny(Separators);

            if (separator >= 0)
            {
                return separator + 1;
            }

            return name.Length >= 2 && name[1] == ':' ? 2 : 0;
        }

        private static string FileNameBase(string name)
        {
            int start = FileNameStart(name);
            string file = name.Substring(start);

            if (file.Length == 0)
            {
                return name.LastIndexOfAny(Separators) >= 0 ? "." : "";
            }

            int dot = file.LastIndexOf('.');
            return dot < 0 ? file : file.Substring(0, dot);
        }

        private static string FileNameDirectory(string name)
        {
            int separator = name.LastIndexOfAny(Separators);

            if (separator >= 0)
            {
                return name.Substring(0, separator);
            }

            return name.Length >= 2 && name[1] == ':' ? name.Substring(0, 2) : "";
        }

        private static object FileNameExtension(string name)
        {
            string file = name.Substring(FileNameStart(name));
            int dot = file.LastIndexOf('.');
            return dot < 0 ? null : file.Substring(dot);
        }

        /// <summary>
        /// <c>(vl-filename-mktemp [pattern [dir [ext]]])</c>: a name that does not exist, from the first five characters of
        /// the pattern's name and a counter of three digits that belongs to this interpreter. Nothing is created.
        /// </summary>
        private object MakeTempName(object[] a)
        {
            string pattern = a.Length > 0 && a[0] != null ? Str(a[0]) : "$VL~~";
            string folder = a.Length > 1 && a[1] != null ? Str(a[1]) : null;
            string extension = a.Length > 2 && a[2] != null ? Str(a[2]) : null;
            int separator = pattern.LastIndexOfAny(Separators);
            string file = pattern.Substring(separator + 1);
            int dot = file.LastIndexOf('.');
            string stem = dot < 0 ? file : file.Substring(0, dot);

            if (stem.Length > 5)
            {
                stem = stem.Substring(0, 5);
            }

            if (extension == null)
            {
                extension = dot < 0 ? "" : file.Substring(dot);
            }

            if (folder == null)
            {
                folder = separator > 0 ? pattern.Substring(0, separator) : Path.GetTempPath();
            }

            if (folder.Length > 0 && Array.IndexOf(Separators, folder[folder.Length - 1]) >= 0)
            {
                folder = folder.Substring(0, folder.Length - 1);
            }

            while (true)
            {
                string candidate = folder + "\\" + stem + Inv(_tempCounter++).PadLeft(3, '0') + extension;

                if (!Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        #endregion

        #region Files and folders

        private object FileSize(string name)
        {
            string path = Resolve(name);

            try
            {
                if (File.Exists(path))
                {
                    return (int)new FileInfo(path).Length;
                }

                return Directory.Exists(path) ? (object)0 : null;
            }
            catch (Exception exception) when (IoFailure(exception))
            {
                return null;
            }
        }

        private object FileSystemTime(string name)
        {
            string path = Resolve(name);

            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                DateTime time = File.GetLastWriteTime(path);
                return ListOf(
                    new object[]
                    {
                        time.Year,
                        time.Month,
                        (int)time.DayOfWeek,
                        time.Day,
                        time.Hour,
                        time.Minute,
                        time.Second,
                        time.Millisecond,
                    }
                );
            }
            catch (Exception exception) when (IoFailure(exception))
            {
                return null;
            }
        }

        /// <summary>
        /// <c>vl-file-copy</c>: the number of bytes copied; nil when the source is missing, the target is a folder, or the
        /// target exists and nothing was said to append. Appending adds the source at the end of the target.
        /// </summary>
        private object CopyFile(string from, string to, bool append)
        {
            string source = Resolve(from);
            string target = Resolve(to);

            try
            {
                if (!File.Exists(source) || Directory.Exists(target) || (File.Exists(target) && !append))
                {
                    return null;
                }

                if (!append)
                {
                    File.Copy(source, target);
                    return (int)new FileInfo(target).Length;
                }

                byte[] bytes = LispFile.ReadAll(source);

                using (var stream = new FileStream(target, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    stream.Write(bytes, 0, bytes.Length);
                }

                return bytes.Length;
            }
            catch (Exception exception) when (IoFailure(exception))
            {
                return null;
            }
        }

        /// <summary><c>vl-file-rename</c>: T, replacing a file of the new name; nil when the old file is missing.</summary>
        private object RenameFile(string from, string to)
        {
            string source = Resolve(from);
            string target = Resolve(to);

            try
            {
                if (!File.Exists(source) || Directory.Exists(target))
                {
                    return null;
                }

                string full = Path.GetFullPath(target);

                if (string.Equals(Path.GetFullPath(source), full, StringComparison.OrdinalIgnoreCase))
                {
                    return _true;
                }

                if (!Directory.Exists(Path.GetDirectoryName(full)))
                {
                    return null;
                }

                File.Delete(target);
                File.Move(source, target);
                return _true;
            }
            catch (Exception exception) when (IoFailure(exception))
            {
                return null;
            }
        }

        /// <summary><c>vl-file-delete</c>: T for a file or an empty folder; nil when it is missing or cannot be removed.</summary>
        private object DeleteFile(string name)
        {
            string path = Resolve(name);

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    return _true;
                }

                if (Directory.Exists(path))
                {
                    Directory.Delete(path, false);
                    return _true;
                }
            }
            catch (Exception exception) when (IoFailure(exception)) { }

            return null;
        }

        /// <summary><c>vl-mkdir</c>: T, also for a folder that is there; nil when the folder above it is missing.</summary>
        private object MakeFolder(string name)
        {
            try
            {
                string full = Path.GetFullPath(Resolve(name));

                if (Directory.Exists(full))
                {
                    return _true;
                }

                string trimmed = full.TrimEnd(Separators);
                string parent = Path.GetDirectoryName(trimmed);

                if (parent != null && !Directory.Exists(parent))
                {
                    return null;
                }

                Directory.CreateDirectory(trimmed);
                return _true;
            }
            catch (Exception exception) when (IoFailure(exception))
            {
                return null;
            }
        }

        /// <summary>
        /// <c>(vl-directory-files [dir [pattern [flag]]])</c>: the names in a folder that fit the pattern (<c>*</c> and
        /// <c>?</c>, either case), files and folders for a flag of nil or 0, files for 1, folders for -1, with <c>.</c> and
        /// <c>..</c> first wherever folders are listed and the rest in order of name, upper and lower case alike.
        /// </summary>
        private object ListFolder(object[] a)
        {
            string folder = a.Length > 0 && a[0] != null ? Str(a[0]) : null;
            string pattern = a.Length > 1 && a[1] != null ? Str(a[1]) : "*.*";
            int flag = a.Length > 2 && a[2] != null ? Int(a[2]) : 0;
            string path = folder == null ? CurrentFolder?.Invoke() : Resolve(folder);

            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            Regex fit = GlobRegex(pattern);
            var names = new List<string>();

            try
            {
                if (!Directory.Exists(path))
                {
                    return null;
                }

                foreach (FileSystemInfo entry in new DirectoryInfo(path).GetFileSystemInfos())
                {
                    bool isFolder = entry is DirectoryInfo;
                    bool wanted = flag == 1 ? !isFolder : flag != -1 || isFolder;

                    if (wanted && fit.IsMatch(entry.Name))
                    {
                        names.Add(entry.Name);
                    }
                }
            }
            catch (Exception exception) when (IoFailure(exception))
            {
                return null;
            }

            names.Sort(
                (x, y) =>
                {
                    int c = string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
                    return c != 0 ? c : string.CompareOrdinal(x, y);
                }
            );
            var result = new List<object>();

            if (flag != 1)
            {
                foreach (string dots in new[] { ".", ".." })
                {
                    if (fit.IsMatch(dots))
                    {
                        result.Add(dots);
                    }
                }
            }

            result.AddRange(names);
            return ListOf(result);
        }

        /// <summary>
        /// Makes the test for a file pattern: <c>*</c> any run of characters, <c>?</c> any one, either case, and a pattern
        /// ending in <c>.*</c> also fits a name with no dot, so that <c>*.*</c> fits every name as it does on Windows.
        /// </summary>
        private static Regex GlobRegex(string pattern)
        {
            bool dotStar = pattern.EndsWith(".*", StringComparison.Ordinal);
            string core = dotStar ? pattern.Substring(0, pattern.Length - 2) : pattern;
            string body = Regex.Escape(core).Replace("\\*", ".*").Replace("\\?", ".");
            return new Regex(
                "^" + body + (dotStar ? "(\\..*)?" : "") + "$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline
            );
        }

        #endregion
    }
}
