using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using GeometryHelper.Viewer.Builds;
using Microsoft.Win32;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// What a comparison is to run: the script, the two builds of the library, and how long a script may take.
    /// </summary>
    internal sealed class CompareSetup
    {
        internal string Script { get; set; }

        internal BuildSource Left { get; set; }

        internal BuildSource Right { get; set; }

        internal string Repository { get; set; }

        internal int TimeoutSeconds { get; set; }

        internal bool Watch { get; set; }
    }

    /// <summary>
    /// The window that asks for a <see cref="CompareSetup"/>: the script, and for each side the viewer's own library, a DLL, or a commit.
    /// </summary>
    internal sealed class CompareDialog : Window
    {
        private readonly TextBox _script = new TextBox { MinWidth = 420 };
        private readonly TextBox _repository = new TextBox { MinWidth = 420 };
        private readonly TextBox _timeout = new TextBox { Width = 60 };
        private readonly CheckBox _watch = new CheckBox { Content = "Run a side again when its DLL changes on disk", Margin = new Thickness(0, 6, 0, 0) };
        private readonly SideBox _left = new SideBox("Left");
        private readonly SideBox _right = new SideBox("Right");
        private readonly TextBlock _problem = new TextBlock { Foreground = System.Windows.Media.Brushes.Firebrick, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };

        internal CompareDialog(Window owner, CompareSetup start)
        {
            Owner = owner;
            Title = "Compare two builds of the library";
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;

            var panel = new StackPanel { Margin = new Thickness(12), Background = SystemColors.ControlBrush };
            panel.Children.Add(Row("Script", _script, Browse("...", () => PickFile(_script, "Scripts (*.csx)|*.csx"))));
            panel.Children.Add(_left.Panel);
            panel.Children.Add(_right.Panel);
            panel.Children.Add(Row("Repository (for commits)", _repository, Browse("...", PickFolder)));
            panel.Children.Add(Row("Seconds a script may run", _timeout));
            panel.Children.Add(_watch);
            panel.Children.Add(_problem);

            var ok = new Button { Content = "Compare", IsDefault = true, Padding = new Thickness(18, 3, 18, 3), Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(18, 3, 18, 3) };
            ok.Click += (s, e) => Accept();
            panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0), Children = { ok, cancel } });
            Content = panel;

            _script.Text = start.Script ?? string.Empty;
            _left.Set(start.Left);
            _right.Set(start.Right);
            _repository.Text = start.Repository ?? string.Empty;
            _timeout.Text = start.TimeoutSeconds.ToString(CultureInfo.InvariantCulture);
            _watch.IsChecked = start.Watch;
        }

        internal CompareSetup Result { get; private set; }

        private static StackPanel Row(string label, params UIElement[] controls)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            row.Children.Add(new TextBlock { Text = label, Width = 170, VerticalAlignment = VerticalAlignment.Center });

            foreach (UIElement control in controls)
            {
                if (control is FrameworkElement element)
                {
                    element.Margin = new Thickness(0, 0, 4, 0);
                }

                row.Children.Add(control);
            }

            return row;
        }

        private static Button Browse(string text, Action click)
        {
            var button = new Button { Content = text, Padding = new Thickness(8, 1, 8, 1) };
            button.Click += (s, e) => click();
            return button;
        }

        private static void PickFile(TextBox into, string filter)
        {
            var dialog = new OpenFileDialog { Filter = filter + "|All files (*.*)|*.*" };

            if (dialog.ShowDialog() == true)
            {
                into.Text = dialog.FileName;
            }
        }

        private void PickFolder()
        {
            var dialog = new OpenFolderDialog { Title = "The folder of the repository" };

            if (dialog.ShowDialog(this) == true)
            {
                _repository.Text = dialog.FolderName;
            }
        }

        private void Accept()
        {
            _problem.Text = string.Empty;

            if (!File.Exists(_script.Text.Trim()))
            {
                _problem.Text = "The script does not exist: " + _script.Text;
                return;
            }

            if (!int.TryParse(_timeout.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds) || seconds < 1)
            {
                _problem.Text = "The seconds a script may run must be a whole number of at least 1.";
                return;
            }

            string repository = _repository.Text.Trim();

            if (!_left.TryRead(repository, out BuildSource left, out string problem) || !_right.TryRead(repository, out BuildSource right, out problem))
            {
                _problem.Text = problem;
                return;
            }

            Result = new CompareSetup
            {
                Script = Path.GetFullPath(_script.Text.Trim()),
                Left = left,
                Right = right,
                Repository = repository,
                TimeoutSeconds = seconds,
                Watch = _watch.IsChecked == true,
            };
            DialogResult = true;
        }

        /// <summary>
        /// The controls of one side: which kind of source, and its text.
        /// </summary>
        private sealed class SideBox
        {
            private readonly ComboBox _kind = new ComboBox { Width = 150 };
            private readonly TextBox _value = new TextBox { MinWidth = 420 };

            internal SideBox(string name)
            {
                _kind.Items.Add("Own library");
                _kind.Items.Add("DLL file");
                _kind.Items.Add("Git commit");
                _kind.SelectedIndex = 0;
                _value.IsEnabled = false;
                _kind.SelectionChanged += (s, e) => _value.IsEnabled = _kind.SelectedIndex != 0;
                var browse = Browse("...", () =>
                {
                    if (_kind.SelectedIndex == 1)
                    {
                        PickFile(_value, "GeometryHelper.dll|*.dll");
                    }
                });
                var row = Row(name, _kind, _value, browse);
                Panel = row;
            }

            internal StackPanel Panel { get; }

            internal void Set(BuildSource source)
            {
                switch (source?.Kind ?? BuildSourceKind.Own)
                {
                    case BuildSourceKind.Dll:
                        _kind.SelectedIndex = 1;
                        _value.Text = source.Dll;
                        break;

                    case BuildSourceKind.Commit:
                        _kind.SelectedIndex = 2;
                        _value.Text = source.Commit;
                        break;

                    default:
                        _kind.SelectedIndex = 0;
                        _value.Text = string.Empty;
                        break;
                }
            }

            internal bool TryRead(string repository, out BuildSource source, out string problem)
            {
                source = null;
                problem = null;
                string text = _value.Text.Trim();

                switch (_kind.SelectedIndex)
                {
                    case 0:
                        source = BuildSource.Own;
                        return true;

                    case 1:
                        if (!File.Exists(text))
                        {
                            problem = "There is no DLL at '" + text + "'.";
                            return false;
                        }

                        source = BuildSource.FromDll(text);
                        return true;

                    default:
                        if (text.Length == 0)
                        {
                            problem = "Write the commit: a hash, a tag or a branch.";
                            return false;
                        }

                        if (!Directory.Exists(repository))
                        {
                            problem = "A commit needs the folder of its repository.";
                            return false;
                        }

                        source = BuildSource.FromCommit(text, repository);
                        return true;
                }
            }
        }
    }
}
