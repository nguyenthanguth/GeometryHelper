using System;
using System.Collections;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The ATTRIBs of an INSERT, as AutoCAD's <c>AttributeCollection</c>: its <see cref="DxfAttributeReference"/>s in the order
    /// of the file, read as they stand each time, and the one way an attribute is added to it,
    /// <see cref="AppendAttribute"/>.
    /// </summary>
    /// <remarks>
    /// The INSERT of BOLT with the ATTRIB MARK of B-12 has a collection of <see cref="Count"/> 1 whose first is MARK. An
    /// attribute erased leaves it, the others keeping their handles and places in the file.
    /// </remarks>
    public sealed class DxfAttributeCollection : IReadOnlyList<DxfAttributeReference>
    {
        private readonly DxfBlockReference _owner;

        internal DxfAttributeCollection(DxfBlockReference owner)
        {
            _owner = owner;
        }

        /// <summary>Gets how many ATTRIBs the INSERT has, as AutoCAD's <c>Count</c>.</summary>
        public int Count => _owner.Attributes().Count;

        /// <summary>Gets the ATTRIB at a place, from nought, in the order of the file.</summary>
        /// <param name="index">The place.</param>
        /// <returns>The attribute; the same object each time while its record is there.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the place is not that of an ATTRIB.</exception>
        public DxfAttributeReference this[int index] => _owner.Attributes()[index];

        /// <summary>
        /// Adds an attribute made in code to the INSERT, as AutoCAD's <c>AppendAttribute</c>: after its other ATTRIBs, before
        /// their SEQEND, with a handle of its own and the INSERT as its owner, its look and groups as made; every other record
        /// keeping its handle and bytes. An INSERT with no ATTRIB yet is given its attributes-follow flag (66) and a SEQEND.
        /// The same object stands for the new ATTRIB from then on.
        /// </summary>
        /// <param name="attribute">The attribute, made in code, as <see cref="DxfAttributeReference.SetAttributeFromBlock(DxfAttributeDefinition, Geometry.GeoTransform3)"/> fills one.</param>
        /// <exception cref="ArgumentNullException">Thrown when the attribute is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the attribute has no tag, or a look the file's version cannot hold.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the attribute is in a file or of an INSERT already, or the INSERT is erased.</exception>
        public void AppendAttribute(DxfAttributeReference attribute) => _owner.AppendAttribute(attribute);

        /// <summary>Gets an enumerator over the ATTRIBs as they stand when it is asked for.</summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator<DxfAttributeReference> GetEnumerator() => _owner.Attributes().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
