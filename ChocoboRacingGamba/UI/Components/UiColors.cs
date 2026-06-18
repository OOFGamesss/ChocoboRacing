using System.Numerics;

namespace ChocoboRacing.UI.Components;

/// <summary>
/// Shared semantic text colours used across the UI.
/// </summary>
internal static class UiColors
{
    internal static readonly Vector4 Gold     = new(1f, 0.85f, 0.2f, 1f);
    internal static readonly Vector4 Positive = new(0.4f, 1f, 0.4f, 1f);
    internal static readonly Vector4 Negative = new(1f, 0.4f, 0.4f, 1f);
    internal static readonly Vector4 Warning  = new(1f, 0.6f, 0.2f, 1f);
    internal static readonly Vector4 Info     = new(0.4f, 0.8f, 1f, 1f);
    internal static readonly Vector4 Accent   = new(0.4f, 0.9f, 1f, 1f);
    internal static readonly Vector4 Muted    = new(0.5f, 0.5f, 0.5f, 1f);
    internal static readonly Vector4 Subtle   = new(0.7f, 0.7f, 0.7f, 1f);
}
