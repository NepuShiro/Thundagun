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
    public Queue<RenderTask> Tasks { get; private set; } = new();
    public bool IsCompleteEngine { get; set; } = false;
    public bool IsCompleteUnity { get; set; } = false;

    public RenderConnector Connector;
    private TimeSpan LastWorkInterval = TimeSpan.Zero;
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

    // should be singleton?
    public RenderQueueProcessor()
    {
        Instance = this;
    }

    // convenience method
    public static void MarkIsCompleteEngine()
    {
        RenderQueueProcessor instance = Instance;
        Instance.IsCompleteEngine = true;
    }

    // convenience method
    public static bool GetIsCompleteUnity()
    {
        RenderQueueProcessor instance = Instance;
        if (instance != null)
        {
            if (instance.Tasks.Count != 0)
            {
                return instance.IsCompleteUnity;
            }
            return false;
        }
        return false;
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
            if (Tasks.Count == 0)
            {
                return;
            }

            var renderingContext = RenderHelper.CurrentRenderingContext;
            RenderHelper.BeginRenderContext(RenderingContext.RenderToAsset);

            DateTime startTime = DateTime.Now;
            TimeSpan timeElapsed;

            TimeSpan unityLastNonWorkInterval = SynchronizationManager.UnityLastUpdateInterval - LastWorkInterval;
            TimeSpan unityAllowedWorkInterval = TimeSpan.FromMilliseconds(1000.0 / Thundagun.Config.GetValue(Thundagun.MinUnityTickRate)) - unityLastNonWorkInterval;

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
            if (IsCompleteEngine && Tasks.Count == 0)
            {
                IsCompleteUnity = true;
            }

            timeElapsed = (DateTime.Now - startTime);
            LastWorkInterval = timeElapsed;

            // end critical region
            SynchronizationManager.UnlockResonite();

            if (renderingContext.HasValue)
            {
                RenderHelper.BeginRenderContext(renderingContext.Value);
            }
        }
    }
}