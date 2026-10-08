using System.Collections;
using Deeploration.Player;
using UnityEngine;

namespace Deeploration.Prologue
{
    /// <summary>
    /// Gestisce l'effetto rinvenimento del Diver nella scena SCN_Gameplay:
    /// avvio a schermo nero, camera a terra inclinata (posa stordita), dissolvenza in apertura,
    /// rialzamento progressivo ad altezza eretta, battuta di riavvio tuta e restituzione dei controlli.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class PlayerWakeUpSequence : MonoBehaviour
    {
        [Header("Riferimenti")]
        [SerializeField] private DiverController diver;
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private CanvasGroup fadeOverlay;
        [SerializeField] private SubtitlePanel subtitlePanel;
        [SerializeField] private SubtitleLine wakeUpSubtitle;

        [Header("Parametri Rinvenimento")]
        [SerializeField, Tooltip("Durata complessiva del risveglio e rialzamento in secondi.")]
        private float wakeUpDuration = 3.5f;

        [SerializeField, Tooltip("Altezza iniziale della camera rispetto al pavimento della capsula.")]
        private float initialHeadHeight = 0.25f;

        [SerializeField, Tooltip("Altezza finale eretta della camera.")]
        private float standingHeadHeight = 1.375f;

        [SerializeField, Tooltip("Inclinazione laterale (roll) del casco a terra.")]
        private float initialRoll = 28.0f;

        [SerializeField, Tooltip("Inclinazione verticale (pitch) iniziale del casco.")]
        private float initialPitch = 12.0f;

        [SerializeField, Tooltip("Ritardo prima dell'apertura del nero (secondi).")]
        private float fadeDelay = 0.4f;

        [SerializeField, Tooltip("Durata della dissolvenza da nero.")]
        private float fadeDuration = 2.0f;

        [SerializeField, Tooltip("Ritardo prima della comparsa della battuta di emergenza.")]
        private float subtitleDelay = 1.2f;

        [SerializeField, Tooltip("Durata visibilità della battuta di emergenza.")]
        private float subtitleDuration = 5.0f;

        private float timer;
        private bool sequenceCompleted;
        private bool subtitleTriggered;

        private void Awake()
        {
            if (diver == null) diver = Object.FindFirstObjectByType<DiverController>();
            if (cameraTarget == null && diver != null)
            {
                var camRoot = diver.transform.Find("PlayerCameraRoot");
                if (camRoot != null) cameraTarget = camRoot;
            }

            if (fadeOverlay == null)
            {
                var fadeGo = GameObject.Find("FadeOverlay");
                if (fadeGo != null) fadeOverlay = fadeGo.GetComponent<CanvasGroup>();
            }

            if (subtitlePanel == null)
            {
                subtitlePanel = Object.FindFirstObjectByType<SubtitlePanel>();
            }

            // Imposta subito lo stato iniziale stordito a nero
            if (fadeOverlay != null)
            {
                fadeOverlay.alpha = 1.0f;
                fadeOverlay.blocksRaycasts = true;
            }

            if (diver != null)
            {
                diver.LockMovement = true;
            }
        }

        private void Start()
        {
            if (diver != null)
            {
                diver.LockMovement = true;
                diver.CameraBasePosition = new Vector3(0f, standingHeadHeight, 0f);
            }

            if (cameraTarget != null)
            {
                cameraTarget.localPosition = new Vector3(0f, initialHeadHeight, 0f);
                cameraTarget.localRotation = Quaternion.Euler(initialPitch, 0f, initialRoll);
            }
        }

        private void Update()
        {
            if (sequenceCompleted) return;

            timer += Time.deltaTime;

            // 1. Dissolvenza dal nero (riapertura occhi)
            if (fadeOverlay != null && timer >= fadeDelay)
            {
                float fadeProgress = Mathf.Clamp01((timer - fadeDelay) / fadeDuration);
                // Curva smooth per apertura naturale dello sguardo
                fadeOverlay.alpha = 1.0f - Mathf.SmoothStep(0f, 1f, fadeProgress);
            }

            // 2. Battuta diagnostica riavvio tuta (Beat 7)
            if (!subtitleTriggered && timer >= subtitleDelay)
            {
                subtitleTriggered = true;
                if (subtitlePanel != null && wakeUpSubtitle != null)
                {
                    subtitlePanel.Show(wakeUpSubtitle);
                    StartCoroutine(ClearSubtitleAfter(subtitleDuration));
                }
            }

            // 3. Completamento sequenza
            if (timer >= wakeUpDuration)
            {
                CompleteWakeUp();
            }
        }

        private void LateUpdate()
        {
            if (sequenceCompleted) return;

            // Rialzamento del corpo (interpolazione altezza e rotazione casco)
            float t = Mathf.Clamp01(timer / wakeUpDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            float currentHeight = Mathf.Lerp(initialHeadHeight, standingHeadHeight, smoothT);
            float currentRoll = Mathf.Lerp(initialRoll, 0f, smoothT);
            float currentPitch = Mathf.Lerp(initialPitch, 0f, smoothT);

            if (diver != null)
            {
                diver.CameraBasePosition = new Vector3(0f, currentHeight, 0f);
            }

            if (cameraTarget != null)
            {
                cameraTarget.localPosition = new Vector3(0f, currentHeight, 0f);
                cameraTarget.localRotation = Quaternion.Euler(currentPitch, cameraTarget.localEulerAngles.y, currentRoll);
            }
        }

        private void CompleteWakeUp()
        {
            sequenceCompleted = true;

            if (diver != null)
            {
                diver.CameraBasePosition = new Vector3(0f, standingHeadHeight, 0f);
                diver.ResetMotion();
                diver.LockMovement = false;
            }

            if (cameraTarget != null)
            {
                cameraTarget.localPosition = new Vector3(0f, standingHeadHeight, 0f);
                cameraTarget.localRotation = Quaternion.identity;
            }

            if (fadeOverlay != null)
            {
                fadeOverlay.alpha = 0.0f;
                fadeOverlay.blocksRaycasts = false;
            }

            enabled = false;
        }

        private IEnumerator ClearSubtitleAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (subtitlePanel != null)
            {
                subtitlePanel.Clear();
            }
        }
    }
}
