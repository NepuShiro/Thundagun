using System;
using System.Collections.Generic;
using Elements.Core;
using FrooxEngine;
using UnityEngine;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors.ComponentConnectors;

public class CameraRenderEx : MonoBehaviour
{
    public Engine Engine;
    public CameraConnector Owner;
    public float NearClip;
    public float FarClip;
    public bool DoubleBuffer;
    public bool UseTransformScale;
    public float OrthographicSize;
    public int RenderToDisplay;
    public bool RenderShadows;
    public UnityEngine.Camera Camera;
    public UnityEngine.RenderTexture Texture;
    public List<GameObject> SelectiveRender = new();
    public List<GameObject> ExcludeRender = new();
    private UnityEngine.RenderTexture _prevTexture;
    private UnityEngine.Rect? _prevRect;
    private ShadowQuality? _prevShadowQuality;
    private Dictionary<GameObject, int> _previousLayers;
    private RenderingContext? _prevContext;

    internal void Deinitialize() => Engine = null;

    private void OnDestroy() => Deinitialize();

    private void OnPreCull()
    {
        try
        {
            if (!RenderShadows)
            {
                _prevShadowQuality = QualitySettings.shadows;
                QualitySettings.shadows = ShadowQuality.Disable;
            }

            if (SelectiveRender.Count > 0)
            {
                var layer = LayerMask.NameToLayer("Temp");
                _previousLayers ??= new Dictionary<GameObject, int>();
                RenderHelper.SetHiearchyLayer(SelectiveRender, layer, _previousLayers);
                RenderHelper.RestoreHiearachyLayer(ExcludeRender, _previousLayers);
            }
            else if (ExcludeRender.Count > 0)
            {
                var layer = LayerMask.NameToLayer("Temp");
                _previousLayers ??= new Dictionary<GameObject, int>();
                RenderHelper.SetHiearchyLayer(ExcludeRender, layer, _previousLayers);
            }

            _prevContext = RenderHelper.CurrentRenderingContext;
            RenderHelper.BeginRenderContext(RenderingContext.Camera);
        }
        catch (Exception ex)
        {
            UniLog.Error("Exception in Camera OnPreCull\n" + DebugManager.PreprocessException(ex));
        }
    }

    private void OnPreRender()
    {
        try
        {
            var num1 = MathX.Clamp(
                MathX.FilterInvalid(
                    !UseTransformScale ? 1f : MathX.AvgComponent(Camera.transform.lossyScale.ToEngine())), 1E-05f,
                1000000f);
            var nearClip = MathX.Clamp(NearClip * num1, 0.0001f, 1000000f);
            Camera.orthographicSize = MathX.Clamp(OrthographicSize * num1, 1E-06f, 1000000f);
            ;
            Camera.nearClipPlane = nearClip;
            Camera.farClipPlane = MathX.Clamp(FarClip * num1, MathX.Max(0.0001f, nearClip + 0.0001f), 1000000f);
            if (Texture != null)
                Camera.targetTexture = Texture;
            if (!DoubleBuffer || Camera.targetTexture == null)
            {
                return;
            }
            var descriptor = Camera.targetTexture.descriptor;
            if (Camera.rect != new UnityEngine.Rect(0.0f, 0.0f, 1f, 1f))
            {
                var rect = Camera.rect;
                descriptor.height = (int)(rect.height * descriptor.height);
                descriptor.width = (int)(rect.width * descriptor.width);
                _prevRect = rect;
                rect = new UnityEngine.Rect(0.0f, 0.0f, 1f, 1f);
                Camera.rect = rect;
            }

            var temporary = UnityEngine.RenderTexture.GetTemporary(descriptor);
            _prevTexture = Camera.targetTexture;
            Camera.targetTexture = temporary;
        }
        catch (Exception ex)
        {
            UniLog.Error("Exception in Camera OnPreRender\n" + DebugManager.PreprocessException(ex));
        }
    }

    private void OnPostRender()
    {
        try
        {
            Engine.CameraRendered();
            if (_prevShadowQuality.HasValue)
            {
                QualitySettings.shadows = _prevShadowQuality.Value;
                _prevShadowQuality = new ShadowQuality?();
            }

            if (_previousLayers is { Count: > 0 })
            {
                RenderHelper.RestoreLayers(_previousLayers);
                _previousLayers.Clear();
            }

            if (_prevContext.HasValue)
                RenderHelper.BeginRenderContext(_prevContext.Value);
            if (!DoubleBuffer || Camera.targetTexture == null)
            {
                return;
            }
            if (_prevRect.HasValue)
            {
                var texture = Camera.targetTexture;
                Graphics.CopyTexture(texture, 0, 0, 0, 0, texture.width,
                    texture.height, _prevTexture, 0, 0,
                    (int)(_prevRect.Value.x * _prevTexture.width),
                    (int)(_prevRect.Value.y * _prevTexture.height));
                Camera.rect = _prevRect.Value;
                _prevRect = new UnityEngine.Rect?();
            }
            else
                Graphics.CopyTexture(Camera.targetTexture, _prevTexture);

            var targetTexture = Camera.targetTexture;
            Camera.targetTexture = _prevTexture;
            _prevTexture = null;
            UnityEngine.RenderTexture.ReleaseTemporary(targetTexture);
        }
        catch (Exception ex)
        {
            UniLog.Error("Exception in Camera OnPreCull\n" + DebugManager.PreprocessException(ex));
        }
    }
}