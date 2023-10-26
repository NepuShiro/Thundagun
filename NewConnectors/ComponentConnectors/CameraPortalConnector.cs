using Elements.Core;
using FrooxEngine;
using UnityEngine;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors.ComponentConnectors;

public class CameraPortalConnector : ComponentConnectorSingle<FrooxEngine.CameraPortal>
{
	public CameraPortal currentPortal;
	public static int LayerMask;

	public override void ApplyChanges() => Thundagun.QueuePacket(new ApplyChangesCameraPortalConnector(this));

	public override void DestroyMethod(bool destroyingWorld)
	{
		Cleanup(destroyingWorld);
		base.DestroyMethod(destroyingWorld);
	}

	public void Cleanup(bool destroyingWorld)
	{
		if (currentPortal != null)
		{
			currentPortal.Engine = null;
			if (!destroyingWorld && currentPortal) Object.Destroy(currentPortal);
		}
		currentPortal = null;
	}
}

public class ApplyChangesCameraPortalConnector : UpdatePacket<CameraPortalConnector>
{
	public MeshRendererConnector MeshRenderer;
	public Engine Engine;
	public AssetConnectors.RenderTextureConnector ReflectionTexture;
	public Vector3 Normal;
	public float ClipPlaneOffset;
	public CameraClearFlags? OverrideClearFlag;
	public float? OverrideFarClip;
	public float? OverrideNearClip;
	public bool DisablePixelLights;
	public bool DisableShadows;
	public CameraPortal.Mode RenderMode;
	public Matrix4x4 PortalTransform;
	public Vector3 PortalPlanePosition;
	public Vector3 PortalPlaneNormal;

	public ApplyChangesCameraPortalConnector(CameraPortalConnector owner) : base(owner)
	{
		var target = owner.Owner.Renderer.Target;
		MeshRenderer = target?.Connector as MeshRendererConnector;
		Engine = owner.Owner.Engine;
		
		if (target?.Mesh.Target != null && MeshRenderer == null && target.IsChangeDirty) owner.Owner.MarkChangeDirty();

		var v = MathX.FilterInvalid(owner.Owner.PlaneNormal);
		if (v.SqrMagnitude < 1E-06f) v = float3.Forward;
		var clipPlaneOffset = MathX.FilterInvalid(owner.Owner.PlaneOffset);
		ReflectionTexture = owner.Owner.ReflectionTexture?.Asset?.Connector as AssetConnectors.RenderTextureConnector;
		Normal = v.ToUnity();
		ClipPlaneOffset = clipPlaneOffset;
		OverrideClearFlag = owner.Owner.OverrideClear.Value?.ToUnity();
		OverrideFarClip = owner.Owner.OverrideFarClip.Value;
		OverrideNearClip = owner.Owner.OverrideNearClip.Value;
		DisablePixelLights = owner.Owner.DisablePerPixelLights.Value;
		DisableShadows = owner.Owner.DisableShadows.Value;
		var value = owner.Owner.RenderMode.Value;
		if (value != 0 && value == FrooxEngine.CameraPortal.Mode.Portal)
		{
			RenderMode = CameraPortal.Mode.Portal;
			owner.Owner.GetPortalData(out var matrix, out var portalPlanePosition, out var portalPlaneNormal);
			PortalTransform = matrix.ToUnity();
			PortalPlanePosition = portalPlanePosition.ToUnity();
			PortalPlaneNormal = portalPlaneNormal.ToUnity();
		}
		else RenderMode = CameraPortal.Mode.Mirror;
	}

	public override void Update()
	{
		var gameObject = MeshRenderer?.MeshRenderer?.gameObject;
		if (gameObject != Owner.currentPortal?.gameObject)
		{
			Owner.Cleanup(destroyingWorld: false);
			if (gameObject != null)
			{
				Owner.currentPortal = gameObject.AddComponent<CameraPortal>();
				Owner.currentPortal.ReflectLayers = CameraPortalConnector.LayerMask;
				Owner.currentPortal.Engine = Engine;
			}
		}
		if (Owner.currentPortal == null) return;
		
		Owner.currentPortal.ReflectionTexture = ReflectionTexture?.RenderTexture;
		Owner.currentPortal.Normal = Normal;
		Owner.currentPortal.ClipPlaneOffset = ClipPlaneOffset;
		Owner.currentPortal.OverrideClearFlag = OverrideClearFlag;
		Owner.currentPortal.OverrideFarClip = OverrideFarClip;
		Owner.currentPortal.OverrideNearClip = OverrideNearClip;
		Owner.currentPortal.DisablePixelLights = DisablePixelLights;
		Owner.currentPortal.DisableShadows = DisableShadows;

		if (RenderMode == CameraPortal.Mode.Mirror)
		{
			Owner.currentPortal.RenderMode = CameraPortal.Mode.Mirror;
		}
		else
		{
			Owner.currentPortal.RenderMode = CameraPortal.Mode.Portal;
			Owner.currentPortal.PortalTransform = PortalTransform;
			Owner.currentPortal.PortalPlanePosition = PortalPlanePosition;
			Owner.currentPortal.PortalPlaneNormal = PortalPlaneNormal;
		}
	}
}