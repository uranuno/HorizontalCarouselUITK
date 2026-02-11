using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

[UxmlElement]
public partial class HorizontalCarousel : VisualElement
{
    public const string ussClassName = "horizontal-carousel";
    public const string viewportUssClassName = ussClassName + "__viewport";
    public const string containerUssClassName = ussClassName + "__container";

    // 現在のスクロール位置（virtual座標）
    private float m_ScrollOffset;

    private int m_ScrolledItemIndex;

    private bool m_IsDragging;

    private IVisualElementScheduledItem m_ScrollAnimation;

    private float m_AnimStartTime;
    private float m_AnimDuration;

    private float m_AnimStartOffset;
    private float m_AnimTargetOffset;

    private const long k_IntervalMs = 16;

    [UxmlAttribute]
    public VisualTreeAsset itemTemplate { get; set; }

    [UxmlAttribute]
    public int visibleItemCount { get; set; }

    [UxmlAttribute]
    public int offsetItemCount { get; set; }

    [UxmlAttribute]
    public float fixedItemWidth { get; set; } = 100f;

    [UxmlAttribute]
    public float animationSpeed { get; set; } = 0.25f;

    [UxmlAttribute]
    public float swipeMultiplier { get; set; } = 3f;

    [UxmlAttribute]
    public float maxSlide { get; set; } = 0.5f;

    [UxmlAttribute]
    public bool isCenter { get; set; } = false;

    private float m_CenterOffset;

    private VisualElement m_Viewport;

    private VisualElement m_Container;

    public override VisualElement contentContainer => m_Container;

    private class ItemState
    {
        public VisualElement item;

        // どのItemsSource をBind しているか（is-selected 判定に使用）
        public int srcIndex;
    }

    private List<ItemState> m_ItemStates = new List<ItemState>();

    private int m_ItemCount = 0;

    public Func<VisualElement> makeItem { get; set; }

    public Action<VisualElement, int> bindItem { get; set; }

    private IList m_ItemsSource;

    private int m_ItemSourceCount = 0;

    public IList itemsSource
    {
        get => m_ItemsSource;
        set
        {
            if (value == m_ItemsSource)
                return;

            m_ItemsSource = value;
            Rebuild();
        }
    }

    private int m_Value = -1;

    public int value
    {
        get => m_Value;
        set
        {
            if (SetValue(value))
            {
                var srcWidth = m_ItemSourceCount * fixedItemWidth;
                var currentCycle = Mathf.Floor(m_ScrollOffset / srcWidth);
                // 現在の周を考慮して移動
                var targetOffset = currentCycle * srcWidth + m_Value * fixedItemWidth;
                StartScrollAnimation(targetOffset);
            }
        }
    }

    // 相対量でのスクロール
    public void ScrollBy(int amount)
    {
        // 連打防止
        if (m_ScrollAnimation?.isActive == true)
            return;

        var targetOffset = m_ScrollOffset + amount * fixedItemWidth;

        var targetIndex = Mathf.RoundToInt(targetOffset / fixedItemWidth);
        SetValue(targetIndex);

        StartScrollAnimation(targetOffset);
    }

    public HorizontalCarousel()
    {
        AddToClassList(ussClassName);

        m_Viewport = new VisualElement { name = viewportUssClassName };
        m_Viewport.AddToClassList(viewportUssClassName);
        m_Viewport.pickingMode = PickingMode.Ignore;
        hierarchy.Add(m_Viewport);

        m_Container = new VisualElement { name = containerUssClassName };
        m_Container.AddToClassList(containerUssClassName);
        // contentContainer を上書きしているので単純なAdd だと無限ループになる
        m_Viewport.Add(m_Container);

        var scrollable = new Scrollable(OnDown, OnDrag, OnUp);
        m_Container.AddManipulator(scrollable);

        m_Viewport.RegisterCallback<GeometryChangedEvent>(OnViewportGeometryChanged);
    }

