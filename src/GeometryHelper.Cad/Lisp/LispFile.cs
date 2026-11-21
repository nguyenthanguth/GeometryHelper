using System;
using System.IO;
using System.Security;
using System.Text;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// A file a routine opened with <c>open</c>, what <c>(type f)</c> calls FILE. A file opened to read has all its text
    /// read when it is opened, by the encoding rules of <see cref="LispTextEncoding"/>, and is read a line or a character at
    /// a time from there; a file opened to write or append writes each piece of text at once, as text does in C: every new
    /// line becomes CR LF. It prints as <c>#&lt;file "name"&gt;</c>, with the name as the routine gave it, and as
    /// <c>#&lt;FILE internal&gt;</c> once it is closed.
    /// </summary>
    internal sealed class LispFile
    {
        private readonly LispTextCursor _cursor;
        private readonly FileStream _stream;
        private readonly Encoding _encoding;

        private LispFile(string name, char mode, LispTextCursor cursor, FileStream stream, Encoding encoding)
        {
            Name = name;
            Mode = mode;
            _cursor = cursor;
            _stream = stream;
            _encoding = encoding;
        }

        /// <summary>Gets the name as the routine gave it.</summary>
        internal string Name { get; }

        /// <summary>Gets the mode the file was opened in: <c>r</c>, <c>w</c> or <c>a</c>.</summary>
        internal char Mode { get; }

        /// <summary>Gets a value saying whether the file was opened to read.</summary>
        internal bool CanRead => Mode == 'r';

        /// <summary>Gets a value saying whether <c>close</c> has been called.</summary>
        internal bool IsClosed { get; private set; }

        /// <summary>Gets the text <c>prin1</c> and <c>princ</c> write for the file.</summary>
        internal string PrintForm => IsClosed ? "#<FILE internal>" : "#<file \"" + Name + "\">";

        /// <summary>
        /// Opens a file. A file that cannot be opened (missing to read, in a folder that is missing, locked, a folder) is not an
        /// error but nil to the routine, so it is null here.
        /// </summary>
        /// <param name="name">The name as the routine gave it.</param>
        /// <param name="path">Where it is, with a relative name already taken from the current folder.</param>
        /// <param name="mode">The lower-case mode, <c>r</c>, <c>w</c> or <c>a</c>.</param>
        /// <param name="encoding">The encoding named, <c>utf8</c> or <c>utf8-bom</c> in lower case; null for none.</param>
        /// <returns>The file, or null.</returns>
        internal static LispFile Open(string name, string path, char mode, string encoding)
        {
            try
            {
                if (mode == 'r')
                {
                    if (!File.Exists(path))
                    {
                        return null;
                    }

                    byte[] bytes = ReadAll(path);
                    string text;

                    if (encoding == null)
                    {
                        Encoding detected = LispTextEncoding.Detect(bytes, out int bom);
                        text = detected.GetString(bytes, bom, bytes.Length - bom);
                    }
                    else
                    {
                        bool hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
                        text = LispTextEncoding.DecodeUtf8(bytes, hasBom ? 3 : 0);
                    }

                    return new LispFile(name, mode, new LispTextCursor(text), null, null);
                }

                byte[] existing = mode == 'a' && File.Exists(path) ? ReadAll(path) : Array.Empty<byte>();
                bool fresh = existing.Length == 0;
                Encoding writing =
                    encoding != null || (!fresh && LispTextEncoding.IsUtf8(existing))
                        ? new UTF8Encoding(false)
                        : LispTextEncoding.AnsiWithEscapes();
                var stream = new FileStream(
                    path,
                    mode == 'w' ? FileMode.Create : FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read
                );

                if (fresh && encoding == "utf8-bom")
                {
                    stream.Write(new byte[] { 0xEF, 0xBB, 0xBF }, 0, 3);
                    stream.Flush();
                }

                return new LispFile(name, mode, null, stream, writing);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (SecurityException)
            {
                return null;
            }
        }

        internal static byte[] ReadAll(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var bytes = new byte[stream.Length];
                int read = 0;

                while (read < bytes.Length)
                {
                    int n = stream.Read(bytes, read, bytes.Length - read);

                    if (n <= 0)
                    {
                        break;
                    }

                    read += n;
                }

                if (read < bytes.Length)
                {
                    Array.Resize(ref bytes, read);
                }

                return bytes;
            }
        }

        /// <summary>Reads the next line, without its end.</summary>
        /// <returns>The line; null at the end of the file.</returns>
        internal string ReadLine() => _cursor.ReadLine();

        /// <summary>Reads the next character.</summary>
        /// <returns>Its code, 10 for the end of a line; -1 at the end of the file.</returns>
        internal int ReadChar() => _cursor.ReadChar();

        /// <summary>
        /// Writes text, each <c>\n</c> as CR LF, and does not keep it back: what a routine wrote can be read by another
        /// <c>open</c> before this file is closed.
        /// </summary>
        /// <param name="text">The text.</param>
        internal void Write(string text)
        {
            byte[] bytes = _encoding.GetBytes(text.Replace("\n", "\r\n"));
            _stream.Write(bytes, 0, bytes.Length);
            _stream.Flush();
        }

        /// <summary>Closes the file; closing it again does nothing.</summary>
        internal void Close()
        {
            IsClosed = true;
            _stream?.Dispose();
        }
    }

    /// <summary>
    /// A text read line by line or character by character, as <c>read-line</c> and <c>read-char</c> read a file and the
    /// keyboard: CR LF, LF and a lone CR each end a line, and the last line needs no end.
    /// </summary>
    internal sealed class LispTextCursor
    {
        private string _text;
        private int _position;

        /// <summary>Makes a cursor at the start of a text.</summary>
        /// <param name="text">The text.</param>
        internal LispTextCursor(string text)
        {
            _text = text;
        }

        /// <summary>Gets a value saying whether no text is left.</summary>
        internal bool AtEnd => _position >= _text.Length;

        /// <summary>Replaces what is left with new text.</summary>
        /// <param name="text">The text.</param>
        internal void Refill(string text)
        {
            _text = text;
            _position = 0;
        }

        /// <summary>Reads the next line, without its end.</summary>
        /// <returns>The line; null when no text is left.</returns>
        internal string ReadLine()
        {
            if (AtEnd)
            {
                return null;
            }

            int end = _text.IndexOfAny(new[] { '\r', '\n' }, _position);

            if (end < 0)
            {
                string rest = _text.Substring(_position);
                _position = _text.Length;
                return rest;
            }

            string line = _text.Substring(_position, end - _position);
            _position = end + (_text[end] == '\r' && end + 1 < _text.Length && _text[end + 1] == '\n' ? 2 : 1);
            return line;
        }

        /// <summary>Reads the next character.</summary>
        /// <returns>Its code, 10 for any end of a line; -1 when no text is left.</returns>
        internal int ReadChar()
        {
            if (AtEnd)
            {
                return -1;
            }

            char c = _text[_position++];

            if (c == '\r')
            {
                if (_position < _text.Length && _text[_position] == '\n')
                {
                    _position++;
                }

                return 10;
            }

            return c;
        }
    }
}
