using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Elements.Core;
using FrooxEngine;
using UnityEngine;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors;

public class RenderConnector : IRenderConnector
{
    public static bool _initialized;
    public static Camera360 camera360;
    public static UnityEngine.Camera camera;
    public static RenderQueueProcessor renderQueue;
    public static int _privateLayerMask;
    public static int _hiddenLayerMask;
    
    public void Initialize(RenderManager manager)
    {
        Thundagun.CurrentPackets.Add(new InitializeRenderConnector(this));
        renderQueue.Connector = this;
    }

    public GameObject GetGameObject(Slot slot) => ((SlotConnector) slot.Connector).GeneratedGameObject;

    public Task<byte[]> Render(FrooxEngine.RenderSettings renderSettings) => renderQueue.Enqueue(renderSettings);

    public byte[] RenderImmediate(FrooxEngine.RenderSettings renderSettings)
    {
        var renderSettings1 = renderSettings;
        var texture2D = new UnityEngine.Texture2D(renderSettings1.size.x, renderSettings1.size.y,
            renderSettings1.textureFormat.ToUnity(), false);
        var temporary = UnityEngine.RenderTexture.GetTemporary(renderSettings1.size.x, renderSettings1.size.y, 24,
            RenderTextureFormat.ARGB32);
        var active = UnityEngine.RenderTexture.active;
        var dictionary = Pool.BorrowDictionary<GameObject, int>();
        var list1 = (List<GameObject>) null;
        var list2 = (List<GameObject>) null;
        var layer = LayerMask.NameToLayer("Temp");
        var num = 1 << layer;
        if (renderSettings1.excludeObjects != null && renderSettings1.excludeObjects.Count > 0)
        {
            list2 = Pool.BorrowList<GameObject>();
            foreach (var excludeObject in renderSettings1.excludeObjects)
            {
                var gameObject = GetGameObject(excludeObject);
                if (gameObject != null)
                    list2.Add(gameObject);
            }
        }

        if (renderSettings1.renderObjects != null && renderSettings1.renderObjects.Count > 0)
        {
            list1 = Pool.BorrowList<GameObject>();
            foreach (var renderObject in renderSettings1.renderObjects)
            {
                var gameObject = GetGameObject(renderObject);
                if (gameObject != null)
                    list1.Add(gameObject);
            }
        }

        if (list1 != null)
        {
            RenderHelper.SetHiearchyLayer(list1, layer, dictionary);
            RenderHelper.RestoreHiearachyLayer(list2, dictionary);
        }
        else if (list2 != null)
            RenderHelper.SetHiearchyLayer(list2, layer, dictionary);

        if (renderSettings1.fov >= 180.0)
        {
            if (list1 != null)
            {
                camera360.Camera.cullingMask = num & _hiddenLayerMask;
            }
            else
            {
                camera360.Camera.cullingMask = ~num & _hiddenLayerMask;
                if (!renderSettings1.renderPrivateUI)
                    camera360.Camera.cullingMask &= _privateLayerMask;
            }

            CameraInitializer.SetPostProcessing(camera360.Camera, renderSettings1.postProcesing, false,
                renderSettings1.screenspaceReflections);
            camera360.transform.position = renderSettings1.position.ToUnity();
            camera360.transform.rotation = renderSettings1.rotation.ToUnity();
            camera360.Camera.clearFlags = renderSettings1.clear.ToUnity();
            camera360.Camera.backgroundColor = renderSettings1.clearColor.ToUnity(ColorProfile.sRGB);
            camera360.Camera.nearClipPlane = renderSettings1.near;
            camera360.Camera.farClipPlane = renderSettings1.far;
            camera360.Render(temporary);
        }
        else
        {
            if (list1 != null)
            {
                camera.cullingMask = num & _hiddenLayerMask;
            }
            else
            {
                camera.cullingMask = ~num & _hiddenLayerMask;
                if (!renderSettings1.renderPrivateUI)
                    camera.cullingMask &= _privateLayerMask;
            }

            CameraInitializer.SetPostProcessing(camera, renderSettings1.postProcesing, false,
                renderSettings1.screenspaceReflections);
            camera.transform.position = renderSettings1.position.ToUnity();
            camera.transform.rotation = renderSettings1.rotation.ToUnity();
            camera.clearFlags = renderSettings1.clear.ToUnity();
            camera.backgroundColor = renderSettings1.clearColor.ToUnity(ColorProfile.sRGB);
            camera.nearClipPlane = renderSettings1.near;
            camera.farClipPlane = renderSettings1.far;
            camera.targetTexture = temporary;
            camera.fieldOfView = renderSettings1.fov;
            camera.orthographicSize = renderSettings1.ortographicSize;
            camera.orthographic = renderSettings1.projection == CameraProjection.Orthographic;
            camera.Render();
        }

        if (list1 != null)
            RenderHelper.RestoreHiearachyLayer(list1, dictionary);
        else if (list2 != null)
            RenderHelper.RestoreHiearachyLayer(list2, dictionary);
        Pool.Return(ref dictionary);
        if (list1 != null)
            Pool.Return(ref list1);
        if (list2 != null)
            Pool.Return(ref list2);
        UnityEngine.RenderTexture.active = temporary;
        texture2D.ReadPixels(new UnityEngine.Rect(0.0f, 0.0f, renderSettings1.size.x, renderSettings1.size.y), 0, 0,
            false);
        UnityEngine.RenderTexture.active = active;
        UnityEngine.RenderTexture.ReleaseTemporary(temporary);
        var rawTextureData = texture2D.GetRawTextureData();
        UnityEngine.Object.Destroy(texture2D);
        return rawTextureData;
    }

