using System.Collections.Generic;
using UnityEngine;

public class lerp_buckets_new : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] public GameObject origin;
    [SerializeField] public GameObject target;
    [SerializeField] public GameObject controller;
    [SerializeField] public float updateStep = 0.01f;
    [SerializeField] public float indexStep = 0.01f;
    [SerializeField] public float _maxScale = 10;
    [SerializeField] public float _minScale;
    [SerializeField] public int downSample;

    private float indexTime = 0f;

    [SerializeField] public float range = 2;
    [SerializeField] public float angleReal = 0f;

    //[SerializeField] public float lerp_range = 1f;

    private Vector3[] vertices1;
    private Vector3[] vertices2;
    private Vector3[] original;
    private float angle = 0f;
    private int smallestVertices;
    private Mesh mesh1;
    private Mesh mesh2;
    private float updateTime = 0f;


    public AudioSource audioSource;
    [SerializeField] public float audioUpdateStep = 0.01f;
    [SerializeField] public float decayTime = 0.001f;

    [SerializeField] public int sampleDataLength = 1024;
    private float audioUpdateTime = 0;
    private float clipLoudness = 0f;
    private float[] clipSampleData;

    private List<int>[] verticesBucketList;
    private float displacement;
    private float bucketSize;
    [SerializeField] public int bucketNum = 128;
    [SerializeField] public bool circleOrLine;
    [SerializeField] public bool constantWave;
    [SerializeField] public bool reverse;

    private int index;
    private int lastBucketNum;
    private float[] buckets;

    void Start()
    {
        mesh1 = origin.GetComponent<MeshFilter>().mesh;
        mesh1.MarkDynamic();
        vertices1 = mesh1.vertices;
        mesh2 = target.GetComponent<MeshFilter>().mesh;
        mesh2.MarkDynamic();
        vertices2 = mesh2.vertices;
        original = mesh1.vertices;
        if (vertices1.Length > vertices2.Length)
        {
            smallestVertices = vertices2.Length;
        }
        else
        {
            smallestVertices = vertices1.Length;
        }
        for (var i = smallestVertices / downSample; i < smallestVertices; i++)
        {
            vertices1[i].x = 0f;
            vertices1[i].y = 0f;
            vertices1[i].z = 0f;

        }
        initBuckets();
    }

    private void Awake()
    {
        if (!audioSource)
        {
            Debug.LogError(GetType() + ".Awake: there was no audioSource set.");
        }
        clipSampleData = new float[sampleDataLength];

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

    private void calcYVertices()
    {

        float range;

        float maxValue = 0;
        float minValue = 0;

        for (var i = 0; i < original.Length; i++)
        {
            if (original[i].x > maxValue)
            {
                maxValue = vertices1[i].x;
            }
            if (original[i].x < minValue)
            {
                minValue = original[i].x;
            }
        }
        range = maxValue - minValue;
        bucketSize = range / bucketNum;

        for (var i = 0; i < buckets.Length; i++)
        {
            buckets[i] = (bucketSize * i) + minValue;
        }
        int vertexListIndex = 0;
        for (var i = 0; i < original.Length/downSample; i++)
        {
            for (var f = 0; f < buckets.Length; f++)
            {
                if (constantWave == true)
                {
                    //all behind the wave is on the bucket
                    if (original[i].y < buckets[f] + bucketSize)
                    {
                        verticesBucketList[f].Add(i);
                        vertexListIndex++;
                    }
                }
                else
                {
                    //each bucket is filled independently, only the wave changes
                    if (original[i].y > buckets[f] && original[i].y < buckets[f] + bucketSize)
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
        Vector3 centralPoint = original[original.Length / 2];
        float _maxValue = 0;
        float _minValue = 0;

        for (var i = 0; i < original.Length; i++)
        {

            var currenDistance = Vector3.Distance(original[i], centralPoint);
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
        for (var i = 0; i < original.Length/downSample; i++)
        {
            for (var f = 0; f < buckets.Length; f++)
            {
                if (constantWave == true)
                {
                    //all behind the wave is on the bucket
                    var vertexDistance = Vector3.Distance(original[i], centralPoint);
                    if (vertexDistance > buckets[f])
                    {
                        verticesBucketList[f].Add(i);
                        vertexListIndex++;
                    }
                }
                else
                {
                    //each bucket is filled independently, only the wave changes
                    var vertexDistance = Vector3.Distance(original[i], centralPoint);
                    if (vertexDistance > buckets[f] && vertexDistance < buckets[f] + bucketSize)
                    {
                        verticesBucketList[f].Add(i);
                        vertexListIndex++;
                    }
                }
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        AudioCalc();
        meshCalc();
        indexCalc();
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

        }
    }
    private void meshCalc()
    {

        updateTime += Time.deltaTime;
        if (updateTime >= updateStep)
        {
            updateTime = 0f;
            angle = controller.transform.localRotation.x;
            angle = angle * range;
            if (constantWave)
            {
                foreach (var localIndex in verticesBucketList[index])
                {
                    vertices1[localIndex] = Vector3.Lerp(original[localIndex], vertices2[localIndex], displacement);
                }
            }
            else
            {

                for (var i = 0; i < vertices1.Length/downSample; i++)
                {
                    if (Vector3.Distance(original[i], vertices1[i]) > 1)
                    {
                        vertices1[i] = Vector3.Lerp(vertices1[i], original[i], decayTime);
                    }
                    else
                    {
                        vertices1[i] = original[i];
                    }
                }
                foreach (var localIndex in verticesBucketList[index])
                {
                    vertices1[localIndex] = Vector3.Lerp(vertices1[localIndex], vertices2[localIndex], displacement);
                }
            }
            //angleReal = displacement;
            mesh1.vertices = vertices1;
            mesh1.RecalculateBounds();

        }
    }
    private void indexCalc()
    {
        indexTime += Time.deltaTime;
        if (indexTime >= indexStep)
        {
            indexTime = 0f;
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
