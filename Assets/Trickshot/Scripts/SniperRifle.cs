using UnityEngine;

namespace Trickshot
{
    /// <summary>
    /// Bolt-action hitscan sniper. Hold right mouse to scope, left mouse to fire, Shift to hold your breath.
    ///
    /// The scope behaves the way classic console-shooter snipers do:
    ///   - the rifle swings up to the eye over adsTime and the black scope picture only snaps in at the end;
    ///   - a shot is pinpoint the moment the scope is fully up and has full hip spread before that,
    ///     which is what makes quickscoping a timing skill;
    ///   - the scope drifts in a slow figure of eight until you hold your breath;
    ///   - recoil throws the scope up and it settles back by itself while the bolt cycles.
    /// </summary>
    public class SniperRifle : MonoBehaviour
    {
        [Header("References")]
        public Camera cam;
        public PlayerController player;
        [Tooltip("Optional gun model. It swings to the eye while scoping and hides once the scope picture is up.")]
        public GameObject viewModel;
        [Tooltip("Optional point the tracer starts from.")]
        public Transform muzzle;

        [Header("Scope")]
        public float hipFov = 80f;
        public float scopedFov = 15f;
        [Tooltip("Seconds for the scope to come fully up. Shots are only pinpoint once it has.")]
        public float adsTime = 0.38f;
        [Tooltip("How far through the scope-in the black scope picture appears (0-1).")]
        [Range(0.5f, 1f)] public float overlayAt = 0.85f;
        [Tooltip("On: mouse speed follows the zoom, so a flick covers the same distance on screen scoped or not.")]
        public bool fovScaledSensitivity = true;
        [Tooltip("Mouse speed while scoped. Only used when Fov Scaled Sensitivity is off.")]
        [Range(0.05f, 1f)] public float scopedSensitivity = 0.25f;
        [Tooltip("Movement speed while scoped, as a fraction of normal.")]
        [Range(0.1f, 1f)] public float adsMoveScale = 0.4f;
        [Tooltip("Where the gun model moves to while scoping, relative to its hip position.")]
        public Vector3 viewModelAdsOffset = new Vector3(-0.2f, 0.105f, -0.12f);

        [Header("Sway and breath")]
        [Tooltip("How far the scope drifts, in degrees. 0 turns sway off.")]
        public float swayDegrees = 0.25f;
        public float swaySpeed = 1.6f;
        [Tooltip("Seconds you can hold your breath.")]
        public float holdBreathTime = 4f;
        [Tooltip("Seconds of breath regained per second.")]
        public float breathRecovery = 0.6f;
        [Tooltip("Sway while holding your breath, as a fraction of normal.")]
        public float steadiedSway = 0.08f;
        [Tooltip("Sway after running out of breath, as a multiple of normal.")]
        public float exhaustedSway = 1.8f;

        [Header("Shooting")]
        [Tooltip("Seconds between shots.")]
        public float boltTime = 1f;
        public float range = 600f;
        [Tooltip("Random cone (degrees) on any shot fired before the scope is fully up.")]
        public float hipSpreadDegrees = 5f;
        [Tooltip("Degrees the scope jumps on a shot.")]
        public float recoilKick = 2.2f;
        [Tooltip("How quickly the scope settles back after a shot. Higher is faster.")]
        public float recoilRecovery = 7f;
        public LayerMask hitMask = Physics.DefaultRaycastLayers;

        public bool IsAiming { get; private set; }
        /// <summary>0 = at the hip, 1 = fully scoped.</summary>
        public float AdsProgress { get; private set; }
        /// <summary>Seconds since the scope started coming up. Keeps running until it is fully back down.</summary>
        public float ScopeTime { get; private set; }
        /// <summary>A shot fired now would be scored as a scoped shot.</summary>
        public bool IsScoped => AdsProgress > 0.01f;
        /// <summary>A shot fired now would be pinpoint.</summary>
        public bool IsFullyScoped => AdsProgress >= 1f;
        public bool ShowScopeOverlay => AdsProgress >= overlayAt;
        public float ScopeOverlayAlpha => Mathf.InverseLerp(overlayAt, 1f, AdsProgress);
        public float BoltProgress => boltTime <= 0f ? 1f : 1f - Mathf.Clamp01(cooldown / boltTime);
        public float Breath01 => holdBreathTime <= 0f ? 0f : Mathf.Clamp01(breath / holdBreathTime);
        public bool IsHoldingBreath { get; private set; }
        public bool IsExhausted { get; private set; }

        float cooldown;
        float kick;
        float breath;
        float swayScale = 1f;
        Vector3 viewModelHipPosition;
        LineRenderer tracer;
        float tracerTimer;

        void Start()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
            if (player == null) player = GetComponentInParent<PlayerController>();
            if (cam != null) cam.fieldOfView = hipFov;
            if (viewModel != null) viewModelHipPosition = viewModel.transform.localPosition;
            breath = holdBreathTime;
            BuildTracer();
        }

