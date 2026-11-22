using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// Reads the font lists of the Text Style dialog and the MTEXT editor ahead, once the window has drawn: the TrueType
    /// families of the machine (<see cref="CadTrueTypeFonts.WarmUp"/>) and the SHX files of the font folders, each on a
    /// thread of its own at below normal priority. The first STYLE then finds both lists read; a STYLE typed before they are
    /// is held up by the same reading and does not start it again.
    /// </summary>
    internal static class FontListWarmUp
    {
        private static readonly object Gate = new object();
        private static string _folders;

        /// <summary>
        /// Starts the reading and gives back at once. The SHX folders are read again only where they are not the ones read
        /// last, as a change of Global settings makes them.
        /// </summary>
        /// <param name="shxFolders">The folders the SHX fonts are looked for in, in order.</param>
        /// <returns>The task, complete when both lists are read.</returns>
        internal static Task Start(IEnumerable<string> shxFolders)
        {
            List<string> folders = (shxFolders ?? Array.Empty<string>()).ToList();
            string key = string.Join("|", folders);
            Task trueType = CadTrueTypeFonts.WarmUp();

            lock (Gate)
            {
                if (key == _folders)
                {
                    return trueType;
                }

                _folders = key;
            }

            return Task.WhenAll(trueType, CadWarmUp.Run("the SHX fonts", () => TextStyleViewModel.ShxFiles(folders)));
        }
    }
}
