using FrooxEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors.ComponentConnectors;

public abstract class RenderContextOverride<D> : ComponentConnectorSingle<D> where D : ImplementableComponent<IConnector>
{
	public RenderingContextHandler _handler;

	private bool _isOverriden;

	private RenderingContext? _overridenContext;

	protected RenderingContext? _registeredContext { get; private set; }

	protected abstract RenderingContext Context { get; }

	public override void ApplyChanges() => Thundagun.QueuePacket(new ApplyChangesRenderContextOverrideConnector<D>(this));

	protected abstract void Override();

	protected abstract void Restore();

	public abstract void UpdateSetup();

	public override void Initialize()
	{
		base.Initialize();
		_handler = HandleRenderingContextSwitch;
	}

	public override void Destroy(bool destroyingWorld)
	{
		UnregisterHandler();
		base.Destroy(destroyingWorld);
	}

	public void RunOverride()
	{
		if (_isOverriden)
		{
			throw new Exception("RenderTransform is already overriden!");
		}
		_isOverriden = true;
		_overridenContext = RenderHelper.CurrentRenderingContext;
		Override();
	}

	public void RunRestore()
	{
		if (!_isOverriden)
		{
			throw new Exception("RenderTransform is not overriden");
		}
		Restore();
		_isOverriden = false;
		_overridenContext = null;
	}

	public override void ApplyChanges()
	{
		RenderingContext? renderingContext = ((base.Owner.Enabled && base.Owner.Slot.IsActive) ? new RenderingContext?(Context) : null);
		if (renderingContext == RenderingContext.UserView && !base.Owner.IsUnderLocalUser)
		{
			renderingContext = null;
		}
		if (_registeredContext != renderingContext)
		{
			if (_isOverriden)
			{
				RunRestore();
			}
			UnregisterHandler();
			if (renderingContext.HasValue)
			{
				RenderHelper.RegisterRenderContextEvents(renderingContext.Value, _handler);
			}
			_registeredContext = renderingContext;
		}
		UpdateSetup();
		if (_isOverriden)
		{
			throw new InvalidOperationException("RenderTransform is overriden while being updated. " + $"Current: {RenderHelper.CurrentRenderingContext}, Overriden: {_overridenContext}");
		}
		if (_registeredContext.HasValue && RenderHelper.CurrentRenderingContext == _registeredContext)
		{
			RunOverride();
		}
	}

	public void HandleRenderingContextSwitch(RenderingContextStage stage)
	{
		switch (stage)
		{
		case RenderingContextStage.Begin:
			RunOverride();
			break;
		case RenderingContextStage.End:
			if (_isOverriden)
			{
				RunRestore();
			}
			break;
		}
	}

	public void UnregisterHandler()
	{
		if (_registeredContext.HasValue)
		{
			RenderHelper.UnregisterRenderContextEvents(_registeredContext.Value, _handler);
			_registeredContext = null;
		}
	}
}

public class ApplyChangesRenderContextOverrideConnector<D> : UpdatePacket<D> where D : ImplementableComponent<IConnector>
{
    public RenderingContextHandler _handler;
	public bool _isOverriden;
    public RenderingContext? _overridenContext;
    protected RenderingContext? _registeredContext { get; private set; }
    protected RenderingContext Context { get; }
    protected RenderingContext? RenderingContext2;
	public RenderTransformOverride RTO;
    //public List<SkinnedMeshRendererConnector> SkinnecMeshes;
    //public Vector3? TargetPosition;
    //public Quaternion? TargetRotation;
    //public Vector3? TargetScale;
    public ApplyChangesRenderContextOverrideConnector(D owner) : base(owner)
    {
		RTO = owner as RenderTransformOverride;
        RenderingContext2 = RTO.Enabled && RTO.Slot.IsActive ? new RenderingContext?(Context) : null;
        if (RenderingContext2 == RenderingContext.UserView && !RTO.IsUnderLocalUser) RenderingContext2 = null;
        //Context = renderingContext;

        //SkinnecMeshes = owner.Owner.SkinnedMeshRenderers.Select(i => i.Connector as SkinnedMeshRendererConnector)
        //    .Where(i => i is not null).ToList();

        //TargetPosition = owner.Owner.PositionOverride.Value?.ToUnity();
        //TargetRotation = owner.Owner.RotationOverride.Value?.ToUnity();
        //TargetScale = owner.Owner.ScaleOverride.Value?.ToUnity();
    }

    public override void Update()
    {
		var connector = RTO.Connector as RenderContextOverride<RenderTransformOverride>;
        if (_registeredContext != RenderingContext2)
        {
            if (_isOverriden) connector.RunRestore();
            connector.UnregisterHandler();
            if (RenderingContext2.HasValue) RenderHelper.RegisterRenderContextEvents(RenderingContext2.Value, connector._handler);
            _registeredContext = RenderingContext2;
        }

        connector.UpdateSetup();
		if (_isOverriden)
		{
			throw new InvalidOperationException("RenderTransform is overriden while being updated. " + $"Current: {RenderHelper.CurrentRenderingContext}, Overriden: {_overridenContext}");
		}
		if (_registeredContext.HasValue && RenderHelper.CurrentRenderingContext == _registeredContext)
		{
			connector.RunOverride();
		}

        //foreach (var skinnedMeshRenderer in SkinnecMeshes) skinnedMeshRenderer.ForceRecalculationPerRender();

        //Owner.TargetPosition = TargetPosition;
        //Owner.TargetRotation = TargetRotation;
        //Owner.TargetScale = TargetScale;

        //if (Owner.IsOverriden)
        //    throw new InvalidOperationException("RenderTransform is overriden while being updated");
        //if (Owner.RegisteredContext.HasValue && RenderHelper.CurrentRenderingContext == Owner.RegisteredContext) Owner.Override();
    }
}