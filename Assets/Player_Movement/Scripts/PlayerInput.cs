using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    Vector2 input;
    Vector2 down;
    Vector2 raw;
    Vector2 previous;

    int jumpTimer;
    bool jump;
    bool spriting;
    bool crouch;
    bool crouching;

    void Start()
    {
        jumpTimer = -1;
        input = Vector2.zero;
    }

    void Update()
    {
        input.x = Input.GetAxis("Horizontal"); // Gets horizontal axis, normally mapped on the left and right arrow keys and has a value between 1 and -1
        input.y = Input.GetAxis("Vertical"); // Gets vertical axis, normally mapped on the up and down keys and also has a value between 1 and -1
        input *= (input.x != 0.0f && input.y != 0.0f) ? .7071f : 1.0f; // This determines the speed at which movement is

        down = Vector2.zero;
        raw.x = Input.GetAxisRaw("Horizontal");
        raw.y = Input.GetAxisRaw("Vertical");
        spriting = Input.GetKey(KeyCode.LeftShift); //Declares the variable sprinting and initialises it with the corrosponding key "Left Shift"
        crouch = Input.GetKeyDown(KeyCode.C); //Declares the variable sprinting and initialises it with the corrosponding key "C"
        crouching = Input.GetKey(KeyCode.C); 

        if (raw.x != previous.x)
        {
            previous.x = raw.x;
            if (previous.x != 0)
                down.x = previous.x;
        }
        if (raw.y != previous.y)
        {
            previous.y = raw.y;
            if (previous.y != 0)
                down.y = previous.y;
        }
    }

    public void FixedUpdate()
    {
        if (!Input.GetKey(KeyCode.Space))
        {
            jump = false;
            jumpTimer++;
        }
        else if (jumpTimer > 0)
            jump = true;
    }

    public Vector2 get()
    {
        return input;
    }

    public Vector2 getRaw()
    {
        return raw;
    }

    public Vector2 onDown()
    {
        return down;
    }

    public bool Jump()
    {
        return jump;
    }

    public void ResetJump()
    {
        jumpTimer = -1;
    }

    public bool Run()
    {
        return spriting;
    }

    public bool Crouch()
    {
        return crouch;
    }

    public bool Crouching()
    {
        return crouching;
    }
}
