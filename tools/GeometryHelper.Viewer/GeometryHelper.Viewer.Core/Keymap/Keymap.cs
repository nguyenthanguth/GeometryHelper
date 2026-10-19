using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// What the viewer can be told to do by name, with the phase of the work it arrives in. The keymap binds gestures to
    /// these names.
    /// </summary>
    public sealed class KeymapAction
    {
        internal KeymapAction(string name, string description, bool implemented)
        {
            Name = name;
            Description = description;
            Implemented = implemented;
        }

        /// <summary>
        /// Gets the name used in <c>keymap.json</c>.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets what the action does.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets whether this version of the viewer does it; a key bound to an action it does not do says so in the status
        /// bar and nothing more.
        /// </summary>
        public bool Implemented { get; }
    }

    /// <summary>
    /// The actions of the viewer.
    /// </summary>
    public static class KeymapActions
    {
        private static readonly KeymapAction[] _all =
        {
            new KeymapAction("ZoomAtCursor", "Zoom in or out about the point under the cursor.", true),
            new KeymapAction("ZoomIn", "Zoom in about the middle of the view.", true),
            new KeymapAction("ZoomOut", "Zoom out about the middle of the view.", true),
            new KeymapAction("Pan", "Move the view with the mouse.", true),
            new KeymapAction("PanLeft", "Move the view left by a tenth of its width.", true),
            new KeymapAction("PanRight", "Move the view right by a tenth of its width.", true),
            new KeymapAction("PanUp", "Move the view up by a tenth of its height.", true),
            new KeymapAction("PanDown", "Move the view down by a tenth of its height.", true),
            new KeymapAction("Rotate", "Turn the view about its rotation centre with the mouse.", true),
            new KeymapAction("RotateLeft", "Turn the view left by 15 degrees.", true),
            new KeymapAction("RotateRight", "Turn the view right by 15 degrees.", true),
            new KeymapAction("RotateUp", "Tilt the view up by 15 degrees.", true),
            new KeymapAction("RotateDown", "Tilt the view down by 15 degrees.", true),
            new KeymapAction("SetRotationCentre", "Pick the point the view turns about.", true),
            new KeymapAction("ClearRotationCentre", "Turn about the middle of the scene again.", true),
            new KeymapAction("TogglePlan", "Switch between the 3D view and the plan.", true),
            new KeymapAction("FitAll", "Fit the whole scene in the view.", true),
            new KeymapAction("FitSelection", "Fit the selected items in the view.", true),
            new KeymapAction("ViewIso", "Look at the scene from a corner above it.", true),
            new KeymapAction("ViewTop", "Look down on the scene, the plan.", true),
            new KeymapAction("ViewFront", "Look at the scene from the front, along +Y.", true),
            new KeymapAction("ViewRight", "Look at the scene from the right, along -X.", true),
            new KeymapAction("RenderWireframe", "Draw lines only.", true),
            new KeymapAction("RenderShaded", "Draw surfaces only.", true),
            new KeymapAction("RenderShadedEdges", "Draw surfaces and their edges.", true),
            new KeymapAction("RenderTransparent", "Draw surfaces see-through, with their edges.", true),
            new KeymapAction("Select", "Select the item under the cursor, replacing the selection.", true),
            new KeymapAction("SelectAdd", "Add the item under the cursor to the selection.", true),
            new KeymapAction("SelectToggle", "Toggle the item under the cursor in the selection.", true),
            new KeymapAction("BoxSelect", "Select by a window dragged left to right, or a crossing dragged right to left.", true),
            new KeymapAction("Cancel", "Clear the selection, and stop a script that is running.", true),
            new KeymapAction("HideSelected", "Hide the selected items.", true),
            new KeymapAction("ShowAll", "Show every item again.", true),
            new KeymapAction("Properties", "Show the properties of the selection.", true),
            new KeymapAction("MeasureDistance", "Measure a distance between two points.", true),
            new KeymapAction("MeasureAngle", "Measure an angle.", true),
            new KeymapAction("MeasureRadius", "Measure a radius.", true),
            new KeymapAction("SnapEnd", "Snap to ends.", true),
            new KeymapAction("SnapMid", "Snap to middles.", true),
            new KeymapAction("SnapIntersection", "Snap to intersections.", true),
            new KeymapAction("SnapCentre", "Snap to centres.", true),
            new KeymapAction("SnapNearest", "Snap to the nearest point.", true),
            new KeymapAction("RunScript", "Run the open script.", true),
            new KeymapAction("RerunScript", "Run the open script again, or read the open scene file again.", true),
            new KeymapAction("Screenshot", "Save the view as a picture.", false),
            new KeymapAction("OpenFile", "Open a script or a scene file.", true),
            new KeymapAction("SaveSceneAs", "Save the scene as a file.", true),
        };

        /// <summary>
        /// Gets every action, in a fixed order.
        /// </summary>
        public static IReadOnlyList<KeymapAction> All => _all;

        /// <summary>
        /// Gets whether a name is an action.
        /// </summary>
        public static bool IsKnown(string name) => _all.Any(action => string.Equals(action.Name, name, StringComparison.Ordinal));
    }

    /// <summary>
    /// The gestures bound to one action.
    /// </summary>
    public sealed class KeyBinding
    {
        internal KeyBinding(string action, IReadOnlyList<Gesture> gestures)
        {
            Action = action;
            Gestures = gestures;
        }

        /// <summary>
        /// Gets the name of the action.
        /// </summary>
        public string Action { get; }

        /// <summary>
        /// Gets the gestures that do it.
        /// </summary>
        public IReadOnlyList<Gesture> Gestures { get; }
    }

    /// <summary>
    /// Which gesture does which action. The viewer ships a default, <c>keymap.json</c>, and reads a copy of it the user keeps
    /// in the application data folder; an action the copy lists takes its gestures from the copy, and the others keep
    /// theirs from the default.
    /// </summary>
    public sealed class Keymap
    {
        private const string ResourceName = "GeometryHelper.Viewer.keymap.json";

        private readonly Dictionary<string, string> _actionOf;

        private Keymap(IReadOnlyList<KeyBinding> bindings)
        {
            Bindings = bindings;
            _actionOf = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (KeyBinding binding in bindings)
            {
                foreach (Gesture gesture in binding.Gestures)
                {
                    _actionOf[gesture.ToString()] = binding.Action;
                }
            }
        }

        /// <summary>
        /// Gets the bindings, in the order of the file.
        /// </summary>
        public IReadOnlyList<KeyBinding> Bindings { get; }

        /// <summary>
        /// Gets the text of the default keymap, as shipped.
        /// </summary>
        public static string DefaultJson()
        {
            using (Stream stream = typeof(Keymap).GetTypeInfo().Assembly.GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("The default keymap is missing from the assembly.");
                }

                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Gets the default keymap.
        /// </summary>
        public static Keymap Default() => Parse(DefaultJson());

        /// <summary>
        /// Reads a keymap from its text.
        /// </summary>
        /// <param name="json">The text: <c>{ "bindings": [ { "action": "Pan", "gestures": ["MiddleDrag"] }, ... ] }</c>.</param>
        /// <returns>The keymap.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        /// <exception cref="FormatException">
        /// Thrown when the text is not JSON of that shape, names an action that does not exist or one twice, holds a gesture
        /// that does not read, or binds one gesture to two actions or twice to one.
        /// </exception>
        public static Keymap Parse(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            JsonDocument document;

            try
            {
                document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            }
            catch (JsonException e)
            {
                throw new FormatException("The keymap is not valid JSON: " + e.Message, e);
            }

            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object
                    || !document.RootElement.TryGetProperty("bindings", out JsonElement list)
                    || list.ValueKind != JsonValueKind.Array)
                {
                    throw new FormatException("A keymap is an object with a \"bindings\" list.");
                }

                var bindings = new List<KeyBinding>();
                var seenActions = new HashSet<string>(StringComparer.Ordinal);
                var owner = new Dictionary<string, string>(StringComparer.Ordinal);

                foreach (JsonElement element in list.EnumerateArray())
                {
                    string action = element.ValueKind == JsonValueKind.Object && element.TryGetProperty("action", out JsonElement name) && name.ValueKind == JsonValueKind.String
                        ? name.GetString()
                        : null;

                    if (action == null)
                    {
                        throw new FormatException("A binding needs an \"action\" naming what it does.");
                    }

                    if (!KeymapActions.IsKnown(action))
                    {
                        throw new FormatException("'" + action + "' is not an action of the viewer.");
                    }

                    if (!seenActions.Add(action))
                    {
                        throw new FormatException("The action '" + action + "' is bound twice; list all its gestures in one binding.");
                    }

                    if (!element.TryGetProperty("gestures", out JsonElement gestureList) || gestureList.ValueKind != JsonValueKind.Array)
                    {
                        throw new FormatException("The binding of '" + action + "' needs a list of \"gestures\".");
                    }

                    var gestures = new List<Gesture>();

                    foreach (JsonElement text in gestureList.EnumerateArray())
                    {
                        if (text.ValueKind != JsonValueKind.String)
                        {
                            throw new FormatException("A gesture of '" + action + "' is not text.");
                        }

                        Gesture gesture = Gesture.Parse(text.GetString());
                        string key = gesture.ToString();

                        if (owner.TryGetValue(key, out string other))
                        {
                            throw new FormatException("The gesture '" + key + "' is bound to " + (other == action ? "'" + action + "' twice." : "both '" + other + "' and '" + action + "'."));
                        }

                        owner[key] = action;
                        gestures.Add(gesture);
                    }

                    bindings.Add(new KeyBinding(action, gestures));
                }

                return new Keymap(bindings);
            }
        }

        /// <summary>
        /// Reads the keymap the user keeps, laid over the default.
        /// </summary>
        /// <param name="userPath">The path of the user's file; it need not exist.</param>
        /// <param name="problem">Why the file was not used, or null.</param>
        /// <returns>
        /// The default keymap with each action the user's file lists taking its gestures from the file, and a gesture the file
        /// gives to one action leaving whichever the default had given it to. The default keymap alone where there is no file
        /// or it does not read.
        /// </returns>
        public static Keymap Load(string userPath, out string problem)
        {
            problem = null;
            Keymap shipped = Default();

            if (userPath == null || !File.Exists(userPath))
            {
                return shipped;
            }

            try
            {
                Keymap user = Parse(File.ReadAllText(userPath, Encoding.UTF8));
                var taken = new HashSet<string>(user.Bindings.SelectMany(b => b.Gestures).Select(g => g.ToString()), StringComparer.Ordinal);
                var listed = new HashSet<string>(user.Bindings.Select(b => b.Action), StringComparer.Ordinal);
                var merged = new List<KeyBinding>(user.Bindings);

                foreach (KeyBinding binding in shipped.Bindings.Where(b => !listed.Contains(b.Action)))
                {
                    merged.Add(new KeyBinding(binding.Action, binding.Gestures.Where(g => !taken.Contains(g.ToString())).ToList()));
                }

                return new Keymap(merged);
            }
            catch (Exception e) when (e is FormatException || e is IOException || e is UnauthorizedAccessException)
            {
                problem = "The keymap " + userPath + " was not used: " + e.Message;
                return shipped;
            }
        }

        /// <summary>
        /// Gets the action a gesture does.
        /// </summary>
        /// <param name="gesture">The gesture.</param>
        /// <returns>The name of the action, or null when nothing is bound to the gesture.</returns>
        public string ActionFor(Gesture gesture) => gesture != null && _actionOf.TryGetValue(gesture.ToString(), out string action) ? action : null;

        /// <summary>
        /// Gets the gestures that do an action.
        /// </summary>
        /// <param name="action">The name of the action.</param>
        /// <returns>The gestures; none for an action with none.</returns>
        public IReadOnlyList<Gesture> GesturesOf(string action)
            => Bindings.Where(b => string.Equals(b.Action, action, StringComparison.Ordinal)).SelectMany(b => b.Gestures).ToList();
    }
}
