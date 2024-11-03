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
    public override string Author => "Fro Zen";
    public override string Version => "1.0.0";

    public static double Performance;

    public static readonly Queue<IUpdatePacket> CurrentPackets = new();

    //public static List<IUpdatePacket> CurrentPackets = new();

    public static Task CurrentTask;

    // Due to Unity's initialization sequence, both threads might need to run concurrently during startup to avoid potential deadlocks
    public static bool lockResoniteUnlockUnity = false;

    // The shared object used as the synchronization primitive for locking and coordinating thread access
    public static readonly object lockObject = new();

    //public static Task<int> AssetTask;

    //public static Thread CurrentThread;
    public static void QueuePacket(IUpdatePacket packet) {

        lock (CurrentPackets)
        {
            CurrentPackets.Enqueue(packet);
        }
        
    } 
    
    internal static ModConfiguration Config;
    
    [AutoRegisterConfigKey]
    internal readonly static ModConfigurationKey<bool> OutputDebug =
        new("OutputDebug", "Output Debug", () => true);
    [AutoRegisterConfigKey]
    internal readonly static ModConfigurationKey<float> TickRate =
        new("TickRate", "Tick Rate", () => 30);
    [AutoRegisterConfigKey]
    internal readonly static ModConfigurationKey<SyncMode> Mode =
        new("SyncMode", "Sync Mode", () => SyncMode.Sync);


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

            //the following are not required, after initialization
            //(which only happens once before the update loop switches over)
            //they do literally nothing
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
        var initInfos = (ConcurrentDictionary<Type, WorkerInitInfo>) initInfosField?.GetValue(null);
            
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

        //workerInitializerField?.SetValue(null, types.ToArray());
    }
}

[HarmonyPatch(typeof(FrooxEngineRunner))]
public static class FrooxEngineRunnerPatch
{

    public static Queue<int> assets_processed = new();

    public static DateTime lastrender;
    public static DateTime lastTick;

    public static bool firstrunengine = false;
    //public static bool firstrunrender = false;
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
            //Thundagun.AssetTask.Wait();
            //Thundagun.CurrentTask.Wait();
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
                //if we have a current task, wait for it to finish
                if (Thundagun.CurrentTask is null)
                {
                    Thundagun.CurrentTask = Task.Run(() =>
                    {

                        while (!shutdown) 
                        {
                            int total = 0;
                            lock (assets_processed) //so we don't spiral into oblivion where the assets processed grows faster than the while loop can dequeue them.
                            {
                                while (assets_processed.Count() > 0)
                                {
                                    total += assets_processed.Dequeue();
                                }
                            }

                            DateTime beforeEngine = DateTime.Now;
                            engine.AssetsUpdated(total); //inform engine we updated the assets from the render queue.
                            engine.RunUpdateLoop(); // generate our next engine update, adding packets to our list.
                            TimeSpan engine_time = (DateTime.Now - beforeEngine);
                            TimeSpan ticktime = TimeSpan.FromSeconds((1 / Math.Abs(Thundagun.Config.GetValue(Thundagun.TickRate))+1));
                            if (engine_time < ticktime)
                            {
                                Task.Delay(ticktime - engine_time);
                            }
                            //Thundagun.CurrentPackets = new(); //create a new list to put packets into during the engine update loop iteration.

                            //Thundagun.UpdatePackets.Enqueue(new List<IUpdatePacket>(Thundagun.CurrentPackets)); //enqueue the list, since we are done adding to it, so it can be processed by rendering.
                            RenderConnector.renderQueue.MarkAsCompleted(); //mark the render queue as completed, which is used in async mode
                            
                            
                            // Acquire an exclusive lock on the shared object to coordinate the FrooxEngine thread's access
                            lock (Thundagun.lockObject)
                            {
                                // Check if the FrooxEngine thread is allowed to proceed. If not, wait until Unity signals readiness
                                // This loop handles spurious wakeups and ensures the FrooxEngine thread only continues when allowed


                                while (Thundagun.Config.GetValue(Thundagun.Mode) == Thundagun.SyncMode.Sync && !Thundagun.lockResoniteUnlockUnity)
                                {
                                    Monitor.Wait(Thundagun.lockObject); // Release the lock and put the FrooxEngine thread into a waiting state until Unity signals
                                }

                                // Change the lock state to indicate that FrooxEngine has locked and Unity is now allowed to run
                                Thundagun.lockResoniteUnlockUnity = false;

                                // Signal the Unity thread that FrooxEngine has completed its update and it can now proceed
                                Monitor.Pulse(Thundagun.lockObject);
                            }
                        }


                    });

                }
                
                    // Acquire an exclusive lock on the shared object to coordinate Unity's access and synchronization with FrooxEngine
                    lock (Thundagun.lockObject)
                    {
                        // Check if Unity needs to wait for FrooxEngine to complete its update. If so, put Unity into a waiting state
                        // This ensures that Unity only proceeds when signaled by FrooxEngine, maintaining proper synchronization
                        while (Thundagun.Config.GetValue(Thundagun.Mode) == Thundagun.SyncMode.Sync && Thundagun.lockResoniteUnlockUnity)
                        {
                            Monitor.Wait(Thundagun.lockObject); // Unity waits until FrooxEngine signals it is ready
                        }

                        // Change the lock state to indicate that Unity is now running, allowing FrooxEngine to wait for the next cycle
                        Thundagun.lockResoniteUnlockUnity = true;

                        // Signal the FrooxEngine thread that Unity has completed its update and FrooxEngine can now proceed
                        Monitor.Pulse(Thundagun.lockObject);
                    }


