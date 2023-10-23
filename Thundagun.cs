using System;
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
using UnityAssetIntegrator = Thundagun.NewConnectors.UnityAssetIntegrator;
using WorldConnector = Thundagun.NewConnectors.WorldConnector;

namespace Thundagun;

public class Thundagun : ResoniteMod
{
    public override string Name => "Thundagun";
    public override string Author => "Fro Zen";
    public override string Version => "1.0.0";

    public static List<IUpdatePacket> CurrentPackets = new();

    public static Task CurrentTask;
    
    public override void OnEngineInit()
    {
        var harmony = new Harmony("Thundagun");
        harmony.PatchAll();
    }
}

[HarmonyPatch(typeof(FrooxEngineRunner))]
public static class FrooxEngineRunnerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("Update")]
    private static bool Update(FrooxEngineRunner __instance, ref Engine ____frooxEngine, ref bool ____shutdownRequest, ref Stopwatch ____externalUpdate, ref World ____lastFocusedWorld)
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
                UpdateHeadOutput(__instance, focusedWorld);
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
                foreach (var update in updates) update.Update();
                
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
    [HarmonyReversePatch]
    [HarmonyPatch("UpdateHeadOutput")]
    public static void UpdateHeadOutput(object instance, World focusedWorld) => throw new NotImplementedException("stub");
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

[HarmonyPatch(typeof(EngineInitializer))]
public static class EngineInitializerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("InitializeConnectorFields")]
    private static bool InitializeConnectorFieldsPrefix(List<Type> allTypes, Type type, Type connectorType,
        bool connectorMandatory, Type defaultConnector, bool verbose)
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
            return false;
        }
        if (type == typeof(World))
        {
            field1.SetValue(null, typeof(WorldConnector));
            return false;
        }
        if (type == typeof(AssetManager))
        {
            field1.SetValue(null, typeof(UnityAssetIntegrator));
            return false;
        }
        if (type == typeof(RenderManager))
        {
            field1.SetValue(null, typeof(RenderConnector));
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(WorkerInitializer))]
public class WorkerInitializerPatch
{
    [HarmonyPostfix]
    [HarmonyPatch("Initialize")]
    public static void InitializePatch(List<Type> allTypes, bool verbose)
    {
        var fieldInfo = typeof(WorkerInitializer).GetField("connectors", BindingFlags.Static | BindingFlags.NonPublic);
        var types = typeof(Thundagun).Assembly.GetTypes().Where(i => i.IsClass && i.GetInterfaces().Contains(typeof(IConnector))).ToList();

        //put all connectors that need no changes here
        /*
        types.AddRange(new[]
        {
            typeof(),
        });
        */

        fieldInfo?.SetValue(null, types.ToArray());
    }
}

[HarmonyPatch(typeof(AssetInitializer))]
public class AssetInitializerPatch
{
    public static bool Done;
    
    [HarmonyPrefix]
    [HarmonyPatch("Initialize")]
    private static bool Initialize(Type assetType)
    {
        if (Done) return true;
        var fieldInfo = typeof(AssetInitializer).GetField("connectors", BindingFlags.Static | BindingFlags.NonPublic);
        var types = typeof(Thundagun).Assembly.GetTypes().Where(i => i.IsClass && i.IsAbstract && i.GetInterfaces().Contains(typeof(IAssetConnector))).ToList();
        
        //put all connectors that need no changes here
        types.AddRange(new[]
        {
            typeof(MaterialConnector),
            typeof(MaterialConnectorBase),
            typeof(MaterialPropertyBlockConnector),
            typeof(ShaderConnector),
            typeof(VideoTextureConnector) //TODO: investigate
        });
        
        fieldInfo?.SetValue(null, types.ToArray());
        
        Done = true;
        return true;
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