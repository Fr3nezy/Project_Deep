using System.Collections.Generic;
using Deeploration.Player;
using Deeploration.Prologue;
using TMPro;
using UnityEngine;

namespace Deeploration.Comms
{
    /// <summary>Priorità dei messaggi sullo schermo comunicazioni (GDD, «Concorrenza degli avvisi»).</summary>
    public enum CommsPriority
    {
        /// <summary>Consiglio da tutorial: scade in coda, se interrotto si scarta.</summary>
        Hint = 0,
        /// <summary>Narrazione: se interrotta torna in testa alla coda e riparte dall'inizio della frase.</summary>
        Dialogue = 1,
        /// <summary>Ti uccide se lo ignori: interrompe tutto e resta poco sullo schermo.</summary>
        Critical = 2,
    }

    /// <summary>
    /// Schermo comunicazioni del casco: pannello su cerniera che si ribalta verso il giocatore, scrive a macchina
    /// nome di chi parla e battuta (TextMeshPro 3D), suona la voce se la battuta ce l'ha e si richiude.
    /// Un messaggio alla volta con le regole del GDD: priorità, interruzione, coda, scadenza dei consigli,
    /// niente ripetizioni ravvicinate. Tempo non scalato, come gli avvisi del CRT.
    /// </summary>
    public sealed class HelmetComms : MonoBehaviour
    {
        private static readonly Color Amber = new(1f, 0.69f, 0.25f);
        private static readonly Color EmergencyRed = new(1f, 0.27f, 0.2f);

        [Header("Pannello")]
        [Tooltip("Pannello che ruota sulla cerniera. Deve essere importato in posa aperta: quella posa è angolo 0.")]
        [SerializeField] private Transform panel;
        [Tooltip("Angolo del pannello chiuso attorno all'asse X del genitore, in gradi.")]
        [SerializeField] private float closedAngle = -162.5f;
        [SerializeField, Min(0.05f)] private float flipSeconds = 0.45f;

        [Header("Testi")]
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text bodyText;
        [Tooltip("Caratteri scritti al secondo quando la battuta non ha voce. Con la voce la scrittura segue la durata del clip.")]
        [SerializeField, Min(1f)] private float charsPerSecond = 40f;
        [Tooltip("Coda di riverbero a fine clip, esclusa dalla sincronia fra parole e voce (robot.py, ambiente scafo: ~0.5 s).")]
        [SerializeField, Min(0f)] private float voiceTailSeconds = 0.5f;
        [Tooltip("Lettura dopo la scrittura: secondi per carattere, almeno minHoldSeconds.")]
        [SerializeField, Min(0f)] private float readSecondsPerChar = 0.045f;
        [SerializeField, Min(0f)] private float minHoldSeconds = 1.5f;
        [Tooltip("Il pannello resta aperto questi secondi dopo un messaggio: se ne arriva un altro non si richiude e riapre.")]
        [SerializeField, Min(0f)] private float lingerSeconds = 3f;

        [Header("Audio")]
        [Tooltip("Sorgente 2D della voce e del tick. Vuota = aggiunta da sola.")]
        [SerializeField] private AudioSource voiceSource;
        [Tooltip("Tick della telescrivente quando la battuta non ha voce. Vuoto = sintetizzato.")]
        [SerializeField] private AudioClip typeTick;
        [SerializeField, Range(0f, 1f)] private float tickVolume = 0.25f;
        [Tooltip("Bip di comunicazione in arrivo, all'apertura del pannello. Vuoto = sintetizzato (due toni, alto poi basso).")]
        [SerializeField] private AudioClip incomingBeep;
        [SerializeField, Range(0f, 1f)] private float beepVolume = 0.5f;

        [Header("Regole della coda (GDD)")]
        [SerializeField, Min(0f)] private float criticalSeconds = 4.5f;
        [SerializeField, Min(0f)] private float hintExpirySeconds = 8f;
        [SerializeField, Min(0f)] private float gapSeconds = 0.4f;
        [SerializeField, Min(0f)] private float repeatBlockSeconds = 10f;

        [Header("Riferimenti (auto-trovati se vuoti)")]
        [SerializeField] private DiverFlashlight flashlight;

        private sealed class Message
        {
            public SubtitleLine Line;
            public CommsPriority Priority;
            public float QueuedAt;
        }

        private readonly List<Message> queue = new();
        private readonly Dictionary<SubtitleLine, float> lastShown = new();
        private Message current;
        private Quaternion openRotation;
        private float openness; // 0 chiuso, 1 aperto
        private float typedChars, typeRate, holdUntil, nextAllowed, lingerUntil, pauseUntil;
        private int totalChars, shownChars, turnedPage;
        private bool wasOpen, voicePending, introPending;
        private float introUntil;

        /// <summary>Una battuta comincia sullo schermo (non quando entra in coda). Usato da HelmetMarkerTarget.</summary>
        public static event System.Action<SubtitleLine> LineStarted;

