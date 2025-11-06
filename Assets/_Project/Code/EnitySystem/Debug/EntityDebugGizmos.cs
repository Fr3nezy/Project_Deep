using UnityEngine;

namespace Deeploration.EntitySystem
{
    /// <summary>
    /// Componente per la visualizzazione di gizmos di debug per le entità.
    /// Mostra barre di stato, raggio di sensing, target e stato corrente.
    /// Supporta preview in edit mode per fine-tuning dei parametri FOV.
    /// </summary>
    [RequireComponent(typeof(EntityStatus))]
    [RequireComponent(typeof(SenseController))]
    [ExecuteInEditMode] // Permette esecuzione nell'editor per preview
    public class EntityDebugGizmos : MonoBehaviour
    {
        [Header("Configurazione Visualizzazione")]
        [SerializeField, Tooltip("Abilita/disabilita i gizmos di debug")]
        private bool showGizmos = true;

        [SerializeField, Tooltip("Mostra le barre di stato (HP, Stamina, Hunger)")]
        private bool showStatusBars = true;

        [SerializeField, Tooltip("Mostra il raggio di sensing")]
        private bool showSenseRadius = true;

        [SerializeField, Tooltip("Mostra le linee verso i target")]
        private bool showTargetLines = true;

        [SerializeField, Tooltip("Mostra lo stato corrente come testo")]
        private bool showStateText = true;

        [Header("FOV Gizmos (Campo Visivo)")]
        [SerializeField, Tooltip("Mostra le zone del campo visivo")]
        private bool showFovGizmos = true;

        [SerializeField, Tooltip("Abilita preview dei gizmos in edit mode (senza runtime)")]
        private bool editorPreviewMode = true;

        [SerializeField] private Color biteZoneColor = new Color(1f, 0f, 0f, 0.2f);
        [SerializeField] private Color decisionZoneColor = new Color(1f, 1f, 0f, 0.15f);
        [SerializeField] private Color detectionZoneColor = new Color(0f, 1f, 0f, 0.1f);

        [Header("Parametri Barre di Stato")]
        [SerializeField, Tooltip("Altezza sopra l'entità dove disegnare le barre")]
        [Range(0.5f, 5f)]
        private float barHeightOffset = 2f;

        [SerializeField, Tooltip("Larghezza delle barre di stato")]
        [Range(0.5f, 3f)]
        private float barWidth = 1.5f;

        [SerializeField, Tooltip("Altezza di ogni singola barra")]
        [Range(0.05f, 0.3f)]
        private float barHeight = 0.15f;

        [SerializeField, Tooltip("Spazio tra le barre")]
        [Range(0.02f, 0.2f)]
        private float barSpacing = 0.05f;

