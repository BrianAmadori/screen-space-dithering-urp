using System;
using Brian.Dithering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Camera))]
public class Dithering : MonoBehaviour
{
    public bool enabled = false;

    public Palette palette = null;
    public Pattern pattern = null;
    public Texture2D patternTexture = null;

    [Header("Grain")] 
    public bool grainColored = false;

    [Range(0f, 1f), Tooltip("Grain strength. Higher means more visible grain.")]
    public float grainIntensity = 0;

    [Range(0.3f, 3f), Tooltip("Grain particle size.")]
    public float grainSize = 0;

    [Range(0f, 1f), Tooltip("Controls the noisiness response curve based on scene luminance. Lower values mean less noise in dark areas.")]
    public float grainLuminanceContribution = 0;

    [Tooltip("Is the grain static or animated.")]
    public bool grainAnimated = false;
}