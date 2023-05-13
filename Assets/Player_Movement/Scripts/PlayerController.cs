﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum Status { idle, moving, crouching, sliding, climbingLadder, wallRunning, grabbedLedge, climbingLedge, vaulting }

public class PlayerController : MonoBehaviour
{
    public Status status;
    [SerializeField]
    private LayerMask vaultLayer;
    [SerializeField]
    private LayerMask ledgeLayer;
    [SerializeField]
    private LayerMask ladderLayer;
    [SerializeField]
    private LayerMask wallrunLayer;

    GameObject vaultHelper;

    Vector3 wallNormal = Vector3.zero;
    Vector3 ladderNormal = Vector3.zero;
    Vector3 pushFrom;
    Vector3 slideDir;
    Vector3 vaultOver;
    Vector3 vaultDir;

    PlayerMovement movement;
    PlayerInput playerInput;
    AnimateLean animateLean;

    bool canInteract;
    bool canGrabLedge;
    bool controlledSlide;

    float rayDistance;
    float slideLimit;
    float slideTime;
    float radius;
    float height;
    float halfradius;
    float halfheight;

    int wallDir = 1;

    private void Start()
    {
        CreateVaultHelper();
        playerInput = GetComponent<PlayerInput>();
        movement = GetComponent<PlayerMovement>();

        if (GetComponentInChildren<AnimateLean>())
            animateLean = GetComponentInChildren<AnimateLean>();

        slideLimit = movement.controller.slopeLimit - .1f;
        radius = movement.controller.radius;
        height = movement.controller.height;
        halfradius = radius / 2f;
        halfheight = height / 2f;
        rayDistance = height * .5f + radius;
    }

    /******************************* UPDATE ******************************/
    void Update()
    {
        //Updates
        UpdateInteraction();
        UpdateMovingStatus();


        //Check for movement updates
        CheckSliding();
        CheckCrouching();
        CheckForWallrun();
        CheckLadderClimbing();
        UpdateLedgeGrabbing();
        CheckForVault();

        //Misc
        UpdateLean();
    }

    void UpdateInteraction()
    {
        if (!canInteract)
        {
            if (movement.grounded || movement.moveDirection.y < 0)
                canInteract = true;
        }
        else if ((int)status >= 6)
            canInteract = false;
    }

    void UpdateMovingStatus()
    {
        if ((int)status <= 1)
        {
            status = Status.idle;
            if (playerInput.get().magnitude > 0.02f)
                status = Status.moving;
        }
    }

    void UpdateLean()
    {
        if (animateLean == null) return;
        Vector2 lean = Vector2.zero;
        if (status == Status.wallRunning)
            lean.x = wallDir;
        if (status == Status.sliding && controlledSlide)
            lean.y = -1;
        else if (status == Status.grabbedLedge || status == Status.vaulting)
            lean.y = 1;
        animateLean.SetLean(lean);
    }
    /*********************************************************************/


    /******************************** MOVE *******************************/
    void FixedUpdate()
    {
        if (status == Status.sliding)
        {
            SlideMovement();
            return;
        }
        if (status == Status.climbingLadder)
        {
            LadderMovement();
            return;
        }
        if (status == Status.grabbedLedge)
        {
            GrabbedLedgeMovement();
            return;
        }
        if (status == Status.climbingLedge)
        {
            ClimbLedgeMovement();
            return;
        }
        if (status == Status.wallRunning)
        {
            WallrunningMovement();
            return;
        }
        if(status == Status.vaulting)
        {
            VaultMovement();
            return;
        }

        DefaultMovement();
    }

    void DefaultMovement()
    {
        if (playerInput.Run() && status == Status.crouching)
            Uncrouch();

        movement.Move(playerInput.get(), playerInput.Run(), (status == Status.crouching));
        if (movement.grounded && playerInput.Jump())
        {
            if (status == Status.crouching)
                Uncrouch();

            movement.Jump(Vector3.up, 1f);
            playerInput.ResetJump();
        }
    }
    /*********************************************************************/

    /****************************** SLIDING ******************************/
    void SlideMovement()
    {
        if (movement.grounded && playerInput.Jump())
        {
            if (controlledSlide)
                slideDir = transform.forward;
            movement.Jump(slideDir + Vector3.up, 1f);
            playerInput.ResetJump();
            slideTime = 0;
        }

        movement.Move(slideDir, movement.slideSpeed, 1f);
        if (slideTime <= 0)
        {
            if (playerInput.Crouching())
                Crouch();
            else
                Uncrouch();
        }
    }

