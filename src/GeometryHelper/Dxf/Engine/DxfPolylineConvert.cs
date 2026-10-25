using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The conversions between a <see cref="DxfPolyline"/> and a <see cref="DxfPolyline2d"/>, as AutoCAD's <c>ConvertFrom</c> and
    /// <c>ConvertTo</c>: the data carried over, and where the place is taken, the record written anew as the other type in its
    /// place, its handle, owner, groups of the applications, look and extended data kept, the new object standing for it from
    /// then on and the old one reading as the record was, erased.
    /// </summary>
    internal static class DxfPolylineConvert
    {
        /// <summary>Gives a polyline the data of a polyline of the plane, taking its place where asked.</summary>
        internal static void Into(DxfPolyline2d source, DxfPolyline target, bool transferId)
        {
            Live(source, "entity");
            Live(target, "entity");

            if (source.PolyType == DxfPoly2dType.QuadSplinePoly || source.PolyType == DxfPoly2dType.CubicSplinePoly)
            {
                throw new ArgumentException("A polyline fitted with a spline is not converted, as AutoCAD does not convert one: straighten it first.", "entity");
            }

            List<DxfPolyVertex> vertices = source.Model(out _);
            bool closed = source.Closed;
            GeoVector3 normal = source.Normal;
            double elevation = source.Elevation;
            double thickness = source.Thickness;
            bool generation = source.LinetypeGenerationOn;
            double constant = source.ConstantWidth;

            if (!transferId)
            {
                target.SetPropertiesFrom(source);
                target.Take(vertices, closed, normal, elevation, thickness, generation, constant);
                return;
            }

            if (target.IsResident)
            {
                throw new InvalidOperationException("The polyline that takes another's place is to be in no file: make it in code.");
            }

            if (!source.IsResident)
            {
                throw new InvalidOperationException("The polyline of the plane is in no file, and has no place to be taken.");
            }

            DxfItem item = source.Item;

            if (!item.File.Supports(1014))
            {
                throw new ArgumentException("A DXF older than R14 (AC1014) has no LWPOLYLINE to take the polyline's place.", "entity");
            }

            DxfItem snapshot = item.File.CopyOf(item);
            item.File.SetShape(item, null, DxfPolyline.Records(vertices, closed, normal, elevation, thickness, generation, constant));
            Hand(source, target, item, snapshot);
        }

        /// <summary>A polyline of the plane of a polyline's data, taking its place where asked.</summary>
        internal static DxfPolyline2d Out(DxfPolyline source, bool transferId)
        {
            Live(source, "transferId");
            List<DxfPolyVertex> vertices = source.Model();
            bool closed = source.Closed;
            GeoVector3 normal = source.Normal;
            double elevation = source.Elevation;
            double thickness = source.Thickness;
            bool generation = source.Plinegen;
            var target = new DxfPolyline2d();

            if (!transferId)
            {
                target.SetPropertiesFrom(source);
                target.Take(vertices, closed, normal, elevation, thickness, generation);
                return target;
            }

            if (!source.IsResident)
            {
                throw new InvalidOperationException("The polyline is in no file, and has no place to be taken.");
            }

            DxfItem item = source.Item;
            DxfItem snapshot = item.File.CopyOf(item);
            item.File.SetShape(item, null, DxfPolyline2d.Records(vertices, closed, normal, elevation, thickness, generation));
            Hand(source, target, item, snapshot);
            return target;
        }

        /// <summary>
        /// Hands a record over from one object to another, as AutoCAD hands an object id over: the new object stands for the
        /// record, and the old one for a copy of it as it was before, erased.
        /// </summary>
        private static void Hand(DxfEntity from, DxfEntity to, DxfItem item, DxfItem snapshot)
        {
            item.ReplaceWrapper(to);
            to.Bind(item);
            snapshot.IsRemoved = true;
            from.Bind(snapshot);
        }

        /// <summary>Refuses an erased entity.</summary>
        private static void Live(DxfEntity entity, string parameter)
        {
            if (entity.IsErased)
            {
                throw new InvalidOperationException("The " + entity.Item.Type + " " + (entity.Handle ?? string.Empty) + " is erased, and is not converted (" + parameter + ").");
            }
        }
    }
}
