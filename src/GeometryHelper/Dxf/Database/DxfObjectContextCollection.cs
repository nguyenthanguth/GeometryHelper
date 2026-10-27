using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The drawing's list of scales, as AutoCAD's <c>ObjectContextCollection</c> of the name <c>ACDB_ANNOTATIONSCALES</c>: the
    /// SCALE objects of the dictionary ACAD_SCALELIST of the named object dictionary, each a <see cref="DxfAnnotationScale"/>,
    /// in the order of the dictionary, and the annotation scale of model space among them as <see cref="CurrentContext"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A drawing AutoCAD saved lists its scales, 1:1, 1:2 and on, as SCALEs whose entries are named <c>A0</c>, <c>A1</c> and on;
    /// a drawing of ezdxf holds the dictionary empty. <c>scales.AddContext(new DxfAnnotationScale { Name = "1:50", PaperUnits = 1,
    /// DrawingUnits = 50 })</c> adds the scale of a plan in millimetres drawn at 1:50, its entry named by the first such name the
    /// list has not; <c>RemoveContext("1:50")</c> takes it out again where nothing uses it.
    /// </para>
    /// </remarks>
    public sealed class DxfObjectContextCollection : IEnumerable<DxfObjectContext>
    {
        private const string ScaleList = "ACAD_SCALELIST";

        private readonly DxfDatabase _database;

        internal DxfObjectContextCollection(DxfDatabase database)
        {
            _database = database;
        }

        /// <summary>Gets the name of the collection, as AutoCAD's <c>Name</c>: <c>ACDB_ANNOTATIONSCALES</c>.</summary>
        public string Name => DxfObjectContextManager.AnnotationScales;

        /// <summary>
        /// Gets or sets the current scale, as AutoCAD's <c>CurrentContext</c>: the scale the annotation scale of model space
        /// (<see cref="DxfDatabase.Cannoscale"/>, CANNOSCALE) names, which a new annotative object takes.
        /// </summary>
        /// <value>
        /// Get, the scale of the list of that name; null where the list has none of it, as the empty list of a drawing of ezdxf
        /// has no 1:1. Set, a scale of this drawing's list, whose name is written into CANNOSCALE as
        /// <see cref="DxfDatabase.Cannoscale"/> writes it.
        /// </value>
        /// <exception cref="ArgumentNullException">Thrown when the scale set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the scale set is not one of this drawing's list.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file with no variables of the OBJECTS section to hold it.</exception>
        public DxfObjectContext CurrentContext
        {
            get => Find(_database, _database.Cannoscale);

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _database.Cannoscale = Listed(value, nameof(value)).Name;
            }
        }

        /// <summary>Gets the scale of a name, as AutoCAD's <c>GetContext</c>: <c>GetContext("1:50")</c>.</summary>
        /// <param name="contextName">The name, letters of either case the same, the spaces around it taken off.</param>
        /// <returns>The scale; null where the list has none of the name.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public DxfObjectContext GetContext(string contextName)
        {
            if (contextName == null)
            {
                throw new ArgumentNullException(nameof(contextName));
            }

            return Find(_database, contextName);
        }

        /// <summary>Gets whether the list has a scale of a name, as AutoCAD's <c>HasContext</c>.</summary>
        /// <param name="contextName">The name, letters of either case the same.</param>
        /// <returns>Whether it has.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public bool HasContext(string contextName) => GetContext(contextName) != null;

        /// <summary>
        /// Adds a scale made in code to the list, as AutoCAD's <c>AddContext</c>: a SCALE at the end of the OBJECTS section, owned
        /// by ACAD_SCALELIST and named in it by the first of <c>A0</c>, <c>A1</c> and on it has not, its name (300), units
        /// (140, 141) and the flag of the unit scale (290) written as AutoCAD writes them, the flag 1 for a scale of as many units
        /// of the paper as of the drawing, 1:1, where the list has no unit scale, as AutoCAD keeps one, else 0; the class SCALE listed in the
        /// CLASSES section where it is not. The dictionary ACAD_SCALELIST is added to the named object dictionary where it has
        /// none, in a file of R2007 (AC1021) or later, the version of AutoCAD 2008, which brought the list. The scale stands for
        /// its SCALE from then on.
        /// </summary>
        /// <param name="context">The scale, a <see cref="DxfAnnotationScale"/> made in code and in no file.</param>
        /// <exception cref="ArgumentNullException">Thrown when the scale is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the context is not a <see cref="DxfAnnotationScale"/>, is in a file
        /// already, has no name, or has the name of a scale of the list, as AutoCAD answers eDuplicateKey.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the drawing has no OBJECTS section, or has no ACAD_SCALELIST and
        /// is older than R2007, or holds an ACAD_SCALELIST that is not a dictionary.</exception>
        public void AddContext(DxfObjectContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!(context is DxfAnnotationScale scale))
            {
                throw new ArgumentException("The list of scales takes a DxfAnnotationScale: a " + context.GetType().Name + " is not added.", nameof(context));
            }

            if (scale.Item != null)
            {
                throw new ArgumentException("A scale is added made in code and in no file yet: the " + scale + " is in one.", nameof(context));
            }

            DxfAnnotationScale.CheckName(scale.Name, nameof(context));

            if (Find(_database, scale.Name) != null)
            {
                throw new ArgumentException("The drawing's list of scales has a scale named " + scale.Name + " already, as AutoCAD answers eDuplicateKey.", nameof(context));
            }

            DxfDBDictionary list = List(true);
            string name = scale.Name;
            string paper = Numbers.Exact(scale.PaperUnits);
            string drawing = Numbers.Exact(scale.DrawingUnits);

            // A scale of as many units of the paper as of the drawing is the unit scale where the list has none, as AutoCAD
            // keeps one in every list.
            string unit = BitConverter.DoubleToInt64Bits(scale.PaperUnits) == BitConverter.DoubleToInt64Bits(scale.DrawingUnits) && !Scales(_database).Exists(s => s.IsUnitScale) ? "1" : "0";
            _database.File.AddClass("SCALE", "AcDbScale", 1153);
            scale.Bind(list.Add(NewKey(list), "SCALE", b =>
            {
                b.Group(100, "AcDbScale");
                b.Group(70, "0");
                b.Group(300, name);
                b.Group(140, paper);
                b.Group(141, drawing);
                b.Group(290, unit);
            }));
        }

        /// <summary>
        /// Takes a scale out of the list, as AutoCAD's <c>RemoveContext</c>: its SCALE erased with its entry of ACAD_SCALELIST.
        /// The scale AutoCAD keeps is refused: the unit scale (290 of 1), the current scale (<see cref="CurrentContext"/>), and a
        /// scale an object points at (the 340 of an annotative object's context, or any other hard pointer), as AutoCAD keeps a
        /// scale in use.
        /// </summary>
        /// <param name="contextName">The name of the scale, letters of either case the same.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the list has no scale of the name.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the scale is the unit scale, the current scale, or one in use.</exception>
        public void RemoveContext(string contextName)
        {
            if (contextName == null)
            {
                throw new ArgumentNullException(nameof(contextName));
            }

            DxfAnnotationScale scale = Find(_database, contextName);

            if (scale == null)
            {
                throw new KeyNotFoundException("The drawing's list of scales has no scale named " + contextName + ".");
            }

            if (scale.IsUnitScale)
            {
                throw new InvalidOperationException("The scale " + scale.Name + " is the drawing's unit scale, which AutoCAD keeps in every list.");
            }

            if (string.Equals(scale.Name, _database.Cannoscale?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The scale " + scale.Name + " is the current annotation scale (CANNOSCALE), which AutoCAD keeps: make another current first.");
            }

            int uses = _database.File.CountUses().OfHandle(scale.Handle);

            if (uses > 0)
            {
                throw new InvalidOperationException("The scale " + scale.Name + " is in use, " + uses.ToString(CultureInfo.InvariantCulture) + " objects pointing at it, and AutoCAD keeps a scale in use: take it from them first.");
            }

            DxfDBDictionary list = List(false);
            list.Remove(list.NameAt(DxfDBObject.OfObject(scale.Item)));
        }

        /// <summary>Gets an enumerator over the scales of the list, each a <see cref="DxfAnnotationScale"/>, in the order of the dictionary ACAD_SCALELIST.</summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator<DxfObjectContext> GetEnumerator()
        {
            foreach (DxfAnnotationScale scale in Scales(_database))
            {
                yield return scale;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>The scales of a drawing's list, in the order of ACAD_SCALELIST; none where it has no list.</summary>
        internal static List<DxfAnnotationScale> Scales(DxfDatabase database)
        {
            var scales = new List<DxfAnnotationScale>();
            DxfDBDictionary list = ListOf(database);

            if (list == null)
            {
                return scales;
            }

            foreach (DxfDBDictionaryEntry entry in list)
            {
                if (entry.Value?.Item != null && entry.Value.Item.Type == "SCALE")
                {
                    scales.Add(new DxfAnnotationScale(entry.Value.Item));
                }
            }

            return scales;
        }

        /// <summary>The first scale of a drawing's list of a name, letters of either case the same; null where there is none.</summary>
        internal static DxfAnnotationScale Find(DxfDatabase database, string name)
        {
            if (name == null)
            {
                return null;
            }

            foreach (DxfAnnotationScale scale in Scales(database))
            {
                if (string.Equals(scale.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return scale;
                }
            }

            return null;
        }

        /// <summary>A scale of this drawing's list, given as a context; refused where it is not one.</summary>
        internal DxfAnnotationScale Listed(DxfObjectContext context, string parameter)
            => Listed(_database, context, parameter);

        /// <summary>A scale of a drawing's list, given as a context: a SCALE of its file, not erased; refused where it is not one.</summary>
        internal static DxfAnnotationScale Listed(DxfDatabase database, DxfObjectContext context, string parameter)
        {
            if (!(context is DxfAnnotationScale scale) || scale.Item == null || scale.Item.File != database.File || scale.Item.IsRemoved)
            {
                throw new ArgumentException("The context is to be a scale of the drawing's list of scales: the " + context + " is not one.", parameter);
            }

            return scale;
        }

        /// <summary>The dictionary ACAD_SCALELIST of a drawing; null where it has none, or one that is not a dictionary.</summary>
        private static DxfDBDictionary ListOf(DxfDatabase database)
        {
            DxfDBDictionary root = database.NamedObjectsDictionary;
            return root != null && root.Contains(ScaleList) ? root.GetAt(ScaleList) as DxfDBDictionary : null;
        }

        /// <summary>The dictionary ACAD_SCALELIST, made where it is to be and the drawing has none.</summary>
        private DxfDBDictionary List(bool make)
        {
            DxfDBDictionary list = ListOf(_database);

            if (list != null || !make)
            {
                return list;
            }

            DxfDBDictionary root = _database.NamedObjectsDictionary;

            if (root == null)
            {
                throw new InvalidOperationException("A DXF " + (_database.Version ?? "with no version") + " has no OBJECTS section to hold a list of scales: R13 (AC1012) brought it.");
            }

            if (root.Contains(ScaleList))
            {
                throw new InvalidOperationException("The named object dictionary holds an ACAD_SCALELIST that is not a dictionary, which the scales are kept in.");
            }

            if (!_database.File.Supports(1021))
            {
                throw new InvalidOperationException("A DXF older than R2007 (AC1021) has no list of scales: AutoCAD 2008, which saves R2007, brought it.");
            }

            root.SetAt(ScaleList, new DxfDBDictionary());
            return ListOf(_database);
        }

        /// <summary>The first of <c>A0</c>, <c>A1</c> and on the list has no entry of, as AutoCAD names the entries of its scales.</summary>
        private static string NewKey(DxfDBDictionary list)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfDBDictionaryEntry entry in list)
            {
                keys.Add(entry.Key.Trim());
            }

            for (int n = 0; ; n++)
            {
                string key = "A" + n.ToString(CultureInfo.InvariantCulture);

                if (!keys.Contains(key))
                {
                    return key;
                }
            }
        }
    }
}
