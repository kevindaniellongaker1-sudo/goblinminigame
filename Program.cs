using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

var rng = new Random();
var player = new Player(rng);
int groupsDefeated = 0;

Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("   One Who Stands Against The Horde  (OWSATH)       ");
Console.WriteLine("═══════════════════════════════════════════════════════");

ShowHiscores();

Console.WriteLine("\n[N]ew game  [L]oad saved game");
Console.Write("Choice: ");
string startChoice = (Console.ReadLine() ?? "n").Trim().ToLower();

if (startChoice.StartsWith("l"))
{
    var saves = ListSaves();
    if (!saves.Any())
    {
        Console.WriteLine("  No saved games found. Starting new game.");
        AskName(player);
    }
    else
    {
        Console.WriteLine("\n── Saved Games ──");
        for (int i = 0; i < saves.Count; i++)
            Console.WriteLine($"  [{i + 1}] {saves[i].name,-22}  Wave {saves[i].wave,3}  Level {saves[i].level}");
        Console.Write("Enter number or name to load (Enter = new game): ");
        string pick = (Console.ReadLine() ?? "").Trim();

        bool loaded = false;
        if (int.TryParse(pick, out int idx) && idx >= 1 && idx <= saves.Count)
            loaded = TryLoadGame(player, saves[idx - 1].path);
        else if (!string.IsNullOrEmpty(pick))
        {
            var match = saves.FirstOrDefault(s => s.name.Equals(pick, StringComparison.OrdinalIgnoreCase));
            if (match.path != null) loaded = TryLoadGame(player, match.path);
        }

        if (loaded)
            Console.WriteLine($"  Loaded! Level {player.Level}, HP {player.HP}/{player.MaxHP}, Wave {groupsDefeated + 1}");
        else
        {
            Console.WriteLine("  Starting new game.");
            AskName(player);
        }
    }
}
else
{
    AskName(player);
}

Console.WriteLine($"\nYou are {player.Name}!");
Console.WriteLine($"HP: {player.HP}/{player.MaxHP}\n");

if (player.PendingFeats > 0) SelectFeats(player);

while (true)
{
    int waveNum = groupsDefeated + 1;
    var group = BuildGroup(waveNum, rng);

    Console.WriteLine($"\n──────────────────────────────────");
    Console.WriteLine($" GROUP {waveNum}: {DescribeGroup(group)}");
    Console.WriteLine($"──────────────────────────────────");

    var session = new CombatSession(player, group, rng, XpThreshold, GainXP);
    bool survived = session.Run();

    if (!survived)
    {
        Console.WriteLine($"\n╔═══ YOU HAVE FALLEN ═══╗");
        Console.WriteLine($"  Reached wave {waveNum}  Groups defeated: {groupsDefeated}  Level: {player.Level}");
        UpdateHiscores(player.Name, waveNum, player.Level);
        break;
    }

    groupsDefeated++;
    Console.WriteLine($"\n✓ Group {groupsDefeated} cleared!  HP: {player.HP}/{player.MaxHP}  XP: {player.XP}  Level: {player.Level}");
    while (player.PendingFeats > 0) SelectFeats(player);

    if (session.PlayerFled)
    {
        SaveGame(player, groupsDefeated);
        Console.WriteLine("  (Auto-saved after fleeing.)");
    }

    Console.WriteLine("\n[1] Move forward  [2] Rest and recover HP  [3] Go home");
    Console.Write("Choice: ");
    string next = (Console.ReadLine() ?? "1").Trim().ToLower();

    if (next is "3" or "home" or "quit" or "q" or "go home")
    {
        Console.WriteLine($"\nYou return home! Groups defeated: {groupsDefeated}  Level: {player.Level}. Well done!");
        SaveGame(player, groupsDefeated);
        break;
    }
    if (next is "2" or "rest" or "heal")
    {
        int dice = 1 + player.GetFeatStacks("Potion Brewer");
        int recovered = 0;
        for (int d = 0; d < dice; d++) recovered += rng.Next(player.MinPotionHeal, player.MaxPotionHeal + 1);
        recovered = Math.Min(recovered, player.MaxHP - player.HP);
        player.HP += recovered;
        Console.WriteLine($"You rest and recover {recovered} HP. ({player.HP}/{player.MaxHP})");
    }
}

Console.WriteLine("\nThanks for playing!");

// ── Helpers ───────────────────────────────────────────────────────────────

List<Enemy> BuildGroup(int waveNum, Random r)
{
    var g = new List<Enemy>();
    if (waveNum <= 10)
    {
        for (int i = 0; i < waveNum; i++) g.Add(new Goblin(r, $"Goblin {i + 1}"));
    }
    else if (waveNum <= 20)
    {
        int hobs = Math.Min(waveNum - 10, 10);
        int gobs = Math.Max(0, 21 - waveNum);
        for (int i = 0; i < hobs; i++) g.Add(new Hobgoblin(r, $"Hobgoblin {i + 1}"));
        for (int i = 0; i < gobs; i++) g.Add(new Goblin(r, $"Goblin {i + 1}"));
    }
    else if (waveNum <= 30)
    {
        // Wave 21-30: orcs replace hobgoblins one-for-one
        int orcs = waveNum - 20;
        int hobs = Math.Max(0, 10 - orcs);
        for (int i = 0; i < hobs; i++) g.Add(new Hobgoblin(r, $"Hobgoblin {i + 1}"));
        for (int i = 0; i < orcs; i++) g.Add(new Orc(r, $"Orc {i + 1}"));
    }
    else if (waveNum <= 40)
    {
        // Wave 31-40: trolls replace orcs one-for-one
        int trolls = waveNum - 30;
        int orcs = Math.Max(0, 10 - trolls);
        for (int i = 0; i < orcs; i++) g.Add(new Orc(r, $"Orc {i + 1}"));
        for (int i = 0; i < trolls; i++) g.Add(new Troll(r, $"Troll {i + 1}"));
    }
    else
    {
        // Wave 41+: ogres replace trolls one-for-one; each ogre brings companions (1d6)
        int ogres = Math.Min(waveNum - 40, 10);
        int trolls = Math.Max(0, 10 - ogres);
        for (int i = 0; i < trolls; i++) g.Add(new Troll(r, $"Troll {i + 1}"));
        for (int i = 0; i < ogres; i++)
        {
            g.Add(new Ogre(r, $"Ogre {i + 1}"));
            int cr = r.Next(1, 7);
            switch (cr)
            {
                case 1: g.Add(new Ogre(r, $"Ogre Extra {i + 1}")); break;
                case 2: case 3: for (int j = 0; j < 3; j++) g.Add(new Orc(r, $"Orc Extra {i * 3 + j + 1}")); break;
                case 4: g.Add(new Troll(r, $"Troll Extra A {i + 1}")); g.Add(new Troll(r, $"Troll Extra B {i + 1}")); break;
                case 5: for (int j = 0; j < 4; j++) g.Add(new Hobgoblin(r, $"Hob Extra {i * 4 + j + 1}")); break;
                default: for (int j = 0; j < 5; j++) g.Add(new Goblin(r, $"Gob Extra {i * 5 + j + 1}")); break;
            }
        }
    }
    return g;
}

string DescribeGroup(List<Enemy> g) =>
    string.Join(", ", g.GroupBy(e => e.TypeName).Select(gr => $"{gr.Count()}x {gr.Key}"));

int XpThreshold(int level)
{
    if (level <= 1) return 0;
    int total = 0, gap = 30;
    for (int i = 1; i < level; i++)
    {
        total += gap;
        // Base rate = 10. Multiplier increases each 10-level tier.
        // ×1→×2→×2.5→×3→×4→×4.5→×5→×5.5→×6→×6.5 (per-tier inc rises by 0.5×10 each bracket past 50).
        // After level 100: double the rate (×13 = 130); holds through level 200.
        int inc = i < 10  ? 10
                : i < 20  ? 20
                : i < 30  ? 25
                : i < 40  ? 30
                : i < 50  ? 40
                : i < 60  ? 45
                : i < 70  ? 50
                : i < 80  ? 55
                : i < 90  ? 60
                : i < 100 ? 65
                : 130;
        gap += inc;
    }
    return total;
}

void GainXP(int xp)
{
    player.XP += xp;
    Console.WriteLine($"  +{xp} XP! (Total: {player.XP}, Level: {player.Level})");
    while (player.XP >= XpThreshold(player.Level + 1))
    {
        player.Level++;
        Console.WriteLine($"\n★★★ LEVEL UP! You are now Level {player.Level}! ★★★");

        if (player.Level >= 2)
        {
            player.SavedStatPoints++;
            Console.WriteLine($"  Stat point gained! (Total saved: {player.SavedStatPoints})");
            SpendStatPoints(player);
        }

        if (player.Level % 5 == 0)
        {
            player.GearPointsAvailable++;
            Console.WriteLine($"  Gear point earned! (Level {player.Level} milestone)");
            SpendGearPoints(player);
        }
    }
}

void SelectFeats(Player p)
{
    while (p.PendingFeats > 0)
    {
        Console.WriteLine("\n═══ FEAT SELECTION ═══");
        var avail = FeatDef.All.Where(f =>
        {
            if (f.Prerequisite != null && !p.HasFeat(f.Prerequisite)) return false;
            if (!f.Stackable && p.HasFeat(f.Name)) return false;
            if (f.MaxStacks > 0 && p.GetFeatStacks(f.Name) >= f.MaxStacks) return false;
            return true;
        }).ToList();

        for (int i = 0; i < avail.Count; i++)
        {
            string pre = avail[i].Prerequisite != null ? $" (Req: {avail[i].Prerequisite})" : "";
            string stk = avail[i].Stackable ? " [stackable]" : "";
            Console.WriteLine($"  [{i + 1}] {avail[i].Name}{pre}{stk}");
            Console.WriteLine($"       {avail[i].Desc}");
        }

        Console.Write($"Select feat (1-{avail.Count}): ");
        if (int.TryParse(Console.ReadLine()?.Trim(), out int fi) && fi >= 1 && fi <= avail.Count)
        {
            var f = avail[fi - 1];
            p.AddFeat(f.Name);
            Console.WriteLine($"✓ Feat gained: {f.Name}");
            p.PendingFeats--;
            if (f.Name == "Toughness")
            {
                int extra = p.MaxHP;
                p.MaxHP *= 2;
                p.HP += extra;
                Console.WriteLine($"  Max HP doubled to {p.MaxHP}!");
            }
        }
        else Console.WriteLine("Invalid. Try again.");
    }
}

