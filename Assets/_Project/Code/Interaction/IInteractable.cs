using UnityEngine;

namespace Deeploration.Interaction
{
    public interface IInteractable
    {
        string PromptText { get; }
        bool CanInteract(GameObject user);
        void Interact(GameObject user);
    }
}
