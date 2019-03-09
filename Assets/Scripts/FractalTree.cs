using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using math = Unity.Mathematics;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Rendering;
using System.Collections;
using Random = Unity.Mathematics.Random;

public enum FallState
{
    Initial = 0,
    Attached = 1,
    Flying = 2,
    Grounded = 3
}

public struct PetalData
{
    public float3 Position;
	public float3 Velocity;
    public quaternion Rotation;
    public float3 Scale;
    public float3 AngularVelocity;
	public FallState FallState;
}

public struct AudioSourceTimer
{
    public AudioSource AudioSource;
    public float Timer;
}

public class FractalTree : MonoBehaviour
{
    [Header("References")]
    [SerializeField] MeshFilter meshFilter;

    [Header("Generated Data")]
    [SerializeField] Mesh treeMesh = null;

    [Header("Generation")]
    [Range(1, 100)] public uint Seed;
    [Range(0, 16)] public int TotalDepth;
    [Range(0, 1)] public float BranchTilt;
    [Range(0, 2)] public float BranchLength;
    [Range(0, 2)] public float TiltVariation;
    [Range(0, 2)] public float TangentVariation;
    [Range(0, 1)] public float Flatness;
    [Range(1, 16)] public float TrunkDepth;
    [Range(0, 10)] public float TrunkLength;
    [Range(0, 2)] public float LengthVariation;
    [Range(0.001f, 1)] public float Radius;
    [Range(0, 10)] public float TrunkRadius;
    [Range(0, 1)] public float LeafMinSize;
    [Range(0, 2)] public float LeafMaxSize;

    [Header("Wind")]
    public ParticleSystem WindParticleSystem;
    [Range(0, 1)] public float WindDirectionVariation;
    [Range(0, 1)] public float WindDirectionProbability;
    [Range(0, 1)] public float WindDirectionReachDampen;
    [Range(0, 1)] public float WindForceBase;
    [Range(0, 1)] public float WindForceVariation;
    [Range(0, 1)] public float WindForceProbability;
    [Range(0, 10)] public float WindForceReachDampen;
    [Range(0, 1)] public float Gravity;
    [Tooltip("Leaves per second")] [Range(0, 9999)] public float FallRate;
    [Range(0, 2)] public float PetalRotationForce;

    [Header("Petal Mesh Data")]
    public Material PetalMaterial;
    public Mesh PetalMesh;

    #region Generation

    struct Branch
    {
        public float3 From, To;
        public quaternion Rotation;
        public int Depth;
        public float Trunkness;
    }

    struct Leaf
    {
        public float3 Position;
        public float3 Normal;
        public quaternion LookAt;
        public float3 Scale;
    }

    readonly List<Branch> branches = new List<Branch>();
    readonly List<Leaf> leaves = new List<Leaf>();

    void Reset()
    {
        meshFilter = GetComponent<MeshFilter>();
    }

