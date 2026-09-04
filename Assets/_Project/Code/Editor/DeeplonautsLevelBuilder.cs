#if UNITY_EDITOR
using Deeploration.Environment;
using Deeploration.Interaction;
using Deeploration.Player;
using Deeploration.Quests;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Deeploration.Editor
{
    public static class DeeplonautsLevelBuilder
    {
        private const string ROOT_NAME = "_BLOCKOUT_OBJECTIVE_1";

        [MenuItem("Deeplonauts/Costruisci Blockout Completo (Obiettivo 1)")]
        public static void BuildFullBlockout()
        {
            GameObject existing = GameObject.Find(ROOT_NAME);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            GameObject root = new GameObject(ROOT_NAME);
            Undo.RegisterCreatedObjectUndo(root, "Crea Blockout Obiettivo 1");

            // Materiali base puliti
            Material matRock = CreateOrGetMaterial("M_Greybox_Rock", new Color(0.18f, 0.22f, 0.25f), 0.1f, 0.8f);
            Material matMetal = CreateOrGetMaterial("M_Greybox_Industrial", new Color(0.12f, 0.14f, 0.16f), 0.5f, 0.4f);
            Material matHazard = CreateOrGetMaterial("M_Greybox_Hazard", new Color(0.85f, 0.55f, 0.1f), 0.2f, 0.5f);
            Material matBeacon = CreateOrGetMaterial("M_Greybox_BeaconGlow", new Color(1f, 0.85f, 0.4f), 0f, 0f, true, new Color(1f, 0.85f, 0.4f) * 2.5f);
            Material matPowerCell = CreateOrGetMaterial("M_PowerCell_Glow", new Color(0.1f, 0.9f, 1f), 0.8f, 0.2f, true, new Color(0.1f, 0.9f, 1f) * 3.5f);

            // 0. Setup Quest System
            QuestProfile questProfile = CreateOrGetQuestProfile();
            QuestManager questMgr = root.AddComponent<QuestManager>();
            var qpField = typeof(QuestManager).GetField("activeQuestProfile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (qpField != null) qpField.SetValue(questMgr, questProfile);

            // 1. Crash Site / Ascensore (0, 0, 0)
            BuildCrashElevator(root.transform, matMetal, matHazard);

            // 2. Canyon di Onboarding (0, 0, 0) -> (0, -2, 35)
            BuildOnboardingCanyon(root.transform, matRock, matMetal, matPowerCell);

            // 3. Hub Base DR-04 (0, -2, 45)
            BuildBaseHub(root.transform, matMetal, matHazard);

            // 4. Nodo Generatori (26, -2, 60)
            BuildGeneratorNode(root.transform, matMetal, matHazard, matPowerCell);

            // 5. Nodo Ricarica O2 / Isola di Luce (-26, -2, 45)
            BuildOxygenRefillNode(root.transform, matMetal, matBeacon);

            // 6. Nodo Comunicazioni (24, 1, 28)
            BuildCommsNode(root.transform, matRock, matMetal);

            // 7. Faglia Abissale / Crush Depth (0, -2, -15) -> Sud
            BuildAbyssalTrench(root.transform, matRock, matHazard);

            // 8. Atmosfera Subacquea (Fog, Ambient, Marine Snow)
            SetupUnderwaterAtmosphere(root.transform);

            // Setup Player (Interaction + Hands) e posizionamento
            SetupPlayerComponents();
            PositionPlayerAtStart();

            Selection.activeGameObject = root;
            Debug.Log("<color=#4285f4>[Deeplonauts]</color> Blockout e Sistema Obiettivi (Obiettivo 1) generati con successo!");
        }

        [MenuItem("Deeplonauts/Piazza Solo Oggetti Interagibili (Testbed Minimale)")]
        public static void BuildInteractivePropsOnly()
        {
            const string PROPS_ROOT = "_OBJECTIVE_1_INTERACTIONS";
            GameObject existing = GameObject.Find(PROPS_ROOT);
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            GameObject oldBlockout = GameObject.Find(ROOT_NAME);
            if (oldBlockout != null) Undo.DestroyObjectImmediate(oldBlockout);

            GameObject root = new GameObject(PROPS_ROOT);
            Undo.RegisterCreatedObjectUndo(root, "Crea Oggetti Interagibili Obiettivo 1");

            Material matMetal = CreateOrGetMaterial("M_Greybox_Industrial", new Color(0.12f, 0.14f, 0.16f), 0.5f, 0.4f);
            Material matHazard = CreateOrGetMaterial("M_Greybox_Hazard", new Color(0.85f, 0.55f, 0.1f), 0.2f, 0.5f);
            Material matBeacon = CreateOrGetMaterial("M_Greybox_BeaconGlow", new Color(1f, 0.85f, 0.4f), 0f, 0f, true, new Color(1f, 0.85f, 0.4f) * 2.5f);
            Material matPowerCell = CreateOrGetMaterial("M_PowerCell_Glow", new Color(0.1f, 0.9f, 1f), 0.8f, 0.2f, true, new Color(0.1f, 0.9f, 1f) * 3.5f);

            // 1. QuestManager
            QuestProfile questProfile = CreateOrGetQuestProfile();
            QuestManager questMgr = root.AddComponent<QuestManager>();
            var qpField = typeof(QuestManager).GetField("activeQuestProfile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (qpField != null) qpField.SetValue(questMgr, questProfile);

            // 2. 3 Socket Generatori su piedistalli di fronte al player
            GameObject genGroup = new GameObject("01_Generators");
            genGroup.transform.SetParent(root.transform);
            CreateBox(genGroup.transform, "Pedestal_A", new Vector3(-2f, 0.5f, 5f), new Vector3(1.2f, 1f, 1f), matMetal);
            CreateSocket(genGroup.transform, "Socket_Gen_A", new Vector3(-2f, 1.1f, 5.4f), matHazard, "power_cell", "Generatore A");

            CreateBox(genGroup.transform, "Pedestal_B", new Vector3(0f, 0.5f, 5f), new Vector3(1.2f, 1f, 1f), matMetal);
            CreateSocket(genGroup.transform, "Socket_Gen_B", new Vector3(0f, 1.1f, 5.4f), matHazard, "power_cell", "Generatore B");

            CreateBox(genGroup.transform, "Pedestal_C", new Vector3(2f, 0.5f, 5f), new Vector3(1.2f, 1f, 1f), matMetal);
            CreateSocket(genGroup.transform, "Socket_Gen_C", new Vector3(2f, 1.1f, 5.4f), matHazard, "power_cell", "Generatore C");

            // 3. 3 Celle Energetiche a terra vicino al player
            GameObject cellsGroup = new GameObject("02_PowerCells");
            cellsGroup.transform.SetParent(root.transform);
            CreatePowerCell(cellsGroup.transform, "PowerCell_1", new Vector3(-1.5f, 0.3f, 2.2f), matPowerCell);
            CreatePowerCell(cellsGroup.transform, "PowerCell_2", new Vector3(0f, 0.3f, 2.2f), matPowerCell);
            CreatePowerCell(cellsGroup.transform, "PowerCell_3", new Vector3(1.5f, 0.3f, 2.2f), matPowerCell);

            // 4. Stazione O2 sulla sinistra
            GameObject o2Group = new GameObject("03_OxygenStation");
            o2Group.transform.SetParent(root.transform);
            o2Group.transform.position = new Vector3(-5f, 0f, 4f);
            CreateBox(o2Group.transform, "Pillar", new Vector3(0f, 1.5f, 0f), new Vector3(0.5f, 3f, 0.5f), matMetal);
            CreateBox(o2Group.transform, "Beacon", new Vector3(0f, 3.2f, 0f), new Vector3(0.6f, 0.6f, 0.6f), matBeacon);

            GameObject o2LightObj = new GameObject("O2BeaconLight");
            o2LightObj.transform.SetParent(o2Group.transform);
            o2LightObj.transform.localPosition = new Vector3(0f, 3.2f, 0f);
            Light o2Light = o2LightObj.AddComponent<Light>();
            o2Light.type = LightType.Point;
            o2Light.color = new Color(1f, 0.82f, 0.35f);
            o2Light.intensity = 15f;
            o2Light.range = 10f;

            GameObject o2Zone = new GameObject("O2RefillZone");
            o2Zone.transform.SetParent(o2Group.transform);
            o2Zone.transform.localPosition = Vector3.zero;
            SphereCollider o2Col = o2Zone.AddComponent<SphereCollider>();
            o2Col.isTrigger = true;
            o2Col.radius = 4f;

            StressLightZone slZone = o2Zone.AddComponent<StressLightZone>();
            var vlField = typeof(StressLightZone).GetField("visualLight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (vlField != null) vlField.SetValue(slZone, o2Light);
            var srField = typeof(StressLightZone).GetField("stressRelief", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (srField != null) srField.SetValue(slZone, 1.0f);

            OxygenRefillStation refill = o2Zone.AddComponent<OxygenRefillStation>();
            var rfLight = typeof(OxygenRefillStation).GetField("stationBeaconLight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (rfLight != null) rfLight.SetValue(refill, o2Light);

            // 5. Console Comms sulla destra
            GameObject commsGroup = new GameObject("04_CommsTerminal");
            commsGroup.transform.SetParent(root.transform);
            commsGroup.transform.position = new Vector3(5f, 0f, 4f);
            GameObject consoleObj = CreateBox(commsGroup.transform, "Console_Desk", new Vector3(0f, 0.6f, 0f), new Vector3(1.2f, 1.2f, 0.8f), matMetal);

            SimpleInteractable commsInteract = consoleObj.AddComponent<SimpleInteractable>();
            var pField = typeof(SimpleInteractable).GetField("promptText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (pField != null) pField.SetValue(commsInteract, "Attiva Frequenza Emergenza Comms");
            var qIdF = typeof(SimpleInteractable).GetField("questObjectiveId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (qIdF != null) qIdF.SetValue(commsInteract, "comms_signal");

            GameObject commsLightObj = new GameObject("CommsLight");
            commsLightObj.transform.SetParent(commsGroup.transform);
            commsLightObj.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            Light commsLight = commsLightObj.AddComponent<Light>();
            commsLight.type = LightType.Point;
            commsLight.color = new Color(0.3f, 0.7f, 1f);
            commsLight.intensity = 3f;
            commsLight.range = 5f;

            // 6. Airlock Portale in fondo
            GameObject doorGroup = new GameObject("05_AirlockDoor");
            doorGroup.transform.SetParent(root.transform);
            doorGroup.transform.position = new Vector3(0f, 0f, 9f);
            CreateBox(doorGroup.transform, "Frame", new Vector3(0f, 1.8f, 0f), new Vector3(3.6f, 3.6f, 0.8f), matHazard);
            GameObject doorPanel = CreateBox(doorGroup.transform, "Panel", new Vector3(0f, 1.5f, 0f), new Vector3(2.2f, 2.8f, 0.2f), matMetal);

            GameObject doorLightObj = new GameObject("DoorLight");
            doorLightObj.transform.SetParent(doorGroup.transform);
            doorLightObj.transform.localPosition = new Vector3(0f, 3.2f, -0.5f);
            Light doorLight = doorLightObj.AddComponent<Light>();
            doorLight.type = LightType.Point;
            doorLight.color = Color.red;
            doorLight.intensity = 2f;
            doorLight.range = 4f;

            AirlockDoor airlock = doorGroup.AddComponent<AirlockDoor>();
            var lightF = typeof(AirlockDoor).GetField("doorStatusLight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (lightF != null) lightF.SetValue(airlock, doorLight);
            var panelF = typeof(AirlockDoor).GetField("doorPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (panelF != null) panelF.SetValue(airlock, doorPanel.transform);

            // Setup Player
            SetupPlayerComponents();

            Selection.activeGameObject = root;
            Debug.Log("<color=#4285f4>[Deeplonauts]</color> Oggetti interagibili minimi piazzati sul piano con successo!");
        }

        [MenuItem("Deeplonauts/Configura Atmosfera Subacquea (Fog + Lighting)")]
        public static void ApplyAtmosphereOnly()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.032f;
            RenderSettings.fogColor = new Color(0.012f, 0.026f, 0.042f, 1f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.005f, 0.01f, 0.018f, 1f);

            Debug.Log("<color=#4285f4>[Deeplonauts]</color> Atmosfera subacquea URP applicata ai RenderSettings.");
        }

        private static void BuildCrashElevator(Transform parent, Material matMetal, Material matHazard)
        {
            GameObject elevator = new GameObject("01_CrashElevator");
            elevator.transform.SetParent(parent);
            elevator.transform.position = new Vector3(0f, 0f, 0f);

            CreateBox(elevator.transform, "Floor", new Vector3(0f, 0.1f, 0f), new Vector3(3.2f, 0.2f, 3.2f), matMetal);
            CreateBox(elevator.transform, "Wall_Left", new Vector3(-1.5f, 1.7f, 0f), new Vector3(0.2f, 3.2f, 3.2f), matMetal);
            CreateBox(elevator.transform, "Wall_Right", new Vector3(1.5f, 1.7f, 0f), new Vector3(0.2f, 3.2f, 3.2f), matMetal);
            CreateBox(elevator.transform, "Wall_Back", new Vector3(0f, 1.7f, -1.5f), new Vector3(3.2f, 3.2f, 0.2f), matMetal);
            CreateBox(elevator.transform, "Ceiling", new Vector3(0f, 3.3f, 0f), new Vector3(3.2f, 0.2f, 3.2f), matMetal);

            CreateBox(elevator.transform, "DoorFrame_Top", new Vector3(0f, 2.7f, 1.5f), new Vector3(3.2f, 1.2f, 0.2f), matHazard);
            CreateBox(elevator.transform, "DoorFrame_Left", new Vector3(-1.1f, 1.1f, 1.5f), new Vector3(1f, 2.0f, 0.2f), matHazard);
            CreateBox(elevator.transform, "DoorFrame_Right", new Vector3(1.1f, 1.1f, 1.5f), new Vector3(1f, 2.0f, 0.2f), matHazard);

            GameObject lightObj = new GameObject("EmergencyLight");
            lightObj.transform.SetParent(elevator.transform);
            lightObj.transform.position = new Vector3(0f, 2.9f, 0f);
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.9f, 0.2f, 0.1f);
            light.intensity = 3.5f;
            light.range = 5.5f;
            light.shadows = LightShadows.Soft;

            CreateBox(elevator.transform, "CutCable_1", new Vector3(-0.4f, 3.8f, 0f), new Vector3(0.15f, 1f, 0.15f), matMetal);
            CreateBox(elevator.transform, "CutCable_2", new Vector3(0.4f, 4.1f, -0.2f), new Vector3(0.15f, 1.5f, 0.15f), matMetal);
        }

        private static void BuildOnboardingCanyon(Transform parent, Material matRock, Material matMetal, Material matPowerCell)
        {
            GameObject canyon = new GameObject("02_OnboardingCanyon");
            canyon.transform.SetParent(parent);

            CreateBox(canyon.transform, "Path_Segment_1", new Vector3(0f, -0.1f, 9f), new Vector3(6f, 0.2f, 16f), matRock);
            CreateBox(canyon.transform, "Path_Segment_2", new Vector3(0f, -1.0f, 24f), new Vector3(7f, 0.2f, 16f), matRock);
            CreateBox(canyon.transform, "Path_Segment_3", new Vector3(0f, -1.9f, 37f), new Vector3(10f, 0.2f, 12f), matRock);

            CreateBox(canyon.transform, "RockWall_L1", new Vector3(-4.5f, 2f, 6f), new Vector3(3f, 4.5f, 8f), matRock);
            CreateBox(canyon.transform, "RockWall_L2", new Vector3(-5.0f, 1.5f, 16f), new Vector3(3.5f, 5f, 12f), matRock);
            CreateBox(canyon.transform, "RockWall_L3", new Vector3(-5.5f, 1.0f, 28f), new Vector3(4f, 5.5f, 14f), matRock);

            CreateBox(canyon.transform, "RockWall_R1", new Vector3(4.5f, 2f, 6f), new Vector3(3f, 4.5f, 8f), matRock);
            CreateBox(canyon.transform, "RockWall_R2", new Vector3(5.0f, 1.5f, 16f), new Vector3(3.5f, 5f, 12f), matRock);
            CreateBox(canyon.transform, "RockWall_R3", new Vector3(5.5f, 1.0f, 28f), new Vector3(4f, 5.5f, 14f), matRock);

            CreateBox(canyon.transform, "Debris_MetalHull", new Vector3(-1.8f, 0.4f, 14f), new Vector3(1.2f, 0.8f, 2f), matMetal, new Vector3(15f, 25f, -10f));
            CreateBox(canyon.transform, "Debris_Rock_1", new Vector3(1.6f, -0.3f, 22f), new Vector3(1.8f, 1.2f, 1.5f), matRock, new Vector3(-10f, 40f, 5f));

            // Cella energetica 1 caduta vicino ai detriti dell'ascensore
            CreatePowerCell(canyon.transform, "PowerCell_Debris", new Vector3(-1.5f, 0.2f, 16f), matPowerCell);
        }

        private static void BuildBaseHub(Transform parent, Material matMetal, Material matHazard)
        {
            GameObject baseHub = new GameObject("03_BaseHub_DR04");
            baseHub.transform.SetParent(parent);
            baseHub.transform.position = new Vector3(0f, -2f, 48f);

            CreateBox(baseHub.transform, "BasePlaza", new Vector3(0f, -0.1f, 0f), new Vector3(32f, 0.2f, 32f), matMetal);
            CreateBox(baseHub.transform, "StationModule_Main", new Vector3(0f, 3f, 8f), new Vector3(14f, 6f, 10f), matMetal);
            CreateBox(baseHub.transform, "StationModule_RoofCap", new Vector3(0f, 6.2f, 8f), new Vector3(12f, 0.5f, 8f), matHazard);

            CreateBox(baseHub.transform, "AirlockFrame", new Vector3(0f, 1.8f, 2.8f), new Vector3(4f, 3.6f, 1.2f), matHazard);
            GameObject doorObj = CreateBox(baseHub.transform, "AirlockDoor_Panel", new Vector3(0f, 1.5f, 2.9f), new Vector3(2.4f, 2.8f, 0.3f), matMetal);

            GameObject lightObj = new GameObject("AirlockStatusLight");
            lightObj.transform.SetParent(baseHub.transform);
            lightObj.transform.position = new Vector3(0f, 3.4f, 3.4f);
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.1f, 0.05f);
            light.intensity = 2.5f;
            light.range = 5f;

            // Script AirlockDoor reattivo alla quest
            AirlockDoor airlock = baseHub.AddComponent<AirlockDoor>();
            var lightF = typeof(AirlockDoor).GetField("doorStatusLight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (lightF != null) lightF.SetValue(airlock, light);
            var panelF = typeof(AirlockDoor).GetField("doorPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (panelF != null) panelF.SetValue(airlock, doorObj.transform);

            CreateBox(baseHub.transform, "AirlockRamp", new Vector3(0f, 0.15f, 0f), new Vector3(3.5f, 0.3f, 4.5f), matMetal);
        }

        private static void BuildGeneratorNode(Transform parent, Material matMetal, Material matHazard, Material matPowerCell)
        {
            GameObject genNode = new GameObject("04_Node_Generators");
            genNode.transform.SetParent(parent);
            genNode.transform.position = new Vector3(26f, -2f, 60f);

            CreateBox(genNode.transform, "GenPlatform", new Vector3(0f, 0.2f, 0f), new Vector3(9f, 0.4f, 9f), matMetal);

            CreateBox(genNode.transform, "Generator_Unit_A", new Vector3(-2.6f, 1.5f, 0f), new Vector3(1.6f, 2.4f, 2.2f), matMetal);
            CreateBox(genNode.transform, "Generator_Unit_B", new Vector3(0f, 1.5f, 0f), new Vector3(1.6f, 2.4f, 2.2f), matMetal);
            CreateBox(genNode.transform, "Generator_Unit_C", new Vector3(2.6f, 1.5f, 0f), new Vector3(1.6f, 2.4f, 2.2f), matMetal);

            // 3 Socket interattivi per le celle
            CreateSocket(genNode.transform, "Socket_Gen_A", new Vector3(-2.6f, 1.2f, 1.15f), matHazard, "power_cell", "Generatore A");
            CreateSocket(genNode.transform, "Socket_Gen_B", new Vector3(0f, 1.2f, 1.15f), matHazard, "power_cell", "Generatore B");
            CreateSocket(genNode.transform, "Socket_Gen_C", new Vector3(2.6f, 1.2f, 1.15f), matHazard, "power_cell", "Generatore C");

            // Celle energetiche 2 e 3 nei pressi del generatore
            CreatePowerCell(genNode.transform, "PowerCell_Gen_1", new Vector3(-3.2f, 0.6f, -2.5f), matPowerCell);
            CreatePowerCell(genNode.transform, "PowerCell_Gen_2", new Vector3(3.4f, 0.6f, 2.8f), matPowerCell);

            CreateBox(genNode.transform, "PowerConduit", new Vector3(-12f, 0.1f, -6f), new Vector3(18f, 0.2f, 0.4f), matHazard, new Vector3(0f, 25f, 0f));

            GameObject lightObj = new GameObject("GeneratorWorkLight");
            lightObj.transform.SetParent(genNode.transform);
            lightObj.transform.position = new Vector3(0f, 3.2f, 0f);
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.9f, 0.65f, 0.2f);
            light.intensity = 2f;
            light.range = 8f;
        }

        private static void BuildOxygenRefillNode(Transform parent, Material matMetal, Material matBeacon)
        {
            GameObject o2Node = new GameObject("05_Node_OxygenRefill");
            o2Node.transform.SetParent(parent);
            o2Node.transform.position = new Vector3(-26f, -2f, 48f);

            CreateBox(o2Node.transform, "RefillPad", new Vector3(0f, 0.15f, 0f), new Vector3(7f, 0.3f, 7f), matMetal);
            CreateBox(o2Node.transform, "Gantry_Pillar", new Vector3(0f, 3.2f, -1.8f), new Vector3(0.6f, 6.2f, 0.6f), matMetal);
            CreateBox(o2Node.transform, "Gantry_Arm", new Vector3(0f, 6f, 0f), new Vector3(0.5f, 0.5f, 3.8f), matMetal);
            CreateBox(o2Node.transform, "DispenserHead", new Vector3(0f, 4.8f, 1.4f), new Vector3(0.8f, 1.2f, 0.8f), matMetal);
            CreateBox(o2Node.transform, "BeaconMesh", new Vector3(0f, 6.5f, 1.4f), new Vector3(0.7f, 0.7f, 0.7f), matBeacon);

            GameObject lightObj = new GameObject("SafeBeaconLight");
            lightObj.transform.SetParent(o2Node.transform);
            lightObj.transform.position = new Vector3(0f, 6.5f, 1.4f);
            Light beaconLight = lightObj.AddComponent<Light>();
            beaconLight.type = LightType.Point;
            beaconLight.color = new Color(1f, 0.82f, 0.35f);
            beaconLight.intensity = 18f;
            beaconLight.range = 16f;
            beaconLight.shadows = LightShadows.Soft;

            GameObject zoneObj = new GameObject("SafeOxygenStressZone");
            zoneObj.transform.SetParent(o2Node.transform);
            zoneObj.transform.position = new Vector3(0f, 1.5f, 0f);

            SphereCollider collider = zoneObj.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 8.5f;

            StressLightZone lightZone = zoneObj.AddComponent<StressLightZone>();
            var field = typeof(StressLightZone).GetField("visualLight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(lightZone, beaconLight);
            var reliefField = typeof(StressLightZone).GetField("stressRelief", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (reliefField != null) reliefField.SetValue(lightZone, 1.0f);

            // Ricarica Ossigeno + Avanzamento Quest
            OxygenRefillStation refillStation = zoneObj.AddComponent<OxygenRefillStation>();
            var rLightF = typeof(OxygenRefillStation).GetField("stationBeaconLight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (rLightF != null) rLightF.SetValue(refillStation, beaconLight);
        }

        private static void BuildCommsNode(Transform parent, Material matRock, Material matMetal)
        {
            GameObject commsNode = new GameObject("06_Node_Comms");
            commsNode.transform.SetParent(parent);
            commsNode.transform.position = new Vector3(22f, 0f, 26f);

            CreateBox(commsNode.transform, "RockPlateau", new Vector3(0f, -0.5f, 0f), new Vector3(10f, 2f, 10f), matRock);
            CreateBox(commsNode.transform, "Ramp_Access", new Vector3(-5f, -1f, 0f), new Vector3(4f, 1.5f, 5f), matRock, new Vector3(0f, 0f, 18f));
            CreateBox(commsNode.transform, "Antenna_Base", new Vector3(1f, 0.8f, 1f), new Vector3(1.2f, 1.4f, 1.2f), matMetal);
            CreateBox(commsNode.transform, "Antenna_Mast_Fallen", new Vector3(3.2f, 0.4f, 1f), new Vector3(5f, 0.3f, 0.3f), matMetal, new Vector3(0f, 20f, -12f));

            // Console terminale interattiva
            GameObject consoleObj = CreateBox(commsNode.transform, "Console_Terminal", new Vector3(-1.8f, 0.8f, 0f), new Vector3(1.4f, 1.2f, 0.8f), matMetal);
            SimpleInteractable interactable = consoleObj.AddComponent<SimpleInteractable>();
            var pField = typeof(SimpleInteractable).GetField("promptText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (pField != null) pField.SetValue(interactable, "Attiva Frequenza Emergenza Comms");
            var qIdF = typeof(SimpleInteractable).GetField("questObjectiveId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (qIdF != null) qIdF.SetValue(interactable, "comms_signal");

            GameObject lightObj = new GameObject("CommsWorkLight");
            lightObj.transform.SetParent(commsNode.transform);
            lightObj.transform.position = new Vector3(-1.8f, 2.2f, 0f);
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.3f, 0.7f, 1f);
            light.intensity = 2f;
            light.range = 6f;
        }

        private static void BuildAbyssalTrench(Transform parent, Material matRock, Material matHazard)
        {
            GameObject trench = new GameObject("07_AbyssalTrench_CrushDepth");
            trench.transform.SetParent(parent);
            trench.transform.position = new Vector3(0f, -2f, -14f);

            CreateBox(trench.transform, "TrenchLedge_Warning", new Vector3(0f, 0.3f, 0f), new Vector3(28f, 0.6f, 1.2f), matHazard);
            CreateBox(trench.transform, "Cliff_Drop_Wall", new Vector3(0f, -8f, -6f), new Vector3(34f, 16f, 3f), matRock, new Vector3(-35f, 0f, 0f));
            CreateBox(trench.transform, "DeepAbyss_Seabed", new Vector3(0f, -16f, -25f), new Vector3(50f, 0.5f, 35f), matRock);
        }

        private static void SetupUnderwaterAtmosphere(Transform parent)
        {
            GameObject atmos = new GameObject("08_UnderwaterAtmosphere");
            atmos.transform.SetParent(parent);

            ApplyAtmosphereOnly();

            GameObject snowObj = new GameObject("MarineSnowParticles");
            snowObj.transform.SetParent(atmos.transform);
            snowObj.transform.position = new Vector3(0f, 2f, 0f);

            ParticleSystem ps = snowObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 12f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startColor = new Color(0.85f, 0.95f, 1f, 0.35f);
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 45f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(14f, 8f, 14f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = false;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.06f;
            noise.frequency = 0.2f;

            ParticleSystemRenderer psRenderer = snowObj.GetComponent<ParticleSystemRenderer>();
            if (psRenderer != null)
            {
                psRenderer.sharedMaterial = CreateOrGetParticleMaterial("M_MarineSnow", new Color(0.85f, 0.95f, 1f, 0.35f));
                psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            }

            snowObj.AddComponent<MarineSnowFollower>();
        }

        private static GameObject CreateSocket(Transform parent, string name, Vector3 localPos, Material mat, string acceptedItemId, string socketName)
        {
            GameObject socketObj = CreateBox(parent, name, localPos, new Vector3(0.7f, 0.7f, 0.3f), mat);
            ItemSocket socket = socketObj.AddComponent<ItemSocket>();

            var accF = typeof(ItemSocket).GetField("acceptedItemId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (accF != null) accF.SetValue(socket, acceptedItemId);

            var nameF = typeof(ItemSocket).GetField("socketName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (nameF != null) nameF.SetValue(socket, socketName);

            // Spia socket
            GameObject lightObj = new GameObject("SocketStatusLight");
            lightObj.transform.SetParent(socketObj.transform);
            lightObj.transform.localPosition = new Vector3(0f, 0.5f, 0.1f);
            Light statusLight = lightObj.AddComponent<Light>();
            statusLight.type = LightType.Point;
            statusLight.color = Color.red;
            statusLight.intensity = 1.5f;
            statusLight.range = 2f;

            var slF = typeof(ItemSocket).GetField("statusLight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (slF != null) slF.SetValue(socket, statusLight);

            var qIdF = typeof(ItemSocket).GetField("questObjectiveId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (qIdF != null) qIdF.SetValue(socket, "gen_power");

            return socketObj;
        }

        private static GameObject CreatePowerCell(Transform parent, string name, Vector3 localPos, Material matGlow)
        {
            GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cell.name = name;
            cell.transform.SetParent(parent);
            cell.transform.localPosition = localPos;
            cell.transform.localScale = new Vector3(0.35f, 0.45f, 0.35f);

            MeshRenderer mr = cell.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = matGlow;

            Rigidbody rb = cell.AddComponent<Rigidbody>();
            rb.mass = 5f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            cell.AddComponent<ItemPickup>();

            // Luce punto glow attorno alla cella
            GameObject lightObj = new GameObject("CellGlowLight");
            lightObj.transform.SetParent(cell.transform);
            lightObj.transform.localPosition = Vector3.zero;
            Light glow = lightObj.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(0.2f, 0.9f, 1f);
            glow.intensity = 1.5f;
            glow.range = 2.5f;

            return cell;
        }

        private static void SetupPlayerComponents()
        {
            GameObject player = GameObject.Find("PlayerCapsule") ?? GameObject.FindWithTag("Player");
            if (player == null) return;

            if (player.GetComponent<PlayerInteraction>() == null)
            {
                player.AddComponent<PlayerInteraction>();
            }

            if (player.GetComponent<PlayerHands>() == null)
            {
                player.AddComponent<PlayerHands>();
            }
        }

        private static void PositionPlayerAtStart()
        {
            GameObject player = GameObject.Find("PlayerCapsule") ?? GameObject.FindWithTag("Player");
            if (player != null)
            {
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                player.transform.position = new Vector3(0f, 0.3f, 0.3f);
                player.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

                if (cc != null) cc.enabled = true;
            }
        }

        private static QuestProfile CreateOrGetQuestProfile()
        {
            string path = "Assets/_Project/Entities/Profiles/Quest_Objective_1.asset";
            QuestProfile profile = AssetDatabase.LoadAssetAtPath<QuestProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<QuestProfile>();
                profile.name = "Quest_Objective_1";

                var objectives = new System.Collections.Generic.List<ObjectiveDefinition>
                {
                    CreateObjective("gen_power", "Celle Generatore", 3),
                    CreateObjective("o2_beacon", "Stazione Ricarica O₂", 1),
                    CreateObjective("comms_signal", "Console Comms", 1)
                };

                var objField = typeof(QuestProfile).GetField("objectives", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (objField != null) objField.SetValue(profile, objectives);

                AssetDatabase.CreateAsset(profile, path);
                AssetDatabase.SaveAssets();
            }

            return profile;
        }

        private static ObjectiveDefinition CreateObjective(string id, string title, int amount)
        {
            ObjectiveDefinition obj = new ObjectiveDefinition();
            var idF = typeof(ObjectiveDefinition).GetField("objectiveId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (idF != null) idF.SetValue(obj, id);
            var tF = typeof(ObjectiveDefinition).GetField("title", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (tF != null) tF.SetValue(obj, title);
            var aF = typeof(ObjectiveDefinition).GetField("requiredAmount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (aF != null) aF.SetValue(obj, amount);
            return obj;
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat, Vector3? rotation = null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (rotation.HasValue)
                go.transform.localEulerAngles = rotation.Value;

            if (mat != null)
            {
                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = mat;
            }

            return go;
        }

        private static Material CreateOrGetMaterial(string matName, Color color, float metallic = 0f, float smoothness = 0.5f, bool isEmissive = false, Color? emissionColor = null)
        {
            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            string path = $"Assets/_Project/Prototype/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(uLit);
                mat.name = matName;
                mat.color = color;
                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Smoothness", smoothness);

                if (isEmissive && emissionColor.HasValue)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", emissionColor.Value);
                }

                AssetDatabase.CreateAsset(mat, path);
            }

            return mat;
        }

        private static Material CreateOrGetParticleMaterial(string matName, Color color)
        {
            Shader pShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (pShader == null) pShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (pShader == null) pShader = Shader.Find("Particles/Standard Unlit");
            if (pShader == null) pShader = Shader.Find("Sprites/Default");

            string path = $"Assets/_Project/Prototype/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(pShader);
                mat.name = matName;

                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Blend", 0);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                mat.SetColor("_BaseColor", color);
                mat.SetColor("_Color", color);

                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.SetColor("_BaseColor", color);
                mat.SetColor("_Color", color);
            }

            return mat;
        }
    }
}
#endif
