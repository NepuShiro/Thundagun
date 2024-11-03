using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Elements.Core;
using FrooxEngine;
using HarmonyLib;
using Leap;
using ResoniteModLoader;
using UnityEngine;
using UnityEngine.XR;
using UnityFrooxEngineRunner;
using RenderConnector = Thundagun.NewConnectors.RenderConnector;
using SlotConnector = Thundagun.NewConnectors.SlotConnector;
using Texture2DConnector = Thundagun.NewConnectors.AssetConnectors.TextureConnector;
using UnityAssetIntegrator = Thundagun.NewConnectors.UnityAssetIntegrator;
using WorldConnector = Thundagun.NewConnectors.WorldConnector;

namespace Thundagun;

public class Thundagun : ResoniteMod
{
    public override string Name => "Thundagun";
    public override string Author => "Fro Zen, DoubleStyx, 989onan";
    public override string Version => "1.0.0";

    public static double Performance;
    public static DateTime unityStartTime = DateTime.Now;
    public static DateTime resoniteStartTime = DateTime.Now;
    private static double _unityEMA = 16.67;
    private static double _resoniteEMA = 16.67;
    public static void UpdateUnityEMA(double frameTime)
    {
        double alpha = Mathf.Clamp01(Config.GetValue(EMAExponent));
        _unityEMA = alpha * frameTime + (1 - alpha) * _unityEMA;
    }
    public static void UpdateResoniteEMA(double frameTime)
    {
        double alpha = Mathf.Clamp01(Config.GetValue(EMAExponent));
        _resoniteEMA = alpha * frameTime + (1 - alpha) * _resoniteEMA;
    }
    private static bool AsyncThresholdReached
    {
        get
        {
            double threshold = Config.GetValue(RatioThreshold);
            return (_unityEMA / _resoniteEMA) > threshold || (_resoniteEMA / _unityEMA) > threshold;
        }
    }
    public static bool UseAsync
    {
        get
        {
            return (Thundagun.Config.GetValue(Mode) == SyncMode.Async && !Thundagun.Config.GetValue(AutoMode)) || (Thundagun.Config.GetValue(AutoMode) && AsyncThresholdReached);
        }
    }
    

    public static readonly Queue<IUpdatePacket> CurrentPackets = new();

    public static Task CurrentTask;

    public static bool lockResoniteUnlockUnity = false;

    public static readonly object lockObject = new();

    public static void QueuePacket(IUpdatePacket packet)
    {
        lock (CurrentPackets)
        {
            CurrentPackets.Enqueue(packet);
        }

    }

    internal static ModConfiguration Config;

    [AutoRegisterConfigKey]
    internal readonly static ModConfigurationKey<bool> DebugLogging =
        new("DebugLogging", "Debug Logging: Whether to enable debug logging.", () => false);
    [AutoRegisterConfigKey]
    internal readonly static ModConfigurationKey<float> DebugLoggingTickRate =
        new("DebugLoggingTickRate", "Debug Logging Tick Rate: The rate at which debug logs are written.", () => 30);
    [AutoRegisterConfigKey]
    internal readonly static ModConfigurationKey<SyncMode> Mode =
        new("SyncMode", "Sync Mode: Whether to use sync mode or async mode.", () => SyncMode.Sync);
    [AutoRegisterConfigKey]
    internal readonly static ModConfigurationKey<bool> AutoMode =
        new("AutoMode", "Auto Mode: Auto switches to async if the game-renderer/renderer-game ratio is greater than some threshold.", () => false);
    [AutoRegisterConfigKey]
    internal readonly static ModConfigurationKey<double> RatioThreshold =
        new("RatioThreshold", "Ratio Threshold: The ratio to use when deciding whether to switch to async during auto mode.", () => 4.0);
    [AutoRegisterConfigKey]
    internal readonly static ModConfigurationKey<float> EMAExponent =
        new("EMAExponent", "EMA Exponent: The exponent used for the exponential moving average for calculating framerate.", () => 0.1f);

