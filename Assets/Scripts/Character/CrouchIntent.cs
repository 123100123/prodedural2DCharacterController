// Whether the character is crouched: it crouches when asked to, and a character crouched under a ceiling too low to
// stand under stays crouched until there is room. States that cannot crouch (rolling, climbing) never do.
public static class CrouchIntent {
    public static bool Resolve((bool requested, bool alreadyCrouched, bool headroomBlocked) crouch, bool allowed) {
        return allowed && (crouch.requested || (crouch.alreadyCrouched && crouch.headroomBlocked));
    }
}
