using System.Collections.Generic;
using UnityEngine;

namespace Trickshot
{
    public enum MatchState
    {
        Live,
        RoundOver,
        MatchOver
    }

    /// <summary>
    /// TEMPLATE. Shared scaffolding for the main game modes: two team scores, a clock, a match
    /// state, and a simple score bar. Until there are real opponents, the enemy team is practice
    /// dummies on the training range. They do not shoot back.
    ///
    /// To build the real mode, replace SpawnEnemy with real players or bots and keep the rules
    /// in the subclass (OnBegin, Tick, OnEnemyKilled).
    /// </summary>
    public abstract class MatchMode : MonoBehaviour
    {
        /// <summary>The map this mode is played on. Null means the training range.</summary>
        public Arena arena;

        [Header("Used when there is no arena: the training range")]
        public Vector3 origin = Vector3.zero;
        public Vector3 forward = Vector3.forward;
        public float arcDegrees = 35f;
        public Vector2 enemyDistance = new Vector2(15f, 45f);
        [Tooltip("Strafe speed of the stand-in enemies. 0 = they stand still.")]
        public float enemyStrafeSpeed = 3f;
        [Tooltip("The enemy side. It decides the colour of the paint they leave when they die.")]
        public Team enemyTeam = Team.Red;
        [Tooltip("Credits paid for winning a match.")]
        public int winCredits = 50;

        public abstract string DisplayName { get; }
        public abstract string Summary { get; }
        public abstract string[] Rules { get; }

        public int FriendlyScore { get; protected set; }
        public int EnemyScore { get; protected set; }
        public MatchState State { get; protected set; }
        public float TimeLeft { get; protected set; }
        /// <summary>Large text across the screen, such as ROUND WON. Null for none.</summary>
        public string Banner { get; protected set; }

        protected readonly List<Target> enemies = new List<Target>();

        static readonly Color EnemyBody = new Color(0.62f, 0.12f, 0.14f);
        static readonly Color EnemyHead = new Color(1f, 0.45f, 0.4f);
        static readonly Color Tangy = new Color(1f, 0.62f, 0.1f);

        Material bodyMaterial;
        Material headMaterial;
        GUIStyle style;

