using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

[UxmlElement]
public partial class HorizontalCarousel : VisualElement, INotifyValueChanged<int>
{
    internal static readonly BindingId itemsSourceProperty = nameof(itemsSource);
    internal static readonly BindingId valueProperty = nameof(value);

    public const string ussClassName = "horizontal-carousel";
    public const string viewportUssClassName = ussClassName + "__viewport";
    public const string containerUssClassName = ussClassName + "__container";
    public const string selectedUssClassName = "is-selected";

    // 現在のスクロール位置（仮想座標）
    private float m_ScrollOffset;

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
    public bool center { get; set; } = false; // 中央寄せ

    [UxmlAttribute]
    public bool wrap { get; set; } = true; // falseにすると端で止まる

    [UxmlAttribute]
    public float elasticity { get; set; } = 0.2f;

    private float m_CenterOffset;

    private VisualElement m_Viewport;

    private VisualElement m_Container;

    private class ItemState
    {
        // 仮想index（wrap:false時に範囲外を隠すのに使用）
        public int virtualIndex;

        // どのItemsSource をBind しているか（is-selected 判定に使用）
        public int srcIndex;
    }

    private Dictionary<VisualElement, ItemState> m_ItemStates = new();

    private int m_ItemCount = 0;

    public Func<VisualElement> makeItem { get; set; }

    public Action<VisualElement, int> bindItem { get; set; }

    private IList m_ItemsSource;

    private int m_ItemSourceCount = 0;

    [CreateProperty]
    public IList itemsSource
    {
        get => m_ItemsSource;
        set
        {
            if (value == m_ItemsSource)
                return;

            m_ItemsSource = value;
            m_ItemSourceCount = m_ItemsSource?.Count ?? 0;
            m_Value = -1; //value のセットも別途呼ぶこと

            Rebuild();

            NotifyPropertyChanged(in itemsSourceProperty);
        }
    }

    private int m_Value;

    [CreateProperty]
    public int value
    {
        get => m_Value;
        set
        {
            var previous = m_Value;

            SetValueWithoutNotify(value);

            if (previous != m_Value)
            {
                Debug.Log($"value updated! previous:{previous} value:{m_Value}");

                m_ScrollAnimation?.Pause();

                // 仮想座標リセット
                m_ScrollOffset = m_Value * fixedItemWidth;
                PositionAllItems();

                // 次フレームで選択状態にする
                UpdateItemSelected(false);
                schedule.Execute(() => UpdateItemSelected(true));

                NotifyValueChanged(previous, m_Value);
            }
        }
    }

    public bool canGoToNext => wrap || (value < m_ItemSourceCount - 1 && value >= 0);

    public bool canGoToPrevious => wrap || (value > 0);

    // 相対量でのスクロール
    public void ScrollBy(int amount)
    {
        // 連打防止
        if (m_ScrollAnimation?.isActive == true)
            return;

        var targetOffset = m_ScrollOffset + amount * fixedItemWidth;
        ScrollTo(targetOffset);

        UpdateItemSelected(false);
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

    private (VisualElement, ItemState) MakeItem()
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
            virtualIndex = 0,
            srcIndex = -1
        };

