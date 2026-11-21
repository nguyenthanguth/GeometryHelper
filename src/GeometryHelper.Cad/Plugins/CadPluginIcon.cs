using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;

namespace GeometryHelper.Cad.Plugins
{
    /// <summary>
    /// The picture of a ribbon button or a DRAWINGS button, described without WPF: path data, or the bytes of an image. The
    /// bytes are copied in when the icon is made, so that it keeps no stream, no file lock and no reference to the plugin's
    /// assembly, which unloading the plugin needs. The window draws it fitted into 32 pixels on a large button and 16 on a
    /// standard one; a raster icon of 16 by 16 is drawn as it is on a standard button.
    /// </summary>
    public sealed class CadPluginIcon
    {
        private readonly byte[] _image;
        private IReadOnlyList<byte> _view;

        private CadPluginIcon(string pathData, byte[] image)
        {
            PathData = pathData;
            _image = image;
        }

        /// <summary>
        /// Gets the path data of an icon drawn as a path, as WPF writes it ("M5 12 L19 12"), on a square of 24 drawn 2 wide
        /// like the presenter's own icons; null for an image.
        /// </summary>
        public string PathData { get; }

        /// <summary>Gets the bytes of an icon made from an image, as they were given; null for path data.</summary>
        public IReadOnlyList<byte> Image =>
            _image == null ? null : _view ?? (_view = new ReadOnlyCollection<byte>(_image));

        /// <summary>The bytes of an image icon, not copied; null for path data. The window decodes them.</summary>
        internal byte[] ImageBytes => _image;

        /// <summary>Makes an icon from path data on a square of 24, drawn 2 wide: <c>FromPathData("M4 12 H20 M12 4 V20")</c> is a plus.</summary>
        /// <param name="pathData">The path data. The window falls back to the presenter's plugin icon where it cannot read it, and logs one Warn.</param>
        /// <returns>The icon.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path data is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the path data is empty or only white space.</exception>
        public static CadPluginIcon FromPathData(string pathData)
        {
            if (pathData == null)
            {
                throw new ArgumentNullException(nameof(pathData));
            }

            if (pathData.Trim().Length == 0)
            {
                throw new ArgumentException("An icon's path data is not empty.", nameof(pathData));
            }

            return new CadPluginIcon(pathData, null);
        }

        /// <summary>Makes an icon from the bytes of an image file: PNG, BMP, JPEG, GIF or ICO. The bytes are copied.</summary>
        /// <param name="image">The bytes. The window falls back to the presenter's plugin icon where it cannot decode them, and logs one Warn.</param>
        /// <returns>The icon.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the bytes are null.</exception>
        /// <exception cref="ArgumentException">Thrown when there are no bytes.</exception>
        public static CadPluginIcon FromImage(byte[] image)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            if (image.Length == 0)
            {
                throw new ArgumentException("An icon's image is not empty.", nameof(image));
            }

            return new CadPluginIcon(null, (byte[])image.Clone());
        }

        /// <summary>
        /// Makes an icon from an image the assembly embeds, its bytes read and copied at once. The name is the resource's
        /// full name, <c>"MyPlugin.circle16.png"</c> for <c>circle16.png</c> of a project whose root namespace is MyPlugin.
        /// </summary>
        /// <param name="assembly">The assembly that embeds it, usually the plugin's own.</param>
        /// <param name="resourceName">The resource's full name.</param>
        /// <returns>The icon.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the assembly or the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the assembly has no such resource, or it is empty.</exception>
        public static CadPluginIcon FromResource(Assembly assembly, string resourceName)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            if (resourceName == null)
            {
                throw new ArgumentNullException(nameof(resourceName));
            }

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new ArgumentException(
                        "The assembly " + assembly.GetName().Name + " has no resource " + resourceName + ".",
                        nameof(resourceName)
                    );
                }

                using (var copy = new MemoryStream())
                {
                    stream.CopyTo(copy);
                    return FromImage(copy.ToArray());
                }
            }
        }

        /// <summary>
        /// Makes an icon from an image file, its bytes read and copied at once, so that the file is not held open and can be
        /// deleted or replaced. A plugin names a file beside it with <c>Path.Combine(host.Folder, "circle16.png")</c>.
        /// </summary>
        /// <param name="path">The full path of the file; a relative path is refused.</param>
        /// <returns>The icon.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the path is not a full path, or the file is empty.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        public static CadPluginIcon FromFile(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (!Path.IsPathRooted(path))
            {
                throw new ArgumentException(
                    "The icon file " + path + " needs a full path: Path.Combine(host.Folder, ...).",
                    nameof(path)
                );
            }

            return FromImage(File.ReadAllBytes(path));
        }
    }
}
