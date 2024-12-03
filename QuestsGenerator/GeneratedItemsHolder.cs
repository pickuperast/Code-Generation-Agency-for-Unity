using System.Collections.Generic;
using UnityEngine;

//[CreateAssetMenu(fileName = "GeneratedItemsHolder", menuName = "ScriptableObjects/GeneratedItemsHolder", order = 1)]
public class GeneratedItemsHolder : ScriptableObject
{
    public List<GeneratedItemDefinition> GeneratedItems => _generatedItems;
    [SerializeField] private List<GeneratedItemDefinition> _generatedItems = new ();
}