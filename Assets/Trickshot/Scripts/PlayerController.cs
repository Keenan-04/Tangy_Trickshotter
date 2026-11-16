using UnityEngine;

namespace Trickshot
{
    /// <summary>
    /// First-person movement, mouse look, and the spin tracker.
    /// Yaw lives on this object, pitch on cameraPivot.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("References")]
        public Transform cameraPivot;

        [Header("Movement")]
        public float walkSpeed = 7f;
        public float sprintSpeed = 10.5f;
        public float jumpHeight = 2.4f;
        public float gravity = -20f;
        [Tooltip("How quickly you can steer while in the air.")]
        public float airAcceleration = 25f;
        [Tooltip("Seconds after walking off a ledge in which you can still jump.")]
        public float coyoteTime = 0.1f;

        [Header("Look")]
        public float mouseSensitivity = 1f;

        [Header("Spin tracking")]
        [Tooltip("Seconds after landing that a spin still counts for the next shot.")]
        public float landingGrace = 0.25f;
        [Tooltip("Off: only spins done in the air count. On: spins on the ground count too, but drain away.")]
        public bool countGroundSpins = false;
        [Tooltip("Degrees per second a ground spin drains (only used when countGroundSpins is on).")]
        public float groundSpinDecay = 360f;

        /// <summary>Set by the weapon to slow the mouse down while scoped.</summary>
        public float SensitivityScale { get; set; } = 1f;

        /// <summary>Set by the weapon to slow you down while scoped. Sprinting is off below 1.</summary>
        public float MoveSpeedScale { get; set; } = 1f;

        /// <summary>True while the cursor is locked and the game is taking input.</summary>
        public bool InputActive { get; private set; }

        public bool IsAirborne => timeSinceGrounded > 0.08f;
        public float AirTime => timeSinceGrounded;

        /// <summary>Net degrees turned in one direction since the spin started.</summary>
        public float SpinDegrees => Mathf.Abs(spinAccum);

        CharacterController cc;
        Vector3 horizontalVelocity;
        float verticalVelocity;
        float pitch;
        float timeSinceGrounded;
        float groundedFor;
        float spinAccum;
        bool jumped;
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        void Awake()
        {
            Instance = this;
            cc = GetComponent<CharacterController>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            SetCursorLocked(true);
        }

        void OnDisable()
        {
            InputActive = false; // switched off while the menu is up
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // Read the lock state once at the start of the frame so the click that
            // re-locks the cursor does not also fire the rifle.
            InputActive = Cursor.lockState == CursorLockMode.Locked;
            if (!InputActive)
            {
                if (TSInput.FirePressed) SetCursorLocked(true);
            }
            else if (TSInput.PausePressed)
            {
                SetCursorLocked(false);
            }

            float yawDelta = 0f;
            if (InputActive)
            {
                yawDelta = Look();
                if (TSInput.SensitivityDownPressed) mouseSensitivity = Mathf.Max(0.1f, mouseSensitivity - 0.1f);
                if (TSInput.SensitivityUpPressed) mouseSensitivity = Mathf.Min(10f, mouseSensitivity + 0.1f);
            }

            Move(InputActive ? TSInput.Move : Vector2.zero, dt);
            TrackSpin(yawDelta, dt);

            if ((InputActive && TSInput.RespawnPressed) || transform.position.y < -50f) Respawn();
        }

        float Look()
        {
            Vector2 d = TSInput.LookDelta * (mouseSensitivity * SensitivityScale);

            transform.Rotate(0f, d.x, 0f, Space.Self);

            pitch = Mathf.Clamp(pitch - d.y, -89f, 89f);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            return d.x;
        }

        void Move(Vector2 input, float dt)
        {
            bool grounded = cc.isGrounded;
            if (grounded) timeSinceGrounded = 0f;
            else timeSinceGrounded += dt;

            bool sprinting = InputActive && TSInput.SprintHeld && MoveSpeedScale >= 0.99f;
            float speed = (sprinting ? sprintSpeed : walkSpeed) * MoveSpeedScale;
            Vector3 wish = (transform.right * input.x + transform.forward * input.y) * speed;

            if (grounded)
            {
                horizontalVelocity = wish;
                if (verticalVelocity <= 0f)
                {
                    verticalVelocity = -2f; // keeps the controller pressed onto the ground
                    jumped = false;
                }
            }
            else if (input.sqrMagnitude > 0.01f)
            {
                // In the air you keep your momentum and can only nudge it.
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, wish, airAcceleration * dt);
            }

            if (InputActive && TSInput.JumpPressed && !jumped && timeSinceGrounded <= coyoteTime)
            {
                verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
                jumped = true;
            }

            verticalVelocity += gravity * dt;

            CollisionFlags flags = cc.Move((horizontalVelocity + Vector3.up * verticalVelocity) * dt);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
        }

        void TrackSpin(float yawDelta, float dt)
        {
            if (!cc.isGrounded)
            {
                // Turning one way adds, turning back subtracts, so wiggling the mouse earns nothing.
                spinAccum += yawDelta;
                groundedFor = 0f;
                return;
            }

            groundedFor += dt;
            if (countGroundSpins)
            {
                spinAccum += yawDelta;
                spinAccum = Mathf.MoveTowards(spinAccum, 0f, groundSpinDecay * dt);
            }
            else if (groundedFor > landingGrace)
            {
                spinAccum = 0f;
            }
        }

        /// <summary>Called by the weapon after a shot so one spin cannot pay out twice.</summary>
        public void ConsumeSpin()
        {
            spinAccum = 0f;
        }

        public void AddRecoil(float degreesUp)
        {
            pitch = Mathf.Clamp(pitch - degreesUp, -89f, 89f);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        public void Respawn()
        {
            // A CharacterController ignores direct position changes unless it is switched off first.
            cc.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            cc.enabled = true;

            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            spinAccum = 0f;
            pitch = 0f;
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.identity;
        }

        public void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
