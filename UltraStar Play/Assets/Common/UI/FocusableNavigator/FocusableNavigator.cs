using System;
using System.Collections.Generic;
using System.Linq;
using PrimeInputActions;
using UniInject;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

// Disable warning about fields that are never assigned, their values are injected.
#pragma warning disable CS0649

/**
 * Changes focus between focusable VisualElements on navigation InputAction
 * (e.g. arrow keys or controller stick).
 * And sends submit event to focused VisualElement on corresponding InputAction.
 */
public class FocusableNavigator : MonoBehaviour, INeedInjection
{
    [Inject]
    protected UIDocument uiDocument;

    [Inject(Optional = true)]
    protected EventSystem eventSystem;

    [Inject]
    protected Settings settings;
    
    public bool focusLastElementIfNothingFocused;

    public bool logFocusedVisualElements;

    public VisualElement FocusedVisualElement => uiDocument.rootVisualElement.focusController.focusedElement as VisualElement;
    protected VisualElement lastFocusedVisualElement;

    protected readonly Subject<NoNavigationTargetFoundEvent> noNavigationTargetFoundEventStream = new();
    public IObservable<NoNavigationTargetFoundEvent> NoNavigationTargetFoundEventStream => noNavigationTargetFoundEventStream;

    protected readonly Subject<NoSubmitTargetFoundEvent> noSubmitTargetFoundEventStream = new();
    public IObservable<NoSubmitTargetFoundEvent> NoSubmitTargetFoundEventStream => noSubmitTargetFoundEventStream;

    private readonly List<CustomNavigationTarget> customNavigationTargets = new();

    public virtual void Start()
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        if (eventSystem != null
            && settings.DeveloperSettings.enableEventSystemOnAndroid)
        {
            eventSystem.sendNavigationEvents = false;
        }

        if (focusLastElementIfNothingFocused)
        {
            NoNavigationTargetFoundEventStream.Subscribe(evt =>
            {
                if (evt.FocusedVisualElement == null)
                {
                    FocusLastFocusedVisualElement();
                }
            });
        }