        return (item, itemState);
    }

    private void UpdateItem(VisualElement item, int virtualIndex)
    {
        item.visible = wrap //wrap時は常に見える
            || (virtualIndex >= 0 && virtualIndex < m_ItemSourceCount);

        // 見えるときだけバインドを更新する
        if (item.visible)
        {
            var srcIndex = RepeatInt(virtualIndex, m_ItemSourceCount);
            BindItem(item, srcIndex);

            m_ItemStates[item].srcIndex = srcIndex;
        }

        //Debug.Log($"UpdateItem old:{m_ItemStates[item].virtualIndex} new:{virtualIndex} item.visible:{item.visible}");
        m_ItemStates[item].virtualIndex = virtualIndex;
    }

    private void BindItem(VisualElement item, int srcIndex)
    {
        if (bindItem != null)
        {
            bindItem.Invoke(item, srcIndex);
        }

        // autoAssignSource フラグ要る？
        item.dataSource = m_ItemsSource;
        item.dataSourcePath = PropertyPath.FromIndex(srcIndex);
    }

    private VisualElement ItemAt(int index)
    {
        return m_Container.ElementAt(index);
    }

    // モデルが更新されたら呼ぶ
    private void Rebuild()
    {
        m_ItemCount = Mathf.Min(visibleItemCount + 1, m_ItemSourceCount);

        m_ItemStates.Clear();
        m_Container.Clear();

        m_ScrollOffset = 0;

        for (int i = 0; i < m_ItemCount; i++)
        {
            var (item, itemState) = MakeItem();
            m_ItemStates[item] = itemState;

            UpdateItem(item, i);
        }

        Debug.Log($"Rebuild! m_ItemCount: {m_ItemCount}");
    }

    private void OnDown(Scrollable scrollable)
    {
        // 連打防止
        if (m_ScrollAnimation?.isActive == true)
            return;

        m_IsDragging = true;

        UpdateItemSelected(false);
    }

    private void OnDrag(Scrollable scrollable)
    {
        if (!m_IsDragging || m_ScrollAnimation?.isActive == true)
            return;

        var delta = scrollable.deltaPos.x;
        if(!wrap)
        {
            var endScrollOffset = (m_ItemSourceCount - 1) * fixedItemWidth;
            if (m_ScrollOffset < 0 || m_ScrollOffset > endScrollOffset)
            {
                delta *= elasticity; // 範囲外では%分だけ反映
            }
        }

        m_ScrollOffset -= delta;
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

        ScrollTo(predictedOffset);
    }

    private void OnViewportGeometryChanged(GeometryChangedEvent evt)
    {
        m_CenterOffset = m_Viewport.layout.width * 0.5f - (offsetItemCount + 0.5f) * fixedItemWidth;
        //Debug.Log($"m_CenterOffset: {m_CenterOffset}");
        PositionAllItems();
    }

    private void PositionAllItems()
    {
        if (m_ItemCount <= 0)
            return;

        var headIndex = Mathf.FloorToInt(m_ScrollOffset / fixedItemWidth);

        var endHeadIndex = m_ItemSourceCount - 1;
        var endScrollOffset = endHeadIndex * fixedItemWidth;

        if (!wrap)
            headIndex = Mathf.Clamp(headIndex, 0, endHeadIndex);

        float layoutOffset;
        int paddingUnit;

        if (!wrap && m_ScrollOffset < 0)
        {
            layoutOffset = m_ScrollOffset;
            paddingUnit = 0;
        }
        else if (!wrap && m_ScrollOffset > endScrollOffset)
        {
            paddingUnit = RepeatInt(endHeadIndex, m_ItemCount);

            var endLayoutOffset = paddingUnit * fixedItemWidth;
            var overScroll = m_ScrollOffset - endScrollOffset;
            layoutOffset = endLayoutOffset + overScroll;
        }
        else
        {
            layoutOffset = Mathf.Repeat(m_ScrollOffset, m_ItemCount * fixedItemWidth);
            paddingUnit = RepeatInt(headIndex, m_ItemCount);
        }

        //Debug.Log($"PositionAllItems m_ScrollOffset: {m_ScrollOffset}, layoutOffset: {layoutOffset}");

        if (center)
            layoutOffset -= m_CenterOffset;

        foreach (var item in m_ItemStates.Keys)
        {
            item.style.translate = new Translate(-layoutOffset, 0);
        }
        m_Container.style.paddingLeft = paddingUnit * fixedItemWidth;

        //Debug.Log($"PositionAllItems m_ScrollOffset:{m_ScrollOffset} layoutOffset:{layoutOffset} targetIndex:{targetIndex} paddingUnit:{paddingUnit}");

        var headIndexWithOffset = headIndex - offsetItemCount;
        var headItemIndex = m_ItemStates[ItemAt(0)].virtualIndex;
        if (headIndexWithOffset != headItemIndex)
        {
            var indexDiff = headIndexWithOffset - headItemIndex;

            if (Mathf.Abs(indexDiff) >= m_ItemCount)
            {
                //Debug.Log($"indexDiff: {indexDiff}, targetIndex: {headIndex}, headItemIndex: {headItemIndex}, m_ScrollOffset: {m_ScrollOffset}");

                // 全件バインドし直し（順序維持のため BringToFront/SendToBack は不要）
                // ツリー上の順序はそのままに、各スロットへ正しい virtualIndex をバインド
                for (int i = 0; i < m_ItemCount; i++)
                {
                    var virtualIndex = headIndexWithOffset + i;
                    UpdateItem(ItemAt(i), virtualIndex);
                }
            }
            else if (indexDiff > 0)
            {
                for (int i = 0; i < indexDiff; i++)
                {
                    var item = ItemAt(0);
                    item.BringToFront();

                    var newVirtualIndex = headItemIndex + m_ItemCount + i;
                    UpdateItem(item, newVirtualIndex);
                }
            }
            else
            {
                for (int i = 0; i < -indexDiff; i++)
                {
                    var item = ItemAt(m_ItemCount - 1);
                    item.SendToBack();

                    var newVirtualIndex = headItemIndex - 1 - i;
                    UpdateItem(item, newVirtualIndex);
                }
            }

            //Debug.Log($"HeadItemIndex old:{headItemIndex} new:{headIndexWithOffset}");
        }
    }

    // 仮想座標指定でのスクロール
    // value の更新、アニメーションも含む
    private void ScrollTo(float targetOffset)
    {
        // アイテム単位の位置に丸める
        var targetIndex = Mathf.RoundToInt(targetOffset / fixedItemWidth);

        if (!wrap)
            targetIndex = Mathf.Clamp(targetIndex, 0, m_ItemSourceCount - 1);

        // value更新
        var previous = m_Value;
        SetValueWithoutNotify(targetIndex);

        var snappedOffset = targetIndex * fixedItemWidth;
        // アニメ後に選択状態の更新が呼ばれる
        StartScrollAnimation(snappedOffset);

        // イベント
        if (previous != m_Value)
            NotifyValueChanged(previous, m_Value);
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

        if (t < 1)
        {
            var easedT = Easing.OutCubic(t);
            m_ScrollOffset = Mathf.Lerp(m_AnimStartOffset, m_AnimTargetOffset, easedT);
            PositionAllItems();
        }
        else
        {
            m_ScrollOffset = m_AnimTargetOffset;
            PositionAllItems();

            UpdateItemSelected(true);

            m_ScrollAnimation.Pause();
        }
    }

    public void SetValueWithoutNotify(int newValue)
    {
        m_Value = m_ItemSourceCount > 0 ?
            RepeatInt(newValue, m_ItemSourceCount) : -1;

        //Debug.Log($"SetValueWithoutNotify m_ItemSourceCount:{m_ItemSourceCount} value:{m_Value}");
    }

    private void NotifyValueChanged(int previousValue, int newValue)
    {
        using (var evt = ChangeEvent<int>.GetPooled(previousValue, newValue))
        {
            evt.target = this;
            SendEvent(evt);
        }

        NotifyPropertyChanged(in valueProperty);
    }

    private void UpdateItemSelected(bool selected)
    {
        foreach (var (item, itemState) in m_ItemStates)
        {
            if (selected && itemState.srcIndex == m_Value)
                item.AddToClassList(selectedUssClassName);
            else
                item.RemoveFromClassList(selectedUssClassName);
        }
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
