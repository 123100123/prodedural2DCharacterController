// The sorting layers of the world, back to front: one plain background layer behind the Default layer that holds the
// ground, the steps and the character. Code, the setup tools and the lights all take the names from here.
public static class EnvironmentLayers {
    public const string Background = "Background";
    public const string Level = "Default";

    // Back to front.
    public static readonly string[] BackToFront = { Background, Level };

    // The layers the shadows of the level's blocks fall on: the background behind them, and the level itself (a step
    // shadows the floor, a block the character).
    public static readonly string[] ShadowedByBlocks = { Background, Level };

    // Layers an earlier version of the background used, removed by the setup tool.
    public static readonly string[] Obsolete = { "BackgroundFar", "BackgroundMid", "BackgroundWall" };
}
