using UnityEngine;

public class TankControl : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float turnSpeed = 180f;  // скорость поворота (в градусах в секунду)

    private Vector3 moveDirection;
    private float turnInput;

    void Update()
    {
        HandleInput();
        MoveCharacter();
    }

    // Обрабатываем пользовательский ввод
    private void HandleInput()
    {
        // Движение вперед / назад
        moveDirection = Vector3.zero;

        if (Input.GetKey(KeyCode.W))  // Вперед
        {
            moveDirection = Vector3.forward;
        }
        else if (Input.GetKey(KeyCode.S))  // Назад
        {
            moveDirection = Vector3.back;
        }

        // Повороты
        if (Input.GetKey(KeyCode.A))  // Поворот влево
        {
            turnInput = -1f;
        }
        else if (Input.GetKey(KeyCode.D))  // Поворот вправо
        {
            turnInput = 1f;
        }
        else
        {
            turnInput = 0f;
        }
    }

    // Двигаем персонажа
    private void MoveCharacter()
    {
        // Двигаем персонажа по оси Z (вперед или назад)
        transform.Translate(moveDirection * moveSpeed * Time.deltaTime, Space.Self);

        // Поворачиваем персонажа по оси Y
        if (turnInput != 0f)
        {
            transform.Rotate(Vector3.up, turnInput * turnSpeed * Time.deltaTime);
        }
    }
}