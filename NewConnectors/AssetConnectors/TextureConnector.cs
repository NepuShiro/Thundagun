using System;
using System.Collections;
using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using UnityEngine;
using UnityFrooxEngineRunner;
using NativeGraphics.NET;
using ResoniteModLoader;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using UnityEngine.Rendering;
using Unity.Collections;
using UnityEngine.Experimental.Rendering;

namespace Thundagun.NewConnectors.AssetConnectors;

public class TextureConnector :
    AssetConnector,
    ITexture2DConnector,
    ICubemapConnector,
    IUnityTextureProvider, ITexture3DConnector
{
    private ColorProfile? _targetProfile;
    private UnityEngine.Texture2D _unityTexture2D;
    private UnityEngine.Cubemap _unityCubemap;
    private UnityEngine.Texture3D _unityTexture3D;
    public const int TIMESLICE_RESOLUTION = 65536;

    private SharpDX.Direct3D11.Texture2D _dx11Tex;
    private ShaderResourceView _dx11Resource;
    private int _totalMips;

    private TextureFilterMode _filterMode;
    private int _anisoLevel;
    private FrooxEngine.TextureWrapMode _wrapU;
    private FrooxEngine.TextureWrapMode _wrapV;
    private FrooxEngine.TextureWrapMode _wrapW;
    private float _mipmapBias;
    private AssetIntegrated _onPropertiesSet;
    private int _lastLoadedMip;
    private bool _texturePropertiesDirty;


    public UnityEngine.Texture2D UnityTexture2D => _unityTexture2D;

    public UnityEngine.Cubemap UnityCubemap => _unityCubemap;

    Texture IUnityTextureProvider.UnityTexture => (Texture)(UnityTexture2D ?? ((object)UnityCubemap) ?? ((object)_unityTexture3D));

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
            Profile = profile,
            Depth = 1
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
            Profile = profile,
            Depth = 1
        });
    }
    public void SetTexture3DFormat(int width, int height, int depth, int mipmaps, Elements.Assets.TextureFormat format, ColorProfile profile, AssetIntegrated onDone)
    {
        this.SetTextureFormat(new TextureConnector.TextureFormatData
        {
            Type = TextureConnector.TextureType.Texture3D,
            Width = width,
            Height = height,
            Depth = depth,
            Mips = mipmaps,
            Format = format,
            OnDone = onDone,
            Profile = profile
        });
    }

    private void SetTextureFormatUnity(TextureConnector.TextureFormatData format)
    {
        bool environmentInstanceChanged = false;
        if (format.Type == TextureConnector.TextureType.Texture2D)
        {
            UnityEngine.TextureFormat textureFormat = format.Format.ToUnity(true);
            if (this._unityTexture2D == null || this._unityTexture2D.width != format.Width || this._unityTexture2D.height != format.Height || this._unityTexture2D.format != textureFormat || this._unityTexture2D.mipmapCount > 1 != format.Mips > 1)
            {
                this.Destroy();
                this._unityTexture2D = new UnityEngine.Texture2D(format.Width, format.Height, textureFormat, format.Mips > 1);
                environmentInstanceChanged = true;
            }
        }
        else if (format.Type == TextureConnector.TextureType.Texture3D)
        {
            ColorProfile profile = format.Profile;
            GraphicsFormat graphicsFormat = format.Format.ToUnityExperimental(ref profile);
            if (profile != format.Profile)
            {
                this._targetProfile = profile;//new ColorProfile?(profile);
            }
            if (_unityTexture3D == null || _unityTexture3D.width != format.Width || _unityTexture3D.height != format.Height || _unityTexture3D.depth != format.Depth || _unityTexture3D.graphicsFormat != graphicsFormat || _unityTexture3D.mipmapCount > 1 != format.Mips > 1 || profile != _targetProfile)
            {

                this.Destroy();
                this._targetProfile = profile;//new ColorProfile?(profile);
                this._unityTexture3D = new UnityEngine.Texture3D(format.Width, format.Height, format.Depth, graphicsFormat, TextureCreationFlags.None);
                environmentInstanceChanged = true;
                //ColorProfile colorProfile = profile;
                //ColorProfile? targetProfile = this._targetProfile;
                //if (colorProfile == targetProfile.GetValueOrDefault() & targetProfile != null)
                //{
                    //goto IL_199;
                //}
            }
            
        }
        this.AssignTextureProperties();
        format.OnDone(environmentInstanceChanged);
    }

    private void SetTextureFormat(TextureFormatData format)
    {
        if (format.Type == TextureConnector.TextureType.Texture3D)
        {
            SetFormatUnity();
            return;
        }
        switch (UnityAssetIntegrator.GraphicsDeviceType)
        {
            case GraphicsDeviceType.Direct3D11:
                UnityAssetIntegrator.EnqueueRenderThreadProcessing(SetTextureFormatDX11Native(format));
                return;
                /*
                case GraphicsDeviceType.OpenGLES2:
                case GraphicsDeviceType.OpenGLES3:
                case GraphicsDeviceType.OpenGLCore:
                    UnityAssetIntegrator.EnqueueRenderThreadProcessing(this.SetTextureFormatOpenGLNative(format));
                    return;
                    */
        }
        SetFormatUnity();
        void SetFormatUnity()
        {
            UnityAssetIntegrator.EnqueueProcessing(delegate
			{
				SetTextureFormatUnity(format);
			}, highPriority: true);
        }
    }



    public void SetTexture2DData(Bitmap2D data, int startMipLevel, TextureUploadHint hint, AssetIntegrated onSet) =>
        SetTextureData(new TextureUploadData
        {
            Bitmap2D = data,
            StartMip = startMipLevel,
            Hint = hint,
            OnDone = onSet,
        });

    public void SetCubemapData(BitmapCube data, int startMipLevel, AssetIntegrated onSet) =>
        SetTextureData(new TextureUploadData
        {
            BitmapCube = data,
            StartMip = startMipLevel,
            OnDone = onSet,
        });

    private void SetTextureData(TextureUploadData data)
    {
        if (data.Bitmap3D != null)
        {
            //UnityAssetIntegrator.EnqueueRenderThreadProcessing(() => UploadTextureDataUnity(data));
            EnqueueUnityUpload();
            return;
        }
        switch (UnityAssetIntegrator.GraphicsDeviceType)
        {
            case GraphicsDeviceType.Direct3D11:
                data.Format.ToDX11(out var convertToFormat, data.Bitmap.Profile, Engine.SystemInfo);
                if (convertToFormat != data.Format)
                {
                    UniLog.Warning(
                        $"Converting texture format from {data.Format} to {convertToFormat}. Texture: {data.Bitmap}. Asset: {Asset}");
                    data.ConvertTo(convertToFormat);
                }

                UnityAssetIntegrator.EnqueueRenderThreadProcessing(UploadTextureDataDX11Native(data));
                return;
                /*
                case GraphicsDeviceType.OpenGLES2:
                case GraphicsDeviceType.OpenGLES3:
                case GraphicsDeviceType.OpenGLCore:
                {
                    Helper.OpenGL_TextureFormat openGL_TextureFormat = data.Format.ToOpenGL(data.Bitmap.Profile, base.Engine.SystemInfo);
                    if (openGL_TextureFormat.sourceFormat != data.Format)
                    {
                        UniLog.Warning($"Converting texture format from {data.Format} to {openGL_TextureFormat}. Texture: {data.Bitmap}. Asset: {Asset}");
                        data.ConvertTo(openGL_TextureFormat.sourceFormat);
                    }
                    base.UnityAssetIntegrator.EnqueueRenderThreadProcessing(UploadTextureDataOpenGLNative(data));
                    return;
                }
                */
        }
        EnqueueUnityUpload();
        void EnqueueUnityUpload()
		{
			base.UnityAssetIntegrator.EnqueueProcessing(delegate
			{
				UploadTextureDataUnity(data);
			}, Asset.HighPriorityIntegration);
		}
    }

    private void UploadTextureDataUnity(TextureConnector.TextureUploadData data)
    {
        if (data.Bitmap2D != null)
        {
            int2 @int = new int2(this._unityTexture2D.width, this._unityTexture2D.height);
            int num = 0;
            for (int i = 0; i < data.StartMip; i++)
            {
                int2 int2 = Bitmap2DBase.AlignSize(in @int, data.Format);
                num += int2.x * int2.y;
                @int = @int / 2;
                @int = MathX.Max(in @int, 1);
            }
            num = (int)MathX.BitsToBytes((double)num * data.Format.GetBitsPerPixel());
            NativeArray<byte> rawTextureData = this._unityTexture2D.GetRawTextureData<byte>();
            Bitmap2D bitmap2D = data.Bitmap2D;
            byte[] array;
            if ((array = ((bitmap2D != null) ? bitmap2D.RawData : null)) == null)
            {
                BitmapCube bitmapCube = data.BitmapCube;
                array = ((bitmapCube != null) ? bitmapCube.RawData : null);
            }
            byte[] array2 = array;
            for (int j = 0; j < array2.Length; j++)
            {
                rawTextureData[j + num] = array2[j];
            }
        }
        else if (data.Bitmap3D != null)
        {
            //ColorProfile profile = data.Bitmap3D.Profile;
            //ColorProfile? targetProfile = _targetProfile;
            if (_targetProfile.HasValue && data.Bitmap3D.Profile != _targetProfile)
            {
                data.Bitmap3D.ConvertToProfile(this._targetProfile.Value);
            }
            this._unityTexture3D.SetPixelData<byte>(data.Bitmap3D.RawData, data.StartMip);
        }
        if (data.StartMip == 0)
        {
            UnityEngine.Texture2D unityTexture2D = this._unityTexture2D;
            if (unityTexture2D != null)
            {
                unityTexture2D.Apply(false, !data.Hint.readable);
            }
            UnityEngine.Cubemap unityCubemap = this._unityCubemap;
            if (unityCubemap != null)
            {
                unityCubemap.Apply(false, !data.Hint.readable);
            }
            UnityEngine.Texture3D unityTexture3D = this._unityTexture3D;
            if (unityTexture3D != null)
            {
                unityTexture3D.Apply(false, !data.hint3D.readable);
            }
            base.Engine.TextureUpdated();
        }
        data.OnDone(false);
    }

    public void SetTexture3DData(Bitmap3D data, Texture3DUploadHint hint, AssetIntegrated onSet)
    {
        this.SetTextureData(new TextureConnector.TextureUploadData
        {
            Bitmap3D = data,
            StartMip = 0,
            OnDone = onSet,
            hint3D = hint
        });
    }

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

    public void SetTexture3DProperties(TextureFilterMode filterMode, FrooxEngine.TextureWrapMode wrapU, FrooxEngine.TextureWrapMode wrapV, FrooxEngine.TextureWrapMode wrapW, AssetIntegrated onSet)
    {
        this._texturePropertiesDirty = true;
        this._filterMode = filterMode;
        this._wrapU = wrapU;
        this._wrapV = wrapV;
        this._wrapW = wrapW;
        this._onPropertiesSet = onSet;
        if (onSet != null)
        {
            base.UnityAssetIntegrator.EnqueueProcessing(UpdateTextureProperties, Asset.HighPriorityIntegration);
        }
    }

    public override void Unload() => UnityAssetIntegrator.EnqueueProcessing(Destroy, true);

    private IEnumerator DestroyDX11(ShaderResourceView resource, SharpDX.Direct3D11.Texture2D tex)
    {
        if (resource != null)
        {
            resource.Dispose();
        }
        if (tex != null)
        {
            tex.Dispose();
        }
        yield break;
    }

    private void Destroy()
    {
        if ((bool)_unityTexture2D)
		{
			UnityEngine.Object.DestroyImmediate(_unityTexture2D, allowDestroyingAssets: true);
		}
		if ((bool)_unityCubemap)
		{
			UnityEngine.Object.DestroyImmediate(_unityCubemap, allowDestroyingAssets: true);
		}
		if ((bool)_unityTexture3D)
		{
			UnityEngine.Object.DestroyImmediate(_unityTexture3D, allowDestroyingAssets: true);
		}
        if (this._dx11Resource != null)
        {
            base.UnityAssetIntegrator.EnqueueRenderThreadProcessing(DestroyDX11(_dx11Resource, _dx11Tex));
            this._dx11Tex = null;
            this._dx11Resource = null;
        }
        this._unityTexture2D = null;
        this._unityCubemap = null;
        this._unityTexture2D = null;
        this._targetProfile = null;
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

        if (this._unityTexture3D != null)
        {
            if (this._filterMode == TextureFilterMode.Anisotropic)
            {
                this._unityTexture3D.filterMode = FilterMode.Trilinear;
                this._unityTexture3D.anisoLevel = this._anisoLevel;
            }
            else
            {
                this._unityTexture3D.filterMode = this._filterMode.ToUnity();
                this._unityTexture3D.anisoLevel = 0;
            }
            this._unityTexture3D.mipMapBias = this._mipmapBias;
            this._unityTexture3D.wrapModeU = this._wrapU.ToUnity();
            this._unityTexture3D.wrapModeV = this._wrapV.ToUnity();
            this._unityTexture3D.wrapModeW = this._wrapW.ToUnity();
        }
    }

    private void UpdateTextureProperties()
    {
        AssignTextureProperties();
        var onPropertiesSet = _onPropertiesSet;
        _onPropertiesSet = null;
        onPropertiesSet(false);
    }

    private void GenerateUnityTextureFromDX11(TextureFormatData format)
    {
        switch (format.Type)
        {
            case TextureType.Texture2D:
                _unityTexture2D = UnityEngine.Texture2D.CreateExternalTexture(format.Width, format.Height,
                    format.Format.ToUnity(), format.Mips > 1, linear: false, _dx11Resource.NativePointer);
                break;
            case TextureType.Cubemap:
                _unityCubemap = UnityEngine.Cubemap.CreateExternalTexture(format.Width, format.Format.ToUnity(),
                    format.Mips > 1, _dx11Resource.NativePointer);
                break;
        }

        AssignTextureProperties();
        format.OnDone(environmentInstanceChanged: true);
    }

    private IEnumerator SetTextureFormatDX11Native(TextureFormatData format)
    {
        var format2 = format.Format.ToDX11(out _, format.Profile, Engine.SystemInfo);
        var description = _dx11Tex?.Description ?? default(Texture2DDescription);
        var flag = false;
        if (_dx11Tex == null
            || description.Width != format.Width
            || description.Height != format.Height
            || description.ArraySize != format.ArraySize
            || description.Format != format2
            || description.MipLevels != format.Mips)
        {
            if (_dx11Tex != null)
            {
                var oldUnityTex = _unityTexture2D;
                var oldUnityCube = _unityCubemap;
                var oldDX11tex = _dx11Tex;
                var oldDX11res = _dx11Resource;
                var oldOnDone = format.OnDone;
                format.OnDone = delegate
                {
                    if (oldUnityTex) UnityEngine.Object.DestroyImmediate(oldUnityTex);
                    if (oldUnityCube) UnityEngine.Object.DestroyImmediate(oldUnityCube);
                    oldDX11res?.Dispose();
                    oldDX11tex?.Dispose();
                    oldOnDone(environmentInstanceChanged: true);
                };
            }

            description.Width = format.Width;
            description.Height = format.Height;
            description.MipLevels = format.Mips;
            description.ArraySize = format.ArraySize;
            description.Format = format2;
            description.SampleDescription.Count = 1;
            description.Usage = ResourceUsage.Default;
            description.BindFlags = BindFlags.ShaderResource;
            description.CpuAccessFlags = CpuAccessFlags.None;
            description.OptionFlags = format.Type == TextureType.Texture2D
                ? ResourceOptionFlags.ResourceClamp
                : ResourceOptionFlags.TextureCube | ResourceOptionFlags.ResourceClamp;
            var description2 = default(ShaderResourceViewDescription);
            description2.Format = description.Format;
            description2.Dimension = ((format.Type == TextureType.Texture2D)
                ? ShaderResourceViewDimension.Texture2D
                : ShaderResourceViewDimension.TextureCube);
            switch (format.Type)
            {
                case TextureType.Texture2D:
                    description2.Texture2D.MipLevels = format.Mips;
                    description2.Texture2D.MostDetailedMip = 0;
                    break;
                case TextureType.Cubemap:
                    description2.TextureCube.MipLevels = format.Mips;
                    description2.TextureCube.MostDetailedMip = 0;
                    break;
            }

            try
            {
                _dx11Tex = new SharpDX.Direct3D11.Texture2D(UnityAssetIntegrator._dx11device, description);
                _dx11Resource = new ShaderResourceView(UnityAssetIntegrator._dx11device, _dx11Tex, description2);
                _totalMips = format.Mips;
            }
            catch (Exception ex)
            {
                UniLog.Error(
                    $"Exception creating texture: Width: {description.Width}, Height: {description.Height}, Mips: {description.MipLevels}, format: {format2}.");
                throw ex;
            }

            _lastLoadedMip = format.Mips;
            flag = true;
        }

        if (flag)
        {
            UnityAssetIntegrator.EnqueueProcessing(delegate { GenerateUnityTextureFromDX11(format); },
                highPriority: true);
        }
        else if (_texturePropertiesDirty)
        {
            UnityAssetIntegrator.EnqueueProcessing(delegate
            {
                AssignTextureProperties();
                format.OnDone(environmentInstanceChanged: false);
            }, highPriority: true);
        }
        else
        {
            format.OnDone(environmentInstanceChanged: false);
        }

        yield break;
    }

    private IEnumerator UploadTextureDataDX11Native(TextureUploadData data)
    {
        var elements = data.ElementCount;
        var hint = data.Hint;
        var bitmap = data.Bitmap;
        var faceSize = data.FaceSize;
        var format = data.Format;
        var totalMipMaps = _totalMips;
        var width = hint.region?.width ?? faceSize.x;
        var height = hint.region?.height ?? faceSize.y;
        var startX = hint.region?.x ?? 0;
        var startY = hint.region?.y ?? 0;
        var blockSize = format.BlockSize();
        var bitsPerPixel = format.GetBitsPerPixel();
        if (width > 0 || height > 0)
        {
            for (var mip = 0; mip < bitmap.MipMapLevels; mip++)
            {
                for (var face = 0; face < elements; face++)
                {
                    var levelSize = data.MipMapSize(mip);
                    var targetMip = data.StartMip + mip;
                    width = MathX.Min(width, levelSize.x - startX);
                    height = MathX.Min(height, levelSize.y - startY);
                    var mipSize = Bitmap2DBase.AlignSize(in levelSize, data.Format);
                    var size2 = new int2(width, height);
                    size2 = Bitmap2DBase.AlignSize(in size2, data.Format);
                    var rowGranularity3 = 65536 / width;
                    rowGranularity3 -= rowGranularity3 % 4;
                    rowGranularity3 = MathX.Max(4, rowGranularity3);
                    var row = 0;
                    var rowPitch = (int)(MathX.BitsToBytes(mipSize.x * bitsPerPixel) * blockSize.y);
                    while (row < height)
                    {
                        if (row > 0) yield return null;

                        ResourceRegion? resourceRegion = new ResourceRegion(startX, startY + row, 0, startX + size2.x,
                            MathX.Min(startY + row + rowGranularity3, startY + size2.y), 1);
                        if (resourceRegion.Value.Left == 0 && resourceRegion.Value.Top == 0 &&
                            resourceRegion.Value.Right == mipSize.x && resourceRegion.Value.Bottom == mipSize.y)
                        {
                            resourceRegion = null;
                        }

                        var num = startY + row;
                        if (data.Bitmap2D != null)
                        {
                            num = levelSize.y - num - 1;
                        }

                        var num2 = (int)MathX.BitsToBytes(data.PixelStart(startX, num, mip, face) * bitsPerPixel);
                        UnityAssetIntegrator._dx11device.ImmediateContext.UpdateSubresource(ref bitmap.RawData[num2],
                            _dx11Tex, targetMip + face * totalMipMaps, rowPitch, 0, resourceRegion);
                        row += rowGranularity3;
                        Engine.TextureSliceUpdated();
                    }
                }

                width /= 2;
                height /= 2;
                startX /= 2;
                startY /= 2;
                width = MathX.Max(width, 1);
                height = MathX.Max(height, 1);
            }

            _lastLoadedMip = MathX.Min(_lastLoadedMip, data.StartMip);
            _dx11Tex.Device.ImmediateContext.SetMinimumLod(_dx11Tex, _lastLoadedMip);
        }

        Engine.TextureUpdated();
        data.OnDone(environmentInstanceChanged: false);
    }

    private enum TextureType
    {
        Texture2D,
        Cubemap,
        Texture3D
    }

    private class TextureFormatData
    {
        public TextureType Type;
        public int Width;
        public int Height;
        public int Depth;
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
                    TextureType.Texture3D => Depth,
                    _ => throw new Exception("Invalid texture type: " + Type)
                };
            }
        }
    }

    private class TextureUploadData
    {
        public Bitmap2D Bitmap2D;

        public Bitmap3D Bitmap3D;

        public BitmapCube BitmapCube;

        public int StartMip;

        public TextureUploadHint Hint;

        public AssetIntegrated OnDone;

        public Texture3DUploadHint hint3D;

        public Bitmap Bitmap
        {
            get
            {
                Bitmap result;
                if ((result = this.Bitmap2D) == null)
                {
                    result = ((this.BitmapCube != null) ? this.BitmapCube : this.Bitmap3D);
                }
                return result;
            }
        }

        public Elements.Assets.TextureFormat Format => Bitmap.Format;


        public int2 FaceSize
        {
            get
            {
                Bitmap2D bitmap2D = this.Bitmap2D;
                if (bitmap2D != null)
                {
                    return bitmap2D.Size;
                }
                BitmapCube bitmapCube = this.BitmapCube;
                if (bitmapCube == null)
                {
                    return int2.Zero;
                }
                return bitmapCube.Size;
            }
        }

        public int2 MipMapSize(int mip)
        {
            Bitmap2D bitmap2D = this.Bitmap2D;
            if (bitmap2D != null)
            {
                return bitmap2D.MipMapSize(mip);
            }
            BitmapCube bitmapCube = this.BitmapCube;
            if (bitmapCube == null)
            {
                return int2.Zero;
            }
            return bitmapCube.MipMapSize(mip);
        }

        public int ElementCount
        {
            get
            {
                if (this.Bitmap2D != null)
                {
                    return 1;
                }
                if (this.BitmapCube != null)
                {
                    return 6;
                }
                if (this.Bitmap3D != null)
                {
                    return this.Bitmap3D.Size.z;
                }
                throw new Exception("Invalid state, must have either Bitmap2D, BitmapCUBE or Bitmap3D");
            }
        }

        public int PixelStart(int x, int y, int mip, int face)
        {
            Bitmap2D bitmap2D = this.Bitmap2D;
            if (bitmap2D == null)
            {
                return this.BitmapCube.PixelStart(x, y, (BitmapCube.Face)face, mip);
            }
            return bitmap2D.PixelStart(x, y, mip);
        }

        public void ConvertTo(Elements.Assets.TextureFormat format)
        {
            if (this.Bitmap2D != null)
            {
                this.Bitmap2D = this.Bitmap2D.ConvertTo(format);
            }
            if (this.BitmapCube != null)
            {
                this.BitmapCube = this.BitmapCube.ConvertTo(format);
            }
        }
    }
}