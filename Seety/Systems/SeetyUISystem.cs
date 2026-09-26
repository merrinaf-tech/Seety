using System;
using System.Collections.Generic;
using Colossal.UI.Binding;
using Game.City;
using Game.Prefabs;
using Game.Simulation;
using Game.UI;
using Game.UI.InGame;
using Unity.Entities;

namespace Seety.Systems
{
    /// <summary>
    /// Feeds the strip and handles clicks on it.
    ///
    /// Reads only. Nothing here writes city state - Seety reports, it does not govern. The one
    /// thing it does change is which infoview is active, and only because the player asked by
    /// clicking.
    /// </summary>
    public partial class SeetyUISystem : UISystemBase
    {
        private const string Group = "seety";

        /// <summary>
        /// City-wide figures move slowly and the strip is glanceable, not an instrument, so this
        /// stays well off the per-frame budget.
        ///
        /// Five seconds, arrived at by trying shorter ones. It started at half a second, which
        /// was defensible for a number that ticks but not for a list: the traffic window reorders
        /// its rows as jams form and clear, and a row can move out from under the cursor between
        /// deciding to click it and clicking it. Two seconds still did that often enough to
        /// notice. Nothing here changes fast enough to be worth the flicker.
        /// </summary>
        private const double RefreshIntervalSeconds = 5.0;

        private PrefabSystem _prefabs;

        /// <summary>Asked for the map's extent, so stuck vehicles off it can be ignored.</summary>
        private Game.Simulation.TerrainSystem _terrain;
        private InfoviewsUISystem _infoviews;

        /// <summary>Resolved once and kept, purely so AddFunds does not create a system on a click.</summary>
        private CitySystem _cityForFunds;
        private Vitals.VitalReader _reader;

        private RawValueBinding _vitalsBinding;
        private ValueBinding<bool> _visibleBinding;

        /// <summary>Whether the bar's icons carry a white edge. See SeetySettings.IconOutline.</summary>
        private ValueBinding<bool> _iconOutlineBinding;

        /// <summary>Whether the bar is drawn as the game's floating buttons. See SeetySettings.GameButtonStyle.</summary>
        private ValueBinding<bool> _gameButtonStyleBinding;
        private ValueBinding<bool> _toolbarTrendsBinding;

        /// <summary>Public transport standing still: the other half of the traffic window.</summary>
        private readonly Vitals.StoppedTransitList _transit = new Vitals.StoppedTransitList();
        private EntityQuery _transitQuery;

        /// <summary>True while the traffic window lists transit rather than road jams.</summary>
        private ValueBinding<bool> _transitModeBinding;
        private ValueBinding<int> _posXBinding;
        private ValueBinding<int> _posYBinding;

        private readonly List<Vitals.Vital> _active = new List<Vitals.Vital>();
        private readonly Dictionary<string, float> _values = new Dictionary<string, float>();

        /// <summary>Infoview prefab name -> entity, resolved once the prefabs exist.</summary>
        private readonly Dictionary<string, Entity> _infoviewEntities = new Dictionary<string, Entity>();

        /// <summary>Infoview name -> the game's own sort key, from InfoviewPrefab.</summary>
        private readonly Dictionary<string, long> _infoviewOrder = new Dictionary<string, long>();
        private bool _infoviewsResolved;

        /// <summary>Thresholds stay quiet until someone lives here. See VitalReader.HasCitizens.</summary>
        private bool _hasCitizens;

        /// <summary>Every active notification icon in the city. Built once, counted per refresh.</summary>
        private EntityQuery _iconQuery;
        private EntityQuery _commercialCompanyQuery;
        private EntityQuery _industrialCompanyQuery;

        /// <summary>Counting notifications walks a collection, so it only happens when shown.</summary>
        private bool _problemsActive;

        /// <summary>The grouped view the Problems row expands into.</summary>
        private readonly Notifications.NotificationBreakdown _breakdown = new Notifications.NotificationBreakdown();

        /// <summary>Passengers and freight per mode, behind the Transport row.</summary>
        private readonly Vitals.TransportBreakdown _transport = new Vitals.TransportBreakdown();

        private bool _transportActive;

        /// <summary>
        /// While on, the strip shows every row in the catalogue - including the ones switched off
        /// - so the player can pick by clicking the thing itself rather than hunting a checkbox.
        /// </summary>
        private bool _configMode;

        private ValueBinding<bool> _configModeBinding;

        /// <summary>Which row the player has open, or empty. Only its history is fetched.</summary>
        private string _expandedId = string.Empty;

        /// <summary>The schools of one level, behind whichever education row is open.</summary>
        private readonly Vitals.SchoolBreakdown _schools = new Vitals.SchoolBreakdown();

        private EntityQuery _schoolQuery;

        /// <summary>The city's cemeteries, behind the Cemetery space row.</summary>
        private readonly Vitals.CemeteryBreakdown _cemeteries = new Vitals.CemeteryBreakdown();

        private EntityQuery _cemeteryQuery;
        private Game.UI.NameSystem _names;

        /// <summary>Workforce against workplaces, behind the Workers row.</summary>
        private readonly Vitals.WorkforceTable _workforce = new Vitals.WorkforceTable();

        private RawValueBinding _workforceBinding;
        private RawValueBinding _demographicsBinding;

        /// <summary>The citizen census, shared by the workforce table and the demographics view.</summary>
        private CitizenCensusSystem _census;
        private RawValueBinding _historyBinding;
        private RawValueBinding _notificationsBinding;

        /// <summary>The blue demand bars taken apart per resource. See ResourceBreakdown.</summary>
        private readonly Vitals.ResourceBreakdown _shops = new Vitals.ResourceBreakdown();

        private readonly Vitals.ResourceBreakdown _factories = new Vitals.ResourceBreakdown();

        /// <summary>
        /// Office is not a separate company kind in this data - see ResourceBreakdown.RefreshOffice
        /// - so this reads the same systems as _factories, filtered to the four resources
        /// EconomyUtils.IsOfficeResource recognises.
        /// </summary>
        private readonly Vitals.ResourceBreakdown _offices = new Vitals.ResourceBreakdown();

        /// <summary>Stuck vehicles, grouped by kind. Only refreshed while the traffic row is open.</summary>
        private readonly Vitals.TrafficJamBreakdown _jams = new Vitals.TrafficJamBreakdown();

        private EntityQuery _jamQuery;

        /// <summary>Transit vehicles, for counting who and what is aboard right now.</summary>
        private EntityQuery _passengerVehicleQuery;

        private EntityQuery _cargoVehicleQuery;

        /// <summary>Taxis and road freight, which the two queries above do not match.</summary>
        private EntityQuery _taxiQuery;

        private EntityQuery _deliveryTruckQuery;

        private RawValueBinding _resourcesBinding;
        private Game.Rendering.CameraUpdateSystem _camera;

        /// <summary>The in-world notification icons, and whether they are currently hidden.</summary>
        private Notifications.NotificationIconVisibility _iconVisibility;
        private ValueBinding<bool> _iconsHiddenBinding;

        /// <summary>The journey of whatever is selected, while the traffic window's switch is on.</summary>
        private readonly Vitals.JourneyTrace _journey = new Vitals.JourneyTrace();
        private ValueBinding<bool> _journeyOnBinding;
        private RawValueBinding _journeyBinding;

        /// <summary>
        /// The game's own selection, which is what the journey window follows. Also how a click on
        /// a transit leg opens that line: SelectedInfoUISystem reads this same property, so
        /// writing it is the whole of "open the line" - no panel of Seety's own to keep in step.
        /// </summary>
        private Game.Tools.ToolSystem _tools;
        private SelectedInfoUISystem _selectedInfo;

        /// <summary>Selection changes refresh immediately; moving subjects also refresh twice a second.</summary>
        private Entity _lastSelected;
        private double _nextJourneyRefresh;

        private double _nextRefresh;
        private double _nextIconRefresh;

