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
        /*
        double timeoutThreshold = Thundagun.Config.GetValue(Thundagun.TimeoutThreshold);
        double timeoutCooldown = Thundagun.Config.GetValue(Thundagun.TimeoutCooldown);
        double timeoutWorkInterval = Thundagun.Config.GetValue(Thundagun.TimeoutWorkInterval);

        DateTime now = DateTime.Now;
        double timeSinceLastStateUpdate = (now - Thundagun.lastStateUpdate).TotalMilliseconds;
        bool useBatchProcessing = Thundagun.CurrentSyncMode == Thundagun.SyncMode.Async;

        if (timeSinceLastStateUpdate > timeoutThreshold)
        {
            Thundagun.lastTimeout = now;
            useBatchProcessing = false;
        }
        */

        lock (batchQueue)
        {
            if (batchQueue.Count == 0)
            {
                return;
            }

            var renderingContext = RenderHelper.CurrentRenderingContext;
            RenderHelper.BeginRenderContext(RenderingContext.RenderToAsset);

            double timeElapsed = 0.0;
            DateTime startTime = DateTime.Now;

            while (batchQueue.Count > 0)
            {
                var batch = batchQueue.Peek();

                // is it safe to access the sync mode from here?
                if (!batch.IsComplete && SynchronizationManager.CurrentSyncMode == SyncMode.Async && !SynchronizationManager.Timeout)
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
                    if (timeElapsed > Thundagun.Config.GetValue(Thundagun.TimeoutWorkInterval) && SynchronizationManager.Timeout)
                    {
                        break;
                    }
                }
                timeElapsed = (DateTime.Now - startTime).TotalMilliseconds;
                if (timeElapsed > Thundagun.Config.GetValue(Thundagun.TimeoutWorkInterval) && SynchronizationManager.Timeout)
                {
                    break;
                }
                if (batch.IsComplete && batch.Tasks.Count == 0)
                {
                    batchQueue.Dequeue();
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