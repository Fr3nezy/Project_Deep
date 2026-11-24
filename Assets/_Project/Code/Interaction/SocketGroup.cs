using UnityEngine;
using UnityEngine.Events;

namespace Deeploration.Interaction
{
    /// <summary>
    /// Gate a N su N: parte onAllSocketed una volta, quando tutti i GrabSocket elencati sono occupati.
    /// Il conteggio segue anche le celle sfilate; il completamento resta.
    /// </summary>
    public sealed class SocketGroup : MonoBehaviour
    {
        [SerializeField] private GrabSocket[] sockets = new GrabSocket[0];

        public UnityEvent onAllSocketed = new UnityEvent();

        [Header("Debug (sola lettura)")]
        [SerializeField] private int filled;
        [SerializeField] private bool completed;

        public int Filled => filled;
        public int Total => sockets.Length;
        public bool Completed => completed;
        public GrabSocket GetSocket(int index) => sockets[index];

        private void OnEnable()
        {
            foreach (GrabSocket socket in sockets)
                if (socket != null) { socket.onSocketed.AddListener(Refresh); socket.onUnsocketed.AddListener(Refresh); }
            Refresh();
        }

        private void OnDisable()
        {
            foreach (GrabSocket socket in sockets)
                if (socket != null) { socket.onSocketed.RemoveListener(Refresh); socket.onUnsocketed.RemoveListener(Refresh); }
        }

        private void Refresh()
        {
            filled = 0;
            foreach (GrabSocket socket in sockets)
                if (socket != null && socket.IsOccupied) filled++;
            if (completed || sockets.Length == 0 || filled < sockets.Length) return;
            completed = true;
            onAllSocketed.Invoke();
        }
    }
}
