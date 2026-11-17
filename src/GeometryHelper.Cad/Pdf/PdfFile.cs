using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The structure of a PDF file: its cross-reference tables and streams (also the hybrid and the incremental ones), its
    /// object streams, and the indirect objects found through them; a file whose tables are broken is rebuilt by scanning for
    /// its objects. Encrypted files are refused. Nothing is drawn here: this is the reading of objects.
    /// </summary>
    internal sealed class PdfFile
    {
        private readonly byte[] _data;
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly Dictionary<int, object> _cache = new Dictionary<int, object>();
        private readonly Dictionary<int, List<KeyValuePair<int, object>>> _streams =
            new Dictionary<int, List<KeyValuePair<int, object>>>();
        private bool _rebuilt;

        private struct Entry
        {
            public int Kind; // 1 at an offset, 2 in an object stream
            public long A; // offset, or the number of the object stream
            public long B; // generation, or the index in the object stream
        }

        private PdfFile(byte[] data)
        {
            _data = data;
        }

        /// <summary>Gets the trailer: the dictionary that names the root.</summary>
        public PdfDict Trailer { get; private set; } = new PdfDict();

        /// <summary>Opens a PDF from its bytes.</summary>
        /// <param name="data">The whole file.</param>
        /// <returns>The file.</returns>
        /// <exception cref="PdfException">Thrown when the bytes are no PDF, the file is encrypted, or it has no pages to find.</exception>
        public static PdfFile Open(byte[] data)
        {
            if (data == null || data.Length < 16 || IndexOf(data, "%PDF-", 0, Math.Min(data.Length, 1024)) < 0)
            {
                throw new PdfException("The file is not a PDF.");
            }

            var file = new PdfFile(data);

            try
            {
                file.ReadTables();
            }
            catch (PdfException)
            {
                file.Rebuild();
            }
            catch (FormatException)
            {
                file.Rebuild();
            }

            if (
                !(file.Resolve(file.Trailer.TryGetValue("Root", out object root) ? root : null) is PdfDict catalog)
                || !catalog.ContainsKey("Pages")
            )
            {
                file.Rebuild();
            }

            if (file.Trailer.ContainsKey("Encrypt"))
            {
                throw new PdfException("The PDF is protected by a password.");
            }

            if (!(file.Resolve(file.Trailer.TryGetValue("Root", out object found) ? found : null) is PdfDict))
            {
                throw new PdfException("The PDF has no pages to find.");
            }

            return file;
        }

        #region Resolving

        /// <summary>Follows references to the object they name; null for a missing object or a loop.</summary>
        public object Resolve(object value)
        {
            for (int depth = 0; depth < 32; depth++)
            {
                if (!(value is PdfRef reference))
                {
                    return value;
                }

                value = Load(reference.Number);
            }

            return null;
        }

        /// <summary>The object as a dictionary; a stream gives its dictionary; null where it is neither.</summary>
        public PdfDict Dict(object value)
        {
            object resolved = Resolve(value);
            return resolved as PdfDict ?? (resolved as PdfStream)?.Dictionary;
        }

        /// <summary>The object as a stream; null where it is none.</summary>
        public PdfStream Stream(object value) => Resolve(value) as PdfStream;

        /// <summary>The object as an array, its items not resolved; null where it is none.</summary>
        public List<object> Array(object value) => Resolve(value) as List<object>;

        /// <summary>The object as a number; the fallback where it is none.</summary>
        public double Number(object value, double fallback = 0.0) => Resolve(value) is double d ? d : fallback;

        /// <summary>The object as a name's text; null where it is none.</summary>
        public string Name(object value) => (Resolve(value) as PdfName)?.Value;

        /// <summary>The object as a string's text; null where it is none.</summary>
        public string Text(object value) => (Resolve(value) as PdfString)?.Text;

        /// <summary>The value of a key of a dictionary, resolved; null where the dictionary has none.</summary>
        public object Get(PdfDict dictionary, string key) =>
            dictionary != null && dictionary.TryGetValue(key, out object value) ? Resolve(value) : null;

        /// <summary>The numbers of an array, each resolved; null where the object is no array of numbers.</summary>
        public double[] Numbers(object value)
        {
            List<object> array = Array(value);

            if (array == null)
            {
                return null;
            }

            var numbers = new double[array.Count];

            for (int i = 0; i < numbers.Length; i++)
            {
                numbers[i] = Number(array[i]);
            }

            return numbers;
        }

        #endregion

        #region Streams

        /// <summary>
        /// The bytes of a stream with its filters undone, up to the first picture filter: of which the name and parameters are
        /// given, null where the stream has none.
        /// </summary>
        public byte[] Decode(PdfStream stream, out string imageFilter, out PdfDict imageParameters)
        {
            imageFilter = null;
            imageParameters = null;
            byte[] bytes = stream.Raw();
            object filter = Resolve(
                stream.Dictionary.TryGetValue("Filter", out object f) ? f
                : stream.Dictionary.TryGetValue("F", out object f2) && !(Resolve(f2) is PdfString) ? f2
                : null
            );
            object parameters = Resolve(
                stream.Dictionary.TryGetValue("DecodeParms", out object p) ? p
                : stream.Dictionary.TryGetValue("DP", out object p2) ? p2
                : null
            );
            var names = new List<string>();
            var settings = new List<PdfDict>();

            if (filter is PdfName single)
            {
                names.Add(single.Value);
                settings.Add(Dict(parameters is List<object> one && one.Count > 0 ? one[0] : parameters));
            }
            else if (filter is List<object> list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    names.Add(Name(list[i]));
                    List<object> all = parameters as List<object>;
                    settings.Add(
                        Dict(
                            all != null && i < all.Count ? all[i]
                            : i == 0 && all == null ? parameters
                            : null
                        )
                    );
                }
            }

            for (int i = 0; i < names.Count; i++)
            {
                if (names[i] == null)
                {
                    continue;
                }

                if (PdfFilters.IsImageFilter(names[i]))
                {
                    imageFilter = names[i];
                    imageParameters = settings[i];
                    return bytes;
                }

                bytes = PdfFilters.Apply(names[i], bytes, settings[i]);
            }

            return bytes;
        }

        /// <summary>The bytes of a stream with every filter undone; a stream that ends in a picture filter gives its bytes as they are.</summary>
        public byte[] Decode(PdfStream stream) => Decode(stream, out _, out _);

        #endregion

        #region Objects

        private object Load(int number)
        {
            if (_cache.TryGetValue(number, out object cached))
            {
                return cached;
            }

            object value = null;

            if (_entries.TryGetValue(number, out Entry entry))
            {
                try
                {
                    value =
                        entry.Kind == 1 ? ReadAt(entry.A, number) : ReadCompressed((int)entry.A, (int)entry.B, number);
                }
                catch (PdfException)
                {
                    value = null;
                }
                catch (FormatException)
                {
                    value = null;
                }

                if (value == null && entry.Kind == 1 && !_rebuilt)
                {
                    // The table pointed at no object of that number: the tables of the file are not to be trusted.
                    Rebuild();
                    return Load(number);
                }
            }

            _cache[number] = value;
            return value;
        }

        private object ReadAt(long offset, int number)
        {
            if (offset < 0 || offset >= _data.Length)
            {
                return null;
            }

            var parser = new PdfParser(_data, (int)offset, _data.Length, true);
            object n = parser.Read();
            object g = parser.Read();
            object keyword = parser.Read();

            if (
                !(n is double numberRead)
                || !(g is double)
                || !(keyword is PdfKeyword k)
                || k.Value != "obj"
                || (int)numberRead != number
            )
            {
                return null;
            }

            object body = parser.Read();

            if (body is PdfDict dictionary)
            {
                int mark = parser.Position;
                parser.SkipWhite();

                if (parser.Position + 6 <= _data.Length && Matches(parser.Position, "stream"))
                {
                    int start = parser.Position + 6;

                    if (start < _data.Length && _data[start] == 13)
                    {
                        start++;
                    }

                    if (start < _data.Length && _data[start] == 10)
                    {
                        start++;
                    }

                    return new PdfStream(dictionary, _data, start, StreamLength(dictionary, start));
                }

                parser.Position = mark;
            }

            return body;
        }

        private int StreamLength(PdfDict dictionary, int start)
        {
            int length = -1;

            if (dictionary.TryGetValue("Length", out object value))
            {
                // A /Length that is a reference to the object being read is resolved with no cache of it: read plainly.
                object resolved =
                    value is PdfRef reference && _entries.TryGetValue(reference.Number, out Entry e) && e.Kind == 1
                        ? ReadAt(e.A, reference.Number)
                        : Resolve(value);

                if (resolved is double d && d >= 0 && d < _data.Length)
                {
                    length = (int)d;
                }
            }

            if (length >= 0 && start + length <= _data.Length)
            {
                int after = start + length;

                while (after < _data.Length && PdfParser.IsWhite(_data[after]))
                {
                    after++;
                }

                if (Matches(after, "endstream"))
                {
                    return length;
                }
            }

            int end = IndexOf(_data, "endstream", start, _data.Length);

            if (end < 0)
            {
                return Math.Max(0, _data.Length - start);
            }

            // The line break that comes before "endstream" is not part of the data.
            if (end > start && _data[end - 1] == 10)
            {
                end--;
            }

            if (end > start && _data[end - 1] == 13)
            {
                end--;
            }

            return end - start;
        }

        private object ReadCompressed(int stream, int index, int number)
        {
            if (!_streams.TryGetValue(stream, out List<KeyValuePair<int, object>> objects))
            {
                objects = new List<KeyValuePair<int, object>>();
                _streams[stream] = objects;

                if (Load(stream) is PdfStream s)
                {
                    byte[] bytes = Decode(s);
                    int count = (int)Number(s.Dictionary.TryGetValue("N", out object n) ? n : null);
                    int first = (int)Number(s.Dictionary.TryGetValue("First", out object fo) ? fo : null);
                    var head = new PdfParser(bytes, 0, bytes.Length, false);
                    var offsets = new List<KeyValuePair<int, int>>();

                    for (int i = 0; i < count; i++)
                    {
                        if (!(head.Read() is double num) || !(head.Read() is double off))
                        {
                            break;
                        }

                        offsets.Add(new KeyValuePair<int, int>((int)num, (int)off));
                    }

                    foreach (KeyValuePair<int, int> pair in offsets)
                    {
                        var body = new PdfParser(bytes, first + pair.Value, bytes.Length, true);
                        objects.Add(new KeyValuePair<int, object>(pair.Key, body.Read()));
                    }
                }
            }

            if (index >= 0 && index < objects.Count && objects[index].Key == number)
            {
                return objects[index].Value;
            }

            foreach (KeyValuePair<int, object> pair in objects)
            {
                if (pair.Key == number)
                {
                    return pair.Value;
                }
            }

            return null;
        }

        #endregion

        #region Tables

        private void ReadTables()
        {
            int search = Math.Max(0, _data.Length - 2048);
            int at = LastIndexOf(_data, "startxref", search);

            if (at < 0)
            {
                throw new PdfException("The file has no cross-reference table.");
            }

            var parser = new PdfParser(_data, at + 9, _data.Length, false);

            if (!(parser.Read() is double start))
            {
                throw new PdfException("The file's cross-reference table is not where it says.");
            }

            var seen = new HashSet<long>();
            long next = (long)start;
            bool first = true;

            while (next >= 0 && seen.Add(next))
            {
                PdfDict trailer = ReadSection(next);

                if (first)
                {
                    Trailer = trailer;
                    first = false;
                }
                else
                {
                    foreach (KeyValuePair<string, object> pair in trailer)
                    {
                        if (!Trailer.ContainsKey(pair.Key))
                        {
                            Trailer[pair.Key] = pair.Value;
                        }
                    }
                }

                next = trailer.TryGetValue("Prev", out object prev) && prev is double p ? (long)p : -1;
            }
        }

        private PdfDict ReadSection(long offset)
        {
            if (offset < 0 || offset >= _data.Length)
            {
                throw new PdfException("The cross-reference table is outside the file.");
            }

            var parser = new PdfParser(_data, (int)offset, _data.Length, true);
            parser.SkipWhite();

            if (Matches(parser.Position, "xref"))
            {
                parser.Position += 4;
                return ReadTable(parser);
            }

            object n = parser.Read();
            object g = parser.Read();
            object keyword = parser.Read();

            if (!(n is double) || !(g is double) || !(keyword is PdfKeyword k) || k.Value != "obj")
            {
                throw new PdfException("The cross-reference table is not where it says.");
            }

            if (!(Load2(parser) is PdfStream stream))
            {
                throw new PdfException("The cross-reference stream is missing.");
            }

            ReadXrefStream(stream);
            return stream.Dictionary;
        }

        /// <summary>Reads the object after "obj" at the parser, with its stream where it has one.</summary>
        private object Load2(PdfParser parser)
        {
            object body = parser.Read();

            if (body is PdfDict dictionary)
            {
                parser.SkipWhite();

                if (Matches(parser.Position, "stream"))
                {
                    int start = parser.Position + 6;

                    if (start < _data.Length && _data[start] == 13)
                    {
                        start++;
                    }

                    if (start < _data.Length && _data[start] == 10)
                    {
                        start++;
                    }

                    return new PdfStream(dictionary, _data, start, StreamLength(dictionary, start));
                }
            }

            return body;
        }

        private PdfDict ReadTable(PdfParser parser)
        {
            while (true)
            {
                parser.SkipWhite();
                int mark = parser.Position;
                object first = parser.Read();

                if (first is PdfKeyword keyword && keyword.Value == "trailer")
                {
                    break;
                }

                if (!(first is double startNumber) || !(parser.Read() is double count))
                {
                    parser.Position = mark;
                    throw new PdfException("The cross-reference table is damaged.");
                }

                for (int i = 0; i < (int)count; i++)
                {
                    object offset = parser.Read();
                    object generation = parser.Read();
                    object kind = parser.Read();

                    if (!(offset is double o) || !(generation is double gen) || !(kind is PdfKeyword k))
                    {
                        throw new PdfException("The cross-reference table is damaged.");
                    }

                    int number = (int)startNumber + i;

                    if (k.Value == "n" && !_entries.ContainsKey(number))
                    {
                        _entries[number] = new Entry
                        {
                            Kind = 1,
                            A = (long)o,
                            B = (long)gen,
                        };
                    }
                }
            }

            if (!(parser.Read() is PdfDict trailer))
            {
                throw new PdfException("The cross-reference table has no trailer.");
            }

            if (trailer.TryGetValue("XRefStm", out object hybrid) && hybrid is double h)
            {
                try
                {
                    ReadSection((long)h);
                }
                catch (PdfException)
                {
                    // The stream of a hybrid file is an extra: the table holds the rest.
                }
            }

            return trailer;
        }

        private void ReadXrefStream(PdfStream stream)
        {
            PdfDict dictionary = stream.Dictionary;
            double[] widths = Numbers(dictionary.TryGetValue("W", out object w) ? w : null);

            if (widths == null || widths.Length < 3)
            {
                throw new PdfException("The cross-reference stream has no field widths.");
            }

            int w0 = (int)widths[0];
            int w1 = (int)widths[1];
            int w2 = (int)widths[2];
            double[] index = Numbers(dictionary.TryGetValue("Index", out object ix) ? ix : null);

            if (index == null)
            {
                index = new[] { 0.0, Number(dictionary.TryGetValue("Size", out object size) ? size : null) };
            }

            byte[] bytes = Decode(stream);
            int row = w0 + w1 + w2;
            int at = 0;

            for (int s = 0; s + 1 < index.Length; s += 2)
            {
                for (int i = 0; i < (int)index[s + 1] && at + row <= bytes.Length; i++, at += row)
                {
                    long type = w0 == 0 ? 1 : Field(bytes, at, w0);
                    long a = Field(bytes, at + w0, w1);
                    long b = Field(bytes, at + w0 + w1, w2);
                    int number = (int)index[s] + i;

                    if (_entries.ContainsKey(number) || (type != 1 && type != 2))
                    {
                        continue;
                    }

                    _entries[number] = new Entry
                    {
                        Kind = (int)type,
                        A = a,
                        B = b,
                    };
                }
            }
        }

        private static long Field(byte[] bytes, int at, int width)
        {
            long value = 0;

            for (int i = 0; i < width; i++)
            {
                value = (value << 8) | bytes[at + i];
            }

            return value;
        }

        /// <summary>Rebuilds the tables by scanning the file for "n g obj", the trailer and any cross-reference stream's dictionary.</summary>
        private void Rebuild()
        {
            _rebuilt = true;
            _entries.Clear();
            _cache.Clear();
            _streams.Clear();
            PdfDict trailer = null;
            var catalogAt = new List<int>();
            int i = 0;

            while (i < _data.Length)
            {
                int obj = IndexOf(_data, " obj", i, _data.Length);

                if (obj < 0)
                {
                    break;
                }

                // Back over the generation and the number.
                int p = obj - 1;

                while (p >= 0 && _data[p] >= '0' && _data[p] <= '9')
                {
                    p--;
                }

                int generationStart = p + 1;

                if (p >= 0 && PdfParser.IsWhite(_data[p]) && generationStart < obj)
                {
                    while (p >= 0 && PdfParser.IsWhite(_data[p]))
                    {
                        p--;
                    }

                    int numberEnd = p + 1;

                    while (p >= 0 && _data[p] >= '0' && _data[p] <= '9')
                    {
                        p--;
                    }

                    int numberStart = p + 1;

                    if (
                        numberStart < numberEnd
                        && int.TryParse(
                            Encoding.ASCII.GetString(_data, numberStart, numberEnd - numberStart),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out int number
                        )
                    )
                    {
                        _entries[number] = new Entry
                        {
                            Kind = 1,
                            A = numberStart,
                            B = 0,
                        };
                    }
                }

                i = obj + 4;
            }

            int at = 0;

            while ((at = IndexOf(_data, "trailer", at, _data.Length)) >= 0)
            {
                var parser = new PdfParser(_data, at + 7, _data.Length, true);

                if (parser.Read() is PdfDict found && found.ContainsKey("Root"))
                {
                    trailer = found;
                }

                at += 7;
            }

            // Object streams hold objects the scan could not see: read each one and enter what it holds.
            foreach (int number in new List<int>(_entries.Keys))
            {
                object value = null;

                try
                {
                    value = ReadAt(_entries[number].A, number);
                }
                catch (PdfException)
                {
                    continue;
                }

                if (value is PdfStream stream)
                {
                    string type = Name(stream.Dictionary.TryGetValue("Type", out object t) ? t : null);

                    if (type == "XRef" && trailer == null && stream.Dictionary.ContainsKey("Root"))
                    {
                        trailer = stream.Dictionary;
                    }

                    if (type == "ObjStm")
                    {
                        try
                        {
                            byte[] bytes = Decode(stream);
                            int count = (int)Number(stream.Dictionary.TryGetValue("N", out object n) ? n : null);
                            var head = new PdfParser(bytes, 0, bytes.Length, false);

                            for (int k = 0; k < count; k++)
                            {
                                if (head.Read() is double num && head.Read() is double)
                                {
                                    _entries[(int)num] = new Entry
                                    {
                                        Kind = 2,
                                        A = number,
                                        B = k,
                                    };
                                }
                            }
                        }
                        catch (PdfException)
                        {
                            // An object stream that cannot be inflated holds nothing the page needs that can be found.
                        }
                    }
                }
                else if (
                    value is PdfDict dictionary
                    && trailer == null
                    && Name(dictionary.TryGetValue("Type", out object t2) ? t2 : null) == "Catalog"
                )
                {
                    catalogAt.Add(number);
                }
            }

            if (trailer == null)
            {
                trailer = new PdfDict();

                if (catalogAt.Count > 0)
                {
                    trailer["Root"] = new PdfRef(catalogAt[catalogAt.Count - 1], 0);
                }
            }

            Trailer = trailer;
            _cache.Clear();
        }

        #endregion

        #region Searching

        private bool Matches(int at, string word)
        {
            if (at < 0 || at + word.Length > _data.Length)
            {
                return false;
            }

            for (int i = 0; i < word.Length; i++)
            {
                if (_data[at + i] != word[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static int IndexOf(byte[] data, string word, int from, int to)
        {
            byte first = (byte)word[0];
            int last = Math.Min(to, data.Length) - word.Length;

            for (int i = Math.Max(0, from); i <= last; i++)
            {
                if (data[i] != first)
                {
                    continue;
                }

                int k = 1;

                while (k < word.Length && data[i + k] == word[k])
                {
                    k++;
                }

                if (k == word.Length)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int LastIndexOf(byte[] data, string word, int from)
        {
            for (int i = data.Length - word.Length; i >= from; i--)
            {
                int k = 0;

                while (k < word.Length && data[i + k] == word[k])
                {
                    k++;
                }

                if (k == word.Length)
                {
                    return i;
                }
            }

            return -1;
        }

        #endregion
    }
}
