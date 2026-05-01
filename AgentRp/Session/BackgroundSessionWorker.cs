using System.Threading.Channels;

namespace AgentRp.Session;

public sealed class BackgroundSessionWorker : IAsyncDisposable
{
    readonly Channel<Func<CancellationToken, Task>> _queue = Channel.CreateUnbounded<Func<CancellationToken, Task>>();
    readonly CancellationTokenSource _cts = new();
    readonly Task _runner;

    public BackgroundSessionWorker()
    {
        _runner = Task.Run(RunAsync);
    }

    public Exception? LastError { get; private set; }
    public event Func<Task>? Failed;

    public void Enqueue(Func<CancellationToken, Task> work)
    {
        _queue.Writer.TryWrite(work);
    }

    async Task RunAsync()
    {
        await foreach (var work in _queue.Reader.ReadAllAsync(_cts.Token))
        {
            try
            {
                await work(_cts.Token);
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                LastError = ex;
                var failed = Failed;
                if (failed is not null)
                    await failed.Invoke();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _queue.Writer.TryComplete();
        try
        {
            await _runner;
        }
        catch (OperationCanceledException)
        {
        }

        _cts.Dispose();
    }
}