                //if (Thundagun.CurrentThread is not null) Thundagun.CurrentThread.Join();


                //rethrow errors if they occured in the update loop
                if (Thundagun.CurrentTask?.Exception is not null) throw Thundagun.CurrentTask.Exception;
                //if (Thundagun.AssetTask?.Exception is not null) throw Thundagun.AssetTask.Exception;

                //head output and framerate boilerplate
                var focusedWorld = engine.WorldManager.FocusedWorld;
                var lastFocused = ____lastFocusedWorld;
                UpdateHeadOutput(focusedWorld, engine, ____vrOutput, ____screenOutput, ____audioListener, ref ____worlds);


                //more boilerplate
                engine.InputInterface.UpdateWindowResolution(new int2(Screen.width, Screen.height));
                
                //finally the interesting shit
                //first, we copy the list of current update packets into a local variable
                //then, we clear the original list and start the async update task
                
                var boilerplateTime = DateTime.Now;
                List<IUpdatePacket> updates;
                lock (Thundagun.CurrentPackets)
                {
                    updates = new List<IUpdatePacket>(Thundagun.CurrentPackets);
                    Thundagun.CurrentPackets.Clear();
                }


                //if (UnityAssetIntegrator._instance is not null)
                //{
                //    if (Thundagun.AssetTask is not null)
                //    {
                //        Thundagun.Msg("waiting on asset thread!");
                //        Thundagun.AssetTask.Wait();
                //        Thundagun.Msg("returning amount of assets updated!");
                //        Engine.Current.AssetsUpdated(Thundagun.AssetTask.Result);
                //    }
                //    else
                //    {

                //        Thundagun.Msg("telling game engine that no assets have updated.");
                //        Engine.Current.AssetsUpdated(0);
                //    }
                //    Thundagun.Msg("creating asset task");
                //    Thundagun.AssetTask = Task.Run<int>(() =>
                //    {
                //        Thundagun.Msg("starting unity queue processing.");
                //        return UnityAssetIntegrator._instance.ProcessQueue1(1000);
                //    });
                //}
                if (UnityAssetIntegrator._instance is not null /* && (DateTime.Now- lastrender).TotalSeconds > 1*/) //run asset integrator always
                {
                    lock (assets_processed)
                    {
                        //we inform the engine that the assets updated later.
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



                if (Thundagun.Config.GetValue(Thundagun.OutputDebug)) 
                {
                    Thundagun.Msg($"LastRender vs now: {(lastrender- starttime).TotalSeconds}");
                    Thundagun.Msg($"Boilerplate: {(boilerplateTime - starttime).TotalSeconds} Asset Integration time: {(assetTime - boilerplateTime).TotalSeconds} Loop time: {(loopTime - assetTime).TotalSeconds} Update time: {(updateTime - loopTime).TotalSeconds} Finished: {(finishTime - updateTime).TotalSeconds} total time: {(finishTime - starttime).TotalSeconds}");
                }
                lastrender = DateTime.Now;
                //__instance.UpdateFrooxEngine();
            }
            catch (Exception ex)
            {
                Thundagun.Msg($"Exception updating FrooxEngine:\n{ex}");
                DateTime startwait = DateTime.Now;
                int i = 0;
                Task wait = new Task(() => Task.Delay(10000));
                wait.Start();
                wait.Wait(); //force wait for 5000 milliseconds.
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

    /*
    public static void UpdateFrameRate(SystemInfoConnector systemInfo, Stopwatch externalUpdate, ref int framerateCounter, Stopwatch framerateUpdate)
    {
        var fps = systemInfo?.FPS ?? 0.0f;
        var externalUpdateTime = externalUpdate.ElapsedMilliseconds * (1f / 1000f);
        if (!framerateUpdate.IsRunning)
        {
            framerateUpdate.Restart();
        }
        else
        {
            framerateCounter++;
            var elapsedMilliseconds = framerateUpdate.ElapsedMilliseconds;
            if (elapsedMilliseconds >= 500L)
            {
                if (systemInfo != null)
                    fps = framerateCounter / (elapsedMilliseconds * (1f / 1000f));
                framerateCounter = 0;
                framerateUpdate.Restart();
            }
        }
        //var renderTime = !XRStats.TryGetGPUTimeLastFrame(out var gpuTimeLastFrame) ? -1f : gpuTimeLastFrame * (1f / 1000f);
        var immediateFPS = 1f / Time.unscaledDeltaTime;
        systemInfo?.UpdateTime(fps, immediateFPS, externalUpdateTime, externalUpdateTime);
    }
    */
    
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
            var transform2 = ((WorldConnector) world.Connector).WorldRoot.transform;
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
    //patch asset types
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
        //Thundagun.Msg($"Patched {asset.GetType().Name}");
        return false;
    }
}

public static class WorkerInitializerPatch
{
    public static void Initialize(Type workerType, WorkerInitInfo __result)
    {
        //if the type doesn't implement a connector, skip this
        if (!workerType.GetInterfaces().Contains(typeof(IImplementable))) return;
        
        //TODO: make this static
        //get all connector types from this mod
        var types = typeof(Thundagun)
            .Assembly
            .GetTypes()
            .Where(i => i.IsClass && i.GetInterfaces().Contains(typeof(IConnector)))
            .ToList();
        
        //find a type that implements IConnector<T>, where T is workerType
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