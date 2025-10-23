using UnityEngine;

namespace Deeploration.EntitySystem
{
    /// <summary>
    /// Tipi di entità nel sistema. Usato per identificare prede e predatori.
    /// </summary>
    public enum EntityType
    {
        None,
        SmallFish,      // Pesci piccoli (plancton eaters)
        MediumFish,     // Pesci medi (mangiano piccoli)
        LargeFish,      // Pesci grandi (predatori)
        Shark,          // Squali (top predator)
        Jellyfish,      // Meduse
        Crab,           // Granchi (bottom feeders)
        Player,          // Giocatore
        Plankton
    }

    /// <summary>
    /// Stati comportamentali dell'entità.
    /// </summary>
    public enum EntityState
    {
        Idle,       // Vagabondaggio casuale
        Hunt,       // Caccia di una preda
        Flee,       // Fuga da un predatore
        Rest,       // Riposo per recuperare stamina
        Dead        // Morto (per pooling)
    }

    /// <summary>
    /// Tipi di parametri vitali per l'API GetStatusValue.
    /// </summary>
    public enum StatusType
    {
        Health,
        Stamina,
        Hunger
    }
}
