using UnityEngine;
using FireFight.Managers;
enum SprayPattern{Jet, Cone}


public class HoseController : MonoBehaviour
{
    [SerializeField]
    private Transform nozzlePoint;
    [SerializeField]
    private FireManager fireManager;
    [SerializeField]
    private SprayPattern spraypattern;
    [SerializeField]
    private float sprayRange;
    [SerializeField]
    private float coneAngle;
    [SerializeField]
    private float coolingPerSecond;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
        
    }
}
