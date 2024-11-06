using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using FrooxEngine;
using UnityEngine;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors;

public class RenderQueueProcessor : MonoBehaviour
{
    public RenderConnector Connector;

    private Queue<Batch> _batchQueue = new();
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

    // Normally there isn't a constructor here
    public RenderQueueProcessor()
    {
        EarlyLogger.Log($"Set RenderQueueProcessor");
        Instance = this;
    }

    public void MarkIsCompleteEngine()
    {
        if (_batchQueue.Count != 0)
            _batchQueue.Last().IsCompleteEngine = true;
    }

    public bool GetIsCompleteUnity()
    {
        if (_batchQueue.Count != 0)
        {
            bool isCompleteUnity = _batchQueue.Peek().IsCompleteUnity;
            if (isCompleteUnity)
            {
                _batchQueue.Dequeue();
                return isCompleteUnity;
            }
        }
        return false;
    }

    public Task<byte[]> Enqueue(FrooxEngine.RenderSettings settings)
    {
        EarlyLogger.Log($"Enqueue");
        var task = new TaskCompletionSource<byte[]>();
        var renderTask = new RenderTask(settings, task);

        lock (_batchQueue)
        {
            if (_batchQueue.Count == 0)
            {
                var newBatch = new Batch();
                _batchQueue.Enqueue(newBatch);
            }
            _batchQueue.Last().Tasks.Enqueue(renderTask);
        }

        return task.Task;
    }

    private void LateUpdate()
    {
        EarlyLogger.Log($"LateUpdate");
        lock (_batchQueue)
        {
            if (_batchQueue.Count == 0)
            {
                EarlyLogger.Log($"No batches");
                return;
            }

            var renderingContext = RenderHelper.CurrentRenderingContext;
            RenderHelper.BeginRenderContext(RenderingContext.RenderToAsset);

            DateTime startTime = DateTime.Now;
            TimeSpan timeElapsed;

            TimeSpan unityLastNonWorkInterval = SynchronizationManager.UnityLastUpdateInterval - LastWorkInterval;
            TimeSpan unityAllowedWorkInterval = TimeSpan.FromMilliseconds(1000.0 / Thundagun.Config.GetValue(Thundagun.MinUnityTickRate)) - unityLastNonWorkInterval;

            while (_batchQueue.Count > 0)
            {
                var batch = _batchQueue.Peek();

                while (batch.Tasks.Count > 0)
                {
                    EarlyLogger.Log($"Batch exists");
                    var renderTask = batch.Tasks.Dequeue();
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
                        EarlyLogger.Log($"Departing early");
                        break;
                    }
                }
                timeElapsed = (DateTime.Now - startTime);
                if (timeElapsed > unityAllowedWorkInterval)
                {
                    break;
                }
                if (batch.IsCompleteEngine && batch.Tasks.Count == 0)
                {
                    EarlyLogger.Log($"Unity is done with batch");
                    batch.IsCompleteUnity = true;
                }
            }

            timeElapsed = (DateTime.Now - startTime);
            LastWorkInterval = timeElapsed;

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
    public bool IsCompleteEngine { get; set; } = false;
    public bool IsCompleteUnity { get; set; } = false;
}