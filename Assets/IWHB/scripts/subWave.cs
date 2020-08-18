using System.Linq;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using System;

public class subWave : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] public GameObject planeToMod;
    [SerializeField] public AudioSource audioSource;
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

    [SerializeField] public int sampleDataLength = 1024;
    [SerializeField] public bool reverse;
    [SerializeField] public bool bounce;
    private int direct = 1;


    [SerializeField] public bool thresholdUse;
    [SerializeField] public bool thresholdInvert;
    [SerializeField] public float threshold = 0.5f;
    [SerializeField] public int centerVert = 0;


    private float audioUpdateTime = 0;
    private float indexUpdateTime = 0;
    private float meshUpdateTime = 0;


    private float clipLoudness;
    private float[] clipSampleData;
    private List<int>[] verticesBucketList;
    private List<int>[,] subVerticesBucketList;

    private float displacement;
    private float bucketSize;
    [SerializeField] public int bucketNum = 128;
    [SerializeField] public int subBucketNum = 128;


    private int lastBucketNum;
    private float[] buckets;
    private float[,] subBuckets;
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
        subVerticesBucketList = new List<int>[bucketNum,subBucketNum];
        subBuckets = new float[bucketNum, subBucketNum];
        for (var i = 0; i < bucketNum; i++)
        {
            verticesBucketList[i] = new List<int>();
        }
        for(var i =0; i< bucketNum; i++)
        {
            for(var p = 0; p < subBucketNum; p++)
            {
                subVerticesBucketList[i, p] = new List<int>();
            }
        }
        if (circleOrLine)
        {
            calcYVertices();
            subCalc();
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
        //int vertexListIndex = 0;
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
                        //vertexListIndex++;
                    }
                }
                else
                {
                    //each bucket is filled independently, only the wave changes
                    if (vertices[i].x > buckets[f] && vertices[i].x < buckets[f] + bucketSize)
                    {
                        verticesBucketList[f].Add(i);
                        //vertexListIndex++;
                    }
                }
            }
        }
    }

    private void subCalc()
    {

        for (int bigIndex = 0; bigIndex < bucketNum; bigIndex++)
        {
            float subrange;
            float submaxValue = 0f;
            float subminValue = 0f;
            float subBucketSize;
            for (var i=0; i< verticesOriginal.Length; i++)
            {
                if (vertices[i].y > submaxValue)
                {
                    submaxValue = vertices[i].y;
                }
                if (vertices[i].y < subminValue)
                {
                    subminValue = vertices[i].y;
                }
            }
            subrange = submaxValue - subminValue;
            subBucketSize = subrange / subBucketNum;
            for (var i = 0; i < subBucketNum; i++)
            {
                subBuckets[bigIndex, i] = (subBucketSize * i) + subminValue;
            }
            //int vertexListIndex = 0;
            foreach (var miniIndex in verticesBucketList[bigIndex])
            {
                for (var x = 0; x < subBucketNum; x++)
                {
                    if (vertices[miniIndex].y > subBuckets[bigIndex, x] && vertices[miniIndex].y < subBuckets[bigIndex, x] + subBucketSize)
                    {
                        subVerticesBucketList[bigIndex, x].Add(miniIndex);
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
        //int vertexListIndex = 0;
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
                        //vertexListIndex++;
                    }
                }
                else
                {
                    //each bucket is filled independently, only the wave changes
                    var vertexDistance = Vector3.Distance(centralPoint, vertices[i]);
                    if (vertexDistance > buckets[f] && vertexDistance < buckets[f] + bucketSize)
                    {
                        verticesBucketList[f].Add(i);
                        //vertexListIndex++;
                    }
                }
            }
        }
        for (int bigIndex = 0; bigIndex < bucketNum; bigIndex++)
        {
            float subrange;
            float submaxValue = 0f;
            float subminValue = 0f;
            float subBucketSize;
            for (var i = 0; i < verticesOriginal.Length; i++)
            {
                var currentAngle = Vector3.Angle(centralPoint, vertices[i]);
                if (currentAngle > submaxValue)
                {
                    submaxValue = vertices[i].y;
                }
                if (currentAngle < subminValue)
                {
                    subminValue = vertices[i].y;
                }
            }
            subrange = submaxValue - subminValue;
            subBucketSize = subrange / subBucketNum;
            for (var i = 0; i < subBucketNum; i++)
            {
                subBuckets[bigIndex, i] = (subBucketSize * i) + subminValue;
            }
            //int vertexListIndex = 0;
            foreach (var miniIndex in verticesBucketList[bigIndex])
            {
                for (var x = 0; x < subBucketNum; x++)
                {
                    var vertexAngle = Vector3.Angle(centralPoint, vertices[miniIndex]);
                    if (vertexAngle > subBuckets[bigIndex, x] && vertexAngle < subBuckets[bigIndex, x] + subBucketSize)
                    {
                        subVerticesBucketList[bigIndex, x].Add(miniIndex);
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
            // decay function
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
                
            //updating the actual vertices
            for(int vertIndex=0; vertIndex<subBucketNum; vertIndex++)
            {
                foreach (var miniIndex in subVerticesBucketList[index, vertIndex])
                {
                    //Debug.Log("working");

                    vertices[miniIndex].z = verticesOriginal[miniIndex].z * ((clipSampleData[vertIndex] * _maxScale)+ _minScale);
                    //Debug.Log(subVerticesBucketList[index, vertIndex][miniIndex]);
                }
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        indexUpdateTime += Time.deltaTime;
        if (indexUpdateTime >= indexUpdateStep)
        {
            indexUpdateTime = 0f;
            if (bounce && thresholdUse)
            {
                
                if (thresholdInvert)
                {
                    if (clipLoudness < threshold)
                    {
                        index += direct;
                    }
                }
                else
                {
                    if (clipLoudness > threshold)
                    {
                        index += direct;
                    }
                }
                if (index == verticesBucketList.Length - 1 || index == 0)
                {
                    direct = -direct;
                }
                if (index < 0)
                {
                    index = 0;
                }
                if (index > verticesBucketList.Length)
                {
                    index = verticesBucketList.Length;
                }
            }
            if (bounce && !thresholdUse)
            {
                index += direct;

                if (index == verticesBucketList.Length - 1 || index == 0)
                {
                    direct = -direct;
                }
            }
            if (!bounce && thresholdUse && reverse)
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
            if (!bounce && thresholdUse && !reverse)
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
            if (!bounce && !thresholdUse)
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
}
