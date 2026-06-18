using Raylib_cs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

// ── Shared state pushed by game thread, read by render thread ─────────────

class RenderSnapshot
{
    public GridPos PlayerPos;
    public int PlayerHP, PlayerMaxHP, PlayerLevel, WaveNum;
    public List<(GridPos Pos, string TypeName, int HP, int MaxHP)> Enemies = new();
    public List<GridPos> GroundWeapons = new();
}

class SharedGameState
{
    private readonly object _lock = new();
    private RenderSnapshot _snap = new();
    public volatile bool GameOver = false;

    public void Push(RenderSnapshot snap)  { lock (_lock) _snap = snap; }
    public RenderSnapshot Pull()           { lock (_lock) return _snap; }
}

// ── Raylib-based display window (runs on main thread) ─────────────────────

class GraphicsDisplay
{
    // Layout
    const int Cell    = 20;   // pixels per grid square
    const int View    = 25;   // squares visible in each direction from player
    const int ViewPx  = (View * 2 + 1) * Cell;
    const int Panel   = 260;
    const int WinW    = ViewPx + Panel;
    const int WinH    = ViewPx;

    readonly SharedGameState _state;
    readonly Dictionary<string, Texture2D> _tex = new();

    public GraphicsDisplay(SharedGameState state) => _state = state;

    public void Run()
    {
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
        Raylib.InitWindow(WinW, WinH, "One Who Stands Against The Horde");
        Raylib.SetTargetFPS(30);

        LoadTextures();

        while (!Raylib.WindowShouldClose() && !_state.GameOver)
        {
            var snap = _state.Pull();
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            DrawMap(snap);
            DrawPanel(snap);
            Raylib.EndDrawing();
        }

        foreach (var t in _tex.Values) Raylib.UnloadTexture(t);
        Raylib.CloseWindow();
    }

    // ── Asset loading ──────────────────────────────────────────────────────

    void LoadTextures()
    {
        var names = new[] { "Ogre", "OrcBarbarian", "Troll", "Orc",
                            "Hobgoblin", "Goblin", "SpellGoblin", "Player" };
        foreach (var n in names)
        {
            string path = Path.Combine("assets", n.ToLower() + ".png");
            if (File.Exists(path))
                _tex[n] = Raylib.LoadTexture(path);
        }
    }

    // ── Map rendering ──────────────────────────────────────────────────────

    void DrawMap(RenderSnapshot snap)
    {
        int ox = snap.PlayerPos.X - View;   // grid origin offset
        int oy = snap.PlayerPos.Y - View;

        // Grid lines (subtle)
        for (int r = 0; r <= View * 2 + 1; r++)
        {
            int py = r * Cell;
            Raylib.DrawLine(0, py, ViewPx, py, new Color(30, 30, 30, 255));
        }
        for (int c = 0; c <= View * 2 + 1; c++)
        {
            int px = c * Cell;
            Raylib.DrawLine(px, 0, px, ViewPx, new Color(30, 30, 30, 255));
        }

        // Ground weapons (small yellow dot)
        foreach (var wp in snap.GroundWeapons)
        {
            int sx = (wp.X - ox) * Cell;
            int sy = (wp.Y - oy) * Cell;
            if (sx < 0 || sy < 0 || sx >= ViewPx || sy >= ViewPx) continue;
            Raylib.DrawRectangle(sx + 6, sy + 6, Cell - 12, Cell - 12, Color.Gold);
        }

        // Enemies
        foreach (var (pos, type, hp, maxHp) in snap.Enemies)
        {
            int sx = (pos.X - ox) * Cell;
            int sy = (pos.Y - oy) * Cell;
            if (sx < 0 || sy < 0 || sx >= ViewPx || sy >= ViewPx) continue;
            DrawEntity(sx, sy, type, hp, maxHp);
        }

        // Player
        {
            int sx = View * Cell;
            int sy = View * Cell;
            if (_tex.TryGetValue("Player", out var ptex))
            {
                var dst = new Rectangle(sx, sy, Cell, Cell);
                Raylib.DrawTexturePro(ptex,
                    new Rectangle(0, 0, ptex.Width, ptex.Height),
                    dst, Vector2.Zero, 0, Color.White);
            }
            else
            {
                Raylib.DrawRectangle(sx + 1, sy + 1, Cell - 2, Cell - 2, new Color(0, 255, 255, 255));
                Raylib.DrawText("@", sx + 4, sy + 3, 12, Color.Black);
            }
        }
    }

