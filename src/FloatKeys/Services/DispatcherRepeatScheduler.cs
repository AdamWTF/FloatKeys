using System.Windows.Threading;
using FloatKeys.Input;

namespace FloatKeys.Services;

internal sealed class DispatcherRepeatScheduler : IRepeatScheduler
{
    public IDisposable Schedule(TimeSpan delay, Action callback) => new ScheduledTick(delay, callback);
    private sealed class ScheduledTick : IDisposable
    {
        private readonly DispatcherTimer timer;
        private Action? callback;
        internal ScheduledTick(TimeSpan delay, Action callback)
        {
            this.callback = callback;
            timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = delay };
            timer.Tick += Tick;
            timer.Start();
        }
        private void Tick(object? sender, EventArgs e)
        {
            var action = callback;
            Dispose();
            action?.Invoke();
        }
        public void Dispose()
        {
            callback = null;
            timer.Stop();
            timer.Tick -= Tick;
        }
    }
}
