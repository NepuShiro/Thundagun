using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using UnityFrooxEngineRunner;
using Mesh = UnityEngine.Mesh;
using Object = UnityEngine.Object;
using Transform = UnityEngine.Transform;

namespace Thundagun.NewConnectors.ComponentConnectors;

public class SkinnedMeshRendererConnector :
    MeshRendererConnectorBase<SkinnedMeshRenderer, UnityEngine.SkinnedMeshRenderer>,
    ISkinnedMeshRendererConnector
{
    public SkinnedMeshRendererConnector _proxySource;
    public SkinBoundsUpdater _boundsUpdater;
    public Transform[] bones;
    private bool _sendingBoundsUpdate;
    private HashSet<RenderTransformOverrideConnector> _forceRecalcRegistrations;
    public SkinnedBounds _currentBoundsMethod = (SkinnedBounds)(-1);

    public bool LocalBoundingBoxAvailable { get; internal set; }

    public BoundingBox LocalBoundingBox { get; internal set; }

    public bool ForceRecalcPerRender
	{
		get
		{
			HashSet<RenderTransformOverrideConnector> forceRecalcRegistrations = _forceRecalcRegistrations;
			if (forceRecalcRegistrations == null)
			{
				return false;
			}
			return forceRecalcRegistrations.Count > 0;
		}
	}

    public event Action BoundsUpdated;

    public override bool UseMeshFilter => false;

    public bool ForceRecalcActive => base.MeshRenderer?.forceMatrixRecalculationPerRender ?? false;

    public override void AssignMesh(UnityEngine.SkinnedMeshRenderer renderer, Mesh mesh) => renderer.sharedMesh = mesh;

    public override void ApplyChanges()
    {
        Thundagun.QueuePacket(new ApplyChangesSkinnedMeshRenderer(this));
    }
    public void CleanupBoundsUpdater()
    {
        if ((bool)_boundsUpdater)
            Object.Destroy(_boundsUpdater);
        _boundsUpdater = null;
    }

    public override void OnCleanupRenderer()
    {
        base.OnCleanupRenderer();
        if (!(_boundsUpdater != null))
            return;
        CleanupBoundsUpdater();
    }

    internal void RequestForceRecalcPerRender(RenderTransformOverrideConnector connector)
	{
		if (_forceRecalcRegistrations == null)
		{
			_forceRecalcRegistrations = new HashSet<RenderTransformOverrideConnector>();
		}
		if (!ForceRecalcPerRender && base.MeshRenderer != null)
		{
			base.MeshRenderer.forceMatrixRecalculationPerRender = true;
		}
		_forceRecalcRegistrations.Add(connector);
	}

    internal void RemoveRequestForceRecalcPerRender(RenderTransformOverrideConnector connector)
	{
		_forceRecalcRegistrations.Remove(connector);
		if (!ForceRecalcPerRender && base.MeshRenderer != null)
		{
			base.MeshRenderer.forceMatrixRecalculationPerRender = false;
		}
	}

    public void SendBoundsUpdated()
    {
        if (_sendingBoundsUpdate)
            return;
        try
        {
            _sendingBoundsUpdate = true;
            if (BoundsUpdated == null)
                return;
            BoundsUpdated.Invoke();
        }
        finally
        {
            _sendingBoundsUpdate = false;
        }
    }

    public void CleanupProxy()
    {
        if (_proxySource == null)
            return;
        _proxySource.BoundsUpdated -= ProxyBoundsUpdated;
        _proxySource = null;
    }

    public void ProxyBoundsUpdated()
    {
        if (base.MeshRenderer != null && _proxySource.MeshRenderer != null)
		{
			base.MeshRenderer.localBounds = _proxySource.MeshRenderer.localBounds;
		}
    }

    public override void OnAttachRenderer()
    {
        base.OnAttachRenderer();
        Owner.BlendShapeWeightsChanged = true;
    }
    public override void DestroyMethod(bool destroyingWorld)
    {
        CleanupProxy();
        BoundsUpdated = null;
        if (_boundsUpdater != null)
        {
            if (!destroyingWorld && (bool)_boundsUpdater) Object.Destroy(_boundsUpdater);
            _boundsUpdater = null;
        }
        bones = null;
        base.DestroyMethod(destroyingWorld);
    }
}