void SpendStatPoints(Player p)
{
    while (p.SavedStatPoints > 0)
    {
        Console.WriteLine($"\n═══ STAT POINTS: {p.SavedStatPoints} available ═══");
        Console.WriteLine("  ── 1 point: max stat ──");
        Console.WriteLine($"  [1]  Max Dodge→{p.MaxDodge+1}     [2]  Max Attack→{p.MaxAttack+1}     [3]  Max Grapple→{p.MaxGrapple+1}    [4]  Max Block→{p.MaxBlock+1}");
        Console.WriteLine($"  [5]  Max Damage→{p.MaxDamage+1}    [6]  Max HP→{p.MaxHP+1}         [7]  Max Parry→{p.MaxParry+1}      [8]  Max Bard Song→{p.MaxBardSong+1}");
        Console.WriteLine($"  [9]  Max Grapple Dmg→{p.MaxGrappleDmg+1}  [10] Max Potion Heal→{p.MaxPotionHeal+1}  [11] Max Power Atk→{p.MaxPowerAtk+1}  [12] Max Limb Break→{p.MaxLimbBreak+1}");
        if (p.SavedStatPoints >= 2)
        {
            Console.WriteLine("  ── 2 points: min stat ──");
            Console.WriteLine($"  [13] Min Dodge→{p.MinDodge+1}  [14] Min Attack→{p.MinAttack+1}  [15] Min Grapple→{p.MinGrapple+1}  [16] Min Block→{p.MinBlock+1}");
            Console.WriteLine($"  [17] Min Damage→{p.MinDamage+1}  [18] Min Parry→{p.MinParry+1}  [19] Min Bard Song→{p.MinBardSong+1}  [20] Min Grapple Dmg→{p.MinGrappleDmg+1}");
            Console.WriteLine($"  [21] Min Power Atk→{p.MinPowerAtk+1}  [22] Min Limb Break→{p.MinLimbBreak+1}  [23] Min Potion Heal→{p.MinPotionHeal+1}");
        }
        if (p.SavedStatPoints >= 3)
            Console.WriteLine("  ── 3 points: [24] Extra action/turn   [25] Gear point   [26] Keep saving (exit)");
        if (p.SavedStatPoints >= 4)
            Console.WriteLine("  ── 4 points: [27] Pick a feat");
        Console.Write("  Choice ([S]ave all for later): ");
        string raw = (Console.ReadLine() ?? "s").Trim().ToLower();
        if (raw == "s" || raw == "save") break;
        if (!int.TryParse(raw, out int ch)) { Console.WriteLine("  Invalid."); continue; }
        bool exitLoop = false;
        switch (ch)
        {
            case 1:  p.MaxDodge++;      p.SavedStatPoints--;  Console.WriteLine($"  Max Dodge → {p.MaxDodge}"); break;
            case 2:  p.MaxAttack++;     p.SavedStatPoints--;  Console.WriteLine($"  Max Attack → {p.MaxAttack}"); break;
            case 3:  p.MaxGrapple++;    p.SavedStatPoints--;  Console.WriteLine($"  Max Grapple → {p.MaxGrapple}"); break;
            case 4:  p.MaxBlock++;      p.SavedStatPoints--;  Console.WriteLine($"  Max Block → {p.MaxBlock}"); break;
            case 5:  p.MaxDamage++;     p.SavedStatPoints--;  Console.WriteLine($"  Max Damage → {p.MaxDamage}"); break;
            case 6:  p.MaxHP++;         p.SavedStatPoints--;  Console.WriteLine($"  Max HP → {p.MaxHP}"); break;
            case 7:  p.MaxParry++;      p.SavedStatPoints--;  Console.WriteLine($"  Max Parry → {p.MaxParry}"); break;
            case 8:  p.MaxBardSong++;   p.SavedStatPoints--;  Console.WriteLine($"  Max Bard Song → {p.MaxBardSong}"); break;
            case 9:  p.MaxGrappleDmg++; p.SavedStatPoints--;  Console.WriteLine($"  Max Grapple Dmg → {p.MaxGrappleDmg}"); break;
            case 10: p.MaxPotionHeal++; p.SavedStatPoints--;  Console.WriteLine($"  Max Potion Heal → {p.MaxPotionHeal}"); break;
            case 11: p.MaxPowerAtk++;   p.SavedStatPoints--;  Console.WriteLine($"  Max Power Atk → {p.MaxPowerAtk}"); break;
            case 12: p.MaxLimbBreak++;  p.SavedStatPoints--;  Console.WriteLine($"  Max Limb Break → {p.MaxLimbBreak}"); break;
            case 13 when p.SavedStatPoints >= 2: p.MinDodge++;      p.SavedStatPoints -= 2; Console.WriteLine($"  Min Dodge → {p.MinDodge}"); break;
            case 14 when p.SavedStatPoints >= 2: p.MinAttack++;     p.SavedStatPoints -= 2; Console.WriteLine($"  Min Attack → {p.MinAttack}"); break;
            case 15 when p.SavedStatPoints >= 2: p.MinGrapple++;    p.SavedStatPoints -= 2; Console.WriteLine($"  Min Grapple → {p.MinGrapple}"); break;
            case 16 when p.SavedStatPoints >= 2: p.MinBlock++;      p.SavedStatPoints -= 2; Console.WriteLine($"  Min Block → {p.MinBlock}"); break;
            case 17 when p.SavedStatPoints >= 2: p.MinDamage++;     p.SavedStatPoints -= 2; Console.WriteLine($"  Min Damage → {p.MinDamage}"); break;
            case 18 when p.SavedStatPoints >= 2: p.MinParry++;      p.SavedStatPoints -= 2; Console.WriteLine($"  Min Parry → {p.MinParry}"); break;
            case 19 when p.SavedStatPoints >= 2: p.MinBardSong++;   p.SavedStatPoints -= 2; Console.WriteLine($"  Min Bard Song → {p.MinBardSong}"); break;
            case 20 when p.SavedStatPoints >= 2: p.MinGrappleDmg++; p.SavedStatPoints -= 2; Console.WriteLine($"  Min Grapple Dmg → {p.MinGrappleDmg}"); break;
            case 21 when p.SavedStatPoints >= 2: p.MinPowerAtk++;   p.SavedStatPoints -= 2; Console.WriteLine($"  Min Power Atk → {p.MinPowerAtk}"); break;
            case 22 when p.SavedStatPoints >= 2: p.MinLimbBreak++;  p.SavedStatPoints -= 2; Console.WriteLine($"  Min Limb Break → {p.MinLimbBreak}"); break;
            case 23 when p.SavedStatPoints >= 2: p.MinPotionHeal++; p.SavedStatPoints -= 2; Console.WriteLine($"  Min Potion Heal → {p.MinPotionHeal}"); break;
            case 24 when p.SavedStatPoints >= 3: p.AdditionalActions++; p.SavedStatPoints -= 3; Console.WriteLine($"  +1 action/turn! Bonus actions: {p.AdditionalActions}"); break;
            case 25 when p.SavedStatPoints >= 3: p.GearPointsAvailable++; p.SavedStatPoints -= 3; Console.WriteLine("  Gear point gained!"); SpendGearPoints(p); break;
            case 26 when p.SavedStatPoints >= 3: exitLoop = true; break;
            case 27 when p.SavedStatPoints >= 4: p.SavedStatPoints -= 4; p.PendingFeats++; SelectFeats(p); break;
            default: Console.WriteLine("  Invalid choice or insufficient points."); break;
        }
        if (exitLoop) break;
    }
    if (p.SavedStatPoints > 0) Console.WriteLine($"  {p.SavedStatPoints} stat point(s) saved for later.");
}

void SpendGearPoints(Player p)
{
    while (p.GearPointsAvailable > 0)
    {
        Console.WriteLine($"\n═══ GEAR POINTS: {p.GearPointsAvailable} available (each item up to 3×) ═══");
        var items = new (string Key, string Desc, Action<Player> Apply)[]
        {
            ("Armor",         "-1 incoming damage per point",                           pl => { pl.ArmorDamageReduction++; Console.WriteLine($"  Armor: -{pl.ArmorDamageReduction} incoming dmg"); }),
            ("Staff",         "Main damage min and max +1 (non-lethal style)",          pl => { pl.MinDamage++; pl.MaxDamage++; Console.WriteLine($"  Staff: dmg {pl.MinDamage}-{pl.MaxDamage}"); }),
            ("Blade",         "Melee damage min and max +1",                            pl => { pl.MinDamage++; pl.MaxDamage++; Console.WriteLine($"  Blade: dmg {pl.MinDamage}-{pl.MaxDamage}"); }),
            ("Brass Ringlets","Non-weapon (kick/headbutt) damage bonus +1",             pl => { pl.RingletBonus++; Console.WriteLine($"  Brass Ringlets: unarmed bonus +{pl.RingletBonus}"); }),
            ("Instrument",    "Bard song min and max +1",                               pl => { pl.MinBardSong++; pl.MaxBardSong++; Console.WriteLine($"  Instrument: bard song {pl.MinBardSong}-{pl.MaxBardSong}"); }),
            ("Silk",          "Dodge min and max +1",                                   pl => { pl.MinDodge++; pl.MaxDodge++; Console.WriteLine($"  Silk: dodge {pl.MinDodge}-{pl.MaxDodge}"); }),
            ("Arm Band",      "Block min and max +1",                                   pl => { pl.MinBlock++; pl.MaxBlock++; Console.WriteLine($"  Arm Band: block {pl.MinBlock}-{pl.MaxBlock}"); }),
            ("Strength Band", "Grapple and grapple damage min and max +1",              pl => { pl.MinGrapple++; pl.MaxGrapple++; pl.MinGrappleDmg++; pl.MaxGrappleDmg++; Console.WriteLine($"  Strength Band: grapple {pl.MinGrapple}-{pl.MaxGrapple}, dmg {pl.MinGrappleDmg}-{pl.MaxGrappleDmg}"); }),
            ("Fencing",       "Parry min and max +1",                                   pl => { pl.MinParry++; pl.MaxParry++; Console.WriteLine($"  Fencing: parry {pl.MinParry}-{pl.MaxParry}"); }),
            ("Grimoire",      "Learn one spell (Fire Blast, Chain Lightning, Frost Burst)", pl => LearnSpell(pl)),
            ("Alchemy",       "Potion healing min and max +1",                          pl => { pl.MinPotionHeal++; pl.MaxPotionHeal++; Console.WriteLine($"  Alchemy: heal {pl.MinPotionHeal}-{pl.MaxPotionHeal}"); }),
        };
        var avail = items.Where(it => p.GearCounts.GetValueOrDefault(it.Key, 0) < 3).ToList();
        for (int i = 0; i < avail.Count; i++)
        {
            int taken = p.GearCounts.GetValueOrDefault(avail[i].Key, 0);
            Console.WriteLine($"  [{i+1}] {avail[i].Key} ({taken}/3) — {avail[i].Desc}");
        }
        Console.Write("  Choose: ");
        if (int.TryParse(Console.ReadLine()?.Trim(), out int g) && g >= 1 && g <= avail.Count)
        {
            var it = avail[g - 1];
            it.Apply(p);
            p.GearCounts[it.Key] = p.GearCounts.GetValueOrDefault(it.Key, 0) + 1;
            p.GearPointsAvailable--;
        }
        else Console.WriteLine("  Invalid.");
    }
}

void LearnSpell(Player p)
{
    var allSpells = new[] { "Fire Blast", "Chain Lightning", "Frost Burst" };
    var available = allSpells.Where(s => !p.KnownSpells.Contains(s)).ToList();
    if (!available.Any()) { Console.WriteLine("  You already know all spells!"); return; }
    Console.WriteLine("  Available spells:");
    Console.WriteLine("    Fire Blast       — 4-12 dmg to 2-4 enemies; burning 1-4/action for 4-8 actions");
    Console.WriteLine("    Chain Lightning  — 3-6 dmg to 5-11 enemies/action; self-damage if >3 actions");
    Console.WriteLine("    Frost Burst      — 2-8 dmg to 2-8 enemies; -2 to -8 on their rolls for 2-6 actions");
    for (int i = 0; i < available.Count; i++) Console.WriteLine($"  [{i+1}] {available[i]}");
    Console.Write("  Learn: ");
    if (int.TryParse(Console.ReadLine()?.Trim(), out int s) && s >= 1 && s <= available.Count)
    { p.KnownSpells.Add(available[s - 1]); Console.WriteLine($"  ✓ Learned: {available[s - 1]}!"); }
    else Console.WriteLine("  Invalid.");
}

void AskName(Player p)
{
    Console.Write("\nEnter your name (or Enter for 'The Lone Warrior'): ");
    string input = (Console.ReadLine() ?? "").Trim();
    if (!string.IsNullOrEmpty(input)) p.Name = input;
}

string GameSaveDir()
{
    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    if (string.IsNullOrEmpty(desktop) || !Directory.Exists(desktop))
        desktop = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    string dir = Path.Combine(desktop, "OWSATH");
    Directory.CreateDirectory(dir);
    return dir;
}

string SaveFilePath(string name)
{
    string safe = new string(name.Where(c => char.IsLetterOrDigit(c)).ToArray());
    if (string.IsNullOrEmpty(safe)) safe = "default";
    return Path.Combine(GameSaveDir(), $"{safe}.sav");
}

void SaveGame(Player p, int groups)
{
    string path = SaveFilePath(p.Name);
    var lines = new List<string>
    {
        $"Name={p.Name}",
        $"HP={p.HP}", $"MaxHP={p.MaxHP}",
        $"MinAttack={p.MinAttack}", $"MaxAttack={p.MaxAttack}",
        $"MinDamage={p.MinDamage}", $"MaxDamage={p.MaxDamage}",
        $"MinDodge={p.MinDodge}", $"MaxDodge={p.MaxDodge}",
        $"MinGrapple={p.MinGrapple}", $"MaxGrapple={p.MaxGrapple}",
        $"MinGrappleDmg={p.MinGrappleDmg}", $"MaxGrappleDmg={p.MaxGrappleDmg}",
        $"MinBlock={p.MinBlock}", $"MaxBlock={p.MaxBlock}",
        $"MinParry={p.MinParry}", $"MaxParry={p.MaxParry}",
        $"MinBardSong={p.MinBardSong}", $"MaxBardSong={p.MaxBardSong}",
        $"MinPowerAtk={p.MinPowerAtk}", $"MaxPowerAtk={p.MaxPowerAtk}",
        $"MinLimbBreak={p.MinLimbBreak}", $"MaxLimbBreak={p.MaxLimbBreak}",
        $"MinPotionHeal={p.MinPotionHeal}", $"MaxPotionHeal={p.MaxPotionHeal}",
        $"SavedStatPoints={p.SavedStatPoints}",
        $"GearPointsAvailable={p.GearPointsAvailable}",
        $"AdditionalActions={p.AdditionalActions}",
        $"ArmorDamageReduction={p.ArmorDamageReduction}",
        $"RingletBonus={p.RingletBonus}",
        $"Level={p.Level}", $"XP={p.XP}", $"PendingFeats={p.PendingFeats}",
        $"HasGoblinSword={p.HasGoblinSword}",
        $"OffhandMaxDamage={p.OffhandMaxDamage}",
        $"Feats={string.Join("|", p.Feats)}",
        $"FeatStacks={string.Join("|", p.FeatStacks.Select(kv => $"{kv.Key}:{kv.Value}"))}",
        $"GearCounts={string.Join("|", p.GearCounts.Select(kv => $"{kv.Key}:{kv.Value}"))}",
        $"KnownSpells={string.Join("|", p.KnownSpells)}",
        $"GroupsDefeated={groups}",
    };
    File.WriteAllLines(path, lines);
    Console.WriteLine($"  ✓ Game saved ({p.Name}).");
}

