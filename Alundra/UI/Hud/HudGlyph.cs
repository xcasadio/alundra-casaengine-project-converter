#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Session;

namespace Alundra.UI.Hud;

/// <summary>
/// E13 C2 (docs/plan-e13-hud.md, tranche "Écran MGUI : la composition statique"): the semantic identity
/// of one of the jauge's 24 ACTIVE tiles (relevé C, all 24 re-read from data-extracted/ui/wind.json's own
/// array position, reproduced identically by alundra-project/UI/wind-sprites.json's own "index"/
/// "asset_id" - the wind index -> asset-id mapping this enum stands in for lives on
/// <see cref="HudScreen"/>, never here, so this file stays MGUI/asset-agnostic (mission item 7's
/// own "fonction pure").
/// </summary>
public enum HudGlyph
{
    Digit0,
    Digit1,
    Digit2,
    Digit3,
    Digit4,
    Digit5,
    Digit6,
    Digit7,
    Digit8,
    Digit9,
    Slash,
    BigHeartFull,
    BigHeartEmpty,
    SmallHeartFull,
    SmallHeartEmpty,
    MagicPipFull0,
    MagicPipFull1,
    MagicPipFull2,
    MagicPipFull3,
    MagicPipEmpty,
    Coin0,
    Coin1,
    Coin2,
    Coin3,
}
