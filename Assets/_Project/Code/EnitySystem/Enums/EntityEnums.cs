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
    /// Stati comportamentali dell'entità (aggiornati per FOV).
    /// </summary>
    public enum EntityState
    {
        Idle,       // Vagabondaggio casuale
        Inspect,    // Avvicinamento lento per investigare (Detection Zone)
        Hunt,       // Caccia di una preda
        Flee,       // Fuga da un predatore
        Rest,       // Riposo per recuperare stamina
        Bite,       // Attacco diretto (Bite Zone)
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

    /// <summary>
    /// Stile di movimento dell'entità. Influenza il comportamento del wander.
    /// </summary>
    public enum MovementStyle
    {
        Calm,       // Movimento lento e fluido (grandi creature)
        Active,     // Movimento standard (pesci medi)
        Nervous     // Movimento veloce e scattoso (piccole creature)
    }

    /// <summary>
    /// Zone del campo visivo (FOV) del pesce.
    /// Determina il comportamento basato su distanza e angolo.
    /// </summary>
    public enum FovZone
    {
        None,       // Fuori campo visivo
        Detection5, // Zona di avvistamento esterno (5m)
        Decision4,  // Zona di decisione rapida (4m)
        Bite1       // Zona di morso/attacco (1m)
    }
}
