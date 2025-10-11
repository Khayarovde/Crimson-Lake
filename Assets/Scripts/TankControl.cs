using UnityEngine;

public class TankControl : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float turnSpeed = 180f;  // �������� �������� (� �������� � �������)

    private Vector3 moveDirection;
    private float turnInput;

    void Update()
    {
        HandleInput();
        MoveCharacter();
    }

    // ������������ ���������������� ����
    private void HandleInput()
    {
        // �������� ������ / �����
        moveDirection = Vector3.zero;

        if (Input.GetKey(KeyCode.W))  // ������
        {
            moveDirection = Vector3.forward;
        }
        else if (Input.GetKey(KeyCode.S))  // �����
        {
            moveDirection = Vector3.back;
        }

        // ��������
        if (Input.GetKey(KeyCode.A))  // ������� �����
        {
            turnInput = -1f;
        }
        else if (Input.GetKey(KeyCode.D))  // ������� ������
        {
            turnInput = 1f;
        }
        else
        {
            turnInput = 0f;
        }
    }

    // ������� ���������
    private void MoveCharacter()
    {
        // ������� ��������� �� ��� Z (������ ��� �����)
        transform.Translate(moveDirection * moveSpeed * Time.deltaTime, Space.Self);

        // ������������ ��������� �� ��� Y
        if (turnInput != 0f)
        {
            transform.Rotate(Vector3.up, turnInput * turnSpeed * Time.deltaTime);
        }
    }
}