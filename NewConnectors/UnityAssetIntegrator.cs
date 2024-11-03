using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Elements.Core;
using FrooxEngine;
using NativeGraphics.NET;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors;

public class UnityAssetIntegrator : IAssetManagerConnector
{
    public static UnityAssetIntegrator _instance;
    internal static SharpDX.Direct3D11.Device _dx11device;
    private ConcurrentQueue<QueueAction> _highpriorityQueue = new();
    private ConcurrentQueue<QueueAction> _processingQueue = new();
    private ConcurrentQueue<QueueAction> renderThreadQueue = new();
    private ConcurrentQueue<Action> _taskQueue = new();
    private Stopwatch _stopwatch = new();
    private double _maxMilliseconds;
    private Action<int> renderThreadCallback;
    private IntPtr renderThreadPointer;
    public Engine Engine => AssetManager.Engine;

    public AssetManager AssetManager { get; private set; }
    public GraphicsDeviceType GraphicsDeviceType { get; private set; }
    public static bool IsEditor { get; private set; }

    public static bool IsDebugBuild { get; private set; }

    public bool RenderThreadProcessingEnabled { get; private set; }

    [MonoPInvokeCallback(typeof(RenderEventDelegate))]
    private static void RenderThreadCallback()
    {
        try
        {
            //if (!IsDebugBuild) GarbageCollector.GCMode = GarbageCollector.Mode.Disabled;
            _instance.ProcessQueue2(MathX.Max(_instance._maxMilliseconds, 2.0), true);
        }
        catch (Exception ex)
        {
            UniLog.Error("Exception in render thread queue processing:\n" + ex);
        }
        finally
        {
            //if (!IsDebugBuild) GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
        }
    }


    public async Task Initialize(AssetManager owner)
    {
        var unityAssetIntegrator = this;
        _instance = _instance == null
            ? unityAssetIntegrator
            : throw new Exception("UnityAssetIntegrator has already been initialized");
        unityAssetIntegrator.AssetManager = owner;
        await InitializationTasks.Enqueue(delegate
        {
            GraphicsDeviceType = SystemInfo.graphicsDeviceType;
            IsEditor = Application.isEditor;
            IsDebugBuild = UnityEngine.Debug.isDebugBuild;
            UniLog.Log($"Graphics Device Type: {GraphicsDeviceType}");
            switch (GraphicsDeviceType)
            {
                case GraphicsDeviceType.Direct3D11:
                    {
                        var texture2D = new UnityEngine.Texture2D(4, 4);
                        _dx11device = new SharpDX.Direct3D11.Texture2D(texture2D.GetNativeTexturePtr()).Device;
                        if (texture2D) UnityEngine.Object.Destroy(texture2D);
                        RenderThreadProcessingEnabled = true;
                        break;
                    }
                case GraphicsDeviceType.OpenGLES2:
                case GraphicsDeviceType.OpenGLES3:
                case GraphicsDeviceType.OpenGLCore:
                    RenderThreadProcessingEnabled = true;
                    break;
            }
            if (RenderThreadProcessingEnabled)
            {
                Callback.SetUpdateCallback(RenderThreadCallback);
                renderThreadPointer = Callback.GetRenderEventFunc();
            }
        });
    }
    public void EnqueueRenderThreadProcessing(IEnumerator coroutine)
    {
        if (!RenderThreadProcessingEnabled) throw new NotSupportedException("Render Thread Processing is not enabled");
        renderThreadQueue.Enqueue(new QueueAction(coroutine));
    }

    public void EnqueueRenderThreadProcessing(Action action)
    {
        if (!RenderThreadProcessingEnabled) throw new NotSupportedException("Render Thread Processing is not enabled");
        renderThreadQueue.Enqueue(new QueueAction(action));
    }
    public void EnqueueProcessing(IEnumerator coroutine, bool highPriority)
    {
        if (highPriority) _highpriorityQueue.Enqueue(new QueueAction(coroutine));
        else _processingQueue.Enqueue(new QueueAction(coroutine));
    }
    public void EnqueueProcessing(Action action, bool highPriority)
    {
        if (highPriority) _highpriorityQueue.Enqueue(new QueueAction(action));
        else _processingQueue.Enqueue(new QueueAction(action));
    }

