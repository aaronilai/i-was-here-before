// using System.Linq;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using Unity.Burst;
using Firefly;

public class MeshTransforms : MonoBehaviour
{
    public GameObject planeToMod;
    public AudioSource audioSource;
    Mesh mesh;
    Vector3[] vertices;
    NativeArray<Vector3> vertexArray;
    [SerializeField] public float updateMeshStep = 0.01f;
    [SerializeField] public float decayTime = 0.01f;
    [SerializeField] public bool _useJobs;
    [SerializeField] public int sampleDataLength = 1024;
    [SerializeField] public int batches = 10;


    private float currentUpdateTime = 0f;

    private float clipLoudness;
    private float objectToRMS;
    private float[] clipSampleData;
    private List<int>[] verticesBucketList;
    private float displacement;
    private float decay;
    private float maxValue = 20;
    private float minValue = 1;

    JobHandle handle;
    float currentStep = 0f;

    private void Start()
    {
        mesh = planeToMod.GetComponent<MeshFilter>().mesh;
        mesh.MarkDynamic();
        vertices = mesh.vertices;
        vertexArray = new NativeArray<Vector3>(vertices, Allocator.TempJob);
    }

    private void Awake()
    {
        if (!planeToMod)
        {
            Debug.LogError(GetType() + ".Awake: there was no Mesh set.");
        }
    }

    // Update is called once per frame
    private void Update()
    {

        if (_useJobs)
        {
            ExecuteMeshJobs();

        }
        else
        {
            MeshCalc();
        }
        //}


    }
    private void AudioCalc()
    {
        currentUpdateTime += Time.deltaTime;
        if (currentUpdateTime >= updateMeshStep)
        {
            currentUpdateTime = 0f;
            audioSource.clip.GetData(clipSampleData, audioSource.timeSamples); //I read 1024 samples, which is about 80 ms on a 44khz stereo clip, beginning at the current sample position of the clip.
            clipLoudness = 0f;
            foreach (var sample in clipSampleData)
            {
                clipLoudness += Mathf.Abs(sample);
            }
            clipLoudness /= sampleDataLength; //clipLoudness is what you are looking for
            displacement = (clipLoudness * minValue) + maxValue;
        }
    }
    private void MeshCalc()
    {
        currentStep += Time.deltaTime;
        if (currentStep > updateMeshStep)
        {
            currentStep = 0f;
            for (var i = 0; i < vertices.Length; i++)
            {
                if (vertices[i].z >= 0f)
                {
                    vertices[i].z -= vertices[i].z * decayTime;
                }
                else
                {
                    vertices[i].z += decayTime;
                }
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }
    }

    unsafe private void ExecuteMeshJobs()
    {

        var job = new ParallelMeshJob1
        {
            Vertices = vertexArray,
            DeltaTime = Time.deltaTime,
            Step = updateMeshStep,
            Decay = decayTime
        };
        handle = job.Schedule(vertices.Length, 64);
        //var generateMeshJob = new ParallelMeshJob1
        //{
        //    Vertices = UnsafeUtility.AddressOf(ref vertices[0]),
        //}
        //.Schedule();

    }
    public void LateUpdate()
    {
        if (_useJobs)
        {
            handle.Complete();

            vertexArray.CopyTo(vertices);
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }


    }
    private void OnDestroy()
    {
        if (_useJobs)
        {
            vertexArray.Dispose();
        }
    }

}

//[BurstCompile]
//[BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
//public struct ParallelMeshJob : IJobParallelFor
//{
//    public NativeArray<Vector3> Vertices;
//    public float Decay;
//    public float DeltaTime;
//    public float Step;
//    public void Execute(int i)
//    {
//        var vertex = Vertices[i];
//        var currentStep = 0f;
//        currentStep += DeltaTime;
//        if (currentStep > Step)
//        {
//            currentStep = 0f;
//            if (vertex.z > 0)
//            {
//                vertex.z -= vertex.z * Decay;
//            }
//            else
//            {
//                vertex.z += Decay;
//            }

//            Vertices[i] = vertex;
//        }
//    }
//}
[BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
unsafe public struct ParallelMeshJob1 : IJobParallelFor
{
    public NativeArray<Vector3> Vertices;
    public float Decay;
    public float DeltaTime;
    public float Step;
    //public NativeCounter.Concurrent Counter;
    //[NativeDisableUnsafePtrRestriction] public void* Vertices;
    public void Execute(int i)
    {

        var currentStep = 0f;
        currentStep += DeltaTime;
        var vertex = Vertices[i];
        if (currentStep > Step)
        {
            currentStep = 0f;
            if (vertex.z > 0)
            {
                vertex.z -= vertex.z * Decay;
            }
            else
            {
                vertex.z += Decay;
            }

            Vertices[i] = vertex;
        }
        //UnsafeUtility.WriteArrayElement(Vertices, i, vertex);
    }
}