using System.Collections.Generic;
using UnityEngine;

public static class SliceVisual
{
    public static event System.Action<GameObject> ToppingCollected;

    static GameObject pepperoniModel;
    static readonly List<GameObject> toppings = new();
    static Transform templateHolder;

    const float HalfWidthPerLength = 0.336f;
    const float CheeseTop = 0.022f;
    const float PepperoniSize = 0.16f;
    const float ToppingSize = 0.2f;

    public const float ToppingChance = 0.55f;
    static readonly float[] pepperoniBands = { 0.34f, 0.5f, 0.64f };

    public static void Reset(GameObject pepperoni)
    {
        pepperoniModel = pepperoni;
        toppings.Clear();
        ToppingCollected = null;
        templateHolder = null;
    }

    public static void CollectTopping(GameObject visual)
    {
        if (visual == null) return;
        if (templateHolder == null) templateHolder = new GameObject("Topping Templates").transform;
        GameObject template = Object.Instantiate(visual, templateHolder);
        template.name = visual.name;
        template.SetActive(false);
        toppings.Add(template);
        ToppingCollected?.Invoke(template);
    }

    public static Transform Create(GameObject model, Transform parent, float length, bool tipAtMinX)
    {
        var root = new GameObject("Slice").transform;
        var align = new GameObject("Align").transform;
        align.SetParent(root, false);
        GameObject instance = Object.Instantiate(model, align, false);

        if (TryBounds(instance, out Bounds b))
        {
            var tip = new Vector3(tipAtMinX ? b.min.x : b.max.x, b.min.y, b.center.z);
            instance.transform.position -= tip;

            align.localRotation = Quaternion.FromToRotation(tipAtMinX ? Vector3.right : Vector3.left, Vector3.forward);
            align.localScale = Vector3.one * (length / Mathf.Max(0.0001f, b.size.x));
        }

        if (pepperoniModel != null)
        {
            int count = Random.Range(1, 4), first = Random.Range(0, 3);
            for (int i = 0; i < count; i++)
            {
                float start = pepperoniBands[(first + i) % 3];
                Place(root, pepperoniModel, length, PepperoniSize, Random.Range(start, start + 0.08f), true);
            }
        }

        foreach (GameObject topping in toppings)
        {
            if (Random.value > ToppingChance) continue;
            AddTopping(root, topping, length);
            if (Random.value < 0.15f) AddTopping(root, topping, length);
        }

        root.SetParent(parent, false);
        return root;
    }

    public static Transform AddTopping(Transform slice, GameObject template, float length)
    {
        return Place(slice, template, length, ToppingSize, Random.Range(0.28f, 0.76f), false);
    }

    public static GameObject FindTopping(string nameContains)
    {
        foreach (GameObject t in toppings)
            if (t != null && t.name.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) >= 0) return t;
        return null;
    }

    public static Transform CreateLoose(GameObject template, Transform parent, float size)
    {
        var holder = new GameObject(template.name).transform;
        GameObject instance = Object.Instantiate(template, holder, false);
        instance.SetActive(true);
        if (TryBounds(instance, out Bounds b))
        {
            instance.transform.position -= b.center;
            holder.localScale = Vector3.one * (size / Mathf.Max(0.0001f, Mathf.Max(b.size.x, b.size.y, b.size.z)));
        }
        holder.SetParent(parent, false);
        return holder;
    }

    static Transform Place(Transform slice, GameObject model, float length, float sizeFraction, float alongFraction, bool flat)
    {
        if (model == null) return null;

        var holder = new GameObject(model.name).transform;
        GameObject instance = Object.Instantiate(model, holder, false);
        instance.SetActive(true);

        float size = sizeFraction * length;
        if (TryBounds(instance, out Bounds b))
        {
            instance.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
            float largest = flat ? Mathf.Max(b.size.x, b.size.z) : Mathf.Max(b.size.x, b.size.y, b.size.z);
            holder.localScale = Vector3.one * (size / Mathf.Max(0.0001f, largest));
        }

        holder.SetParent(slice, false);
        float along = alongFraction * length;
        float room = Mathf.Max(0f, along * HalfWidthPerLength - size * 0.6f);
        holder.localPosition = new Vector3(Random.Range(-room, room), CheeseTop * length, along);
        holder.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        return holder;
    }

    static bool TryBounds(GameObject go, out Bounds bounds)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        bounds = default;
        if (renderers.Length == 0) return false;
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return true;
    }
}
