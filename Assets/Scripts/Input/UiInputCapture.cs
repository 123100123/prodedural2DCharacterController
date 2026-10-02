using System.Collections.Generic;
using UnityEngine;

// Lets on-screen UI (e.g. a debug panel) keep gameplay from reacting to input meant for it: clicks over the UI and
// typing into its fields. Gameplay asks here without knowing which UI is open.
public static class UiInputCapture {
    public interface ICapture {
        bool IsPointerOverUi { get; }
        bool IsTyping { get; }
    }

    private static readonly List<ICapture> captures = new List<ICapture>();

    public static bool IsPointerCaptured => captures.Exists(capture => capture.IsPointerOverUi);
    public static bool IsKeyboardCaptured => captures.Exists(capture => capture.IsTyping);

    public static void Register(ICapture capture) {
        if (!captures.Contains(capture)) {
            captures.Add(capture);
        }
    }

    public static void Unregister(ICapture capture) => captures.Remove(capture);

    // Static state outlives a play session when domain reload is disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnPlay() => captures.Clear();
}
