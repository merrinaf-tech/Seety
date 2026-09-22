using System.Collections.Generic;
using Game.UI;
using Unity.Entities;

namespace Seety.Vitals
{
    /// <summary>One step of a journey: a stretch of street, or a ride on one transport line.</summary>
    public sealed class JourneyLeg
    {
        /// <summary>"road" or "transit". The UI draws a transit leg as a clickable row.</summary>
        public string Kind;

        /// <summary>The street, or the line. Already resolved to something a player recognises.</summary>
        public string Name;

        /// <summary>
        /// The route entity behind a transit leg, as "index:version", or empty for a road leg.
        /// Clicking the row sends this back and the line's own panel opens.
        /// </summary>
        public string Route;

        /// <summary>
        /// How far this leg runs, in metres, summed over the lanes it folded together.
        ///
        /// Metres and not the player's own unit, because this is the simulation's figure and the
        /// conversion belongs where the number is drawn - the game can be switched between unit
        /// systems while a panel is open, and a value already converted here would be stale the
        /// moment it was.
        /// </summary>
        public float Metres;

        /// <summary>
        /// The line's colour as "#rrggbb", or empty for a road leg.
        ///
        /// The player picked it and the game draws every other mention of that line in it, so it
        /// identifies a line faster than its name does - which matters here, because an unnamed
        /// line falls back to the name of the tool that built it, and several lines can carry the
        /// same fallback.
        /// </summary>
        public string Colour;

        /// <summary>The line's number, or zero. Drawn inside the colour, as vanilla does.</summary>
        public int Number;
    }

    /// <summary>
    /// Where the selected thing is, where it is going, and the way it means to get there.
    ///
    /// Everything here already exists in the simulation: a moving entity carries the path the game
    /// itself computed for it, as a buffer of lanes it will traverse in order. Nothing is
    /// predicted or re-routed - this reads the plan the game is already following, which is why
    /// the list changes the moment the game changes its mind.
    ///
    /// The work is not in finding the path but in making it legible. A path element is one lane,
    /// so a single street arrives as a dozen consecutive elements, and a bus ride arrives as one
    /// element per stop-to-stop hop. Read literally it produces a hundred rows saying almost
    /// nothing. So consecutive elements resolving to the same street, or to the same line, are
    /// folded into one leg: the row count then matches the number of decisions in the journey,
    /// which is what someone reading it is actually after.
    /// </summary>
    public sealed class JourneyTrace
    {
        /// <summary>
        /// Rows past this are cut. A journey across a large city can hold hundreds of elements
        /// before folding, and a window nobody can read to the end is not more informative.
        /// </summary>
        private const int MaxLegs = 24;

        /// <summary>
        /// How far up the ownership chain a lane is followed before giving up.
        ///
        /// A lane belongs to an edge, which belongs to an aggregate; a route lane belongs to a
        /// waypoint, which belongs to the line. Four hops covers both with room to spare, and the
        /// bound matters more than the exact figure: an ownership cycle in unexpected data would
        /// otherwise hang the UI thread rather than produce a missing row.
        /// </summary>
        private const int MaxOwnerHops = 4;

        private readonly List<JourneyLeg> _legs = new List<JourneyLeg>();

        /// <summary>
        /// The name of each street and line met while reading one path, kept for that read only.
        ///
        /// A path element is a lane, so one street arrives as dozens of consecutive elements that
        /// all resolve to the same aggregate. Naming each of them asked the game to build the same
        /// string over and over - a few hundred allocations twice a second, on the thread that
        /// draws the frame, to produce at most twenty-four rows.
        ///
        /// Cleared at the start of every read rather than kept between them, so a street renamed
        /// while the panel is open is named correctly on the next refresh.
        /// </summary>
        private readonly Dictionary<Entity, string> _placeNames = new Dictionary<Entity, string>();

        /// <summary>Whether anything selected has a journey to show at all.</summary>
        public bool HasSubject { get; private set; }
        public bool Truncated { get; private set; }

        /// <summary>What was selected, named.</summary>
        public string Subject { get; private set; }

        /// <summary>Where it is right now - a street, or the line it is riding.</summary>
        public string Here { get; private set; }

