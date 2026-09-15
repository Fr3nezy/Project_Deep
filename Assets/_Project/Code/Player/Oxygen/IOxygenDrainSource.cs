namespace Deeploration.Player
{
    /// <summary>
    /// Contratto di consumo ossigeno. Ogni sistema che deve incidere sul respiro del diver
    /// (attività fisica, stress comportamentale, stress ambientale, equipaggiamento) implementa
    /// questa interfaccia. OxygenSystem li raccoglie senza conoscerne i tipi concreti: i sistemi
    /// restano indipendenti tra loro e si aggiungono senza modificare il serbatoio.
    /// </summary>
    public interface IOxygenDrainSource
    {
        /// <summary>Ossigeno al secondo richiesto in questo istante. 0 se la fonte è inattiva.</summary>
        float GetOxygenDrainPerSecond();
    }
}