public class ApplyChangesSkinnedMeshRenderer : ApplyChangesMeshRendererConnectorBase<SkinnedMeshRenderer,
    UnityEngine.SkinnedMeshRenderer>
{
    public SkinnedBounds SkinnedBounds;
    public bool BoundsChanged;
    public List<BoneMetadata> BoneMetadata;
    public List<ApproximateBoneBounds> ApproximateBounds;
    public SkinnedMeshRendererConnector Proxy;
    public UnityEngine.Bounds Bounds;

    public bool BonesChanged;
    public bool BlendShapeWeightsChanged;
    public int? BoneCount;
    public int? BlendShapeCount;

    public List<SlotConnector> Bones;
    public SlotConnector RootBone;
    public List<float> BlendShapeWeights;

    public SkinnedMeshRendererConnector Skinned => Owner as SkinnedMeshRendererConnector;

    public ApplyChangesSkinnedMeshRenderer(SkinnedMeshRendererConnector owner) : base(owner)
    {
        SkinnedBounds = owner.Owner.BoundsComputeMethod.Value;
        if (SkinnedBounds == SkinnedBounds.Static && owner.Owner.Slot.ActiveUserRoot == owner.Owner.LocalUserRoot)
            SkinnedBounds = SkinnedBounds.FastDisjointRootApproximate;

        BoundsChanged = owner.Owner.ProxyBoundsSource.WasChanged || owner.Owner.ExplicitLocalBounds.WasChanged;
        if (MeshWasChanged || Skinned._currentBoundsMethod != SkinnedBounds || BoundsChanged)
        {
            owner.Owner.ProxyBoundsSource.WasChanged = false;
            owner.Owner.ExplicitLocalBounds.WasChanged = false;
        }

        switch (SkinnedBounds)
        {
            case SkinnedBounds.Proxy:
                Proxy = owner.Owner.ProxyBoundsSource.Target?.SkinConnector as SkinnedMeshRendererConnector;
                break;
            case SkinnedBounds.Static:
                break;
            case SkinnedBounds.Explicit:
                Bounds = owner.Owner.ExplicitLocalBounds.Value.ToUnity();
                break;
            case SkinnedBounds.FastDisjointRootApproximate:
            case SkinnedBounds.MediumPerBoneApproximate:
            case SkinnedBounds.SlowRealtimeAccurate:
                if (owner.Owner.Mesh.Asset is FrooxEngine.Mesh mesh && mesh.BoneMetadata != null && mesh.ApproximateBoneBounds != null)
                {
                    BoneMetadata = new List<BoneMetadata>(mesh.BoneMetadata);
                    ApproximateBounds = new List<ApproximateBoneBounds>(mesh.ApproximateBoneBounds);
                }
                break;
        }

        BonesChanged = owner.Owner.BonesChanged;
        if (BonesChanged || MeshWasChanged)
        {
            owner.Owner.BonesChanged = false;
        }

        BlendShapeCount = owner.Owner.Mesh.Asset?.Data?.BlendShapeCount;

        BoneCount = owner.Owner.Mesh.Asset?.Data?.BoneCount;

        Bones = new List<SlotConnector>();
        foreach (var bone in owner.Owner.Bones)
        {
            if (bone?.Connector is SlotConnector slotConnector)
            {
                Bones.Add(bone?.Connector as SlotConnector);
            }
            else
            {
                Bones.Add(null);
            }
        }

        RootBone = owner.Owner.GetRootBone()?.Connector as SlotConnector;

        BlendShapeWeightsChanged = owner.Owner.BlendShapeWeightsChanged;
        if (BlendShapeWeightsChanged || MeshWasChanged)
        {
            owner.Owner.BlendShapeWeightsChanged = false;
        }

        BlendShapeWeights = new List<float>(owner.Owner.BlendShapeWeights);
    }

    public override void OnUpdateRenderer(bool instantiated)
    {
        var skinnedBounds = SkinnedBounds;
        if (MeshWasChanged || Skinned._currentBoundsMethod != skinnedBounds || BoundsChanged)
        {
            //Owner.Owner.RunSynchronously(() =>
            //{
            //    Owner.Owner.ProxyBoundsSource.WasChanged = false;
            //    Owner.Owner.ExplicitLocalBounds.WasChanged = false;
            //});
            if (skinnedBounds != SkinnedBounds.Static && skinnedBounds != SkinnedBounds.Proxy &&
                skinnedBounds != SkinnedBounds.Explicit)
            {
                if (Skinned._boundsUpdater == null)
                {
                    Skinned.LocalBoundingBoxAvailable = false;
                    Skinned._boundsUpdater = Skinned.MeshRenderer.gameObject.AddComponent<SkinBoundsUpdater>();
                    Skinned._boundsUpdater.connector = Skinned;
                }
                Skinned._boundsUpdater.boundsMethod = skinnedBounds;
                Skinned._boundsUpdater.boneMetadata = BoneMetadata;
                Skinned._boundsUpdater.approximateBounds = ApproximateBounds;
                Skinned.MeshRenderer.updateWhenOffscreen = skinnedBounds == SkinnedBounds.SlowRealtimeAccurate;
            }
            else
            {
                if (Skinned._boundsUpdater != null)
                {
                    Skinned.LocalBoundingBoxAvailable = false;
                    Skinned.MeshRenderer.updateWhenOffscreen = false;
                    Skinned.CleanupBoundsUpdater();
                }

                if (skinnedBounds == SkinnedBounds.Proxy)
                {
                    Skinned.CleanupProxy();
                    Skinned._proxySource = Proxy;
                    if (Skinned._proxySource != null)
                    {
                        Skinned._proxySource.BoundsUpdated += Skinned.ProxyBoundsUpdated;
                        Skinned.ProxyBoundsUpdated();
                    }
                }

                if (skinnedBounds == SkinnedBounds.Explicit)
                {
                    Skinned.MeshRenderer.localBounds = Bounds;
                    Skinned.LocalBoundingBoxAvailable = true;
                    Skinned.SendBoundsUpdated();
                }
            }
            Skinned._currentBoundsMethod = skinnedBounds;
        }
        if (BonesChanged || MeshWasChanged)
        {
            Owner.Owner.BonesChanged = false;
            var boneCount = BoneCount;
            var blendShapeCount = BlendShapeCount;
            var weightBonelessOverride = boneCount.GetValueOrDefault() == 0 && blendShapeCount.GetValueOrDefault() > 0;
            if (weightBonelessOverride) boneCount = 1;
            Skinned.bones = Skinned.bones.EnsureExactSize(boneCount.GetValueOrDefault());
            if (Skinned.bones != null)
            {
                if (weightBonelessOverride)
                {
                    Skinned.bones[0] = Skinned.AttachedGameObject.transform;
                }
                else
                {
                    var num7 = MathX.Min(Skinned.bones.Length, Bones.Count);
                    for (var index = 0; index < num7; index++)
                    {
                        var obj = Bones[index];
                        if (obj is null) continue;
                        Skinned.bones[index] = obj.ForceGetGameObject().transform;
                    }
                }
            }

            Skinned.MeshRenderer.bones = Skinned.bones;
            Skinned.MeshRenderer.rootBone = weightBonelessOverride
                    ? Skinned.AttachedGameObject.transform
                    : RootBone?.ForceGetGameObject().transform;
        }

        if (BlendShapeWeightsChanged || MeshWasChanged)
        {
            Owner.Owner.BlendShapeWeightsChanged = false;
            var valueOrDefault = BlendShapeCount.GetValueOrDefault();
            var index1 = 0;
            for (var index2 = MathX.Min(valueOrDefault, BlendShapeWeights.Count); index1 < index2; index1++)
            {
                Skinned.MeshRenderer.SetBlendShapeWeight(index1, BlendShapeWeights[index1]);
            }
            for (; index1 < valueOrDefault; index1++)
            {
                Skinned.MeshRenderer.SetBlendShapeWeight(index1, 0.0f);
            }
        }

        if (Skinned.ForceRecalcPerRender) Skinned.MeshRenderer.forceMatrixRecalculationPerRender = true;
        Skinned.SendBoundsUpdated();
    }
}