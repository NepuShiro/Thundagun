using System;
using System.Collections.Generic;
using System.Reflection;
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

[HarmonyPatch(typeof(EngineInitializer))]
public static class EngineInitializerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("InitializeConnectorFields")]
    private static bool InitializeConnectorFieldsPrefix(List<Type> allTypes, Type type, Type connectorType,
        bool connectorMandatory, Type defaultConnector, bool verbose)
    {
        var field1 = type.GetField("__connectorType", BindingFlags.FlattenHierarchy | BindingFlags.NonPublic | BindingFlags.Static);
        var field2 = type.GetField("__connectorTypes", BindingFlags.FlattenHierarchy | BindingFlags.NonPublic | BindingFlags.Static);
        if (type == typeof(Slot))
        {
            field1.SetValue(null, typeof(SlotConnector));
            return false;
        }
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