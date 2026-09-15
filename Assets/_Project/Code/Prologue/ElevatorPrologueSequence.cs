using System.Collections;
using System.Collections.Generic;
using Deeploration.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deeploration.Prologue
{
    /// <summary>
    /// Gestisce la sequenza di discesa dell'ascensore: blocco locomozione con freelook,
    /// scorrimento cubi/treadmill dall'oblò, screenshake procedurale continuo e da impatto,
    /// sincronizzazione dei sottotitoli a battute, dissolvenza a nero e caricamento del gameplay.
    /// </summary>
    public class ElevatorPrologueSequence : MonoBehaviour
    {
        [System.Serializable]
        public class DialogueCue
        {
            public float timestamp;
            public float duration = 4.0f;
            public SubtitleLine line;
            [System.NonSerialized] public bool played;
        }

        [Header("Player & Controlli")]
        [SerializeField] private DiverController diver;
        [SerializeField] private Transform cameraTarget;

        [Header("UI & Sottotitoli")]
        [SerializeField] private SubtitlePanel subtitlePanel;
        [SerializeField] private CanvasGroup fadeOverlay;
        [SerializeField] private List<DialogueCue> dialogueCues = new List<DialogueCue>();

        [Header("Treadmill Cubi Esterni")]
        [SerializeField] private Transform cubesParent;
        [SerializeField] private int cubeCount = 14;
        [SerializeField] private float cubeSpeed = 9.0f;
        [SerializeField] private bool invertCubeDirection = false;
        [SerializeField] private Vector3 cubeBoundsMin = new Vector3(-3.0f, -2.5f, -14.0f);
        [SerializeField] private Vector3 cubeBoundsMax = new Vector3(3.0f, 6.5f, -7.0f);
        [SerializeField] private Material cubeMaterial;

        [Header("Screenshake")]
        [SerializeField, Range(0f, 0.2f)] private float descentShakePos = 0.025f;
        [SerializeField, Range(0f, 2f)] private float descentShakeRot = 0.45f;
        [SerializeField] private float descentShakeFreq = 14.0f;
        [SerializeField] private float impactTimestamp = 30.0f;
        [SerializeField] private float impactDuration = 2.0f;
        [SerializeField] private float impactShakePos = 0.40f;
        [SerializeField] private float impactShakeRot = 4.0f;

        [Header("Transizione Scena")]
        [SerializeField] private float fadeDuration = 1.8f;
        [SerializeField] private string nextSceneName = "GameplayLoop_Blockout";

        private float timer;
        private bool impacted;
        private bool fading;
        private float currentImpactShake;
        private Vector3 initialCamLocalPos;
        private Quaternion initialCamLocalRot;
        private List<Transform> spawnedCubes = new List<Transform>();

        private void Awake()
        {
            if (diver == null) diver = Object.FindFirstObjectByType<DiverController>();
            if (cameraTarget == null && diver != null)
            {
                var camRoot = diver.transform.Find("PlayerCameraRoot");
                if (camRoot != null) cameraTarget = camRoot;
            }

            if (cameraTarget != null)
            {
                initialCamLocalPos = cameraTarget.localPosition;
                initialCamLocalRot = cameraTarget.localRotation;
            }

            if (fadeOverlay != null)
            {
                fadeOverlay.alpha = 0f;
            }
        }

        private void Start()
        {
            if (diver != null)
            {
                diver.LockMovement = true;
            }

            SetupTreadmillCubes();
        }

        private void Update()
        {
            timer += Time.deltaTime;

            UpdateDialogue();
            UpdateTreadmillCubes();
            UpdateShake();

            if (!impacted && timer >= impactTimestamp)
            {
                TriggerImpact();
            }
        }

        private void LateUpdate()
        {
            ApplyCameraShake();
        }

        private void SetupTreadmillCubes()
        {
            if (cubesParent == null)
            {
                var existingParent = GameObject.Find("Treadmill_Cubes");
                if (existingParent != null)
                {
                    cubesParent = existingParent.transform;
                }
                else
                {
                    var parentGo = new GameObject("Treadmill_Cubes");
                    cubesParent = parentGo.transform;
                }
            }

            if (spawnedCubes.Count == 0)
            {
                // Prioritizza i cubi già presenti fisicamente nella scena
                if (cubesParent != null && cubesParent.childCount > 0)
                {
                    foreach (Transform child in cubesParent)
                    {
                        spawnedCubes.Add(child);
                    }
                }
                else
                {
                    for (int i = 0; i < cubeCount; i++)
                    {
                        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cube.name = $"Passing_Cube_{i:00}";
                        cube.transform.SetParent(cubesParent, false);

                        // Distribuisci i cubi nello spazio dell'oblò
                        float rx = Random.Range(cubeBoundsMin.x, cubeBoundsMax.x);
                        float ry = Random.Range(cubeBoundsMin.y, cubeBoundsMax.y);
                        float rz = Random.Range(cubeBoundsMin.z, cubeBoundsMax.z);
                        cube.transform.position = new Vector3(rx, ry, rz);

                        float scale = Random.Range(0.4f, 1.1f);
                        cube.transform.localScale = new Vector3(scale, scale, scale);

                        // Rimuovi collider per evitare conflitti fisici con l'ascensore
                        var col = cube.GetComponent<Collider>();
                        if (col != null) Destroy(col);

                        if (cubeMaterial != null)
                        {
                            var mr = cube.GetComponent<MeshRenderer>();
                            if (mr != null) mr.sharedMaterial = cubeMaterial;
                        }

                        spawnedCubes.Add(cube.transform);
                    }
                }
            }
        }

        private void UpdateTreadmillCubes()
        {
            if (spawnedCubes.Count == 0 || (impacted && currentImpactShake <= 0.01f)) return;

            float dir = invertCubeDirection ? -1.0f : 1.0f;
            float movement = dir * cubeSpeed * Time.deltaTime;

            foreach (var cube in spawnedCubes)
            {
                if (cube == null) continue;

                Vector3 pos = cube.position;
                pos.y += movement;

                // Wrap-around quando escono dai limiti
                if (!invertCubeDirection && pos.y > cubeBoundsMax.y)
                {
                    pos.y = cubeBoundsMin.y;
                    pos.x = Random.Range(cubeBoundsMin.x, cubeBoundsMax.x);
                    pos.z = Random.Range(cubeBoundsMin.z, cubeBoundsMax.z);
                }
                else if (invertCubeDirection && pos.y < cubeBoundsMin.y)
                {
                    pos.y = cubeBoundsMax.y;
                    pos.x = Random.Range(cubeBoundsMin.x, cubeBoundsMax.x);
                    pos.z = Random.Range(cubeBoundsMin.z, cubeBoundsMax.z);
                }

                cube.position = pos;
            }
        }

        private void UpdateDialogue()
        {
            if (subtitlePanel == null) return;

            foreach (var cue in dialogueCues)
            {
                if (!cue.played && cue.line != null && timer >= cue.timestamp)
                {
                    cue.played = true;
                    subtitlePanel.Show(cue.line);
                    StartCoroutine(ClearSubtitleAfter(cue.duration));
                }
            }
        }

        private IEnumerator ClearSubtitleAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            if (subtitlePanel != null)
            {
                subtitlePanel.Clear();
            }
        }

        private void UpdateShake()
        {
            if (impacted && currentImpactShake > 0f)
            {
                currentImpactShake = Mathf.MoveTowards(currentImpactShake, 0f, (1f / impactDuration) * Time.deltaTime);
            }
        }

        private void ApplyCameraShake()
        {
            if (cameraTarget == null) return;

            // 1. Shake leggero continuo della discesa
            float timeCoeff = Time.time * descentShakeFreq;
            float noiseX = (Mathf.PerlinNoise(timeCoeff, 0f) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(0f, timeCoeff) - 0.5f) * 2f;
            float noiseZ = (Mathf.PerlinNoise(timeCoeff, timeCoeff) - 0.5f) * 2f;

            Vector3 posOffset = new Vector3(noiseX, noiseY, noiseZ) * descentShakePos;
            Vector3 rotOffset = new Vector3(noiseY, noiseX, noiseZ) * descentShakeRot;

            // 2. Shake violento dell'impatto sul finale
            if (currentImpactShake > 0f)
            {
                float impNoiseX = (Mathf.PerlinNoise(Time.time * 30f, 10f) - 0.5f) * 2f;
                float impNoiseY = (Mathf.PerlinNoise(20f, Time.time * 30f) - 0.5f) * 2f;
                float impNoiseZ = (Mathf.PerlinNoise(Time.time * 30f, Time.time * 30f) - 0.5f) * 2f;

                posOffset += new Vector3(impNoiseX, impNoiseY, impNoiseZ) * (impactShakePos * currentImpactShake);
                rotOffset += new Vector3(impNoiseY, impNoiseX, impNoiseZ) * (impactShakeRot * currentImpactShake);
            }

            cameraTarget.localPosition += posOffset;
            cameraTarget.localRotation *= Quaternion.Euler(rotOffset);
        }

        public void TriggerImpact()
        {
            if (impacted) return;
            impacted = true;
            currentImpactShake = 1.0f;

            if (!fading)
            {
                StartCoroutine(FadeAndLoadSceneRoutine());
            }
        }

        private IEnumerator FadeAndLoadSceneRoutine()
        {
            fading = true;

            // Attendi brevemente durante l'apice dell'impatto prima di avviare il fade
            yield return new WaitForSeconds(0.6f);

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                if (fadeOverlay != null)
                {
                    fadeOverlay.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                }
                yield return null;
            }

            if (fadeOverlay != null)
            {
                fadeOverlay.alpha = 1f;
            }

            if (!string.IsNullOrEmpty(nextSceneName))
            {
                SceneManager.LoadScene(nextSceneName);
            }
        }
    }
}
