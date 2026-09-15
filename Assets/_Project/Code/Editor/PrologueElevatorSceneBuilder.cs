#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Deeploration.Player;
using Deeploration.Prologue;
using StarterAssets;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Deeploration.Editor
{
    public static class PrologueElevatorSceneBuilder
    {
        private const string SCENE_DIR = "Assets/_Project/Prototype";
        private const string SCENE_PATH = "Assets/_Project/Prototype/Prologue_Elevator.unity";
        private const string PLAYER_PREFAB_PATH = "Assets/_Project/Prefabs/Player.prefab";

        [MenuItem("Deeplonauts/Prologue/Build Elevator Prologue Scene")]
        public static void BuildElevatorScene()
        {
            if (File.Exists(SCENE_PATH))
            {
                if (!EditorUtility.DisplayDialog("Attenzione: Scena esistente",
                    $"La scena '{SCENE_PATH}' esiste già.\nRigenerarla da zero cancellerà la mesh e gli elementi inseriti o accordati manualmente.\n\nVuoi davvero sovrascriverla?",
                    "Sì, sovrascrivi", "Annulla"))
                {
                    Debug.LogWarning("[PrologueElevatorSceneBuilder] Operazione annullata per preservare la scena esistente.");
                    return;
                }
            }

            if (!Directory.Exists(SCENE_DIR))
            {
                Directory.CreateDirectory(SCENE_DIR);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupEnvironmentAtmosphere();

            Material matHull = GetOrCreateMaterial("M_Elevator_Hull", new Color(0.12f, 0.14f, 0.17f), 0.7f, 0.3f);
            Material matFrame = GetOrCreateMaterial("M_Elevator_Frame", new Color(0.25f, 0.27f, 0.30f), 0.5f, 0.5f);
            Material matGlass = GetOrCreateMaterial("M_Elevator_Glass", new Color(0.1f, 0.3f, 0.4f, 0.4f), 0.9f, 0.1f, isTransparent: true);
            Material matAbyss = GetOrCreateMaterial("M_Abyss_Rock", new Color(0.04f, 0.07f, 0.10f), 0.1f, 0.9f);

            GameObject elevatorRoot = new GameObject("01_Elevator_Rig");
            elevatorRoot.transform.position = Vector3.zero;
            elevatorRoot.transform.rotation = Quaternion.identity;

            BuildElevatorCabin(elevatorRoot.transform, matHull, matFrame, matGlass);
            SetupCabinLighting(elevatorRoot.transform);

            GameObject blenderSlot = new GameObject("DROP_BLENDER_MESH_HERE");
            blenderSlot.transform.SetParent(elevatorRoot.transform, false);

            GameObject playerInstance = SetupPlayer(elevatorRoot.transform);

            BuildTreadmillEnvironment(matAbyss);

            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#10b981>[PrologueElevatorSceneBuilder]</color> Scena 'Prologue_Elevator.unity' generata con successo!");
        }

        private static void SetupEnvironmentAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.05f;
            RenderSettings.fogColor = new Color(0.01f, 0.04f, 0.08f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.02f, 0.06f, 0.10f);
            RenderSettings.ambientEquatorColor = new Color(0.01f, 0.03f, 0.06f);
            RenderSettings.ambientGroundColor = new Color(0.005f, 0.01f, 0.02f);
        }

        private static void BuildElevatorCabin(Transform parent, Material matHull, Material matFrame, Material matGlass)
        {
            GameObject cabin = new GameObject("Cabin_Geometry");
            cabin.transform.SetParent(parent, false);

            // Pavimento con collider spesso verso il basso per prevenire tunneling
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(cabin.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            floor.transform.localScale = new Vector3(3.2f, 0.2f, 3.2f);
            var floorBox = floor.GetComponent<BoxCollider>();
            if (floorBox != null)
            {
                floorBox.center = new Vector3(0f, -2.4f, 0f);
                floorBox.size = new Vector3(1.0f, 5.0f, 1.0f);
            }
            SetMaterial(floor, matHull);

            // Soffitto
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Ceiling";
            ceiling.transform.SetParent(cabin.transform, false);
            ceiling.transform.localPosition = new Vector3(0f, 3.1f, 0f);
            ceiling.transform.localScale = new Vector3(3.2f, 0.2f, 3.2f);
            SetMaterial(ceiling, matHull);

            // Parete Posteriore
            GameObject wallBack = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallBack.name = "Wall_Back";
            wallBack.transform.SetParent(cabin.transform, false);
            wallBack.transform.localPosition = new Vector3(0f, 1.5f, -1.55f);
            wallBack.transform.localScale = new Vector3(3.2f, 3.0f, 0.2f);
            SetMaterial(wallBack, matHull);

            // Parete Sinistra
            GameObject wallLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallLeft.name = "Wall_Left";
            wallLeft.transform.SetParent(cabin.transform, false);
            wallLeft.transform.localPosition = new Vector3(-1.55f, 1.5f, 0f);
            wallLeft.transform.localScale = new Vector3(0.2f, 3.0f, 3.2f);
            SetMaterial(wallLeft, matHull);

            // Parete Destra
            GameObject wallRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallRight.name = "Wall_Right";
            wallRight.transform.SetParent(cabin.transform, false);
            wallRight.transform.localPosition = new Vector3(1.55f, 1.5f, 0f);
            wallRight.transform.localScale = new Vector3(0.2f, 3.0f, 3.2f);
            SetMaterial(wallRight, matHull);

            // Parete Frontale con apertura oblò centrale
            GameObject wallFrontBottom = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallFrontBottom.name = "Wall_Front_Bottom";
            wallFrontBottom.transform.SetParent(cabin.transform, false);
            wallFrontBottom.transform.localPosition = new Vector3(0f, 0.45f, 1.55f);
            wallFrontBottom.transform.localScale = new Vector3(3.2f, 0.9f, 0.2f);
            SetMaterial(wallFrontBottom, matHull);

            GameObject wallFrontTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallFrontTop.name = "Wall_Front_Top";
            wallFrontTop.transform.SetParent(cabin.transform, false);
            wallFrontTop.transform.localPosition = new Vector3(0f, 2.55f, 1.55f);
            wallFrontTop.transform.localScale = new Vector3(3.2f, 0.9f, 0.2f);
            SetMaterial(wallFrontTop, matHull);

            GameObject wallFrontLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallFrontLeft.name = "Wall_Front_Left";
            wallFrontLeft.transform.SetParent(cabin.transform, false);
            wallFrontLeft.transform.localPosition = new Vector3(-1.1f, 1.5f, 1.55f);
            wallFrontLeft.transform.localScale = new Vector3(1.0f, 1.2f, 0.2f);
            SetMaterial(wallFrontLeft, matHull);

            GameObject wallFrontRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallFrontRight.name = "Wall_Front_Right";
            wallFrontRight.transform.SetParent(cabin.transform, false);
            wallFrontRight.transform.localPosition = new Vector3(1.1f, 1.5f, 1.55f);
            wallFrontRight.transform.localScale = new Vector3(1.0f, 1.2f, 0.2f);
            SetMaterial(wallFrontRight, matHull);

            // Cornice Oblò
            GameObject portholeFrame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            portholeFrame.name = "Porthole_Frame";
            portholeFrame.transform.SetParent(cabin.transform, false);
            portholeFrame.transform.localPosition = new Vector3(0f, 1.5f, 1.57f);
            portholeFrame.transform.localScale = new Vector3(1.24f, 1.24f, 0.08f);
            SetMaterial(portholeFrame, matFrame);

            // Vetro Oblò (IsTrigger per permettere la vista senza interferire)
            GameObject portholeGlass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            portholeGlass.name = "Porthole_Glass";
            portholeGlass.transform.SetParent(cabin.transform, false);
            portholeGlass.transform.localPosition = new Vector3(0f, 1.5f, 1.55f);
            portholeGlass.transform.localScale = new Vector3(1.16f, 1.16f, 0.04f);
            var glassCollider = portholeGlass.GetComponent<Collider>();
            if (glassCollider != null) glassCollider.isTrigger = true;
            SetMaterial(portholeGlass, matGlass);
        }

        private static void SetupCabinLighting(Transform parent)
        {
            GameObject lightsRoot = new GameObject("Cabin_Lights");
            lightsRoot.transform.SetParent(parent, false);

            GameObject mainLight = new GameObject("Light_Main_Amber");
            mainLight.transform.SetParent(lightsRoot.transform, false);
            mainLight.transform.localPosition = new Vector3(0f, 2.7f, 0f);
            var pl = mainLight.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(1f, 0.72f, 0.35f);
            pl.range = 5.0f;
            pl.intensity = 8.0f;
            pl.shadows = LightShadows.Soft;

            GameObject emergencyLight = new GameObject("Light_Emergency_Accent");
            emergencyLight.transform.SetParent(lightsRoot.transform, false);
            emergencyLight.transform.localPosition = new Vector3(0f, 2.8f, -1.2f);
            var el = emergencyLight.AddComponent<Light>();
            el.type = LightType.Point;
            el.color = new Color(0.9f, 0.15f, 0.1f);
            el.range = 3.5f;
            el.intensity = 2.5f;
        }

        private static GameObject SetupPlayer(Transform elevatorRoot)
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
            GameObject player = null;

            if (playerPrefab != null)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            }
            else
            {
                player = new GameObject("Player");
            }

            player.name = "Player";
            player.transform.SetParent(elevatorRoot, false);
            // Posizione a terra nella cabina, rivolto in avanti verso l'oblò
            player.transform.localPosition = new Vector3(0f, 0.05f, -0.4f);
            player.transform.localRotation = Quaternion.identity;

            var diver = player.GetComponentInChildren<DiverController>();
            if (diver != null)
            {
                // Rimuovi Rigidbody spurio per impedire contrasti con CharacterController
                var rb = diver.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Undo.DestroyObjectImmediate(rb);
                }
            }

            return player;
        }

        private static void BuildTreadmillEnvironment(Material matAbyss)
        {
            GameObject treadmillRoot = new GameObject("Treadmill_Environment");
            treadmillRoot.transform.position = Vector3.zero;

            // Pareti del tunnel abissale esterno
            GameObject shaftRoot = new GameObject("Abyssal_Shaft");
            shaftRoot.transform.SetParent(treadmillRoot.transform, false);

            GameObject frontRock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frontRock.name = "Rock_Wall_Front";
            frontRock.transform.SetParent(shaftRoot.transform, false);
            frontRock.transform.localPosition = new Vector3(0f, 1.5f, 8.0f);
            frontRock.transform.localScale = new Vector3(16f, 30f, 1f);
            SetMaterial(frontRock, matAbyss);

            GameObject leftRock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftRock.name = "Rock_Wall_Left";
            leftRock.transform.SetParent(shaftRoot.transform, false);
            leftRock.transform.localPosition = new Vector3(-8f, 1.5f, 4.0f);
            leftRock.transform.localScale = new Vector3(1f, 30f, 12f);
            SetMaterial(leftRock, matAbyss);

            GameObject rightRock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightRock.name = "Rock_Wall_Right";
            rightRock.transform.SetParent(shaftRoot.transform, false);
            rightRock.transform.localPosition = new Vector3(8f, 1.5f, 4.0f);
            rightRock.transform.localScale = new Vector3(1f, 30f, 12f);
            SetMaterial(rightRock, matAbyss);

            // Luce oceanica esterna (filtra attraverso l'oblò)
            GameObject oceanLight = new GameObject("Ocean_Deep_Light");
            oceanLight.transform.SetParent(treadmillRoot.transform, false);
            oceanLight.transform.localPosition = new Vector3(0f, 2.5f, 5.0f);
            oceanLight.transform.localRotation = Quaternion.Euler(15f, 180f, 0f);
            var ol = oceanLight.AddComponent<Light>();
            ol.type = LightType.Spot;
            ol.color = new Color(0.05f, 0.45f, 0.65f);
            ol.range = 15f;
            ol.spotAngle = 65f;
            ol.intensity = 15f;
            ol.shadows = LightShadows.Soft;

            // Simulatore di moto subacqueo con Particle System (neve marina / bolle verso l'alto)
            GameObject snowEmitter = new GameObject("Underwater_Descent_Emitter");
            snowEmitter.transform.SetParent(treadmillRoot.transform, false);
            snowEmitter.transform.localPosition = new Vector3(0f, -3.0f, 3.5f);

            var ps = snowEmitter.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 2.0f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(7.0f, 11.0f); // Moto verso l'alto veloce
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = new Color(0.4f, 0.8f, 0.9f, 0.6f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 500;

            var emission = ps.emission;
            emission.rateOverTime = 80f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(4f, 0.5f, 4f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(8f); // Velocità ascensionale per simulare discesa ascensore

            var psr = snowEmitter.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                Material snowMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Prototype/M_MarineSnow.mat");
                if (snowMat != null) psr.sharedMaterial = snowMat;
            }
        }

        private static Material GetOrCreateMaterial(string matName, Color color, float metallic, float smoothness, bool isTransparent = false)
        {
            string path = $"Assets/_Project/Prototype/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                mat = new Material(shader);
                mat.color = color;
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);

                if (isTransparent)
                {
                    mat.SetFloat("_Surface", 1); // Transparent
                    mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = (int)RenderQueue.Transparent;
                }

                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static void SetMaterial(GameObject go, Material mat)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && mat != null)
            {
                mr.sharedMaterial = mat;
            }
        }

        [MenuItem("Deeplonauts/Prologue/Setup Elevator Sequence & Dialogues")]
        public static void SetupElevatorSceneComponents()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.path.Contains("Prologue_Elevator"))
            {
                activeScene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            }

            // 1. Player setup: lock movement
            var diver = Object.FindFirstObjectByType<DiverController>();
            if (diver != null)
            {
                diver.LockMovement = true;
                EditorUtility.SetDirty(diver);
            }

            Transform camTarget = null;
            if (diver != null)
            {
                var camRoot = diver.transform.Find("PlayerCameraRoot");
                if (camRoot != null) camTarget = camRoot;
            }

            // 2. Setup or find PrologueCanvas
            GameObject canvasObj = GameObject.Find("PrologueCanvas");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("PrologueCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(canvasObj, "Crea PrologueCanvas");
            }

            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // SubtitleView
            Transform subViewTr = canvasObj.transform.Find("SubtitleView");
            GameObject subtitleView;
            if (subViewTr == null)
            {
                subtitleView = new GameObject("SubtitleView", typeof(RectTransform), typeof(Image));
                subtitleView.transform.SetParent(canvasObj.transform, false);
            }
            else subtitleView = subViewTr.gameObject;

            RectTransform subRect = subtitleView.GetComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.1f, 0.05f);
            subRect.anchorMax = new Vector2(0.9f, 0.22f);
            subRect.offsetMin = Vector2.zero;
            subRect.offsetMax = Vector2.zero;

            Image subBg = subtitleView.GetComponent<Image>();
            subBg.color = new Color(0f, 0f, 0f, 0.65f);

            // Speaker Text
            Transform speakerTr = subtitleView.transform.Find("SpeakerText");
            GameObject speakerObj = speakerTr != null ? speakerTr.gameObject : new GameObject("SpeakerText", typeof(RectTransform), typeof(Text), typeof(LocalizeStringEvent));
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
            Transform bodyTr = subtitleView.transform.Find("BodyText");
            GameObject bodyObj = bodyTr != null ? bodyTr.gameObject : new GameObject("BodyText", typeof(RectTransform), typeof(Text), typeof(LocalizeStringEvent));
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

            // Fade Overlay
            Transform fadeTr = canvasObj.transform.Find("FadeOverlay");
            GameObject fadeObj = fadeTr != null ? fadeTr.gameObject : new GameObject("FadeOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            fadeObj.transform.SetParent(canvasObj.transform, false);
            RectTransform fadeRect = fadeObj.GetComponent<RectTransform>();
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = Vector2.one;
            fadeRect.offsetMin = Vector2.zero;
            fadeRect.offsetMax = Vector2.zero;

            Image fadeImg = fadeObj.GetComponent<Image>();
            fadeImg.color = Color.black;

            CanvasGroup fadeCg = fadeObj.GetComponent<CanvasGroup>();
            fadeCg.alpha = 0f;
            fadeCg.blocksRaycasts = false;

            // 3. Setup Sequence Manager
            GameObject seqObj = GameObject.Find("Prologue_Sequence_Manager");
            if (seqObj == null)
            {
                seqObj = new GameObject("Prologue_Sequence_Manager");
                Undo.RegisterCreatedObjectUndo(seqObj, "Crea Sequence Manager");
            }

            var seq = seqObj.GetComponent<ElevatorPrologueSequence>() ?? seqObj.AddComponent<ElevatorPrologueSequence>();

            // Reflection wiring
            void SetField(string name, object val)
            {
                var f = typeof(ElevatorPrologueSequence).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null) f.SetValue(seq, val);
            }

            SetField("diver", diver);
            SetField("cameraTarget", camTarget);
            SetField("subtitlePanel", subPanel);
            SetField("fadeOverlay", fadeCg);
            SetField("cubeMaterial", AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Prototype/M_Greybox_Hazard.mat"));
            SetField("nextSceneName", "GameplayLoop_Blockout");
            SetField("impactTimestamp", 30.0f);
            SetField("cubeBoundsMin", new Vector3(-3.0f, -2.5f, -14.0f));
            SetField("cubeBoundsMax", new Vector3(3.0f, 6.5f, -7.0f));

            var cues = new List<ElevatorPrologueSequence.DialogueCue>();
            float[] times = { 2.0f, 7.0f, 12.0f, 17.0f, 22.0f, 26.5f };
            float[] durs = { 4.0f, 4.0f, 4.0f, 4.0f, 4.0f, 3.0f };
            for (int i = 1; i <= 6; i++)
            {
                string linePath = $"Assets/_Project/Prologue/Lines/Subtitle_Beat_{i}.asset";
                var line = AssetDatabase.LoadAssetAtPath<SubtitleLine>(linePath);
                if (line != null)
                {
                    cues.Add(new ElevatorPrologueSequence.DialogueCue
                    {
                        timestamp = times[i - 1],
                        duration = durs[i - 1],
                        line = line
                    });
                }
            }
            SetField("dialogueCues", cues);

            EditorUtility.SetDirty(seqObj);
            EditorUtility.SetDirty(canvasObj);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            Debug.Log("<color=#10b981>[PrologueElevatorSceneBuilder]</color> Sequenza ascensore, dialoghi e screenshake configurati con successo!");
        }
    }
}
#endif
