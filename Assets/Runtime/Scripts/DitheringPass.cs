using Brian.Dithering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DitheringPass : ScriptableRenderPass
{
    private static readonly int _Grain_Params1 = Shader.PropertyToID("_Grain_Params1");
    private static readonly int _Grain_Params2 = Shader.PropertyToID("_Grain_Params2");
    private static readonly int _GrainTex      = Shader.PropertyToID("_GrainTex");
    private static readonly int _PatternTex = Shader.PropertyToID("_PatternTex");
    private static readonly int _PatternSize = Shader.PropertyToID("_PatternSize");
    private static readonly int _PaletteTex = Shader.PropertyToID("_PaletteTex");
    private static readonly int _PaletteHeight = Shader.PropertyToID("_PaletteHeight");
    private static readonly int _PaletteColorCount = Shader.PropertyToID("_PaletteColorCount");
    private static readonly int _InverseView = Shader.PropertyToID("_InverseView");

    private Shader ditheringShader = null;
    private Material ditheringMaterial = null;
    private Shader noiseLutShader = null;
    private Material noiseLutMaterial = null;
    
    public FilterMode filterMode { get; set; }

    private DitheringFeature.Settings settings;

    private RenderTargetIdentifier source { get; set; }
    private RenderTargetIdentifier destination { get; set; }

    RenderTargetHandle temporaryColorTexture;
    RenderTargetHandle destinationTexture;
    RenderTargetHandle noiseLutTexture;
    string profilerTag;

#if !UNITY_2020_2_OR_NEWER // v8
	private ScriptableRenderer renderer;
#endif

    public DitheringPass(RenderPassEvent renderPassEvent, DitheringFeature.Settings settings, string tag)
    {
        this.renderPassEvent = renderPassEvent;
        this.settings = settings;
        profilerTag = tag;
        temporaryColorTexture.Init("_TemporaryColorTexture");
        noiseLutTexture.Init("_NoiseLutTexture");

        ditheringShader = Shader.Find("Hidden/Dithering/Dithering Image Effect");
        ditheringMaterial = new Material(ditheringShader);
        ditheringMaterial.hideFlags = HideFlags.HideAndDontSave;

        noiseLutShader = Shader.Find("Hidden/Post FX/Grain Generator");
        noiseLutMaterial = new Material(noiseLutShader);
        noiseLutMaterial.hideFlags = HideFlags.HideAndDontSave;

        if (settings.dstType == DitheringFeature.Target.TextureID)
        {
            destinationTexture.Init(settings.dstTextureId);
        }
    }

    public void Setup(ScriptableRenderer renderer)
    {
#if UNITY_2020_2_OR_NEWER // v10+
        if (settings.requireDepthNormals)
            ConfigureInput(ScriptableRenderPassInput.Normal);
#else // v8
		this.renderer = renderer;
#endif
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (settings == null)
        {
            Debug.LogWarning("Bad settings for Dithering Pass!");
            return;
        }

        Dithering ditheringSettings = renderingData.cameraData.camera.GetComponent<Dithering>();

        if (ditheringSettings == null || !ditheringSettings.enabled)
            return;

        Pattern pattern = ditheringSettings.pattern;
        Palette palette = ditheringSettings.palette;

        if (palette == null)
            return;

        CommandBuffer cmd = CommandBufferPool.Get(profilerTag);
        RenderTextureDescriptor opaqueDesc = renderingData.cameraData.cameraTargetDescriptor;
        opaqueDesc.depthBufferBits = 0;

        // Set Source / Destination
#if UNITY_2020_2_OR_NEWER // v10+
        var renderer = renderingData.cameraData.renderer;
#else // v8
		// For older versions, cameraData.renderer is internal so can't be accessed. Will pass it through from AddRenderPasses instead
		var renderer = this.renderer;
#endif

        // note : Seems this has to be done in here rather than in AddRenderPasses to work correctly in 2021.2+
        if (settings.srcType == DitheringFeature.Target.CameraColor)
        {
            source = renderer.cameraColorTarget;
        }
        else if (settings.srcType == DitheringFeature.Target.TextureID)
        {
            source = new RenderTargetIdentifier(settings.srcTextureId);
        }
        else if (settings.srcType == DitheringFeature.Target.RenderTextureObject)
        {
            source = new RenderTargetIdentifier(settings.srcTextureObject);
        }

        if (settings.dstType == DitheringFeature.Target.CameraColor)
        {
            destination = renderer.cameraColorTarget;
        }
        else if (settings.dstType == DitheringFeature.Target.TextureID)
        {
            destination = new RenderTargetIdentifier(settings.dstTextureId);
        }
        else if (settings.dstType == DitheringFeature.Target.RenderTextureObject)
        {
            destination = new RenderTargetIdentifier(settings.dstTextureObject);
        }

        if (settings.setInverseViewMatrix)
        {
            Shader.SetGlobalMatrix(_InverseView, renderingData.cameraData.camera.cameraToWorldMatrix);
        }

        if (settings.dstType == DitheringFeature.Target.TextureID)
        {
            if (settings.overrideGraphicsFormat)
            {
                opaqueDesc.graphicsFormat = settings.graphicsFormat;
            }

            cmd.GetTemporaryRT(destinationTexture.id, opaqueDesc, filterMode);
        }
        
        float rndOffsetX = 0;
        float rndOffsetY = 0;

        if(ditheringSettings.grainAnimated)
        {
            rndOffsetX = Random.Range(0, 1f);
            rndOffsetY = Random.Range(0, 1f);
        }

        RenderTextureDescriptor lutDesc = new RenderTextureDescriptor(192, 192, RenderTextureFormat.ARGBHalf, 0);
        cmd.GetTemporaryRT(noiseLutTexture.id, lutDesc, FilterMode.Point);
        RenderTargetIdentifier lutDestinationTarget = new RenderTargetIdentifier(noiseLutTexture.id);
        
        // Write over noise lut texture
        Blit(cmd, 
            new RenderTargetIdentifier(BuiltinRenderTextureType.None), 
            noiseLutTexture.Identifier(), 
            noiseLutMaterial, 
            ditheringSettings.grainColored ? 1 : 0);
        
        cmd.SetGlobalTexture(_GrainTex, lutDestinationTarget);
        
        // Dithering Phase
        Material material = ditheringMaterial;

        if (ditheringSettings.patternTexture == null)
            ditheringSettings.patternTexture = Texture2D.whiteTexture;
        
        Texture2D patTex = (pattern == null ? (Texture2D)ditheringSettings.patternTexture : pattern.Texture);

        material.SetFloat(_PaletteColorCount, palette.MixedColorCount);
        material.SetFloat(_PaletteHeight, palette.Texture.height);
        material.SetTexture(_PaletteTex, palette.Texture);

        material.SetFloat(_PatternSize, patTex.width);
        material.SetTexture(_PatternTex, patTex);
        material.SetVector(_Grain_Params1, new Vector2(ditheringSettings.grainLuminanceContribution, ditheringSettings.grainIntensity * 20f));
        material.SetVector(_Grain_Params2, new Vector4((float)opaqueDesc.width / (float)lutDesc.width / ditheringSettings.grainSize, (float)opaqueDesc.height / (float)lutDesc.height / ditheringSettings.grainSize, rndOffsetX, rndOffsetY));

        // Write over temporary color texture with noise material
        cmd.GetTemporaryRT(temporaryColorTexture.id, opaqueDesc, filterMode);
        
        // Write over final destination with dithering material
        Blit(cmd, source, temporaryColorTexture.Identifier());
        Blit(cmd, temporaryColorTexture.Identifier(), destination, material);

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        if (settings.dstType == DitheringFeature.Target.TextureID)
        {
            cmd.ReleaseTemporaryRT(destinationTexture.id);
        }

        cmd.ReleaseTemporaryRT(noiseLutTexture.id);
        cmd.ReleaseTemporaryRT(temporaryColorTexture.id);
    }
}