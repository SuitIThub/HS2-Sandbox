using System;
using System.Reflection;
using KKAPI.Studio;
using UnityEngine;
using UnityEngine.UI;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Hijacks the original "Son length" row slider (clears game listeners) so overall size is <see cref="SonScaleSettings.Master"/> only.
    /// Clones that row twice for split penis length (chain Z) and girth (X/Y) as siblings under Manipulate → Chara → State.
    /// Late slider resync runs after default UI sync so body state cannot snap handles away from our settings.
    /// </summary>
    [DefaultExecutionOrder(550000)]
    public sealed class SonScaleManipulateUi : MonoBehaviour
    {
        /// <summary>Full path to the vanilla row GameObject named "Son Length" (not a container above it).</summary>
        private const string VanillaSonLengthRowPath =
            "StudioScene/Canvas Main Menu/02_Manipulate/00_Chara/01_State/Viewport/Content/Etc/Son Length";

        private const string VanillaSliderObjectName = "Slider Son len";

        private const string MasterCaption = "Overall size";
        private const string LengthTitle = "Penis Length";
        private const string GirthTitle = "Penis Girth";
        private const string BallsTitle = "Balls scale";

        public const float MinMul = 0.2f;
        public const float MaxMul = 3f;

        private const float TryIntervalSeconds = 0.75f;

        private static readonly BindingFlags TextPropertyFlags =
            BindingFlags.Public | BindingFlags.Instance;

        private static SonScaleManipulateUi? _instance;

        private float _nextTryTime;
        private bool _suppressEvents;
        private bool _loggedInject;

        private GameObject? _vanillaRow;
        private Slider? _vanillaMasterSlider;

        private GameObject? _rowLength;
        private GameObject? _rowGirth;
        private GameObject? _rowBalls;
        private Slider? _sliderLength;
        private Slider? _sliderGirth;
        private Slider? _sliderBalls;

        private void Awake()
        {
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>Push <see cref="SonScaleSettings"/> into the injected sliders (e.g. after IMGUI reset).</summary>
        public static void PushSettingsToSliders()
        {
            _instance?.ApplySettingsToWidgets();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextTryTime)
                return;

            _nextTryTime = Time.unscaledTime + TryIntervalSeconds;

            if (!StudioAPI.StudioLoaded)
            {
                TearDownInjected();
                _loggedInject = false;
                return;
            }

            if (IsInjectionHealthy())
                return;

            TearDownInjected();
            TryInject();
        }

        /// <summary>
        /// Manipulate / ChaControl often reassigns slider <see cref="Slider.value"/> from body state each frame.
        /// Keep widgets aligned with <see cref="SonScaleSettings"/> without fighting an in-progress drag.
        /// </summary>
        private void LateUpdate()
        {
            if (!StudioAPI.StudioLoaded || _suppressEvents)
                return;

            const float drift = 0.002f;
            bool changed = false;

            if (_vanillaMasterSlider != null && Mathf.Abs(_vanillaMasterSlider.value - SonScaleSettings.Master) > drift)
            {
                _suppressEvents = true;
                _vanillaMasterSlider.value = Mathf.Clamp(SonScaleSettings.Master, MinMul, MaxMul);
                _suppressEvents = false;
                changed = true;
            }

            if (_sliderLength != null && Mathf.Abs(_sliderLength.value - SonScaleSettings.Length) > drift)
            {
                _suppressEvents = true;
                _sliderLength.value = Mathf.Clamp(SonScaleSettings.Length, MinMul, MaxMul);
                _suppressEvents = false;
                changed = true;
            }

            if (_sliderGirth != null && Mathf.Abs(_sliderGirth.value - SonScaleSettings.Girth) > drift)
            {
                _suppressEvents = true;
                _sliderGirth.value = Mathf.Clamp(SonScaleSettings.Girth, MinMul, MaxMul);
                _suppressEvents = false;
                changed = true;
            }

            if (_sliderBalls != null && Mathf.Abs(_sliderBalls.value - SonScaleSettings.Balls) > drift)
            {
                _suppressEvents = true;
                _sliderBalls.value = Mathf.Clamp(SonScaleSettings.Balls, MinMul, MaxMul);
                _suppressEvents = false;
                changed = true;
            }

            if (changed)
            {
                if (_vanillaMasterSlider != null && _vanillaRow != null)
                    SetRowCaption(_vanillaRow, _vanillaMasterSlider, MasterCaption);
                if (_sliderLength != null && _rowLength != null)
                    SetRowCaption(_rowLength, _sliderLength, FormatCaption(LengthTitle));
                if (_sliderGirth != null && _rowGirth != null)
                    SetRowCaption(_rowGirth, _sliderGirth, FormatCaption(GirthTitle));
                if (_sliderBalls != null && _rowBalls != null)
                    SetRowCaption(_rowBalls, _sliderBalls, FormatCaption(BallsTitle));
            }
        }

        private bool IsInjectionHealthy()
        {
            if (_rowLength == null || _rowGirth == null || _rowBalls == null)
                return false;

            GameObject? vanillaRow = GameObject.Find(VanillaSonLengthRowPath);
            if (vanillaRow == null)
                return false;

            Transform? vanillaParent = vanillaRow.transform.parent;
            if (vanillaParent == null)
                return false;

            return _rowLength.transform.parent == vanillaParent
                && _rowGirth.transform.parent == vanillaParent
                && _rowBalls.transform.parent == vanillaParent;
        }

        private void TearDownInjected()
        {
            if (_rowLength != null)
                Destroy(_rowLength);
            if (_rowGirth != null)
                Destroy(_rowGirth);
            if (_rowBalls != null)
                Destroy(_rowBalls);

            _vanillaRow = null;
            _vanillaMasterSlider = null;
            _rowLength = null;
            _rowGirth = null;
            _rowBalls = null;
            _sliderLength = null;
            _sliderGirth = null;
            _sliderBalls = null;
        }

        private void TryInject()
        {
            GameObject? vanillaRow = GameObject.Find(VanillaSonLengthRowPath);
            if (vanillaRow == null)
                return;

            if (vanillaRow.GetComponentInChildren<Slider>(true) == null)
                return;

            Transform? parent = vanillaRow.transform.parent;
            if (parent == null)
                return;

            int vanillaIndex = vanillaRow.transform.GetSiblingIndex();

            GameObject lenInstance = Instantiate(vanillaRow, parent, false);
            _rowLength = ConfigureClonedRow(
                lenInstance,
                "HS2Sandbox_SonScale_Length",
                LengthTitle,
                v => SonScaleSettings.Length = v,
                SonScaleSettings.Length);
            if (_rowLength == null)
                return;

            _rowLength.transform.SetSiblingIndex(Mathf.Clamp(vanillaIndex + 1, 0, parent.childCount - 1));

            GameObject girInstance = Instantiate(vanillaRow, parent, false);
            _rowGirth = ConfigureClonedRow(
                girInstance,
                "HS2Sandbox_SonScale_Girth",
                GirthTitle,
                v => SonScaleSettings.Girth = v,
                SonScaleSettings.Girth);
            if (_rowGirth == null)
            {
                TearDownInjected();
                return;
            }

            _rowGirth.transform.SetSiblingIndex(Mathf.Clamp(vanillaIndex + 2, 0, parent.childCount - 1));

            GameObject ballsInstance = Instantiate(vanillaRow, parent, false);
            _rowBalls = ConfigureClonedRow(
                ballsInstance,
                "HS2Sandbox_SonScale_Balls",
                BallsTitle,
                v => SonScaleSettings.Balls = v,
                SonScaleSettings.Balls);
            if (_rowBalls == null)
            {
                TearDownInjected();
                return;
            }

            _rowBalls.transform.SetSiblingIndex(Mathf.Clamp(vanillaIndex + 3, 0, parent.childCount - 1));

            _sliderLength = _rowLength.GetComponentInChildren<Slider>(true);
            _sliderGirth = _rowGirth.GetComponentInChildren<Slider>(true);
            _sliderBalls = _rowBalls.GetComponentInChildren<Slider>(true);

            _vanillaRow = vanillaRow;
            HijackVanillaMasterSlider(vanillaRow);

            ApplySettingsToWidgets();

            if (!_loggedInject)
            {
                _loggedInject = true;
                SandboxServices.Log.LogInfo(
                    "Son scale: hijacked Son length master slider; added length, girth, and balls rows under Manipulate → Chara → State.");
            }
        }

        private void HijackVanillaMasterSlider(GameObject vanillaRow)
        {
            Slider? sl = FindSliderByName(vanillaRow.transform, VanillaSliderObjectName)
                ?? vanillaRow.GetComponentInChildren<Slider>(true);

            if (sl == null)
                return;

            _vanillaMasterSlider = sl;

            float oldMin = sl.minValue;
            float oldMax = sl.maxValue;
            float oldVal = sl.value;
            float oldSpan = oldMax - oldMin;
            if (oldSpan > 1e-6f)
            {
                float t = Mathf.Clamp01((oldVal - oldMin) / oldSpan);
                SonScaleSettings.Master = Mathf.Lerp(MinMul, MaxMul, t);
            }

            sl.onValueChanged.RemoveAllListeners();
            sl.minValue = MinMul;
            sl.maxValue = MaxMul;

            _suppressEvents = true;
            sl.value = Mathf.Clamp(SonScaleSettings.Master, MinMul, MaxMul);
            _suppressEvents = false;

            sl.onValueChanged.AddListener(v =>
            {
                if (_suppressEvents)
                    return;

                SonScaleSettings.Master = Mathf.Clamp(v, MinMul, MaxMul);
            });

            SetRowCaption(vanillaRow, sl, MasterCaption);
        }

        private static Slider? FindSliderByName(Transform root, string objectName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == objectName)
                    return t.GetComponent<Slider>();
            }

            return null;
        }

        private GameObject? ConfigureClonedRow(
            GameObject clone,
            string rowObjectName,
            string title,
            Action<float> applySetting,
            float initialValue)
        {
            clone.name = rowObjectName;

            Slider? sl = clone.GetComponentInChildren<Slider>(true);
            if (sl == null)
            {
                Destroy(clone);
                return null;
            }

            sl.onValueChanged.RemoveAllListeners();
            sl.minValue = MinMul;
            sl.maxValue = MaxMul;
            sl.wholeNumbers = false;

            float clamped = Mathf.Clamp(initialValue, MinMul, MaxMul);
            _suppressEvents = true;
            sl.value = clamped;
            _suppressEvents = false;

            sl.onValueChanged.AddListener(v =>
            {
                if (_suppressEvents)
                    return;

                applySetting(v);
                SetRowCaption(clone, sl, FormatCaption(title));
            });

            SetRowCaption(clone, sl, FormatCaption(title));
            return clone;
        }

        private static string FormatCaption(string title) => title;

        private void ApplySettingsToWidgets()
        {
            _suppressEvents = true;

            if (_vanillaMasterSlider != null && _vanillaRow != null)
            {
                _vanillaMasterSlider.value = Mathf.Clamp(SonScaleSettings.Master, MinMul, MaxMul);
                SetRowCaption(_vanillaRow, _vanillaMasterSlider, MasterCaption);
            }

            if (_sliderLength != null && _rowLength != null)
                _sliderLength.value = Mathf.Clamp(SonScaleSettings.Length, MinMul, MaxMul);

            if (_sliderGirth != null && _rowGirth != null)
                _sliderGirth.value = Mathf.Clamp(SonScaleSettings.Girth, MinMul, MaxMul);

            if (_sliderBalls != null && _rowBalls != null)
                _sliderBalls.value = Mathf.Clamp(SonScaleSettings.Balls, MinMul, MaxMul);

            _suppressEvents = false;

            if (_rowLength != null && _sliderLength != null)
                SetRowCaption(_rowLength, _sliderLength, FormatCaption(LengthTitle));
            if (_rowGirth != null && _sliderGirth != null)
                SetRowCaption(_rowGirth, _sliderGirth, FormatCaption(GirthTitle));
            if (_rowBalls != null && _sliderBalls != null)
                SetRowCaption(_rowBalls, _sliderBalls, FormatCaption(BallsTitle));
        }

        /// <summary>Sets the row label (legacy <see cref="Text"/> or TMPro <c>TextMeshProUGUI</c>) outside the slider subtree.</summary>
        private static void SetRowCaption(GameObject row, Slider slider, string text)
        {
            foreach (Text ugui in row.GetComponentsInChildren<Text>(true))
            {
                if (ugui.transform == slider.transform || ugui.transform.IsChildOf(slider.transform))
                    continue;

                ugui.text = text;
                return;
            }

            foreach (Component c in row.GetComponentsInChildren<Component>(true))
            {
                if (c == null)
                    continue;

                Transform ct = c.transform;
                if (ct == slider.transform || ct.IsChildOf(slider.transform))
                    continue;

                System.Type ty = c.GetType();
                if (ty.Name != "TextMeshProUGUI")
                    continue;

                PropertyInfo? prop = ty.GetProperty("text", TextPropertyFlags);
                if (prop != null && prop.CanWrite && prop.PropertyType == typeof(string))
                {
                    prop.SetValue(c, text, null);
                    return;
                }
            }
        }
    }
}
