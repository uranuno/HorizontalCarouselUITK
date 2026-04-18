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
            var prevButton = group.Q<Button>("prev");
            var nextButton = group.Q<Button>("next");

            prevButton.clicked += () =>
            {
                horizontalCarousel.ScrollBy(-1);
            };
            nextButton.clicked += () =>
            {
                horizontalCarousel.ScrollBy(1);
            };

            horizontalCarousel.RegisterValueChangedCallback(evt =>
            {
                prevButton.SetEnabled(horizontalCarousel.canGoToPrevious);
                nextButton.SetEnabled(horizontalCarousel.canGoToNext);
            });
        });
    }
}
