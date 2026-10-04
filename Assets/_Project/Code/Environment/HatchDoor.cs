using UnityEngine;
using UnityEngine.Events;

namespace Deeploration.Environment
{
    public class HatchDoor : MonoBehaviour
    {
        [Header("Cerniera")]
        [Tooltip("Transform ruotato all'apertura. Il pivot deve stare sull'asse della cerniera. Se vuoto usa questo oggetto.")]
        [SerializeField] private Transform doorPivot;
        [Tooltip("Asse di rotazione nello spazio locale del pivot.")]
        [SerializeField] private Vector3 hingeAxis = Vector3.right;
        [SerializeField] private float openAngle = 110f;
        [SerializeField] private float openDuration = 1.6f;
        [SerializeField] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Eventi")]
        public UnityEvent onOpenStarted = new UnityEvent();
        public UnityEvent onOpened = new UnityEvent();

        [Header("Debug")]
        [SerializeField] private bool isOpen;

        private Quaternion closedRotation;
        private float elapsed = -1f;

        public bool IsOpen => isOpen;
        public bool IsMoving => elapsed >= 0f;

        private void Awake()
        {
            if (doorPivot == null) doorPivot = transform;
            closedRotation = doorPivot.localRotation;
        }

        private void Update()
        {
            if (elapsed < 0f) return;

            elapsed += Time.deltaTime;
            float t = openDuration > 0f ? Mathf.Clamp01(elapsed / openDuration) : 1f;
            doorPivot.localRotation = closedRotation * Quaternion.AngleAxis(openCurve.Evaluate(t) * openAngle, hingeAxis);

            if (t >= 1f)
            {
                elapsed = -1f;
                onOpened?.Invoke();
            }
        }

        public void Open()
        {
            if (isOpen) return;
            isOpen = true;
            elapsed = 0f;
            onOpenStarted?.Invoke();
        }
    }
}