    public void EnqueueTask(Action action) => _taskQueue.Enqueue(action);

    public int ProcessQueue(double maxMilliseconds)
    {
        Thundagun.QueuePacket(new ProcessQueueUnityAssetIntegrator(this, maxMilliseconds));
        return 0;
    }

    public int ProcessQueue1(double maxMilliseconds)
    {
        //Thundagun.Msg("Processing asset queue");


        while (_taskQueue.TryDequeue(out var val))
        {
            try
            {
                val();
            }
            catch (Exception ex)
            {
                UniLog.Error("Exception running AssetIntegrator task:\n" + ex);
            }
        }
        if (RenderThreadProcessingEnabled && renderThreadQueue.Count > 0)
        {
            GL.IssuePluginEvent(renderThreadPointer, 0);
        }
        return ProcessQueue2(maxMilliseconds, false);
    }

    private int ProcessQueue2(double maxMilliseconds, bool renderThread)
    {
        var num = 0;
        _stopwatch.Restart();
        var hasPriorityQueue = false;
        var hasQueue = false;
        try
        {
            double elapsedMilliseconds2;
            double num2;
            do
            {
                var elapsedMilliseconds = _stopwatch.GetElapsedMilliseconds();
                hasPriorityQueue = false;
                hasQueue = false;
                QueueAction val;
                if (!renderThread)
                {
                    if (_highpriorityQueue.TryPeek(out val)) hasPriorityQueue = true;
                    else if (_processingQueue.TryPeek(out val)) hasQueue = true;
                }
                else hasQueue = renderThreadQueue.TryPeek(out val);

                if (!(hasPriorityQueue || hasQueue)) break;

                num++;

                var actionDone = false;
                if (val.Action != null)
                {
                    val.Action();
                    actionDone = true;
                }
                else if (!val.Coroutine.MoveNext()) actionDone = true;

                if (actionDone)
                {
                    if (!renderThread)
                    {
                        if (hasPriorityQueue) _highpriorityQueue.TryDequeue(out _);
                        else _processingQueue.TryDequeue(out _);
                    }
                    else renderThreadQueue.TryDequeue(out _);
                }
                elapsedMilliseconds2 = _stopwatch.GetElapsedMilliseconds();
                num2 = elapsedMilliseconds2 - elapsedMilliseconds;
            }
            while (elapsedMilliseconds2 + num2 < maxMilliseconds);
        }
        catch (Exception ex)
        {
            UniLog.Warning("Exception integrating asset: " + ex);
            if (!renderThread)
            {
                if (hasPriorityQueue) _highpriorityQueue.TryDequeue(out _);
                else _processingQueue.TryDequeue(out _);
            }
            else
            {
                UniLog.Error("DeviceRemovedReason: " + _dx11device.DeviceRemovedReason.Code.ToString("X8"));
                renderThreadQueue.TryDequeue(out _);
            }
        }
        _maxMilliseconds = maxMilliseconds - _stopwatch.GetElapsedMilliseconds();
        return num;
    }

    private struct QueueAction
    {
        public readonly Action Action;
        public readonly IEnumerator Coroutine;

        public QueueAction(Action action)
        {
            Action = action;
            Coroutine = null;
        }

        public QueueAction(IEnumerator coroutine)
        {
            Action = null;
            Coroutine = coroutine;
        }
    }
}

public class ProcessQueueUnityAssetIntegrator : UpdatePacket<UnityAssetIntegrator>
{
    private readonly double _maxMilliseconds;
    public ProcessQueueUnityAssetIntegrator(UnityAssetIntegrator owner, double maxMilliseconds) : base(owner) =>
        _maxMilliseconds = maxMilliseconds;
    public override void Update() => Engine.Current.AssetsUpdated(Owner.ProcessQueue1(_maxMilliseconds));
}