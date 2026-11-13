using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The annotation scale of model space (CANNOSCALE) set as AutoCAD 2021 sets it: the scale named, and with ANNOAUTOSCALE on
    /// each annotative object of model space shown at it as OBJECTSCALE shows it, the layers it takes by the size of the variable.
    /// </summary>
    public static class CadAnnotationScale
    {
        /// <summary>
        /// Sets CANNOSCALE and, where <paramref name="autoScale"/> is above nought, gives the annotative objects of model space the
        /// scale: those on layers that are on, thawed and not locked from 1, those on locked layers from 2, and those on layers
        /// that are off or frozen from 4. An object that cannot be shown at the scale (a kind the presenter lays out at one scale
        /// only) is left, and one shown at it already.
        /// </summary>
        /// <param name="database">The drawing.</param>
        /// <param name="name">The name of a scale of the drawing's list.</param>
        /// <param name="autoScale">The value of ANNOAUTOSCALE.</param>
        /// <returns>How many objects were given the scale.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the drawing or the name is null.</exception>
        public static int Set(DxfDatabase database, string name, int autoScale)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            database.Cannoscale = name;

            if (autoScale <= 0)
            {
                return 0;
            }

            DxfAnnotationScale scale = CadCommands.ScaleNamed(database, name);

            if (scale == null)
            {
                return 0;
            }

            int given = 0;
            var entities = new List<DxfEntity>();

            foreach (DxfEntity entity in database.ModelSpace)
            {
                entities.Add(entity);
            }

            foreach (DxfEntity entity in entities)
            {
                if (entity.IsErased || entity.Annotative != DxfAnnotativeStates.True || entity.HasContext(scale))
                {
                    continue;
                }

                if (!LayerTaken(database, entity.Layer, autoScale))
                {
                    continue;
                }

                try
                {
                    CadCommands.AddScaleTo(database, entity, scale);
                    given++;
                }
                catch (Exception e) when (e is InvalidOperationException || e is NotSupportedException)
                {
                    // An object that cannot be shown at the scale keeps the scales it has.
                }
            }

            return given;
        }

        private static bool LayerTaken(DxfDatabase database, string layer, int autoScale)
        {
            if (string.IsNullOrEmpty(layer) || !database.LayerTable.Has(layer))
            {
                return true;
            }

            DxfLayerTableRecord record = database.LayerTable[layer];

            if (record.IsOff || record.IsFrozen)
            {
                return autoScale >= 4;
            }

            return record.IsLocked ? autoScale >= 2 : autoScale >= 1;
        }
    }
}
