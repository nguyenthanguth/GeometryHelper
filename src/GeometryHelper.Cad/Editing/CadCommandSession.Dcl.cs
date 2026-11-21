using System.Collections.Generic;
using GeometryHelper.Cad.Dcl;
using GeometryHelper.Cad.Lisp;

namespace GeometryHelper.Cad
{
    /// <summary>The dialogs of AutoLISP's DCL functions: what shows them for this session.</summary>
    public sealed partial class CadCommandSession
    {
        private DclLibrary _dclLibrary;

        /// <summary>
        /// Gets or sets what shows the DCL dialogs of this session's routines: the window, or a test's script. Null, as it is
        /// until set, is a session with no window: <c>new_dialog</c> gives nil, as AutoCAD's console does.
        /// </summary>
        internal IDclHost DclHost { get; set; }

        /// <summary>Gets the DCL files this document's routines loaded, by the ids <c>load_dialog</c> gave.</summary>
        internal DclLibrary DclLibrary => _dclLibrary ?? (_dclLibrary = new DclLibrary(DclSearchFolders));

        // Where load_dialog looks for a bare name, as findfile does for a file of code: the support folders, and before them the
        // current folder and the drawing's while LEGACYCODESEARCH is on.
        private IEnumerable<string> DclSearchFolders()
        {
            LispInterpreter lisp = Lisp;

            if (LoadSecurity.LegacyCodeSearch)
            {
                yield return lisp.CurrentFolder?.Invoke();
                yield return lisp.DrawingFolder?.Invoke();
            }

            foreach (string folder in lisp.SupportFolders)
            {
                yield return folder;
            }
        }
    }
}
