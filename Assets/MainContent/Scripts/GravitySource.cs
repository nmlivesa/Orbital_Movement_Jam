using UnityEngine;

public class GravitySource : MonoBehaviour
{

    [SerializeField]
    float standardGravityMultiplier = 1f;
    void OnEnable()
    {
        CustomGravity.Register(this);
    }
    void OnDisable()
    {
        CustomGravity.Unregister(this);
    }

    public virtual Vector3 GetGravity(Vector3 position)
    {
        return Physics.gravity * standardGravityMultiplier;
    }
}
