using System.Collections.Generic;
using UnityEngine;

namespace Trickshot
{
    /// <summary>
    /// TEMPLATE. Team Deathmatch: everyone respawns, each kill is a point for the killer's team,
    /// and the first team to the score limit wins. If the clock runs out, the team ahead wins.
    /// The enemy team is stand-in dummies that cannot score yet.
    /// </summary>
    public class TeamDeathmatchMode : MatchMode
    {
        [Header("Rules")]
        public int scoreLimit = 30;
        [Tooltip("Match length in seconds.")]
        public float timeLimit = 300f;
        [Tooltip("Enemies on the field at once.")]
        public int enemiesAlive = 5;
        public float respawnDelay = 2f;

        readonly List<float> respawnTimers = new List<float>();

        public override string DisplayName => "TEAM DEATHMATCH";
        public override string Summary => "Two teams, respawns on, first to the score limit.";
        public override string[] Rules => new[]
        {
            arena != null ? "Map: " + arena.name + ", a skyscraper rooftop" : "Map: training range",
            "First team to " + scoreLimit + " kills wins",
            Mathf.RoundToInt(timeLimit / 60f) + " minute time limit; the team ahead wins on time",
            "Respawn " + respawnDelay.ToString("0.#") + " s after a death",
            "Template: enemies are dummies and do not shoot back"
        };

        protected override void OnBegin()
        {
            respawnTimers.Clear();
            TimeLeft = timeLimit;
            for (int i = 0; i < enemiesAlive; i++) SpawnEnemy();
        }

        protected override void Tick(float dt)
        {
            TimeLeft -= dt;
            if (TimeLeft <= 0f)
            {
                EndMatch(FriendlyScore > EnemyScore);
                return;
            }

            for (int i = respawnTimers.Count - 1; i >= 0; i--)
            {
                respawnTimers[i] -= dt;
                if (respawnTimers[i] > 0f) continue;
                respawnTimers.RemoveAt(i);
                SpawnEnemy();
            }
        }

        protected override void OnEnemyKilled()
        {
            FriendlyScore++;
            if (FriendlyScore >= scoreLimit) EndMatch(true);
            else respawnTimers.Add(respawnDelay);
        }
    }
}