    public void UpdateDynamicGI(bool immediate) => DynamicGIManager.ScheduleDynamicGIUpdate(immediate);

    public void RegisterCamera(FrooxEngine.Camera camera) => RegisterUnityCamera(camera.ToUnity());

    public static void RegisterUnityCamera(UnityEngine.Camera camera) =>
        camera.gameObject.AddComponent<ShaderCameraProperties>();

    public void SetupPostProcessing(FrooxEngine.Camera camera, bool motionBlur, bool screenspaceReflections) =>
        CameraInitializer.SetupPostProcessing(camera.ToUnity(), motionBlur, screenspaceReflections);

    public void RemovePostProcessing(FrooxEngine.Camera camera) =>
        CameraInitializer.RemovePostProcessing(camera.ToUnity());
}

public class InitializeRenderConnector : UpdatePacket<RenderConnector>
{
    public InitializeRenderConnector(RenderConnector owner) : base(owner)
    {
    }

    public override void Update()
    {
        if (RenderConnector._initialized)
            return;
        RenderConnector._initialized = true;
        var gameObject1 = new GameObject("CaptureCam");
        var gameObject2 = new GameObject("CaptureCam360");
        RenderConnector.camera = gameObject1.AddComponent<UnityEngine.Camera>();
        gameObject1.AddComponent<ShaderCameraProperties>();
        RenderConnector.camera.stereoTargetEye = StereoTargetEyeMask.None;
        RenderConnector.camera.enabled = false;
        RenderConnector.camera.nearClipPlane = 0.05f;
        var settings = new CameraSettings()
        {
            IsSingleCapture = true,
            SetupPostProcessing = true
        };
        CameraInitializer.SetupCamera(RenderConnector.camera, settings);
        RenderConnector.camera360 = gameObject2.AddComponent<Camera360>();
        RenderConnector.camera360.DisplayCamera.enabled = false;
        RenderConnector.camera360.Camera.nearClipPlane = 0.05f;
        RenderConnector.camera360.projectionMaterial = Resources.Load<UnityEngine.Material>("EquirectangularProjection");
        CameraInitializer.SetupCamera(RenderConnector.camera360.Camera, settings);
        RenderConnector.camera360.Camera.gameObject.AddComponent<ShaderCameraProperties>();
        RenderConnector._privateLayerMask = ~LayerMask.GetMask("Private");
        RenderConnector._hiddenLayerMask = ~LayerMask.GetMask("Hidden", "Overlay");
        RenderHelper.RegisterCamera = RenderConnector.RegisterUnityCamera;
        RenderConnector.renderQueue = new GameObject("RenderQueue").AddComponent<RenderQueueProcessor>();
    }
}