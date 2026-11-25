using System;
using Deeploration.Player;
using Deeploration.Prologue;
using UnityEngine;

namespace Deeploration.Comms
{
    /// <summary>
    /// Avvisi di H.E.L.M. sull'ossigeno, come il PDA di una tuta: ogni soglia (frazione del serbatoio o secondi rimasti
    /// al consumo attuale) parla una volta quando la si scende e si riarma solo quando il serbatoio risale (ricarica) di rearmMargin
    /// sopra il livello a cui ha parlato: lo scatto che alza il consumo non fa ripetere «30 secondi».
    /// Se più soglie scattano nello stesso frame parla solo la più grave (l'ultima della lista).
    /// </summary>
    public sealed class OxygenComms : MonoBehaviour
    {
        [Serializable]
        public struct Callout
        {
            public SubtitleLine line;
            [Tooltip("Vero: threshold sono secondi di ossigeno rimasti al consumo attuale. Falso: frazione del serbatoio (0.5 = 50%).")]
            public bool seconds;
            public float threshold;
            public CommsPriority priority;
        }

        [SerializeField] private OxygenSystem oxygen;
        [Tooltip("In ordine di gravità crescente: a parità di frame parla l'ultima.")]
        [SerializeField] private Callout[] callouts = Array.Empty<Callout>();
        [Tooltip("Riarmo: la soglia torna attiva quando il serbatoio (0-1) risale di tanto sopra il livello a cui ha parlato.")]
        [SerializeField, Range(0.01f, 1f)] private float rearmMargin = 0.1f;

        private HelmetComms comms;
        private bool[] fired;
        private float[] firedAt;

        private void Start()
        {
            if (oxygen == null) oxygen = FindFirstObjectByType<OxygenSystem>();
            comms = FindFirstObjectByType<HelmetComms>();
            if (oxygen == null || comms == null) Debug.LogError("[OxygenComms] Manca OxygenSystem o HelmetComms in scena.", this);
            fired = new bool[callouts.Length];
            firedAt = new float[callouts.Length];
            if (oxygen == null) return;
            // Chi parte già sotto una soglia (cheat, debug) non la sente finché non ricarica
            for (int i = 0; i < callouts.Length; i++) Consume(i);
        }

        private float Value(Callout c)
        {
            if (!c.seconds) return oxygen.NormalizedOxygen;
            float drain = oxygen.CurrentDrainPerSecond;
            return drain > 0.0001f ? oxygen.CurrentOxygen / drain : float.PositiveInfinity;
        }

        private void Consume(int i)
        {
            if (Value(callouts[i]) > callouts[i].threshold) return;
            fired[i] = true;
            firedAt[i] = oxygen.NormalizedOxygen;
        }

        private void Update()
        {
            if (oxygen == null || comms == null || oxygen.InfiniteOxygen) return;
            int speak = -1;
            for (int i = 0; i < callouts.Length; i++)
            {
                if (fired[i])
                {
                    if (oxygen.NormalizedOxygen > firedAt[i] + rearmMargin) fired[i] = false;
                }
                else if (Value(callouts[i]) <= callouts[i].threshold)
                {
                    Consume(i);
                    speak = i;
                }
            }
            if (speak < 0 || callouts[speak].line == null) return;
            // Le soglie meno gravi restano consumate anche se non ancora scese (consumo alto: 30 s prima del 25%): niente «25%» dopo «30 secondi»
            for (int i = 0; i < speak; i++) if (!fired[i]) { fired[i] = true; firedAt[i] = oxygen.NormalizedOxygen; }
            comms.Say(callouts[speak].line, callouts[speak].priority);
        }
    }
}