        protected virtual void Awake()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null)
            {
                bodyMaterial = new Material(lit) { color = EnemyBody };
                headMaterial = new Material(lit) { color = EnemyHead };
            }
            enabled = false; // the menu switches the chosen mode on
        }

        void OnDisable()
        {
            ClearEnemies();
            Banner = null;
        }

        void OnDestroy()
        {
            if (bodyMaterial != null) Destroy(bodyMaterial);
            if (headMaterial != null) Destroy(headMaterial);
        }

        /// <summary>Starts a fresh match.</summary>
        public void Begin()
        {
            ClearEnemies();
            FriendlyScore = 0;
            EnemyScore = 0;
            Banner = null;
            State = MatchState.Live;

            ScoreManager score = ScoreManager.Instance;
            if (score != null)
            {
                score.quickscopeOnly = false;
                score.ResetScore();
            }

            OnBegin();
        }

        void Update()
        {
            if (State == MatchState.MatchOver)
            {
                if (TSInput.ConfirmPressed) Begin();
                return;
            }
            Tick(Time.deltaTime);
        }

        protected abstract void OnBegin();
        protected abstract void Tick(float dt);
        protected abstract void OnEnemyKilled();

        /// <summary>Extra line under the clock, such as enemies left.</summary>
        protected virtual string StatusLine => null;

        protected void EndMatch(bool won)
        {
            ClearEnemies();
            State = MatchState.MatchOver;
            Banner = won ? "VICTORY" : "DEFEAT";
            if (won) Profile.AddCredits(winCredits);
        }

        protected Target SpawnEnemy()
        {
            Vector3 flatForward = new Vector3(forward.x, 0f, forward.z);
            if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
            flatForward.Normalize();

            Vector3 position = origin;
            Vector3 toEnemy = flatForward;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                toEnemy = Quaternion.Euler(0f, Random.Range(-arcDegrees, arcDegrees), 0f) * flatForward;
                position = origin + toEnemy * Random.Range(enemyDistance.x, enemyDistance.y);
                position.y = origin.y;
                if (IsClear(position)) break;
            }

            Vector3 lookAt = origin;
            if (arena != null && arena.enemySpawns != null && arena.enemySpawns.Length > 0)
            {
                // On a real map enemies appear at its spawn points and face the player.
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    position = arena.enemySpawns[Random.Range(0, arena.enemySpawns.Length)];
                    if (IsClear(position)) break;
                }
                PlayerController player = PlayerController.Instance;
                lookAt = player != null ? player.transform.position : arena.playerSpawn;
                toEnemy = position - lookAt;
                toEnemy.y = 0f;
                toEnemy = toEnemy.sqrMagnitude > 0.01f ? toEnemy.normalized : Vector3.forward;
            }

            Target enemy = Target.Build(position, lookAt, bodyMaterial, headMaterial);
            enemy.name = "Enemy (placeholder)";
            enemy.team = enemyTeam;
            enemy.transform.SetParent(transform, true);
            enemy.strafeAxis = Vector3.Cross(Vector3.up, toEnemy);
            enemy.strafeSpeed = enemyStrafeSpeed * Random.Range(0.8f, 1.2f);
            enemy.Killed += HandleKilled;
            enemies.Add(enemy);
            return enemy;
        }

        bool IsClear(Vector3 position)
        {
            foreach (Target t in enemies)
            {
                if (t != null && (t.transform.position - position).sqrMagnitude < 9f) return false;
            }
            return true;
        }

        void HandleKilled(Target enemy)
        {
            enemies.Remove(enemy);
            Destroy(enemy.gameObject);
            if (State == MatchState.Live) OnEnemyKilled();
        }

        protected void ClearEnemies()
        {
            foreach (Target t in enemies)
            {
                if (t != null) Destroy(t.gameObject);
            }
            enemies.Clear();
        }

        // Score bar at the top of the screen and the banner across the middle.
        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;

            float w = Screen.width;
            float h = Screen.height;
            float ui = h / 1080f;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow };
                style.normal.textColor = Color.white;
            }

            Text(new Rect(0f, 14f * ui, w, 30f * ui), DisplayName + "   (TEMPLATE)", 20f * ui, new Color(1f, 1f, 1f, 0.7f));
            Text(new Rect(0f, 40f * ui, w, 60f * ui), "YOU  " + FriendlyScore + "    -    " + EnemyScore + "  ENEMY", 46f * ui, Color.white);

            int seconds = Mathf.CeilToInt(Mathf.Max(0f, TimeLeft));
            Text(new Rect(0f, 98f * ui, w, 34f * ui), (seconds / 60) + ":" + (seconds % 60).ToString("00"), 26f * ui, Tangy);

            string status = StatusLine;
            if (!string.IsNullOrEmpty(status))
                Text(new Rect(0f, 130f * ui, w, 30f * ui), status, 20f * ui, new Color(1f, 1f, 1f, 0.7f));

            if (!string.IsNullOrEmpty(Banner))
            {
                Text(new Rect(0f, h * 0.3f, w, 90f * ui), Banner, 72f * ui, Tangy);
                if (State == MatchState.MatchOver)
                    Text(new Rect(0f, h * 0.3f + 92f * ui, w, 34f * ui), "ENTER  play again        M  menu", 24f * ui, Color.white);
            }
        }

        void Text(Rect r, string text, float size, Color color)
        {
            style.fontSize = Mathf.Max(9, Mathf.RoundToInt(size));
            style.alignment = TextAnchor.UpperCenter;
            GUI.color = new Color(0f, 0f, 0f, color.a * 0.85f);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);
            GUI.color = color;
            GUI.Label(r, text, style);
            GUI.color = Color.white;
        }
    }
}
