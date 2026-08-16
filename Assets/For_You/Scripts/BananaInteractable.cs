using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 香蕉交互与堆叠生成组件 (包含多层级绝对显性 3D 咬痕缺口系统)
    /// 1. 每次按下 Q 键交互，香蕉模型截短 18% 并生成 3D 果肉缺口 (Bite Mark Notch) + 果肉碎屑喷溅。
    /// 2. 连续咬满 maxBites 次 (如 4~5 次) 后，香蕉被完全吃完销毁。
    /// 3. 每次按 Q 交互百分百保证生成 1~2 只新香蕉堆叠下落，计数器 counter+1。
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
        [Tooltip("一只香蕉最多可以被咬的次数 (默认 4 次，每次交互缺一个口)")]
        [Range(1, 10)] public int maxBites = 4;

        [Tooltip("当前已被咬的次数")]
        public int currentBites = 0;

        [Tooltip("咬痕缺口产生的物理半径 (米)")]
        public float biteRadius = 0.18f;

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

            // 2. 驱动 3D 模型产生显性咬痕缺口形变 + 果肉碎屑粒子
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
        /// 每次按 Q 交互时，为 3D 香蕉模型切出/凹陷一个明显的咬痕缺口 (Bite Mark Notch)
        /// 融合三大层级视觉：3D 咬痕缺口 + 阶梯切削形变 + 咬痕碎屑粒子喷溅 (100% 绝对显性可见)
        /// </summary>
        public void ApplyBiteMarkDeformation()
        {
            currentBites++;

            // 1. 阶梯切削形变：每次被咬，模型 Y 轴长度缩短 18%，直观呈现被咬掉一段
            Vector3 currentLocalScale = transform.localScale;
            transform.localScale = new Vector3(currentLocalScale.x, currentLocalScale.y * 0.82f, currentLocalScale.z);

            // 2. 计算咬痕在世界坐标中的位置
            MeshFilter mf = GetComponentInChildren<MeshFilter>();
            Vector3 worldBitePos = transform.position + transform.up * (0.2f - currentBites * 0.08f);

            if (mf != null && mf.sharedMesh != null)
            {
                Bounds bounds = mf.sharedMesh.bounds;
                worldBitePos = mf.transform.TransformPoint(new Vector3(
                    (currentBites % 2 == 1) ? bounds.extents.x * 0.5f : -bounds.extents.x * 0.5f,
                    Mathf.Lerp(bounds.max.y * 0.6f, bounds.min.y * 0.6f, (float)currentBites / (maxBites + 1)),
                    bounds.center.z
                ));

                // 尝试 CPU 顶点凹陷形变
                TryCarveMeshVertices(mf, currentBites, bounds);
            }

            // 3. 实例化显性 3D 咬痕缺口标记 (3D Bite Crater Notch)
            CreateVisibleBiteNotchObject(worldBitePos);

            // 4. 喷溅香蕉果肉碎屑粒子
            SpawnBiteCrumbs(worldBitePos);
        }

        /// <summary>
        /// 尝试 CPU 顶点雕刻凹陷缺口
        /// </summary>
        private void TryCarveMeshVertices(MeshFilter mf, int biteIndex, Bounds bounds)
        {
            try
            {
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
                float biteRatio = (float)biteIndex / (maxBites + 1);

                Vector3 ext = bounds.extents;
                Vector3 biteCenter = bounds.center;

                if (ext.y >= ext.x && ext.y >= ext.z)
                {
                    biteCenter.y = Mathf.Lerp(bounds.max.y * 0.7f, bounds.min.y * 0.7f, biteRatio);
                    biteCenter.x += (biteIndex % 2 == 1 ? ext.x * 0.5f : -ext.x * 0.5f);
                }
                else if (ext.z >= ext.x && ext.z >= ext.y)
                {
                    biteCenter.z = Mathf.Lerp(bounds.max.z * 0.7f, bounds.min.z * 0.7f, biteRatio);
                    biteCenter.x += (biteIndex % 2 == 1 ? ext.x * 0.5f : -ext.x * 0.5f);
                }
                else
                {
                    biteCenter.x = Mathf.Lerp(bounds.max.x * 0.7f, bounds.min.x * 0.7f, biteRatio);
                    biteCenter.y += (biteIndex % 2 == 1 ? ext.y * 0.5f : -ext.y * 0.5f);
                }

                float radius = Mathf.Max(ext.x, Mathf.Max(ext.y, ext.z)) * 0.65f;
                bool modified = false;

                for (int i = 0; i < verts.Length; i++)
                {
                    float dist = Vector3.Distance(verts[i], biteCenter);
                    if (dist < radius)
                    {
                        float factor = Mathf.Pow(1f - (dist / radius), 1.5f);
                        verts[i] = Vector3.Lerp(verts[i], biteCenter, factor * 0.75f);
                        modified = true;
                    }
                }

                if (modified)
                {
                    _bittenMeshCopy.vertices = verts;
                    _bittenMeshCopy.RecalculateBounds();
                    _bittenMeshCopy.RecalculateNormals();
                    mf.mesh = _bittenMeshCopy;

                    MeshCollider mc = GetComponentInChildren<MeshCollider>();
                    if (mc != null) mc.sharedMesh = _bittenMeshCopy;
                }
            }
            catch
            {
                // 静默防护
            }
        }

        /// <summary>
        /// 实例化显性 3D 咬痕缺口标记 (3D Bite Crater Notch)
        /// </summary>
        private void CreateVisibleBiteNotchObject(Vector3 worldPos)
        {
            GameObject notch = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            notch.name = $"Bite_Notch_{currentBites}";
            notch.transform.SetParent(transform, true);
            notch.transform.position = worldPos;
            notch.transform.localScale = Vector3.one * (0.16f * transform.lossyScale.x);

            Renderer r = notch.GetComponent<Renderer>();
            if (r != null)
            {
                Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlit == null) unlit = Shader.Find("Unlit/Color");
                if (unlit == null) unlit = Shader.Find("Sprites/Default");
                Material mat = new Material(unlit);
                mat.SetColor("_BaseColor", new Color(0.45f, 0.35f, 0.15f, 0.95f));
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(0.45f, 0.35f, 0.15f, 0.95f));
                r.sharedMaterial = mat;
            }

            Destroy(notch.GetComponent<Collider>());
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
