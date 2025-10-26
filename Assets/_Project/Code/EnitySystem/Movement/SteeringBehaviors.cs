using UnityEngine;

namespace Deeploration.EntitySystem
{
    /// <summary>
    /// Classe statica che contiene le logiche di steering per il movimento delle entità.
    /// Implementa comportamenti come Seek, Flee, Separation, e Wander.
    /// </summary>
    public static class SteeringBehaviors
    {
        /// <summary>
        /// Calcola la forza di steering per muoversi verso un target (Seek).
        /// </summary>
        /// <param name="currentPosition">Posizione corrente</param>
        /// <param name="currentVelocity">Velocità corrente</param>
        /// <param name="targetPosition">Posizione del target</param>
        /// <param name="maxSpeed">Velocità massima</param>
        /// <param name="maxForce">Forza di steering massima</param>
        /// <returns>Forza di steering da applicare</returns>
        public static Vector3 Seek(Vector3 currentPosition, Vector3 currentVelocity, Vector3 targetPosition, float maxSpeed, float maxForce)
        {
            // Calcola direzione desiderata
            Vector3 desired = (targetPosition - currentPosition).normalized * maxSpeed;
            
            // Calcola forza di steering (differenza tra velocità desiderata e corrente)
            Vector3 steer = desired - currentVelocity;
            
            // Limita la forza
            return Vector3.ClampMagnitude(steer, maxForce);
        }

        /// <summary>
        /// Calcola la forza di steering per allontanarsi da un target (Flee).
        /// </summary>
        /// <param name="currentPosition">Posizione corrente</param>
        /// <param name="currentVelocity">Velocità corrente</param>
        /// <param name="targetPosition">Posizione da cui fuggire</param>
        /// <param name="maxSpeed">Velocità massima</param>
        /// <param name="maxForce">Forza di steering massima</param>
        /// <returns>Forza di steering da applicare</returns>
        public static Vector3 Flee(Vector3 currentPosition, Vector3 currentVelocity, Vector3 targetPosition, float maxSpeed, float maxForce)
        {
            // Calcola direzione opposta al target
            Vector3 desired = (currentPosition - targetPosition).normalized * maxSpeed;
            
            // Calcola forza di steering
            Vector3 steer = desired - currentVelocity;
            
            return Vector3.ClampMagnitude(steer, maxForce);
        }

        /// <summary>
        /// Calcola la forza di steering per evitare collisioni con altre entità (Separation).
        /// </summary>
        /// <param name="currentPosition">Posizione corrente</param>
        /// <param name="neighbors">Array di posizioni delle entità vicine</param>
        /// <param name="separationRadius">Raggio di separazione desiderato</param>
        /// <param name="maxForce">Forza di steering massima</param>
        /// <returns>Forza di steering da applicare</returns>
        public static Vector3 Separation(Vector3 currentPosition, Vector3[] neighbors, float separationRadius, float maxForce)
        {
            Vector3 steer = Vector3.zero;
            int count = 0;

            foreach (Vector3 neighbor in neighbors)
            {
                float distance = Vector3.Distance(currentPosition, neighbor);
                
                if (distance > 0 && distance < separationRadius)
                {
                    // Calcola vettore di allontanamento, pesato per la distanza
                    Vector3 diff = (currentPosition - neighbor).normalized;
                    diff /= distance; // Peso inversamente proporzionale alla distanza
                    
                    steer += diff;
                    count++;
                }
            }

            if (count > 0)
            {
                steer /= count; // Media
                steer = steer.normalized * maxForce;
            }

            return steer;
        }

