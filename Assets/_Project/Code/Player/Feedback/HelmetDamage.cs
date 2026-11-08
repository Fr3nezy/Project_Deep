using System;
using System.Collections.Generic;
using Deeploration.Creatures;
using UnityEngine;

namespace Deeploration.Player
{
    /// <summary>
    /// Danni al vetro del casco (bozza): a ogni morso ricevuto da <see cref="DiverBiteReceiver"/> compare una crepa
    /// sulla visiera, nel punto colpito (centro della visuale con un po' di variazione). Le crepe già presenti si
    /// allargano a ogni morso; al morso numero <c>criticalBites</c> suona l'allarme e da quel momento il vetro
    /// scricchiola. Le crepe sono quad figli del casco, quindi seguono il suo ondeggiare, e restano fino a
    /// <see cref="ResetDamage"/> (la riparazione alla stazione di aggancio, non ancora collegata). Solo visivo e sonoro:
    /// nessun effetto su ossigeno o stress oltre a quelli del morso.
    /// </summary>
    public sealed class HelmetDamage : MonoBehaviour
    {
        [Header("Riferimenti")]
        [Tooltip("Chi riceve i morsi (PlayerCapsule). Vuoto = cercato in scena all'avvio.")]
        [SerializeField] private DiverBiteReceiver biteReceiver;

        [Tooltip("Camera del sub. Vuoto = la camera fra i genitori.")]
        [SerializeField] private Camera viewCamera;

        [Tooltip("Materiale trasparente delle crepe, con atlante 2x2 (T_HelmetCracks).")]
        [SerializeField] private Material crackMaterial;

        [Header("Crepe")]
        [Tooltip("Lato (m) di una crepa nuova sul vetro.")]
        [SerializeField, Min(0.01f)] private float crackSize = 0.09f;

        [Tooltip("Variazione casuale della dimensione (moltiplicatore minimo, massimo).")]
        [SerializeField] private Vector2 sizeVariation = new Vector2(0.8f, 1.25f);

        [Tooltip("Raggio (viewport 0..1) attorno al centro in cui cade il punto d'impatto.")]
        [SerializeField, Range(0f, 0.5f)] private float impactJitter = 0.12f;

        [Tooltip("Di quanto si allargano le crepe già presenti a ogni nuovo morso.")]
        [SerializeField, Min(1f)] private float growPerBite = 1.25f;

        [Tooltip("Allargamento massimo di una crepa rispetto alla sua dimensione iniziale.")]
        [SerializeField, Min(1f)] private float maxGrowth = 1.8f;

        // shortcut: distanza fissa invece di un raggio sulla visiera (mesh dell'FBX non leggibile a runtime); se le
        // crepe ai lati si staccano dal vetro, attivare Read/Write su Helmet.fbx e intersecare la visiera.
        [Tooltip("Distanza (m) delle crepe dalla camera lungo il raggio: appena dentro la visiera (0.407 m sull'asse).")]
        [SerializeField, Min(0.15f)] private float glassDistance = 0.39f;

        [Header("Stato critico")]
        [Tooltip("Morsi dopo i quali il vetro è critico: allarme e scricchiolii.")]
        [SerializeField, Min(1)] private int criticalBites = 3;

        [Tooltip("Secondi fra due scricchiolii del vetro dal secondo morso (minimo, massimo).")]
        [SerializeField] private Vector2 creakInterval = new Vector2(5f, 12f);

        [Header("Suoni (vuoti = sintetici)")]
        [SerializeField] private AudioClip crackClip;
        [SerializeField] private AudioClip creakClip;
        [SerializeField] private AudioClip alarmClip;
        [SerializeField, Range(0f, 1f)] private float crackVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float creakVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float alarmVolume = 0.5f;

        private sealed class Crack
        {
            public Transform transform;
            public float baseScale;
            public float growth = 1f;
        }

        private readonly List<Crack> cracks = new List<Crack>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private AudioSource source;
        private int bites;
        private float nextCreak = float.PositiveInfinity;
        private static AudioClip synthCrack, synthCreak, synthAlarm;

