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
Crowd(20, -160); Crowd(15, 320);
jams.Refresh(query, entities, names, null);
Check(jams.Groups.Count == 2, "two distinct queues");
var west = jams.Groups.Single(jam => jam.Position.x < 0);
var east = jams.Groups.Single(jam => jam.Position.x > 0);
Check(west.Id != east.Id, "distinct location IDs");
vehicles.Clear(); Crowd(16, -160); Crowd(25, 320); vehicles.Reverse();
jams.Refresh(query, entities, names, null);
Check(jams.Groups[0].Id == east.Id, "list still sorts worst first");
Check(jams.Groups[1].Id == west.Id, "identity survives reordered input and ranking");
Check(jams.Groups[0].Name == east.Name && jams.Groups[1].Name == west.Name, "labels stay with their locations");
var camera = new Game.Rendering.CameraUpdateSystem();
Check(jams.Jump(west.Id, camera) && camera.activeCameraController.pivot.x == -160, "old click goes west");
names.Names[street] = "Renamed Street";
jams.Refresh(query, entities, names, null);
Check(jams.Jump(east.Id, camera) && camera.activeCameraController.pivot.x == 320, "street rename preserves identity");
vehicles.Clear(); Crowd(15, 320);
jams.Refresh(query, entities, names, null);
Check(!jams.Jump(west.Id, camera), "disappeared queue does not redirect to surviving queue");
Check(!jams.Jump("Renamed Street", camera), "labels are not action identifiers");
vehicles.Clear(); Crowd(8, 1); Crowd(8, 33);
jams.Refresh(query, entities, names, null);
var tiedId = jams.Groups[0].Id;
vehicles.Reverse(); jams.Refresh(query, entities, names, null);
Check(jams.Groups.Count == 1 && jams.Groups[0].Id == tiedId, "tied neighbouring cells have deterministic identity");
vehicles.Clear(); jams.Refresh(query, entities, names, null);
Check(jams.Groups.Count == 0 && !jams.Jump(tiedId, camera), "empty query clears stale targets");
Console.WriteLine("PASS: traffic identity, ranking, renaming, stale clicks and tied cells");

// The cutoff is local and absolute: a city can have many blocked cars without any jam worth a
// row. Test the production grouping, including disappearance and invalidation of old clicks.
vehicles.Clear();
for (int i = 0; i < 12; i++) Crowd(14, i * 320);
jams.Refresh(query, entities, names, null);
Check(jams.Groups.Count == 0, "many separate minor queues do not fill the list");
vehicles.Clear(); Crowd(15, 320);
jams.Refresh(query, entities, names, null);
Check(jams.Groups.Count == 1 && jams.Groups[0].Count == 15, "fifteen blocked vehicles meet the threshold");
var thresholdId = jams.Groups[0].Id;
vehicles.RemoveAt(vehicles.Count - 1);
jams.Refresh(query, entities, names, null);
Check(jams.Groups.Count == 0, "a queue falling below fifteen disappears immediately");
Check(!jams.Jump(thresholdId, camera), "a subthreshold queue has no stale camera target");
vehicles.Clear(); Crowd(14, -320); Crowd(20, 320);
jams.Refresh(query, entities, names, null);
Check(jams.Groups.Count == 1 && jams.Groups[0].Count == 20 && jams.Groups[0].Position.x == 320,
    "a real queue is listed without padding with smaller queues");
vehicles.Clear(); Crowd(7, 1); Crowd(8, 33);
jams.Refresh(query, entities, names, null);
Check(jams.Groups.Count == 1 && jams.Groups[0].Count == 15,
    "a queue across neighbouring cells reaches the threshold together");
vehicles.Clear(); Crowd(7, 1); Crowd(8, 320);
jams.Refresh(query, entities, names, null);
Check(jams.Groups.Count == 0, "distant queues cannot combine to reach the threshold");
Console.WriteLine("PASS: traffic minimum size, empty lists, disappearing queues and local grouping");

