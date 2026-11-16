using UnityEngine;

namespace Trickshot
{
    /// <summary>
    /// TEMPLATE. Elimination: no respawns. Wipe out the other team to win the round; the first
    /// team to enough rounds wins the match. Until enemies can fight back, running out of time
    /// is the only way to lose a round.
    /// </summary>
    public class EliminationMode : MatchMode
    {
        [Header("Rules")]
        public int roundsToWin = 4;
        public int enemiesPerRound = 4;
        [Tooltip("Round length in seconds.")]
        public float roundTime = 90f;
        [Tooltip("Pause between rounds in seconds.")]
        public float intermission = 4f;

        float wait;

        public override string DisplayName => "ELIMINATION";
        public override string Summary => "No respawns. Wipe the other team to take the round.";
        public override string[] Rules => new[]
        {
            "First team to " + roundsToWin + " rounds wins",
            "No respawns; eliminate all " + enemiesPerRound + " enemies to win the round",
            Mathf.RoundToInt(roundTime) + " second rounds; time running out loses the round",
            "Template: enemies are dummies and do not shoot back"
        };

        protected override string StatusLine =>
            State == MatchState.Live ? "Enemies left  " + enemies.Count : null;

        protected override void OnBegin()
        {
            StartRound();
        }

        void StartRound()
        {
            ClearEnemies();
            Banner = null;
            TimeLeft = roundTime;
            State = MatchState.Live;
            for (int i = 0; i < enemiesPerRound; i++) SpawnEnemy();
        }

        protected override void Tick(float dt)
        {
            if (State == MatchState.RoundOver)
            {
                wait -= dt;
                if (wait <= 0f) StartRound();
                return;
            }

            TimeLeft -= dt;
            if (TimeLeft <= 0f) FinishRound(false);
        }

        protected override void OnEnemyKilled()
        {
            if (enemies.Count == 0) FinishRound(true);
        }

        void FinishRound(bool won)
        {
            if (won) FriendlyScore++;
            else EnemyScore++;

            if (FriendlyScore >= roundsToWin || EnemyScore >= roundsToWin)
            {
                EndMatch(FriendlyScore > EnemyScore);
                return;
            }

            ClearEnemies();
            State = MatchState.RoundOver;
            Banner = won ? "ROUND WON" : "ROUND LOST";
            wait = intermission;
        }
    }
}
