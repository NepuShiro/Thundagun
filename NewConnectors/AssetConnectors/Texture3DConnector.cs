using System;
using System.Collections;
using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using UnityEngine;
using UnityFrooxEngineRunner;
using Object = UnityEngine.Object;
using Texture3D = UnityEngine.Texture3D;
using TextureFormat = UnityEngine.TextureFormat;
using TextureWrapMode = FrooxEngine.TextureWrapMode;

namespace Thundagun.NewConnectors.AssetConnectors;

  public class Texture3DConnector :
    AssetConnector, ITexture2DConnector, IAssetConnector, ICubemapConnector, ITexture3DConnector, IUnityTextureProvider
{
    public Texture3D UnityTexture3D { get; private set; }

    public Texture UnityTexture => UnityTexture3D;

    public void SetTexture3DData(Bitmap3D data, Texture3DUploadHint hint, AssetIntegrated onSet)
    {
      UniLog.Log($"Texture's color profile is: {data.Profile}");
      var colors = new Color[data.TotalPixels];
      for (var index = 0; index < colors.Length; index++)
      {
        var colorX = new colorX(data.DecodePixel(index), data.Profile);
        colors[index] = colorX.ToProfile(ColorProfile.Linear).ToUnity();
      }
      UnityAssetIntegrator.EnqueueProcessing(SetTexture3DData(data, t => t.SetPixels(colors), onSet), Asset.HighPriorityIntegration);
    }

    public void UpdateProperties(
      TextureWrapMode wrapU,
      TextureWrapMode wrapV,
      TextureWrapMode wrapW, 
      AssetIntegrated onSet)
    {
      UnityAssetIntegrator.EnqueueProcessing(() => SetTexture3DProperties(wrapU, wrapV, wrapW, onSet), true);
    }

    public override void Unload() => UnityAssetIntegrator.EnqueueProcessing(Destroy, true);

    private void Destroy()
    {
      if (UnityTexture3D != null)
        Object.Destroy(UnityTexture3D); 
      //this entire connector was 'remade' purely so that i could remove the DestroyImmediate from here
      UnityTexture3D = null;
    }

    private void SetTexture3DProperties(
      TextureWrapMode wrapU,
      TextureWrapMode wrapV,
      TextureWrapMode wrapW, 
      ColorProfile profile,
      AssetIntegrated onSet)
    {
      if (UnityTexture3D != null)
      {
        UnityTexture3D.wrapModeU = wrapU.ToUnity();
        UnityTexture3D.wrapModeV = wrapV.ToUnity();
        UnityTexture3D.wrapModeW = wrapW.ToUnity();
      }
      onSet(false);
    }

    private IEnumerator SetTexture3DData(
      Bitmap3D data,
      Action<Texture3D> setData,
      AssetIntegrated onDone)
    {
      var textureFormat = data.Format.ToUnity();
      Destroy();
      if (data.Profile != ColorProfile.Linear)
        textureFormat = TextureFormat.RGBAHalf;
      UnityTexture3D = new Texture3D(data.Size.x, data.Size.y, data.Size.z, textureFormat, data.HasMipMaps);
      yield return null;
      setData(UnityTexture3D);
      yield return null;
      UnityTexture3D.Apply(false, true);
      onDone(true);
      Engine.TextureUpdated();
    }
  }