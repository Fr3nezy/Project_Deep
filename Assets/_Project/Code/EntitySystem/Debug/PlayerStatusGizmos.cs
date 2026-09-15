using UnityEngine;

namespace Deeploration.EntitySystem
{
    /// <summary>
    /// Componente per visualizzare debug info del Player (barre fame/stamina, testo).
    /// Simile a EntityDebugGizmos, ma specifico per PlayerStatus.
    /// </summary>
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
        private Transform rootTransform;

        private void Awake()
        {
            // Cerca PlayerStatus su questo GameObject, parent o children
            playerStatus = GetComponentInParent<PlayerStatus>();
            if (playerStatus == null)
            {
                playerStatus = GetComponentInChildren<PlayerStatus>();
            }
            
            if (playerStatus == null)
            {
                Debug.LogError("[PlayerStatusGizmos] PlayerStatus not found on this GameObject, parent, or children!", this);
            }
            else
            {
                // Usa il transform del PlayerStatus come root per le barre
                rootTransform = playerStatus.transform;
            }
        }

        private void OnDrawGizmos()
        {
            if (playerStatus == null || rootTransform == null) return;

            if (!showStatusBars && !showStatusInfo) return;

            // Usa rootTransform (dove si trova PlayerStatus) per posizionare le barre
            Vector3 basePosition = rootTransform.position + Vector3.up * barHeight;

            // Barre impilate verticalmente, centrate sopra il player
            // Barra Salute (rosso, in alto)
            DrawBar(basePosition + Vector3.up * 0.6f, playerStatus.CurrentHealth, playerStatus.MaxHealth, playerStatus.Profile.healthBarColor);

            // Barra Stamina (verde, al centro)
            DrawBar(basePosition + Vector3.up * 0.3f, playerStatus.CurrentStamina, playerStatus.MaxStamina, playerStatus.Profile.staminaBarColor);

            // Barra Fame (arancione, in basso)
            DrawBar(basePosition, playerStatus.CurrentHunger, playerStatus.MaxHunger, playerStatus.Profile.hungerBarColor);
        }

        private void DrawBar(Vector3 center, float current, float max, Color color)
        {
            if (max <= 0) return;

            float fillRatio = Mathf.Clamp01(current / max);

            // Centra la barra sopra il player
            Vector3 barSize = new Vector3(barWidth, barThickness, barThickness);

            // Barra sfondo (grigio scuro)
            Gizmos.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            Gizmos.DrawCube(center, barSize);

            // Barra riempimento (allineata a sinistra)
            if (fillRatio > 0)
            {
                Gizmos.color = color;
                Vector3 fillSize = new Vector3(barWidth * fillRatio, barThickness, barThickness);
                Vector3 fillCenter = center - Vector3.right * (barWidth - fillSize.x) * 0.5f;
                Gizmos.DrawCube(fillCenter, fillSize);
            }

            // Bordo barra (nero)
            Gizmos.color = Color.black;
            Gizmos.DrawWireCube(center, barSize);
        }

        private void OnDrawGizmosSelected()
        {
            if (playerStatus == null || rootTransform == null || !showStatusInfo) return;

            // Testo info
            string info = playerStatus.GetDebugInfo();

            #if UNITY_EDITOR
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.fontSize = 12;
            style.fontStyle = FontStyle.Bold;

            Vector3 textPosition = rootTransform.position + Vector3.up * (barHeight + 1f);
            UnityEditor.Handles.Label(textPosition, info, style);
            #endif
        }
    }
}
