using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    Rigidbody _rb; //RigidBody
    Vector3 _input; // вектор для управления
    public float speed; // скорость движения
    public float speedTurn; // скорость поворота
    public bool matrix = false; // Преобразование поворота для изометрии
    Vector3 relative;
    void Start()
    {
        _rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        InputGet();
        Look();
    }
    private void FixedUpdate()
    {
        if (_input != Vector3.zero)
        {
            Move();
        }
    }
    void InputGet()
    {
        _input = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")); // получаем данные с клавиатуры
        if (Input.GetKey(KeyCode.LeftShift)) // ускорение при шифте
        {
            speed = 5f;
        }
        else
        {
            speed = 3f;
        }

    }
    void Move()
    {
        _rb.MovePosition(transform.position + (transform.forward *_input.magnitude)*speed * Time.deltaTime); // сообственно само движение
    }
    void Look()
    {
       if(_input != Vector3.zero)
        {
            if (matrix == false) // требуется ли доп поворот
            {
               relative = (transform.position + _input) - transform.position;
            }
            else
            {
               relative = (transform.position + _input) - transform.position;
            }
            var rot = Quaternion.LookRotation(relative, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation,rot,speedTurn*Time.deltaTime);
            GetComponent<PlayerAnimationCon>().Run(speed,true); // включаем анимации 
        }
        else
        {
            GetComponent<PlayerAnimationCon>().Run(speed, false);
        }
    }
}