bool TryLoadGame(Player p, string filePath)
{
    try
    {
        var dict = File.ReadAllLines(filePath)
            .Where(l => l.Contains('='))
            .ToDictionary(
                l => l[..l.IndexOf('=')],
                l => l[(l.IndexOf('=') + 1)..]);

        string G(string k) => dict.GetValueOrDefault(k, "");
        int I(string k, int def = 0) => int.TryParse(G(k), out int v) ? v : def;
        bool B(string k) => G(k) == "True";

        p.Name = G("Name");
        p.HP = I("HP"); p.MaxHP = I("MaxHP");
        p.MinAttack = I("MinAttack"); p.MaxAttack = I("MaxAttack");
        p.MinDamage = I("MinDamage"); p.MaxDamage = I("MaxDamage");
        p.MinDodge = I("MinDodge"); p.MaxDodge = I("MaxDodge");
        p.MinGrapple = I("MinGrapple"); p.MaxGrapple = I("MaxGrapple");
        p.MinGrappleDmg = I("MinGrappleDmg"); p.MaxGrappleDmg = I("MaxGrappleDmg");
        p.MinBlock = I("MinBlock"); p.MaxBlock = I("MaxBlock");
        p.MinParry = I("MinParry"); p.MaxParry = I("MaxParry");
        p.MinBardSong = I("MinBardSong"); p.MaxBardSong = I("MaxBardSong");
        p.MinPowerAtk = I("MinPowerAtk"); p.MaxPowerAtk = I("MaxPowerAtk");
        p.MinLimbBreak = I("MinLimbBreak"); p.MaxLimbBreak = I("MaxLimbBreak");
        p.MinPotionHeal = I("MinPotionHeal"); p.MaxPotionHeal = I("MaxPotionHeal");
        p.SavedStatPoints = I("SavedStatPoints");
        p.GearPointsAvailable = I("GearPointsAvailable");
        p.AdditionalActions = I("AdditionalActions");
        p.ArmorDamageReduction = I("ArmorDamageReduction");
        p.RingletBonus = I("RingletBonus");
        p.Level = I("Level"); p.XP = I("XP"); p.PendingFeats = I("PendingFeats");
        p.HasGoblinSword = B("HasGoblinSword");
        p.OffhandMaxDamage = I("OffhandMaxDamage");

        p.Feats = G("Feats").Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();
        p.FeatStacks = G("FeatStacks").Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Split(':'))
            .Where(a => a.Length == 2 && int.TryParse(a[1], out _))
            .ToDictionary(a => a[0], a => int.Parse(a[1]));
        p.GearCounts = G("GearCounts").Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Split(':'))
            .Where(a => a.Length == 2 && int.TryParse(a[1], out _))
            .ToDictionary(a => a[0], a => int.Parse(a[1]));
        p.KnownSpells = G("KnownSpells").Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();

        groupsDefeated = I("GroupsDefeated");
        return true;
    }
    catch
    {
        Console.WriteLine("  Save file corrupted. Starting fresh.");
        return false;
    }
}

List<(string name, int wave, int level, string path)> ListSaves()
{
    var result = new List<(string, int, int, string)>();
    string dir = GameSaveDir();
    foreach (var f in Directory.GetFiles(dir, "*.sav").Where(f => !f.Contains("hiscores")))
    {
        try
        {
            var dict = File.ReadAllLines(f)
                .Where(l => l.Contains('='))
                .ToDictionary(l => l[..l.IndexOf('=')], l => l[(l.IndexOf('=') + 1)..]);
            string name = dict.GetValueOrDefault("Name", "Unknown");
            int gd = int.TryParse(dict.GetValueOrDefault("GroupsDefeated", "0"), out int g) ? g : 0;
            int level = int.TryParse(dict.GetValueOrDefault("Level", "1"), out int lv) ? lv : 1;
            result.Add((name, gd + 1, level, f));
        }
        catch { }
    }
    return result.OrderByDescending(s => s.Item2).ThenByDescending(s => s.Item3).ToList();
}

void ShowHiscores()
{
    string scorePath = Path.Combine(GameSaveDir(), "owsath_hiscores.sav");
    if (!File.Exists(scorePath)) return;
    try
    {
        var scores = new List<(string name, int wave, int level)>();
        foreach (var line in File.ReadAllLines(scorePath))
        {
            string nm = "Unknown"; int wv = 0; int lv = 1;
            foreach (var part in line.Split('|'))
            {
                int eq = part.IndexOf('=');
                if (eq < 0) continue;
                string k = part[..eq], v = part[(eq + 1)..];
                if (k == "Name") nm = v;
                else if (k == "Wave" && int.TryParse(v, out int w)) wv = w;
                else if (k == "Level" && int.TryParse(v, out int l)) lv = l;
            }
            if (wv > 0) scores.Add((nm, wv, lv));
        }
        var top = scores.OrderByDescending(s => s.wave).ThenByDescending(s => s.level).Take(3).ToList();
        if (!top.Any()) return;
        Console.WriteLine("\n── Hall of the Fallen ── Top Waves ──");
        for (int i = 0; i < top.Count; i++)
            Console.WriteLine($"  #{i + 1}  {top[i].name,-22}  Wave {top[i].wave,3}  Level {top[i].level,3}");
    }
    catch { }
}

void UpdateHiscores(string name, int wave, int level)
{
    string scorePath = Path.Combine(GameSaveDir(), "owsath_hiscores.sav");
    var scores = new List<(string name, int wave, int level)>();
    if (File.Exists(scorePath))
    {
        try
        {
            foreach (var line in File.ReadAllLines(scorePath))
            {
                string nm = "Unknown"; int wv = 0; int lv = 1;
                foreach (var part in line.Split('|'))
                {
                    int eq = part.IndexOf('=');
                    if (eq < 0) continue;
                    string k = part[..eq], v = part[(eq + 1)..];
                    if (k == "Name") nm = v;
                    else if (k == "Wave" && int.TryParse(v, out int w)) wv = w;
                    else if (k == "Level" && int.TryParse(v, out int l)) lv = l;
                }
                if (wv > 0) scores.Add((nm, wv, lv));
            }
        }
        catch { }
    }
    scores.Add((name, wave, level));
    scores = scores.OrderByDescending(s => s.wave).ThenByDescending(s => s.level).Take(10).ToList();
    File.WriteAllLines(scorePath, scores.Select(s => $"Name={s.name}|Wave={s.wave}|Level={s.level}"));
    Console.WriteLine($"  Score recorded: {name}  Wave {wave}  Level {level}");
}

// ═══════════════════════════════════════════════════════════════════════════
// CLASSES
// ═══════════════════════════════════════════════════════════════════════════

class Player
{
    public string Name = "The Lone Warrior";
    public int HP, MaxHP;
    public int MinAttack = 1, MaxAttack = 6;
    public int MinDamage = 1, MaxDamage = 9;
    public int MinDodge = 1, MaxDodge = 6;
    public int MinGrapple = 1, MaxGrapple = 6;
    public int MinGrappleDmg = 1, MaxGrappleDmg = 4;
    public int MinBlock = 1, MaxBlock = 6;
    public int MinParry = 1, MaxParry = 6;
    public int MinBardSong = 1, MaxBardSong = 6;
    public int MinPowerAtk = 4, MaxPowerAtk = 4;
    public int MinLimbBreak = 1, MaxLimbBreak = 6;
    public int MinPotionHeal = 1, MaxPotionHeal = 6;
    public int SavedStatPoints = 0;
    public int GearPointsAvailable = 0;
    public int AdditionalActions = 0;
    public int ArmorDamageReduction = 0;
    public int RingletBonus = 0;
    public int ChainLightningUses = 0;
    public int Level = 1, XP = 0, PendingFeats = 1;
    public bool Defending = false;
    public bool HasGoblinSword = false;
    public int OffhandMaxDamage = 4;
    public bool IsGrappled = false;
    public Enemy? GrappledBy = null;
    public List<string> Feats = new();
    public Dictionary<string, int> FeatStacks = new();
    public Dictionary<string, int> GearCounts = new();
    public List<string> KnownSpells = new();

    public Player(Random rng)
    {
        MaxHP = rng.Next(3, 13);
        HP = MaxHP;
    }

    public bool HasFeat(string name) => Feats.Contains(name);
    public int GetFeatStacks(string name) => FeatStacks.GetValueOrDefault(name, HasFeat(name) ? 1 : 0);

    public void AddFeat(string name)
    {
        if (!Feats.Contains(name)) Feats.Add(name);
        FeatStacks[name] = GetFeatStacks(name) + (FeatDef.All.First(f => f.Name == name).Stackable ? 1 : 0);
        if (!FeatStacks.ContainsKey(name)) FeatStacks[name] = 1;
    }

    public void ClearRoundEffects() { Defending = false; ChainLightningUses = 0; }
}

abstract class Enemy
{
    public string Name;
    public string TypeName;
    public int HP, MaxHP;
    public int MinAttack, MaxAttack;
    public int MinDamage, MaxDamage;
    public int MinDodge, MaxDodge;
    public int XPValue;
    public bool Alive = true;
    public bool KnockedDown = false;
    public bool KnockedOut = false;
    public bool OffBalance = false;
    public bool Disarmed = false;
    public bool Grappled = false;
    public bool CanMove = true;
    public bool Charmed = false;
    public bool HasFledBefore = false;
    public int ConsecutiveDmgTurns = 0;
    public int HpAtTurnStart = 0;
    public bool GrappleNextTurn = false;
    public int KOTurns = 0;
    public int KOCount = 0;
    public bool XpAwarded = false;
    public int BleedDmg = 0;
    public int BurningDmg = 0, BurningTurns = 0;
    public int FrostPenalty = 0, FrostTurns = 0;
    public int DodgePenalty = 0;
    public int AttackPenalty = 0;
    public int WeaponDistance = 0;
    public int MinGrapple = 1, MaxGrapple = 6;
    public int GrappleDmgMin = 1, GrappleDmgMax = 6;
    public bool HasDoubleTap = false;
    public bool HasBlock = false;
    public bool HasParry = false;
    public int BlockMin = 1, BlockMax = 6;
    public bool MagicResistant = false;
    public bool HitBySpell = false;
    public bool HasKick = false;
    public int KickDmgMin = 1, KickDmgMax = 4;
    public bool HasArmBlock = false;
    public bool MagicVulnerable = false;
    public int ToughHideMin = 0, ToughHideMax = 0;
    public int OffhandMinAtk = 1, OffhandMaxAtk = 6;
    public int OffhandMinDmg = 1, OffhandMaxDmg = 4;
    public bool PowerAttackMode = false;
    public bool DroppedWeapon = false;

    public Enemy(string name, string typeName) { Name = name; TypeName = typeName; }

    public string DisplayStatus()
    {
        var parts = new List<string> { $"{Name}: HP {HP}/{MaxHP}" };
        if (KnockedOut) parts.Add($"KO({KOTurns}t)");
        if (KnockedDown) parts.Add("Down");
        if (OffBalance) parts.Add("Off-balance");
        if (Disarmed) parts.Add($"Disarmed({WeaponDistance}ft)");
        if (Grappled) parts.Add("Grappled");
        if (BleedDmg > 0) parts.Add($"Bleed({BleedDmg})");
        if (BurningDmg > 0) parts.Add($"Burning({BurningDmg}×{BurningTurns}t)");
        if (FrostPenalty > 0) parts.Add($"Frozen(-{FrostPenalty}/{FrostTurns}t)");
        if (Charmed) parts.Add("Charmed");
        return string.Join(" ", parts);
    }

    public string ShortStatus() => $"{Name} HP:{HP}/{MaxHP}";

    public void EndOfRound()
    {
        OffBalance = false;
        HitBySpell = false;
        DodgePenalty = Math.Max(0, DodgePenalty - 2);
        AttackPenalty = Math.Max(0, AttackPenalty - 2);
        Charmed = false;
    }
}

class Goblin : Enemy
{
    public Goblin(Random rng, string name) : base(name, "Goblin")
    {
        MaxHP = rng.Next(2, 9); HP = MaxHP;
        MinAttack = 1; MaxAttack = 6;
        MinDamage = 1; MaxDamage = 6;
        MinDodge = 1; MaxDodge = 6;
        XPValue = 10;
    }
}

class Hobgoblin : Enemy
{
    public Hobgoblin(Random rng, string name) : base(name, "Hobgoblin")
    {
        MaxHP = 16; HP = MaxHP;
        MinAttack = 1; MaxAttack = 8;
        MinDamage = 1; MaxDamage = 8;
        MinDodge = 2; MaxDodge = 8;
        MinGrapple = 2; MaxGrapple = 8;
        GrappleDmgMin = 1; GrappleDmgMax = 4;
        XPValue = 20;
    }
}

class Orc : Enemy
{
    public Orc(Random rng, string name) : base(name, "Orc")
    {
        MaxHP = 25; HP = MaxHP;
        MinAttack = 3; MaxAttack = 9;
        MinDamage = 2; MaxDamage = 10;
        MinDodge = 2; MaxDodge = 8;
        MinGrapple = 3; MaxGrapple = 12;
        GrappleDmgMin = 2; GrappleDmgMax = 8;
        HasDoubleTap = true;
        HasBlock = true; BlockMin = 2; BlockMax = 10;
        XPValue = 30;
    }
}

class Troll : Enemy
{
    public Troll(Random rng, string name) : base(name, "Troll")
    {
        MaxHP = 28; HP = MaxHP;
        MinAttack = 2; MaxAttack = 12;
        MinDamage = 3; MaxDamage = 12;
        MinDodge = 1; MaxDodge = 6;
        MinGrapple = 2; MaxGrapple = 12;
        GrappleDmgMin = 2; GrappleDmgMax = 6;
        HasDoubleTap = true;
        HasParry = true; BlockMin = 2; BlockMax = 12;
        HasKick = true; KickDmgMin = 2; KickDmgMax = 6;
        MagicResistant = true;
        XPValue = 45;
    }
}

class Ogre : Enemy
{
    public Ogre(Random rng, string name) : base(name, "Ogre")
    {
        MaxHP = 45; HP = MaxHP;
        MinAttack = 4; MaxAttack = 16;
        MinDamage = 3; MaxDamage = 12;
        MinDodge = 1; MaxDodge = 6;
        MinGrapple = 4; MaxGrapple = 12;
        GrappleDmgMin = 3; GrappleDmgMax = 9;
        HasDoubleTap = true;
        HasArmBlock = true; BlockMin = 4; BlockMax = 12;
        MagicVulnerable = true;
        ToughHideMin = 1; ToughHideMax = 4;
        OffhandMinAtk = 4; OffhandMaxAtk = 12;
        OffhandMinDmg = 2; OffhandMaxDmg = 8;
        XPValue = 50;
    }
}

