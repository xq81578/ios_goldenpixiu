using UnityEngine;
using UnityEngine.UI;

namespace Slot.Common.UI
{
    /// <summary>
    /// Rounded rectangle for uGUI using an SDF in the fragment shader (smooth edges at any contrast).
    /// Use on a Button instead of Image; keep Target Graphic pointing here and Color Tint on the Button.
    /// Per-instance material is not serialized on <see cref="Graphic.m_Material"/> (avoids Missing ref after Play Mode).
    /// Requires <see cref="DefaultResourcesMaterialName"/> under a Resources folder, or assign <see cref="m_SdfTemplateMaterial"/>.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Rounded Rect (SDF)")]
    public class RoundedRectGraphic : MaskableGraphic, ISerializationCallbackReceiver
    {
        /// <summary>Resources.Load material name (no path, no extension).</summary>
        public const string DefaultResourcesMaterialName = "UIRoundedRectSDF";

        [Tooltip("Optional. Drag the project’s UIRoundedRectSDF material here when not using Resources, or after moving assets so the default name/path does not apply.")]
        [SerializeField] Material m_SdfTemplateMaterial;

        [SerializeField, Min(0f)] float m_Radius = 20f;

        [SerializeField] bool m_IndependentCorners;

        [SerializeField, Min(0f)] float m_RadiusTL = 20f;
        [SerializeField, Min(0f)] float m_RadiusTR = 20f;
        [SerializeField, Min(0f)] float m_RadiusBR = 20f;
        [SerializeField, Min(0f)] float m_RadiusBL = 20f;

        [Tooltip("Multiplies screen-space edge softness. Higher = slightly blurrier edge; helps if edges still look harsh.")]
        [SerializeField, Range(0.35f, 3f)] float m_EdgeAaScale = 1f;

        [HideInInspector] [SerializeField] int m_CornerSegments = 10;
        [HideInInspector] [SerializeField] float m_MaxStraightEdgeSegment = 32f;

        static Material s_CachedResourcesTemplate;

        [System.NonSerialized] Material _drawInstance;
        [System.NonSerialized] Object _drawTemplateSource;

        static readonly int s_IdRectLbrt = Shader.PropertyToID("_RectLbrt");
        static readonly int s_IdRadii = Shader.PropertyToID("_Radii");
        static readonly int s_IdEdgeAa = Shader.PropertyToID("_EdgeAaScale");

        Vector4 _sdfRectLbrt;
        Vector4 _sdfRadiiTrBrTlBl;
        bool _sdfParamsReady;

        public void OnBeforeSerialize()
        {
#if UNITY_EDITOR
            if (m_Material != null && !UnityEditor.EditorUtility.IsPersistent(m_Material))
                m_Material = null;
#endif
        }

        public void OnAfterDeserialize() { }

        public override Material material
        {
            get
            {
                EnsureDrawMaterial();
                return _drawInstance != null ? _drawInstance : defaultMaterial;
            }
            set
            {
                DestroyDrawInstance();
                m_Material = value;
                SetMaterialDirty();
            }
        }

        public override Material defaultMaterial
        {
            get
            {
                var mat = ResolveSdfTemplateMaterial();
                return mat != null ? mat : base.defaultMaterial;
            }
        }

        protected override void OnEnable()
        {
#if UNITY_EDITOR
            StripInvalidSerializedMaterial();
#endif
            DestroyDrawInstance();
            base.OnEnable();
            SetMaterialDirty();
            SetVerticesDirty();
            ValidateUserMaterialWarning();
        }

#if UNITY_EDITOR
        void StripInvalidSerializedMaterial()
        {
            if (m_Material == null)
                return;
            if (UnityEditor.EditorUtility.IsPersistent(m_Material))
                return;
            Object.DestroyImmediate(m_Material);
            m_Material = null;
        }
#endif

