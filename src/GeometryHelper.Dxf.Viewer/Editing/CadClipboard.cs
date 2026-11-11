using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The clipboard COPYCLIP puts objects on and PASTECLIP takes them from: the window's is the Windows clipboard, shared by
    /// the drawings of the app and by every app that runs; a test gives a <see cref="CadMemoryClipboard"/>, so that it never
    /// touches the real one.
    /// </summary>
    public interface ICadClipboard
    {
        /// <summary>Puts objects copied, or text, on the clipboard, in place of what it held.</summary>
        /// <param name="data">What is copied.</param>
        void SetData(CadClipboardData data);

        /// <summary>Gets what the clipboard holds: objects of a drawing, plain text of another app, or null for neither.</summary>
        /// <returns>The data, or null.</returns>
        CadClipboardData GetData();
    }

    /// <summary>A clipboard held in memory, for a test or a host with no Windows clipboard.</summary>
    public sealed class CadMemoryClipboard : ICadClipboard
    {
        private CadClipboardData _data;

        /// <inheritdoc/>
        public void SetData(CadClipboardData data) => _data = data;

        /// <inheritdoc/>
        public CadClipboardData GetData() => _data;
    }

    /// <summary>
    /// What a clipboard holds for the presenter: objects copied, as a small DXF of their own (the objects in its model space
    /// with the layers, linetypes, styles and blocks they need, its <c>$INSBASE</c> the base point they are pasted by), with
    /// where they came from; and plain text, the words of the TEXTs and MTEXTs copied for other apps, or text another app
    /// copied, which PASTECLIP pastes as an MTEXT.
    /// </summary>
    public sealed class CadClipboardData
    {
        /// <summary>The name of the private clipboard format the objects go on the Windows clipboard under.</summary>
        public const string Format = "GeometryHelper.Drawing.Entities";

        private const string Header = "GeometryHelper clipboard 1";

        /// <summary>Makes what a clipboard holds.</summary>
        /// <param name="drawing">The DXF of the objects copied; null for text alone.</param>
        /// <param name="text">The plain text; null for none.</param>
        /// <param name="source">What tells the drawing copied from from any other, the same for every copy of one drawing open; null for text of another app.</param>
        /// <param name="sourceName">The name of the drawing copied from, as plan-level2.dxf; null for none.</param>
        /// <param name="spaceName">The space copied from, Model or a layout's name; null for none.</param>
        /// <param name="count">How many objects were copied.</param>
        /// <exception cref="ArgumentException">Thrown when there is neither a drawing nor text.</exception>
        public CadClipboardData(
            string drawing,
            string text,
            string source = null,
            string sourceName = null,
            string spaceName = null,
            int count = 0
        )
        {
            if (drawing == null && text == null)
            {
                throw new ArgumentException("A clipboard holds objects, text or both.", nameof(drawing));
            }

            Drawing = drawing;
            Text = text;
            Source = source;
            SourceName = sourceName;
            SpaceName = spaceName;
            Count = Math.Max(0, count);
        }

        /// <summary>Gets the DXF of the objects copied; null for text alone.</summary>
        public string Drawing { get; }

        /// <summary>Gets the plain text; null for none.</summary>
        public string Text { get; }

        /// <summary>Gets what tells the drawing copied from from any other; null for text of another app.</summary>
        public string Source { get; }

        /// <summary>Gets the name of the drawing copied from; null for none.</summary>
        public string SourceName { get; }

        /// <summary>Gets the space copied from; null for none.</summary>
        public string SpaceName { get; }

        /// <summary>Gets how many objects were copied.</summary>
        public int Count { get; }

        /// <summary>Gets whether the clipboard holds objects of a drawing.</summary>
        public bool HasObjects => Drawing != null;

        /// <summary>
        /// Writes the objects as the private clipboard format holds them: a few lines saying where they came from, then the
        /// DXF; null where there are no objects.
        /// </summary>
        /// <returns>The text of the format, or null.</returns>
        public string ToFormat()
        {
            if (Drawing == null)
            {
                return null;
            }

            var builder = new StringBuilder();
            builder.Append(Header).Append('\n');
            builder.Append("source=").Append(Source ?? string.Empty).Append('\n');
            builder.Append("name=").Append(SourceName ?? string.Empty).Append('\n');
            builder.Append("space=").Append(SpaceName ?? string.Empty).Append('\n');
            builder.Append("count=").Append(Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append('\n');
            builder.Append(Drawing);
            return builder.ToString();
        }

        /// <summary>Reads what <see cref="ToFormat"/> wrote, with the plain text beside it; null where the format is not one this writes.</summary>
        /// <param name="format">The text of the private format; null for none.</param>
        /// <param name="text">The plain text beside it; null for none.</param>
        /// <returns>The data, or null where there is neither.</returns>
        public static CadClipboardData FromFormat(string format, string text)
        {
            if (format == null || !format.StartsWith(Header + "\n", StringComparison.Ordinal))
            {
                return text == null ? null : new CadClipboardData(null, text);
            }

            string source = null;
            string name = null;
            string space = null;
            int count = 0;

            using (var reader = new StringReader(format))
            {
                reader.ReadLine();
                string line;

                while ((line = reader.ReadLine()) != null && line.Length > 0)
                {
                    int equals = line.IndexOf('=');

                    if (equals < 0)
                    {
                        continue;
                    }

                    string value = line.Substring(equals + 1);

                    switch (line.Substring(0, equals))
                    {
                        case "source":
                            source = value.Length == 0 ? null : value;
                            break;
                        case "name":
                            name = value.Length == 0 ? null : value;
                            break;
                        case "space":
                            space = value.Length == 0 ? null : value;
                            break;
                        case "count":
                            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out count);
                            break;
                    }
                }

                return new CadClipboardData(reader.ReadToEnd(), text, source, name, space, count);
            }
        }
    }
}
