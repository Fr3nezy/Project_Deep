using UnityEngine;

namespace Deeploration.Creatures
{
    /// <summary>
    /// Occhi che guardano: ogni osso dell'occhio ruota dalla posa di riposo verso il punto che la creatura sta
    /// guardando (<see cref="Creature.TryGetFocus"/>: il casco del sub, la preda, la luce), entro un limite di yaw e
    /// pitch oltre il quale il bulbo si schiaccerebbe contro la pelle. Senza un punto da guardare fa piccoli scatti
    /// casuali (saccadi) con pause; mentre guarda qualcosa gli scatti sono minimi. Gli occhi si muovono insieme.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreatureEyes : MonoBehaviour
    {
        [Tooltip("Ossa degli occhi. L'asse Y locale è lo sguardo a riposo, il pivot è il centro del bulbo.")]
        [SerializeField] private Transform[] eyes;

        [Tooltip("Rotazione massima a destra e a sinistra dalla posa di riposo (gradi).")]
        [SerializeField, Range(0f, 60f)] private float maxYaw = 20f;

        [Tooltip("Rotazione massima in alto e in basso dalla posa di riposo (gradi).")]
        [SerializeField, Range(0f, 60f)] private float maxPitch = 15f;

        [Tooltip("Velocità con cui l'occhio raggiunge il punto voluto (1/s): alta, i movimenti degli occhi sono scatti.")]
        [SerializeField, Min(0.1f)] private float speed = 25f;

        [Tooltip("Pausa fra due scatti (secondi, minimo e massimo).")]
        [SerializeField] private Vector2 saccadeInterval = new Vector2(0.4f, 1.6f);

        [Tooltip("Ampiezza degli scatti senza un punto da guardare (gradi).")]
        [SerializeField, Range(0f, 30f)] private float idleSaccade = 8f;

        [Tooltip("Ampiezza degli scatti mentre guarda qualcosa (gradi).")]
        [SerializeField, Range(0f, 10f)] private float focusSaccade = 2f;

        [Tooltip("Renderer della creatura (opzionale): se assegnato, gli occhi vengono scritti solo quando è visibile.")]
        [SerializeField] private Renderer visibilityRenderer;

        private Creature creature;
        private Quaternion[] restLocal;
        private Vector2[] current;
        private Vector2 saccade;
        private float nextSaccadeTime;

        private void Awake()
        {
            creature = GetComponent<Creature>();
            int count = eyes != null ? eyes.Length : 0;
            restLocal = new Quaternion[count];
            current = new Vector2[count];
            for (int i = 0; i < count; i++) restLocal[i] = eyes[i].localRotation;
        }

        private void OnEnable()
        {
            if (current != null)
                for (int i = 0; i < current.Length; i++) current[i] = Vector2.zero;
            saccade = Vector2.zero;
            nextSaccadeTime = 0f;
        }

        private void LateUpdate()
        {
            if (restLocal == null || restLocal.Length == 0) return;
            float now = Time.time;
            bool hasFocus = false;
            Vector3 focus = default;
            if (creature != null) hasFocus = creature.TryGetFocus(out focus);

            if (now >= nextSaccadeTime)
            {
                float amplitude = hasFocus ? focusSaccade : idleSaccade;
                saccade = Random.insideUnitCircle * amplitude;
                nextSaccadeTime = now + Random.Range(saccadeInterval.x, saccadeInterval.y);
            }

            if (visibilityRenderer != null && !visibilityRenderer.isVisible) return;

            float blend = 1f - Mathf.Exp(-speed * Time.deltaTime);
            for (int i = 0; i < eyes.Length; i++)
            {
                Transform eye = eyes[i];
                eye.localRotation = restLocal[i];
                Quaternion restWorld = eye.rotation;
                Vector3 look = restWorld * Vector3.up;
                Quaternion frame = Quaternion.LookRotation(look, transform.up);

                Vector2 target = saccade;
                if (hasFocus)
                {
                    Vector3 local = Quaternion.Inverse(frame) * (focus - eye.position).normalized;
                    target.x += Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                    target.y += -Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg;
                }
                target.x = Mathf.Clamp(target.x, -maxYaw, maxYaw);
                target.y = Mathf.Clamp(target.y, -maxPitch, maxPitch);
                current[i] = Vector2.Lerp(current[i], target, blend);

                Vector3 direction = frame * (Quaternion.Euler(current[i].y, current[i].x, 0f) * Vector3.forward);
                eye.rotation = Quaternion.FromToRotation(look, direction) * restWorld;
            }
        }
    }
}