        /// <summary>Vero mentre un messaggio è sullo schermo o in coda.</summary>
        public bool IsBusy => current != null || queue.Count > 0;

        private void Awake()
        {
            if (flashlight == null) flashlight = FindFirstObjectByType<DiverFlashlight>();
            if (voiceSource == null)
            {
                voiceSource = gameObject.AddComponent<AudioSource>();
                voiceSource.playOnAwake = false;
                voiceSource.spatialBlend = 0f;
            }
            if (typeTick == null) typeTick = SynthTick();
            if (incomingBeep == null) incomingBeep = SynthBeep();
            if (panel != null) openRotation = panel.localRotation;
            ApplyPanel(false);
            Clear();
        }

        /// <summary>Mette in coda una battuta. Ignorata se identica a una mostrata da meno di repeatBlockSeconds.</summary>
        public void Say(SubtitleLine line, CommsPriority priority = CommsPriority.Dialogue)
        {
            if (line == null || line.text == null || line.text.IsEmpty)
            {
                Debug.LogError("[HelmetComms] Battuta vuota o senza testo.", this);
                return;
            }
            float now = Time.unscaledTime;
            if (lastShown.TryGetValue(line, out float last) && now - last < repeatBlockSeconds) return;
            if (current != null && current.Line == line) return;
            foreach (Message queued in queue) if (queued.Line == line) return;

            var message = new Message { Line = line, Priority = priority, QueuedAt = now };
            if (current != null && priority > current.Priority)
            {
                // Il dialogo interrotto riparte da capo dopo; il consiglio interrotto si perde.
                if (current.Priority == CommsPriority.Dialogue) queue.Insert(0, current);
                voiceSource.Stop();
                current = null;
                Begin(message);
                return;
            }
            int index = queue.FindIndex(m => m.Priority < priority);
            queue.Insert(index < 0 ? queue.Count : index, message);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            queue.RemoveAll(m => m.Priority == CommsPriority.Hint && now - m.QueuedAt > hintExpirySeconds);

            if (current == null && queue.Count > 0 && now >= nextAllowed)
            {
                Message next = queue[0];
                queue.RemoveAt(0);
                Begin(next);
            }

            bool wantOpen = current != null || now < lingerUntil;
            // Bip solo quando il pannello comincia ad aprirsi: un messaggio che arriva a pannello già aperto non suona.
            if (wantOpen && !wasOpen) voiceSource.PlayOneShot(incomingBeep, beepVolume);
            wasOpen = wantOpen;
            openness = Mathf.MoveTowards(openness, wantOpen ? 1f : 0f, Time.unscaledDeltaTime / flipSeconds);
            ApplyPanel(wantOpen);
            ApplyInk();

            if (current == null || openness < 1f) return;

            // Jingle della battuta: a pannello aperto, prima di voce e scrittura.
            if (introPending)
            {
                introPending = false;
                voiceSource.PlayOneShot(current.Line.intro);
                introUntil = now + current.Line.intro.length;
            }
            if (now < introUntil) return;

            // La voce parte a pannello aperto, insieme alla prima parola, e non sopra il bip.
            if (voicePending)
            {
                voicePending = false;
                voiceSource.clip = current.Line.voice;
                voiceSource.Play();
            }

            if (shownChars < totalChars)
            {
                if (now < pauseUntil) return;
                typedChars = Mathf.Min(totalChars, typedChars + typeRate * Time.unscaledDeltaTime);
                // Una parola alla volta, mai oltre la fine della pagina in corso.
                int reveal = Mathf.Min(WordEnd(Mathf.CeilToInt(typedChars)), PageOf(shownChars).lastCharacterIndex + 1);
                if (reveal <= shownChars) return;

                // Prima parola di una pagina nuova: senza voce, pausa per finire di leggere quella vecchia.
                int nextPage = PageIndex(shownChars);
                if (shownChars > 0 && nextPage != PageIndex(shownChars - 1) && nextPage != turnedPage)
                {
                    turnedPage = nextPage;
                    if (current.Line.voice == null)
                    {
                        pauseUntil = now + Mathf.Max(minHoldSeconds, PageLength(PageOf(shownChars - 1)) * readSecondsPerChar);
                        typedChars = shownChars;
                        return;
                    }
                }
                shownChars = reveal;
                bodyText.maxVisibleCharacters = shownChars;
                bodyText.pageToDisplay = PageIndex(shownChars - 1) + 1;
                if (current.Line.voice == null) voiceSource.PlayOneShot(typeTick, tickVolume);
                if (shownChars >= totalChars)
                {
                    float hold = current.Priority == CommsPriority.Critical
                        ? criticalSeconds
                        : Mathf.Max(minHoldSeconds, PageLength(PageOf(totalChars - 1)) * readSecondsPerChar);
                    holdUntil = now + hold;
                }
                return;
            }

            if (now < holdUntil || voiceSource.isPlaying) return;
            lastShown[current.Line] = now;
            current = null;
            nextAllowed = now + gapSeconds;
            lingerUntil = now + lingerSeconds;
        }