        void DestroyDrawInstance()
        {
            if (_drawInstance == null)
            {
                _drawTemplateSource = null;
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Object.DestroyImmediate(_drawInstance);
            else
                Destroy(_drawInstance);
#else
            Destroy(_drawInstance);
#endif
            _drawInstance = null;
            _drawTemplateSource = null;
        }

        void EnsureDrawMaterial()
        {
            Material templateSource = m_Material != null ? m_Material : ResolveSdfTemplateMaterial();
            if (templateSource == null || templateSource.shader == null)
            {
                DestroyDrawInstance();
                return;
            }

            if (!MaterialUsesExpectedSdfShader(templateSource))
            {
                DestroyDrawInstance();
                return;
            }

            if (_drawInstance != null && _drawTemplateSource == templateSource)
                return;

            DestroyDrawInstance();
            _drawInstance = new Material(templateSource);
            _drawTemplateSource = templateSource;
        }

        void ValidateUserMaterialWarning()
        {
            if (m_Material == null)
                return;
            if (MaterialUsesExpectedSdfShader(m_Material))
                return;
            var t = ResolveSdfTemplateMaterial();
            var hint = t != null && t.shader != null
                ? "Use the same Shader as the template: \"" + t.shader.name + "\"."
                : "Assign Sdf Template Material or place \"" + DefaultResourcesMaterialName + ".mat\" in a Resources folder.";
            Debug.LogWarning("[RoundedRectGraphic] \"" + name + "\" Material slot should be empty or use the SDF shader. " + hint, this);
        }

        Material ResolveSdfTemplateMaterial()
        {
            if (m_SdfTemplateMaterial != null && m_SdfTemplateMaterial.shader != null)
                return m_SdfTemplateMaterial;

            if (s_CachedResourcesTemplate == null)
                s_CachedResourcesTemplate = Resources.Load<Material>(DefaultResourcesMaterialName);

            return s_CachedResourcesTemplate;
        }

        bool MaterialUsesExpectedSdfShader(Material m)
        {
            if (m == null || m.shader == null)
                return false;
            var template = ResolveSdfTemplateMaterial();
            if (template != null && template.shader != null)
                return m.shader == template.shader;
            return m.HasProperty(s_IdRectLbrt);
        }

        public float Radius
        {
            get => m_Radius;
            set
            {
                if (Mathf.Approximately(m_Radius, value)) return;
                m_Radius = Mathf.Max(0f, value);
                SetVerticesDirty();
            }
        }

        public int CornerSegments
        {
            get => m_CornerSegments;
            set => m_CornerSegments = Mathf.Clamp(value, 1, 32);
        }

        public float EdgeAaScale
        {
            get => m_EdgeAaScale;
            set
            {
                m_EdgeAaScale = Mathf.Clamp(value, 0.35f, 3f);
                SetVerticesDirty();
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            m_CornerSegments = Mathf.Clamp(m_CornerSegments, 1, 32);
            m_MaxStraightEdgeSegment = Mathf.Max(8f, m_MaxStraightEdgeSegment);
            m_EdgeAaScale = Mathf.Clamp(m_EdgeAaScale, 0.35f, 3f);
            StripInvalidSerializedMaterial();
            DestroyDrawInstance();
            base.OnValidate();
            SetMaterialDirty();
            SetVerticesDirty();
            ValidateUserMaterialWarning();

            if (m_Material == null && ResolveSdfTemplateMaterial() == null)
            {
                Debug.LogError(
                    "[RoundedRectGraphic] No SDF template on \"" + name +
                    "\". Assign Sdf Template Material or add \"" + DefaultResourcesMaterialName + ".mat\" under Slotframework/Resources (or any Resources folder).",
                    this);
            }
        }
#endif

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }

        protected override void UpdateMaterial()
        {
            base.UpdateMaterial();
            ApplySdfParamsToMaterial();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            float w = rect.width;
            float h = rect.height;
            if (w <= 0f || h <= 0f)
            {
                _sdfParamsReady = false;
                return;
            }

            float left = rect.xMin;
            float right = rect.xMax;
            float bottom = rect.yMin;
            float top = rect.yMax;

            float rtl, rtr, rbr, rbl;
            if (m_IndependentCorners)
            {
                rtl = m_RadiusTL;
                rtr = m_RadiusTR;
                rbr = m_RadiusBR;
                rbl = m_RadiusBL;
            }
            else
            {
                rtl = rtr = rbr = rbl = m_Radius;
            }

            ScaleCornerRadii(ref rtl, ref rtr, ref rbr, ref rbl, w, h);

            _sdfRectLbrt = new Vector4(left, bottom, right, top);
            _sdfRadiiTrBrTlBl = new Vector4(rtr, rbr, rtl, rbl);
            _sdfParamsReady = true;

            var lb = color;

            void AddVert(float x, float y, float u, float v)
            {
                var vert = UIVertex.simpleVert;
                vert.position = new Vector3(x, y, 0f);
                vert.color = lb;
                vert.uv0 = new Vector2(u, v);
                vh.AddVert(vert);
            }

            AddVert(left, bottom, 0f, 0f);
            AddVert(left, top, 0f, 1f);
            AddVert(right, top, 1f, 1f);
            AddVert(right, bottom, 1f, 0f);

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(0, 2, 3);
        }

        void ApplySdfParamsToMaterial()
        {
            if (!_sdfParamsReady || canvasRenderer == null || canvasRenderer.materialCount < 1)
                return;

            var rm = canvasRenderer.GetMaterial(0);
            if (rm == null || !rm.HasProperty(s_IdRectLbrt))
                return;

            rm.SetVector(s_IdRectLbrt, _sdfRectLbrt);
            rm.SetVector(s_IdRadii, _sdfRadiiTrBrTlBl);
            rm.SetFloat(s_IdEdgeAa, m_EdgeAaScale);
        }

        static void ScaleCornerRadii(ref float rtl, ref float rtr, ref float rbr, ref float rbl, float w, float h)
        {
            float SafeDiv(float num, float den)
            {
                return den > 1e-4f ? num / den : float.PositiveInfinity;
            }

            float s = 1f;
            s = Mathf.Min(s, SafeDiv(w, rtl + rtr));
            s = Mathf.Min(s, SafeDiv(w, rbl + rbr));
            s = Mathf.Min(s, SafeDiv(h, rtl + rbl));
            s = Mathf.Min(s, SafeDiv(h, rtr + rbr));
            if (float.IsInfinity(s) || s >= 1f)
                return;
            rtl *= s;
            rtr *= s;
            rbr *= s;
            rbl *= s;
        }
    }
}
