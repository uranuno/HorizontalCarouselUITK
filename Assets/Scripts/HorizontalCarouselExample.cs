using System;
using UnityEngine;
using UnityEngine.UIElements;

public class HorizontalCarouselExample : MonoBehaviour
{
    [SerializeField]
    private CardData[] m_CardDatas = new CardData[0];

    [SerializeField]
    private VisualTreeAsset m_ItemTemplate;

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        root.Query("group").ForEach(group =>
        {
            var horizontalCarousel = group.Q<HorizontalCarousel>();

            group.Q<Button>("prev").clicked += () =>
            {
                horizontalCarousel.ScrollBy(-1);
            };
            group.Q<Button>("next").clicked += () =>
            {
                horizontalCarousel.ScrollBy(1);
            };
        });
    }
}
