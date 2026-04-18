using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PhysicsHazard : MonoBehaviour
{
    [Header("Lifetime")]
    public float lifeTime = 8f;

    [Header("Custom Gravity")]
    public bool useCustomGravity = false;
    public float gravityMultiplier = 2f;

    [Header("Destruction")]
    [Tooltip("Destroy this object when colliding with objects with this tag.")]
    public string destroyOnTag = "Wall";

    private Rigidbody rb;
    private bool launched = false;
    private Vector3 debugLaunchDirection = Vector3.zero;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = !useCustomGravity;
    }

    void FixedUpdate()
    {
        if (useCustomGravity)
        {
            rb.AddForce(Physics.gravity * gravityMultiplier, ForceMode.Acceleration);
        }
    }

    public void Launch(
        Vector3 downhillDirection,
        float launchForce,
        float yawAngleDegrees,
        float pitchAngleDegrees,
        float upwardForce,
        float torqueAmount)
    {
        if (launched) return;
        launched = true;

        Vector3 flatDirection = new Vector3(downhillDirection.x, 0f, downhillDirection.z).normalized;

        if (flatDirection == Vector3.zero)
        {
            Debug.LogWarning("Downhill direction is zero. Hazard cannot launch properly.");
            return;
        }

        float randomYaw = Random.Range(-yawAngleDegrees, yawAngleDegrees);
        float randomPitch = Random.Range(-pitchAngleDegrees, pitchAngleDegrees);

        Quaternion yawRotation = Quaternion.AngleAxis(randomYaw, Vector3.up);

        Vector3 rightAxis = Vector3.Cross(Vector3.up, flatDirection).normalized;
        Quaternion pitchRotation = Quaternion.AngleAxis(randomPitch, rightAxis);

        Vector3 finalDirection = yawRotation * pitchRotation * flatDirection;
        finalDirection.Normalize();

        debugLaunchDirection = finalDirection;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Vector3 launchVector = finalDirection * launchForce + Vector3.up * upwardForce;

        rb.AddForce(launchVector, ForceMode.VelocityChange);

        Vector3 randomTorqueAxis = Random.onUnitSphere;
        rb.AddTorque(randomTorqueAxis * torqueAmount, ForceMode.Impulse);

        Destroy(gameObject, lifeTime);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(destroyOnTag))
        {
            Destroy(gameObject);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (debugLaunchDirection == Vector3.zero) return;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + debugLaunchDirection * 4f);
    }
}