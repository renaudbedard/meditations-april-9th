using System.Threading.Tasks;
using UnityEngine;

public class Petal : MonoBehaviour
{
	[SerializeField] AudioSource audioSource;

    public FractalTree Tree;

	OneShotGroup group;

	void Reset()
	{
		audioSource = GetComponent<AudioSource>();
	}

	void Awake()
	{
		group = (OneShotGroup) Random.Range(0, 3);
		audioSource.enabled = false;
	}

    public void DisableAudio()
    {
        audioSource.enabled = false;
    }

	public void TriggerAudio(OneShotGroup forced)
	{
		audioSource.enabled = true;
        var clip = AudioManager.Instance.GetOneShot(forced);
        audioSource.clip = clip;
        audioSource.Play();
        Tree.QueueDisableAudioSource(this, clip.length);
	}
	public void TriggerAudio()
	{
		audioSource.enabled = true;
        var clip = AudioManager.Instance.GetOneShot(group);
        audioSource.clip = clip;
        audioSource.Play();
        Tree.QueueDisableAudioSource(this, clip.length);
    }
}
