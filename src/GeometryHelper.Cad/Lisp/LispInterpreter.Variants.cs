using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// The variants and safearrays of ActiveX, what <c>(vl-load-com)</c> brings besides the objects: <c>vlax-make-variant</c>,
    /// <c>vlax-variant-*</c>, <c>vlax-make-safearray</c>, <c>vlax-safearray-*</c>, <c>vlax-3d-point</c>, <c>vlax-tmatrix</c>, the
    /// old names <c>variant-value</c> and <c>safearray-value</c>, and the <c>vlax-vb*</c> constants. Measured in AutoCAD 2021's
    /// accoreconsole (probes a2, a3, a4), which has them although it has no objects.
    /// </summary>
    public sealed partial class LispInterpreter
    {
        /// <summary>The most elements <c>vlax-make-safearray</c> makes, so that a bound typed by mistake cannot fill the memory.</summary>
        private const long MaxSafeArrayItems = 1 << 24;

        partial void RegisterVariants() => OnLoadCom(DefineVariants);

        private void DefineVariants()
        {
            Define("VLAX-MAKE-VARIANT", 0, 2, MakeVariant);
            LispBuiltin variantValue = Define("VLAX-VARIANT-VALUE", 1, 1, a => VariantValue(Variant(a[0])));
            Define("VLAX-VARIANT-TYPE", 1, 1, a => Variant(a[0]).Type);
            Define("VLAX-VARIANT-CHANGE-TYPE", 2, 2, VariantChangeType);
            Define("VLAX-MAKE-SAFEARRAY", 2, -1, MakeSafeArray);
            Define("VLAX-SAFEARRAY-FILL", 2, 2, SafeArrayFill);
            LispBuiltin safeArrayList = Define("VLAX-SAFEARRAY->LIST", 1, 1, a => SafeArrayToList(SafeArray(a[0])));
            Define("VLAX-SAFEARRAY-GET-ELEMENT", 2, -1, SafeArrayGetElement);
            Define("VLAX-SAFEARRAY-PUT-ELEMENT", 3, -1, SafeArrayPutElement);
            Define("VLAX-SAFEARRAY-GET-DIM", 1, 1, a => SafeArray(a[0]).Rank);
            Define("VLAX-SAFEARRAY-GET-L-BOUND", 2, 2, a => BoundOf(a, upper: false));
            Define("VLAX-SAFEARRAY-GET-U-BOUND", 2, 2, a => BoundOf(a, upper: true));
            Define("VLAX-SAFEARRAY-TYPE", 1, 1, a => SafeArray(a[0]).ElementType);
            Define("VLAX-3D-POINT", 1, 3, Point3D);
            Define("VLAX-TMATRIX", 1, 1, TransformMatrix);

            // The old names are the same functions, as AutoCAD 2021 prints them (a2).
            Intern("VARIANT-VALUE").Value = variantValue;
            Intern("SAFEARRAY-VALUE").Value = safeArrayList;

            // Ordinary symbols: a routine may set one (a4: (setq vlax-vbDouble 99) changes it).
            Intern("VLAX-VBEMPTY").Value = 0;
            Intern("VLAX-VBNULL").Value = 1;
            Intern("VLAX-VBINTEGER").Value = 2;
            Intern("VLAX-VBLONG").Value = 3;
            Intern("VLAX-VBSINGLE").Value = 4;
            Intern("VLAX-VBDOUBLE").Value = 5;
            Intern("VLAX-VBSTRING").Value = 8;
            Intern("VLAX-VBOBJECT").Value = 9;
            Intern("VLAX-VBBOOLEAN").Value = 11;
            Intern("VLAX-VBVARIANT").Value = 12;
            Intern("VLAX-VBARRAY").Value = 8192;
        }

        #region Arguments and values

        private static LispVariant Variant(object value) => value as LispVariant ?? throw TypeError("variantp", value);

        private static LispSafeArray SafeArray(object value) =>
            value as LispSafeArray ?? throw TypeError("safearrayp", value);

        private static LispException NoCoercion(object value) =>
            new LispException("lisp value has no coercion to VARIANT with this type:  " + Print(value, true));

        private static LispException InvalidIndex() =>
            new LispException("ActiveX Server returned an error: Invalid index");

        /// <summary>The symbol <c>:vlax-true</c> or <c>:vlax-false</c>: what a Boolean reads as.</summary>
        private object VlaxBoolean(bool value) => Intern(value ? ":VLAX-TRUE" : ":VLAX-FALSE");

        private bool IsTrueSymbol(object value) =>
            ReferenceEquals(value, _true) || ReferenceEquals(value, Intern(":VLAX-TRUE"));

        private bool IsFalseSymbol(object value) => ReferenceEquals(value, Intern(":VLAX-FALSE"));

        /// <summary>A value of the interpreter as the scalar a variant keeps: an int, a double, a string, a bool.</summary>
        private bool TryScalar(object value, out object scalar)
        {
            scalar = value;

            switch (value)
            {
                case int _:
                case double _:
                case string _:
                    return true;
                case LispVariant variant when variant.Type < 8192 && variant.Type != 9:
                    scalar = variant.Value;
                    return true;
            }

            if (IsTrueSymbol(value))
            {
                scalar = true;
                return true;
            }

            if (IsFalseSymbol(value))
            {
                scalar = false;
                return true;
            }

            return false;
        }

        /// <summary>The variant a value makes with no type given (a2, a3): an integer 3, a real 5, a string 8, nil 0, true and false 11.</summary>
        private LispVariant VariantOfValue(object value)
        {
            switch (value)
            {
                case null:
                    return LispVariantConversion.Scalar(0, null);
                case int i:
                    return LispVariantConversion.Scalar(3, i);
                case double d:
                    return LispVariantConversion.Scalar(5, d);
                case string s:
                    return LispVariantConversion.Scalar(8, s);
                case LispVariant variant:
                    return variant;
                case LispSafeArray array:
                    return LispVariant.OfArray(array);
                case LispVlaObject _:
                    return LispVariantConversion.Scalar(9, value);
            }

            if (IsTrueSymbol(value))
            {
                return LispVariantConversion.Scalar(11, true);
            }

            if (IsFalseSymbol(value))
            {
                return LispVariantConversion.Scalar(11, false);
            }

            throw NoCoercion(value);
        }

        /// <summary>What a variant holds as a value of the interpreter: a Boolean as <c>:vlax-true</c> or <c>:vlax-false</c>.</summary>
        private object VariantValue(LispVariant variant)
        {
            switch (variant.Type)
            {
                case 2:
                case 3:
                case 4:
                case 5:
                case 8:
                case 9:
                    return variant.Value;
                case 11:
                    return VlaxBoolean((bool)variant.Value);
                default:
                    return variant.Type >= 8192 ? variant.Value : null;
            }
        }

        /// <summary>An element of a safearray as a value of the interpreter.</summary>
        private object ItemValue(object item) => item is bool flag ? VlaxBoolean(flag) : item;

        #endregion

        #region Variants

        private object MakeVariant(object[] a)
        {
            object value = a.Length > 0 ? a[0] : null;

            if (a.Length < 2)
            {
                return VariantOfValue(value);
            }

            int type = Int(a[1]);

            if (value is LispSafeArray array)
            {
                return type == 8192 + array.ElementType ? LispVariant.OfArray(array) : throw NoCoercion(value);
            }

            if (type == 0 || type == 1)
            {
                return LispVariantConversion.Scalar(type, null);
            }

            if (type == 9)
            {
                return value is LispVlaObject ? LispVariantConversion.Scalar(9, value) : throw NoCoercion(value);
            }

            return LispVariantConversion.TryChangeType(VariantOfValue(value), type, out LispVariant converted)
                ? converted
                : throw NoCoercion(value);
        }

        private object VariantChangeType(object[] a)
        {
            LispVariant variant = Variant(a[0]);
            return LispVariantConversion.TryChangeType(variant, Int(a[1]), out LispVariant converted)
                ? converted
                : null;
        }

        #endregion

        #region Safearrays

        private object MakeSafeArray(object[] a)
        {
            int type = Int(a[0]);
            int rank = a.Length - 1;
            var lower = new int[rank];
            var upper = new int[rank];
            long count = 1;

            for (int i = 0; i < rank; i++)
            {
                if (!(a[i + 1] is LispCons pair) || pair.Cdr == null || pair.Cdr is LispCons)
                {
                    throw new LispException("Array specification (bounds) is not a dotted pair.");
                }

                lower[i] = Int(pair.Car);
                upper[i] = Int(pair.Cdr);

                if (upper[i] < lower[i])
                {
                    throw new LispException("Upper bound in SAFEARRAYBOUND occurred to be less than lower.");
                }

                count *= (long)upper[i] - lower[i] + 1;

                if (count > MaxSafeArrayItems)
                {
                    throw new LispException("ActiveX Server returned an error: Out of memory");
                }
            }

            return new LispSafeArray(type, lower, upper);
        }

        /// <summary>The value an item takes for the element type of its array (a3: 1.6 into integers is 2, 1 into strings is "1").</summary>
        private object ItemFor(int elementType, object value)
        {
            switch (elementType)
            {
                case 12:
                    return VariantOfValue(value);
                case 9:
                    return value == null || value is LispVlaObject ? value : throw NoCoercion(value);
                case 11:
                    if (value == null)
                    {
                        return false;
                    }

                    break;
                case 2:
                case 3:
                case 4:
                case 5:
                case 8:
                    break;
                default:
                    throw NoCoercion(value);
            }

            if (
                !TryScalar(value, out object scalar)
                || !LispVariantConversion.TryConvert(scalar, elementType, out object item)
            )
            {
                throw NoCoercion(value);
            }

            return item;
        }

        private object SafeArrayFill(object[] a)
        {
            LispSafeArray array = SafeArray(a[0]);

            if (a[1] != null && !(a[1] is LispCons))
            {
                throw TypeError("listp", a[1]);
            }

            var flat = new List<object>();

            if (!Flatten(array, 0, a[1], flat) || flat.Count != array.Items.Length)
            {
                throw new LispException(
                    "vlax-safearray-fill failed.  Invalid initialization list. "
                        + Print(array, true)
                        + " "
                        + Print(a[1], true)
                );
            }

            var converted = new object[flat.Count];

            for (int i = 0; i < converted.Length; i++)
            {
                converted[i] = ItemFor(array.ElementType, flat[i]);
            }

            converted.CopyTo(array.Items, 0);
            return array;
        }

        /// <summary>Walks a nested list that has to fit the dimensions of an array exactly, one level a dimension.</summary>
        private static bool Flatten(LispSafeArray array, int dimension, object list, List<object> flat)
        {
            int size = array.Upper[dimension] - array.Lower[dimension] + 1;
            int count = 0;
            object rest = list;

            while (rest is LispCons cell)
            {
                count++;

                if (dimension == array.Rank - 1)
                {
                    flat.Add(cell.Car);
                }
                else if (!(cell.Car == null || cell.Car is LispCons) || !Flatten(array, dimension + 1, cell.Car, flat))
                {
                    return false;
                }

                rest = cell.Cdr;
            }

            return rest == null && count == size;
        }

        private object SafeArrayToList(LispSafeArray array)
        {
            if (array.Items.Length == 0)
            {
                throw InvalidIndex();
            }

            int offset = 0;
            return ListOfDimension(array, 0, ref offset);
        }

        private object ListOfDimension(LispSafeArray array, int dimension, ref int offset)
        {
            int size = array.Upper[dimension] - array.Lower[dimension] + 1;
            var items = new object[size];

            for (int i = 0; i < size; i++)
            {
                items[i] =
                    dimension == array.Rank - 1
                        ? ItemValue(array.Items[offset++])
                        : ListOfDimension(array, dimension + 1, ref offset);
            }

            return ListOf(items);
        }

        /// <summary>The place of an element in <see cref="LispSafeArray.Items"/>, from one index per dimension.</summary>
        private static int OffsetOf(LispSafeArray array, object[] a, string name, int first, int count)
        {
            if (count != array.Rank)
            {
                throw new LispException(
                    name + " failed. Wrong count of indexes: " + count.ToString(CultureInfo.InvariantCulture)
                );
            }

            long offset = 0;

            for (int d = 0; d < count; d++)
            {
                int index = Int(a[first + d]);

                if (index < array.Lower[d] || index > array.Upper[d])
                {
                    throw InvalidIndex();
                }

                offset = offset * ((long)array.Upper[d] - array.Lower[d] + 1) + (index - array.Lower[d]);
            }

            return (int)offset;
        }

        private object SafeArrayGetElement(object[] a)
        {
            LispSafeArray array = SafeArray(a[0]);
            return ItemValue(array.Items[OffsetOf(array, a, "safearray-get-element", 1, a.Length - 1)]);
        }

        private object SafeArrayPutElement(object[] a)
        {
            LispSafeArray array = SafeArray(a[0]);
            int offset = OffsetOf(array, a, "safearray-put-element", 1, a.Length - 2);
            object value = a[a.Length - 1];
            array.Items[offset] = ItemFor(array.ElementType, value);
            return value;
        }

        private static object BoundOf(object[] a, bool upper)
        {
            LispSafeArray array = SafeArray(a[0]);
            int dimension = Int(a[1]);

            if (dimension < 1 || dimension > array.Rank)
            {
                throw InvalidIndex();
            }

            return upper ? array.Upper[dimension - 1] : array.Lower[dimension - 1];
        }

        #endregion

        #region Points and matrices

        private object Point3D(object[] a)
        {
            var values = new double[3];

            if (a.Length == 1)
            {
                if (a[0] != null && !(a[0] is LispCons))
                {
                    throw new LispException("bad 3D point: " + Print(a[0], true));
                }

                var items = new List<object>();
                object rest = a[0];

                while (rest is LispCons cell)
                {
                    items.Add(cell.Car);
                    rest = cell.Cdr;
                }

                if (rest != null || items.Count < 2 || items.Count > 3)
                {
                    throw new LispException("bad 3D point list: " + Print(a[0], true));
                }

                for (int i = 0; i < items.Count; i++)
                {
                    values[i] = ToDouble(Number(items[i]));
                }
            }
            else
            {
                for (int i = 0; i < a.Length; i++)
                {
                    values[i] = ToDouble(Number(a[i]));
                }
            }

            return LispVariant.Point(values[0], values[1], values[2]);
        }

        private object TransformMatrix(object[] a)
        {
            var matrix = new LispSafeArray(5, new[] { 0, 0 }, new[] { 3, 3 });
            object rows = a[0];

            for (int r = 0; r < 4; r++)
            {
                if (!(rows is LispCons row))
                {
                    throw BadMatrix(a[0]);
                }

                object cells = row.Car;

                for (int c = 0; c < 4; c++)
                {
                    if (!(cells is LispCons cell) || !(cell.Car is int || cell.Car is double))
                    {
                        throw BadMatrix(a[0]);
                    }

                    matrix.Items[r * 4 + c] = ToDouble(cell.Car);
                    cells = cell.Cdr;
                }

                if (cells != null)
                {
                    throw BadMatrix(a[0]);
                }

                rows = row.Cdr;
            }

            return rows == null ? LispVariant.OfArray(matrix) : throw BadMatrix(a[0]);
        }

        private static LispException BadMatrix(object list) =>
            new LispException("bad transform matrix list: " + Print(list, true));

        #endregion
    }
}
