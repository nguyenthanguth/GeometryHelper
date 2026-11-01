using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A column of the block properties table of a dynamic block, AutoCAD's column of its Block Properties Table: the
    /// property of a parameter of the block that a row sets, the distance of a linear parameter, the distance along x or
    /// along y of an XY parameter, the distance or the angle of a polar parameter, the angle of a rotation parameter or the
    /// state of the visibility parameter; or an attribute definition of the block, whose attribute a row fills in. See
    /// <see cref="DxfBlockTableRecord.AddBlockPropertiesTable"/>.
    /// </summary>
    /// <remarks>
    /// <c>DxfBlockPropertiesTableColumn.Distance(width)</c> is the column of the Width of a plate: a row of the value 200 sets
    /// Width to 200. <c>DxfBlockPropertiesTableColumn.Attribute(mark)</c> is the column of the attribute MARK: a row of the
    /// text P200 gives the INSERT's attribute MARK the value P200.
    /// </remarks>
    public sealed class DxfBlockPropertiesTableColumn
    {
        private DxfBlockPropertiesTableColumn(DxfBlockTableRecord block, int node, string handle, string name, string connection, DxfBlockParameterValueSet valueSet, bool isAngle, DxfAttributeDefinition attribute, IReadOnlyList<string> states = null)
        {
            States = states;
            Block = block;
            Node = node;
            Handle = handle;
            Name = name;
            Connection = connection;
            ValueSet = valueSet;
            IsAngle = isAngle;
            Definition = attribute;
        }

        /// <summary>The block the column's parameter or attribute definition is of.</summary>
        internal DxfBlockTableRecord Block { get; }

        /// <summary>The id of the node of the column's parameter (its 90); nought for an attribute definition.</summary>
        internal int Node { get; }

        /// <summary>The handle of the column's parameter or attribute definition (its 340).</summary>
        internal string Handle { get; }

        /// <summary>The name of the property the column sets, as Width, or the tag of its attribute definition.</summary>
        internal string Name { get; }

        /// <summary>The parameter's connection the column sets (its 301): UpdatedDistance, UpdatedDistanceX, UpdatedDistanceY or UpdatedAngle; empty for an attribute definition.</summary>
        internal string Connection { get; }

        /// <summary>The values a distance or an angle may take; null for an attribute, or a distance or an angle of any value.</summary>
        internal DxfBlockParameterValueSet ValueSet { get; }

        /// <summary>Whether the column gives an angle, in radians, rather than a distance.</summary>
        internal bool IsAngle { get; }

        /// <summary>The attribute definition of the column, whose cells are texts; null for a parameter's column, of numbers.</summary>
        internal DxfAttributeDefinition Definition { get; }

        /// <summary>The names of the states of the visibility parameter of the column, whose cells name one; null for another column.</summary>
        internal IReadOnlyList<string> States { get; }

        /// <summary>
        /// Makes the column of the distance of a linear parameter, its connection UpdatedDistance as AutoCAD writes it: each row
        /// gives the distance, above the point tolerance and of the parameter's value set where it has one.
        /// </summary>
        /// <param name="parameter">The linear parameter.</param>
        /// <returns>The column.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameter is null.</exception>
        public static DxfBlockPropertiesTableColumn Distance(DxfBlockLinearParameter parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            return new DxfBlockPropertiesTableColumn(parameter.Block, parameter.Node, parameter.Handle, parameter.Name, "UpdatedDistance", parameter.ValueSet, false, null);
        }

        /// <summary>
        /// Makes the column of the distance along x of an XY parameter, its connection UpdatedDistanceX as AutoCAD's Parking
        /// sample writes it: each row gives the distance, above the point tolerance and of the parameter's set along x where
        /// it has one.
        /// </summary>
        /// <param name="parameter">The XY parameter.</param>
        /// <returns>The column.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameter is null.</exception>
        public static DxfBlockPropertiesTableColumn X(DxfBlockXYParameter parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            return new DxfBlockPropertiesTableColumn(parameter.Block, parameter.Node, parameter.Handle, parameter.XName, "UpdatedDistanceX", parameter.XValueSet, false, null);
        }

        /// <summary>
        /// Makes the column of the distance along y of an XY parameter, its connection UpdatedDistanceY: each row gives the
        /// distance, above the point tolerance and of the parameter's set along y where it has one.
        /// </summary>
        /// <param name="parameter">The XY parameter.</param>
        /// <returns>The column.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameter is null.</exception>
        public static DxfBlockPropertiesTableColumn Y(DxfBlockXYParameter parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            return new DxfBlockPropertiesTableColumn(parameter.Block, parameter.Node, parameter.Handle, parameter.YName, "UpdatedDistanceY", parameter.YValueSet, false, null);
        }

        /// <summary>
        /// Makes the column of the distance of a polar parameter, its connection UpdatedDistance as a linear parameter's: each
        /// row gives the distance, above the point tolerance and of the parameter's distance set where it has one.
        /// </summary>
        /// <param name="parameter">The polar parameter.</param>
        /// <returns>The column.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameter is null.</exception>
        public static DxfBlockPropertiesTableColumn Distance(DxfBlockPolarParameter parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            return new DxfBlockPropertiesTableColumn(parameter.Block, parameter.Node, parameter.Handle, parameter.DistanceName, "UpdatedDistance", parameter.DistanceValueSet, false, null);
        }

        /// <summary>
        /// Makes the column of the angle of a polar parameter, its connection UpdatedAngle: each row gives the angle in radians,
        /// of the parameter's angle set where it has one.
        /// </summary>
        /// <param name="parameter">The polar parameter.</param>
        /// <returns>The column.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameter is null.</exception>
        public static DxfBlockPropertiesTableColumn Angle(DxfBlockPolarParameter parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            return new DxfBlockPropertiesTableColumn(parameter.Block, parameter.Node, parameter.Handle, parameter.AngleName, "UpdatedAngle", parameter.AngleValueSet, true, null);
        }

        /// <summary>
        /// Makes the column of the angle of a rotation parameter, its connection UpdatedAngle: each row gives the angle in
        /// radians from the parameter's base angle, of its value set where it has one.
        /// </summary>
        /// <param name="parameter">The rotation parameter.</param>
        /// <returns>The column.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameter is null.</exception>
        public static DxfBlockPropertiesTableColumn Angle(DxfBlockRotationParameter parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            return new DxfBlockPropertiesTableColumn(parameter.Block, parameter.Node, parameter.Handle, parameter.Name, "UpdatedAngle", parameter.ValueSet, true, null);
        }

        /// <summary>
        /// Makes the column of the visibility state of a block, its connection VisibilityState as AutoCAD 2021 wrote the column
        /// its <c>AddItem</c> gave a visibility parameter (plugin D26b): each row gives the name of one of the states, which the
        /// row shows. A block of the states Open and Closed given the rows (100, "Open") and (200, "Closed") of Width and this
        /// column shows Closed at the row 1.
        /// </summary>
        /// <param name="block">The dynamic block, given its visibility parameter by
        /// <see cref="DxfBlockTableRecord.AddVisibilityParameter"/>.</param>
        /// <returns>The column.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the block is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the block has no visibility parameter.</exception>
        public static DxfBlockPropertiesTableColumn Visibility(DxfBlockTableRecord block)
        {
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            DxfItem node = block.IsResident ? DxfDynamicBlockWriter.NodeOf(block, "BLOCKVISIBILITYPARAMETER") : null;

            if (node == null || !int.TryParse(DxfDynamicProperties.Text(DxfDynamicProperties.Section(node.Groups, "AcDbEvalExpr"), 90)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
            {
                throw new ArgumentException("The block " + block.Name + " has no visibility parameter.", nameof(block));
            }

            List<DxfTypedValue> own = DxfDynamicProperties.Section(node.Groups, "AcDbBlockVisibilityParameter");
            var states = new List<string>();

            foreach (DxfTypedValue state in own)
            {
                if (state.TypeCode == 303)
                {
                    states.Add(DxfReader.Decode(state.Value ?? string.Empty));
                }
            }

            string name = DxfReader.NameOf(DxfDynamicProperties.Text(own, 301) ?? string.Empty);
            return new DxfBlockPropertiesTableColumn(block, id, node.Handle, name, "VisibilityState", null, false, null, states.AsReadOnly());
        }

        /// <summary>
        /// Makes the column of an attribute definition of the block, of no connection as AutoCAD writes it: each row gives the
        /// text the INSERT's attribute of its tag is given.
        /// </summary>
        /// <param name="definition">The attribute definition, of the block the table is given to.</param>
        /// <returns>The column.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the attribute definition is null.</exception>
        public static DxfBlockPropertiesTableColumn Attribute(DxfAttributeDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            return new DxfBlockPropertiesTableColumn(null, 0, definition.Handle, definition.Tag, string.Empty, null, false, definition);
        }
    }
}
