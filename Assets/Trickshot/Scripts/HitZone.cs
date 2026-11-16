using UnityEngine;

namespace Trickshot
{
    public enum HitZoneType
    {
        Head,   // one shot, headshot bonus
        Upper,  // chest and stomach: one shot
        Lower   // legs: two shots
    }

    /// <summary>Marks one collider of a target as a body part. The rifle looks for this on whatever it hits.</summary>
    public class HitZone : MonoBehaviour
    {
        public Target target;
        public HitZoneType zone;
    }
}
