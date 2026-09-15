#if UNITY_EDITOR
using System.IO;
using Deeploration.Interaction;
using Deeploration.Player;
using Deeploration.Prologue;
using StarterAssets;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;

namespace Deeploration.Editor
{
    public static class PrologueWave1Builder
    {
        private const string SCENE_PATH = "Assets/_Project/Prototype/GameplayLoop_Blockout.unity";
        private const string PROLOGUE_ROOT = "Assets/_Project/Prologue";
        private const string TIMELINES_ROOT = "Assets/_Project/Prologue/Timelines";
        private const string LINES_ROOT = "Assets/_Project/Prologue/Lines";
        private const string SIGNALS_ROOT = "Assets/_Project/Prologue/Signals";
        private const string ANIMS_ROOT = "Assets/_Project/Prologue/Animations";
        private const string LOC_ROOT = "Assets/_Project/Localization";

        [MenuItem("Deeplonauts/Prologue/Build Wave 1 Prologue Setup")]
        public static void BuildWave1()
        {
            if (File.Exists(SCENE_PATH))
            {
                if (!EditorUtility.DisplayDialog("Attenzione: Scena esistente",
                    $"La scena '{SCENE_PATH}' esiste già.\nRigenerare il setup Wave 1 potrebbe reimpostare director e blocking.\n\nVuoi procedere comunque?",
                    "Sì, procedi", "Annulla"))
                {
                    Debug.LogWarning("[PrologueWave1Builder] Operazione annullata per preservare la scena esistente.");
                    return;
                }
            }

            EnsureDirectories();
            SetupLocalizationAndLines();
            SignalAsset introSignal = SetupSignals();
            AnimationClip camAnimClip = SetupIntroCameraAnimation();
            TimelineAsset introTimeline = CreateIntroTimeline(introSignal, camAnimClip);
            TimelineAsset exitTimeline = CreateExitTimeline();
            ConfigureScene(introTimeline, exitTimeline, introSignal);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#10b981>[PrologueWave1Builder]</color> Wave 1 setup completato con successo!");
        }

        private static void EnsureDirectories()
        {
            string[] dirs = { PROLOGUE_ROOT, TIMELINES_ROOT, LINES_ROOT, SIGNALS_ROOT, ANIMS_ROOT, LOC_ROOT };
            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
            AssetDatabase.Refresh();
        }

        private static void SetupLocalizationAndLines()
        {
            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings == null)
            {
                string settingsPath = $"{LOC_ROOT}/LocalizationSettings.asset";
                settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(settingsPath);
                if (settings == null)
                {
                    settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                    AssetDatabase.CreateAsset(settings, settingsPath);
                }
                LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            }

            Locale itLocale = LocalizationEditorSettings.GetLocale(new LocaleIdentifier("it"));
            if (itLocale == null)
            {
                itLocale = Locale.CreateLocale("it");
                itLocale.name = "Italian (it)";
                string itPath = $"{LOC_ROOT}/Locale_it.asset";
                AssetDatabase.CreateAsset(itLocale, itPath);
                LocalizationEditorSettings.AddLocale(itLocale);
            }

            Locale enLocale = LocalizationEditorSettings.GetLocale(new LocaleIdentifier("en"));
            if (enLocale == null)
            {
                enLocale = Locale.CreateLocale("en");
                enLocale.name = "English (en)";
                string enPath = $"{LOC_ROOT}/Locale_en.asset";
                AssetDatabase.CreateAsset(enLocale, enPath);
                LocalizationEditorSettings.AddLocale(enLocale);
            }

            LocalizationSettings.SelectedLocale = itLocale;

            var collection = LocalizationEditorSettings.GetStringTableCollection("Prologue");
            if (collection == null)
            {
                collection = LocalizationEditorSettings.CreateStringTableCollection("Prologue", LOC_ROOT, new[] { itLocale, enLocale });
            }

            var itTable = collection.GetTable(itLocale.Identifier) as StringTable;
            var enTable = collection.GetTable(enLocale.Identifier) as StringTable;

            void SetEntry(string key, string itText, string enText)
            {
                if (itTable != null)
                {
                    var entry = itTable.GetEntry(key) ?? itTable.AddEntry(key, itText);
                    entry.Value = itText;
                }
                if (enTable != null)
                {
                    var entry = enTable.GetEntry(key) ?? enTable.AddEntry(key, enText);
                    entry.Value = enText;
                }
            }

