using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FrooxEngine;
using Leap;
using UnityEngine;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors;

public class RenderQueueProcessor : MonoBehaviour // compare
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

    public Task<byte[]> Enqueue(FrooxEngine.RenderSettings settings) // compare
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
        // reused config values
        double timeoutThreshold = Thundagun.Config.GetValue(Thundagun.TimeoutThreshold);
        double timeoutCooldown = Thundagun.Config.GetValue(Thundagun.TimeoutCooldown);
        double timeoutWorkInterval = Thundagun.Config.GetValue(Thundagun.TimeoutWorkInterval);

        // current timestamp
        DateTime now = DateTime.Now;
        // checks if we're in a batch holding pattern
        double timeSinceLastStateUpdate = (now - Thundagun.lastStateUpdate).TotalMilliseconds;
        // we can only know the batch flag from the ema
        bool useBatchProcessing = Thundagun.CurrentSyncMode == Thundagun.SyncMode.Async;

        // if we've been skipping for too long
        if (timeSinceLastStateUpdate > timeoutThreshold)
        {
            // enter a timeout
            Thundagun.lastTimeout = now;
            // ensure we stop batching this iteration to escape the holding pattern early
            useBatchProcessing = false;
        }

        double timeElapsed;

        lock (batchQueue)
        {
            if (batchQueue.Count == 0)
            {
                return;
            }

            var renderingContext = RenderHelper.CurrentRenderingContext;
            RenderHelper.BeginRenderContext(RenderingContext.RenderToAsset);

            while (batchQueue.Count > 0)
            {
                // gets the oldest batch?
                var batch = batchQueue.Peek();

                // fine for normal batching or holding pattern
                if (!batch.IsComplete && useBatchProcessing)
                {
                    return;
                }


                // start tracking time now that there is work to do
                Thundagun.lastStateUpdate = now;

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
                    // update the current time
                    now = DateTime.Now;
                    // figure out how long we've been working for
                    timeElapsed = (now - Thundagun.lastStateUpdate).TotalMilliseconds;
                    // if we're in a timeout currently
                    if (now - Thundagun.lastTimeout < TimeSpan.FromSeconds(timeoutCooldown))
                    {
                        // if we've been working for too long
                        if (timeElapsed > timeoutWorkInterval)
                        {
                            break;
                        }
                    }
                }
                // update the current time
                now = DateTime.Now;
                // figure out how long we've been working for
                timeElapsed = (now - Thundagun.lastStateUpdate).TotalMilliseconds;
                // if we're in a timeout currently
                if (now - Thundagun.lastTimeout < TimeSpan.FromSeconds(timeoutCooldown))
                {
                    // if we've been working for too long
                    if (timeElapsed > timeoutWorkInterval)
                    {
                        break;
                    }
                }
                // remove the batch if it's empty and completed
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