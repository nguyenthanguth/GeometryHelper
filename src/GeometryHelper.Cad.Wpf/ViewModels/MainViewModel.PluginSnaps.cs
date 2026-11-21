using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Cad.Plugins;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The object snaps the plugins loaded add (<see cref="CadPluginHost.ObjectSnaps"/>): read again when the plugins change,
    /// on or off by keyword as <see cref="GlobalSettings.PluginSnaps"/> keeps them, given to <see cref="MainViewModel.Snap"/>
    /// with the drawing they are asked about, listed in the OSNAP menu, the Shift+right-click menu and Drafting Settings, and
    /// taken alone for the next point when their keyword is typed.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private IReadOnlyList<PluginSnapInfo> _pluginSnapList = Array.Empty<PluginSnapInfo>();

        /// <summary>
        /// Reads the snaps of every plugin loaded, in the order they were loaded, and gives <see cref="Snap"/> those that are on:
        /// a keyword chosen in <see cref="GlobalSettings.PluginSnaps"/> as it was, else the snap's
        /// <see cref="CadCustomSnap.IsOnByDefault"/>. A snap whose plugin is gone is dropped, and an override of it cleared.
        /// </summary>
        partial void SyncPluginSnaps()
        {
            var snaps = new List<PluginSnapInfo>();

            foreach (CadLoadedPlugin entry in LoadedPlugins())
            {
                foreach (CadCustomSnap snap in entry.Plugin.Host.ObjectSnaps)
                {
                    if (snap != null && !snaps.Exists(i => i.Snap == snap))
                    {
                        snaps.Add(new PluginSnapInfo(snap, entry.Name));
                    }
                }
            }

            _pluginSnapList = snaps;
            Snap.CustomFailed = OnPluginSnapFailed;
            Snap.KeepFailuresOf(snaps.Select(i => i.Snap).ToList());

            if (Snap.CustomOverride != null && !snaps.Exists(i => i.Snap == Snap.CustomOverride))
            {
                Snap.CustomOverride = null;
            }

            GivePluginSnapsToSnap();
            Raise(nameof(HasPluginSnaps));
        }

        /// <summary>Gets whether a plugin loaded adds an object snap: Drafting Settings then shows its "Plugin snaps" group.</summary>
        internal bool HasPluginSnaps => _pluginSnapList.Count > 0;

        /// <summary>Gets the snaps the plugins loaded add, in the order they were loaded.</summary>
        internal IReadOnlyList<PluginSnapInfo> PluginSnapList => _pluginSnapList;

        /// <summary>Gets whether a snap of a plugin is on: the choice kept for its keyword, else its default.</summary>
        internal bool IsPluginSnapOn(CadCustomSnap snap)
        {
            string on = snap.Keyword + "=1";
            string off = snap.Keyword + "=0";

            // The last entry of a keyword counts, as a later write replaces an earlier one.
            for (int i = _settings.PluginSnaps.Count - 1; i >= 0; i--)
            {
                string entry = _settings.PluginSnaps[i]?.Trim();

                if (string.Equals(entry, on, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(entry, off, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return snap.IsOnByDefault;
        }

        /// <summary>
        /// Keeps the choice of snaps of plugins, as OK of Drafting Settings and a row of the OSNAP menu make it: an entry per
        /// keyword in <see cref="GlobalSettings.PluginSnaps"/> ("CTOP=1"), those of snaps not loaded left as they are, and
        /// the snaps on given to <see cref="Snap"/>. Raises a change of <see cref="SnapModes"/> where one differs, so that the
        /// host keeps it in the settings file as it keeps the snaps ticked.
        /// </summary>
        /// <param name="choices">The snaps and whether each is on.</param>
        internal void SetPluginSnaps(IEnumerable<KeyValuePair<CadCustomSnap, bool>> choices)
        {
            bool changed = false;

            foreach (KeyValuePair<CadCustomSnap, bool> choice in choices)
            {
                string keyword = choice.Key.Keyword;
                string entry = keyword + (choice.Value ? "=1" : "=0");
                List<string> kept = _settings.PluginSnaps;

                // A snap chosen on gets another try where its finder had thrown.
                if (choice.Value)
                {
                    Snap.ForgetFailure(choice.Key);
                }

                // A snap left as its default, and never chosen, writes nothing.
                if (
                    choice.Value == choice.Key.IsOnByDefault
                    && !kept.Exists(e =>
                        (e ?? string.Empty).Trim().StartsWith(keyword + "=", StringComparison.OrdinalIgnoreCase)
                    )
                )
                {
                    continue;
                }

                int same = kept.Count(e =>
                    string.Equals((e ?? string.Empty).Trim(), entry, StringComparison.OrdinalIgnoreCase)
                );
                int ofKeyword = kept.Count(e =>
                    (e ?? string.Empty).Trim().StartsWith(keyword + "=", StringComparison.OrdinalIgnoreCase)
                );

                // A keyword already kept as chosen, and once, stays where it is.
                if (same == 1 && ofKeyword == 1)
                {
                    continue;
                }

                kept.RemoveAll(e =>
                    (e ?? string.Empty).Trim().StartsWith(keyword + "=", StringComparison.OrdinalIgnoreCase)
                );
                kept.Add(entry);
                changed = true;
            }

            SyncPluginSnaps();

            if (changed)
            {
                Raise(nameof(SnapModes));
            }
        }

        /// <summary>Gives <see cref="Snap"/> the snaps of plugins that are on, in the order they were loaded.</summary>
        private void GivePluginSnapsToSnap() =>
            Snap.CustomSnaps = _pluginSnapList.Where(i => IsPluginSnapOn(i.Snap)).Select(i => i.Snap).ToList();

        /// <summary>
        /// A finder threw: the snap is off for the session. The command line says which, once, with the first line of the
        /// message, and the log gets a Warn.
        /// </summary>
        private void OnPluginSnapFailed(CadCustomSnap snap, Exception exception)
        {
            PluginSnapInfo info = _pluginSnapList.FirstOrDefault(i => i.Snap == snap);
            string message = (exception?.Message ?? string.Empty).Split('\r', '\n').FirstOrDefault()?.Trim();

            if (string.IsNullOrEmpty(message))
            {
                message = exception?.GetType().Name ?? "unknown error";
            }

            string plugin = info?.PluginName ?? string.Empty;
            GeometryHelperLog.Warn(
                "ObjectSnap: the snap " + snap.Keyword + " of the plugin " + plugin + " threw and is off.",
                exception
            );
            Say("The snap " + snap.Keyword + " of " + plugin + " failed and is off: " + message);
        }

        /// <summary>
        /// Takes a snap of a plugin for the next point alone, as typing its keyword or choosing it in the Shift+right-click
        /// menu does: whether OSNAP is on or off, only it gives the point. The command line says it, "Specify next point:
        /// _ctop of", and the override ends once the prompt has its answer or goes.
        /// </summary>
        /// <param name="snap">The snap.</param>
        /// <returns>Whether it was taken: false where no prompt waits for a point or objects.</returns>
        internal bool OverridePluginSnap(CadCustomSnap snap)
        {
            if (_current == null || !CadPicking.WantsModifiers(_current.Session))
            {
                return false;
            }

            CadCommandSession session = _current.Session;
            string word = "_" + snap.Keyword.ToLowerInvariant();

            // As a built-in snap, " and" takes none: what was chosen is answered as typed text, which that prompt refuses.
            if (session.IsAskingSecondObject)
            {
                session.Feed(word);
                SyncHistory();
                return true;
            }

            Snap.Override = null;
            session.SnapOverride = null;
            // Any change of the prompt ends it (OnPromptChanged), as it ends the override of a built-in snap.
            Snap.CustomOverride = snap;
            session.Write(session.Prompt.Text + " " + word + " of");
            SyncHistory();
            return true;
        }

        /// <summary>Ends the override of a snap of a plugin, where a built-in snap is taken in its place.</summary>
        private void ClearPluginSnapOverride() => Snap.CustomOverride = null;

        /// <summary>The snap a typed word names, in either case with or without a leading "_": the whole keyword; null for none.</summary>
        private PluginSnapInfo PluginSnapTyped(string word)
        {
            string typed = word.Trim().ToUpperInvariant();

            if (typed.StartsWith("_", StringComparison.Ordinal))
            {
                typed = typed.Substring(1);
            }

            return _pluginSnapList.FirstOrDefault(i => i.Snap.Keyword == typed);
        }

        /// <summary>A snap of a plugin loaded: the snap and the name of the plugin that added it.</summary>
        internal sealed class PluginSnapInfo
        {
            public PluginSnapInfo(CadCustomSnap snap, string pluginName)
            {
                Snap = snap;
                PluginName = pluginName;
            }

            public CadCustomSnap Snap { get; }

            public string PluginName { get; }
        }
    }
}