    public enum SyncMode
    {
        Sync,
        Async
    }
    public override void OnEngineInit()
    {
        var harmony = new Harmony("Thundagun");
        Config = GetConfiguration();

        PatchEngineTypes();
        PatchComponentConnectors(harmony);

        var workerInitializerMethod = typeof(WorkerInitializer)
            .GetMethods(AccessTools.all)
            .First(i => i.Name.Contains("Initialize") && i.GetParameters().Length == 1 &&
                        i.GetParameters()[0].ParameterType == typeof(Type));
        var workerInitializerPatch =
            typeof(WorkerInitializerPatch).GetMethod(nameof(WorkerInitializerPatch.Initialize));

        harmony.Patch(workerInitializerMethod, postfix: new HarmonyMethod(workerInitializerPatch));

        harmony.PatchAll();
    }

    public static void PatchEngineTypes()
    {
        var engineTypes = typeof(Slot).Assembly.GetTypes()
            .Where(i => i.GetCustomAttribute<ImplementableClassAttribute>() is not null).ToList();
        foreach (var type in engineTypes)
        {
            var field1 = type.GetField("__connectorType",
                BindingFlags.FlattenHierarchy | BindingFlags.NonPublic | BindingFlags.Static);
            var field2 = type.GetField("__connectorTypes",
                BindingFlags.FlattenHierarchy | BindingFlags.NonPublic | BindingFlags.Static);

            // WorldManager
            // AudioSystem TODO; check this one again

            if (type == typeof(Slot))
            {
                field1.SetValue(null, typeof(SlotConnector));
                Msg($"Patched {type.Name}");
            }
            else if (type == typeof(World))
            {
                field1.SetValue(null, typeof(WorldConnector));
                Msg($"Patched {type.Name}");
            }
            else if (type == typeof(AssetManager))
            {
                field1.SetValue(null, typeof(UnityAssetIntegrator));
                Msg($"Patched {type.Name}");
            }
            else if (type == typeof(RenderManager))
            {
                field1.SetValue(null, typeof(RenderConnector));
                Msg($"Patched {type.Name}");
            }
        }
    }

    public static void PatchComponentConnectors(Harmony harmony)
    {
        /*
        var workerInitializerField =
            typeof(WorkerInitializer).GetField("connectors", BindingFlags.Static | BindingFlags.NonPublic);
            */
        var types = typeof(Thundagun).Assembly.GetTypes()
            .Where(i => i.IsClass && i.GetInterfaces().Contains(typeof(IConnector))).ToList();

        var initInfosField = typeof(WorkerInitializer).GetField("initInfos", AccessTools.all);
        var initInfos = (ConcurrentDictionary<Type, WorkerInitInfo>)initInfosField?.GetValue(null);

        Msg($"Attempting to patch component types");

        foreach (var t in initInfos.Keys)
        {
            Msg($"Attempting " + t.Name);
            var connectorType = typeof(IConnector<>).MakeGenericType(!t.IsGenericType ? t : t.GetGenericTypeDefinition());
            var array = types.Where(j => j.GetInterfaces().Any(i => i == connectorType)).ToArray();
            if (array.Length == 1)
            {
                initInfos[t].connectorType = array[0];
                Msg($"Patched " + t.Name);
            }
        }
    }
}

[HarmonyPatch(typeof(FrooxEngineRunner))]
public static class FrooxEngineRunnerPatch
{

    public static Queue<int> assets_processed = new();

    public static DateTime lastrender;
    public static DateTime lastTick;

    public static bool firstrunengine = false;
    public static bool shutdown = false;

    [HarmonyPrefix]
    [HarmonyPatch("Update")]