    void Refresh()
    {
#if UNITY_EDITOR
        if (!treeMesh || !meshFilter.sharedMesh)
            meshFilter.sharedMesh = treeMesh = new Mesh();
#endif

        leaves.Clear();
        branches.Clear();

        var random = new Random(Seed);
        BuildSubtree(float3(0, 0, 0), math.quaternion.identity, TotalDepth, random);

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var indices = new List<int>();
        var texCoords = new List<Vector4>();

        for (int i = 0; i < branches.Count; i++)
        {
            var branch = branches[i];

            var baseNormalizedDepth = ((float)branch.Depth + 1) / (TotalDepth + 1);
            var topNormalizedDepth = max((float)branch.Depth / (TotalDepth + 1), 0.001f);

            var baseRadius = Radius * baseNormalizedDepth;
            var topRadius = Radius * topNormalizedDepth;

            var baseTrunkness = 1 - saturate(((float)TotalDepth - branch.Depth) / TrunkDepth);
            var topTrunkness = 1 - saturate(((float)TotalDepth - (branch.Depth - 1)) / TrunkDepth);

            baseRadius *= lerp(1, 1 + TrunkRadius, baseTrunkness);
            topRadius *= lerp(1, 1 + TrunkRadius, topTrunkness);

            int segments = Mathf.RoundToInt(Mathf.Lerp(4, 7, baseNormalizedDepth));

            var direction = normalize(branch.To - branch.From);

            int fromVertex = vertices.Count;

            for (int j = 0; j < segments; j++)
            {
                var angle = (float)j / segments * Mathf.PI * 2;
                var offset = mul(branch.Rotation, float3(cos(angle), 0, sin(angle)) * baseRadius);
                vertices.Add(branch.From + offset);
                normals.Add(normalize(offset));

                var bendability = 1 - baseNormalizedDepth;
                texCoords.Add(new Vector4(bendability, 0, 0, 0));

                offset = mul(branch.Rotation, float3(cos(angle), 0, sin(angle)) * topRadius);
                vertices.Add(branch.To + offset);
                normals.Add(normalize(offset));

                bendability = 1 - topNormalizedDepth;
                texCoords.Add(new Vector4(bendability, 0, 0, 0));
            }

            int toVertex = vertices.Count;

            var range = toVertex - fromVertex;
            for (int j = 0; j < range; j += 2)
            {
                indices.Add(WrapIndex(fromVertex, j, range));
                indices.Add(WrapIndex(fromVertex, j + 1, range));
                indices.Add(WrapIndex(fromVertex, j + 2, range));

                indices.Add(WrapIndex(fromVertex, j + 2, range));
                indices.Add(WrapIndex(fromVertex, j + 1, range));
                indices.Add(WrapIndex(fromVertex, j + 3, range));
            }
        }

        treeMesh.Clear();

        treeMesh.indexFormat = IndexFormat.UInt32;
        treeMesh.subMeshCount = 1;

        treeMesh.SetVertices(vertices);
        treeMesh.SetUVs(0, texCoords);
        treeMesh.SetIndices(indices.ToArray(), MeshTopology.Triangles, 0);
        treeMesh.SetNormals(normals);

        treeMesh.RecalculateBounds();
    }

    int WrapIndex(int @base, int offset, int limit)
    {
        return (offset % limit) + @base;
    }

    void OnValidate()
    {
        Refresh();
    }

    void BuildSubtree(float3 origin, quaternion rotation, int depth, math.Random rng)
    {
        var normalizedDepth = ((float)depth + 1) / (TotalDepth + 1);
        var trunkness = 1 - saturate(((float)TotalDepth - depth) / TrunkDepth);
        var lengthModifier = lerp(1, 1 + TrunkLength, trunkness);

        lengthModifier += rng.NextFloat(-1, 1) * LengthVariation;

        var dir = rotate(rotation, float3(0, 1, 0));
        var dest = origin + dir * BranchLength * normalizedDepth * lengthModifier;

        branches.Add(new Branch
        {
            From = origin,
            To = dest,
            Rotation = rotation,
            Depth = depth,
            Trunkness = trunkness
        });

        if (depth > 0)
        {
            var commonYaw = math.quaternion.AxisAngle(new float3(0, 1, 0), rng.NextFloat() * (float)PI * 2);
            var flatYaw = math.quaternion.identity;

            for (int i = 0; i < 2; i++)
            {
                var randomYaw = math.quaternion.AxisAngle(new float3(0, 1, 0), rng.NextFloat() * (float)PI * 2);
                var actualYaw = slerp(slerp(commonYaw, randomYaw, TangentVariation), flatYaw, Flatness);
                var tangent = rotate(actualYaw, float3(0, 0, 1));

                var tiltAngle = lerp(BranchTilt, rng.NextFloat() * BranchTilt * 2, TiltVariation);
                var actualTiltAngle = lerp(-1, 1, i) * tiltAngle;
                var pitch = math.quaternion.AxisAngle(tangent, actualTiltAngle);

                BuildSubtree(dest, mul(pitch, rotation), depth - 1, rng);
            }
        }
        else
        {
            var leaf = new Leaf
            {
                Position = dest,
                Normal = dest - origin,
            };

            leaf.LookAt = Quaternion.LookRotation(leaf.Normal);
            leaf.Scale = float3(rng.NextFloat(LeafMinSize, LeafMaxSize));

            leaves.Add(leaf);
        }
    }

