using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/BookContent", order = 2)]
public class BookContent : ScriptableObject
{
    public string bookName;

    public List<BookPage> bookPages = new List<BookPage>();
}