class FeatDef
{
    public string Name, Desc;
    public string? Prerequisite;
    public bool Stackable;
    public int MaxStacks;
    public FeatDef(string n, string d, string? pre = null, bool stackable = false, int maxStacks = 0)
    { Name = n; Desc = d; Prerequisite = pre; Stackable = stackable; MaxStacks = maxStacks; }

    public static readonly List<FeatDef> All = new()
    {
        new("Block", "Roll 1-6 vs enemy attack. On success, enemy gets -2 dodge till their turn."),
        new("Parry", "After a successful block, roll 1-6 vs dodge. Success knocks enemy down.", "Block"),
        new("Counter Strike", "On successful dodge, block, or parry get a free attack on the attacker."),
        new("Double Tap", "Each attack action also makes an off-hand attack (1d6 atk, 1d4 dmg)."),
        new("Basic Combo", "Attacks include a kick (1d4 atk, 1d4 dmg).", "Double Tap"),
        new("Fury of Blows", "Attacks add another kick and a headbutt (1d6 atk, 1d4 dmg).", "Basic Combo"),
        new("Power Attack", "Per attack: -2 to attack roll, +4 damage."),
        new("Sunder", "Per attack: -1 atk; on hit roll 1-6: 4-5 = bleed, 6 = double bleed."),
        new("Disarm", "Per attack: target weapon instead of body; on hit, knock weapon 10 ft away."),
        new("Thin the Herd", "Killing an enemy grants a free attack on another adjacent enemy."),
        new("Slayer", "Enemy at low HP: get a free attack per turn against them.", "Thin the Herd"),
        new("Sap", "Per attack: -2 atk; on hit, 1d4 dmg + chance die special effect."),
        new("Opportunist", "Attack debuffed enemies (off-balance, down, KO, disarmed) at stacking -1 penalty.", "Counter Strike"),
        new("Toughness", "Double max HP (up to 3 times).", null, true, 3),
        new("Built", "+1 to minimum damage rolls.", null, true),
        new("Talented", "+1 to minimum attack rolls.", null, true),
        new("Potion Brewer", "Add another d6 to healing potion rolls.", null, true),
        new("Closeliner", "+1 min grapple and +1 min grapple damage rolls.", null, true),
        new("Judo", "On dodge/block/parry/enemy miss: free grapple. On success: hold, throw, or disarm."),
        new("Kehon", "Enemy enters/leaves range, KO, disarmed, or off-balance: instant free grapple."),
        new("Taekwondo", "Break limbs of grappled enemies (double damage; effects by limb)."),
        new("Judo Black Belt", "Grapple 2 enemies from missed attacks; extras auto-knocked down.", "Judo"),
        new("Kehon Black Belt", "Instantly KO grappled enemies (1 action); break free of grapple freely.", "Kehon"),
        new("Taekwondo Black Belt", "Grapple 2 adjacent enemies simultaneously.", "Taekwondo"),
        new("Chidia", "Unarmed: +2 actions per turn; all attacks are non-lethal (max KO 12-48 turns)."),
        new("Chidia Black Belt", "Break weapons/hands with blocks/parries.", "Chidia"),
        new("MMA", "Double min/max damage; +2 attacks and +2 grapples per action.", null),
        new("Bard Song", "Roll 1d6 + stacks vs enemy 2d4; on success, enemies attack each other.", null, true),
    };
}

// ═══════════════════════════════════════════════════════════════════════════
// COMBAT SESSION
// ═══════════════════════════════════════════════════════════════════════════

class CombatSession
{
    readonly Player P;
    readonly Random Rng;
    readonly Func<int, int> XpThreshold;
    readonly Action<int> GainXP;
    List<Enemy> Active;
    List<(List<Enemy> batch, int turns)> Pending = new();
    public bool PlayerFled = false;

    public CombatSession(Player p, List<Enemy> enemies, Random rng, Func<int, int> xpFn, Action<int> gainXp)
    {
        P = p; Active = enemies; Rng = rng; XpThreshold = xpFn; GainXP = gainXp;
    }

    public bool Run()
    {
        int turnNum = 0;
        while (P.HP > 0)
        {
            var alive = Active.Where(e => e.Alive).ToList();
            if (!alive.Any() && !Pending.Any()) break;

            turnNum++;
            Console.WriteLine($"\n━━━━ Turn {turnNum} ━━━━");

            // Arrive reinforcements
            var newPending = new List<(List<Enemy> batch, int turns)>();
            foreach (var (batch, turns) in Pending)
            {
                if (turns <= 0)
                {
                    Console.WriteLine("\n! REINFORCEMENTS ARRIVE !");
                    foreach (var e in batch) { e.Alive = true; e.HP = e.MaxHP; Active.Add(e); Console.WriteLine($"  {e.Name} charges in! (HP:{e.HP})"); }
                }
                else newPending.Add((batch, turns - 1));
            }
            Pending = newPending;

            alive = Active.Where(e => e.Alive).ToList();

            // Bleed
            foreach (var e in alive.ToList())
            {
                if (e.BleedDmg > 0)
                {
                    e.HP -= e.BleedDmg;
                    Console.WriteLine($"  {e.Name} bleeds for {e.BleedDmg}. HP: {e.HP}/{e.MaxHP}");
                    if (!e.Alive) { Console.WriteLine($"  {e.Name} bleeds out!"); if (!e.XpAwarded) { e.XpAwarded = true; GainXP(e.XPValue); } }
                }
            }

            // Burning
            foreach (var e in alive.ToList())
            {
                if (e.BurningDmg > 0)
                {
                    e.HP -= e.BurningDmg;
                    Console.WriteLine($"  {e.Name} burns for {e.BurningDmg}. HP: {e.HP}/{e.MaxHP}");
                    e.BurningTurns--;
                    if (e.BurningTurns <= 0) { e.BurningDmg = 0; Console.WriteLine($"  {e.Name}'s flames die out."); }
                    if (!e.Alive) { Console.WriteLine($"  {e.Name} burns out!"); if (!e.XpAwarded) { e.XpAwarded = true; GainXP(e.XPValue); } }
                }
            }
            // Frost countdown
            foreach (var e in alive.ToList())
            {
                if (e.FrostTurns > 0)
                {
                    e.FrostTurns--;
                    if (e.FrostTurns <= 0) { e.FrostPenalty = 0; Console.WriteLine($"  {e.Name}'s frost clears."); }
                }
            }

            alive = Active.Where(e => e.Alive).ToList();
            if (!alive.Any() && !Pending.Any()) break;

            Console.WriteLine($"\nHP: {P.HP}/{P.MaxHP}  XP: {P.XP}  Level: {P.Level}");
            for (int i = 0; i < alive.Count; i++) Console.WriteLine($"  [{i + 1}] {alive[i].DisplayStatus()}");

            // Snapshot HP so consecutive-damage tracking works after player acts
            foreach (var e in Active.Where(x => x.Alive)) e.HpAtTurnStart = e.HP;

            bool fled = PlayerTurn();
            if (fled) { PlayerFled = true; Console.WriteLine("You escaped!"); return true; }
            if (P.HP <= 0) break;

            alive = Active.Where(e => e.Alive).ToList();
            if (!alive.Any() && !Pending.Any()) break;

            // All alive enemies KO'd (and no reinforcements coming) → player wins
            if (!Pending.Any() && alive.Any() && alive.All(e => e.KnockedOut))
            {
                Console.WriteLine("\nAll enemies are knocked out! You stand victorious.");
                return true;
            }

            EnemyTurn();
            P.ClearRoundEffects();

            foreach (var e in Active.Where(e => e.Alive)) e.EndOfRound();

            // Check again after enemy turn (e.g. charmed enemies KO each other)
            alive = Active.Where(e => e.Alive).ToList();
            if (!Pending.Any() && alive.Any() && alive.All(e => e.KnockedOut))
            {
                Console.WriteLine("\nAll enemies are knocked out! You stand victorious.");
                return true;
            }
        }
        return P.HP > 0;
    }

    // ── PLAYER TURN ──────────────────────────────────────────────────────

    bool PlayerTurn()
    {
        int actLeft = 2 + P.AdditionalActions;
        if (P.HasFeat("Chidia")) actLeft += 2;

        bool fled = false;
        bool justBlocked = false;
        Enemy? blockTarget = null;

        while (actLeft > 0 && !fled && P.HP > 0)
        {
            var alive = Active.Where(e => e.Alive).ToList();
            if (!alive.Any()) break;

            Console.WriteLine($"\n  [Actions: {actLeft}]");

            // While grappled, auto-roll each action to break free
            if (P.IsGrappled && P.GrappledBy != null && P.GrappledBy.Alive)
            {
                bool vsOgre = P.GrappledBy is Ogre;
                int minG = vsOgre ? 8 : P.MinGrapple + P.GetFeatStacks("Closeliner");
                int maxG = vsOgre ? 12 : P.MaxGrapple;
                int pGr = Rng.Next(minG, maxG + 1);
                int eGr = Rng.Next(P.GrappledBy.MinGrapple, P.GrappledBy.MaxGrapple + 1);
                string breakType = vsOgre ? "Counter-grapple (8-12)" : "Break-free";
                Console.WriteLine($"  [GRAPPLED by {P.GrappledBy.Name}] {breakType}: your {pGr} vs their {eGr}.");
                if (pGr >= eGr) { P.IsGrappled = false; P.GrappledBy = null; Console.WriteLine("  You break free!"); }
                else Console.WriteLine("  Still held — you can act but cannot run.");
            }
            else if (P.IsGrappled) { P.IsGrappled = false; P.GrappledBy = null; } // grappler died

            var opts = BuildOpts(justBlocked, alive, blockTarget);
            for (int i = 0; i < opts.Count; i++) Console.Write($"[{i + 1}]{opts[i]}  ");
            Console.WriteLine();
            Console.Write("  Action: ");
            string raw = (Console.ReadLine() ?? "").Trim().ToLower();

            string chosen;
            if (int.TryParse(raw, out int n) && n >= 1 && n <= opts.Count) chosen = opts[n - 1];
            else chosen = opts.FirstOrDefault(o => o.StartsWith(raw)) ?? raw;

            Enemy? target = null;
            if (chosen is "attack" or "grapple" or "block" or "parry" or "sap" or "sunder" or "disarm")
            {
                target = PickTarget(alive);
                if (target == null) continue;
            }

            switch (chosen)
            {
                case "attack":
                    DoAttack(target!);
                    justBlocked = false;
                    break;

                case "grapple":
                    DoGrapple(target!);
                    justBlocked = false;
                    break;

                case "defend":
                    P.Defending = true;
                    Console.WriteLine("  Defensive stance. Incoming damage halved this round.");
                    justBlocked = false;
                    break;

                case "healing potion":
                    DoHeal();
                    justBlocked = false;
                    break;

                case "run":
                {
                    if (P.IsGrappled) { Console.WriteLine("  You can't run while grappled!"); continue; }
                    Console.WriteLine("  You try to escape!");
                    bool blocked = false;
                    foreach (var e in alive)
                    {
                        if (blocked) break;
                        int eAtk = Rng.Next(e.MinAttack, e.MaxAttack + 1) - e.AttackPenalty;
                        int pDdg = Rng.Next(P.MinDodge, P.MaxDodge + 1) - 2;
                        Console.WriteLine($"  {e.Name} reacts! Roll {eAtk} vs your dodge-2 ({pDdg}).");
                        if (eAtk >= pDdg)
                        {
                            int dmg = Rng.Next(e.MinDamage, e.MaxDamage + 1);
                            if (P.Defending) dmg = Math.Max(1, dmg / 2);
                            Console.WriteLine($"  {e.Name} hits you for {dmg}! You fail to escape. HP:{P.HP - dmg}/{P.MaxHP}");
                            P.HP -= dmg;
                            blocked = true;
                        }
                        else Console.WriteLine($"  {e.Name} misses — you slip past!");
                    }
                    if (!blocked) fled = true;
                    justBlocked = false;
                    break;
                }

                case "cast spell":
                {
                    if (!P.KnownSpells.Any()) break;
                    Console.WriteLine("  Known spells:");
                    for (int si = 0; si < P.KnownSpells.Count; si++) Console.WriteLine($"  [{si+1}] {P.KnownSpells[si]}");
                    Console.Write("  Cast which: ");
                    if (int.TryParse(Console.ReadLine()?.Trim(), out int si2) && si2 >= 1 && si2 <= P.KnownSpells.Count)
                        DoSpell(P.KnownSpells[si2 - 1], alive);
                    else Console.WriteLine("  Invalid.");
                    justBlocked = false;
                    break;
                }

                case "block":
                {
                    if (target == null) break;
                    if (target is Ogre && target.PowerAttackMode)
                    {
                        int rawDmg = Math.Max(1, Rng.Next(target.MinDamage, target.MaxDamage + 1) / 2);
                        rawDmg = ReduceByToughHide(target, rawDmg);
                        Console.WriteLine($"  The ogre's power attack overwhelms your guard! You take {rawDmg} damage. HP:{P.HP - rawDmg}/{P.MaxHP}");
                        P.HP -= rawDmg;
                        justBlocked = false;
                        break;
                    }
                    int bRoll = Rng.Next(P.MinBlock, P.MaxBlock + 1);
                    int eAtk = Rng.Next(target.MinAttack, target.MaxAttack + 1);
                    Console.WriteLine($"  Block! Roll {bRoll} vs {target.Name}'s attack {eAtk}.");
                    if (bRoll >= eAtk)
                    {
                        Console.WriteLine($"  Blocked! {target.Name} is off-balance (-2 dodge).");
                        target.DodgePenalty += 2; target.OffBalance = true;
                        justBlocked = true; blockTarget = target;
                        FreeAttackPrompt("Counter Strike", target);
                        if (P.HasFeat("Judo")) JudoPrompt(target);
                        if (P.HasFeat("Chidia Black Belt") && bRoll == 6)
                            Console.WriteLine($"  Chidia Black Belt! {target.Name}'s weapon arm is damaged!");
                    }
                    else
                    {
                        int dmg = Math.Max(1, Rng.Next(target.MinDamage, target.MaxDamage + 1) / 2);
                        Console.WriteLine($"  Block failed! You take {dmg} damage. HP: {P.HP - dmg}/{P.MaxHP}");
                        P.HP -= dmg;
                        justBlocked = false;
                    }
                    break;
                }

                case "parry":
                {
                    if (blockTarget == null || !blockTarget.Alive) { Console.WriteLine("  No blocked target for parry."); continue; }
                    if (blockTarget is Ogre) { Console.WriteLine("  You can't parry an ogre — they're too massive!"); justBlocked = false; continue; }
                    int pRoll = Rng.Next(P.MinParry, P.MaxParry + 1);
                    int pDdg = Rng.Next(blockTarget.MinDodge, blockTarget.MaxDodge + 1);
                    Console.WriteLine($"  Parry! Roll {pRoll} vs {blockTarget.Name}'s dodge {pDdg}.");
                    if (pRoll >= pDdg)
                    {
                        Console.WriteLine($"  Parry! {blockTarget.Name} is knocked off their feet!");
                        blockTarget.KnockedDown = true; blockTarget.OffBalance = true;
                        FreeAttackPrompt("Counter Strike", blockTarget);
                        if (P.HasFeat("Judo")) JudoPrompt(blockTarget);
                    }
                    else Console.WriteLine("  Parry failed!");
                    justBlocked = false;
                    break;
                }

                case "bard song":
                {
                    int stacks = Math.Max(1, P.GetFeatStacks("Bard Song"));
                    int songRoll = Rng.Next(P.MinBardSong, P.MaxBardSong + 1) + stacks;
                    Console.WriteLine($"  Bard Song! Roll {songRoll}.");
                    foreach (var e in alive)
                    {
                        int res = Rng.Next(2, 9);
                        if (songRoll > res) { e.Charmed = true; Console.WriteLine($"  {e.Name} charmed!"); }
                        else Console.WriteLine($"  {e.Name} resists (rolled {res}).");
                    }
                    justBlocked = false;
                    break;
                }

                case "pick up goblin sword":
                    P.HasGoblinSword = true;
                    P.OffhandMaxDamage += 2;
                    Console.WriteLine($"  You grab a goblin sword! Off-hand max damage → {P.OffhandMaxDamage}.");
                    break;

                default:
                    Console.WriteLine($"  Unknown action '{chosen}'. Try again.");
                    continue;
            }

            actLeft--;
        }

        // End of player turn: Opportunist checks
        if (P.HasFeat("Opportunist"))
        {
            var debuffed = Active.Where(e => e.Alive && (e.OffBalance || e.KnockedDown || e.KnockedOut || e.Disarmed)).ToList();
            if (debuffed.Any()) OpportunistAttacks(debuffed);
        }

        return fled;
    }

