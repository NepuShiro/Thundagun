using FrooxEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityFrooxEngineRunner;
using UnityEngine;

namespace Thundagun.NewConnectors.ComponentConnectors;

public class RenderMaterialOverrideConnector : RenderContextOverride<RenderMaterialOverride>
{
	public class MaterialOverride
	{
		public int index;

		public UnityEngine.Material original;

		public UnityEngine.Material replacement;
	}

	public IRendererConnector mesh;

	private List<MaterialOverride> overrides = new List<MaterialOverride>();

	public List<MaterialOverride> RmoOverrides;

	public int OverridesCount;

	public override RenderingContext Context => base.Owner.Context.Value;

	protected override void Override()
	{
		Renderer renderer = mesh?.Renderer;
		if (renderer == null)
		{
			return;
		}
		UnityEngine.Material[] sharedMaterials = renderer.sharedMaterials;
		foreach (MaterialOverride @override in overrides)
		{
			if (@override.index >= 0 && @override.index < sharedMaterials.Length)
			{
				@override.original = sharedMaterials[@override.index];
				sharedMaterials[@override.index] = @override.replacement;
			}
		}
		renderer.sharedMaterials = sharedMaterials;
	}

	protected override void Restore()
	{
		Renderer renderer = mesh?.Renderer;
		if (renderer == null)
		{
			return;
		}
		UnityEngine.Material[] sharedMaterials = renderer.sharedMaterials;
		foreach (MaterialOverride @override in overrides)
		{
			if (@override.index >= 0 && @override.index < sharedMaterials.Length)
			{
				sharedMaterials[@override.index] = @override.original;
				@override.original = null;
			}
		}
		renderer.sharedMaterials = sharedMaterials;
	}

	public override void UpdateSetup()
	{
		//mesh = base.Owner.Renderer.Target?.Connector as IRendererConnector;
		while (overrides.Count > OverridesCount)
		{
			overrides.RemoveAt(overrides.Count - 1);
		}
		while (OverridesCount > overrides.Count)
		{
			overrides.Add(new MaterialOverride());
		}
		for (int i = 0; i < OverridesCount; i++)
		{
			MaterialOverride materialOverride = overrides[i];
			var materialOverride2 = RmoOverrides[i];
			materialOverride.index = materialOverride2.index;
			materialOverride.replacement = materialOverride2.replacement;
		}
	}
}