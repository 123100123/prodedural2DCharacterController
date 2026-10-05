using UnityEngine;

// Physics queries for ledges: scans columns ahead for the first top surface inside a height window,
// refines its near edge and checks that the character fits on top. One-way platforms are not ledges and block nothing
// here: the character passes up through them instead.
public sealed class LedgeDetector {
    private const int EdgeRefineIterations = 5;
    private const float TopTolerance = 0.1f;
    private const float RayHeadroom = 0.05f;

    private readonly ClimbSettings settings;

    public LedgeDetector(ClimbSettings settings) {
        this.settings = settings;
    }

    public bool TryFindLedge((float originX, int direction, (float minTop, float maxTop) window) search, out Ledge ledge) {
        ledge = default;
        int columnCount = Mathf.FloorToInt(settings.searchDistance / settings.searchStep);
        float previousX = search.originX;

        for (int column = 1; column <= columnCount; column++) {
            float x = search.originX + search.direction * column * settings.searchStep;
            if (IsBlockedAboveWindow((x, search.window))) {
                return false;
            }
            if (TryFindTopInWindow((x, search.window), out float topY)) {
                Vector2 corner = new Vector2(RefineEdgeX((previousX, x, search.window)), topY);
                ledge = new Ledge(corner, search.direction);
                return HasRoomToClimb(ledge);
            }
            previousX = x;
        }
        return false;
    }

    // A wall taller than the window ends the search: nothing behind it is reachable.
    private bool IsBlockedAboveWindow((float x, (float minTop, float maxTop) window) column) {
        Vector2 windowTop = new Vector2(column.x, column.window.maxTop + RayHeadroom);
        return OneWayFilter.OverlapsSolidPoint(windowTop, settings.ledgeMask);
    }

    private bool TryFindTopInWindow((float x, (float minTop, float maxTop) window) column, out float topY) {
        Vector2 origin = new Vector2(column.x, column.window.maxTop + RayHeadroom);
        float depth = column.window.maxTop - column.window.minTop + RayHeadroom;
        RaycastHit2D hit = OneWayFilter.Raycast((origin, Vector2.down, depth), settings.ledgeMask, OneWaySupport.None);

        topY = hit.point.y;
        return hit.collider != null && hit.distance > 0f && LedgeGrabPolicy.IsInWindow(topY, column.window);
    }

    // Binary search between an open column and a column with a top, converging on the edge.
    private float RefineEdgeX((float openX, float solidX, (float minTop, float maxTop) window) bracket) {
        float openX = bracket.openX;
        float solidX = bracket.solidX;
        for (int iteration = 0; iteration < EdgeRefineIterations; iteration++) {
            float middleX = (openX + solidX) / 2f;
            if (TryFindTopInWindow((middleX, bracket.window), out _)) {
                solidX = middleX;
            }
            else {
                openX = middleX;
            }
        }
        return solidX;
    }

    private bool HasRoomToClimb(Ledge ledge) => HasStandingRoom(ledge) && HasDeepEnoughTop(ledge);

    private bool HasStandingRoom(Ledge ledge) {
        Vector2 size = settings.standingRoom;
        Vector2 center = ledge.Corner + new Vector2(ledge.ClimbDirection * size.x / 2f, size.y / 2f + RayHeadroom);
        return !OneWayFilter.OverlapsSolidBox((center, size), settings.ledgeMask);
    }

    private bool HasDeepEnoughTop(Ledge ledge) {
        Vector2 origin = ledge.Corner + new Vector2(ledge.ClimbDirection * settings.minTopDepth, TopTolerance);
        RaycastHit2D hit = OneWayFilter.Raycast((origin, Vector2.down, 2f * TopTolerance), settings.ledgeMask, OneWaySupport.None);
        return hit.collider != null && hit.distance > 0f;
    }
}
