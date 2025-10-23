using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace GameSystem
{
    public class EntityManager : MonoBehaviour
    {
        private static EntityManager instance;
        
        public static EntityManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<EntityManager>();
                    
                    if (instance == null)
                    {
                        GameObject go = new GameObject("EntityManager");
                        instance = go.AddComponent<EntityManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;
        [SerializeField] private Key debugKey = Key.E;

        private List<Entity> allEntities = new List<Entity>();
        private Keyboard keyboard;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning($"[EntityManager] Duplicate instance detected! Destroying {gameObject.name}");
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            keyboard = Keyboard.current;
        }

        public void RegisterEntity(Entity entity)
        {
            if (entity == null)
                return;

            if (!allEntities.Contains(entity))
            {
                allEntities.Add(entity);

                if (showDebugLogs)
                {
                    Debug.Log($"[EntityManager] Registered: {entity.GetEntityName()} (Total: {allEntities.Count})");
                }
            }
        }

        public void UnregisterEntity(Entity entity)
        {
            if (entity == null)
                return;

            if (allEntities.Remove(entity))
            {
                if (showDebugLogs)
                {
                    Debug.Log($"[EntityManager] Unregistered: {entity.GetEntityName()} (Total: {allEntities.Count})");
                }
            }
        }

        // TWO OVERLOADS FOR GetEntitiesInRadius:
        
        public List<Entity> GetEntitiesInRadius(Vector3 position, float radius)
        {
            List<Entity> result = new List<Entity>();
            float radiusSqr = radius * radius;

            foreach (Entity entity in allEntities)
            {
                if (entity == null || entity.IsDead())
                    continue;

                float distanceSqr = (entity.GetPosition() - position).sqrMagnitude;
                if (distanceSqr <= radiusSqr)
                {
                    result.Add(entity);
                }
            }

            return result;
        }

        public List<Entity> GetEntitiesInRadius(Vector3 position, float radius, Entity excludeEntity)
        {
            List<Entity> result = new List<Entity>();
            float radiusSqr = radius * radius;

            foreach (Entity entity in allEntities)
            {
                if (entity == null || entity.IsDead() || entity == excludeEntity)
                    continue;

                float distanceSqr = (entity.GetPosition() - position).sqrMagnitude;
                if (distanceSqr <= radiusSqr)
                {
                    result.Add(entity);
                }
            }

            return result;
        }

        public List<Entity> GetAllEntities()
        {
            return new List<Entity>(allEntities);
        }

        public int GetEntityCount()
        {
            return allEntities.Count;
        }

        private void Update()
        {
            if (keyboard != null && keyboard[debugKey].wasPressedThisFrame)
            {
                DebugListEntities();
            }
        }

        private void DebugListEntities()
        {
            Debug.Log($"=== ENTITY MANAGER: {allEntities.Count} entities ===");
            foreach (Entity entity in allEntities)
            {
                if (entity != null)
                {
                    string status = entity.IsDead() ? "DEAD" : "ALIVE";
                    Debug.Log($"  - {entity.GetEntityName()} ({entity.GetEntityType()}) [{status}]");
                }
            }
        }

        public void DebugPrintEntities()
        {
            DebugListEntities();
        }
    }
}
