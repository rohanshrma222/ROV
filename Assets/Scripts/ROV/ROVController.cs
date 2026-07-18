using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 6-DOF Rigidbody-based ROV controller.
/// Supports both keyboard (WASD + Q/E for vertical, mouse for yaw/pitch)
/// and on-screen Joystick Pack joysticks simultaneously.
/// Attach to the ROV root GameObject which must have a Rigidbody.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ROVController : MonoBehaviour
{
    [Header("Thrust Settings")]
    [SerializeField] float thrustForce    = 18f;
    [SerializeField] float verticalForce  = 12f;

    [Header("Rotation Rate (deg/sec at full stick deflection)")]
    [Tooltip("Turn rate is driven directly by how far the stick is deflected, instead of an accumulating torque, so a small nudge always produces a small, proportional turn.")]
    [SerializeField] float maxYawRate   = 67.5f;
    [SerializeField] float maxPitchRate = 45f;

    [Header("Drag (simulates water resistance)")]
    [SerializeField] float linearDrag  = 4f;
    [SerializeField] float angularDrag = 5f;

    [Header("Physics Stabilization")]
    [Tooltip("Gently rights the ROV's roll and pitch back to upright position")]
    [SerializeField] float selfRightingStrength = 15f;
    [Tooltip("Smoothing factor for input changes (higher = snappier, lower = more inertial)")]
    [SerializeField] float inputSmoothing = 8f;

    private Vector3 _smoothMove;
    private float _smoothYaw;
    private float _smoothPitch;
    private float _smoothAscend;

    [Header("Joystick References (optional — keyboard works without these)")]
    [Tooltip("Left joystick: X = strafe/yaw, Y = forward/back")]
    [SerializeField] Joystick moveJoystick;
    [Tooltip("Right joystick: X = yaw, Y = ascend/descend")]
    [SerializeField] Joystick lookJoystick;
    [Tooltip("Scales the left joystick's output before it's applied — lower feels less twitchy")]
    [SerializeField, Range(0.1f, 1f)] float moveJoystickSensitivity = 0.5f;
    [Tooltip("Scales the right joystick's output before it's applied — lower feels less twitchy")]
    [SerializeField, Range(0.05f, 1f)] float lookJoystickSensitivity = 0.25f;

    [Header("Mouse Look")]
    [SerializeField] float mouseSensitivity = 2f;
    [SerializeField] bool  invertPitch      = false;

    // ── Public sensor readouts ──────────────────────────────────────────────
    /// Depth below water surface in metres (used by ROVHUD / World.cs).
    public float Depth    => Mathf.Max(0f, WaterSurfaceY - transform.position.y);
    /// Current heading in degrees 0-360.
    public float Heading  => (transform.eulerAngles.y + 360f) % 360f;

    // ── Internals ───────────────────────────────────────────────────────────
    Rigidbody _rb;
    float     _waterSurfaceY = 8f;   // fallback — overridden if UnderwaterEnvironment present
    float     WaterSurfaceY   => _waterSurfaceY;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity              = false;
        _rb.linearDamping           = linearDrag;
        _rb.angularDamping          = angularDrag;
        _rb.interpolation           = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode  = CollisionDetectionMode.ContinuousDynamic;
        _rb.constraints             = RigidbodyConstraints.None;

        // Automatically attach the click/drag model rotator component at runtime
        if (gameObject.GetComponent<ROVModelRotator>() == null)
        {
            gameObject.AddComponent<ROVModelRotator>();
        }
    }

    public bool IsDraggingModel { get; set; }

    /// <summary>
    /// Assigns joystick references at runtime, for ROVs spawned dynamically (e.g. AR placement)
    /// rather than wired via the Inspector.
    /// </summary>
    public void ConfigureJoysticks(Joystick move, Joystick look)
    {
        moveJoystick = move;
        lookJoystick = look;
    }

    void Start()
    {
        // Try to read water level from UnderwaterEnvironment
        var uw = FindFirstObjectByType<UnderwaterEnvironment>();
        if (uw != null) _waterSurfaceY = uw.waterLevel;
    }

    void FixedUpdate()
    {
        if (IsDraggingModel) return;

        Vector3 targetMove   = GetMoveInput();
        float   targetYaw    = GetYawInput();
        float   targetPitch  = GetPitchInput();
        float   targetAscend = GetVerticalInput();

        // Smooth inputs to simulate inertia and gradual acceleration/deceleration
        _smoothMove   = Vector3.Lerp(_smoothMove, targetMove, Time.fixedDeltaTime * inputSmoothing);
        _smoothYaw    = Mathf.Lerp(_smoothYaw, targetYaw, Time.fixedDeltaTime * inputSmoothing);
        _smoothPitch  = Mathf.Lerp(_smoothPitch, targetPitch, Time.fixedDeltaTime * inputSmoothing);
        _smoothAscend = Mathf.Lerp(_smoothAscend, targetAscend, Time.fixedDeltaTime * inputSmoothing);

        // ── Thrust (forward/back + strafe + vertical) ──────────────────────
        Vector3 localForce = new Vector3(_smoothMove.x * thrustForce, _smoothAscend * verticalForce, _smoothMove.z * thrustForce);
        _rb.AddRelativeForce(localForce, ForceMode.Force);

        // ── Rotation (yaw + pitch) ─────────────────────────────────────────
        // Rate-controlled: stick deflection maps directly to a turn rate, so it stops
        // turning as soon as the stick is centred instead of spinning up the longer
        // it's held (which is what a torque that keeps accumulating every frame does).
        Vector3 localAngularVel = transform.InverseTransformDirection(_rb.angularVelocity);
        localAngularVel.y = _smoothYaw * maxYawRate * Mathf.Deg2Rad;
        localAngularVel.x = -_smoothPitch * maxPitchRate * Mathf.Deg2Rad;
        _rb.angularVelocity = transform.TransformDirection(localAngularVel);

        // ── Self-Righting (gentle force to pull roll/pitch back to upright) ─
        if (selfRightingStrength > 0.01f)
        {
            // Get angles in -180 to 180 space
            float rollAngle = transform.eulerAngles.z;
            float pitchAngle = transform.eulerAngles.x;

            if (rollAngle > 180f) rollAngle -= 360f;
            if (pitchAngle > 180f) pitchAngle -= 360f;

            // Apply opposing corrective torque to neutralize rotation
            float correctiveRoll = -rollAngle * selfRightingStrength * Time.fixedDeltaTime;
            float correctivePitch = -pitchAngle * selfRightingStrength * Time.fixedDeltaTime;

            // Dampen active roll/pitch velocity to prevent oscillation
            Vector3 dampAngularVel = transform.InverseTransformDirection(_rb.angularVelocity);
            correctiveRoll -= dampAngularVel.z * selfRightingStrength * 0.1f * Time.fixedDeltaTime;
            correctivePitch -= dampAngularVel.x * selfRightingStrength * 0.1f * Time.fixedDeltaTime;

            _rb.AddRelativeTorque(new Vector3(correctivePitch, 0f, correctiveRoll), ForceMode.VelocityChange);
        }
    }

    // ── Input helpers ───────────────────────────────────────────────────────

    Vector3 GetMoveInput()
    {
        float x = 0f, z = 0f;

        // Keyboard
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)   z += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) z -= 1f;
        }

        // Joystick (additive — whichever is larger wins)
        if (moveJoystick != null)
        {
            if (Mathf.Abs(moveJoystick.Horizontal) > 0.1f) x = Mathf.Clamp(x + moveJoystick.Horizontal * moveJoystickSensitivity, -1f, 1f);
            if (Mathf.Abs(moveJoystick.Vertical)   > 0.1f) z = Mathf.Clamp(z + moveJoystick.Vertical   * moveJoystickSensitivity, -1f, 1f);
        }

        return new Vector3(x, 0f, z);
    }

    float GetYawInput()
    {
        float yaw = 0f;

        // Mouse
        if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            yaw += Mouse.current.delta.x.ReadValue() * mouseSensitivity * Time.fixedDeltaTime;

        // Joystick right stick X
        if (lookJoystick != null && Mathf.Abs(lookJoystick.Horizontal) > 0.1f)
            yaw += lookJoystick.Horizontal * lookJoystickSensitivity;

        return Mathf.Clamp(yaw, -1f, 1f);
    }

    float GetPitchInput()
    {
        float pitch = 0f;

        if (Mouse.current != null && Mouse.current.rightButton.isPressed)
        {
            float raw = Mouse.current.delta.y.ReadValue() * mouseSensitivity * Time.fixedDeltaTime;
            pitch += invertPitch ? -raw : raw;
        }

        if (lookJoystick != null && Mathf.Abs(lookJoystick.Vertical) > 0.1f)
            pitch += lookJoystick.Vertical * lookJoystickSensitivity;

        return Mathf.Clamp(pitch, -1f, 1f);
    }

    float GetVerticalInput()
    {
        float v = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.eKey.isPressed) v += 1f;
            if (Keyboard.current.qKey.isPressed) v -= 1f;
        }

        // Right joystick vertical for ascend/descend as fallback when no look joystick assigned separately
        if (moveJoystick != null && lookJoystick == null)
        {
            // use nothing — keyboard only handles vertical in single-joystick setup
        }

        return Mathf.Clamp(v, -1f, 1f);
    }
}
