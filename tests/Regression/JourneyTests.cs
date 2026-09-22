using Seety.Vitals;
using Unity.Entities;
using Game.Pathfind;

static class JourneyTests
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    public static void Run()
    {
        var world = new EntityManager();
        var names = new Game.UI.NameSystem();
        Entity Named(string name) { var e = world.Create(); names.Names[e] = name; return e; }
        Entity Street(string name, float metres = 100f)
        {
            var street = Named(name); var edge = world.Create();
            world.Set(edge, new Game.Net.Aggregated { m_Aggregate = street });
            var lane = world.Create(); world.Set(lane, new Game.Common.Owner { m_Owner = edge });
            world.Set(lane, new Game.Net.Curve { m_Bezier = new Colossal.Mathematics.Bezier4x3 { Length = metres } });
            return lane;
        }
        // A step that uses a whole lane. Real elements use a slice, which is what makes turns
        // shorter than the lanes they cross, but a full lane keeps the arithmetic readable here.
        PathElement Step(Entity target) => new PathElement { m_Target = target, m_TargetDelta = new Unity.Mathematics.float2(0f, 1f) };
        Entity Stop(Entity line)
        {
            var stop = world.Create(); world.AddComponent<Game.Routes.Waypoint>(stop);
            world.Set(stop, new Game.Common.Owner { m_Owner = line }); return stop;
        }
        var road = Street("Main Street");
        var otherRoad = Street("Main Street");
        var line = Named("Line 1"); world.AddComponent<Game.Routes.Route>(line);
        world.Set(line, new Game.Routes.Color { m_Color = new UnityEngine.Color32(0x1E, 0x90, 0xFF, 255) });
        world.Set(line, new Game.Routes.RouteNumber { m_Number = 7 });
        var otherLine = Named("Line 1"); world.AddComponent<Game.Routes.Route>(otherLine);
        var firstStop = Stop(line); var secondStop = Stop(line); var otherStop = Stop(otherLine);
        var destination = Named("School"); world.AddComponent<Game.Buildings.Building>(destination);
        world.Set(destination, new Game.Objects.Transform { m_Position = new Unity.Mathematics.float3(5f, 0f, 7f) });
        var parking = Named("Car Drive Lane 3"); world.Set(parking, new Game.Common.Owner { m_Owner = destination });
        var body = world.Create();
        world.Set(body, new Game.Creatures.HumanCurrentLane { m_Lane = road });
        world.Set(body, new PathInformation { m_Destination = parking });
        var path = new DynamicBuffer<PathElement>();
        foreach (var target in new[] { road, road, firstStop, secondStop, otherStop, road, otherRoad })
            path.Add(Step(target));
        world.Set(body, path);
        var citizen = Named("Jane");
        world.Set(citizen, new Game.Citizens.CurrentTransport { m_CurrentTransport = body });
        var trace = new JourneyTrace(); trace.Refresh(citizen, world, names);
        Check(trace.Subject == "Jane" && trace.Here == "Main Street" && trace.Destination == "School", "citizen carrier and parking destination");
        Check(trace.Legs.Count == 3, "fold adjacent rows, and drop the leg that only repeats Now");
        Check(trace.HereMetres == 200f, "the dropped leg leaves its distance on the Now line");
        Check(JourneyTrace.Resolve(trace.Legs[0].Route) == line && JourneyTrace.Resolve(trace.Legs[1].Route) == otherLine, "line references retain identity");
        Check(trace.Legs[0].Colour == "#1e90ff" && trace.Legs[0].Number == 7, "a transit leg carries the line colour and number");
        Check(trace.Legs[1].Colour == "" && trace.Legs[1].Number == 0, "a line without a colour or number says so rather than inventing one");
        Check(trace.Legs[2].Kind == "road" && trace.Legs[2].Colour == "", "road legs carry no colour");
        // The pathfind ends on a lane in the school car park, which has no position of its own.
        Check(JourneyTrace.Resolve(trace.DestinationRef) == destination, "a destination inside a car park still flies to the building");
        world.Set(body, new PathOwner { m_ElementIndex = 4 }); trace.Refresh(citizen, world, names);
        Check(trace.Legs.Count == 2 && JourneyTrace.Resolve(trace.Legs[0].Route) == otherLine, "skip travelled elements");
        var bus = Named("Bus"); world.Set(bus, new Game.Routes.CurrentRoute { m_Route = line });
        world.Set(body, new Game.Creatures.CurrentVehicle { m_Vehicle = bus });
        trace.Refresh(citizen, world, names);
        Check(trace.Here == "Line 1" && trace.Destination == "School", "passenger keeps personal destination while displaying current line");
        world.Set(body, new PathOwner { m_ElementIndex = int.MaxValue }); trace.Refresh(citizen, world, names);
        Check(trace.Legs.Count == 0, "exhausted path has no stale legs");
        trace.Refresh(destination, world, names);
        Check(!trace.HasSubject && trace.Legs.Count == 0 && trace.Destination == null, "nontravelling selection clears trace");
        trace.Refresh(Entity.Null, world, names);
        Check(trace.Subject == null && !trace.HasSubject, "cleared selection clears subject");
        var deleted = world.Create(); world.Recycle(deleted); trace.Refresh(deleted, world, names);
        Check(!trace.HasSubject, "destroyed selection is safe");
        foreach (var invalid in new[] { "", "1", "1:x", "1:2:3", "-1:2", "1:-2", "0:1" })
            Check(JourneyTrace.Resolve(invalid) == Entity.Null, "invalid reference rejected: " + invalid);
        world.Set(body, new PathOwner { m_ElementIndex = 0 }); path.Clear();
        for (int i = 0; i < 25; i++) path.Add(Step(Street("Street " + i)));
        trace.Refresh(citizen, world, names);
        Check(trace.Legs.Count == 24 && trace.Truncated, "long journeys indicate omitted steps");
        var cycle = world.Create(); world.Set(cycle, new Game.Common.Owner { m_Owner = cycle });
        path.Clear(); path.Add(Step(cycle)); trace.Refresh(citizen, world, names);
        Check(trace.Legs.Count == 0 && !trace.Truncated, "owner cycles terminate and truncation resets");
        // Back on foot: riding the same line would make the first transit leg a repeat of Now.
        world.Set(body, new Game.Creatures.CurrentVehicle { m_Vehicle = Entity.Null });
        var segment = world.Create(); world.Set(segment, new Game.Common.Owner { m_Owner = line });
        path.Clear(); path.Add(Step(segment)); path.Add(Step(line));
        trace.Refresh(citizen, world, names);
        Check(trace.Legs.Count == 1 && JourneyTrace.Resolve(trace.Legs[0].Route) == line, "route segments and direct routes resolve to the same line");
        // One street is stored as lane, junction, lane, junction, lane. The junctions belong to
        // nodes rather than to an aggregate, so they name nothing - and clearing the fold when an
        // element names nothing made every lane after a junction look like a fresh leg, which is
        // what put five identical street rows on screen.
        var junction = world.Create();
        world.Set(junction, new Game.Net.Curve { m_Bezier = new Colossal.Mathematics.Bezier4x3 { Length = 5f } });
        var first = Street("First Street", 120f);
        path.Clear();
        foreach (var target in new[] { first, junction, first, junction, first, Street("Second Street", 40f) })
            path.Add(Step(target));
        trace.Refresh(citizen, world, names);
        Check(trace.Legs.Count == 2, "unnamed junctions do not split one street into repeated legs");
        Check(trace.Legs[0].Name == "First Street" && trace.Legs[1].Name == "Second Street", "a differently named street still gets its own leg");
        Check(trace.Legs[0].Metres == 370f, "a folded leg sums its lanes and the junctions inside it");
        Check(trace.Legs[1].Metres == 40f, "each leg measures only its own ground");
        // Now names the street underfoot, so the leg repeating it goes and its distance moves up.
        path.Clear();
        foreach (var target in new[] { road, road, Street("Elm Street", 50f) }) path.Add(Step(target));
        trace.Refresh(citizen, world, names);
        Check(trace.Here == "Main Street" && trace.Legs.Count == 1 && trace.Legs[0].Name == "Elm Street", "the leg repeating Now is dropped");
        Check(trace.HereMetres == 200f && trace.Legs[0].Metres == 50f, "its distance moves to Now and the rest is untouched");
        // A long run down one street must not ask the game for that street's name once per lane:
        // the elements fold into a single row, and naming is the expensive half of reading one.
        var longStreet = Street("Long Road", 60f);
        var shortStreet = Street("Short Row", 20f);
        path.Clear();
        for (int i = 0; i < 40; i++) path.Add(Step(i < 30 ? longStreet : shortStreet));
        names.Lookups = 0;
        trace.Refresh(citizen, world, names);
        Check(trace.Legs.Count == 2, "forty elements, two streets, two rows");
        Check(names.Lookups <= 6, "names are resolved per place, not per path element: " + names.Lookups);
        // Cleared between reads, so a street renamed while the panel is open reads correctly next
        // time rather than serving the name this read happened to cache.
        names.Names[world.GetComponentData<Game.Net.Aggregated>(world.GetComponentData<Game.Common.Owner>(longStreet).m_Owner).m_Aggregate] = "Renamed Road";
        trace.Refresh(citizen, world, names);
        Check(trace.Legs[0].Name == "Renamed Road", "a cached name does not outlive the read that cached it");

        // Waiting on a platform: no lane underfoot, so the station is what names the spot.
        var station = Named("Central Station"); world.AddComponent<Game.Buildings.Building>(station);
        var waiting = Named("Ada");
        world.Set(waiting, new Game.Citizens.CurrentTransport { m_CurrentTransport = world.Create() });
        world.Set(waiting, new Game.Citizens.CurrentBuilding { m_CurrentBuilding = station });
        trace.Refresh(waiting, world, names);
        Check(trace.Here == "Central Station" && trace.HasSubject, "a citizen waiting indoors is placed by their building");
        var outdoors = Named("Ben");
        world.Set(outdoors, new Game.Citizens.CurrentTransport { m_CurrentTransport = world.Create() });
        trace.Refresh(outdoors, world, names);
        Check(trace.Here == null, "nowhere to report stays nowhere rather than guessing");
        Console.WriteLine("PASS: selected journeys, folding, line colours, indoor waits, stale selections, cycles and truncation");
    }
}
