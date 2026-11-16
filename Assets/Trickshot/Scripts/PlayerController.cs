using UnityEngine;

namespace Trickshot
{
    /// <summary>
    /// First-person movement, mouse look, and the spin tracker.
    /// Yaw lives on this object, pitch on cameraPivot.
    ///
    /// Movement is momentum based: you speed up and slow down over a few frames instead of
    /// snapping, keep your speed through the air, and can slide by crouching at a sprint.
    /// A slide can be jumped out of with its speed intact.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("References")]
        public Transform cameraPivot;

        [Header("Speed")]
        public float walkSpeed = 7f;
        public float sprintSpeed = 10.5f;
        public float crouchSpeed = 3.5f;
        [Tooltip("How fast you reach full speed on the ground, in m/s per second.")]
        public float groundAcceleration = 70f;
        [Tooltip("How fast you stop when you let go of the keys.")]
        public float groundDeceleration = 55f;
        [Tooltip("How quickly you can steer while in the air.")]
        public float airAcceleration = 25f;

        [Header("Jump")]
        public float jumpHeight = 2.4f;
        public float gravity = -20f;
        [Tooltip("Seconds after walking off a ledge in which you can still jump.")]
        public float coyoteTime = 0.1f;
        [Tooltip("Seconds a jump press is remembered, so pressing just before landing still jumps.")]
        public float jumpBuffer = 0.12f;

        [Header("Crouch and slide")]
        public float crouchHeight = 1.1f;
        [Tooltip("You must be moving at least this fast for crouch to start a slide.")]
        public float slideMinSpeed = 8f;
        [Tooltip("Speed added when the slide starts.")]
        public float slideBoost = 3f;
        public float slideMaxSpeed = 14.5f;
        [Tooltip("How fast a slide loses speed, in m/s per second.")]
        public float slideFriction = 6.5f;
        [Tooltip("The slide ends when it drops to this speed.")]
        public float slideEndSpeed = 4.5f;
        [Tooltip("How sharply you can steer a slide, in degrees per second.")]
        public float slideSteer = 70f;
        [Tooltip("Seconds before you can slide again.")]
        public float slideCooldown = 0.35f;
        [Tooltip("Degrees the camera leans during a slide.")]
        public float slideLean = 3.5f;

        [Header("Look")]
        public float mouseSensitivity = 1f;

        [Header("Spin tracking")]
        [Tooltip("Seconds after landing that a spin still counts for the next shot.")]
        public float landingGrace = 0.25f;
        [Tooltip("Off: only spins done in the air count. On: spins on the ground count too, but drain away.")]
        public bool countGroundSpins = false;
        [Tooltip("Degrees per second a ground spin drains (only used when countGroundSpins is on).")]
        public float groundSpinDecay = 360f;

        [Header("Falling")]
        [Tooltip("Fall this far below the spawn point and you are put back on it.")]
        public float fallLimit = 40f;

        /// <summary>Set by the weapon to slow the mouse down while scoped.</summary>
        public float SensitivityScale { get; set; } = 1f;

        /// <summary>Set by the weapon to slow you down while scoped. Sprinting is off below 1.</summary>
        public float MoveSpeedScale { get; set; } = 1f;

        /// <summary>Set by the loadout: heavier rifles are slower to carry.</summary>
        public float WeaponMoveScale { get; set; } = 1f;

        /// <summary>True while the cursor is locked and the game is taking input.</summary>
        public bool InputActive { get; private set; }

        public bool IsAirborne => timeSinceGrounded > 0.08f;
        public float AirTime => timeSinceGrounded;
        public bool IsSliding { get; private set; }
        public bool IsCrouching { get; private set; }
        /// <summary>Speed along the ground, in metres per second.</summary>
        public float Speed => horizontalVelocity.magnitude;

        /// <summary>Net degrees turned in one direction since the spin started.</summary>
        public float SpinDegrees => Mathf.Abs(spinAccum);

        CharacterController cc;
        Vector3 horizontalVelocity;
        float verticalVelocity;
        float pitch;
        float roll;
        float timeSinceGrounded;
        float groundedFor;
        float spinAccum;
        bool jumped;
        bool wasGrounded = true;
        float jumpBufferTimer;
        float slideCooldownTimer;
        float standHeight;
        float eyeBelowTop = 0.18f;
        bool eyeMeasured;
        float landDip;
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        void Awake()
        {
            Instance = this;
            cc = GetComponent<CharacterController>();
            standHeight = cc.height;
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
            UpdateCamera(dt);

            if ((InputActive && TSInput.RespawnPressed) || transform.position.y < spawnPosition.y - fallLimit) Respawn();
        }

        float Look()
        {
            Vector2 d = TSInput.LookDelta * (mouseSensitivity * SensitivityScale);
            transform.Rotate(0f, d.x, 0f, Space.Self);
            pitch = Mathf.Clamp(pitch - d.y, -89f, 89f);
            return d.x;
        }

