using UnityEngine;

public class RespawnOnFall : MonoBehaviour
{
    [Header("���������")]
    [Tooltip("�����, ���� ��������������� ����� ��� �������")]
    public Transform respawnPoint;

    [Tooltip("���� Y ������ ��������� ���� ����� ��������, ��������� �������")]
    public float fallHeight = -10f;

    // �������� ������ ������������ ����� ������� (XR Rig), ����������� ��� ������
    private Vector3 cameraOffset;

    void Start()
    {
        if (respawnPoint == null)
        {
            Debug.LogError("RespawnOnFall: �� ��������� ����� ��������!");
            return;
        }

        if (Camera.main != null)
            cameraOffset = Camera.main.transform.position - transform.position;
        else
            Debug.LogWarning("RespawnOnFall: ������� ������ �� �������. ����� �������������� ������� �������.");
    }

    void Update()
    {
        if (respawnPoint == null) return;

        Vector3 playerPos = Camera.main != null ? Camera.main.transform.position : transform.position;

        if (playerPos.y < fallHeight)
        {
            TeleportToRespawn();
        }
    }

    void TeleportToRespawn()
    {
        // ���������� ���� XR Rig (���� ������) ���, ����� ������ ��������� ��� ������ ��������
        if (Camera.main != null)
            transform.position = respawnPoint.position - cameraOffset;
        else
            transform.position = respawnPoint.position;

        // ���������� �������, ����� �������� ����� (���� respawnPoint ����� �����������)
        transform.rotation = respawnPoint.rotation;

        // ���������� ������, ���� ���� Rigidbody
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Debug.Log("����� ��������� �� ����� ��������");
    }
}