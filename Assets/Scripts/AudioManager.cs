using UnityEngine;

public enum OneShotGroup
{
	A = 0,
	B = 3, // yeah i know
	C = 1,
	D = 2
}

public class AudioManager : MonoBehaviour
{
	public static AudioManager Instance { get; private set; }

	public AudioClip[] GroupA;
	public AudioClip[] GroupB;
	public AudioClip[] GroupC;
	public AudioClip[] GroupD;

	float[] padVolumes;
	float[] padVelocity;

	int currentPad = -1;

	[Range(1, 30)] public float SwitchPadEvery;
	[Range(0, 10)] public float PadBlendTime;

	public AudioSource[] PadSources;
	public AudioSource NoiseSource;

	float sincePadChange;

	void Awake()
	{
		Instance = this;
		padVolumes = new float[PadSources.Length];
		padVelocity = new float[PadSources.Length];
	}

	void Update()
	{
		sincePadChange += Time.deltaTime;

		if (sincePadChange >= SwitchPadEvery)
		{
			sincePadChange -= SwitchPadEvery;
			currentPad = Random.Range(0, PadSources.Length);
		}

		for (int i = 0; i < PadSources.Length; i++)
		{
			padVolumes[i] = Mathf.SmoothDamp(padVolumes[i], currentPad == i ? 1 : 0, ref padVelocity[i], PadBlendTime);
			PadSources[i].volume = padVolumes[i];
		}
	}

	public AudioClip GetOneShot(OneShotGroup group)
	{
		switch (group)
		{
			case OneShotGroup.A: return GroupA[Random.Range(0, GroupA.Length)];
			case OneShotGroup.B: return GroupB[Random.Range(0, GroupB.Length)];
			case OneShotGroup.C: return GroupC[Random.Range(0, GroupC.Length)];
			case OneShotGroup.D: return GroupD[Random.Range(0, GroupD.Length)];
		}
		return null;
	}
}
