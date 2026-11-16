using System.Collections.Generic;
using UnityEngine;

namespace Trickshot
{
    public enum TrainerMode
    {
        Flick,      // three targets, a new one appears for each kill
        Strafe,     // two targets, strafing switched on
        Quickscope  // one target at a time, hardscoped hits score nothing
    }

    public enum TrainerState
    {
        WarmUp,     // targets are up but nothing is recorded
        Countdown,
        Running,
        Results
    }

    /// <summary>A finished round, kept so the HUD can show it.</summary>
    public class RoundResults
    {
        public TrainerMode mode;
        public float seconds;
        public int score;
        public int kills;
        public int headshots;
        public int shotsFired;
        public int shotsHit;
        public int best;
        public bool newBest;
    }

    /// <summary>
    /// Runs the drills: spawns dummies in an arc in front of the firing point, times the round,
    /// and keeps the best score for each mode. Press 1, 2 or 3 to start a round.
    /// Strafing, strafe speed, head size and round length can be changed at any time,
    /// from code or from the ConfigPanel on the range.
    /// </summary>
    public class AimTrainer : MonoBehaviour
    {
        public static AimTrainer Instance { get; private set; }

        [Header("Round")]
        public float roundSeconds = 60f;
        public float countdownSeconds = 3f;

        [Header("Spawning")]
        [Tooltip("Where the player shoots from. Targets appear in an arc in front of this point.")]
        public Vector3 origin = Vector3.zero;
        public Vector3 forward = Vector3.forward;
        [Tooltip("Half-width of the arc, in degrees.")]
        public float arcDegrees = 35f;
        [Tooltip("Closest two targets may spawn to each other, in metres.")]
        public float minSeparation = 2.5f;

        [Header("Distances (metres)")]
        public Vector2 flickDistance = new Vector2(15f, 45f);
        public Vector2 strafeDistance = new Vector2(18f, 38f);
        public Vector2 quickscopeDistance = new Vector2(12f, 30f);

        [Header("Range settings (also on the in-world panel)")]
        [Tooltip("Whether the dummies strafe. Pressing 1, 2 or 3 sets this to the mode's default.")]
        public bool strafing = false;
        [Tooltip("Strafe speed in metres per second. Each dummy varies a little around this.")]
        public float strafeSpeed = 3.5f;
        [Tooltip("Metres either side of its spawn point a dummy will strafe.")]
        public float strafeRange = 4f;
        [Tooltip("Head size as a multiple of the normal 25 cm head.")]
        public float headScale = 1f;

        [Header("Look")]
        public Color bodyColor = new Color(0.85f, 0.42f, 0.05f);
        public Color headColor = new Color(1f, 0.85f, 0.2f);

        public TrainerMode Mode { get; private set; } = TrainerMode.Flick;
        public TrainerState State { get; private set; } = TrainerState.WarmUp;
        public float TimeLeft { get; private set; }
        public float CountdownLeft { get; private set; }
        public RoundResults LastResults { get; private set; }

        readonly List<Target> targets = new List<Target>();
        float activeRoundSeconds;
        Material bodyMaterial;
        Material headMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        void Awake()
        {
            Instance = this;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null)
            {
                bodyMaterial = new Material(lit) { color = bodyColor };
                headMaterial = new Material(lit) { color = headColor };
            }
        }

        void Start()
        {
            ApplyModeRules();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (bodyMaterial != null) Destroy(bodyMaterial);
            if (headMaterial != null) Destroy(headMaterial);
        }

        void Update()
        {
            PlayerController player = PlayerController.Instance;
            if (player != null && player.InputActive)
            {
                int pressed = TSInput.ModePressed;
                if (pressed >= 0) StartRound((TrainerMode)pressed);
            }

            switch (State)
            {
                case TrainerState.WarmUp:
                    FillTargets();
                    break;

                case TrainerState.Countdown:
                    CountdownLeft -= Time.deltaTime;
                    if (CountdownLeft <= 0f) BeginRunning();
                    break;

                case TrainerState.Running:
                    TimeLeft -= Time.deltaTime;
                    if (TimeLeft <= 0f) EndRound();
                    else FillTargets();
                    break;
            }
        }

        public static string ModeName(TrainerMode mode)
        {
            switch (mode)
            {
                case TrainerMode.Strafe: return "STRAFE";
                case TrainerMode.Quickscope: return "QUICKSCOPE";
                default: return "FLICK";
            }
        }

        public int BestScore(TrainerMode mode) => PlayerPrefs.GetInt(BestKey(mode), 0);

        // Best scores are kept per setup, so an easy setup cannot overwrite a hard one.
        string BestKey(TrainerMode mode)
        {
            int seconds = Mathf.RoundToInt(State == TrainerState.Running || State == TrainerState.Results ? activeRoundSeconds : roundSeconds);
            string key = "Trickshot.Best." + mode + "." + seconds + "s.head" + Mathf.RoundToInt(headScale * 100f);
            if (strafing) key += ".strafe" + Mathf.RoundToInt(strafeSpeed * 10f);
            return key;
        }