            SetEntry("UI/prologue.hatch.open", "Apri Portello", "Open Hatch");
            SetEntry("Prologue/speaker.bathy", "B.A.T.H.Y.", "B.A.T.H.Y.");
            SetEntry("Prologue/beat1", "Sistemi avviati. Discesa verso Fossa di Cayman.", "Systems online. Descending into Cayman Trench.");
            SetEntry("Prologue/beat2", "Rilevata perturbazione idrodinamica minore.", "Minor hydrodynamic disturbance detected.");
            SetEntry("Prologue/beat3", "Allarme: impatti multipli sullo scafo esterno!", "Warning: multiple impacts on outer hull!");
            SetEntry("Prologue/beat4", "Integrità cavo primario compromessa. Tensione critica.", "Primary cable integrity compromised. Critical tension.");
            SetEntry("Prologue/beat5", "Rottura cavo secondario. Cedimento strutturale.", "Secondary cable failure. Structural collapse.");
            SetEntry("Prologue/beat6", "Frenata d'emergenza... impatto imminente!", "Emergency brake... brace for impact!");
            SetEntry("Prologue/beat7", "Riavvio di emergenza... Profondità 8.420m. Integrità scafandro confermata.", "Emergency reboot... Depth 8,420m. Suit integrity confirmed.");

            if (itTable != null) EditorUtility.SetDirty(itTable);
            if (enTable != null) EditorUtility.SetDirty(enTable);
            if (collection != null) EditorUtility.SetDirty(collection);

            // Crea o aggiorna SubtitleLine per i 7 beat
            for (int i = 1; i <= 7; i++)
            {
                string linePath = $"{LINES_ROOT}/Subtitle_Beat_{i}.asset";
                SubtitleLine line = AssetDatabase.LoadAssetAtPath<SubtitleLine>(linePath);
                if (line == null)
                {
                    line = ScriptableObject.CreateInstance<SubtitleLine>();
                    AssetDatabase.CreateAsset(line, linePath);
                }
                line.speaker.SetReference("Prologue", "Prologue/speaker.bathy");
                line.text.SetReference("Prologue", $"Prologue/beat{i}");
                EditorUtility.SetDirty(line);
            }
        }

        private static SignalAsset SetupSignals()
        {
            string signalPath = $"{SIGNALS_ROOT}/OnIntroFinished.signal";
            SignalAsset signal = AssetDatabase.LoadAssetAtPath<SignalAsset>(signalPath);
            if (signal == null)
            {
                signal = ScriptableObject.CreateInstance<SignalAsset>();
                AssetDatabase.CreateAsset(signal, signalPath);
            }
            return signal;
        }

