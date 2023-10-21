using System.Collections.Generic;
using Elements.Core;
using FrooxEngine;
using UnityEngine;
using UnityFrooxEngineRunner;
using Rect = UnityEngine.Rect;
using RenderTexture = UnityEngine.RenderTexture;

namespace Thundagun.NewConnectors.ComponentConnectors;

public class CameraConnector : ComponentConnector<FrooxEngine.Camera>
{
    public bool PostprocessingSetup;
    public bool ScreenspaceReflections;
    public bool MotionBlur;
    public GameObject CameraGo;
    public CameraRenderEx RenderEx;
    public static int LayerMask;
    public static int PrivateLayerMask;
    public UnityEngine.Camera UnityCamera { get; set; }

    public override void Initialize()
    {
        Thundagun.CurrentPackets.Add(new InitializeCameraConnector(this));
    }

    public override void ApplyChanges()
    {
        UnityCamera.orthographic = Owner.Projection.Value == CameraProjection.Orthographic;
        UnityCamera.fieldOfView = Owner.FieldOfView;
        UnityCamera.orthographicSize = Owner.OrthographicSize;
        RenderEx.OrthographicSize = Owner.OrthographicSize;
        RenderEx.UseTransformScale = Owner.UseTransformScale;
        RenderEx.NearClip = Owner.NearClipping;
        RenderEx.FarClip = Owner.FarClipping;
        UnityCamera.clearFlags = Owner.Clear.Value.ToUnity();
        UnityCamera.backgroundColor = Owner.ClearColor.Value.ToUnity(ColorProfile.sRGB);
        UnityCamera.rect = Owner.Viewport.Value.ToUnity();
        UnityCamera.depth = Owner.Depth.Value;
        UnityCamera.renderingPath = Owner.ForwardOnly.Value ? RenderingPath.Forward : RenderingPath.UsePlayerSettings;
        RenderEx.RenderShadows = Owner.RenderShadows.Value;
        if (Owner.Postprocessing != PostprocessingSetup ||
            Owner.ScreenSpaceReflections != ScreenspaceReflections || Owner.MotionBlur != MotionBlur)
        {
            UnityCamera.targetTexture = null;
            PostprocessingSetup = Owner.Postprocessing;
            ScreenspaceReflections = Owner.ScreenSpaceReflections;
            MotionBlur = Owner.MotionBlur;
            if (Owner.Postprocessing) Owner.World.Render.Connector.SetupPostProcessing(Owner, MotionBlur, ScreenspaceReflections);
            else Owner.World.Render.Connector.RemovePostProcessing(Owner);
        }

        RenderEx.Texture = Owner.RenderTexture.Asset.GetUnity();
        RenderEx.DoubleBuffer = Owner.DoubleBuffered.Value && !(bool) Owner.Postprocessing;
        RenderEx.SelectiveRender.Clear();
        RenderEx.ExcludeRender.Clear();
        Helper.ConvertSlots(Owner.SelectiveRender, RenderEx.SelectiveRender);
        Helper.ConvertSlots(Owner.ExcludeRender, RenderEx.ExcludeRender);
        UnityCamera.cullingMask = RenderEx.SelectiveRender.Count <= 0
            ? (Owner.RenderPrivateUI ? PrivateLayerMask : LayerMask)
            : 1 << UnityEngine.LayerMask.NameToLayer("Temp");
        UnityCamera.targetTexture = RenderEx.Texture;
        UnityCamera.enabled = UnityCamera.targetTexture != null && Owner.Enabled && Owner.Slot.IsActive;
    }

    public override void DestroyMethod(bool destroyingWorld)
    {
        RenderEx?.Deinitialize();
        if (!destroyingWorld && (bool) (Object) CameraGo)
            Object.Destroy(CameraGo);
        base.DestroyMethod(destroyingWorld);
    }
}

public class InitializeCameraConnector : UpdatePacket<CameraConnector>
{
    public IRenderConnector RenderConnector;
    
    public InitializeCameraConnector(CameraConnector owner) : base(owner) => RenderConnector = owner.Owner.World.Render.Connector;

