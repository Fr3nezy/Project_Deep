using System.Collections;
using Deeploration.Player;
using UnityEngine;

namespace Deeploration.Prologue
{
    /// <summary>
    /// Rinvenimento del Diver nella scena SCN_Gameplay, ancora agganciato al sedile della capsula:
    /// avvio a schermo nero con la testa reclinata (posa da perdita di sensi), dissolvenza in apertura,
    /// battuta di riavvio tuta e testa che si rialza ruotando sull'asse X locale attorno al collo.
    /// A fine sequenza il diver resta seduto: visuale libera, locomozione bloccata e CharacterController
    /// spento finché l'uscita dal portello (HatchExit) non lo porta fuori.
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

        [Header("Posa da svenuto")]
        [SerializeField, Tooltip("Se attivo, pitch e roll della posa si leggono dalla rotazione del diver in scena (che poi torna dritto). Altrimenti si usano i valori sotto.")]
        private bool readPoseFromTransform = true;

        [SerializeField, Tooltip("Testa reclinata in avanti (gradi su X locale, positivo = verso il basso).")]
        private float slumpPitch = 25.0f;

        [SerializeField, Tooltip("Inclinazione laterale del corpo sul sedile della capsula schiantata (gradi su Z locale). Resta anche dopo il risveglio, finché il diver è seduto.")]
        private float slumpRoll = 17.0f;

        [SerializeField, Tooltip("Altezza degli occhi del diver seduto, nello spazio locale del diver.")]
        private float eyeHeight = 1.375f;

        [SerializeField, Tooltip("Altezza del perno del collo attorno a cui ruota la testa.")]
        private float neckHeight = 1.2f;

        [Header("Rialzamento della testa")]
        [SerializeField, Tooltip("Attesa prima che la testa inizi a sollevarsi (secondi).")]
        private float liftDelay = 1.2f;

        [SerializeField, Tooltip("Durata del sollevamento sull'asse X.")]
        private float liftDuration = 2.6f;

        [SerializeField, Tooltip("Andamento del sollevamento (0 = reclinata, 1 = dritta). Il default ha una breve esitazione a metà.")]
        private AnimationCurve liftCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.4f, 0.34f),
            new Keyframe(0.55f, 0.38f),
            new Keyframe(1f, 1f, 0f, 0f));

        [Header("Dissolvenza e battuta")]
        [SerializeField, Tooltip("Ritardo prima dell'apertura del nero (secondi).")]
        private float fadeDelay = 0.4f;

        [SerializeField, Tooltip("Durata della dissolvenza da nero.")]
        private float fadeDuration = 2.0f;

        [SerializeField, Tooltip("Ritardo prima della comparsa della battuta di emergenza.")]
        private float subtitleDelay = 1.2f;

        [SerializeField, Tooltip("Durata visibilità della battuta di emergenza.")]
        private float subtitleDuration = 5.0f;

        private CharacterController controller;
        private float timer;
        private bool sequenceCompleted;
        private bool subtitleTriggered;

        private void Awake()
        {
            if (diver == null) diver = Object.FindFirstObjectByType<DiverController>();
            if (cameraTarget == null && diver != null) cameraTarget = diver.CameraTarget;

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
                Transform body = diver.transform;
                if (readPoseFromTransform)
                {
                    // La posa authorata in scena inclina tutto il diver: la si trasferisce alla testa
                    Quaternion yawOnly = Quaternion.Euler(0f, body.eulerAngles.y, 0f);
                    Vector3 tilt = (Quaternion.Inverse(yawOnly) * body.rotation).eulerAngles;
                    slumpPitch = Mathf.DeltaAngle(0f, tilt.x);
                    slumpRoll = Mathf.DeltaAngle(0f, tilt.z);
                    body.rotation = yawOnly;
                }

                // Seduto: niente CharacterController (non entra nella capsula) e niente visuale finché la testa non è su
                controller = diver.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;
                diver.LockMovement = true;
                diver.enabled = false;
            }

            ApplyHeadPose(slumpPitch, slumpRoll);
        }

        private void Update()
        {
            if (sequenceCompleted) return;

            timer += Time.deltaTime;

            // 1. Dissolvenza dal nero (riapertura occhi)
            if (fadeOverlay != null && timer >= fadeDelay)
            {
                float fadeProgress = Mathf.Clamp01((timer - fadeDelay) / fadeDuration);
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
            float endTime = Mathf.Max(liftDelay + liftDuration, fadeDelay + fadeDuration);
            if (timer >= endTime)
            {
                CompleteWakeUp();
            }
        }

        private void LateUpdate()
        {
            if (sequenceCompleted) return;

            float lift = liftDuration > 0f ? Mathf.Clamp01((timer - liftDelay) / liftDuration) : 1f;
            // Solo l'asse X: il roll è l'inclinazione del sedile e resta
            float pitch = Mathf.LerpUnclamped(slumpPitch, 0f, liftCurve.Evaluate(lift));
            ApplyHeadPose(pitch, slumpRoll);
        }

        /// <summary>La testa ruota attorno al collo: anche la camera si sposta, non solo si inclina.</summary>
        private void ApplyHeadPose(float pitch, float roll)
        {
            if (cameraTarget == null) return;

            Quaternion head = Quaternion.Euler(pitch, 0f, roll);
            Vector3 neck = new Vector3(0f, neckHeight, 0f);
            cameraTarget.localPosition = neck + head * new Vector3(0f, eyeHeight - neckHeight, 0f);
            cameraTarget.localRotation = head;
        }

        private void CompleteWakeUp()
        {
            sequenceCompleted = true;
            ApplyHeadPose(0f, slumpRoll);

            if (diver != null)
            {
                // Resta seduto: visuale libera con il roll del sedile, locomozione bloccata finché HatchExit non lo porta fuori
                diver.CameraBasePosition = cameraTarget != null ? cameraTarget.localPosition : new Vector3(0f, eyeHeight, 0f);
                diver.ViewRoll = slumpRoll;
                diver.enabled = true;
                diver.SetViewPitch(0f);
                diver.ResetMotion();
                diver.LockMovement = true;
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
