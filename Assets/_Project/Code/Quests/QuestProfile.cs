using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deeploration.Quests
{
    [Serializable]
    public class ObjectiveDefinition
    {
        [SerializeField] private string objectiveId;
        [SerializeField] private string title;
        [SerializeField] private int requiredAmount = 1;
        [SerializeField] private int currentAmount = 0;

        public string ObjectiveId => objectiveId;
        public string Title => title;
        public int RequiredAmount => requiredAmount;
        public int CurrentAmount => currentAmount;
        public bool IsCompleted => currentAmount >= requiredAmount;

        public ObjectiveDefinition Clone()
        {
            return new ObjectiveDefinition
            {
                objectiveId = this.objectiveId,
                title = this.title,
                requiredAmount = this.requiredAmount,
                currentAmount = this.currentAmount
            };
        }

        public bool Advance(int amount = 1)
        {
            if (IsCompleted) return false;
            currentAmount = Mathf.Min(currentAmount + amount, requiredAmount);
            return IsCompleted;
        }

        public void SetAmount(int amount)
        {
            currentAmount = Mathf.Clamp(amount, 0, requiredAmount);
        }

        public string GetFormattedStatus()
        {
            if (IsCompleted)
            {
                return $"[x] {title} ({currentAmount}/{requiredAmount})";
            }
            return $"[ ] {title} ({currentAmount}/{requiredAmount})";
        }
    }

    [CreateAssetMenu(fileName = "Quest_Profile", menuName = "Deeploration/Quest Profile")]
    public class QuestProfile : ScriptableObject
    {
        [Header("Informazioni Missione")]
        [SerializeField] private string questId = "obj_1_base_dr04";
        [SerializeField] private string questTitle = "OBIETTIVO 1 — Riapertura Base DR-04";
        [TextArea(2, 4)]
        [SerializeField] private string questDescription = "Ripristina i generatori, ricarica l'ossigeno e apri l'airlock della base.";

        [Header("Obiettivi")]
        [SerializeField] private List<ObjectiveDefinition> objectives = new List<ObjectiveDefinition>();

        public string QuestId => questId;
        public string QuestTitle => questTitle;
        public string QuestDescription => questDescription;
        public IReadOnlyList<ObjectiveDefinition> Objectives => objectives;

        public List<ObjectiveDefinition> CreateRuntimeObjectives()
        {
            List<ObjectiveDefinition> list = new List<ObjectiveDefinition>(objectives.Count);
            foreach (var obj in objectives)
            {
                list.Add(obj.Clone());
            }
            return list;
        }
    }
}
