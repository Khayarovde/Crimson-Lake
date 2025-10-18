using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    Rigidbody _rb; //RigidBody
    Vector3 _input; // ������ ��� ����������
    public float speed; // �������� ��������
    public float speedTurn; // �������� ��������
    public bool matrix = false; // �������������� �������� ��� ���������
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
        _input = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")); // �������� ������ � ����������
        if (Input.GetKey(KeyCode.LeftShift)) // ��������� ��� �����
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
        _rb.MovePosition(transform.position + (transform.forward *_input.magnitude)*speed * Time.deltaTime); // ����������� ���� ��������
    }
    void Look()
    {
       if(_input != Vector3.zero)
        {
            if (matrix == false) // ��������� �� ��� �������
            {
               relative = (transform.position + _input) - transform.position;
            }
            else
            {
               relative = (transform.position + _input) - transform.position;
            }
            var rot = Quaternion.LookRotation(relative, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation,rot,speedTurn*Time.deltaTime);
            GetComponent<PlayerAnimationCon>().Run(speed,true); // �������� �������� 
        }
        else
        {
            GetComponent<PlayerAnimationCon>().Run(speed, false);
        }
    }
}
