using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Elements.Core;
using FrooxEngine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors;

public class UnityAssetIntegrator : IAssetManagerConnector
{
    private static UnityAssetIntegrator _instance;
    private SpinQueue<QueueAction> _highpriorityQueue = new();
    private SpinQueue<QueueAction> _processingQueue = new();
    private SpinQueue<Action> _taskQueue = new();
    private Stopwatch _stopwatch = new();
    private double _maxMilliseconds;

    public Engine Engine => AssetManager.Engine;

    public AssetManager AssetManager { get; private set; }
    public static bool IsEditor { get; private set; }

    public static bool IsDebugBuild { get; private set; }

    public async Task Initialize(AssetManager owner)
    {
        var unityAssetIntegrator = this;
        _instance = _instance == null
            ? unityAssetIntegrator
            : throw new Exception("UnityAssetIntegrator has already been initialized");
        unityAssetIntegrator.AssetManager = owner;
        await InitializationTasks.Enqueue(delegate
        {
            IsEditor = Application.isEditor;
            IsDebugBuild = UnityEngine.Debug.isDebugBuild;
        });
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
        Thundagun.CurrentPackets.Add(new ProcessQueueUnityAssetIntegrator(this, maxMilliseconds));
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
        return ProcessQueue2(maxMilliseconds);
    }

    private int ProcessQueue2(double maxMilliseconds)
    {
        //Thundagun.Msg("Starting second process queue");
        //Thundagun.Msg($"Max millis: {maxMilliseconds}");
        //Thundagun.Msg($"Num queued: {_highpriorityQueue.Count + _processingQueue.Count}");
        var num1 = 0;
        _stopwatch.Restart();
        var hasHighPriority = false;
        try
        {
            double elapsedMilliseconds1;
            double num2;
            do
            {
                var elapsedMilliseconds2 = _stopwatch.GetElapsedMilliseconds();
                QueueAction val;
                hasHighPriority = false;
                var hasNormalPriority = false;
                if (_highpriorityQueue.TryPeek(out val)) hasHighPriority = true;
                else if (_processingQueue.TryPeek(out val)) hasNormalPriority = true;
                
                if (hasHighPriority | hasNormalPriority)
                {
                    
                    num1++;
                    var actionDone = false;
                    if (val.Action != null)
                    {
                        val.Action();
                        actionDone = true;
                    }
                    else if (!val.Coroutine.MoveNext()) actionDone = true;
                    if (actionDone)
                    {
                        if (hasHighPriority) _highpriorityQueue.TryDequeue(out _);
                        else _processingQueue.TryDequeue(out _);
                    }
                    
                    elapsedMilliseconds1 = _stopwatch.GetElapsedMilliseconds();
                    num2 = elapsedMilliseconds1 - elapsedMilliseconds2;
                }
                else break;
            } 
            while (elapsedMilliseconds1 + num2 < maxMilliseconds);
        }
        catch (Exception ex)
        {
            UniLog.Warning("Exception integrating asset: " + ex);
            if (hasHighPriority) _highpriorityQueue.TryDequeue(out _);
            else _processingQueue.TryDequeue(out _);
        }

        _maxMilliseconds = maxMilliseconds - _stopwatch.GetElapsedMilliseconds();
        
        //Thundagun.Msg($"Num processed: {num1}");
        
        return num1;
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