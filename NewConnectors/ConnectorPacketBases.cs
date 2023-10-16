using FrooxEngine;
using UnityEngine;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors;


public class InitializeComponentConnector<TD, TC> : UpdatePacket<ComponentConnector<TD, TC>>
    where TD : ImplementableComponent<TC>
    where TC : class, IConnector
{
    public SlotConnector Connector;
    public InitializeComponentConnector(ComponentConnector<TD, TC> connector, TD component) : base(connector)
    {
        Connector = (SlotConnector) component.Slot.Connector;
    }

    public override void Update()
    {
        Owner.SlotConnector = Connector;
        Owner.AttachedGameObject = Owner.SlotConnector.RequestGameObject();
    }
}

public class InitializeComponentConnector<TD> : InitializeComponentConnector<TD, IConnector>
    where TD : ImplementableComponent<IConnector>
{
    public InitializeComponentConnector(ComponentConnector<TD, IConnector> connector, TD component) : base(connector, component)
    {
    }
}

public class InitializeUnityComponentConnector<TC, TU> : InitializeComponentConnector<TC>
    where TC : ImplementableComponent 
    where TU : MonoBehaviour, IConnectorBehaviour
{
    public UnityComponentConnector<TC, TU> OwnerConnector => Owner as UnityComponentConnector<TC, TU>;
    public InitializeUnityComponentConnector(UnityComponentConnector<TC, TU> connector, TC component) : base(connector, component)
    {
        
    }

    public override void Update()
    {
        base.Update();
        OwnerConnector.UnityComponent = Owner.AttachedGameObject.AddComponent<TU>();
        OwnerConnector.UnityComponent.AssignConnector(Owner);
    }
}



public class DestroyComponentConnector<TD, TC> : UpdatePacket<ComponentConnector<TD, TC>>
    where TD : ImplementableComponent<TC>
    where TC : class, IConnector
{
    public bool DestroyingWorld;
    public DestroyComponentConnector(ComponentConnector<TD, TC> connector, TD component, bool destroyingWorld) : base(connector)
    {
        DestroyingWorld = destroyingWorld;
    }

    public override void Update()
    {
        Owner.DestroyMethod(DestroyingWorld);
        if (Owner.SlotConnector != null && !DestroyingWorld)
            Owner.SlotConnector.FreeGameObject();
        Owner.SlotConnector = null;
        Owner.AttachedGameObject = null;
    }
}

public class DestroyComponentConnector<TD> : DestroyComponentConnector<TD, IConnector>
    where TD : ImplementableComponent<IConnector>
{
    public DestroyComponentConnector(ComponentConnector<TD> connector, TD component, bool destroyingWorld) :
        base(connector, component, destroyingWorld)
    {
    }
}

public class DestroyUnityComponentConnector<TC, TU> : DestroyComponentConnector<TC>
    where TC : ImplementableComponent 
    where TU : MonoBehaviour, IConnectorBehaviour
{
    public UnityComponentConnector<TC, TU> OwnerConnector => Owner as UnityComponentConnector<TC, TU>;
    
    public DestroyUnityComponentConnector(UnityComponentConnector<TC, TU> connector, TC component, bool destroyingWorld) :
        base(connector, component, destroyingWorld)
    {
    }

    public override void Update()
    {
        if (OwnerConnector.UnityComponent != null)
        {
            OwnerConnector.UnityComponent.ClearConnector();
            if (!DestroyingWorld && OwnerConnector.UnityComponent)
                Object.Destroy(OwnerConnector.UnityComponent);
            OwnerConnector.UnityComponent = default;
        }
        base.Update();
    }
}