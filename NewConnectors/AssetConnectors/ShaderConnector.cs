using System;
using System.Collections;
using System.IO;
using Elements.Core;
using FrooxEngine;
using UnityEngine;
using UnityFrooxEngineRunner;

namespace Thundagun.NewConnectors.AssetConnectors;

public class ShaderConnector : AssetConnector, IShaderConnector
{
	private AssetBundle _assetBundle;

	public UnityEngine.Shader UnityShader { get; private set; }

	public void LoadFromFile(string file, AssetIntegrated onLoaded)
	{
		UnityAssetIntegrator.EnqueueProcessing(LoadShader(file, onLoaded), highPriority: true);
	}

	private IEnumerator LoadShader(string file, AssetIntegrated onLoaded)
	{
		UnloadImmediate();
		try
		{
			var bundleRequest = AssetBundle.LoadFromFileAsync(file);
			AssetBundleRequest shaderRequest;
			bundleRequest.completed += delegate
			{
				try
				{
					_assetBundle = bundleRequest.assetBundle;
					if (_assetBundle == null)
					{
						UniLog.Warning($"Could not load shader asset bundle: {file}, exists: {File.Exists(file)}");
						onLoaded(environmentInstanceChanged: true);
					}
					else
					{
						shaderRequest = _assetBundle.LoadAssetAsync<UnityEngine.Shader>(_assetBundle.GetAllAssetNames()[0]);
						shaderRequest.completed += delegate
						{
							try
							{
								UnityShader = shaderRequest.asset as UnityEngine.Shader;
								onLoaded(environmentInstanceChanged: true);
							}
							catch (Exception arg2)
							{
								UniLog.Error($"Exception loading shader from the loaded bundle {file}\n{arg2}");
								onLoaded(environmentInstanceChanged: true);
							}
						};
					}
				}
				catch (Exception arg)
				{
					UniLog.Error($"Exception processing loaded shader bundle for {file}\n{arg}");
					onLoaded(environmentInstanceChanged: true);
				}
			};
		}
		catch (Exception ex)
		{
			UniLog.Error("Exception loading shader from file: " + file + "\n" + ex);
			throw;
		}
		yield break;
	}

	public override void Unload()
	{
		UnityAssetIntegrator.EnqueueProcessing(UnloadImmediate, highPriority: true);
	}

	private void UnloadImmediate()
	{
		if (_assetBundle != null)
		{
			_assetBundle.Unload(unloadAllLoadedObjects: true);
			if (_assetBundle) UnityEngine.Object.Destroy(_assetBundle);
		}
		if (UnityShader) UnityEngine.Object.Destroy(UnityShader);
		_assetBundle = null;
		UnityShader = null;
	}
}
