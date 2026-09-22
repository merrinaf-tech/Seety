using Game.Notifications;
using Game.Prefabs;
using Game.Tools;
using Seety.Notifications;
using Seety.Vitals;
using Unity.Entities;
using Unity.Mathematics;

static void Check(bool ok, string message)
{
    if (!ok) throw new Exception(message);
}

// Two spots on the same street exchange congestion ranking. A click already in flight must
// still go to its original spot, and the displayed suffix must not follow the ranking either.
var entities = new EntityManager();
var street = entities.Create();
var road = entities.Create();
entities.AddComponent<Game.Net.Edge>(road);
entities.Set(road, new Game.Net.Aggregated { m_Aggregate = street });
var lane = entities.Create();
entities.Set(lane, new Game.Common.Owner { m_Owner = road });
var names = new Game.UI.NameSystem();
names.Names[street] = "Sunset Street";
var vehicles = new List<Entity>();
void Crowd(int count, float x)
{
    for (int i = 0; i < count; i++)
    {
        var car = entities.Create();
        entities.Set(car, new Game.Objects.Transform { m_Position = new float3(x, 0, 0) });
        entities.Set(car, new Game.Vehicles.CarCurrentLane { m_Lane = lane });
        vehicles.Add(car);
    }
}
var query = new EntityQuery(() => vehicles);
var jams = new TrafficJamBreakdown();
Crowd(20, -160); Crowd(10, 320);
jams.Refresh(query, entities, names, null);
Check(jams.Groups.Count == 2, "two distinct queues");
var west = jams.Groups.Single(jam => jam.Position.x < 0);
var east = jams.Groups.Single(jam => jam.Position.x > 0);
Check(west.Id != east.Id, "distinct location IDs");
vehicles.Clear(); Crowd(6, -160); Crowd(25, 320); vehicles.Reverse();
jams.Refresh(query, entities, names, null);
Check(jams.Groups[0].Id == east.Id, "list still sorts worst first");
Check(jams.Groups[1].Id == west.Id, "identity survives reordered input and ranking");
Check(jams.Groups[0].Name == east.Name && jams.Groups[1].Name == west.Name, "labels stay with their locations");
var camera = new Game.Rendering.CameraUpdateSystem();
Check(jams.Jump(west.Id, camera) && camera.activeCameraController.pivot.x == -160, "old click goes west");
names.Names[street] = "Renamed Street";
jams.Refresh(query, entities, names, null);
Check(jams.Jump(east.Id, camera) && camera.activeCameraController.pivot.x == 320, "street rename preserves identity");
vehicles.Clear(); Crowd(12, 320);
jams.Refresh(query, entities, names, null);
Check(!jams.Jump(west.Id, camera), "disappeared queue does not redirect to surviving queue");
Check(!jams.Jump("Renamed Street", camera), "labels are not action identifiers");
vehicles.Clear(); Crowd(5, 1); Crowd(5, 33);
jams.Refresh(query, entities, names, null);
var tiedId = jams.Groups[0].Id;
vehicles.Reverse(); jams.Refresh(query, entities, names, null);
Check(jams.Groups.Count == 1 && jams.Groups[0].Id == tiedId, "tied neighbouring cells have deterministic identity");
vehicles.Clear(); jams.Refresh(query, entities, names, null);
Check(jams.Groups.Count == 0 && !jams.Jump(tiedId, camera), "empty query clears stale targets");
Console.WriteLine("PASS: traffic identity, ranking, renaming, stale clicks and tied cells");

var world = new EntityManager();
Entity Prefab(bool enabled)
{
    var e = world.Create(); world.AddComponent<NotificationIconDisplayData>(e);
    world.SetComponentEnabled<NotificationIconDisplayData>(e, enabled); return e;
}
Entity Notification(bool hidden)
{
    var e = world.Create(); world.AddComponent<Icon>(e);
    if (hidden) world.AddComponent<Hidden>(e); return e;
}
var enabledPrefab = Prefab(true);
var disabledPrefab = Prefab(false);
var visibleIcon = Notification(false);
var alreadyHiddenIcon = Notification(true);
var visibility = new NotificationIconVisibility(world,
    new EntityQuery(() => world.All.Where(e => world.HasComponent<NotificationIconDisplayData>(e))),
    new EntityQuery(() => world.All.Where(e => world.HasComponent<Icon>(e) && !world.HasComponent<Hidden>(e))));
visibility.Set(true); visibility.Set(true);
Check(world.HasComponent<Hidden>(visibleIcon), "visible icon hidden");
Check(!world.IsComponentEnabled<NotificationIconDisplayData>(enabledPrefab), "enabled prefab disabled");
var newIcon = Notification(false);
var newPrefab = Prefab(true);
visibility.KeepUp();
Check(world.HasComponent<Hidden>(newIcon), "new icon caught while hidden");
Check(!world.IsComponentEnabled<NotificationIconDisplayData>(newPrefab), "new prefab caught while hidden");
var recycled = world.Recycle(visibleIcon);
world.AddComponent<Icon>(recycled); world.AddComponent<Hidden>(recycled);
world.FailNextRemoval = true;
visibility.Set(false);
Check(!visibility.Hidden, "requested visible state retained after partial restore");
visibility.KeepUp();
Check(world.HasComponent<Hidden>(alreadyHiddenIcon), "pre-existing hidden icon preserved");
Check(world.HasComponent<Hidden>(recycled), "recycled entity is not owned");
Check(!world.HasComponent<Hidden>(newIcon), "partial restoration retried");
Check(world.IsComponentEnabled<NotificationIconDisplayData>(enabledPrefab), "owned prefab restored");
Check(world.IsComponentEnabled<NotificationIconDisplayData>(newPrefab), "new owned prefab restored");
Check(!world.IsComponentEnabled<NotificationIconDisplayData>(disabledPrefab), "pre-disabled prefab preserved");
visibility.Set(true); visibility.Restore(); visibility.Restore();
Check(!world.HasComponent<Hidden>(newIcon) && world.HasComponent<Hidden>(alreadyHiddenIcon), "unload and repeated restore preserve ownership");
Console.WriteLine("PASS: notification ownership, new icons, entity recycling, retry and unload");

JourneyTests.Run();
