using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FrooxEngine;
using UnityEngine;
using UnityFrooxEngineRunner;
using System.Threading;

namespace Thundagun.NewConnectors;

public class RenderQueueProcessor : MonoBehaviour
{
    public static EngineCompletionStatus engineCompletionStatus = new EngineCompletionStatus();
    private static RenderQueueProcessor _instance;
    public static RenderQueueProcessor Instance
    {
        get
        {
            return _instance;
        }
        private set
        {
            _instance = value;
        }
    }
    public Queue<RenderTask> TaskBuffer { get; private set; } = new Queue<RenderTask>();
    public Queue<RenderTask> Tasks { get; private set; } = new Queue<RenderTask>();
    public RenderConnector Connector;
    private TimeSpan LastWorkInterval = TimeSpan.Zero;

    public RenderQueueProcessor()
    {
        Instance = this;
    }

    public Task<byte[]> Enqueue(FrooxEngine.RenderSettings settings)
    {
        EarlyLogger.Log($"Enqueue");
        var task = new TaskCompletionSource<byte[]>();
        var renderTask = new RenderTask(settings, task);

        lock (TaskBuffer)
        {
            TaskBuffer.Enqueue(renderTask);
        }

        return task.Task;
    }

    private void LateUpdate()
    {
        lock (Tasks)
        {
            var renderingContext = RenderHelper.CurrentRenderingContext;
            RenderHelper.BeginRenderContext(RenderingContext.RenderToAsset);

            TimeSpan unityLastNonWorkInterval = SynchronizationManager.UnityLastUpdateInterval - LastWorkInterval;
            TimeSpan unityAllowedWorkInterval = TimeSpan.FromMilliseconds(1000.0 / Thundagun.Config.GetValue(Thundagun.MinUnityTickRate)) - unityLastNonWorkInterval;

            DateTime startTime = DateTime.Now;
            TimeSpan timeElapsed;

            while (Tasks.Count > 0)
            {
                EarlyLogger.Log($"Tasks available");
                var renderTask = Tasks.Dequeue();
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
            LastWorkInterval = timeElapsed;
            lock (engineCompletionStatus)
            {
                if (engineCompletionStatus.EngineCompleted && Tasks.Count == 0)
                {
                    // Swap queues and reset completion flag
                    (Tasks, TaskBuffer) = (TaskBuffer, Tasks);
                    TaskBuffer.Clear();
                    engineCompletionStatus.EngineCompleted = false;

                    // Signal Resonite to proceed
                    Monitor.PulseAll(engineCompletionStatus);
                    EarlyLogger.Log("Unity completed queue swap, signaled Resonite");
                }
            }

            if (renderingContext.HasValue)
            {
                RenderHelper.BeginRenderContext(renderingContext.Value);
            }
        }
    }
}

public class EngineCompletionStatus
{
    public bool EngineCompleted = false;
}