using UnityEngine;

namespace Trickshot
{
    /// <summary>
    /// A person-sized thick stick figure to shoot at: a ball head, a body and arms, and legs, each
    /// part with its own HitZone. Head and body hits kill in one shot, leg hits take two.
    /// It can strafe like a player, and bursts into its team's paint colour when it dies.
    /// </summary>
    public class Target : MonoBehaviour
    {
        // The head is a 25 cm ball, which is the part to practise on.
        public const float HeadDiameter = StickFigure.HeadDiameter;
        public const float HeadCentreHeight = StickFigure.HeadCentreHeight;

        [Tooltip("Points for a kill, before multipliers.")]
        public int killPoints = 100;
        [Tooltip("Points for a hit that does not kill, before multipliers.")]
        public int hitPoints = 25;
        public float maxHealth = 100f;
        [Tooltip("Damage of a leg shot. Head and body shots always kill.")]
        public float lowerDamage = 50f;

        [Header("Team")]
        [Tooltip("Decides the colour of the paint it leaves: red, blue, or orange for training dummies.")]
        public Team team = Team.Neutral;
        [Tooltip("Blobs of paint thrown when it dies.")]
        public int deathPaint = 30;

        [Header("Strafing (0 speed = stands still)")]
        public float strafeSpeed = 0f;
        [Tooltip("Metres either side of the spawn point.")]
        public float strafeRange = 4f;
        public Vector3 strafeAxis = Vector3.right;
        [Tooltip("Set by the trainer so dummies do not all move at exactly the same speed.")]
        public float speedVariance = 1f;

        public bool IsAlive { get; private set; } = true;
        public float Health { get; private set; }
        public Color PaintColor => PaintSplash.ColorOf(team);

        /// <summary>Raised once, on the shot that kills it.</summary>
        public event System.Action<Target> Killed;

        Vector3 startPosition;
        float offset;
        float direction = 1f;
        float flipTimer;
        Transform head;

        void Awake()
        {
            Health = maxHealth;
        }

        void Start()
        {
            startPosition = transform.position;
            direction = Random.value < 0.5f ? -1f : 1f;
            flipTimer = Random.Range(0.4f, 1.4f);
        }

        void Update()
        {
            if (!IsAlive || strafeSpeed <= 0f) return;

            // Change direction at random, like someone trying not to get shot.
            flipTimer -= Time.deltaTime;
            offset += direction * strafeSpeed * Time.deltaTime;
            if (Mathf.Abs(offset) >= strafeRange)
            {
                offset = Mathf.Clamp(offset, -strafeRange, strafeRange);
                direction = -Mathf.Sign(offset);
                flipTimer = Random.Range(0.4f, 1.4f);
            }
            else if (flipTimer <= 0f)
            {
                direction = -direction;
                flipTimer = Random.Range(0.4f, 1.4f);
            }

            Vector3 axis = strafeAxis.sqrMagnitude > 0.0001f ? strafeAxis.normalized : Vector3.right;
            transform.position = startPosition + axis * offset;
        }

        /// <summary>Resizes the head (and its hitbox). 1 = the normal 25 cm head. It grows upwards from the neck.</summary>
        public void SetHeadScale(float scale)
        {
            if (head == null) return;
            float diameter = HeadDiameter * Mathf.Max(0.1f, scale);
            float neckHeight = HeadCentreHeight - HeadDiameter * 0.5f;
            head.localScale = Vector3.one * diameter;
            head.localPosition = new Vector3(0f, neckHeight + diameter * 0.5f, 0f);
        }

        /// <summary>Applies one bullet. Returns true if it killed the target.</summary>
        public bool TakeHit(HitZoneType zone)
        {
            if (!IsAlive) return false;

            Health -= zone == HitZoneType.Lower ? lowerDamage : maxHealth;
            if (Health > 0f) return false;

            IsAlive = false;
            PaintSplash.Burst(transform.position + Vector3.up * 1.15f, PaintColor, deathPaint, 6.5f);
            Killed?.Invoke(this);
            return true;
        }

        /// <summary>Builds a stick figure standing at feetPosition and facing lookAt.</summary>
        public static Target Build(Vector3 feetPosition, Vector3 lookAt, Material bodyMaterial, Material headMaterial)
        {
            var root = new GameObject("Dummy");
            root.transform.position = feetPosition;
            Vector3 facing = lookAt - feetPosition;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.001f) root.transform.rotation = Quaternion.LookRotation(facing);

            var target = root.AddComponent<Target>();
            Transform t = root.transform;
            float sw = StickFigure.ShoulderHalfWidth;
            float hw = StickFigure.HipHalfWidth;

            target.head = Zone(target, HitZoneType.Head,
                StickFigure.Ball(t, "Head", new Vector3(0f, HeadCentreHeight, 0f), HeadDiameter, headMaterial, true)).transform;

            // Body and arms count as the upper body.
            Zone(target, HitZoneType.Upper, StickFigure.Limb(t, "Body", StickFigure.Hip, StickFigure.Chest, StickFigure.BodyThickness, bodyMaterial, true));
            Zone(target, HitZoneType.Upper, StickFigure.Limb(t, "Shoulders", new Vector3(-sw, 1.42f, 0f), new Vector3(sw, 1.42f, 0f), 0.12f, bodyMaterial, true));
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 shoulder = new Vector3(sw * side, 1.42f, 0f);
                Vector3 elbow = new Vector3(0.3f * side, 1.12f, 0.02f);
                Vector3 hand = new Vector3(0.34f * side, 0.86f, 0.06f);
                Zone(target, HitZoneType.Upper, StickFigure.Limb(t, "Upper Arm", shoulder, elbow, StickFigure.ArmThickness, bodyMaterial, true));
                Zone(target, HitZoneType.Upper, StickFigure.Limb(t, "Forearm", elbow, hand, StickFigure.ArmThickness, bodyMaterial, true));
            }

            Zone(target, HitZoneType.Lower, StickFigure.Limb(t, "Hips", new Vector3(-hw, 0.93f, 0f), new Vector3(hw, 0.93f, 0f), StickFigure.LegThickness, bodyMaterial, true));
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 hip = new Vector3(hw * side, 0.93f, 0f);
                Vector3 knee = new Vector3(0.13f * side, 0.5f, 0.02f);
                Vector3 foot = new Vector3(0.16f * side, 0.07f, 0f);
                Zone(target, HitZoneType.Lower, StickFigure.Limb(t, "Thigh", hip, knee, StickFigure.LegThickness, bodyMaterial, true));
                Zone(target, HitZoneType.Lower, StickFigure.Limb(t, "Shin", knee, foot, StickFigure.LegThickness, bodyMaterial, true));
            }

            return target;
        }

        static GameObject Zone(Target target, HitZoneType zone, GameObject part)
        {
            var hitZone = part.AddComponent<HitZone>();
            hitZone.target = target;
            hitZone.zone = zone;
            return part;
        }
    }
}
