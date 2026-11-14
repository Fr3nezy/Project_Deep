using System.Collections.Generic;
using Deeploration.Environment;
using Deeploration.Player;
using Deeploration.Prologue;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deeploration.DevTools
{
    /// <summary>
    /// Menu cheat per test e demo (anche in build): teletrasporto ai DevWarpPoint della scena,
    /// invulnerabilità, ossigeno infinito, ricarica, sblocco del flash. In Editor può far partire
    /// il diver fuori dalla capsula, in piedi su un DevWarpPoint, saltando risveglio e uscita.
    /// Uno per scena, su un oggetto qualsiasi.
    /// </summary>
    public sealed class CheatMenu : MonoBehaviour
    {
        [Header("Partenza (solo in Editor)")]
        [Tooltip("Parte in piedi su startPoint invece di svegliarsi nella capsula. Ignorato in build: la build parte sempre dal risveglio.")]
        [SerializeField] private bool startOutside;
        [SerializeField] private DevWarpPoint startPoint;

        [Header("Menu")]
        [SerializeField] private Key toggleKey = Key.F1;
        [Tooltip("Ferma il tempo (Time.timeScale = 0) finché il menu è aperto.")]
        [SerializeField] private bool pauseWhileOpen = true;

        [Header("Posa")]
        [Tooltip("Layer su cui posare il diver sotto il marker (default: Seafloor).")]
        [SerializeField] private LayerMask groundMask = 1 << 10;
        [Tooltip("Altezza degli occhi in piedi se in scena non c'è un HatchExit da cui leggerla.")]
        [SerializeField] private float fallbackEyeHeight = 1.375f;

        private DiverController diver;
        private CharacterController controller;
        private OxygenSystem oxygen;
        private DiverRespawn respawn;
        private DiverFlashlight flashlight;
        private StarterAssetsInputs input;
        private PlayerWakeUpSequence wakeUp;
        private HatchExit hatchExit;
        private readonly List<DevWarpPoint> points = new List<DevWarpPoint>();

        private bool open;
        private float savedTimeScale = 1f;
        private Vector2 scroll;

        public bool IsOpen => open;

        private void Start()
        {
            diver = FindAnyObjectByType<DiverController>();
            if (diver != null)
            {
                controller = diver.GetComponent<CharacterController>();
                oxygen = diver.GetComponent<OxygenSystem>();
                respawn = diver.GetComponent<DiverRespawn>();
                flashlight = diver.GetComponentInChildren<DiverFlashlight>(true);
                input = diver.GetComponent<StarterAssetsInputs>();
            }
            else
            {
                Debug.LogWarning("[CheatMenu] Nessun DiverController in scena: menu senza effetto.", this);
            }
            wakeUp = FindAnyObjectByType<PlayerWakeUpSequence>();
            hatchExit = FindAnyObjectByType<HatchExit>();

            points.AddRange(FindObjectsByType<DevWarpPoint>(FindObjectsSortMode.None));
            points.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Label, b.Label));

            // Start gira dopo tutti gli Awake: il risveglio ha già messo il diver seduto e lo schermo a nero.
            if (Application.isEditor && startOutside && startPoint != null) WarpTo(startPoint);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame) SetOpen(!open);
        }

        private void OnDisable()
        {
            if (open) SetOpen(false);
        }

        public void SetOpen(bool value)
        {
            if (open == value) return;
            open = value;

            if (pauseWhileOpen)
            {
                if (open)
                {
                    savedTimeScale = Time.timeScale;
                    Time.timeScale = 0f;
                }
                else
                {
                    Time.timeScale = savedTimeScale;
                }
            }

            if (input != null)
            {
                input.cursorLocked = !open;
                input.cursorInputForLook = !open;
                input.look = Vector2.zero;
            }
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }

        /// <summary>Porta il diver in piedi sul marker, posato sul fondale, e ne fa il checkpoint del respawn.</summary>
        public void WarpTo(DevWarpPoint point)
        {
            if (diver == null || point == null) return;

            if (wakeUp != null) wakeUp.Skip();

            Vector3 position = point.transform.position;
            if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f, groundMask, QueryTriggerInteraction.Ignore))
            {
                position = hit.point;
                // Il pivot del diver non coincide con la base del capsule (stessa correzione di HatchExit).
                if (controller != null) position.y += controller.height * 0.5f - controller.center.y + controller.skinWidth;
            }

            // shortcut: un'uscita dal portello in corso continua a muovere il diver; non si teletrasporta durante quei 2.5 s.
            diver.PlaceStanding(position, point.transform.eulerAngles.y, hatchExit != null ? hatchExit.EyeHeight : fallbackEyeHeight);

            // Senza checkpoint il respawn riporterebbe il diver sul sedile della capsula, dove non si muove.
            if (respawn != null) respawn.SetCheckpoint(point.transform);
        }

        private void OnGUI()
        {
            if (!open) return;

            GUILayout.BeginArea(new Rect(20f, 20f, 280f, Screen.height - 40f), GUI.skin.box);
            GUILayout.Label($"Cheat  ({toggleKey} per chiudere)");

            if (oxygen != null)
            {
                oxygen.Invulnerable = GUILayout.Toggle(oxygen.Invulnerable, " Invulnerabile");
                oxygen.InfiniteOxygen = GUILayout.Toggle(oxygen.InfiniteOxygen, " Ossigeno infinito");
                if (GUILayout.Button($"Ricarica O₂ ({oxygen.NormalizedOxygen:P0})")) oxygen.SetNormalized(1f);
            }

            if (flashlight != null)
            {
                GUI.enabled = !flashlight.FlashUnlocked;
                if (GUILayout.Button(flashlight.FlashUnlocked ? "Flash sbloccato" : "Sblocca flash")) flashlight.UnlockFlash();
                GUI.enabled = true;
            }

            GUILayout.Space(8f);
            GUILayout.Label("Teletrasporto");
            if (points.Count == 0) GUILayout.Label("Nessun DevWarpPoint in scena.");
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (DevWarpPoint point in points)
            {
                if (point != null && GUILayout.Button(point.Label))
                {
                    WarpTo(point);
                    SetOpen(false);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void OnDrawGizmos()
        {
            if (startOutside && startPoint != null)
                DevWarpPoint.DrawMarker(startPoint.transform, new Color(0.3f, 0.75f, 1f), "PARTENZA · " + startPoint.Label);
        }

#if UNITY_EDITOR
        /// <summary>Dal menu contestuale di un DevWarpPoint: lo imposta come partenza e attiva startOutside.</summary>
        public void SetStartPoint(DevWarpPoint point)
        {
            UnityEditor.Undo.RecordObject(this, "Parti da qui");
            startPoint = point;
            startOutside = true;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
