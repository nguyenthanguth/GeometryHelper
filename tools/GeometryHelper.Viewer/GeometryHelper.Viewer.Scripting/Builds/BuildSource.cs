using System;
using System.Globalization;
using System.IO;

namespace GeometryHelper.Viewer.Builds
{
    /// <summary>
    /// Where a build of the library comes from.
    /// </summary>
    public enum BuildSourceKind
    {
        /// <summary>The GeometryHelper.dll the viewer itself runs with.</summary>
        Own,

        /// <summary>A GeometryHelper.dll at a path.</summary>
        Dll,

        /// <summary>A commit of a Git repository, built in the viewer's cache.</summary>
        Commit,
    }

    /// <summary>
    /// A build of the library to run a script against: the viewer's own, a DLL at a path, or a commit of a repository.
    /// </summary>
    public sealed class BuildSource
    {
        private BuildSource(BuildSourceKind kind, string dll, string commit, string repository)
        {
            Kind = kind;
            Dll = dll;
            Commit = commit;
            Repository = repository;
        }

        /// <summary>
        /// Gets the kind of source.
        /// </summary>
        public BuildSourceKind Kind { get; }

        /// <summary>
        /// Gets the path of the DLL for <see cref="BuildSourceKind.Dll"/>; null otherwise.
        /// </summary>
        public string Dll { get; }

        /// <summary>
        /// Gets the commit (a hash, a short hash, a tag or a branch name) for <see cref="BuildSourceKind.Commit"/>; null otherwise.
        /// </summary>
        public string Commit { get; }

        /// <summary>
        /// Gets the folder of the repository <see cref="Commit"/> is in; null otherwise.
        /// </summary>
        public string Repository { get; }

        /// <summary>
        /// Gets the viewer's own library as a source.
        /// </summary>
        public static BuildSource Own { get; } = new BuildSource(BuildSourceKind.Own, null, null, null);

        /// <summary>
        /// Makes a source of a DLL at a path.
        /// </summary>
        /// <param name="path">The path of GeometryHelper.dll.</param>
        /// <exception cref="ArgumentException">Thrown when the path is empty.</exception>
        public static BuildSource FromDll(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A DLL needs a path.", nameof(path));
            }

            return new BuildSource(BuildSourceKind.Dll, Path.GetFullPath(path), null, null);
        }

        /// <summary>
        /// Makes a source of a commit of a repository.
        /// </summary>
        /// <param name="commit">A hash, a short hash, a tag or a branch name.</param>
        /// <param name="repository">The folder of the repository, or of a work tree of it.</param>
        /// <exception cref="ArgumentException">Thrown when the commit or the repository is empty.</exception>
        public static BuildSource FromCommit(string commit, string repository)
        {
            if (string.IsNullOrWhiteSpace(commit))
            {
                throw new ArgumentException("A commit needs a name.", nameof(commit));
            }

            if (string.IsNullOrWhiteSpace(repository))
            {
                throw new ArgumentException("A commit needs the repository it is in.", nameof(repository));
            }

            return new BuildSource(BuildSourceKind.Commit, null, commit.Trim(), Path.GetFullPath(repository));
        }

        /// <summary>
        /// Reads a source written as <c>own</c>, <c>dll:C:\path\GeometryHelper.dll</c> or <c>commit:9ee3f57</c>.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="repository">The repository a commit is in.</param>
        /// <param name="source">The source when the method returns true.</param>
        /// <param name="problem">What is wrong with the text when the method returns false.</param>
        /// <returns>true when the text is a source.</returns>
        public static bool TryParse(string text, string repository, out BuildSource source, out string problem)
        {
            source = null;
            problem = null;
            string trimmed = (text ?? string.Empty).Trim();

            if (string.Equals(trimmed, "own", StringComparison.OrdinalIgnoreCase))
            {
                source = Own;
                return true;
            }

            if (trimmed.StartsWith("dll:", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 4)
            {
                source = FromDll(trimmed.Substring(4));
                return true;
            }

            if (trimmed.StartsWith("commit:", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 7)
            {
                if (string.IsNullOrWhiteSpace(repository))
                {
                    problem = "A commit needs the repository it is in.";
                    return false;
                }

                source = FromCommit(trimmed.Substring(7), repository);
                return true;
            }

            problem = "'" + text + "' is not a source; write own, dll:<path> or commit:<hash>.";
            return false;
        }

        /// <summary>
        /// Gets the source in words, for a title and the output pane.
        /// </summary>
        public string Describe()
        {
            switch (Kind)
            {
                case BuildSourceKind.Own: return "own library";
                case BuildSourceKind.Dll: return Dll;
                default: return string.Format(CultureInfo.InvariantCulture, "commit {0} of {1}", Commit, Repository);
            }
        }

        /// <summary>
        /// Gets the source in the form <see cref="TryParse"/> reads.
        /// </summary>
        public override string ToString()
        {
            switch (Kind)
            {
                case BuildSourceKind.Own: return "own";
                case BuildSourceKind.Dll: return "dll:" + Dll;
                default: return "commit:" + Commit;
            }
        }
    }
}
