using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Tools
{
    public class LogTool : EditorWindow
    {
        // UI Elements
        private VisualElement rootLeftPane;
        private ListView _eventOccurenceListView;
        private Label _logCountLabel;
        private Button _toggleFilterButton;
        private TwoPaneSplitView _splitView;

        // Data
        private static List<AbstractLogFilter> _filters;

        private static readonly List<BaseListViewLine<GameEvent>> logLines = new List<BaseListViewLine<GameEvent>>()
        {
            new StaticSubscriberBoundLine(),
            new StaticSubscriberUnboundLine(),
            new DynamicSubscriberBoundLine(),
            new DynamicSubscriberUnboundLine(),
            new EventRaisedLine()
        };

        private readonly ObservableCollection<GameEvent> _eventOccurrenceList =
            new ObservableCollection<GameEvent>();

        private readonly List<GameEvent> _filteredEventOccurrenceList =
            new List<GameEvent>();

        [MenuItem("Tools/Game Event Hub/Log")]
        public static void ShowWindow()
        {
            // This method is called when the user selects the menu item in the Editor.
            EditorWindow wnd = GetWindow<LogTool>();
            wnd.titleContent = new GUIContent("Event Log");

            // Limit size of the window.
            wnd.minSize = new Vector2(450, 200);
            wnd.maxSize = new Vector2(1920, 720);
        }

        private void CreateGUI()
        {
            LoadFilters();
            _splitView = new TwoPaneSplitView(0, 0, TwoPaneSplitViewOrientation.Horizontal);

            _splitView.Add(DrawLeftPane());

            _splitView.Add(DrawRightPane());

            rootVisualElement.Add(_splitView);
        }

        private void LoadFilters()
        {
            _filters = new List<AbstractLogFilter>();
            var derivedTypes = TypeCache.GetTypesDerivedFrom(typeof(AbstractLogFilter));

            var sortedDerivedTypes = derivedTypes.OrderBy(type =>
            {
                var instance = (AbstractLogFilter)Activator.CreateInstance(type);
                return instance.Order;
            });

            foreach (var derivedType in sortedDerivedTypes)
            {
                var filter = (AbstractLogFilter)Activator.CreateInstance(derivedType);
                filter.Initialize(ApplyFilters);
                _filters.Add(filter);
            }
        }

        private VisualElement DrawLeftPane()
        {
            rootLeftPane = new VisualElement();
            rootLeftPane.style.minWidth = 0;
            rootLeftPane.style.overflow = Overflow.Hidden;
            rootLeftPane.style.maxWidth = Length.Percent(60);

            var filtersTitle = UIToolkitHelpers.Title("Filters");
            rootLeftPane.Add(filtersTitle);

            var filtersContainer = new VisualElement();
            filtersContainer.style.flexGrow = 1;


            for (int i = 0; i < _filters.Count; i++)
            {
                var filter = _filters[i];

                var singleFilterLine = new VisualElement();
                singleFilterLine.style.height = filter.Height;
                singleFilterLine.style.minHeight = 50;
                singleFilterLine.style.marginLeft = 10;
                singleFilterLine.style.marginRight = 10;
                singleFilterLine.style.flexDirection = FlexDirection.Row;
                singleFilterLine.style.alignItems = Align.Center;
                singleFilterLine.style.borderBottomWidth = 1;
                singleFilterLine.style.borderBottomColor = new Color(0.5f, 0.5f, 0.5f);

                singleFilterLine.Add(filter.DrawFilter());

                filtersContainer.Add(singleFilterLine);
            }

            rootLeftPane.Add(filtersContainer);

            return rootLeftPane;
        }

        private VisualElement DrawRightPane()
        {
            var rootRightPane = new VisualElement();
            rootRightPane.style.overflow = Overflow.Hidden;

            rootRightPane.Add(UIToolkitHelpers.Title("Event Log"));


            _toggleFilterButton =
                UIToolkitHelpers.ButtonWithIcon("toggle-filters", 40, 16, "FilterByType", "d_FilterByType");


            _toggleFilterButton.clicked += () =>
            {
                _splitView.fixedPaneInitialDimension =
                    Mathf.Approximately(_splitView.fixedPaneInitialDimension, 300) ? 0 : 300;
            };
            _toggleFilterButton.tooltip = "Show/Hide filters";
            _toggleFilterButton.style.position = Position.Absolute;
            _toggleFilterButton.style.borderTopRightRadius = 15;
            _toggleFilterButton.style.borderBottomRightRadius = 15;
            _toggleFilterButton.style.height = 30;
            _toggleFilterButton.style.marginTop = 10;
            _toggleFilterButton.style.marginLeft = -2;

            rootRightPane.Add(_toggleFilterButton);

            var toolbarContainer = new VisualElement();
            toolbarContainer.style.flexDirection = FlexDirection.Row;
            toolbarContainer.style.alignItems = Align.Center;
            toolbarContainer.style.justifyContent = Justify.SpaceBetween;

            toolbarContainer.Add(UIToolkitHelpers.ItalicLabel("<b>Right click</b> on a log to see more options", 10));

            _logCountLabel = UIToolkitHelpers.ItalicLabel("Log count: 0", 0, 10, TextAnchor.MiddleRight);

            toolbarContainer.Add(_logCountLabel);

            rootRightPane.Add(toolbarContainer);

            rootRightPane.Add(UIToolkitHelpers.Spacer(15));

            _eventOccurenceListView = new ListView();
            _eventOccurenceListView.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;
            _eventOccurenceListView.fixedItemHeight = 50;
            _eventOccurenceListView.style.flexGrow = 1;
            _eventOccurenceListView.itemsSource = _filteredEventOccurrenceList;
            _eventOccurenceListView.makeItem = DrawLogLine;
            _eventOccurenceListView.bindItem = (element, i) =>
            {
                logLines.First(logLine => logLine.IsLineApplicable(_filteredEventOccurrenceList[i]))
                    .BindLogLine(element, _filteredEventOccurrenceList[i]);

                logLines.First(logLine => logLine.IsLineApplicable(_filteredEventOccurrenceList[i]))
                    .AppendRightClickMenu(element, _filteredEventOccurrenceList[i], i);
            };

            _eventOccurrenceList.CollectionChanged += (sender, args) => { ApplyFilters(); };

            rootRightPane.Add(_eventOccurenceListView);

            var clearLogButton = new Button(() =>
            {
                _eventOccurrenceList.Clear();
                ApplyFilters();
            });
            clearLogButton.text = "Clear Log";
            clearLogButton.style.minHeight = 40;
            clearLogButton.style.marginTop = 10;
            clearLogButton.style.marginBottom = 10;
            clearLogButton.style.marginLeft = 10;
            clearLogButton.style.marginRight = 10;

            rootRightPane.Add(clearLogButton);

            return rootRightPane;
        }

        private void UpdateLogCountLabel()
        {
            if (_logCountLabel == null) return;

            var filteredCount = _filteredEventOccurrenceList.Count;
            var totalCount = _eventOccurrenceList.Count;

            if (filteredCount != totalCount)
            {
                _logCountLabel.text =
                    $"Log count: {filteredCount} / {totalCount} <color=orange>(filters applied)</color>";
                return;
            }

            _logCountLabel.text = $"Log count: {_filteredEventOccurrenceList.Count}";
        }

        private VisualElement DrawLogLine()
        {
            var rootLogLine = UIToolkitHelpers.ListViewLine("root-log-line");
            rootLogLine.style.overflow = Overflow.Hidden;

            // Add unity internal icon as image
            rootLogLine.Add(UIToolkitHelpers.DrawUnityIcon("sv_icon_dot15_pix16_gizmo", "sv_icon_dot9_pix16_gizmo", 16,
                "dynamic_subscriber"));
            rootLogLine.Add(UIToolkitHelpers.DrawUnityIcon("sv_icon_dot13_pix16_gizmo", "sv_icon_dot9_pix16_gizmo", 16,
                "event_raised"));
            rootLogLine.Add(UIToolkitHelpers.DrawUnityIcon("sv_icon_dot9_pix16_gizmo", "sv_icon_dot9_pix16_gizmo", 16,
                "static_subscriber"));

            var label = new Label("Event name");
            label.name = "main-log-text";
            label.style.flexGrow = 1;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.enableRichText = true;

            rootLogLine.Add(label);

            var subscribersCount = new Label("Subscribers count");
            subscribersCount.name = "subscribers-count";
            rootLogLine.Add(subscribersCount);

            rootLogLine.Add(UIToolkitHelpers.ButtonWithIcon("ping", 35, 16, "console.infoicon",
                "GameObject Icon", "Ping game object in scene"));

            rootLogLine.Add(UIToolkitHelpers.ButtonWithIcon("open-data", 35, 16, "console.infoicon",
                "console.infoicon", "Display event data"));

            rootLogLine.Add(UIToolkitHelpers.ButtonWithIcon("open-actors", 35, 16, "UnityEditor.HierarchyWindow",
                "UnityEditor.HierarchyWindow", "Dispaly emitter and subscribers called"));

            return rootLogLine;
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                GameEventHub.Bind(this);
            }

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            GameEventHub.Unbind(this);
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                GameEventHub.Bind(this);
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                GameEventHub.Unbind(this);
            }
        }

        [OnGameEvent(SubscriberPriority.Essential)]
        private void OnEventRaised(OnEventRaised e)
        {
            _eventOccurrenceList.Insert(0, e);
        }

        [OnGameEvent(SubscriberPriority.Essential)]
        private void OnObjectBound(OnObjectBoundToEventSystem e)
        {
            if (ReferenceEquals(e.BoundObject, this))
            {
                return;
            }

            _eventOccurrenceList.Insert(0, e);
        }

        [OnGameEvent(SubscriberPriority.Essential)]
        private void OnObjectUnbound(OnObjectUnboundFromEventSystem e)
        {
            if (ReferenceEquals(e.unboundObject, this))
            {
                return;
            }

            _eventOccurrenceList.Insert(0, e);
        }

        private void ApplyFilters()
        {
            _filteredEventOccurrenceList.Clear();

            foreach (var gameEvent in _eventOccurrenceList)
            {
                if (_filters.Count == 0 || _filters.All(filter => filter.EvaluateFilter(gameEvent)))
                {
                    _filteredEventOccurrenceList.Add(gameEvent);
                }
            }

            _eventOccurenceListView?.Rebuild();
            UpdateLogCountLabel();
        }
    }
}