using System.Collections.Generic;
using UnityEngine;

public static class EnemyConfigLoader
{
    private static bool _loaded = false;
    private static Dictionary<int, EnemyConfig> _byId;

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        _byId = new Dictionary<int, EnemyConfig>();

        TextAsset jsonAsset = Resources.Load<TextAsset>("Enemy");
        if (jsonAsset == null)
        {
            return;
        }

        EnemyConfig[] configs = JsonArrayHelper.FromJson<EnemyConfig>(jsonAsset.text);
        if (configs == null)
        {
            return;
        }

        foreach (var cfg in configs)
        {
            if (cfg == null) continue;

            if (_byId.ContainsKey(cfg.id))
            {
                continue;
            }

            _byId.Add(cfg.id, cfg);
        }
    }
    public static EnemyConfig GetEnemy(int id)
    {
        EnsureLoaded();

        if (_byId == null)  return null;

        if (_byId.TryGetValue(id, out var cfg))
            return cfg;

        Debug.LogWarning($"[EnemyConfigLoader] Enemy id {id} 를 찾지 못했습니다.");
        return null;
    }
}
