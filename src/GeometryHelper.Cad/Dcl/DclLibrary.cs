using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text;
using GeometryHelper.Cad.Lisp;

namespace GeometryHelper.Cad.Dcl
{
    /// <summary>
    /// The DCL files one interpreter has loaded, by the ids <c>load_dialog</c> gave (D315): 1 for the first file and one more
    /// for each load after it, a file loaded twice getting two. Finds a file as <c>findfile</c> does, reads it as UTF-8 when it
    /// is UTF-8 and as the system's ANSI code page when not, and parses it with its includes.
    /// </summary>
    internal sealed class DclLibrary
    {
        private readonly Dictionary<int, DclFile> _files = new Dictionary<int, DclFile>();
        private readonly Func<IEnumerable<string>> _folders;
        private int _lastId;

        /// <summary>Makes a library whose files are looked for in the folders <paramref name="folders"/> gives, in order.</summary>
        /// <param name="folders">The folders a name with no folder is looked for in: the current folder, the drawing's, the support folders.</param>
        internal DclLibrary(Func<IEnumerable<string>> folders)
        {
            _folders = folders ?? (() => Array.Empty<string>());
        }

        /// <summary>Adds a file already parsed, as a test builds one.</summary>
        /// <param name="file">The file.</param>
        /// <returns>Its id.</returns>
        internal int Add(DclFile file)
        {
            _lastId++;
            _files[_lastId] = file;
            return _lastId;
        }

        /// <summary>Gets the file an id stands for; null for an id never given or unloaded.</summary>
        /// <param name="id">The id.</param>
        /// <returns>The file, or null.</returns>
        internal DclFile Get(int id) => _files.TryGetValue(id, out DclFile file) ? file : null;

        /// <summary>Drops a file; the dialogs made from it keep their tiles. An id that stands for no file is ignored.</summary>
        /// <param name="id">The id.</param>
        internal void Unload(int id) => _files.Remove(id);

        /// <summary>
        /// Finds a file, reads it and parses it. A name with no extension gets <c>.dcl</c>; a name with a folder is that file;
        /// any other is looked for in the folders in order.
        /// </summary>
        /// <param name="name">The name as the routine wrote it.</param>
        /// <returns>The id of the file, or -1 where it is not found or cannot be read.</returns>
        /// <exception cref="DclException">Thrown for a file that is found and cannot be parsed (D307, D338).</exception>
        internal int Load(string name)
        {
            string path = Find(name, null);
            string text = path == null ? null : ReadText(path);

            if (text == null)
            {
                return -1;
            }

            return Add(DclParser.Parse(text, path, Include));
        }

        private KeyValuePair<string, string>? Include(string name, string from)
        {
            string folder = null;

            try
            {
                folder = Path.GetDirectoryName(from);
            }
            catch (ArgumentException) { }

            string path = Find(name, folder);
            string text = path == null ? null : ReadText(path);
            return text == null ? (KeyValuePair<string, string>?)null : new KeyValuePair<string, string>(path, text);
        }

        private string Find(string name, string first)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            try
            {
                string withExtension = Path.HasExtension(name) ? name : name + ".dcl";

                if (Path.IsPathRooted(withExtension))
                {
                    return File.Exists(withExtension) ? Path.GetFullPath(withExtension) : null;
                }

                var folders = new List<string> { first };
                folders.AddRange(_folders());

                foreach (string folder in folders)
                {
                    if (string.IsNullOrEmpty(folder))
                    {
                        continue;
                    }

                    string candidate = Path.Combine(folder, withExtension);

                    if (File.Exists(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }
            }
            catch (Exception exception) when (IoFailure(exception)) { }

            return null;
        }

        /// <summary>Reads a file as UTF-8 when its bytes are UTF-8 (a byte order mark skipped), else as the system's ANSI code page; null where it cannot be read.</summary>
        private static string ReadText(string path)
        {
            byte[] bytes;

            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception exception) when (IoFailure(exception))
            {
                return null;
            }

            int skip = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;

            try
            {
                return new UTF8Encoding(false, true).GetString(bytes, skip, bytes.Length - skip);
            }
            catch (DecoderFallbackException)
            {
                return LispTextEncoding.Ansi.GetString(bytes, skip, bytes.Length - skip);
            }
        }

        private static bool IoFailure(Exception exception) =>
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is ArgumentException
            || exception is NotSupportedException
            || exception is SecurityException;
    }
}
