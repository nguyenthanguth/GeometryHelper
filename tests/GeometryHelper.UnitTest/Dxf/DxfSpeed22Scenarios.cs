using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Runs for the work of tables10, whose results are held to what the library gave before it: the edits of
    /// <see cref="DxfSpeed21Scenarios"/> on tables whose drawing hands out handles of digits alone to the fields they make, and
    /// the search of a file for what points at a handle (<c>PointedAmong</c>) among records made at random, with the handles
    /// looked for of digits alone, of digits and letters, and of one or two digits.
    /// </summary>
    internal static class DxfSpeed22Scenarios
    {
        /// <summary>The table of <see cref="DxfSpeed18Scenarios.Table"/> in a drawing whose next handle is the one given.</summary>
        internal static DxfDatabase Table(
            string version,
            int rows,
            int columns,
            bool content,
            bool blocks,
            string nextHandle
        )
        {
            DxfDatabase db = DxfSpeed18Scenarios.Table(version, rows, columns, content, blocks);
            string text = Regex.Replace(db.ToText(), @"(\$HANDSEED\r?\n\s*5\r?\n)[0-9A-Fa-f]+", "${1}" + nextHandle);
            return DxfDatabase.Parse(text);
        }

        /// <summary>A run of edits of a table of 8 rows and 6 columns, saved as it stands; with <paramref name="reopen"/> the drawing is opened anew from its text before each edit.</summary>
        internal static string Run(
            string version,
            int seed,
            int edits,
            bool reopen,
            bool content,
            bool blocks,
            bool first,
            string nextHandle
        )
        {
            DxfDatabase db = Table(version, 8, 6, content, blocks, nextHandle);
            DxfTable table = db.ModelSpace.OfType<DxfTable>().Single();
            long[] state = { seed * 2654435761L + 424242 };

            for (int i = 0; i < edits; i++)
            {
                if (reopen)
                {
                    db = DxfDatabase.Parse(db.ToText());
                    table = db.ModelSpace.OfType<DxfTable>().Single();
                }

                try
                {
                    DxfSpeed21Scenarios.Edit(table, state, first);
                }
                catch (InvalidOperationException) { }
                catch (ArgumentException) { }
            }

            return db.ToText();
        }

        /// <summary>The SHA-256 of the file each run of 120 edits saves, the fields it makes numbered from 9000 and from 70000 (digits alone for the first ten of each, then a letter among them).</summary>
        internal static List<string> Runs()
        {
            var lines = new List<string>();

            foreach (string version in new[] { "AC1032", "AC1021", "AC1018" })
            {
                foreach (string handle in new[] { "9000", "70000" })
                {
                    for (int seed = 1; seed <= 2; seed++)
                    {
                        foreach (bool blocks in new[] { false, true })
                        {
                            foreach (bool content in new[] { false, true })
                            {
                                string run = Run(version, seed, 120, false, content, blocks, true, handle);
                                lines.Add(
                                    version
                                        + " from "
                                        + handle
                                        + " seed "
                                        + seed
                                        + (blocks ? " blocks" : " text")
                                        + (content ? " content " : " plain ")
                                        + DxfSpeed12Scenarios.Hash(Encoding.UTF8.GetBytes(run))
                                );
                            }
                        }
                    }
                }
            }

            return lines;
        }

        // The handles looked for, one to two digits, digits alone, a letter among the digits, in the order the runs take them.
        private static readonly string[] Pool =
        {
            "190",
            "1900",
            "19000",
            "190000",
            "7B00A",
            "C00",
            "12",
            "5",
            "300000",
            "987654",
            "4D1",
            "8080",
        };

        // The pointer groups of a record, and the codes that point at nothing.
        private static readonly int[] PointerCodes = { 340, 360, 330, 331, 344, 350, 1005, 480 };
        private static readonly int[] OtherCodes = { 90, 91, 1, 2, 40, 10, 300, 70 };

        /// <summary>The forms a value can take, of the handle given: as it is, with zeros, spaces and tabs before and after, in the other case, and what only looks like it.</summary>
        private static string[] Forms(string handle) =>
            new[]
            {
                handle,
                "0" + handle,
                "000" + handle,
                " " + handle,
                handle + " ",
                "\t" + handle,
                handle + "\t",
                " 0" + handle + " ",
                handle.ToLowerInvariant(),
                handle + "0",
                "1" + handle,
                handle + ".5",
                handle + "A",
                "G" + handle,
                handle + handle,
            };

        private static string Group(int code, string value) =>
            code.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(3) + "\n" + value + "\n";

        /// <summary>The records of a drawing of R2018, made at random from the seed given.</summary>
        internal static string Records(int seed, int count)
        {
            long state = seed * 2654435761L + 7;

            int Next(int below)
            {
                state = (state * 6364136223846793005L + 1442695040888963407L) & long.MaxValue;
                return (int)((state >> 33) % below);
            }

            var b = new StringBuilder();
            string[] types = { "XRECORD", "FIELD", "DICTIONARY", "FIELDLIST", "DATALINK" };

            for (int k = 0; k < count; k++)
            {
                string type = types[Next(types.Length)];
                b.Append(Group(0, type))
                    .Append(Group(5, "E" + k.ToString("X", System.Globalization.CultureInfo.InvariantCulture)));

                if (Next(3) != 0)
                {
                    b.Append(Group(330, "F6"));
                }

                int groups = 2 + Next(24);

                for (int g = 0; g < groups; g++)
                {
                    int pick = Next(10);

                    if (pick < 5)
                    {
                        string[] forms = Forms(Pool[Next(Pool.Length)]);
                        b.Append(Group(PointerCodes[Next(PointerCodes.Length)], forms[Next(forms.Length)]));
                    }
                    else if (pick < 8)
                    {
                        string[] forms = Forms(Pool[Next(Pool.Length)]);
                        b.Append(Group(OtherCodes[Next(OtherCodes.Length)], forms[Next(forms.Length)]));
                    }
                    else if (pick == 8)
                    {
                        // A braced group, whose pointers are not counted.
                        string[] forms = Forms(Pool[Next(Pool.Length)]);
                        b.Append(Group(102, "{ACAD_REACTORS"))
                            .Append(Group(330, forms[Next(forms.Length)]))
                            .Append(Group(102, "}"));
                    }
                    else
                    {
                        b.Append(Group(1, "Text of " + Pool[Next(Pool.Length)] + " and " + Next(100000)));
                    }
                }
            }

            return DxfDimensionDrawings.R2018.Replace("  0\nENDSEC\n  0\nEOF\n", b + "  0\nENDSEC\n  0\nEOF\n");
        }

        /// <summary>
        /// What <c>PointedAmong</c> gives for the handles looked for among records made at random, each of its results as
        /// text: the handles given, those pointed at with the first record that points at each, and those a FIELD names as its own.
        /// </summary>
        internal static string Pointed(int seed, int wanted, int aside)
        {
            DxfDatabase db = DxfDatabase.Parse(Records(seed, 40));
            var handles = new List<string>();
            long state = seed * 40503L + wanted;

            for (int k = 0; k < wanted; k++)
            {
                state = (state * 6364136223846793005L + 1442695040888963407L) & long.MaxValue;
                handles.Add(Pool[(int)((state >> 33) % Pool.Length)]);
            }

            var apart = new List<string>();

            for (int k = 0; k < aside; k++)
            {
                apart.Add("E" + (k * 3).ToString("X", System.Globalization.CultureInfo.InvariantCulture));
            }

            Dictionary<string, string> found = db.File.PointedAmong(
                handles,
                apart,
                new[] { "FIELDLIST" },
                out Dictionary<string, string> named
            );
            var text = new StringBuilder();

            foreach (string handle in handles)
            {
                text.Append(handle).Append(' ');
            }

            foreach (KeyValuePair<string, string> pair in found.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                text.Append("|P ").Append(pair.Key).Append('=').Append(pair.Value);
            }

            foreach (KeyValuePair<string, string> pair in named.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                text.Append("|N ").Append(pair.Key).Append('=').Append(pair.Value);
            }

            return text.ToString();
        }

        /// <summary>The SHA-256 of what each search of a file of random records gives, for 1 to 6 handles looked for and 0 to 2 records left aside.</summary>
        internal static List<string> PointedRuns()
        {
            var lines = new List<string>();

            for (int seed = 1; seed <= 10; seed++)
            {
                for (int wanted = 1; wanted <= 6; wanted++)
                {
                    string result = Pointed(seed, wanted, seed % 3);
                    lines.Add(
                        "seed "
                            + seed
                            + " wanted "
                            + wanted
                            + " "
                            + DxfSpeed12Scenarios.Hash(Encoding.UTF8.GetBytes(result))
                    );
                }
            }

            return lines;
        }
    }
}