    #endregion

    NativeArray<PetalData> petalData;
    NativeQueue<int> groundedIndices, risingIndices;
    JobHandle petalUpdateJobHandle;
    Queue<int> detachableIndices;

    Pool<AudioSource> audioSources;
    readonly List<AudioSourceTimer> audioDisableQueue = new List<AudioSourceTimer>();

    NativeArray<Matrix4x4> petalMatrices;
    readonly Matrix4x4[] matrixBuffer = new Matrix4x4[1022];

    CommandBuffer depthCommandBuffer;
    CommandBuffer opaqueCommandBuffer;

    NativeArray<float> instanceData;
    readonly float[] instanceDataBuffer = new float[1022];
    readonly List<MaterialPropertyBlock> instanceDataPropertyBlocks = new List<MaterialPropertyBlock>();
    int shadowCasterPassId, attachStatePropertyId;

    float fallTimer;

    float3 windBaseDirection, windBaseTangent;
    float3 windDirectionRandomTarget;
    Vector3 windDirectionVelocity;
    float3 windDirection;
    float windForce;
    float windForceRandomTarget;
    float windForceVelocity;
    Random windRng;
    float shakeAccumulator;

    float shake;
    float bendAngle;
    Vector3 bendAxis;

    public bool DoRise { private get; set; }

    float sinceTrig;

    Camera mainCamera;

    void Awake()
    {
        audioSources = new Pool<AudioSource>(() =>
            {
                var audioSourceGO = new GameObject("Petal AudioSource");
                var audioSource = audioSourceGO.AddComponent<AudioSource>();

                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1;
                audioSource.minDistance = 2;
                audioSource.maxDistance = 100;
                audioSource.rolloffMode = AudioRolloffMode.Logarithmic;

                audioSource.enabled = false;
                audioSourceGO.transform.SetParent(transform);
                return audioSource;
            },
            audioSource =>
            {
                audioSource.Stop();
                audioSource.clip = null;
                audioSource.enabled = false;
            }
        )
        {
            Capacity = 1000
        };

        Seed = new uint[] { 43, 4, 12, 25, 70, 81, 92, 100 }.Shuffle(new Random((uint) DateTime.Now.Ticks)).First();
		Refresh();

		windRng = new Random(Seed);

        PetalMesh = new Mesh
        {
            vertices = new Vector3[]
            {
                float3(-0.5f, -0.5f, 0), float3(0.5f, -0.5f, 0), float3(-0.5f, 0.5f, 0), float3(0.5f, 0.5f, 0),
                float3(-0.5f, -0.5f, 0), float3(0.5f, -0.5f, 0), float3(-0.5f, 0.5f, 0), float3(0.5f, 0.5f, 0)
            },
            triangles = new int[]
            {
                0, 1, 2, 2, 1, 3,
                4, 6, 5, 6, 7, 5
            },
            normals = new Vector3[]
            {
                float3(0, 0, 1), float3(0, 0, 1), float3(0, 0, 1), float3(0, 0, 1),
                float3(0, 0, -1), float3(0, 0, -1), float3(0, 0, -1), float3(0, 0, -1)
            }
        };
        PetalMesh.UploadMeshData(true);

		depthCommandBuffer = new CommandBuffer();
		opaqueCommandBuffer = new CommandBuffer();

        mainCamera = Camera.main;

        mainCamera.AddCommandBuffer(CameraEvent.AfterDepthTexture, depthCommandBuffer);
        mainCamera.AddCommandBuffer(CameraEvent.AfterForwardOpaque, opaqueCommandBuffer);
		FindObjectOfType<Light>().AddCommandBuffer(LightEvent.AfterShadowMapPass, depthCommandBuffer);

        shadowCasterPassId = PetalMaterial.FindPass("ShadowCaster");
        attachStatePropertyId = Shader.PropertyToID("_AttachState");
    }

