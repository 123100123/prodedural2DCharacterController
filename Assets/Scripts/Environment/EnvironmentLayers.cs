// The sorting layers of the world, back to front: three background layers behind the Default layer that holds the
// ground, the steps and the character. Code, the setup tools and the lights all take the names from here.
public static class EnvironmentLayers {
    public const string Far = "BackgroundFar";
    public const string Mid = "BackgroundMid";
    public const string Wall = "BackgroundWall";
    public const string Level = "Default";

    // Back to front.
    public static readonly string[] BackToFront = { Far, Mid, Wall, Level };

    // The layers the shadows of the level's blocks fall on: the wall and the middle ruins behind them, and the level
    // itself (a step shadows the floor, a block the character). The far scenery stays lit.
    public static readonly string[] ShadowedByBlocks = { Mid, Wall, Level };
}
