using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using GeometryHelper.Dxf.Viewer.Plugins;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>A button of the Plugins tab: one command of a plugin loaded, which it starts on the command line.</summary>
    public sealed class PluginCommandButton
    {
        internal PluginCommandButton(CadPluginCommand command, string plugin)
        {
            Name = command.Name;
            Alias = command.Alias;
            Description = command.Description ?? string.Empty;
            Plugin = plugin;
            Label = Description.Length > 0 && Description.Length <= 24 ? Description : command.Name;
            ToolTip =
                command.Name
                + (string.IsNullOrEmpty(Alias) ? string.Empty : " (" + Alias + ")")
                + (Description.Length == 0 ? string.Empty : ": " + Description)
                + " · "
                + plugin;
        }

        /// <summary>Gets the command's name, as NUMCIRCLES.</summary>
        public string Name { get; }

        /// <summary>Gets its alias, as NC; null or empty for none.</summary>
        public string Alias { get; }

        /// <summary>Gets what it does, in the plugin's words.</summary>
        public string Description { get; }

        /// <summary>Gets the name of its plugin.</summary>
        public string Plugin { get; }

        /// <summary>Gets the label under the button: the description where it is short, else the command's name.</summary>
        public string Label { get; }

        /// <summary>Gets the tooltip: name, alias, description and plugin.</summary>
        public string ToolTip { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// The Plugins ribbon tab, as the design draws it: the group Command plugins, a button per command of each plugin loaded,
    /// which starts the command as typing its name does; and the group Manage: Load .dll, Reload, Unload, Plugin manager.
    /// Reload and Unload act on the one plugin loaded, and open the Plugin manager to choose where there are several.
    /// </summary>
    public sealed class PluginRibbonViewModel : ObservableObject
    {
        private readonly MainViewModel _model;
        private IReadOnlyList<PluginCommandButton> _commands = Array.Empty<PluginCommandButton>();

        internal PluginRibbonViewModel(MainViewModel model)
        {
            _model = model;
            RunCommand = new DelegateCommand(
                p => _model.StartCommand((p as PluginCommandButton)?.Name),
                _ => _model.HasDocument
            );
            LoadCommand = new DelegateCommand(_ => _model.RequestPluginLoad(), _ => HasManager);
            ReloadCommand = new DelegateCommand(
                _ => OnOne(p => _model.ReloadPlugin(p.Name)),
                _ => HasManager && _model.Plugins.Plugins.Count > 0
            );
            UnloadCommand = new DelegateCommand(
                _ => OnOne(p => _model.UnloadPlugin(p.Name)),
                _ => HasManager && _model.Plugins.Plugins.Any(p => p.IsLoaded)
            );
            ManagerCommand = new DelegateCommand(_ => _model.RequestPluginManager(), _ => HasManager);
            _model.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.HasDocument))
                {
                    ((DelegateCommand)RunCommand).RaiseCanExecuteChanged();
                }
            };
        }

        /// <summary>Gets the buttons of the commands of the plugins loaded, plugin by plugin, each plugin's in the order it declares them.</summary>
        public IReadOnlyList<PluginCommandButton> Commands
        {
            get => _commands;
            private set
            {
                if (Set(ref _commands, value))
                {
                    Raise(nameof(HasCommands));
                }
            }
        }

        /// <summary>Gets whether a plugin loaded has a command.</summary>
        public bool HasCommands => _commands.Count > 0;

        /// <summary>Gets whether the window has a plugin manager, so that Manage works.</summary>
        public bool HasManager => _model.Plugins != null;

        /// <summary>Gets the command of a plugin's button, given the button: the command started on the command line.</summary>
        public ICommand RunCommand { get; }

        /// <summary>Gets the command of Load .dll.</summary>
        public ICommand LoadCommand { get; }

        /// <summary>Gets the command of Reload.</summary>
        public ICommand ReloadCommand { get; }

        /// <summary>Gets the command of Unload.</summary>
        public ICommand UnloadCommand { get; }

        /// <summary>Gets the command of Plugin manager.</summary>
        public ICommand ManagerCommand { get; }

        /// <summary>Reads the plugins loaded again.</summary>
        internal void Refresh()
        {
            var buttons = new List<PluginCommandButton>();

            if (_model.Plugins != null)
            {
                foreach (CadLoadedPlugin plugin in _model.Plugins.Plugins.Where(p => p.IsLoaded && p.Plugin.IsEnabled))
                {
                    buttons.AddRange(plugin.Plugin.Commands.Select(c => new PluginCommandButton(c, plugin.Name)));
                }
            }

            Commands = buttons;
            Raise(nameof(HasManager));

            foreach (
                ICommand command in new[] { RunCommand, LoadCommand, ReloadCommand, UnloadCommand, ManagerCommand }
            )
            {
                ((DelegateCommand)command).RaiseCanExecuteChanged();
            }
        }

        /// <summary>Acts on the one plugin there is to act on, loaded first; with several, opens the Plugin manager to choose.</summary>
        private void OnOne(Action<CadLoadedPlugin> act)
        {
            List<CadLoadedPlugin> loaded = _model.Plugins.Plugins.Where(p => p.IsLoaded).ToList();
            List<CadLoadedPlugin> all = _model.Plugins.Plugins.ToList();
            CadLoadedPlugin one =
                loaded.Count == 1 ? loaded[0]
                : loaded.Count == 0 && all.Count == 1 ? all[0]
                : null;

            if (one != null)
            {
                act(one);
            }
            else
            {
                _model.RequestPluginManager();
            }
        }
    }
}
