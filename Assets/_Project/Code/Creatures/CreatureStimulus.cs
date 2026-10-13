using System.Collections.Generic;
using UnityEngine;

namespace Deeploration.Creatures
{
    /// <summary>
    /// Uno stimolo che le creature possono percepire in questo frame: una luce (fascio direzionale o punto) o un
    /// rumore. Le portate sono quelle per una creatura con sensibilità 1; ogni specie le scala con le proprie
    /// sensibilità del <see cref="CreatureProfile"/>. Gli stimoli vengono raccolti dal <see cref="CreatureManager"/>
    /// una volta per frame dalle sorgenti registrate (<see cref="IStimulusSource"/>).
    /// </summary>
    public struct CreatureStimulus
    {
        /// <summary>Valore di <see cref="ConeCos"/> per una luce visibile da ogni direzione.</summary>
        public const float Omnidirectional = -2f;

        /// <summary>Tipo di stimolo.</summary>
        public StimulusKind Kind;

        /// <summary>Punto da cui parte lo stimolo; per una luce è anche l'origine del raggio di linea di vista.</summary>
        public Vector3 Position;

        /// <summary>Portata (m) per una creatura con sensibilità 1.</summary>
        public float Radius;

        /// <summary>Asse del fascio (normalizzato), solo per le luci direzionali.</summary>
        public Vector3 Direction;

        /// <summary>Coseno del semi-angolo del fascio; <see cref="Omnidirectional"/> per un punto luce.</summary>
        public float ConeCos;

        /// <summary>Entro questa distanza (m, a sensibilità 1) una luce direzionale si vede anche fuori dal fascio.</summary>
        public float HaloRadius;

        /// <summary>True se lo stimolo viene dal sub.</summary>
        public bool FromPlayer;

        /// <summary>Rumore udibile in ogni direzione, attraverso gli ostacoli.</summary>
        public static CreatureStimulus Noise(Vector3 position, float radius, bool fromPlayer) => new CreatureStimulus
        {
            Kind = StimulusKind.Noise, Position = position, Radius = radius, ConeCos = Omnidirectional,
            FromPlayer = fromPlayer
        };

        /// <summary>Punto luce visibile da ogni direzione, se non ci sono ostacoli in mezzo.</summary>
        public static CreatureStimulus PointLight(Vector3 position, float radius, bool fromPlayer) => new CreatureStimulus
        {
            Kind = StimulusKind.Light, Position = position, Radius = radius, ConeCos = Omnidirectional,
            HaloRadius = radius, FromPlayer = fromPlayer
        };

        /// <summary>
        /// Fascio di luce: si vede dentro il cono di semi-angolo <paramref name="halfAngle"/> (gradi) fino a
        /// <paramref name="radius"/>, e da ogni direzione entro <paramref name="haloRadius"/>.
        /// </summary>
        public static CreatureStimulus Beam(Vector3 position, Vector3 direction, float radius, float halfAngle,
            float haloRadius, bool fromPlayer) => new CreatureStimulus
        {
            Kind = StimulusKind.Light, Position = position, Radius = radius, Direction = direction.normalized,
            ConeCos = Mathf.Cos(halfAngle * Mathf.Deg2Rad), HaloRadius = haloRadius, FromPlayer = fromPlayer
        };
    }

    /// <summary>
    /// Qualcosa che emette stimoli per le creature: il sub, e in futuro le creature bioluminescenti. Il manager
    /// chiama <see cref="EmitStimuli"/> una volta per frame.
    /// </summary>
    public interface IStimulusSource
    {
        /// <summary>Aggiunge a <paramref name="output"/> gli stimoli attivi in questo frame (anche nessuno).</summary>
        void EmitStimuli(List<CreatureStimulus> output);
    }
}