        void Update()
        {
            if (cam == null || player == null) return;
            float dt = Time.deltaTime;

            IsAiming = player.InputActive && TSInput.AimHeld;

            float target = IsAiming ? 1f : 0f;
            AdsProgress = Mathf.MoveTowards(AdsProgress, target, dt / Mathf.Max(adsTime, 0.01f));

            // The timer only resets once the scope is all the way down, so tapping
            // right mouse cannot be used to refresh a hardscope into a quickscope.
            ScopeTime = AdsProgress > 0f ? ScopeTime + dt : 0f;

            float eased = Mathf.SmoothStep(0f, 1f, AdsProgress);
            float fov = Mathf.Lerp(hipFov, scopedFov, eased);
            cam.fieldOfView = fov;

            player.SensitivityScale = fovScaledSensitivity
                ? Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(hipFov * 0.5f * Mathf.Deg2Rad)
                : Mathf.Lerp(1f, scopedSensitivity, eased);
            player.MoveSpeedScale = Mathf.Lerp(1f, adsMoveScale, eased);

            if (viewModel != null)
            {
                viewModel.transform.localPosition = viewModelHipPosition + viewModelAdsOffset * eased;
                viewModel.SetActive(!ShowScopeOverlay);
            }

            UpdateBreath(dt);

            // Sway and recoil turn the camera itself, so the bullet always goes where the crosshair is.
            kick = Mathf.Lerp(kick, 0f, 1f - Mathf.Exp(-recoilRecovery * dt));
            float amplitude = swayDegrees * swayScale * eased;
            float t = Time.time * swaySpeed;
            float swayX = Mathf.Sin(t) * amplitude;
            float swayY = Mathf.Sin(t * 2f) * 0.5f * amplitude;
            cam.transform.localRotation = Quaternion.Euler(-kick - swayY, swayX, 0f);

            if (cooldown > 0f) cooldown -= dt;
            // Clicking the settings panel works its controls instead of firing.
            if (player.InputActive && TSInput.FirePressed && cooldown <= 0f && !ConfigPanel.IsPointedAt) Fire();

            if (tracer != null && tracer.enabled)
            {
                tracerTimer -= dt;
                if (tracerTimer <= 0f) tracer.enabled = false;
            }
        }

        void UpdateBreath(float dt)
        {
            bool wantsHold = IsAiming && IsFullyScoped && TSInput.HoldBreathHeld;

            if (wantsHold && !IsExhausted && breath > 0f)
            {
                IsHoldingBreath = true;
                breath -= dt;
                if (breath <= 0f)
                {
                    breath = 0f;
                    IsExhausted = true;
                }
            }
            else
            {
                IsHoldingBreath = false;
                breath = Mathf.Min(holdBreathTime, breath + breathRecovery * dt);
                if (IsExhausted && breath >= holdBreathTime * 0.5f) IsExhausted = false;
            }

            float wanted = IsHoldingBreath ? steadiedSway : IsExhausted ? exhaustedSway : 1f;
            swayScale = Mathf.MoveTowards(swayScale, wanted, 4f * dt);
        }

        void Fire()
        {
            cooldown = boltTime;

            bool scoped = IsScoped;

            // Pinpoint only once the scope is fully up. Anything earlier, including letting go
            // of the scope just before firing, gets the full hip-fire cone.
            float spread = IsFullyScoped ? 0f : hipSpreadDegrees;

            Transform ct = cam.transform;
            Vector3 dir = ct.forward;
            if (spread > 0.001f)
            {
                Vector2 off = Random.insideUnitCircle * spread;
                dir = ct.rotation * Quaternion.Euler(off.y, off.x, 0f) * Vector3.forward;
            }

            Vector3 origin = ct.position;
            Vector3 end = origin + dir * range;
            float distance = range;
            HitZone hitZone = null;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                distance = hit.distance;
                hitZone = hit.collider.GetComponent<HitZone>();
            }

            var shot = new ShotInfo
            {
                scoped = scoped,
                scopeTime = ScopeTime,
                spinDegrees = player.SpinDegrees,
                airborne = player.IsAirborne,
                distance = distance
            };

            ScoreManager score = ScoreManager.Instance;
            Target hitTarget = hitZone != null ? hitZone.target : null;
            if (hitTarget != null && hitTarget.IsAlive)
            {
                shot.zone = hitZone.zone;
                shot.killed = hitTarget.TakeHit(hitZone.zone);
                if (score != null) score.RegisterHit(shot, shot.killed ? hitTarget.killPoints : hitTarget.hitPoints);
            }
            else if (score != null)
            {
                score.RegisterMiss(shot);
            }

            player.ConsumeSpin();
            kick += recoilKick;
            ShowTracer(end);
        }

        void BuildTracer()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return; // no tracer, everything else still works

            var go = new GameObject("Tracer");
            tracer = go.AddComponent<LineRenderer>();
            tracer.useWorldSpace = true;
            tracer.positionCount = 2;
            tracer.startWidth = 0.03f;
            tracer.endWidth = 0.03f;
            tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracer.receiveShadows = false;
            var mat = new Material(shader);
            mat.color = new Color(1f, 0.85f, 0.4f);
            tracer.material = mat;
            tracer.enabled = false;
        }

        void ShowTracer(Vector3 end)
        {
            if (tracer == null) return;
            Vector3 start;
            if (muzzle != null && !ShowScopeOverlay) start = muzzle.position;
            else start = cam.transform.position + cam.transform.forward * 0.5f - cam.transform.up * 0.08f;

            tracer.SetPosition(0, start);
            tracer.SetPosition(1, end);
            tracer.enabled = true;
            tracerTimer = 0.07f;
        }

        void OnDestroy()
        {
            if (tracer != null) Destroy(tracer.gameObject);
        }
    }
}
