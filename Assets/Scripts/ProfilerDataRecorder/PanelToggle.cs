using UnityEngine;
using UnityEngine.UI;

public class PanelToggle : MonoBehaviour
{
    public GameObject panel;

    public void TogglePanel()
    {
        panel.SetActive(!panel.activeSelf);
    }
}