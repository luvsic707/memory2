using System.Collections;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 莫比乌斯环视角拉远与光影揭示控制器 (Stage 3)
    /// 1. 第一阶段 (Phase 1): 摄像机作为第一人称视角低空紧跟球体，在环面上随其扭转前行，视野局促，隐藏环带整体形状。
    ///    - 灯光为紧跟球体的窄光速聚光灯，四周一片黑暗，给玩家“在直行前进”的错觉。
    /// 2. 触发器 (Trigger): 当球体走完第一圈（2π，HasCompletedFirstLap 变为 true），开启连续镜头拉远。
    /// 3. 第二阶段 (Phase 2): 摄像机在单镜头长镜头 (One Continuous Shot) 下平滑过渡，
    ///    - 从紧贴球体的视角连续拉远、升起至高空第三人称全景视角，严禁任何黑屏、转场或瞬移。
    ///    - 聚光灯范围在同一个协程中同步拓宽，全局环境光 (Ambient Light) 渐渐亮起，将隐藏在黑暗中的无穷大（∞）环带渐渐揭露给玩家。
    /// 4. 第三阶段 (Phase 3): 摄像机稳定在高空，展示西西弗斯式的无尽循环。灯光变为冷色、平滑均匀的全局环境光，昭示荒谬的永恒。
    /// </summary>
    public class CameraRevealSequence : MonoBehaviour
    {
        public enum SequencePhase
        {
            FirstPersonRiding, // 阶段 1：第一人称骑行
            PullingOut,        // 阶段 2：长镜头拉远过渡
            ThirdPersonStatic  // 阶段 3：第三人称静止全景
        }

        [Header("核心引用")]
        [Tooltip("滚球控制器引用")]
        public BallOnMobius targetBall;

        [Tooltip("莫比乌斯环引用")]
        public MobiusStrip mobiusStrip;

        [Tooltip("主摄像机（若为空，自动绑定 Camera.main）")]
        public Camera mainCamera;

        [Tooltip("球体跟随光源（Spotlight 或 Point Light，若为空，自动在球体子节点寻找）")]
        public Light followLight;

        [Header("摄像机参数")]
        [Tooltip("拉镜头曲线，使用 EaseInOut 确保起步和落点平滑")]
        public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("拉镜头时长（秒）")]
        public float pullOutDuration = 4.0f;

        [Tooltip("第一人称起始视野 FoV")]
        public float fovStart = 65f;

        [Tooltip("第三人称终点视野 FoV")]
        public float fovEnd = 45f;

        [Tooltip("第一人称下摄像机相对于滚球局部的偏移 (x: 侧向, y: 法线向上, z: 切线向后)")]
        public Vector3 fpOffset = new Vector3(0f, 0.6f, -1.8f);

        [Tooltip("第一人称下摄像机的视线向前延伸距离")]
        public float fpLookAhead = 2.5f;

        [Tooltip("第三人称终点高空摄像机世界坐标")]
        public Vector3 tpPosition = new Vector3(0f, 16f, -22f);

        [Tooltip("第三人称终点高空摄像机旋转欧拉角")]
        public Vector3 tpRotation = new Vector3(35f, 0f, 0f);

        [Header("光影动画参数")]
        [Tooltip("阶段 1 窄光源跟随距离/范围")]
        public float fpLightRange = 8f;

        [Tooltip("阶段 1 窄光源跟随强度")]
        public float fpLightIntensity = 5f;

        [Tooltip("阶段 3 宽光源揭示范围")]
        public float tpLightRange = 45f;

        [Tooltip("阶段 3 宽光源跟随强度")]
        public float tpLightIntensity = 1.2f;

        [Tooltip("阶段 1 黑暗的环境光颜色")]
        public Color fpAmbientColor = Color.black;

        [Tooltip("阶段 3 全局均匀的冷色调环境光")]
        public Color tpAmbientColor = new Color(0.18f, 0.22f, 0.28f);

        // 运行状态
        private SequencePhase currentPhase = SequencePhase.FirstPersonRiding;
        private bool hasTriggeredTransition = false;

        private void Start()
        {
            // 自动配置引用
            if (targetBall == null) targetBall = FindObjectOfType<BallOnMobius>();
            if (mobiusStrip == null) mobiusStrip = FindObjectOfType<MobiusStrip>();
            if (mainCamera == null) mainCamera = Camera.main;

            if (targetBall != null && followLight == null)
            {
                followLight = targetBall.GetComponentInChildren<Light>();
            }

            // 初始化环境光模式为三色/单色，以便能被脚本平滑控制
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = fpAmbientColor;

            if (followLight != null)
            {
                followLight.range = fpLightRange;
                followLight.intensity = fpLightIntensity;
            }

            if (mainCamera != null)
            {
                mainCamera.fieldOfView = fovStart;
            }

            Debug.Log("[CameraReveal] 莫比乌斯相机序列已就绪，当前为阶段 1：第一人称环带跟踪骑行。");
        }

        private void Update()
        {
            if (targetBall == null || mobiusStrip == null || mainCamera == null) return;

            switch (currentPhase)
            {
                case SequencePhase.FirstPersonRiding:
                    // 1. 阶段 1：实时计算并让相机以第一人称贴合路径跟踪滚球
                    UpdateFirstPersonCamera();

                    // 检测球体是否越过第一圈 2π 关卡
                    if (targetBall.HasCompletedFirstLap && !hasTriggeredTransition)
                    {
                        hasTriggeredTransition = true;
                        StartCoroutine(TriggerRevealTransition());
                    }
                    break;

                case SequencePhase.PullingOut:
                    // 阶段 2：通过协程过渡，在此处不手动干预
                    break;

                case SequencePhase.ThirdPersonStatic:
                    // 3. 阶段 3：镜头已拉至高空，固定视角冷光环视滚球的无穷循环
                    mainCamera.transform.position = tpPosition;
                    mainCamera.transform.rotation = Quaternion.Euler(tpRotation);
                    mainCamera.fieldOfView = fovEnd;

                    if (followLight != null)
                    {
                        followLight.range = tpLightRange;
                        followLight.intensity = tpLightIntensity;
                    }
                    RenderSettings.ambientLight = tpAmbientColor;
                    break;
            }
        }

        /// <summary>
        /// 第一人称相机跟踪计算
        /// 根据莫比乌斯环的切线和法线方向，动态构建一个摄像机的跟随矩阵，使其产生过山车般的扭转贴地视角。
        /// </summary>
        private void UpdateFirstPersonCamera()
        {
            float t = targetBall.t;
            float uMod = t % (2f * Mathf.PI);
            float vRide = (Mathf.Floor(t / (2f * Mathf.PI)) % 2 == 0) ? targetBall.vOffset : -targetBall.vOffset;
            float R = mobiusStrip.radius;

            Vector3 ballCenter = targetBall.transform.position;

            // 数值计算局部正交基 (Tangent, Normal, Binormal)
            float eps = 0.01f;
            Vector3 dU = (MobiusStrip.GetMobiusPoint(uMod + eps, vRide, R) - MobiusStrip.GetMobiusPoint(uMod - eps, vRide, R)) / (2f * eps);
            Vector3 dV = (MobiusStrip.GetMobiusPoint(uMod, vRide + eps, R) - MobiusStrip.GetMobiusPoint(uMod, vRide - eps, R)) / (2f * eps);

            Vector3 tangent = mobiusStrip.transform.TransformDirection(dU).normalized;
            Vector3 binormal = mobiusStrip.transform.TransformDirection(dV).normalized;
            Vector3 normal = Vector3.Cross(tangent, binormal).normalized;
            if (targetBall.invertNormal) normal = -normal;

            // 相机位置 = 球心 + 沿法线上移 + 沿切线后移 + 沿侧向偏移
            Vector3 camPos = ballCenter + normal * fpOffset.y + tangent * fpOffset.z + binormal * fpOffset.x;
            
            // 相机视点 = 球体前方延伸点
            Vector3 lookTarget = ballCenter + tangent * fpLookAhead + normal * fpOffset.y;

            mainCamera.transform.position = camPos;
            mainCamera.transform.rotation = Quaternion.LookRotation(lookTarget - camPos, normal);
        }

        /// <summary>
        /// 协程：长镜头拉远过渡与灯光范围同步扩散动画
        /// </summary>
        private IEnumerator TriggerRevealTransition()
        {
            currentPhase = SequencePhase.PullingOut;
            Debug.Log("<color=cyan>[CameraReveal] 相机序列进入阶段 2：启动一镜到底拉镜头 & 环境光匀速扩展动画...</color>");

            float elapsed = 0f;

            while (elapsed < pullOutDuration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / pullOutDuration);
                
                // 使用 AnimationCurve 进行曲线缓动
                float tBlend = transitionCurve.Evaluate(normalizedTime);

                // 1. 实时计算当前帧下的第一人称虚拟位置（因为球一直在滚动）
                float tBall = targetBall.t;
                float uMod = tBall % (2f * Mathf.PI);
                float vRide = (Mathf.Floor(tBall / (2f * Mathf.PI)) % 2 == 0) ? targetBall.vOffset : -targetBall.vOffset;
                float R = mobiusStrip.radius;
                Vector3 ballCenter = targetBall.transform.position;

                float eps = 0.01f;
                Vector3 dU = (MobiusStrip.GetMobiusPoint(uMod + eps, vRide, R) - MobiusStrip.GetMobiusPoint(uMod - eps, vRide, R)) / (2f * eps);
                Vector3 dV = (MobiusStrip.GetMobiusPoint(uMod, vRide + eps, R) - MobiusStrip.GetMobiusPoint(uMod, vRide - eps, R)) / (2f * eps);
                Vector3 tangent = mobiusStrip.transform.TransformDirection(dU).normalized;
                Vector3 binormal = mobiusStrip.transform.TransformDirection(dV).normalized;
                Vector3 normal = Vector3.Cross(tangent, binormal).normalized;
                if (targetBall.invertNormal) normal = -normal;

                Vector3 currentFpPos = ballCenter + normal * fpOffset.y + tangent * fpOffset.z + binormal * fpOffset.x;
                Vector3 lookTarget = ballCenter + tangent * fpLookAhead + normal * fpOffset.y;
                Quaternion currentFpRot = Quaternion.LookRotation(lookTarget - currentFpPos, normal);

                // 2. 将相机在“动感第一人称”与“静态第三人称”之间平滑插值，实现一镜到底
                Vector3 lerpedPos = Vector3.Lerp(currentFpPos, tpPosition, tBlend);
                Quaternion lerpedRot = Quaternion.Slerp(currentFpRot, Quaternion.Euler(tpRotation), tBlend);
                float lerpedFov = Mathf.Lerp(fovStart, fovEnd, tBlend);

                mainCamera.transform.position = lerpedPos;
                mainCamera.transform.rotation = lerpedRot;
                mainCamera.fieldOfView = lerpedFov;

                // 3. 同步进行光影缓动扩散，黑暗的四周渐渐被照亮，展现出莫比乌斯环的无限轮廓
                if (followLight != null)
                {
                    followLight.range = Mathf.Lerp(fpLightRange, tpLightRange, tBlend);
                    followLight.intensity = Mathf.Lerp(fpLightIntensity, tpLightIntensity, tBlend);
                }
                RenderSettings.ambientLight = Color.Lerp(fpAmbientColor, tpAmbientColor, tBlend);

                yield return null;
            }

            // 过渡结束，正式切入第三阶段
            currentPhase = SequencePhase.ThirdPersonStatic;
            Debug.Log("<color=green>[CameraReveal] 相机序列进入阶段 3：终点全景固定视角。球体已进入荒谬循环。</color>");

            // ==========================================
            // TODO: 转场退出至 Stage4 (Alienation/Office) 的时机
            // 可在此处监听玩家离开按键或自动触发
            // E.g., SceneTransitionManager.Instance.LoadNextScene();
            // ==========================================
        }
    }
}
