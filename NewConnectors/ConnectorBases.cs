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

    public virtual IUpdatePacket InitializePacket() =>
        new InitializeComponentConnector<TD, TC, ComponentConnector<TD, TC>>(this, Owner);

    public virtual IUpdatePacket DestroyPacket(bool destroyingWorld) => new DestroyComponentConnector<TD, TC>(this, Owner, destroyingWorld);

    public override void Initialize() => Thundagun.QueuePacket(InitializePacket());
    public override void Destroy(bool destroyingWorld) => Thundagun.QueuePacket(DestroyPacket(destroyingWorld));

    public virtual void DestroyMethod(bool destroyingWorld)
    {
    }
}

public abstract class ComponentConnectorSingle<TD> : ComponentConnector<TD, IConnector> 
    where TD : ImplementableComponent<IConnector>
{
    public override IUpdatePacket InitializePacket() =>
        new InitializeComponentConnectorSingle<TD, ComponentConnectorSingle<TD>>(this, Owner);
    public override IUpdatePacket DestroyPacket(bool destroyingWorld) =>
        new DestroyComponentConnector<TD>(this, Owner, destroyingWorld);
}

public abstract class UnityComponentConnector<TC, TU> : ComponentConnectorSingle<TC>
    where TC : ImplementableComponent
    where TU : MonoBehaviour, IConnectorBehaviour
{
    public TU UnityComponent { get; set; }

    public override IUpdatePacket InitializePacket() => 
        new InitializeUnityComponentConnector<TC, TU, UnityComponentConnector<TC, TU>>(this, Owner);

    public override IUpdatePacket DestroyPacket(bool destroyingWorld) =>
        new DestroyUnityComponentConnector<TC, TU>(this, Owner, destroyingWorld);
}
