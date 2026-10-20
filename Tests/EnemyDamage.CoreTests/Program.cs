using System;
using TapTapGameJam.Combat;

static class Program
{
    static int checks;
    static void Equal(int actual, int expected, string message)
    {
        if (actual != expected) throw new Exception(message + $": expected {expected}, got {actual}");
        checks++;
    }
    static void Main()
    {
        var rules = new EnemyDamageRules();
        for (int enemy = 1; enemy <= 3; enemy++)
            for (int instrument = 0; instrument < 3; instrument++)
                Equal(rules.Resolve((EnemyResistance)enemy, (TowerInstrument)instrument, 1, 0, .12),
                    instrument == enemy - 1 ? 1 : 0, "ordinary enemy resistance matrix");
        foreach (TowerInstrument first in Enum.GetValues<TowerInstrument>())
            foreach (TowerInstrument second in Enum.GetValues<TowerInstrument>())
            {
                rules.Reset();
                Equal(rules.Resolve(EnemyResistance.Boss, first, 1, 0, .12), 0, "first hit waits");
                Equal(rules.Resolve(EnemyResistance.Boss, second, 1, .1, .12), first == second ? 0 : 2,
                    "different types required regardless of arrival order");
            }
        rules.Reset();
        Equal(rules.Resolve(EnemyResistance.Boss, TowerInstrument.Duduk, 1, 0, .12), 0, "solo immune");
        Equal(rules.Resolve(EnemyResistance.Boss, TowerInstrument.Chord, 1, .121, .12), 0, "old hit expires");
        Equal(rules.Resolve(EnemyResistance.Boss, TowerInstrument.Bell, 1, .13, .12), 2, "only recent pair counted");
        Equal(rules.Resolve(EnemyResistance.Boss, TowerInstrument.Duduk, 1, .14, .12), 1, "third type does not replay damage");
        Equal(rules.Resolve(EnemyResistance.Boss, TowerInstrument.Duduk, 1, 1, .12), 0, "no lasting vulnerability");
        rules.Reset();
        Equal(rules.Resolve(EnemyResistance.Boss, TowerInstrument.Duduk, 1, 0, .12), 0, "reset clears history");
        Equal(rules.Resolve(EnemyResistance.Boss, TowerInstrument.Bell, 1, .12, .12), 2, "inclusive window boundary");
        rules.Reset();
        Equal(rules.Resolve(EnemyResistance.Boss, TowerInstrument.Duduk, 0, 0, .12), 0, "zero damage cannot prime combo");
        Equal(rules.Resolve(EnemyResistance.Boss, TowerInstrument.Bell, 1, .01, .12), 0, "invalid hit not stored");
        Equal(rules.Resolve(EnemyResistance.Any, TowerInstrument.Chord, -1, 1, .12), 0, "negative damage rejected");
        Equal(rules.Resolve(EnemyResistance.Any, TowerInstrument.Chord, 3, 1, .12), 3, "base enemy remains unrestricted");
        var health = new BaseHealthState();
        Equal(health.Current, 5, "base starts with five lives");
        for (int remaining = 4; remaining >= 0; remaining--)
        {
            Equal(health.LoseLife() ? 1 : 0, 1, "each arrival consumes a life");
            Equal(health.Current, remaining, "one life lost per arrival");
            Equal(health.IsDefeated ? 1 : 0, remaining == 0 ? 1 : 0, "defeat only on fifth arrival");
        }
        Equal(health.LoseLife() ? 1 : 0, 0, "further arrivals cannot trigger defeat again");
        Equal(health.Current, 0, "base health never goes negative");
        health.Reset();
        Equal(health.Current, 5, "retry restores five lives");
        Equal(health.IsDefeated ? 1 : 0, 0, "retry clears defeat");
        health.LoseLife();
        Equal(health.Current, 4, "damage works again after retry");
        for (int hp = 0; hp <= 5; hp++)
            for (int spawned = 0; spawned <= 10; spawned++)
                for (int living = 0; living <= spawned; living++)
                    Equal(WaveCompletionRules.HasWon(hp, 10, spawned, living) ? 1 : 0,
                        hp > 0 && spawned == 10 && living == 0 ? 1 : 0,
                        "victory requires the full wave resolved and surviving base health");
        Equal(WaveCompletionRules.HasWon(5, 0, 0, 0) ? 1 : 0, 0, "unlimited or unconfigured wave cannot win");
        Equal(WaveCompletionRules.HasWon(1, 10, 10, 0) ? 1 : 0, 1, "mixed kills and four arrivals can win at one life");
        Equal(WaveCompletionRules.HasWon(0, 10, 10, 0) ? 1 : 0, 0, "final arrival killing the base always loses");
        Console.WriteLine($"COMBAT_HEALTH_RULES_PASS: {checks} assertions");
    }
}
