using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BlurRenderPass : ScriptableRenderPass
{
    [SerializeField] private Material material;
    private BlurSettings blurSettings;

    private RenderTargetHandle source;
    private RenderTargetHandle blurTex;

    // 생성자에서는 기본적인 설정만 합니다.
    public BlurRenderPass()
    {
        // 셰이더를 미리 로드해서 Material을 생성해둡니다.
        material = new Material(Shader.Find("PostProcessing/Blur"));
        // 렌더링 순서를 정합니다.
        renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    // 이 Setup 메서드가 매 프레임 호출되어 현재 상태를 전달받습니다. (중요!)
    public bool Setup(BlurSettings settings)
    {
        this.blurSettings = settings;
        // 설정이 유효하고, 활성화 상태이며, 재질이 있을 때만 true를 반환합니다.
        return blurSettings != null && blurSettings.IsActive() && material != null;
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        var renderer = renderingData.cameraData.renderer;
        source = new RenderTargetHandle(renderer.cameraColorTarget);
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        // 여기서 IsActive 체크를 할 필요가 없습니다. 
        // Feature에서 이미 체크하고 이 패스를 등록했기 때문입니다.
        blurTex = new RenderTargetHandle();
        blurTex.Init("_BlurTex"); // PropertyToID 대신 Init을 사용하는 것이 더 최신 방식입니다.
        cmd.GetTemporaryRT(blurTex.id, cameraTextureDescriptor);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get("Blur Post Process");

        int gridSize = Mathf.CeilToInt(blurSettings.strength.value * 3.0f);
        if (gridSize % 2 == 0)
        {
            gridSize++;
        }

        material.SetInteger("_GridSize", gridSize);
        material.SetFloat("_Spread", blurSettings.strength.value);

        cmd.Blit(source.Identifier(), blurTex.Identifier(), material, 0);
        cmd.Blit(blurTex.Identifier(), source.Identifier(), material, 1);

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);
    }

    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(blurTex.id);
    }
}