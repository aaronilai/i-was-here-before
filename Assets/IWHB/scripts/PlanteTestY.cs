using System.Linq;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;


public class PlanteTestY : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject planeToMod;
    public AudioSource audioSource;
    Mesh mesh;
    Vector3[] vertices;
    Vector3[] verticesOriginal;
    [SerializeField] public float _maxScale = 10;
    [SerializeField] public float _minScale;

    [SerializeField] public float audioUpdateStep = 0.01f;
    [SerializeField] public float meshUpdateStep = 0.01f;
    [SerializeField] public float indexUpdateStep = 0.01f;

    [SerializeField] public float decayTime = 0.001f;
    [SerializeField] public bool constantWave = false;
    [SerializeField] public bool _useJobs;
    [SerializeField] public bool circleOrLine;
    [SerializeField] public int centerVert = 0;


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
    int index = 0;
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
            clipLoudness /= sampleDataLength; //clipLoudness is what you are looking for
            displacement = (clipLoudness * _maxScale) + _minScale;

            // transform.localScale = new Vector3(1, 1, objectToRMS);

        }
    }

    private void calcYVertices()
    {

        float range;

        float maxValue = 0;
        float minValue = 0;

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
                foreach (var localIndex in verticesBucketList[index])
                {
                    vertices[localIndex].z = verticesOriginal[localIndex].z * displacement;
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
                    if (verticesOriginal[i].z > vertices[i].z)
                    {
                        vertices[i].z += vertices[i].z * decayTime;
                    }
                }
                foreach (var localIndex in verticesBucketList[index])
                {
                    vertices[localIndex].z = verticesOriginal[localIndex].z * displacement;
                }
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        indexUpdateTime += Time.deltaTime;
        if (indexUpdateTime >= indexUpdateStep)
        {
            indexUpdateTime = 0f;
            if (_useJobs)
            {
                ExecuteMeshJobs(vertices, verticesBucketList, displacement);
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
            }
            else
            {
                if (thresholdUse)
                {
                    if (reverse)
                    {

                        if (index == 0)
                        {
                            index = verticesBucketList.Length;
                        }
                        if (thresholdInvert)
                        {
                            if (clipLoudness < threshold)
                            {
                                index--;
                            }
                        }
                        else
                        {
                            if (clipLoudness > threshold)
                            {
                                index--;
                            }

                        }

                    }
                    else
                    {
                        if (thresholdInvert)
                        {
                            if (clipLoudness < threshold)
                            {
                                index++;
                            }
                        }
                        else
                        {
                            if (clipLoudness > threshold)
                            {
                                index++;

                            }

                        }
                        if (index == verticesBucketList.Length)
                        {
                            index = 0;
                        }
                    }
                }
                else
                {
                    if (reverse)
                    {

                        if (index == 0)
                        {
                            index = verticesBucketList.Length;
                        }
                        index--;

                    }
                    else
                    {
                        index++;
                        if (index == verticesBucketList.Length)
                        {
                            index = 0;
                        }
                    }
                }

            }


        }



        // assign the local vertices array into the vertices array of the Mesh.


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

        vertexArray.CopyTo(vertices);
        vertexArray.Dispose();
    }
}