    public static bool Update(FrooxEngineRunner __instance,
        ref Engine ____frooxEngine, ref bool ____shutdownRequest, ref Stopwatch ____externalUpdate, ref World ____lastFocusedWorld,
        ref HeadOutput ____vrOutput, ref HeadOutput ____screenOutput, ref AudioListener ____audioListener, ref List<World> ____worlds)
    {
        shutdown = ____shutdownRequest;
        if (!__instance.IsInitialized || ____frooxEngine == null)
            return false;
        if (____shutdownRequest)
        {
            __instance.Shutdown(ref ____frooxEngine);
        }
        else
        {
            ____externalUpdate.Stop();
            try
            {
                UpdateFrameRate(__instance);
                DateTime starttime = DateTime.Now;


                var engine = ____frooxEngine;
                if (Thundagun.CurrentTask is null)
                {
                    Thundagun.CurrentTask = Task.Run(() =>
                    {

                        while (!shutdown)
                        {
                            int total = 0;
                            lock (assets_processed)
                            {
                                while (assets_processed.Count() > 0)
                                {
                                    total += assets_processed.Dequeue();
                                }
                            }

                            DateTime beforeEngine = DateTime.Now;
                            engine.AssetsUpdated(total); 
                            engine.RunUpdateLoop(); 
                            TimeSpan engine_time = (DateTime.Now - beforeEngine);
                            TimeSpan ticktime = TimeSpan.FromSeconds((1 / Math.Abs(Thundagun.Config.GetValue(Thundagun.DebugLoggingTickRate)) + 1));
                            if (engine_time < ticktime)
                            {
                                Task.Delay(ticktime - engine_time);
                            }


                            RenderConnector.renderQueue.MarkAsCompleted();

                            TimeSpan elapsed = DateTime.Now - Thundagun.resoniteStartTime;

                            Thundagun.UpdateResoniteEMA(elapsed.TotalMilliseconds);

                            lock (Thundagun.lockObject)
                            {

                                while (!Thundagun.UseAsync && !Thundagun.lockResoniteUnlockUnity)
                                {
                                    Monitor.Wait(Thundagun.lockObject);
                                }

                                Thundagun.lockResoniteUnlockUnity = false;

                                Monitor.Pulse(Thundagun.lockObject);
                            }
                            Thundagun.resoniteStartTime = DateTime.Now;
                        }


                    });

                }

                TimeSpan elapsed = DateTime.Now - Thundagun.unityStartTime;

                Thundagun.UpdateUnityEMA(elapsed.TotalMilliseconds);

                lock (Thundagun.lockObject)
                {
                    while (!Thundagun.UseAsync && Thundagun.lockResoniteUnlockUnity)
                    {
                        Monitor.Wait(Thundagun.lockObject);
                    }

                    Thundagun.lockResoniteUnlockUnity = true;

                    Monitor.Pulse(Thundagun.lockObject);
                }

                Thundagun.unityStartTime = DateTime.Now;




                if (Thundagun.CurrentTask?.Exception is not null) throw Thundagun.CurrentTask.Exception;

                var focusedWorld = engine.WorldManager.FocusedWorld;
                var lastFocused = ____lastFocusedWorld;
                UpdateHeadOutput(focusedWorld, engine, ____vrOutput, ____screenOutput, ____audioListener, ref ____worlds);


                engine.InputInterface.UpdateWindowResolution(new int2(Screen.width, Screen.height));

                var boilerplateTime = DateTime.Now;
                List<IUpdatePacket> updates;
                lock (Thundagun.CurrentPackets)
                {
                    updates = new List<IUpdatePacket>(Thundagun.CurrentPackets);
                    Thundagun.CurrentPackets.Clear();
                }


                if (UnityAssetIntegrator._instance is not null) 
                {
                    lock (assets_processed)
                    {
                        assets_processed.Enqueue(UnityAssetIntegrator._instance.ProcessQueue1(1000));
                    }

                }

                var assetTime = DateTime.Now;







                var loopTime = DateTime.Now;

                foreach (var update in updates)
                {
                    try
                    {
                        update.Update();
                    }
                    catch (Exception e)
                    {
                        Thundagun.Msg(e);
                    }
                }




                var updateTime = DateTime.Now;

                if (focusedWorld != lastFocused)
                {
                    DynamicGIManager.ScheduleDynamicGIUpdate(true);
                    ____lastFocusedWorld = focusedWorld;
                    ____frooxEngine.GlobalCoroutineManager.RunInUpdates(10, () => DynamicGIManager.ScheduleDynamicGIUpdate(true));
                    ____frooxEngine.GlobalCoroutineManager.RunInSeconds(1f, () => DynamicGIManager.ScheduleDynamicGIUpdate(true));
                    ____frooxEngine.GlobalCoroutineManager.RunInSeconds(5f, () => DynamicGIManager.ScheduleDynamicGIUpdate(true));
                }
                UpdateQualitySettings(__instance);

                var finishTime = DateTime.Now;



                if (Thundagun.Config.GetValue(Thundagun.DebugLogging))
                {
                    Thundagun.Msg($"LastRender vs now: {(lastrender - starttime).TotalSeconds}");
                    Thundagun.Msg($"Boilerplate: {(boilerplateTime - starttime).TotalSeconds} Asset Integration time: {(assetTime - boilerplateTime).TotalSeconds} Loop time: {(loopTime - assetTime).TotalSeconds} Update time: {(updateTime - loopTime).TotalSeconds} Finished: {(finishTime - updateTime).TotalSeconds} total time: {(finishTime - starttime).TotalSeconds}");
                }
                lastrender = DateTime.Now;
            }
            catch (Exception ex)
            {
                Thundagun.Msg($"Exception updating FrooxEngine:\n{ex}");
                DateTime startwait = DateTime.Now;
                int i = 0;
                Task wait = new Task(() => Task.Delay(10000));
                wait.Start();
                wait.Wait();
                UniLog.Error($"Exception updating FrooxEngine:\n{ex}");
                ____frooxEngine = null;
                __instance.Shutdown(ref ____frooxEngine);

                return false;
            }
            __instance.DynamicGI?.UpdateDynamicGI();
            ____externalUpdate.Restart();
        }
        return false;
    }

