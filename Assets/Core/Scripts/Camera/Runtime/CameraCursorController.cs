using UnityEngine;

internal sealed class CameraCursorController
{
    private static CameraCursorController owner;
    private bool freeCursorHeld;
    private bool lockStateBeforeFree;
    private bool skipLookNextFrame;

    public bool IsLocked { get; private set; }
    public bool FreeCursorHeld => freeCursorHeld;
    public bool SkipLookThisFrame { get; private set; }

    public void Begin(bool lockCursor)
    {
        Release();
        SetCursorLock(lockCursor);
        skipLookNextFrame = lockCursor;
    }

    public void Tick(CharacterInputReader input, bool lockCursor, bool focused)
    {
        // 锁定状态刚发生变化的这一帧鼠标增量不可靠（光标回中），跳过它避免镜头跳变。
        SkipLookThisFrame = skipLookNextFrame;
        skipLookNextFrame = false;

        // Alt 是“临时交出鼠标”的修饰键：按住时释放光标并暂停注视，松开后按进入前的意图恢复。
        bool freeHeld = input.FreeCursorHeld;
        if (freeHeld != freeCursorHeld)
        {
            if (freeHeld)
            {
                lockStateBeforeFree = IsLocked;
                SetCursorLock(false);
                SkipLookThisFrame = true;
            }
            else if (lockStateBeforeFree && lockCursor && focused)
            {
                // 焦点不在本程序时不抢回鼠标，避免把光标从编辑器或其它窗口拉走。
                SetCursorLock(true);
                SkipLookThisFrame = true;
            }
            freeCursorHeld = freeHeld;
        }

        // 按住 Alt 期间不处理 Escape 和左键：此时的左键用于操作 UI，不能重新锁定光标。
        if (freeCursorHeld)
        {
            return;
        }

        // Escape 释放鼠标；需要锁定时点击画面可重新捕获。
        if (input.ReleaseCursorPressed)
        {
            SetCursorLock(false);
        }
        else if (lockCursor && input.LockCursorPressed)
        {
            SetCursorLock(true);
        }
    }

    public void Release()
    {
        SetCursorLock(false);

        // 停用或退出播放时清空 Alt 状态，重新启用后按 lockCursor 重新判断。
        freeCursorHeld = false;
        lockStateBeforeFree = false;
        SkipLookThisFrame = false;
        skipLookNextFrame = false;
    }

    // 统一更新内部状态和 Unity 光标状态，避免两者不同步。
    private void SetCursorLock(bool locked)
    {
        if (locked)
        {
            if (IsLocked && owner == this && Cursor.lockState == CursorLockMode.Locked && !Cursor.visible)
            {
                return;
            }
            if (owner != null)
            {
                owner.IsLocked = false;
            }
            owner = this;
            IsLocked = true;
            SkipLookThisFrame = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            return;
        }

        bool heldByThisInstance = owner == this;
        IsLocked = false;
        if (!heldByThisInstance)
        {
            return;
        }
        owner = null;
        SkipLookThisFrame = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
