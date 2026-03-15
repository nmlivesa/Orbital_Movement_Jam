using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent (typeof(Camera))]
public class OrbitCamera : MonoBehaviour
{
    InputAction lookAction;

    Camera regularCamera;

    [SerializeField]
    Transform focus = default;

    [SerializeField, Range(1f, 20f)]
    float distance = 5f;

    [SerializeField, Min(0f)]
    float focusRadius = 1f;

    [SerializeField, Range(0f, 1f)]
    float focusCentering = 0.5f;


    Vector3 focusPoint, previousFocusPoint;


    Quaternion gravityAlignment = Quaternion.identity;

    Quaternion orbitRotation;


    [SerializeField, Range(0.1f, 0.7f)]
    float rotationSpeed = 0.5f;
    [SerializeField, Range(5f, 360f)]
    float automaticRotationSpeed = 60f;

    [SerializeField]
    Vector2 orbitAngles = new Vector2(45f, 0f);
    
    [SerializeField, Range(-89f, 89f)]
    float minVerticalAngle = -30f, maxVerticalAngle = 60f;


    [SerializeField, Min(0f)]
    float alignDelay = 5f;

    [SerializeField, Range(0f, 90f)]
    float alignSmoothRange = 45f;

    [SerializeField, Min(0f)]
    float upAlignmentSpeed = 360f;

    float lastManualRotationTime;

    [SerializeField]
    LayerMask obstructionMask = -1;

    //Editor Only method to ensure editable variables make sense
    void OnValidate()
    {
        if (maxVerticalAngle < minVerticalAngle)
        {
            maxVerticalAngle = minVerticalAngle;
        }
    }
    private void Awake()
    {
        regularCamera = GetComponent<Camera>();
        focusPoint = focus.position;
        transform.localRotation = Quaternion.Euler(orbitAngles);
        transform.localRotation = orbitRotation = Quaternion.Euler(orbitAngles);
    }

    private void Start()
    {
        lookAction = InputSystem.actions.FindAction("Look");
    }

    void LateUpdate()
    {
        /*gravityAlignment =
            Quaternion.FromToRotation(
                gravityAlignment * Vector3.up,
                CustomGravity.GetUpAxis(focusPoint)
            ) * gravityAlignment;
        /* FromToRotation gives the smallest rotation to go from one direction to another. 
         Multiply our direction by that rotation */
        UpdateGravityAlignment();
        UpdateFocusPoint();
        //ManualRotation();
        
        if (ManualRotation() || AutomaticRotation())
        {
            ConstrainAngles();
            orbitRotation = Quaternion.Euler(orbitAngles);
        }
        Quaternion lookRotation = gravityAlignment * orbitRotation;
        Vector3 lookDirection = lookRotation * Vector3.forward;
        Vector3 lookPosition = focusPoint - lookDirection * distance;
        /* lookDirection * distance scales the normalized direction vector by our desired distance,
         which is then subtracted from the updated focusPoint to give us our new camera position*/

        Vector3 rectOffset = lookDirection * regularCamera.nearClipPlane;
        Vector3 rectPosition = lookPosition + rectOffset;
        Vector3 castFrom = focus.position;
        Vector3 castLine = rectPosition - castFrom;
        float castDistance = castLine.magnitude;
        Vector3 castDirection = castLine / castDistance;


        if (Physics.BoxCast(castFrom, CameraHalfExtends, castDirection, 
            out RaycastHit hit, lookRotation, castDistance, obstructionMask))
        {
            rectPosition = castFrom + castDirection * hit.distance;
            lookPosition = rectPosition - rectOffset;
        }

        transform.SetPositionAndRotation(lookPosition, lookRotation);
    }

    void UpdateGravityAlignment()
    {
        Vector3 fromUp = gravityAlignment * Vector3.up;
        Vector3 toUp = CustomGravity.GetUpAxis(focusPoint);
        if (toUp == Vector3.zero)
        {
            toUp = Vector3.up; // Fallback in case no gravity source is found
        }
        //Debug.Log("From Up: " + fromUp + " To Up: " + toUp);
        float dot = Mathf.Clamp(Vector3.Dot(fromUp, toUp), -1f, 1f);
        float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
        float maxAngle = upAlignmentSpeed * Time.deltaTime;

        Quaternion newAlignment =
            Quaternion.FromToRotation(fromUp, toUp) * gravityAlignment;
        if (angle <= maxAngle)
        {
            gravityAlignment = newAlignment;
        }
        else
        {
            gravityAlignment = Quaternion.SlerpUnclamped(
                gravityAlignment, newAlignment, maxAngle / angle
            );
        }
    }

