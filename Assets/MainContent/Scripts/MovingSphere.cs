using System.Xml.Serialization;
using Unity.Properties;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
//using UnityEngine.UIElements;

public class MovingSphere : MonoBehaviour
{
    InputAction moveAction, jumpAction, diveAction, glideAction;
    [SerializeField]
    Transform playerInputSpace = default;
    Vector3 upAxis, rightAxis, forwardAxis;

    Vector3 velocity, desiredVelocity, contactNormal, steepNormal;

    [SerializeField, Range(0f, 100f)]
    float maxSpeed = 10f, maxAcceleration = 10f, maxAirAcceleration = 1f;
    [SerializeField, Range(1f, 10f)]
    float brakeMultiplier = 2f;  

    
    Rigidbody body;

    bool jumpDesired = false;

    [SerializeField, Range(0f, 10f)]
    float diveGravityMultiplier = 4f;

    //bool onGround;
    int groundContactCount, steepContactCount;
    bool OnGround => groundContactCount > 0;
    bool OnSteep => steepContactCount > 0;
    [SerializeField, Range(0f, 10f)]
    float jumpHeight = 2f;
    [SerializeField, Range(0, 5)]
    int maxAirJumps = 0;
    int jumpPhase;
    [SerializeField, Range(0f, 90f)]
    float maxGroundAngle = 25f;
    float minGroundDotProduct;
    int stepsSinceLastGrounded, stepsSinceLastJump;
    [SerializeField, Range(0f, 100f)]
    float maxSnapSpeed = 100f;
    [SerializeField, Min(0f)]
    float probeDistance = 1f;
    [SerializeField]
    LayerMask probeMask = -1;

