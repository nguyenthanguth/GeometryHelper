using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GeometryHelper.Benchmarks
{
    /// <summary>
    /// The signatures approved for the cases, kept in <c>benchmarks/baselines/signatures.txt</c>: one case a line, its
    /// name, a space and its signature, sorted by name, so that a change to one shows in a diff as that line alone.
    /// </summary>
    /// <remarks>
    /// Lines starting with <c>#</c> are comments. The file is written with LF line endings and no byte-order mark.
    /// </remarks>
    internal static class ApprovedSignatures
    {
        /// <summary>The name of the file, in the baselines folder.</summary>
        internal const string FileName = "signatures.txt";

        /// <summary>
        /// Reads the approved signatures.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <returns>The signature of each case by its name; empty when there is no file.</returns>
        /// <exception cref="InvalidDataException">Thrown for a line that is not a name and a signature, or a name given twice.</exception>
        internal static SortedDictionary<string, string> Read(string path)
        {
            var approved = new SortedDictionary<string, string>(StringComparer.Ordinal);

            if (!File.Exists(path))
            {
                return approved;
            }

            int number = 0;

            foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
            {
                number++;

                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                string[] parts = line.Split(' ');

                if (parts.Length != 2 || parts[1].Length != 64)
                {
                    throw new InvalidDataException(path + ", line " + number + ": not a name and a signature.");
                }

                if (approved.ContainsKey(parts[0]))
                {
                    throw new InvalidDataException(path + ", line " + number + ": " + parts[0] + " is approved twice.");
                }

                approved.Add(parts[0], parts[1]);
            }

            return approved;
        }

        /// <summary>
        /// Writes the approved signatures, sorted by name.
        /// </summary>
        /// <param name="path">The file; its folder is made if need be.</param>
        /// <param name="approved">The signature of each case by its name.</param>
        /// <param name="provenance">What they were approved on, for the comment at the top: the commit, the runtime.</param>
        internal static void Write(string path, SortedDictionary<string, string> approved, string provenance)
        {
            var text = new StringBuilder();
            text.Append("# The result signatures approved for the benchmark cases: a name, a space, the SHA-256 of its result.\n");
            text.Append("# Written by --approve. What each case signs: benchmarks/GeometryHelper.Benchmarks/Scenarios.\n");
            text.Append("# Last approved on ").Append(provenance).Append(".\n");

            foreach (KeyValuePair<string, string> entry in approved.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                text.Append(entry.Key).Append(' ').Append(entry.Value).Append('\n');
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
        }
    }
}
