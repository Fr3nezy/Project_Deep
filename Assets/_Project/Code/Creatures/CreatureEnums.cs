namespace Deeploration.Creatures
{
    /// <summary>Comportamento attivo di una creatura. Gli stati crescono con le task di M2 (luce, orbita, morso…).</summary>
    public enum CreatureState
    {
        /// <summary>Vaga attorno al punto di casa (il nido) senza meta.</summary>
        Wander,
        /// <summary>Si allontana dal sub alla velocità di fuga finché non torna calma.</summary>
        Flee
    }

    /// <summary>
    /// Livello di dettaglio dell'intelligenza, deciso dal <see cref="CreatureManager"/> per distanza dal sub e
    /// visibilità in camera. Il movimento resta fluido a ogni livello: cambia quanto spesso la creatura ragiona
    /// e quanti raggi lancia.
    /// </summary>
    public enum CreatureLod
    {
        /// <summary>Vicina e visibile: ragiona e lancia raggi a ogni frame.</summary>
        Near,
        /// <summary>A media distanza, o vicina ma fuori inquadratura: ragiona e lancia raggi a intervalli.</summary>
        Mid,
        /// <summary>Lontana: ragiona di rado e non lancia raggi.</summary>
        Far
    }
}
