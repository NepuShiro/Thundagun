using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FrooxEngine;
using UnityEngine;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors;

public class RenderQueueProcessor : MonoBehaviour
{
    public RenderConnector Connector;

    private Queue<Batch> batchQueue = new(); 

    public void MarkAsCompleted()
    {
        lock (batchQueue)
        {
            if (batchQueue.Count > 0)
            {
                batchQueue.Peek().MarkComplete();
            }
        }
    }

    public Task<byte[]> Enqueue(FrooxEngine.RenderSettings settings)
    {
        var task = new TaskCompletionSource<byte[]>();
        var renderTask = new RenderTask(settings, task);

        lock (batchQueue)
        {
            if (batchQueue.Count == 0 || batchQueue.Peek().IsComplete)
            {
                var newBatch = new Batch();
                batchQueue.Enqueue(newBatch);
            }
            batchQueue.Peek().AddTask(renderTask);
        }

        return task.Task;
    }

    private void LateUpdate()
    {
        bool useBatchProcessing = Thundagun.CurrentSyncMode == Thundagun.SyncMode.Async;
        bool departEarly = Thundagun.CurrentSyncMode == Thundagun.SyncMode.Desync;
        TimeSpan timeOffset = (DateTime.Now - Thundagun.unityStartTime) - TimeSpan.FromMilliseconds(Thundagun.timeBudget);
        Thundagun.timeBudget =  Thundagun.timeBudget - timeOffset.TotalMilliseconds;
        DateTime departureTime = DateTime.Now + TimeSpan.FromMilliseconds(Thundagun.timeBudget);
        lock (batchQueue)
        {
            if (batchQueue.Count == 0)
            {
                return;
            }

            var renderingContext = RenderHelper.CurrentRenderingContext;
            RenderHelper.BeginRenderContext(RenderingContext.RenderToAsset);

            if (useBatchProcessing)
            {
                while (batchQueue.Count > 0 && batchQueue.Peek().IsComplete)
                {
                    var completedBatch = batchQueue.Dequeue();
                    foreach (var renderTask in completedBatch.Tasks)
                    {
                        try
                        {
                            renderTask.task.SetResult(Connector.RenderImmediate(renderTask.settings));
                        }
                        catch (Exception ex)
                        {
                            renderTask.task.SetException(ex);
                        }
                    }
                }
            }
            else
            {
                while (batchQueue.Count > 0)
                {
                    var batch = batchQueue.Peek();
                    while (batch.Tasks.Count > 0)
                    {
                        if (departEarly && (DateTime.Now > departureTime))
                        {
                            break;
                        }
                        var renderTask = batch.Tasks.Dequeue();
                        try
                        {
                            renderTask.task.SetResult(Connector.RenderImmediate(renderTask.settings));
                        }
                        catch (Exception ex)
                        {
                            renderTask.task.SetException(ex);
                        }
                    }
                    // This is added to avoid accidentally dequeuing the batch if we are departing early and left tasks behind
                    // In the rare chance that the batch was actually fully processed, it'll be dequeued the next frame
                    if (departEarly && (DateTime.Now > departureTime))
                    {
                        break;
                    }
                    if (batch.IsComplete)
                    {
                        batchQueue.Dequeue(); 
                    }
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
    public bool IsComplete { get; private set; } = false;

    public void AddTask(RenderTask task)
    {
        Tasks.Enqueue(task);
    }

    public void MarkComplete()
    {
        IsComplete = true;
    }
}