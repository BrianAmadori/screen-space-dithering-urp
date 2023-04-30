using System;
using Brian.Dithering;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class DitheringFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public bool setInverseViewMatrix = false;
        public bool requireDepthNormals = false;

        [NonSerialized] public RenderPassEvent Event = RenderPassEvent.AfterRenderingPostProcessing;

        [NonSerialized] public Target srcType = Target.CameraColor;
        [NonSerialized] public string srcTextureId = "_CameraColorTexture";
        [NonSerialized] public RenderTexture srcTextureObject;

        [NonSerialized] public Target dstType = Target.CameraColor;
        [NonSerialized] public string dstTextureId = "_BlitPassTexture";
        [NonSerialized] public RenderTexture dstTextureObject;

        public bool overrideGraphicsFormat = false;
        public UnityEngine.Experimental.Rendering.GraphicsFormat graphicsFormat;
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
        bool isOverlayCamera = renderingData.cameraData.renderType == CameraRenderType.Overlay;

#if !UNITY_2021_2_OR_NEWER
        // AfterRenderingPostProcessing event is fixed in 2021.2+ so this workaround is no longer required
        if (isOverlayCamera)
        {
            settings.srcType = Target.CameraColor;
            settings.srcTextureId = "";

            settings.dstType = Target.CameraColor;
            settings.dstTextureId = "";
        }
        else
        {
            settings.srcType = Target.TextureID;
            settings.srcTextureId = "_AfterPostProcessTexture";

            settings.dstType = Target.TextureID;
            settings.dstTextureId = "_AfterPostProcessTexture";
        }
#endif

        ditheringPass.Setup(renderer);
        renderer.EnqueuePass(ditheringPass);
    }
}