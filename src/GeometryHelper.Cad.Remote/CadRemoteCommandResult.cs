using System.Collections.Generic;

namespace GeometryHelper.Cad.Remote
{
    /// <summary>What <see cref="CadRemoteDocument.SendCommand(string)"/> got back: whether the command ended, and what the command line wrote.</summary>
    public sealed class CadRemoteCommandResult
    {
        internal CadRemoteCommandResult(bool completed, IReadOnlyList<string> output)
        {
            Completed = completed;
            Output = output;
        }

        /// <summary>
        /// Gets whether no command was left running when the call returned. False when the time given ran out with a command
        /// still waiting, a LINE waiting for its next point: it goes on in the app, and its prompt is in <see cref="Output"/>.
        /// </summary>
        public bool Completed { get; }

        /// <summary>Gets the lines the command line wrote meanwhile: the prompts, the answers typed and the messages, in order.</summary>
        public IReadOnlyList<string> Output { get; }

        /// <inheritdoc/>
        public override string ToString() =>
            (Completed ? "Completed" : "Still running")
            + " ("
            + Output.Count
            + (Output.Count == 1 ? " line)" : " lines)");
    }
}
