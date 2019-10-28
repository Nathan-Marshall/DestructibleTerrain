using Unity.Profiling;

public static class DTProfileMarkers
{
    public static readonly string Namespace = "DestructibleTerrain";
        
    public static readonly string TriangulationStr = Namespace + ".Triangulation";
    public static readonly ProfilerMarker Triangulation = new ProfilerMarker(TriangulationStr);

    public static readonly string HertelMehlhornStr = Namespace + ".HertelMehlhorn";
    public static readonly ProfilerMarker HertelMehlhorn = new ProfilerMarker(HertelMehlhornStr);
}