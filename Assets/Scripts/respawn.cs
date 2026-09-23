using UnityEngine;

public class Respawnable : MonoBehaviour
{
    public float fallThreshold = -10f;
    public Transform respawnPoint;

    private Vector3 initialPosition;
    private Quaternion initialRotation;

    void Start()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    void Update()
    {
        if (transform.position.y < fallThreshold)
            Respawn();
    }

    void Respawn()
    {
        Vector3 targetPos;
        Quaternion targetRot;

        if (respawnPoint != null)
        {
            targetPos = respawnPoint.position;
            targetRot = respawnPoint.rotation;
        }
        else
        {
            GameObject xrOrigin = GameObject.Find("XR Origin");
            if (xrOrigin != null)
            {
                targetPos = xrOrigin.transform.position + xrOrigin.transform.forward * 1.5f + Vector3.up * 1f;
                targetRot = Quaternion.identity;
            }
            else
            {
                targetPos = initialPosition;
                targetRot = initialRotation;
            }
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        transform.SetPositionAndRotation(targetPos, targetRot);
    }
}