using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Makes the rig's 2D lights. A light made in code affects no sorting layer until told to, so each one is given all of
// them (the background layers, and the level).
public static class Light2DFactory {
    public static Light2D Create(string name, Transform parent, Light2D.LightType type) {
        GameObject host = new GameObject(name);
        host.transform.SetParent(parent, false);
        Light2D light = host.AddComponent<Light2D>();
        light.lightType = type;
        light.targetSortingLayers = AllSortingLayers();
        return light;
    }

    public static int[] AllSortingLayers() {
        List<int> layers = new List<int>();
        foreach (SortingLayer layer in SortingLayer.layers) {
            layers.Add(layer.id);
        }
        return layers.ToArray();
    }
}
