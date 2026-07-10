using UnityEngine;

/// <summary>uGUI half: instantiates the assigned prefab under a full-rect container. Concretes only bind the text component and its property setters.</summary>
public abstract class UGuiTextBenchmarkBase : TextBenchmarkBase<Component>
{
    [Header("Prefab")]
    public GameObject prefab;

    protected Transform container;

    protected abstract Component GetInstanceComponent(GameObject go);

    protected override bool ValidateSetup()
    {
        if (prefab != null) return true;
        Debug.LogError("Please assign prefab!");
        return false;
    }

    protected override void SetupContainer()
    {
        var go = new GameObject($"{SystemName}BenchmarkContainer", typeof(RectTransform));
        container = go.transform;
        container.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.localScale = Vector3.one;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    protected override void TeardownContainer()
    {
        if (container != null)
            Destroy(container.gameObject);
    }

    protected override Component CreateInstance(int index)
    {
        var go = Instantiate(prefab, container);
        go.SetActive(true);
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(10, -10 - index * 50);
        }
        return GetInstanceComponent(go);
    }

    protected override void SetInspectionName(Component instance, int index, BenchmarkInspectionPhase phase)
    {
        if (instance != null)
            instance.gameObject.name = $"{SystemName}_{phase}_{index:000}";
    }

    protected override int CountInspectionObjects() => container != null ? container.childCount : 0;

    protected override string DescribeInspectionInstance(Component instance, int index)
    {
        if (instance == null) return "null";
        var rt = instance.transform as RectTransform;
        var rect = rt != null ? $"{rt.rect.width:F1}x{rt.rect.height:F1}" : "no RectTransform";
        var position = rt != null ? rt.anchoredPosition.ToString("F1") : "n/a";
        return $"{instance.gameObject.name}: component={instance.GetType().Name}, active={instance.gameObject.activeInHierarchy}, rect={rect}, anchored={position}";
    }

    protected override void DestroyInstance(Component instance)
    {
        if (instance != null)
            Destroy(instance.gameObject);
    }

    protected override void SetRectSize(Component instance, float width, float height)
    {
        var rt = (RectTransform)instance.transform;
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }
}
