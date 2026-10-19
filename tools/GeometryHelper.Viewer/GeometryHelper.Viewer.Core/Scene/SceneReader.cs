using System;
using System.IO;
using System.Text;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Reads a scene from its text form.
    /// </summary>
    public static class SceneReader
    {
        /// <summary>
        /// Reads a scene given as a path or as the JSON itself: text that starts with a brace is JSON, anything else is the
        /// path of a file.
        /// </summary>
        /// <param name="pathOrJson">The path of a scene file, or the scene.</param>
        /// <returns>The scene.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        /// <exception cref="FormatException">Thrown when the text is not a scene.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        public static Scene Read(string pathOrJson)
        {
            if (pathOrJson == null)
            {
                throw new ArgumentNullException(nameof(pathOrJson));
            }

            return pathOrJson.TrimStart().StartsWith("{", StringComparison.Ordinal) ? ReadJson(pathOrJson) : ReadFile(pathOrJson);
        }

        /// <summary>
        /// Reads a scene from a file.
        /// </summary>
        /// <param name="path">The path of a UTF-8 file.</param>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="FormatException">Thrown when the file is not a scene.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        public static Scene ReadFile(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            return SceneFile.Read(File.ReadAllText(path, Encoding.UTF8));
        }

        /// <summary>
        /// Reads a scene from its JSON.
        /// </summary>
        /// <param name="json">The scene as <see cref="SceneWriter.ToJson"/> writes it.</param>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        /// <exception cref="FormatException">Thrown when the text is not a scene.</exception>
        public static Scene ReadJson(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            return SceneFile.Read(json);
        }
    }
}