        DropdownField dropdownField = uiDocument.rootVisualElement.Q<DropdownField>();
        dropdownField.RegisterValueChangedCallback(evt =>
        {
            Debug.Log("DropdownField value changed: " + evt.newValue);
        });
    }

    protected virtual void Update()
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        if (uiDocument == null)
        {
            // Injection failed
            return;
        }

        VisualElement focusedVisualElement = FocusedVisualElement;
        if (focusedVisualElement != null)
        {
            lastFocusedVisualElement = focusedVisualElement;
        }
    }

    public void FocusLastFocusedVisualElement()
    {
        if (lastFocusedVisualElement != null
            && IsFocusableNow(lastFocusedVisualElement))
        {
            if (logFocusedVisualElements)
            {
                DoFocusVisualElement(lastFocusedVisualElement,
                    $"Moving focus to last focused VisualElement: {lastFocusedVisualElement}");
            }
        }
    }

    public virtual void OnBack()
    {
        if (!settings.DeveloperSettings.enableEventSystemOnAndroid)
        {
            return;
        }

        if (IsDropdownList(FocusedVisualElement))
        {
            FocusedVisualElement.SendEvent(NavigationCancelEvent.GetPooled());
            InputManager.GetInputAction(R.InputActions.usplay_back).CancelNotifyForThisFrame();
        }
    }

    public virtual void OnSubmit()
    {
        if (!settings.DeveloperSettings.enableEventSystemOnAndroid)
        {
            return;
        }

        if (IsDropdownList(FocusedVisualElement))
        {
            FocusedVisualElement.SendEvent(NavigationSubmitEvent.GetPooled());
            return;
        }

        VisualElement focusedVisualElement = FocusedVisualElement;
        if (focusedVisualElement != null)
        {
            focusedVisualElement.SendEvent(new NavigationSubmitEvent());
        }
        else
        {
            noSubmitTargetFoundEventStream.OnNext(new NoSubmitTargetFoundEvent());
        }
    }

    public virtual void OnNavigate(Vector2 navigationDirection)
    {
        if (!settings.DeveloperSettings.enableEventSystemOnAndroid)
        {
            return;
        }
        
        VisualElement focusedVisualElement = FocusedVisualElement;
        if (focusedVisualElement == null)
        {
            noNavigationTargetFoundEventStream.OnNext(new NoNavigationTargetFoundEvent
            {
                NavigationDirection = navigationDirection,
                FocusableNavigatorRootVisualElement = null,
                FocusedVisualElement = null,
            });
            return;
        }

        // Keep focus on TextField if not navigating away from it
        if (focusedVisualElement is TextField focusedTextField)
        {
            if (!IsNavigatingAwayFromTextField(navigationDirection, focusedTextField))
            {
                return;
            }
        }

        // Use custom navigation target if any
        if (TryNavigateToCustomNavigationTarget(focusedVisualElement, navigationDirection))
        {
            return;
        }

        if (IsDropdownList(focusedVisualElement))
        {
            NavigateDropdownList(focusedVisualElement, navigationDirection);
        }
        
        NavigateToBestMatchingNavigationTarget(focusedVisualElement, navigationDirection);
    }

    private void NavigateDropdownList(
        VisualElement focusedVisualElement,
        Vector2 navigationDirection)
    {
        if (logFocusedVisualElements)
        {
            Debug.Log("NavigateDropdownList");
        }
        
        if (navigationDirection.y > 0)
        {
            focusedVisualElement.SendEvent(NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Up));
        }
        else if (navigationDirection.y < 0)
        {
            focusedVisualElement.SendEvent(NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Down));
        }
    }

    private bool IsDropdownList(VisualElement focusedVisualElement)
    {
        if (focusedVisualElement.name != "unity-content-container"
            && !focusedVisualElement.ClassListContains("unity-base-dropdown__item"))
        {
            return false;
        }

        VisualElement unityBaseDropdown = VisualElementUtils.GetParent(focusedVisualElement,
                   parent => parent.ClassListContains("unity-base-dropdown"));
        return unityBaseDropdown != null;
    }

    private void NavigateToBestMatchingNavigationTarget(VisualElement focusedVisualElement, Vector2 navigationDirection)
    {
        // Find eligible elements for navigation, i.e., all descendants of the current focusableNavigatorRootVisualElement.
        VisualElement focusableNavigatorRootVisualElement = GetFocusableNavigatorRootVisualElement(focusedVisualElement);
        if (focusableNavigatorRootVisualElement == null)
        {
            noNavigationTargetFoundEventStream.OnNext(new NoNavigationTargetFoundEvent
            {
                NavigationDirection = navigationDirection,
                FocusableNavigatorRootVisualElement = null,
                FocusedVisualElement = focusedVisualElement,
            });
            return;
        }
        Rect startRect = focusedVisualElement.worldBound;

        List<VisualElement> focusableVisualElements = GetFocusableVisualElementsInDescendants(focusableNavigatorRootVisualElement);
        focusableVisualElements = focusableVisualElements
            .Where(it => it != focusedVisualElement)
            .ToList();

        // Only consider the VisualElements that are in the navigation direction
        List<VisualElement> visualElementsInDirection = GetVisualElementsInDirection(
            startRect,
            navigationDirection,
            focusableVisualElements);

        // Choose the nearest VisualElement
        VisualElement nearestVisualElement = visualElementsInDirection.FindMinElement(visualElement => GetVisualElementDistance(visualElement, focusedVisualElement));
        if (nearestVisualElement != null)
        {
            DoFocusVisualElement(nearestVisualElement,
                $"Moving focus to VisualElement with distance {GetVisualElementDistance(nearestVisualElement, focusedVisualElement)}: {nearestVisualElement}");
        }
        else
        {
            noNavigationTargetFoundEventStream.OnNext(new NoNavigationTargetFoundEvent
            {
                NavigationDirection = navigationDirection,
                FocusableNavigatorRootVisualElement = focusableNavigatorRootVisualElement,
                FocusedVisualElement = focusedVisualElement,
            });
        }
    }

    protected void DoFocusVisualElement(VisualElement visualElement, string logMessage)
    {
        if (logFocusedVisualElements
            && !logMessage.IsNullOrEmpty())
        {
            Debug.Log(logMessage);
        }

        if (visualElement == null)
        {
            Debug.LogError("Attempt to focus null VisualElement");
            return;
        }

        visualElement.Focus();
        visualElement.ScrollToSelf();
    }
    
    private bool TryNavigateToCustomNavigationTarget(VisualElement focusedVisualElement, Vector2 navigationDirection)
    {
        CustomNavigationTarget customNavigationTarget = customNavigationTargets.FirstOrDefault(customNavigationTarget =>
            customNavigationTarget.Matches(focusedVisualElement, navigationDirection));
        if (customNavigationTarget != null)
        {
            DoFocusVisualElement(customNavigationTarget.TargetVisualElement,
                    $"Moving focus to VisualElement from custom navigation target (start: {customNavigationTarget.StartVisualElement.name}, direction: {navigationDirection}, target: {customNavigationTarget.TargetVisualElement.name}");
            return true;
        }

        return false;
    }

    public void AddCustomNavigationTarget(
        VisualElement startVisualElement,
        Vector2 navigationDirection,
        VisualElement targetVisualElement,
        bool alsoAddOppositeDirection = false)
    {
        CustomNavigationTarget customNavigationTarget = new(startVisualElement, navigationDirection, targetVisualElement);
        customNavigationTargets.Add(customNavigationTarget);

        if (alsoAddOppositeDirection)
        {
            AddCustomNavigationTarget(targetVisualElement, -navigationDirection, startVisualElement, false);
        }
    }

    private static float GetVisualElementDistance(VisualElement visualElementA, VisualElement visualElementB)
    {
        float RectangleDistance(Rect rectA, Rect rectB)
        {
            // https://gamedev.stackexchange.com/questions/154036/efficient-minimum-distance-between-two-axis-aligned-squares
            Rect rectOuter = new Rect(
                Mathf.Min(rectA.xMin, rectB.xMin),
                Mathf.Min(rectA.yMin, rectB.yMin),
                Mathf.Max(rectA.xMax, rectB.xMax) - Mathf.Min(rectA.xMin, rectB.xMin),
                Mathf.Max(rectA.yMax, rectB.yMax) - Mathf.Min(rectA.yMin, rectB.yMin));
            float innerWidth = Mathf.Max(0, rectOuter.width - rectA.width - rectB.width);
            float innerHeight = Mathf.Max(0, rectOuter.height - rectA.height - rectB.height);
            float minDistance = Mathf.Sqrt(innerWidth * innerWidth + innerHeight * innerHeight);
            return minDistance;
        }

        float distance = RectangleDistance(visualElementA.worldBound, visualElementB.worldBound);

        return distance;
    }

    private bool IsNavigatingAwayFromTextField(Vector2 navigationDirection, TextField focusedTextField)
    {
        if (focusedTextField.isReadOnly)
        {
            return true;
        }

        // Navigate away from TextField
        // when cursor was already at first or last position in text field
        // and still navigating towards same direction.
        bool isContinuedNavigationFromTextStart = focusedTextField.cursorIndex == 0
                                                  // Navigate left or up is continued navigation
                                                  && (navigationDirection.x < 0 || navigationDirection.y > 0);
        bool isContinuedNavigationFromTextEnd = focusedTextField.cursorIndex == focusedTextField.value.Length
                                                // Navigate right or down is continued navigation
                                                && (navigationDirection.x > 0 || navigationDirection.y < 0);
        bool isContinuedNavigation = isContinuedNavigationFromTextStart || isContinuedNavigationFromTextEnd;
        return isContinuedNavigation
               // There must not be any selection
               && focusedTextField.selectIndex == focusedTextField.cursorIndex;
    }

    protected virtual List<VisualElement> GetFocusableVisualElementsInDescendants(VisualElement rootVisualElement)
    {
        List<VisualElement> descendants = rootVisualElement.Query<VisualElement>()
            .Where(descendant => descendant.tabIndex > 0
                || descendant
                    is Button
                    or Toggle
                    or DropdownField
                    or EnumField
                    or TextField
                    or IntegerField
                    or LongField
                    or FloatField
                    or DoubleField
                    or MinMaxSlider
                    or RadioButton)
            .Where(descendant => IsFocusableNow(descendant))
            .ToList();
        return descendants;
    }

    protected VisualElement GetFocusableNavigatorRootVisualElement()
    {
        return GetFocusableNavigatorRootVisualElement(FocusedVisualElement);
    }

    protected virtual VisualElement GetFocusableNavigatorRootVisualElement(VisualElement visualElement)
    {
        if (visualElement == null)
        {
            return null;
        }

        if (visualElement.ClassListContains(R.UssClasses.focusableNavigatorRoot))
        {
            return visualElement;
        }

        return GetFocusableNavigatorRootVisualElement(visualElement.parent);
    }

    protected List<VisualElement> GetVisualElementsInDirection(Rect startRect, Vector2 direction, List<VisualElement> visualElements)
    {
        bool IsInDirection(Rect rect)
        {
            // LEFT SIDE is xMin, RIGHT SIDE is xMax, TOP SIDE is yMin, BOTTOM SIDE is yMax
            if (direction.x < 0)
            {
                // Go left => RIGHT SIDE of target must be left of LEFT SIDE of start
                return rect.xMax <= startRect.xMin;
            }
            else if (direction.x > 0)
            {
                // Go right => LEFT SIDE of target must be right of RIGHT SIDE of start
                return rect.xMin >= startRect.xMax;
            }
            else if (direction.y <= 0)
            {
                // Go down => TOP SIDE of target must be below BOTTOM SIDE of start
                return rect.yMin >= startRect.yMax;
            }
            else if (direction.y > 0)
            {
                // Go up => BOTTOM SIDE of target must be above TOP SIDE of start
                return rect.yMax <= startRect.yMin;
            }

            return false;
        }

        Vector2 startRectCenter = startRect.center;
        return visualElements.Where(visualElement =>
            {
                Vector2 currentPoint = visualElement.worldBound.center;
                Vector2 towardsCurrentPointDirection = currentPoint - startRectCenter;
                // Y-Axis must be inverted
                float angle = Vector2.Angle(direction, new Vector2(towardsCurrentPointDirection.x, -towardsCurrentPointDirection.y));
                return angle is < 90 or > 270;
            })
            .Where(visualElement => IsInDirection(visualElement.worldBound))
            .ToList();
    }

    protected bool IsFocusableNow(VisualElement visualElement)
    {
        return visualElement != null
               && visualElement.IsVisibleByDisplay()
               && visualElement.GetAncestors().AllMatch(ancestor => ancestor.IsVisibleByDisplay())
               && !float.IsNaN(visualElement.worldBound.center.x)
               && !float.IsNaN(visualElement.worldBound.center.y)
               && visualElement is not Focusable { focusable: false }
               && visualElement.enabledInHierarchy
               && visualElement.canGrabFocus
               && !visualElement.ClassListContains(R.UssClasses.focusableNavigatorIgnore);
    }
}
