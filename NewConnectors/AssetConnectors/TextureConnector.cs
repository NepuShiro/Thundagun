using System;
using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using UnityEngine;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors.AssetConnectors;

public class TextureConnector :
    AssetConnector,
    ITexture2DConnector,
    ICubemapConnector,
    IUnityTextureProvider
{
    private UnityEngine.Texture2D _unityTexture2D;
    private UnityEngine.Cubemap _unityCubemap;
    public const int TIMESLICE_RESOLUTION = 65536;
    private TextureFilterMode _filterMode;
    private int _anisoLevel;
    private FrooxEngine.TextureWrapMode _wrapU;
    private FrooxEngine.TextureWrapMode _wrapV;
    private float _mipmapBias;
    private AssetIntegrated _onPropertiesSet;
    private bool _texturePropertiesDirty;

    public UnityEngine.Texture2D UnityTexture2D => _unityTexture2D;

    public UnityEngine.Cubemap UnityCubemap => _unityCubemap;

    Texture IUnityTextureProvider.UnityTexture => (Texture) UnityTexture2D ?? UnityCubemap;

    public void SetTexture2DFormat(
        int width,
        int height,
        int mips,
        Elements.Assets.TextureFormat format,
        ColorProfile profile,
        AssetIntegrated onDone)
    {
        SetTextureFormat(new TextureFormatData
        {
            Type = TextureType.Texture2D,
            Width = width,
            Height = height,
            Mips = mips,
            Format = format,
            OnDone = onDone,
            Profile = profile
        });
    }

    public void SetCubemapFormat(
        int size,
        int mips,
        Elements.Assets.TextureFormat format,
        ColorProfile profile,
        AssetIntegrated onDone)
    {
        SetTextureFormat(new TextureFormatData
        {
            Type = TextureType.Cubemap,
            Width = size,
            Height = size,
            Mips = mips,
            Format = format,
            OnDone = onDone,
            Profile = profile
        });
    }

    private void SetTextureFormat(TextureFormatData format) => UnityAssetIntegrator.EnqueueProcessing(() => SetTextureFormatUnity(format), true);

    public void SetTexture2DData(Bitmap2D data, int startMipLevel, TextureUploadHint hint, AssetIntegrated onSet) =>
        SetTextureData(new TextureUploadData
        {
            //Bitmap2D = data,
            StartMip = startMipLevel,
            Hint = hint,
            OnDone = onSet,
            Format = data.Format,
            RawData = data.RawData,
        });

    public void SetCubemapData(BitmapCube data, int startMipLevel, AssetIntegrated onSet) => 
        SetTextureData(new TextureUploadData
        {
            //BitmapCube = data,
            StartMip = startMipLevel,
            OnDone = onSet,
            Format = data.Format,
            RawData = data.RawData,
        });

    private void SetTextureData(TextureUploadData data) =>
        UnityAssetIntegrator.EnqueueProcessing(() => UploadTextureDataUnity(data),
            Asset.HighPriorityIntegration);

    //TODO: separate this into a packet, this is technically thread safe but it can also change mid-frame
    public void SetTexture2DProperties(
        TextureFilterMode filterMode,
        int anisoLevel,
        FrooxEngine.TextureWrapMode wrapU,
        FrooxEngine.TextureWrapMode wrapV,
        float mipmapBias,
        AssetIntegrated onSet)
    {
        _texturePropertiesDirty = true;
        _filterMode = filterMode;
        _anisoLevel = anisoLevel;
        _wrapU = wrapU;
        _wrapV = wrapV;
        _mipmapBias = mipmapBias;
        _onPropertiesSet = onSet;
        if (onSet == null)
            return;
        UnityAssetIntegrator.EnqueueProcessing(UpdateTextureProperties, Asset.HighPriorityIntegration);
    }

    public void SetCubemapProperties(
        TextureFilterMode filterMode,
        int anisoLevel,
        float mipmapBias,
        AssetIntegrated onSet)
    {
        _texturePropertiesDirty = true;
        _filterMode = filterMode;
        _anisoLevel = anisoLevel;
        _mipmapBias = mipmapBias;
        _onPropertiesSet = onSet;
        if (onSet == null)
            return;
        UnityAssetIntegrator.EnqueueProcessing(UpdateTextureProperties, Asset.HighPriorityIntegration);
    }

    public override void Unload() => UnityAssetIntegrator.EnqueueProcessing(Destroy, true);

    private void Destroy()
    {
        if (_unityTexture2D) UnityEngine.Object.Destroy(_unityTexture2D);
        _unityTexture2D = null;
    }

    private void AssignTextureProperties()
    {
        if (!_texturePropertiesDirty)
            return;
        _texturePropertiesDirty = false;
        if (_unityTexture2D != null)
        {
            if (_filterMode == TextureFilterMode.Anisotropic)
            {
                _unityTexture2D.filterMode = FilterMode.Trilinear;
                _unityTexture2D.anisoLevel = _anisoLevel;
            }
            else
            {
                _unityTexture2D.filterMode = _filterMode.ToUnity();
                _unityTexture2D.anisoLevel = 0;
            }

            _unityTexture2D.wrapModeU = _wrapU.ToUnity();
            _unityTexture2D.wrapModeV = _wrapV.ToUnity();
            _unityTexture2D.mipMapBias = _mipmapBias;
        }

        if (_unityCubemap != null)
        {
            if (_filterMode == TextureFilterMode.Anisotropic)
            {
                _unityCubemap.filterMode = FilterMode.Trilinear;
                _unityCubemap.anisoLevel = _anisoLevel;
            }
            else
            {
                _unityCubemap.filterMode = _filterMode.ToUnity();
                _unityCubemap.anisoLevel = 0;
            }

            _unityCubemap.mipMapBias = _mipmapBias;
        }
    }

    private void UpdateTextureProperties()
    {
        AssignTextureProperties();
        var onPropertiesSet = _onPropertiesSet;
        _onPropertiesSet = null;
        onPropertiesSet(false);
    }

    private void SetTextureFormatUnity(TextureFormatData format)
    {
        var unity = format.Format.ToUnity();
        var environmentInstanceChanged = false;
        if (_unityTexture2D == null ||
            _unityTexture2D.width != format.Width || _unityTexture2D.height != format.Height ||
            _unityTexture2D.format != unity || _unityTexture2D.mipmapCount > 1 != format.Mips > 1)
        {
            Destroy();
            _unityTexture2D = new UnityEngine.Texture2D(format.Width, format.Height, unity, format.Mips > 1);
            environmentInstanceChanged = true;
        }

        AssignTextureProperties();
        format.OnDone(environmentInstanceChanged);
    }

    private void UploadTextureDataUnity(TextureUploadData data)
    {
        var size = new int2(_unityTexture2D.width, _unityTexture2D.height);
        var num = 0;
        for (var index = 0; index < data.StartMip; ++index)
        {
            var alignedSize = Bitmap2DBase.AlignSize(in size, data.Format);
            num += alignedSize.x * alignedSize.y;
            size /= 2;
            size = MathX.Max(size, 1);
        }
        var bytes = (int) MathX.BitsToBytes(num * data.Format.GetBitsPerPixel());
        var rawTextureData = _unityTexture2D.GetRawTextureData<byte>();
        for (var index = 0; index < data.RawData.Length; ++index)
            rawTextureData[index/* + bytes*/] = data.RawData[index];
        if (data.StartMip == 0)
        {
            _unityTexture2D?.Apply(false, !data.Hint.readable);
            _unityCubemap?.Apply(false, !data.Hint.readable);
            Engine.TextureUpdated();
        }

        data.OnDone(false);
    }

    private enum TextureType
    {
        Texture2D,
        Cubemap,
    }

    private class TextureFormatData
    {
        public TextureType Type;
        public int Width;
        public int Height;
        public int Mips;
        public Elements.Assets.TextureFormat Format;
        public AssetIntegrated OnDone;
        public ColorProfile Profile;

        public int ArraySize
        {
            get
            {
                return Type switch
                {
                    TextureType.Texture2D => 1,
                    TextureType.Cubemap => 6,
                    _ => throw new Exception("Invalid texture type: " + Type)
                };
            }
        }
    }

    private class TextureUploadData
    {
        //public Bitmap2D Bitmap2D;
        //public BitmapCube BitmapCube;
        public int StartMip;
        public TextureUploadHint Hint;
        public AssetIntegrated OnDone;
        public Elements.Assets.TextureFormat Format;
        public byte[] RawData;

        //public Bitmap Bitmap => (Bitmap) Bitmap2D ?? BitmapCube;

        //public Elements.Assets.TextureFormat Format => Bitmap.Format;

        /*
        public int2 FaceSize
        {
            get
            {
                if (Bitmap2D != null) return Bitmap2D.Size;
                return BitmapCube?.Size ?? int2.Zero;
            }
        }

        public int2 MipMapSize(int mip) => 
            Bitmap2D?.MipMapSize(mip) ?? 
            (BitmapCube?.MipMapSize(mip) ?? int2.Zero);

        public int ElementCount
        {
            get
            {
                if (Bitmap2D != null)
                    return 1;
                if (BitmapCube != null)
                    return 6;
                throw new Exception("Invalid state, must have either Bitmap2D or BitmapCUBE");
            }
        }

        public int PixelStart(int x, int y, int mip, int face) => 
            Bitmap2D?.PixelStart(x, y, mip) ??
            BitmapCube.PixelStart(x, y, (BitmapCube.Face) face, mip);

        public void ConvertTo(Elements.Assets.TextureFormat format)
        {
            Bitmap2D = Bitmap2D?.ConvertTo(format);
            BitmapCube = BitmapCube?.ConvertTo(format);
        }
        */
    }
}