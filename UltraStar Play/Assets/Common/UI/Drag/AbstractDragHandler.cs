using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UniInject;
using UniRx;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Disable warning about fields that are never assigned, their values are injected.
#pragma warning disable CS0649

abstract public class AbstractDragHandler<EVENT> : MonoBehaviour, INeedInjection, IBeginDragHandler, IEndDragHandler, IDragHandler
{
    private readonly List<IDragListener<EVENT>> dragListeners = new List<IDragListener<EVENT>>();

    [Inject]
    public GraphicRaycaster graphicRaycaster;

    private bool isDragging;
    private bool ignoreDrag;

    private EVENT dragStartEvent;

    public RectTransform targetRectTransform;

    private List<IDisposable> disposables = new List<IDisposable>();
    
    void Start()
    {
        InputManager.GetInputAction(R.InputActions.usplay_back).PerformedAsObservable(10)
            .Where(_ => isDragging)
            .Subscribe(_ =>
            {
                CancelDrag();
                // Cancel other callbacks. To do so, this subscription has a higher priority. 
                InputManager.GetInputAction(R.InputActions.usplay_back).CancelNotifyForThisFrame();
            });
    }

    public void AddListener(IDragListener<EVENT> listener)
    {
        dragListeners.Add(listener);
    }

    public void RemoveListener(IDragListener<EVENT> listener)
    {
        dragListeners.Remove(listener);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        ignoreDrag = false;
        isDragging = true;
        dragStartEvent = CreateDragEventStart(eventData);
        NotifyListeners(listener => listener.OnBeginDrag(dragStartEvent), true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (ignoreDrag)
        {
            return;
        }

        EVENT dragEvent = CreateDragEvent(eventData, dragStartEvent);
        NotifyListeners(listener => listener.OnDrag(dragEvent), false);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (ignoreDrag
            || !isDragging)
        {
            return;
        }

        EVENT dragEvent = CreateDragEvent(eventData, dragStartEvent);
        NotifyListeners(listener => listener.OnEndDrag(dragEvent), false);
        isDragging = false;
    }

    private void CancelDrag()
    {
        if (ignoreDrag)
        {
            return;
        }

        isDragging = false;
        ignoreDrag = true;
        NotifyListeners(listener => listener.CancelDrag(), false);
    }

    private void NotifyListeners(Action<IDragListener<EVENT>> action, bool includeCanceledListeners)
    {
        foreach (IDragListener<EVENT> listener in dragListeners)
        {
            if (includeCanceledListeners || !listener.IsCanceled())
            {
                action(listener);
            }
        }
    }

    private void OnDestroy()
    {
        disposables.ForEach(it => it.Dispose());
    }

    abstract protected EVENT CreateDragEventStart(PointerEventData eventData);
    abstract protected EVENT CreateDragEvent(PointerEventData eventData, EVENT dragStartEvent);

    protected GeneralDragEvent CreateGeneralDragEvent(PointerEventData eventData, GeneralDragEvent dragStartEvent)
    {
        float xDistanceInPixels = eventData.position.x - dragStartEvent.StartPositionInPixels.x;
        float yDistanceInPixels = eventData.position.y - dragStartEvent.StartPositionInPixels.y;

        float widthInPixels = targetRectTransform.rect.width;
        float heightInPixels = targetRectTransform.rect.height;
        float xDistanceInPercent = xDistanceInPixels / widthInPixels;
        float yDistanceInPercent = yDistanceInPixels / heightInPixels;

        GeneralDragEvent result = new GeneralDragEvent(dragStartEvent.StartPositionInPixels,
            dragStartEvent.StartPositionInPercent,
            new Vector2(xDistanceInPixels, yDistanceInPixels),
            new Vector2(xDistanceInPercent, yDistanceInPercent),
            dragStartEvent.RaycastResultsDragStart,
            dragStartEvent.InputButton);
        return result;
    }

    protected GeneralDragEvent CreateGeneralDragEventStart(PointerEventData eventData)
    {
        float xDragStartInPixels = eventData.pressPosition.x;
        float yDragStartInPixels = eventData.pressPosition.y;
        float xDistanceInPixels = 0;
        float yDistanceInPixels = 0;

        List<RaycastResult> raycastResults = new List<RaycastResult>();
        PointerEventData eventDataForRaycast = new PointerEventData(EventSystem.current);
        eventDataForRaycast.position = eventData.pressPosition;
        graphicRaycaster.Raycast(eventDataForRaycast, raycastResults);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRectTransform,
                                                                eventData.pressPosition,
                                                                eventData.pressEventCamera,
                                                                out Vector2 localPoint);

        float widthInPixels = targetRectTransform.rect.width;
        float heightInPixels = targetRectTransform.rect.height;
        float xDragStartInPercent = (localPoint.x + (widthInPixels / 2)) / widthInPixels;
        float yDragStartInPercent = (localPoint.y + (heightInPixels / 2)) / heightInPixels;

        float xDistanceInPercent = 0;
        float yDistanceInPercent = 0;

        GeneralDragEvent result = new GeneralDragEvent(new Vector2(xDragStartInPixels, yDragStartInPixels),
            new Vector2(xDragStartInPercent, yDragStartInPercent),
            new Vector2(xDistanceInPixels, yDistanceInPixels),
            new Vector2(xDistanceInPercent, yDistanceInPercent),
            raycastResults,
            eventData.button);
        return result;
    }
}
