using System;

namespace GeometryHelper.Dxf
{
    public abstract partial class DxfDBObject
    {
        // The entry of the extension dictionary that holds an object's fields, and the property a text's words are.
        private const string FieldDictionaryName = "ACAD_FIELD";
        private const string TextProperty = "TEXT";

        /// <summary>
        /// Gets the field of the object's words, as AutoCAD's <c>GetField()</c>: that of the property TEXT, which a TEXT, an
        /// MTEXT, an attribute and a dimension keep their words' field under, as <see cref="GetField(string)"/> gives it.
        /// </summary>
        /// <returns>The field; null where the object has none.</returns>
        public DxfField GetField() => GetField(TextProperty);

        /// <summary>
        /// Gets the field of a property of the object, as AutoCAD's <c>GetField(propertyName)</c>: the FIELD its extension
        /// dictionary's dictionary ACAD_FIELD names by the property's name, as AutoCAD keeps it.
        /// </summary>
        /// <param name="propertyName">The name of the property, letters of either case the same: TEXT for the words of a text.</param>
        /// <returns>The field; null where the object has no extension dictionary, no ACAD_FIELD in it, or no FIELD of the name there.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public DxfField GetField(string propertyName)
        {
            if (propertyName == null)
            {
                throw new ArgumentNullException(nameof(propertyName));
            }

            DxfDBDictionary fields = FieldDictionary(false);
            return fields != null && fields.Contains(propertyName) ? fields.GetAt(propertyName) as DxfField : null;
        }

        /// <summary>
        /// Gives the object's words a field, as AutoCAD's <c>SetField(field)</c>: under the property TEXT, as
        /// <see cref="SetField(string, DxfField)"/> gives it.
        /// </summary>
        /// <param name="field">The field, made in code and in no file yet.</param>
        /// <returns>The field, now in the object's file.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the field is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the field or a field it holds is in a file already, as the child of a
        /// field given to another object is; nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the object is in no file, is erased, or its file is older than
        /// R2004, which brought fields.</exception>
        public DxfField SetField(DxfField field) => SetField(TextProperty, field);

        /// <summary>
        /// Gives a property of the object a field, as AutoCAD's <c>SetField(propertyName, field)</c>: the field and its children
        /// written as FIELDs at the end of the OBJECTS section, named by the property's name in the dictionary ACAD_FIELD of the
        /// object's extension dictionary, both made where the object has none, each owned hard by what holds it, in the layout the
        /// DXF reference gives (not yet checked on a drawing AutoCAD saved, as <see cref="DxfField"/> says); the class FIELD listed
        /// in the CLASSES section where it is not. A field the property had is erased with its
        /// children. The object's words are not changed: AutoCAD writes the field's value in them when it evaluates it.
        /// <c>text.SetField(new DxfField("Drawn %&lt;\\AcVar Date&gt;%", true))</c> gives a TEXT the date as a field.
        /// </summary>
        /// <param name="propertyName">The name of the property: TEXT for the words of a text.</param>
        /// <param name="field">The field, made in code and in no file yet.</param>
        /// <returns>The field, now in the object's file.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name or the field is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty or has a control character in it, or the field or a
        /// field it holds is in a file already, as the child of a field given to another object is; nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the object is in no file, is erased, or its file is older than
        /// R2004, which brought fields; or its extension dictionary holds an ACAD_FIELD that is not a dictionary.</exception>
        public DxfField SetField(string propertyName, DxfField field)
        {
            CheckProperty(propertyName);

            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }

            // The field and every field it holds, which are written with it and bound to their records.
            if (field.AnyInFile())
            {
                throw new ArgumentException("A field is given to an object made in code and in no file yet, with every field it holds: the " + field + " or one of its fields is in one.", nameof(field));
            }

            DxfFile file = InFile("be given a field");

