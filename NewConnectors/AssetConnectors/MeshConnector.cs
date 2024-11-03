using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors.AssetConnectors;

public class MeshConnector : AssetConnector, IMeshConnector
{
    private UnityEngine.Mesh _mesh;
    private UnityMeshData _meshGenData;
    private MeshUploadHint _uploadHint;
    private BoundingBox _bounds;
    private AssetIntegrated _onLoaded;
    public static volatile int MeshDataCount;

    public UnityEngine.Mesh Mesh => _mesh;

    public void UpdateMeshData(
        MeshX meshx,
        MeshUploadHint uploadHint,
        BoundingBox bounds,
        AssetIntegrated onLoaded)
    {
        var data = new UnityMeshData();
        meshx.GenerateUnityMeshData(ref data, ref uploadHint, Engine.SystemInfo);
        UnityAssetIntegrator.EnqueueProcessing(() => Upload2(data, uploadHint, bounds, onLoaded), Asset.HighPriorityIntegration);
    }

    private void Upload2(UnityMeshData data, MeshUploadHint hint, BoundingBox bounds, AssetIntegrated onLoaded)
    {
        if (data == null)
            return;
        if (_mesh != null && !_mesh.isReadable)
        {
            if (_mesh)
                UnityEngine.Object.Destroy(_mesh);
            _mesh = null;
        }

        var environmentInstanceChanged = false;
        if (_mesh == null)
        {
            _mesh = new UnityEngine.Mesh();
            environmentInstanceChanged = true;
            if (hint[MeshUploadHint.Flag.Dynamic])
                _mesh.MarkDynamic();
        }

        data.Assign(_mesh, hint);

        _mesh.bounds = bounds.ToUnity();
        _mesh.UploadMeshData(!hint[MeshUploadHint.Flag.Readable]);
        if (hint[MeshUploadHint.Flag.Dynamic])
        {
            _meshGenData = data;
            _uploadHint = hint;
            _bounds = bounds;
            _onLoaded = onLoaded;
        }
        onLoaded(environmentInstanceChanged);
        Engine.MeshUpdated();
    }

    private void Upload()
    {
        if (_meshGenData == null)
            return;
        if (_mesh != null && !_mesh.isReadable)
        {
            if (_mesh)
                UnityEngine.Object.Destroy(_mesh);
            _mesh = null;
        }

        var environmentInstanceChanged = false;
        if (_mesh == null)
        {
            _mesh = new UnityEngine.Mesh();
            environmentInstanceChanged = true;
            if (_uploadHint[MeshUploadHint.Flag.Dynamic])
                _mesh.MarkDynamic();
        }

        _meshGenData.Assign(_mesh, _uploadHint);

        _mesh.bounds = _bounds.ToUnity();
        _mesh.UploadMeshData(!_uploadHint[MeshUploadHint.Flag.Readable]);
        if (!_uploadHint[MeshUploadHint.Flag.Dynamic])
            _meshGenData = null;
        _onLoaded(environmentInstanceChanged);
        _onLoaded = null;
        Engine.MeshUpdated();
    }

    public override void Unload() => UnityAssetIntegrator.EnqueueProcessing(Destroy, true);
    private void Destroy()
    {
        if (_mesh != null)
            UnityEngine.Object.Destroy(_mesh);
        _mesh = null;
        _meshGenData = null;
    }
}