        /// <summary>Starts a round of the current mode with the current settings.</summary>
        public void StartRound()
        {
            ClearTargets();
            ApplyModeRules();
            CountdownLeft = countdownSeconds;
            State = TrainerState.Countdown;
        }

        public void SetStrafing(bool on)
        {
            strafing = on;
            foreach (Target t in targets) ApplyStrafe(t);
        }

        public void SetStrafeSpeed(float metresPerSecond)
        {
            strafeSpeed = Mathf.Max(0f, metresPerSecond);
            foreach (Target t in targets) ApplyStrafe(t);
        }

        public void SetHeadScale(float scale)
        {
            headScale = Mathf.Max(0.1f, scale);
            foreach (Target t in targets)
            {
                if (t != null) t.SetHeadScale(headScale);
            }
        }

        void ApplyStrafe(Target t)
        {
            if (t == null) return;
            t.strafeRange = strafeRange;
            t.strafeSpeed = strafing ? strafeSpeed * t.speedVariance : 0f;
        }

        /// <summary>Clears the range and starts the countdown for a round of the given mode.</summary>
        public void StartRound(TrainerMode mode)
        {
            Mode = mode;
            strafing = mode == TrainerMode.Strafe;
            StartRound();
        }

        void BeginRunning()
        {
            if (ScoreManager.Instance != null) ScoreManager.Instance.ResetScore();
            activeRoundSeconds = Mathf.Max(1f, roundSeconds); // fixed for this round, whatever the slider does next
            TimeLeft = activeRoundSeconds;
            State = TrainerState.Running;
            FillTargets();
        }

        void EndRound()
        {
            TimeLeft = 0f;
            ScoreManager score = ScoreManager.Instance;

            var results = new RoundResults { mode = Mode, seconds = activeRoundSeconds };
            if (score != null)
            {
                results.score = score.Score;
                results.kills = score.Kills;
                results.headshots = score.Headshots;
                results.shotsFired = score.ShotsFired;
                results.shotsHit = score.ShotsHit;
            }

            int previousBest = BestScore(Mode);
            results.newBest = results.score > previousBest;
            results.best = Mathf.Max(previousBest, results.score);
            if (results.newBest)
            {
                PlayerPrefs.SetInt(BestKey(Mode), results.score);
                PlayerPrefs.Save();
            }

            LastResults = results;
            ClearTargets();
            State = TrainerState.Results;
        }

        void ApplyModeRules()
        {
            if (ScoreManager.Instance != null) ScoreManager.Instance.quickscopeOnly = Mode == TrainerMode.Quickscope;
        }

        int WantedTargets()
        {
            switch (Mode)
            {
                case TrainerMode.Strafe: return 2;
                case TrainerMode.Quickscope: return 1;
                default: return 3;
            }
        }

        Vector2 DistanceRange()
        {
            switch (Mode)
            {
                case TrainerMode.Strafe: return strafeDistance;
                case TrainerMode.Quickscope: return quickscopeDistance;
                default: return flickDistance;
            }
        }

        void FillTargets()
        {
            int wanted = WantedTargets();
            while (targets.Count < wanted) Spawn();
        }

        void Spawn()
        {
            Vector3 flatForward = new Vector3(forward.x, 0f, forward.z);
            if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
            flatForward.Normalize();

            Vector2 range = DistanceRange();
            Vector3 position = origin;
            Vector3 toTarget = flatForward;

            // A few tries to find a spot that is not on top of another target.
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float angle = Random.Range(-arcDegrees, arcDegrees);
                float distance = Random.Range(range.x, range.y);
                toTarget = Quaternion.Euler(0f, angle, 0f) * flatForward;
                position = origin + toTarget * distance;
                position.y = origin.y;
                if (IsClear(position)) break;
            }

            Target target = Target.Build(position, origin, bodyMaterial, headMaterial);
            target.transform.SetParent(transform, true);

            if (bodyMaterial == null)
            {
                // No URP shader found: tint whatever material the primitives came with.
                foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
                    r.material.color = r.GetComponent<HitZone>().zone == HitZoneType.Head ? headColor : bodyColor;
            }

            target.strafeAxis = Vector3.Cross(Vector3.up, toTarget); // sideways as seen from the firing point
            target.speedVariance = Random.Range(0.8f, 1.2f);
            target.SetHeadScale(headScale);
            ApplyStrafe(target);

            target.Killed += OnTargetKilled;
            targets.Add(target);
        }

        bool IsClear(Vector3 position)
        {
            float min = minSeparation * minSeparation;
            foreach (Target t in targets)
            {
                if (t != null && (t.transform.position - position).sqrMagnitude < min) return false;
            }
            return true;
        }

        void OnTargetKilled(Target target)
        {
            targets.Remove(target);
            Destroy(target.gameObject);
        }

        void ClearTargets()
        {
            foreach (Target t in targets)
            {
                if (t != null) Destroy(t.gameObject);
            }
            targets.Clear();
        }
    }
}
