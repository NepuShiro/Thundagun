using System;

namespace Thundagun.NewConnectors.ComponentConnectors;

public class MeshRendererConnector : MeshRendererConnectorBase<FrooxEngine.MeshRenderer, UnityEngine.MeshRenderer>
{
    public override bool UseMeshFilter => true;

    public override void AssignMesh(UnityEngine.MeshRenderer renderer, UnityEngine.Mesh mesh) => throw new NotImplementedException();
}