    void CheckSliding()
    {
        //Check to slide when running
        if(playerInput.Crouch() && canSlide())
        {
            slideDir = transform.forward;
            movement.controller.height = halfheight;
            controlledSlide = true;
            slideTime = 1f;
        }

        //Lower slidetime
        if (slideTime > 0)
        {
            status = Status.sliding;
            slideTime -= Time.deltaTime;
        }

        RaycastHit hit;
        if (Physics.SphereCast(transform.position, halfradius / 3f, -Vector3.up, out hit, rayDistance))
        {
            float angle = Vector3.Angle(hit.normal, Vector3.up);
            if (angle > slideLimit && movement.moveDirection.y < 0)
            {
                Vector3 hitNormal = hit.normal;
                slideDir = new Vector3(hitNormal.x, -hitNormal.y, hitNormal.z);
                Vector3.OrthoNormalize(ref hitNormal, ref slideDir);
                controlledSlide = false;
                status = Status.sliding;
            }
        }
    }

    bool canSlide()
    {
        if (!movement.grounded) return false;
        if (playerInput.get().magnitude <= 0.02f || !playerInput.Run()) return false;
        if (slideTime > 0 || status == Status.sliding) return false;
        return true;
    }
    /*********************************************************************/

    /***************************** CROUCHING *****************************/
    void CheckCrouching()
    {
        if (!movement.grounded || (int)status > 2) return;

        if(playerInput.Crouch())
        {
            if (status != Status.crouching)
                Crouch();
            else
                Uncrouch();
        }
    }

    void Crouch()
    {
        movement.controller.height = halfheight;
        status = Status.crouching;
    }

    void Uncrouch()
    {
        movement.controller.height = height;
        status = Status.moving;
    }
    /*********************************************************************/

    /************************** LADDER CLIMBING **************************/
    void LadderMovement()
    {
        Vector3 input = playerInput.get();
        Vector3 move = Vector3.Cross(Vector3.up, ladderNormal).normalized;
        move *= input.x;
        move.y = input.y * movement.walkSpeed;

        bool goToGround = false;
        goToGround = (move.y < -0.02f && movement.grounded);

        if (playerInput.Jump())
        {
            movement.Jump((-ladderNormal + Vector3.up * 2f).normalized, 1f);
            playerInput.ResetJump();
            status = Status.moving;
        }

        if (!hasObjectInfront(0.05f, ladderLayer) || goToGround)
        {
            status = Status.moving;
            Vector3 pushUp = ladderNormal;
            pushUp.y = 0.25f;

            movement.ForceMove(pushUp, movement.walkSpeed, 0.25f, true);
        }
        else
            movement.Move(move, 1f, 0f);
    }