        /// <summary>
        /// Calcola la forza di steering per vagabondare casualmente (Wander) - DEPRECATO.
        /// Usa WanderPerlin per movimento più organico.
        /// </summary>
        [System.Obsolete("Use WanderPerlin instead for more organic movement")]
        public static Vector3 Wander(Vector3 currentVelocity, ref float wanderAngle, float wanderDistance, float wanderRadius, float wanderJitter, float maxForce)
        {
            // Aggiungi variazione casuale all'angolo
            wanderAngle += Random.Range(-wanderJitter, wanderJitter);

            // Calcola punto sul cerchio di wander
            Vector3 circleCenter = currentVelocity.normalized * wanderDistance;
            Vector3 displacement = new Vector3(
                Mathf.Cos(wanderAngle * Mathf.Deg2Rad),
                Mathf.Sin(wanderAngle * Mathf.Deg2Rad),
                0f
            ) * wanderRadius;

            // Converti in coordinate 3D (assume movimento su piano XZ con variazione Y)
            displacement = new Vector3(displacement.x, displacement.y * 0.3f, displacement.z);

            Vector3 wanderForce = circleCenter + displacement;
            
            return Vector3.ClampMagnitude(wanderForce, maxForce);
        }

        /// <summary>
        /// Calcola la forza di steering per vagabondare organicamente usando Perlin Noise.
        /// Movimento fluido e naturale su piano XZ con variazione Y limitata.
        /// </summary>
        /// <param name="currentPosition">Posizione corrente</param>
        /// <param name="currentVelocity">Velocità corrente</param>
        /// <param name="time">Tempo corrente (usa Time.time)</param>
        /// <param name="wanderStrength">Intensità del movimento (0.5-2)</param>
        /// <param name="wanderFrequency">Velocità di cambio direzione (0.1-2)</param>
        /// <param name="perlinScale">Scala del noise (più basso = più fluido)</param>
        /// <param name="maxForce">Forza di steering massima</param>
        /// <returns>Forza di steering da applicare</returns>
        public static Vector3 WanderPerlin(
            Vector3 currentPosition,
            Vector3 currentVelocity,
            float time,
            float wanderStrength,
            float wanderFrequency,
            float perlinScale,
            float maxForce)
        {
            // Usa la posizione come seed per offset unici per ogni entità
            float offsetX = currentPosition.x * 0.1f;
            float offsetZ = currentPosition.z * 0.1f;

            // 3 campioni Perlin indipendenti per X, Y, Z
            // Centrato su 0.5, poi rimappato a [-0.5, 0.5]
            float noiseX = Mathf.PerlinNoise(time * wanderFrequency + offsetX, 0f) - 0.5f;
            float noiseY = Mathf.PerlinNoise(0f, time * wanderFrequency + offsetZ) - 0.5f;
            float noiseZ = Mathf.PerlinNoise(time * wanderFrequency + offsetX, time * wanderFrequency + offsetZ) - 0.5f;

            // Costruisci forza wander
            // Movimento principalmente orizzontale (XZ), poco verticale (Y)
            Vector3 wanderForce = new Vector3(
                noiseX * wanderStrength,
                noiseY * wanderStrength * 0.2f,  // Ridotto movimento verticale
                noiseZ * wanderStrength
            );

            // Applica scala perlin per controllo della fluidità
            wanderForce *= perlinScale;

            // Combina con direzione corrente per continuità
            if (currentVelocity.magnitude > 0.1f)
            {
                Vector3 forward = currentVelocity.normalized;
                wanderForce += forward * 0.3f;  // Mantieni un po' della direzione corrente
            }

            return Vector3.ClampMagnitude(wanderForce, maxForce);
        }

        /// <summary>
        /// Calcola la forza di steering per seguire un percorso curvo verso un target (Pursuit).
        /// Predice la posizione futura del target basandosi sulla sua velocità.
        /// </summary>
        /// <param name="currentPosition">Posizione corrente</param>
        /// <param name="currentVelocity">Velocità corrente</param>
        /// <param name="targetPosition">Posizione del target</param>
        /// <param name="targetVelocity">Velocità del target</param>
        /// <param name="maxSpeed">Velocità massima</param>
        /// <param name="maxForce">Forza di steering massima</param>
        /// <returns>Forza di steering da applicare</returns>
        public static Vector3 Pursuit(Vector3 currentPosition, Vector3 currentVelocity, Vector3 targetPosition, Vector3 targetVelocity, float maxSpeed, float maxForce)
        {
            // Calcola tempo di predizione basato sulla distanza
            float distance = Vector3.Distance(currentPosition, targetPosition);
            float predictionTime = distance / maxSpeed;

            // Predici posizione futura del target
            Vector3 futurePosition = targetPosition + targetVelocity * predictionTime;

            // Usa Seek verso la posizione predetta
            return Seek(currentPosition, currentVelocity, futurePosition, maxSpeed, maxForce);
        }

