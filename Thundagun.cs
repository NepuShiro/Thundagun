using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Elements.Core;
using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using Thundagun.NewConnectors;

namespace Thundagun;

public class Thundagun : ResoniteMod
{
    public override string Name => "Thundagun";
    public override string Author => "Fro Zen";
    public override string Version => "1.0.0";

    public static List<IUpdatePacket> CurrentPackets = new();
    
    public override void OnEngineInit()
    {
        var harmony = new Harmony("Thundagun");
        /*
        var type = AccessTools.AllTypes().First(i => i.Name.Contains("<FinishInitialization>d__332"));
        var method = type.GetMethod("MoveNext", AccessTools.all);
        var patch = typeof(Thundagun).GetMethod(nameof(MethodPatch));
        harmony.Patch(method, new HarmonyMethod(patch));
        */
    }
}

[HarmonyPatch(typeof(UnityFrooxEngineRunner.FrooxEngineRunner))]
public static class FrooxEngineRunnerPatch
{
    
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
        // AudioSystem
        
        //TODO:
        // RenderManager
        
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
        var types = typeof(Thundagun).Assembly.GetTypes().Where(i => i.GetInterfaces().Contains(typeof(IConnector))).ToArray();
        fieldInfo?.SetValue(null, types);
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