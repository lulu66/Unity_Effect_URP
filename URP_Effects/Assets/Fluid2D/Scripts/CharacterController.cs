using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterController : MonoBehaviour
{
    [SerializeField] float MoveSpeed = 3;
    private Animator mAnimator;

    void Start()
    {
        mAnimator = GetComponent<Animator>();
    }
    void Update()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 dir = new Vector3(h, 0, v);

        if(dir.magnitude != 0)
		{
            transform.rotation = Quaternion.LookRotation(dir);

            mAnimator.SetBool("isMove", true);
            transform.Translate(Vector3.forward * MoveSpeed * Time.deltaTime);
		}
		else
		{
            mAnimator.SetBool("isMove", false);
		}

    }




}
