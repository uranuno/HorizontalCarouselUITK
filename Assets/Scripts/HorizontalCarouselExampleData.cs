using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

[CreateAssetMenu]
public class HorizontalCarouselExampleData : ScriptableObject, INotifyBindablePropertyChanged
{
    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

    [SerializeField]
    private List<CardData> m_Cards = new();

    [CreateProperty(ReadOnly = true)]
    public List<CardData> cards => m_Cards;

    [CreateProperty(ReadOnly = true)]
    public int count => m_Cards?.Count ?? 0;

    [SerializeField]
    private int m_Selected;

    [CreateProperty]
    public int selected
    {
        get => m_Selected;
        set
        {
            if(m_Selected == value)
                return;

            //Debug.Log($"Example Data updated! previous: {m_Selected} m_Value: {value}");

            m_Selected = value;
            Notify();
        }
    }

    private void Notify([CallerMemberName] string propertyName = "")
    {
        propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(propertyName));
    }
}
