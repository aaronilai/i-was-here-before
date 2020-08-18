using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaneVertMap : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject planeToMod;

    Mesh mesh;
    Vector3[] vertices;
    Vector3[] verticesOriginal;
    [SerializeField] public float _maxScale;
    [SerializeField] public float _minScale;
    public AudioSource audioSource;
    [SerializeField] public float updateStep = 0.01f;
    [SerializeField] public float updateMeshStep = 0.01f;
    [SerializeField] public float decayTime = 0.001f;
    [SerializeField] public bool point = false;
    [SerializeField] public bool usesDecay = false;
    [SerializeField] public int sampleDataLength = 1024;

    private float currentUpdateTime = 0f;
    private float currentUpdateTime2 = 0f;

    private float clipLoudness;
    private float objectToRMS;
    private float[] clipSampleData;
    private List<int>[] verticesBucketList;
    private float displacement;
    private float decay;
    private float maxValue = 20;
    private float minValue = 1;
    int index = 0;
    private void Start()
    {
        mesh = planeToMod.GetComponent<MeshFilter>().mesh;
        vertices = mesh.vertices;
        verticesOriginal = mesh.vertices;
 
        //calcYVertices();
        //sampleDataLength = vertices.Length;
    }

    private void Awake()
    {

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
            audioSource.clip.GetData(clipSampleData, audioSource.timeSamples); //I read 1024 samples, which is about 80 ms on a 44khz stereo clip, beginning at the current sample position of the clip.
            clipLoudness = 0f;
            foreach (var sample in clipSampleData)
            {
                clipLoudness += Mathf.Abs(sample);
            }
            clipLoudness /= sampleDataLength; //clipLoudness is what you are looking for
            objectToRMS = (clipLoudness * _maxScale) + _minScale;

            displacement = objectToRMS;
            // transform.localScale = new Vector3(1, 1, objectToRMS);

        }
    }




    private void MeshCalc()
    {
        currentUpdateTime2 += Time.deltaTime;

        if (currentUpdateTime2 >= updateMeshStep)
        {
            currentUpdateTime2 = 0f;



            





            // each vertex as sample, simultaneous
            if (point == false) {

                for (var i = 0; i < vertices.Length; i++)
                {
                    vertices[i].y = clipSampleData[i] * _maxScale + _minScale;
                }
            }

            if (point == true && usesDecay==true)
            {
                //Any point not being displaced is decaying
                for (var i = 0; i < vertices.Length; i++)
                {
                    if (verticesOriginal[i].y < vertices[i].y)
                    {
                        vertices[i].y -= vertices[i].y * decayTime;
                    }
                    else
                    {
                        vertices[i].y = verticesOriginal[i].y;
                    }
                }
                // one by one
                vertices[index].y = vertices[index].y + displacement;
            }

            if (point == true && usesDecay == false)
            {
                // one by one
                vertices[index].y = vertices[index].y + displacement;
            }

            //on a group
            //if (vertices.Length > index + group)
            //{
            //    for (var i = 0; i < group; i++)
            //    {
            //        vertices[index + i].z = vertices[index + i].z + displacement;
            //    }
            //}


            index++;
            if (index == vertices.Length)
            {
                index = 0;
            }

            // assign the local vertices array into the vertices array of the Mesh.
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }
    }
}