        private static AnimationClip SetupIntroCameraAnimation()
        {
            string clipPath = $"{ANIMS_ROOT}/IntroCam_Animation.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                clip.name = "IntroCam_Animation";
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            clip.ClearCurves();

            var px = new AnimationCurve();
            var py = new AnimationCurve();
            var pz = new AnimationCurve();
            var rx = new AnimationCurve();
            var ry = new AnimationCurve();
            var rz = new AnimationCurve();

            void AddFrame(float t, float x, float y, float z, float rotX, float rotY, float rotZ)
            {
                px.AddKey(new Keyframe(t, x));
                py.AddKey(new Keyframe(t, y));
                pz.AddKey(new Keyframe(t, z));
                rx.AddKey(new Keyframe(t, rotX));
                ry.AddKey(new Keyframe(t, rotY));
                rz.AddKey(new Keyframe(t, rotZ));
            }

            // Beat 1: 0s - 12s (Discesa calma, sguardo iniziale verso le luci del soffitto poi livellato)
            AddFrame(0f, 0.27f, 1.67f, -0.37f, -15f, -0.24f, -11.33f);
            AddFrame(5f, 0.27f, 1.67f, -0.37f, 0f, -0.24f, -11.33f);
            AddFrame(8f, 0.27f, 1.67f, -0.37f, -6f, 4f, -11.33f);
            AddFrame(12f, 0.27f, 1.67f, -0.37f, 0f, -0.24f, -11.33f);

            // Beat 2: 12s - 23s (Perturbazione idrodinamica minore, vibrazioni sottili)
            AddFrame(14f, 0.29f, 1.66f, -0.36f, 1.5f, 1f, -9.5f);
            AddFrame(16f, 0.25f, 1.68f, -0.38f, -1f, -1.5f, -13f);
            AddFrame(19f, 0.28f, 1.66f, -0.36f, 2f, 0.5f, -10f);
            AddFrame(23f, 0.27f, 1.67f, -0.37f, 0f, -0.24f, -11.33f);

            // Beat 3: 23s - 34s (Impatti violenti sullo scafo esterno, scossoni bruschi)
            AddFrame(24f, 0.15f, 1.62f, -0.42f, 5f, -8f, -4f);
            AddFrame(24.4f, 0.35f, 1.70f, -0.33f, -4f, 6f, -16f);
            AddFrame(27f, 0.12f, 1.58f, -0.45f, 8f, -10f, -2f);
            AddFrame(27.5f, 0.32f, 1.69f, -0.34f, -3f, 5f, -15f);
            AddFrame(30f, 0.18f, 1.63f, -0.40f, 4f, -5f, -7f);
            AddFrame(34f, 0.27f, 1.67f, -0.37f, -20f, 0f, -11.33f);

            // Beat 4: 34s - 45s (Tensione critica cavo primario, sguardo verso il soffitto e vibrazioni ad alta frequenza)
            AddFrame(37f, 0.28f, 1.65f, -0.37f, -25f, 2f, -9f);
            AddFrame(40f, 0.26f, 1.68f, -0.36f, -24f, -2f, -13f);
            AddFrame(44f, 0.27f, 1.67f, -0.37f, -15f, 0f, -11.33f);

            // Beat 5: 45s - 56s (Rottura cavo secondario, caduta libera e disorientamento)
            AddFrame(46f, 0.27f, 1.82f, -0.37f, 12f, 4f, -5f);
            AddFrame(48f, 0.38f, 1.48f, -0.32f, -14f, 10f, -20f);
            AddFrame(51f, 0.18f, 1.58f, -0.40f, 8f, -12f, -4f);
            AddFrame(55f, 0.27f, 1.40f, -0.37f, 14f, 0f, -11.33f);

            // Beat 6: 56s - 66s (Frenata di emergenza e schianto violento sul fondale)
            AddFrame(57f, 0.25f, 1.50f, -0.35f, 6f, 4f, -15f);
            AddFrame(59f, 0.29f, 1.35f, -0.39f, -8f, -6f, -8f);
            AddFrame(60f, 0.38f, 0.35f, -0.25f, 24f, -25f, 35f);
            AddFrame(63f, 0.38f, 0.35f, -0.25f, 24f, -25f, 35f);
            AddFrame(66f, 0.38f, 0.35f, -0.25f, 23f, -24f, 34f);

            // Beat 7: 66s - 78s (Riavvio scafandro, rialzarsi da terra, visuale perfettamente allineata alla posa iniziale del diver)
            AddFrame(68f, 0.35f, 0.45f, -0.28f, 18f, -18f, 24f);
            AddFrame(71f, 0.32f, 0.95f, -0.32f, 10f, -10f, 10f);
            AddFrame(74f, 0.29f, 1.42f, -0.35f, 4f, -4f, -4f);
            AddFrame(77f, 0.27f, 1.67f, -0.37f, 1.18f, -0.24f, -11.33f);
            AddFrame(78f, 0.27f, 1.67f, -0.37f, 1.18f, -0.24f, -11.33f);

            clip.SetCurve("", typeof(Transform), "m_LocalPosition.x", px);
            clip.SetCurve("", typeof(Transform), "m_LocalPosition.y", py);
            clip.SetCurve("", typeof(Transform), "m_LocalPosition.z", pz);
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.x", rx);
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.y", ry);
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.z", rz);

            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static TimelineAsset CreateIntroTimeline(SignalAsset introSignal, AnimationClip camAnimClip)
        {
            string path = $"{TIMELINES_ROOT}/Intro_Blockout.playable";
            TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
            if (timeline == null)
            {
                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                AssetDatabase.CreateAsset(timeline, path);
            }

            foreach (var output in timeline.GetOutputTracks())
            {
                timeline.DeleteTrack(output);
            }

            // 1. Cinemachine Track con 2 shot: Intro Camera animata (0-78s) con blend finale verso PlayerFollowCamera (74-78s)
            var cineTrack = timeline.CreateTrack<global::CinemachineTrack>(null, "CinemachineTrack");
            var shot1Clip = cineTrack.CreateClip<global::CinemachineShot>();
            shot1Clip.start = 0.0;
            shot1Clip.duration = 78.0;
            shot1Clip.easeOutDuration = 4.0;
            var shot1 = shot1Clip.asset as global::CinemachineShot;
            if (shot1 != null)
                shot1.VirtualCamera.exposedName = System.Guid.NewGuid().ToString();

            var shot2Clip = cineTrack.CreateClip<global::CinemachineShot>();
            shot2Clip.start = 74.0;
            shot2Clip.duration = 4.0;
            shot2Clip.easeInDuration = 4.0;
            var shot2 = shot2Clip.asset as global::CinemachineShot;
            if (shot2 != null)
                shot2.VirtualCamera.exposedName = System.Guid.NewGuid().ToString();

            // 2. Animation Track per Prologue_IntroCamera
            var animTrack = timeline.CreateTrack<AnimationTrack>(null, "IntroCamTrack");
            var animTimelineClip = animTrack.CreateClip(camAnimClip);
            animTimelineClip.start = 0.0;
            animTimelineClip.duration = 78.0;

            // 3. Subtitle Track nativa sincronizzata per i 7 beat
            var subTrack = timeline.CreateTrack<SubtitleTrack>(null, "Subtitles");
            double[] starts = { 2.0, 13.0, 24.0, 35.0, 46.0, 57.0, 67.0 };
            double[] durations = { 8.0, 8.0, 8.0, 8.0, 8.0, 7.0, 9.0 };

            for (int i = 1; i <= 7; i++)
            {
                var subClipItem = subTrack.CreateClip<SubtitleClip>();
                subClipItem.start = starts[i - 1];
                subClipItem.duration = durations[i - 1];

                var asset = subClipItem.asset as SubtitleClip;
                if (asset != null)
                {
                    string linePath = $"{LINES_ROOT}/Subtitle_Beat_{i}.asset";
                    asset.Line = AssetDatabase.LoadAssetAtPath<SubtitleLine>(linePath);
                }
            }

            // 4. Signal Track a 78s per completare l'intro e abilitare il portello
            SignalTrack sigTrack = timeline.CreateTrack<SignalTrack>(null, "Events");
            var marker = sigTrack.CreateMarker<SignalEmitter>(78.0);
            marker.asset = introSignal;

            EditorUtility.SetDirty(timeline);
            return timeline;
        }

        private static TimelineAsset CreateExitTimeline()
        {
            string path = $"{TIMELINES_ROOT}/ExitTitle_Blockout.playable";
            TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
            if (timeline == null)
            {
                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                AssetDatabase.CreateAsset(timeline, path);
            }

            foreach (var output in timeline.GetOutputTracks())
            {
                timeline.DeleteTrack(output);
            }

            string animPath = $"{ANIMS_ROOT}/ExitFade_Animation.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                clip.name = "ExitFade_Animation";

                AnimationCurve alphaCurve = new AnimationCurve();
                alphaCurve.AddKey(new Keyframe(0f, 0f));
                alphaCurve.AddKey(new Keyframe(1.5f, 0f));
                alphaCurve.AddKey(new Keyframe(2.5f, 1f));
                alphaCurve.AddKey(new Keyframe(5.5f, 1f));
                alphaCurve.AddKey(new Keyframe(6.5f, 0f));

                clip.SetCurve("", typeof(CanvasGroup), "m_Alpha", alphaCurve);
                AssetDatabase.CreateAsset(clip, animPath);
            }

            AnimationTrack animTrack = timeline.CreateTrack<AnimationTrack>(null, "FadeTrack");
            var timelineClip = animTrack.CreateClip(clip);
            timelineClip.start = 0;
            timelineClip.duration = 6.5;

            EditorUtility.SetDirty(timeline);
            return timeline;
        }

