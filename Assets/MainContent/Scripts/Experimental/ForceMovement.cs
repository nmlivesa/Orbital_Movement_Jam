using UnityEngine;
using UnityEngine.InputSystem;
/*TODO
1. Finish contact normal system and add conditional checks when jumping to determine what normal to use 
2. Project move forces onto contact normal plane?
3. Actual breaks?
4. Finish movement input smoothing
5. Ground snapping?

DONE
Finish jump input holding so input is not lost when pressed before landing & add coyote time 

 */
public class ForceMovement : MonoBehaviour
{
    Rigidbody rb;

    [SerializeField]
    Transform playerTransform;
    Vector3 upAxis, rightAxis, forwardAxis;

    InputAction moveAction, jumpAction; //grabs inputs
    Vector2 moveInput; //stores inputs
    bool jumpDesired, jumpHeld, jumpTriggered;

    //jump variables
    [SerializeField]
    int coyoteTimeSteps = 2; // number of physics steps in which jumps will work after leaving or before touching ground
    [SerializeField, Range(0.1f, 5f)]
    float jumpTime = 0.5f;
    [SerializeField, Range(1f, 10f)]
    float jumpImpulseForce = 5f, jumpContinuousForceMultiplier = 10f;

    //movement variables
    [SerializeField, Range(0f, 30f)]
    float airMoveForce = 2.5f;
    [SerializeField, Range(1f, 30f)]
    float moveForce = 5f, brakeConstant = 10f, moveTopSpeedFromInput = 15f;

    //for smoothing curves
    //float timeMoveHeld = 0f;
    float timeJumpHeld = 0f;
    [SerializeField]
    AnimationCurve jumpCurve, movementSmoothingCurve, brakeForceCurve;

    //status variables
    [SerializeField, Range(0f, 90f)]
    float maxGroundAngle = 25f;
    float minGroundDotProduct;
    Vector3 contactNormal, steepNormal;
    int groundContactCount, steepContactCount;
    bool OnGround => groundContactCount > 0;
    bool OnSteep => steepContactCount > 0;
    int stepsSinceLastOnGround = 0;
    int stepsSinceJumpPressed = 0;
    int stepsSinceJumpImpulse = 0;



    private void OnValidate()
    {
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        OnValidate();
    }

    private void Start()
    {
        moveAction = InputSystem.actions.FindAction("Move");
        jumpAction = InputSystem.actions.FindAction("Jump");
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        
    }

   



    void Update()
    {
        UpdateAxis();

        if (jumpAction.WasPressedThisFrame())
        {
            jumpDesired = jumpHeld = true;
            stepsSinceJumpPressed = 0;
        }
        if (jumpTriggered)
        {
            jumpHeld = jumpAction.IsPressed();
            if (!jumpHeld)
            {
                timeJumpHeld = 0f;
                jumpTriggered = false;
            }
        }

        moveInput = moveAction.ReadValue<Vector2>();
    }

    void UpdateAxis()
    {
        if (playerTransform)
        {
            rightAxis = Vector3.ProjectOnPlane(playerTransform.right, upAxis).normalized;
            forwardAxis = Vector3.ProjectOnPlane(playerTransform.forward, upAxis).normalized;
        }
        else
        {
            rightAxis = Vector3.ProjectOnPlane(Vector3.right, upAxis).normalized;
            forwardAxis = Vector3.ProjectOnPlane(Vector3.forward, upAxis).normalized;
        }
    }


    private void FixedUpdate()
    {
        
        Vector3 gravity = CustomGravity.GetGravity(rb.position, out upAxis);
        UpdateState();
        rb.AddForce(gravity, ForceMode.Acceleration);
        //upAxis = -Physics.gravity.normalized; //can be changed later when editing gravity
        Move();
        //Rotate();

        if (jumpDesired && (stepsSinceLastOnGround < coyoteTimeSteps))  // should allow for jumps when after leaving ground for a few frames
        {
            JumpImpulse();
        }
        if (jumpTriggered)
        {
            JumpContinued();
        }
        ClearState();
        Debug.Log("velocity magnitude: " + rb.linearVelocity.magnitude);
    }