	void Start()
	{
		petalData = new NativeArray<PetalData>(leaves.Count, Allocator.Persistent);
        groundedIndices = new NativeQueue<int>(Allocator.Persistent);
        detachableIndices = new Queue<int>();
        petalMatrices = new NativeArray<Matrix4x4>(leaves.Count, Allocator.Persistent);
        instanceData = new NativeArray<float>(leaves.Count, Allocator.Persistent);
        risingIndices = new NativeQueue<int>(Allocator.Persistent);

        var orderedLeaves = leaves.Shuffle(windRng).ToArray();

        for (int i = 0; i < leaves.Count; i++)
        {
            detachableIndices.Enqueue(i);
            instanceData[i] = 1;
            petalData[i] = new PetalData
            {
                Position = orderedLeaves[i].Position,
                Rotation = orderedLeaves[i].LookAt,
                Scale = orderedLeaves[i].Scale
            };
        }
		for (int i = 0; i < leaves.Count; i += instanceDataBuffer.Length)
			instanceDataPropertyBlocks.Add(new MaterialPropertyBlock());

		windDirection = windBaseDirection = WindParticleSystem.transform.forward;
		windBaseTangent = WindParticleSystem.transform.right;
		windForceRandomTarget = WindForceBase + windRng.NextFloat(-WindForceVariation, WindForceVariation);
		windDirectionRandomTarget = mul(math.quaternion.AxisAngle(windBaseTangent, (float)windRng.NextDouble(-PI, PI) * WindDirectionVariation),
			(mul(math.quaternion.AxisAngle(Vector3.up, (float)windRng.NextDouble(-PI, PI) * WindDirectionVariation),
			windBaseDirection)));
		windForce = 0;
	}

	void OnDestroy()
	{
        instanceData.Dispose();
		petalData.Dispose();
		groundedIndices.Dispose();
		petalMatrices.Dispose();
        risingIndices.Dispose();
    }

	IEnumerator CloseIn(float seconds)
	{
        yield return new WaitForSeconds(seconds);
		CameraControl.Instance.DoClose(5);
	}

    public void QueueReturnAudioSource(AudioSource audioSource, float time)
    {
        audioDisableQueue.Add(new AudioSourceTimer { AudioSource = audioSource, Timer = time });
    }

	void Update()
	{
		if (Probability(WindDirectionProbability))
		{
			windDirectionRandomTarget = mul(math.quaternion.AxisAngle(windBaseTangent, (float)windRng.NextDouble(-PI, PI) * WindDirectionVariation),
				(mul(math.quaternion.AxisAngle(Vector3.up, (float)windRng.NextDouble(-PI, PI) * 0.5f * WindDirectionVariation),
				windBaseDirection)));
		}
		windDirection = normalize(Vector3.SmoothDamp(windDirection, windDirectionRandomTarget, ref windDirectionVelocity, WindDirectionReachDampen));

		if (Probability(WindForceProbability))
			windForceRandomTarget = WindForceBase + pow(windRng.NextFloat(), 2) * WindForceVariation;
		windForce = Mathf.SmoothDamp(windForce, windForceRandomTarget, ref windForceVelocity, WindForceReachDampen);

		bendAngle = -windForce / (WindForceBase + WindForceVariation) * 0.17f;
		shakeAccumulator += Time.deltaTime * 30 * abs(bendAngle);

        bendAxis = cross(windDirection, float3(0, 1, 0));
        shake = sin(shakeAccumulator) * cos(shakeAccumulator / 2) * sin(shakeAccumulator / 4) * sin(shakeAccumulator / 8);

        // detach petals
        fallTimer += Time.deltaTime;
        while (fallTimer > 1 / FallRate && detachableIndices.Count > 0)
        {
            fallTimer -= 1 / FallRate;

            var petalIndex = detachableIndices.Dequeue();
            var data = petalData[petalIndex];
            data.FallState = FallState.Flying;
            petalData[petalIndex] = data;

            instanceData[petalIndex] = 0;

            // bake the vertex shader animation in its transform
            float variation = shake * 0.15f * abs(bendAngle);

            // TODO: how do we do this without a transform?
            // orderedPetals[petalIndex].transform.RotateAround(Vector3.zero, bendAxis, Mathf.Rad2Deg * ((bendAngle + variation) * 0.5f));

            // no more petals!
            if (detachableIndices.Count == 0)
            {
                Debug.Log("No more petals, closing...");
                StartCoroutine(CloseIn(15));
                break;
            }
        }

        groundedIndices.Clear();
        risingIndices.Clear();
        var petalUpdateJob = new PetalUpdateJob
		{
			DeltaTime = Time.deltaTime,
			Gravity = Gravity,
			PetalRotationForce = Mathf.Deg2Rad * PetalRotationForce,
			WindDirection = windDirection,
			WindForce = windForce,
            GroundedIndices = groundedIndices.ToConcurrent(),
			DataArray = petalData,
            MatricesArray = petalMatrices,
            RisingIndices = risingIndices.ToConcurrent(),
            CameraPosition = mainCamera.transform.localPosition,
            RandomSeed = (uint)Time.frameCount,
            DoRise = DoRise
        };
        DoRise = false;

        petalUpdateJobHandle = petalUpdateJob.Schedule(petalData.Length, petalData.Length / 8);

        JobHandle.ScheduleBatchedJobs();

        var normalizedWindForce = saturate(abs(windForce) / (WindForceBase + WindForceVariation));
        AudioManager.Instance.NoiseSource.volume = normalizedWindForce;
        var mainModule = WindParticleSystem.main;
        mainModule.startSpeed = new ParticleSystem.MinMaxCurve(1, normalizedWindForce * 10);
        WindParticleSystem.transform.LookAt(WindParticleSystem.transform.position + new Vector3(windDirection.x, 0, windDirection.z), Vector3.up);

        Shader.SetGlobalVector("_BendAxis", bendAxis);
        Shader.SetGlobalFloat("_BendAngle", bendAngle);
        Shader.SetGlobalFloat("_Shake", shake);

        for (int i = audioDisableQueue.Count - 1; i >= 0; i--)
        {
            var queueElement = audioDisableQueue[i];
            queueElement.Timer -= Time.deltaTime;
            if (queueElement.Timer <= 0)
            {
	            audioSources.Return(queueElement.AudioSource);
	            audioDisableQueue.RemoveAtSwapBack(i);
            }
            else
	            audioDisableQueue[i] = queueElement;
        }
    }