        /// <summary>
        /// How much of <see cref="Here"/> is still ahead, in metres, or zero when that is unknown.
        ///
        /// The first leg of a journey is almost always the street the thing is already standing
        /// on, which "Now" has just named. Rather than print that row twice, the row is dropped
        /// and the distance it carried moves here - so nothing is lost and the list below starts
        /// at the first change of street.
        /// </summary>
        public float HereMetres { get; private set; }

        /// <summary>Where it is headed, from the path the game computed.</summary>
        public string Destination { get; private set; }

        /// <summary>
        /// The destination itself, as "index:version", so the row can take the camera there.
        /// Empty when the destination could be named but not located.
        /// </summary>
        public string DestinationRef { get; private set; }

        public IReadOnlyList<JourneyLeg> Legs
        {
            get { return _legs; }
        }

        /// <summary>
        /// Reads the selected entity's journey. Safe to call with nothing selected, with something
        /// selected that never moves, and with a vehicle between paths - all three are the same
        /// answer here, an empty trace, and the UI says so rather than showing a stale one.
        /// </summary>
        public void Refresh(Entity selected, EntityManager entities, NameSystem names)
        {
            _legs.Clear();
            HasSubject = false;
            Truncated = false;
            Subject = null;
            Here = null;
            HereMetres = 0f;
            Destination = null;
            DestinationRef = string.Empty;

            if (selected == Entity.Null || !entities.Exists(selected))
            {
                return;
            }

            Subject = SafeName(names, selected);

            // A citizen is not the thing that moves. It points at whatever is currently carrying
            // it - its own body while walking, a car while driving - and the path lives there.
            // Selecting a citizen in the game selects the citizen, so without this hop the most
            // obvious thing to click would be the one thing with no journey on it.
            Entity carrier = selected;
            if (entities.HasComponent<Game.Citizens.CurrentTransport>(selected))
            {
                Entity transport = entities
                    .GetComponentData<Game.Citizens.CurrentTransport>(selected).m_CurrentTransport;

                if (transport != Entity.Null && entities.Exists(transport))
                {
                    carrier = transport;
                }
            }

            Entity hereRoute;
            Here = WhereItIs(carrier, entities, names, out hereRoute);

            // Waiting on a platform is not the same as being on a lane: a citizen inside a
            // station has no road under them, and the trace used to give up and say nothing. The
            // building they are standing in is the answer anyone would give, and the game
            // records it.
            if (string.IsNullOrEmpty(Here))
            {
                Here = Indoors(selected, entities, names);
            }

            Entity destination;
            Destination = WhereItIsGoing(carrier, entities, names, out destination);
            DestinationRef = destination == Entity.Null ? string.Empty : Reference(destination);
            ReadLegs(carrier, entities, names);

            // "Now" has already named the street underfoot, so the leg repeating it is a row that
            // tells the reader nothing they did not just read. It goes, and its remaining distance
            // moves up to the line that does name the place.
            //
            // A transit first leg is matched on the route rather than on the name, for the same
            // reason the fold is: two lines can be called the same thing, and boarding a second
            // "Line 1" after riding the first is a real step, not a repetition.
            if (_legs.Count > 0 && !string.IsNullOrEmpty(Here))
            {
                bool repeatsHere = _legs[0].Kind == "transit"
                    ? hereRoute != Entity.Null && _legs[0].Route == Reference(hereRoute)
                    : _legs[0].Name == Here;

                if (repeatsHere)
                {
                    HereMetres = _legs[0].Metres;
                    _legs.RemoveAt(0);
                }
            }

            HasSubject = !string.IsNullOrEmpty(Here)
                || !string.IsNullOrEmpty(Destination)
                || _legs.Count > 0;
        }

