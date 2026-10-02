using UnityEngine;

// How the gait answers a planted foot trailing the hip further than a normal stance. A little further (speeding up)
// is routine: the cadence quickens, shortening the stride on the shared clock, so the next steps come sooner while both
// legs stay half a cycle apart. Only a foot far behind (a real loss of support, e.g. being shoved) takes an emergency
// balance step out of turn, which re-phases the clock, and never twice within the cooldown.
public static class SupportRecovery {
    public readonly struct Response {
        public Response((float catchUp, bool needsBalanceStep) response) {
            CatchUp = response.catchUp;
            NeedsBalanceStep = response.needsBalanceStep;
        }

        // Cadence multiplier, 1 when the feet keep up.
        public float CatchUp { get; }
        public bool NeedsBalanceStep { get; }
    }

    // trailExcess: how much further behind the hip the furthest planted foot is than a normal stance ends.
    public static Response Assess((float trailExcess, float secondsSinceBalanceStep) support, BalanceSettings settings) {
        float beyondCatchUp = support.trailExcess - settings.catchUpMargin;
        float catchUp = 1f + Mathf.Clamp(beyondCatchUp * settings.catchUpGain, 0f, settings.maxCatchUp - 1f);
        bool needsBalanceStep = support.trailExcess > settings.emergencyMargin && support.secondsSinceBalanceStep >= settings.emergencyCooldown;
        return new Response((catchUp, needsBalanceStep));
    }
}