        // Fine della parola che contiene il carattere count-1: le parole compaiono intere.
        private int WordEnd(int count)
        {
            TMP_CharacterInfo[] chars = bodyText.textInfo.characterInfo;
            count = Mathf.Clamp(count, 0, totalChars);
            while (count > 0 && count < totalChars && !char.IsWhiteSpace(chars[count].character)) count++;
            return count;
        }

        private int PageIndex(int charIndex)
        {
            TMP_TextInfo info = bodyText.textInfo;
            for (int i = 0; i < info.pageCount; i++)
                if (charIndex <= info.pageInfo[i].lastCharacterIndex) return i;
            return Mathf.Max(0, info.pageCount - 1);
        }

        private TMP_PageInfo PageOf(int charIndex) => bodyText.textInfo.pageInfo[PageIndex(charIndex)];

        private static int PageLength(TMP_PageInfo page) => page.lastCharacterIndex - page.firstCharacterIndex + 1;

        private void Begin(Message message)
        {
            current = message;
            string speaker = message.Line.speaker != null && !message.Line.speaker.IsEmpty
                ? message.Line.speaker.GetLocalizedString()
                : string.Empty;
            speakerText.text = speaker;
            bodyText.text = message.Line.text.GetLocalizedString();
            // A pannello chiuso il testo è spento e TMP non conta i caratteri: va acceso prima di misurarlo.
            bodyText.enabled = true;
            bodyText.pageToDisplay = 1;
            bodyText.ForceMeshUpdate();
            totalChars = bodyText.textInfo.characterCount;
            typedChars = 0f;
            shownChars = 0;
            turnedPage = 0;
            pauseUntil = 0f;
            bodyText.maxVisibleCharacters = 0;
            // Testo vuoto (chiave mancante nella tabella): niente da scrivere, si chiude dopo minHoldSeconds.
            holdUntil = totalChars == 0 ? Time.unscaledTime + minHoldSeconds : float.MaxValue;
            typeRate = charsPerSecond;
            introPending = message.Line.intro != null;
            introUntil = 0f;
            voicePending = message.Line.voice != null;
            if (voicePending)
            {
                // Le parole seguono la voce: tutto il testo nella durata parlata del clip.
                typeRate = totalChars / Mathf.Max(0.5f, message.Line.voice.length - voiceTailSeconds);
            }
            LineStarted?.Invoke(message.Line);
        }

        private void Clear()
        {
            if (speakerText != null) speakerText.text = string.Empty;
            if (bodyText != null)
            {
                bodyText.text = string.Empty;
                bodyText.maxVisibleCharacters = 0;
            }
        }

        private void ApplyPanel(bool wantOpen)
        {
            if (panel == null) return;
            float eased = Mathf.SmoothStep(0f, 1f, openness);
            panel.localRotation = Quaternion.AngleAxis(Mathf.LerpUnclamped(closedAngle, 0f, eased), Vector3.right) * openRotation;
            // Testo solo a pannello tutto aperto: lo shader TMP usa uno ZTest globale che la UI può mettere ad Always,
            // e mentre il pannello scende dietro il bordo del casco il testo resterebbe disegnato sopra.
            bool visible = wantOpen && openness >= 1f;
            if (speakerText != null) speakerText.enabled = visible;
            if (bodyText != null) bodyText.enabled = visible;
        }

        private void ApplyInk()
        {
            Color ink = flashlight != null && flashlight.CurrentMode == FlashlightMode.Red ? EmergencyRed : Amber;
            if (speakerText != null) speakerText.color = ink;
            if (bodyText != null) bodyText.color = ink;
        }

        // Click secco da telescrivente: 12 ms di rumore con inviluppo esponenziale.
        private static AudioClip SynthTick()
        {
            const int rate = 44100;
            const int length = rate * 12 / 1000;
            var data = new float[length];
            var random = new System.Random(7);
            for (int i = 0; i < length; i++)
                data[i] = ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-i / (length * 0.18f)) * 0.8f;
            AudioClip clip = AudioClip.Create("Comms_Tick", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Bip da radio di bordo: due toni quadri smussati da 90 ms, 1320 Hz poi 880 Hz, con 30 ms di pausa.
        private static AudioClip SynthBeep()
        {
            const int rate = 44100;
            const int tone = rate * 90 / 1000;
            const int gap = rate * 30 / 1000;
            var data = new float[tone * 2 + gap];
            for (int t = 0; t < 2; t++)
            {
                float freq = t == 0 ? 1320f : 880f;
                int start = t * (tone + gap);
                for (int i = 0; i < tone; i++)
                {
                    float wave = Mathf.Clamp(Mathf.Sin(2f * Mathf.PI * freq * i / rate) * 3f, -1f, 1f);
                    float envelope = Mathf.Min(1f, i / 220f) * Mathf.Min(1f, (tone - i) / 440f);
                    data[start + i] = wave * envelope * 0.35f;
                }
            }
            AudioClip clip = AudioClip.Create("Comms_Beep", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