    List<string> BuildOpts(bool justBlocked, List<Enemy> alive, Enemy? blockTarget = null)
    {
        var o = new List<string> { "attack", "grapple", "defend", "healing potion", "run" };
        if (P.HasFeat("Block")) o.Add("block");
        if (P.HasFeat("Parry") && justBlocked && !(blockTarget is Ogre)) o.Add("parry");
        if (P.HasFeat("Bard Song")) o.Add("bard song");
        if (P.KnownSpells.Any()) o.Add("cast spell");
        bool deadGoblin = Active.Any(e => !e.Alive && e is Goblin);
        if (P.HasFeat("Double Tap") && deadGoblin && !P.HasGoblinSword) o.Add("pick up goblin sword");
        return o;
    }

    Enemy? PickTarget(List<Enemy> alive)
    {
        if (alive.Count == 1) return alive[0];
        for (int i = 0; i < alive.Count; i++) Console.Write($"[{i + 1}]{alive[i].Name}  ");
        Console.WriteLine();
        Console.Write("  Target #: ");
        if (int.TryParse(Console.ReadLine()?.Trim(), out int ti) && ti >= 1 && ti <= alive.Count)
            return alive[ti - 1];
        Console.WriteLine("  Invalid target.");
        return null;
    }

    // ── ATTACK ────────────────────────────────────────────────────────────

    void DoAttack(Enemy target)
    {
        // Modifiers
        bool usePower = false, useSunder = false, useDisarm = false, useSap = false;
        var mods = new List<string>();
        if (P.HasFeat("Power Attack")) mods.Add("[P]ower(-2atk/+4dmg)");
        if (P.HasFeat("Sunder")) mods.Add("[S]under(-1atk/bleed)");
        if (P.HasFeat("Disarm")) mods.Add("[D]isarm");
        if (P.HasFeat("Sap")) mods.Add("[A]p(-2atk/effects)");
        if (mods.Any())
        {
            Console.Write($"  Modifier? {string.Join("  ", mods)}  [N]one: ");
            string m = (Console.ReadLine() ?? "n").Trim().ToLower();
            usePower = m.StartsWith("p") && P.HasFeat("Power Attack");
            useSunder = m.StartsWith("s") && P.HasFeat("Sunder");
            useDisarm = m.StartsWith("d") && P.HasFeat("Disarm");
            useSap = (m.StartsWith("a")) && P.HasFeat("Sap");
        }

        int atkPen = 0, dmgBonus = 0;
        if (usePower) { atkPen -= 2; dmgBonus += Rng.Next(P.MinPowerAtk, P.MaxPowerAtk + 1); }
        if (useSunder) atkPen--;
        if (useSap) atkPen -= 2;

        int minAtk = P.MinAttack + (P.HasFeat("Talented") ? P.GetFeatStacks("Talented") : 0);
        int maxAtk = P.MaxAttack;
        int minDmg = P.MinDamage + (P.HasFeat("Built") ? P.GetFeatStacks("Built") : 0);
        int maxDmg = P.MaxDamage;
        if (P.HasFeat("MMA")) { minDmg *= 2; maxDmg *= 2; }

        PerformAttack(target, Rng.Next(minAtk, maxAtk + 1) + atkPen, minDmg, maxDmg, dmgBonus, useSunder, useDisarm, useSap);

        // Off-hand (Double Tap)
        if (P.HasFeat("Double Tap") && target.Alive)
        {
            int ofAtk = Rng.Next(1, 7);
            int ofDdg = Rng.Next(target.MinDodge, target.MaxDodge + 1) - target.DodgePenalty;
            Console.WriteLine($"  Off-hand: roll {ofAtk} vs dodge {ofDdg}.");
            if (ofAtk >= ofDdg && !EnemyBlocks(target, ofAtk))
            {
                int ofDmg = Rng.Next(1, P.OffhandMaxDamage + 1);
                TryEnemyArmBlock(target, ofAtk, ref ofDmg);
                ofDmg = ReduceByToughHide(target, ofDmg);
                Console.WriteLine($"  Off-hand HIT! {ofDmg} dmg → {target.Name} HP:{target.HP - ofDmg}/{target.MaxHP}");
                target.HP -= ofDmg;
                if (!target.Alive) HandleKill(target);
            }
            else if (ofAtk < ofDdg) Console.WriteLine("  Off-hand MISS!");
        }

        // Kick (Basic Combo)
        if (P.HasFeat("Basic Combo") && target.Alive) DoKick(target);

        // Fury of Blows: extra kick + headbutt
        if (P.HasFeat("Fury of Blows") && target.Alive)
        {
            DoKick(target);
            if (target.Alive)
            {
                int hbA = Rng.Next(1, 7), hbD = Rng.Next(target.MinDodge, target.MaxDodge + 1) - target.DodgePenalty;
                Console.WriteLine($"  Headbutt: {hbA} vs {hbD}.");
                if (hbA >= hbD && !EnemyBlocks(target, hbA))
                {
                    int d = Rng.Next(1, 5) + P.RingletBonus;
                    TryEnemyArmBlock(target, hbA, ref d);
                    d = ReduceByToughHide(target, d);
                    target.HP -= d;
                    Console.WriteLine($"  Headbutt HIT! {d} dmg.");
                    if (!target.Alive) HandleKill(target);
                }
                else if (hbA < hbD) Console.WriteLine("  Headbutt MISS!");
            }
        }

        // MMA: 2 extra main attacks
        if (P.HasFeat("MMA"))
            for (int i = 0; i < 2 && target.Alive; i++)
            {
                Console.WriteLine($"  MMA bonus attack {i + 1}:");
                PerformAttack(target, Rng.Next(minAtk, maxAtk + 1), minDmg, maxDmg, 0, false, false, false);
            }
    }

    void PerformAttack(Enemy target, int atkRoll, int minDmg, int maxDmg, int dmgBonus, bool sunder, bool disarm, bool sap)
    {
        int ddg = Rng.Next(target.MinDodge, target.MaxDodge + 1) - target.DodgePenalty - target.FrostPenalty;
        Console.WriteLine($"  Attack: {atkRoll} vs {target.Name}'s dodge {ddg}.");

        if (atkRoll < ddg)
        {
            Console.WriteLine("  MISS!");
            if (P.HasFeat("Judo")) JudoPrompt(target);
            return;
        }

        if (EnemyBlocks(target, atkRoll)) return;

        if (disarm)
        {
            Console.WriteLine($"  Disarm HIT! {target.Name}'s weapon flies 10 ft away!");
            target.Disarmed = true; target.WeaponDistance = 10;
            if (P.HasFeat("Opportunist")) OpportunistPromptNote();
            return;
        }

        if (sap)
        {
            int sd = Rng.Next(1, 5);
            int cd = Rng.Next(1, 7);
            Console.WriteLine($"  Sap HIT! {sd} dmg, chance die: {cd}");
            target.HP -= sd;
            switch (cd)
            {
                case 1: Console.WriteLine("  Enemy shrugs off the worst (-2 dmg taken)."); break;
                case 3: case 4:
                    target.OffBalance = true; target.DodgePenalty += 2; target.AttackPenalty += 2;
                    Console.WriteLine($"  {target.Name} off-balance (-2 dodge/-2 atk)!"); break;
                case 5:
                    KnockOut(target); break;
                case 6:
                    KnockOut(target); target.BleedDmg++;
                    Console.WriteLine($"  {target.Name} is also BLEEDING!"); break;
            }
            if (!target.Alive) HandleKill(target);
            return;
        }

        int dmg = Rng.Next(minDmg, maxDmg + 1) + dmgBonus;
        TryEnemyArmBlock(target, atkRoll, ref dmg);
        dmg = ReduceByToughHide(target, dmg);
        Console.WriteLine($"  HIT! {dmg} dmg → {target.Name} HP:{target.HP - dmg}/{target.MaxHP}");
        target.HP -= dmg;

        if (sunder && target.Alive)
        {
            int sd = Rng.Next(1, 7);
            if (sd >= 6) { target.BleedDmg += 2; Console.WriteLine($"  DOUBLE BLEED! ({target.BleedDmg}/turn)"); }
            else if (sd >= 4) { target.BleedDmg++; Console.WriteLine($"  BLEED! ({target.BleedDmg}/turn)"); }
            else Console.WriteLine("  No bleed.");
        }

        if (!target.Alive) { HandleKill(target); return; }

        // Slayer
        if (P.HasFeat("Slayer") && target.HP <= target.MaxHP / 3)
        {
            Console.Write($"  Slayer! {target.Name} is low HP. Free attack? (y/n): ");
            if ((Console.ReadLine() ?? "").Trim().ToLower() == "y") FreeAttack(target);
        }
    }

    void DoKick(Enemy target)
    {
        int kA = Rng.Next(1, 5), kD = Rng.Next(target.MinDodge, target.MaxDodge + 1) - target.DodgePenalty;
        Console.WriteLine($"  Kick: {kA} vs {kD}.");
        if (kA >= kD && !EnemyBlocks(target, kA))
        {
            int d = Rng.Next(1, 5) + P.RingletBonus;
            TryEnemyArmBlock(target, kA, ref d);
            d = ReduceByToughHide(target, d);
            target.HP -= d;
            Console.WriteLine($"  Kick HIT! {d} dmg → {target.Name} HP:{target.HP}/{target.MaxHP}");
            if (!target.Alive) HandleKill(target);
        }
        else if (kA < kD) Console.WriteLine("  Kick MISS!");
    }

    void FreeAttack(Enemy target)
    {
        if (!target.Alive) return;
        int a = Rng.Next(P.MinAttack, P.MaxAttack + 1);
        int d = Rng.Next(target.MinDodge, target.MaxDodge + 1) - target.DodgePenalty;
        Console.WriteLine($"  [Free attack] {a} vs {d}.");
        if (a >= d && !EnemyBlocks(target, a))
        {
            int dmg = Rng.Next(P.MinDamage, P.MaxDamage + 1) + (P.HasFeat("Built") ? P.GetFeatStacks("Built") : 0);
            TryEnemyArmBlock(target, a, ref dmg);
            dmg = ReduceByToughHide(target, dmg);
            target.HP -= dmg;
            Console.WriteLine($"  HIT! {dmg} dmg → {target.Name} HP:{target.HP}/{target.MaxHP}");
            if (!target.Alive) HandleKill(target);
        }
        else if (a < d) Console.WriteLine("  Miss!");
    }

    void FreeAttackPrompt(string feat, Enemy target)
    {
        if (!P.HasFeat(feat) || !target.Alive) return;
        Console.Write($"  {feat}! Free attack on {target.Name}? (y/n): ");
        if ((Console.ReadLine() ?? "").Trim().ToLower() == "y") FreeAttack(target);
    }