    void UpdateFocusPoint ()
    {
        previousFocusPoint = focusPoint;
        Vector3 targetPoint = focus.position;
        if (focusRadius > 0f)
        {
            float distance = Vector3.Distance(targetPoint, focusPoint);
            float t = 1f;
            if (distance > 0.01f && focusCentering > 0f)
            {
                t = Mathf.Pow(1f - focusCentering, Time.unscaledDeltaTime);
                /*deltaTime is affected by the game's time scale, so things like slow motion 
                 might make the camera less responsive. unscaledDeltaTime is not affected by this*/
            }
            if (distance > focusRadius)
            {
                t = Mathf.Min(t, focusRadius / distance);
            }
            focusPoint = Vector3.Lerp(targetPoint, focusPoint, t);
        }
        else
        {
            focusPoint = targetPoint;
        }
            
    }

    bool ManualRotation ()
    {
        Vector2 rawInput = lookAction.ReadValue<Vector2>();
        Vector2 input = new Vector2(-rawInput.y, rawInput.x);
        
        const float e = 0.001f;
        if (input.x < -e || input.x > e || input.y < -e || input.y > e)
        {
            orbitAngles += rotationSpeed * input; // removed: * Time.unscaledDeltaTime
            // multiplying by deltaTime introduced spikes in camera movement, which were larger at lower frame rates
            lastManualRotationTime = Time.unscaledTime;
            return true;
        }
        return false;
    }

    bool AutomaticRotation ()
    {
        if (Time.unscaledTime - lastManualRotationTime < alignDelay)
        {
            return false;
        }
        Vector3 alignedDelta = Quaternion.Inverse(gravityAlignment) * (focusPoint - previousFocusPoint);
        Vector2 movement = new Vector2(alignedDelta.x, alignedDelta.z);
        float movementDeltaSqr = movement.sqrMagnitude;
        if (movementDeltaSqr < 0.0001f)
        {
            return false;
        }

        float headingAngle = GetAngle(movement / Mathf.Sqrt(movementDeltaSqr));
        float deltaAbs = Mathf.Abs(Mathf.DeltaAngle(orbitAngles.y, headingAngle));
        float rotationChange = automaticRotationSpeed * Mathf.Min(Time.unscaledDeltaTime, movementDeltaSqr);
        if (deltaAbs < alignSmoothRange)
        {
            rotationChange *= deltaAbs / alignSmoothRange;
        }
        else if (180f - deltaAbs < alignSmoothRange)
        {
            rotationChange *= (180f -deltaAbs) / alignSmoothRange;
        }
            orbitAngles.y = Mathf.MoveTowardsAngle(orbitAngles.y, headingAngle, rotationChange);
        Debug.Log("Automatic Rotation Applied");
        return true;
    }

    void ConstrainAngles ()
    {
        orbitAngles.x = Mathf.Clamp(orbitAngles.x, minVerticalAngle, maxVerticalAngle);
        if (orbitAngles.y < 0f)
        {
            orbitAngles.y += 360f;
        }
        else if (orbitAngles.y >= 360f)
        {
            orbitAngles.y -= 360f;
        }
    }

    static float GetAngle (Vector2 direction)
    {
        float angle = Mathf.Acos(direction.y) * Mathf.Rad2Deg;
        return direction.x < 0f ? 360f - angle : angle;
    }

    Vector3 CameraHalfExtends
    {
        get
        {
            Vector3 halfExtends;
            halfExtends.y =
                regularCamera.nearClipPlane *
                Mathf.Tan(0.5f * Mathf.Deg2Rad * regularCamera.fieldOfView);
            halfExtends.x = halfExtends.y * regularCamera.aspect;
            halfExtends.z = 0f;
            return halfExtends;
        }
    }
}
