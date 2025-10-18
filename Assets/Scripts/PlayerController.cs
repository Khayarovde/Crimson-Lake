using UnityEngine;

public class TankController : MonoBehaviour
{
    public Animator animator; // Animator component
    public float moveSpeed = 3f; // Forward-backward speed
    public float rotateSpeed = 10f; // Rotation speed
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        ProcessMovement();
        RotateByMouse();
    }

    private void FixedUpdate()
    {
        ApplyMovement();
    }

    void ProcessMovement()
    {
        // Берём только ось Vertical (W/S)
        float vertical = Input.GetAxisRaw("Vertical");

        // Меняем состояние анимации
        animator.SetBool("IsRunning", vertical != 0);
    }

    void ApplyMovement()
    {
        // Ось Vertical определяет направление движения (вперёд/назад)
        float vertical = Input.GetAxisRaw("Vertical");

        // Строго двигаемся по направлению вперёд или назад
        Vector3 movement = transform.forward * vertical;

        // Двигаем тело персонажа
        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
    }

    void RotateByMouse()
    {
        // Получаем позицию мыши на экране
        Vector3 screenPosition = Input.mousePosition;

        // Пересчитываем позицию мыши в локальные координаты
        Vector3 localMousePosition = new Vector3(screenPosition.x - Screen.width / 2f, 0, screenPosition.y - Screen.height / 2f);

        // Получаем направление из центрального положения в сторону мыши
        Vector3 direction = localMousePosition.normalized;

        // Формируем целевое вращение
        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);

        // Поворачиваем игрока плавно
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
    }
}