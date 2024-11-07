using System.Collections.Generic;
using Elements.Core;
using FrooxEngine;
using UnityEngine;

namespace Thundagun.NewConnectors.ComponentConnectors;

public class RenderTransformOverrideConnector : RenderContextOverride<RenderTransformOverride>
{
    public Vector3? TargetPosition;
    public Quaternion? TargetRotation;
    public Vector3? TargetScale;
    private Vector3? _originalPosition;
    private Quaternion? _originalRotation;
    private Vector3? _originalScale;
	public List<FrooxEngine.SkinnedMeshRenderer> _renderers;
	public bool _renderersDirty;
    private HashSet<SkinnedMeshRendererConnector> _registeredSkinnedConnectors = new HashSet<SkinnedMeshRendererConnector>();
	public override RenderingContext Context => base.Owner.Context.Value;

    public override void UpdateSetup()
	{
		if (!base._registeredContext.HasValue)
		{
			ClearRecalcRequests();
			//base.Owner.RenderersDirty = true;
		}
	}

    public override void DestroyMethod(bool destroyingWorld)
    {
        if (!destroyingWorld)
		{
			ClearRecalcRequests();
		}
		base.DestroyMethod(destroyingWorld);
    }

    private void ClearRecalcRequests()
	{
		foreach (SkinnedMeshRendererConnector registeredSkinnedConnector in _registeredSkinnedConnectors)
		{
			registeredSkinnedConnector.RemoveRequestForceRecalcPerRender(this);
		}
		_registeredSkinnedConnectors.Clear();
	}

    protected override void Override()
    {
        if (_renderersDirty)
		{
			bool flag = true;
			HashSet<SkinnedMeshRendererConnector> hashSet = Pool.BorrowHashSet<SkinnedMeshRendererConnector>();
			foreach (SkinnedMeshRendererConnector registeredSkinnedConnector in _registeredSkinnedConnectors)
			{
				hashSet.Add(registeredSkinnedConnector);
			}
			foreach (FrooxEngine.SkinnedMeshRenderer skinnedMeshRenderer in _renderers)
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
				//base.Owner.RenderersDirty = false;
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
}