        /// <summary>
        /// The current position, preferring the line over the street.
        ///
        /// Someone on a bus is on a road too, and saying so would be true and useless: the street
        /// under the bus is not a fact they can act on, and it changes every few seconds. The line
        /// is the answer to "where is it" for anything riding one.
        /// </summary>
        private static string WhereItIs(Entity carrier, EntityManager entities, NameSystem names,
            out Entity hereRoute)
        {
            hereRoute = Entity.Null;

            Entity vehicle = Entity.Null;
            if (entities.HasComponent<Game.Creatures.CurrentVehicle>(carrier))
            {
                vehicle = entities.GetComponentData<Game.Creatures.CurrentVehicle>(carrier).m_Vehicle;
            }

            Entity riding = vehicle != Entity.Null && entities.Exists(vehicle) ? vehicle : carrier;

            if (entities.HasComponent<Game.Routes.CurrentRoute>(riding))
            {
                Entity route = entities.GetComponentData<Game.Routes.CurrentRoute>(riding).m_Route;
                string line = SafeName(names, route);

                if (!string.IsNullOrEmpty(line))
                {
                    hereRoute = route;
                    return line;
                }
            }

            Entity lane = CurrentLane(riding, entities);
            if (lane != Entity.Null)
            {
                string kind;
                string name;
                Entity route;
                if (Classify(lane, entities, names, out kind, out name, out route))
                {
                    return name;
                }
            }

            // Riding something unnamed, or standing still inside a building. Naming the carrier is
            // the last thing left that is true.
            return riding == carrier ? null : SafeName(names, riding);
        }

        /// <summary>
        /// The destination the game itself is steering towards.
        ///
        /// <see cref="Game.Pathfind.PathInformation"/> carries it outright - it is the result of
        /// the pathfind, not a guess from the last element of the buffer, and it survives the
        /// buffer being consumed as the journey proceeds. Target is the fallback for things that
        /// carry an order without a computed path yet.
        /// </summary>
        private static string WhereItIsGoing(Entity carrier, EntityManager entities, NameSystem names,
            out Entity destination)
        {
            destination = Entity.Null;

            if (entities.HasComponent<Game.Pathfind.PathInformation>(carrier))
            {
                Entity place = entities
                    .GetComponentData<Game.Pathfind.PathInformation>(carrier).m_Destination;

                string label = NamedPlace(place, entities, names);
                if (!string.IsNullOrEmpty(label))
                {
                    destination = Settled(place, entities);
                    return label;
                }
            }

            if (entities.HasComponent<Game.Common.Target>(carrier))
            {
                Entity place = entities.GetComponentData<Game.Common.Target>(carrier).m_Target;
                string label = NamedPlace(place, entities, names);

                if (!string.IsNullOrEmpty(label))
                {
                    destination = Settled(place, entities);
                }

                return label;
            }

            return null;
        }

        /// <summary>
        /// Where a citizen is when they are on no lane at all - inside a building, or waiting on
        /// its platform. The station is what a player would call that spot.
        /// </summary>
        private static string Indoors(Entity selected, EntityManager entities, NameSystem names)
        {
            if (!entities.HasComponent<Game.Citizens.CurrentBuilding>(selected))
            {
                return null;
            }

            Entity building = entities
                .GetComponentData<Game.Citizens.CurrentBuilding>(selected).m_CurrentBuilding;

            return SafeName(names, building);
        }

        /// <summary>
        /// The entity a click should fly to.
        ///
        /// A pathfind often ends at a lane inside a car park rather than at the building itself,
        /// and a lane has no position of its own to point a camera at. Walking up to the owner
        /// that does have one keeps the click landing on the place the row names.
        /// </summary>
        private static Entity Settled(Entity place, EntityManager entities)
        {
            Entity current = place;

            for (int hop = 0; hop < MaxOwnerHops; hop++)
            {
                if (current == Entity.Null || !entities.Exists(current))
                {
                    return Entity.Null;
                }

                if (entities.HasComponent<Game.Objects.Transform>(current))
                {
                    return current;
                }

                if (!entities.HasComponent<Game.Common.Owner>(current))
                {
                    return Entity.Null;
                }

                current = entities.GetComponentData<Game.Common.Owner>(current).m_Owner;
            }

            return Entity.Null;
        }

