using System.Collections;
using Coffee.UIEffects;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using MTFrame.MTEvent;
using LanguageSetting;

public class ChooseManager : MonoBehaviour
{
    [Tooltip("返回首页按钮，当进入LevelMapPanel的时候才显示")]
    public TouchUIButton BackBtn;

    [Tooltip("确定按钮")]
    public TouchUIButton ConfirmBtn;

    [Tooltip("渐变0.5秒消失和显示")]
    public CanvasGroup ChoosePanel;

    [Tooltip("分别对应益智海盗、森林侦探、太空工程师、海洋守护队四个游戏")]
    public ChooseBtn[] chooseBtns = new ChooseBtn[4];

    [Tooltip("关卡地图的父物体，把需要承载LevelMapPanel的物体拖到这里")]
    public Transform LevelMapParent;

    [Tooltip("从Resources加载并实例化到LevelMapParent下，返回首页时销毁实例并释放未使用资源，预制体根物体挂有LevelMapPanel")]
    public string[] LoadPaths = new string[4]
    {
        "益智海盗", "森林侦探", "太空工程师", "海洋守护队"
    };

    private const float FadeDuration = 0.5f;
    private const float SelectionDuration = 10f;
    private const float ConfirmBreathScale = 1.05f;
    private const float ConfirmBreathDuration = 0.7f;

    private UnityAction[] chooseActions;
    private bool[] chooseInteractable;
    private bool confirmInteractable;
    private bool backInteractable;
    private int selectedIndex = -1;
    private float selectionDeadline;
    private bool switching;
    private bool openingMap;
    private bool returningHome;
    private int pendingNextLevelCommands;
    private Tween homeFade;
    private Tween confirmBreath;
    private Vector3 confirmDefaultScale;
    private LevelMapPanel currentMap;

    [Tooltip("播放对应的故事语音，顺序跟chooseBtns的顺序一致，点击就触发播放")]
    public string[] audioPath_cn = new string[4];
    public string[] audioPath_en = new string[4];

    private void Awake()
    {
        int count = chooseBtns?.Length ?? 0;
        chooseActions = new UnityAction[count];
        chooseInteractable = new bool[count];

        for (int i = 0; i < count; i++)
        {
            int index = i;
            chooseActions[i] = () => SelectGame(index);
            chooseInteractable[i] =
                chooseBtns[i] != null && chooseBtns[i].interactable;
        }

        confirmInteractable = ConfirmBtn != null && ConfirmBtn.interactable;

        if (ConfirmBtn != null)
            confirmDefaultScale = ConfirmBtn.transform.localScale;

        backInteractable = BackBtn != null && BackBtn.interactable;
    }

    private void OnEnable()
    {
        EventManager.AddListener(
            GenericEventEnumType.Message,
            nameof(OrderData),
            OnOrderReceived);

        for (int i = 0; i < chooseActions.Length; i++)
        {
            if (chooseBtns[i] != null)
                chooseBtns[i].AddListener(chooseActions[i]);
        }

        if (ConfirmBtn != null)
            ConfirmBtn.AddListener(ConfirmSelection);

        if (BackBtn != null)
            BackBtn.AddListener(ReturnFromMap);

        SetBackVisible(false);

        if (ChoosePanel != null)
            ChoosePanel.alpha = 1f;

        SetHomeInput(true);
    }

    private void OnDisable()
    {
        EventManager.RemoveListener(
            GenericEventEnumType.Message,
            nameof(OrderData),
            OnOrderReceived);

        openingMap = false;
        returningHome = false;
        pendingNextLevelCommands = 0;

        for (int i = 0; i < chooseActions.Length; i++)
        {
            if (chooseBtns[i] != null)
                chooseBtns[i].RemoveListener(chooseActions[i]);
        }

        if (ConfirmBtn != null)
            ConfirmBtn.RemoveListener(ConfirmSelection);

        if (BackBtn != null)
            BackBtn.RemoveListener(ReturnFromMap);

        homeFade?.Kill();
        homeFade = null;

        ClearSelection(false);
        SetHomeInput(false);
        SetBackVisible(false);
        switching = false;

        if (currentMap != null)
            Destroy(currentMap.gameObject);

        currentMap = null;
    }

