using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Core;
using FrooxEngine;
using UnityEngine;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors.ComponentConnectors;

public class RenderTransformOverrideConnector : ComponentConnectorSingle<RenderTransformOverride>
{
    public RenderingContextHandler Handler;
    public RenderingContext? RegisteredContext;
    public bool IsOverriden;
    public Vector3? TargetPosition;
    public Quaternion? TargetRotation;
    public Vector3? TargetScale;
    private Vector3? _originalPosition;
    private Quaternion? _originalRotation;
    private Vector3? _originalScale;

    public override void Initialize()
    {
        base.Initialize();
        Handler = HandleRenderingContextSwitch;
    }

    public override void ApplyChanges() => Thundagun.QueuePacket(new ApplyChangesRenderTransformOverrideConnector(this));

    public override void DestroyMethod(bool destroyingWorld)
    {
        UnregisterHandler();
        base.DestroyMethod(destroyingWorld);
    }

    public void UnregisterHandler()
    {
        if (!RegisteredContext.HasValue)
            return;
        RenderHelper.UnregisterRenderContextEvents(RegisteredContext.Value, Handler);
        RegisteredContext = new RenderingContext?();
    }

    public void Override()
    {
        IsOverriden = !IsOverriden ? true : throw new Exception("RenderTransform is already overriden!");
        if (!AttachedGameObject) return;
        var transform = AttachedGameObject.transform;
        if (!transform) return;
        if (TargetPosition.HasValue)
        {
            _originalPosition = transform.localPosition;
            transform.localPosition = TargetPosition.Value;
        }
        else _originalPosition = new Vector3?();
        if (TargetRotation.HasValue)
        {
            _originalRotation = transform.localRotation;
            transform.localRotation = TargetRotation.Value;
        }
        else _originalRotation = new Quaternion?();
        if (TargetScale.HasValue)
        {
            _originalScale = transform.localScale;
            transform.localScale = TargetScale.Value;
        }
        else _originalScale = new Vector3?();
    }

    public void Restore()
    {
        if (!IsOverriden) throw new Exception("RenderTransform is not overriden");
        if (AttachedGameObject)
        {
            var transform = AttachedGameObject.transform;
            if (transform)
            {
                if (_originalPosition.HasValue) transform.localPosition = _originalPosition.Value;
                if (_originalRotation.HasValue) transform.localRotation = _originalRotation.Value;
                if (_originalScale.HasValue) transform.localScale = _originalScale.Value;
            }
        }
        IsOverriden = false;
    }

    private void HandleRenderingContextSwitch(RenderingContextStage stage)
    {
        switch (stage)
        {
            case RenderingContextStage.Begin:
                Override();
                break;
            case RenderingContextStage.End:
                if (!IsOverriden)
                    break;
                Restore();
                break;
        }
    }
}

public class ApplyChangesRenderTransformOverrideConnector : UpdatePacket<RenderTransformOverrideConnector>
{
    public RenderingContext? Context;
    public List<SkinnedMeshRendererConnector> SkinnecMeshes;
    public Vector3? TargetPosition;
    public Quaternion? TargetRotation;
    public Vector3? TargetScale;
    public ApplyChangesRenderTransformOverrideConnector(RenderTransformOverrideConnector owner) : base(owner)
    {
        var renderingContext = owner.Owner.Enabled && owner.Owner.Slot.IsActive ? new RenderingContext?(owner.Owner.Context.Value) : null;
        if (renderingContext == RenderingContext.UserView && !owner.Owner.IsUnderLocalUser) renderingContext = null;
        Context = renderingContext;

        SkinnecMeshes = owner.Owner.SkinnedMeshRenderers.Select(i => i.Connector as SkinnedMeshRendererConnector)
            .Where(i => i is not null).ToList();
        
        TargetPosition = owner.Owner.PositionOverride.Value?.ToUnity();
        TargetRotation = owner.Owner.RotationOverride.Value?.ToUnity();
        TargetScale = owner.Owner.ScaleOverride.Value?.ToUnity();
    }

    public override void Update()
    {
        if (Owner.RegisteredContext != Context)
        {
            if (Owner.IsOverriden) Owner.Restore();
            Owner.UnregisterHandler();
            if (Context.HasValue) RenderHelper.RegisterRenderContextEvents(Context.Value, Owner.Handler);
            Owner.RegisteredContext = Context;
        }
        
        foreach (var skinnedMeshRenderer in SkinnecMeshes) skinnedMeshRenderer.ForceRecalculationPerRender();
        
        Owner.TargetPosition = TargetPosition;
        Owner.TargetRotation = TargetRotation;
        Owner.TargetScale = TargetScale;
        
        if (Owner.IsOverriden)
            throw new InvalidOperationException("RenderTransform is overriden while being updated");
        if (Owner.RegisteredContext.HasValue && RenderHelper.CurrentRenderingContext == Owner.RegisteredContext) Owner.Override();
    }
}