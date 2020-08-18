using System.Linq;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;


public class directOrderWave : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject planeToMod;
    public AudioSource audioSource;
    Mesh mesh;
    Vector3[] vertices;
    Vector3[] verticesOriginal;
    [SerializeField] public float _maxScale = 10;
    [SerializeField] public float _minScale;
    [SerializeField] public int centerVert;

    [SerializeField] public float audioUpdateStep = 0.01f;
    [SerializeField] public float meshUpdateStep = 0.01f;
    [SerializeField] public float indexUpdateStep = 0.01f;

    [SerializeField] public float decayTime = 0.001f;
    [SerializeField] public bool constantWave = false;
    [SerializeField] public bool _useJobs;
    [SerializeField] public bool circleOrLine;

    [SerializeField] public int sampleDataLength = 1024;
    public bool reverse;

    public bool thresholdUse;
    public bool thresholdInvert;
    [SerializeField] public float threshold = 0.5f;


    private float audioUpdateTime = 0;
    private float indexUpdateTime = 0;
    private float meshUpdateTime = 0;


    private float clipLoudness;
    private float[] clipSampleData;
    private List<int>[] verticesBucketList;
    private float displacement;
    private float bucketSize;
    [SerializeField] public int bucketNum = 128;
    private int lastBucketNum;
    private float[] buckets;
    private int index = 0;
    private void Start()
    {
        lastBucketNum = bucketNum;

        mesh = planeToMod.GetComponent<MeshFilter>().mesh;
        vertices = mesh.vertices;
        verticesOriginal = mesh.vertices;
        initBuckets();

        //sampleDataLength = vertices.Length;
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

    private void initBuckets()
    {
        buckets = new float[bucketNum];
        verticesBucketList = new List<int>[bucketNum];
        for (var i = 0; i < bucketNum; i++)
        {
            verticesBucketList[i] = new List<int>();
        }
        if (circleOrLine)
        {
            calcYVertices();
        }
        else
        {
            calcCircleVertices();
        }
    }

    private void AudioCalc()
    {
        audioUpdateTime += Time.deltaTime;
        if (audioUpdateTime >= audioUpdateStep)
        {

            audioUpdateTime = 0f;

            audioSource.clip.GetData(clipSampleData, audioSource.timeSamples);//I read 1024 samples, which is about 80 ms on a 44khz stereo clip, beginning at the current sample position of the clip.
            clipLoudness = 0f;
            foreach (var sample in clipSampleData)
            {
                clipLoudness += Mathf.Abs(sample);
            }
            clipLoudness /= sampleDataLength;
            displacement = (clipLoudness * _maxScale) + _minScale;

        }
    }

    private void calcYVertices()
    {

        float range;

        float maxValue = 0;
        float minValue = 0;

        for (var i = 0; i < vertices.Length; i++)
        {
            if (vertices[i].x > maxValue)
            {
                maxValue = vertices[i].x;
            }
            if (vertices[i].x < minValue)
            {
                minValue = vertices[i].x;
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
                    if (vertices[i].x < buckets[f] + bucketSize)
                    {
                        verticesBucketList[f].Add(i);
                        vertexListIndex++;
                    }
                }
                else
                {
                    //each bucket is filled independently, only the wave changes
                    if (vertices[i].x > buckets[f] && vertices[i].x < buckets[f] + bucketSize)
                    {
                        verticesBucketList[f].Add(i);
                        vertexListIndex++;
                    }
                }
            }
        }
    }
    private void calcCircleVertices()
    {

        float range;
        Vector3 centralPoint = vertices[centerVert];
        float _maxValue = 0;
        float _minValue = 0;

        for (var i = 0; i < vertices.Length; i++)
        {

            var currenDistance = Vector3.Distance(vertices[i], centralPoint);
            if (currenDistance > _maxValue)
            {
                _maxValue = currenDistance;
            }
            if (currenDistance < _minValue)
            {
                _minValue = currenDistance;
            }
        }
        range = _maxValue - _minValue;
        bucketSize = range / bucketNum;

        for (var i = 0; i < buckets.Length; i++)
        {
            buckets[i] = bucketSize * i;
        }
        int vertexListIndex = 0;
        for (var i = 0; i < vertices.Length; i++)
        {
            for (var f = 0; f < buckets.Length; f++)
            {
                if (constantWave == true)
                {
                    //all behind the wave is on the bucket
                    var vertexDistance = Vector3.Distance(vertices[i], centralPoint);
                    if (vertexDistance > buckets[f])
                    {
                        verticesBucketList[f].Add(i);
                        vertexListIndex++;
                    }
                }
                else
                {
                    //each bucket is filled independently, only the wave changes
                    var vertexDistance = Vector3.Distance(vertices[i], centralPoint);
                    if (vertexDistance > buckets[f] && vertexDistance < buckets[f] + bucketSize)
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
            if (circleOrLine)
            {
                calcYVertices();
            }
            else
            {
                calcCircleVertices();
            }

            lastBucketNum = bucketNum;
        }

        meshUpdateTime += Time.deltaTime;
        if (meshUpdateTime > meshUpdateStep)
        {
            meshUpdateTime = 0f;


            if (constantWave)
            {
                int wrappingIndex = 0;
                for (var i = 0; i < vertices.Length; i++)
                {
                    if (verticesOriginal[i].z < vertices[i].z)
                    {
                        vertices[i].z -= vertices[i].z * decayTime;
                    }
                    else
                    {
                        vertices[i].z = verticesOriginal[i].z;
                    }
                }
                foreach (var localIndex in verticesBucketList[index])
                {
                    if (wrappingIndex >= sampleDataLength)
                    {
                        wrappingIndex = 0;
                    }
                    vertices[localIndex].z = (clipSampleData[wrappingIndex] * _maxScale) + _minScale;
                    wrappingIndex++;
                }
            }
            else
            {
                for (var i = 0; i < vertices.Length; i++)
                {
                    if (verticesOriginal[i].z < vertices[i].z)
                    {
                        vertices[i].z -= vertices[i].z * decayTime;
                    }
                    else
                    {
                        vertices[i].z = verticesOriginal[i].z;
                    }
                }
                for(var i=0; i< verticesBucketList.Length; i++)
                {
                    foreach (var localIndex in verticesBucketList[i])
                    {
                        vertices[localIndex].z = verticesOriginal[localIndex].z*((clipSampleData[i] * _maxScale) + _minScale);
                    }
                }
                
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }
    }

    private void ExecuteMeshJobs(Vector3[] vertices, List<int>[] verticesBucketList, float displacement)
    {
        var jobHandles = new List<JobHandle>();
        var vertexArray = new NativeArray<Vector3>(vertices, Allocator.TempJob);
        int[] localVertexBucket = new int[verticesBucketList[index].Count];
        for (var x = 0; x < verticesBucketList[index].Count; x++)
        {
            localVertexBucket[x] = verticesBucketList[index][x];
        }
        var vertexBucketList = new NativeArray<int>(localVertexBucket, Allocator.TempJob);

        int a;
        for (a = 0; a < localVertexBucket.Length; a++)
        {
            var job = new ParallelMeshJob
            {
                Vertices = vertexArray
            };
            if (a == 0)
            {
                jobHandles.Add(job.Schedule(vertexBucketList.Length, 250));
            }
            else
            {
                jobHandles.Add(job.Schedule(vertexBucketList.Length, 250, jobHandles[a - 1]));
            }
        }
        jobHandles.Last().Complete();
        index++;
        if (index == vertexBucketList.Length)
        {
            index = 0;
        }

        vertexArray.CopyTo(vertices);
        vertexArray.Dispose();
    }
}
