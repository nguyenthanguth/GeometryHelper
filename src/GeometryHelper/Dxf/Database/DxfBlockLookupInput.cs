using System;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// An input column of the table of a lookup parameter of a dynamic block, AutoCAD's Input Property of its Lookup Table:
    /// the property of another parameter that a row of the table sets, the distance of a linear parameter or the x or the y
    /// of a point parameter, as AutoCAD's save of ACadSharp's lookup sample holds them. See
    /// <see cref="DxfBlockTableRecord.AddLookupParameter"/>.
    /// </summary>
    /// <remarks>
    /// <c>DxfBlockLookupInput.Distance(width)</c> is the column of the Width of a panel: a row of the value 200 sets Width to
    /// 200, and an INSERT of a Width of 200 shows that row's label.
    /// </remarks>
    public sealed class DxfBlockLookupInput
    {
        private DxfBlockLookupInput(DxfBlockTableRecord block, int node, string name, string connection, DxfBlockParameterValueSet valueSet)
        {
            Block = block;
            Node = node;
            Name = name;
            Connection = connection;
            ValueSet = valueSet;
        }

        /// <summary>The block the input's parameter is of.</summary>
        internal DxfBlockTableRecord Block { get; }

        /// <summary>The id of the node of the input's parameter (the column's 94).</summary>
        internal int Node { get; }

        /// <summary>The name of the property the column sets, as Width or Tag X.</summary>
        internal string Name { get; }

        /// <summary>The parameter's connection the column sets (its 304): UpdatedDistance, UpdatedX or UpdatedY.</summary>
        internal string Connection { get; }

        /// <summary>The values a distance may take; null for a position, or a distance of any value.</summary>
        internal DxfBlockParameterValueSet ValueSet { get; }

        /// <summary>
        /// Makes the column of the distance of a linear parameter, its connection UpdatedDistance as AutoCAD writes it: each row
        /// gives the distance, above the point tolerance and of the parameter's value set where it has one.
        /// </summary>
        /// <param name="parameter">The linear parameter.</param>
        /// <returns>The column.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameter is null.</exception>
        public static DxfBlockLookupInput Distance(DxfBlockLinearParameter parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            return new DxfBlockLookupInput(parameter.Block, parameter.Node, parameter.Name, "UpdatedDistance", parameter.ValueSet);
        }

        /// <summary>Makes the column of the x of a point parameter, its connection UpdatedX as AutoCAD writes it: each row gives the x.</summary>
        /// <param name="parameter">The point parameter.</param>
        /// <returns>The column.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameter is null.</exception>
        public static DxfBlockLookupInput X(DxfBlockPointParameter parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            return new DxfBlockLookupInput(parameter.Block, parameter.Node, parameter.Name + " X", "UpdatedX", null);
        }

        /// <summary>Makes the column of the y of a point parameter, its connection UpdatedY as AutoCAD writes it: each row gives the y.</summary>
        /// <param name="parameter">The point parameter.</param>
        /// <returns>The column.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameter is null.</exception>
        public static DxfBlockLookupInput Y(DxfBlockPointParameter parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            return new DxfBlockLookupInput(parameter.Block, parameter.Node, parameter.Name + " Y", "UpdatedY", null);
        }
    }
}
