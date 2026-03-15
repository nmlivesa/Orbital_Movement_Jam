using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CustomGravityRigidBody : MonoBehaviour
{
    [SerializeField]
    bool floatToSleep = false;

    float floatDelay;

    Rigidbody body;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
    }

    void FixedUpdate()
    {
        if (floatToSleep)
        {
            if (body.IsSleeping())
            {
                return;
            }

            if (body.linearVelocity.sqrMagnitude < 0.0001f)
            {
                floatDelay += Time.deltaTime;
                if (floatDelay >= 1f)
                {
                    return;
                }
            }
            else
            {
                floatDelay = 0f;
            }
        }
        

        body.AddForce(CustomGravity.GetGravity(body.position), ForceMode.Acceleration);
    }
}
