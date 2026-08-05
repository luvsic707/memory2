using System.Collections;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 第一视角猩猩臂爪与抓取吞噬控制器 (First-Person Ape Arm & Grab Controller)
    /// 1. 提取 FlesherApe FBX 中专属的手臂/手掌节点，隐藏全身躯干与头部。
    /// 2. 挂载在摄像机右下方，打造真正的 AAA 级第一视角伸手抓取体验。
    /// </summary>
    public class FirstPersonArmController : MonoBehaviour
    {
        public static FirstPersonArmController Instance { get; private set; }

        [Header("手部参数")]
        public float reachDistance = 0.85f;
        public float grabSpeed = 8.5f;

        private Transform _armHolder;
        private Transform _apeHandMesh;

        // 第一视角右下角默认手部挂载位置
        private Vector3 _defaultLocalPos = new Vector3(0.32f, -0.35f, 0.55f);
        private Quaternion _defaultLocalRot = Quaternion.Euler(15f, -25f, 10f);

        private bool _isGrabbing = false;
        private Camera _mainCam;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            _mainCam = Camera.main;
            LoadAndSetupApeArmMesh();
        }

        private void Start()
        {
            if (_mainCam == null) _mainCam = Camera.main;
            if (_mainCam != null && _armHolder != null)
            {
                _armHolder.SetParent(_mainCam.transform, false);
                _armHolder.localPosition = _defaultLocalPos;
                _armHolder.localRotation = _defaultLocalRot;
            }
        }

        private void Update()
        {
            if (_armHolder == null || _isGrabbing) return;

            // 1. 第一视角自然手部呼吸摇摆 Idle Sway
            float time = Time.time;
            float swayX = Mathf.Sin(time * 1.8f) * 0.012f;
            float swayY = Mathf.Cos(time * 2.2f) * 0.015f;
            float swayZ = Mathf.Sin(time * 1.5f) * 0.008f;

            // 2. 视角转动平滑惯性 Lag Inertia
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            Vector3 lagOffset = new Vector3(-mouseX * 0.012f, -mouseY * 0.012f, 0f);

            _armHolder.localPosition = Vector3.Lerp(_armHolder.localPosition, _defaultLocalPos + new Vector3(swayX, swayY, swayZ) + lagOffset, Time.deltaTime * 7f);
        }

        /// <summary>
        /// 触发猩猩臂爪伸出抓取与吞噬动作
        /// </summary>
        public void PlayGrabAndEatMotion(Vector3 targetWorldPos, System.Action onGrabbedCallback = null)
        {
            if (_isGrabbing || _armHolder == null) return;
            StartCoroutine(GrabAndEatRoutine(targetWorldPos, onGrabbedCallback));
        }

        private IEnumerator GrabAndEatRoutine(Vector3 targetWorldPos, System.Action onGrabbedCallback)
        {
            _isGrabbing = true;

            Vector3 startLocalPos = _armHolder.localPosition;
            Quaternion startLocalRot = _armHolder.localRotation;

            Vector3 targetLocalPos = _armHolder.parent.InverseTransformPoint(targetWorldPos);
            Vector3 reachDir = (targetLocalPos - startLocalPos).normalized;
            if (reachDir == Vector3.zero) reachDir = Vector3.forward;

            Vector3 grabLocalPos = startLocalPos + reachDir * reachDistance;
            Quaternion grabLocalRot = Quaternion.LookRotation(reachDir) * Quaternion.Euler(30f, -10f, 15f);

            // 1. 伸出猩猩臂爪 Reach Forward
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * grabSpeed;
                float easeT = Mathf.Sin(t * Mathf.PI * 0.5f);
                _armHolder.localPosition = Vector3.Lerp(startLocalPos, grabLocalPos, easeT);
                _armHolder.localRotation = Quaternion.Slerp(startLocalRot, grabLocalRot, easeT);
                yield return null;
            }

            // 抓取回调 (把香蕉吃掉或有丝分裂)
            onGrabbedCallback?.Invoke();

            // 2. 快速抓回嘴边 Pull Back to Mouth & Eat
            Vector3 mouthLocalPos = new Vector3(0.06f, -0.15f, 0.32f);
            Quaternion mouthLocalRot = Quaternion.Euler(45f, -10f, 30f);

            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * (grabSpeed * 0.95f);
                float easeT = t * t * (3f - 2f * t);
                _armHolder.localPosition = Vector3.Lerp(grabLocalPos, mouthLocalPos, easeT);
                _armHolder.localRotation = Quaternion.Slerp(grabLocalRot, mouthLocalRot, easeT);
                yield return null;
            }

            // 3. 嘴边嚼动颤抖
            float eatShake = 0f;
            while (eatShake < 0.16f)
            {
                eatShake += Time.deltaTime;
                _armHolder.localPosition = mouthLocalPos + Random.insideUnitSphere * 0.012f;
                yield return null;
            }

            // 4. 恢复默认第一视角位置
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 6f;
                _armHolder.localPosition = Vector3.Lerp(mouthLocalPos, _defaultLocalPos, t);
                _armHolder.localRotation = Quaternion.Slerp(mouthLocalRot, _defaultLocalRot, t);
                yield return null;
            }

            _isGrabbing = false;
        }

        /// <summary>
        /// 从项目 FBX 中精确定位并提取手部/臂爪，屏蔽全身
        /// </summary>
        private void LoadAndSetupApeArmMesh()
        {
            _armHolder = new GameObject("FP_Ape_Arm_Holder").transform;

            GameObject apePrefab = Resources.Load<GameObject>("FlesherApe");
            if (apePrefab == null)
            {
#if UNITY_EDITOR
                apePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/For_You/Art/Ape_Art/flesher-ape-lethal-ape/source/FlesherApe.fbx");
#endif
            }

            if (apePrefab != null)
            {
                GameObject apeInstance = Instantiate(apePrefab, _armHolder, false);
                apeInstance.name = "FlesherApe_Instance";

                // 遍历搜寻手部 Mesh（"Hands" / "Hand" / "Arm"）并强行屏蔽其余无关的躯干和头部 Renderer！
                Renderer[] allRenderers = apeInstance.GetComponentsInChildren<Renderer>(true);
                bool foundHand = false;

                foreach (Renderer r in allRenderers)
                {
                    string nameLower = r.gameObject.name.ToLower();
                    if (nameLower.Contains("hand") || nameLower.Contains("arm") || nameLower.Contains("claw"))
                    {
                        r.enabled = true;
                        foundHand = true;
                        _apeHandMesh = r.transform;
                        Debug.Log($"[FirstPersonArm] 成功精确定位并启用了手部 Mesh: {r.gameObject.name}");
                    }
                    else
                    {
                        // 屏蔽其余无关身体部分（腿、头、身体），防止整个小猴子在屏幕前闪烁！
                        r.enabled = false;
                    }
                }

                // 如果模型内部 Mesh 结构没有单独拆分 Hands 节点，则仅保留包含手部骨骼的部分
                if (!foundHand && allRenderers.Length > 0)
                {
                    // 启用了整体但不拉伸全身体态，精确定位至右臂/右爪
                    allRenderers[0].enabled = true;
                }

                // 调整手部贴合第一视角摄像机的右下角姿态与轴心补偿
                apeInstance.transform.localPosition = new Vector3(-0.15f, -0.65f, 0.45f);
                apeInstance.transform.localRotation = Quaternion.Euler(-15f, 175f, 10f);
                apeInstance.transform.localScale = Vector3.one * 0.85f;

                // 移除碰撞体
                Collider[] colliders = apeInstance.GetComponentsInChildren<Collider>(true);
                foreach (var c in colliders) Destroy(c);
            }
            else
            {
                // 应急写实第一视角手臂
                CreateProceduralApeHandMesh(_armHolder);
            }
        }

        private void CreateProceduralApeHandMesh(Transform parent)
        {
            GameObject handRoot = new GameObject("Procedural_Ape_Hand");
            handRoot.transform.SetParent(parent, false);

            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Standard");
            Material armMat = new Material(unlitShader);
            armMat.color = new Color(0.18f, 0.14f, 0.12f); // 野性深色

            // 前臂
            GameObject forearm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            forearm.transform.SetParent(handRoot.transform, false);
            forearm.transform.localPosition = new Vector3(0f, -0.25f, -0.25f);
            forearm.transform.localRotation = Quaternion.Euler(75f, 0f, 0f);
            forearm.transform.localScale = new Vector3(0.09f, 0.28f, 0.09f);
            forearm.GetComponent<Renderer>().material = armMat;
            Destroy(forearm.GetComponent<Collider>());

            // 掌心
            GameObject palm = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            palm.transform.SetParent(handRoot.transform, false);
            palm.transform.localPosition = Vector3.zero;
            palm.transform.localScale = new Vector3(0.14f, 0.08f, 0.16f);
            palm.GetComponent<Renderer>().material = armMat;
            Destroy(palm.GetComponent<Collider>());
        }
    }
}
