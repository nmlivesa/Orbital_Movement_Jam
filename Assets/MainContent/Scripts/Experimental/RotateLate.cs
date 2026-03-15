using UnityEngine;

public class RotateLate : MonoBehaviour
{
    [SerializeField]
    Transform target = default;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
       
    }

    private void LateUpdate()
    {
        Quaternion rotation = Quaternion.LookRotation(target.position - transform.position, Vector3.up);
        transform.rotation = rotation;
    }
}