            if (!file.Supports(1018))
            {
                throw new InvalidOperationException("A DXF older than R2004 (AC1018) has no FIELD: fields came with it.");
            }

            DxfField.Write(FieldDictionary(true), propertyName, field);
            return field;
        }

        /// <summary>
        /// Takes out the field of the object's words, as AutoCAD's <c>RemoveField()</c>: that of the property TEXT, as
        /// <see cref="RemoveField(string)"/> takes one out.
        /// </summary>
        /// <returns>The field, erased; null where the object had none.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the object is erased.</exception>
        public DxfField RemoveField() => RemoveField(TextProperty);

        /// <summary>
        /// Takes out the field of a property of the object, as AutoCAD's <c>RemoveField(propertyName)</c>: its entry of the
        /// dictionary ACAD_FIELD taken out, the field erased with its children, and the dictionary ACAD_FIELD taken out of the
        /// extension dictionary once it is empty. The object's words are kept as they are, the field's last value among them.
        /// </summary>
        /// <param name="propertyName">The name of the property, letters of either case the same.</param>
        /// <returns>The field, erased; null where the object had none of the property.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the object is erased.</exception>
        public DxfField RemoveField(string propertyName)
        {
            if (propertyName == null)
            {
                throw new ArgumentNullException(nameof(propertyName));
            }

            if (IsErased)
            {
                InFile("have a field taken out");
            }

            DxfField field = GetField(propertyName);

            if (field == null)
            {
                return null;
            }

            InFile("have a field taken out");
            DxfDBDictionary fields = FieldDictionary(false);
            DxfField.EraseChildren(field);
            fields.Remove(propertyName);

            if (fields.Count == 0)
            {
                ExtensionDictionary.Remove(FieldDictionaryName);
            }

            return field;
        }

        /// <summary>
        /// Takes out a field of the object, as AutoCAD's <c>RemoveField(fieldId)</c>: of the property it is the field of, as
        /// <see cref="RemoveField(string)"/> takes one out.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <exception cref="ArgumentNullException">Thrown when the field is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the field is not one of the object's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the object is erased.</exception>
        public void RemoveField(DxfField field)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }

            string name = FieldDictionary(false)?.NameOf(field);

            if (name == null)
            {
                throw new ArgumentException("The " + field + " is not a field of the object.", nameof(field));
            }

            RemoveField(name);
        }

        /// <summary>
        /// The dictionary ACAD_FIELD of the object's extension dictionary; made, with the extension dictionary, where it is to
        /// be and there is none: owning its fields hard (280 of 1), as AutoCAD writes it. Null where it is not to be made and
        /// there is none.
        /// </summary>
        internal DxfDBDictionary FieldDictionary(bool make)
        {
            DxfDBDictionary extension = ExtensionDictionary;

            if (extension != null && extension.Contains(FieldDictionaryName))
            {
                if (extension.GetAt(FieldDictionaryName) is DxfDBDictionary fields)
                {
                    return fields;
                }

                if (make)
                {
                    throw new InvalidOperationException("The extension dictionary of the object holds an ACAD_FIELD that is not a dictionary, which fields are kept in.");
                }

                return null;
            }

            if (!make)
            {
                return null;
            }

            CreateExtensionDictionary();
            var made = new DxfDBDictionary { TreatElementsAsHard = true };
            ExtensionDictionary.SetAt(FieldDictionaryName, made);
            return made;
        }

        private static void CheckProperty(string propertyName)
        {
            if (propertyName == null)
            {
                throw new ArgumentNullException(nameof(propertyName));
            }

            if (propertyName.Trim().Length == 0)
            {
                throw new ArgumentException("The name of a property has to be given: TEXT for the words of a text.", nameof(propertyName));
            }

            foreach (char c in propertyName)
            {
                if (c < ' ')
                {
                    throw new ArgumentException("The name of a property has no control character in it: '" + propertyName + "' has one.", nameof(propertyName));
                }
            }
        }
    }
}
