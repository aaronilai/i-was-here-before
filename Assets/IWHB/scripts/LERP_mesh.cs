using System;
using Unity.Mathematics;
using UnityEngine;

public class LERP_mesh : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] public GameObject origin;
    [SerializeField] public GameObject target;
    [SerializeField] public GameObject controller;
    [SerializeField] public float updateStep = 0.01f;
    [SerializeField] public float range = 2;
    [SerializeField] public int downSample = 1;

    //[SerializeField] public float lerp_range = 1f;

    private Vector3[] vertices1;
    private Vector3[] vertices2;
    private Vector3[] original;
    private int smallestVertices;
    private Mesh mesh1;
    private Mesh mesh2;
    private float updateTime = 0f;


    public AudioSource audioSource;
    [SerializeField] public float audioUpdateStep = 0.01f;
    [SerializeField] public int sampleDataLength = 1024;
    private float audioUpdateTime = 0;
    private float clipLoudness = 0f;
    private float[] clipSampleData;
    private float angle = 0f;
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
        } else
        {
            smallestVertices = vertices1.Length;
        }
        for (var i = smallestVertices / downSample; i < smallestVertices; i++)
        {
            vertices1[i].x = 0f;
            vertices1[i].y = 0f;
            vertices1[i].z = 0f;

        }
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
    void Update()
    {
        AudioCalc();
        updateTime += Time.deltaTime;
        if (updateTime >= updateStep)
        {
            updateTime = 0f;
            angle = controller.transform.localRotation.x;
            angle = angle * range;
            for (var i=0; i<smallestVertices/downSample; i++)
            {
                vertices1[i] = Vector3.Lerp(original[i], vertices2[i], clipLoudness);
            }
            
            mesh1.vertices = vertices1;
            mesh1.RecalculateBounds();
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
            clipLoudness+= angle;

        }
    }
}
