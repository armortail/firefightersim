using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;


public class NPCMovement : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField]
    private Transform Destination;
    private NavMeshAgent agent;
    [SerializeField]
    private Camera commandCamera;
    void Start()
    {
        Ray aimRay = commandCamera.ViewportPointToRay(new Vector3(0.5f,0.5f,0f));
        
        
    }

    // Update is called once per frame
    void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;
        if (keyboard.nKey.isPressed)
        {
            agent.SetDestination(Destination.position);
        }

    }
}
