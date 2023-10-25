using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Elements.Core;
using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using UnityEngine;
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

    public static readonly List<IUpdatePacket> CurrentPackets = new();

    public static Task CurrentTask;
    public static void QueuePacket(IUpdatePacket packet) => CurrentPackets.Add(packet);

    public override void OnEngineInit()
    {
        var harmony = new Harmony("Thundagun");
        
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
    [HarmonyPrefix]
    [HarmonyPatch("Update")]
    public static bool Update(FrooxEngineRunner __instance, 
        ref Engine ____frooxEngine, ref bool ____shutdownRequest, ref Stopwatch ____externalUpdate, ref World ____lastFocusedWorld, 
        ref HeadOutput ____vrOutput, ref HeadOutput ____screenOutput, ref AudioListener ____audioListener, ref List<World> ____worlds)
    {
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
                //if we have a current task, wait for it to finish
                if (Thundagun.CurrentTask is not null) Thundagun.CurrentTask.Wait();
                
                //rethrow errors if they occured in the update loop
                if (Thundagun.CurrentTask?.Exception is not null)
                    throw Thundagun.CurrentTask.Exception;

                //head output and framerate boilerplate
                var focusedWorld = ____frooxEngine.WorldManager.FocusedWorld;
                var lastFocused = ____lastFocusedWorld;
                UpdateHeadOutput(focusedWorld, ____frooxEngine, ____vrOutput, ____screenOutput, ____audioListener, ref ____worlds);
                UpdateFrameRate(__instance);
                
                //more boilerplate
                ____frooxEngine.InputInterface.UpdateWindowResolution(new int2(Screen.width, Screen.height));
                
                //finally the interesting shit
                //first, we copy the list of current update packets into a local variable
                //then, we clear the original list and start the async update task
                var updates = new List<IUpdatePacket>(Thundagun.CurrentPackets);
                Thundagun.CurrentPackets.Clear();
                var engine = ____frooxEngine;
                Thundagun.CurrentTask = Task.Run(() =>
                {
                    engine.RunUpdateLoop();
                });

                if (UnityAssetIntegrator._instance is not null)
                {
                    Engine.Current.AssetsUpdated(UnityAssetIntegrator._instance.ProcessQueue1(1000));
                }

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

                /*
                var count = updates.Count;
                Thundagun.Msg($"Processed {count} packets");
                */
                
                //uhhh???
                if (focusedWorld != lastFocused)
                {
                    DynamicGIManager.ScheduleDynamicGIUpdate(true);
                    ____lastFocusedWorld = focusedWorld;
                    ____frooxEngine.GlobalCoroutineManager.RunInUpdates(10, () => DynamicGIManager.ScheduleDynamicGIUpdate(true));
                    ____frooxEngine.GlobalCoroutineManager.RunInSeconds(1f, () => DynamicGIManager.ScheduleDynamicGIUpdate(true));
                    ____frooxEngine.GlobalCoroutineManager.RunInSeconds(5f, () => DynamicGIManager.ScheduleDynamicGIUpdate(true));
                }
                UpdateQualitySettings(__instance);
                
                //__instance.UpdateFrooxEngine();
            }
            catch (Exception ex)
            {
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
            //Thundagun.Msg($"Patched " + workerType.Name);
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