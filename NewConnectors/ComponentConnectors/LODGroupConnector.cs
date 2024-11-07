using FrooxEngine;
using System.Collections.Generic;
using UnityEngine;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors.ComponentConnectors;

public class LODGroupConnector : ComponentConnectorSingle<FrooxEngine.LODGroup>
{
	public GameObject groupGO;

	public UnityEngine.LODGroup lodGroup;

	//public override void Initialize()
	//{
	//	base.Initialize();
		
	//}

	public override IUpdatePacket InitializePacket() => new InitializeLODGroupConnector(this);

	public override void ApplyChanges() => Thundagun.QueuePacket(new ApplyChangesLODGroupConnector(this));

	//public override void ApplyChanges()
	//{
	//	LOD[] array = new LOD[base.Owner.LODs.Count];
	//	for (int i = 0; i < base.Owner.LODs.Count; i++)
	//	{
	//		FrooxEngine.LODGroup.LOD lOD = base.Owner.LODs[i];
	//		array[i].screenRelativeTransitionHeight = lOD.ScreenRelativeTransitionHeight;
	//		array[i].fadeTransitionWidth = lOD.FadeTransitionWidth;
	//		Renderer[] array2 = new Renderer[lOD.Renderers.Count];
	//		for (int j = 0; j < lOD.Renderers.Count; j++)
	//		{
	//			array2[j] = (lOD.Renderers[j]?.Connector as IRendererConnector)?.Renderer;
	//		}
	//		array[i].renderers = array2;
	//	}
	//	lodGroup.SetLODs(array);
	//	lodGroup.RecalculateBounds();
	//}

	public override void DestroyMethod(bool destroyingWorld)
	{
		if (!destroyingWorld && (bool)groupGO)
		{
			UnityEngine.Object.Destroy(groupGO);
		}
		groupGO = null;
		base.DestroyMethod(destroyingWorld);
	}
}

public class InitializeLODGroupConnector : InitializeComponentConnectorSingle<FrooxEngine.LODGroup, LODGroupConnector>
{
    public InitializeLODGroupConnector(LODGroupConnector owner) : base(owner, owner.Owner)
    {
    }

    public override void Update()
    {
		base.Update();
		Owner.groupGO = new GameObject("");
		Owner.groupGO.transform.SetParent(Owner.AttachedGameObject.transform, worldPositionStays: false);
		Owner.lodGroup = Owner.groupGO.AddComponent<UnityEngine.LODGroup>();
    }
}

public class ApplyChangesLODGroupConnector : UpdatePacket<LODGroupConnector>
{
	public int LODCount;
	public List<LODStruct> LODS;
	public struct LODStruct
	{
		public float ScreenRelativeTransitionHeight;
		public float FadeTransitionWidth;
		public int RenderersCount;
		public List<IRendererConnector> Renderers;
	}
    public ApplyChangesLODGroupConnector(LODGroupConnector owner) : base(owner)
    {
		LODCount = owner.Owner.LODs.Count;
		LODS = new List<LODStruct>();
		foreach (var lod in owner.Owner.LODs)
		{
			var lodStruct = new LODStruct { 
				ScreenRelativeTransitionHeight = lod.ScreenRelativeTransitionHeight,
				FadeTransitionWidth = lod.FadeTransitionWidth,
				RenderersCount = lod.Renderers.Count
			};
			lodStruct.Renderers = new();
			foreach (var renderer in lod.Renderers)
			{
				if (renderer?.Connector is IRendererConnector rendererConnector)
				{
					lodStruct.Renderers.Add(rendererConnector);
				}
				else
				{
					lodStruct.Renderers.Add(null);
				}
			}
			LODS.Add(lodStruct);
		}
	}

    public override void Update()
    {
		LOD[] array = new LOD[LODCount];
		for (int i = 0; i < LODCount; i++)
		{
			LODStruct lOD = LODS[i];
			array[i].screenRelativeTransitionHeight = lOD.ScreenRelativeTransitionHeight;
			array[i].fadeTransitionWidth = lOD.FadeTransitionWidth;
			Renderer[] array2 = new Renderer[lOD.RenderersCount];
			for (int j = 0; j < lOD.RenderersCount; j++)
			{
				array2[j] = (lOD.Renderers[j])?.Renderer;
			}
			array[i].renderers = array2;
		}
		Owner.lodGroup.SetLODs(array);
		Owner.lodGroup.RecalculateBounds();
    }
}