using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CameraControl : MonoBehaviour
{
	public static CameraControl Instance { get; private set; }

	[Range(1, 100)] public float Speed;
	
	public Image fadeImage;
	public CanvasGroup controlsGroup;

	Vector3 destination;
	Vector3 velocity;

	Vector3 destinationAngles;
	Vector3 angularVelocity;

	FractalTree tree;

	void Awake()
	{
		fadeImage.color = Color.white;
		fadeImage.CrossFadeAlpha(1, 0, true);

		controlsGroup.alpha = 1;
		StartCoroutine(FadeControlsOut(2, 1));

		Instance = this;

		destination = transform.localPosition;
		destinationAngles = transform.eulerAngles;

		tree = FindObjectOfType<FractalTree>();

		if (!Application.isEditor)
		{
			Cursor.visible = false;
			Cursor.lockState = CursorLockMode.Locked;
		}
	}

	IEnumerator FadeControlsOut(float delay, float overSeconds)
	{
		yield return new WaitForSeconds(delay);
		
		float timer = 0;
		while (timer < overSeconds)
		{
			controlsGroup.alpha = 1 - Mathf.Clamp01(timer / overSeconds);
			timer += Time.deltaTime;
			yield return null;
		}
	}

	void Start()
	{
		StartCoroutine(FadeIn());
	}

	IEnumerator FadeIn()
	{
		yield return new WaitForSeconds(1);

		for (float i = 0; i < 1; i += Time.deltaTime)
		{
			AudioListener.volume = i;
			yield return new WaitForEndOfFrame();
		}

		fadeImage.CrossFadeAlpha(0, 2, true);
	}

	void Update()
	{
		var w = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
		var a = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
		var s = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
		var d = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);

		var camTransform = transform;

		destination += camTransform.forward * Time.deltaTime * (w ? 1 : 0) * Speed;
		destination += camTransform.right * Time.deltaTime * (d ? 1 : 0) * Speed;
		destination += -camTransform.right * Time.deltaTime * (a ? 1 : 0) * Speed;
		destination += -camTransform.forward * Time.deltaTime * (s ? 1 : 0) * Speed;

		destination.y = 2.5f;

		camTransform.localPosition = Vector3.SmoothDamp(camTransform.localPosition, destination, ref velocity, 0.1f);

		destinationAngles += new Vector3(-Input.GetAxis("Mouse Y"), Input.GetAxis("Mouse X"), 0);
		
		if (destinationAngles.x < 280 && destinationAngles.x > 90)
			destinationAngles.x = 280;
		if (destinationAngles.x > 80 && destinationAngles.x < 270)
			destinationAngles.x = 80;		

		var currentAngles = camTransform.localEulerAngles;

		currentAngles.x = Mathf.SmoothDampAngle(currentAngles.x, destinationAngles.x, ref angularVelocity.x, 0.05f);
		currentAngles.y = Mathf.SmoothDampAngle(currentAngles.y, destinationAngles.y, ref angularVelocity.y, 0.05f);
		
		camTransform.localEulerAngles = currentAngles;

        if (w || s || d || a)
            tree.DoRise = true;

		if (Input.GetKeyDown(KeyCode.Escape))
        { 
            if (Application.isEditor)
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
            else
                DoClose(1);
        }
	}

    Coroutine closeCoroutine;
	public void DoClose(float inSeconds)
	{
		if (closeCoroutine != null)
			return;

        closeCoroutine = StartCoroutine(Close(inSeconds));
	}

	private IEnumerator Close(float inSeconds)
	{
		fadeImage.color = Color.white;
		fadeImage.CrossFadeAlpha(0, 0, true);
		fadeImage.CrossFadeAlpha(1, inSeconds, true);

		for (float i=0; i<1; i+=Time.deltaTime / inSeconds)
		{
			AudioListener.volume = 1 - i;
			yield return new WaitForEndOfFrame();
		}

		Application.Quit();
	}
}