	void LateUpdate()
	{
        petalUpdateJobHandle.Complete();

        sinceTrig += Time.deltaTime;
        if (sinceTrig > 0.15f && risingIndices.TryDequeue(out int risingPetalIndex))
        {
            PlayAudioClip(OneShotGroup.B, risingPetalIndex);
            sinceTrig = 0;
        }

        while (groundedIndices.TryDequeue(out int groundedPetalIndex))
        {
            PlayAudioClip((OneShotGroup)UnityEngine.Random.Range(0, 3), groundedPetalIndex);
        }

		depthCommandBuffer.Clear();
		opaqueCommandBuffer.Clear();

		for (int i = 0, blockIndex = 0; i < leaves.Count; i += matrixBuffer.Length, blockIndex++)
		{
			var batchSize = Math.Min(leaves.Count - i, 1022);

			var propertyBlock = instanceDataPropertyBlocks[blockIndex];
			instanceData.Slice(i, batchSize).CopyToFast(instanceDataBuffer);
			propertyBlock.SetFloatArray(attachStatePropertyId, instanceDataBuffer);

            petalMatrices.Slice(i, batchSize).CopyToFast(matrixBuffer);

            depthCommandBuffer.DrawMeshInstanced(PetalMesh, 0, PetalMaterial, shadowCasterPassId, matrixBuffer, batchSize, propertyBlock);
			opaqueCommandBuffer.DrawMeshInstanced(PetalMesh, 0, PetalMaterial, 0, matrixBuffer, batchSize, propertyBlock);
		}
	}

    void PlayAudioClip(OneShotGroup group, int petalIndex)
    {
        var petalPosition = petalData[petalIndex].Position;
        var distanceToCamera = distance(petalPosition, mainCamera.transform.position);

        if (distanceToCamera >= 100)
            return;

        var audioSource = audioSources.Take();
        audioSource.enabled = true;

        audioSource.transform.position = petalPosition;
        audioSource.priority = 2 + (int) floor(distanceToCamera);

        var clip = AudioManager.Instance.GetOneShot(group);
        audioSource.clip = clip;
        audioSource.Play();

        QueueReturnAudioSource(audioSource, clip.length);
    }

