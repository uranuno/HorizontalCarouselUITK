using Unity.Properties;
using UnityEngine;

[CreateAssetMenu]
public class CardData : ScriptableObject
{
    [CreateProperty]
    public string label;

    [CreateProperty]
    public Texture2D image;
}
