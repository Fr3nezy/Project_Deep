using UnityEngine;

namespace Deeploration.Environment
{
    /// <summary>
    /// Mantiene il sistema di particelle (Marine Snow) centrato sulla visuale del Diver,
    /// simulando l'abisso oceanico con costo computazionale minimo.
    /// </summary>
    [ExecuteAlways]
    public sealed class MarineSnowFollower : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 1f, 3f);

        private void Start()
        {
            if (target == null && Camera.main != null)
            {
                target = Camera.main.transform;
            }
        }

        private void LateUpdate()
        {
            if (target == null && Camera.main != null)
            {
                target = Camera.main.transform;
            }

            if (target != null)
            {
                transform.position = target.position + target.rotation * offset;
            }
        }
    }
}