    int ReduceByToughHide(Enemy e, int dmg)
    {
        if (e.ToughHideMin <= 0) return dmg;
        int reduction = Rng.Next(e.ToughHideMin, e.ToughHideMax + 1);
        int result = Math.Max(1, dmg - reduction);
        Console.WriteLine($"  {e.Name}'s tough hide absorbs {reduction} damage! ({dmg}→{result})");
        return result;
    }

    bool TryEnemyArmBlock(Enemy target, int atkRoll, ref int dmg)
    {
        if (!target.HasArmBlock) return false;
        if (target.KnockedDown || target.KnockedOut || target.OffBalance) return false;
        int bRoll = Rng.Next(target.BlockMin, target.BlockMax + 1);
        Console.WriteLine($"  {target.Name} raises their arm! Block roll {bRoll} vs attack {atkRoll}.");
        if (bRoll >= atkRoll)
        {
            dmg = Math.Max(1, dmg / 2);
            Console.WriteLine($"  ARM BLOCK! Damage halved to {dmg}.");
            return true;
        }
        Console.WriteLine($"  Arm block failed! ({bRoll} < {atkRoll})");
        return false;
    }

    // Returns true if the enemy successfully blocks or parries the incoming attack.
    // Not available when knocked down, KO'd, or off-balance.
    bool EnemyBlocks(Enemy target, int atkRoll)
    {
        if (!target.HasBlock && !target.HasParry) return false;
        if (target.KnockedDown || target.KnockedOut || target.OffBalance) return false;
        int bRoll = Rng.Next(target.BlockMin, target.BlockMax + 1);
        string verb = target.HasParry ? "parries" : "blocks";
        Console.WriteLine($"  {target.Name} {verb}! Roll {bRoll} vs your attack {atkRoll}.");
        if (bRoll >= atkRoll) { Console.WriteLine($"  {(target.HasParry ? "PARRIED" : "BLOCKED")}!"); return true; }
        Console.WriteLine($"  {(target.HasParry ? "Parry" : "Block")} failed! ({bRoll} < {atkRoll})");
        return false;
    }

    void KnockOut(Enemy e)
    {
        e.KOCount++;
        // 1st KO: 6-8t, 2nd: 8-12t, 3rd: 10-16t, Nth: (4+2N)-(4+4N)t
        int minT = 4 + 2 * e.KOCount;
        int maxT = 4 + 4 * e.KOCount;
        e.KOTurns = Rng.Next(minT, maxT + 1);
        e.KnockedOut = true;
        if (e.HP <= 0) e.HP = 1; // non-lethal: stays at 1 HP
        Console.WriteLine($"  {e.Name} KNOCKED OUT for {e.KOTurns} turns! (KO #{e.KOCount}: {minT}-{maxT}t range)");
        if (!e.XpAwarded) { e.XpAwarded = true; GainXP(e.XPValue); }
    }

    void HandleKill(Enemy e)
    {
        Console.WriteLine($"  {e.Name} is defeated!");
        if (!e.XpAwarded) { e.XpAwarded = true; GainXP(e.XPValue); }
        if (P.IsGrappled && P.GrappledBy == e) { P.IsGrappled = false; P.GrappledBy = null; Console.WriteLine("  You are no longer grappled."); }
        var others = Active.Where(x => x.Alive && x != e).ToList();
        if (P.HasFeat("Thin the Herd") && others.Any())
        {
            Console.Write($"  Thin the Herd! Free attack on another enemy? (y/n): ");
            if ((Console.ReadLine() ?? "").Trim().ToLower() == "y")
            {
                var t = others.Count == 1 ? others[0] : PickTarget(others);
                if (t != null) FreeAttack(t);
            }
        }
    }

    void OpportunistPromptNote() => Console.WriteLine("  (Opportunist: attack them at end of turn!)");

    void OpportunistAttacks(List<Enemy> debuffed)
    {
        Console.WriteLine("\n  [Opportunist] Attack debuffed enemies?");
        for (int i = 0; i < debuffed.Count; i++) Console.WriteLine($"    [{i + 1}] {debuffed[i].ShortStatus()}");
        Console.WriteLine("    [0] Skip");

        int pen = -1;
        while (debuffed.Any(e => e.Alive))
        {
            Console.Write($"  Target (penalty {pen}, 0=stop): ");
            if (!int.TryParse(Console.ReadLine()?.Trim(), out int idx) || idx == 0) break;
            if (idx < 1 || idx > debuffed.Count || !debuffed[idx - 1].Alive) { Console.WriteLine("  Invalid."); continue; }

            var t = debuffed[idx - 1];
            int a = Rng.Next(P.MinAttack, P.MaxAttack + 1) + pen;
            int d = Rng.Next(t.MinDodge, t.MaxDodge + 1) - 4 - t.DodgePenalty;
            Console.WriteLine($"  Opportunist: {a} vs {t.Name}'s dodge {d}.");
            if (a >= d && !EnemyBlocks(t, a))
            {
                int dmg = Rng.Next(P.MinDamage, P.MaxDamage + 1) + (d <= 0 ? 1 : 0);
                TryEnemyArmBlock(t, a, ref dmg);
                dmg = ReduceByToughHide(t, dmg);
                t.HP -= dmg;
                Console.WriteLine($"  HIT! {dmg} dmg → {t.Name} HP:{t.HP}/{t.MaxHP}");
                if (!t.Alive) HandleKill(t);
            }
            else if (a < d) Console.WriteLine("  Miss!");
            pen--;
        }
    }

    // ── GRAPPLE ───────────────────────────────────────────────────────────

    void DoGrapple(Enemy target)
    {
        int minG = P.MinGrapple + P.GetFeatStacks("Closeliner");
        int gRoll = Rng.Next(minG, P.MaxGrapple + 1);
        int dRoll = Rng.Next(target.MinDodge, target.MaxDodge + 1);
        Console.WriteLine($"  Grapple! Roll {gRoll} vs {target.Name}'s dodge {dRoll}.");
        if (gRoll < dRoll) { Console.WriteLine("  Grapple FAILED!"); return; }

        target.Grappled = true;
        Console.WriteLine($"  Grapple SUCCESS! {target.Name} is grappled.");

        string gOpts = P.HasFeat("Judo") ? "[H]old  [T]hrow  [D]isarm" : "[H]old  [T]hrow";
        Console.Write($"  Option: {gOpts}: ");
        string go = (Console.ReadLine() ?? "h").Trim().ToLower();

        if (go.StartsWith("t"))
        {
            target.Grappled = false; target.KnockedDown = true;
            Console.WriteLine($"  {target.Name} thrown to the ground!");
        }
        else if (go.StartsWith("d") && P.HasFeat("Judo"))
        {
            target.Grappled = false; target.Disarmed = true; target.WeaponDistance = 10;
            Console.WriteLine($"  {target.Name}'s weapon wrenched away!");
        }
        else
        {
            int minGD = P.MinGrappleDmg + P.GetFeatStacks("Closeliner");
            int gDmg = Rng.Next(minGD, P.MaxGrappleDmg + 1);
            target.HP -= gDmg;
            Console.WriteLine($"  Grapple damage: {gDmg} → {target.Name} HP:{target.HP}/{target.MaxHP}");
            if (!target.Alive) { HandleKill(target); return; }

            // Taekwondo limb break
            if (P.HasFeat("Taekwondo"))
            {
                Console.Write("  Taekwondo limb break? [L]eg [A]rm [N]eck [B]ack [S]kip: ");
                string lb = (Console.ReadLine() ?? "s").Trim().ToLower();
                int bd = Rng.Next(P.MinLimbBreak, P.MaxLimbBreak + 1);
                switch (lb.Length > 0 ? lb[0] : 's')
                {
                    case 'l': target.HP -= bd; target.CanMove = false; Console.WriteLine($"  Leg broken! {bd} dmg. Can't walk."); break;
                    case 'a': target.HP -= bd; target.Disarmed = true; Console.WriteLine($"  Arm broken! {bd} dmg. Can't hold weapon."); break;
                    case 'n': target.HP = 0; Console.WriteLine("  Neck snapped! Instant kill!"); break;
                    case 'b': target.HP -= bd; target.CanMove = false; target.KnockedDown = true; Console.WriteLine($"  Back broken! {bd} dmg. Can't move."); break;
                }
                if (!target.Alive) HandleKill(target);
            }

            // Kehon Black Belt: instant KO
            if (P.HasFeat("Kehon Black Belt") && target.Alive)
            {
                Console.Write($"  Kehon Black Belt! Instant KO {target.Name}? (y/n): ");
                if ((Console.ReadLine() ?? "").Trim().ToLower() == "y")
                {
                    KnockOut(target);
                    target.Grappled = false;
                }
            }
        }
    }

    void JudoPrompt(Enemy target)
    {
        if (!target.Alive) return;
        Console.Write($"  Judo! Free grapple on {target.Name}? (y/n): ");
        if ((Console.ReadLine() ?? "").Trim().ToLower() == "y") DoGrapple(target);
    }

    void DoHeal()
    {
        int dice = 1 + P.GetFeatStacks("Potion Brewer");
        int h = 0;
        for (int d = 0; d < dice; d++) h += Rng.Next(P.MinPotionHeal, P.MaxPotionHeal + 1);
        h = Math.Min(h, P.MaxHP - P.HP);
        P.HP += h;
        Console.WriteLine($"  Healing potion! +{h} HP. ({P.HP}/{P.MaxHP})");
    }

    void DoSpell(string spell, List<Enemy> alive)
    {
        switch (spell)
        {
            case "Fire Blast":
            {
                int numTargets = Rng.Next(2, 5);
                Console.WriteLine($"  FIRE BLAST! Engulfs {numTargets} enemies in flames.");
                var targets = alive.OrderBy(_ => Rng.Next()).Take(numTargets).ToList();
                int burnDmg = Rng.Next(1, 5);
                int burnTurns = Rng.Next(4, 9);
                foreach (var e in targets)
                {
                    int dmg = Rng.Next(4, 13);
                    if (e.MagicResistant) { dmg = Math.Max(1, dmg / 2); Console.WriteLine($"    (Magic resistant!)"); }
                    else if (e.MagicVulnerable) { dmg = (int)(dmg * 1.5); Console.WriteLine($"    (Magic vulnerable! ×1.5)"); }
                    e.HP -= dmg; e.HitBySpell = true;
                    Console.WriteLine($"    {e.Name} takes {dmg} fire damage! HP:{e.HP}/{e.MaxHP}");
                    if (!e.Alive) { HandleKill(e); continue; }
                    int eBurnDmg = e.MagicResistant ? Math.Max(1, burnDmg / 2) : e.MagicVulnerable ? (int)(burnDmg * 1.5) : burnDmg;
                    e.BurningDmg = Math.Max(e.BurningDmg, eBurnDmg);
                    e.BurningTurns = Math.Max(e.BurningTurns, burnTurns);
                    Console.WriteLine($"    {e.Name} BURNING! ({eBurnDmg}/action × {burnTurns} actions)");
                }
                break;
            }
            case "Chain Lightning":
            {
                int numTargets = Math.Min(Rng.Next(5, 12), alive.Count);
                Console.WriteLine($"  CHAIN LIGHTNING! Arcs through {numTargets} enemies.");
                var targets = alive.OrderBy(_ => Rng.Next()).Take(numTargets).ToList();
                foreach (var e in targets)
                {
                    int dmg = Rng.Next(3, 7);
                    if (e.MagicResistant) { dmg = Math.Max(1, dmg / 2); Console.WriteLine($"    (Magic resistant!)"); }
                    else if (e.MagicVulnerable) { dmg = (int)(dmg * 1.5); Console.WriteLine($"    (Magic vulnerable! ×1.5)"); }
                    e.HP -= dmg; e.HitBySpell = true;
                    Console.WriteLine($"    {e.Name} struck for {dmg} lightning! HP:{e.HP}/{e.MaxHP}");
                    if (!e.Alive) HandleKill(e);
                }
                P.ChainLightningUses++;
                if (P.ChainLightningUses > 3)
                {
                    int selfDmg = Rng.Next(1, 5);
                    P.HP -= selfDmg;
                    Console.WriteLine($"  Channeling backlash! You take {selfDmg} damage. HP:{P.HP}/{P.MaxHP}");
                }
                break;
            }
            case "Frost Burst":
            {
                int numTargets = Math.Min(Rng.Next(2, 9), alive.Count);
                Console.WriteLine($"  FROST BURST! Freezes {numTargets} enemies.");
                var targets = alive.OrderBy(_ => Rng.Next()).Take(numTargets).ToList();
                int frostPen = Rng.Next(2, 9);
                int frostTurns = Rng.Next(2, 7);
                foreach (var e in targets)
                {
                    int dmg = Rng.Next(2, 9);
                    if (e.MagicResistant) { dmg = Math.Max(1, dmg / 2); Console.WriteLine($"    (Magic resistant!)"); }
                    else if (e.MagicVulnerable) { dmg = (int)(dmg * 1.5); Console.WriteLine($"    (Magic vulnerable! ×1.5)"); }
                    e.HP -= dmg; e.HitBySpell = true;
                    Console.WriteLine($"    {e.Name} takes {dmg} frost! HP:{e.HP}/{e.MaxHP}");
                    if (!e.Alive) { HandleKill(e); continue; }
                    int eFrostPen = e.MagicResistant ? Math.Max(1, frostPen / 2) : e.MagicVulnerable ? (int)(frostPen * 1.5) : frostPen;
                    e.FrostPenalty = Math.Max(e.FrostPenalty, eFrostPen);
                    e.FrostTurns = Math.Max(e.FrostTurns, frostTurns);
                    Console.WriteLine($"    {e.Name} FROZEN! (-{eFrostPen} on rolls for {frostTurns} turns)");
                }
                break;
            }
        }
    }