        [Header("Colori")]
        [SerializeField] private Color healthColor = new Color(1f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color staminaColor = new Color(0.2f, 0.5f, 1f, 0.8f);
        [SerializeField] private Color hungerColor = new Color(1f, 0.6f, 0.1f, 0.8f);
        [SerializeField] private Color barBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color senseRadiusColor = new Color(0.5f, 0.8f, 1f, 0.3f);
        [SerializeField] private Color preyLineColor = new Color(0.2f, 1f, 0.2f, 0.7f);
        [SerializeField] private Color predatorLineColor = new Color(1f, 0.2f, 0.2f, 0.7f);

        [Header("Distanza di Rendering")]
        [SerializeField, Tooltip("Distanza massima dalla camera per renderizzare i gizmos")]
        [Range(10f, 100f)]
        private float maxRenderDistance = 50f;

        // Riferimenti
        private EntityStatus entityStatus;
        private Transform cachedTransform;

        // Target corrente (verrà settato dal StateManager/SenseController)
        private Transform currentTarget;
        private bool isTargetPrey;

        private void Awake()
        {
            InitializeForEditor();
        }

        /// <summary>
        /// Inizializza i riferimenti per funzionare anche in edit mode.
        /// </summary>
        private void InitializeForEditor()
        {
            if (entityStatus == null)
                entityStatus = GetComponent<EntityStatus>();

            if (cachedTransform == null)
                cachedTransform = transform;
        }

        /// <summary>
        /// Imposta il target corrente per la visualizzazione della linea.
        /// </summary>
        /// <param name="target">Transform del target</param>
        /// <param name="isPrey">True se è una preda, false se è un predatore</param>
        public void SetDebugTarget(Transform target, bool isPrey)
        {
            currentTarget = target;
            isTargetPrey = isPrey;
        }

        /// <summary>
        /// Pulisce il target corrente.
        /// </summary>
        public void ClearDebugTarget()
        {
            currentTarget = null;
        }

        private void OnDrawGizmos()
        {
            // Inizializza riferimenti per edit mode
            if (editorPreviewMode)
            {
                InitializeForEditor();
            }

            if (!showGizmos || entityStatus == null) return;

            // Verifica distanza dalla camera
            Camera cam = Camera.current ?? Camera.main;
            if (cam != null)
            {
                float distance = Vector3.Distance(cachedTransform.position, cam.transform.position);
                if (distance > maxRenderDistance) return;
            }

            // Disegna componenti di debug
            if (showStatusBars)
            {
                DrawStatusBars();
            }

            if (showSenseRadius && entityStatus.Profile != null)
            {
                DrawSenseRadius();
            }

            if (showTargetLines && currentTarget != null)
            {
                DrawTargetLine();
            }

            if (showFovGizmos && entityStatus.Profile != null)
            {
                DrawFovCones();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos || entityStatus == null) return;

            // Quando l'entità è selezionata, mostra info extra
            if (showStateText)
            {
                DrawStateText();
            }
        }

        /// <summary>
        /// Disegna le barre di stato (HP, Stamina, Hunger).
        /// </summary>
        private void DrawStatusBars()
        {
            Vector3 barPosition = cachedTransform.position + Vector3.up * barHeightOffset;

            // Barra Salute
            DrawBar(barPosition, entityStatus.GetStatusNormalized(StatusType.Health), healthColor, "HP");

            // Barra Stamina
            barPosition.y -= (barHeight + barSpacing);
            DrawBar(barPosition, entityStatus.GetStatusNormalized(StatusType.Stamina), staminaColor, "ST");

            // Barra Fame
            barPosition.y -= (barHeight + barSpacing);
            DrawBar(barPosition, entityStatus.GetStatusNormalized(StatusType.Hunger), hungerColor, "HG");
        }

        /// <summary>
        /// Disegna una singola barra di stato.
        /// </summary>
        private void DrawBar(Vector3 position, float fillAmount, Color color, string label)
        {
            // Calcola direzione verso la camera per billboard effect
            Camera cam = Camera.current ?? Camera.main;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;

            // Background (barra grigia)
            Gizmos.color = barBackgroundColor;
            Vector3 bgStart = position - right * (barWidth * 0.5f);
            Vector3 bgEnd = position + right * (barWidth * 0.5f);
            DrawThickLine(bgStart, bgEnd, barHeight);

            // Foreground (barra colorata)
            Gizmos.color = color;
            float filledWidth = barWidth * Mathf.Clamp01(fillAmount);
            Vector3 fgStart = position - right * (barWidth * 0.5f);
            Vector3 fgEnd = fgStart + right * filledWidth;
            DrawThickLine(fgStart, fgEnd, barHeight);

            // Label (piccolo testo, solo per debug avanzato)
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(position - right * (barWidth * 0.5f + 0.2f), label);
            #endif
        }

        /// <summary>
        /// Disegna una linea spessa (simulata con più linee parallele).
        /// </summary>
        private void DrawThickLine(Vector3 start, Vector3 end, float thickness)
        {
            Camera cam = Camera.current ?? Camera.main;
            Vector3 up = cam != null ? cam.transform.up : Vector3.up;

            for (int i = 0; i < 3; i++)
            {
                float offset = (i - 1) * thickness / 3f;
                Gizmos.DrawLine(start + up * offset, end + up * offset);
            }
        }

        /// <summary>
        /// Disegna il raggio di sensing come wireframe sphere.
        /// </summary>
        private void DrawSenseRadius()
        {
            Gizmos.color = senseRadiusColor;
            Gizmos.DrawWireSphere(cachedTransform.position, entityStatus.Profile.senseRadius);
        }

        /// <summary>
        /// Disegna una linea verso il target corrente.
        /// </summary>
        private void DrawTargetLine()
        {
            Gizmos.color = isTargetPrey ? preyLineColor : predatorLineColor;
            Gizmos.DrawLine(cachedTransform.position, currentTarget.position);

            // Disegna una piccola sfera sul target
            Gizmos.DrawWireSphere(currentTarget.position, 0.5f);
        }

        /// <summary>
        /// Disegna lo stato corrente come testo 3D.
        /// </summary>
        private void DrawStateText()
        {
            #if UNITY_EDITOR
            Vector3 textPosition = cachedTransform.position + Vector3.up * (barHeightOffset + 0.8f);
            
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.fontSize = 12;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;

            UnityEditor.Handles.Label(textPosition, entityStatus.GetDebugInfo(), style);
            #endif
        }

        /// <summary>
        /// Disegna i coni FOV per le tre zone.
        /// </summary>
        private void DrawFovCones()
        {
            SenseController senseController = GetComponent<SenseController>();
            if (senseController == null || entityStatus.Profile == null) return;

            Vector3 fovOrigin = senseController.GetFovOrigin();
            Vector3 fovDirection = senseController.GetFovDirection();
            float halfAngle = entityStatus.Profile.fovAngle / 2f;

            // Bite Zone (rosso)
            Gizmos.color = biteZoneColor;
            DrawFovZone(fovOrigin, entityStatus.Profile.biteRange, fovDirection, halfAngle);

            // Decision Zone (giallo)
            Gizmos.color = decisionZoneColor;
            DrawFovZone(fovOrigin, entityStatus.Profile.decisionRange, fovDirection, halfAngle);

            // Detection Zone (verde)
            Gizmos.color = detectionZoneColor;
            DrawFovZone(fovOrigin, entityStatus.Profile.detectionRange, fovDirection, halfAngle);

            // Linea direzione FOV dall'origine offset
            Gizmos.color = new Color(0f, 1f, 1f, 0.5f); // Cyan trasparente
            Gizmos.DrawLine(fovOrigin, fovOrigin + fovDirection * entityStatus.Profile.detectionRange);

            // Mostra origine FOV offset se diverso dal centro
            if (entityStatus.Profile.fovForwardOffset != 0f)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(fovOrigin, 0.1f);
                Gizmos.DrawLine(cachedTransform.position, fovOrigin);
            }
        }