    void OnValidate()
    {
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
    }
    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        OnValidate();
    }
    private void Start()
    {
        moveAction = InputSystem.actions.FindAction("Move");
        jumpAction = InputSystem.actions.FindAction("Jump");
        diveAction = InputSystem.actions.FindAction("Dive");
        glideAction = InputSystem.actions.FindAction("Glide");
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    //Update is called AFTER FixedUpdate
    private void Update()
    {
        /*GetComponent<Renderer>().material.SetColor(
            "_BaseColor", Color.white * (groundContactCount * 0.25f) // Edit color based on groundContactCount for debug
            ); */

        if (jumpAction.WasPressedThisFrame())
        {
            jumpDesired = true;
        }

        Vector2 moveValue = moveAction.ReadValue<Vector2>();
        if (playerInputSpace)
        {
            rightAxis = ProjectDirectionOnPlane(playerInputSpace.right, upAxis);
            forwardAxis =
                ProjectDirectionOnPlane(playerInputSpace.forward, upAxis);
        }
        else
        {
            rightAxis = ProjectDirectionOnPlane(Vector3.right, upAxis);
            forwardAxis = ProjectDirectionOnPlane(Vector3.forward, upAxis);
        } //Treating the upAxis(direction of gravity) as a normal to project inputs onto
        /*Debug.DrawRay(transform.position, rightAxis, Color.red);
        Debug.DrawRay(transform.position, forwardAxis, Color.cyan);*/
        Debug.DrawRay(transform.position, upAxis, Color.green);

        desiredVelocity =
            new Vector3(moveValue.x, 0f, moveValue.y) * maxSpeed;
        //Debug.Log("dive: " + diveDesired);

    }
    // Order: FixedUpdate, OnCollisionEnter&OnCollisionStay, Update
    private void FixedUpdate()
    {
        Vector3 gravity = CustomGravity.GetGravity(body.position, out upAxis);
        //Debug.Log("gravity: " + gravity);
        //Debug.Log("upAxis: " + upAxis);
        UpdateState();
        if (desiredVelocity.magnitude > 0f || OnGround)
        {
            AdjustVelocity();
        }
        

        if (jumpDesired)
        {
            Jump(gravity);
        }

        if (diveAction.IsInProgress())
        {
            Dive(gravity);
        }
        else
        {
            velocity += gravity * Time.fixedDeltaTime;
        }
        
        

         //Debug.Log("contactNormal: " + contactNormal);

        Debug.Log("velocity: " + velocity + " magnitude: " + velocity.magnitude + " desiredVelocity: " + desiredVelocity + " magnitude: " + desiredVelocity.magnitude);
        Debug.Log("gravity: " + gravity);
        body.linearVelocity = velocity;

        ClearState();
    }

    private void UpdateState ()
    {
        stepsSinceLastGrounded += 1;
        stepsSinceLastJump += 1;
        velocity = body.linearVelocity;
        if (OnGround || SnapToGround() || CheckSteepGroundContacts())
        {
            stepsSinceLastGrounded = 0;
            if (stepsSinceLastJump > 1)
            {
                jumpPhase = 0;
            }
            if (groundContactCount > 1)
            {
                contactNormal.Normalize();
            }
        }
        else
        {
            contactNormal = upAxis;
        }
    }

    void ClearState ()
    {
        groundContactCount = 0;
        steepContactCount = 0;
        contactNormal = steepNormal = Vector3.zero;
        jumpDesired = false;
    }

    private void Jump (Vector3 gravity)
    {
        Vector3 jumpDirection;
        if (OnGround)
        {
            jumpDirection = contactNormal;
        }
        else if (OnSteep)
        {
            jumpDirection = steepNormal;
            jumpPhase = 0;
        }
        else if (maxAirJumps > 0 && jumpPhase <= maxAirJumps)
        {
            if (jumpPhase == 0)
            {
                jumpPhase = 1;
            }
            jumpDirection = contactNormal;
        }
        else
        {
            return;
        }
        stepsSinceLastJump = 0;
        jumpPhase++;
        float jumpSpeed = Mathf.Sqrt(2f * gravity.magnitude * jumpHeight);
        jumpDirection = (jumpDirection + upAxis).normalized;
        float alignedSpeed = Vector3.Dot(velocity, jumpDirection);
        if (alignedSpeed > 0f)
        {
            jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
        }
        velocity += jumpDirection * jumpSpeed;
        jumpDesired = false;
    }

    private void OnCollisionEnter (Collision collision)
    {
        EvaluateCollision(collision);
    }

    private void OnCollisionStay (Collision collision) 
    {
        EvaluateCollision(collision);
    }

    private void EvaluateCollision (Collision collision)
    {
        for (int i = 0; i < collision.contactCount; i++) 
        {
            Vector3 normal = collision.GetContact(i).normal;
            float upDot = Vector3.Dot(upAxis, normal);

            if (upDot >= minGroundDotProduct)
            {
                groundContactCount += 1;
                contactNormal += normal;
            }
            else if (upDot > -0.01f)
            {
                steepContactCount += 1;
                steepNormal += normal;
            }
        }
    }

    /*Vector3 ProjectOnContactPlane (Vector3 vector)
    {
        return vector - contactNormal * Vector3.Dot(vector, contactNormal);
    }*/

    Vector3 ProjectDirectionOnPlane(Vector3 direction, Vector3 normal)
    {
        return (direction - normal * Vector3.Dot(direction, normal)).normalized;
    }
    void AdjustVelocity ()
    {

        Vector3 xAxis = ProjectDirectionOnPlane(rightAxis, contactNormal);
        Vector3 zAxis = ProjectDirectionOnPlane(forwardAxis, contactNormal); // gives unit vectors aligned with ground surface

        float currentX = Vector3.Dot(velocity, xAxis);
        float currentZ = Vector3.Dot(velocity, zAxis); //current x and z velocities relative to ground
                                                       //Debug.Log("xAxis: " + xAxis + " yAxis: " + yAxis + " zAxis: " + zAxis);
                                                       //Debug.Log(" currentX: " + currentX + " currentZ: " + currentZ);
        float acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
        float maxSpeedChange = acceleration * Time.fixedDeltaTime;
        //Debug.Log("brake if less than 0: " + Vector3.Dot(desiredVelocity, velocity));
        if (Vector3.Dot(desiredVelocity, velocity) < 0) // maybe should only work on ground?
        {
            maxSpeedChange *= brakeMultiplier;
        }
        Vector3 velocityNew = Vector3.MoveTowards(new Vector3(currentX, 0f, currentZ), desiredVelocity, maxSpeedChange);        
        // velocity += GroundAdjustedInputDirections * (difference between new and current velocities)
        velocity += xAxis * (velocityNew.x - currentX) + zAxis * (velocityNew.z - currentZ);

    }

   
    //currently skips gravity when applied
    
    bool SnapToGround()
    {
        if (stepsSinceLastGrounded > 1 || stepsSinceLastJump <= 2)
        {
            return false;
        }
        float speed = velocity.magnitude;
        if (speed > maxSnapSpeed)
        {
            return false;
        }
        if (!Physics.Raycast(body.position, -upAxis, out RaycastHit hit, probeDistance, probeMask))
        {
            return false;
        }
        float upDot = Vector3.Dot(upAxis, hit.normal);
        if (upDot < minGroundDotProduct)
        {
            return false;
        }
        
        groundContactCount = 1;
        contactNormal = hit.normal;
        float dot = Vector3.Dot(velocity, hit.normal);
        if (dot > 0f)
        {
            velocity = (velocity - hit.normal * dot).normalized * speed;
        }
        return true;
    }

    bool CheckSteepGroundContacts ()
    {
        if (steepContactCount > 1)
        {
            steepNormal.Normalize();
            float upDot = Vector3.Dot(upAxis, steepNormal);
            if (upDot >= minGroundDotProduct)
            {
                groundContactCount = 1;
                contactNormal = steepNormal;
                return true;
            }
        }
        return false;
    }

    void Dive (Vector3 gravity)
    {
        velocity += gravity * Time.fixedDeltaTime * diveGravityMultiplier;
    }

    
}
