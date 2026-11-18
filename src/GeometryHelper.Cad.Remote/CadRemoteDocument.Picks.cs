using System;
using System.Collections.Generic;
using System.Threading;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad.Remote
{
    /// <summary>Asking the user to pick on the canvas, as Tekla's <c>Picker</c> does.</summary>
    public sealed partial class CadRemoteDocument
    {
        /// <summary>
        /// Asks the user for a point and waits for the answer. The drawing is shown, the prompt appears on the app's command
        /// line, and the user clicks on the canvas (with the snaps and the object snaps the drawing has on) or types the
        /// point. The app stays free for the user while the call waits. The point is in world coordinates, its Z nought.
        /// </summary>
        /// <param name="prompt">What the command line asks, such as <c>Pick the corner of the frame</c>; it is followed by a colon.</param>
        /// <param name="basePoint">A point to draw a rubber band from while the user moves the cursor; null for none.</param>
        /// <param name="timeout">How long to wait for the user; null for as long as it takes. The pick is ended in the app when the time is up.</param>
        /// <returns>The point picked.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the prompt is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the time is negative.</exception>
        /// <exception cref="CadRemoteException">
        /// Thrown with <see cref="CadRemoteException.IsCancelled"/> true when the user presses Esc; with it false when the time
        /// is up, a command is running in the drawing, another program is waiting for the user to pick, or the drawing is closed.
        /// </exception>
        public GeoPoint3 PickPoint(string prompt, GeoPoint3? basePoint = null, TimeSpan? timeout = null)
        {
            JsonObject answer = Pick("point", prompt, timeout, request =>
            {
                if (basePoint.HasValue)
                {
                    request.Set(
                        "base",
                        new JsonObject()
                            .Set("x", basePoint.Value.X)
                            .Set("y", basePoint.Value.Y)
                            .Set("z", basePoint.Value.Z)
                    );
                }
            });
            return new GeoPoint3(answer.GetDouble("x"), answer.GetDouble("y"), answer.GetDouble("z"));
        }

        /// <summary>
        /// Asks the user for objects and waits for the answer, as a command asks "Select objects:": the user clicks them or
        /// drags a window and presses Enter. Objects the user had selected before the call are taken at once, as the app's
        /// commands take them. The objects are copies as <see cref="GetObjects"/> gives them, so <see cref="HandleOf"/> and
        /// <see cref="Modify"/> work on them.
        /// </summary>
        /// <param name="prompt">What the command line asks, such as <c>Select the bars to dimension</c>.</param>
        /// <param name="timeout">How long to wait for the user; null for as long as it takes.</param>
        /// <returns>The objects picked, in the order of their handles; none where the user pressed Enter with none picked.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the prompt is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the time is negative.</exception>
        /// <exception cref="CadRemoteException">Thrown as <see cref="PickPoint"/> throws.</exception>
        public IReadOnlyList<DxfEntity> PickObjects(string prompt, TimeSpan? timeout = null) =>
            Objects(Pick("objects", prompt, timeout, null));

        /// <summary>
        /// Asks the user for one object and waits for the answer: one click ends it. If exactly one object was selected before
        /// the call, it is taken at once.
        /// </summary>
        /// <param name="prompt">What the command line asks, such as <c>Select the plate</c>.</param>
        /// <param name="timeout">How long to wait for the user; null for as long as it takes.</param>
        /// <returns>A copy of the object, as <see cref="GetObject"/> gives it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the prompt is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the time is negative.</exception>
        /// <exception cref="CadRemoteException">Thrown as <see cref="PickPoint"/> throws.</exception>
        public DxfEntity PickObject(string prompt, TimeSpan? timeout = null)
        {
            IReadOnlyList<DxfEntity> found = Objects(Pick("object", prompt, timeout, null));

            if (found.Count == 0)
            {
                throw new CadRemoteException("The object picked could not be read from the drawing.");
            }

            return found[0];
        }

        private JsonObject Pick(string kind, string prompt, TimeSpan? timeout, Action<JsonObject> fill)
        {
            if (prompt == null)
            {
                throw new ArgumentNullException(nameof(prompt));
            }

            if (timeout.HasValue && timeout.Value != Timeout.InfiniteTimeSpan && timeout.Value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "The time to wait cannot be negative.");
            }

            // A negative number of milliseconds is no limit.
            long milliseconds =
                !timeout.HasValue || timeout.Value == Timeout.InfiniteTimeSpan
                    ? -1
                    : (long)timeout.Value.TotalMilliseconds;
            return Ask(RemoteOps.Pick, request =>
            {
                request.Set("kind", kind).Set("prompt", prompt).Set("timeout", milliseconds);
                fill?.Invoke(request);
            });
        }
    }
}