    [HarmonyReversePatch]
    [HarmonyPatch("UpdateFrameRate")]
    public static void UpdateFrameRate(object instance) => throw new NotImplementedException("stub");

    private static void UpdateHeadOutput(World focusedWorld, Engine engine, HeadOutput VR, HeadOutput screen, AudioListener listener, ref List<World> worlds)
    {
        if (focusedWorld == null) return;
        var num = engine.InputInterface.VR_Active ? 1 : 0;
        var headOutput1 = num != 0 ? VR : screen;
        var headOutput2 = num != 0 ? screen : VR;
        if (headOutput2 != null && headOutput2.gameObject.activeSelf) headOutput2.gameObject.SetActive(false);
        if (!headOutput1.gameObject.activeSelf) headOutput1.gameObject.SetActive(true);
        headOutput1.UpdatePositioning(focusedWorld);
        Vector3 position;
        Quaternion rotation;
        if (focusedWorld.OverrideEarsPosition)
        {
            position = focusedWorld.LocalUserEarsPosition.ToUnity();
            rotation = focusedWorld.LocalUserEarsRotation.ToUnity();
        }
        else
        {
            var cameraRoot = headOutput1.CameraRoot;
            position = cameraRoot.position;
            rotation = cameraRoot.rotation;
        }
        listener.transform.SetPositionAndRotation(position, rotation);
        engine.WorldManager.GetWorlds(worlds);
        var transform1 = headOutput1.transform;
        foreach (var world in worlds)
        {
            if (world.Focus != World.WorldFocus.Overlay && world.Focus != World.WorldFocus.PrivateOverlay) continue;
            var transform2 = ((WorldConnector)world.Connector).WorldRoot.transform;
            var userGlobalPosition = world.LocalUserGlobalPosition;
            var userGlobalRotation = world.LocalUserGlobalRotation;

            var t = transform2.transform;

            t.position = transform1.position - userGlobalPosition.ToUnity();
            t.rotation = transform1.rotation * userGlobalRotation.ToUnity();
            t.localScale = transform1.localScale;
        }
        worlds.Clear();
    }