        protected override void OnCreate()
        {
            base.OnCreate();

            _prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
            _infoviews = World.GetOrCreateSystemManaged<InfoviewsUISystem>();

            _reader = new Vitals.VitalReader(
                EntityManager,
                World.GetOrCreateSystemManaged<CitySystem>(),
                World.GetOrCreateSystemManaged<CountHouseholdDataSystem>(),
                World.GetOrCreateSystemManaged<CityStatisticsSystem>(),
                World.GetOrCreateSystemManaged<WaterStatisticsSystem>());

            _iconQuery = GetEntityQuery(
                ComponentType.ReadOnly<Game.Notifications.Icon>(),
                ComponentType.ReadOnly<PrefabRef>());

            // Companies with a PrefabRef, for CompanyPriority to read each one's
            // IndustrialProcessData.m_Output.m_Resource - the same field the game's own company
            // count job reads to bucket a company by what it sells or makes.
            _commercialCompanyQuery = GetEntityQuery(
                ComponentType.ReadOnly<Game.Companies.CommercialCompany>(),
                ComponentType.ReadOnly<PrefabRef>());

            _industrialCompanyQuery = GetEntityQuery(
                ComponentType.ReadOnly<Game.Companies.IndustrialCompany>(),
                ComponentType.ReadOnly<PrefabRef>());

            // StuckMovingObjectSystem's own query has no Car requirement: Blocker means "stuck"
            // for anything that follows a path, and that includes pedestrians and animals, not
            // only vehicles - a stuck pedestrian queue is not a traffic jam. Car narrows this back
            // down to what "traffic" actually means, at the cost of trains, trams, ships and
            // aircraft, which do not jam a road the way a stopped car does either.
            _jamQuery = GetEntityQuery(
                ComponentType.ReadOnly<Game.Vehicles.Blocker>(),
                ComponentType.ReadOnly<Game.Vehicles.Car>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<Game.Common.Deleted>(),
                ComponentType.Exclude<Game.Tools.Temp>());

            // Every mode in one query: PublicTransport marks the role, not the movement, so a
            // bus, a tram, a metro and a ferry all arrive here. No Blocker filter, unlike the jam
            // query above - see StoppedTransitList for which kinds the game answers for.
            _transitQuery = GetEntityQuery(
                ComponentType.ReadOnly<Game.Vehicles.PublicTransport>(),
                ComponentType.ReadOnly<Game.Objects.Transform>(),
                ComponentType.Exclude<Game.Common.Deleted>(),
                ComponentType.Exclude<Game.Tools.Temp>());

            // Deleted excluded so a vehicle already on its way out is not still counted as
            // carrying people, the same exclusion the jam query makes.
            _passengerVehicleQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Game.Vehicles.PublicTransport>(),
                    ComponentType.ReadOnly<PrefabRef>()
                },
                None = new[] { ComponentType.ReadOnly<Game.Common.Deleted>(), ComponentType.ReadOnly<Game.Tools.Temp>() }
            });

            _taxiQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Game.Vehicles.Taxi>(),
                    ComponentType.ReadOnly<PrefabRef>()
                },
                None = new[] { ComponentType.ReadOnly<Game.Common.Deleted>(), ComponentType.ReadOnly<Game.Tools.Temp>() }
            });

            _deliveryTruckQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Game.Vehicles.DeliveryTruck>(),
                    ComponentType.ReadOnly<PrefabRef>()
                },
                None = new[] { ComponentType.ReadOnly<Game.Common.Deleted>(), ComponentType.ReadOnly<Game.Tools.Temp>() }
            });

            _cargoVehicleQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Game.Vehicles.CargoTransport>(),
                    ComponentType.ReadOnly<PrefabRef>()
                },
                None = new[] { ComponentType.ReadOnly<Game.Common.Deleted>(), ComponentType.ReadOnly<Game.Tools.Temp>() }
            });

            _camera = World.GetOrCreateSystemManaged<Game.Rendering.CameraUpdateSystem>();
            _names = World.GetOrCreateSystemManaged<Game.UI.NameSystem>();
            _terrain = World.GetOrCreateSystemManaged<Game.Simulation.TerrainSystem>();
            _census = World.GetOrCreateSystemManaged<CitizenCensusSystem>();

            // Building required, matching Game.Buildings.InitializeSchoolSystem's own
            // m_CreatedSchoolQuery exactly - without it this query could also pick up a School
            // component sitting on something that is not the building itself, which is where a
            // missing Transform (see SchoolBreakdown.HasPosition) most plausibly comes from.
            _schoolQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Game.Buildings.School>(),
                    ComponentType.ReadOnly<Game.Buildings.Building>(),
                    ComponentType.ReadOnly<Game.Buildings.Student>(),
                    ComponentType.ReadOnly<PrefabRef>()
                },
                None = new[] { ComponentType.ReadOnly<Game.Common.Deleted>(), ComponentType.ReadOnly<Game.Tools.Temp>() }
            });

            // Building required for the same reason the school query requires it: it keeps the
            // query to the facility itself rather than anything else carrying the component.
            _cemeteryQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Game.Buildings.DeathcareFacility>(),
                    ComponentType.ReadOnly<Game.Buildings.Building>(),
                    ComponentType.ReadOnly<PrefabRef>()
                },
                None = new[] { ComponentType.ReadOnly<Game.Common.Deleted>(), ComponentType.ReadOnly<Game.Tools.Temp>() }
            });

            // NotificationIconDisplayData, not NotificationIconData: the first is the enableable
            // component the buffer system checks, the second is something else entirely. And
            // IgnoreComponentEnabledState, or once hidden the prefabs drop out of the query and
            // there is no way to bring them back.
            _iconVisibility = new Notifications.NotificationIconVisibility(
                EntityManager,
                GetEntityQuery(new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<NotificationIconDisplayData>() },
                    Options = EntityQueryOptions.IgnoreComponentEnabledState
                }),
                // Only visible icons are ours to hide. Restoration uses the recorded entities.
                GetEntityQuery(new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<Game.Notifications.Icon>() },
                    None = new[] { ComponentType.ReadOnly<Game.Tools.Hidden>() }
                }));

            // Without this the settings have no way to reach the strip.
            Mod.RegisterUISystem(this);

            _cityForFunds = World.GetOrCreateSystemManaged<CitySystem>();

            RebuildActiveVitals();

            _vitalsBinding = new RawValueBinding(Group, "vitals", WriteVitals);
            AddBinding(_vitalsBinding);

            var settings = Mod.Settings;

            // Seeded from the stored setting, not hardcoded true. LoadSettings runs before this
            // system exists, so the ShowStrip setter's notification finds no UI system to tell
            // and is dropped: a player who switched the strip off in the options had it back on
            // every restart until they toggled it twice. Same shape as posX/posY below.
            _visibleBinding = new ValueBinding<bool>(Group, "visible",
                settings == null || settings.ShowStrip);
            AddBinding(_visibleBinding);

            // Seeded from the stored setting rather than from a hardcoded default - see the note
            // on the visible binding above, which is the same mistake this avoids.
            _iconOutlineBinding = new ValueBinding<bool>(Group, "iconOutline",
                settings == null || settings.IconOutline);
            AddBinding(_iconOutlineBinding);

            _gameButtonStyleBinding = new ValueBinding<bool>(Group, "gameButtonStyle",
                settings != null && settings.GameButtonStyle);
            AddBinding(_gameButtonStyleBinding);

            _toolbarTrendsBinding = new ValueBinding<bool>(Group, "toolbarTrends",
                settings != null && settings.ToolbarTrends);
            AddBinding(_toolbarTrendsBinding);

            _posXBinding = new ValueBinding<int>(Group, "posX",
                settings == null ? Settings.SeetySettings.DefaultStripX : settings.StripX);
            _posYBinding = new ValueBinding<int>(Group, "posY",
                settings == null ? Settings.SeetySettings.DefaultStripY : settings.StripY);
            AddBinding(_posXBinding);
            AddBinding(_posYBinding);

            AddBinding(new TriggerBinding<int, int>(Group, "setPosition", OnPositionChanged));

            _notificationsBinding = new RawValueBinding(Group, "notifications", WriteNotifications);
            AddBinding(_notificationsBinding);

            _resourcesBinding = new RawValueBinding(Group, "resources", WriteResources);
            AddBinding(_resourcesBinding);

            AddBinding(new TriggerBinding<string>(Group, "jumpToProblem", OnJumpToProblem));
            AddBinding(new TriggerBinding<string>(Group, "jumpToJam", OnJumpToJam));

            _transitModeBinding = new ValueBinding<bool>(Group, "transitMode", false);
            AddBinding(_transitModeBinding);
            AddBinding(new TriggerBinding<bool>(Group, "setTransitMode", OnSetTransitMode));
            AddBinding(new TriggerBinding<int>(Group, "jumpToSchool", OnJumpToSchool));
            AddBinding(new TriggerBinding<int>(Group, "jumpToCemetery", OnJumpToCemetery));
            AddBinding(new TriggerBinding<string, int>(Group, "jumpToResource", OnJumpToResource));
            AddBinding(new TriggerBinding<string>(Group, "openInfoview", OnOpenInfoview));

            _configModeBinding = new ValueBinding<bool>(Group, "configMode", false);
            AddBinding(_configModeBinding);
            AddBinding(new TriggerBinding<bool>(Group, "setConfigMode", OnSetConfigMode));
            AddBinding(new TriggerBinding<string>(Group, "toggleVital", OnToggleVital));

            _iconsHiddenBinding = new ValueBinding<bool>(Group, "iconsHidden", false);
            AddBinding(_iconsHiddenBinding);
            AddBinding(new TriggerBinding<bool>(Group, "setIconsHidden", OnSetIconsHidden));

            _journeyOnBinding = new ValueBinding<bool>(Group, "journeyOn", false);
            AddBinding(_journeyOnBinding);
            _journeyBinding = new RawValueBinding(Group, "journey", WriteJourney);
            AddBinding(_journeyBinding);
            AddBinding(new TriggerBinding<bool>(Group, "setJourneyOn", OnSetJourneyOn));
            AddBinding(new TriggerBinding<string>(Group, "openJourneyLine", OnOpenJourneyLine));
            AddBinding(new TriggerBinding<string>(Group, "flyToJourneyPlace", OnFlyToJourneyPlace));

            _historyBinding = new RawValueBinding(Group, "history", WriteHistory);
            AddBinding(_historyBinding);

            _workforceBinding = new RawValueBinding(Group, "workforce", WriteWorkforce);
            AddBinding(_workforceBinding);

            _demographicsBinding = new RawValueBinding(Group, "demographics", WriteDemographics);
            AddBinding(_demographicsBinding);

            AddBinding(new TriggerBinding<string>(Group, "expand", OnExpand));

            // The UI sends back the id of the vital that was clicked.
            AddBinding(new TriggerBinding<string>(Group, "activate", OnVitalActivated));
            AddBinding(new TriggerBinding<bool>(Group, "setVisible", OnSetVisible));
        }

        /// <summary>
        /// Adds (or, given a negative amount, removes) money from the city treasury.
        ///
        /// The one place in Seety that writes to the save - see the note on SeetySettings.AddFunds,
        /// which is the only caller. PlayerMoney lives on the city entity as a plain component,
        /// not behind a system method, so this is a direct read-modify-write: no Harmony, no job,
        /// the same EntityManager access every other write in this file already uses.
        /// </summary>
        public void AddFunds(int amount)
        {
            try
            {
                var city = _cityForFunds == null ? Entity.Null : _cityForFunds.City;
                if (city == Entity.Null || !EntityManager.HasComponent<PlayerMoney>(city))
                {
                    Mod.Log.Info("No city loaded; ignoring the funds request.");
                    return;
                }

                var money = EntityManager.GetComponentData<PlayerMoney>(city);
                money.Add(amount);
                EntityManager.SetComponentData(city, money);

                Mod.Log.Info("Added " + amount + " to the city treasury from the options page.");
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Could not change the city treasury.");
            }
        }

        /// <summary>Called when the settings change which vitals are shown, or in what order.</summary>
        public void RebuildActiveVitals()
        {
            _active.Clear();
            _problemsActive = false;
            _transportActive = false;

            var settings = Mod.Settings;
            foreach (var vital in Vitals.VitalCatalog.All())
            {
                // In configuration mode every row is live, so the player sees real numbers while
                // choosing. Otherwise only what they picked is read at all.
                if (_configMode || settings == null || settings.IsVitalEnabled(vital.Id))
                {
                    _active.Add(vital);

                    if (vital.Source == Vitals.VitalSource.Problems)
                    {
                        _problemsActive = true;
                    }
                    else if (vital.Source == Vitals.VitalSource.Transport)
                    {
                        _transportActive = true;
                    }
                }
            }

            SortByGameOrder();

            _nextRefresh = 0.0;

            if (_vitalsBinding != null)
            {
                _vitalsBinding.Update();
            }
        }

        protected override void OnDestroy()
        {
            if (_iconVisibility != null)
            {
                _iconVisibility.Restore();
            }

            base.OnDestroy();
        }

        /// <summary>
        /// Puts the rows in the order the game itself uses for its infoview menu.
        ///
        /// `InfoviewPrefab` carries `m_Group` and `m_Priority`, which is exactly how the game
        /// sorts that menu, so a player who knows where electricity sits in the vanilla panel
        /// finds it in the same place here. Rows with no infoview - Problems, Workers, Tourists -
        /// keep their catalogue position at the front, because Problems belongs first and the
        /// rest have nothing to sort against.
        /// </summary>
        private void SortByGameOrder()
        {
            if (!_infoviewsResolved || _infoviewOrder.Count == 0)
            {
                return;
            }

            var catalogue = new Dictionary<string, int>();
            var index = 0;
            foreach (var vital in Vitals.VitalCatalog.All())
            {
                catalogue[vital.Id] = index++;
            }

            _active.Sort(delegate(Vitals.Vital a, Vitals.Vital b)
            {
                var orderA = OrderOf(a);
                var orderB = OrderOf(b);

                if (orderA != orderB)
                {
                    return orderA.CompareTo(orderB);
                }

                return catalogue[a.Id].CompareTo(catalogue[b.Id]);
            });
        }

        /// <summary>The game's own sort key for a row, or a value that keeps it at the front.</summary>
        private long OrderOf(Vitals.Vital vital)
        {
            foreach (var name in vital.Infoviews)
            {
                long order;
                if (_infoviewOrder.TryGetValue(name, out order))
                {
                    return order;
                }
            }

            return long.MinValue;
        }

        /// <summary>Called by the settings when the bottom-bar trends are switched on or off.</summary>
        public void SetToolbarTrends(bool show)
        {
            if (_toolbarTrendsBinding != null)
            {
                _toolbarTrendsBinding.Update(show);
            }
        }

        /// <summary>Called by the settings when the icon outline is switched on or off.</summary>
        public void SetIconOutline(bool outlined)
        {
            if (_iconOutlineBinding != null)
            {
                _iconOutlineBinding.Update(outlined);
            }
        }

        /// <summary>Called by the settings when the game-button style is switched on or off.</summary>
        public void SetGameButtonStyle(bool gameStyle)
        {
            if (_gameButtonStyleBinding != null)
            {
                _gameButtonStyleBinding.Update(gameStyle);
            }
        }

        public void SetVisible(bool visible)
        {
            if (_visibleBinding != null)
            {
                _visibleBinding.Update(visible);
            }

            if (!visible && !string.IsNullOrEmpty(_expandedId))
            {
                OnExpand(string.Empty);
            }
            _nextRefresh = 0.0;
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            var now = UnityEngine.Time.realtimeSinceStartupAsDouble;
            if (_journeyOnBinding.value)
            {
                var selected = JourneySelection();
                if (selected != _lastSelected || now >= _nextJourneyRefresh)
                {
                    RefreshJourney(selected);
                    _nextJourneyRefresh = now + 0.5;
                }
            }
            // Catch new notification icons independently of the five-second readings refresh,
            // even when the strip itself is hidden.
            if (now >= _nextIconRefresh)
            {
                _nextIconRefresh = now + 0.25;
                _iconVisibility.KeepUp();
            }
            if (now < _nextRefresh)
            {
                return;
            }

            _nextRefresh = now + RefreshIntervalSeconds;

            // Do not scan the city for readings when the HUD is hidden.
            if (!_visibleBinding.value)
            {
                return;
            }

            if (ReadValues())
            {
                _vitalsBinding.Update();
            }

            if (!string.IsNullOrEmpty(_expandedId))
            {
                _historyBinding.Update();

                if (NeedsCensus)
                {
                    RefreshWorkforce();
                }
            }
        }

        /// <summary>Reads every active vital. Returns true when anything actually changed.</summary>
        private bool ReadValues()
        {
            var changed = false;

            // Cheap once it has succeeded, and retried until the prefabs exist, so the strip
            // knows which entries are genuinely clickable rather than assuming they all are.
            ResolveInfoviews();

            var hasCitizens = _reader.HasCitizens;
            if (hasCitizens != _hasCitizens)
            {
                _hasCitizens = hasCitizens;
                changed = true;
            }

            if (_problemsActive)
            {
                var before = _reader.ProblemLevel;
                _reader.RefreshProblems(_iconQuery);
                if (_reader.ProblemLevel != before)
                {
                    changed = true;
                }

                // The count above is the strip's own number, so it stays current whatever is on
                // screen. The grouped list behind it is a different matter: it walks the same
                // icons a second time and records a position for every notification in the city,
                // then reserialises the whole notifications payload. Doing that twice a second
                // for a window nobody has opened was the strip's largest standing cost, and the
                // Problems row is on by default, so every player paid it for the whole game.
                if (_expandedId == ProblemsVitalId)
                {
                    _breakdown.Refresh(_iconQuery, _prefabs);
                    _notificationsBinding.Update();
                }
            }

            if (_expandedId == DemandVitalId)
            {
                RefreshResources();
                _resourcesBinding.Update();
            }

            if (_expandedId == TrafficVitalId)
            {
                // Only the list on screen is read. Scanning both every five seconds to keep the
                // hidden one warm would double the cost of the window for a list nobody is
                // looking at, which is the mistake the problems list already made once.
                if (_transitModeBinding.value)
                {
                    _transit.Refresh(_transitQuery, EntityManager, _names);
                }
                else
                {
                    _jams.Refresh(_jamQuery, EntityManager, _names, _terrain);
                }

                _notificationsBinding.Update();
            }

            // The school list is deliberately NOT refreshed here, and that is not an oversight.
            //
            // It was, for one round, on the grounds that every other list in this group updates
            // live. It broke the window twice over. The rows are sorted by fullness and fullness
            // moves constantly, so they reordered under the pointer; worse, a row's id carries
            // its student count and the UI keys on that id, so every refresh changed the key and
            // React threw the row away and built a new one. A click needs its mousedown and its
            // mouseup on the same node, and there was no longer any such node - clicking a school
            // did nothing whatsoever, silently, because no trigger was ever raised.
            //
            // A snapshot taken when the window opens is what the player wants anyway: a list you
            // are working through should hold still. RefreshSchools stays on OnExpand only.

            if (_transportActive)
            {
                // One pass over the transit vehicles - bounded by how many are running, not by
                // how big the city is - so the strip's own total can stay current.
                var before = _transport.PassengerTotal;
                _transport.Refresh(_passengerVehicleQuery, _taxiQuery, _cargoVehicleQuery,
                    _deliveryTruckQuery, EntityManager);
                if (_transport.PassengerTotal != before)
                {
                    changed = true;
                }

                // Pushing them to the UI is not cheap - it rewrites every breakdown in the group
                // - so that only happens while the window showing them is open.
                if (_expandedId == TransportVitalId)
                {
                    _notificationsBinding.Update();
                }
            }

            foreach (var vital in _active)
            {
                if (vital.Source == Vitals.VitalSource.Vanilla)
                {
                    continue;
                }

                float value;
                try
                {
                    value = vital.Source == Vitals.VitalSource.Transport
                        ? _transport.PassengerTotal
                        : _reader.Read(vital.Source);
                }
                catch (Exception e)
                {
                    // A vital that is not available yet must not take the strip down.
                    Mod.Log.Warn("Could not read " + vital.Id + ": " + e.Message);
                    value = 0f;
                }

                float previous;
                if (!_values.TryGetValue(vital.Id, out previous) || previous != value)
                {
                    _values[vital.Id] = value;
                    changed = true;
                }
            }

            return changed;
        }

        private void WriteVitals(IJsonWriter writer)
        {
            writer.ArrayBegin((uint)_active.Count);

            foreach (var vital in _active)
            {
                WriteVital(writer, vital, true);
            }

            writer.ArrayEnd();
        }

        /// <summary>
        /// One reading. Written for the bar, and again for each reading folded into a bar row's
        /// window - see Vital.Companions.
        /// </summary>
        /// <param name="withCompanions">
        /// False for a companion itself. Nesting stops at one level: a companion's own companion
        /// list would have nowhere to be shown, so it is always sent empty rather than walked.
        /// </param>
        private void WriteVital(IJsonWriter writer, Vitals.Vital vital, bool withCompanions)
        {
            float value;
            _values.TryGetValue(vital.Id, out value);

            writer.TypeBegin("seety.Vital");
            writer.PropertyName("id");
            writer.Write(vital.Id);
            writer.PropertyName("title");
            writer.Write(vital.Title);
            writer.PropertyName("label");
            writer.Write(vital.Label);
            // The English above travels with the row as the fallback, and these name the entry to
            // look up instead. A language missing one string therefore shows that row in English
            // rather than blank - see Localization.LocaleKeys.
            writer.PropertyName("titleKey");
            writer.Write(Localization.LocaleKeys.VitalTitle(vital.Id));
            writer.PropertyName("labelKey");
            writer.Write(Localization.LocaleKeys.VitalLabel(vital.Id));
            writer.PropertyName("icon");
            writer.Write(IconFor(vital, value));
            writer.PropertyName("badge");
            writer.Write(vital.Badge ?? string.Empty);
            writer.PropertyName("value");
            writer.Write(value);
            writer.PropertyName("format");
            writer.Write((int)vital.Format);
            writer.PropertyName("level");
            writer.Write((int)Evaluate(vital, value));
            writer.PropertyName("clickable");
            writer.Write(ResolveEntity(vital) != Entity.Null);

            // Service and hazard rows get their number from vanilla's own bindings, which
            // only the UI can subscribe to. Send it what it needs to do that, plus the
            // threshold, since the value it judges never passes through here.
            var binding = vital.Binding;
            writer.PropertyName("bindGroup");
            writer.Write(binding == null ? string.Empty : binding.Group);
            writer.PropertyName("bindSupply");
            writer.Write(binding == null ? string.Empty : binding.Supply);
            writer.PropertyName("bindDemand");
            writer.Write(binding == null ? string.Empty : binding.Demand);
            writer.PropertyName("bindSupply2");
            writer.Write(binding == null ? string.Empty : binding.Supply2);
            writer.PropertyName("bindDemand2");
            writer.Write(binding == null ? string.Empty : binding.Demand2);
            writer.PropertyName("bindKind");
            writer.Write(binding == null ? 0 : (int)binding.Kind);

            var threshold = vital.Threshold;
            writer.PropertyName("warning");
            writer.Write(threshold == null ? 0f : threshold.Warning);
            writer.PropertyName("critical");
            writer.Write(threshold == null ? 0f : threshold.Critical);
            writer.PropertyName("lowIsBad");
            writer.Write(threshold != null && threshold.LowIsBad);
            writer.PropertyName("hasThreshold");
            writer.Write(threshold != null && _hasCitizens && HighlightingEnabled());
            writer.PropertyName("hasHistory");
            writer.Write(vital.History.HasValue);
            writer.PropertyName("invert");
            writer.Write(vital.Invert);
            writer.PropertyName("factors");
            writer.Write(vital.Factors ?? string.Empty);
            // The game unit this reading is in, or empty for a plain count. See Vital.Unit.
            writer.PropertyName("unit");
            writer.Write(vital.Unit ?? string.Empty);
            writer.PropertyName("enabled");
            writer.Write(Mod.Settings == null || Mod.Settings.IsVitalEnabled(vital.Id));

            var companions = withCompanions ? vital.Companions : EmptyCompanions;
            writer.PropertyName("companions");
            writer.ArrayBegin((uint)companions.Length);
            foreach (var companion in companions)
            {
                WriteVital(writer, companion, false);
            }
            writer.ArrayEnd();

            writer.TypeEnd();
        }

        private static readonly Vitals.Vital[] EmptyCompanions = new Vitals.Vital[0];

        /// <summary>Vanilla's own warning triangle, used only when there is something to warn about.</summary>
        private const string WarningIcon = "Media/Misc/Warning.svg";

        /// <summary>
        /// Problems wears a warning triangle while the count is not zero, and its ordinary icon
        /// when the city is quiet. A row that always looks like an alarm stops being read as one.
        /// </summary>
        private static string IconFor(Vitals.Vital vital, float value)
        {
            if (vital.Source == Vitals.VitalSource.Problems && value > 0f)
            {
                return WarningIcon;
            }

            return vital.Icon ?? string.Empty;
        }

        private bool HighlightingEnabled()
        {
            var settings = Mod.Settings;
            return settings == null || settings.HighlightProblems;
        }

        /// <summary>
        /// Turns a reading into a severity. This is where the design thesis pays off: there is no
        /// separate alert list, only vitals that happen to carry a threshold.
        /// </summary>
        private Vitals.VitalLevel Evaluate(Vitals.Vital vital, float value)
        {
            var settings = Mod.Settings;
            if (settings != null && !settings.HighlightProblems)
            {
                return Vitals.VitalLevel.Normal;
            }

            // Problems grades itself from the game's own IconPriority scale rather than from a
            // threshold of ours, and it is meaningful before anyone moves in - a road laid
            // wrong is a problem in an empty city too.
            if (vital.Source == Vitals.VitalSource.Problems)
            {
                return _reader.ProblemLevel;
            }

            if (vital.Threshold == null || !_hasCitizens)
            {
                return Vitals.VitalLevel.Normal;
            }

            return vital.Threshold.Evaluate(value);
        }

        /// <summary>
        /// The grouped problem list: one row per notification type, worst and largest first.
        /// This is what the Problems entry expands into - the total says something is wrong, the
        /// list says what.
        /// </summary>
        private void WriteNotifications(IJsonWriter writer)
        {
            // One entry per expandable row. Keyed by vital id so the UI needs no table of its
            // own: adding another expandable row later means writing another entry here.
            var schools = _schools.Entries;
            // An open school panel needs an empty array too, so the UI can explain an empty list.
            var hasSchools = Vitals.SchoolBreakdown.LevelFor(_expandedId) >= 0;
            var cemeteries = _cemeteries.Entries;
            var hasCemeteries = _expandedId == CemeteryVitalId;
            var jams = _jams.Groups;

            // Keyed on the row being OPEN, not on the list happening to have something in it.
            // Gated the other way round, a city with no jams produced no traffic block at all,
            // and the window opened with nothing in it - not even the "Nothing to report" line,
            // because the component that draws that was never reached. It also left the last
            // session's jams sitting in the payload after the window was closed.
            var hasJams = _expandedId == TrafficVitalId;

            var count = (uint)((_problemsActive ? 1 : 0) + (_transportActive ? 1 : 0)
                + (hasSchools ? 1 : 0) + (hasJams ? 1 : 0) + (hasCemeteries ? 1 : 0));
            writer.ArrayBegin(count);

            if (_problemsActive)
            {
                writer.TypeBegin("seety.Breakdown");
                writer.PropertyName("id");
                writer.Write(ProblemsVitalId);
                writer.PropertyName("rows");
                WriteProblemRows(writer);
                writer.TypeEnd();
            }

            if (hasJams)
            {
                writer.TypeBegin("seety.Breakdown");
                writer.PropertyName("id");
                writer.Write(TrafficVitalId);
                writer.PropertyName("rows");
                WriteJamRows(writer, _transitModeBinding.value ? _transit.Groups : jams);
                writer.TypeEnd();
            }

            if (_transportActive)
            {
                writer.TypeBegin("seety.Breakdown");
                writer.PropertyName("id");
                writer.Write(TransportVitalId);
                writer.PropertyName("rows");
                WriteTransportRows(writer);
                writer.TypeEnd();
            }

            if (hasCemeteries)
            {
                writer.TypeBegin("seety.Breakdown");
                writer.PropertyName("id");
                writer.Write(CemeteryVitalId);
                writer.PropertyName("rows");
                WriteCemeteryRows(writer, cemeteries);
                writer.TypeEnd();
            }

            if (hasSchools)
            {
                writer.TypeBegin("seety.Breakdown");
                writer.PropertyName("id");
                writer.Write(_expandedId);
                writer.PropertyName("rows");
                WriteSchoolRows(writer, schools);
                writer.TypeEnd();
            }

            writer.ArrayEnd();
        }

        /// <summary>
        /// The schools of the open level, fullest first. The row carries the school's entity id,
        /// because two schools can share a name and the list re-sorts underneath the player.
        /// </summary>
        private void WriteSchoolRows(IJsonWriter writer, System.Collections.Generic.IReadOnlyList<Vitals.SchoolEntry> schools)
        {
            writer.ArrayBegin((uint)schools.Count);

            for (var i = 0; i < schools.Count; i++)
            {
                var school = schools[i];

                var level = Vitals.VitalLevel.Normal;
                if (school.Fullness >= 95f)
                {
                    level = Vitals.VitalLevel.Critical;
                }
                else if (school.Fullness >= 80f)
                {
                    level = Vitals.VitalLevel.Warning;
                }

                // Through the shared writer, not a second copy of it. There used to be two
                // places emitting seety.BreakdownRow and they disagreed on the order of the
                // fields: schools wrote suffix, level, clickable where everything else wrote
                // level, clickable, suffix. A row decoded by position therefore took the numeric
                // level as its `clickable` flag - and VitalLevel.Normal is 0, which is false in
                // JavaScript. Every school under 80% full was silently unclickable, with no
                // trigger raised and nothing in the log to say so, while the fuller ones worked.
                // Invisible on screen too, because the UI hardcodes "%" for these rows and
                // colours the number from its own value rather than from the level.
                WriteRow(writer, school.Name + "  " + school.Students + "/" + school.Capacity,
                    "Media/Game/Icons/Education.svg", (int)school.Fullness, level,
                    // A school with nowhere for the camera to go - see SchoolEntry.HasPosition.
                    school.HasPosition,
                    // The entity id, not the loop counter. See SchoolEntry.Entity.
                    "school:" + school.Entity.Index,
                    "%");
            }

            writer.ArrayEnd();
        }

        /// <summary>
        /// The city's cemeteries, fullest first. Same shape as the school list, and through the
        /// same shared row writer - see the note in WriteSchoolRows about why there is only one.
        /// </summary>
        private static void WriteCemeteryRows(IJsonWriter writer,
            IReadOnlyList<Vitals.CemeteryEntry> cemeteries)
        {
            writer.ArrayBegin((uint)cemeteries.Count);

            foreach (var cemetery in cemeteries)
            {
                var level = Vitals.VitalLevel.Normal;
                if (cemetery.Fullness >= 95f)
                {
                    level = Vitals.VitalLevel.Critical;
                }
                else if (cemetery.Fullness >= 80f)
                {
                    level = Vitals.VitalLevel.Warning;
                }

                WriteRow(writer, cemetery.Name + "  " + cemetery.Stored + "/" + cemetery.Capacity,
                    "Media/Game/Notifications/HearseServiceNeeded.svg", (int)cemetery.Fullness,
                    level, cemetery.HasPosition, "cemetery:" + cemetery.Entity.Index, "%");
            }

            writer.ArrayEnd();
        }

        /// <summary>
        /// The grouped problem list: one row per notification type, worst and largest first.
        /// This is what the Problems entry expands into - the total says something is wrong, the
        /// list says what.
        /// </summary>
        private void WriteProblemRows(IJsonWriter writer)
        {
            var groups = _breakdown.Groups;

            // Only the worst handful. A long list is slow to build, slow to draw, and past the
            // first few rows it stops being something you act on.
            var shown = groups.Count < MaxProblemRows ? groups.Count : MaxProblemRows;

            writer.ArrayBegin((uint)shown);

            for (var i = 0; i < shown; i++)
            {
                var group = groups[i];
                WriteRow(writer, group.Id, group.Icon, group.Count, group.Level, true, "jump");
            }

            writer.ArrayEnd();
        }

        /// <summary>
        /// The city's jams behind the traffic row, worst first. One icon for all of them
        /// - the traffic row's own, already verified - since there is no reliable way to find a
        /// per-vehicle-kind icon file the way a notification's name can be turned into one.
        /// </summary>
        private static void WriteJamRows(IJsonWriter writer, System.Collections.Generic.IReadOnlyList<Vitals.TrafficJamGroup> jams)
        {
            writer.ArrayBegin((uint)jams.Count);

            foreach (var group in jams)
            {
                // One number, because there is only one thing to say: how many vehicles are
                // stuck in the place this row flies to.
                WriteRow(writer, group.Name, "Media/Game/Icons/Traffic.svg", group.Count,
                    Vitals.VitalLevel.Normal, true, "jam:" + group.Id);
            }

            writer.ArrayEnd();
        }

        /// <summary>The vital whose window carries the per-resource tables.</summary>
        private const string DemandVitalId = "demand";

        /// <summary>The vital whose window carries the stuck-vehicle list. Matches TRAFFIC_ID.</summary>
        private const string TrafficVitalId = "traffic";

        /// <summary>The vital whose window carries the grouped problem list.</summary>
        private const string ProblemsVitalId = "problems";

        /// <summary>The vital whose window lists the city's cemeteries. Matches CEMETERY_ID.</summary>
        private const string CemeteryVitalId = "cemetery";

        /// <summary>The vital whose window carries the per-mode transport list.</summary>
        private const string TransportVitalId = "transport";

        /// <summary>
        /// Rereads both resource tables.
        ///
        /// Only while the demand window is open. Each read completes a simulation job handle, and
        /// doing that on every refresh for a window nobody has opened would make the whole strip
        /// pay for a table almost no one is looking at.
        /// </summary>
        private void RefreshResources()
        {
            var companies = World.GetOrCreateSystemManaged<CountCompanyDataSystem>();
            var industrialDemand = World.GetOrCreateSystemManaged<IndustrialDemandSystem>();

            _shops.RefreshCommercial(
                World.GetOrCreateSystemManaged<CommercialDemandSystem>(), companies,
                _commercialCompanyQuery, EntityManager, _names);

            _factories.RefreshIndustrial(
                industrialDemand, companies, _industrialCompanyQuery, EntityManager, _names);

            // Same company query as _factories - see the note on _offices. Office is a resource
            // filter over industrial's own data, not a company kind of its own.
            _offices.RefreshOffice(
                industrialDemand, companies, _industrialCompanyQuery, EntityManager, _names);
        }

        /// <summary>
        /// All three resource tables, keyed so the UI can show whichever demand row was opened.
        /// </summary>
        private void WriteResources(IJsonWriter writer)
        {
            writer.TypeBegin("seety.Resources");
            writer.PropertyName("commercial");
            WriteResourceRows(writer, _shops.Entries);
            writer.PropertyName("industrial");
            WriteResourceRows(writer, _factories.Entries);
            writer.PropertyName("office");
            WriteResourceRows(writer, _offices.Entries);
            writer.TypeEnd();
        }

        private static void WriteResourceRows(IJsonWriter writer,
            IReadOnlyList<Vitals.ResourceEntry> rows)
        {
            writer.ArrayBegin((uint)rows.Count);

            foreach (var row in rows)
            {
                writer.TypeBegin("seety.ResourceRow");
                writer.PropertyName("name");
                writer.Write(row.Name);
                writer.PropertyName("demand");
                writer.Write(row.Demand);
                writer.PropertyName("companies");
                writer.Write(row.Companies);
                writer.PropertyName("noPremises");
                writer.Write(row.NoPremises);
                writer.PropertyName("stock");
                writer.Write(row.Stock);
                writer.PropertyName("staff");
                writer.Write(row.Staff);
                writer.PropertyName("resourceIndex");
                writer.Write(row.ResourceIndex);
                writer.PropertyName("priorityName");
                writer.Write(row.PriorityName ?? string.Empty);
                writer.PropertyName("priorityReason");
                writer.Write(row.PriorityReason ?? string.Empty);
                writer.TypeEnd();
            }

            writer.ArrayEnd();
        }

        /// <summary>Passengers first, then freight. Modes nobody uses are left out.</summary>
        private void WriteTransportRows(IJsonWriter writer)
        {
            var rows = new List<Vitals.TransportMode>();

            // A mode with vehicles but nobody aboard still belongs in the list - an empty line is
            // exactly the thing worth seeing. A mode with no vehicles at all does not.
            foreach (var mode in _transport.Passengers)
            {
                if (mode.Capacity > 0)
                {
                    rows.Add(mode);
                }
            }

            foreach (var mode in _transport.Cargo)
            {
                if (mode.Capacity > 0)
                {
                    rows.Add(mode);
                }
            }

            writer.ArrayBegin((uint)rows.Count);

            foreach (var mode in rows)
            {
                // Aboard right now against what the running vehicles could hold. Sent as two raw
                // numbers plus the unit they are in, rather than pasted into the label here:
                // freight is counted in kilograms, so a cargo train read "3476687" until the UI
                // was given what it needed to say "3,477 t".
                WriteRow(writer, mode.Id, mode.Icon, mode.Aboard, Vitals.VitalLevel.Normal,
                    !string.IsNullOrEmpty(mode.Action), mode.Action, string.Empty,
                    mode.Capacity, mode.Cargo == Vitals.CargoKind.None ? string.Empty : "weight");
            }

            writer.ArrayEnd();
        }

        /// <summary>
        /// One row of an expandable list. <paramref name="action"/> tells the UI what a click
        /// means: "jump" moves the camera to the next one of these, "passenger" and "cargo"
        /// pre-select that mode in vanilla's transportation overview, empty means not clickable.
        /// </summary>
        private static void WriteRow(IJsonWriter writer, string id, string icon, int count,
            Vitals.VitalLevel level, bool clickable, string action = "", string suffix = "",
            int total = 0, string unit = "")
        {
            writer.TypeBegin("seety.BreakdownRow");
            writer.PropertyName("id");
            writer.Write(id);
            writer.PropertyName("icon");
            writer.Write(icon);
            writer.PropertyName("count");
            writer.Write(count);
            writer.PropertyName("level");
            writer.Write((int)level);
            writer.PropertyName("clickable");
            writer.Write(clickable);
            writer.PropertyName("suffix");
            writer.Write(suffix ?? string.Empty);
            writer.PropertyName("action");
            writer.Write(action);
            // A denominator, when the row has one, and the game unit both numbers are in. The UI
            // converts and formats: the raw values here are the game's own internal units - a
            // tenth of a kilowatt, a kilogram - which mean nothing to a player as they stand.
            writer.PropertyName("total");
            writer.Write(total);
            writer.PropertyName("unit");
            writer.Write(unit ?? string.Empty);
            writer.TypeEnd();
        }

        /// <summary>
        /// The player opened or closed a row. Nothing is fetched for a row nobody is looking at:
        /// reading a series allocates, and the strip is on screen the whole game.
        /// </summary>
        private void OnExpand(string id)
        {
            _expandedId = _visibleBinding.value && !_configMode ? id ?? string.Empty : string.Empty;
            if (_expandedId != TrafficVitalId && _journeyOnBinding.value) OnSetJourneyOn(false);
            _historyBinding.Update();

            if (_expandedId == DemandVitalId)
            {
                RefreshResources();
            }

            if (_resourcesBinding != null)
            {
                _resourcesBinding.Update();
            }

            if (_expandedId == TrafficVitalId)
            {
                _jams.Refresh(_jamQuery, EntityManager, _names, _terrain);
            }

            // Built here as well as on the tick, because the tick only keeps it up to date while
            // the window is already open - opening it has to fill it in the first place.
            if (_expandedId == ProblemsVitalId)
            {
                _breakdown.Refresh(_iconQuery, _prefabs);
            }

            if (NeedsCensus)
            {
                RefreshWorkforce();
            }

            RefreshSchools();
            RefreshCemeteries();
            _notificationsBinding.Update();
        }

        /// <summary>
        /// Rebuilds the school list when an education row is open. Like the census, it runs only
        /// while somebody is looking: it walks every school and asks the naming system for a
        /// label, which is not work to do twice a second for nothing.
        /// </summary>
        /// <summary>
        /// Rebuilds the cemetery list when its row is open. Same gate and same reason as the
        /// school list: it walks every facility and asks the naming system for a label.
        /// </summary>
        private void RefreshCemeteries()
        {
            _cemeteries.Refresh(EntityManager, _cemeteryQuery, _names, _expandedId == CemeteryVitalId);
        }

        private void RefreshSchools()
        {
            var level = Vitals.SchoolBreakdown.LevelFor(_expandedId);
            _schools.Refresh(EntityManager, _schoolQuery, _names, level);
        }

        /// <summary>
        /// Opens a vanilla infoview by name. Rows inside a window - a pollution kind, a parking
        /// mode - are not vitals of their own, so they cannot go through OnVitalActivated.
        /// </summary>
        private void OnOpenInfoview(string name)
        {
            try
            {
                ResolveInfoviews();

                Entity entity;
                if (!_infoviewEntities.TryGetValue(name, out entity))
                {
                    Mod.Log.Info("No infoview named '" + name + "'.");
                    return;
                }

                _infoviews.SetActiveInfoview(entity);
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Could not open the infoview '" + name + "'.");
            }
        }

        /// <summary>Take me to that school. The argument is its entity id - see SchoolEntry.Entity.</summary>
        private void OnJumpToSchool(int index)
        {
            try
            {
                // Logged on both outcomes, temporarily - a school row that a player reports as
                // "does nothing" needs to say whether the click reached here at all, and if it
                // did, exactly why Jump refused it: index out of range, no HasPosition, or no
                // active camera controller are three different problems that all looked
                // identical from the strip.
                var entry = _schools.Find(index);
                var name = entry == null ? "(not in the list)" : entry.Name;
                var hasPosition = entry != null && entry.HasPosition;
                var cameraReady = _camera != null && _camera.activeCameraController != null;

                if (_schools.Jump(index, _camera))
                {
                    Mod.Log.Info("Jumped to school #" + index + " ('" + name + "').");
                }
                else
                {
                    Mod.Log.Info("Nothing to jump to for school #" + index + " ('" + name +
                        "'): entries=" + _schools.Entries.Count +
                        " found=" + (entry != null) +
                        " hasPosition=" + hasPosition + " cameraReady=" + cameraReady + ".");
                }
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Could not jump to a school.");
            }
        }

        /// <summary>Take me to that cemetery. The argument is its entity id.</summary>
        private void OnJumpToCemetery(int index)
        {
            try
            {
                var entry = _cemeteries.Find(index);
                var name = entry == null ? "(not in the list)" : entry.Name;

                if (_cemeteries.Jump(index, _camera))
                {
                    Mod.Log.Info("Jumped to cemetery #" + index + " (" + name + ").");
                }
                else
                {
                    Mod.Log.Info("Nothing to jump to for cemetery #" + index + " (" + name +
                        "): entries=" + _cemeteries.Entries.Count +
                        " found=" + (entry != null) +
                        " hasPosition=" + (entry != null && entry.HasPosition) + ".");
                }
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Could not jump to a cemetery.");
            }
        }

        /// <summary>
        /// Jumps to whichever company is worst off for one resource - see CompanyPriority. All
        /// three breakdowns share the resource index space, since it comes from the same
        /// EconomyUtils.GetResourceIndex either way.
        /// </summary>
        private void OnJumpToResource(string kind, int resourceIndex)
        {
            try
            {
                Vitals.ResourceBreakdown breakdown;
                switch (kind)
                {
                    case "industrial":
                        breakdown = _factories;
                        break;
                    case "office":
                        breakdown = _offices;
                        break;
                    default:
                        breakdown = _shops;
                        break;
                }

                if (!breakdown.Jump(resourceIndex, _camera))
                {
                    Mod.Log.Info("Nothing to jump to for resource " + resourceIndex + " (" + kind + ").");
                }
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Could not jump to a resource's priority company.");
            }
        }

        /// <summary>The row whose window carries the workforce-against-workplaces table.</summary>
        private const string WorkforceVitalId = "workers";

        /// <summary>The row whose window carries the age-and-education breakdown.</summary>
        private const string DemographicsVitalId = "happiness";

        /// <summary>True while a row that needs the citizen census is open.</summary>
        private bool NeedsCensus
        {
            get { return _expandedId == WorkforceVitalId || _expandedId == DemographicsVitalId; }
        }

        /// <summary>How many problem types the list shows before it stops being actionable.</summary>
        private const int MaxProblemRows = 10;

        private void RefreshWorkforce()
        {
            _workforce.Refresh(_census);

            _workforceBinding.Update();
            _demographicsBinding.Update();
        }

        /// <summary>
        /// Who actually lives here: age against education, from the same census the workforce
        /// table uses. Nothing new is counted - the pass already walks every citizen and records
        /// both, so this is the same numbers asked a different question.
        ///
        /// It sits behind Happiness rather than in a row of its own. There is no threshold for
        /// "too many teenagers", so a cell on the strip would carry a number nobody can act on;
        /// but "who are these people" is exactly the question you have when their mood drops.
        /// </summary>
        private void WriteDemographics(IJsonWriter writer)
        {
            var rows = _expandedId == DemographicsVitalId
                ? new[]
                {
                    new[] { "Children", "Children" },
                    new[] { "Teens", "Teens" },
                    new[] { "Adults", "Adults" },
                    new[] { "Seniors", "Seniors" }
                }
                : new string[0][];

            writer.ArrayBegin((uint)rows.Length);

            foreach (var row in rows)
            {
                var field = (Systems.CitizenCensusSystem.Field)System.Enum.Parse(
                    typeof(Systems.CitizenCensusSystem.Field), row[1]);

                writer.TypeBegin("seety.AgeRow");
                writer.PropertyName("age");
                writer.Write(row[0]);
                // Sent, not assembled in the UI from the label above. A key built by
                // concatenation is a key nothing can check, and it breaks silently the day the
                // label changes.
                writer.PropertyName("ageKey");
                writer.Write("Seety.AGE_" + row[0].ToUpperInvariant());

                writer.PropertyName("levels");
                writer.ArrayBegin((uint)Systems.CitizenCensusSystem.Levels);
                for (var level = 0; level < Systems.CitizenCensusSystem.Levels; level++)
                {
                    writer.Write(_census.Get(level, field));
                }
                writer.ArrayEnd();

                writer.TypeEnd();
            }

            writer.ArrayEnd();
        }

        /// <summary>
        /// One row per education level, people and jobs side by side. Written only while the
        /// window is open - it reads two systems and nobody needs it otherwise.
        /// </summary>
        private void WriteWorkforce(IJsonWriter writer)
        {
            var rows = _expandedId == WorkforceVitalId ? _workforce.Rows : new List<Vitals.WorkforceRow>();

            writer.TypeBegin("seety.Workforce");
            writer.PropertyName("rows");
            writer.ArrayBegin((uint)rows.Count);

            foreach (var row in rows)
            {
                writer.TypeBegin("seety.WorkforceRow");
                writer.PropertyName("level");
                writer.Write(row.Level);
                writer.PropertyName("total");
                writer.Write(row.Total);
                writer.PropertyName("children");
                writer.Write(row.Children);
                writer.PropertyName("students");
                writer.Write(row.Students);
                writer.PropertyName("childStudents");
                writer.Write(row.ChildStudents);
                writer.PropertyName("seniors");
                writer.Write(row.Seniors);
                writer.PropertyName("workingAge");
                writer.Write(row.WorkingAge);
                writer.PropertyName("workers");
                writer.Write(row.Workers);
                writer.PropertyName("unemployed");
                writer.Write(row.Unemployed);
                writer.PropertyName("under");
                writer.Write(row.Under);
                writer.PropertyName("outside");
                writer.Write(row.Outside);
                writer.PropertyName("commuters");
                writer.Write(row.Commuters);
                writer.PropertyName("jobs");
                writer.Write(row.Jobs);
                writer.PropertyName("vacant");
                writer.Write(row.Vacant);
                writer.TypeEnd();
            }

            writer.ArrayEnd();
            writer.TypeEnd();
        }

        /// <summary>
        /// The recorded series behind the open row, if it has one.
        ///
        /// Labelled with what it actually counts rather than with the row name: a row can show a
        /// rate while its series counts heads, and a chart that quietly implied otherwise would be
        /// the same mistake as a number that disagrees with the game.
        /// </summary>
        private void WriteHistory(IJsonWriter writer)
        {
            var vital = string.IsNullOrEmpty(_expandedId) ? null : Vitals.VitalCatalog.Find(_expandedId);

            writer.TypeBegin("seety.History");
            writer.PropertyName("label");
            writer.Write(vital != null && vital.History.HasValue ? vital.HistoryLabel ?? string.Empty : string.Empty);
            writer.PropertyName("values");

            if (vital == null || !vital.History.HasValue)
            {
                writer.ArrayBegin(0u);
                writer.ArrayEnd();
                writer.TypeEnd();
                return;
            }

            var values = new List<long>();
            try
            {
                var statistics = World.GetOrCreateSystemManaged<CityStatisticsSystem>();
                using (var series = statistics.GetStatisticDataArrayLong(vital.History.Value))
                {
                    for (var i = 0; i < series.Length; i++)
                    {
                        values.Add(series[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Mod.Log.Warn("Could not read the history for " + vital.Id + ": " + e.Message);
            }

            writer.ArrayBegin((uint)values.Count);
            foreach (var value in values)
            {
                writer.Write(value);
            }
            writer.ArrayEnd();
            writer.TypeEnd();
        }

        /// <summary>
        /// Take me to one of these. Repeat clicks tour the group rather than sitting on the same
        /// building, which is the difference between a list that reports and one you can work
        /// through.
        /// </summary>
        private void OnJumpToProblem(string id)
        {
            try
            {
                if (!_breakdown.Jump(id, _camera))
                {
                    Mod.Log.Info("Nothing to jump to for '" + id + "'.");
                }
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Could not jump to '" + id + "'.");
            }
        }

        /// <summary>Jumps to the grid location selected in the traffic list.</summary>
        /// <summary>
        /// Switches the traffic window between road jams and stopped public transport.
        ///
        /// The lists are cleared rather than kept: the rows carry ids that only the list that
        /// built them can resolve, so a click arriving right after a switch would otherwise be
        /// looked up in the wrong table. Zeroing the refresh clock makes the new list appear at
        /// once instead of up to five seconds later.
        /// </summary>
        private void OnSetTransitMode(bool transit)
        {
            try
            {
                _transitModeBinding.Update(transit);

                if (_expandedId == TrafficVitalId)
                {
                    if (transit)
                    {
                        _transit.Refresh(_transitQuery, EntityManager, _names);
                    }
                    else
                    {
                        _jams.Refresh(_jamQuery, EntityManager, _names, _terrain);
                    }

                    _notificationsBinding.Update();
                }

                _nextRefresh = 0.0;
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Could not switch the traffic list.");
            }
        }

        private void OnJumpToJam(string id)
        {
            try
            {
                if (_transitModeBinding.value)
                {
                    if (!_transit.Jump(id, _camera))
                    {
                        Mod.Log.Info("Nothing to jump to for stopped transit '" + id + "'.");
                    }

                    return;
                }

                if (!_jams.Jump(id, _camera))
                {
                    Mod.Log.Info("Nothing to jump to for jam '" + id + "'.");
                }
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Could not jump to a traffic jam.");
            }
        }

        /// <summary>
        /// The player finished dragging the strip. Stored in the settings so the position
        /// survives a restart - a bar you have to reposition every session is worse than one
        /// that cannot move at all.
        /// </summary>
        private void OnPositionChanged(int x, int y)
        {
            _posXBinding.Update(x);
            _posYBinding.Update(y);

            var settings = Mod.Settings;
            if (settings == null)
            {
                return;
            }

            settings.StripX = x;
            settings.StripY = y;
            settings.ApplyAndSave();
        }

        /// <summary>
        /// Enables journey polling only while the traffic window is being viewed.
        /// </summary>
        private void OnSetJourneyOn(bool on)
        {
            on = on && _visibleBinding.value && !_configMode && _expandedId == TrafficVitalId;
            _journeyOnBinding.Update(on);
            RefreshJourney(on ? JourneySelection() : Entity.Null);
            _nextJourneyRefresh = 0;
        }

        private Entity JourneySelection()
        {
            // SelectedInfo resolves a pedestrian model back to its citizen, matching the name
            // and subject the player sees. Resolve lazily: these UI systems may not exist at load.
            if (_selectedInfo == null) _selectedInfo = World.GetExistingSystemManaged<SelectedInfoUISystem>();
            return _selectedInfo == null ? Entity.Null : _selectedInfo.selectedEntity;
        }

        private void RefreshJourney(Entity selected)
        {
            _lastSelected = selected;
            try
            {
                _journey.Refresh(selected, EntityManager, _names);
            }
            catch (Exception e)
            {
                _journey.Refresh(Entity.Null, EntityManager, _names);
                Mod.Log.Warn("Could not read the selected journey: " + e.Message);
            }
            _journeyBinding.Update();
        }

        private void WriteJourney(IJsonWriter writer)
        {
            writer.TypeBegin("seety.Journey");
            writer.PropertyName("hasSubject"); writer.Write(_journey.HasSubject);
            writer.PropertyName("subject"); writer.Write(_journey.Subject ?? string.Empty);
            writer.PropertyName("here"); writer.Write(_journey.Here ?? string.Empty);
            writer.PropertyName("hereMetres"); writer.Write(_journey.HereMetres);
            writer.PropertyName("destination"); writer.Write(_journey.Destination ?? string.Empty);
            writer.PropertyName("destinationRef"); writer.Write(_journey.DestinationRef ?? string.Empty);
            writer.PropertyName("truncated"); writer.Write(_journey.Truncated);
            writer.PropertyName("legs"); writer.ArrayBegin((uint)_journey.Legs.Count);
            foreach (var leg in _journey.Legs)
            {
                writer.TypeBegin("seety.JourneyLeg");
                writer.PropertyName("kind"); writer.Write(leg.Kind);
                writer.PropertyName("name"); writer.Write(leg.Name);
                writer.PropertyName("route"); writer.Write(leg.Route);
                writer.PropertyName("metres"); writer.Write(leg.Metres);
                writer.PropertyName("colour"); writer.Write(leg.Colour ?? string.Empty);
                writer.PropertyName("number"); writer.Write(leg.Number);
                writer.TypeEnd();
            }
            writer.ArrayEnd();
            writer.TypeEnd();
        }

        /// <summary>
        /// Takes the camera to the place a journey ends.
        ///
        /// The same versioned reference the transit rows use, for the same reason: a destination
        /// can be demolished while the panel is open, and an index on its own would send the
        /// camera to whatever has since been given that slot.
        /// </summary>
        private void OnFlyToJourneyPlace(string reference)
        {
            try
            {
                var place = Vitals.JourneyTrace.Resolve(reference);

                if (place == Entity.Null || !EntityManager.Exists(place)
                    || !EntityManager.HasComponent<Game.Objects.Transform>(place))
                {
                    return;
                }

                if (_camera == null || _camera.activeCameraController == null)
                {
                    return;
                }

                var at = EntityManager.GetComponentData<Game.Objects.Transform>(place).m_Position;
                _camera.activeCameraController.pivot = new UnityEngine.Vector3(at.x, at.y, at.z);
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Could not fly to a journey destination.");
            }
        }

        private void OnOpenJourneyLine(string reference)
        {
            if (!_journeyOnBinding.value || _expandedId != TrafficVitalId) return;
            // Only lines in the current trace can be opened; versioned IDs reject stale clicks.
            bool listed = false;
            foreach (var leg in _journey.Legs)
                if (leg.Kind == "transit" && leg.Route == reference) { listed = true; break; }
            var line = Vitals.JourneyTrace.Resolve(reference);
            if (!listed || line == Entity.Null || !EntityManager.Exists(line)
                || !EntityManager.HasComponent<Game.Routes.Route>(line)) return;
            if (_tools == null) _tools = World.GetExistingSystemManaged<Game.Tools.ToolSystem>();
            if (_tools != null) _tools.selected = line;
        }

        /// <summary>Hide or show the notification icons over the city.</summary>
        private void OnSetIconsHidden(bool hidden)
        {
            _iconVisibility.Set(hidden);
            _iconsHiddenBinding.Update(_iconVisibility.Hidden);
        }

        private void OnSetConfigMode(bool on)
        {
            _configMode = on;
            _configModeBinding.Update(on);
            if (on)
            {
                OnExpand(string.Empty);
            }
            RebuildActiveVitals();
        }

        /// <summary>Clicked a row while configuring: show it or stop showing it.</summary>
        private void OnToggleVital(string id)
        {
            var settings = Mod.Settings;
            if (settings == null || string.IsNullOrEmpty(id))
            {
                return;
            }

            settings.SetVitalEnabled(id, !settings.IsVitalEnabled(id));
        }

        private void OnSetVisible(bool visible)
        {
            SetVisible(visible);

            if (Mod.Settings != null)
            {
                Mod.Settings.ShowStrip = visible;

                // Written through, the same as OnPositionChanged: the setter only notifies, it
                // does not persist, so without this the strip would come back on next session.
                Mod.Settings.ApplyAndSave();
            }
        }

        /// <summary>
        /// A vital was clicked: open the vanilla infoview it belongs to. This is the whole point
        /// of the mod - the strip is a way into the game, not a readout.
        /// </summary>
        private void OnVitalActivated(string id)
        {
            var vital = Vitals.VitalCatalog.Find(id);
            if (vital == null)
            {
                return;
            }

            try
            {
                ResolveInfoviews();

                var entity = ResolveEntity(vital);
                if (entity == Entity.Null)
                {
                    Mod.Log.Info("No infoview among [" + string.Join(", ", vital.Infoviews) +
                                 "] for vital '" + id + "'; ignoring the click.");
                    return;
                }

                _infoviews.SetActiveInfoview(entity);
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Could not open the infoview for '" + id + "'.");
            }
        }

        /// <summary>The first candidate infoview that exists in this game, or Entity.Null.</summary>
        private Entity ResolveEntity(Vitals.Vital vital)
        {
            foreach (var name in vital.Infoviews)
            {
                Entity entity;
                if (_infoviewEntities.TryGetValue(name, out entity))
                {
                    return entity;
                }
            }

            return Entity.Null;
        }

        /// <summary>
        /// Builds the infoview name -> entity map. Retried until it finds something, because the
        /// prefabs do not exist until a game is loaded; giving up on the first empty query would
        /// leave every entry permanently non-clickable in a session that started at the menu.
        /// </summary>
        private void ResolveInfoviews()
        {
            if (_infoviewsResolved)
            {
                return;
            }

            var query = GetEntityQuery(ComponentType.ReadOnly<InfoviewData>());
            using (var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                foreach (var entity in entities)
                {
                    var name = _prefabs.GetPrefabName(entity);
                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    _infoviewEntities[name] = entity;

                    try
                    {
                        var prefab = _prefabs.GetPrefab<InfoviewPrefab>(entity);
                        if (prefab != null)
                        {
                            // Group first, then priority: the same two keys the game's own menu
                            // sorts on. Packed into one long so the comparison stays trivial.
                            _infoviewOrder[name] = ((long)prefab.m_Group << 32) + prefab.m_Priority;
                        }
                    }
                    catch (Exception e)
                    {
                        Mod.Log.Warn("Could not read the order of infoview '" + name + "': " + e.Message);
                    }
                }
            }

            if (_infoviewEntities.Count == 0)
            {
                return;
            }

            _infoviewsResolved = true;
            RebuildActiveVitals();

            // The prefab names live in the packed asset database and cannot be read outside a
            // running game, so the catalogue ships candidates. Logging what actually exists is
            // how those guesses get replaced with facts.
            var names = new List<string>(_infoviewEntities.Keys);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            Mod.Log.Info("Resolved " + names.Count + " infoviews: " + string.Join(", ", names.ToArray()));

            foreach (var vital in Vitals.VitalCatalog.All())
            {
                if (vital.Infoviews.Length > 0 && ResolveEntity(vital) == Entity.Null)
                {
                    Mod.Log.Warn("Vital '" + vital.Id + "' matched no infoview; tried: " +
                                 string.Join(", ", vital.Infoviews));
                }
            }
        }
    }
}
