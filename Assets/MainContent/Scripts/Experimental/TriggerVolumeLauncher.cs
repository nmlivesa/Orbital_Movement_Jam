using UnityEngine;
using System.Collections;

public class TriggerVolumeLauncher : MonoBehaviour
{
    [SerializeField]
    private Vector3 launchDirection;
    [SerializeField]
    float impulseForce = 10f, continuousForce = 5f; 

    private void Start()
    {
        if (launchDirection == Vector3.zero)
        {
            launchDirection = transform.up;
        }
        else
        {
            launchDirection = launchDirection.normalized;
        }
    }
    private void OnTriggerEnter(Collider collider)
    {
        
        if (collider.gameObject.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            
            rb.AddForce(launchDirection * impulseForce, ForceMode.Impulse);
        }
    }
    
    private void OnTriggerStay(Collider collider)
    {
        if (collider.gameObject.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.AddForce(launchDirection * continuousForce, ForceMode.Force);
        }
    }
}
