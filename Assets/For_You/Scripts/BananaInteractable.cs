using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 香蕉交互与堆叠生成组件 (纯网格凹陷咬痕缺口系统)
    /// 1. 彻底删除任何外凸球体与实体大包，纯粹通过 3D 网格顶点向内深缩雕刻出“半圆凹陷缺口 (Concave Bite Notch)”。
    /// 2. 凹陷深处自动混合奶油白果肉着色与锯齿牙印。
    /// 3. 连续咬满 maxBites 次 (如 4~5 次) 后，香蕉被彻底吃完销毁。
    /// 4. 每次按 Q 交互百分百保证生成 1~2 只新香蕉堆叠下落，计数器 counter+1。
    /// </summary>
    public class BananaInteractable : MonoBehaviour, IInteractable
    {
        [Header("香蕉生成配置")]
        [Tooltip("生成的香蕉预制体（必须包含 Rigidbody 和 Collider 以便物理堆叠）")]
        public GameObject bananaPrefab;

        [Tooltip("生成新香蕉的范围半径（水平偏移量）")]
        public float spawnRadius = 0.4f;

        [Tooltip("生成时向上偏移的高度，促使香蕉自然坠落堆叠")]
        public float spawnHeightOffset = 1.2f;

        [Tooltip("新生成的香蕉是否也是可交互的？（开启后可以疯狂套娃点击）")]
        public bool spawnAsInteractive = true;

        [Header("咬痕与多阶段吃香蕉配置 (Progressive Bite System)")]
        [Tooltip("一只香蕉最多可以被咬的次数 (默认 4 次，每次交互凹陷缺一个口)")]
        [Range(1, 10)] public int maxBites = 4;

        [Tooltip("当前已被咬的次数")]
        public int currentBites = 0;

        [Header("Stage 1 专属特殊香蕉配置")]
        [Tooltip("该香蕉是否为用于场景切换的特殊红光香蕉")]
        public bool isGlowingBanana = false;

        [Header("音效配置 (Inspector 拖拽)")]
        [Tooltip("吃香蕉/咀嚼音效文件 (拖入你的 .mp3/.wav/.ogg 文件)")]
        public AudioClip eatSoundClip;

        [Tooltip("吃香蕉音效播放音量")]
        [Range(0f, 1f)] public float eatSoundVolume = 0.85f;

        [Header("UI 提示")]
        [SerializeField] private string interactHint = "吃香蕉";

        // 实现 IInteractable 接口的属性
        public string InteractHint => interactHint;

        private Mesh _bittenMeshCopy;

        private void Awake()
        {
            AutoFitCollider();
            if (GetComponent<BananaJuice>() == null)
                gameObject.AddComponent<BananaJuice>();
        }

        private void AutoFitCollider()
        {
            if (GetComponentInChildren<Collider>(true) != null) return;

            BoxCollider box = gameObject.AddComponent<BoxCollider>();

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                box.size = Vector3.one;
                box.center = Vector3.zero;
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
            Vector3 localSize = transform.InverseTransformVector(bounds.size);

            localSize.x = Mathf.Abs(localSize.x);
            localSize.y = Mathf.Abs(localSize.y);
            localSize.z = Mathf.Abs(localSize.z);

            box.center = localCenter;
            box.size = localSize;
            Debug.Log($"[Banana] 自动适配了 BoxCollider！本地中心: {box.center}，本地大小: {box.size}");
        }

        // 实现 IInteractable 接口的方法
        public void Interact()
        {
            BananaJuice juice = GetComponent<BananaJuice>();
            if (juice == null) juice = gameObject.AddComponent<BananaJuice>();

            juice.PlayJuice(() =>
            {
                StartCoroutine(BananaJuice.ShakeMainCamera(juice.shakeIntensity, juice.shakeDuration));

                if (FirstPersonArmController.Instance == null)
                {
                    Camera mainCam = Camera.main;
                    if (mainCam != null)
                    {
                        mainCam.gameObject.AddComponent<FirstPersonArmController>();
                    }
                }

                if (FirstPersonArmController.Instance != null)
                {
                    FirstPersonArmController.Instance.PlayGrabAndEatMotion(transform.position, () =>
                    {
                        ExecuteBananaEatLogic();
                    });
                }
                else
                {
                    ExecuteBananaEatLogic();
                }
            });
        }

        private void ExecuteBananaEatLogic()
        {
            // 1. 播放咀嚼音效
            PlayEatSoundEffect();

            // 2. 驱动 3D 模型产生纯网格向内凹陷咬痕缺口 + 果肉碎屑粒子 (彻底无凸起大包)
            ApplyBiteMarkDeformation();

            // 特殊香蕉：直接吃掉通关
            if (isGlowingBanana)
            {
                Debug.Log("[Banana] 玩家吃下了特殊的通关香蕉！");
                if (Stage1Controller.Instance != null)
                {
                    Stage1Controller.Instance.EatGlowingBanana();
                }
                else
                {
                    EventBus.RaiseAnnouncement("You ate the glowing banana. Loading next stage...");
                    EventBus.RaiseSceneComplete();
                }
                return;
            }

            // 3. 按键 Q 交互一次，counter 计数增加 +1
            if (PlayerBehaviorData.Instance != null)
            {
                PlayerBehaviorData.Instance.AddBanana();
            }
            else
            {
                Debug.LogWarning("[Banana] 找不到 PlayerBehaviorData 持久化实例！无法进行交互计数。");
            }

            // 4. 按键 Q 交互一次，百分百生成更多香蕉堆叠下落
            if (Stage1Controller.Instance != null)
            {
                Stage1Controller.Instance.OnBananaEaten(this);
            }
            else
            {
                SpawnNewBanana();
            }

            // 5. 若已被连续咬满 maxBites 次 (如 4 或 5 次)，该香蕉被彻底吃完销毁
            if (currentBites >= maxBites)
            {
                Debug.Log($"[Banana] '{gameObject.name}' 已被连续咬满 {maxBites} 口，完全吃完！");
                Destroy(gameObject, 0.05f);
            }
        }

        /// <summary>
        /// 凹陷咬痕形变：纯网格向内深削凹陷缺口 (彻底杜绝任何外凸球体与大包)
        /// </summary>
        public void ApplyBiteMarkDeformation()
        {
            currentBites++;

            MeshFilter mf = GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

#if UNITY_EDITOR
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(mf.sharedMesh);
            if (!string.IsNullOrEmpty(assetPath))
            {
                UnityEditor.ModelImporter mi = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.ModelImporter;
                if (mi != null && !mi.isReadable)
                {
                    mi.isReadable = true;
                    mi.SaveAndReimport();
                }
            }
#endif

            if (!mf.sharedMesh.isReadable) return;

            if (_bittenMeshCopy == null)
            {
                _bittenMeshCopy = Instantiate(mf.sharedMesh);
                _bittenMeshCopy.name = $"{mf.sharedMesh.name}_Bitten";
            }

            Vector3[] verts = _bittenMeshCopy.vertices;
            Color[] colors = _bittenMeshCopy.colors;
            if (colors == null || colors.Length != verts.Length)
            {
                colors = new Color[verts.Length];
                for (int c = 0; c < colors.Length; c++) colors[c] = Color.white;
            }

            Bounds bounds = _bittenMeshCopy.bounds;
            Vector3 ext = bounds.extents;

            // 1. 自动寻找香蕉的主轴线 (Y、Z 或 X 轴中较长的一条)
            float biteT = (float)currentBites / (maxBites + 1);
            Vector3 biteSurfacePos = bounds.center;
            Vector3 centerSpine = bounds.center;

            float maxAxisLen = Mathf.Max(ext.x, Mathf.Max(ext.y, ext.z));
            float biteRadius = maxAxisLen * 0.35f;

            if (ext.y >= ext.x && ext.y >= ext.z) // Y 轴为主轴
            {
                float targetY = Mathf.Lerp(bounds.max.y * 0.75f, bounds.min.y * 0.75f, biteT);
                centerSpine = new Vector3(bounds.center.x, targetY, bounds.center.z);
                float sideX = (currentBites % 2 == 1) ? ext.x * 0.7f : -ext.x * 0.7f;
                biteSurfacePos = new Vector3(bounds.center.x + sideX, targetY, bounds.center.z);
            }
            else if (ext.z >= ext.x && ext.z >= ext.y) // Z 轴为主轴
            {
                float targetZ = Mathf.Lerp(bounds.max.z * 0.75f, bounds.min.z * 0.75f, biteT);
                centerSpine = new Vector3(bounds.center.x, bounds.center.y, targetZ);
                float sideX = (currentBites % 2 == 1) ? ext.x * 0.7f : -ext.x * 0.7f;
                biteSurfacePos = new Vector3(bounds.center.x + sideX, bounds.center.y, targetZ);
            }
            else // X 轴为主轴
            {
                float targetX = Mathf.Lerp(bounds.max.x * 0.75f, bounds.min.x * 0.75f, biteT);
                centerSpine = new Vector3(targetX, bounds.center.y, bounds.center.z);
                float sideY = (currentBites % 2 == 1) ? ext.y * 0.7f : -ext.y * 0.7f;
                biteSurfacePos = new Vector3(targetX, bounds.center.y + sideY, bounds.center.z);
            }

            // 2. 雕刻纯 3D 网格向内凹陷缺口 (Concave Crescent Bite Notch)
            bool modified = false;
            Color whiteFleshColor = new Color(0.98f, 0.95f, 0.82f, 1f);

            for (int i = 0; i < verts.Length; i++)
            {
                float dist = Vector3.Distance(verts[i], biteSurfacePos);
                if (dist < biteRadius)
                {
                    // 衰减系数：越靠近咬痕中心，向香蕉内部中轴拉缩得越深
                    float falloff = Mathf.Pow(1f - (dist / biteRadius), 1.4f);

                    // 深度向香蕉内侧中轴坍塌，形成凹陷缺口
                    Vector3 innerCarved = Vector3.Lerp(verts[i], centerSpine, falloff * 0.88f);

                    // 锯齿微牙印细节
                    float toothNoise = (Mathf.Sin(verts[i].x * 45f) + Mathf.Cos(verts[i].z * 45f)) * 0.006f * falloff;
                    verts[i] = innerCarved + (verts[i] - centerSpine).normalized * toothNoise;

                    // 将凹陷缺口深处的顶点着色为奶油白果肉颜色
                    colors[i] = Color.Lerp(colors[i], whiteFleshColor, falloff);
                    modified = true;
                }
            }

            if (modified)
            {
                _bittenMeshCopy.vertices = verts;
                _bittenMeshCopy.colors = colors;
                _bittenMeshCopy.RecalculateBounds();
                _bittenMeshCopy.RecalculateNormals();
                mf.mesh = _bittenMeshCopy;

                MeshCollider mc = GetComponentInChildren<MeshCollider>();
                if (mc != null) mc.sharedMesh = _bittenMeshCopy;
            }

            // 3. 喷溅香蕉果肉碎屑粒子
            Vector3 worldBitePos = mf.transform.TransformPoint(biteSurfacePos);
            SpawnBiteCrumbs(worldBitePos);
        }

        /// <summary>
        /// 喷溅香蕉果肉碎屑粒子 (Bite Particle Burst)
        /// </summary>
        private void SpawnBiteCrumbs(Vector3 worldPos)
        {
            GameObject crumbGo = new GameObject("Banana_Bite_Crumbs");
            crumbGo.transform.position = worldPos;

            ParticleSystem ps = crumbGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = true;
            main.duration = 0.3f;
            main.startLifetime = 0.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startColor = new Color(1f, 0.95f, 0.5f, 0.9f);

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 16) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            var renderer = crumbGo.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            Destroy(crumbGo, 0.7f);
        }

        public void SpawnNewBanana()
        {
            if (bananaPrefab == null && Stage1Controller.Instance != null)
            {
                bananaPrefab = Stage1Controller.Instance.bananaPrefab;
            }
            if (bananaPrefab == null)
            {
                bananaPrefab = gameObject;
            }

            int countToSpawn = (Stage1Controller.Instance != null) ? 1 : 2;
            for (int k = 0; k < countToSpawn; k++)
            {
                Vector3 randomOffset = new Vector3(
                    Random.Range(-spawnRadius, spawnRadius),
                    spawnHeightOffset + k * 0.3f,
                    Random.Range(-spawnRadius, spawnRadius)
                );
                Vector3 spawnPosition = transform.position + randomOffset;
                Quaternion spawnRotation = Random.rotation;

                GameObject newBanana = Instantiate(bananaPrefab, spawnPosition, spawnRotation);
                newBanana.name = "SpawningBanana_Prop";

                BananaInteractable newInteract = newBanana.GetComponent<BananaInteractable>();
                if (newInteract != null)
                {
                    newInteract.currentBites = 0;
                    newInteract._bittenMeshCopy = null;
                    newInteract.spawnAsInteractive = true;
                }

                float distFactor = Stage1Controller.Instance != null ? Stage1Controller.Instance.GetCurrentDistortionFactor() : 0f;
                newBanana.transform.localScale = transform.lossyScale * (1f + distFactor * 0.2f);

                if (Stage1Controller.Instance != null)
                {
                    BananaDistorter distorter = newBanana.GetComponent<BananaDistorter>();
                    if (distorter == null)
                    {
                        distorter = newBanana.AddComponent<BananaDistorter>();
                    }
                    distorter.distortionFactor = Stage1Controller.Instance.GetCurrentDistortionFactor();
                }

                if (!spawnAsInteractive)
                {
                    var interactComponent = newBanana.GetComponent<BananaInteractable>();
                    if (interactComponent != null)
                    {
                        Destroy(interactComponent);
                    }
                }

                Debug.Log($"[Banana] 成功在 {spawnPosition} 处生成了一只新物理香蕉，重力下落堆叠。");
            }
        }

        private void PlayEatSoundEffect()
        {
            if (eatSoundClip != null)
            {
                AudioSource.PlayClipAtPoint(eatSoundClip, transform.position, eatSoundVolume);
            }
            else
            {
                Camera mainCam = Camera.main;
                Vector3 pos = mainCam != null ? mainCam.transform.position : transform.position;
                
                if (FirstPersonArmController.Instance != null && FirstPersonArmController.Instance.defaultEatSound != null)
                {
                    AudioSource.PlayClipAtPoint(FirstPersonArmController.Instance.defaultEatSound, pos, eatSoundVolume);
                }
            }
        }
    }
}