    // ── ENEMY TURN ────────────────────────────────────────────────────────

    void EnemyTurn()
    {
        Console.WriteLine("\n  --- Enemy Turn ---");
        foreach (var e in Active.Where(e => e.Alive).ToList())
        {
            if (!e.Alive) continue;

            // Charmed: attack another enemy
            if (e.Charmed)
            {
                var others = Active.Where(x => x.Alive && x != e).ToList();
                if (others.Any())
                {
                    var victim = others[Rng.Next(others.Count)];
                    int dmg = Rng.Next(e.MinDamage, e.MaxDamage + 1);
                    victim.HP -= dmg;
                    Console.WriteLine($"  {e.Name} (charmed) attacks {victim.Name} for {dmg}! HP:{victim.HP}/{victim.MaxHP}");
                    if (!victim.Alive) { Console.WriteLine($"  {victim.Name} falls to {e.Name}!"); if (!victim.XpAwarded) { victim.XpAwarded = true; GainXP(victim.XPValue); } }
                }
                continue;
            }

            // Hobgoblins: update consecutive-damage counter before acting
            if (e is Hobgoblin)
            {
                if (e.HP < e.HpAtTurnStart) e.ConsecutiveDmgTurns++;
                else e.ConsecutiveDmgTurns = 0;
                if (e.ConsecutiveDmgTurns >= 3) { e.GrappleNextTurn = true; e.ConsecutiveDmgTurns = 0; }
            }

            // Stand up if knocked down
            int actions = (e is Hobgoblin || e is Orc || e is Troll) ? 3 : 2;
            if (e.KnockedDown)
            {
                Console.WriteLine($"  {e.Name} stands up (1 action used).");
                e.KnockedDown = false;
                actions--;
                if (actions <= 0) continue;
            }

            // KO
            if (e.KnockedOut)
            {
                e.KOTurns--;
                Console.WriteLine($"  {e.Name} is knocked out ({Math.Max(0, e.KOTurns)} turns left).");
                if (e.KOTurns <= 0) { e.KnockedOut = false; Console.WriteLine($"  {e.Name} wakes up!"); }
                continue;
            }

            // Retrieve weapon if disarmed
            if (e.Disarmed && e.WeaponDistance > 0 && e.CanMove)
            {
                if (actions >= 2)
                {
                    Console.WriteLine($"  {e.Name} retrieves their weapon.");
                    e.Disarmed = false; e.WeaponDistance = 0; actions -= 2;
                }
                else { Console.WriteLine($"  {e.Name} moves toward their weapon."); actions = 0; }
                if (actions <= 0) continue;
            }

            // ── Goblin flee (HP <= MaxHP/3) ────────────────────────────────
            if (!e.HasFledBefore && e is Goblin && e.HP <= e.MaxHP / 3)
            {
                int fleeRoll = Rng.Next(1, 7);
                if (fleeRoll >= 4)
                {
                    Console.WriteLine($"  {e.Name} tries to flee!");
                    bool stopped = PlayerFreeActionOnFleeingEnemy(e);
                    if (!stopped)
                    {
                        e.HasFledBefore = true;
                        e.Alive = false;
                        var returnedGoblin = new Goblin(Rng, e.Name);
                        Pending.Add((new List<Enemy>
                        {
                            returnedGoblin,
                            new Goblin(Rng, "Goblin Reinforcement A"),
                            new Goblin(Rng, "Goblin Reinforcement B")
                        }, 2));
                    }
                    continue;
                }
            }

            // ── Hobgoblin AI ───────────────────────────────────────────────
            if (e is Hobgoblin)
            {
                // HP <= 5: try to flee
                if (e.HP <= 5 && !e.HasFledBefore)
                {
                    Console.WriteLine($"  {e.Name} is badly wounded and tries to flee!");
                    bool stopped = PlayerFreeActionOnFleeingEnemy(e);
                    if (!stopped)
                    {
                        e.HasFledBefore = true;
                        e.Alive = false;
                        int healAmount = Rng.Next(1, 5);
                        var returnedHob = new Hobgoblin(Rng, e.Name);
                        returnedHob.HP = healAmount;
                        Pending.Add((new List<Enemy> { returnedHob, new Hobgoblin(Rng, "Hobgoblin Reinforcement") }, 2));
                        Console.WriteLine($"  {e.Name} escapes! Will return healed with a friend.");
                    }
                    continue;
                }

                // Grapple triggered by 3 consecutive turns of taking damage
                if (e.GrappleNextTurn && actions > 0)
                {
                    e.GrappleNextTurn = false;
                    int gAtk = Rng.Next(e.MinGrapple, e.MaxGrapple + 1) - e.AttackPenalty;
                    int pDdg = Rng.Next(P.MinDodge, P.MaxDodge + 1);
                    Console.WriteLine($"  {e.Name} grapples! {gAtk} vs your dodge {pDdg}.");
                    if (gAtk >= pDdg)
                    {
                        int gDmg = Rng.Next(e.GrappleDmgMin, e.GrappleDmgMax + 1);
                        P.HP -= gDmg;
                        Console.WriteLine($"  Grappled! {gDmg} crush damage. HP:{P.HP}/{P.MaxHP}");
                    }
                    else Console.WriteLine($"  Grapple attempt failed!");
                    actions--;
                }

                // HP > 6: attack; HP <= 6 but > 5: still attack (not fleeing territory)
                for (int i = 0; i < actions && P.HP > 0; i++)
                    EnemyAttack(e);

                continue;
            }

            // ── Orc AI ─────────────────────────────────────────────────────
            if (e is Orc)
            {
                // Update consecutive-damage counter (4 turns triggers grapple)
                if (e.HP < e.HpAtTurnStart) e.ConsecutiveDmgTurns++;
                else e.ConsecutiveDmgTurns = 0;
                if (e.ConsecutiveDmgTurns >= 4) { e.GrappleNextTurn = true; e.ConsecutiveDmgTurns = 0; }

                // HP <= 5: roll 1-2 → run (1) or grapple (2)
                if (e.HP <= 5 && !e.HasFledBefore)
                {
                    int roll = Rng.Next(1, 3);
                    if (roll == 1)
                    {
                        Console.WriteLine($"  {e.Name} tries to run!");
                        bool stopped = PlayerFreeActionOnFleeingEnemy(e);
                        if (!stopped)
                        {
                            e.HasFledBefore = true;
                            e.Alive = false;
                            int healAmt = Rng.Next(2, 7);
                            int reinfRoll = Rng.Next(1, 6); // 1-2 = orc, 3-5 = two hobgoblins
                            var returnedOrc = new Orc(Rng, e.Name);
                            returnedOrc.HP = healAmt;
                            if (reinfRoll <= 2)
                            {
                                Console.WriteLine($"  {e.Name} will return with another Orc in 3 turns!");
                                Pending.Add((new List<Enemy> { returnedOrc, new Orc(Rng, "Orc Reinforcement") }, 3));
                            }
                            else
                            {
                                Console.WriteLine($"  {e.Name} will return with two Hobgoblins in 3 turns!");
                                Pending.Add((new List<Enemy> { returnedOrc, new Hobgoblin(Rng, "Hobgoblin A"), new Hobgoblin(Rng, "Hobgoblin B") }, 3));
                            }
                        }
                        continue;
                    }
                    else
                    {
                        // Grapple attempt
                        OrcGrappleAction(e);
                        actions--;
                        if (actions <= 0) continue;
                    }
                }

                // Grapple triggered by 4 consecutive damage turns
                if (e.GrappleNextTurn && actions > 0)
                {
                    e.GrappleNextTurn = false;
                    OrcGrappleAction(e);
                    actions--;
                }

                // HP >= 6: attack with remaining actions (maintain grapple if holding player)
                for (int i = 0; i < actions && P.HP > 0; i++)
                {
                    if (P.IsGrappled && P.GrappledBy == e)
                        OrcMaintainGrapple(e);
                    else if (e.HP >= 6)
                        EnemyAttack(e);
                }

                continue;
            }

            // ── Troll AI ───────────────────────────────────────────────────
            if (e is Troll)
            {
                // Update consecutive-damage counter (4 turns triggers grapple)
                if (e.HP < e.HpAtTurnStart) e.ConsecutiveDmgTurns++;
                else e.ConsecutiveDmgTurns = 0;
                if (e.ConsecutiveDmgTurns >= 4) { e.GrappleNextTurn = true; e.ConsecutiveDmgTurns = 0; }
                // Spell hit also triggers grapple
                if (e.HitBySpell) { e.GrappleNextTurn = true; }

                // Free action: regenerate 2-4 HP
                int regen = Rng.Next(2, 5);
                e.HP = Math.Min(e.HP + regen, e.MaxHP);
                Console.WriteLine($"  {e.Name} regenerates {regen} HP! (HP:{e.HP}/{e.MaxHP})");

                // HP <= 4: roll 1d4 — 1=grapple, 2-4=flee
                if (e.HP <= 4 && !e.HasFledBefore)
                {
                    int dieRoll = Rng.Next(1, 5);
                    if (dieRoll == 1)
                    {
                        Console.WriteLine($"  {e.Name} (desperate) grapples!");
                        OrcGrappleAction(e);
                    }
                    else
                    {
                        Console.WriteLine($"  {e.Name} tries to flee!");
                        bool stopped = PlayerFreeActionOnFleeingEnemy(e);
                        if (!stopped)
                        {
                            e.HasFledBefore = true;
                            e.Alive = false;
                            int totalHeal = Rng.Next(2, 5) + Rng.Next(2, 5) + Rng.Next(2, 5) + Rng.Next(1, 5);
                            var returnedTroll = new Troll(Rng, e.Name);
                            returnedTroll.HP = Math.Min(e.HP + totalHeal, returnedTroll.MaxHP);
                            Console.WriteLine($"  {e.Name} escapes! Returns in 3 turns healed {totalHeal} HP.");
                            var batch = new List<Enemy> { returnedTroll };
                            int reinfRoll = Rng.Next(1, 7);
                            switch (reinfRoll)
                            {
                                case 1:
                                    batch.Add(new Troll(Rng, "Troll Backup"));
                                    Console.WriteLine($"  ...with another Troll!");
                                    break;
                                case 2: case 3:
                                    batch.Add(new Orc(Rng, "Orc A")); batch.Add(new Orc(Rng, "Orc B"));
                                    Console.WriteLine($"  ...with two Orcs!");
                                    break;
                                case 4: case 5:
                                    for (int hi = 0; hi < 3; hi++) batch.Add(new Hobgoblin(Rng, $"Hobgoblin {hi+1}"));
                                    Console.WriteLine($"  ...with three Hobgoblins!");
                                    break;
                                default:
                                    for (int gi = 0; gi < 5; gi++) batch.Add(new Goblin(Rng, $"Goblin {gi+1}"));
                                    Console.WriteLine($"  ...with five Goblins!");
                                    break;
                            }
                            Pending.Add((batch, 3));
                        }
                    }
                    continue;
                }

                // HP >= 5: grapple if triggered, then attack
                if (e.GrappleNextTurn && actions > 0)
                {
                    e.GrappleNextTurn = false;
                    OrcGrappleAction(e);
                    actions--;
                }

                for (int i = 0; i < actions && P.HP > 0; i++)
                {
                    if (P.IsGrappled && P.GrappledBy == e)
                        OrcMaintainGrapple(e);
                    else
                    {
                        EnemyAttack(e);
                        if (e.Alive && P.HP > 0) EnemyKick(e);
                    }
                }

                continue;
            }

            // ── Ogre AI ────────────────────────────────────────────────────
            if (e is Ogre)
            {
                // Track consecutive damage turns (5 triggers grapple attempt)
                if (e.HP < e.HpAtTurnStart) e.ConsecutiveDmgTurns++;
                else e.ConsecutiveDmgTurns = 0;
                if (e.ConsecutiveDmgTurns >= 5) { e.GrappleNextTurn = true; e.ConsecutiveDmgTurns = 0; }

                int ogrePct = e.HP * 100 / e.MaxHP;

                // ≤ 9% HP: roll 1d4 per action
                if (ogrePct <= 9 && !e.HasFledBefore)
                {
                    e.PowerAttackMode = false;
                    for (int i = 0; i < actions && P.HP > 0; i++)
                    {
                        int dieRoll = Rng.Next(1, 5);
                        if (dieRoll == 1)
                        {
                            e.DroppedWeapon = true;
                            Console.WriteLine($"  {e.Name} drops their club and grabs with BOTH HANDS!");
                            OgreGrappleAction(e, bothHands: true);
                        }
                        else if (dieRoll == 2)
                        {
                            e.PowerAttackMode = true;
                            EnemyAttack(e);
                        }
                        else
                        {
                            Console.WriteLine($"  {e.Name} tries to flee due to their wounds!");
                            bool stopped = PlayerFreeActionOnFleeingEnemy(e);
                            if (!stopped)
                            {
                                e.HasFledBefore = true;
                                e.Alive = false;
                                int totalHeal = Rng.Next(1, 5) + Rng.Next(1, 5) + Rng.Next(1, 5) + Rng.Next(1, 5);
                                var returnedOgre = new Ogre(Rng, e.Name);
                                returnedOgre.HP = Math.Min(e.HP + totalHeal, returnedOgre.MaxHP);
                                Console.WriteLine($"  {e.Name} escapes! Returns in 4 turns.");
                                var batch = new List<Enemy> { returnedOgre };
                                int reinfRoll = Rng.Next(1, 9);
                                switch (reinfRoll)
                                {
                                    case 1: batch.Add(new Ogre(Rng, "Ogre Backup")); Console.WriteLine("  ...with another Ogre!"); break;
                                    case 2: batch.Add(new Troll(Rng, "Troll A")); batch.Add(new Troll(Rng, "Troll B")); Console.WriteLine("  ...with two Trolls!"); break;
                                    case 3: for (int j = 0; j < 3; j++) batch.Add(new Orc(Rng, $"Orc {j+1}")); Console.WriteLine("  ...with three Orcs!"); break;
                                    case 4: for (int j = 0; j < 4; j++) batch.Add(new Hobgoblin(Rng, $"Hobgoblin {j+1}")); Console.WriteLine("  ...with four Hobgoblins!"); break;
                                    case 5: for (int j = 0; j < 6; j++) batch.Add(new Goblin(Rng, $"Goblin {j+1}")); Console.WriteLine("  ...with six Goblins!"); break;
                                    case 6: for (int j = 0; j < 3; j++) batch.Add(new Troll(Rng, $"Troll {j+1}")); Console.WriteLine("  ...with three Trolls!"); break;
                                    case 7: for (int j = 0; j < 4; j++) batch.Add(new Orc(Rng, $"Orc {j+1}")); Console.WriteLine("  ...with four Orcs!"); break;
                                    default: for (int j = 0; j < 5; j++) batch.Add(new Hobgoblin(Rng, $"Hobgoblin {j+1}")); Console.WriteLine("  ...with five Hobgoblins!"); break;
                                }
                                Pending.Add((batch, 4));
                            }
                            break;
                        }
                    }
                    continue;
                }

                // 10%-24% HP: power attack with club (action 1), grapple with off-hand (action 2)
                if (ogrePct <= 24)
                {
                    e.PowerAttackMode = true;
                    if (actions > 0 && P.HP > 0)
                    {
                        Console.WriteLine($"  {e.Name} winds up for a massive power attack!");
                        EnemyAttack(e);
                        actions--;
                    }
                    if (actions > 0 && P.HP > 0)
                    {
                        Console.WriteLine($"  {e.Name} grabs with their free hand!");
                        OgreGrappleAction(e, bothHands: false);
                    }
                    continue;
                }

                // 25%-49% HP: power attack double tap (can't be blocked or parried)
                if (ogrePct <= 49)
                {
                    e.PowerAttackMode = true;
                    if (e.GrappleNextTurn && actions > 0)
                    {
                        e.GrappleNextTurn = false;
                        OgreGrappleAction(e, bothHands: false);
                        actions--;
                    }
                    for (int i = 0; i < actions && P.HP > 0; i++)
                    {
                        if (P.IsGrappled && P.GrappledBy == e)
                            OgreMaintainGrapple(e);
                        else
                            EnemyAttack(e);
                    }
                    continue;
                }

                // ≥ 50% HP: normal double tap; grapple if triggered
                e.PowerAttackMode = false;
                if (e.GrappleNextTurn && actions > 0)
                {
                    e.GrappleNextTurn = false;
                    OgreGrappleAction(e, bothHands: false);
                    actions--;
                }
                for (int i = 0; i < actions && P.HP > 0; i++)
                {
                    if (P.IsGrappled && P.GrappledBy == e)
                        OgreMaintainGrapple(e);
                    else
                        EnemyAttack(e);
                }
                continue;
            }

            // Normal goblin: attack with all remaining actions
            for (int i = 0; i < actions && P.HP > 0; i++)
                EnemyAttack(e);
        }
    }