    public override void Update()
    {
        Owner.CameraGo = new GameObject("");
        Owner.CameraGo.transform.SetParent(Owner.AttachedGameObject.transform, false);
        Owner.UnityCamera = Owner.CameraGo.AddComponent<UnityEngine.Camera>();
        Owner.UnityCamera.allowHDR = true;
        Owner.UnityCamera.stereoTargetEye = StereoTargetEyeMask.None;
        Owner.RenderEx = Owner.CameraGo.AddComponent<CameraRenderEx>();
        Owner.RenderEx.Camera = Owner.UnityCamera;
        Owner.RenderEx.Engine = Owner.Engine;
        Owner.RenderEx.Owner = Owner;
        if (CameraConnector.LayerMask == 0) CameraConnector.LayerMask = RenderHelper.PUBLIC_RENDER_MASK;
        if (CameraConnector.PrivateLayerMask == 0) CameraConnector.PrivateLayerMask = RenderHelper.PRIVATE_RENDER_MASK;
        if (Owner.Owner is not null && !Owner.Owner.IsDestroyed) RenderConnector.RegisterCamera(Owner.Owner);
    }
}

public class ApplyChangesCameraConnector : UpdatePacket<CameraConnector>
{
    public bool Orthographic;
    public float FieldOfView;
    public float OrthographicSize;
    public bool UseTransformScale;
    public float NearClip;
    public float FarClip;
    public CameraClearFlags ClearFlags;
    public Color BackgroundColor;
    public Rect Rect;
    public float Depth;
    public RenderingPath RenderingPath;
    public bool RenderShadows;
    public IRenderConnector RenderConnector;
    public bool SetupPostProcessing;
    public bool RemovePostProcessing;
    public RenderTexture Texture;
    public bool DoubleBuffer;
    public List<GameObject> SelectiveRender;
    public List<GameObject> ExcludeRender;
    
    public ApplyChangesCameraConnector(CameraConnector owner) : base(owner)
    {
        Orthographic = owner.Owner.Projection.Value == CameraProjection.Orthographic;
        FieldOfView = owner.Owner.FieldOfView;
        OrthographicSize = owner.Owner.OrthographicSize;
        UseTransformScale = owner.Owner.UseTransformScale;
        NearClip = owner.Owner.NearClipping;
        FarClip = owner.Owner.FarClipping;
        ClearFlags = owner.Owner.Clear.Value.ToUnity();
        BackgroundColor = owner.Owner.ClearColor.Value.ToUnity(ColorProfile.sRGB);
        Rect = owner.Owner.Viewport.Value.ToUnity();
        Depth = owner.Owner.Depth.Value;
        RenderingPath = owner.Owner.ForwardOnly.Value ? RenderingPath.Forward : RenderingPath.UsePlayerSettings;
        RenderShadows = owner.Owner.RenderShadows.Value;
        
        if (owner.Owner.Postprocessing != owner.PostprocessingSetup ||
            owner.Owner.ScreenSpaceReflections != owner.ScreenspaceReflections || 
            owner.Owner.MotionBlur != owner.MotionBlur)
        {
            RenderConnector = owner.Owner.World.Render.Connector;
            owner.PostprocessingSetup = owner.Owner.Postprocessing;
            owner.ScreenspaceReflections = owner.Owner.ScreenSpaceReflections;
            owner.MotionBlur = owner.Owner.MotionBlur;
            if (owner.Owner.Postprocessing) SetupPostProcessing = true;
            else RemovePostProcessing = true;
        }
        
        Texture = owner.Owner.RenderTexture.Asset.GetUnity();
        DoubleBuffer = owner.Owner.DoubleBuffered.Value;
        
        
    }

    public override void Update()
    {
        
        
        if (SetupPostProcessing || RemovePostProcessing)
        {
            Owner.UnityCamera.targetTexture = null;
            if (!Owner.Owner.IsDestroyed)
            {
                if (SetupPostProcessing) RenderConnector.SetupPostProcessing(Owner.Owner, Owner.MotionBlur, Owner.ScreenspaceReflections);
                else RenderConnector.RemovePostProcessing(Owner.Owner);
            }
        }
    }
}