using System.Collections.Generic;
using UnityEngine;

public class AmmoPickupSpawnPoint : MonoBehaviour
{
    private static readonly List<AmmoPickupSpawnPoint> s_SpawnPoints = new List<AmmoPickupSpawnPoint>();

    public static int Count
    {
        get
        {
            CleanupNulls();
            return s_SpawnPoints.Count;
        }
    }

    private void OnEnable()
    {
        CleanupNulls();
        if (!s_SpawnPoints.Contains(this))
        {
            s_SpawnPoints.Add(this);
            s_SpawnPoints.Sort(ComparePoints);
        }
    }

    private void OnDisable()
    {
        s_SpawnPoints.Remove(this);
    }

    public static AmmoPickupSpawnPoint GetByIndex(int index)
    {
        CleanupNulls();
        if (index < 0 || index >= s_SpawnPoints.Count)
        {
            return null;
        }

        return s_SpawnPoints[index];
    }

    private static void CleanupNulls()
    {
        s_SpawnPoints.RemoveAll(point => point == null);
    }

    private static int ComparePoints(AmmoPickupSpawnPoint left, AmmoPickupSpawnPoint right)
    {
        int nameComparison = string.CompareOrdinal(left.name, right.name);
        return nameComparison != 0
            ? nameComparison
            : left.GetInstanceID().CompareTo(right.GetInstanceID());
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.78f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.75f);
    }
}