        void Move(Vector2 input, float dt)
        {
            bool grounded = cc.isGrounded;
            if (grounded) timeSinceGrounded = 0f;
            else timeSinceGrounded += dt;

            bool crouchHeld = InputActive && TSInput.CrouchHeld;
            bool crouchPressed = InputActive && TSInput.CrouchPressed;
            bool landed = grounded && !wasGrounded;
            if (landed) landDip = -Mathf.Clamp(-verticalVelocity * 0.014f, 0f, 0.22f);
            wasGrounded = grounded;

            if (slideCooldownTimer > 0f) slideCooldownTimer -= dt;
            if (jumpBufferTimer > 0f) jumpBufferTimer -= dt;
            if (InputActive && TSInput.JumpPressed) jumpBufferTimer = jumpBuffer;

            Vector3 wishDirection = transform.right * input.x + transform.forward * input.y;
            float speed = horizontalVelocity.magnitude;

            // Crouch at speed (or land crouched at speed) to slide.
            if (!IsSliding && grounded && slideCooldownTimer <= 0f && speed >= slideMinSpeed
                && (crouchPressed || (landed && crouchHeld)))
            {
                IsSliding = true;
                horizontalVelocity = horizontalVelocity.normalized * Mathf.Min(speed + slideBoost, Mathf.Max(slideMaxSpeed, speed));
            }

            if (IsSliding && grounded)
            {
                // A slide keeps going by itself, bleeds speed, and can only be steered gently.
                if (wishDirection.sqrMagnitude > 0.01f)
                {
                    horizontalVelocity = Vector3.RotateTowards(horizontalVelocity,
                        wishDirection.normalized * horizontalVelocity.magnitude, slideSteer * Mathf.Deg2Rad * dt, 0f);
                }
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, slideFriction * dt);

                if (!crouchHeld || horizontalVelocity.magnitude < slideEndSpeed) EndSlide();
            }
            else if (grounded)
            {
                bool wantsCrouch = crouchHeld || (IsCrouching && !CanStand());
                IsCrouching = wantsCrouch;

                bool sprinting = InputActive && TSInput.SprintHeld && input.y > 0.1f && MoveSpeedScale >= 0.99f && !IsCrouching;
                float top = (IsCrouching ? crouchSpeed : sprinting ? sprintSpeed : walkSpeed) * MoveSpeedScale * WeaponMoveScale;
                Vector3 wish = wishDirection * top;
                float rate = wishDirection.sqrMagnitude > 0.01f ? groundAcceleration : groundDeceleration;
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, wish, rate * dt);

                if (verticalVelocity <= 0f) jumped = false;
            }
            else if (wishDirection.sqrMagnitude > 0.01f)
            {
                // In the air you keep your speed and can bend its direction.
                float keep = Mathf.Max(speed, walkSpeed * MoveSpeedScale * WeaponMoveScale);
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, wishDirection.normalized * keep, airAcceleration * dt);
            }

            if (grounded && verticalVelocity <= 0f)
            {
                verticalVelocity = -2f; // keeps the controller pressed onto the ground
                jumped = false;
            }

            if (jumpBufferTimer > 0f && !jumped && timeSinceGrounded <= coyoteTime && (IsSliding || !IsCrouching || CanStand()))
            {
                verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
                jumped = true;
                jumpBufferTimer = 0f;
                if (IsSliding) EndSlide(); // slide-jump: the speed carries into the air
                IsCrouching = false;
            }

            verticalVelocity += gravity * dt;

            // Shrink or grow the body. The feet stay where they are.
            bool low = IsSliding || IsCrouching;
            float height = Mathf.MoveTowards(cc.height, low ? crouchHeight : standHeight, 5f * dt);
            if (!Mathf.Approximately(height, cc.height))
            {
                cc.height = height;
                cc.center = new Vector3(0f, height * 0.5f, 0f);
            }

            CollisionFlags flags = cc.Move((horizontalVelocity + Vector3.up * verticalVelocity) * dt);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
            if ((flags & CollisionFlags.Sides) != 0)
            {
                // Running into a wall takes away the speed going into it, so you do not "stick" at full speed.
                Vector3 actual = cc.velocity;
                actual.y = 0f;
                if (actual.sqrMagnitude < horizontalVelocity.sqrMagnitude) horizontalVelocity = actual;
            }
        }

        void EndSlide()
        {
            IsSliding = false;
            IsCrouching = true; // stand up on the next frame if the key is released and there is room
            slideCooldownTimer = slideCooldown;
        }

        bool CanStand()
        {
            float room = standHeight - cc.height;
            if (room <= 0.01f) return true;
            Vector3 top = transform.position + Vector3.up * (cc.height - 0.05f);
            return !Physics.Raycast(top, Vector3.up, room + 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        void UpdateCamera(float dt)
        {
            if (cameraPivot == null) return;

            if (!eyeMeasured)
            {
                eyeBelowTop = standHeight - cameraPivot.localPosition.y; // keep whatever eye height the scene set up
                eyeMeasured = true;
            }

            landDip = Mathf.Lerp(landDip, 0f, 1f - Mathf.Exp(-9f * dt));
            roll = Mathf.Lerp(roll, IsSliding ? slideLean : 0f, 1f - Mathf.Exp(-10f * dt));

            Vector3 p = cameraPivot.localPosition;
            p.y = cc.height - eyeBelowTop + landDip;
            cameraPivot.localPosition = p;
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, roll);
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

        /// <summary>Changes where Respawn (and falling off the map) puts you.</summary>
        public void SetSpawn(Vector3 position, Quaternion rotation)
        {
            spawnPosition = position;
            spawnRotation = rotation;
        }

        public void Respawn()
        {
            // A CharacterController ignores direct position changes unless it is switched off first.
            cc.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            cc.height = standHeight;
            cc.center = new Vector3(0f, standHeight * 0.5f, 0f);
            cc.enabled = true;

            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            spinAccum = 0f;
            pitch = 0f;
            roll = 0f;
            landDip = 0f;
            IsSliding = false;
            IsCrouching = false;
            wasGrounded = true;
        }

        public void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