    private void Update()
    {
        if (selectedIndex >= 0 && Time.unscaledTime >= selectionDeadline)
            ClearSelection(true);
    }

    private void SelectGame(int index)
    {
        if (switching || currentMap != null)
            return;

        PlayStoryVoice(index);

        if (selectedIndex == index && Time.unscaledTime < selectionDeadline)
            return;

        if (selectedIndex >= 0 && selectedIndex != index)
            ClearSelection(true);

        selectedIndex = index;
        selectionDeadline = Time.unscaledTime + SelectionDuration;
    }

    private void PlayStoryVoice(int index)
    {
        bool english = LanguageManger.Instance != null &&
            LanguageManger.Instance.GetLanguageType() == LanguageType.英文;

        string[] paths = english ? audioPath_en : audioPath_cn;

        if (paths == null ||
            index < 0 ||
            index >= paths.Length ||
            string.IsNullOrWhiteSpace(paths[index]))
        {
            return;
        }

        AudioManager.PlayAudio(paths[index],null,MTFrame.MTAudio.AudioEnunType.Speech);
    }

    private void ClearSelection(bool animate)
    {
        if (selectedIndex >= 0 && chooseBtns[selectedIndex] != null)
            chooseBtns[selectedIndex].SetSelected(false, animate);

        selectedIndex = -1;
        selectionDeadline = 0f;
    }

    private void ConfirmSelection()
    {
        if (switching ||
            currentMap != null ||
            selectedIndex < 0 ||
            chooseBtns == null ||
            selectedIndex >= chooseBtns.Length ||
            chooseBtns[selectedIndex] == null)
        {
            return;
        }

        if (Time.unscaledTime >= selectionDeadline)
        {
            ClearSelection(true);
            return;
        }

        if (ChoosePanel == null ||
            LevelMapParent == null ||
            !LevelMapParent.gameObject.activeInHierarchy)
        {
            Debug.LogError(
                "ChooseManager requires ChoosePanel and an active LevelMapParent.",
                this);
            return;
        }

        if (LoadPaths == null ||
            selectedIndex >= LoadPaths.Length ||
            string.IsNullOrWhiteSpace(LoadPaths[selectedIndex]))
        {
            Debug.LogError(
                "ChooseManager is missing a Resources path for the selected game.",
                this);
            return;
        }

        string path = LoadPaths[selectedIndex];
        GameObject prefab = Resources.Load<GameObject>(path);

        if (prefab == null || prefab.GetComponent<LevelMapPanel>() == null)
        {
            Debug.LogError(
                $"Expected a LevelMapPanel on the root of Resources/{path}.",
                this);
            return;
        }

        string gameName = chooseBtns[selectedIndex].gameObject.name;

        if (string.IsNullOrWhiteSpace(gameName))
            return;

        switching = true;
        openingMap = true;
        pendingNextLevelCommands = 0;

        SendToProjection(ClassEnum.UdpGameName, new UdpGameName(gameName));
        ClearSelection(true);
        SetHomeInput(false);

        homeFade = ChoosePanel
            .DOFade(0f, FadeDuration)
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .OnComplete(() => OpenMap(prefab));
    }

    private void OpenMap(GameObject prefab)
    {
        homeFade = null;

        if (LevelMapParent == null ||
            !LevelMapParent.gameObject.activeInHierarchy)
        {
            openingMap = false;
            pendingNextLevelCommands = 0;
            StartCoroutine(ReleaseMapResources());
            ShowHome();
            return;
        }

        GameObject instance = Instantiate(prefab, LevelMapParent, false);
        currentMap = instance.GetComponent<LevelMapPanel>();
        currentMap.enabled = true;

        if (!instance.activeSelf)
            instance.SetActive(true);

        switching = false;
        openingMap = false;
        SetBackVisible(true);

        while (pendingNextLevelCommands > 0)
        {
            pendingNextLevelCommands--;
            currentMap.NextLevel();
        }
    }

