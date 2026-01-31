using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Walk")] public float topSpeed = 8f;
    public float accelerationTime = 0.2f;
    public float decelerationTime = 0.15f;

    [Header("Air Control")] [Range(0f, 1f)]
    public float airControlMultiplier = 0.5f;

    [Header("Jump")] public float jumpMaxHeight = 4f;
    public float timeToApex = 0.4f;
    public float hangTime = 0.02f;
    public float timeToFall = 0.3f;

    public int airJumps = 1;

    [Header("Fall")] public float maxFallSpeed = 20f;

    [Header("Player Forgiveness")] public float coyoteTime = 0.1f;
    public float jumpBuffer = 0.1f;

    [Header("Checks")] public Transform groundCheck;
    public Vector2 groundCheckSize = new Vector2(0.6f, 0.1f);
    public LayerMask groundLayer;

    [Header("Facing")] public bool flipOnInput = true;

    Rigidbody2D rb;

    float accel;
    float decel;

    float jumpVelocity;
    float gravityUp;
    float gravityDown;

    bool isGrounded;
    bool wasGrounded;

    int jumpsLeft;

    float coyoteTimer;
    float bufferTimer;
    float hangTimer;

    float inputX;
    float facingDir = 1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        RecalculateParameters();
    }

    public void RecalculateParameters()
    {
        accel = topSpeed / accelerationTime;
        decel = topSpeed / decelerationTime;

        gravityUp = (2f * jumpMaxHeight) / (timeToApex * timeToApex);
        gravityDown = (2f * jumpMaxHeight) / (timeToFall * timeToFall);

        jumpVelocity = gravityUp * timeToApex;

        if (rb)
            rb.gravityScale = gravityDown / Physics2D.gravity.magnitude;
    }

    void Update()
    {
        // AUTO RUN
        inputX = facingDir;

        // CHANGE DIRECTION BUTTON
        if (Input.GetKeyDown(KeyCode.F))
        {
            facingDir *= -1f;
        }

        if (Input.GetKeyDown(KeyCode.J))
            bufferTimer = jumpBuffer;
        else
            bufferTimer -= Time.deltaTime;
    }

    void FixedUpdate()
    {
        CheckEnvironment();
        HandleTimers();
        HandleHorizontal();
        HandleFlip();
        HandleJump();
        ApplyGravity();
        ClampFall();
    }

    void HandleTimers()
    {
        if (isGrounded)
        {
            coyoteTimer = coyoteTime;

            if (!wasGrounded)
            {
                jumpsLeft = airJumps;
                hangTimer = 0f;
            }
        }
        else
        {
            coyoteTimer -= Time.fixedDeltaTime;
        }

        wasGrounded = isGrounded;
    }

    void HandleHorizontal()
    {
        float targetSpeed = inputX * topSpeed;
        float speedDiff = targetSpeed - rb.linearVelocity.x;

        if (isGrounded && Mathf.Abs(inputX) > 0.01f)
        {
            rb.linearVelocity = new Vector2(targetSpeed, rb.linearVelocity.y);
            return;
        }

        float control = isGrounded ? 1f : airControlMultiplier;
        float rate = Mathf.Abs(targetSpeed) > 0.01f ? accel : decel;
        rate *= control;

        float movement = Mathf.Clamp(
            speedDiff,
            -rate * Time.fixedDeltaTime,
            rate * Time.fixedDeltaTime
        );

        rb.linearVelocity += Vector2.right * movement;
    }

    void HandleJump()
    {
        if (bufferTimer <= 0f) return;

        if (coyoteTimer > 0f)
        {
            DoJump();
        }
        else if (jumpsLeft > 0)
        {
            DoJump();
            jumpsLeft--;
        }
    }

    void DoJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpVelocity);
        bufferTimer = 0f;
        coyoteTimer = 0f;
        hangTimer = hangTime;
    }

    void ApplyGravity()
    {
        if (rb.linearVelocity.y > 0f)
        {
            if (hangTimer > 0f && rb.linearVelocity.y <= 0.1f)
            {
                hangTimer -= Time.fixedDeltaTime;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                rb.gravityScale = 0f;
                return;
            }

            rb.gravityScale = gravityUp / Physics2D.gravity.magnitude;
        }
        else
        {
            rb.gravityScale = gravityDown / Physics2D.gravity.magnitude;
        }
    }

    void ClampFall()
    {
        if (rb.linearVelocity.y < -maxFallSpeed)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
    }

    void CheckEnvironment()
    {
        isGrounded = Physics2D.OverlapBox(
            groundCheck.position,
            groundCheckSize,
            0f,
            groundLayer
        );
    }

    void HandleFlip()
    {
        if (!flipOnInput) return;

        float dir = facingDir;
        if (dir == 0f) return;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * dir;
        transform.localScale = scale;
    }

    void OnDrawGizmos()
    {
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded? Color.green: Color.red;
            Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        }
    }
}
