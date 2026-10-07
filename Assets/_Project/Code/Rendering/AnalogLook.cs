using System;
using UnityEngine.Rendering;

namespace Deeploration.Rendering
{
    /// <summary>
    /// Override di Volume per il look "analogico" (Iron Lung, Buckshot Roulette): pixel grossi,
    /// colori a pochi livelli con dither ordinato e scanline. Lo applica AnalogLookFeature.
    /// </summary>
    [Serializable]
    [VolumeComponentMenu("Deeplonauts/Analog Look")]
    public sealed class AnalogLook : VolumeComponent
    {
        public ClampedFloatParameter intensity = new(0f, 0f, 1f);

        [UnityEngine.Tooltip("Lato del pixel virtuale, in pixel a 1080p (scala con la risoluzione).")]
        public ClampedFloatParameter pixelSize = new(3f, 1f, 8f);

        [UnityEngine.Tooltip("Livelli per canale colore.")]
        public ClampedIntParameter colorLevels = new(12, 2, 64);

        [UnityEngine.Tooltip("Forza del dither ordinato (Bayer 4x4) tra un livello e l'altro.")]
        public ClampedFloatParameter dither = new(1f, 0f, 1f);

        [UnityEngine.Tooltip("Oscuramento delle righe alterne.")]
        public ClampedFloatParameter scanlines = new(0.15f, 0f, 1f);

        public bool IsActive() => active && intensity.value > 0f;
    }
}