// A train is one entity per carriage, each pointing at the lead one. The list once showed a
// single moving seven-carriage train as "7": every carriage was counted, and a carriage without
// Moving was taken to be standing.
var transitWorld = new EntityManager();
var transitNames = new Game.UI.NameSystem();
var redLine = transitWorld.Create();
transitNames.Names[redLine] = "Red Line";
var transitVehicles = new List<Entity>();
var transitQuery = new EntityQuery(() => transitVehicles);
var transit = new StoppedTransitList();
Entity Train(int carriages, float speed, float x,
    Game.Vehicles.PublicTransportFlags state = Game.Vehicles.PublicTransportFlags.None)
{
    Entity lead = default;
    for (int i = 0; i < carriages; i++)
    {
        var car = transitWorld.Create();
        if (i == 0) lead = car;
        transitWorld.Set(car, new Game.Vehicles.PublicTransport { m_State = state });
        transitWorld.AddComponent<Game.Vehicles.Train>(car);
        transitWorld.Set(car, new Game.Vehicles.Controller { m_Controller = lead });
        transitWorld.Set(car, new Game.Vehicles.TrainNavigation { m_Speed = speed });
        transitWorld.Set(car, new Game.Routes.CurrentRoute { m_Route = redLine });
        transitWorld.Set(car, new Game.Objects.Transform { m_Position = new float3(x + i * 12, 0, 0) });
        transitVehicles.Add(car);
    }
    return lead;
}
Train(7, 14f, 0);
transit.Refresh(transitQuery, transitWorld, transitNames);
Check(transit.Groups.Count == 0, "a moving seven-carriage train is not listed");
transitVehicles.Clear(); Train(7, 0f, 0);
transit.Refresh(transitQuery, transitWorld, transitNames);
Check(transit.Groups.Count == 1 && transit.Groups[0].Count == 1, "a stopped train counts once, not once per carriage");
transitVehicles.Reverse();
transit.Refresh(transitQuery, transitWorld, transitNames);
Check(transit.Groups[0].Count == 1 && transit.Groups[0].Position.x == 0, "carriage order does not change the count or the target");
transitVehicles.Clear(); Train(7, 0f, 0); Train(4, 0f, 500); Train(5, 20f, 900);
transit.Refresh(transitQuery, transitWorld, transitNames);
Check(transit.Groups.Count == 1 && transit.Groups[0].Count == 2, "two stopped trains on a line count as two");
transitVehicles.Clear(); Train(6, 0f, 0, Game.Vehicles.PublicTransportFlags.Boarding);
transit.Refresh(transitQuery, transitWorld, transitNames);
Check(transit.Groups.Count == 0, "a train boarding at a stop is not listed");
transitVehicles.Clear();
var mystery = transitWorld.Create();
transitWorld.Set(mystery, new Game.Vehicles.PublicTransport());
transitWorld.AddComponent<Game.Vehicles.Train>(mystery);
transitWorld.Set(mystery, new Game.Routes.CurrentRoute { m_Route = redLine });
transitVehicles.Add(mystery);
transit.Refresh(transitQuery, transitWorld, transitNames);
Check(transit.Groups.Count == 0, "a rail vehicle with no speed to read is not assumed stopped");
transitVehicles.Clear();
var bus = transitWorld.Create();
transitWorld.Set(bus, new Game.Vehicles.PublicTransport());
transitWorld.AddComponent<Game.Vehicles.Blocker>(bus);
transitWorld.Set(bus, new Game.Routes.CurrentRoute { m_Route = redLine });
transitVehicles.Add(bus);
var freeBus = transitWorld.Create();
transitWorld.Set(freeBus, new Game.Vehicles.PublicTransport());
transitWorld.Set(freeBus, new Game.Routes.CurrentRoute { m_Route = redLine });
transitVehicles.Add(freeBus);
transit.Refresh(transitQuery, transitWorld, transitNames);
Check(transit.Groups.Count == 1 && transit.Groups[0].Count == 1, "a blocked bus counts, a free one does not");
Console.WriteLine("PASS: transit trains counted once, speed from the train, boarding and unknown speed excluded");

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
