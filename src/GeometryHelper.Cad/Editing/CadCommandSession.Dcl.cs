using GeometryHelper.Cad.Dcl;

namespace GeometryHelper.Cad
{
    /// <summary>The dialogs of AutoLISP's DCL functions: what shows them for this session.</summary>
    public sealed partial class CadCommandSession
    {
        /// <summary>
        /// Gets or sets what shows the DCL dialogs of this session's routines: the window, or a test's script. Null, as it is
        /// until set, is a session with no window: <c>new_dialog</c> gives nil, as AutoCAD's console does.
        /// </summary>
        internal IDclHost DclHost { get; set; }
    }
}
