using System;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The External References palette (XREF, EXTERNALREFERENCES, IMAGE) and the attach dialogs (XATTACH, IMAGEATTACH), each
    /// shown through a function the workspace sets, so that a test drives them with none. With none set, XATTACH and
    /// IMAGEATTACH ask the file's name on the command line.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>Gets or sets how the workspace shows the External References palette, beside the window and left open.</summary>
        public Action<ExternalReferencesViewModel> ShowExternalReferences { get; set; }

        /// <summary>Gets or sets how the workspace shows the attach dialog; the action it closed with is in its request.</summary>
        public Func<AttachReferenceViewModel, bool> ShowAttachReference { get; set; }

        /// <summary>
        /// Starts a command and answers its prompts with the texts given, each as typed and Enter pressed, an empty one as Enter
        /// alone, while the command runs: the palette's actions run -XREF and -IMAGE so, a name with spaces in it whole.
        /// </summary>
        internal void RunWithAnswers(string command, params string[] answers)
        {
            if (_current == null)
            {
                return;
            }

            CadCommandSession session = _current.Session;
            session.Start(command);

            foreach (string answer in answers)
            {
                if (!session.IsRunning)
                {
                    break;
                }

                if (answer.Length == 0)
                {
                    session.Enter();
                }
                else
                {
                    session.Feed(answer);
                }
            }

            SyncHistory();
            RunAfterCommand();
        }

        /// <summary>A request of the external reference commands: the palette or an attach dialog shown where the workspace shows them.</summary>
        private void OnXrefRequested(CadUiRequestEventArgs e)
        {
            switch (e.Name)
            {
                case "XREF":
                    if (ShowExternalReferences != null && _current != null)
                    {
                        ShowExternalReferences(new ExternalReferencesViewModel(this));
                        e.Handled = true;
                    }

                    break;
                case "XATTACH":
                case "IMAGEATTACH":
                    if (e.Payload is CadAttachRequest request && ShowAttachReference != null)
                    {
                        e.Handled = ShowAttachReference(new AttachReferenceViewModel(request));
                    }

                    break;
            }
        }
    }
}
