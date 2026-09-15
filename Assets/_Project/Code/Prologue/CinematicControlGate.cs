using System.Collections.Generic;
using Deeploration.Player;
using Deeploration.Interaction;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace Deeploration.Prologue
{
    [RequireComponent(typeof(PlayableDirector))]
    public sealed class CinematicControlGate : MonoBehaviour
    {
        [SerializeField] private DiverController diver;
        [SerializeField] private StarterAssetsInputs input;
        [SerializeField] private SubtitlePanel subtitles;
        [SerializeField] private Behaviour[] controls = new Behaviour[0];

        private bool[] previousStates;
        private PlayableDirector director;
        private CharacterController characterController;
        private bool previousControllerState = true;
        public bool IsLocked { get; private set; }

        private void Awake()
        {
            director = GetComponent<PlayableDirector>();
            var unique = new HashSet<Behaviour>();
            bool valid = director != null && diver != null &&
                         input != null && subtitles != null &&
                         controls != null && controls.Length > 0;

            if (controls != null)
            {
                foreach (var control in controls)
                    valid &= control != null && control != this &&
                             control != director && control != subtitles &&
                             unique.Add(control);
            }

            valid &= ContainsControl<PlayerInput>() &&
                     ContainsControl<DiverController>() &&
                     ContainsControl<PlayerInteraction>() &&
                     ContainsControl<PlayerHands>() &&
                     ContainsControl<DiverFlashlight>() &&
                     unique.Contains(diver);

            if (!valid)
            {
                Debug.LogError("CinematicControlGate: riferimenti non validi.", this);
                enabled = false;
                return;
            }

            previousStates = new bool[controls.Length];
            if (diver != null)
                characterController = diver.GetComponent<CharacterController>();
        }

        private bool ContainsControl<T>() where T : Behaviour
        {
            if (controls == null)
                return false;

            foreach (var control in controls)
                if (control is T)
                    return true;

            return false;
        }

        private void OnEnable()
        {
            if (director == null || previousStates == null) return;

            director.played += OnPlayed;
            director.stopped += OnStopped;

            if (director.state == PlayState.Playing)
                Lock();
        }

        private void OnDisable()
        {
            if (director != null)
            {
                director.played -= OnPlayed;
                director.stopped -= OnStopped;
            }

            Unlock();
        }

        private void Start()
        {
            if (director != null && director.state == PlayState.Playing && !IsLocked)
                Lock();
        }

        private void Update()
        {
            if (director != null && director.state == PlayState.Playing && !IsLocked)
                Lock();
        }

        private void OnPlayed(PlayableDirector _) => Lock();
        private void OnStopped(PlayableDirector _) => Unlock();

        public void Lock()
        {
            if (!isActiveAndEnabled || previousStates == null || IsLocked)
                return;

            IsLocked = true;

            if (characterController != null)
            {
                previousControllerState = characterController.enabled;
                characterController.enabled = false;
            }

            for (int i = 0; i < controls.Length; i++)
            {
                previousStates[i] = controls[i].enabled;
                controls[i].enabled = false;
            }

            ResetInput();
            diver.ResetMotion();
        }

        public void Unlock()
        {
            if (!IsLocked) return;

            IsLocked = false;
            ResetInput();

            if (characterController != null)
                characterController.enabled = previousControllerState;

            if (diver != null)
                diver.ResetMotion();

            for (int i = 0; i < controls.Length; i++)
                if (controls[i] != null)
                    controls[i].enabled = previousStates[i];

            if (subtitles != null)
                subtitles.Clear();
        }

        private void ResetInput()
        {
            if (input == null) return;

            input.move = Vector2.zero;
            input.look = Vector2.zero;
            input.jump = false;
            input.jumpDown = false;
            input.sprint = false;
        }
    }
}