    private void ReturnFromMap()
    {
        BeginReturnFromMap(true);
    }

    private void BeginReturnFromMap(bool notifyProjection)
    {
        if (returningHome || (!openingMap && currentMap == null))
            return;

        returningHome = true;
        switching = true;
        openingMap = false;
        pendingNextLevelCommands = 0;

        homeFade?.Kill();
        homeFade = null;

        if (notifyProjection)
            SendToProjection(ClassEnum.OrderData, new OrderData(Order.返回首页));

        SetBackVisible(false);

        if (currentMap != null)
        {
            currentMap.Hide(CloseMap);
        }
        else
        {
            StartCoroutine(ReleaseMapResources());
            ShowHome();
        }
    }

    private static void SendToProjection(ClassEnum classEnum, object data)
    {
        try
        {
            UDPSend sender = UDPSend.Instance;

            if (sender != null)
                sender.TrySend(classEnum, data);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("发送投影端消息失败：" + exception.Message);
        }
    }

    private void OnOrderReceived(EventParamete parameters)
    {
        if (!isActiveAndEnabled || parameters == null)
            return;

        var messages = parameters.GetParameter<OrderData>();

        if (messages == null)
            return;

        foreach (OrderData message in messages)
        {
            if (message == null || message.order != Order.返回首页)
                continue;

            BeginReturnFromMap(false);
            return;
        }

        foreach (OrderData message in messages)
        {
            if (message == null || message.order != Order.下一关)
                continue;

            if (openingMap)
            {
                pendingNextLevelCommands = Mathf.Min(
                    pendingNextLevelCommands + 1,
                    LevelMapPanel.LevelCount - 1);

                continue;
            }

            if (!switching && currentMap != null)
                currentMap.NextLevel();
        }
    }

    private void CloseMap()
    {
        if (currentMap != null)
            Destroy(currentMap.gameObject);

        currentMap = null;
        StartCoroutine(ReleaseMapResources());
        ShowHome();
    }

    private void ShowHome()
    {
        homeFade = ChoosePanel
            .DOFade(1f, FadeDuration)
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .OnComplete(FinishShowingHome);
    }

    private void FinishShowingHome()
    {
        homeFade = null;
        switching = false;
        returningHome = false;
        SetHomeInput(true);
    }

    private IEnumerator ReleaseMapResources()
    {
        yield return null;
        yield return Resources.UnloadUnusedAssets();
    }

    private void SetHomeInput(bool value)
    {
        if (ChoosePanel != null)
        {
            ChoosePanel.interactable = value;
            ChoosePanel.blocksRaycasts = value;
        }

        if (ConfirmBtn != null)
            ConfirmBtn.interactable = value && confirmInteractable;

        SetConfirmBreath(value && confirmInteractable);

        for (int i = 0; i < chooseInteractable.Length; i++)
        {
            if (chooseBtns[i] != null)
                chooseBtns[i].interactable = value && chooseInteractable[i];
        }
    }

    private void SetConfirmBreath(bool value)
    {
        if (!value)
        {
            confirmBreath?.Kill();
            confirmBreath = null;

            if (ConfirmBtn != null)
                ConfirmBtn.transform.localScale = confirmDefaultScale;

            return;
        }

        if (ConfirmBtn == null || confirmBreath != null)
            return;

        confirmBreath = ConfirmBtn.transform
            .DOScale(
                confirmDefaultScale * ConfirmBreathScale,
                ConfirmBreathDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true)
            .OnKill(() => confirmBreath = null);
    }

    private void SetBackVisible(bool value)
    {
        if (BackBtn == null)
            return;

        BackBtn.interactable = value && backInteractable;
        BackBtn.gameObject.SetActive(value);
    }
}