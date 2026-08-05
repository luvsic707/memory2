using System.Collections;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 第一视角猩猩臂爪与抓取吞噬控制器 (First-Person Ape Arm & Grab Controller)
    /// 1. 自动从项目中加载 FlesherApe.fbx 里面的真猩猩手部/臂爪模型。
    /// 2. 挂载在摄像机右下方，消除灰方块与穿模。
    /// 3. 实现抓取 (Reach Out) ➔ 抓紧 (Clench Grab) ➔ 拿向嘴边 (Pull Back & Eat) 动态手臂动作。
    /// </summary>
    public class FirstPersonArmController : MonoBehaviour
    {
        public static FirstPersonArmController Instance { get; private set; }

        [Header("手部参数")]
        public float reachDistance = 0.95f;
        public float grabSpeed = 9f;

        private Transform _armHolder;
        private Vector3 _defaultLocalPos = new Vector3(0.38f, -0.36f, 0.65f);
        private Quaternion _defaultLocalRot = Quaternion.Euler(20f, -30f, 15f);

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
            float swayX = Mathf.Sin(time * 1.8f) * 0.015f;
            float swayY = Mathf.Cos(time * 2.2f) * 0.02f;
            float swayZ = Mathf.Sin(time * 1.5f) * 0.01f;

            // 2. 视角转动平滑惯性 Lag Inertia
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            Vector3 lagOffset = new Vector3(-mouseX * 0.015f, -mouseY * 0.015f, 0f);

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
            Quaternion grabLocalRot = Quaternion.LookRotation(reachDir) * Quaternion.Euler(35f, -15f, 20f);

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
            Vector3 mouthLocalPos = new Vector3(0.08f, -0.18f, 0.35f);
            Quaternion mouthLocalRot = Quaternion.Euler(50f, -15f, 35f);

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
        /// 从项目资源中加载并搭建真实的猩猩臂爪 3D 模型
        /// </summary>
        private void LoadAndSetupApeArmMesh()
        {
            _armHolder = new GameObject("FP_Ape_Arm_Holder").transform;

            // 尝试加载 FlesherApe 资源
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
                apeInstance.name = "FlesherApe_Hands";
                
                // 适度调大猩猩手臂缩放以适配第一视角
                apeInstance.transform.localPosition = new Vector3(-0.1f, -0.4f, 0.2f);
                apeInstance.transform.localRotation = Quaternion.Euler(-10f, 160f, 0f);
                apeInstance.transform.localScale = Vector3.one * 0.45f;

                // 移除手部上的 Collider 以防挡住视角或射线
                Collider[] colliders = apeInstance.GetComponentsInChildren<Collider>(true);
                foreach (var c in colliders) Destroy(c);

                Debug.Log("<color=green>[FirstPersonArm] 成功在第一视角加载并挂载了写实猩猩臂爪 3D 模型 (FlesherApe)！</color>");
            }
            else
            {
                // 降级方案：创建带精美有艺术皮肤纹理的写实手部 Mesh
                CreateStylizedProceduralArm(_armHolder);
            }
        }

        private void CreateStylizedProceduralArm(Transform parent)
        {
            GameObject armRoot = new GameObject("Stylized_Ape_Arm");
            armRoot.transform.SetParent(parent, false);

            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Standard");
            Material armMat = new Material(unlitShader);
            armMat.color = new Color(0.18f, 0.14f, 0.12f); // 猩猩深色皮毛与皮肤

            // 手臂前臂
            GameObject forearm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            forearm.transform.SetParent(armRoot.transform, false);
            forearm.transform.localPosition = new Vector3(0f, -0.2f, -0.25f);
            forearm.transform.localRotation = Quaternion.Euler(75f, 0f, 0f);
            forearm.transform.localScale = new Vector3(0.08f, 0.22f, 0.08f);
            forearm.GetComponent<Renderer>().material = armMat;
            Destroy(forearm.GetComponent<Collider>());

            // 猩猩大手掌
            GameObject palm = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            palm.transform.SetParent(armRoot.transform, false);
            palm.transform.localPosition = Vector3.zero;
            palm.transform.localScale = new Vector3(0.13f, 0.07f, 0.15f);
            palm.GetComponent<Renderer>().material = armMat;
            Destroy(palm.GetComponent<Collider>());
        }
    }
}
