using Unity.Profiling;

public static class DTProfileMarkers
{
    public static readonly string Namespace = "DestructibleTerrain";

    public static readonly string ApplyColliderStr = Namespace + ".ApplyCollider";
    public static readonly ProfilerMarker ApplyCollider = new ProfilerMarker(ApplyColliderStr);

    public static readonly string HertelMehlhornStr = Namespace + ".HertelMehlhorn";
    public static readonly ProfilerMarker HertelMehlhorn = new ProfilerMarker(HertelMehlhornStr);

    public static readonly string IdentifyHolesStr = Namespace + ".IdentifyHoles";
    public static readonly ProfilerMarker IdentifyHoles = new ProfilerMarker(IdentifyHolesStr);

    public static readonly string MeshToPolygroupStr = Namespace + ".MeshToPolygroup";
    public static readonly ProfilerMarker MeshToPolygroup = new ProfilerMarker(MeshToPolygroupStr);

    public static readonly string PolygroupToMeshStr = Namespace + ".PolygroupToMesh";
    public static readonly ProfilerMarker PolygroupToMesh = new ProfilerMarker(PolygroupToMeshStr);

    public static readonly string SimplifyPolygonStr = Namespace + ".SimplifyPolygon";
    public static readonly ProfilerMarker SimplifyPolygon = new ProfilerMarker(SimplifyPolygonStr);

    public static readonly string SubtractPolygroupStr = Namespace + ".SubtractPolygroup";
    public static readonly ProfilerMarker SubtractPolygroup = new ProfilerMarker(SubtractPolygroupStr);

    public static readonly string TransformationStr = Namespace + ".Transformation";
    public static readonly ProfilerMarker Transformation = new ProfilerMarker(TransformationStr);

    public static readonly string TriangulationStr = Namespace + ".Triangulation";
    public static readonly ProfilerMarker Triangulation = new ProfilerMarker(TriangulationStr);

    public static readonly string TriangleNetStr = Namespace + ".TriangleNet";
    public static readonly ProfilerMarker TriangleNet = new ProfilerMarker(TriangleNetStr);
}