    private ItemState MakeItem()
    {
        VisualElement item = null;

        if (makeItem != null)
        {
            item = makeItem.Invoke();
        }
        else if (itemTemplate != null)
        {
            item = itemTemplate.Instantiate();
        }
        else
        {
            item = new Label("Template Not Found");
        }

        item.AddToClassList(ussClassName + "__item");
        m_Container.Add(item);

        // なぜかこれやらないと、長距離移動のとき幅0 になる？（データセットのタイミングでやるのがいい？）
        item.style.width = fixedItemWidth;

        var itemState = new ItemState
        {
            item = item,
            srcIndex = -1
        };

        return itemState;
    }

    private void BindItem(ItemState itemState, int virtualIndex)
    {
        var srcIndex = RepeatInt(virtualIndex, m_ItemSourceCount);

        if (bindItem != null)
        {
            bindItem.Invoke(itemState.item, srcIndex);
        }

        // autoAssignSource フラグ要る？
        itemState.item.dataSource = m_ItemsSource;
        itemState.item.dataSourcePath = PropertyPath.FromIndex(srcIndex);
        itemState.srcIndex = srcIndex;

        // 選択状態をクリアする
        SetItemSelected(itemState, -1);
    }

    /// <summary>
    /// モデルが更新されたら呼ぶ
    /// </summary>
    public void Rebuild()
    {
        m_ItemSourceCount = m_ItemsSource?.Count ?? 0;
        m_ItemCount = Mathf.Min(visibleItemCount, m_ItemSourceCount);

        m_ItemStates.Clear();
        m_Container.Clear();

        m_ScrollOffset = 0;
        m_ScrolledItemIndex = 0;

        m_Value = -1;
        SetValue(0);

        for (int i = 0; i < m_ItemCount; i++)
        {
            var itemState = MakeItem();
            m_ItemStates.Add(itemState);

            BindItem(itemState, i);

            SetItemSelected(itemState, m_Value);
        }

        Debug.Log($"m_ItemCount: {m_ItemCount}");
    }

    private void OnDown(Scrollable scrollable)
    {
        // 連打防止
        if (m_ScrollAnimation?.isActive == true)
            return;

        m_IsDragging = true;
    }

    private void OnDrag(Scrollable scrollable)
    {
        if (!m_IsDragging || m_ScrollAnimation?.isActive == true)
            return;

        m_ScrollOffset -= scrollable.deltaPos.x;
        PositionAllItems();
    }

    private void OnUp(Scrollable scrollable)
    {
        if (!m_IsDragging || m_ScrollAnimation?.isActive == true)
            return;

        m_IsDragging = false;

        //Debug.Log($"velocity:{scrollable.velocity}}");

        var additionalOffset = -scrollable.velocity.x * k_IntervalMs * swipeMultiplier;
        var max = fixedItemWidth * maxSlide;
        additionalOffset = Mathf.Clamp(additionalOffset, -max, max);
        var predictedOffset = m_ScrollOffset + additionalOffset;

        // 予測位置に最も近いカードのインデックスを計算
        var targetIndex = Mathf.RoundToInt(predictedOffset / fixedItemWidth);

        Debug.Log($"m_ScrollOffset:{m_ScrollOffset} additionalOffset:{additionalOffset} targetIndex:{targetIndex}");

        // value 更新
        SetValue(targetIndex);

        var snappedOffset = targetIndex * fixedItemWidth;
        StartScrollAnimation(snappedOffset);
    }

    private void OnViewportGeometryChanged(GeometryChangedEvent evt)
    {
        m_CenterOffset = m_Viewport.layout.width * 0.5f - (offsetItemCount + 0.5f) * fixedItemWidth;
        Debug.Log($"viewportWidth: {m_Viewport.layout.width} contentWidth: {m_Container.layout.width}");
        PositionAllItems();
    }

