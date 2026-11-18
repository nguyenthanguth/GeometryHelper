using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GeometryHelper.Cad.Remote;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>Waiting for the user to pick a point or objects in a drawing, on the drawing's own command line.</summary>
    public sealed partial class CadRemoteServer
    {
        /// <summary>One pick in progress: the command that asks it and what the user answered.</summary>
        private sealed class PickRun
        {
            public CadCommand Command;

            // Set when the command's body is left, however it ends.
            public volatile bool Ended;

            // Set when the user gave an answer, as opposed to ending the command with Esc or Enter.
            public bool Answered;
            public GeoPoint2 Point;
            public IReadOnlyList<string> Handles = Array.Empty<string>();
        }

        /// <summary>
        /// Asks the user for a point, objects or an object, and waits. It starts a command of its own on the drawing's command
        /// line, which asks the prompt as any command does (so the user's snaps, window selection, pre-selected objects and Esc
        /// work as in AutoCAD), then looks every <see cref="CommandPoll"/> milliseconds whether it has ended, each look its own
        /// dispatch: the thread of the drawings is free for the user between them. The request holds the server-wide lock
        /// while it waits, and <see cref="_picking"/> tells the next client that the app is busy rather than letting it wait
        /// for as long as the user likes.
        /// </summary>
        private JsonObject RunPick(JsonObject request)
        {
            string kind = request.GetString("kind");
            long timeout = request.GetLong("timeout", -1);
            JsonObject at = request.GetObject("base");
            GeoPoint2? basePoint = at == null ? (GeoPoint2?)null : new GeoPoint2(at.GetDouble("x"), at.GetDouble("y"));
            var run = new PickRun();
            CadCommandSession session = null;
            CadDocument document = null;
            string prompt = request.GetString("prompt");

            if (kind != "point" && kind != "objects" && kind != "object")
            {
                throw new Refusal("GeometryHelper CAD does not know the pick " + kind + ": it is of an older version.");
            }

            _picking = true;

            try
            {
                Dispatch(() =>
                {
                    document = Find(request);

                    if (!ReferenceEquals(document, _host.ActiveDocument))
                    {
                        _host.Activate(document);
                    }

                    session = _host.SessionOf(document);

                    if (document.IsInGroup || session.IsRunning)
                    {
                        throw new Refusal(
                            "The drawing "
                                + document.Title
                                + " is busy with the command "
                                + (session.Current?.Name ?? "running in it")
                                + ": end it, then ask for the pick again."
                        );
                    }

                    run.Command = PickCommand(run, kind, prompt, basePoint);
                    session.Begin(run.Command);
                    return 0;
                });

                DateTime end = timeout < 0 ? DateTime.MaxValue : DateTime.UtcNow.AddMilliseconds(timeout);

                while (Dispatch(() => Waiting(session, run)) && DateTime.UtcNow < end && !_stopping)
                {
                    Thread.Sleep(CommandPoll);
                }

                bool abandoned = false;
                JsonObject answer = Dispatch(() =>
                {
                    // Still asking: the time is up, or the app is closing. Esc ends the command with what it made, nothing.
                    if (Waiting(session, run))
                    {
                        abandoned = true;
                        session.Escape();
                        return null;
                    }

                    return run.Answered ? PickAnswer(document, kind, run) : null;
                });

                if (answer != null)
                {
                    return answer;
                }

                throw abandoned
                    ? new Refusal(
                        _stopping
                            ? "GeometryHelper CAD is shutting down."
                            : "The user did not pick in time; the pick was ended in the drawing " + document.Title + "."
                    )
                    : new Refusal("The user cancelled the pick.", true);
            }
            finally
            {
                _picking = false;
            }
        }

        private static bool Waiting(CadCommandSession session, PickRun run) =>
            !run.Ended && ReferenceEquals(session.Current, run.Command);

        private static CadCommand PickCommand(PickRun run, string kind, string prompt, GeoPoint2? basePoint) =>
            new CadCommand(
                "REMOTEPICK",
                c => Ending(run, Asking(c, run, kind, prompt, basePoint)),
                usesSelection: kind != "point"
            );

        // The body of a pick, with the mark that it ended set whichever way the command line leaves it.
        private static IEnumerable<CadPrompt> Ending(PickRun run, IEnumerable<CadPrompt> body)
        {
            try
            {
                foreach (CadPrompt prompt in body)
                {
                    yield return prompt;
                }
            }
            finally
            {
                run.Ended = true;
            }
        }

        private static IEnumerable<CadPrompt> Asking(
            CadCommandContext c,
            PickRun run,
            string kind,
            string prompt,
            GeoPoint2? basePoint
        )
        {
            if (kind == "point")
            {
                yield return CadPrompt.ForPoint(prompt, basePoint);

                if (c.Input.IsPoint)
                {
                    run.Point = c.Input.Point;
                    run.Answered = true;
                }
            }
            else if (kind == "objects")
            {
                // Objects on a locked layer are the user's pick all the same: the client reads them, it does not edit them.
                foreach (CadPrompt asked in c.SelectFirst(CadPrompt.ForSelection(prompt)))
                {
                    yield return asked;
                }

                run.Handles = c.Input.Handles;
                run.Answered = true;
            }
            else
            {
                foreach (CadPrompt asked in c.SelectOneFirst(CadPrompt.ForEntity(prompt)))
                {
                    yield return asked;
                }

                if (c.Input.Kind == CadInputKind.Selection && c.Input.Handles.Count > 0)
                {
                    run.Handles = c.Input.Handles;
                    run.Answered = true;
                }
            }
        }

        private static JsonObject PickAnswer(CadDocument document, string kind, PickRun run)
        {
            if (kind == "point")
            {
                return new JsonObject().Set("x", run.Point.X).Set("y", run.Point.Y).Set("z", 0.0);
            }

            var entities = new List<DxfEntity>();

            foreach (string handle in run.Handles.OrderBy(HandleNumber).ThenBy(h => h, StringComparer.Ordinal))
            {
                DxfEntity entity = document.FindEntity(handle);

                if (entity != null && !entity.IsErased)
                {
                    entities.Add(entity);
                }
            }

            return Pack(document, entities);
        }
    }
}
