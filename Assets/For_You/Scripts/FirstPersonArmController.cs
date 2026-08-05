using System.Collections;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 第一视角手臂与抓取吃蕉控制器 (First-Person Arm & Grab Motion Controller)
    /// 1. 自动挂载在 Main Camera 面前右下方。
    /// 2. 实现第一视角手部呼吸 (Idle Sway) 与视角惯性。
    /// 3. 当玩家点击/交互香蕉时，触发全套伸出 (Reach Forward) ➔ 抓紧 (Pinch Grab) ➔ 拿向嘴边吞噬 (Pull Back & Eat) 动态手势！
    /// </summary>
    public class FirstPersonArmController : MonoBehaviour
    {
        public static FirstPersonArmController Instance { get; private set; }

        [Header("手部配置")]
        public Color skinColor = new Color(0.25f, 0.2f, 0.18f); // 野性深色手部皮肤
        public float reachDistance = 0.85f;                     // 伸出抓取距离
        public float grabSpeed = 8f;                            // 抓取动作速度

        private Transform _armHolder;
        private Transform _handTransform;
        private Transform[] _fingerTransforms;

        private Vector3 _defaultLocalPos = new Vector3(0.35f, -0.32f, 0.55f);
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
            CreateFirstPersonArmMesh();
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

            // 1. 手臂呼吸摇摆 Idle Sway Motion
            float time = Time.time;
            float swayX = Mathf.Sin(time * 1.8f) * 0.015f;
            float swayY = Mathf.Cos(time * 2.2f) * 0.02f;
            float swayZ = Mathf.Sin(time * 1.5f) * 0.01f;

            // 2. 视角转动惯性 Inertia
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            Vector3 lagOffset = new Vector3(-mouseX * 0.012f, -mouseY * 0.012f, 0f);

            _armHolder.localPosition = Vector3.Lerp(_armHolder.localPosition, _defaultLocalPos + new Vector3(swayX, swayY, swayZ) + lagOffset, Time.deltaTime * 6f);
        }

        /// <summary>
        /// 触发手部抓取与吞噬动画 (Play Grab & Eat Animation)
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

            // 计算延伸向目标的本地向量
            Vector3 targetLocalPos = _armHolder.parent.InverseTransformPoint(targetWorldPos);
            // 限制手伸出最远距离
            Vector3 reachDir = (targetLocalPos - startLocalPos).normalized;
            Vector3 grabLocalPos = startLocalPos + reachDir * reachDistance;
            Quaternion grabLocalRot = Quaternion.LookRotation(reachDir) * Quaternion.Euler(45f, 0f, 0f);

            // 1. 伸出手臂 Reach Out
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * grabSpeed;
                float easeT = Mathf.Sin(t * Mathf.PI * 0.5f);
                _armHolder.localPosition = Vector3.Lerp(startLocalPos, grabLocalPos, easeT);
                _armHolder.localRotation = Quaternion.Slerp(startLocalRot, grabLocalRot, easeT);

                // 手指微张伸向目标
                SetFingersCurl(0.2f);
                yield return null;
            }

            // 2. 手掌抓握 Clench / Pinch
            float grabTime = 0f;
            while (grabTime < 1f)
            {
                grabTime += Time.deltaTime * 15f;
                SetFingersCurl(Mathf.Lerp(0.2f, 0.9f, grabTime));
                yield return null;
            }

            onGrabbedCallback?.Invoke();

            // 3. 收回至嘴边 Pull Back & Eat Motion
            Vector3 mouthLocalPos = new Vector3(0.05f, -0.15f, 0.3f);
            Quaternion mouthLocalRot = Quaternion.Euler(45f, -10f, 30f);

            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * (grabSpeed * 0.9f);
                float easeT = t * t * (3f - 2f * t); // Smoothstep
                _armHolder.localPosition = Vector3.Lerp(grabLocalPos, mouthLocalPos, easeT);
                _armHolder.localRotation = Quaternion.Slerp(grabLocalRot, mouthLocalRot, easeT);
                yield return null;
            }

            // 4. 嘴边轻微嚼动与恢复 Idle
            float eatShake = 0f;
            while (eatShake < 0.18f)
            {
                eatShake += Time.deltaTime;
                _armHolder.localPosition = mouthLocalPos + Random.insideUnitSphere * 0.015f;
                yield return null;
            }

            // 放出手指
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 6f;
                _armHolder.localPosition = Vector3.Lerp(mouthLocalPos, _defaultLocalPos, t);
                _armHolder.localRotation = Quaternion.Slerp(mouthLocalRot, _defaultLocalRot, t);
                SetFingersCurl(Mathf.Lerp(0.9f, 0.4f, t));
                yield return null;
            }

            _isGrabbing = false;
        }

        private void SetFingersCurl(float curlRatio)
        {
            if (_fingerTransforms == null) return;
            for (int i = 0; i < _fingerTransforms.Length; i++)
            {
                if (_fingerTransforms[i] != null)
                {
                    float angle = Mathf.Lerp(10f, 75f, curlRatio);
                    _fingerTransforms[i].localRotation = Quaternion.Euler(angle, 0f, 0f);
                }
            }
        }

        /// <summary>
        /// 动态程序化生成写实第一视角 3D 手臂与手掌 Mesh
        /// </summary>
        private void CreateFirstPersonArmMesh()
        {
            _armHolder = new GameObject("FP_Arm_Holder").transform;

            // 创建材质
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Standard");
            Material armMat = new Material(unlitShader);
            armMat.color = skinColor;

            // 1. 前臂 Forearm
            GameObject forearm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            forearm.name = "Forearm";
            forearm.transform.SetParent(_armHolder, false);
            forearm.transform.localPosition = new Vector3(0f, -0.2f, -0.2f);
            forearm.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
            forearm.transform.localScale = new Vector3(0.09f, 0.25f, 0.09f);
            forearm.GetComponent<Renderer>().material = armMat;
            Destroy(forearm.GetComponent<Collider>());

            // 2. 手掌 Palm
            GameObject palm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            palm.name = "Palm";
            palm.transform.SetParent(_armHolder, false);
            palm.transform.localPosition = Vector3.zero;
            palm.transform.localRotation = Quaternion.identity;
            palm.transform.localScale = new Vector3(0.12f, 0.04f, 0.14f);
            palm.GetComponent<Renderer>().material = armMat;
            Destroy(palm.GetComponent<Collider>());
            _handTransform = palm.transform;

            // 3. 4根手指 Fingers
            _fingerTransforms = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                GameObject fingerRoot = new GameObject($"Finger_{i}");
                fingerRoot.transform.SetParent(palm.transform, false);
                float posX = (i - 1.5f) * 0.032f;
                fingerRoot.transform.localPosition = new Vector3(posX, 0f, 0.07f);

                GameObject fingerSegment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fingerSegment.transform.SetParent(fingerRoot.transform, false);
                fingerSegment.transform.localPosition = new Vector3(0f, 0f, 0.035f);
                fingerSegment.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                fingerSegment.transform.localScale = new Vector3(0.022f, 0.035f, 0.022f);
                fingerSegment.GetComponent<Renderer>().material = armMat;
                Destroy(fingerSegment.GetComponent<Collider>());

                _fingerTransforms[i] = fingerRoot.transform;
            }
        }
    }
}