    [BurstCompile(FloatMode = FloatMode.Fast)]
	private struct PetalUpdateJob : IJobParallelFor
	{
		[ReadOnly] public float Gravity;
        [ReadOnly] public float DeltaTime;
        [ReadOnly] public float3 WindDirection;
        [ReadOnly] public float3 WindForce;
        [ReadOnly] public float PetalRotationForce;
        [ReadOnly] public float3 CameraPosition;
        [ReadOnly] public uint RandomSeed;
        [ReadOnly] public bool DoRise;

        public NativeArray<PetalData> DataArray;

        [WriteOnly] public NativeQueue<int>.Concurrent GroundedIndices;
        [WriteOnly] public NativeQueue<int>.Concurrent RisingIndices;
        [WriteOnly] public NativeArray<Matrix4x4> MatricesArray;

        public void Execute(int i)
		{
            bool dirty = false;

			var petalData = DataArray[i];

            // TODO it would be more cache-friendly to have petals with the same state close together in memory... how?

            if (petalData.FallState == FallState.Initial)
            {
                // initial copy
                petalData.FallState = FallState.Attached;
                dirty = true;
            }
            else if (petalData.FallState == FallState.Flying)
            {
                // flying leaves
                petalData.Velocity.y -= Gravity * DeltaTime;
                petalData.Velocity += WindDirection * WindForce * DeltaTime;

                petalData.AngularVelocity += WindDirection * DeltaTime * PetalRotationForce;

                petalData.Position += petalData.Velocity;

                petalData.Rotation = mul(petalData.Rotation, math.quaternion.Euler(petalData.AngularVelocity));

                if (petalData.Position.y <= 0)
                {
                    // newly grounded
                    petalData.Position.y = 0;
                    petalData.FallState = FallState.Grounded;
                    petalData.Velocity.y = 0;
                    GroundedIndices.Enqueue(i);
                }

                dirty = true;
            }
            else if (DoRise && petalData.FallState == FallState.Grounded && distance(petalData.Position, CameraPosition) < 6)
            {
                // rising leaves
                petalData.AngularVelocity = Mathf.Deg2Rad * new Random(RandomSeed + (uint)i).NextFloat3Direction();
                petalData.Velocity = normalize(petalData.Position - CameraPosition) * 0.05f;
                petalData.Velocity.y = 0.05f;
                petalData.Position.y += 0.001f;

                petalData.FallState = FallState.Flying;

                dirty = true;

                RisingIndices.Enqueue(i);
            }
            else if (petalData.FallState == FallState.Grounded && (lengthsq(petalData.Velocity.xz) > 0.001f || lengthsq(petalData.AngularVelocity.xz) > 0.001f))
            {
                // roll & decay
                petalData.Velocity = Damp(petalData.Velocity, 0.001f, DeltaTime);
                petalData.AngularVelocity = Damp(petalData.AngularVelocity, 0.001f, DeltaTime);

                petalData.Position += petalData.Velocity;

                petalData.Rotation = mul(petalData.Rotation, math.quaternion.Euler(petalData.AngularVelocity));

                dirty = true;
            }

            if (dirty)
            {
                DataArray[i] = petalData;
                MatricesArray[i] = math.float4x4.TRS(petalData.Position, petalData.Rotation, petalData.Scale);
            }
        }
	}

    public static bool Probability(float aP)
	{
		return UnityEngine.Random.value < 1f - Mathf.Pow(1f - aP, Time.deltaTime);
	}

    public static float Damp(float source, float smoothing, float dt)
    {
        return source * pow(smoothing, dt);
    }
    public static float3 Damp(float3 source, float smoothing, float dt)
    {
        return source * pow(smoothing, dt);
    }
}

public static class IEnumerableExtensions
{
	public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source, Random rng)
	{
		T[] elements = source.ToArray();
		for (int i = elements.Length - 1; i >= 0; i--)
		{
			int swapIndex = rng.NextInt(i + 1);
			yield return elements[swapIndex];
			elements[swapIndex] = elements[i];
		}
	}
}
