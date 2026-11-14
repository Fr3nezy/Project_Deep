using UnityEngine;

namespace Deeploration.DevTools
{
    /// <summary>
    /// Punto di teletrasporto del menu cheat e possibile partenza fuori dalla capsula.
    /// Il diver ci arriva in piedi, posato sul fondale sotto il marker, rivolto lungo la freccia (asse Z locale).
    /// </summary>
    public sealed class DevWarpPoint : MonoBehaviour
    {
        [Tooltip("Nome nel menu cheat. Vuoto = nome dell'oggetto.")]
        [SerializeField] private string label;

        [Tooltip("Posizione nella lista del menu (crescente; a pari valore, ordine alfabetico).")]
        [SerializeField] private int order;

        public string Label => string.IsNullOrEmpty(label) ? name : label;
        public int Order => order;

        private static readonly Color MarkerColor = new Color(1f, 0.62f, 0.1f); // ambra

        private void OnDrawGizmos()
        {
            DrawMarker(transform, MarkerColor, Label);
        }

        /// <summary>Sagoma del diver (2 m) con la freccia della direzione di arrivo e il nome.</summary>
        public static void DrawMarker(Transform t, Color color, string text)
        {
            Vector3 feet = t.position;
            Vector3 forward = Quaternion.Euler(0f, t.eulerAngles.y, 0f) * Vector3.forward;
            Gizmos.color = color;
            Gizmos.matrix = Matrix4x4.TRS(feet, Quaternion.Euler(0f, t.eulerAngles.y, 0f), Vector3.one);
            Gizmos.DrawWireCube(Vector3.up, new Vector3(1f, 2f, 1f));
            Gizmos.matrix = Matrix4x4.identity;

            Vector3 eye = feet + Vector3.up * 1.4f;
            Vector3 tip = eye + forward * 1.5f;
            Vector3 side = Vector3.Cross(Vector3.up, forward) * 0.25f;
            Gizmos.DrawLine(eye, tip);
            Gizmos.DrawLine(tip, tip - forward * 0.4f + side);
            Gizmos.DrawLine(tip, tip - forward * 0.4f - side);
#if UNITY_EDITOR
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.Label(feet + Vector3.up * 2.4f, text);
#endif
        }

#if UNITY_EDITOR
        [ContextMenu("Parti da qui")]
        private void StartHere()
        {
            CheatMenu menu = FindAnyObjectByType<CheatMenu>();
            if (menu == null)
            {
                Debug.LogWarning("[DevWarpPoint] Nessun CheatMenu in scena.", this);
                return;
            }
            menu.SetStartPoint(this);
        }
#endif
    }
}