        /// <summary>
        /// Disegna una singola zona FOV come arco circolare semplificato.
        /// </summary>
        private void DrawFovZone(Vector3 origin, float range, Vector3 direction, float halfAngle)
        {
            const int segments = 8;
            Vector3 center = origin;
            Quaternion rotation = Quaternion.LookRotation(direction);

            // Disegna segmenti dell'arco
            for (int i = 0; i < segments; i++)
            {
                float angle1 = -halfAngle + (halfAngle * 2f * i / segments);
                float angle2 = -halfAngle + (halfAngle * 2f * (i + 1) / segments);

                Vector3 point1 = center + rotation * Quaternion.Euler(0, angle1, 0) * Vector3.forward * range;
                Vector3 point2 = center + rotation * Quaternion.Euler(0, angle2, 0) * Vector3.forward * range;

                Gizmos.DrawLine(point1, point2);
            }

            // Disegna linee laterali del cono
            Vector3 leftPoint = center + rotation * Quaternion.Euler(0, -halfAngle, 0) * Vector3.forward * range;
            Vector3 rightPoint = center + rotation * Quaternion.Euler(0, halfAngle, 0) * Vector3.forward * range;

            Gizmos.DrawLine(center, leftPoint);
            Gizmos.DrawLine(center, rightPoint);
        }

        /// <summary>
        /// Disegna gizmos aggiuntivi per il fear threshold.
        /// </summary>
        private void DrawFearThreshold()
        {
            if (entityStatus.Profile == null) return;

            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawWireSphere(cachedTransform.position, entityStatus.Profile.fearThreshold);
        }

        #region Public API for External Components

        /// <summary>
        /// Attiva/disattiva i gizmos di debug.
        /// </summary>
        public void SetGizmosEnabled(bool enabled)
        {
            showGizmos = enabled;
        }

        /// <summary>
        /// Attiva/disattiva un tipo specifico di gizmo.
        /// </summary>
        public void SetGizmoType(string type, bool enabled)
        {
            switch (type.ToLower())
            {
                case "statusbars":
                    showStatusBars = enabled;
                    break;
                case "senseradius":
                    showSenseRadius = enabled;
                    break;
                case "targetlines":
                    showTargetLines = enabled;
                    break;
                case "statetext":
                    showStateText = enabled;
                    break;
            }
        }

        #endregion
    }
}
