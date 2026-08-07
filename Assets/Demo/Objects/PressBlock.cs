using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PressBlock : MonoBehaviour
{
    public Image[] blocks;
    public Text pressText;
    
    public UnityAction ButtonAction;

    public void Click()
    {
        ButtonAction?.Invoke();
    }

    public void SetColor(Color color)
    {
        for (int i = 0; i < blocks.Length; i++)
        {
            blocks[i].color = color;
        }
    }
}