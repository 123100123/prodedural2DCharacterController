// Every sound the character makes; each is a synthesized clip (a few variants for the ones heard most).
public enum SoundId {
    Footstep,
    Landing,
    Jump,
    Roll,
    Gunshot,
    DryClick,
    MagazineOut,
    MagazineIn,
    Casing,
    TargetHit,
    Ricochet,
    // A bullet knocking on a wooden target, and the crack and clatter of the target breaking.
    WoodKnock,
    WoodBreak,
}

// How the mix settings group the sounds.
public enum SoundCategory {
    Steps,
    Landing,
    Weapon,
    Impacts,
}
