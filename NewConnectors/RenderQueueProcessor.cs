using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FrooxEngine;
using Leap;
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
        // cache config values
        //bool useBatchProcessing = Thundagun.CurrentSyncMode == Thundagun.SyncMode.Async; // fix batch processing later
        double timeoutThreshold = Thundagun.Config.GetValue(Thundagun.TimeoutThreshold);
        double timeoutCooldown = Thundagun.Config.GetValue(Thundagun.TimeoutCooldown);
        double timeoutWorkInterval = Thundagun.Config.GetValue(Thundagun.TimeoutWorkInterval);

        // set arrival time
        DateTime arrivalTime = DateTime.Now;

        // define shared variables
        DateTime now;
        double timeElapsed;

        // begin processing
        lock (batchQueue)
        {
            // if no tasks, return
            if (batchQueue.Count == 0)
            {
                return;
            }
            // if only one task, check if it is complete
            //else if (batchQueue.Count == 1)
            //{
            //    if (useBatchProcessing && !batchQueue.Peek().IsComplete)
            //    {
            //        return;
            //    }
            //}

            var renderingContext = RenderHelper.CurrentRenderingContext;
            RenderHelper.BeginRenderContext(RenderingContext.RenderToAsset);

            // while we still have batches left
            while (batchQueue.Count > 0)
            {
                // get the oldest batch?
                var batch = batchQueue.Peek();

                // if the last batch isn't done yet and we're batching
                //if (!batch.IsComplete && useBatchProcessing)
                //{
                //    // don't process the unfinished batch
                //    return;
                //}

                // while we still have tasks left
                while (batch.Tasks.Count > 0)
                {
                    // pop off a render task
                    var renderTask = batch.Tasks.Dequeue();
                    // try to apply it
                    try
                    {
                        renderTask.task.SetResult(Connector.RenderImmediate(renderTask.settings));
                    }
                    catch (Exception ex)
                    {
                        renderTask.task.SetException(ex);
                    }
                    // update now
                    now = DateTime.Now;
                    // update time elapsed
                    timeElapsed = (now - arrivalTime).TotalMilliseconds;
                    // see if we're in an active timeout
                    if (now - Thundagun.lastTimeout < TimeSpan.FromSeconds(timeoutCooldown))
                    {
                        // help the ema catch up
                        Thundagun.unityEMA = timeoutThreshold;
                        // we might still be in async, ensure we stop doing the whole batch
                        //useBatchProcessing = false;
                        // if so, see if we're running over schedule
                        if (timeElapsed > timeoutWorkInterval)
                        {
                            // if so, break out
                            break;
                        }
                    }
                    else // not in an active timeout, should check if we might be in one
                    {
                        if (timeElapsed > timeoutThreshold)
                        {
                            // update the last timeout to enter a timeout state again
                            Thundagun.lastTimeout = now;
                        }

                    }
                }
                now = DateTime.Now;
                timeElapsed = (now - arrivalTime).TotalMilliseconds;
                // see if we're in an active timeout
                if (now - Thundagun.lastTimeout < TimeSpan.FromSeconds(timeoutCooldown))
                {
                    // help the ema catch up
                    Thundagun.unityEMA = timeoutThreshold;
                    // we might still be in async, ensure we stop doing the whole batch
                    //useBatchProcessing = false;
                    // if so, see if we're running over schedule
                    if (timeElapsed > timeoutWorkInterval)
                    {
                        // if so, break out
                        break;
                    }
                }
                // if a finished batch has been emptied
                if (batch.IsComplete && batch.Tasks.Count == 0)
                {
                    // remove finished empty batch
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