        private static void ConfigureScene(TimelineAsset introTimeline, TimelineAsset exitTimeline, SignalAsset introSignal)
        {
            var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

            // Trova Player
            var diver = Object.FindFirstObjectByType<DiverController>();
            if (diver == null)
            {
                Debug.LogError("[PrologueWave1Builder] DiverController non trovato in scena.");
                return;
            }

            var playerInput = diver.GetComponent<PlayerInput>();
            var starterInput = diver.GetComponent<StarterAssetsInputs>();
            var interaction = diver.GetComponent<PlayerInteraction>();
            var hands = diver.GetComponent<PlayerHands>();
            var flashlight = diver.GetComponent<DiverFlashlight>();

            // Rimuovi eventuale Rigidbody anomalo su Diver (confligge con CharacterController)
            var diverRb = diver.GetComponent<Rigidbody>();
            if (diverRb != null)
            {
                Undo.DestroyObjectImmediate(diverRb);
            }

            // Trova cabina ascensore
            GameObject elevator = GameObject.Find("01_CrashElevator");
            if (elevator == null)
            {
                Debug.LogError("[PrologueWave1Builder] 01_CrashElevator non trovato in scena.");
                return;
            }

            // 1. Hardening collisione pavimento ascensore: spessore aumentato verso il basso (2 metri) per impedire qualsiasi caduta nel vuoto
            Transform floorTransform = elevator.transform.Find("Floor");
            Material wallMat = null;
            if (floorTransform != null)
            {
                var floorBox = floorTransform.GetComponent<BoxCollider>();
                if (floorBox != null)
                {
                    floorBox.center = new Vector3(0f, -4.5f, 0f);
                    floorBox.size = new Vector3(1.0f, 10.0f, 1.0f);
                }
                var mr = floorTransform.GetComponent<MeshRenderer>();
                if (mr != null) wallMat = mr.sharedMaterial;
            }

            // 2. Chiusura pareti frontali ascensore attorno al portello per confinare il player nella cabina
            EnsureWallFrame(elevator, "Wall_Front_Left", new Vector3(-1.10f, 1.70f, 1.50f), new Vector3(1.00f, 3.20f, 0.20f), wallMat);
            EnsureWallFrame(elevator, "Wall_Front_Right", new Vector3(1.10f, 1.70f, 1.50f), new Vector3(1.00f, 3.20f, 0.20f), wallMat);
            EnsureWallFrame(elevator, "Wall_Front_Top", new Vector3(0.00f, 2.65f, 1.50f), new Vector3(1.20f, 1.10f, 0.20f), wallMat);

            // 3. Posiziona il Player root e Diver dentro la cabina, allineati sul pavimento e orientati verso il portello
            Vector3 localSpawn = new Vector3(0f, 0.32f, -0.4f);
            Vector3 worldSpawn = elevator.transform.TransformPoint(localSpawn);
            float worldYaw = elevator.transform.eulerAngles.y;

            GameObject playerRoot = GameObject.Find("Player");
            if (playerRoot != null)
            {
                playerRoot.transform.position = worldSpawn;
                playerRoot.transform.rotation = Quaternion.Euler(0f, worldYaw, 0f);
                EditorUtility.SetDirty(playerRoot.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(playerRoot.transform);
            }

            diver.transform.localPosition = Vector3.zero;
            diver.transform.localRotation = Quaternion.identity;
            EditorUtility.SetDirty(diver.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(diver.transform);

            Transform cameraRoot = diver.transform.Find("PlayerCameraRoot");
            if (cameraRoot != null)
            {
                PrefabUtility.RevertObjectOverride(cameraRoot.transform, InteractionMode.AutomatedAction);
                cameraRoot.localPosition = new Vector3(0f, 1.375f, 0f);
                cameraRoot.localRotation = Quaternion.identity;
                EditorUtility.SetDirty(cameraRoot);
                PrefabUtility.RecordPrefabInstancePropertyModifications(cameraRoot);
            }

            var playerVcam = GameObject.Find("PlayerFollowCamera")?.GetComponent<Cinemachine.CinemachineVirtualCamera>();
            if (playerVcam != null)
            {
                playerVcam.transform.localPosition = Vector3.zero;
                playerVcam.transform.localRotation = Quaternion.identity;
                EditorUtility.SetDirty(playerVcam.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(playerVcam.transform);
            }

            diver.ResetMotion();

            // 4. Allestisci Prologue_IntroCamera (CinemachineVirtualCamera dedicata al cutscene dei 7 beat)
            Transform existingIntroCam = elevator.transform.Find("Prologue_IntroCamera");
            GameObject introCamObj = existingIntroCam != null ? existingIntroCam.gameObject : null;
            if (introCamObj == null)
            {
                introCamObj = new GameObject("Prologue_IntroCamera");
                Undo.RegisterCreatedObjectUndo(introCamObj, "Crea Prologue_IntroCamera");
                introCamObj.transform.SetParent(elevator.transform, false);
            }

            introCamObj.transform.localPosition = new Vector3(0.27f, 1.67f, -0.37f);
            introCamObj.transform.localEulerAngles = new Vector3(-15f, -0.24f, -11.33f);

            var introVcam = introCamObj.GetComponent<Cinemachine.CinemachineVirtualCamera>() ?? introCamObj.AddComponent<Cinemachine.CinemachineVirtualCamera>();
            introVcam.Priority = 0;
            introVcam.m_Lens.FieldOfView = 32.9f;
            introVcam.m_Lens.NearClipPlane = 0.14f;
            introVcam.m_Lens.FarClipPlane = 800f;

            var noise = introCamObj.GetComponent<Cinemachine.CinemachineBasicMultiChannelPerlin>() ?? introCamObj.AddComponent<Cinemachine.CinemachineBasicMultiChannelPerlin>();
            var camAnimator = introCamObj.GetComponent<Animator>() ?? introCamObj.AddComponent<Animator>();
            EditorUtility.SetDirty(introVcam);
            EditorUtility.SetDirty(introCamObj);

            // 5. Allestisci Portello
            GameObject hatch = GameObject.Find("Prologue_Hatch");
            if (hatch == null)
            {
                hatch = GameObject.CreatePrimitive(PrimitiveType.Cube);
                hatch.name = "Prologue_Hatch";
                Undo.RegisterCreatedObjectUndo(hatch, "Crea Portello Prologo");
            }
            hatch.transform.SetParent(elevator.transform);
            hatch.transform.localPosition = new Vector3(0f, 1.05f, 1.5f);
            hatch.transform.localScale = new Vector3(1.15f, 2.0f, 0.12f);
            hatch.transform.localRotation = Quaternion.identity;

            var hatchInteractable = hatch.GetComponent<SimpleInteractable>() ?? hatch.AddComponent<SimpleInteractable>();
            hatchInteractable.SetInteractable(false);
            hatchInteractable.SetPromptText("Apri Portello");

            var hatchLocEvent = hatch.GetComponent<LocalizeStringEvent>() ?? hatch.AddComponent<LocalizeStringEvent>();
            hatchLocEvent.StringReference.SetReference("Prologue", "UI/prologue.hatch.open");

            UnityEventTools.RemovePersistentListener<string>(hatchLocEvent.OnUpdateString, hatchInteractable.SetPromptText);
            UnityEventTools.AddPersistentListener<string>(hatchLocEvent.OnUpdateString, hatchInteractable.SetPromptText);

            // 6. Allestisci UI Canvas Prologo
            GameObject oldCanvas = GameObject.Find("PrologueCanvas");
            if (oldCanvas != null) Undo.DestroyObjectImmediate(oldCanvas);

            GameObject canvasObj = new GameObject("PrologueCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasObj, "Crea PrologueCanvas");

            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Sottotitoli View
            GameObject subtitleView = new GameObject("SubtitleView", typeof(RectTransform), typeof(Image));
            subtitleView.transform.SetParent(canvasObj.transform, false);

            RectTransform subRect = subtitleView.GetComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.1f, 0.05f);
            subRect.anchorMax = new Vector2(0.9f, 0.22f);
            subRect.offsetMin = Vector2.zero;
            subRect.offsetMax = Vector2.zero;

            Image subBg = subtitleView.GetComponent<Image>();
            subBg.color = new Color(0f, 0f, 0f, 0.65f);

            // Speaker Text
            GameObject speakerObj = new GameObject("SpeakerText", typeof(RectTransform), typeof(Text), typeof(LocalizeStringEvent));
            speakerObj.transform.SetParent(subtitleView.transform, false);
            RectTransform speakerRect = speakerObj.GetComponent<RectTransform>();
            speakerRect.anchorMin = new Vector2(0.05f, 0.6f);
            speakerRect.anchorMax = new Vector2(0.95f, 0.95f);
            speakerRect.offsetMin = Vector2.zero;
            speakerRect.offsetMax = Vector2.zero;

            Text speakerText = speakerObj.GetComponent<Text>();
            speakerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            speakerText.fontSize = 20;
            speakerText.fontStyle = FontStyle.Bold;
            speakerText.color = new Color(0.2f, 0.8f, 1f);

            LocalizeStringEvent speakerLoc = speakerObj.GetComponent<LocalizeStringEvent>();

            // Body Text
            GameObject bodyObj = new GameObject("BodyText", typeof(RectTransform), typeof(Text), typeof(LocalizeStringEvent));
            bodyObj.transform.SetParent(subtitleView.transform, false);
            RectTransform bodyRect = bodyObj.GetComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0.05f, 0.05f);
            bodyRect.anchorMax = new Vector2(0.95f, 0.58f);
            bodyRect.offsetMin = Vector2.zero;
            bodyRect.offsetMax = Vector2.zero;

            Text bodyText = bodyObj.GetComponent<Text>();
            bodyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bodyText.fontSize = 18;
            bodyText.color = Color.white;

            LocalizeStringEvent bodyLoc = bodyObj.GetComponent<LocalizeStringEvent>();

            SubtitlePanel subPanel = canvasObj.GetComponent<SubtitlePanel>() ?? canvasObj.AddComponent<SubtitlePanel>();
            var vF = typeof(SubtitlePanel).GetField("view", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (vF != null) vF.SetValue(subPanel, subtitleView);
            var sF = typeof(SubtitlePanel).GetField("speaker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (sF != null) sF.SetValue(subPanel, speakerLoc);
            var bF = typeof(SubtitlePanel).GetField("body", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (bF != null) bF.SetValue(subPanel, bodyLoc);
            var stF = typeof(SubtitlePanel).GetField("speakerText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (stF != null) stF.SetValue(subPanel, speakerText);
            var btF = typeof(SubtitlePanel).GetField("bodyText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (btF != null) btF.SetValue(subPanel, bodyText);

            UnityEventTools.RemovePersistentListener<string>(speakerLoc.OnUpdateString, subPanel.SetSpeakerText);
            UnityEventTools.AddPersistentListener<string>(speakerLoc.OnUpdateString, subPanel.SetSpeakerText);

            UnityEventTools.RemovePersistentListener<string>(bodyLoc.OnUpdateString, subPanel.SetBodyText);
            UnityEventTools.AddPersistentListener<string>(bodyLoc.OnUpdateString, subPanel.SetBodyText);

            subtitleView.SetActive(false);

            // Dissolvenza e Titolo
            GameObject fadeOverlay = new GameObject("FadeOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            fadeOverlay.transform.SetParent(canvasObj.transform, false);
            RectTransform fadeRect = fadeOverlay.GetComponent<RectTransform>();
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = Vector2.one;
            fadeRect.offsetMin = Vector2.zero;
            fadeRect.offsetMax = Vector2.zero;

            Image fadeImg = fadeOverlay.GetComponent<Image>();
            fadeImg.color = Color.black;

            CanvasGroup fadeGroup = fadeOverlay.GetComponent<CanvasGroup>();
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;

            GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(Text));
            titleObj.transform.SetParent(fadeOverlay.transform, false);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            Text titleText = titleObj.GetComponent<Text>();
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 54;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.9f, 0.95f, 1f);
            titleText.text = "DEEPLONAUTS";

            // 7. Setup Prologue Directors
            GameObject oldIntro = GameObject.Find("Prologue_IntroDirector");
            if (oldIntro != null) Undo.DestroyObjectImmediate(oldIntro);
            GameObject introDirectorObj = new GameObject("Prologue_IntroDirector");
            Undo.RegisterCreatedObjectUndo(introDirectorObj, "Crea Prologue_IntroDirector");
            PlayableDirector introDirector = introDirectorObj.AddComponent<PlayableDirector>();
            introDirector.playableAsset = introTimeline;
            introDirector.playOnAwake = true;
            introDirector.extrapolationMode = DirectorWrapMode.None;

            var introGate = introDirectorObj.AddComponent<CinematicControlGate>();
            ConfigureGate(introGate, diver, starterInput, subPanel, playerInput, interaction, hands, flashlight);

            // Bindings su Intro Director
            var brain = Camera.main != null ? Camera.main.GetComponent<Cinemachine.CinemachineBrain>() : null;
            foreach (var output in introTimeline.GetOutputTracks())
            {
                if (output is global::CinemachineTrack cineOutputTrack)
                {
                    if (brain != null) introDirector.SetGenericBinding(cineOutputTrack, brain);

                    var clips = cineOutputTrack.GetClips();
                    foreach (var c in clips)
                    {
                        var shot = c.asset as global::CinemachineShot;
                        if (shot != null)
                        {
                            if (c.start < 1.0 && introVcam != null)
                            {
                                introDirector.SetReferenceValue(shot.VirtualCamera.exposedName, introVcam);
                            }
                            else if (playerVcam != null)
                            {
                                introDirector.SetReferenceValue(shot.VirtualCamera.exposedName, playerVcam);
                            }
                        }
                    }
                }
                else if (output is AnimationTrack animOutputTrack && output.name == "IntroCamTrack")
                {
                    introDirector.SetGenericBinding(animOutputTrack, introCamObj);
                }
                else if (output is SubtitleTrack subOutputTrack)
                {
                    introDirector.SetGenericBinding(subOutputTrack, subPanel);
                }
            }

            SignalReceiver receiver = introDirectorObj.AddComponent<SignalReceiver>();
            var receiverEvent = new UnityEngine.Events.UnityEvent();
            UnityEventTools.AddBoolPersistentListener(receiverEvent, hatchInteractable.SetInteractable, true);
            for (int r = receiver.Count() - 1; r >= 0; r--)
            {
                if (receiver.GetSignalAssetAtIndex(r) == introSignal)
                    receiver.RemoveAtIndex(r);
            }
            receiver.AddReaction(introSignal, receiverEvent);

            // Exit Director
            GameObject oldExit = GameObject.Find("Prologue_ExitDirector");
            if (oldExit != null) Undo.DestroyObjectImmediate(oldExit);
            GameObject exitDirectorObj = new GameObject("Prologue_ExitDirector");
            Undo.RegisterCreatedObjectUndo(exitDirectorObj, "Crea Prologue_ExitDirector");
            PlayableDirector exitDirector = exitDirectorObj.AddComponent<PlayableDirector>();
            exitDirector.playableAsset = exitTimeline;
            exitDirector.playOnAwake = false;
            exitDirector.extrapolationMode = DirectorWrapMode.None;

            var exitGate = exitDirectorObj.AddComponent<CinematicControlGate>();
            ConfigureGate(exitGate, diver, starterInput, subPanel, playerInput, interaction, hands, flashlight);

            foreach (var output in exitTimeline.GetOutputTracks())
            {
                if (output.name == "FadeTrack")
                {
                    exitDirector.SetGenericBinding(output, fadeOverlay);
                    break;
                }
            }

            UnityEventTools.RemovePersistentListener(hatchInteractable.onInteracted, exitDirector.Play);
            UnityEventTools.AddPersistentListener(hatchInteractable.onInteracted, exitDirector.Play);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"<color=#10b981>[PrologueWave1Builder]</color> Scena {SCENE_PATH} configurata e salvata con successo!");
        }

        private static void EnsureWallFrame(GameObject parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            Transform existing = parent.transform.Find(name);
            GameObject wall = existing != null ? existing.gameObject : null;
            if (wall == null)
            {
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = name;
                Undo.RegisterCreatedObjectUndo(wall, $"Crea {name}");
                wall.transform.SetParent(parent.transform, false);
            }
            wall.transform.localPosition = localPos;
            wall.transform.localScale = localScale;
            wall.transform.localRotation = Quaternion.identity;

            if (mat != null)
            {
                var mr = wall.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = mat;
            }
        }

        private static void ConfigureGate(
            CinematicControlGate gate,
            DiverController diver,
            StarterAssetsInputs input,
            SubtitlePanel subtitles,
            PlayerInput playerInput,
            PlayerInteraction interaction,
            PlayerHands hands,
            DiverFlashlight flashlight)
        {
            var dF = typeof(CinematicControlGate).GetField("diver", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (dF != null) dF.SetValue(gate, diver);

            var iF = typeof(CinematicControlGate).GetField("input", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (iF != null) iF.SetValue(gate, input);

            var sF = typeof(CinematicControlGate).GetField("subtitles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (sF != null) sF.SetValue(gate, subtitles);

            var cF = typeof(CinematicControlGate).GetField("controls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (cF != null)
            {
                Behaviour[] controls = new Behaviour[] { playerInput, diver, interaction, hands, flashlight };
                cF.SetValue(gate, controls);
            }
        }
    }
}
#endif
