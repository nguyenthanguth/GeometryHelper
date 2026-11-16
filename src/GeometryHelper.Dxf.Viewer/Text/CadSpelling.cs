using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The words SPELL checks: the words of a TEXT or an MTEXT (the format codes of an MTEXT and the <c>%%</c> codes of a TEXT
    /// left out), the built-in English dictionary (the SCOWL word lists in American and British spelling, about 90 000 words
    /// sorted by how common they are, with some 700 drafting terms; the plain endings -s, -es, -ed, -ing, -ly, -er, -est and
    /// 's are tried), a custom dictionary of words one to a line, and the words nearest a misspelled one. A word of one
    /// letter, with a digit in it or in capitals is passed over, as AutoCAD's Check Spelling Settings pass over words in
    /// uppercase and words with numbers.
    /// </summary>
    internal static class CadSpelling
    {
        /// <summary>A word of a text: where it starts in the text as written, how long it is, and the letters.</summary>
        internal sealed class Word
        {
            internal Word(int start, string text)
            {
                Start = start;
                Text = text;
            }

            /// <summary>Gets where the word starts in the text as written.</summary>
            internal int Start { get; }

            /// <summary>Gets the word.</summary>
            internal string Text { get; }
        }

        // The drafting terms and British spellings the app had before the main dictionary, kept as it lacks some of them.
        private static readonly string[] Legacy = (
            "a about above across add after again against all also always an and any are around as at away "
            + "back be because been before below between both but by can cannot could do does down during each "
            + "end even every few first for from front get give go good had has have he her here high his how if "
            + "in into is it its just keep last left less like long low made make many may more most must my near "
            + "need never new next no not now of off old on once one only or other our out over own part per "
            + "right same see set she should side since so some such than that the their them then there these "
            + "they this those through to too top two under up upon use used very was we well were what when "
            + "where which while who why will with within without would yes you your zero three four five six "
            + "seven eight nine ten hundred thousand third second fourth fifth hello world word text note notes "
            + "name date number title page sheet scale size type value list line lines point points area length "
            + "width height depth thickness diameter radius angle degree degrees distance level plan section "
            + "detail details elevation view views drawing drawings model layout layer layers block blocks "
            + "dimension dimensions style styles font fonts color colour material materials steel concrete "
            + "timber wood iron aluminium aluminum stainless galvanized galvanised painted welded welds weld "
            + "bolt bolts nut nuts washer washers plate plates beam beams column columns brace braces bracing "
            + "channel angle angles pipe pipes tube tubes bar bars rod rods rebar mesh slab slabs wall walls "
            + "floor floors roof roofs foundation foundations footing footings pile piles base bottom top "
            + "center centre centreline centerline edge edges corner corners hole holes slot slots gap gaps "
            + "joint joints connection connections splice supports support bearing grout anchor anchors "
            + "stiffener stiffeners flange flanges web webs cleat cleats lug lugs stud studs shear moment load "
            + "loads dead live wind seismic design designed check checked approved approve issue issued revision "
            + "revisions revised drawn project client contract contractor engineer engineers architect "
            + "architects general notes typical typ minimum maximum min max nominal actual required optional "
            + "note refer refers reference references see schedule table tables item items mark marks "
            + "quantity quantities weight weights mass total unit units metric imperial millimetre millimetres "
            + "millimeter millimeters metre metres meter meters inch inches foot feet yard yards "
            + "tolerance tolerances finish finishes surface surfaces coating coatings paint primer "
            + "galvanizing hot dip cold formed rolled cut cuts cutting drill drilled drilling bend bent "
            + "bending fit fitted fitting fixed fix fixing install installed installation erect erected "
            + "erection assembly assemble assembled fabricate fabricated fabrication shop site field "
            + "supply supplied provide provided include included including excluding exclude unless "
            + "otherwise specified specification specifications standard standards code codes grade grades "
            + "class classes strength yield ultimate tensile compression compressive tension tensile "
            + "stress strain deflection limit limits span spans clear opening openings door doors window "
            + "windows stair stairs ramp ramps rail rails handrail handrails ladder ladders platform "
            + "platforms grating grid grids axis axes north south east west up down left right inside outside "
            + "internal external interior exterior upper lower middle mid end ends start starts finish "
            + "begin begins number numbers sequence order orders position positions place placed placing "
            + "locate located location locations dimension dimensions mark marked marking mirror mirrored "
            + "rotate rotated move moved copy copied array arrays copy hidden visible hatch hatched fill "
            + "solid pattern patterns text mtext leader leaders arrow arrows tick ticks symbol symbols "
            + "legend key keys sample example examples true false on off yes none all any each both either "
            + "neither first second last next previous current original new existing proposed demolish "
            + "demolished remove removed removal replace replaced replacement add added addition additional "
            + "extra spare future provision provisions allow allowed allowance allowances clearance clearances "
            + "overall approx approximate approximately equal equals equally spaced spacing space spaces "
            + "centres centers staggered continuous continuously intermittent full partial penetration "
            + "fillet butt lap seam seams flat round square rectangular circular oval hexagon hexagonal "
            + "taper tapered chamfer chamfers radius radii curve curved arc arcs straight horizontal vertical "
            + "diagonal parallel perpendicular normal tangent tangents intersect intersection intersections "
            + "please thank thanks name names company address phone email fax web page pages of the and or"
        ).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        private const string ResourceName = "GeometryHelper.Dxf.Viewer.english-words.txt.gz";

        // The main dictionary: each word (looked up in either case) with its tier, 0 the most common words, 1 and 2 the less
        // common and 3 the names; read from the embedded list when first asked.
        private static readonly Lazy<Dictionary<string, int>> Main = new Lazy<Dictionary<string, int>>(
            LoadMain,
            LazyThreadSafetyMode.ExecutionAndPublication
        );

        /// <summary>Gets how many words the built-in dictionary holds.</summary>
        internal static int MainCount => Main.Value.Count;

        private static Dictionary<string, int> LoadMain()
        {
            var words = new Dictionary<string, int>(130000, StringComparer.OrdinalIgnoreCase);

            using (Stream stream = typeof(CadSpelling).Assembly.GetManifestResourceStream(ResourceName))
            {
                if (stream != null)
                {
                    using (var zip = new GZipStream(stream, CompressionMode.Decompress))
                    using (var reader = new StreamReader(zip, new UTF8Encoding(false)))
                    {
                        int tier = 0;

                        for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
                        {
                            if (line.Length > 1 && line[0] == '=')
                            {
                                tier = line[1] - '0';
                            }
                            else if (line.Length > 0)
                            {
                                words[line] = tier;
                            }
                        }
                    }
                }
            }

            foreach (string word in Legacy)
            {
                words[word] = 0;
            }

            return words;
        }

        private static bool InMain(string word) => Main.Value.ContainsKey(word);

        private static readonly string[] Endings =
        {
            "s",
            "es",
            "ed",
            "d",
            "ing",
            "ly",
            "er",
            "est",
            "'s",
            "ies",
            "ied",
        };

        /// <summary>
        /// The words of a text as it is written, each with its place in it: runs of letters, digits and apostrophes, those
        /// words an MTEXT's format codes are made of (<c>\fArial;</c>, <c>\H2x;</c>, <c>\P</c>, <c>{</c>) and a TEXT's
        /// <c>%%</c> codes left out.
        /// </summary>
        /// <param name="raw">The text as written, with its codes.</param>
        /// <param name="mtext">Whether the text is an MTEXT's, whose codes begin with a backslash.</param>
        /// <returns>The words, in the order they stand.</returns>
        internal static List<Word> WordsOf(string raw, bool mtext)
        {
            var words = new List<Word>();

            if (string.IsNullOrEmpty(raw))
            {
                return words;
            }

            var current = new StringBuilder();
            int start = 0;
            int i = 0;

            void End()
            {
                if (current.Length > 0)
                {
                    string text = current.ToString().Trim('\'');

                    if (text.Length > 0)
                    {
                        words.Add(new Word(start, text));
                    }

                    current.Clear();
                }
            }

            while (i < raw.Length)
            {
                char c = raw[i];

                if (mtext && c == '\\' && i + 1 < raw.Length)
                {
                    End();
                    char n = raw[i + 1];

                    if ("fFHCcTQWApS".IndexOf(n) >= 0)
                    {
                        int semicolon = raw.IndexOf(';', i);
                        i = semicolon < 0 ? raw.Length : semicolon + 1;
                    }
                    else if (n == 'U' && i + 2 < raw.Length && raw[i + 2] == '+')
                    {
                        i += 7;
                    }
                    else
                    {
                        i += 2;
                    }

                    continue;
                }

                if (!mtext && c == '%' && i + 2 < raw.Length && raw[i + 1] == '%')
                {
                    End();
                    i += 3;
                    continue;
                }

                if (char.IsLetterOrDigit(c) || (c == '\'' && current.Length > 0))
                {
                    if (current.Length == 0)
                    {
                        start = i;
                    }

                    current.Append(c);
                }
                else
                {
                    End();
                }

                i++;
            }

            End();
            return words;
        }

        /// <summary>Whether the word is passed over: one letter, a digit in it, or all in capitals.</summary>
        internal static bool IsPassedOver(string word) =>
            word.Length < 2
            || word.Any(char.IsDigit)
            || (word.Any(char.IsLetter) && string.Equals(word, word.ToUpperInvariant(), StringComparison.Ordinal));

        /// <summary>Whether the word is one of the built-in words, with a plain ending taken off, or of the custom ones.</summary>
        internal static bool IsKnown(string word, ISet<string> custom)
        {
            if (custom != null && custom.Contains(word))
            {
                return true;
            }

            if (InMain(word))
            {
                return true;
            }

            foreach (string ending in Endings)
            {
                if (word.Length > ending.Length + 1 && word.EndsWith(ending, StringComparison.OrdinalIgnoreCase))
                {
                    string stem = word.Substring(0, word.Length - ending.Length);

                    // Plurals add one s to a word that does not end in one, and es to one that does (thiss is no plural of this).
                    bool sibilant =
                        stem.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                        || stem.EndsWith("x", StringComparison.OrdinalIgnoreCase)
                        || stem.EndsWith("z", StringComparison.OrdinalIgnoreCase)
                        || stem.EndsWith("ch", StringComparison.OrdinalIgnoreCase)
                        || stem.EndsWith("sh", StringComparison.OrdinalIgnoreCase);

                    if (
                        (ending == "s" && stem.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                        || (ending == "es" && !sibilant)
                    )
                    {
                        continue;
                    }

                    if (InMain(stem) || InMain(stem + "e") || (custom != null && custom.Contains(stem)))
                    {
                        return true;
                    }

                    // Doubled letters before -ed and -ing (bolted is bolt+ed; fitted is fit+t+ed).
                    if (
                        stem.Length > 2
                        && stem[stem.Length - 1] == stem[stem.Length - 2]
                        && InMain(stem.Substring(0, stem.Length - 1))
                    )
                    {
                        return true;
                    }

                    if (ending == "ies" || ending == "ied")
                    {
                        if (InMain(stem + "y"))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// The words nearest a misspelled one, at most five: those of the built-in and custom words within one change of its
        /// letters (two for a word of five or more), the nearest first; of those as near, two neighbours swapped ("wrold" to
        /// "world"), then letters missing ("helo" to "hello"), then letters added, then any other change, then the more common word (the tiers
        /// of the dictionary), then the shorter, then in ordinal order; in the case the word begins with.
        /// </summary>
        internal static List<string> Suggest(string word, IEnumerable<string> custom)
        {
            int allowed = word.Length >= 5 ? 2 : 1;
            var found = new List<Suggestion>();
            string lower = word.ToLowerInvariant();

            void Consider(string candidate, int tier)
            {
                if (Math.Abs(candidate.Length - lower.Length) > allowed)
                {
                    return;
                }

                int distance = Distance(lower, candidate, allowed);

                if (distance <= allowed)
                {
                    found.Add(new Suggestion(candidate, distance, Slip(lower, candidate), tier));
                }
            }

            foreach (KeyValuePair<string, int> pair in Main.Value)
            {
                Consider(pair.Key.ToLowerInvariant(), pair.Value);
            }

            foreach (string candidate in custom ?? Enumerable.Empty<string>())
            {
                string small = candidate.ToLowerInvariant();

                if (!InMain(small))
                {
                    Consider(small, 0);
                }
            }

            bool capital = char.IsUpper(word[0]);
            return found
                .OrderBy(s => s.Distance)
                .ThenBy(s => s.Slip)
                .ThenBy(s => s.Tier)
                .ThenBy(s => s.Word.Length)
                .ThenBy(s => s.Word, StringComparer.Ordinal)
                .Take(5)
                .Select(s => capital ? char.ToUpperInvariant(s.Word[0]) + s.Word.Substring(1) : s.Word)
                .ToList();
        }

        /// <summary>A word near a misspelled one: how near, the kind of slip it is (see <c>Slip</c>), how common.</summary>
        private readonly struct Suggestion
        {
            internal Suggestion(string word, int distance, int slip, int tier)
            {
                Word = word;
                Distance = distance;
                Slip = slip;
                Tier = tier;
            }

            internal string Word { get; }

            internal int Distance { get; }

            internal int Slip { get; }

            internal int Tier { get; }
        }

        /// <summary>
        /// The kind of slip that makes a misspelled word of a candidate: two neighbours swapped (0), letters missing from the
        /// word (1), letters added to it (2), or any other change (3).
        /// </summary>
        private static int Slip(string word, string candidate)
        {
            if (word.Length == candidate.Length)
            {
                int first = 0;

                while (first < word.Length && word[first] == candidate[first])
                {
                    first++;
                }

                return
                    first + 1 < word.Length
                    && word[first] == candidate[first + 1]
                    && word[first + 1] == candidate[first]
                    && string.CompareOrdinal(word, first + 2, candidate, first + 2, word.Length) == 0
                    ? 0
                    : 3;
            }

            string shorter = word.Length < candidate.Length ? word : candidate;
            string longer = word.Length < candidate.Length ? candidate : word;
            int at = 0;

            foreach (char c in longer)
            {
                if (at < shorter.Length && shorter[at] == c)
                {
                    at++;
                }
            }

            return at < shorter.Length ? 3
                : word.Length < candidate.Length ? 1
                : 2;
        }

        /// <summary>The number of insertions, deletions, substitutions and swaps of neighbours that turn one word into another; above the limit where it is more.</summary>
        private static int Distance(string a, string b, int limit)
        {
            var d = new int[a.Length + 1, b.Length + 1];

            for (int i = 0; i <= a.Length; i++)
            {
                d[i, 0] = i;
            }

            for (int j = 0; j <= b.Length; j++)
            {
                d[0, j] = j;
            }

            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);

                    if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    {
                        d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
                    }
                }
            }

            return d[a.Length, b.Length] > limit ? limit + 1 : d[a.Length, b.Length];
        }

        /// <summary>Reads a custom dictionary: the words of a text file, one to a line, blank lines and lines beginning with # left out.</summary>
        /// <param name="path">The file.</param>
        /// <returns>The words, letters of either case the same.</returns>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        internal static HashSet<string> ReadDictionary(string path)
        {
            var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
            {
                string word = line.Trim();

                if (word.Length > 0 && word[0] != '#')
                {
                    words.Add(word);
                }
            }

            return words;
        }
    }
}
