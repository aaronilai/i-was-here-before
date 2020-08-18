using System.Linq;
using System.Collections.Generic;
using Unity.Collections; 
using Unity.Jobs;
using UnityEngine;

public class PlanteTestOriginal : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject planeToMod;
    public AudioSource audioSource;
    Mesh mesh;
    Vector3[] vertices;
    Vector3[] verticesOriginal;

    [SerializeField] public float _maxScale=10f;
    [SerializeField] public float _minScale=1f;

    [SerializeField] public float updateStep = 0.01f;
    [SerializeField] public float updateMeshStep = 0.01f;
    [SerializeField] public float decayTime = 0.001f;
    [SerializeField] public bool constantWave = false;
    [SerializeField] public bool _useJobs;
    [SerializeField] public int sampleDataLength = 1024;
    [SerializeField] public int batches = 10;


    private float currentUpdateTime = 0f;

    private float clipLoudness;
    private float[] clipSampleData;
    private List<int>[] verticesBucketList;
    private float displacement;
    private float maxValue = 20;
    private float minValue = 1;
    private float bucketSize;
    [SerializeField] public int bucketNum = 128;
    private int lastBucketNum;
    private float[] buckets;
    int index = 0;
    private void Start()
    {
        lastBucketNum = bucketNum;
        mesh = planeToMod.GetComponent<MeshFilter>().mesh;
        vertices = mesh.vertices;
        verticesOriginal = mesh.vertices;
        calcYVertices();
    }

    private void Awake()
    {
        if (!planeToMod)
        {
            Debug.LogError(GetType() + ".Awake: there was no Mesh set.");
        }
        if (!audioSource)
        {
            Debug.LogError(GetType() + ".Awake: there was no audioSource set.");
        }
        clipSampleData = new float[sampleDataLength];

    }

    // Update is called once per frame
    private void Update()
    {
        AudioCalc();
        MeshCalc();


    }

    private void AudioCalc()
    {
        currentUpdateTime += Time.deltaTime;
        if (currentUpdateTime >= updateStep)
        {

            currentUpdateTime = 0f;
            audioSource.clip.GetData(clipSampleData, sampleDataLength); //I read 1024 samples, which is about 80 ms on a 44khz stereo clip, beginning at the current sample position of the clip.
            clipLoudness = 0f;
            foreach (var sample in clipSampleData)
            {
                clipLoudness += Mathf.Abs(sample);
            }
            clipLoudness /= sampleDataLength; //clipLoudness is what you are looking for
            displacement = (clipLoudness * _maxScale) + _minScale;
        }
    }

    private void calcYVertices()
    {
        buckets = new float[bucketNum];
        verticesBucketList = new List<int>[bucketNum];
        for (var i = 0; i < bucketNum; i++)
        {
            verticesBucketList[i] = new List<int>();
        }

        float range;

        Debug.Log("Calculated Vertices buckets");

        //maxValue = yValues.Max();
        //minValue = yValues.Min();
        for (var i = 0; i < vertices.Length; i++)
        {
            if (vertices[i].y > maxValue)
            {
                maxValue = vertices[i].y;
            }
            if (vertices[i].y < minValue)
            {
                minValue = vertices[i].y;
            }
        }
        range = maxValue - minValue;
        bucketSize = range / bucketNum;

        for (var i = 0; i < buckets.Length; i++)
        {
            buckets[i] = (bucketSize * i) + minValue;
        }
        int vertexListIndex = 0;
        for (var i = 0; i < vertices.Length; i++)
        {
            for (var f = 0; f < buckets.Length; f++)
            {
                if (constantWave == true)
                {
                    //all behind the wave is on the bucket
                    if (vertices[i].y < buckets[f] + bucketSize)
                    {
                        verticesBucketList[f].Add(i);
                        vertexListIndex++;
                    }
                }
                else
                {
                    //each bucket is filled independently, only the wave changes
                    if (vertices[i].y > buckets[f] && vertices[i].y < buckets[f] + bucketSize)
                    {
                        verticesBucketList[f].Add(i);
                        vertexListIndex++;
                    }
                }
            }
        }
    }


    private void MeshCalc()
    {
        if (lastBucketNum != bucketNum)
        {
            calcYVertices();
            lastBucketNum = bucketNum;
            index = 0;
        }
        //currentUpdateTime2 += Time.deltaTime;
        //if (currentUpdateTime2 >= updateMeshStep)
        //{
        //    currentUpdateTime2 = 0;

        //    shouldUpdate = false;
        //}

        if (_useJobs)
        {
            //var localVertexBucket = new int[verticesBucketList[index].Count];
            //for (var x = 0; x < verticesBucketList[index].Count; x++)
            //{
            //    localVertexBucket[x] = verticesBucketList[index][x];
            //}
            ExecuteMeshJobs();

            //index++;
            //if (index == verticesBucketList.Length)
            //{
            //    index = 0;
            //}
            mesh.vertices = vertices;
            mesh.RecalculateBounds();

        }
        else
        {
            for (var i = 0; i < vertices.Length; i++)
            {
                if (verticesOriginal[i].z > vertices[i].z)
                {
                    vertices[i].z -= vertices[i].z * decayTime;
                }
                else
                {
                    vertices[i].z = verticesOriginal[i].z;
                }
            }
            if (constantWave)
            {
                foreach (var localIndex in verticesBucketList[index])
                {
                    vertices[localIndex].z = vertices[localIndex].z * displacement;
                }
            }
            else
            {
                foreach (var localIndex in verticesBucketList[index])
                {
                    vertices[localIndex].z = vertices[localIndex].z + displacement;
                }
            }
            index++;
            if (index == verticesBucketList.Length)
            {
                index = 0;
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();

            //}

        }
    }

    private void ExecuteMeshJobs()
    {
        //var jobHandles = new List<JobHandle>();
        var vertexArray = new NativeArray<Vector3>(vertices.Length, Allocator.TempJob);
        //var vertexArrayOriginal = new NativeArray<Vector3>(vertices, Allocator.TempJob);
        //int[] localVertexBucket = new int[verticesBucketList[index].Count];
        //for (var x = 0; x < verticesBucketList[index].Count; x++)
        //{
        //    localVertexBucket[x] = verticesBucketList[index][x];
        //}
        //var vertexBucketList = new NativeArray<int>(verticesBucketList, Allocator.TempJob);

        //int a;
        //for (a = 0; a < vertexArray.Length; a++)
        //{
        var job = new ParallelMeshJob
            {
                Vertices = vertexArray,
                //DeltaTime = Time.deltaTime,
                //CurrentStep = 0,
                //MeshStep = updateMeshStep,
                //VerticesOriginal = vertexArrayOriginal,
                //Displacement = displacement,
                Decay = decayTime
                //IndexList = vertexBucketList
            };
        JobHandle handle = job.Schedule(vertices.Length, batches);
        //}

        //if (handle.IsCompleted)
        //{ 
        //    vertexArray.CopyTo(vertices);
        //    vertexArray.Dispose();
        //}
        handle.Complete();

        vertexArray.Dispose();

        //vertexBucketList.CopyTo(verticesBucketList);

        //vertexArrayOriginal.Dispose();
        //vertexBucketList.Dispose();
        //for (i=0; i<vertices.Length; i++)
        //{

        //    var job = new ParallelMeshDecay
        //    {
        //        Vertices = vertexArray,
        //        VerticesOriginal = vertexArrayOriginal,
        //        DecayTime = decayTime
        //    };
        //    if (i == 0)
        //        {
        //            jobDecayHandles.Add(job.Schedule(vertices.Length, 250));
        //        }
        //        else
        //        {
        //            jobDecayHandles.Add(job.Schedule(vertices.Length, 250, jobDecayHandles[i - 1]));
        //        }
        //}

        //jobDecayHandles.Last().Complete();



    }
}

public struct ParallelMeshJob : IJobParallelFor
{
    public NativeArray<Vector3> Vertices;
    //public NativeArray<Vector3> VerticesOriginal;
    //public float Displacement;
    public float Decay;
    //public NativeArray<int> IndexList;
    public void Execute(int i)
    {
        var vertex = Vertices[i];
        //var vertexOriginal = VerticesOriginal[i];
        //if (IndexList.Contains(i))
        //{
        //    vertex.z = vertex.z + Displacement;
        //}
        //CurrentStep = 0;
        if (vertex.z >= 0)
        {
            vertex.z -= Decay;
        }
        else
        {
            vertex.z = 0;
        }
        //vertex.z = 0;
        Vertices[i] = vertex;
       
    }

}
