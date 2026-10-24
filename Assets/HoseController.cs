using UnityEngine;
using UnityEngine.InputSystem;
using FireFight.Managers;


public class HoseController : MonoBehaviour
{
    [SerializeField]
    private Camera aimCamera;
    [SerializeField]
    private FireManager fireManager;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Mouse mouse = Mouse.current;
        if(mouse == null)
        {
            return;
        }
        if(mouse.rightButton.isPressed)
        {
            Ray sprayRay = aimCamera.ViewportPointToRay(new Vector3(0.5f,0.5f,0f));
        }
        
    }
}