        /// <summary>
        /// A line's colour, as the hex the UI can paint with, or empty when it has none.
        ///
        /// Bytes formatted by hand rather than through a Unity helper: this runs on the UI thread
        /// for every leg of every refresh, and the result is going straight into JSON.
        /// </summary>
        private static string LineColour(Entity route, EntityManager entities)
        {
            if (route == Entity.Null || !entities.Exists(route)
                || !entities.HasComponent<Game.Routes.Color>(route))
            {
                return string.Empty;
            }

            var colour = entities.GetComponentData<Game.Routes.Color>(route).m_Color;

            return "#"
                + colour.r.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)
                + colour.g.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)
                + colour.b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>A line's number, or zero when it carries none.</summary>
        private static int LineNumber(Entity route, EntityManager entities)
        {
            if (route == Entity.Null || !entities.Exists(route)
                || !entities.HasComponent<Game.Routes.RouteNumber>(route))
            {
                return 0;
            }

            return entities.GetComponentData<Game.Routes.RouteNumber>(route).m_Number;
        }

        /// <summary>
        /// Walks the remaining path, folding runs of the same street or line into single legs.
        ///
        /// Elements before <see cref="Game.Pathfind.PathOwner.m_ElementIndex"/> have been
        /// travelled already. Showing them would turn a plan into a history, and the question the
        /// window answers is where the thing is going.
        /// </summary>
        private void ReadLegs(Entity carrier, EntityManager entities, NameSystem names)
        {
            if (!entities.HasBuffer<Game.Pathfind.PathElement>(carrier))
            {
                return;
            }

            int start = 0;
            if (entities.HasComponent<Game.Pathfind.PathOwner>(carrier))
            {
                start = entities.GetComponentData<Game.Pathfind.PathOwner>(carrier).m_ElementIndex;
            }

            DynamicBuffer<Game.Pathfind.PathElement> path =
                entities.GetBuffer<Game.Pathfind.PathElement>(carrier, true);

            if (start < 0)
            {
                start = 0;
            }

            _placeNames.Clear();

            string lastKind = null;
            string lastName = null;
            Entity lastPlace = Entity.Null;

            // The leg the distances are currently landing in. Null until the first element that
            // names somewhere: metres travelled before that belong to no row anyone can see.
            JourneyLeg current = null;

            // Bound raw work too, including paths full of unnamed/unrecognised elements.
            int end = start + System.Math.Min(System.Math.Max(0, path.Length - start), 4096);
            Truncated = end < path.Length;
            for (int i = start; i < end; i++)
            {
                string kind;
                string name;
                Entity route;

                float metres = LaneMetres(path[i], entities);

                if (!Classify(path[i].m_Target, entities, names, _placeNames,
                        out kind, out name, out route))
                {
                    // The connector still covers ground, so its length joins the leg it sits
                    // inside rather than being dropped: a street's figure should be the distance
                    // actually driven along it, junctions included.
                    if (current != null)
                    {
                        current.Metres += metres;
                    }

                    // Not every element names a place. Junction nodes, crossings and the short
                    // connectors between two lanes of one street resolve to nothing, and they sit
                    // *inside* a run rather than between runs.
                    //
                    // Clearing the fold here is what put five "Fernbrook Street" rows in a row: a
                    // street is stored as a lane, a connector, a lane, a connector, and every
                    // connector made the lane after it look like a fresh leg. An element that
                    // names nothing carries nothing, so it no longer breaks the run it is in.
                    continue;
                }

                // The fold, and the two halves are not folded on the same thing.
                //
                // A transit leg is compared on the route itself, because the row is clickable:
                // two lines can share a name and open different panels, so collapsing them would
                // send a click to the wrong one.
                //
                // A road leg is compared on the name alone. It carries nothing else - no click,
                // no second line - so two consecutive rows reading "High Lane" are the same row
                // printed twice whatever the aggregates underneath them are, and telling them
                // apart serves nobody looking at the panel.
                bool sameAsPrevious = kind == lastKind && name == lastName
                    && (kind != "transit" || route == lastPlace);

                if (sameAsPrevious)
                {
                    current.Metres += metres;
                    continue;
                }

                lastKind = kind;
                lastName = name;
                lastPlace = route;
                if (_legs.Count == MaxLegs)
                {
                    Truncated = true;
                    break;
                }

                current = new JourneyLeg
                {
                    Kind = kind,
                    Name = name,
                    Route = kind == "transit" ? Reference(route) : string.Empty,
                    Colour = kind == "transit" ? LineColour(route, entities) : string.Empty,
                    Number = kind == "transit" ? LineNumber(route, entities) : 0,
                    Metres = metres,
                };

                _legs.Add(current);
            }
        }

