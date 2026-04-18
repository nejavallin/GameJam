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
    private bool canDash;
    #endregion

    private Vector3 playerVelocity;
    private bool groundedPlayer;
    private Vector3 smoothMoveVelocity;     // <-- ADD: used internally by SmoothDamp
    private Vector3 currentMove;            // <-- ADD: the smoothed move direction

    [Header("Explosion Settings")]
    [SerializeField] private float eRadius = 5f;
    [SerializeField] private float eMinForce = 2f;
    [SerializeField] private float eMaxForce = 25f;

    [Header("Gravity Settings")]
    [SerializeField] private bool simulateGravity = true;     // wether to simulate gravity at all
    [SerializeField] private float gravityScaleMax = 3f;     // max gravity multiplier
    [SerializeField] private float gravityScaleRate = 0.5f;  // how fast gravity ramps up
    [SerializeField] private float gravityValue = -9.81f;
    private float airTime = 0f;                              // tracks time in the air

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


    private void OnEnable()
    {
        moveAction.action.Enable();
        jumpAction.action.Enable();
    }

    private void OnDisable()
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
        //if (Input.GetKeyDown(KeyCode.K))
        //    GetComponent<PlayerDeath>().Die();

        Debug.DrawRay(new Vector3(transform.position.x, transform.position.y + controller.bounds.max.y, transform.position.z ), transform.forward * 50f, Color.red);
        groundedPlayer = controller.isGrounded;

        if (groundedPlayer && playerVelocity.y < -2f)
            playerVelocity.y = -2f;

        // Read input
        Vector2 input = moveAction.action.ReadValue<Vector2>();
        Vector3 targetMove = Vector3.ClampMagnitude(new Vector3(input.x, 0, input.y), 1f);
        targetMove.x = input.x * horiztontalSpeedModifier;
        
        // CHANGED: Smooth the movement instead of applying raw input
        currentMove = Vector3.SmoothDamp(currentMove, targetMove, ref smoothMoveVelocity, moveSmoothTime);

        // Dash
        if (canDash)
        {
            Dash();
        }

        // CHANGED: Slerp rotation instead of snapping forward
        if (targetMove != Vector3.zero)
        {
            float angle = Vector3.SignedAngle(playerCam.transform.forward, targetMove, Vector3.up);

            // Clamp the angle to the allowed range
            float clampedAngle = Mathf.Clamp(angle, -maxTurnAngle, maxTurnAngle);

            // Reconstruct direction from clamped angle relative to camera forward
            Vector3 clampedDirection = Quaternion.AngleAxis(clampedAngle, Vector3.up) * playerCam.transform.forward;

            Quaternion targetRotation = Quaternion.LookRotation(clampedDirection);
            controller.transform.rotation = Quaternion.Slerp(controller.transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        else if (!isDashing)
        {
            controller.transform.rotation = Quaternion.Slerp(controller.transform.rotation, playerCam.transform.rotation, rotationSpeed * Time.deltaTime);
        }

        // Jump
        if (groundedPlayer && jumpAction.action.WasPressedThisFrame())
            playerVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravityValue);


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
        Vector3 moveVelocity = isDashing
    ? dashDirection * dashSpeed
    : currentMove * playerSpeed;

        Vector3 finalMove = moveVelocity + Vector3.up * playerVelocity.y;
        controller.Move(finalMove * Time.deltaTime);


    }

    // Caluclate dash state
    private void Dash()
    {
        // Cooldown tick
        if (dashCooldownRemaining > 0f)
            dashCooldownRemaining -= Time.deltaTime;

        // Trigger dash
        if (dashAction.action.WasPressedThisFrame() && !isDashing && dashCooldownRemaining <= 0f)
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
            upgrades.TriggerExplosion(eRadius, eMinForce, eMaxForce);
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
            Vector3 leftRayDirection = leftRayRotation * playerCam.transform.forward;
            Vector3 rightRayDirection = rightRayRotation * playerCam.transform.forward;
            Gizmos.DrawRay(controller.transform.position, leftRayDirection * rayRange);
            Gizmos.DrawRay(controller.transform.position, rightRayDirection * rayRange);
        }
    }
}