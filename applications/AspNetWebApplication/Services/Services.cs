using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

using ArcfaceImageVectorizer;

namespace Services;

public interface IVectorizationTaskManager
{

}

public class ConcurrentQueueWithMeanValue
{
    public float MeanValue { get; private set; }

    public ConcurrentQueueWithMeanValue(int maxQueueSize, long initialMeanValue)
    {
        if (maxQueueSize <= 0)
        {
            throw new Exception("Invalid ConcurrentQueueWithMeanValue size");
        }
        MeanValue = initialMeanValue;
        _CurrentValuesSum = 0;
        _MaxQueueSize = maxQueueSize;
    }

    public void Push(long currentValue)
    {
        lock (_Queue)
        {
            long oldestValue = 0;
            if (_Queue.Count >= _MaxQueueSize)
            {
                _Queue.TryDequeue(out oldestValue);
            }
            _Queue.Enqueue(currentValue);
            _CurrentValuesSum -= oldestValue;
            _CurrentValuesSum += currentValue;
            MeanValue = (float) _CurrentValuesSum / _Queue.Count;
        }
    }

    private ConcurrentQueue<long> _Queue = new();
    private long _CurrentValuesSum;
    private int _MaxQueueSize;
}

public class VectorizationTaskManager : IVectorizationTaskManager
{
    public ImageVectorizer ImageVectorizer { get; } = new();
    public ConcurrentDictionary<string, Task<float[]>> VectorizationTaskByImageId { get; } = new();
    public ConcurrentDictionary<string, CancellationTokenSource> 
        CancellationTokenSourcesByImageId  { get; } = new();
    public ConcurrentDictionary<string, Stopwatch> StartVectorizationTimestamp { get; } = new();
    public ConcurrentQueueWithMeanValue ExecutionTimesQueue { get; } = new(20, 500);
}