        /// <summary>
        /// Turns a path element into the street or the line it belongs to.
        ///
        /// The chain is walked rather than read at a fixed depth because the two cases have
        /// different shapes, and because naming the first entity that answered to a name is the
        /// mistake that produced rows called "Car Drive Lane 3" elsewhere in this mod - lane
        /// prefabs carry names, they are simply the wrong ones. Only an aggregate or a route is
        /// accepted as an answer.
        /// </summary>
        private static bool Classify(Entity target, EntityManager entities, NameSystem names,
            out string kind, out string name, out Entity route)
        {
            return Classify(target, entities, names, null, out kind, out name, out route);
        }

        /// <summary>
        /// As above, reusing names already resolved during this read.
        ///
        /// The walk itself stays exactly as it was - which entity is accepted, and at which hop,
        /// does not change - because the cache sits under the naming rather than around the
        /// decision. A place is still rejected when it has no usable name; it is simply not asked
        /// twice.
        /// </summary>
        private static bool Classify(Entity target, EntityManager entities, NameSystem names,
            Dictionary<Entity, string> seen,
            out string kind, out string name, out Entity route)
        {
            kind = null;
            name = null;
            route = Entity.Null;

            Entity current = target;

            for (int hop = 0; hop < MaxOwnerHops; hop++)
            {
                if (current == Entity.Null || !entities.Exists(current))
                {
                    return false;
                }

                // Some path elements lead through a segment instead of a waypoint. Both owner
                // chains end at the route; never fall back to the road underneath that line.
                if (entities.HasComponent<Game.Routes.Route>(current))
                {
                    string label = Remembered(names, current, seen);
                    if (!string.IsNullOrEmpty(label))
                    {
                        kind = "transit";
                        name = label;
                        route = current;
                        return true;
                    }
                }

                // A waypoint is a stop on a line, and its owner is the line itself.
                if (entities.HasComponent<Game.Routes.Waypoint>(current)
                    && entities.HasComponent<Game.Common.Owner>(current))
                {
                    Entity line = entities.GetComponentData<Game.Common.Owner>(current).m_Owner;
                    string label = Remembered(names, line, seen);

                    if (!string.IsNullOrEmpty(label) && entities.Exists(line)
                        && entities.HasComponent<Game.Routes.Route>(line))
                    {
                        kind = "transit";
                        name = label;
                        route = line;
                        return true;
                    }
                }

                if (entities.HasComponent<Game.Net.Aggregated>(current))
                {
                    Entity street = entities.GetComponentData<Game.Net.Aggregated>(current).m_Aggregate;
                    string label = Remembered(names, street, seen);

                    if (!string.IsNullOrEmpty(label))
                    {
                        kind = "road";
                        name = label;
                        route = street;
                        return true;
                    }
                }

                if (!entities.HasComponent<Game.Common.Owner>(current))
                {
                    return false;
                }

                current = entities.GetComponentData<Game.Common.Owner>(current).m_Owner;
            }

            return false;
        }

        /// <summary>
        /// How much ground one path element covers, in metres.
        ///
        /// A lane is a curve, and the element says which slice of that curve is used - a turn
        /// across a junction uses a few metres of a lane whose full length is far more, and a
        /// through movement uses all of it. So the slice is measured rather than the lane:
        /// summing whole lanes would overstate every journey that turns.
        ///
        /// The two ends arrive in either order, since a lane can be travelled against its own
        /// direction, and a negative span would silently subtract from the leg's total.
        /// </summary>
        private static float LaneMetres(Game.Pathfind.PathElement element, EntityManager entities)
        {
            Entity lane = element.m_Target;

            if (lane == Entity.Null || !entities.Exists(lane)
                || !entities.HasComponent<Game.Net.Curve>(lane))
            {
                return 0f;
            }

            var curve = entities.GetComponentData<Game.Net.Curve>(lane);
            var span = element.m_TargetDelta;
            float from = span.x < span.y ? span.x : span.y;
            float to = span.x < span.y ? span.y : span.x;

            float metres = Colossal.Mathematics.MathUtils.Length(
                curve.m_Bezier, new Colossal.Mathematics.Bounds1(from, to));

            // A curve that measures as NaN, or as something absurd, is not worth propagating into
            // a total the player reads as a fact.
            return metres > 0f && metres < 100000f ? metres : 0f;
        }