    [HarmonyReversePatch]
    [HarmonyPatch("UpdateQualitySettings")]
    public static void UpdateQualitySettings(object instance) => throw new NotImplementedException("stub");
    private static void Shutdown(this FrooxEngineRunner runner, ref Engine engine)
    {
        UniLog.Log("Shutting down");
        try
        {
            engine?.Dispose();
        }
        catch (Exception ex)
        {
            UniLog.Error("Exception disposing the engine:\n" + engine);
        }
        engine = null;
        try
        {
            runner.OnFinalizeShutdown?.Invoke();
        }
        catch
        {
        }
        Application.Quit();
        Process.GetCurrentProcess().Kill();
    }
}

[HarmonyPatch(typeof(AssetInitializer))]
public static class AssetInitializerPatch
{
    public static readonly Dictionary<Type, Type> Connectors = new();
    static AssetInitializerPatch()
    {
        var ourTypes = typeof(Thundagun).Assembly.GetTypes()
            .Where(i => i.GetInterfaces().Contains(typeof(IAssetConnector))).ToList();
        var theirTypes = typeof(Slot).Assembly.GetTypes().Where(t =>
        {
            if (!t.IsClass || t.IsAbstract || !typeof(Asset).IsAssignableFrom(t))
                return false;
            return t.InheritsFromGeneric(typeof(ImplementableAsset<,>)) || t.InheritsFromGeneric(typeof(DynamicImplementableAsset<>));
        }).ToList();

        foreach (var t in theirTypes)
        {
            var connectorType = t.GetProperty("Connector", BindingFlags.FlattenHierarchy | BindingFlags.Instance | BindingFlags.Public)?.PropertyType;
            if (connectorType is null) continue;
            var list = ourTypes.Where(i => connectorType.IsAssignableFrom(i)).ToList();
            if (list.Count == 1)
            {
                Connectors.Add(t, list[0]);
            }
        }
    }
    [HarmonyPrefix]
    [HarmonyPatch("GetConnectorType")]
    public static bool GetConnectorType(Asset asset, ref Type __result)
    {
        if (!Connectors.TryGetValue(asset.GetType(), out var t)) return true;
        __result = t;
        return false;
    }
}

public static class WorkerInitializerPatch
{
    public static void Initialize(Type workerType, WorkerInitInfo __result)
    {
        if (!workerType.GetInterfaces().Contains(typeof(IImplementable))) return;

        //TODO: make this static
        //get all connector types from this mod
        var types = typeof(Thundagun)
            .Assembly
            .GetTypes()
            .Where(i => i.IsClass && i.GetInterfaces().Contains(typeof(IConnector)))
            .ToList();

        var connectorType = typeof(IConnector<>)
            .MakeGenericType(workerType.IsGenericType ? workerType.GetGenericTypeDefinition() : workerType);
        var array = types.Where(j => j.GetInterfaces().Any(i => i == connectorType)).ToArray();

        if (array.Length == 1)
        {
            __result.connectorType = array[0];
            Thundagun.Msg($"Patched " + workerType.Name);
        }
    }
}

public abstract class UpdatePacket<T> : IUpdatePacket
{
    public T Owner;
    public abstract void Update();

    public UpdatePacket(T owner)
    {
        Owner = owner;
    }
}

public interface IUpdatePacket
{
    public void Update();
}

public class PerformanceTimer
{
    private string Name;
    private Stopwatch timer;

    public PerformanceTimer(string name)
    {
        Name = name;
        timer = new Stopwatch();
        timer.Start();
    }

    public void End()
    {
        timer.Stop();
        Thundagun.Msg($"{Name}: {timer.Elapsed.TotalSeconds}");
    }
}