    private void PositionAllItems()
    {
        if (m_ItemCount <= 0)
            return;

        var layoutOffset = Mathf.Repeat(m_ScrollOffset, m_ItemCount * fixedItemWidth);
        //Debug.Log($"m_ScrollOffset: {m_ScrollOffset}, layoutOffset: {layoutOffset}");

        if (isCenter)
            layoutOffset -= m_CenterOffset;

        foreach (var itemState in m_ItemStates)
        {
            itemState.item.style.translate = new Translate(-layoutOffset, 0);
        }

        var targetIndex = Mathf.FloorToInt(m_ScrollOffset / fixedItemWidth);

        var paddingUnit = RepeatInt(targetIndex, m_ItemCount);
        m_Container.style.paddingLeft = paddingUnit * fixedItemWidth;
        //Debug.Log($"handleloop targetIndex: {targetIndex} paddingUnit: {paddingUnit}");

        var targetIndexWithOffset = targetIndex - offsetItemCount;

        if (targetIndexWithOffset != m_ScrolledItemIndex)
        {
            var indexDiff = targetIndexWithOffset - m_ScrolledItemIndex;
            //Debug.Log($"indexDiff: {indexDiff}, targetIndex: {targetIndex}, m_ScrolledItemIndex: {m_ScrolledItemIndex}");

            if (indexDiff > 0)
            {
                for (int i = 0; i < indexDiff; i++)
                {
                    var itemIndex = RepeatInt(m_ScrolledItemIndex + i, m_ItemCount);
                    m_ItemStates[itemIndex].item.BringToFront();

                    var nextVirtualIndex = m_ScrolledItemIndex + m_ItemCount + i;
                    BindItem(m_ItemStates[itemIndex], nextVirtualIndex);
                }
            }
            else
            {
                for (int i = 0; i < -indexDiff; i++)
                {
                    var itemIndex = RepeatInt(m_ScrolledItemIndex + m_ItemCount - 1 - i, m_ItemCount);
                    m_ItemStates[itemIndex].item.SendToBack();

                    var nextVirtualIndex = m_ScrolledItemIndex - 1 - i;
                    BindItem(m_ItemStates[itemIndex], nextVirtualIndex);
                }
            }

            m_ScrolledItemIndex = targetIndexWithOffset;
        }
    }

    private void StartScrollAnimation(float targetOffset)
    {
        m_ScrollAnimation?.Pause();

        m_AnimStartTime = Time.unscaledTime;

        m_AnimStartOffset = m_ScrollOffset;
        m_AnimTargetOffset = targetOffset;

        var distance = Mathf.Abs(m_AnimTargetOffset - m_AnimStartOffset);
        m_AnimDuration = distance / fixedItemWidth * Mathf.Max(0.1f, animationSpeed);
        //Debug.Log($"distance:{distance} duration:{m_AnimDuration}");

        if (m_ScrollAnimation == null)
        {
            m_ScrollAnimation = schedule.Execute(ScrollAnimation).Every(k_IntervalMs);
        }
        else
        {
            m_ScrollAnimation.Resume();
        }
    }

    private void ScrollAnimation()
    {
        var elapsed = Time.unscaledTime - m_AnimStartTime;

        var t = Mathf.Clamp01(elapsed / m_AnimDuration);
        var easedT = Easing.OutCubic(t);

        m_ScrollOffset = Mathf.Lerp(m_AnimStartOffset, m_AnimTargetOffset, easedT);
        PositionAllItems();

        if (t >= 1f)
        {
            m_ScrollOffset = m_AnimTargetOffset;
            PositionAllItems();
            m_ScrollAnimation.Pause();

            for (int i = 0; i < m_ItemCount; i++)
                SetItemSelected(m_ItemStates[i], m_Value);
        }
    }

    private bool SetValue(int newValue)
    {
        if (m_ItemSourceCount <= 0)
        {
            m_Value = -1;
            return false;
        }

        newValue = RepeatInt(newValue, m_ItemSourceCount);

        if (newValue == m_Value)
            return false;

        m_Value = newValue;

        Debug.Log($"m_ScrollOffset:{m_ScrollOffset} value:{m_Value}");

        // TODO: イベント？

        return true;
    }

    private void SetItemSelected(ItemState itemState, int targetValue)
    {
        if (itemState.srcIndex == targetValue)
            itemState.item.AddToClassList("is-selected");
        else
            itemState.item.RemoveFromClassList("is-selected");
    }

    // Mathf.Repeat のint版が無いので作成
    private static int RepeatInt(int value, int length)
    {
        if (length <= 0)
            return 0;

        var result = value % length;
        return result < 0 ? result + length : result;
    }
}
