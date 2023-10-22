using System;
using System.Linq;
using Elements.Core;
using FrooxEngine;
using UnityEngine;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors.ComponentConnectors;

public class RenderTransformOverrideConnector : ComponentConnectorSingle<RenderTransformOverride>
{
    private RenderingContextHandler _handler;
    private RenderingContext? _registeredContext;
    private bool _isOverriden;
    private Vector3? _targetPosition;
    private Quaternion? _targetRotation;
    private Vector3? _targetScale;
    private Vector3? _originalPosition;
    private Quaternion? _originalRotation;
    private Vector3? _originalScale;

    public override void Initialize()
    {
        base.Initialize();
        _handler = HandleRenderingContextSwitch;
    }

    public override void ApplyChanges()
    {
        var nullable1 = !Owner.Enabled || !Owner.Slot.IsActive ? new RenderingContext?() : Owner.Context.Value;
        var nullable2 = nullable1;
        if (nullable2.GetValueOrDefault() == RenderingContext.UserView & nullable2.HasValue && !Owner.IsUnderLocalUser)
            nullable1 = new RenderingContext?();
        var registeredContext1 = _registeredContext;
        var nullable3 = nullable1;
        if (!(registeredContext1.GetValueOrDefault() == nullable3.GetValueOrDefault() &
              registeredContext1.HasValue == nullable3.HasValue))
        {
            if (_isOverriden)
                Restore();
            UnregisterHandler();
            if (nullable1.HasValue)
                RenderHelper.RegisterRenderContextEvents(nullable1.Value, _handler);
            _registeredContext = nullable1;
        }

        foreach (var skinnedMeshRenderer in Owner.SkinnedMeshRenderers.Select(i => i.Connector)
                     .OfType<SkinnedMeshRendererConnector>())
        {
            skinnedMeshRenderer.ForceRecalculationPerRender();
        }

        var nullable4 = Owner.PositionOverride.Value;
        ref var local1 = ref nullable4;
        _targetPosition = local1.HasValue ? local1.GetValueOrDefault().ToUnity() : new Vector3?();
        var nullable5 = Owner.RotationOverride.Value;
        ref var local2 = ref nullable5;
        _targetRotation = local2.HasValue ? local2.GetValueOrDefault().ToUnity() : new Quaternion?();
        var nullable6 = Owner.ScaleOverride.Value;
        ref var local3 = ref nullable6;
        _targetScale = local3.HasValue ? local3.GetValueOrDefault().ToUnity() : new Vector3?();
        if (_isOverriden)
            throw new InvalidOperationException("RenderTransform is overriden while being updated");
        if (!_registeredContext.HasValue)
            return;
        var renderingContext2 = RenderHelper.CurrentRenderingContext;
        var registeredContext2 = _registeredContext;
        if (!(renderingContext2.GetValueOrDefault() == registeredContext2.GetValueOrDefault() &
              renderingContext2.HasValue == registeredContext2.HasValue))
            return;
        Override();
    }

    public override void Destroy(bool destroyingWorld)
    {
        UnregisterHandler();
        base.Destroy(destroyingWorld);
    }

    private void UnregisterHandler()
    {
        if (!_registeredContext.HasValue)
            return;
        RenderHelper.UnregisterRenderContextEvents(_registeredContext.Value, _handler);
        _registeredContext = new RenderingContext?();
    }

    private void Override()
    {
        _isOverriden = !_isOverriden ? true : throw new Exception("RenderTransform is already overriden!");
        if (!AttachedGameObject) return;
        var transform = AttachedGameObject.transform;
        if (!transform) return;
        if (_targetPosition.HasValue)
        {
            _originalPosition = transform.localPosition;
            transform.localPosition = _targetPosition.Value;
        }
        else _originalPosition = new Vector3?();
        if (_targetRotation.HasValue)
        {
            _originalRotation = transform.localRotation;
            transform.localRotation = _targetRotation.Value;
        }
        else _originalRotation = new Quaternion?();
        if (_targetScale.HasValue)
        {
            _originalScale = transform.localScale;
            transform.localScale = _targetScale.Value;
        }
        else _originalScale = new Vector3?();
    }

    private void Restore()
    {
        if (!_isOverriden) throw new Exception("RenderTransform is not overriden");
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
        _isOverriden = false;
    }

    private void HandleRenderingContextSwitch(RenderingContextStage stage)
    {
        switch (stage)
        {
            case RenderingContextStage.Begin:
                Override();
                break;
            case RenderingContextStage.End:
                if (!_isOverriden)
                    break;
                Restore();
                break;
        }
    }
}