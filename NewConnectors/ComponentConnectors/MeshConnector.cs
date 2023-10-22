using System;
using Elements.Core;
using FrooxEngine;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors.ComponentConnectors;

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
        meshx.GenerateUnityMeshData(ref _meshGenData, ref uploadHint, Engine.SystemInfo);
        _uploadHint = uploadHint;
        _bounds = bounds;
        _onLoaded = onLoaded;
        UnityAssetIntegrator.EnqueueProcessing(Upload, Asset.HighPriorityIntegration);
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