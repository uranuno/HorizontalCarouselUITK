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

        var horizontalCarousel = root.Q<HorizontalCarousel>();

        horizontalCarousel.itemsSource = m_CardDatas;

        // デバッグボタン
        var scrollButtons = root.Q("button-scroll").Query<Button>();
        scrollButtons.ForEach(button =>
        {
            button.clicked += () =>
            {
                var str = button.text;
                try
                {
                    var scrollAmount = int.Parse(str);
                    horizontalCarousel.ScrollTo(scrollAmount);
                }
                catch (FormatException e)
                {
                    Debug.LogException(e);
                }
            };
        });
    }
}
