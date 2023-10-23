using System;
using Elements.Core;
using FrooxEngine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors.ComponentConnectors;

public class ParticleSystemBehavior : ConnectorBehaviour<ParticleSystemConnector>
{
	private static ParticleEmission[] _emissions;

	private static color[] _colors;

	internal float lastDeltaTime;

	internal int lastCount;

	internal Slot lastSpace;

	internal UnityEngine.ParticleSystem pSystem;

	internal ParticleSystemRenderer pRenderer;

	internal UnityEngine.ParticleSystem.ShapeModule pShape;

	internal UnityEngine.ParticleSystem.TrailModule pTrail;

	internal UnityEngine.ParticleSystem.LightsModule pLights;

	internal UnityEngine.ParticleSystem.Particle[] _particles;

	private RandomXGenerator random = new RandomXGenerator(RandomX.Int);

	private const int GRANULARITY = 100;

	internal FrooxEngine.ParticleSystem FrooxEngineParticleSystem => base.Connector.Owner;

	internal ParticleStyle ParticleStyle => FrooxEngineParticleSystem.Style.Target;

	internal PhysicsManager Physics => base.Connector?.World?.Physics;

	internal void UpdateStyle()
	{
		var main = pSystem.main;
		var textureSheetAnimation = pSystem.textureSheetAnimation;
		main.maxParticles = FrooxEngineParticleSystem.MaxParticles;
		var space = FrooxEngineParticleSystem.SimulationSpace.Space;
		if (lastSpace != space)
		{
			lastSpace = space;
			if (space.IsRootSlot)
			{
				main.simulationSpace = ParticleSystemSimulationSpace.World;
			}
			else
			{
				main.simulationSpace = ParticleSystemSimulationSpace.Custom;
				main.customSimulationSpace = ((SlotConnector)space.Connector).ForceGetGameObject().transform;
			}
		}
		var particleStyle = ParticleStyle;
		if (particleStyle != null)
		{
			pRenderer.enabled = true;
			pRenderer.motionVectorGenerationMode = particleStyle.MotionVectorMode.Value.ToUnity();
			pRenderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
			pRenderer.alignment = particleStyle.Alignment.Value.ToUnity();
			pRenderer.allowRoll = particleStyle.Alignment.Value != ParticleAlignment.Facing;
			pRenderer.material = particleStyle.Material.Asset.GetUnity();
			pRenderer.trailMaterial = particleStyle.TrailMaterial.Asset.GetUnity();
			pRenderer.mesh = particleStyle.Mesh.Asset.GetUnity();
			pRenderer.minParticleSize = particleStyle.MinParticleSize.Value;
			pRenderer.maxParticleSize = particleStyle.MaxParticleSize.Value;
			main.gravityModifier = particleStyle.GravityStrength.Value;
			var colorOverLifetime = pSystem.colorOverLifetime;
			colorOverLifetime.enabled = particleStyle.UseColorOverLifetime.Value;
			if (colorOverLifetime.enabled)
			{
				var array = new GradientColorKey[particleStyle.ColorOverLifetime.Count];
				var array2 = new GradientAlphaKey[particleStyle.AlphaOverLifetime.Count];
				for (var i = 0; i < array.Length; i++)
				{
					var linearKey = particleStyle.ColorOverLifetime[i];
					array[i] = new GradientColorKey(linearKey.value.ToUnity(ColorProfile.sRGB), linearKey.time);
				}
				for (var j = 0; j < array2.Length; j++)
				{
					var linearKey2 = particleStyle.AlphaOverLifetime[j];
					array2[j] = new GradientAlphaKey(linearKey2.value, linearKey2.time);
				}
				var gradient = new Gradient();
				gradient.alphaKeys = array2;
				gradient.colorKeys = array;
				colorOverLifetime.color = new UnityEngine.ParticleSystem.MinMaxGradient(gradient);
			}
			if (pRenderer.mesh != null)
			{
				pRenderer.renderMode = ParticleSystemRenderMode.Mesh;
			}
			else if (MathX.Approximately(particleStyle.LengthScale.Value, 1f) && MathX.Approximately(particleStyle.VelocityScale.Value, 0f))
			{
				pRenderer.renderMode = ParticleSystemRenderMode.Billboard;
			}
			else
			{
				pRenderer.renderMode = ParticleSystemRenderMode.Stretch;
				pRenderer.lengthScale = particleStyle.LengthScale;
				pRenderer.velocityScale = particleStyle.VelocityScale;
			}
			int2 v = particleStyle.AnimationTiles;
			if (MathX.MaxComponent(in v) > 1)
			{
				textureSheetAnimation.enabled = true;
				textureSheetAnimation.numTilesX = particleStyle.AnimationTiles.Value.x;
				textureSheetAnimation.numTilesY = particleStyle.AnimationTiles.Value.y;
				textureSheetAnimation.cycleCount = particleStyle.AnimationCycles;
				textureSheetAnimation.animation = particleStyle.AnimationType.Value.ToUnity();
				textureSheetAnimation.rowMode = (particleStyle.UseRandomRow ? ParticleSystemAnimationRowMode.Random : ParticleSystemAnimationRowMode.Custom);
				textureSheetAnimation.rowIndex = particleStyle.UseRowIndex;
			}
			else
			{
				textureSheetAnimation.enabled = false;
			}
			if (particleStyle.ParticleTrails.Value == ParticleTrailMode.None)
			{
				pTrail.enabled = false;
			}
			else
			{
				pTrail.enabled = true;
				pTrail.mode = particleStyle.ParticleTrails.Value == ParticleTrailMode.PerParticle ? ParticleSystemTrailMode.PerParticle : ParticleSystemTrailMode.Ribbon;
				pTrail.ratio = particleStyle.TrailRatio;
				pTrail.minVertexDistance = particleStyle.TrailMinimumVertexDistance;
				pTrail.worldSpace = particleStyle.TrailWorldSpace;
				pTrail.dieWithParticles = particleStyle.TrailDiesWithParticle;
				pTrail.ribbonCount = particleStyle.RibbonCount;
				pTrail.sizeAffectsWidth = particleStyle.ParticleSizeAffectsTrailWidth;
				pTrail.sizeAffectsLifetime = particleStyle.ParticleSizeAffectsTrailLifetime;
				pTrail.inheritParticleColor = particleStyle.InheritTrailColorFromParticle;
				pTrail.generateLightingData = particleStyle.GenerateLightingDataForTrails;
				pTrail.textureMode = (ParticleSystemTrailTextureMode)particleStyle.TrailTextureMode.Value;
				pTrail.lifetime = new UnityEngine.ParticleSystem.MinMaxCurve(particleStyle.MinTrailLifetime, particleStyle.MaxTrailLifetime);
				var c = particleStyle.MinTrailColor.Value;
				var min = c.ToUnityAuto(base.Connector.Engine);
				var c2 = particleStyle.MaxTrailColor.Value;
				pTrail.colorOverLifetime = new UnityEngine.ParticleSystem.MinMaxGradient(min, c2.ToUnityAuto(base.Connector.Engine));
				pTrail.widthOverTrail = new UnityEngine.ParticleSystem.MinMaxCurve(particleStyle.MinTrailWidth, particleStyle.MaxTrailWidth);
			}
			if (particleStyle.Light.Target == null)
			{
				pLights.enabled = false;
				return;
			}
			pLights.enabled = true;
			pLights.light = ((LightConnector)particleStyle.Light.Target.Connector).UnityLight;
			pLights.ratio = particleStyle.LightsRatio;
			pLights.useRandomDistribution = particleStyle.LightRandomDistribution;
			pLights.useParticleColor = particleStyle.LightsUseParticleColor;
			pLights.sizeAffectsRange = particleStyle.SizeAffectsLightRange;
			pLights.alphaAffectsIntensity = particleStyle.AlphaAffectsLightIntensity;
			pLights.rangeMultiplier = particleStyle.LightRangeMultiplier;
			pLights.intensityMultiplier = particleStyle.LightIntensityMultiplier;
			pLights.maxLights = particleStyle.MaximumLights;
		}
		else if (pRenderer.enabled)
		{
			pSystem.Clear();
			pRenderer.enabled = false;
		}
	}

