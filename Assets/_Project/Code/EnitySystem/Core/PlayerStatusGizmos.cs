using UnityEngine;

namespace Deeploration.EntitySystem
{
    /// <summary>
    /// Componente per visualizzare debug info del Player (barre fame/stamina, testo).
    /// Simile a EntityDebugGizmos, ma specifico per PlayerStatus.
    /// </summary>
    [RequireComponent(typeof(PlayerStatus))]
    public class PlayerStatusGizmos : MonoBehaviour
    {
        [Header("Visualizzazione")]
        [SerializeField, Tooltip("Mostra barre di stato")]
        private bool showStatusBars = true;

        [SerializeField, Tooltip("Mostra info testo")]
        private bool showStatusInfo = true;

        [SerializeField, Tooltip("Altezza delle barre sopra il Player")]
        [Range(1f, 10f)]
        private float barHeight = 4f;

        [SerializeField, Tooltip("Larghezza delle barre")]
        [Range(1f, 5f)]
        private float barWidth = 2f;

        [SerializeField, Tooltip("Spessore barra")]
        [Range(0.1f, 1f)]
        private float barThickness = 0.2f;

        // Componente
        private PlayerStatus playerStatus;
        private Transform cachedTransform;

        private void Awake()
        {
            playerStatus = GetComponent<PlayerStatus>();
            cachedTransform = transform;
        }

        private void OnDrawGizmos()
        {
            if (playerStatus == null) return;

            if (!showStatusBars && !showStatusInfo) return;

            Vector3 basePosition = cachedTransform.position + Vector3.up * barHeight;

            // Barra Stamina (verde)
            DrawBar(basePosition, playerStatus.CurrentStamina, playerStatus.MaxStamina, playerStatus.Profile.staminaBarColor, -1);

            // Barra Fame (arancione)
            DrawBar(basePosition + Vector3.up * 0.3f, playerStatus.CurrentHunger, playerStatus.MaxHunger, playerStatus.Profile.hungerBarColor, 1);

            // Barra Salute (rosso, sotto)
            if (playerStatus.CurrentHealth < playerStatus.MaxHealth)
            {
                DrawBar(basePosition - Vector3.up * 0.3f, playerStatus.CurrentHealth, playerStatus.MaxHealth, playerStatus.Profile.healthBarColor, 0);
            }
        }

        private void DrawBar(Vector3 center, float current, float max, Color color, int side)
        {
            if (max <= 0) return;

            float fillRatio = Mathf.Clamp01(current / max);

            Vector3 barCenter = center + cachedTransform.right * side * (barWidth * 0.5f);
            Vector3 barSize = new Vector3(barWidth, barThickness, barThickness);

            // Barra sfondo (grigio)
            Gizmos.color = Color.gray;
            Gizmos.DrawCube(barCenter, barSize);

            // Barra riempimento
            Gizmos.color = color;
            Vector3 fillSize = new Vector3(barWidth * fillRatio, barThickness, barThickness);
            Gizmos.DrawCube(barCenter - cachedTransform.right * (barSize.x - fillSize.x) * 0.5f, fillSize);
        }

        private void OnDrawGizmosSelected()
        {
            if (playerStatus == null || !showStatusInfo) return;

            // Testo info
            string info = playerStatus.GetDebugInfo();

            #if UNITY_EDITOR
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.fontSize = 12;
            style.fontStyle = FontStyle.Bold;

            Vector3 textPosition = cachedTransform.position + Vector3.up * (barHeight + 1f);
            UnityEditor.Handles.Label(textPosition, info, style);
            #endif
        }
    }
}
