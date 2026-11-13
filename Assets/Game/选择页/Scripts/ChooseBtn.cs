using System.Reflection;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ChooseBtn : TouchUIButton
{
    private const float SelectedScale = 1.06f;
    private const float ScaleDuration = 0.18f;
    private const BindingFlags InstanceField = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo TargetImageField = typeof(TouchUIButton).GetField("targetImage", InstanceField);
    private static readonly FieldInfo PressedSpriteField = typeof(TouchUIButton).GetField("currentPressedSprite", InstanceField);

    private Vector3 defaultScale;
    private Tween scaleTween;
    private bool selected;

    protected override void Awake()
    {
        defaultScale = transform.localScale;
        base.Awake();
    }

    protected override void PlayPressedVisual()
    {
        SetSelected(true);
    }

    protected override void RefreshVisual()
    {
        base.RefreshVisual();
        if (!selected || !interactable) return;

        // Reuse the base button's bound Image and language-specific sprite.
        Image image = TargetImageField?.GetValue(this) as Image;
        Sprite sprite = PressedSpriteField?.GetValue(this) as Sprite;
        if (image != null && sprite != null) image.sprite = sprite;
    }

    public void SetSelected(bool value, bool animate = true)
    {
        if (selected == value && animate) return;
        selected = value;
        RefreshVisual();

        scaleTween?.Kill();
        scaleTween = null;
        Vector3 targetScale = selected ? defaultScale * SelectedScale : defaultScale;
        if (!animate || !isActiveAndEnabled)
        {
            transform.localScale = targetScale;
            return;
        }

        scaleTween = transform.DOScale(targetScale, ScaleDuration).SetEase(Ease.OutQuad).SetUpdate(true).OnKill(() => scaleTween = null);
    }

    private void OnDestroy()
    {
        scaleTween?.Kill();
    }
}
