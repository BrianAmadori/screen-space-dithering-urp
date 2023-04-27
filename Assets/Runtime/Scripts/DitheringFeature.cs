using System;
using Brian.Dithering;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class DitheringFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [System.Serializable]
        public class Dithering
        {
            [Header("Dithering")]
            public Palette palette;
            public Pattern pattern;
            public Texture2D patternTexture;

            [Header("Grain")] 
            public bool grainColored;

            [Range(0f, 1f), Tooltip("Grain strength. Higher means more visible grain.")]
            public float grainIntensity;

            [Range(0.3f, 3f), Tooltip("Grain particle size.")]
            public float grainSize;

            [Range(0f, 1f), Tooltip("Controls the noisiness response curve based on scene luminance. Lower values mean less noise in dark areas.")]
            public float grainLuminanceContribution;

            [Tooltip("Is the grain static or animated.")]
            public bool grainAnimated;
        }

        public RenderPassEvent Event = RenderPassEvent.AfterRendering;
        public bool setInverseViewMatrix = false;
        public bool requireDepthNormals = false;

        public Target srcType = Target.CameraColor;
        public string srcTextureId = "_CameraColorTexture";
        public RenderTexture srcTextureObject;

        public Target dstType = Target.CameraColor;
        public string dstTextureId = "_BlitPassTexture";
        public RenderTexture dstTextureObject;

        public bool overrideGraphicsFormat = false;
        public UnityEngine.Experimental.Rendering.GraphicsFormat graphicsFormat;

        public Dithering dithering = new Dithering();
    }

    public enum Target
    {
        CameraColor,
        TextureID,
        RenderTextureObject
    }

    public Settings settings = new Settings();
    public DitheringPass ditheringPass;

    public override void Create()
    {
        ditheringPass = new DitheringPass(settings.Event, settings, name);

#if !UNITY_2021_2_OR_NEWER
        if (settings.Event == RenderPassEvent.AfterRenderingPostProcessing)
        {
            Debug.LogWarning(
                "Note that the \"After Rendering Post Processing\"'s Color target doesn't seem to work? (or might work, but doesn't contain the post processing) :( -- Use \"After Rendering\" instead!");
        }
#endif

        if (settings.graphicsFormat == UnityEngine.Experimental.Rendering.GraphicsFormat.None)
        {
            settings.graphicsFormat =
                SystemInfo.GetGraphicsFormat(UnityEngine.Experimental.Rendering.DefaultFormat.HDR);
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
#if !UNITY_2021_2_OR_NEWER
        // AfterRenderingPostProcessing event is fixed in 2021.2+ so this workaround is no longer required

        if (settings.Event == RenderPassEvent.AfterRenderingPostProcessing)
        {
        }
        else if (settings.Event == RenderPassEvent.AfterRendering && renderingData.postProcessingEnabled)
        {
            // If event is AfterRendering, and src/dst is using CameraColor, switch to _AfterPostProcessTexture instead.
            if (settings.srcType == Target.CameraColor)
            {
                settings.srcType = Target.TextureID;
                settings.srcTextureId = "_AfterPostProcessTexture";
            }

            if (settings.dstType == Target.CameraColor)
            {
                settings.dstType = Target.TextureID;
                settings.dstTextureId = "_AfterPostProcessTexture";
            }
        }
        else
        {
            // If src/dst is using _AfterPostProcessTexture, switch back to CameraColor
            if (settings.srcType == Target.TextureID && settings.srcTextureId == "_AfterPostProcessTexture")
            {
                settings.srcType = Target.CameraColor;
                settings.srcTextureId = "";
            }

            if (settings.dstType == Target.TextureID && settings.dstTextureId == "_AfterPostProcessTexture")
            {
                settings.dstType = Target.CameraColor;
                settings.dstTextureId = "";
            }
        }
#endif

        ditheringPass.Setup(renderer);
        renderer.EnqueuePass(ditheringPass);
    }
}