        /// <summary>Morsi incassati dal vetro dall'ultima riparazione.</summary>
        public int Bites => bites;

        /// <summary>True dal morso numero criticalBites.</summary>
        public bool Critical => bites >= criticalBites;

        /// <summary>Lanciato a ogni crepa nuova e alla riparazione, con il numero di morsi.</summary>
        public event Action<int> OnDamageChanged;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = GetComponentInParent<Camera>();
            if (biteReceiver == null) biteReceiver = FindFirstObjectByType<DiverBiteReceiver>();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // dentro il casco
            if (synthCrack == null)
            {
                synthCrack = SynthCrack();
                synthCreak = SynthCreak();
                synthAlarm = SynthAlarm();
            }
            if (crackMaterial == null) Debug.LogWarning($"{name}: HelmetDamage senza crackMaterial, nessuna crepa visibile.", this);
        }

        private void OnEnable()
        {
            if (biteReceiver != null) biteReceiver.OnBitten += HandleBite;
        }

        private void OnDisable()
        {
            if (biteReceiver != null) biteReceiver.OnBitten -= HandleBite;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < meshes.Count; i++) Destroy(meshes[i]);
        }

        private void Update()
        {
            if (bites < 2 || Time.time < nextCreak) return;
            nextCreak = Time.time + UnityEngine.Random.Range(creakInterval.x, creakInterval.y) / (Critical ? 2f : 1f);
            source.pitch = UnityEngine.Random.Range(0.85f, 1.15f);
            source.PlayOneShot(creakClip != null ? creakClip : synthCreak, creakVolume * (Critical ? 1f : 0.6f));
        }

        private void HandleBite(Creature biter) => AddCrack();

        /// <summary>Crepa nuova nel punto d'impatto; le crepe già presenti si allargano.</summary>
        public void AddCrack()
        {
            bites++;
            for (int i = 0; i < cracks.Count; i++)
            {
                Crack old = cracks[i];
                old.growth = Mathf.Min(old.growth * growPerBite, maxGrowth);
                old.transform.localScale = Vector3.one * (old.baseScale * old.growth);
            }

            if (crackMaterial != null && viewCamera != null)
            {
                Vector2 jitter = UnityEngine.Random.insideUnitCircle * impactJitter;
                Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f + jitter.x, 0.5f + jitter.y, 0f));
                // dall'occhio, non da ray.origin: ViewportPointToRay parte dal piano near
                cracks.Add(CreateCrack(viewCamera.transform.position + ray.direction * glassDistance, ray.direction));
            }

            source.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
            source.PlayOneShot(crackClip != null ? crackClip : synthCrack, crackVolume);
            if (bites == criticalBites) source.PlayOneShot(alarmClip != null ? alarmClip : synthAlarm, alarmVolume);
            if (bites == 2 || bites == criticalBites)
                nextCreak = Time.time + UnityEngine.Random.Range(creakInterval.x, creakInterval.y) * 0.5f;
            OnDamageChanged?.Invoke(bites);
        }

        /// <summary>Vetro nuovo: toglie le crepe e azzera i morsi (per la riparazione alla stazione).</summary>
        public void ResetDamage()
        {
            for (int i = 0; i < cracks.Count; i++)
                if (cracks[i].transform != null) Destroy(cracks[i].transform.gameObject);
            for (int i = 0; i < meshes.Count; i++) Destroy(meshes[i]);
            cracks.Clear();
            meshes.Clear();
            bites = 0;
            nextCreak = float.PositiveInfinity;
            OnDamageChanged?.Invoke(0);
        }

        private Crack CreateCrack(Vector3 position, Vector3 direction)
        {
            var go = new GameObject($"Crack_{cracks.Count}");
            Transform parent = transform;
            go.transform.SetParent(parent, false);
            // il quad guarda la camera: il suo +Z va lungo il raggio, la faccia visibile resta verso chi guarda
            go.transform.SetPositionAndRotation(position,
                Quaternion.LookRotation(direction, viewCamera.transform.up) * Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f)));
            float scale = crackSize * UnityEngine.Random.Range(sizeVariation.x, sizeVariation.y);
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = AtlasQuad(UnityEngine.Random.Range(0, 4));
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = crackMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            go.layer = parent.gameObject.layer;
            return new Crack { transform = go.transform, baseScale = scale };
        }

        // Quad unitario centrato con le UV di una delle quattro crepe dell'atlante 2x2.
        private Mesh AtlasQuad(int tile)
        {
            float u = (tile % 2) * 0.5f, v = (tile / 2) * 0.5f;
            var mesh = new Mesh
            {
                name = "HelmetCrack",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
                },
                uv = new[]
                {
                    new Vector2(u, v), new Vector2(u + 0.5f, v), new Vector2(u, v + 0.5f), new Vector2(u + 0.5f, v + 0.5f)
                },
                triangles = new[] { 0, 2, 1, 2, 3, 1 }
            };
            mesh.RecalculateNormals();
            meshes.Add(mesh);
            return mesh;
        }

        // Schiocco di vetro: clic secco, rumore acuto e due risonanze che decadono, più un tonfo basso.
        private static AudioClip SynthCrack()
        {
            const int rate = 44100;
            int samples = (int)(rate * 0.7f);
            var data = new float[samples];
            var random = new System.Random(71);
            float hp = 0f, prev = 0f, peak = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)rate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                hp = 0.9f * (hp + noise - prev); // passa-alto: rumore sottile, vetroso
                prev = noise;
                float click = t < 0.004f ? noise * 2f : 0f;
                float hiss = hp * Mathf.Exp(-t / 0.05f);
                float ring = (Mathf.Sin(2f * Mathf.PI * 3150f * t) + 0.7f * Mathf.Sin(2f * Mathf.PI * 4720f * t)) * Mathf.Exp(-t / 0.12f) * 0.35f;
                float thump = Mathf.Sin(2f * Mathf.PI * 90f * t) * Mathf.Exp(-t / 0.06f) * 0.6f;
                float value = click + hiss + ring + thump;
                data[i] = value;
                peak = Mathf.Max(peak, Mathf.Abs(value));
            }
            return Normalise("Helmet_Crack", data, peak, rate);
        }

        // Scricchiolio: impulsi di attrito ("stick-slip") che accelerano, filtrati, con inviluppo lento.
        private static AudioClip SynthCreak()
        {
            const int rate = 44100;
            int samples = (int)(rate * 1.3f);
            var data = new float[samples];
            var random = new System.Random(72);
            float phase = 0f, lp = 0f, peak = 0f, ping = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)rate;
                float rateHz = Mathf.Lerp(18f, 45f, t / 1.3f) * (1f + 0.2f * Mathf.Sin(t * 7f));
                phase += rateHz / rate;
                if (phase >= 1f) { phase -= 1f; ping = 1f; }
                ping *= 0.993f;
                float noise = (float)random.NextDouble() * 2f - 1f;
                lp += (noise * ping - lp) * 0.25f;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.3f));
                float value = (lp + ping * Mathf.Sin(2f * Mathf.PI * 1400f * t) * 0.15f) * env;
                data[i] = value;
                peak = Mathf.Max(peak, Mathf.Abs(value));
            }
            return Normalise("Helmet_Creak", data, peak, rate);
        }

        // Allarme: tre bip brevi a 1.1 kHz.
        private static AudioClip SynthAlarm()
        {
            const int rate = 44100;
            int samples = (int)(rate * 0.75f);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)rate;
                float local = t % 0.25f;
                float gate = local < 0.12f ? Mathf.Clamp01(local / 0.005f) * Mathf.Clamp01((0.12f - local) / 0.005f) : 0f;
                data[i] = gate * (Mathf.Sin(2f * Mathf.PI * 1100f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * 2200f * t));
            }
            return Normalise("Helmet_Alarm", data, 1.3f, rate);
        }

        private static AudioClip Normalise(string clipName, float[] data, float peak, int rate)
        {
            if (peak > 0f)
                for (int i = 0; i < data.Length; i++) data[i] *= 0.9f / peak;
            AudioClip clip = AudioClip.Create(clipName, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
