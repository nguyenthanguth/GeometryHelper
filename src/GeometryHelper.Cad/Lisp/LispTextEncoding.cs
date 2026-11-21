using System;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// The text encodings of AutoLISP files, as AutoCAD 2021 measured them: a file read with no encoding named is UTF-8 when
    /// it begins with the UTF-8 byte order mark or is UTF-8 from end to end, UTF-16 when it begins with that mark, and the
    /// system's ANSI code page otherwise; writing with no encoding named is ANSI, and a character the code page lacks is
    /// written as <c>\U+XXXX</c> (a file with <c>đ</c> in a routine's text gets <c>\U+0111</c>). Shared by <c>open</c> and
    /// <c>load</c>.
    /// </summary>
    internal static class LispTextEncoding
    {
        private static readonly Lazy<Encoding> s_ansiWithEscapes = new Lazy<Encoding>(() =>
            Encoding.GetEncoding(Encoding.Default.CodePage, new EscapeFallback(), DecoderFallback.ReplacementFallback)
        );

        /// <summary>Gets the system's ANSI code page, what a file is read as when it is not UTF-8.</summary>
        internal static Encoding Ansi => Encoding.Default;

        /// <summary>
        /// Chooses how to read the bytes of a file when the routine named no encoding: a UTF-8 byte order mark, or a UTF-16
        /// one (little or big end, the second ours), decides; otherwise the bytes are UTF-8 when all of them are, and ANSI
        /// when not.
        /// </summary>
        /// <param name="bytes">The whole file.</param>
        /// <param name="bomLength">The length of the byte order mark to skip: 3, 2 or 0.</param>
        /// <returns>The encoding to read the text with.</returns>
        internal static Encoding Detect(byte[] bytes, out int bomLength)
        {
            bomLength = 0;

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                bomLength = 3;
                return new UTF8Encoding(false, true);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                bomLength = 2;
                return new UnicodeEncoding(false, false);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                bomLength = 2;
                return new UnicodeEncoding(true, false);
            }

            return IsUtf8(bytes) ? new UTF8Encoding(false, true) : Ansi;
        }

        /// <summary>Tells whether the bytes are UTF-8 from end to end, plain ASCII included.</summary>
        /// <param name="bytes">The bytes.</param>
        /// <returns>True when every byte belongs to a well-formed UTF-8 sequence.</returns>
        internal static bool IsUtf8(byte[] bytes)
        {
            try
            {
                new UTF8Encoding(false, true).GetCharCount(bytes);
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }

        /// <summary>
        /// Makes the encoding a file is written with when the routine named none: the ANSI code page, with each character it
        /// lacks written as <c>\U+XXXX</c>, four upper-case hexadecimal digits (a pair of surrogates as two such escapes).
        /// </summary>
        /// <returns>The encoding.</returns>
        internal static Encoding AnsiWithEscapes() => s_ansiWithEscapes.Value;

        /// <summary>
        /// Reads UTF-8 as <c>open</c> does with <c>"utf8"</c>: each byte that is not part of a well-formed sequence becomes a
        /// question mark, one for one, so that an ANSI <c>é</c> read as UTF-8 is <c>?</c> and the text round-trips in length.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <param name="start">Where the text begins, after any byte order mark.</param>
        /// <returns>The text.</returns>
        internal static string DecodeUtf8(byte[] bytes, int start)
        {
            var text = new StringBuilder(bytes.Length - start);
            int i = start;

            while (i < bytes.Length)
            {
                byte lead = bytes[i];

                if (lead < 0x80)
                {
                    text.Append((char)lead);
                    i++;
                    continue;
                }

                int extra;
                int low = 0x80;
                int high = 0xBF;
                int code;

                if (lead >= 0xC2 && lead <= 0xDF)
                {
                    extra = 1;
                    code = lead & 0x1F;
                }
                else if (lead >= 0xE0 && lead <= 0xEF)
                {
                    extra = 2;
                    code = lead & 0x0F;
                    low = lead == 0xE0 ? 0xA0 : 0x80;
                    high = lead == 0xED ? 0x9F : 0xBF;
                }
                else if (lead >= 0xF0 && lead <= 0xF4)
                {
                    extra = 3;
                    code = lead & 0x07;
                    low = lead == 0xF0 ? 0x90 : 0x80;
                    high = lead == 0xF4 ? 0x8F : 0xBF;
                }
                else
                {
                    text.Append('?');
                    i++;
                    continue;
                }

                bool good = i + extra < bytes.Length;

                for (int k = 1; good && k <= extra; k++)
                {
                    byte next = bytes[i + k];
                    good = next >= (k == 1 ? low : 0x80) && next <= (k == 1 ? high : 0xBF);
                    code = (code << 6) | (next & 0x3F);
                }

                if (!good)
                {
                    text.Append('?');
                    i++;
                    continue;
                }

                text.Append(char.ConvertFromUtf32(code));
                i += extra + 1;
            }

            return text.ToString();
        }

        /// <summary>Writes the characters a code page cannot hold as <c>\U+XXXX</c>.</summary>
        private sealed class EscapeFallback : EncoderFallback
        {
            public override int MaxCharCount => 14;

            public override EncoderFallbackBuffer CreateFallbackBuffer() => new EscapeBuffer();
        }

        private sealed class EscapeBuffer : EncoderFallbackBuffer
        {
            private string _text = "";
            private int _next;

            public override int Remaining => _text.Length - _next;

            public override bool Fallback(char charUnknown, int index)
            {
                _text = Escape(charUnknown);
                _next = 0;
                return true;
            }

            public override bool Fallback(char charUnknownHigh, char charUnknownLow, int index)
            {
                _text = Escape(charUnknownHigh) + Escape(charUnknownLow);
                _next = 0;
                return true;
            }

            public override char GetNextChar() => _next < _text.Length ? _text[_next++] : '\0';

            public override bool MovePrevious()
            {
                if (_next == 0)
                {
                    return false;
                }

                _next--;
                return true;
            }

            public override void Reset()
            {
                _text = "";
                _next = 0;
            }

            private static string Escape(char c) => "\\U+" + ((int)c).ToString("X4", CultureInfo.InvariantCulture);
        }
    }
}
