using Unity.VisualScripting;
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
        agent = GetComponent<NavMeshAgent>();

    }

    // Update is called once per frame
    void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;
        if (keyboard.nKey.wasPressedThisFrame)
        {
            Ray aimRay = commandCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit hit;
            if (Physics.Raycast(aimRay, out hit, 100f))
            {
                agent.SetDestination(hit.point);
                Debug.Log(hit.point);
            }
        }

    }
}
