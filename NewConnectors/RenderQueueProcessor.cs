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

    private Queue<RenderTask> Tasks = new Queue<RenderTask>();
    public RenderConnector Connector;

    public RenderQueueProcessor()
    {
        Instance = this;
    }

    public Task<byte[]> Enqueue(FrooxEngine.RenderSettings settings)
    {
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
        lock (engineCompletionStatus)
        {
            if (Tasks.Count == 0 && engineCompletionStatus.EngineCompleted)
            {
                engineCompletionStatus.EngineCompleted = false;
            }

            if (!engineCompletionStatus.EngineCompleted)
                return;
        }


        lock (Tasks)
        {
            var renderingContext = RenderHelper.CurrentRenderingContext;
            RenderHelper.BeginRenderContext(RenderingContext.RenderToAsset);

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