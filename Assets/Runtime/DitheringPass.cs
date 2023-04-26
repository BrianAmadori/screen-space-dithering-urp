using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DitheringPass : ScriptableRenderPass
{
    internal static readonly int _Grain_Params1 = Shader.PropertyToID("_Grain_Params1");
    internal static readonly int _Grain_Params2 = Shader.PropertyToID("_Grain_Params2");
    internal static readonly int _GrainTex      = Shader.PropertyToID("_GrainTex");
    internal static readonly int _Phase         = Shader.PropertyToID("_Phase");

    public Material blitMaterial = null;
    private Shader ditheringShader = null;
    private Material ditheringMaterial = null;
    private Shader noiseShader = null;
    private Material noiseMaterial = null;
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
        blitMaterial = settings.blitMaterial;
        profilerTag = tag;
        temporaryColorTexture.Init("_TemporaryColorTexture");
        noiseLutTexture.Init("_NoiseLutTexture");

        ditheringShader = Shader.Find("Hidden/Dithering/Image Effect");
        ditheringMaterial = new Material(ditheringShader);
        ditheringMaterial.hideFlags = HideFlags.HideAndDontSave;

        noiseShader = Shader.Find("Hidden/Post FX/Uber Shader_Grain");
        noiseMaterial = new Material(noiseShader);
        noiseMaterial.hideFlags = HideFlags.HideAndDontSave;

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
            Shader.SetGlobalMatrix("_InverseView", renderingData.cameraData.camera.cameraToWorldMatrix);
        }

        if (settings.dstType == DitheringFeature.Target.TextureID)
        {
            if (settings.overrideGraphicsFormat)
            {
                opaqueDesc.graphicsFormat = settings.graphicsFormat;
            }

            cmd.GetTemporaryRT(destinationTexture.id, opaqueDesc, filterMode);
        }
        
        
        /*
         * 			RenderTexture transport = RenderTexture.GetTemporary(cam.pixelWidth,cam.pixelHeight);
			
			if(profile.grain.enabled==false){
				profile.grain.enabled=true;
			}
			
			var context = m_Context.Reset();
            context.profile = profile;
            context.renderTextureFactory = m_RenderTextureFactory;
            context.materialFactory = m_MaterialFactory;
            context.camera = cam;
#if UNITY_EDITOR
			var uberMaterial = m_MaterialFactory.Get("Hidden/Post FX/Uber Shader_Grain");
#else
			var uberMaterial = ub;
#endif
            uberMaterial.shaderKeywords = null;

			Texture autoExposure = GU.whiteTexture;
			uberMaterial.SetTexture("_AutoExposure", autoExposure);

			m_Grain.Init(context,profile.grain);

			TryPrepareUberImageEffect(m_Grain, uberMaterial);

			Graphics.Blit(source, transport, uberMaterial, 0);

         */
        
        // Noise Phase
        noiseMaterial.shaderKeywords = null;
        Texture autoExposure = Texture2D.whiteTexture;
        noiseMaterial.SetTexture("_AutoExposure", autoExposure);
        noiseMaterial.EnableKeyword("GRAIN");

        float rndOffsetX = 0;
        float rndOffsetY = 0;
        float time = 4;

        if(settings.grainAnimated)
        {
            time = Random.Range(0f,1f);
            rndOffsetX = Random.Range(0f,1f);
            rndOffsetY = Random.Range(0f,1f);
        }

         RenderTextureDescriptor lutDesc = new RenderTextureDescriptor(opaqueDesc.width, opaqueDesc.height, RenderTextureFormat.ARGBHalf, 0);
         cmd.GetTemporaryRT(noiseLutTexture.id, lutDesc, FilterMode.Point);
         RenderTargetIdentifier lutDestinationTarget = new RenderTargetIdentifier(noiseLutTexture.id);
        
        noiseLutMaterial.SetFloat(_Phase, time / 20f);

        // Write over noise lut texture
        Blit(cmd, 
            new RenderTargetIdentifier(BuiltinRenderTextureType.None), 
            noiseLutTexture.Identifier(), 
            noiseLutMaterial, 
            settings.grainColored ? 1 : 0);

        cmd.SetGlobalTexture(_GrainTex, lutDestinationTarget);
        cmd.SetGlobalVector(_Grain_Params1, new Vector2(settings.grainLuminanceContribution, settings.grainIntensity * 20f));
        cmd.SetGlobalVector(_Grain_Params2, new Vector4((float)opaqueDesc.width / (float)lutDesc.width / settings.grainSize, (float)opaqueDesc.height / (float)lutDesc.height / settings.grainSize, rndOffsetX, rndOffsetY));

        // Write over temporary color texture with noise material
        cmd.GetTemporaryRT(temporaryColorTexture.id, opaqueDesc, filterMode);
        Blit(cmd, source, temporaryColorTexture.Identifier(), noiseMaterial);
        //Blit(cmd, source, temporaryColorTexture.Identifier());

        // Dithering Phase
        Material blitMaterial = ditheringMaterial;
        
        Texture2D patTex = (settings.pattern == null ? settings.patternTexture : settings.pattern.Texture);

        blitMaterial.SetFloat("_PaletteColorCount", settings.palette.MixedColorCount);
        blitMaterial.SetFloat("_PaletteHeight", settings.palette.Texture.height);
        blitMaterial.SetTexture("_PaletteTex", settings.palette.Texture);
        blitMaterial.SetFloat("_PatternSize", patTex.width);
        blitMaterial.SetTexture("_PatternTex", patTex);

        // Write over final destination with dithering material
        Blit(cmd, temporaryColorTexture.Identifier(), destination, blitMaterial, settings.blitMaterialPassIndex);

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
    
    /*
     *
     *
     *   static class Uniforms
        {
            internal static readonly int _Grain_Params1 = Shader.PropertyToID("_Grain_Params1");
            internal static readonly int _Grain_Params2 = Shader.PropertyToID("_Grain_Params2");
            internal static readonly int _GrainTex      = Shader.PropertyToID("_GrainTex");
            internal static readonly int _Phase         = Shader.PropertyToID("_Phase");
        }
     *
     *  public void Prepare(Material uberMaterial, Material grainMaterial)
        {
            var settings = model.settings;

            uberMaterial.EnableKeyword("GRAIN");

            float rndOffsetX;
            float rndOffsetY;
            float time;

            if(!model.settings.animated){
                time = 4f;
                rndOffsetX = 0f;
                rndOffsetY = 0f;
            }else{
                time = Random.Range(0f,1f);
                rndOffsetX = Random.Range(0f,1f);
                rndOffsetY = Random.Range(0f,1f);
            }


            // Generate the grain lut for the current frame first
            if (m_GrainLookupRT == null || !m_GrainLookupRT.IsCreated())
            {
                GU.Destroy(m_GrainLookupRT);

                m_GrainLookupRT = new RenderTexture(192, 192, 0, RenderTextureFormat.ARGBHalf)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Repeat,
                    anisoLevel = 0,
                    name = "Grain Lookup Texture"
                };

                m_GrainLookupRT.Create();
            }

            //var grainMaterial = context.materialFactory.Get("Hidden/Post FX/Grain Generator");
            grainMaterial.SetFloat(Uniforms._Phase, time / 20f);

            Graphics.Blit((Texture)null, m_GrainLookupRT, grainMaterial, settings.colored ? 1 : 0);

            // Send everything to the uber shader
            uberMaterial.SetTexture(Uniforms._GrainTex, m_GrainLookupRT);
            uberMaterial.SetVector(Uniforms._Grain_Params1, new Vector2(settings.luminanceContribution, settings.intensity * 20f));
            uberMaterial.SetVector(Uniforms._Grain_Params2, new Vector4((float)context.width / (float)m_GrainLookupRT.width / settings.size, (float)context.height / (float)m_GrainLookupRT.height / settings.size, rndOffsetX, rndOffsetY));
        }

     *
     * 
     */
}