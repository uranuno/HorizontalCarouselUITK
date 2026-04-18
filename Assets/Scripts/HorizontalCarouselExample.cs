using UnityEngine;
using UnityEngine.UIElements;

public class HorizontalCarouselExample : MonoBehaviour
{
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
