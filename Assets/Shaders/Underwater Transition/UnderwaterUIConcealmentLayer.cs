using UnityEngine;
using UnityEngine.UI;

namespace Moonlight.Rendering
{
    /// <summary>
    /// Dedicated fullscreen UI concealment layer positioned above all gameplay HUD elements
    /// in Screen Space - Overlay. Completely hides the viewport and all HUD components during
    /// camera water-crossing transitions, guaranteeing no raw or mismatched frames leak through.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public sealed class UnderwaterUIConcealmentLayer : MonoBehaviour
    {
        [Header("Concealment Visuals")]
        [Tooltip("Immersion color of the fullscreen concealment veil.")]
        [SerializeField] private Color veilColor = new Color(0.025f, 0.18f, 0.25f, 1f);

        [Tooltip("Target sorting order for ScreenSpaceOverlay. 32767 ensures it renders strictly above all HUD canvases.")]
        [SerializeField] private int overlaySortingOrder = 32767;

        private Canvas concealmentCanvas;
        private CanvasGroup concealmentCanvasGroup;
        private Image veilImage;

        public float CurrentCover { get; private set; }

        private void Awake()
        {
            EnsureHierarchy();
            SetCoverAmount(0f);
        }

        private void OnEnable()
        {
            EnsureHierarchy();
        }

        public void SetVeilColor(Color color)
        {
            veilColor = color;
            if (veilImage != null)
                veilImage.color = veilColor;
        }

        public void SetCoverAmount(float cover)
        {
            CurrentCover = Mathf.Clamp01(cover);
            EnsureHierarchy();

            if (CurrentCover <= 0.0005f)
            {
                if (concealmentCanvasGroup != null)
                    concealmentCanvasGroup.alpha = 0f;
                if (concealmentCanvas != null && concealmentCanvas.enabled)
                    concealmentCanvas.enabled = false;
                return;
            }

            if (concealmentCanvas != null && !concealmentCanvas.enabled)
                concealmentCanvas.enabled = true;

            if (concealmentCanvasGroup != null)
                concealmentCanvasGroup.alpha = CurrentCover;
        }

        public bool IsFullyConcealed => CurrentCover >= 0.99f;

        private void EnsureHierarchy()
        {
            if (concealmentCanvas == null)
            {
                concealmentCanvas = GetComponent<Canvas>();
                if (concealmentCanvas == null)
                    concealmentCanvas = gameObject.AddComponent<Canvas>();

                concealmentCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                concealmentCanvas.sortingOrder = overlaySortingOrder;
            }

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            if (concealmentCanvasGroup == null)
            {
                concealmentCanvasGroup = GetComponent<CanvasGroup>();
                if (concealmentCanvasGroup == null)
                    concealmentCanvasGroup = gameObject.AddComponent<CanvasGroup>();

                concealmentCanvasGroup.blocksRaycasts = false;
                concealmentCanvasGroup.interactable = false;
            }

            if (veilImage == null)
            {
                Transform veilChild = transform.Find("ConcealmentVeil");
                GameObject childObj;
                if (veilChild == null)
                {
                    childObj = new GameObject("ConcealmentVeil", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    childObj.transform.SetParent(transform, false);
                }
                else
                {
                    childObj = veilChild.gameObject;
                }

                veilImage = childObj.GetComponent<Image>();
                if (veilImage == null)
                    veilImage = childObj.AddComponent<Image>();

                veilImage.color = veilColor;
                veilImage.raycastTarget = false;

                RectTransform rect = childObj.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                }
            }
            else
            {
                veilImage.color = veilColor;
            }
        }

        public static UnderwaterUIConcealmentLayer GetOrCreate(Transform parent = null)
        {
            var existing = FindObjectOfType<UnderwaterUIConcealmentLayer>(true);
            if (existing != null)
                return existing;

            var go = new GameObject("UnderwaterUIConcealmentLayer",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(CanvasGroup),
                typeof(UnderwaterUIConcealmentLayer));

            if (parent != null)
                go.transform.SetParent(parent, false);

            return go.GetComponent<UnderwaterUIConcealmentLayer>();
        }
    }
}
