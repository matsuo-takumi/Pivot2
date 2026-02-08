using System.Collections.Concurrent;

namespace Pivot.Engine.Core;

public enum JobPriority
{
    High = 0,   // Immediate (e.g., visible on screen)
    Normal = 1, // Pre-fetching nearby
    Low = 2     // Background analysis
}

public abstract class Job
{
    public string Id { get; }
    public abstract Task ExecuteAsync(CancellationToken ct);

    protected Job(string id)
    {
        Id = id;
    }
}

public class JobScheduler : IDisposable
{
    private readonly PriorityQueue<Job, int> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task[] _workers;
    private readonly object _lock = new();

    public JobScheduler(int concurrencyLevel = 4)
    {
        _concurrencySemaphore = new SemaphoreSlim(concurrencyLevel);
        _workers = new Task[concurrencyLevel];
        for (int i = 0; i < concurrencyLevel; i++)
        {
            _workers[i] = Task.Run(WorkerLoop);
        }
    }

    public void Enqueue(Job job, JobPriority priority)
    {
        lock (_lock)
        {
            _queue.Enqueue(job, (int)priority);
        }
        _signal.Release();
    }

    private async Task WorkerLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(_cts.Token);
                
                Job? job;
                lock (_lock)
                {
                    if (!_queue.TryDequeue(out job, out _)) continue;
                }

                await _concurrencySemaphore.WaitAsync(_cts.Token);
                try
                {
                    await job.ExecuteAsync(_cts.Token);
                }
                finally
                {
                    _concurrencySemaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // Log error but continue
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            Task.WaitAll(_workers, 1000);
        }
        catch { }
        _cts.Dispose();
        _signal.Dispose();
        _concurrencySemaphore.Dispose();
    }
}
