using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// One section of a sheet list file: its header, as <c>Target</c> or <c>DWF6Sheet:plan-level2-Layout1</c>, and its
    /// <c>key=value</c> lines in order.
    /// </summary>
    public sealed class CadDsdSection
    {
        private readonly List<KeyValuePair<string, string>> _pairs = new List<KeyValuePair<string, string>>();

        /// <summary>Makes an empty section.</summary>
        /// <param name="name">The header between the brackets.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public CadDsdSection(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>Gets the header between the brackets.</summary>
        public string Name { get; }

        /// <summary>Gets the lines in the order of the file.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Pairs => _pairs;

        /// <summary>Gets the value of a key, letters of either case the same; the last line of the key counts.</summary>
        /// <param name="key">The key.</param>
        /// <returns>The value; null where the section has no such line.</returns>
        public string Get(string key)
        {
            for (int i = _pairs.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_pairs[i].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return _pairs[i].Value;
                }
            }

            return null;
        }

        /// <summary>Sets the value of a key where the section has it, in place, else adds the line at the end.</summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value; null is written as nothing.</param>
        public void Set(string key, string value)
        {
            for (int i = _pairs.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_pairs[i].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    _pairs[i] = new KeyValuePair<string, string>(_pairs[i].Key, value ?? string.Empty);
                    return;
                }
            }

            _pairs.Add(new KeyValuePair<string, string>(key, value ?? string.Empty));
        }

        internal void Add(string key, string value) =>
            _pairs.Add(new KeyValuePair<string, string>(key, value ?? string.Empty));
    }

    /// <summary>
    /// A sheet list file, the .dsd (Drawing Set Description) of AutoCAD's Publish: a text file of the INI kind that keeps the
    /// sheets of the Publish dialog and where they go. It is read and written as plain sections, so that what a list holds
    /// beyond what this program knows, such as AutoCAD's <c>[PdfOptions]</c>, comes back out as it went in.
    /// <see cref="CadPublishList.Load"/> and <see cref="CadPublishList.Save"/> turn it into sheets and options.
    /// </summary>
    /// <remarks>
    /// The file is made of <c>[DWF6Version]</c> and <c>[DWF6MinorVersion]</c>; a <c>[DWF6Sheet:name]</c> for each sheet, with
    /// <c>DWG</c> (the drawing's path), <c>Layout</c> (<c>Model</c> or the layout's name), <c>Setup</c> (empty for the
    /// sheet's own page setup, else the page setup's name, and <c>name|path</c> where it comes from another drawing),
    /// <c>OriginalSheetPath</c>, <c>Has Plot Port</c> and <c>Has3DDWF</c>; <c>[Target]</c> with <c>Type</c> (0 single-sheet
    /// DWF, 1 multi-sheet DWF, 2 the plotter named in the page setup, 3 single-sheet DWFx, 4 multi-sheet DWFx, 5 single-sheet
    /// PDF, 6 multi-sheet PDF, the values of AutoCAD's <c>SheetType</c>), <c>DWF</c> (the multi-sheet file), <c>OUT</c> (the
    /// output folder) and <c>PWD</c>; and <c>[SheetSet Properties]</c>, of which <c>NoOfCopies</c> and <c>ViewFile</c> (Open
    /// in viewer when done) are read. A file of ASCII letters is written as it is, CRLF lines; one with other letters is
    /// written as UTF-16 with its byte order mark. A file is read as UTF-16 or UTF-8 by its byte order mark, else as UTF-8
    /// where that is valid, else as Latin-1.
    /// </remarks>
    public sealed class CadDsdFile
    {
        private readonly List<CadDsdSection> _sections = new List<CadDsdSection>();

        /// <summary>Gets the sections in the order of the file.</summary>
        public IList<CadDsdSection> Sections => _sections;

        /// <summary>Finds a section by its header, letters of either case the same.</summary>
        /// <param name="name">The header.</param>
        /// <returns>The first section of the name; null where there is none.</returns>
        public CadDsdSection Find(string name)
        {
            foreach (CadDsdSection section in _sections)
            {
                if (string.Equals(section.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return section;
                }
            }

            return null;
        }

        /// <summary>
        /// Reads the text of a sheet list. A line before the first header, and a line that is no <c>key=value</c>, is passed
        /// over; a blank line and one that begins with a semicolon are too.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The sections.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        /// <exception cref="FormatException">Thrown when the text is no sheet list: it has no <c>[DWF6Version]</c> section.</exception>
        public static CadDsdFile Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var file = new CadDsdFile();
            CadDsdSection current = null;

            foreach (string raw in text.TrimStart('﻿').Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
            {
                string line = raw.Trim();

                if (line.Length == 0 || line[0] == ';')
                {
                    continue;
                }

                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    current = new CadDsdSection(line.Substring(1, line.Length - 2));
                    file._sections.Add(current);
                    continue;
                }

                int equals = line.IndexOf('=');

                if (current != null && equals > 0)
                {
                    current.Add(line.Substring(0, equals).Trim(), line.Substring(equals + 1).Trim());
                }
            }

            if (file.Find("DWF6Version") == null)
            {
                throw new FormatException("The file is no sheet list: it has no [DWF6Version] section.");
            }

            return file;
        }

        /// <summary>Reads a sheet list file.</summary>
        /// <param name="path">The file.</param>
        /// <returns>The sections.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        /// <exception cref="FormatException">Thrown when the file is no sheet list.</exception>
        public static CadDsdFile Read(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            return Parse(Decode(File.ReadAllBytes(path)));
        }

        /// <summary>Gets the text of the list, CRLF lines, the sections in order and a line each.</summary>
        /// <returns>The text.</returns>
        public string ToText()
        {
            var text = new StringBuilder();

            foreach (CadDsdSection section in _sections)
            {
                text.Append('[').Append(section.Name).Append("]\r\n");

                foreach (KeyValuePair<string, string> pair in section.Pairs)
                {
                    text.Append(pair.Key).Append('=').Append(pair.Value).Append("\r\n");
                }
            }

            return text.ToString();
        }

        /// <summary>Writes the list to a file, replacing a file of the name.</summary>
        /// <param name="path">The file.</param>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
        public void Write(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            string text = ToText();
            bool ascii = true;

            foreach (char each in text)
            {
                if (each > 0x7F)
                {
                    ascii = false;
                    break;
                }
            }

            byte[] body = ascii ? Encoding.ASCII.GetBytes(text) : Encoding.Unicode.GetBytes(text);
            byte[] bytes = ascii ? body : Combine(Encoding.Unicode.GetPreamble(), body);
            File.WriteAllBytes(path, bytes);
        }

        private static byte[] Combine(byte[] first, byte[] second)
        {
            var all = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, all, 0, first.Length);
            Buffer.BlockCopy(second, 0, all, first.Length, second.Length);
            return all;
        }

        private static string Decode(byte[] bytes)
        {
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            }

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            }

            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (ArgumentException)
            {
                return Encoding.GetEncoding(28591).GetString(bytes);
            }
        }
    }
}
