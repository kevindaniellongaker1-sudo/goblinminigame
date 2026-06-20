# OWSATH — One Who Stands Against The Horde

Console combat game built in C# .NET 8. Single file `Program.cs` (~4000 lines).
Raylib-cs 6.1.1 graphics on main thread; game logic on background thread.
Development branch: `claude/todo-implementation-ymd2ro`

## Project structure

- `Program.cs` — all game logic (top-level statements + local functions + classes at the bottom)
- `GraphicsDisplay.cs` — Raylib rendering, reads from `SharedGameState`
- `goblinminigame.csproj` — .NET 8, Raylib-cs 6.1.1

## Architecture notes

- `RunGameLogic(SharedGameState)` is the master local function — everything lives inside it
- `CombatSession` class holds a single combat loop; player is `P`, enemies are `Active`
- `Enemy.Alive` is a **computed property** (`HP > 0 && !Fled`) — never set it directly
- `Enemy.Fled = true` to make an enemy flee (not `Alive = false`)
- `GainXP(int xp)` iterates `allPlayers`; XP is × 0.9 per player when party > 1
- Wave spawn makes at most **12 companion rolls** at group start; the group can still grow beyond 12 enemies from those rolls, and in-combat reinforcements (Pending) are unlimited

## Character types

| Type | Weapon | Special |
|---|---|---|
| Mage | Wand + Staff | Air Blade, Air Wave |
| Priest | Unarmed | Prayers: Healing, Forgiveness, Lord's Prayer |
| Warrior | 2× Hand Axe | Bonus actions scale with level |
| Duelist | Rapier + daggers | Duelist Points, special actions |
| Archer | Bow + Short Sword | 50 arrows, arrow crafting |
| Martial Artist | Unarmed | Martial art style, grapple/throw |
| Berserker | Great Axe | Whirlwind spin, Rage (survive lethal hits, heal after rage) |

## Enemy roster

| Enemy | First appears | Notes |
|---|---|---|
| Goblin | Wave 1 | Basic |
| Hobgoblin | Wave 11 | Tougher |
| Orc | Wave 21 | |
| OrcBarbarian | Wave 41+ (roll) | Double-tap |
| Troll | Wave 31 | Regenerates 2-4 HP/turn; flees at HP≤4, returns healed with companions |
| SpellGoblin | Wave 51+ (roll) | Magic attacks |
| Ogre | Wave 41 | Club sweep, companions per roll |
| NecromancerTroll | Wave 71+ (roll) | Raises dead, negative touch 2d4, flees + returns with undead army |

## Key systems

### Non-lethal damage
Weapons: unarmed, Staff, Ogre Club, Mace, War Mace, Warhammer → KO living enemies.
Undead always take lethal damage. `IsNonLethalAttack()` + `ResolveDowned()` centralize this.

### Multiplayer (1-4 players)
- Asked at startup; each player picks load or new character independently
- All players get XP when any enemy dies/is KO'd (×0.9 per player when >1)
- Wave starts at lowest `GroupsDefeated` across all players
- All player saves are written on flee or go-home

### Save system
- Per-character `.sav` files in the game save directory
- File name = alphanumeric characters of player name
- `TryLoadGame` sets `p.GroupsDefeated`; `SaveGame` writes all fields including `GroupsDefeated`

### Necromancy
- `RaiseDead(corpse, necro)` — restores HP, sets `IsUndead=true`, strips all feats
- `MakeUndeadEnemy(e)` — same transformation for freshly constructed enemies
- Undead trolls (`Troll` with `IsUndead=true`) skip the regeneration step
- NecromancerTroll flees at HP≤4; returns in 3 turns with 1-3 undead Orc/Troll/Ogre companions

### Berserker rage
- Rage points reset to full (`1 + (level-2)/4+1` from level 2) at the start of each wave
- `IsRaging`, `RageTurnsLeft`, `RagePointsSpent` also reset at wave start
- Rage ends naturally: heal 1d4 × rage points spent

## Suggestions for next session

- **Multiplayer combat turns**: currently only player 1 fights; consider adding turn-taking so each player acts in sequence per round
- **Rest between waves heals all party members**, not just player 1
- **Undead weaknesses**: add a system where holy/fire damage deals bonus damage to undead (Priest prayers already partially do this via radiant smite)
- **Troll ally**: after wave 50+, could a reformed troll join the party as a temporary ally?
- **Goblin variants**: sniper goblin (ranged), shaman goblin (buffs allies), bomb goblin (AoE on death)
- **Difficulty scaling after wave 70**: group composition feels thin — consider adding elite versions of existing enemies with bonus feats
- **Equipment drops**: enemies could drop usable items (potions, weapons) with some probability
- **Berserker whirlwind**: verify that the hit count scales correctly every 3 levels starting at level 2
- **Status display**: during combat, show all party members' HP/level in the header line (currently only shows player 1)
- **Save on level-up**: auto-save after a level-up so progress is never lost mid-session