    void CheckLadderClimbing()
    {
        if (!canInteract)
            return;
        //Check for ladder all across player (so they cannot use the side)
        bool right = Physics.Raycast(transform.position + (transform.right * halfradius), transform.forward, radius + 0.125f, ladderLayer);
        bool left = Physics.Raycast(transform.position - (transform.right * halfradius), transform.forward, radius + 0.125f, ladderLayer);

        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, radius + 0.125f, ladderLayer) && right && left)
        {
            if (hit.normal != hit.transform.forward) return;

            ladderNormal = -hit.normal;
            if (hasObjectInfront(0.05f, ladderLayer) && playerInput.get().y > 0.02f)
            {
                canInteract = false;
                status = Status.climbingLadder;
            }
        }
    }
    /*********************************************************************/

    /**************************** WALLRUNNING ****************************/
    void WallrunningMovement()
    {
        Vector3 input = playerInput.get();
        float s = (input.y > 0) ? input.y : 0;

        Vector3 move = wallNormal * s;

        if (playerInput.Jump())
        {
            movement.Jump(((Vector3.up * (s + 0.5f)) + (wallNormal * 2f * s) + (transform.right * -wallDir * 1.25f)).normalized, s + 0.5f);
            playerInput.ResetJump();
            status = Status.moving;
        }

        if (!hasWallToSide(wallDir) || movement.grounded)
            status = Status.moving;

        movement.Move(move, movement.runSpeed, (1f - s) + (s / 4f));
    }

    void CheckForWallrun()
    {
        if (!canInteract || movement.grounded || movement.moveDirection.y >= 0)
            return;

        int wall = 0;
        if (hasWallToSide(1))
            wall = 1;
        else if (hasWallToSide(-1))
            wall = -1;

        if (wall == 0) return;

        RaycastHit hit;
        if(Physics.Raycast(transform.position + (transform.right * wall * radius), transform.right * wall, out hit, halfradius, wallrunLayer))
        {
            wallDir = wall;
            wallNormal = Vector3.Cross(hit.normal, Vector3.up) * -wallDir;
            status = Status.wallRunning;
        }
    }

    bool hasWallToSide(int dir)
    {
        //Check for ladder in front of player
        Vector3 top = transform.position + (transform.right * 0.25f * dir);
        Vector3 bottom = top - (transform.up * radius);
        top += (transform.up * radius);

        return (Physics.CapsuleCastAll(top, bottom, 0.25f, transform.right * dir, 0.05f, wallrunLayer).Length >= 1);
    }
    /*********************************************************************/

    /******************** LEDGE GRABBING AND CLIMBING ********************/
    void GrabbedLedgeMovement()
    {
        if (playerInput.Jump())
        {
            movement.Jump((Vector3.up - transform.forward).normalized, 1f);
            playerInput.ResetJump();
            status = Status.moving;
        }

        movement.Move(Vector3.zero, 0f, 0f); //Stay in place
    }

    void ClimbLedgeMovement()
    {
        Vector3 dir = pushFrom - transform.position;
        Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
        Vector3 move = Vector3.Cross(dir, right).normalized;

        movement.Move(move, movement.walkSpeed, 0f);
        if (new Vector2(dir.x, dir.z).magnitude < 0.125f)
            status = Status.idle;
    }

    void CheckLedgeGrab()
    {
        //Check for ledge to grab onto 
        Vector3 dir = transform.TransformDirection(new Vector3(0, -0.5f, 1).normalized);
        Vector3 pos = transform.position + (Vector3.up * height / 3f) + (transform.forward * radius / 2f);
        bool right = Physics.Raycast(pos + (transform.right * radius / 2f), dir, radius + 0.125f, ledgeLayer);
        bool left = Physics.Raycast(pos - (transform.right * radius / 2f), dir, radius + 0.125f, ledgeLayer);

        RaycastHit hit;
        if (Physics.Raycast(pos, dir, out hit, radius + 0.125f, ledgeLayer) && right && left)
        {
            Vector3 rotatePos = transform.InverseTransformPoint(hit.point);
            rotatePos.x = 0; rotatePos.z = 1;
            pushFrom = transform.position + transform.TransformDirection(rotatePos); //grab the position with local z = 1
            rotatePos.z = radius * 2f;

            Vector3 checkCollisions = transform.position + transform.TransformDirection(rotatePos); //grab it closer now

            //Check if you would be able to stand on the ledge
            if (!Physics.SphereCast(checkCollisions, radius, Vector3.up, out hit, height - radius))
            {
                canInteract = false;
                status = Status.grabbedLedge;
            }
        }
    }

    void UpdateLedgeGrabbing()
    {
        if (movement.grounded || movement.moveDirection.y > 0)
            canGrabLedge = true;

        if (status != Status.climbingLedge)
        {
            if (canGrabLedge && !movement.grounded)
            {
                if (movement.moveDirection.y < 0)
                    CheckLedgeGrab();
            }

            if (status == Status.grabbedLedge)
            {
                canGrabLedge = false;
                Vector2 down = playerInput.onDown();
                if (down.y == -1)
                    status = Status.moving;
                else if (down.y == 1)
                    status = Status.climbingLedge;
            }
        }
    }
    /*********************************************************************/

    /***************************** VAULTING ******************************/
    void VaultMovement()
    {
        Vector3 dir = vaultOver - transform.position;
        Vector3 localPos = vaultHelper.transform.InverseTransformPoint(transform.position);
        Vector3 move = (vaultDir + (Vector3.up * -(localPos.z - radius) * height)).normalized;

        if(localPos.z > halfheight)
        {
            movement.controller.height = height;
            status = Status.moving;
        }

        movement.Move(move, movement.runSpeed, 0f);
    }

    void CheckForVault()
    {
        if (status == Status.vaulting) return;

        float checkDis = 0.05f;
        checkDis += (movement.controller.velocity.magnitude / 16f); //Check farther if moving faster
        if(hasObjectInfront(checkDis, vaultLayer) && playerInput.Jump())
        {
            RaycastHit sphereHit;
            if (Physics.SphereCast(transform.position + (transform.forward * (radius - 0.25f)), 0.25f, transform.forward, out sphereHit, checkDis, vaultLayer))
            {
                RaycastHit hit;
                if (Physics.SphereCast(sphereHit.point + (Vector3.up * halfheight), radius, Vector3.down, out hit, halfheight - radius, vaultLayer))
                {
                    //Check above the point to make sure the player can fit
                    RaycastHit trash;
                    if (Physics.SphereCast(hit.point + (Vector3.up * radius), radius, Vector3.up, out trash, height-radius))
                        return; //If cannot fit the player then do not vault

                    vaultOver = hit.point;
                    vaultDir = transform.forward;
                    SetVaultHelper();

                    canInteract = false;
                    status = Status.vaulting;
                    movement.controller.height = radius;
                }
            }
        }
    }

    void CreateVaultHelper()
    {
        vaultHelper = new GameObject();
        vaultHelper.transform.name = "(IGNORE) Vault Helper";
    }

    void SetVaultHelper()
    {
        vaultHelper.transform.position = vaultOver;
        vaultHelper.transform.rotation = Quaternion.LookRotation(vaultDir);
    }
    /*********************************************************************/

    bool hasObjectInfront(float dis, LayerMask layer)
    {
        Vector3 top = transform.position + (transform.forward * 0.25f);
        Vector3 bottom = top - (transform.up * halfheight);

        return (Physics.CapsuleCastAll(top, bottom, 0.25f, transform.forward, dis, layer).Length >= 1);
    }
}
