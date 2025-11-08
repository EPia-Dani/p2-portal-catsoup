using UnityEngine;
using TMPro;

public class FPSDisplay : MonoBehaviour
{
    private float _deltaTime;
    private GUIStyle _style;

    private void Awake()
    {
        _style = new GUIStyle
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = 16,
            normal = { textColor = new Color(1f, 1f, 1f, 0.9f) }
        };
    }

    private void Update()
    {
        _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
    }

    private void OnGUI()
    {
        if (!enabled) return;
        int w = Screen.width, h = Screen.height;
        var rect = new Rect(10, 10, w, h * 2 / 100);
        float msec = _deltaTime * 1000.0f;
        float fps = 1.0f / _deltaTime;
        string text = $"{fps:0.} FPS  ({msec:0.0} ms)";
        GUI.Label(rect, text, _style);
    }
}