        /// <summary>The lane a thing is on, whichever kind of thing it is.</summary>
        private static Entity CurrentLane(Entity entity, EntityManager entities)
        {
            if (entities.HasComponent<Game.Vehicles.CarCurrentLane>(entity))
            {
                return entities.GetComponentData<Game.Vehicles.CarCurrentLane>(entity).m_Lane;
            }

            if (entities.HasComponent<Game.Creatures.HumanCurrentLane>(entity))
            {
                return entities.GetComponentData<Game.Creatures.HumanCurrentLane>(entity).m_Lane;
            }

            return Entity.Null;
        }

        /// <summary>
        /// Names a destination. A pathfind can end at a building, at a lane inside its car park,
        /// or at a piece of road, so the same ownership walk that names a leg is what turns the
        /// last two into something worth printing.
        /// </summary>
        private static string NamedPlace(Entity place, EntityManager entities, NameSystem names)
        {
            if (place == Entity.Null || !entities.Exists(place))
            {
                return null;
            }

            // Destinations can be parking lanes owned by a building. Prefer that building over
            // a raw lane prefab label such as "Car Drive Lane 3".
            Entity owner = place;
            for (int hop = 0; hop < MaxOwnerHops && owner != Entity.Null && entities.Exists(owner); hop++)
            {
                if (entities.HasComponent<Game.Buildings.Building>(owner))
                {
                    string label = SafeName(names, owner);
                    if (!string.IsNullOrEmpty(label)) return label;
                }
                if (!entities.HasComponent<Game.Common.Owner>(owner)) break;
                owner = entities.GetComponentData<Game.Common.Owner>(owner).m_Owner;
            }

            string kind;
            string name;
            Entity route;
            if (Classify(place, entities, names, out kind, out name, out route))
            {
                return name;
            }

            return SafeName(names, place);
        }

        /// <summary>
        /// How an entity crosses to the UI and back.
        ///
        /// Both halves are needed: an index alone is reused the moment the entity is destroyed, so
        /// a click held over from a line that has since been deleted would open whatever inherited
        /// its slot. With the version attached, a stale click resolves to nothing and is dropped.
        /// </summary>
        private static string Reference(Entity entity)
        {
            if (entity == Entity.Null)
            {
                return string.Empty;
            }

            return entity.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ":"
                + entity.Version.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Reads back what <see cref="Reference"/> wrote. Entity.Null on anything else.</summary>
        public static Entity Resolve(string reference)
        {
            if (string.IsNullOrEmpty(reference))
            {
                return Entity.Null;
            }

            int split = reference.IndexOf(':');
            if (split <= 0 || split >= reference.Length - 1)
            {
                return Entity.Null;
            }

            int index;
            int version;
            if (!int.TryParse(reference.Substring(0, split),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out index)
                || !int.TryParse(reference.Substring(split + 1),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out version))
            {
                return Entity.Null;
            }

            return index > 0 && version > 0 ? new Entity { Index = index, Version = version } : Entity.Null;
        }

        /// <summary>
        /// A place's name, asked for once per read.
        ///
        /// A null cache means "ask the game" - the callers that run a single time per refresh
        /// have nothing to gain from remembering, and would only have to invalidate it.
        /// </summary>
        private static string Remembered(NameSystem names, Entity entity,
            Dictionary<Entity, string> seen)
        {
            if (seen == null)
            {
                return SafeName(names, entity);
            }

            string label;
            if (seen.TryGetValue(entity, out label))
            {
                return label;
            }

            label = SafeName(names, entity);
            seen[entity] = label;
            return label;
        }

        private static string SafeName(NameSystem names, Entity entity)
        {
            if (entity == Entity.Null)
            {
                return null;
            }

            try
            {
                return names.GetRenderedLabelName(entity);
            }
            catch
            {
                return null;
            }
        }
    }
}
