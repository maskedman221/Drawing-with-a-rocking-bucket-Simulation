using UnityEngine;

public static class UIDashboardBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureDashboardExists()
    {
        if (Object.FindFirstObjectByType<UIDashboardBuilder>() != null ||
            Object.FindFirstObjectByType<UIManager>() != null ||
            Object.FindFirstObjectByType<SimulationController>() == null)
        {
            return;
        }

        GameObject builderObject = new GameObject("DashboardBuilder");
        UIDashboardBuilder builder = builderObject.AddComponent<UIDashboardBuilder>();
        builder.BuildDashboard();
    }
}
