using System.Globalization;
using System.Threading;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>What a VLA-OBJECT stands for.</summary>
    internal enum LispVlaKind
    {
        /// <summary>The application.</summary>
        Application,

        /// <summary>The document of the session.</summary>
        Document,

        /// <summary>A collection: Blocks, Layers, TextStyles, Linetypes, DimStyles, SelectionSets.</summary>
        Collection,

        /// <summary>A block definition, model space or a layout's paper space.</summary>
        Block,

        /// <summary>A record of a table: a layer, a text style, a linetype, a dimension style.</summary>
        Record,

        /// <summary>An entity.</summary>
        Entity,

        /// <summary>A selection set of the SelectionSets collection.</summary>
        SelectionSet,

        /// <summary>An AcCmColor, a copy of a color.</summary>
        Color,
    }

    /// <summary>
    /// A VLA-OBJECT of AutoLISP: an object of the presenter's drawing seen as AutoCAD's ActiveX object, named by its kind, the
    /// interface it prints with and a key (the handle of an entity, a record or a block; the name of a collection or a set).
    /// It prints as <c>#&lt;VLA-OBJECT IAcadLine 000001d38254e2a0&gt;</c> with an address that is made up and stays the same
    /// for the life of the value, as a function's does.
    /// </summary>
    internal sealed class LispVlaObject
    {
        private static long s_nextAddress = 0x1e4a7f3c000;
        private readonly string _printForm;

        /// <summary>Makes a value.</summary>
        /// <param name="kind">What it stands for.</param>
        /// <param name="interfaceName">The interface it prints with: <c>IAcadLine</c>, <c>IAcadDocument</c>.</param>
        /// <param name="key">The handle of an entity, a record or a block; the name of a collection or a set; "" for the others.</param>
        internal LispVlaObject(LispVlaKind kind, string interfaceName, string key)
        {
            Kind = kind;
            InterfaceName = interfaceName;
            Key = key ?? "";
            Address = Interlocked.Add(ref s_nextAddress, 0x60);
            _printForm =
                "#<VLA-OBJECT " + interfaceName + " " + Address.ToString("x16", CultureInfo.InvariantCulture) + ">";
        }

        /// <summary>Gets what it stands for.</summary>
        internal LispVlaKind Kind { get; }

        /// <summary>Gets the interface it prints with.</summary>
        internal string InterfaceName { get; }

        /// <summary>Gets the handle of an entity, a record or a block, the name of a collection or a set, or "".</summary>
        internal string Key { get; }

        /// <summary>Gets or sets what a kind keeps with it: the handles of a selection set, the color of an AcCmColor.</summary>
        internal object State { get; set; }

        /// <summary>Gets or sets whether <c>vlax-release-object</c> has let it go.</summary>
        internal bool IsReleased { get; set; }

        /// <summary>Gets the made-up address it prints with.</summary>
        internal long Address { get; }

        /// <summary>Gets how prin1 and princ write it; a released value has lost its interface and its address (AutoCAD 2021, GUI g2).</summary>
        internal string PrintForm => IsReleased ? "#<VLA-OBJECT 0000000000000000>" : _printForm;
    }
}
