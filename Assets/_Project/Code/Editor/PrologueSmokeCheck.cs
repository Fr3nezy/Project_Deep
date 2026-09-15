using Deeploration.Interaction;
using Deeploration.Player;
using Deeploration.Prologue;
using StarterAssets;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Playables;

namespace Deeploration.Editor
{
    public static class PrologueSmokeCheck
    {
        [MenuItem("Deeploration/Tests/Check Prologue Control Restored")]
        public static void Check()
        {
            Assert.IsTrue(EditorApplication.isPlaying, "Unity must be in Play Mode to run PrologueSmokeCheck.");

            var diver = Object.FindFirstObjectByType<DiverController>();
            Assert.IsNotNull(diver, "DiverController must be present in scene.");

            var cc = diver.GetComponent<CharacterController>();
            Assert.IsNotNull(cc, "CharacterController must be present on Diver.");

            var rb = diver.GetComponent<Rigidbody>();
            Assert.IsNull(rb, "Diver must NOT have a dynamic Rigidbody (conflicts with CharacterController).");

            var elevator = GameObject.Find("01_CrashElevator");
            Assert.IsNotNull(elevator, "01_CrashElevator must be present.");

            // Verifica che il Diver sia posizionato solidamente dentro la cabina ascensore
            Vector3 diverPos = diver.transform.position;
            Assert.IsTrue(diverPos.y > 0.1f && diverPos.y < 3.0f, $"Diver must not fall into void. Current Y: {diverPos.y}");

            var brain = Camera.main != null ? Camera.main.GetComponent<Cinemachine.CinemachineBrain>() : null;
            Assert.IsNotNull(brain, "MainCamera must have CinemachineBrain component.");

            var introDir = GameObject.Find("Prologue_IntroDirector")?.GetComponent<PlayableDirector>();
            Assert.IsNotNull(introDir, "Prologue_IntroDirector must be present.");

            var introGate = introDir.GetComponent<CinematicControlGate>();
            Assert.IsNotNull(introGate, "CinematicControlGate must be attached to Prologue_IntroDirector.");

            // Se l'intro sta ancora riproducendo, verifica cinematica attiva e controlli bloccati
            if (introDir.state == PlayState.Playing)
            {
                Assert.IsTrue(introGate.IsLocked, "CinematicControlGate must be locked while intro director is playing.");
                Assert.IsFalse(diver.enabled, "DiverController must be disabled while intro is playing.");
                Assert.IsNotNull(brain.ActiveVirtualCamera, "CinemachineBrain must have an active virtual camera.");
                Assert.AreEqual("Prologue_IntroCamera", brain.ActiveVirtualCamera.Name, "Active camera during intro must be Prologue_IntroCamera.");

                // Avanza al termine (78s) per verificare lo stacco finale
                introDir.time = 78.0;
                introDir.Evaluate();
                introDir.Stop();
            }

            // Verifica post-cutscene
            Assert.IsFalse(introGate.IsLocked, "CinematicControlGate must be unlocked after intro completes.");
            Assert.IsTrue(diver.enabled, "DiverController must be re-enabled after intro completes.");
            Assert.IsTrue(cc.enabled, "CharacterController must be enabled after intro completes.");

            var input = diver.GetComponent<StarterAssetsInputs>();
            Assert.IsNotNull(input, "StarterAssetsInputs must be present.");

            input.jump = true;
            input.jumpDown = true;
            input.JumpInput(false);

            Assert.IsFalse(input.jump, "Jump input must be reset.");
            Assert.IsFalse(input.jumpDown, "JumpDown input must be reset.");

            diver.ResetMotion();
            Assert.AreApproximatelyEqual(0f, diver.CurrentSpeed, "Diver current speed must be 0 after reset.");

            var hatch = GameObject.Find("Prologue_Hatch")?.GetComponent<SimpleInteractable>();
            Assert.IsNotNull(hatch, "Prologue_Hatch with SimpleInteractable must exist.");
            Assert.IsTrue(hatch.enabled, "Prologue_Hatch must be enabled.");

            Debug.Log("<color=#10b981>[PrologueSmokeCheck]</color> PASS: Intro cinematica, collisioni ascensore, camera cut e controlli Diver validati con successo!");
        }
    }
}
