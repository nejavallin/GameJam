using System;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;


public class playerController : MonoBehaviour
{
    [Header("Movement Settings")]
    #region
    [SerializeField] private float playerSpeed = 5.0f;
    [SerializeField] private float horiztontalSpeedModifier = 2.0f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float rotationSpeed = 10f;      // <-- ADD: controls turn speed
    [SerializeField] private float maxTurnAngle = 75f;      
    [SerializeField] private float moveSmoothTime = 0.1f;    // <-- ADD: controls acceleration feel
    [SerializeField] private CharacterController controller;
    #endregion

    #region Getters
    public float Speed => playerSpeed;
    public float JumpHeight => jumpHeight;
    public float HorizontalSpeedModifier => horiztontalSpeedModifier;
    public float RotationSpeed => rotationSpeed;
    #endregion
    #region Modify Methods=>
    // Only PlayerUpgrades should call these
    public void ModifySpeed(float amount) => playerSpeed += amount;
    public void ModifyJumpHeight(float amount) => jumpHeight += amount;

    public void ModifyHorizontalSpeed(float amount) => horiztontalSpeedModifier += amount;
    public void ModifyRotationSpeed(float amount) => rotationSpeed += amount;
    #endregion

    [Header("Knockback Settings")]
    [SerializeField] private float knockbackDecayRate = 8f;   // how fast it fades
    private Vector3 knockbackVelocity = Vector3.zero;

    [Header("Dash Settings")]
    #region
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;
    
    private bool isDashing = false;
    private float dashTimeRemaining = 0f;
    private float dashCooldownRemaining = 0f;
    private Vector3 dashDirection;

    // Can dash flag
    private bool canDash = true;
    #endregion

    private Vector3 playerVelocity;
    private bool groundedPlayer;
    private Vector3 smoothMoveVelocity;     // <-- ADD: used internally by SmoothDamp
    private Vector3 currentMove;            // <-- ADD: the smoothed move direction

    private float coyoteTime = 0.12f;
    private float coyoteTimeCounter;


    [Header("Ragdoll Settings")]
    #region
    [SerializeField] private float ragdollBounceDamping = 0.5f;  // energy lost per bounce (0-1)
    [SerializeField] private float ragdollDuration = 1.5f;
    [SerializeField] private float ragdollMinBounceSpeed = 1f;   // below this, stop bouncing
    [SerializeField] private Vector3 ragdollTumbleAxis;          // set per-call or randomized
    [SerializeField] public float tumbleSpeed = 180f; 
    [SerializeField] public float knockbackForce = 180f;

    private bool isRagdolling = false;
    private float ragdollTimeRemaining = 0f;
    private Vector3 ragdollVelocity = Vector3.zero;
    #endregion

    [Header("Gravity Settings")]
    [SerializeField] private bool simulateGravity = true;     // wether to simulate gravity at all
    [SerializeField] private float gravityScaleMax = 3f;     // max gravity multiplier
    [SerializeField] private float gravityScaleRate = 0.5f;  // how fast gravity ramps up
    [SerializeField] private float gravityValue = -9.81f;
    private float airTime = 0f;       // tracks time in the air

    [Header("Explosion")]
    [SerializeField] float radius = 5f;
    [SerializeField] float minForce = 2f;
    [SerializeField] float maxForce = 25f;

    #region Refernces
    #region Input Refernces=>
    [Header("Input Actions")]
    public InputActionReference moveAction;
    public InputActionReference jumpAction;
    public InputActionReference dashAction;
    #endregion
    
    private Camera playerCam; // Main Cam Ref

    private PlayerUpgrades upgrades; // upgrades component

    [SerializeField] private CollisionRelay relay;
    #endregion


    public void OnEnable()
    {
        moveAction.action.Enable();
        jumpAction.action.Enable();
        dashAction.action.Enable();
    }

    public void OnDisable()
    {
        moveAction.action.Disable();
        jumpAction.action.Disable();
    }

    

    void Awake()
    {
        upgrades = GetComponent<PlayerUpgrades>();
        canDash = true;
        playerCam = Camera.main;
        relay.OnCollision += HandleCollision;
    }
    void OnDestroy()
    {
        relay.OnCollision -= HandleCollision;
    }

    private void Start()
    {
        playerCam = Camera.main;
        
    }

