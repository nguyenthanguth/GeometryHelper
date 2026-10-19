using GeometryHelper.Geometry;
using GeometryHelper.Meshing;

namespace GeometryHelper.Viewer
{
    public sealed partial class SceneWriter
    {
        /// <summary>
        /// Adds a point in the plane.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        public SceneWriter Add(GeoPoint2 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds a point in space.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        public SceneWriter Add(GeoPoint3 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds a segment in the plane.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        public SceneWriter Add(GeoLine2 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds a segment in space.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        public SceneWriter Add(GeoLine3 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds a ray in the plane, drawn to the edge of the scene.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        public SceneWriter Add(GeoRay2 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds a ray in space, drawn to the edge of the scene.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        public SceneWriter Add(GeoRay3 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds an open chain in the plane.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the shape is null.</exception>
        public SceneWriter Add(GeoPolyline2 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds a closed polygon in the plane.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the shape is null.</exception>
        public SceneWriter Add(GeoPolygon2 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds an open chain in space.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the shape is null.</exception>
        public SceneWriter Add(GeoPolyline3 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds a closed flat polygon in space.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the shape is null.</exception>
        public SceneWriter Add(GeoPolygon3 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds an open chain in the plane whose edges may be arcs.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the shape is null.</exception>
        public SceneWriter Add(GeoPolylineArc2 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds a closed loop in the plane whose edges may be arcs.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the shape is null.</exception>
        public SceneWriter Add(GeoPolygonArc2 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds an open chain in space whose edges may be arcs.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the shape is null.</exception>
        public SceneWriter Add(GeoPolylineArc3 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds a closed flat loop in space whose edges may be arcs.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the shape is null.</exception>
        public SceneWriter Add(GeoPolygonArc3 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds an arc in the plane.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        public SceneWriter Add(GeoArc2 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds an arc in space.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        public SceneWriter Add(GeoArc3 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds a circle in the plane.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        public SceneWriter Add(GeoCircle2 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds a circle in space.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        public SceneWriter Add(GeoCircle3 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds an ellipse in the plane.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        public SceneWriter Add(GeoEllipse2 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds an ellipse in space.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        public SceneWriter Add(GeoEllipse3 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds a rectangle in the plane, which may be turned.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        public SceneWriter Add(GeoRectangle2 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds a triangle in the plane.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        public SceneWriter Add(GeoTriangle2 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds a triangle in space.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        public SceneWriter Add(GeoTriangle3 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds a face with its holes in the plane.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the shape is null.</exception>
        public SceneWriter Add(GeoFace2 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds a flat face with its holes in space.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the shape is null.</exception>
        public SceneWriter Add(GeoFace3 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds a body, its openings included.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the shape is null.</exception>
        public SceneWriter Add(GeoSolid3 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds a mesh in the plane.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the shape is null.</exception>
        public SceneWriter Add(GeoMesh2 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds a mesh of a flat shape in space.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the shape is null.</exception>
        public SceneWriter Add(GeoMesh3 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds an axis-aligned box.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        public SceneWriter Add(GeoAabb3 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds an oriented box.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the shape is null.</exception>
        public SceneWriter Add(GeoObb3 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);

        /// <summary>
        /// Adds a plane, drawn as a square patch sized to the scene.
        /// </summary>
        /// <param name="geometry">The shape.</param>
        /// <param name="layer">The layer; the layer named "default" when none is given.</param>
        /// <param name="name">The name in the scene tree; the kind and a number when none is given.</param>
        /// <param name="color">The colour of this item as #RRGGBB; its layer's when none is given.</param>
        /// <param name="props">Properties to show with the item: an anonymous object, a dictionary, or any object whose public properties are read.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        public SceneWriter Add(GeoPlane3 geometry, string layer = null, string name = null, string color = null, object props = null)
            => Put(geometry, layer, name, color, props);
    }
}
