using System.Collections.Generic;
using UnityEngine;

public class PickupSpawnPoint : MonoBehaviour
{
    private static readonly List<PickupSpawnPoint> s_SpawnPoints = new List<PickupSpawnPoint>();

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

    public static PickupSpawnPoint GetByIndex(int index)
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

    private static int ComparePoints(PickupSpawnPoint left, PickupSpawnPoint right)
    {
        int nameComparison = string.CompareOrdinal(left.name, right.name);
        return nameComparison != 0
            ? nameComparison
            : left.GetInstanceID().CompareTo(right.GetInstanceID());
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.45f, 0.95f, 0.35f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
}
