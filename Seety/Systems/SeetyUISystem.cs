using System;
using System.Collections.Generic;
using Colossal.UI.Binding;
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
        /// City-wide figures move slowly and the strip is glanceable, not an instrument.
        /// Refreshing twice a second is plenty and keeps this off the per-frame budget.
        /// </summary>
        private const double RefreshIntervalSeconds = 0.5;

        private PrefabSystem _prefabs;
        private InfoviewsUISystem _infoviews;
        private Vitals.VitalReader _reader;

        private RawValueBinding _vitalsBinding;
        private ValueBinding<bool> _visibleBinding;
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
        private Game.UI.NameSystem _names;

        /// <summary>Workforce against workplaces, behind the Workers row.</summary>
        private readonly Vitals.WorkforceTable _workforce = new Vitals.WorkforceTable();

        private RawValueBinding _workforceBinding;
        private RawValueBinding _demographicsBinding;

        /// <summary>The citizen census, shared by the workforce table and the demographics view.</summary>
        private CitizenCensusSystem _census;
        private RawValueBinding _historyBinding;
        private RawValueBinding _notificationsBinding;
        private Game.Rendering.CameraUpdateSystem _camera;

        /// <summary>The in-world notification icons, and whether they are currently hidden.</summary>
        private Notifications.NotificationIconVisibility _iconVisibility;
        private ValueBinding<bool> _iconsHiddenBinding;

        private double _nextRefresh;

        protected override void OnCreate()
        {
            base.OnCreate();

            _prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
            _infoviews = World.GetOrCreateSystemManaged<InfoviewsUISystem>();

            _reader = new Vitals.VitalReader(
                EntityManager,
                World.GetOrCreateSystemManaged<CitySystem>(),
                World.GetOrCreateSystemManaged<CountHouseholdDataSystem>(),
                World.GetOrCreateSystemManaged<CityStatisticsSystem>());

            _iconQuery = GetEntityQuery(
                ComponentType.ReadOnly<Game.Notifications.Icon>(),
                ComponentType.ReadOnly<PrefabRef>());

            _camera = World.GetOrCreateSystemManaged<Game.Rendering.CameraUpdateSystem>();
            _names = World.GetOrCreateSystemManaged<Game.UI.NameSystem>();
            _census = World.GetOrCreateSystemManaged<CitizenCensusSystem>();

            _schoolQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Game.Buildings.School>(),
                    ComponentType.ReadOnly<Game.Buildings.Student>(),
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
                // Icons still on screen, and icons already hidden. Two queries so both directions
                // are a single batched structural change rather than a walk.
                GetEntityQuery(new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<Game.Notifications.Icon>() },
                    None = new[] { ComponentType.ReadOnly<Game.Tools.Hidden>() }
                }),
                GetEntityQuery(new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<Game.Notifications.Icon>(),
                        ComponentType.ReadOnly<Game.Tools.Hidden>()
                    }
                }));

            // Without this the settings have no way to reach the strip.
            Mod.RegisterUISystem(this);

            RebuildActiveVitals();

            _vitalsBinding = new RawValueBinding(Group, "vitals", WriteVitals);
            AddBinding(_vitalsBinding);

            _visibleBinding = new ValueBinding<bool>(Group, "visible", true);
            AddBinding(_visibleBinding);

            var settings = Mod.Settings;
            _posXBinding = new ValueBinding<int>(Group, "posX",
                settings == null ? Settings.SeetySettings.DefaultStripX : settings.StripX);
            _posYBinding = new ValueBinding<int>(Group, "posY",
                settings == null ? Settings.SeetySettings.DefaultStripY : settings.StripY);
            AddBinding(_posXBinding);
            AddBinding(_posYBinding);

            AddBinding(new TriggerBinding<int, int>(Group, "setPosition", OnPositionChanged));

            _notificationsBinding = new RawValueBinding(Group, "notifications", WriteNotifications);
            AddBinding(_notificationsBinding);

            AddBinding(new TriggerBinding<string>(Group, "jumpToProblem", OnJumpToProblem));
            AddBinding(new TriggerBinding<int>(Group, "jumpToSchool", OnJumpToSchool));
            AddBinding(new TriggerBinding<string>(Group, "openInfoview", OnOpenInfoview));

            _configModeBinding = new ValueBinding<bool>(Group, "configMode", false);
            AddBinding(_configModeBinding);
            AddBinding(new TriggerBinding<bool>(Group, "setConfigMode", OnSetConfigMode));
            AddBinding(new TriggerBinding<string>(Group, "toggleVital", OnToggleVital));

            _iconsHiddenBinding = new ValueBinding<bool>(Group, "iconsHidden", false);
            AddBinding(_iconsHiddenBinding);
            AddBinding(new TriggerBinding<bool>(Group, "setIconsHidden", OnSetIconsHidden));

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

        public void SetVisible(bool visible)
        {
            if (_visibleBinding != null)
            {
                _visibleBinding.Update(visible);
            }
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            var now = UnityEngine.Time.realtimeSinceStartupAsDouble;
            if (now < _nextRefresh)
            {
                return;
            }

            _nextRefresh = now + RefreshIntervalSeconds;

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

            // New icons appear all the time; while hidden they have to be caught as they arrive.
            _iconVisibility.KeepUp();

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

                _breakdown.Refresh(_iconQuery, _prefabs);
                _notificationsBinding.Update();
            }

            if (_transportActive)
            {
                var before = _transport.PassengerTotal;
                _transport.Refresh(World.GetOrCreateSystemManaged<CityStatisticsSystem>());
                if (_transport.PassengerTotal != before)
                {
                    changed = true;
                }

                _notificationsBinding.Update();
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
                float value;
                _values.TryGetValue(vital.Id, out value);

                writer.TypeBegin("seety.Vital");
                writer.PropertyName("id");
                writer.Write(vital.Id);
                writer.PropertyName("title");
                writer.Write(vital.Title);
                writer.PropertyName("label");
                writer.Write(vital.Label);
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
                writer.Write(threshold != null && HighlightingEnabled());
                writer.PropertyName("hasHistory");
                writer.Write(vital.History.HasValue);
                writer.PropertyName("invert");
                writer.Write(vital.Invert);
                writer.PropertyName("factors");
                writer.Write(vital.Factors ?? string.Empty);
                writer.PropertyName("enabled");
                writer.Write(Mod.Settings == null || Mod.Settings.IsVitalEnabled(vital.Id));

                writer.TypeEnd();
            }

            writer.ArrayEnd();
        }

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
            var hasSchools = schools.Count > 0;

            var count = (uint)((_problemsActive ? 1 : 0) + (_transportActive ? 1 : 0) + (hasSchools ? 1 : 0));
            writer.ArrayBegin(count);

            if (_problemsActive)
            {
                writer.TypeBegin("seety.Breakdown");
                writer.PropertyName("id");
                writer.Write("problems");
                writer.PropertyName("rows");
                WriteProblemRows(writer);
                writer.TypeEnd();
            }

            if (_transportActive)
            {
                writer.TypeBegin("seety.Breakdown");
                writer.PropertyName("id");
                writer.Write("transport");
                writer.PropertyName("rows");
                WriteTransportRows(writer);
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
        /// The schools of the open level, fullest first. The row id is its index, because two
        /// schools can share a name and the camera has to know which one was clicked.
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

                writer.TypeBegin("seety.BreakdownRow");
                writer.PropertyName("id");
                writer.Write(school.Name + "  " + school.Students + "/" + school.Capacity);
                writer.PropertyName("icon");
                writer.Write("Media/Game/Icons/Education.svg");
                writer.PropertyName("count");
                writer.Write((int)school.Fullness);
                writer.PropertyName("suffix");
                writer.Write("%");
                writer.PropertyName("level");
                writer.Write((int)level);
                writer.PropertyName("clickable");
                writer.Write(true);
                writer.PropertyName("action");
                writer.Write("school:" + i);
                writer.TypeEnd();
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

        /// <summary>Passengers first, then freight. Modes nobody uses are left out.</summary>
        private void WriteTransportRows(IJsonWriter writer)
        {
            var rows = new List<Vitals.TransportMode>();

            foreach (var mode in _transport.Passengers)
            {
                if (mode.Count > 0)
                {
                    rows.Add(mode);
                }
            }

            foreach (var mode in _transport.Cargo)
            {
                if (mode.Count > 0)
                {
                    rows.Add(mode);
                }
            }

            writer.ArrayBegin((uint)rows.Count);

            foreach (var mode in rows)
            {
                // No severity: a mode carrying people is not a problem. Clicking pre-selects that
                // mode in vanilla's transportation overview - see the note on TransportMode.Action.
                WriteRow(writer, mode.Id, mode.Icon, mode.Count, Vitals.VitalLevel.Normal,
                    !string.IsNullOrEmpty(mode.Action), mode.Action);
            }

            writer.ArrayEnd();
        }

        /// <summary>
        /// One row of an expandable list. <paramref name="action"/> tells the UI what a click
        /// means: "jump" moves the camera to the next one of these, "passenger" and "cargo"
        /// pre-select that mode in vanilla's transportation overview, empty means not clickable.
        /// </summary>
        private static void WriteRow(IJsonWriter writer, string id, string icon, int count,
            Vitals.VitalLevel level, bool clickable, string action = "")
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
            writer.Write(string.Empty);
            writer.PropertyName("action");
            writer.Write(action);
            writer.TypeEnd();
        }

        /// <summary>
        /// The player opened or closed a row. Nothing is fetched for a row nobody is looking at:
        /// reading a series allocates, and the strip is on screen the whole game.
        /// </summary>
        private void OnExpand(string id)
        {
            _expandedId = id ?? string.Empty;
            _historyBinding.Update();

            if (NeedsCensus)
            {
                RefreshWorkforce();
            }

            RefreshSchools();
        }

        /// <summary>
        /// Rebuilds the school list when an education row is open. Like the census, it runs only
        /// while somebody is looking: it walks every school and asks the naming system for a
        /// label, which is not work to do twice a second for nothing.
        /// </summary>
        private void RefreshSchools()
        {
            var level = Vitals.SchoolBreakdown.LevelFor(_expandedId);
            _schools.Refresh(EntityManager, _schoolQuery, _names, level);
            _notificationsBinding.Update();
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

        /// <summary>Take me to that school. The index is its position in the list on screen.</summary>
        private void OnJumpToSchool(int index)
        {
            try
            {
                if (!_schools.Jump(index, _camera))
                {
                    Mod.Log.Info("Nothing to jump to at school index " + index + ".");
                }
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Could not jump to a school.");
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
        /// Hide or show the notification icons over the city. Lives in the Active problems window
        /// because that is where you are when they are in your way.
        /// </summary>
        private void OnSetIconsHidden(bool hidden)
        {
            _iconVisibility.Set(hidden);
            _iconsHiddenBinding.Update(_iconVisibility.Hidden);
        }

        private void OnSetConfigMode(bool on)
        {
            _configMode = on;
            _configModeBinding.Update(on);
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
