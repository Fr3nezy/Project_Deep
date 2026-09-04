using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Deeploration.Interaction;
using Deeploration.Quests;

namespace Deeploration.Player
{
    public sealed class SuitDebugOverlay : MonoBehaviour
    {
        [Header("Sistemi Tuta")]
        [SerializeField] private OxygenSystem oxygen;
        [SerializeField] private StressSystem stress;
        [SerializeField] private DiverFlashlight flashlight;
        [SerializeField] private PlayerInteraction interaction;
        [SerializeField] private PlayerHands hands;
        [SerializeField] private QuestManager questManager;

        [Header("UI")]
        [SerializeField] private Text counter;

        [Header("Feedback Vignetta URP")]
        [SerializeField] private Volume volume;
        [SerializeField, Range(0f, 1f)] private float minimumVignetteIntensity = 0.2f;
        [SerializeField, Range(0f, 1f)] private float maximumVignetteIntensity = 0.4f;
        [SerializeField, Range(1f, 20f)] private float vignetteResponse = 7f;

        private float oxygenNormalized = 1f;
        private float stressNormalized;
        private float displayedStress;
        private Vignette vignette;
        private readonly StringBuilder sb = new StringBuilder(256);

        private void Awake()
        {
            if (oxygen == null)
                oxygen = FindFirstObjectByType<OxygenSystem>();

            if (stress == null)
                stress = FindFirstObjectByType<StressSystem>();

            if (flashlight == null)
                flashlight = FindFirstObjectByType<DiverFlashlight>();

            if (interaction == null)
                interaction = FindFirstObjectByType<PlayerInteraction>();

            if (hands == null)
                hands = FindFirstObjectByType<PlayerHands>();

            if (questManager == null)
                questManager = QuestManager.Instance;

            if (counter != null)
            {
                counter.verticalOverflow = VerticalWrapMode.Overflow;
                counter.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            VolumeProfile profile = volume != null ? volume.profile : null;
            if (profile != null && profile.TryGet(out vignette))
                vignette.intensity.overrideState = true;
        }

        private void OnEnable()
        {
            if (oxygen != null)
            {
                oxygen.OnOxygenChanged += HandleOxygenChanged;
                oxygenNormalized = oxygen.NormalizedOxygen;
            }

            if (stress != null)
            {
                stress.OnStressChanged += HandleStressChanged;
                stressNormalized = stress.NormalizedStress;
            }

            if (flashlight != null)
                flashlight.OnModeChanged += HandleFlashlightModeChanged;

            if (interaction != null)
                interaction.OnTargetChanged += HandleTargetChanged;

            if (hands != null)
            {
                hands.OnItemPickedUp += HandleItemChanged;
                hands.OnItemDropped += HandleItemChanged;
            }

            if (questManager != null)
                questManager.OnQuestStateChanged += HandleQuestChanged;

            RefreshCounter();
        }

        private void OnDisable()
        {
            if (oxygen != null)
                oxygen.OnOxygenChanged -= HandleOxygenChanged;
            if (stress != null)
                stress.OnStressChanged -= HandleStressChanged;
            if (flashlight != null)
                flashlight.OnModeChanged -= HandleFlashlightModeChanged;
            if (interaction != null)
                interaction.OnTargetChanged -= HandleTargetChanged;
            if (hands != null)
            {
                hands.OnItemPickedUp -= HandleItemChanged;
                hands.OnItemDropped -= HandleItemChanged;
            }
            if (questManager != null)
                questManager.OnQuestStateChanged -= HandleQuestChanged;
        }

        private void Update()
        {
            float blend = 1f - Mathf.Exp(-vignetteResponse * Time.deltaTime);
            displayedStress = Mathf.Lerp(displayedStress, stressNormalized, blend);

            if (vignette != null)
            {
                float maximum = Mathf.Max(minimumVignetteIntensity, maximumVignetteIntensity);
                vignette.intensity.value = Mathf.Lerp(minimumVignetteIntensity, maximum, displayedStress);
            }

            RefreshCounter();
        }

        private void HandleOxygenChanged(float normalized)
        {
            oxygenNormalized = normalized;
            RefreshCounter();
        }

        private void HandleStressChanged(float normalized)
        {
            stressNormalized = normalized;
            RefreshCounter();
        }

        private void HandleFlashlightModeChanged(FlashlightMode mode)
        {
            RefreshCounter();
        }

        private void HandleTargetChanged(IInteractable target)
        {
            RefreshCounter();
        }

        private void HandleItemChanged(ItemPickup item)
        {
            RefreshCounter();
        }

        private void HandleQuestChanged(QuestManager manager)
        {
            RefreshCounter();
        }

        private void RefreshCounter()
        {
            if (counter == null) return;

            sb.Clear();

            // 1. Parametri Vitali Tuta
            string torchStatus = flashlight != null ? flashlight.CurrentMode.ToString().ToUpper() : "N/A";
            sb.AppendLine($"O₂  {oxygenNormalized * 100f:0.0}%");
            sb.AppendLine($"STRESS  {stressNormalized * 100f:0}%");
            sb.AppendLine($"TORCH  [{torchStatus}]");

            // 2. Stato Mani / Oggetto Trasportato
            if (hands != null && hands.IsHoldingItem)
            {
                sb.AppendLine($"<color=#10b981>MANI: [{hands.CurrentItem.ItemName.ToUpper()}]</color> (Premi G per lasciare)");
            }

            // 3. Checklist Missione / Obiettivi
            if (questManager == null)
                questManager = QuestManager.Instance ?? FindFirstObjectByType<QuestManager>();

            if (questManager != null)
            {
                sb.AppendLine();
                sb.AppendLine($"<color=#f59e0b>-- {questManager.ActiveQuestTitle} --</color>");
                if (questManager.IsQuestCompleted)
                {
                    sb.AppendLine("<color=#10b981>★ TUTTI GLI OBIETTIVI COMPLETATI ★</color>");
                }
                else
                {
                    foreach (var obj in questManager.RuntimeObjectives)
                    {
                        if (obj.IsCompleted)
                            sb.AppendLine($"<color=#10b981>[x] {obj.Title} ({obj.CurrentAmount}/{obj.RequiredAmount})</color>");
                        else
                            sb.AppendLine($"<color=#9ca3af>[ ] {obj.Title} ({obj.CurrentAmount}/{obj.RequiredAmount})</color>");
                    }
                }
            }

            // 4. Prompt Interazione Diegetico
            if (interaction != null && interaction.HasTarget && !string.IsNullOrEmpty(interaction.CurrentPrompt))
            {
                sb.AppendLine();
                sb.AppendLine($"<color=#38bdf8>>> [E] {interaction.CurrentPrompt} <<</color>");
            }

            counter.text = sb.ToString();
        }
    }
}
