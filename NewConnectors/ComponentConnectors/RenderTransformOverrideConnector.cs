using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Core;
using FrooxEngine;
using UnityEngine;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors.ComponentConnectors;

public class RenderTransformOverrideConnector : RenderContextOverride<RenderTransformOverride>
{
    //public RenderingContextHandler Handler;
    //public RenderingContext? RegisteredContext;
    //public bool IsOverriden;
    public Vector3? TargetPosition;
    public Quaternion? TargetRotation;
    public Vector3? TargetScale;
    private Vector3? _originalPosition;
    private Quaternion? _originalRotation;
    private Vector3? _originalScale;
    private HashSet<SkinnedMeshRendererConnector> _registeredSkinnedConnectors = new HashSet<SkinnedMeshRendererConnector>();
	protected override RenderingContext Context => base.Owner.Context.Value;

    public override void UpdateSetup()
	{
		if (!base._registeredContext.HasValue)
		{
			ClearRecalcRequests();
			base.Owner.RenderersDirty = true;
		}
		TargetPosition = base.Owner.PositionOverride.Value?.ToUnity();
		TargetRotation = base.Owner.RotationOverride.Value?.ToUnity();
		TargetScale = base.Owner.ScaleOverride.Value?.ToUnity();
	}

    //public override void Initialize()
    //{
    //    base.Initialize();
    //    Handler = HandleRenderingContextSwitch;
    //}

    

    public override void DestroyMethod(bool destroyingWorld)
    {
        if (!destroyingWorld)
		{
			ClearRecalcRequests();
		}
		base.DestroyMethod(destroyingWorld);
        //UnregisterHandler();
        //base.DestroyMethod(destroyingWorld);
    }

    private void ClearRecalcRequests()
	{
		foreach (SkinnedMeshRendererConnector registeredSkinnedConnector in _registeredSkinnedConnectors)
		{
			registeredSkinnedConnector.RemoveRequestForceRecalcPerRender(this);
		}
		_registeredSkinnedConnectors.Clear();
	}
    //public void UnregisterHandler()
    //{
    //    if (!RegisteredContext.HasValue)
    //        return;
    //    RenderHelper.UnregisterRenderContextEvents(RegisteredContext.Value, Handler);
    //    RegisteredContext = new RenderingContext?();
    //}

    protected override void Override()
    {
        //IsOverriden = !IsOverriden ? true : throw new Exception("RenderTransform is already overriden!");
        //if (!AttachedGameObject) return;
        //var transform = AttachedGameObject.transform;
        //if (!transform) return;
        //if (TargetPosition.HasValue)
        //{
        //    _originalPosition = transform.localPosition;
        //    transform.localPosition = TargetPosition.Value;
        //}
        //else _originalPosition = new Vector3?();
        //if (TargetRotation.HasValue)
        //{
        //    _originalRotation = transform.localRotation;
        //    transform.localRotation = TargetRotation.Value;
        //}
        //else _originalRotation = new Quaternion?();
        //if (TargetScale.HasValue)
        //{
        //    _originalScale = transform.localScale;
        //    transform.localScale = TargetScale.Value;
        //}
        //else _originalScale = new Vector3?();
        if (base.Owner.RenderersDirty)
		{
			bool flag = true;
			HashSet<SkinnedMeshRendererConnector> hashSet = Pool.BorrowHashSet<SkinnedMeshRendererConnector>();
			foreach (SkinnedMeshRendererConnector registeredSkinnedConnector in _registeredSkinnedConnectors)
			{
				hashSet.Add(registeredSkinnedConnector);
			}
			foreach (FrooxEngine.SkinnedMeshRenderer skinnedMeshRenderer in base.Owner.SkinnedMeshRenderers)
			{
				if (skinnedMeshRenderer == null)
				{
					continue;
				}
				if (skinnedMeshRenderer.Connector is SkinnedMeshRendererConnector skinnedMeshRendererConnector)
				{
					if (!hashSet.Remove(skinnedMeshRendererConnector))
					{
						skinnedMeshRendererConnector.RequestForceRecalcPerRender(this);
						_registeredSkinnedConnectors.Add(skinnedMeshRendererConnector);
					}
				}
				else
				{
					flag = false;
				}
			}
			foreach (SkinnedMeshRendererConnector item in hashSet)
			{
				item.RemoveRequestForceRecalcPerRender(this);
				_registeredSkinnedConnectors.Remove(item);
			}
			Pool.Return(ref hashSet);
			if (flag)
			{
				base.Owner.RenderersDirty = false;
			}
		}
		UnityEngine.Transform transform = base.AttachedGameObject.transform;
		if (TargetPosition.HasValue)
		{
			_originalPosition = transform.localPosition;
			transform.localPosition = TargetPosition.Value;
		}
		else
		{
			_originalPosition = null;
		}
		if (TargetRotation.HasValue)
		{
			_originalRotation = transform.localRotation;
			transform.localRotation = TargetRotation.Value;
		}
		else
		{
			_originalRotation = null;
		}
		if (TargetScale.HasValue)
		{
			_originalScale = transform.localScale;
			transform.localScale = TargetScale.Value;
		}
		else
		{
			_originalScale = null;
		}
    }

    protected override void Restore()
    {
        //if (!IsOverriden) throw new Exception("RenderTransform is not overriden");
        //if (AttachedGameObject)
        //{
        //    var transform = AttachedGameObject.transform;
        //    if (transform)
        //    {
        //        if (_originalPosition.HasValue) transform.localPosition = _originalPosition.Value;
        //        if (_originalRotation.HasValue) transform.localRotation = _originalRotation.Value;
        //        if (_originalScale.HasValue) transform.localScale = _originalScale.Value;
        //    }
        //}
        //IsOverriden = false;
        UnityEngine.Transform transform = base.AttachedGameObject.transform;
		if (_originalPosition.HasValue)
		{
			transform.localPosition = _originalPosition.Value;
		}
		if (_originalRotation.HasValue)
		{
			transform.localRotation = _originalRotation.Value;
		}
		if (_originalScale.HasValue)
		{
			transform.localScale = _originalScale.Value;
		}
    }

    //private void HandleRenderingContextSwitch(RenderingContextStage stage)
    //{
    //    switch (stage)
    //    {
    //        case RenderingContextStage.Begin:
    //            Override();
    //            break;
    //        case RenderingContextStage.End:
    //            if (!IsOverriden)
    //                break;
    //            Restore();
    //            break;
    //    }
    //}
}