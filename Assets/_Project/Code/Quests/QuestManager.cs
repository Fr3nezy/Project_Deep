using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Deeploration.Quests
{
    public class QuestManager : MonoBehaviour
    {
        private static QuestManager _instance;
        public static QuestManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<QuestManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("QuestManager");
                        _instance = go.AddComponent<QuestManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Configurazione Missione")]
        [SerializeField] private QuestProfile activeQuestProfile;

        [Header("Eventi")]
        public UnityEvent onQuestCompleted = new UnityEvent();

        [Header("Debug Runtime")]
        [SerializeField] private string activeQuestTitle;
        [SerializeField] private bool isQuestCompleted;
        [SerializeField] private List<ObjectiveDefinition> runtimeObjectives = new List<ObjectiveDefinition>();

        public string ActiveQuestTitle => activeQuestTitle;
        public bool IsQuestCompleted => isQuestCompleted;
        public IReadOnlyList<ObjectiveDefinition> RuntimeObjectives => runtimeObjectives;

        public event Action<QuestManager> OnQuestStateChanged;
        public event Action OnQuestCompleted;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;

            if (activeQuestProfile == null)
            {
#if UNITY_EDITOR
                activeQuestProfile = UnityEditor.AssetDatabase.LoadAssetAtPath<QuestProfile>("Assets/_Project/Entities/Profiles/Quest_Objective_1.asset");
#endif
            }

            InitializeQuest();
        }

        private void InitializeQuest()
        {
            if (activeQuestProfile != null)
            {
                activeQuestTitle = activeQuestProfile.QuestTitle;
                runtimeObjectives = activeQuestProfile.CreateRuntimeObjectives();
                isQuestCompleted = CheckIfAllCompleted();
            }
        }

        public void LoadQuest(QuestProfile profile)
        {
            if (profile == null) return;
            activeQuestProfile = profile;
            InitializeQuest();
            OnQuestStateChanged?.Invoke(this);
        }

        public bool AdvanceObjective(string objectiveId, int amount = 1)
        {
            if (isQuestCompleted) return false;

            ObjectiveDefinition target = runtimeObjectives.Find(o => o.ObjectiveId == objectiveId);
            if (target == null)
            {
                Debug.LogWarning($"[QuestManager] Obiettivo '{objectiveId}' non trovato nella quest corrente.");
                return false;
            }

            bool justCompleted = target.Advance(amount);
            Debug.Log($"<color=#4285f4>[QuestManager]</color> Avanzamento '{target.Title}': {target.CurrentAmount}/{target.RequiredAmount}");

            CheckQuestCompletion();
            OnQuestStateChanged?.Invoke(this);
            return justCompleted;
        }

        public void SetObjectiveCount(string objectiveId, int amount)
        {
            if (isQuestCompleted) return;

            ObjectiveDefinition target = runtimeObjectives.Find(o => o.ObjectiveId == objectiveId);
            if (target != null)
            {
                target.SetAmount(amount);
                CheckQuestCompletion();
                OnQuestStateChanged?.Invoke(this);
            }
        }

        private bool CheckIfAllCompleted()
        {
            if (runtimeObjectives == null || runtimeObjectives.Count == 0) return false;

            foreach (var obj in runtimeObjectives)
            {
                if (!obj.IsCompleted) return false;
            }
            return true;
        }

        private void CheckQuestCompletion()
        {
            if (!isQuestCompleted && CheckIfAllCompleted())
            {
                isQuestCompleted = true;
                Debug.Log($"<color=#10b981>[QuestManager]</color> ★ MISSIONE COMPLETATA: {activeQuestTitle} ★");
                onQuestCompleted?.Invoke();
                OnQuestCompleted?.Invoke();
            }
        }
    }
}