    void OrcGrappleAction(Enemy e)
    {
        if (P.IsGrappled && P.GrappledBy == e)
        {
            OrcMaintainGrapple(e);
        }
        else
        {
            int gAtk = Rng.Next(e.MinGrapple, e.MaxGrapple + 1) - e.AttackPenalty;
            int pDdg = Rng.Next(P.MinDodge, P.MaxDodge + 1);
            Console.WriteLine($"  {e.Name} goes for a grapple! {gAtk} vs your dodge {pDdg}.");
            if (gAtk >= pDdg)
            {
                P.IsGrappled = true; P.GrappledBy = e;
                Console.WriteLine($"  {e.Name} grabs you!");
            }
            else Console.WriteLine($"  Grapple missed!");
        }
    }

    void OrcMaintainGrapple(Enemy e)
    {
        int gDmg = Rng.Next(e.GrappleDmgMin, e.GrappleDmgMax + 1);
        P.HP -= gDmg;
        Console.WriteLine($"  {e.Name} crushes you for {gDmg} damage! HP:{P.HP}/{P.MaxHP}");
        // Player rolls to break free each grapple action
        int minG = P.MinGrapple + P.GetFeatStacks("Closeliner");
        int pGr = Rng.Next(minG, P.MaxGrapple + 1);
        int eGr = Rng.Next(e.MinGrapple, e.MaxGrapple + 1);
        Console.WriteLine($"  Break-free roll: your {pGr} vs {e.Name}'s {eGr}.");
        if (pGr >= eGr) { P.IsGrappled = false; P.GrappledBy = null; Console.WriteLine("  You break free!"); }
        else Console.WriteLine("  Still held!");
    }

    void OgreGrappleAction(Enemy e, bool bothHands)
    {
        if (P.IsGrappled && P.GrappledBy == e)
        {
            OgreMaintainGrapple(e);
        }
        else
        {
            int gAtk = Rng.Next(e.MinGrapple, e.MaxGrapple + 1) - e.AttackPenalty;
            int pDdg = Rng.Next(P.MinDodge, P.MaxDodge + 1);
            Console.WriteLine($"  {e.Name} lunges to grapple{(bothHands ? " with both hands" : "")}! {gAtk} vs your dodge {pDdg}.");
            Console.WriteLine($"  (You can only counter-grapple 8-12 or dodge to escape an Ogre's grab!)");
            if (gAtk >= pDdg)
            {
                P.IsGrappled = true; P.GrappledBy = e;
                Console.WriteLine($"  {e.Name} seizes you{(bothHands ? " with crushing force" : "")}!");
            }
            else Console.WriteLine($"  Ogre's grapple missed!");
        }
    }

    void OgreMaintainGrapple(Enemy e)
    {
        int gDmg = e.DroppedWeapon
            ? Rng.Next(e.GrappleDmgMin * 2, e.GrappleDmgMax * 2 + 1)   // both hands = double damage
            : Rng.Next(e.GrappleDmgMin, e.GrappleDmgMax + 1);
        P.HP -= gDmg;
        Console.WriteLine($"  {e.Name} crushes you for {gDmg} damage! HP:{P.HP}/{P.MaxHP}");
        // Player must use counter-grapple 8-12 to break free
        int pGr = Rng.Next(8, 13);
        int eGr = Rng.Next(e.MinGrapple, e.MaxGrapple + 1);
        Console.WriteLine($"  Counter-grapple (8-12): your {pGr} vs {e.Name}'s {eGr}.");
        if (pGr >= eGr) { P.IsGrappled = false; P.GrappledBy = null; Console.WriteLine("  You break the ogre's grip!"); }
        else Console.WriteLine("  Still held!");
    }

    void EnemyKick(Enemy e)
    {
        if (!e.HasKick || !e.Alive || P.HP <= 0) return;
        int kAtk = Rng.Next(1, 7);
        int pDdg = Rng.Next(P.MinDodge, P.MaxDodge + 1);
        Console.WriteLine($"  {e.Name} kicks! Roll {kAtk} vs your dodge {pDdg}.");
        if (kAtk >= pDdg)
        {
            int kDmg = Rng.Next(e.KickDmgMin, e.KickDmgMax + 1);
            if (P.Defending) kDmg = Math.Max(1, kDmg / 2);
            if (P.ArmorDamageReduction > 0) kDmg = Math.Max(1, kDmg - P.ArmorDamageReduction);
            P.HP -= kDmg;
            Console.WriteLine($"  Kick HIT! {kDmg} damage. HP:{P.HP}/{P.MaxHP}");
        }
        else Console.WriteLine("  Kick missed!");
    }

    // Player gets one free action when an enemy tries to flee.
    // Returns true if the enemy was stopped (flee fails), false if they escape.
    bool PlayerFreeActionOnFleeingEnemy(Enemy e)
    {
        Console.Write($"  Free response! [A]ttack or [G]rapple {e.Name}? ");
        string choice = (Console.ReadLine() ?? "a").Trim().ToLower();

        if (choice.StartsWith("g"))
        {
            bool vsOgre = e is Ogre;
            int minG = vsOgre ? 8 : P.MinGrapple + P.GetFeatStacks("Closeliner");
            int maxG = vsOgre ? 12 : P.MaxGrapple;
            int gRoll = Rng.Next(minG, maxG + 1);
            int gDdg = Rng.Next(e.MinDodge, e.MaxDodge + 1) - 2;
            Console.WriteLine($"  Grapple{(vsOgre ? " (counter 8-12)" : "")}: {gRoll} vs {e.Name}'s dodge-2 ({gDdg}).");
            if (gRoll >= gDdg)
            {
                Console.WriteLine($"  You grab {e.Name} — flee stopped!");
                e.Grappled = true;
                return true;
            }
            Console.WriteLine($"  Grapple missed — {e.Name} slips away.");
            return false;
        }
        else
        {
            int aRoll = Rng.Next(P.MinAttack, P.MaxAttack + 1);
            int aDdg = Rng.Next(e.MinDodge, e.MaxDodge + 1) - 2;
            Console.WriteLine($"  Attack: {aRoll} vs {e.Name}'s dodge-2 ({aDdg}).");
            if (aRoll >= aDdg)
            {
                // Ogre arm block on flee: if arm block succeeds, ogre escapes anyway (too big)
                if (e is Ogre && e.HasArmBlock && !e.KnockedDown && !e.KnockedOut && !e.OffBalance)
                {
                    int bRoll = Rng.Next(e.BlockMin, e.BlockMax + 1);
                    Console.WriteLine($"  {e.Name} deflects with their arm! Block roll {bRoll} vs {aRoll}.");
                    if (bRoll >= aRoll)
                    {
                        int glanceDmg = Math.Max(1, Rng.Next(P.MinDamage, P.MaxDamage + 1) / 2);
                        glanceDmg = ReduceByToughHide(e, glanceDmg);
                        e.HP -= glanceDmg;
                        Console.WriteLine($"  ARM BLOCK! Glancing blow {glanceDmg} dmg — too massive to stop! {e.Name} HP:{e.HP}/{e.MaxHP}");
                        return false;
                    }
                    Console.WriteLine($"  Arm block failed!");
                }
                if (!EnemyBlocks(e, aRoll))
                {
                    int dmg = Rng.Next(P.MinDamage, P.MaxDamage + 1);
                    dmg = ReduceByToughHide(e, dmg);
                    e.HP -= dmg;
                    Console.WriteLine($"  HIT! {dmg} dmg — flee stopped! {e.Name} HP:{e.HP}/{e.MaxHP}");
                    if (!e.Alive) HandleKill(e);
                    return true;
                }
            }
            if (aRoll < aDdg) Console.WriteLine($"  Missed — {e.Name} gets away.");
            else Console.WriteLine($"  Blocked — {e.Name} gets away.");
            return false;
        }
    }

    void EnemyAttack(Enemy e)
    {
        if (!e.Alive || e.KnockedOut) return;
        int eAtk = Rng.Next(e.MinAttack, e.MaxAttack + 1) - e.AttackPenalty - e.FrostPenalty;
        if (e.PowerAttackMode) eAtk = Math.Max(1, eAtk - 2); // power attack penalty
        int pDdg = Rng.Next(P.MinDodge, P.MaxDodge + 1);
        Console.WriteLine($"  {e.Name} attacks{(e.PowerAttackMode ? " (POWER)" : "")}! Roll {eAtk} vs your dodge {pDdg}.");
        if (eAtk >= pDdg)
        {
            int dmg = Rng.Next(e.MinDamage, e.MaxDamage + 1);
            if (e.PowerAttackMode) dmg += 4; // power attack bonus
            if (e.Disarmed) dmg = Math.Max(1, dmg / 2);
            if (P.Defending) dmg = Math.Max(1, dmg / 2);
            if (P.ArmorDamageReduction > 0) dmg = Math.Max(1, dmg - P.ArmorDamageReduction);
            Console.WriteLine($"  HIT! You take {dmg} damage. HP: {P.HP - dmg}/{P.MaxHP}");
            P.HP -= dmg;
            // Double Tap off-hand
            if (e.HasDoubleTap && P.HP > 0 && !e.DroppedWeapon)
            {
                int ofAtk = Rng.Next(e.OffhandMinAtk, e.OffhandMaxAtk + 1) - e.AttackPenalty;
                int ofDdg = Rng.Next(P.MinDodge, P.MaxDodge + 1);
                Console.WriteLine($"  {e.Name} off-hand! Roll {ofAtk} vs your dodge {ofDdg}.");
                if (ofAtk >= ofDdg)
                {
                    int ofDmg = Rng.Next(e.OffhandMinDmg, e.OffhandMaxDmg + 1);
                    if (P.Defending) ofDmg = Math.Max(1, ofDmg / 2);
                    if (P.ArmorDamageReduction > 0) ofDmg = Math.Max(1, ofDmg - P.ArmorDamageReduction);
                    Console.WriteLine($"  Off-hand HIT! {ofDmg} damage. HP:{P.HP - ofDmg}/{P.MaxHP}");
                    P.HP -= ofDmg;
                }
                else Console.WriteLine("  Off-hand miss!");
            }
        }
        else
        {
            Console.WriteLine("  MISS!");
            FreeAttackPrompt("Counter Strike", e);
            if (P.HasFeat("Judo")) JudoPrompt(e);
            if (P.HasFeat("Kehon"))
            {
                Console.Write($"  Kehon! Instant grapple on {e.Name}? (y/n): ");
                if ((Console.ReadLine() ?? "").Trim().ToLower() == "y") DoGrapple(e);
            }
        }
    }
}
