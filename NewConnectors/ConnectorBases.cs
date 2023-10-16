using FrooxEngine;
using UnityEngine;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors;

public abstract class ComponentConnector<TD, TC> : Connector<TD>
    where TD : ImplementableComponent<TC>
    where TC : class, IConnector
{
    public SlotConnector SlotConnector { get; set; }

    public GameObject AttachedGameObject { get; set; }

    public virtual InitializeComponentConnector<TD, TC> InitializePacket() =>
        new(this, Owner);

    public virtual DestroyComponentConnector<TD, TC> DestroyPacket(bool destroyingWorld) => new(this, Owner, destroyingWorld);

    public override void Initialize() => Thundagun.CurrentPackets.Add(InitializePacket());
    public override void Destroy(bool destroyingWorld) => Thundagun.CurrentPackets.Add(DestroyPacket(destroyingWorld));

    public virtual void DestroyMethod(bool destroyingWorld)
    {
    }
}

public abstract class ComponentConnector<TD> : ComponentConnector<TD, IConnector> 
    where TD : ImplementableComponent<IConnector>
{
    public override InitializeComponentConnector<TD, IConnector> InitializePacket() =>
        new InitializeComponentConnector<TD>(this, Owner);
    public override DestroyComponentConnector<TD, IConnector> DestroyPacket(bool destroyingWorld) =>
        new DestroyComponentConnector<TD>(this, Owner, destroyingWorld);
}

public abstract class UnityComponentConnector<TC, TU> : ComponentConnector<TC>
    where TC : ImplementableComponent
    where TU : MonoBehaviour, IConnectorBehaviour
{
    public TU UnityComponent { get; set; }

    public override InitializeComponentConnector<TC, IConnector> InitializePacket() => 
        new InitializeUnityComponentConnector<TC, TU>(this, Owner);

    public override DestroyComponentConnector<TC, IConnector> DestroyPacket(bool destroyingWorld) =>
        new DestroyUnityComponentConnector<TC, TU>(this, Owner, destroyingWorld);
}
