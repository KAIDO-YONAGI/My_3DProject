using UnityEngine;

public interface ICanvasManager
{
    /// <summary>
    /// 本面板订阅的画布开关事件资产。
    /// 该事件资产还必须配置到 <see cref="UIManager"/> 的 toggleCanvasEvents 列表中，
    /// 才能接收 UIManager 发出的开关请求。
    /// </summary>
    ToggleCanvasEventSO ToggleCanvasEvent { get; }

    /// <summary>
    /// 场景加载完成事件资产。
    /// 面板通常在收到该事件后重置为关闭状态，避免持久化对象跨场景保留旧的显示状态。
    /// </summary>
    SceneLoadedEventSO SceneLoadedEvent { get; }

    /// <summary>
    /// 当前面板获得焦点时，是否允许通过 ESC 键关闭。
    /// </summary>
    bool CloseOnEscape => true;

    /// <summary>
    /// 打开时是否阻塞全局面板输入。
    /// 为 true 时，<see cref="UIManager"/> 会吞掉其它面板的切换输入，包括 ESC；
    /// 面板自身仍可通过直接调用关闭逻辑或关闭按钮关闭。
    /// </summary>
    bool BlocksGlobalInput => false;

    /// <summary>
    /// 同步更新画布的可见状态、交互状态和层级顺序。
    /// </summary>
    /// <param name="canvasGroup">控制画布显隐和交互的 CanvasGroup。</param>
    /// <param name="canvas">需要更新 sortingOrder 的 Canvas。</param>
    /// <param name="canvasToToggle">画布对应的逻辑类型。</param>
    /// <param name="state">true 表示打开，false 表示关闭。</param>
    public void ToggleCanvas(
        CanvasGroup canvasGroup,
        Canvas canvas,
        MyEnums.CanvasToToggle canvasToToggle,
        bool state)
    {
        SetCanvaState(canvasGroup, canvasToToggle, state);
        RefreshCanvaOrder(canvas, canvasToToggle, state);
    }

    /// <summary>
    /// 将画布设置为关闭状态。
    /// 该方法会同步通知 <see cref="UIManager"/> 更新焦点栈、ESC 关闭规则和全局输入阻塞状态。
    /// </summary>
    /// <param name="canva">控制画布显隐和交互的 CanvasGroup。</param>
    /// <param name="canvasToToggle">画布对应的逻辑类型。</param>
    void SetCanvaInactive(
        CanvasGroup canva,
        MyEnums.CanvasToToggle canvasToToggle)
    {
        SetCanvaState(canva, canvasToToggle, false);
    }

    /// <summary>
    /// 设置 CanvasGroup 的显示、交互和射线拦截状态，并向 UIManager 上报真实状态。
    /// </summary>
    /// <param name="canva">需要修改的 CanvasGroup。</param>
    /// <param name="canvasToToggle">画布对应的逻辑类型。</param>
    /// <param name="state">true 表示显示并允许交互，false 表示隐藏且禁用交互。</param>
    void SetCanvaState(
          CanvasGroup canva,
          MyEnums.CanvasToToggle canvasToToggle,
          bool state)
    {
        canva.alpha = state ? 1 : 0;
        canva.blocksRaycasts = state;
        canva.interactable = state;

        UIManager.Report(canvasToToggle, state, CloseOnEscape, BlocksGlobalInput);
    }

    /// <summary>
    /// 根据画布当前的开关状态刷新 sortingOrder。
    /// 打开的画布按照 UIManager 维护的打开顺序获得层级，关闭时恢复默认层级。
    /// </summary>
    /// <param name="canvas">需要设置 sortingOrder 的 Canvas。</param>
    /// <param name="canvasToToggle">画布对应的逻辑类型。</param>
    /// <param name="state">true 表示按焦点栈计算层级，false 表示恢复默认层级。</param>
    void RefreshCanvaOrder(
       Canvas canvas,
       MyEnums.CanvasToToggle canvasToToggle,
       bool state)
    {
        int order = UIManager.Instance != null
            //按 open-order 链表顺序分配降序排序优先级，顶层（链表末尾）最高
            ? UIManager.Instance.GetCanvasOrder(canvasToToggle, state)
            : UIManager.DefaultOrder;
        if (canvas == null)
        {
            return;
        }

        canvas.sortingOrder = order;
    }
}
