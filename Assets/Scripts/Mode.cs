using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Items/Mode")]
public class Mode : ScriptableObject {

    [SerializeField] string modeName = default;
    public string ModeName { get { return modeName; } }
    [SerializeField] string buttonText = default;
    public string ButtonText { get { return buttonText; } }
    [SerializeField] [TextArea(2, 5)] string description = default;
    public string Description { get { return description; } }

}