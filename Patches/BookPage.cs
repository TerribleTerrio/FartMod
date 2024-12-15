using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/BookPage", order = 2)]
public class BookPage : ScriptableObject
{
    public int pageNumber;
    
    [TextArea(2, 20)]
    public string pageContent;
}