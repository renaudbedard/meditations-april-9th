using MiniEngineAO;
using Tayx.Graphy;
using UnityEngine;

internal class QualityChooser : MonoBehaviour
{
	GraphyManager graphy;

	[SerializeField] AmbientOcclusion ambientOcclusion = null;
	[SerializeField] RenderTexture renderTarget = null;

	[SerializeField] [Range(0, 240)] int framerateThreshold = 45;
	[SerializeField] [Range(0, 5)] float timeThreshold = 0.5f;
	[SerializeField] [Range(0, 2)] float qualityDelectDelay = 0.5f;
	[SerializeField] [Range(0, 10)] float fixedQualityTime = 6;

	float accumulatedTime;
	float renderScale;
	float sinceGameStarted;
	float minTimeSinceGameStarted;

	private void Awake()
	{
		graphy = GraphyManager.Instance;
		SetRenderScale(1);
		QualitySettings.SetQualityLevel(2);
		SyncQuality();
	}
	
	private void SetRenderScale(float renderScale)
	{
		this.renderScale = renderScale;

		if (renderTarget.IsCreated())
			renderTarget.Release();

		renderTarget.width = Mathf.RoundToInt(Screen.width * renderScale);
		renderTarget.height = Mathf.RoundToInt(Screen.height * renderScale);

		renderTarget.Create();
	}

	private void Update()
	{
		sinceGameStarted += Time.deltaTime;

		if (sinceGameStarted < qualityDelectDelay)
			return;
		
		if (sinceGameStarted > fixedQualityTime)
		{
			enabled = false;
			return;
		}
		
		if (graphy.CurrentFPS < framerateThreshold)
		{
			accumulatedTime += Time.deltaTime;
			if (accumulatedTime > timeThreshold)
			{
				Debug.Log($"FPS is too low! (current: {graphy.CurrentFPS})");
				DecreaseQuality();
				accumulatedTime = 0;
			}
		}
		else
			accumulatedTime = Mathf.Max(accumulatedTime - Time.deltaTime, 0);
	}

	void DecreaseQuality()
	{
		if (renderTarget.height > 1080)
		{
			SetRenderScale(renderScale - 0.25f);
			Debug.Log($"Decreased render scale to {renderScale}");
			return;
		}

		var qualityLevel = QualitySettings.GetQualityLevel();

		if (qualityLevel == 0)
			return;

		qualityLevel--;

		QualitySettings.SetQualityLevel(qualityLevel);
		Debug.Log($"Decreased quality level to {qualityLevel}");

		SyncQuality();
	}

	void SyncQuality()
	{
		var qualityLevel = QualitySettings.GetQualityLevel();

		switch (qualityLevel)
		{
			case 0:
				ambientOcclusion.enabled = false;
				break;

			case 1:
				ambientOcclusion.enabled = true;
				if (renderTarget.IsCreated())
					renderTarget.Release();
				renderTarget.antiAliasing = 1;
				renderTarget.Create();
				break;

			case 2:
				ambientOcclusion.enabled = true;
				if (renderTarget.IsCreated())
					renderTarget.Release();
				renderTarget.antiAliasing = 4;
				renderTarget.Create();
				break;
		}
	}

}
