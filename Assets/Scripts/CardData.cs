using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

[CreateAssetMenu]
public class CardData : ScriptableObject
{
    [CreateProperty]
    public string label;

    [CreateProperty]
    public Texture2D image;

    [CreateProperty]
    public MaterialDefinition materialDefinition;
}
