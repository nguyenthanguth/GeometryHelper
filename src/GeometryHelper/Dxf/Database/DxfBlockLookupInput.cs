using System;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// An input column of the table of a lookup parameter of a dynamic block, AutoCAD's Input Property of its Lookup Table:
    /// the property of another parameter that a row of the table sets, the distance of a linear parameter or the x or the y
    /// of a point parameter, as AutoCAD's save of ACadSharp's lookup sample holds them; the distance along x or along y of
    /// an XY parameter, the distance or the angle of a polar parameter, or the angle of a rotation parameter, as AutoCAD 2021
    /// read and set them (plugin D25b). See <see cref="DxfBlockTableRecord.AddLookupParameter"/>.
    /// </summary>
    /// <remarks>
    /// <c>DxfBlockLookupInput.Distance(width)</c> is the column of the Width of a panel: a row of the value 200 sets Width to
    /// 200, and an INSERT of a Width of 200 shows that row's label.
    /// </remarks>
    public sealed class DxfBlockLookupInput
    {
        private DxfBlockLookupInput(
            DxfBlockTableRecord block,
            int node,
            string name,
            string connection,
            DxfBlockParameterValueSet valueSet,
            bool isAngle = false
        )
        {
            Block = block;
            Node = node;
            Name = name;
            Connection = connection;
            ValueSet = valueSet;
            IsAngle = isAngle;
        }

        /// <summary>The block the input's parameter is of.</summary>
        internal DxfBlockTableRecord Block { get; }

        /// <summary>The id of the node of the input's parameter (the column's 94).</summary>
        internal int Node { get; }

        /// <summary>The name of the property the column sets, as Width or Tag X.</summary>
        internal string Name { get; }

        /// <summary>The parameter's connection the column sets (its 304): UpdatedDistance, UpdatedX, UpdatedY, UpdatedDistanceX, UpdatedDistanceY or UpdatedAngle.</summary>
        internal string Connection { get; }

        /// <summary>The values a distance or an angle may take; null for a position, or a distance or an angle of any value.</summary>
        internal DxfBlockParameterValueSet ValueSet { get; }

        /// <summary>Whether the column gives an angle, in radians, rather than a distance or a position.</summary>
        internal bool IsAngle { get; }

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

            return new DxfBlockLookupInput(
                parameter.Block,
                parameter.Node,
                parameter.Name,
                "UpdatedDistance",
                parameter.ValueSet
            );
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

        /// <summary>
        /// Makes the column of the distance along x of an XY parameter, its connection UpdatedDistanceX as AutoCAD's Civil
        /// sample writes it: each row gives the distance, above the point tolerance and of the parameter's set along x where it
        /// has one.
        /// </summary>
        /// <param name="parameter">The XY parameter.</param>
        /// <returns>The column.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameter is null.</exception>
        public static DxfBlockLookupInput X(DxfBlockXYParameter parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            return new DxfBlockLookupInput(
                parameter.Block,
                parameter.Node,
                parameter.XName,
                "UpdatedDistanceX",
                parameter.XValueSet
            );
        }

        /// <summary>
        /// Makes the column of the distance along y of an XY parameter, its connection UpdatedDistanceY: each row gives the
        /// distance, above the point tolerance and of the parameter's set along y where it has one.
        /// </summary>
        /// <param name="parameter">The XY parameter.</param>
        /// <returns>The column.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameter is null.</exception>
        public static DxfBlockLookupInput Y(DxfBlockXYParameter parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            return new DxfBlockLookupInput(
                parameter.Block,
                parameter.Node,
                parameter.YName,
                "UpdatedDistanceY",
                parameter.YValueSet
            );
        }

        /// <summary>
        /// Makes the column of the distance of a polar parameter, its connection UpdatedDistance as a linear parameter's: each
        /// row gives the distance, above the point tolerance and of the parameter's distance set where it has one.
        /// </summary>
        /// <param name="parameter">The polar parameter.</param>
        /// <returns>The column.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameter is null.</exception>
        public static DxfBlockLookupInput Distance(DxfBlockPolarParameter parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            return new DxfBlockLookupInput(
                parameter.Block,
                parameter.Node,
                parameter.DistanceName,
                "UpdatedDistance",
                parameter.DistanceValueSet
            );
        }

        /// <summary>
        /// Makes the column of the angle of a polar parameter, its connection UpdatedAngle: each row gives the angle in
        /// radians, of the parameter's angle set where it has one.
        /// </summary>
        /// <param name="parameter">The polar parameter.</param>
        /// <returns>The column.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameter is null.</exception>
        public static DxfBlockLookupInput Angle(DxfBlockPolarParameter parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            return new DxfBlockLookupInput(
                parameter.Block,
                parameter.Node,
                parameter.AngleName,
                "UpdatedAngle",
                parameter.AngleValueSet,
                true
            );
        }

        /// <summary>
        /// Makes the column of the angle of a rotation parameter, its connection UpdatedAngle: each row gives the angle in
        /// radians from the parameter's base angle, of its value set where it has one.
        /// </summary>
        /// <param name="parameter">The rotation parameter.</param>
        /// <returns>The column.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameter is null.</exception>
        public static DxfBlockLookupInput Angle(DxfBlockRotationParameter parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            return new DxfBlockLookupInput(
                parameter.Block,
                parameter.Node,
                parameter.Name,
                "UpdatedAngle",
                parameter.ValueSet,
                true
            );
        }
    }
}
