using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// The objects one drawing is made from, and the frame its views are set in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The objects are kept in the order of their ids, compared character by character, so a drawing never follows the
    /// order the model gave them in: a beam "B-2" and its end plate "B-1" come out plate first however they were passed.
    /// </para>
    /// <para>
    /// The frame is the one the views look along. Given none, it is the frame of the main object, the one marked
    /// <see cref="DrawingObject.IsMain"/> with the lowest id; with none marked, the frame of the object with the lowest
    /// id; with no objects at all, the world's. A beam 3 000 long lying along the model's Y axis is then drawn as one
    /// lying along X: its front view shows its length across the sheet whichever way it lies in the model.
    /// </para>
    /// </remarks>
    public sealed class DrawingSource
    {
        private readonly Dictionary<string, DrawingObject> _byId;

        /// <summary>
        /// Makes the source of a drawing from its objects, set in the frame of its main object.
        /// </summary>
        /// <param name="objects">The objects, in any order.</param>
        /// <exception cref="ArgumentNullException">Thrown when the objects are null.</exception>
        /// <exception cref="ArgumentException">Thrown when an object is null or two objects have the same id.</exception>
        public DrawingSource(IEnumerable<DrawingObject> objects)
        {
            DrawingObject[] sorted = Sort(objects, out _byId);
            Objects = new ReadOnlyCollection<DrawingObject>(sorted);
            Frame = MainFrame(sorted);
        }

        /// <summary>
        /// Makes the source of a drawing from its objects, set in a frame given.
        /// </summary>
        /// <param name="objects">The objects, in any order.</param>
        /// <param name="frame">The frame the views are set in: X along the length of what is drawn, Y up in its
        /// cross-section.</param>
        /// <exception cref="ArgumentNullException">Thrown when the objects are null.</exception>
        /// <exception cref="ArgumentException">Thrown when an object is null, two objects have the same id, or the frame
        /// is not a frame of three unit axes square to each other.</exception>
        public DrawingSource(IEnumerable<DrawingObject> objects, GeoCoordinateSystem3 frame)
        {
            if (!frame.IsValid)
            {
                throw new ArgumentException("The frame has to have three unit axes square to each other.", nameof(frame));
            }

            DrawingObject[] sorted = Sort(objects, out _byId);
            Objects = new ReadOnlyCollection<DrawingObject>(sorted);
            Frame = frame;
        }

        /// <summary>Gets the objects, in the order of their ids compared character by character.</summary>
        public IReadOnlyList<DrawingObject> Objects { get; }

        /// <summary>Gets the frame the views of the drawing are set in.</summary>
        public GeoCoordinateSystem3 Frame { get; }

        /// <summary>
        /// Finds the object with an id.
        /// </summary>
        /// <param name="id">The id, compared character by character, case and all.</param>
        /// <param name="found">The object, or null where there is none with the id.</param>
        /// <returns>Whether there is an object with the id.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the id is null.</exception>
        public bool TryGetObject(string id, out DrawingObject found)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            return _byId.TryGetValue(id, out found);
        }

        private static DrawingObject[] Sort(IEnumerable<DrawingObject> objects, out Dictionary<string, DrawingObject> byId)
        {
            if (objects == null)
            {
                throw new ArgumentNullException(nameof(objects));
            }

            byId = new Dictionary<string, DrawingObject>(StringComparer.Ordinal);
            var list = new List<DrawingObject>();

            foreach (DrawingObject item in objects)
            {
                if (item == null)
                {
                    throw new ArgumentException("An object of a source cannot be null.", nameof(objects));
                }

                if (byId.ContainsKey(item.Id))
                {
                    throw new ArgumentException("The id \"" + item.Id + "\" is given to two objects.", nameof(objects));
                }

                byId.Add(item.Id, item);
                list.Add(item);
            }

            // Ids are unique, so the order of their ids is a full order.
            DrawingObject[] sorted = list.ToArray();
            Array.Sort(sorted, (a, b) => string.CompareOrdinal(a.Id, b.Id));
            return sorted;
        }

        private static GeoCoordinateSystem3 MainFrame(DrawingObject[] sorted)
        {
            foreach (DrawingObject item in sorted)
            {
                if (item.IsMain)
                {
                    return item.Frame;
                }
            }

            return sorted.Length > 0 ? sorted[0].Frame : GeoCoordinateSystem3.Global;
        }
    }
}
