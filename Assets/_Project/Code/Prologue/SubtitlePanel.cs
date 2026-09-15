using UnityEngine;
using UnityEngine.Localization.Components;

namespace Deeploration.Prologue
{
    public sealed class SubtitlePanel : MonoBehaviour
    {
        [SerializeField] private GameObject view;
        [SerializeField] private LocalizeStringEvent speaker;
        [SerializeField] private LocalizeStringEvent body;
        [SerializeField] private UnityEngine.UI.Text speakerText;
        [SerializeField] private UnityEngine.UI.Text bodyText;

        public void SetSpeakerText(string text)
        {
            if (speakerText != null) speakerText.text = text;
        }

        public void SetBodyText(string text)
        {
            if (bodyText != null) bodyText.text = text;
        }

        private void Awake()
        {
            if (view == null || speaker == null || body == null ||
                view == gameObject ||
                !speaker.transform.IsChildOf(view.transform) ||
                !body.transform.IsChildOf(view.transform))
            {
                Debug.LogError("Configurazione SubtitlePanel non valida.", this);
                enabled = false;
                return;
            }

            if (speakerText == null && speaker != null) speakerText = speaker.GetComponent<UnityEngine.UI.Text>();
            if (bodyText == null && body != null) bodyText = body.GetComponent<UnityEngine.UI.Text>();

            if (speaker != null)
            {
                speaker.OnUpdateString.RemoveListener(SetSpeakerText);
                speaker.OnUpdateString.AddListener(SetSpeakerText);
            }

            if (body != null)
            {
                body.OnUpdateString.RemoveListener(SetBodyText);
                body.OnUpdateString.AddListener(SetBodyText);
            }
        }

        private void OnDestroy()
        {
            if (speaker != null) speaker.OnUpdateString.RemoveListener(SetSpeakerText);
            if (body != null) body.OnUpdateString.RemoveListener(SetBodyText);
        }

        public void Show(SubtitleLine line)
        {
            if (!isActiveAndEnabled) return;

            Clear();

            if (line == null || line.text == null || line.text.IsEmpty)
            {
                Debug.LogError("Battuta mancante o senza testo.", this);
                return;
            }

            if (line.speaker != null && !line.speaker.IsEmpty)
            {
                speaker.StringReference = line.speaker;
                try
                {
                    string spk = line.speaker.GetLocalizedString();
                    if (!string.IsNullOrEmpty(spk)) SetSpeakerText(spk);
                }
                catch
                {
                    // Se la localizzazione non è ancora pronta, delega a OnUpdateString
                }
            }
            else
            {
                speaker.StringReference = null;
                speaker.OnUpdateString.Invoke(string.Empty);
            }

            body.StringReference = line.text;
            try
            {
                string bdy = line.text.GetLocalizedString();
                if (!string.IsNullOrEmpty(bdy)) SetBodyText(bdy);
            }
            catch
            {
                // Se la localizzazione non è ancora pronta, delega a OnUpdateString
            }

            view.SetActive(true);
        }

        public void Clear()
        {
            if (speaker != null)
            {
                speaker.StringReference = null;
                speaker.OnUpdateString.Invoke(string.Empty);
            }

            if (body != null)
            {
                body.StringReference = null;
                body.OnUpdateString.Invoke(string.Empty);
            }

            if (speakerText != null) speakerText.text = string.Empty;
            if (bodyText != null) bodyText.text = string.Empty;

            if (view != null)
                view.SetActive(false);
        }

        private void OnDisable()
        {
            if (speaker != null) speaker.OnUpdateString.RemoveListener(SetSpeakerText);
            if (body != null) body.OnUpdateString.RemoveListener(SetBodyText);
            Clear();
        }
    }
}
