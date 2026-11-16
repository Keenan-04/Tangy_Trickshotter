using UnityEngine;

namespace Trickshot
{
    /// <summary>
    /// A person-sized practice dummy: legs, torso and a head, each with its own HitZone.
    /// Head and torso hits kill in one shot, leg hits take two. It can strafe like a player.
    /// </summary>
    public class Target : MonoBehaviour
    {
        // Proportions of a 1.8 m person. The head is a 25 cm sphere, which is the part to practise on.
        public const float HeadDiameter = 0.25f;
        public const float HeadCentreHeight = 1.64f;

        [Tooltip("Points for a kill, before multipliers.")]
        public int killPoints = 100;
        [Tooltip("Points for a hit that does not kill, before multipliers.")]
        public int hitPoints = 25;
        public float maxHealth = 100f;
        [Tooltip("Damage of a leg shot. Head and torso shots always kill.")]
        public float lowerDamage = 50f;

        [Header("Strafing (0 speed = stands still)")]
        public float strafeSpeed = 0f;
        [Tooltip("Metres either side of the spawn point.")]
        public float strafeRange = 4f;
        public Vector3 strafeAxis = Vector3.right;
        [Tooltip("Set by the trainer so dummies do not all move at exactly the same speed.")]
        public float speedVariance = 1f;

        public bool IsAlive { get; private set; } = true;
        public float Health { get; private set; }

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
            Killed?.Invoke(this);
            return true;
        }

        /// <summary>Builds a dummy out of primitives, standing at feetPosition and facing lookAt.</summary>
        public static Target Build(Vector3 feetPosition, Vector3 lookAt, Material bodyMaterial, Material headMaterial)
        {
            var root = new GameObject("Dummy");
            root.transform.position = feetPosition;
            Vector3 facing = lookAt - feetPosition;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.001f) root.transform.rotation = Quaternion.LookRotation(facing);

            var target = root.AddComponent<Target>();

            Part(target, PrimitiveType.Cube, "Legs", HitZoneType.Lower,
                new Vector3(0f, 0.45f, 0f), new Vector3(0.42f, 0.9f, 0.26f), bodyMaterial);
            Part(target, PrimitiveType.Cube, "Torso", HitZoneType.Upper,
                new Vector3(0f, 1.2f, 0f), new Vector3(0.55f, 0.6f, 0.28f), bodyMaterial);
            target.head = Part(target, PrimitiveType.Sphere, "Head", HitZoneType.Head,
                new Vector3(0f, HeadCentreHeight, 0f), Vector3.one * HeadDiameter, headMaterial);

            return target;
        }

        static Transform Part(Target target, PrimitiveType shape, string name, HitZoneType zone,
            Vector3 localPosition, Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.SetParent(target.transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;

            Renderer r = go.GetComponent<Renderer>();
            if (r != null && material != null) r.sharedMaterial = material;

            var hitZone = go.AddComponent<HitZone>();
            hitZone.target = target;
            hitZone.zone = zone;
            return go.transform;
        }
    }
}