    void Update()
    {
        if (isRagdolling)
        {
            HandleRagdoll();
            return; // skip all normal movement
        }
        //if (Input.GetKeyDown(KeyCode.K))
        //    GetComponent<PlayerDeath>().Die();

        Debug.DrawRay(new Vector3(controller.transform.position.x,  controller.bounds.max.y, controller.transform.position.z ), controller.transform.forward * 50f, Color.red);
        groundedPlayer = controller.isGrounded;

        // Read input
        Vector2 input = moveAction.action.ReadValue<Vector2>();
        Vector3 targetMove = Vector3.ClampMagnitude(new Vector3(input.x, 0, input.y), 1f);
        targetMove.x = input.x * horiztontalSpeedModifier;
        
        // CHANGED: Smooth the movement instead of applying raw input
        currentMove = Vector3.SmoothDamp(currentMove, targetMove, ref smoothMoveVelocity, moveSmoothTime);
        groundedPlayer = IsGrounded();
        // Dash
        if (canDash)
        {
            Dash();
        }

        // CHANGED: Slerp rotation instead of snapping forward
        if (targetMove != Vector3.zero)
        {
            Vector3 flatCamForward = Vector3.ProjectOnPlane(playerCam.transform.forward, Vector3.up).normalized;
            float angle = Vector3.SignedAngle(flatCamForward, targetMove, Vector3.up);

            // Clamp the angle to the allowed range
            float clampedAngle = Mathf.Clamp(angle, -maxTurnAngle, maxTurnAngle);

            // Reconstruct direction from clamped angle relative to camera forward
            Vector3 clampedDirection = Quaternion.AngleAxis(clampedAngle, Vector3.up) * flatCamForward;

            Quaternion targetRotation = Quaternion.LookRotation(clampedDirection);
            controller.transform.rotation = Quaternion.Slerp(controller.transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        else if (!isDashing)
        {
            Vector3 flatCamForward = Vector3.ProjectOnPlane(playerCam.transform.forward, Vector3.up).normalized;
            Quaternion flatCamRotation = Quaternion.LookRotation(flatCamForward);
            controller.transform.rotation = Quaternion.Slerp(controller.transform.rotation, flatCamRotation, rotationSpeed * Time.deltaTime);
        }

        // Jump
        //if (IsGrounded())
        //    coyoteTimeCounter = coyoteTime;
        //else
        //    coyoteTimeCounter -= Time.deltaTime;

        //if (coyoteTimeCounter > 0f && jumpAction.action.WasPressedThisFrame())
        //{
        //    playerVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravityValue);
        //    coyoteTimeCounter = 0f;
        //}

        Debug.DrawRay(controller.transform.position, Vector3.down * (controller.height / 2 + 0.3f), Color.green);
        // Apply gravity
        if (simulateGravity)
        {
            if (groundedPlayer)
            {
                airTime = 0f; // reset when grounded
            }
            else
            {
                airTime += Time.deltaTime; // accumulate air time
            }

            // Gravity scales up the longer you're airborne, clamped to gravityScaleMax
            float gravityMultiplier = Mathf.Clamp(1f + airTime * gravityScaleRate, 1f, gravityScaleMax);
            playerVelocity.y += gravityValue * gravityMultiplier * Time.deltaTime;
        }

        // CHANGED: Use smoothed currentMove for movement


        if (groundedPlayer && playerVelocity.y < 0f)
            playerVelocity.y = -2f;

        // ...

        // CHANGED: add knockbackVelocity into final movement
        Vector3 moveVelocity = isDashing
            ? dashDirection * dashSpeed
            : currentMove * playerSpeed;

        Vector3 finalMove = moveVelocity + Vector3.up * playerVelocity.y + knockbackVelocity;
        controller.Move(finalMove * Time.deltaTime);

        // Decay knockback each frame
        knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, knockbackDecayRate * Time.deltaTime);
    }
    // Move on slope
    //private Vector3 GetSlopeMoveDirection(Vector3 moveDirection)
    //{
    //    if (Physics.Raycast(controller.transform.position, Vector3.down, out RaycastHit hit, controller.height / 2 + 0.3f))
    //    {
    //        return Vector3.ProjectOnPlane(moveDirection, hit.normal).normalized;
    //    }
    //    return moveDirection;
    //}
    private bool IsGrounded()
    {
        return Physics.Raycast(controller.transform.position, Vector3.down,
            controller.height / 2 + 0.1f);
    }
    // Caluclate dash state
    private void Dash()
    {
        // Cooldown tick
        if (dashCooldownRemaining > 0f)
            dashCooldownRemaining -= Time.deltaTime;

        // Trigger dash
        if ((dashAction.action.WasPressedThisFrame() | jumpAction.action.WasCompletedThisFrame())&& !isDashing && dashCooldownRemaining <= 0f)
        {
            simulateGravity = false;
            isDashing = true;
            dashTimeRemaining = dashDuration;
            dashCooldownRemaining = dashCooldown;
            // Disable player input
            OnDisable();

            // Dash in input direction, or forward if standing still
            dashDirection = currentMove.magnitude > 0.1f
                ? currentMove.normalized
                : transform.forward;
        }

        // Tick dash
        if (isDashing)
        {
            dashTimeRemaining -= Time.deltaTime;
            if (dashTimeRemaining <= 0f)
            {
                simulateGravity = true;
                isDashing = false;
                OnEnable();
            }
        }
    }

    void HandleCollision(ControllerColliderHit hit)
    {
        if (hit.gameObject.CompareTag("Expotion"))
        {
            upgrades.TriggerExplosion(radius, minForce, maxForce);
        }
        else if (!hit.gameObject.CompareTag("Ground"))
        {
            Vector3 dir = (transform.position - hit.transform.position).normalized; // away from hit
            dir.y = 0.5f;
            ApplyRagdoll(dir, knockbackForce);
        }
    }

    public void KnockedBack(Vector3 direction, float force)
    {
        knockbackVelocity = direction.normalized * force;

        // Optional: interrupt dash if mid-dash
        if (isDashing)
        {
            isDashing = false;
            simulateGravity = true;
            OnEnable();
        }
    }

    public void ApplyRagdoll(Vector3 direction, float force)
    {
        if (isRagdolling) return;

        isRagdolling = true;
        ragdollTimeRemaining = ragdollDuration;

        // Knockback IS the ragdoll launch velocity — no separate system needed
        ragdollVelocity = direction.normalized * force;
        ragdollVelocity.y = Mathf.Abs(ragdollVelocity.y) + 4f;

        ragdollTumbleAxis = new Vector3(
            UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f)).normalized;

        if (isDashing)
        {
            isDashing = false;
            simulateGravity = true;
        }

        OnDisable();
    }

