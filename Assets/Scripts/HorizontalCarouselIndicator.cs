using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class HorizontalCarouselIndicator : VisualElement, INotifyValueChanged<int>
{
    internal static readonly BindingId countProperty = nameof(count);

    internal static readonly BindingId valueProperty = nameof(value);

    public const string ussClassName = "horizontal-carousel-indicator";

    private GroupBox m_DotGroup;

    private int m_Value;

    [CreateProperty]
    public int count
    {
        get => m_DotGroup.childCount;
        set
        {
            if (value == m_DotGroup.childCount)
                return;

            Rebuild(value);

            NotifyPropertyChanged(in countProperty);
        }
    }

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
                //Debug.Log($"Indicator.value updated! previous: {previous} m_Value: {m_Value}");

                using (var evt = ChangeEvent<int>.GetPooled(previous, m_Value))
                {
                    evt.target = this;
                    SendEvent(evt);
                }

                NotifyPropertyChanged(in valueProperty);
            }
        }
    }

    public HorizontalCarouselIndicator()
    {
        AddToClassList(ussClassName);

        m_DotGroup = new GroupBox();
        hierarchy.Add(m_DotGroup);
    }

    public void Rebuild(int count)
    {
        m_DotGroup.Clear();

        for (var i = 0; i < count; i++)
        {
            var dot = new RadioButton();
            dot.RegisterValueChangedCallback(OnDotValueChanged);
            m_DotGroup.Add(dot);
        }

        SetValueWithoutNotify(0);
    }

    private void OnDotValueChanged(ChangeEvent<bool> evt)
    {
        if (evt.newValue == false)
            return;

        if (evt.target is RadioButton dot && dot.hierarchy.parent == m_DotGroup)
        {
            value = m_DotGroup.IndexOf(dot);

            evt.StopPropagation();
        }
    }

    public void SetValueWithoutNotify(int newValue)
    {
        if (m_Value >= 0 && m_Value < count)
        {
            if (m_DotGroup.ElementAt(m_Value) is RadioButton dot)
                dot.SetValueWithoutNotify(false);
        }

        m_Value = (newValue >= 0 && newValue < count) ?
            newValue : -1;
        //Debug.Log($"SetValueWithoutNotify m_Value: {m_Value}");

        if (m_Value >= 0)
        {
            if (m_DotGroup.ElementAt(m_Value) is RadioButton dot)
                dot.SetValueWithoutNotify(true);
        }
    }
}
