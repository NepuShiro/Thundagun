using System;
using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using UnityEngine;
using UnityFrooxEngineRunner;
using TextureWrapMode = FrooxEngine.TextureWrapMode;

namespace Thundagun.NewConnectors.AssetConnectors;

public class VideoTextureConnector : AssetConnector, IVideoTextureConnector, IUnityTextureProvider
{
    //TODO: implement this after the vlc update, there's no point in doing this right now when it's going to get nuked soon

    public override void Unload() { }
    public void LoadLocal(string path, string forceEngine, string mime, AssetIntegrated onReady, Action onTextureUpdated) { }
    public void LoadStream(string uri, string forceEngine, string mime, AssetIntegrated onReady, Action onTextureUpdated) { }
    public void SetTextureProperties(TextureFilterMode filterMode, int anisoLevel, TextureWrapMode wrapU, TextureWrapMode wrapV) { }
    public void SetPlaybackProperties(int? audioTrackIndex) { }
    public void Setup(World world, PlaybackCallback callback) { }
    public void AudioRead<S>(Span<S> buffer) where S : unmanaged, IAudioSample<S> { }
    public int2 Size { get; }
    public bool HasAlpha { get; }
    public float Length { get; }
    public float CurrentClockError { get; }
    public string PlaybackEngine { get; }
    public Texture UnityTexture { get; }
}