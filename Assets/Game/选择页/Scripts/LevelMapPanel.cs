using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class LevelMapPanel : MonoBehaviour
{
    [Tooltip("渐变出现和隐藏动画都0.5秒，渐变动画结束，才能进行物体销毁")]
    public CanvasGroup Myself;

    [Tooltip("进度文本，格式为3/5，最大关卡数都为5，第一关是1")]
    public Text ProcessText;

    [Tooltip("进度条，通过FillAmount控制，1是满格")]
    public Image ProcessImage;

    [Tooltip("未过关、当前关卡和已过关的状态UI，均通过CanvasGroup控制显隐。解锁时LockedImage回弹缩小并隐藏，随后CurrentImage回弹出现。进入下一关时显示上一关的CompletedImage，第五关保持CurrentImage显示。")]
    public CanvasGroup[] LockedImage = new CanvasGroup[5];
    public CanvasGroup[] CurrentImage = new CanvasGroup[5];
    public CanvasGroup[] CompletedImage = new CanvasGroup[5];

    [Tooltip("关卡图标,默认隐藏，控制FillAmount从0到1显示，时间为0.5秒，子物体有个Text文本，图标完全显示出来之后，再通过缩放动画显示文本，缩放动画要有点回弹效果")]
    public Image[] LevelImage = new Image[5];

    [Tooltip("按状态和关卡数给LevelImage赋值，通往下一关的线显示完成后，把上一关的LevelImage切换为已通关图片")]
    public Sprite[] CompletedSprite = new Sprite[5];
    public Sprite[] CurrentSprite = new Sprite[5];

    [Tooltip("LevelImage之间链接的线，默认隐藏，第一关没有线，从第二关开始要先按照控制FillAmount从0到1(时间0.25秒)显示线，再显示LevelImage")]
    public Image[] Lines = new Image[4];

    private const int LevelCount = 5;
    private const float FadeDuration = 0.5f;
    private const float MarkerDuration = 0.3f;
    private const float LineDuration = 0.25f;
    private const float IconDuration = 0.5f;
    private const float LabelDuration = 0.3f;

    private readonly Vector3[] lockedScales = new Vector3[LevelCount];
    private readonly Vector3[] currentScales = new Vector3[LevelCount];
    private readonly Dictionary<CanvasGroup, (bool Interactable, bool BlocksRaycasts)> markerInput = new Dictionary<CanvasGroup, (bool Interactable, bool BlocksRaycasts)>();
    private readonly Vector3[] labelScales = new Vector3[LevelCount];
    private readonly Text[] labels = new Text[LevelCount];
    private Tween animation;
    private bool closing;

    public int CurrentLevel { get; private set; }

    private void Awake()
    {
        if (Myself == null) Myself = GetComponent<CanvasGroup>();
        if (Myself == null) Myself = gameObject.AddComponent<CanvasGroup>();

        ConfigureFill(ProcessImage);
        for (int i = 0; i < LevelCount; i++)
        {
            CanvasGroup locked = At(LockedImage, i);
            CanvasGroup current = At(CurrentImage, i);
            if (locked != null) lockedScales[i] = locked.transform.localScale;
            if (current != null) currentScales[i] = current.transform.localScale;
            Image icon = At(LevelImage, i);
            if (icon == null) continue;
            ConfigureFill(icon);
            labels[i] = icon.GetComponentInChildren<Text>(true);
            if (labels[i] != null) labelScales[i] = labels[i].transform.localScale;
        }

        for (int i = 0; i < LevelCount - 1; i++) ConfigureFill(At(Lines, i));
    }

    private void OnEnable()
    {
        closing = false;
        CurrentLevel = 1;
        Myself.alpha = 0f;
        SetInput(false);
        ResetLevels();
        UpdateProgressText();

        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Append(Myself.DOFade(1f, FadeDuration).SetEase(Ease.Linear));
        AppendUnlock(sequence, 0);
        animation = sequence.OnComplete(FinishAnimation);
    }

    private void OnDisable()
    {
        animation?.Kill();
        animation = null;
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            NextLevel();
            return;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
        if (Input.GetKeyDown(KeyCode.Space)) NextLevel();
#endif
    }

    public void NextLevel()
    {
        if (!isActiveAndEnabled || closing || animation != null || CurrentLevel >= LevelCount) return;

        int previous = CurrentLevel - 1;
        SetMarkerVisible(At(CurrentImage, previous), false);
        SetMarkerVisible(At(CompletedImage, previous), true);

        CurrentLevel++;
        UpdateProgressText();
        SetInput(false);
        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        AppendUnlock(sequence, CurrentLevel - 1);
        if (sequence.Duration() <= 0f)
        {
            sequence.Kill();
            SetLevelSprite(previous, At(CompletedSprite, previous));
            FinishAnimation();
            return;
        }

        animation = sequence.OnComplete(FinishAnimation);
    }

    public void Hide(Action onHidden = null)
    {
        if (closing) return;
        closing = true;
        SetInput(false);
        animation?.Kill();
        if (!isActiveAndEnabled)
        {
            FinishHiding(onHidden);
            return;
        }

        animation = Myself.DOFade(0f, FadeDuration).SetEase(Ease.Linear).SetUpdate(true).OnComplete(() => FinishHiding(onHidden));
    }

    private void FinishHiding(Action onHidden)
    {
        animation = null;
        onHidden?.Invoke();
    }

    private void ResetLevels()
    {
        if (ProcessImage != null) ProcessImage.fillAmount = 0f;
        for (int i = 0; i < LevelCount; i++)
        {
            CanvasGroup locked = At(LockedImage, i);
            CanvasGroup current = At(CurrentImage, i);
            if (locked != null) locked.transform.localScale = lockedScales[i];
            if (current != null) current.transform.localScale = currentScales[i];

            ResetMarker(locked, true);
            ResetMarker(current, false);
            ResetMarker(At(CompletedImage, i), false);
            ResetFill(At(LevelImage, i));
            if (labels[i] != null)
            {
                labels[i].transform.localScale = labelScales[i];
                labels[i].gameObject.SetActive(false);
            }
        }

        for (int i = 0; i < LevelCount - 1; i++) ResetFill(At(Lines, i));
    }

    private void AppendUnlock(Sequence sequence, int index)
    {
        float startTime = sequence.Duration();
        AppendLevelReveal(sequence, index);
        InsertProgressAnimation(sequence, index, startTime);
    }

    private void InsertProgressAnimation(Sequence sequence, int index, float startTime)
    {
        CanvasGroup locked = At(LockedImage, index);
        CanvasGroup current = At(CurrentImage, index);
        bool fillProgress = ProcessImage != null && index > 0;
        if (locked == null && current == null && !fillProgress) return;

        Sequence progress = DOTween.Sequence();
        if (locked != null)
        {
            progress.Append(locked.transform.DOScale(Vector3.zero, MarkerDuration).SetEase(Ease.InBack));
            progress.AppendCallback(() => HideMarker(locked, lockedScales[index]));
        }

        if (current != null)
        {
            current.transform.localScale = Vector3.zero;
            progress.AppendCallback(() => SetMarkerVisible(current, true));
            progress.Append(current.transform.DOScale(currentScales[index], MarkerDuration).SetEase(Ease.OutBack));
        }

        // The fill sprite spans the first and last markers.
        if (fillProgress) progress.Insert(0f, ProcessImage.DOFillAmount(index / (LevelCount - 1f), MarkerDuration).SetEase(Ease.OutQuad));
        sequence.Insert(startTime, progress);
    }

    private void AppendLevelReveal(Sequence sequence, int index)
    {
        if (index > 0)
        {
            int previous = index - 1;
            AppendFill(sequence, At(Lines, previous), LineDuration);
            sequence.AppendCallback(() => SetLevelSprite(previous, At(CompletedSprite, previous)));
        }

        SetLevelSprite(index, At(CurrentSprite, index));
        AppendFill(sequence, At(LevelImage, index), IconDuration);
        Text label = labels[index];
        if (label == null) return;
        label.transform.localScale = Vector3.zero;
        sequence.AppendCallback(() => label.gameObject.SetActive(true));
        sequence.Append(label.transform.DOScale(labelScales[index], LabelDuration).SetEase(Ease.OutBack));
    }

    private static void AppendFill(Sequence sequence, Image image, float duration)
    {
        if (image == null) return;
        sequence.AppendCallback(() => image.gameObject.SetActive(true));
        sequence.Append(image.DOFillAmount(1f, duration).SetEase(Ease.Linear));
    }

    private void ResetMarker(CanvasGroup group, bool visible)
    {
        if (group == null) return;
        SetMarkerVisible(group, visible);
        if (!group.gameObject.activeSelf) group.gameObject.SetActive(true);
    }

    private void SetMarkerVisible(CanvasGroup group, bool visible)
    {
        if (group == null) return;
        if (!markerInput.TryGetValue(group, out var input))
        {
            input = (group.interactable, group.blocksRaycasts);
            markerInput.Add(group, input);
        }

        group.alpha = visible ? 1f : 0f;
        group.interactable = visible && input.Interactable;
        group.blocksRaycasts = visible && input.BlocksRaycasts;
    }

    private void SetLevelSprite(int index, Sprite sprite)
    {
        Image image = At(LevelImage, index);
        if (image != null && sprite != null) image.sprite = sprite;
    }

    private void UpdateProgressText()
    {
        if (ProcessText != null) ProcessText.text = $"{CurrentLevel}/{LevelCount}";
    }

    private void FinishAnimation()
    {
        animation = null;
        SetInput(true);
    }

    private void SetInput(bool value)
    {
        if (Myself == null) return;
        Myself.interactable = value;
        Myself.blocksRaycasts = value;
    }

    private static void ConfigureFill(Image image)
    {
        if (image == null || image.type == Image.Type.Filled) return;
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
    }

    private static void ResetFill(Image image)
    {
        if (image == null) return;
        image.fillAmount = 0f;
        image.gameObject.SetActive(false);
    }

    private void HideMarker(CanvasGroup marker, Vector3 scale)
    {
        SetMarkerVisible(marker, false);
        marker.transform.localScale = scale;
    }

    private static T At<T>(T[] items, int index) where T : UnityEngine.Object
    {
        return items != null && index >= 0 && index < items.Length ? items[index] : null;
    }
}