    void DrawEntity(int sx, int sy, string type, int hp, int maxHp)
    {
        if (_tex.TryGetValue(type, out var tex))
        {
            var dst = new Rectangle(sx, sy, Cell, Cell);
            Raylib.DrawTexturePro(tex,
                new Rectangle(0, 0, tex.Width, tex.Height),
                dst, Vector2.Zero, 0, Color.White);
        }
        else
        {
            var col = EnemyColor(type);
            Raylib.DrawRectangle(sx + 1, sy + 1, Cell - 2, Cell - 2, col);
            string lbl = type switch
            {
                "SpellGoblin"  => "SG",
                "Goblin"       => "G",
                "Hobgoblin"    => "H",
                "Orc"          => "O",
                "OrcBarbarian" => "OB",
                "Troll"        => "T",
                "Ogre"         => "Og",
                _              => "?"
            };
            Raylib.DrawText(lbl, sx + 2, sy + 4, 9, Color.Black);
        }

        // HP bar (2px strip at bottom of cell)
        if (maxHp > 0)
        {
            int barW = (int)((Cell - 2) * hp / (float)maxHp);
            Raylib.DrawRectangle(sx + 1, sy + Cell - 3, Cell - 2, 2, Color.DarkGray);
            Raylib.DrawRectangle(sx + 1, sy + Cell - 3, barW,     2, new Color(50, 205, 50, 255));
        }
    }

    // ── Status panel ───────────────────────────────────────────────────────

    void DrawPanel(RenderSnapshot snap)
    {
        int px = ViewPx + 10;
        Raylib.DrawRectangle(ViewPx, 0, Panel, WinH, new Color(18, 18, 24, 255));
        Raylib.DrawLine(ViewPx, 0, ViewPx, WinH, new Color(60, 60, 80, 255));

        Raylib.DrawText("OWSATH", px, 12, 20, Color.White);
        Raylib.DrawText($"Wave {snap.WaveNum}", px, 40, 15, Color.LightGray);
        Raylib.DrawText($"Level {snap.PlayerLevel}", px, 60, 15, Color.LightGray);

        // HP label + bar
        bool crit = snap.PlayerHP <= snap.PlayerMaxHP / 4;
        var hpCol = crit ? Color.Red : new Color(50, 205, 50, 255);
        Raylib.DrawText($"HP  {snap.PlayerHP} / {snap.PlayerMaxHP}", px, 88, 14, hpCol);
        int barW = snap.PlayerMaxHP > 0
            ? (int)((Panel - 20) * snap.PlayerHP / (float)snap.PlayerMaxHP) : 0;
        Raylib.DrawRectangle(px,       108, Panel - 20, 10, new Color(50, 50, 50, 255));
        Raylib.DrawRectangle(px,       108, barW,       10, hpCol);

        // Enemy count
        Raylib.DrawText($"Enemies: {snap.Enemies.Count}", px, 130, 13, Color.Orange);

        // Legend
        int ly = WinH - 160;
        Raylib.DrawText("Legend", px, ly, 13, Color.Gray);
        DrawLegend(px, ly + 18, "G",  "Goblin",       new Color(144, 238, 144, 255));
        DrawLegend(px, ly + 34, "SG", "Spell Goblin", new Color(180, 80, 220, 255));
        DrawLegend(px, ly + 50, "H",  "Hobgoblin",    Color.Green);
        DrawLegend(px, ly + 66, "O",  "Orc",          new Color(200, 100, 50, 255));
        DrawLegend(px, ly + 82, "OB", "Orc Barb",     new Color(220, 60, 20, 255));
        DrawLegend(px, ly + 98, "T",  "Troll",        new Color(80, 130, 80, 255));
        DrawLegend(px, ly +114, "Og", "Ogre",         new Color(180, 80, 80, 255));
    }

    void DrawLegend(int x, int y, string abbr, string label, Color col)
    {
        Raylib.DrawRectangle(x, y, 18, 14, col);
        Raylib.DrawText(abbr,   x + 2, y + 1,  9, Color.Black);
        Raylib.DrawText(label,  x + 22, y + 1, 11, Color.LightGray);
    }

    // ── Colour table for entities without sprite ────────────────────────────

    static Color EnemyColor(string type) => type switch
    {
        "Goblin"       => new Color(144, 238, 144, 255),
        "SpellGoblin"  => new Color(180, 80, 220, 255),
        "Hobgoblin"    => Color.Green,
        "Orc"          => new Color(200, 100, 50, 255),
        "OrcBarbarian" => new Color(220, 60, 20, 255),
        "Troll"        => new Color(80, 130, 80, 255),
        "Ogre"         => new Color(180, 80, 80, 255),
        _              => Color.Gray
    };
}
