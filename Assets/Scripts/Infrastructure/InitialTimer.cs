// csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InitialTimer : MonoBehaviour
{
    [SerializeField] private int startSeconds = 30;
    [SerializeField] private bool autoStart = true;

    [Header("Portals")]
    [SerializeField] private GameObject[] portalPrefabs;
    [SerializeField] private Transform[] portalSpawnPoints;

    [Header("Clock renderers")]
    [SerializeField] private Renderer minutesRenderer;
    [SerializeField] private Renderer secondsRenderer;
    [SerializeField] private Renderer centiRenderer;
    [SerializeField] private Renderer milliRenderer;

    [Header("Number materials")]
    [SerializeField] private Material[] numberedMaterials; // can contain "88", "59", "01"
    [SerializeField] private Material fallback59; // used for values <= 59 when exact missing
    [SerializeField] private Material fallback01; // used for 0/1 fallback
    [SerializeField] private Material fallback88; // generic fallback

    private float remainingSeconds;
    private Dictionary<string, Material> materialLookup;

    void Awake()
    {
        BuildMaterialLookup();
    }

    void Start()
    {
        remainingSeconds = Mathf.Max(0, startSeconds);
        UpdateClockDisplay(remainingSeconds);
        if (autoStart) StartCoroutine(CountdownCoroutine());
    }

    void BuildMaterialLookup()
    {
        materialLookup = new Dictionary<string, Material>();
        if (numberedMaterials == null) return;
        foreach (var m in numberedMaterials)
        {
            if (m == null) continue;
            // add by full name and last two characters (common case for "01","59","88")
            string nameKey = m.name;
            if (!materialLookup.ContainsKey(nameKey)) materialLookup.Add(nameKey, m);

            if (nameKey.Length >= 2)
            {
                string last2 = nameKey.Substring(nameKey.Length - 2);
                if (!materialLookup.ContainsKey(last2)) materialLookup.Add(last2, m);
            }
        }
    }

    IEnumerator CountdownCoroutine()
    {
        UpdateClockDisplay(remainingSeconds);

        while (remainingSeconds > 0f)
        {
            yield return new WaitForSeconds(1f);
            remainingSeconds = Mathf.Max(0f, remainingSeconds - 1f);
            UpdateClockDisplay(remainingSeconds);
        }

        OnTimerFinished();
    }

    void UpdateClockDisplay(float secondsRemaining)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(secondsRemaining));
        int mins = totalSeconds / 60;
        int secs = totalSeconds % 60;

        // coarse centiseconds/milliseconds for this implementation
        int centis = 0;
        int millis = 0;

        SetRendererMaterial(minutesRenderer, mins);
        SetRendererMaterial(secondsRenderer, secs);
        SetRendererMaterial(centiRenderer, centis);
        SetRendererMaterial(milliRenderer, millis);
    }

    void SetRendererMaterial(Renderer r, int value)
    {
        if (r == null) return;
        var mat = GetMaterialForTwoDigit(value);
        if (mat != null)
        {
            r.material = mat;
        }
    }

    Material GetMaterialForTwoDigit(int value)
    {
        string key = value.ToString("D2");
        if (materialLookup != null && materialLookup.TryGetValue(key, out var exact)) return exact;

        // targeted fallbacks
        if (value <= 59 && fallback59 != null) return fallback59;
        if (value <= 1 && fallback01 != null) return fallback01;
        if (fallback88 != null) return fallback88;

        // last resort: any provided numbered material
        if (numberedMaterials != null && numberedMaterials.Length > 0) return numberedMaterials[0];

        return null;
    }

    void OnTimerFinished()
    {
        if (portalPrefabs == null || portalSpawnPoints == null) return;
        int count = Mathf.Min(portalPrefabs.Length, portalSpawnPoints.Length);
        for (int i = 0; i < count; i++)
        {
            var prefab = portalPrefabs[i];
            var spawn = portalSpawnPoints[i];
            if (prefab == null || spawn == null) continue;
            Instantiate(prefab, spawn.position, spawn.rotation);
        }
    }

    public void StartCountdown()
    {
        StopAllCoroutines();
        remainingSeconds = Mathf.Max(0, startSeconds);
        StartCoroutine(CountdownCoroutine());
    }
}