    private void HandleRagdoll()
    {
        ragdollTimeRemaining -= Time.deltaTime;

        // Tumble visually (rotate the child character, not this transform)
        controller.transform.Rotate(ragdollTumbleAxis * tumbleSpeed * Time.deltaTime, Space.World);

        // Apply gravity to ragdoll velocity
        ragdollVelocity.y += gravityValue * Time.deltaTime;

        CollisionFlags flags = controller.Move(ragdollVelocity * Time.deltaTime);

        // If we hit the ground, bounce
        if ((flags & CollisionFlags.Below) != 0 && ragdollVelocity.y < 0f)
        {
            ragdollVelocity.y = -ragdollVelocity.y * ragdollBounceDamping;
            ragdollVelocity.x *= ragdollBounceDamping;
            ragdollVelocity.z *= ragdollBounceDamping;

            // If barely moving vertically, kill the bounce
            if (Mathf.Abs(ragdollVelocity.y) < ragdollMinBounceSpeed)
                ragdollVelocity.y = 0f;
        }

        // End ragdoll when timer runs out
        if (ragdollTimeRemaining <= 0f)
        {
            isRagdolling = false;
            controller.transform.rotation = Quaternion.identity; // or snap to camera forward
            OnEnable();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (playerCam != null)
        {
            float totalFOV = maxTurnAngle * 2;
            float rayRange = 10.0f;
            float halfFOV = totalFOV / 2.0f;
            Quaternion leftRayRotation = Quaternion.AngleAxis(-halfFOV, Vector3.up);
            Quaternion rightRayRotation = Quaternion.AngleAxis(halfFOV, Vector3.up);
            Vector3 leftRayDirection = leftRayRotation * transform.forward;
            Vector3 rightRayDirection = rightRayRotation * transform.forward;
            Gizmos.DrawRay(controller.transform.position, leftRayDirection * rayRange);
            Gizmos.DrawRay(controller.transform.position, rightRayDirection * rayRange);
        }
    }
}