        /// <summary>
        /// Calcola la forza di steering per evitare un target in movimento (Evade).
        /// </summary>
        /// <param name="currentPosition">Posizione corrente</param>
        /// <param name="currentVelocity">Velocità corrente</param>
        /// <param name="targetPosition">Posizione del target</param>
        /// <param name="targetVelocity">Velocità del target</param>
        /// <param name="maxSpeed">Velocità massima</param>
        /// <param name="maxForce">Forza di steering massima</param>
        /// <returns>Forza di steering da applicare</returns>
        public static Vector3 Evade(Vector3 currentPosition, Vector3 currentVelocity, Vector3 targetPosition, Vector3 targetVelocity, float maxSpeed, float maxForce)
        {
            // Predici posizione futura del target
            float distance = Vector3.Distance(currentPosition, targetPosition);
            float predictionTime = distance / maxSpeed;
            Vector3 futurePosition = targetPosition + targetVelocity * predictionTime;

            // Usa Flee dalla posizione predetta
            return Flee(currentPosition, currentVelocity, futurePosition, maxSpeed, maxForce);
        }

        /// <summary>
        /// Limita la velocità per mantenere l'entità entro confini specificati.
        /// Applica una forza repulsiva quando si avvicina ai confini.
        /// </summary>
        /// <param name="currentPosition">Posizione corrente</param>
        /// <param name="boundsCenter">Centro dei confini</param>
        /// <param name="boundsSize">Dimensioni dei confini</param>
        /// <param name="maxForce">Forza di steering massima</param>
        /// <returns>Forza di steering da applicare</returns>
        public static Vector3 StayInBounds(Vector3 currentPosition, Vector3 boundsCenter, Vector3 boundsSize, float maxForce)
        {
            Vector3 steer = Vector3.zero;
            float margin = 5f; // Margine prima di applicare la forza

            // Controlla ogni asse
            if (Mathf.Abs(currentPosition.x - boundsCenter.x) > boundsSize.x / 2 - margin)
            {
                steer.x = Mathf.Sign(boundsCenter.x - currentPosition.x) * maxForce;
            }

            if (Mathf.Abs(currentPosition.y - boundsCenter.y) > boundsSize.y / 2 - margin)
            {
                steer.y = Mathf.Sign(boundsCenter.y - currentPosition.y) * maxForce;
            }

            if (Mathf.Abs(currentPosition.z - boundsCenter.z) > boundsSize.z / 2 - margin)
            {
                steer.z = Mathf.Sign(boundsCenter.z - currentPosition.z) * maxForce;
            }

            return steer;
        }

        /// <summary>
        /// Combina multiple forze di steering con pesi specifici.
        /// </summary>
        /// <param name="forces">Array di tuple (forza, peso)</param>
        /// <param name="maxForce">Forza massima risultante</param>
        /// <returns>Forza di steering combinata</returns>
        public static Vector3 CombineForces((Vector3 force, float weight)[] forces, float maxForce)
        {
            Vector3 totalForce = Vector3.zero;

            foreach (var (force, weight) in forces)
            {
                totalForce += force * weight;
            }

            return Vector3.ClampMagnitude(totalForce, maxForce);
        }

        /// <summary>
        /// Calcola una forza per mantenere una profondità preferita nell'oceano.
        /// </summary>
        /// <param name="currentY">Altezza corrente</param>
        /// <param name="preferredY">Altezza preferita</param>
        /// <param name="tolerance">Tolleranza prima di correggere</param>
        /// <param name="maxForce">Forza massima</param>
        /// <returns>Forza verticale da applicare</returns>
        public static float MaintainDepth(float currentY, float preferredY, float tolerance, float maxForce)
        {
            float difference = preferredY - currentY;

            if (Mathf.Abs(difference) < tolerance)
            {
                return 0f;
            }

            return Mathf.Clamp(difference, -maxForce, maxForce);
        }
    }
}
