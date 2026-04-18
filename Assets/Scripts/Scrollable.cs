using System;
using UnityEngine;
using UnityEngine.UIElements;

public class Scrollable : Manipulator
{
    private Action<Scrollable> m_DownHandler;

    private Action<Scrollable> m_ScrollHandler;

    private Action<Scrollable> m_UpHandler;

    private bool m_IsDown;

    private int m_PointerId = PointerId.invalidPointerId;

    private Vector2 m_LastPos;

    private long m_LastTimestamp;

    public Vector2 deltaPos {  get; private set; } = Vector2.zero;

    public Vector2 velocity { get; private set; } = Vector2.zero;

    public Scrollable(
        Action<Scrollable> downHandler,
        Action<Scrollable> scrollHandler,
        Action<Scrollable> upHandler
        )
    {
        m_DownHandler = downHandler;
        m_ScrollHandler = scrollHandler;
        m_UpHandler = upHandler;
    }

    protected override void RegisterCallbacksOnTarget()
    {
        target.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
        target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        target.RegisterCallback<PointerUpEvent>(OnPointerUp);
    }

    protected override void UnregisterCallbacksFromTarget()
    {
        target.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
        target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
        target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        // マルチタッチ防止
        if (m_IsDown)
            return;

        m_IsDown = true;
        m_LastPos = evt.position;
        m_LastTimestamp = evt.timestamp;
        deltaPos = Vector2.zero;
        velocity = Vector2.zero;

        m_PointerId = evt.pointerId; //要る？
        target.CapturePointer(evt.pointerId);

        m_DownHandler?.Invoke(this);
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (!m_IsDown || !target.HasPointerCapture(evt.pointerId))
            return;

        Vector2 position = evt.position;
        deltaPos = position - m_LastPos;
        m_LastPos = position;

        var deltaTime = evt.timestamp - m_LastTimestamp;
        // deltaTime が0 になるときがある？
        if (deltaTime > 0)
        {
            var newVelocity = deltaPos / deltaTime;
            velocity = Vector2.Lerp(velocity, newVelocity, 0.5f);
            m_LastTimestamp = evt.timestamp;
            //Debug.Log($"deltaTime: {deltaTime}, newVelocity: {newVelocity}, deltaPos: {deltaPos}");
        }
        else
        {
            Debug.LogWarning($"deltaTime:{deltaTime}");
        }

        m_ScrollHandler?.Invoke(this);

        evt.StopPropagation();
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (!m_IsDown || !target.HasPointerCapture(evt.pointerId))
            return;

        target.ReleasePointer(evt.pointerId);
        m_PointerId = PointerId.invalidPointerId;

        m_IsDown = false;

        m_UpHandler?.Invoke(this);

        evt.StopPropagation();
    }
}
