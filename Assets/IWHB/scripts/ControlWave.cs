using UnityEngine;

public class ControlWave : MonoBehaviour
{
    [SerializeField] public AudioEchoFilter filter;
    [SerializeField] public GameObject controller;
    private float updateTime=0f;
    [SerializeField] public float updateStep = 0.01f;
    [SerializeField] public float decayRate= 10;
    [SerializeField] public float offset = 10;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
        updateTime += Time.deltaTime;
        if (updateTime >= updateStep) {
            updateTime = 0f;
            filter.wetMix = controller.transform.localRotation.x*offset;
            if (filter.dryMix >= 1f)
            {
                filter.dryMix = 1;
            }

        }


        
    }
}
