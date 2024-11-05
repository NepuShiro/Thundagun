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

    // Normally there isn't a constructor here
    public RenderQueueProcessor()
    {
        Thundagun.MarkAsCompletedAction = MarkAsCompleted;
    }

    public void MarkAsCompleted()
    {
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

    private int GetLastBatchTaskCount()
    {
        if (_batchQueue.Count == 0)
            return 0;
        return _batchQueue.Last().Tasks.Count;
    }

    private void LateUpdate()
    {
        Thundagun.Msg($"Backmost batch task count: {GetLastBatchTaskCount()}");
        lock (_batchQueue)
        {
            if (_batchQueue.Count == 0)
            {
                return;
            }

            var renderingContext = RenderHelper.CurrentRenderingContext;
            RenderHelper.BeginRenderContext(RenderingContext.RenderToAsset);

            DateTime startTime = DateTime.Now;
            double timeElapsed;

            while (_batchQueue.Count > 0)
            {
                var batch = _batchQueue.Peek();

                if (!batch.IsComplete && SynchronizationManager.CurrentSyncMode == SyncMode.Async)
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
                    timeElapsed = (DateTime.Now - startTime).TotalMilliseconds;
                    if (timeElapsed > Thundagun.Config.GetValue(Thundagun.DesyncWorkInterval))
                    {
                        break;
                    }
                }
                timeElapsed = (DateTime.Now - startTime).TotalMilliseconds;
                if (timeElapsed > Thundagun.Config.GetValue(Thundagun.DesyncWorkInterval))
                {
                    break;
                }
                if (batch.IsComplete && batch.Tasks.Count == 0)
                {
                    _batchQueue.Dequeue();
                }
            }

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