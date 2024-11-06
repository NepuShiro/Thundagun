using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FrooxEngine;
using UnityEngine;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors;

public class RenderQueueProcessor : MonoBehaviour
{
    public RenderConnector Connector;

    private Queue<Batch> _batchQueue = new();
    private TimeSpan LastWorkInterval = TimeSpan.FromMilliseconds(10);

    // Normally there isn't a constructor here
    public RenderQueueProcessor()
    {
        Thundagun.MarkAsCompletedAction = MarkAsCompleted;
    }

    public void MarkAsCompleted()
    {
        if (_batchQueue.Count != 0)
            _batchQueue.Last().IsComplete = true;
    }

    public Task<byte[]> Enqueue(FrooxEngine.RenderSettings settings)
    {
        var task = new TaskCompletionSource<byte[]>();
        var renderTask = new RenderTask(settings, task);

        lock (_batchQueue)
        {
            if (_batchQueue.Count == 0 || _batchQueue.Last().IsComplete)
            {
                var newBatch = new Batch();
                _batchQueue.Enqueue(newBatch);
            }
            _batchQueue.Last().Tasks.Enqueue(renderTask);
        }

        return task.Task;
    }

    private void LateUpdate()
    {
        lock (_batchQueue)
        {
            if (_batchQueue.Count == 0)
            {
                return;
            }

            var renderingContext = RenderHelper.CurrentRenderingContext;
            RenderHelper.BeginRenderContext(RenderingContext.RenderToAsset);

            DateTime startTime = DateTime.Now;
            TimeSpan timeElapsed;

            TimeSpan unityLastNonWorkInterval = SynchronizationManager.UnityLastUpdateInterval - LastWorkInterval;
            TimeSpan unityAllowedWorkInterval = TimeSpan.FromMilliseconds(Thundagun.Config.GetValue(Thundagun.MaxUpdateInterval)) - unityLastNonWorkInterval;

            while (_batchQueue.Count > 0)
            {
                var batch = _batchQueue.Peek();

                // We might not actually need batching anymore, but I'll leave it in for frame consistency?
                if (!batch.IsComplete)
                {
                    return;
                }

                while (batch.Tasks.Count > 0)
                {
                    var renderTask = batch.Tasks.Dequeue();
                    try
                    {
                        renderTask.task.SetResult(Connector.RenderImmediate(renderTask.settings));
                    }
                    catch (Exception ex)
                    {
                        renderTask.task.SetException(ex);
                    }
                    timeElapsed = (DateTime.Now - startTime);
                    if (timeElapsed > unityAllowedWorkInterval)
                    {
                        break;
                    }
                }
                timeElapsed = (DateTime.Now - startTime);
                if (timeElapsed > unityAllowedWorkInterval)
                {
                    break;
                }
                if (batch.IsComplete && batch.Tasks.Count == 0)
                {
                    _batchQueue.Dequeue();
                }
            }

            timeElapsed = (DateTime.Now - startTime);
            LastWorkInterval = timeElapsed;

            if (renderingContext.HasValue)
            {
                RenderHelper.BeginRenderContext(renderingContext.Value);
            }
        }
    }
}

public class Batch
{
    public Queue<RenderTask> Tasks { get; private set; } = new();
    public bool IsComplete { get; set; } = false;
}