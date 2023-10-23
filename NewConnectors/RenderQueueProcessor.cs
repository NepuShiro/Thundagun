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
    private Queue<RenderTask> tasks = new();

    public Task<byte[]> Enqueue(FrooxEngine.RenderSettings settings)
    {
        var task = new TaskCompletionSource<byte[]>();
        var renderTask = new RenderTask(settings, task);
        lock (tasks)
            tasks.Enqueue(renderTask);
        return task.Task;
    }

    private void LateUpdate()
    {
        lock (tasks)
        {
            if (tasks.Count == 0)
                return;
            var renderingContext = RenderHelper.CurrentRenderingContext;
            RenderHelper.BeginRenderContext(RenderingContext.RenderToAsset);
            while (tasks.Count > 0)
            {
                var renderTask = tasks.Dequeue();
                try
                {
                    renderTask.task.SetResult(Connector.RenderImmediate(renderTask.settings));
                }
                catch (Exception ex)
                {
                    renderTask.task.SetException(ex);
                }
            }
            if (!renderingContext.HasValue)
                return;
            RenderHelper.BeginRenderContext(renderingContext.Value);
        }
    }
}