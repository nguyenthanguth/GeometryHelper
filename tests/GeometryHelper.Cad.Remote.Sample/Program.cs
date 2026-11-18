using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad.Remote.Sample
{
    /// <summary>
    /// A console program that drives a running GeometryHelper CAD: it lists the apps it finds, connects to the one the user
    /// worked in last and runs <see cref="RemoteSample.Run"/>. Start the app first; the program waits for you to click a
    /// point in the drawing.
    /// </summary>
    public static class Program
    {
        /// <summary>Finds the running apps and drives the one activated last.</summary>
        /// <returns>0 when the sample ran to its end, 1 when the pick was cancelled, 2 when no app is running or the app refused.</returns>
        public static int Main()
        {
            IReadOnlyList<CadRunningApp> running = CadApplication.GetRunning();

            if (running.Count == 0)
            {
                Console.Error.WriteLine(
                    "No GeometryHelper CAD is running. Start the app, and make sure that Global settings allow other programs to connect."
                );
                return 2;
            }

            foreach (CadRunningApp app in running)
            {
                Console.WriteLine(app + ": " + string.Join(", ", app.Drawings));
            }

            try
            {
                using (var app = new CadApplication())
                {
                    Console.WriteLine("Click a point in the drawing when the app asks for it.");
                    return RemoteSample.Run(app, Console.Out);
                }
            }
            catch (CadRemoteException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }
        }
    }
}
