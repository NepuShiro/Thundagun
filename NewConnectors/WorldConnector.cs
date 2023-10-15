using FrooxEngine;
using UnityEngine;

namespace Thundagun.NewConnectors;

public class WorldConnector : IWorldConnector
{
    public World Owner { get; private set; }
    public static WorldConnector Constructor() => new WorldConnector();
    public GameObject WorldRoot { get; private set; }
    
    public void Initialize(World owner)
    {
        throw new System.NotImplementedException();
    }

    public void ChangeFocus(World.WorldFocus focus)
    {
        throw new System.NotImplementedException();
    }

    public void Destroy()
    {
        throw new System.NotImplementedException();
    }
}