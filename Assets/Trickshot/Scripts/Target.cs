using System.Collections;
using UnityEngine;

namespace Trickshot
{
    /// <summary>
    /// Something worth shooting. Put this on any object with a collider (or on its parent).
    /// It pops when hit and comes back after a delay. Give it a move distance to make it slide.
    /// </summary>
    public class Target : MonoBehaviour
    {
        [Tooltip("Points before any multipliers.")]
        public int basePoints = 100;
        public float respawnDelay = 1.5f;

        [Header("Movement (optional)")]
        public Vector3 moveAxis = Vector3.right;
        [Tooltip("Metres either side of the start position. 0 = stands still.")]
        public float moveDistance = 0f;
        public float moveSpeed = 1f;

        public bool IsAlive { get; private set; } = true;

        Vector3 startPosition;
        float phase;
        Renderer[] renderers;
        Collider[] colliders;

        void Start()
        {
            startPosition = transform.position;
            phase = Random.value * Mathf.PI * 2f;
            renderers = GetComponentsInChildren<Renderer>();
            colliders = GetComponentsInChildren<Collider>();
        }

        void Update()
        {
            if (moveDistance <= 0f) return;
            Vector3 axis = moveAxis.sqrMagnitude > 0.0001f ? moveAxis.normalized : Vector3.right;
            transform.position = startPosition + axis * (Mathf.Sin(Time.time * moveSpeed + phase) * moveDistance);
        }

        public void Hit()
        {
            if (!IsAlive) return;
            StartCoroutine(PopAndRespawn());
        }

        IEnumerator PopAndRespawn()
        {
            IsAlive = false;
            SetVisible(false);
            yield return new WaitForSeconds(respawnDelay);
            SetVisible(true);
            IsAlive = true;
        }

        void SetVisible(bool visible)
        {
            if (renderers != null)
                foreach (Renderer r in renderers) if (r != null) r.enabled = visible;
            if (colliders != null)
                foreach (Collider c in colliders) if (c != null) c.enabled = visible;
        }
    }
}
