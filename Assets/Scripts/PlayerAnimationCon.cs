using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimationCon : MonoBehaviour
{

    Animator anim;
    float t;

    void Start()
    {
        anim = GetComponent<Animator>();
    }


    void Update()
    {

     }
   public void Run(float speed,bool status)
    {
        anim.SetBool("Run", status);
        if (status == true)
        {
            if (speed>3)
            {
                t += Time.deltaTime;
                if (t >= 2) { t = 2; }
                anim.SetFloat("Speed", t);
            }
            else
            {
                t -= Time.deltaTime;
                if (t <= 0) { t = 0; }
                anim.SetFloat("Speed", t);
            }
        }
        else
        {
            t = 0;
        }
    }
}
