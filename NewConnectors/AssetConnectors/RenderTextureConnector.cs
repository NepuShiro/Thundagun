using System;
using Elements.Core;
using FrooxEngine;
using UnityEngine;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors.AssetConnectors;

public class RenderTextureConnector :
    AssetConnector,
    IRenderTextureConnector,
    IAssetConnector,
    IUnityTextureProvider
{
    public int2 Size { get; private set; }

    public bool HasAlpha { get; private set; }

    public UnityEngine.RenderTexture RenderTexture { get; private set; }

    public Texture UnityTexture => RenderTexture;

    public override void Unload()
    {
        UnityAssetIntegrator.EnqueueProcessing(() =>
        {
            var _tex = RenderTexture;
            RenderTexture = null;
            if (!(_tex != null))
                return;
            if (!_tex)
                return;
            UnityEngine.Object.Destroy(_tex);
        }, true);
    }

    public void Update(
        int2 size,
        int depth,
        TextureFilterMode filterMode,
        int anisoLevel,
        FrooxEngine.TextureWrapMode wrapU,
        FrooxEngine.TextureWrapMode wrapV,
        Action onUpdated)
    {
        UnityAssetIntegrator.EnqueueProcessing(() =>
        {
            size = MathX.Clamp(in size, 4, 8192);
            Size = size;
            Unload();
            RenderTexture = new UnityEngine.RenderTexture(Size.x, Size.y, depth, RenderTextureFormat.ARGBHalf);
            RenderTexture.Create();
            if (filterMode == TextureFilterMode.Anisotropic)
            {
                RenderTexture.filterMode = FilterMode.Trilinear;
                RenderTexture.anisoLevel = anisoLevel;
            }
            else
            {
                RenderTexture.filterMode = filterMode.ToUnity();
                RenderTexture.anisoLevel = 0;
            }

            RenderTexture.wrapModeU = wrapU.ToUnity();
            RenderTexture.wrapModeV = wrapV.ToUnity();
            onUpdated();
        }, false);
    }
}