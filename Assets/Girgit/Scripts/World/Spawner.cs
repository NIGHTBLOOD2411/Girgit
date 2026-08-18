using System.Collections.Generic;
using UnityEngine;

namespace Girgit
{
    /// <summary>
    /// Spawns pooled copies of Prefab from a small area centered on this
    /// transform, each moving in MoveDirection at its own random speed.
    /// Instances are never destroyed — once one has been off-screen for
    /// DespawnDelay seconds (having been on-screen at least once since it
    /// spawned), PooledMover asks this spawner to recycle it, so the pool
    /// only ever grows to ActiveCount.
    /// </summary>
    public class Spawner : MonoBehaviour
    {
        [Tooltip("The dot/bar prefab this spawner pools and spawns.")]
        public GameObject Prefab;

        [Tooltip("How many copies are alive (spawned, not yet recycled) at once.")]
        [Min(1)] public int ActiveCount = 5;

        [Tooltip("Direction spawned instances travel (doesn't need to be normalized). Left = (-1, 0).")]
        public Vector2 MoveDirection = Vector2.left;

        [Tooltip("Width of the spawn area, centered on this transform's X.")]
        [Min(0f)] public float SpawnWidth = 1f;

        [Tooltip("Height of the spawn area, centered on this transform's Y.")]
        [Min(0f)] public float SpawnHeight = 0f;

        [Tooltip("Random scroll speed (world units/second) rolled fresh for each spawned instance.")]
        public Vector2 SpeedRange = new Vector2(1.5f, 4f);

        [Tooltip("Seconds an instance must sit off-screen before it's recycled back into the pool.")]
        [Min(0f)] public float DespawnDelay = 2f;

        [Tooltip("Minimum seconds between two consecutive spawns, even if more than one slot is free at once. 0 = refill instantly.")]
        [Min(0f)] public float SpawnInterval = 0f;

        [Tooltip("Start-only: the first batch is spread across this much extra distance behind the spawn area (opposite MoveDirection), so the screen isn't empty until things scroll in.")]
        [Min(0f)] public float InitialSpreadWidth = 20f;

        readonly Queue<GameObject> _pool = new Queue<GameObject>();
        readonly List<PooledMover> _active = new List<PooledMover>();
        float _spawnCooldown;

        void Start()
        {
            if (Prefab == null) return;
            for (int i = 0; i < ActiveCount; i++)
                Spawn(i / (float)Mathf.Max(1, ActiveCount) * InitialSpreadWidth);
        }

        void Update()
        {
            if (Prefab == null) return;

            if (_spawnCooldown > 0f) _spawnCooldown -= Time.deltaTime;

            while (_active.Count < ActiveCount && _spawnCooldown <= 0f)
            {
                Spawn(0f);
                _spawnCooldown += SpawnInterval;
            }
        }

        void Spawn(float spreadDistance)
        {
            Vector3 dir = MoveDirection.sqrMagnitude > 0.0001f ? ((Vector3)MoveDirection).normalized : Vector3.left;

            // Parented under this spawner (not scene root) so it stays nested
            // inside Parallax_Back/Front — GameManager's victory/game-over
            // fade walks EnvironmentRoots' children to fade them with the player.
            GameObject go = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(Prefab, transform);
            go.transform.position = RandomSpawnPosition() - dir * spreadDistance;
            go.SetActive(true);

            var mover = go.GetComponent<PooledMover>();
            if (mover == null) mover = go.AddComponent<PooledMover>();
            mover.Init(this, dir, Random.Range(SpeedRange.x, SpeedRange.y), DespawnDelay);
            _active.Add(mover);
        }

        Vector3 RandomSpawnPosition()
        {
            float x = transform.position.x + Random.Range(-SpawnWidth * 0.5f, SpawnWidth * 0.5f);
            float y = transform.position.y + Random.Range(-SpawnHeight * 0.5f, SpawnHeight * 0.5f);
            return new Vector3(x, y, transform.position.z);
        }

        /// <summary>Called by a PooledMover once it's been off-screen long enough — deactivates it and returns it to the pool for the next Spawn().</summary>
        public void Recycle(PooledMover mover)
        {
            _active.Remove(mover);
            mover.gameObject.SetActive(false);
            _pool.Enqueue(mover.gameObject);
        }
    }
}
