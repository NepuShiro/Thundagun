using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FrooxEngine;
using UnityEngine;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors;

public class RenderQueueProcessor : MonoBehaviour
{
    public static bool IsCompleteEngine { get; set; } = false;
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
    public Queue<RenderTask> Tasks { get; private set; } = new();
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

        lock (Tasks)
        {
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
                timeElapsed = (DateTime.Now - startTime);
                if (timeElapsed > unityAllowedWorkInterval)
                {
                    break;
                }
            }

            timeElapsed = (DateTime.Now - startTime);
            LastWorkInterval = timeElapsed;

            if (IsCompleteEngine && Tasks.Count == 0)
            {
                SynchronizationManager.UnlockResonite();
            }

            if (renderingContext.HasValue)
            {
                RenderHelper.BeginRenderContext(renderingContext.Value);
            }
        }
    }
}