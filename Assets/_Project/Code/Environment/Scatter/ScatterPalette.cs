using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deeploration.Environment
{
    /// <summary>
    /// Associa il nome di un oggetto sorgente nella collection "ScatterSources" del .blend al prefab Unity
    /// che lo sostituisce quando ScatterRoot rigenera lo scatter.
    /// </summary>
    [CreateAssetMenu(menuName = "Deeplonauts/Scatter Palette", fileName = "ScatterPalette")]
    public class ScatterPalette : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("Nome esatto dell'oggetto sorgente in Blender.")]
            public string sourceName;
            public GameObject prefab;
            [Tooltip("Correzione in gradi se il prefab non ha gli stessi assi del sorgente in Blender.")]
            public Vector3 rotationOffset;
            public bool enabled = true;
        }

        public List<Entry> entries = new List<Entry>();

        /// <summary>Voce attiva con prefab per quel nome, o null.</summary>
        public Entry Find(string sourceName)
        {
            foreach (var e in entries)
                if (e != null && e.enabled && e.prefab != null && e.sourceName == sourceName)
                    return e;
            return null;
        }
    }
}
