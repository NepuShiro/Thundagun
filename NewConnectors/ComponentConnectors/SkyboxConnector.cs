using FrooxEngine;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors.ComponentConnectors;

public class SkyboxConnector : ComponentConnectorSingle<Skybox>
{
    public override void ApplyChanges() => Thundagun.CurrentPackets.Add(new ApplyChangesSkyboxConnector(this));
}

public class ApplyChangesSkyboxConnector : UpdatePacket<SkyboxConnector>
{
    public bool Active;
    public MaterialConnector Material;
    public IRenderConnector RenderConnector;
    
    public ApplyChangesSkyboxConnector(SkyboxConnector owner) : base(owner)
    {
        Active = !owner.Owner.ActiveSkybox || owner.World.Focus != World.WorldFocus.Focused;
        if (!Active) return;
        Material = owner.Owner.Material?.Asset?.Connector as MaterialConnector;
        RenderConnector = owner.World.Render.Connector;
    }

    public override void Update()
    {
        var mat = Material?.UnityMaterial ?? MaterialConnector.NullMaterial;
        var old = UnityEngine.RenderSettings.skybox;
        UnityEngine.RenderSettings.skybox = mat;
        RenderConnector.UpdateDynamicGI(old != mat);
    }
}