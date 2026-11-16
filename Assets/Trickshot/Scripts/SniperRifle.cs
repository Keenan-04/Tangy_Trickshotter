using UnityEngine;

namespace Trickshot
{
    /// <summary>
    /// Bolt-action hitscan sniper. Hold right mouse to scope, left mouse to fire.
    /// Tracks how long the scope has been up and hands every shot to the ScoreManager.
    /// </summary>
    public class SniperRifle : MonoBehaviour
    {
        [Header("References")]
        public Camera cam;
        public PlayerController player;
        [Tooltip("Optional gun model, hidden while the scope overlay is up.")]
        public GameObject viewModel;
        [Tooltip("Optional point the tracer starts from.")]
        public Transform muzzle;

        [Header("Scope")]
        public float hipFov = 80f;
        public float scopedFov = 15f;
        [Tooltip("Seconds for the scope to come fully up. Shots are only pinpoint once it has.")]
        public float adsTime = 0.22f;
        [Range(0.05f, 1f)] public float scopedSensitivity = 0.25f;

        [Header("Shooting")]
        [Tooltip("Seconds between shots.")]
        public float boltTime = 0.8f;
        public float range = 600f;
        [Tooltip("Random cone (degrees) on no-scopes. This is the price of the no-scope multiplier.")]
        public float hipSpreadDegrees = 2f;
        public float recoilKick = 1.5f;
        public LayerMask hitMask = Physics.DefaultRaycastLayers;

        public bool IsAiming { get; private set; }
        /// <summary>0 = at the hip, 1 = fully scoped.</summary>
        public float AdsProgress { get; private set; }
        /// <summary>Seconds since the scope started coming up. Keeps running until it is fully back down.</summary>
        public float ScopeTime { get; private set; }
        /// <summary>A shot fired now would be scored as a scoped shot.</summary>
        public bool IsScoped => AdsProgress > 0.01f;
        public bool ShowScopeOverlay => AdsProgress >= 0.85f;
        public float BoltProgress => boltTime <= 0f ? 1f : 1f - Mathf.Clamp01(cooldown / boltTime);

        float cooldown;
        LineRenderer tracer;
        float tracerTimer;

        void Start()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
            if (player == null) player = GetComponentInParent<PlayerController>();
            if (cam != null) cam.fieldOfView = hipFov;
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
            cam.fieldOfView = Mathf.Lerp(hipFov, scopedFov, eased);
            player.SensitivityScale = Mathf.Lerp(1f, scopedSensitivity, eased);
            if (viewModel != null) viewModel.SetActive(!ShowScopeOverlay);

            if (cooldown > 0f) cooldown -= dt;
            if (player.InputActive && TSInput.FirePressed && cooldown <= 0f) Fire();

            if (tracer != null && tracer.enabled)
            {
                tracerTimer -= dt;
                if (tracerTimer <= 0f) tracer.enabled = false;
            }
        }

        void Fire()
        {
            cooldown = boltTime;

            bool scoped = IsScoped;

            // Pinpoint only when fully scoped. Half-raised scopes are partly accurate, and letting
            // go of the scope right before firing gives you hip-fire spread with scoped scoring.
            float spread = IsAiming ? hipSpreadDegrees * (1f - AdsProgress) : hipSpreadDegrees;

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
            Target hitTarget = null;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                distance = hit.distance;
                hitTarget = hit.collider.GetComponentInParent<Target>();
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
            if (hitTarget != null && hitTarget.IsAlive)
            {
                if (score != null) score.RegisterHit(shot, hitTarget.basePoints);
                hitTarget.Hit();
            }
            else if (score != null)
            {
                score.RegisterMiss(shot);
            }

            player.ConsumeSpin();
            player.AddRecoil(recoilKick);
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
