// Minimal in-memory engine surface for exercising the actual mod sources without Unity native
// libraries. The production build separately checks these calls against the game's assemblies.
namespace Unity.Collections
{
    public enum Allocator { Temp }
    public sealed class NativeArray<T> : IDisposable
    {
        private readonly T[] _items;
        public NativeArray(IEnumerable<T> items) { _items = items.ToArray(); }
        public int Length => _items.Length;
        public T this[int index] => _items[index];
        public void Dispose() { }
    }
}
namespace Unity.Entities
{
    public readonly record struct Entity(int Index, int Version = 1)
    {
        public static Entity Null => default;
    }
    public sealed class EntityQuery
    {
        private readonly Func<IEnumerable<Entity>> _read;
        public EntityQuery(Func<IEnumerable<Entity>> read) { _read = read; }
        public bool IsEmptyIgnoreFilter => !_read().Any();
        public Unity.Collections.NativeArray<Entity> ToEntityArray(Unity.Collections.Allocator _) => new(_read());
    }
    public sealed class DynamicBuffer<T> : List<T> { public int Length => Count; }
    public sealed class EntityManager
    {
        private readonly Dictionary<Entity, Dictionary<Type, object>> _data = new();
        private readonly HashSet<(Entity, Type)> _disabled = new();
        private int _next;
        public bool FailNextRemoval;
        public IEnumerable<Entity> All => _data.Keys;
        public Entity Create() { var e = new Entity(++_next); _data[e] = new(); return e; }
        public Entity Recycle(Entity old)
        {
            _data.Remove(old);
            var e = new Entity(old.Index, old.Version + 1); _data[e] = new(); return e;
        }
        public bool Exists(Entity e) => _data.ContainsKey(e);
        public bool HasComponent<T>(Entity e) => Exists(e) && _data[e].ContainsKey(typeof(T));
        public T GetComponentData<T>(Entity e) => (T)_data[e][typeof(T)];
        public void Set<T>(Entity e, T value) => _data[e][typeof(T)] = value;
        public bool HasBuffer<T>(Entity e) => HasComponent<DynamicBuffer<T>>(e);
        public DynamicBuffer<T> GetBuffer<T>(Entity e, bool readOnly = false) => GetComponentData<DynamicBuffer<T>>(e);
        public void AddComponent<T>(Entity e) where T : new() => Set(e, new T());
        public void AddComponent<T>(Unity.Collections.NativeArray<Entity> entities) where T : new()
        { for (int i = 0; i < entities.Length; i++) AddComponent<T>(entities[i]); }
        public void RemoveComponent<T>(Entity e)
        {
            if (FailNextRemoval) { FailNextRemoval = false; throw new InvalidOperationException("Injected failure"); }
            _data[e].Remove(typeof(T));
        }
        public bool IsComponentEnabled<T>(Entity e) => !_disabled.Contains((e, typeof(T)));
        public void SetComponentEnabled<T>(Entity e, bool enabled)
        {
            if (enabled) _disabled.Remove((e, typeof(T))); else _disabled.Add((e, typeof(T)));
        }
    }
}
namespace Unity.Mathematics
{
    public struct float3
    {
        public float x, y, z;
        public float3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static float3 zero => default;
        public static float3 operator +(float3 a, float3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static float3 operator /(float3 a, float b) => new(a.x / b, a.y / b, a.z / b);
    }
    public struct float2
    {
        public float x, y;
        public float2(float x, float y) { this.x = x; this.y = y; }
    }
    public static class math { public static float floor(float value) => MathF.Floor(value); }
}
// Just enough curve maths for JourneyTrace.LaneMetres. A real Bezier4x3 is four control points
// and its length is an integral; the production code never looks inside one, it only hands the
// pair to MathUtils.Length. So the stub carries the answer instead of the shape, which lets a
// test state "this lane is 120 metres long" and read a leg's total back.
namespace Colossal.Mathematics
{
    public struct Bezier4x3 { public float Length; }
    public struct Bounds1
    {
        public float min, max;
        public Bounds1(float min, float max) { this.min = min; this.max = max; }
    }
    public static class MathUtils
    {
        public static float Length(Bezier4x3 curve, Bounds1 span)
            => curve.Length * (span.max - span.min);
    }
}
namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }
    public struct Bounds { public Vector3 size, min, max; }
    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }
}
namespace Game.Rendering
{
    public sealed class CameraController { public UnityEngine.Vector3 pivot; }
    public sealed class CameraUpdateSystem { public CameraController activeCameraController = new(); }
}
namespace Game.UI
{
    public sealed class NameSystem
    {
        public readonly Dictionary<Unity.Entities.Entity, string> Names = new();
        // Counted so a test can assert how often the game is asked to build a name: the real one
        // concatenates strings per call, and a path element is a lane, not a street.
        public int Lookups;
        public string GetRenderedLabelName(Unity.Entities.Entity entity)
        {
            Lookups++;
            return Names[entity];
        }
    }
}
namespace Game.Simulation
{
    public sealed class TerrainSystem { public UnityEngine.Bounds GetTerrainBounds() => default; }
}
namespace Game.Objects { public struct Transform { public Unity.Mathematics.float3 m_Position; } }
namespace Game.Vehicles
{
    public struct Blocker { }
    public struct CarCurrentLane { public Unity.Entities.Entity m_Lane; }
}
namespace Game.Buildings { public struct Building { } }
namespace Game.Common { public struct Owner { public Unity.Entities.Entity m_Owner; } }
namespace Game.Common { public struct Target { public Unity.Entities.Entity m_Target; } }
namespace Game.Citizens
{
    public struct CurrentTransport { public Unity.Entities.Entity m_CurrentTransport; }
    public struct CurrentBuilding { public Unity.Entities.Entity m_CurrentBuilding; }
}
namespace Game.Creatures
{
    public struct CurrentVehicle { public Unity.Entities.Entity m_Vehicle; }
    public struct HumanCurrentLane { public Unity.Entities.Entity m_Lane; }
}
namespace Game.Routes
{
    public struct CurrentRoute { public Unity.Entities.Entity m_Route; }
    public struct Route { }
    public struct Waypoint { }
    public struct Color { public UnityEngine.Color32 m_Color; }
    public struct RouteNumber { public int m_Number; }
}
namespace Game.Pathfind
{
    public struct PathInformation { public Unity.Entities.Entity m_Destination; }
    public struct PathOwner { public int m_ElementIndex; }
    public struct PathElement
    {
        public Unity.Entities.Entity m_Target;
        public Unity.Mathematics.float2 m_TargetDelta;
    }
}
namespace Game.Net
{
    public struct Edge { }
    public struct Aggregated { public Unity.Entities.Entity m_Aggregate; }
    public struct Curve { public Colossal.Mathematics.Bezier4x3 m_Bezier; public float m_Length; }
}
namespace Game.Notifications { public struct Icon { } }
namespace Game.Tools { public struct Hidden { } }
namespace Game.Prefabs { public struct NotificationIconDisplayData { } }
namespace Seety
{
    public static class Mod
    {
        public static class Log { public static void Warn(string message) => Console.WriteLine(message); }
    }
}
