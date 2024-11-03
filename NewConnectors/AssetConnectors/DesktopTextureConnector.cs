using System;
using Elements.Core;
using FrooxEngine;
using UnityEngine;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors.AssetConnectors;

public class DesktopTextureConnector :
    AssetConnector,
    IDesktopTextureConnector,
    IUnityTextureProvider
{
    private IDisplayTextureSource _lastSource;
    private Action _onUpdated;

    public int2 Size
    {
        get
        {
            var unityTexture1 = UnityTexture;
            var width = unityTexture1 != null ? unityTexture1.width : 0;
            var unityTexture2 = UnityTexture;
            var height = unityTexture2 != null ? unityTexture2.height : 0;
            return new int2(width, height);
        }
    }

    public Texture UnityTexture => (_lastSource as IUnityTextureProvider)?.UnityTexture;

    public void Update(int index, Action onUpdated)
    {
        var screen = Engine.InputInterface.TryGetDisplay(index) as IDisplayTextureSource;
        if (screen == _lastSource)
            return;
        FreeSource();
        _onUpdated = onUpdated;
        if (screen != null)
            UnityAssetIntegrator.EnqueueProcessing(() =>
            {
                _lastSource = screen;
                screen.RegisterRequest(TextureUpdated);
                _onUpdated();
            }, true);
        else
            onUpdated();
    }

    public override void Unload() => FreeSource();

    private void FreeSource()
    {
        var source = _lastSource;
        _lastSource = null;
        _onUpdated = null;
        if (source == null)
            return;
        UnityAssetIntegrator.EnqueueProcessing(() => source.UnregisterRequest(TextureUpdated), true);
    }

    private void TextureUpdated()
    {
        var onUpdated = _onUpdated;
        onUpdated?.Invoke();
    }
}