using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BlurRendererFeature : ScriptableRendererFeature
{
    BlurRenderPass blurRenderPass;

    public override void Create()
    {
        blurRenderPass = new BlurRenderPass();
        name = "Blur";
    }

    // 이 부분이 핵심입니다.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // 1. 현재 씬의 Volume 설정 스택을 가져옵니다.
        var stack = VolumeManager.instance.stack;
        // 2. 스택에서 우리가 만든 BlurSettings를 찾아옵니다.
        BlurSettings blurSettings = stack.GetComponent<BlurSettings>();

        // 3. Setup 메서드를 호출하여 현재 설정으로 패스를 초기화합니다.
        //    Setup이 true를 반환하면 (즉, 효과가 활성화되어 있으면)
        if (blurRenderPass.Setup(blurSettings))
        {
            // 4. 렌더러에 이 패스를 추가(Enqueue)합니다.
            renderer.EnqueuePass(blurRenderPass);
        }
    }
}