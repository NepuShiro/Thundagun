using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FrooxEngine;
using UnityEngine;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors;

public class RenderQueueProcessor : MonoBehaviour
{
    public static EngineCompletionStatus engineCompletionStatus = new EngineCompletionStatus();
    private static RenderQueueProcessor _instance;
    public static RenderQueueProcessor Instance
    {
        get { return _instance; }
        private set { _instance = value; }
    }

    private Queue<RenderTask> TaskBuffer = new Queue<RenderTask>();
    private Queue<RenderTask> Tasks = new Queue<RenderTask>();
    public RenderConnector Connector;
    private TimeSpan LastWorkInterval = TimeSpan.Zero;
    private bool useDoubleBuffering = false;

    public RenderQueueProcessor()
    {
        Instance = this;
    }

    public Task<byte[]> Enqueue(FrooxEngine.RenderSettings settings)
    {
        EarlyLogger.Log("Enqueue");
        var task = new TaskCompletionSource<byte[]>();
        var renderTask = new RenderTask(settings, task);

        lock (TaskBuffer)
        {
            if (useDoubleBuffering)
                TaskBuffer.Enqueue(renderTask);
            else
                Tasks.Enqueue(renderTask);
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
            TimeSpan unityAllowedWorkInterval = TimeSpan.FromMilliseconds(1000.0 / Thundagun.Config.GetValue(Thundagun.MinFramerate)) - unityLastNonWorkInterval;

            DateTime startTime = DateTime.Now;
            TimeSpan timeElapsed;

            while (Tasks.Count > 0)
            {
                var renderTask = Tasks.Dequeue();
                try
                {
                    renderTask.task.SetResult(Connector.RenderImmediate(renderTask.settings));
                }
                catch (Exception ex)
                {
                    renderTask.task.SetException(ex);
                }

                timeElapsed = DateTime.Now - startTime;
                if (timeElapsed > unityAllowedWorkInterval)
                {
                    break;
                }
            }

            LastWorkInterval = DateTime.Now - startTime;

            if (engineCompletionStatus.EngineCompleted && Tasks.Count == 0)
            {
                if (useDoubleBuffering)
                {
                    lock (TaskBuffer)
                    {
                        (Tasks, TaskBuffer) = (TaskBuffer, Tasks);
                        TaskBuffer.Clear();
                    }
                }
                bool currentBufferingSetting = Thundagun.Config.GetValue(Thundagun.UseDoubleBuffering);
                if (currentBufferingSetting != useDoubleBuffering)
                {
                    useDoubleBuffering = currentBufferingSetting;
                }
                engineCompletionStatus.EngineCompleted = false;
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