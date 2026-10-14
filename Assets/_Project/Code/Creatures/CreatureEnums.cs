namespace Deeploration.Creatures
{
    /// <summary>Comportamento attivo di una creatura. Gli stati crescono con le task di M2 (luce, orbita, morso…).</summary>
    public enum CreatureState
    {
        /// <summary>Vaga attorno al punto di casa (il nido) senza meta.</summary>
        Wander,
        /// <summary>Si allontana dallo stimolo percepito alla velocità di fuga finché non torna calma.</summary>
        Flee,
        /// <summary>Si avvicina a uno stimolo che la attira, si ferma a distanza e ci gira attorno osservandolo.</summary>
        Inspect,
        /// <summary>
        /// Predatore: gira attorno al sub finché è a portata (<c>stalkRange</c>); la luce e il rumore del sub fanno
        /// salire l'aggressività e stringono l'orbita.
        /// </summary>
        Stalk
    }

    /// <summary>
    /// Categoria di uno stimolo: ogni specie sceglie nel profilo come reagire a ciascuna. Le luci richiedono la
    /// linea di vista, il rumore no.
    /// </summary>
    public enum StimulusCategory
    {
        /// <summary>Nessuno stimolo percepito.</summary>
        None,
        /// <summary>Luce debole e stabile: torcia bassa, scia del prato di penne, creature luminose.</summary>
        WeakLight,
        /// <summary>Luce forte e stabile: torcia piena.</summary>
        StrongLight,
        /// <summary>Accensione improvvisa di una luce forte (pochi decimi di secondo).</summary>
        Flash,
        /// <summary>Rumore: passi, atterraggi.</summary>
        Noise
    }

    /// <summary>Reazione di una specie a una categoria di stimolo.</summary>
    public enum StimulusReaction
    {
        /// <summary>Non reagisce.</summary>
        Ignore,
        /// <summary>Si avvicina alla sorgente fino a una distanza di sicurezza e la osserva (stato Inspect).</summary>
        Approach,
        /// <summary>Scappa dalla sorgente (stato Flee). Vince su Approach.</summary>
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
