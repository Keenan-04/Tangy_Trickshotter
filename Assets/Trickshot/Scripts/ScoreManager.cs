using System;
using System.Collections.Generic;
using UnityEngine;

namespace Trickshot
{
    /// <summary>Everything the scoring rules need to know about one shot.</summary>
    public struct ShotInfo
    {
        public bool scoped;        // false = no-scope
        public float scopeTime;    // seconds since the scope started coming up
        public float spinDegrees;  // net rotation built up before the shot
        public bool airborne;
        public float distance;     // metres to whatever was hit
        public HitZoneType zone;   // body part hit (only meaningful on a hit)
        public bool killed;        // this shot finished the target
    }

    public class ShotResult
    {
        public bool hit;
        public bool killed;
        public bool headshot;
        public float headMultiplier = 1f;
        public int points;
        public int basePoints;
        public float scopeMultiplier = 1f;
        public float spinMultiplier = 1f;
        public float bonusMultiplier = 1f;
        public float time;
        public readonly List<string> tags = new List<string>();
    }

    /// <summary>
    /// The scoring rules. points = base x head x scope x spin x style.
    ///   head:  a headshot multiplies the kill.
    ///   scope: no-scope pays the most, a fast quickscope a bit less, and the multiplier
    ///          keeps draining the longer you stay scoped in.
    ///   spin:  every half turn (180) adds to the multiplier.
    ///   style: extras such as being airborne or a long-distance hit.
    /// All the numbers are public so you can tune them in the Inspector.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        [Header("Headshots")]
        public float headshotMultiplier = 1.5f;

        [Header("Scope")]
        [Tooltip("On: a hardscoped hit scores nothing. The trainer switches this on for the quickscope drill.")]
        public bool quickscopeOnly = false;
        [Tooltip("Multiplier for a shot fired without scoping at all. Keep this the highest.")]
        public float noScopeMultiplier = 3f;
        [Tooltip("Multiplier for a shot fired the instant you start scoping.")]
        public float quickscopeMax = 2.5f;
        [Tooltip("Multiplier at the very end of the quickscope window.")]
        public float quickscopeMin = 1.5f;
        [Tooltip("Seconds after scoping in that still count as a quickscope.")]
        public float quickscopeWindow = 0.65f;
        [Tooltip("Lowest multiplier a hardscope can drain to. Set it negative to make camping the scope cost points.")]
        public float hardscopeFloor = 0.1f;
        [Tooltip("Seconds scoped at which the multiplier reaches the floor.")]
        public float hardscopeFloorTime = 3f;

        [Header("Spins")]
        [Tooltip("Degrees of rotation per spin step. 180 means a 360 is two steps.")]
        public float spinStepDegrees = 180f;
        [Tooltip("Multiplier added per spin step. 0.5 makes a 360 worth x2 and a 720 worth x3.")]
        public float bonusPerSpinStep = 0.5f;
        [Tooltip("Degrees of slack, so a 350 still counts as a 360.")]
        public float spinForgiveness = 15f;

        [Header("Style bonuses (added together, then applied as 1 + total)")]
        public float airborneBonus = 0.5f;
        public float longshotDistance = 40f;
        public float longshotBonus = 0.5f;

        public int Score { get; private set; }
        public int BestShot { get; private set; }
        public int ShotsFired { get; private set; }
        public int ShotsHit { get; private set; }
        public int Kills { get; private set; }
        public int Headshots { get; private set; }
        public ShotResult LastResult { get; private set; }

        /// <summary>Raised after every shot, hit or miss.</summary>
        public event Action<ShotResult> ShotResolved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool IsQuickscope(float scopeTime) => scopeTime <= quickscopeWindow;

        public float ScopeMultiplier(bool scoped, float scopeTime)
        {
            if (!scoped) return noScopeMultiplier;

            if (scopeTime <= quickscopeWindow)
            {
                float q = scopeTime / Mathf.Max(quickscopeWindow, 0.0001f);
                return Mathf.Lerp(quickscopeMax, quickscopeMin, q);
            }

            float h = Mathf.InverseLerp(quickscopeWindow, hardscopeFloorTime, scopeTime);
            return Mathf.Lerp(quickscopeMin, hardscopeFloor, h);
        }

        public int SpinSteps(float spinDegrees)
        {
            float step = Mathf.Max(spinStepDegrees, 1f);
            return Mathf.Max(0, Mathf.FloorToInt((spinDegrees + spinForgiveness) / step));
        }

        public float SpinMultiplier(float spinDegrees) => 1f + SpinSteps(spinDegrees) * bonusPerSpinStep;

        /// <summary>Works out what a hit would be worth without changing the score.</summary>
        public ShotResult Evaluate(ShotInfo shot, int basePoints)
        {
            var r = new ShotResult { hit = true, killed = shot.killed, basePoints = basePoints, time = Time.time };

            if (shot.zone == HitZoneType.Head)
            {
                r.headshot = true;
                r.headMultiplier = headshotMultiplier;
                r.tags.Add("HEADSHOT");
            }
            else if (!shot.killed)
            {
                r.tags.Add("HIT");
            }

            r.scopeMultiplier = ScopeMultiplier(shot.scoped, shot.scopeTime);
            if (!shot.scoped) r.tags.Add("NO SCOPE");
            else if (IsQuickscope(shot.scopeTime)) r.tags.Add("QUICKSCOPE");
            else r.tags.Add("HARDSCOPE");

            int steps = SpinSteps(shot.spinDegrees);
            r.spinMultiplier = 1f + steps * bonusPerSpinStep;
            if (steps > 0) r.tags.Add(Mathf.RoundToInt(steps * spinStepDegrees).ToString());

            float bonus = 0f;
            if (shot.airborne)
            {
                bonus += airborneBonus;
                r.tags.Add("AIRBORNE");
            }
            if (shot.distance >= longshotDistance)
            {
                bonus += longshotBonus;
                r.tags.Add("LONGSHOT");
            }
            r.bonusMultiplier = 1f + bonus;

            r.points = Mathf.RoundToInt(basePoints * r.headMultiplier * r.scopeMultiplier * r.spinMultiplier * r.bonusMultiplier);

            if (quickscopeOnly && shot.scoped && !IsQuickscope(shot.scopeTime))
            {
                r.points = 0;
                r.tags.Add("TOO SLOW");
            }
            return r;
        }

        public ShotResult RegisterHit(ShotInfo shot, int basePoints)
        {
            ShotResult r = Evaluate(shot, basePoints);
            ShotsFired++;
            ShotsHit++;
            if (r.killed)
            {
                Kills++;
                Profile.AddWeaponXp(r.headshot ? Profile.HeadshotXp : Profile.KillXp); // levels the equipped rifle
            }
            if (r.killed && r.headshot) Headshots++;
            Score += r.points;
            if (r.points > BestShot) BestShot = r.points;
            Publish(r);
            return r;
        }

        public ShotResult RegisterMiss(ShotInfo shot)
        {
            var r = new ShotResult { hit = false, time = Time.time };
            r.tags.Add("MISS");
            ShotsFired++;
            Publish(r);
            return r;
        }

        public void ResetScore()
        {
            Score = 0;
            BestShot = 0;
            ShotsFired = 0;
            ShotsHit = 0;
            Kills = 0;
            Headshots = 0;
            LastResult = null;
        }

        void Publish(ShotResult r)
        {
            LastResult = r;
            ShotResolved?.Invoke(r);
        }
    }
}