	public void Init()
	{
		if (!ParticleSystemWorker.Initialized)
		{
			new GameObject("ParticleSystemManager").AddComponent<ParticleSystemWorker>();
			ParticleSystemWorker.RegisterJobProcessor(base.Connector.Owner.Engine.WorkProcessor);
		}
		var o = new GameObject("ParticleSystem");
		o.transform.SetParent(base.transform, worldPositionStays: false);
		pSystem = o.AddComponent<UnityEngine.ParticleSystem>();
		pRenderer = o.GetComponent<ParticleSystemRenderer>();
		var main = pSystem.main;
		main.simulationSpace = ParticleSystemSimulationSpace.World;
		main.scalingMode = ParticleSystemScalingMode.Hierarchy;
		main.useUnscaledTime = true;
		pShape = pSystem.shape;
		pTrail = pSystem.trails;
		pLights = pSystem.lights;
		var emission = pSystem.emission;
		emission.enabled = false;
	}

	public void Cleanup() => Destroy(pSystem.gameObject);

	private void LateUpdate()
	{
		if (ParticleStyle == null) return;
		var particleStyle = ParticleStyle;
		var emitParams = default(UnityEngine.ParticleSystem.EmitParams);
		emitParams.applyShapeToPosition = false;
		var particleCount = pSystem.particleCount;
		var num = pSystem.main.maxParticles - particleCount;
		var min = MathX.Max(0f, particleStyle.MinStartLifetime);
		var max = MathX.Max(0f, particleStyle.MaxStartLifetime);
		var world = FrooxEngineParticleSystem.World;
		foreach (var emitter in FrooxEngineParticleSystem.Emitters)
		{
			if (num == 0) break;
			if (emitter.CurrentCount <= 0) continue;
			var generatesColors = emitter.GeneratesColors;
			var num2 = MathX.Min(emitter.CurrentCount, num);
			_emissions = _emissions.EnsureSize(num2);
			if (generatesColors) _colors = _colors.EnsureSize(num2);
			var num3 = emitter.GenerateParticles(FrooxEngineParticleSystem.SimulationSpace.Space, num2, _emissions, generatesColors ? _colors : null);
			if (num3 == 0) continue;
			if (world.Focus == World.WorldFocus.PrivateOverlay || world.Focus == World.WorldFocus.Overlay)
			{
				var focusedWorld = world.Engine.WorldManager.FocusedWorld;
				for (var i = 0; i < num3; i++)
				{
					_emissions[i].Position = WorldManager.TransferPoint(_emissions[i].Position, world, focusedWorld);
					_emissions[i].Direction = WorldManager.TransferDirection(_emissions[i].Direction, world, focusedWorld);
				}
			}
			bool flag = particleStyle.Use3DRotation;
			for (var j = 0; j < num3; j++)
			{
				emitParams.position = _emissions[j].Position.ToUnity();
				emitParams.velocity = _emissions[j].Direction.ToUnity() * random.Range(particleStyle.MinStartSpeed.Value, particleStyle.MaxStartSpeed.Value);
				if (flag)
				{
					var randomXGenerator = random;
					float3 min2 = particleStyle.MinStartRotation3D;
					float3 max2 = particleStyle.MaxStartRotation3D;
					emitParams.rotation3D = randomXGenerator.Lerp(in min2, in max2).ToUnity();
					var randomXGenerator2 = random;
					min2 = particleStyle.MinStartAngularVelocity3D;
					max2 = particleStyle.MaxStartAngularVelocity3D;
					emitParams.angularVelocity3D = randomXGenerator2.Lerp(in min2, in max2).ToUnity();
				}
				else
				{
					emitParams.rotation = random.Range(particleStyle.MinStartRotation, particleStyle.MaxStartRotation);
					emitParams.angularVelocity = random.Range(particleStyle.MinStartAngularVelocity, particleStyle.MaxStartAngularVelocity.Value);
				}
				emitParams.startSize = random.Range(particleStyle.MinStartSize.Value, particleStyle.MaxStartSize.Value);
				emitParams.startLifetime = random.Range(min, max);
				var randomXGenerator3 = random;
				var min3 = particleStyle.MinStartColor.Value;
				var max3 = particleStyle.MaxStartColor.Value;
				var a = randomXGenerator3.Lerp(in min3, in max3).ToProfile(ColorProfile.sRGB);
				if (generatesColors)
				{
					a *= _colors[j];
				}
				var c = (color32)a;
				emitParams.startColor = c.ToUnity();
				pSystem.Emit(emitParams, 1);
			}
			num -= num3;
		}
		if (particleStyle.Collisions.Value)
		{
			ScheduleCollisionComputation();
		}
	}

	private void ScheduleCollisionComputation()
	{
		var particleCount = pSystem.particleCount;
		_particles = _particles.EnsureSize(particleCount);
		lastDeltaTime = Time.deltaTime;
		var particles = pSystem.GetParticles(_particles);
		if (particles != 0)
		{
			for (var i = 0; i < particles; i += 100)
			{
				var job = new ParticleJob(this, i, Math.Min(particles, i + 100));
				ParticleSystemWorker.RegisterJob(in job);
			}
			lastCount = particles;
			ParticleSystemWorker.RegisterJobData(this);
		}
	}
}