using System.Threading.Tasks;
using System.Windows.Threading;

namespace GeometryHelper.Drawing.Wpf.UnitTest
{
    /// <summary>Waits for a task on an STA thread while its dispatcher runs, so that the task's awaits come back to it.</summary>
    internal static class TaskPump
    {
        public static T Pump<T>(this Task<T> task)
        {
            var frame = new DispatcherFrame();
            task.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
            return task.GetAwaiter().GetResult();
        }
    }
}