    void UpdateState()
    {
        stepsSinceLastOnGround++;
        stepsSinceJumpPressed++;
        stepsSinceJumpImpulse++;
        if (stepsSinceJumpImpulse <= coyoteTimeSteps) // prevents abusing coyote time to jump multiple times
        {
            jumpDesired = false;
        }
        if (OnGround)
        {
            stepsSinceLastOnGround = 0;
        }
        
    }

    void ClearState()
    {
        groundContactCount = 0;
        steepContactCount = 0;
        contactNormal = steepNormal = Vector3.zero;

        if (rb.IsSleeping() && jumpDesired) //had issues with jumps being ignored when rb was sleeping. rb goes to sleep when no forces are acting on it
        {
            rb.WakeUp();
        }
        else if (stepsSinceJumpPressed > coyoteTimeSteps) // holds jump input for a few frames. Allows for jumps when pressed slightly before landing
        {
            jumpDesired = false;
        }
    }


    void Move()
    {
        
        Vector3 inputForce = Vector3.ClampMagnitude(moveInput.x * rightAxis + moveInput.y * forwardAxis, 1f);
        //clamps max input value and applies directions perpendicular to the upAxis
        Vector3 inputForceScaled = OnGround ? inputForce * moveForce : inputForce * airMoveForce;
        
        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, upAxis);
        //Debug.Log("rb.linearVelocity: " + rb.linearVelocity);
        //Debug.Log("LocallyHorizontalVelocity: " + horizontalVelocity);
        float dotInputDirAndHorizontalVelDir = Vector3.Dot(inputForce, horizontalVelocity.normalized);
        if (dotInputDirAndHorizontalVelDir >= 0f)
        {
            //Debug.Log("regular movement force");
            //float smoothingFactor = movementSmoothingCurve.Evaluate(horizontalVelocity.magnitude / moveTopSpeedFromInput);
            //inputForceScaled -= Vector3.Project(inputForceScaled, horizontalVelocity) * smoothingFactor;
            rb.AddForce(inputForceScaled, ForceMode.Force);
            //This works for the most part, but angular velocities still allow speeds to rise above the max
            if (horizontalVelocity.magnitude >= moveTopSpeedFromInput)
            {
                rb.AddForce(-horizontalVelocity.normalized * inputForceScaled.magnitude, ForceMode.Force);
            }
        }
        else
        {
            //Debug.Log("braking movement force");
            float brakeFactor = brakeConstant * brakeForceCurve.Evaluate(-dotInputDirAndHorizontalVelDir) + 1f;
            rb.AddForce(inputForceScaled * brakeFactor, ForceMode.Force);
        }
    }


    void Rotate() //only for testing stuff. Actually rotating the player should be handled elsewhere
    {
        float smooth = 0.5f;
        //float tiltAngle = 10.0f;

        Vector3 LocallyHorizontalVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, upAxis);

        //float tiltAroundY = moveInput.x * tiltAngle * Time.fixedDeltaTime;
        // Rotate the object by converting the angles into a quaternion.
        //Quaternion target = Quaternion.Euler(0, tiltAroundY, 0);

        //Rotates object in the direction of its velocity or any other argument given
        Quaternion target = Quaternion.LookRotation(LocallyHorizontalVelocity, upAxis);



        // Dampen towards the target rotation
        rb.rotation = Quaternion.Slerp(rb.rotation, target, Time.fixedDeltaTime * smooth);

        // Rotation without any dampening
        //transform.rotation = transform.rotation * target;
    }




    void JumpImpulse()
    {
        rb.AddForce(upAxis * jumpImpulseForce, ForceMode.Impulse);
        jumpDesired = false;
        jumpTriggered = true;     
        stepsSinceJumpImpulse = 0;
    }
    void JumpContinued()
    {
        rb.AddForce(upAxis * jumpImpulseForce * jumpContinuousForceMultiplier * jumpCurve.Evaluate(timeJumpHeld / jumpTime), ForceMode.Force);
        timeJumpHeld += Time.fixedDeltaTime;
        if (timeJumpHeld > jumpTime || !jumpHeld)
        {
            jumpTriggered = false;
            timeJumpHeld = 0f;
        }
    }



    private void OnCollisionEnter(Collision collision)
    {
        EvaluateCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        EvaluateCollision(collision);
    }

    private void EvaluateCollision(Collision collision)
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
}
