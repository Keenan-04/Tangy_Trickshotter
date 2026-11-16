using UnityEngine;

namespace Trickshot
{
    /// <summary>
    /// Convenience for prototyping: when you press Play in a scene that has no player,
    /// this spawns the test range automatically.
    /// DELETE THIS FILE once you have your own level with a player placed in it.
    /// </summary>
    public static class TrickshotAutoStart
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void SpawnRangeIfSceneHasNoPlayer()
        {
            if (PlayerController.Instance != null) return;
            new GameObject("Trickshot Bootstrap").AddComponent<TrickshotBootstrap>();
        }
    }
}
